using System;
using Server.Engines.Companions;
using Server.Mobiles;

namespace Server.Engines.Bands;

/// <summary>
/// The captain's brain: ordinary melee fighting, plus leading. Out of a fight the captain walks the
/// patrol from spawner to spawner, but waits for stragglers and lets the band rest and heal first.
/// A fight is picked only when the band is ready, and joined whenever anyone in the band is
/// attacked. A foe a player is fighting is given up at once. When the companions read the fight
/// as lost and the band is hurting, the captain calls the retreat and runs, and the band, seeing
/// its employer flee, runs too.
/// </summary>
public sealed class BandCaptainAI : MeleeAI
{
    private const int DwellMinMs = 4000;
    private const int DwellMaxMs = 12000;
    private const int ArrivalRange = 2;
    private const int StragglerRange = 8;
    private const int StuckTicks = 12;
    private const double RestHealth = 0.7;

    // Outmatched, read by a companion this recently, still counts.
    private const int OutmatchedMemoryMs = 3000;
    private const double RetreatHealth = 0.5;
    private const double LastStandHealth = 0.25;
    private const int RetreatMs = 8000;
    private const int RegroupMs = 20000;
    private const int YieldPauseMs = 5000;
    private const int YieldSpeechCooldownMs = 30000;

    private PathFollower _leg;
    private Point3D _legGoal;
    private Point3D _goal;
    private Point3D _lastGoal;
    private bool _hasGoal;
    private long _dwellUntil;
    private Point3D _lastLocation;
    private int _stuck;

    private long _retreatUntil;
    private long _regroupUntil;
    private long _nextYieldSpeech;

    public BandCaptainAI(BandCaptain captain) : base(captain) => Captain = captain;

    public BandCaptain Captain { get; }

    private CompanionParty Party => CompanionSystem.GetParty(Captain);

    private bool Retreating => Core.TickCount - _retreatUntil < 0;

    // ---------- Out of a fight ----------

    public override bool DoActionWander()
    {
        if (TryJoinBandFight() || IsReady() && TryPickFight())
        {
            return true;
        }

        Mobile.Warmode = false;
        Patrol();
        return true;
    }

    public override bool DoActionGuard() => DoActionWander();

    /// <summary>Rested, regrouped and all together: the band can take on something new.</summary>
    private bool IsReady() => Core.TickCount - _regroupUntil >= 0 && !NeedsRest() && !HasStragglers();

