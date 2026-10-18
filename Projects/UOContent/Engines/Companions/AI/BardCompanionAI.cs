using System;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Engines.Companions;

/// <summary>
/// Crowd control from the second rank. Calms whatever threatens the backline, turns enemies on
/// each other when outnumbered, and discords whatever the employer is fighting. Outmatched by
/// brutes, setting them on each other comes before everything and is worth a long shot. Uses the
/// same difficulty rolls as the player skills, and wears out instruments the same way.
/// </summary>
public sealed class BardCompanionAI : CompanionAI
{
    private const int SongCooldownMs = 7000;
    private const string DiscordMod = "CompanionDiscord";
    private static readonly TimeSpan DiscordDuration = TimeSpan.FromSeconds(20);

    private long _nextSong;
    private long _nextTune;

    // Set when the last look over the fight found nothing worth singing at.
    private bool _songless;

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

        if (Threats.Count == 0)
        {
            _songless = false;
            return false;
        }

        if (now - _nextSong < 0)
        {
            return false;
        }

        var instrument = Instrument;

        // Provocation takes two enemies out of the fight with one song, so it comes first;
        // peace jumps ahead only to save a badly wounded backliner, unless brutes are winning.
        var desperate = Outmatched && BruteCount > 0;

        if (instrument == null)
        {
            Note("I've no instrument to play");
        }

        if (instrument != null && (desperate && TryProvoke(instrument, DesperateChance) ||
                                   TryPeace(instrument, true) || TryProvoke(instrument, MinimumChance) ||
                                   TryPeace(instrument, false) || TryDiscord(instrument)))
        {
            _nextSong = now + SongCooldownMs;
            _songless = false;
        }
        else
        {
            if (instrument != null && !_songless)
            {
                Note(
                    _bestProvokeChance > 0
                        ? $"held my songs: the best provocation odds were only {_bestProvokeChance:P0}"
                        : Threats.Count < 2 ? "held my songs: nothing to pair up, and no one to calm"
                        : "held my songs: nothing I could sway"
                );
            }

            _songless = true;
        }

