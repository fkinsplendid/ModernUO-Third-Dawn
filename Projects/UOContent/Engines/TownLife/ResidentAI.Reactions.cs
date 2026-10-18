using System;
using System.Collections.Generic;
using Server.Engines.Companions;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Server.Engines.TownLife;

/// <summary>Something about a passer-by worth remarking on.</summary>
public enum ReactionKind
{
    Naked,
    Armed,
    Casting,
    ExoticMount,
    Plate,
    Running,
    Entourage,
    Horse,
    ScaryPet,
    CutePet,
    Monster,
    Killer,
    Count
}

/// <summary>
/// Noticing what goes on around them. Every couple of seconds a resident looks about: a monster
/// loose in the street sends them running and shouting for the guards; a murderer, or someone's
/// dragon, makes them step out of the way; and they remark on what passers-by are up to: no
/// clothes, a blade drawn, a spell being cast, a nightmare for a mount, full plate, running
/// headlong, a train of hired companions. Each remark is made once per passer-by, not every
/// time they're seen.
/// </summary>
public sealed partial class ResidentAI
{
    private const int NoticeRange = 7;
    private const int MonsterRange = 7;
    private const int PetFearRange = 4;
    private const int MurdererFearRange = 5;
    private const int RunningRange = 3;
    private const int LookIntervalMs = 2500;
    private const int MonsterFleeMs = 8000;
    private const double RemarkChance = 0.6;
    private const int MonsterHits = 25;

    // How long a remark about someone is remembered: a while for what they are, briefly for what they're doing.
    private const long LastingMemoryMs = 600000;
    private const long MomentMemoryMs = 120000;

    private long _nextLook;
    private Mobile _monster;
    private long _monsterUntil;
    private Mobile _avoid;
    private long _avoidUntil;
    private readonly Dictionary<Mobile, long[]> _remembered = [];

    // ---------- Looking about ----------

    private void LookAround()
    {
        var now = Core.TickCount;

        if (now - _nextLook < 0 || Mobile.Map == null || Mobile.Map == Map.Internal)
        {
            return;
        }

        _nextLook = now + LookIntervalMs;

        PlayerMobile remarkable = null;
        var remark = ReactionKind.Count;

        foreach (var m in Mobile.Map.GetMobilesInRange(Mobile.Location, NoticeRange))
        {
            if (m == Mobile || m.Hidden || !m.Alive)
            {
                continue;
            }

            if (m is PlayerMobile player)
            {
                var killedOneOfUs = KilledOneOfUs(player);

                if ((player.Kills >= 5 || killedOneOfUs) && Mobile.InRange(player, MurdererFearRange))
                {
                    StepAwayFrom(player, 2000);
                }

                // The town remembers: whoever killed one of their own is called out, once a while.
                if (killedOneOfUs && remark == ReactionKind.Count && Mobile.InLOS(player) &&
                    FirstTime(player, ReactionKind.Killer, now, LastingMemoryMs))
                {
                    remarkable = player;
                    remark = ReactionKind.Killer;
                    continue;
                }

                if (remark == ReactionKind.Count && Noticeable(player, now) is var kind && kind != ReactionKind.Count)
                {
                    remarkable = player;
                    remark = kind;
                }
            }
            else if (m is BaseCreature creature && NoticeCreature(creature, now))
            {
                // Running from a monster: nothing else matters.
                return;
            }
        }

        if (remarkable != null)
        {
            Remark(remarkable, remark, now);
        }
    }

    /// <summary>Killed someone from this town, not long ago. The town remembers.</summary>
    private bool KilledOneOfUs(PlayerMobile player) =>
        TownNews.About(player, NewsKind.ResidentKilled, Resident.TownName) != null;

