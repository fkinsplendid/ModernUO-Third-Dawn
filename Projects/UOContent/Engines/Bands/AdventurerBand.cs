using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Engines.Companions;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Bands;

/// <summary>
/// Marks where an adventuring band works: placed by staff on a dungeon level, invisible to players.
/// It musters a captain with a warrior, a bard and a mage, gives the captain a patrol through the
/// level's spawners, patches up band ghosts nobody raised, and when the captain falls, sends the
/// rest home and musters a fresh band later.
/// </summary>
[SerializationGenerator(0)]
public partial class AdventurerBand : Item
{
    private const int DefaultLevel = 10;
    private const int DefaultPatrolRange = 30;
    private const int MinLegLength = 5;
    private const int MaxLegLength = 24;
    private const int PlayerWatchRange = 18;
    private static readonly long WaypointRefreshMs = (long)TimeSpan.FromMinutes(10).TotalMilliseconds;

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private BandCaptain _captain;

    // Companion level of every member; the captain is built to match.
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _level;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _patrolRange;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextMuster;

    // Patrol points: the spawners on this level, where the monsters are. Found afresh after a restart.
    private readonly List<Point3D> _waypoints = [];
    private long _waypointsFoundAt;

    // When each band ghost died, for those nobody has raised.
    private readonly Dictionary<BaseCompanion, long> _ghostsSince = [];

    [Constructible]
    public AdventurerBand() : base(0x1F1C)
    {
        Visible = false;
        Movable = false;
        _level = DefaultLevel;
        _patrolRange = DefaultPatrolRange;
        BandSystem.Register(this);
    }

    public override string DefaultName => "an adventuring band";

    public CompanionParty Members => CompanionSystem.GetParty(_captain);

    [AfterDeserialization]
    private void AfterDeserialization() => BandSystem.Register(this);

    // ---------- Upkeep ----------

    internal void OnTick()
    {
        if (Deleted || Parent != null || Map == null || Map == Map.Internal)
        {
            return;
        }

        if (_captain?.Deleted != false)
        {
            Captain = null;

            if (Core.Now >= _nextMuster && !IsWatched())
            {
                Muster();
            }

            return;
        }

        TendMembers();
    }

    /// <summary>Bands arrive out of sight, not in front of a player.</summary>
    private bool IsWatched()
    {
        foreach (var _ in Map.GetMobilesInRange<PlayerMobile>(Location, PlayerWatchRange))
        {
            return true;
        }

        return false;
    }

    /// <summary>Raises a fresh band at the marker.</summary>
    public void Muster()
    {
        Disband();

        var captain = new BandCaptain(this, _level);
        captain.MoveToWorld(Location, Map);
        Captain = captain;

        Recruit(captain, new CompanionWarrior());
        Recruit(captain, new CompanionBard());
        Recruit(captain, new CompanionMage());
    }

    private void Recruit(BandCaptain captain, BaseCompanion member)
    {
        member.Level = _level;

        if (!member.JoinBand(captain))
        {
            member.Delete();
        }
    }

    /// <summary>
    /// Keeps the band supplied, fight or no fight, so it never runs out of reagents, bandages or
    /// potions. A ghost the band's own healers couldn't raise is patched up once the fighting is over.
    /// </summary>
    private void TendMembers()
    {
        var party = Members;

        if (party == null)
        {
            return;
        }

        var now = Core.TickCount;
        var fighting = _captain.Combatant != null;
        var patience = (long)BandSystem.GhostPatience.TotalMilliseconds;

        foreach (var member in party.Members)
        {
            if (member == null)
            {
                continue;
            }

            if (!member.IsDeadPet)
            {
                _ghostsSince.Remove(member);
                member.RestockForBand();
                continue;
            }

            if (!_ghostsSince.TryGetValue(member, out var since))
            {
                _ghostsSince[member] = now;
            }
            else if (!fighting && now - since >= patience)
            {
                _ghostsSince.Remove(member);
                member.PlaySound(0x214);
                member.FixedEffect(0x376A, 10, 16);
                member.ResurrectPet();
            }
        }
    }

    /// <summary>The captain is dead: the rest of the band makes for home, and a new one comes in time.</summary>
    internal void OnCaptainFell(BandCaptain captain)
    {
        if (captain != _captain)
        {
            return;
        }

        Captain = null;
        NextMuster = Core.Now + BandSystem.RespawnDelay;

        if (CompanionSystem.GetParty(captain) is { } party)
        {
            using var queue = PooledRefQueue<BaseCompanion>.Create();

            foreach (var member in party.Members)
            {
                if (member != null)
                {
                    queue.Enqueue(member);
                }
            }

            while (queue.Count > 0)
            {
                var member = queue.Dequeue();

                if (!member.IsDeadPet)
                {
                    member.Say(Utility.RandomBool() ? "The captain's down! Get out, now!" : "We've lost the captain! Run!");
                }

                Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 4)), () => MakeForHome(member));
            }
        }
    }

    private static void MakeForHome(BaseCompanion member)
    {
        if (member.Deleted)
        {
            return;
        }

        if (member.Map != null && member.Map != Map.Internal)
        {
            Effects.SendLocationParticles(
                EffectItem.Create(member.Location, member.Map, EffectItem.DefaultDuration),
                0x3728,
                10,
                10,
                2023
            );
            member.PlaySound(0x1FC);
        }

        member.Delete();
    }

    /// <summary>Sends the current band away for good. The marker stays and musters again later.</summary>
    public void Disband()
    {
        var captain = _captain;
        Captain = null;
        _ghostsSince.Clear();

        if (captain == null)
        {
            return;
        }

        if (CompanionSystem.GetParty(captain) is { } party)
        {
            using var queue = PooledRefQueue<BaseCompanion>.Create();

            foreach (var member in party.Members)
            {
                if (member != null)
                {
                    queue.Enqueue(member);
                }
            }

            while (queue.Count > 0)
            {
                queue.Dequeue().Delete();
            }
        }

        captain.Delete();
    }

    // ---------- Patrol ----------

    /// <summary>
    /// The next stop on the patrol: a spawner on this level a fair walk from <paramref name="from"/>,
    /// not the one just visited. The marker itself when the level has none.
    /// </summary>
    public Point3D NextWaypoint(Point3D from, Point3D last)
    {
        var now = Core.TickCount;

        if (_waypoints.Count == 0 || now - _waypointsFoundAt >= WaypointRefreshMs)
        {
            FindWaypoints();
            _waypointsFoundAt = now;
        }

        using var choices = PooledRefList<Point3D>.Create();

        foreach (var point in _waypoints)
        {
            if (point != last && Utility.InRange(from, point, MaxLegLength) && !Utility.InRange(from, point, MinLegLength - 1))
            {
                choices.Add(point);
            }
        }

        if (choices.Count > 0)
        {
            return choices[Utility.Random(choices.Count)];
        }

        // Nothing at a good distance: any spawner but the last, else home.
        foreach (var point in _waypoints)
        {
            if (point != last)
            {
                choices.Add(point);
            }
        }

        return choices.Count > 0 ? choices[Utility.Random(choices.Count)] : Location;
    }

    private void FindWaypoints()
    {
        _waypoints.Clear();

        foreach (var spawner in Map.GetItemsInRange<BaseSpawner>(Location, _patrolRange))
        {
            if (spawner.Parent == null)
            {
                _waypoints.Add(spawner.Location);
            }
        }
    }

    // ---------- Lifetime ----------

    public override void OnDelete()
    {
        Disband();
        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        BandSystem.Unregister(this);
        _captain = null;
        _ghostsSince.Clear();
        base.OnAfterDelete();
    }
}
