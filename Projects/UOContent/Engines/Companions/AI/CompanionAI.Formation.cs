using System;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Walking with the employer. Each role keeps its own place relative to the way the employer is
/// travelling (warrior at the right flank, bard behind on the right, mage behind on the left) and
/// everyone keeps a tile of personal space, but never strays out of arm's reach of the employer, so
/// items can always be handed across. When the employer stops or
/// turns on the spot, a companion already standing somewhere reasonable stays put rather than
/// shuffling about. In cramped places they take the nearest free tile the employer can see.
/// </summary>
public abstract partial class CompanionAI
{
    private const int FormationRange = 10;
    private const int ChaseRange = 24;
    private const int SpotSearchRadius = 2;
    private const int SpotRecheckMs = 1000;

    // One clear tile between people, and within the two tiles a player can hand an item across.
    private const int PersonalSpace = 2;
    private const int ArmsReach = 2;

    // Place in the formation as (forward, right), in steps of personal space. Indexed by role.
    private static readonly (int Forward, int Right)[] _formation = [(0, 1), (-1, 1), (-1, -1)];

    private Point3D _spot;
    private bool _hasSpot;
    private Point3D _spotAnchor;
    private long _nextSpotCheck;
    private Direction _heading;
    private Point3D _lastEmployerLocation;
    private PathFollower _spotPath;

    /// <summary>
    /// Following the employer means keeping formation. Fallen behind, they make their way back
    /// rather than stopping where they are as an ordinary pet would past its perception range.
    /// </summary>
    public override bool DoOrderFollow()
    {
        var employer = Employer;

        if (Party == null || Mobile.ControlTarget != employer || employer?.Map != Mobile.Map ||
            !Mobile.InRange(employer, ChaseRange))
        {
            _hasSpot = false;
            return base.DoOrderFollow();
        }

        KeepFormation();
        return true;
    }

    private bool InFormationRange(Mobile employer) =>
        employer?.Deleted == false && employer.Map == Mobile.Map && Mobile.InRange(employer, FormationRange);

    /// <summary>Moves to, or stays at, this companion's place beside the employer.</summary>
    protected void KeepFormation()
    {
        var employer = Employer;

        if (!InFormationRange(employer))
        {
            _hasSpot = false;

            if (employer?.Map == Mobile.Map)
            {
                MoveTo(employer, true, 2);
            }

            return;
        }

        TrackHeading(employer);

        var now = Core.TickCount;

        var employerMoved = employer.Location != _spotAnchor;

        if (!_hasSpot || employerMoved || now - _nextSpotCheck >= 0)
        {
            _hasSpot = ChooseSpot(employer, !employerMoved, out _spot);
            _spotAnchor = employer.Location;
            _nextSpotCheck = now + SpotRecheckMs;
        }

        if (!_hasSpot)
        {
            // Nowhere sensible to stand, as in a crowded corridor: just stay close.
            if (!Mobile.InRange(employer, ArmsReach))
            {
                MoveTo(employer, true, 2);
            }

            return;
        }

        if (Mobile.X == _spot.X && Mobile.Y == _spot.Y)
        {
            // Settled. Now and then look the same way the employer is looking.
            var facing = employer.Direction & Direction.Mask;

            if ((Mobile.Direction & Direction.Mask) != facing && Utility.RandomDouble() < 0.1)
            {
                Mobile.Direction = facing;
            }

            return;
        }

        StepTowards(_spot);
    }

    /// <summary>The way the employer is travelling, which stays put while they stand and turn.</summary>
    private void TrackHeading(Mobile employer)
    {
        var location = employer.Location;

        if (location.X == _lastEmployerLocation.X && location.Y == _lastEmployerLocation.Y)
        {
            return;
        }

        _heading = Distance(location, _lastEmployerLocation) <= 3
            ? Utility.GetDirection(_lastEmployerLocation, location)
            : employer.Direction & Direction.Mask;

        _lastEmployerLocation = location;
    }

