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

    /// <summary>What a band takes from monsters, as a share of the blow. Lower makes bands harder to bring down.</summary>
    public static double DamageTakenScale { get; private set; }

    /// <summary>What a band deals to monsters, as a share of the blow. Lower makes fights last longer.</summary>
    public static double DamageDealtScale { get; private set; }

    /// <summary>How long after a band falls before a new one musters in its place.</summary>
    public static TimeSpan RespawnDelay { get; private set; }

    /// <summary>How long a band ghost waits, with no fight on, before the band patches them up itself.</summary>
    public static TimeSpan GhostPatience { get; private set; }

    public static void Configure()
    {
        DamageTakenScale = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("bands.damageTakenScale", 0.5), 0.05, 1.0);
        DamageDealtScale = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("bands.damageDealtScale", 0.5), 0.05, 1.0);
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

    /// <summary>A band member is struck by <paramref name="from"/>.</summary>
    public static void ScaleTaken(Mobile from, ref int damage)
    {
        if (IsMonster(from))
        {
            damage = Scale(damage, BandSystem.DamageTakenScale);
        }
    }

    /// <summary>A band member strikes <paramref name="to"/>.</summary>
    public static void ScaleDealt(Mobile to, ref int damage)
    {
        if (IsMonster(to))
        {
            damage = Scale(damage, BandSystem.DamageDealtScale);
        }
    }

    private static bool IsMonster(Mobile m) => m is BaseCreature && !IsBandMember(m) && !IsOffLimits(m);

    private static int Scale(int damage, double scale) => damage > 0 ? Math.Max(1, (int)Math.Round(damage * scale)) : damage;
}