    /// <summary>True if it sent them running.</summary>
    private bool NoticeCreature(BaseCreature creature, long now)
    {
        if (IsMonster(creature))
        {
            if (Mobile.InRange(creature, MonsterRange) && Mobile.InLOS(creature))
            {
                _monster = creature;
                _monsterUntil = now + MonsterFleeMs;
                return true;
            }

            return false;
        }

        if (creature is BaseCompanion || creature.ControlMaster is not PlayerMobile master || master.Mount == creature)
        {
            return false;
        }

        if (IsScaryPet(creature))
        {
            if (Mobile.InRange(creature, PetFearRange))
            {
                StepAwayFrom(creature, 2500);

                if (FirstTime(master, ReactionKind.ScaryPet, now, LastingMemoryMs))
                {
                    SayReaction(ReactionKind.ScaryPet, creature, now);
                }
            }
        }
        else if (creature.Body.IsAnimal && creature.MinTameSkill <= 30 && Mobile.InRange(creature, 3) &&
                 FirstTime(master, ReactionKind.CutePet, now, LastingMemoryMs) && Utility.RandomDouble() < 0.4)
        {
            SayReaction(ReactionKind.CutePet, creature, now);
        }

        return false;
    }

    /// <summary>
    /// Loose in town and out for blood: anything hostile big enough to hurt someone (rats and
    /// mongbats don't count), and wild summons like blade spirits, which go for anyone.
    /// Pets and townsfolk aren't.
    /// </summary>
    public static bool IsMonster(BaseCreature creature) =>
        !creature.Controlled && !creature.Blessed && !creature.IsDeadPet &&
        creature is not (TownLife.Resident or BaseVendor or BaseCompanion) &&
        (creature.Summoned || creature.FightMode != FightMode.None && creature.Karma < 0 && creature.HitsMax >= MonsterHits);

    /// <summary>Hard to tame, or big enough to take several pet slots: dragons, drakes, nightmares and the like.</summary>
    public static bool IsScaryPet(BaseCreature pet) => pet.MinTameSkill >= 80.0 || pet.ControlSlots >= 3;

    /// <summary>The most remarkable thing about a passer-by that hasn't been remarked on yet, or Count.</summary>
    private ReactionKind Noticeable(PlayerMobile player, long now)
    {
        if (!Mobile.InLOS(player))
        {
            return ReactionKind.Count;
        }

        if (IsUnclothed(player) && FirstTime(player, ReactionKind.Naked, now, LastingMemoryMs))
        {
            return ReactionKind.Naked;
        }

        if (player.Warmode && player.Weapon is BaseWeapon and not Fists &&
            FirstTime(player, ReactionKind.Armed, now, MomentMemoryMs))
        {
            return ReactionKind.Armed;
        }

        if (player.Spell is Spell { IsCasting: true } && FirstTime(player, ReactionKind.Casting, now, MomentMemoryMs))
        {
            return ReactionKind.Casting;
        }

        if (player.Mount is BaseMount mount)
        {
            if (IsScaryPet(mount) || mount.MinTameSkill >= 70.0)
            {
                if (FirstTime(player, ReactionKind.ExoticMount, now, LastingMemoryMs))
                {
                    return ReactionKind.ExoticMount;
                }
            }
            else if (Utility.RandomDouble() < 0.2 && FirstTime(player, ReactionKind.Horse, now, LastingMemoryMs))
            {
                return ReactionKind.Horse;
            }
        }

        if (player.FindItemOnLayer(Layer.InnerTorso) is BaseArmor { MaterialType: ArmorMaterialType.Plate } &&
            FirstTime(player, ReactionKind.Plate, now, LastingMemoryMs))
        {
            return ReactionKind.Plate;
        }

        if ((player.Direction & Direction.Running) != 0 && now - player.LastMoveTime < 1000 &&
            Mobile.InRange(player, RunningRange) && FirstTime(player, ReactionKind.Running, now, MomentMemoryMs))
        {
            return ReactionKind.Running;
        }

        if (CountCompanions(player) >= 2 && FirstTime(player, ReactionKind.Entourage, now, LastingMemoryMs))
        {
            return ReactionKind.Entourage;
        }

        return ReactionKind.Count;
    }