        return false;
    }

    /// <summary>
    /// Fights back if cornered, and draws steel when told to kill or when there's nothing left
    /// worth singing at; otherwise stays at the employer's shoulder playing.
    /// </summary>
    public override bool DoActionCombat()
    {
        var attacker = FindThreatAttacking(Mobile, 2);

        if (attacker != null)
        {
            Mobile.Combatant = attacker;
            return base.DoActionCombat();
        }

        if (Mobile.Combatant != null && (Mobile.ControlOrder == OrderType.Attack || _songless || Instrument == null))
        {
            return base.DoActionCombat();
        }

        KeepFormation();
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

    // A bard who knows their craft doesn't strum at something they can't hope to sway.
    private const double MinimumChance = 0.3;

    // ...unless the brutes will win otherwise.
    private const double DesperateChance = 0.15;

    // The best provocation odds seen on the last look, for explaining a silent bard.
    private double _bestProvokeChance;

    private bool InBardRange(Mobile target, SkillName skill) =>
        Mobile.InRange(target, BaseInstrument.GetBardRange(Mobile, skill)) && Mobile.InLOS(target);

    /// <summary>Musicianship above 100 eases every song, exactly as it does for players.</summary>
    private double AdjustDifficulty(double difficulty)
    {
        var music = Mobile.Skills.Musicianship.Value;
        return music > 100.0 ? difficulty - (music - 100.0) * 0.5 : difficulty;
    }

    /// <summary>
    /// Odds of a song landing: the musicianship roll, then the skill roll against
    /// difficulty ± 25, the same two rolls the player skills make.
    /// </summary>
    private double SongChance(SkillName skill, double difficulty)
    {
        var music = Math.Clamp(Mobile.Skills.Musicianship.Value / 100.0, 0.0, 1.0);
        var roll = Math.Clamp((Mobile.Skills[skill].Value - (difficulty - 25.0)) / 50.0, 0.0, 1.0);

        return music * roll;
    }

    private bool PlaySong(BaseInstrument instrument, SkillName skill, double difficulty, Mobile target)
    {
        Mobile.RevealingAction();
        Mobile.Direction = Mobile.GetDirectionTo(target);

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
        Note($"played {skill} on {target.Name}, and it {(success ? "worked" : "failed")}");
        return success;
    }

    private double PeaceDifficulty(BaseInstrument instrument, Mobile target) =>
        AdjustDifficulty(instrument.GetDifficultyFor(target) - 10.0);

    private static bool IsBadlyHurt(Mobile m) => m is { Alive: true } && m.Hits < m.HitsMax * 0.4;

    /// <summary>
    /// Calms whatever is attacking the bard or mage, or an extra attacker on a wounded employer,
    /// choosing the one the song is most likely to reach. In an emergency, only attackers of a
    /// badly wounded bard or mage are considered.
    /// </summary>
    private bool TryPeace(BaseInstrument instrument, bool emergencyOnly)
    {
        var employer = Employer;
        var employerHurt = employer is { Alive: true } && employer.Hits < employer.HitsMax / 2;
        var mage = Party[CompanionRole.Mage];

        BaseCreature best = null;
        var bestChance = MinimumChance;
        var bestDifficulty = 0.0;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is not BaseCreature { Uncalmable: false, BardPacified: false } creature ||
                !InBardRange(creature, SkillName.Peacemaking))
            {
                continue;
            }

            var victim = creature.Combatant;
            var worthCalming = emergencyOnly
                ? (victim == Mobile || victim != null && victim == mage) && IsBadlyHurt(victim)
                : victim == Mobile || victim != null && victim == mage ||
                  employerHurt && victim == employer && creature != employer.Combatant;

            if (!worthCalming)
            {
                continue;
            }

            var difficulty = PeaceDifficulty(instrument, creature);
            var chance = SongChance(SkillName.Peacemaking, difficulty);

            if (chance > bestChance)
            {
                best = creature;
                bestChance = chance;
                bestDifficulty = difficulty;
            }
        }

        if (best == null)
        {
            return false;
        }

        if (PlaySong(instrument, SkillName.Peacemaking, bestDifficulty, best))
        {
            var seconds = Math.Clamp(100 - bestDifficulty / 1.5, 10, 120);
            best.Pacify(Mobile, Core.Now + TimeSpan.FromSeconds(seconds));
            best.Combatant = null;
            best.Warmode = false;
        }

        return true;
    }

    /// <summary>
    /// When outnumbered, sets one enemy on another. Picks the pairing with the best odds,
    /// favouring the strongest instigator when the odds are close.
    /// </summary>
    private bool TryProvoke(BaseInstrument instrument, double minimumChance)
    {
        _bestProvokeChance = 0;

        if (Threats.Count < 2)
        {
            return false;
        }

        BaseCreature bestInstigator = null, bestVictim = null;
        var bestScore = 0.0;
        var bestDifficulty = 0.0;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is not BaseCreature { Unprovokable: false, BardProvoked: false, Controlled: false } instigator ||
                !InBardRange(instigator, SkillName.Provocation) || !Mobile.CanBeHarmful(instigator, false))
            {
                continue;
            }

            for (var j = 0; j < Threats.Count; j++)
            {
                if (i == j || Threats[j] is not BaseCreature { Controlled: false } victim ||
                    !InBardRange(victim, SkillName.Provocation) || !Mobile.CanBeHarmful(victim, false))
                {
                    continue;
                }

                var difficulty = AdjustDifficulty(
                    (instrument.GetDifficultyFor(instigator) + instrument.GetDifficultyFor(victim)) * 0.5 - 5.0
                );
                var chance = SongChance(SkillName.Provocation, difficulty);
                _bestProvokeChance = Math.Max(_bestProvokeChance, chance);

                if (chance < minimumChance)
                {
                    continue;
                }

                var score = chance * (1.0 + instigator.HitsMax / 200.0);

                if (score > bestScore)
                {
                    bestInstigator = instigator;
                    bestVictim = victim;
                    bestScore = score;
                    bestDifficulty = difficulty;
                }
            }
        }

        if (bestInstigator == null)
        {
            return false;
        }

        if (PlaySong(instrument, SkillName.Provocation, bestDifficulty, bestVictim))
        {
            bestInstigator.Provoke(Mobile, bestVictim, true);
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

        var difficulty = AdjustDifficulty(instrument.GetDifficultyFor(creature) - 10.0);

        if (SongChance(SkillName.Discordance, difficulty) < MinimumChance)
        {
            return false;
        }

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
