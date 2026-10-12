using System;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Engines.Companions;

/// <summary>
/// Crowd control from the second rank. Calms whatever threatens the backline, turns enemies on
/// each other when outnumbered, and discords whatever the employer is fighting. Uses the same
/// difficulty rolls as the player skills, and wears out instruments the same way.
/// </summary>
public sealed class BardCompanionAI : CompanionAI
{
    private const int SongCooldownMs = 7000;
    private const string DiscordMod = "CompanionDiscord";
    private static readonly TimeSpan DiscordDuration = TimeSpan.FromSeconds(20);

    private long _nextSong;
    private long _nextTune;

    public BardCompanionAI(BaseCompanion companion) : base(companion)
    {
    }

    private BaseInstrument Instrument => Mobile.Backpack?.FindItemByType<BaseInstrument>();

    protected override bool TryRoleSupport()
    {
        if (TryRevive() || TryBandage(Threats.Count > 0 ? 0.5 : 0.8))
        {
            return true;
        }

        var now = Core.TickCount;

        if (Threats.Count == 0 || now - _nextSong < 0)
        {
            return false;
        }

        var instrument = Instrument;

        if (instrument != null && (TryPeace(instrument) || TryProvoke(instrument) || TryDiscord(instrument)))
        {
            _nextSong = now + SongCooldownMs;
        }

        return false;
    }

    /// <summary>Fights back if cornered; otherwise stays at the employer's shoulder playing.</summary>
    public override bool DoActionCombat()
    {
        var attacker = FindThreatAttacking(Mobile, 2);

        if (attacker != null)
        {
            Mobile.Combatant = attacker;
            return base.DoActionCombat();
        }

        var employer = Employer;

        if (employer?.Map == Mobile.Map && !Mobile.InRange(employer, 2))
        {
            MoveTo(employer, true, 2);
        }

        return true;
    }

    protected override bool TryRoleIdle()
    {
        var now = Core.TickCount;

        if (now - _nextTune < 0)
        {
            return false;
        }

        _nextTune = now + Utility.RandomMinMax(120000, 300000);

        var instrument = Instrument;

        if (instrument == null || Companion.Quiet || Utility.RandomDouble() > 0.5)
        {
            return false;
        }

        if (BaseInstrument.CheckMusicianship(Mobile))
        {
            Mobile.Emote("*plays a lively tune*");
            instrument.PlayInstrumentWell(Mobile);
        }
        else
        {
            Mobile.Emote("*fumbles a few notes*");
            instrument.PlayInstrumentBadly(Mobile);
        }

        return false;
    }

    private bool InBardRange(Mobile target, SkillName skill) =>
        Mobile.InRange(target, BaseInstrument.GetBardRange(Mobile, skill)) && Mobile.InLOS(target);

    /// <summary>Musicianship roll, then the skill roll against the target's difficulty.</summary>
    private bool PlaySong(BaseInstrument instrument, SkillName skill, double difficulty, Mobile target)
    {
        Mobile.RevealingAction();
        Mobile.Direction = Mobile.GetDirectionTo(target);

        var music = Mobile.Skills.Musicianship.Value;

        if (music > 100.0)
        {
            difficulty -= (music - 100.0) * 0.5;
        }

        var success = BaseInstrument.CheckMusicianship(Mobile) &&
                      Mobile.CheckTargetSkill(skill, target, difficulty - 25.0, difficulty + 25.0);

        if (success)
        {
            instrument.PlayInstrumentWell(Mobile);
        }
        else
        {
            instrument.PlayInstrumentBadly(Mobile);
        }

        instrument.ConsumeUse(Mobile);
        return success;
    }

