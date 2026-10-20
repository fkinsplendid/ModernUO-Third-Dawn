using System;
using Server.Gumps;
using Server.Items;
using Server.Regions;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fifth;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Second;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;

namespace Server.Engines.Companions;

/// <summary>
/// Healer first, artillery second. Cures and heals the party unprompted, blesses them in the lulls
/// of a hunt, keeps its distance in combat, raises a fallen employer, and opens a gate out when the
/// fight is lost or the party is weighed down with loot. Every spell costs real reagents.
/// </summary>
public sealed partial class MageCompanionAI : CompanionAI
{
    // The circles go into the book at about an even chance per cast (Gate 60-100, Resurrection
    // 70-110 pre-ML). A fizzle still costs the reagents, so a mage tries a few times before giving up.
    internal static readonly double GateSkill = CompanionMage.CircleSkill(7);
    internal static readonly double ResurrectSkill = CompanionMage.CircleSkill(8);
    private const int GateWaitMs = 8000;
    private const int MaxGateAttempts = 3;
    private const int ResurrectRetryMs = 2000;
    private const int RaiseClaimMs = 6000;

    // Blessings are kept up through the lulls of a hunt, not while strolling about town.
    private const int BlessAfterFightMs = 60000;
    private static readonly TimeSpan TownGateCooldown = TimeSpan.FromMinutes(10);

    private long _gateDeadline;
    private bool _gateIsEscape;
    private string _gateDestination;
    private bool _gateRefused;
    private Spell _gateSpell;
    private int _gateFizzles;
    private bool _retryTownGate;
    private bool _gateRequested;
    private GateDestination? _gateTarget;
    private Spell _resurrectSpell;
    private Mobile _resurrectTarget;
    private Spell _markSpell;
    private RecallRune _markRune;
    private string _markName;
    private long _nextBlessCheck;

    public MageCompanionAI(BaseCompanion companion) : base(companion)
    {
    }

    private double Magery => Mobile.Skills.Magery.Value;

    protected override bool KnowsSpell(Spell spell) => (Companion as CompanionMage)?.Knows(spell) == true;

    private bool Knows<T>() where T : Spell => (Companion as CompanionMage)?.Knows(typeof(T)) == true;

    protected override bool TryRoleSupport()
    {
        if (_gateDeadline != 0)
        {
            CheckGateOpened();
        }

        CheckResurrectLanded();
        CheckMarkLanded();

        if (Mobile.Spell != null)
        {
            return false;
        }

        if (InSeriousTrouble && TryEscapeGate())
        {
            return true;
        }

        if (!InSeriousTrouble)
        {
            _gateRefused = false;

            if (_gateIsEscape && _gateDeadline == 0)
            {
                _gateFizzles = 0;
            }
        }

        return TryResurrect() || TryEscapeMelee() || TryCure() || TryHeal() || TryShield() ||
               Threats.Count == 0 && TryBless();
    }

    protected override bool TryRoleIdle()
    {
        if (_retryTownGate && _gateDeadline == 0 && Mobile.Spell == null)
        {
            _retryTownGate = false;

            if (OpenGate(false, _gateTarget))
            {
                return true;
            }

            _gateFizzles = 0;
            _gateTarget = null;
            Mobile.Say(GateRefusal());
        }

        // Between fights a mage sits in a trance whenever the party stands still. Walking breaks it.
        if (Mobile.Mana < Mobile.ManaMax * 0.9)
        {
            Meditate();
        }

        var party = Party;

        if (!party.WantsTownGate || Core.TickCount - party.NextTownGate < 0)
        {
            return false;
        }

        if (Mobile.Region.IsPartOf<TownRegion>())
        {
            party.WantsTownGate = false;
            return false;
        }

        party.WantsTownGate = false;
        party.NextTownGate = Core.TickCount + (long)TownGateCooldown.TotalMilliseconds;
        _gateFizzles = 0;
        _gateRequested = false;
        return OpenGate(false);
    }

    // ---------- Healing ----------

    private bool TryCure()
    {
        var patient = FindPatient(1.0, 10);

        if (patient?.Poisoned != true || Mobile.Mana < 6)
        {
            return false;
        }

        return Treat(new CureSpell(Mobile), patient, 6);
    }