    private static readonly Layer[] _clothingLayers =
    [
        Layer.Shirt, Layer.InnerTorso, Layer.MiddleTorso, Layer.OuterTorso, Layer.Pants, Layer.InnerLegs, Layer.OuterLegs
    ];

    public static bool IsUnclothed(Mobile m)
    {
        foreach (var layer in _clothingLayers)
        {
            if (m.FindItemOnLayer(layer) != null)
            {
                return false;
            }
        }

        return true;
    }

    private int CountCompanions(PlayerMobile player)
    {
        var count = 0;

        foreach (var companion in Mobile.Map.GetMobilesInRange<BaseCompanion>(player.Location, NoticeRange))
        {
            if (companion.Employer == player && !companion.IsDeadPet)
            {
                count++;
            }
        }

        return count;
    }

    private void Remark(PlayerMobile player, ReactionKind kind, long now)
    {
        if (kind == ReactionKind.Killer)
        {
            SayLine(ResidentSpeech.Recognise(Resident, player) ?? ResidentSpeech.Reaction(Resident, kind), player, now);
            return;
        }

        // A remark rolled and passed over is still "noticed": it isn't rolled again every look.
        if (Utility.RandomDouble() < RemarkChance)
        {
            SayReaction(kind, player, now);
        }
    }

    private void SayReaction(ReactionKind kind, Mobile at, long now) =>
        SayLine(ResidentSpeech.Reaction(Resident, kind), at, now);

    private void SayLine(string line, Mobile at, long now)
    {
        var town = Resident.Town;

        if (town == null || now - town.NextVoice < 0 || Conversation.IsTalking(Mobile))
        {
            return;
        }

        Mobile.Direction = Mobile.GetDirectionTo(at);
        Mobile.Say(line);
        town.NextVoice = now + 3000;
    }

    /// <summary>Records that <paramref name="kind"/> has been noticed about <paramref name="who"/>. False if it already had been.</summary>
    private bool FirstTime(Mobile who, ReactionKind kind, long now, long memoryMs)
    {
        if (!_remembered.TryGetValue(who, out var until))
        {
            Forget(now);
            _remembered[who] = until = new long[(int)ReactionKind.Count];
        }

        if (until[(int)kind] != 0 && now - until[(int)kind] < 0)
        {
            return false;
        }

        until[(int)kind] = now + memoryMs;
        return true;
    }

    private void Forget(long now)
    {
        if (_remembered.Count < 16)
        {
            return;
        }

        var stale = new List<Mobile>();

        foreach (var (who, until) in _remembered)
        {
            var any = false;

            foreach (var t in until)
            {
                any |= t != 0 && now - t < 0;
            }

            if (!any || who.Deleted)
            {
                stale.Add(who);
            }
        }

        foreach (var who in stale)
        {
            _remembered.Remove(who);
        }
    }

    // ---------- Getting out of the way ----------

    private void StepAwayFrom(Mobile m, int ms)
    {
        _avoid = m;
        _avoidUntil = Core.TickCount + ms;
    }

    /// <summary>A few steps back from something alarming, then on with the day.</summary>
    private bool TryStepAway()
    {
        if (_avoid == null)
        {
            return false;
        }

        if (Core.TickCount - _avoidUntil >= 0 || _avoid.Deleted || _avoid.Map != Mobile.Map ||
            !Mobile.InRange(_avoid, NoticeRange))
        {
            _avoid = null;
            _leg = null;
            Resident.Reroute();
            return false;
        }

        Conversation.Of(Mobile)?.End();

        var away = _avoid.GetDirectionTo(Mobile);

        if (!DoMove(away, true))
        {
            DoMove((Direction)(((int)away + Utility.RandomList(1, 7)) & 7), true);
        }

        return true;
    }
}