    /// <summary>
    /// The free tile nearest this companion's place. Once the employer has stopped, anywhere
    /// reasonable will do, so nobody shuffles about; on the move they keep their place properly.
    /// </summary>
    private bool ChooseSpot(Mobile employer, bool settled, out Point3D spot)
    {
        var ideal = IdealSpot(employer);
        var here = Mobile.Location;

        if (settled && Distance(here, ideal) <= SpotSearchRadius && Distance(here, employer.Location) <= ArmsReach &&
            HasRoom(here, employer))
        {
            spot = here;
            return true;
        }

        spot = default;
        var found = false;
        var bestScore = double.MaxValue;

        for (var dx = -SpotSearchRadius; dx <= SpotSearchRadius; dx++)
        {
            for (var dy = -SpotSearchRadius; dy <= SpotSearchRadius; dy++)
            {
                if (!TryGetStandingSpot(ideal.X + dx, ideal.Y + dy, employer, out var candidate) ||
                    Distance(candidate, employer.Location) > ArmsReach || !HasRoom(candidate, employer))
                {
                    continue;
                }

                var score = Distance(candidate, ideal) + Mobile.GetDistanceToSqrt(candidate) * 0.1;

                if (score < bestScore && employer.InLOS(candidate))
                {
                    spot = candidate;
                    bestScore = score;
                    found = true;
                }
            }
        }

        return found;
    }

    private Point3D IdealSpot(Mobile employer)
    {
        var (x, y) = FormationOffset(Companion.Role, _heading, PersonalSpace);
        return new Point3D(employer.X + x, employer.Y + y, employer.Z);
    }

    /// <summary>Where <paramref name="role"/> stands relative to an employer travelling towards <paramref name="heading"/>.</summary>
    public static (int X, int Y) FormationOffset(CompanionRole role, Direction heading, int space)
    {
        var (forward, right) = _formation[(int)role];
        heading &= Direction.Mask;

        var x = 0;
        var y = 0;
        Movement.Movement.Offset(heading, ref x, ref y, forward * space);
        Movement.Movement.Offset((Direction)(((int)heading + 2) & 7), ref x, ref y, right * space);

        // Diagonal headings add up past the intended distance; pulling each axis back in keeps
        // the direction and the distance.
        return (Math.Clamp(x, -space, space), Math.Clamp(y, -space, space));
    }

    private bool TryGetStandingSpot(int x, int y, Mobile employer, out Point3D spot)
    {
        var map = Mobile.Map;
        var z = employer.Z;

        if (!map.CanFit(x, y, z, 16))
        {
            z = map.GetAverageZ(x, y);

            if (Math.Abs(z - employer.Z) > 8 || !map.CanFit(x, y, z, 16))
            {
                spot = default;
                return false;
            }
        }

        spot = new Point3D(x, y, z);
        return true;
    }

    /// <summary>Clear of the employer, the other companions, and the places they're heading for.</summary>
    private bool HasRoom(Point3D spot, Mobile employer)
    {
        if (Distance(spot, employer.Location) < PersonalSpace)
        {
            return false;
        }

        foreach (var member in Party.Members)
        {
            if (member == Companion || !CompanionParty.IsInWorld(member) || member.Map != Mobile.Map)
            {
                continue;
            }

            if (Distance(spot, member.Location) < PersonalSpace ||
                member.AIObject is CompanionAI { _hasSpot: true } ally && Distance(spot, ally._spot) < PersonalSpace)
            {
                return false;
            }
        }

        return true;
    }

    private static int Distance(Point3D a, Point3D b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void StepTowards(Point3D spot)
    {
        var run = !Mobile.InRange(spot, 2);

        if (Path == null || Path != _spotPath)
        {
            if (DoMove(Mobile.GetDirectionTo(spot, run), true))
            {
                return;
            }

            // Blocked: find a way round.
            _spotPath = new PathFollower(Mobile, spot) { Mover = DoMoveImpl };
            Path = _spotPath;
        }

        if (Path.Goal is not Point3D goal || goal != spot)
        {
            _spotPath = new PathFollower(Mobile, spot) { Mover = DoMoveImpl };
            Path = _spotPath;
        }

        if (Path.Follow(run, 0))
        {
            Path = null;
            _spotPath = null;
        }
    }
}
