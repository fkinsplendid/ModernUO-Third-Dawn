using System;
using System.Collections.Generic;
using Server.Collections;
using Server.Engines.Companions;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Bands;

/// <summary>
/// Adventuring bands: NPC parties fighting their own battles in the dungeons. Owns the settings
/// and the one timer that musters, maintains and replaces every band.
/// </summary>
public static class BandSystem
{
    /// <summary>Any team but the monsters' own, so monsters and bands see each other as foes.</summary>
    public const int Team = 0x5A;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);
    private static readonly HashSet<AdventurerBand> _bands = [];
    private static TimerExecutionToken _timer;

    /// <summary>
    /// The furthest a band's blows are weakened against foes far weaker than the band, so a pushover
    /// becomes a scrap. Fights between equals are untouched.
    /// </summary>
    public static double MinDamageDealtScale { get; private set; }

    /// <summary>
    /// The furthest monsters' blows are softened against a band facing far stronger foes. Only a
    /// nudge: a hopeless fight is still lost, or run from.
    /// </summary>
    public static double MinDamageTakenScale { get; private set; }

    /// <summary>How long after a band falls before a new one musters in its place.</summary>
    public static TimeSpan RespawnDelay { get; private set; }

    /// <summary>How long a band ghost waits, with no fight on, before the band patches them up itself.</summary>
    public static TimeSpan GhostPatience { get; private set; }

    public static void Configure()
    {
        MinDamageDealtScale = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("bands.minDamageDealtScale", 0.3), 0.05, 1.0);
        MinDamageTakenScale = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("bands.minDamageTakenScale", 0.8), 0.05, 1.0);
        RespawnDelay = ServerConfiguration.GetOrUpdateSetting("bands.respawnDelay", TimeSpan.FromMinutes(30));
        GhostPatience = ServerConfiguration.GetOrUpdateSetting("bands.ghostPatience", TimeSpan.FromMinutes(2));
    }

    public static void Initialize()
    {
        Timer.StartTimer(TickInterval, TickInterval, Tick, out _timer);
    }

    internal static void Register(AdventurerBand band) => _bands.Add(band);

    internal static void Unregister(AdventurerBand band) => _bands.Remove(band);

    /// <summary>Runs outside the bands' own AI, which sleeps when no player is near.</summary>
    private static void Tick()
    {
        using var queue = PooledRefQueue<AdventurerBand>.Create();

        foreach (var band in _bands)
        {
            queue.Enqueue(band);
        }

        while (queue.Count > 0)
        {
            queue.Dequeue().OnTick();
        }
    }
}

/// <summary>Who a band may fight, what it may take, and how hard blows land between bands and monsters.</summary>
public static class BandRules
{
    // A monster a player fought this recently is still theirs.
    private const int ClaimMemoryMs = 20000;

    public static bool IsBandMember(Mobile m) => m is BandCaptain or BaseCompanion { InBand: true };

    /// <summary>
    /// Both in the same band: the captain, their companions, and anything those companions summoned.
    /// Bandmates never hurt each other, and a stray blow between them is never taken as an attack.
    /// </summary>
    public static bool AreBandmates(Mobile a, Mobile b) => a != null && b != null && CaptainOf(a) is { } captain && captain == CaptainOf(b);

    private static BandCaptain CaptainOf(Mobile m) =>
        m switch
        {
            BandCaptain captain                            => captain,
            BaseCompanion { Employer: BandCaptain captain } => captain,
            BaseCreature { Summoned: true, SummonMaster: BaseCompanion { Employer: BandCaptain captain } } => captain,
            _                                              => null
        };

    /// <summary>Players, and anything belonging to one. A band never raises a hand against them.</summary>
    public static bool IsOffLimits(Mobile m) => m.Player || OwnerOf(m) is { Player: true };

    private static Mobile OwnerOf(Mobile m) =>
        m switch
        {
            BaseCreature { Controlled: true } bc => bc.ControlMaster,
            BaseCreature { Summoned: true } bc   => bc.SummonMaster,
            _                                    => null
        };

    /// <summary>A player, or a player's pet, is fighting it or was moments ago. No kill stealing.</summary>
    public static bool IsClaimedByPlayer(Mobile m)
    {
        if (m.Combatant is { } foe && IsOffLimits(foe))
        {
            return true;
        }

        var now = Core.Now;

        foreach (var info in m.Aggressors)
        {
            if (IsOffLimits(info.Attacker) && (now - info.LastCombatTime).TotalMilliseconds < ClaimMemoryMs)
            {
                return true;
            }
        }

        foreach (var info in m.Aggressed)
        {
            if (IsOffLimits(info.Defender) && (now - info.LastCombatTime).TotalMilliseconds < ClaimMemoryMs)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A player has a right to this corpse's loot, so the band leaves it for them.</summary>
    public static bool HasPlayerClaim(Corpse corpse)
    {
        foreach (var m in corpse.Aggressors)
        {
            if (m != null && IsOffLimits(m))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <paramref name="member"/> is struck by a monster. Against far stronger foes the blow is
    /// softened a little, never enough to turn a hopeless fight.
    /// </summary>
    public static void ScaleTaken(Mobile member, Mobile from, ref int damage)
    {
        if (IsMonster(from))
        {
            var ratio = ThreatRatioOf(member);
            damage = Scale(damage, ratio > 1.0 ? Math.Max(BandSystem.MinDamageTakenScale, 1.0 / ratio) : 1.0);
        }
    }

    /// <summary>
    /// <paramref name="member"/> strikes a monster. Against far weaker foes the blow is weakened, so
    /// the fight still takes a while. Between equals, or against the stronger, it lands in full.
    /// </summary>
    public static void ScaleDealt(Mobile member, Mobile to, ref int damage)
    {
        if (IsMonster(to))
        {
            damage = Scale(damage, Math.Clamp(ThreatRatioOf(member), BandSystem.MinDamageDealtScale, 1.0));
        }
    }

    // A fight read older than this is stale: treat the fight as even.
    private const int ThreatReadMemoryMs = 2000;

    /// <summary>How the band reads its current fight: enemy strength over its own, 1 when unread.</summary>
    private static double ThreatRatioOf(Mobile member)
    {
        var party = CompanionSystem.GetParty(CaptainOf(member));

        return party != null && party.ThreatRatioAt != 0 && Core.TickCount - party.ThreatRatioAt < ThreatReadMemoryMs
            ? party.ThreatRatio
            : 1.0;
    }

    private static bool IsMonster(Mobile m) => m is BaseCreature && !IsBandMember(m) && !IsOffLimits(m);

    private static int Scale(int damage, double scale) => damage > 0 ? Math.Max(1, (int)Math.Round(damage * scale)) : damage;
}