    private bool TryHeal()
    {
        var patient = FindPatient(Threats.Count > 0 ? 0.65 : 0.85, 10);

        if (patient == null || patient.Poisoned)
        {
            return false;
        }

        var missing = patient.HitsMax - patient.Hits;

        if (missing > 20 && Magery >= 50.0 && Mobile.Mana >= 11)
        {
            return Treat(new GreaterHealSpell(Mobile), patient, 11);
        }

        return Mobile.Mana >= 4 && Treat(new HealSpell(Mobile), patient, 4);
    }

    /// <summary>Casts a healing spell and tells the other healers this patient is taken.</summary>
    private bool Treat(Spell spell, Mobile patient, int mana)
    {
        if (!CastOrApproach(spell, patient, mana))
        {
            return false;
        }

        Party.ClaimHeal(patient, Companion, 3000);
        return true;
    }

    private bool TryBless()
    {
        var now = Core.TickCount;

        if (now - _nextBlessCheck < 0 || Magery < 40.0 || Mobile.Mana < Mobile.ManaMax / 2 ||
            !FoughtWithin(BlessAfterFightMs))
        {
            return false;
        }

        _nextBlessCheck = now + 15000;

        var employer = Employer;

        if (NeedsBless(employer))
        {
            return CastOrApproach(new BlessSpell(Mobile), employer, 9);
        }

        foreach (var ally in Party.Members)
        {
            if (CompanionParty.IsFighting(ally) && NeedsBless(ally))
            {
                return CastOrApproach(new BlessSpell(Mobile), ally, 9);
            }
        }

        return false;
    }

    private bool NeedsBless(Mobile m) =>
        m?.Alive == true && m.Map == Mobile.Map && Mobile.InRange(m, 10) && m.GetStatMod("[Magic] Str Buff") == null;

