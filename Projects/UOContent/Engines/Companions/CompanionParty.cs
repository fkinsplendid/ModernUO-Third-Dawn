using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Runtime view of one player's companions. Rebuilt from the companions themselves on load,
/// so nothing here is serialized. Coordinates things no single companion should decide alone:
/// who heals whom, which corpses are ours to loot, and who gets to talk.
/// </summary>
public sealed class CompanionParty
{
    private const int RoleCount = 3;
    private const int MaxPendingCorpses = 16;

    private readonly BaseCompanion[] _members = new BaseCompanion[RoleCount];
    private readonly List<PendingCorpse> _pendingCorpses = new(MaxPendingCorpses);

    // Heal claims stop two healers spending a turn on the same patient.
    private readonly Mobile[] _healPatients = new Mobile[RoleCount + 1];
    private readonly BaseCompanion[] _healers = new BaseCompanion[RoleCount + 1];
    private readonly long[] _healClaimExpiry = new long[RoleCount + 1];

    public CompanionParty(Mobile owner) => Owner = owner;

    public Mobile Owner { get; }

    /// <summary>Tick count before which no companion may start idle chatter.</summary>
    public long NextChatter { get; set; }

    /// <summary>Tick count before which the party will not suggest another gate to town.</summary>
    public long NextTownGate { get; set; }

    /// <summary>The escape or town gate the party is currently heading through, if any.</summary>
    public Moongate ActiveGate { get; set; }

    /// <summary>Someone is weighed down with loot; the mage should offer a gate to town.</summary>
    public bool WantsTownGate { get; set; }

    /// <summary>Tick count before which no other companion joins in on a shared moment (a revival cheer).</summary>
    public long NextCheer { get; set; }

    // Places someone has remarked on, and when, so arriving somewhere gets one remark, not three.
    private readonly Dictionary<string, long> _placeRemarks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly long PlaceRemarkCooldownMs = (long)TimeSpan.FromMinutes(30).TotalMilliseconds;

    /// <summary>True if nobody in the party has remarked on <paramref name="place"/> lately.</summary>
    public bool CanRemarkOn(string place, long now) =>
        !_placeRemarks.TryGetValue(place, out var last) || now - last >= PlaceRemarkCooldownMs;

    public void RemarkedOn(string place, long now) => _placeRemarks[place] = now;

    public bool IsEmpty
    {
        get
        {
            for (var i = 0; i < _members.Length; i++)
            {
                if (_members[i] != null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public BaseCompanion this[CompanionRole role] => _members[(int)role];

    /// <summary>All slots, including empty ones. Callers skip nulls.</summary>
    public ReadOnlySpan<BaseCompanion> Members => _members;

    internal void Set(BaseCompanion companion) => _members[(int)companion.Role] = companion;

    internal void Remove(BaseCompanion companion)
    {
        ref var slot = ref _members[(int)companion.Role];

        if (slot == companion)
        {
            slot = null;
        }
    }

    public static bool IsInWorld(BaseCompanion c) =>
        c?.Deleted == false && c.State == CompanionState.Active && c.Map != null && c.Map != Map.Internal;

    public static bool IsFighting(BaseCompanion c) => IsInWorld(c) && !c.IsDeadPet;

    /// <summary>True for the owner, any of their companions, their mounts, and the owner's other pets.</summary>
    public bool IsFriendly(Mobile m)
    {
        if (m == null)
        {
            return false;
        }

        if (m == Owner)
        {
            return true;
        }

        return m is BaseCompanion c && c.Party == this ||
               m is BaseCreature { Controlled: true } bc &&
               (bc.ControlMaster == Owner || bc.ControlMaster is BaseCompanion master && master.Party == this);
    }

    public int CountInWorld(bool aliveOnly)
    {
        var count = 0;

        for (var i = 0; i < _members.Length; i++)
        {
            var c = _members[i];

            if (IsInWorld(c) && (!aliveOnly || !c.IsDeadPet))
            {
                count++;
            }
        }

        return count;
    }

    // ---------- Heal coordination ----------

    public bool IsHealClaimedByOther(Mobile patient, BaseCompanion healer)
    {
        var now = Core.TickCount;

        for (var i = 0; i < _healPatients.Length; i++)
        {
            if (_healPatients[i] == patient && _healers[i] != healer && _healClaimExpiry[i] - now > 0)
            {
                return true;
            }
        }

        return false;
    }

    public void ClaimHeal(Mobile patient, BaseCompanion healer, int durationMs)
    {
        var now = Core.TickCount;
        var free = -1;

        for (var i = 0; i < _healPatients.Length; i++)
        {
            if (_healPatients[i] == patient)
            {
                free = i;
                break;
            }

            if (free == -1 && (_healPatients[i] == null || _healClaimExpiry[i] - now <= 0))
            {
                free = i;
            }
        }

        if (free == -1)
        {
            return;
        }

        _healPatients[free] = patient;
        _healers[free] = healer;
        _healClaimExpiry[free] = now + durationMs;
    }

    // ---------- Loot coordination ----------

    private sealed class PendingCorpse
    {
        public Corpse Corpse;
        public BaseCompanion Looter;
    }

    /// <summary>Called when a creature the party killed dies. The corpse becomes ours to search.</summary>
    public void AddCorpse(Corpse corpse)
    {
        if (corpse == null || IndexOf(corpse) >= 0)
        {
            return;
        }

        if (_pendingCorpses.Count >= MaxPendingCorpses)
        {
            _pendingCorpses.RemoveAt(0);
        }

        _pendingCorpses.Add(new PendingCorpse { Corpse = corpse });
    }

    /// <summary>
    /// Nearest unclaimed corpse within range of the looter, now claimed by them,
    /// so two companions never rummage through the same body.
    /// </summary>
    public Corpse ClaimCorpse(BaseCompanion looter, int range)
    {
        PendingCorpse best = null;
        var bestDistance = double.MaxValue;

        for (var i = _pendingCorpses.Count - 1; i >= 0; i--)
        {
            var pending = _pendingCorpses[i];
            var corpse = pending.Corpse;

            if (corpse.Deleted || corpse.Items.Count == 0)
            {
                _pendingCorpses.RemoveAt(i);
                continue;
            }

            // A claim lapses if the looter is no longer out fighting (dead, gone to the guild, waiting).
            var claimedByOther = pending.Looter != null && pending.Looter != looter && IsFighting(pending.Looter);

            if (claimedByOther || corpse.Map != looter.Map)
            {
                continue;
            }

            var distance = looter.GetDistanceToSqrt(corpse);

            if (distance <= range && distance < bestDistance)
            {
                best = pending;
                bestDistance = distance;
            }
        }

        if (best == null)
        {
            return null;
        }

        best.Looter = looter;
        return best.Corpse;
    }

    public void ReleaseCorpse(Corpse corpse)
    {
        var index = IndexOf(corpse);

        if (index >= 0)
        {
            _pendingCorpses[index].Looter = null;
        }
    }

    public void FinishCorpse(Corpse corpse)
    {
        var index = IndexOf(corpse);

        if (index >= 0)
        {
            _pendingCorpses.RemoveAt(index);
        }
    }

    private int IndexOf(Corpse corpse)
    {
        for (var i = 0; i < _pendingCorpses.Count; i++)
        {
            if (_pendingCorpses[i].Corpse == corpse)
            {
                return i;
            }
        }

        return -1;
    }
}