    private bool TryPickFight()
    {
        if (!AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
        {
            return false;
        }

        Engage(Mobile.FocusMob);
        return true;
    }

    /// <summary>Anyone in the band attacked, or already fighting: the captain wades in, ready or not.</summary>
    private bool TryJoinBandFight()
    {
        if (Retreating)
        {
            return false;
        }

        var foe = BandFoe(Mobile);

        if (foe == null && Party is { } party)
        {
            foreach (var member in party.Members)
            {
                if (CompanionParty.IsFighting(member) && (foe = BandFoe(member)) != null)
                {
                    break;
                }
            }
        }

        if (foe == null)
        {
            return false;
        }

        Engage(foe);
        return true;
    }

    private Mobile BandFoe(Mobile member)
    {
        var foe = member.Combatant;

        if (IsFairFoe(foe))
        {
            return foe;
        }

        foreach (var info in member.Aggressors)
        {
            if (IsFairFoe(info.Attacker))
            {
                return info.Attacker;
            }
        }

        return null;
    }

    private bool IsFairFoe(Mobile m) =>
        m is { Deleted: false, Alive: true } && m.Map == Mobile.Map && Mobile.InRange(m, Mobile.RangePerception) &&
        Captain.IsEnemy(m) && Mobile.CanBeHarmful(m, false);

    private void Engage(Mobile foe)
    {
        Mobile.Combatant = foe;
        Mobile.FocusMob = foe;
        Action = ActionType.Combat;
        _hasGoal = false;
        _leg = null;
    }

    /// <summary>Anyone in the band, the captain included, below <see cref="RestHealth"/>.</summary>
    private bool NeedsRest()
    {
        if (Mobile.Hits < Mobile.HitsMax * RestHealth)
        {
            return true;
        }

        if (Party is not { } party)
        {
            return false;
        }

        foreach (var member in party.Members)
        {
            if (CompanionParty.IsInWorld(member) && (member.IsDeadPet || member.Hits < member.HitsMax * RestHealth))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasStragglers()
    {
        if (Party is not { } party)
        {
            return false;
        }

        foreach (var member in party.Members)
        {
            if (CompanionParty.IsFighting(member) && (member.Map != Mobile.Map || !Mobile.InRange(member, StragglerRange)))
            {
                return true;
            }
        }

        return false;
    }

    // ---------- Patrol ----------

    private void Patrol()
    {
        if (NeedsRest() || HasStragglers())
        {
            return;
        }

        var band = Captain.Band;

        if (band == null)
        {
            WalkRandomInHome(3, 2, 1);
            return;
        }

        var now = Core.TickCount;

        if (!_hasGoal)
        {
            if (now - _dwellUntil < 0)
            {
                return;
            }

            _goal = band.NextWaypoint(Mobile.Location, _lastGoal);
            _hasGoal = true;
        }

        if (Mobile.InRange(_goal, ArrivalRange))
        {
            Arrive(now);
            return;
        }

        if (_leg == null || _legGoal != _goal)
        {
            _leg = new PathFollower(Mobile, _goal) { Mover = DoMoveImpl };
            _legGoal = _goal;
            _stuck = 0;
        }

        if (_leg.Follow(false, ArrivalRange))
        {
            Arrive(now);
            return;
        }

        // Can't get there (a ledge, a locked door, another level): pick somewhere else.
        if (Mobile.Location == _lastLocation)
        {
            if (++_stuck >= StuckTicks)
            {
                Arrive(now);
            }
        }
        else
        {
            _stuck = 0;
        }

        _lastLocation = Mobile.Location;
    }

    private void Arrive(long now)
    {
        _lastGoal = _goal;
        _hasGoal = false;
        _leg = null;
        _stuck = 0;
        _dwellUntil = now + Utility.RandomMinMax(DwellMinMs, DwellMaxMs);
    }

    // ---------- In a fight ----------

    public override bool DoActionCombat()
    {
        var foe = Mobile.Combatant;

        // Never a bandmate: a band from before bandmates were protected can still carry one in the save.
        if (BandRules.AreBandmates(Mobile, foe))
        {
            Mobile.Combatant = null;
            Mobile.FocusMob = null;
            Mobile.Warmode = false;
            Action = ActionType.Wander;
            return true;
        }

        if (foe != null && BandRules.IsClaimedByPlayer(foe))
        {
            YieldTo(foe);
            return true;
        }

        if (ShouldRetreat())
        {
            CallRetreat();
            return true;
        }

        return base.DoActionCombat();
    }

    /// <summary>A player took this one on: leave it to them.</summary>
    private void YieldTo(Mobile foe)
    {
        var now = Core.TickCount;

        Mobile.Combatant = null;
        Mobile.FocusMob = null;
        Mobile.Warmode = false;
        Action = ActionType.Wander;
        _regroupUntil = Math.Max(_regroupUntil, now + YieldPauseMs);

        if (now - _nextYieldSpeech >= 0)
        {
            _nextYieldSpeech = now + YieldSpeechCooldownMs;
            Mobile.Say(Utility.RandomBool() ? "All yours, friend." : $"Leave the {Name(foe)}, lads. It's spoken for.");
        }
    }

    private static string Name(Mobile m) => CompanionSpeech.BareName(m.Name);

    /// <summary>
    /// The companions read the fight as lost and the band is hurting, or the captain is close to
    /// falling. Captains don't run from fights they're winning.
    /// </summary>
    private bool ShouldRetreat()
    {
        if (Retreating)
        {
            return false;
        }

        var health = Mobile.Hits / (double)Mobile.HitsMax;

        if (health < LastStandHealth)
        {
            return true;
        }

        var party = Party;

        return party != null && party.LastOutmatched != 0 &&
               Core.TickCount - party.LastOutmatched < OutmatchedMemoryMs &&
               (health < RetreatHealth || HasFallen(party));
    }

    private static bool HasFallen(CompanionParty party)
    {
        foreach (var member in party.Members)
        {
            if (CompanionParty.IsInWorld(member) && member.IsDeadPet)
            {
                return true;
            }
        }

        return false;
    }

    private void CallRetreat()
    {
        var now = Core.TickCount;

        _retreatUntil = now + RetreatMs;
        _regroupUntil = now + RegroupMs;
        _hasGoal = false;
        _leg = null;

        Mobile.Say(Utility.RandomBool() ? "Fall back! Fall back!" : "Too many of them! Back, all of you!");

        // One word is enough: the retreat is the whole party's.
        if (Party is { } party)
        {
            foreach (var member in party.Members)
            {
                if (CompanionParty.IsFighting(member) && member.AIObject is CompanionAI ai)
                {
                    ai.OrderRetreat();
                    break;
                }
            }
        }

        Mobile.FocusMob = Mobile.Combatant;
        Action = ActionType.Flee;
    }

    /// <summary>Runs from whatever is nearest until the retreat is over, then regroups.</summary>
    public override bool DoActionFlee()
    {
        if (!Retreating)
        {
            if (_retreatUntil == 0)
            {
                return base.DoActionFlee();
            }

            _retreatUntil = 0;
            Mobile.Combatant = null;
            Mobile.FocusMob = null;
            Mobile.Warmode = false;
            Action = ActionType.Wander;
            return true;
        }

        var foe = Mobile.FocusMob ?? Mobile.Combatant;

        if (foe is { Deleted: false } && foe.Map == Mobile.Map)
        {
            var away = foe.GetDirectionTo(Mobile);

            if (!DoMove(away | Direction.Running, true))
            {
                DoMove((Direction)(((int)away + 1) & 7) | Direction.Running, true);
            }
        }

        return true;
    }
}
