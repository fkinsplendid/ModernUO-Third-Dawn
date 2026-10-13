using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ModernUO.CodeGeneratedEvents;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Owns the employer → party registry, the shared wage clock, and the world events
/// (login, logout, kills) that companions react to. One timer serves every companion.
/// </summary>
public static class CompanionSystem
{
    private static readonly Dictionary<Mobile, CompanionParty> _parties = new();
    private static readonly TimeSpan SeparationCheckInterval = TimeSpan.FromSeconds(2);

    private static TimerExecutionToken _minuteTimer;
    private static TimerExecutionToken _separationTimer;

    public static void Initialize()
    {
        EventSink.Logout += OnLogout;

        Timer.StartTimer(TimeSpan.FromMinutes(1.0), TimeSpan.FromMinutes(1.0), OnMinuteElapsed, out _minuteTimer);
        Timer.StartTimer(SeparationCheckInterval, SeparationCheckInterval, CheckSeparations, out _separationTimer);
    }

    /// <summary>Runs outside the companions' own AI, which sleeps when no player is near them.</summary>
    private static void CheckSeparations()
    {
        foreach (var party in _parties.Values)
        {
            foreach (var companion in party.Members)
            {
                companion?.CheckSeparation();
            }
        }
    }

    public static CompanionParty GetParty(Mobile employer) =>
        employer != null && _parties.TryGetValue(employer, out var party) ? party : null;

    public static BaseCompanion GetCompanion(Mobile employer, CompanionRole role) => GetParty(employer)?[role];

    internal static void Register(BaseCompanion companion)
    {
        var employer = companion.Employer;

        if (employer == null)
        {
            return;
        }

        ref var party = ref CollectionsMarshal.GetValueRefOrAddDefault(_parties, employer, out _);
        party ??= new CompanionParty(employer);

        var existing = party[companion.Role];

        if (existing != null && existing != companion)
        {
            existing.Party = null;
        }

        party.Set(companion);
        companion.Party = party;
    }

    internal static void Unregister(BaseCompanion companion)
    {
        var party = companion.Party;

        if (party == null)
        {
            return;
        }

        party.Remove(companion);
        companion.Party = null;

        if (party.IsEmpty)
        {
            _parties.Remove(party.Owner);
        }
    }

    private static void OnMinuteElapsed()
    {
        // Snapshot first: a companion may leave its party (unpaid) while we iterate.
        using var queue = PooledRefQueue<BaseCompanion>.Create();

        foreach (var party in _parties.Values)
        {
            foreach (var companion in party.Members)
            {
                if (companion != null)
                {
                    queue.Enqueue(companion);
                }
            }
        }

        while (queue.Count > 0)
        {
            queue.Dequeue().OnMinuteElapsed();
        }
    }

    private static void OnLogout(Mobile m)
    {
        var party = GetParty(m);

        if (party == null)
        {
            return;
        }

        foreach (var companion in party.Members)
        {
            companion?.WaitForEmployer();
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        var party = GetParty(pm);

        if (party == null)
        {
            return;
        }

        foreach (var companion in party.Members)
        {
            companion?.RejoinEmployer();
        }
    }

    /// <summary>
    /// Every party that dealt damage shares the experience in full and gets the corpse
    /// added to its loot list. Only creatures killed by someone in the party count.
    /// </summary>
    [OnEvent(nameof(BaseCreature.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature dead)
    {
        if (dead is BaseCompanion || dead.Controlled || dead.Summoned || dead.NoKillAwards || _parties.Count == 0)
        {
            return;
        }

        var killerParty = FindPartyOf(dead.LastKiller);

        if (killerParty == null)
        {
            return;
        }

        var experience = Math.Max(1, (int)((Math.Abs(dead.Fame) / 15 + dead.HitsMax / 10) * CompanionSettings.ExperienceScale));

        foreach (var companion in killerParty.Members)
        {
            if (CompanionParty.IsFighting(companion) && companion.InRange(dead.Location, 18))
            {
                companion.OnPartyKill(dead, experience);
            }
        }

        killerParty.AddCorpse(dead.Corpse as Corpse);
    }

    /// <summary>The party credited with a kill: a companion, the employer, their pets, or a creature our bard provoked.</summary>
    private static CompanionParty FindPartyOf(Mobile m) =>
        m switch
        {
            BaseCompanion c                                              => c.Party,
            BaseCreature { Controlled: true, ControlMaster: { } master } => GetParty(master),
            BaseCreature { BardProvoked: true, BardMaster: BaseCompanion bard } => bard.Party,
            _                                                            => GetParty(m)
        };
}