    /// <summary>
    /// Once the fighting stops, raises a dead employer (who is asked whether to accept) and then
    /// any fallen companions.
    /// </summary>
    private bool TryResurrect()
    {
        if (Threats.Count > 0 || !Knows<ResurrectionSpell>() || Mobile.Mana < 50 || _resurrectSpell != null)
        {
            return false;
        }

        var employer = Employer;

        if (employer?.Alive == false && Core.TickCount - NextEmployerRes >= 0 && CanReach(employer) &&
            ApproachAndResurrect(employer, out var cast))
        {
            if (cast)
            {
                // Don't badger the employer if they decline the offer.
                NextEmployerRes = Core.TickCount + 30000;
            }

            return true;
        }

        foreach (var ally in Party.Members)
        {
            if (ally != Companion && CompanionParty.IsInWorld(ally) && ally.IsDeadPet && CanReach(ally) &&
                ApproachAndResurrect(ally, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanReach(Mobile m) => m.Map == Mobile.Map && Mobile.InRange(m, 12);

    /// <summary>The spell needs a touch: walk up to the ghost, then cast. True if the tick was spent.</summary>
    private bool ApproachAndResurrect(Mobile ghost, out bool cast)
    {
        cast = false;
        var spell = new ResurrectionSpell(Mobile);

        if (!HasReagents(spell.Info))
        {
            return false;
        }

        // A fallen companion stands still for this, or it drifts out of reach mid-chant.
        Party.ClaimHeal(ghost, Companion, RaiseClaimMs);

        if (!Mobile.InRange(ghost, 1))
        {
            MoveTo(ghost, true, 1);
            return true;
        }

        cast = CastAt(spell, ghost);

        if (cast)
        {
            _resurrectSpell = spell;
            _resurrectTarget = ghost;
        }

        return cast;
    }

    /// <summary>
    /// Once a Resurrection has run its course, checks whether it took. A fizzle (no offer sent, still
    /// a ghost) is tried again shortly, rather than waiting as if the employer had declined.
    /// </summary>
    private void CheckResurrectLanded()
    {
        if (_resurrectSpell == null || _resurrectSpell.State != SpellState.None || Mobile.Spell == _resurrectSpell)
        {
            return;
        }

        var ghost = _resurrectTarget;
        _resurrectSpell = null;
        _resurrectTarget = null;

        if (ghost == null || ghost.Deleted)
        {
            return;
        }

        if (ghost == Employer && !ghost.Alive && !ghost.HasGump<ResurrectGump>())
        {
            NextEmployerRes = Core.TickCount + ResurrectRetryMs;
            Companion.Speak(SpeechContext.Fizzle, urgent: true);
        }
        else if (ghost is BaseCompanion { IsDeadPet: true })
        {
            Companion.Speak(SpeechContext.Fizzle, urgent: true);
        }
    }

    /// <summary>Casts if the patient is in range; otherwise walks towards them.</summary>
    private bool CastOrApproach(Spell spell, Mobile target, int mana)
    {
        if (Mobile.Mana < mana || !KnowsSpell(spell))
        {
            return false;
        }

        if (!Mobile.InRange(target, 10) || !Mobile.InLOS(target))
        {
            MoveTo(target, true, 4);
            return true;
        }

        return CastAt(spell, target);
    }

    // ---------- Combat ----------

    /// <summary>
    /// Keeps out of melee, stays near the employer, and casts when mana allows. Out of mana and
    /// with nothing on top of them, they meditate where they stand, as a player mage would.
    /// </summary>
    public override bool DoActionCombat()
    {
        var target = Mobile.Combatant;

        if (!IsValidFocusMob(target))
        {
            Mobile.Combatant = null;
            return true;
        }

        // Out of reach, out of sight, or held by a paralyze that a spell would break: turn on
        // something else instead.
        if ((!InSpellRange(target) || target.Paralyzed) && FindFreeThreat() is { } other)
        {
            Mobile.Combatant = target = other;
        }

        var adjacent = FindThreatAttacking(Mobile, 1);

        if (adjacent != null)
        {
            KeepDistanceFrom(adjacent, 3);
        }
        else if (Employer is { } employer && employer.Map == Mobile.Map && !Mobile.InRange(employer, 6))
        {
            MoveTo(employer, true, 3);
        }

        Mobile.Direction = Mobile.GetDirectionTo(target);

        if (Mobile.Spell != null || TryTactics(target))
        {
            return true;
        }

        // Held fast and nobody on it: a spell would only set it free. Catch breath instead.
        if (target.Paralyzed && !IsEngaged(target))
        {
            Meditate();
            return true;
        }

        // Keep a quarter of the mana pool for healing.
        var spell = Mobile.Mana > Mobile.ManaMax / 4 && InSpellRange(target) ? ChooseAttack(target) : null;

        if (spell != null)
        {
            CastAt(spell, target);
        }
        else if (adjacent == null)
        {
            Meditate();
        }

        return true;
    }

    private const int SpellRange = 10;

    /// <summary>The nearest threat in spell range that isn't held by paralysis.</summary>
    private Mobile FindFreeThreat()
    {
        Mobile best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < Threats.Count; i++)
        {
            var threat = Threats[i];
            var distance = Mobile.GetDistanceToSqrt(threat);

            if (distance < bestDistance && !threat.Paralyzed && InSpellRange(threat))
            {
                best = threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    private bool InSpellRange(Mobile target) => Mobile.InRange(target, SpellRange) && Mobile.InLOS(target);

    /// <summary>
    /// The strongest damage spell this mage knows and can pay for, in mana and in reagents. Runs
    /// down the list rather than giving up when the pack is out of one reagent. Each spell is tried
    /// about as often as the mage expects to land it, so a newly learned circle is attempted, and
    /// fizzled, and trained, while the old reliable ones fill in. Poison is decided separately.
    /// </summary>
    internal Spell ChooseAttack(Mobile target)
    {
        // Flame strike's mana is dear; it's a now-and-then spell however skilled the mage.
        var spell = Utility.RandomDouble() < 0.3 ? Confident(new FlameStrikeSpell(Mobile)) : null;

        spell ??= Confident(new EnergyBoltSpell(Mobile));

        if (target.Int < Mobile.Int)
        {
            spell ??= Confident(new MindBlastSpell(Mobile));
        }

        spell ??= Confident(new LightningSpell(Mobile));
        spell ??= Confident(new FireballSpell(Mobile));

        return spell ?? Affordable(new MagicArrowSpell(Mobile));
    }

    /// <summary>The spell, if affordable and a roll against its odds says the mage will chance it.</summary>
    private Spell Confident(Spell spell) => Utility.RandomDouble() < CastChance(spell, Magery) ? Affordable(spell) : null;

    private Spell Affordable(Spell spell) =>
        Mobile.Mana >= spell.GetMana() && KnowsSpell(spell) && HasReagents(spell.Info) ? spell : null;

    /// <summary>Tries for a meditative trance. The skill itself decides whether it takes.</summary>
    private void Meditate()
    {
        if (!Mobile.Meditating && Mobile.Mana < Mobile.ManaMax && Mobile.Spell == null && Mobile.Target == null &&
            Core.TickCount - Mobile.NextSkillTime >= 0)
        {
            Mobile.UseSkill(SkillName.Meditation);
        }
    }
}