    /// <summary>Calms a creature attacking the bard or mage, or a second attacker on a wounded employer.</summary>
    private bool TryPeace(BaseInstrument instrument)
    {
        var target = FindThreatAttacking(Mobile, 10) ?? FindThreatAttacking(Party[CompanionRole.Mage], 10);
        var employer = Employer;

        if (target == null && employer is { Alive: true } && employer.Hits < employer.HitsMax / 2)
        {
            for (var i = 0; i < Threats.Count; i++)
            {
                var threat = Threats[i];

                if (threat.Combatant == employer && threat != employer.Combatant)
                {
                    target = threat;
                    break;
                }
            }
        }

        if (target is not BaseCreature { Uncalmable: false, BardPacified: false } creature ||
            !InBardRange(creature, SkillName.Peacemaking))
        {
            return false;
        }

        var difficulty = instrument.GetDifficultyFor(creature) - 10.0;

        if (PlaySong(instrument, SkillName.Peacemaking, difficulty, creature))
        {
            var seconds = Math.Clamp(100 - difficulty / 1.5, 10, 120);
            creature.Pacify(Mobile, Core.Now + TimeSpan.FromSeconds(seconds));
            creature.Combatant = null;
            creature.Warmode = false;
        }

        return true;
    }

    /// <summary>When outnumbered, sets the strongest enemy on another.</summary>
    private bool TryProvoke(BaseInstrument instrument)
    {
        if (Threats.Count < 2)
        {
            return false;
        }

        BaseCreature strongest = null;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is BaseCreature { Unprovokable: false, BardProvoked: false, Controlled: false } bc &&
                InBardRange(bc, SkillName.Provocation) && (strongest == null || bc.HitsMax > strongest.HitsMax))
            {
                strongest = bc;
            }
        }

        if (strongest == null)
        {
            return false;
        }

        BaseCreature victim = null;
        var victimDistance = double.MaxValue;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is BaseCreature { Controlled: false } bc && bc != strongest && InBardRange(bc, SkillName.Provocation))
            {
                var distance = strongest.GetDistanceToSqrt(bc);

                if (distance < victimDistance)
                {
                    victim = bc;
                    victimDistance = distance;
                }
            }
        }

        if (victim == null || !Mobile.CanBeHarmful(strongest, false) || !Mobile.CanBeHarmful(victim, false))
        {
            return false;
        }

        var difficulty = (instrument.GetDifficultyFor(strongest) + instrument.GetDifficultyFor(victim)) * 0.5 - 5.0;

        if (PlaySong(instrument, SkillName.Provocation, difficulty, victim))
        {
            strongest.Provoke(Mobile, victim, true);
        }

        return true;
    }

    /// <summary>
    /// Weakens the employer's target by the same proportion pre-AOS discordance does:
    /// a fifth of the bard's Discordance skill, as a percentage of stats and skills.
    /// </summary>
    private bool TryDiscord(BaseInstrument instrument)
    {
        var target = Employer?.Combatant ?? Party[CompanionRole.Warrior]?.Combatant;

        if (target is not BaseCreature { BardImmune: false } creature || !Threats.Contains(creature) ||
            creature.GetStatMod($"{DiscordMod}Str") != null || !InBardRange(creature, SkillName.Discordance))
        {
            return false;
        }

        var effect = 0;

        if (Discordance.GetEffect(creature, ref effect))
        {
            return false;
        }

        var difficulty = instrument.GetDifficultyFor(creature) - 10.0;

        if (!PlaySong(instrument, SkillName.Discordance, difficulty, creature))
        {
            return true;
        }

        var scalar = -Mobile.Skills.Discordance.Value / 500.0;

        creature.AddStatMod(new StatMod(StatType.Str, $"{DiscordMod}Str", (int)(creature.RawStr * scalar), DiscordDuration));
        creature.AddStatMod(new StatMod(StatType.Dex, $"{DiscordMod}Dex", (int)(creature.RawDex * scalar), DiscordDuration));
        creature.AddStatMod(new StatMod(StatType.Int, $"{DiscordMod}Int", (int)(creature.RawInt * scalar), DiscordDuration));

        var skills = creature.Skills;

        for (var i = 0; i < skills.Length; i++)
        {
            var skill = skills[i];

            if (skill.Value > 0)
            {
                creature.AddSkillMod(
                    new TimedSkillMod(skill.SkillName, DiscordMod, true, skill.Value * scalar, DiscordDuration)
                );
            }
        }

        creature.FixedEffect(0x376A, 1, 32);
        return true;
    }
}
