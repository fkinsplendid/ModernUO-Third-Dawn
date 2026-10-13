using System;
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
/// Healer first, artillery second. Cures and heals the party unprompted, blesses them between
/// fights, keeps its distance in combat, raises a fallen employer, and opens a gate out when the
/// fight is lost or the party is weighed down with loot. Every spell costs real reagents.
/// </summary>
public sealed class MageCompanionAI : CompanionAI
{
    private const double GateSkill = 70.0;
    private const int GateWaitMs = 8000;
    private static readonly TimeSpan TownGateCooldown = TimeSpan.FromMinutes(10);

    private long _gateDeadline;
    private bool _gateIsEscape;
    private string _gateDestination;
    private bool _gateRefused;
    private long _nextBlessCheck;

    public MageCompanionAI(BaseCompanion companion) : base(companion)
    {
    }

    private double Magery => Mobile.Skills.Magery.Value;

    protected override bool TryRoleSupport()
    {
        if (_gateDeadline != 0)
        {
            CheckGateOpened();
        }

        if (Mobile.Spell != null)
        {
            return false;
        }

        if (InSeriousTrouble && TryEscapeGate())
        {
            return true;
        }

        _gateRefused &= InSeriousTrouble;

        return TryResurrect() || TryCure() || TryHeal() || Threats.Count == 0 && TryBless();
    }

    protected override bool TryRoleIdle()
    {
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
        return OpenGate(false);
    }

    /// <summary>Opens a gate to town on the employer's request. Returns false if it can't.</summary>
    public bool RequestTownGate() => Mobile.Spell == null && OpenGate(false);

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

        if (now - _nextBlessCheck < 0 || Magery < 40.0 || Mobile.Mana < Mobile.ManaMax / 2)
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
        if (Threats.Count > 0 || Magery < 80.0 || Mobile.Mana < 50)
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

        if (!Mobile.InRange(ghost, 1))
        {
            MoveTo(ghost, true, 1);
            return true;
        }

        cast = CastAt(spell, ghost);
        return cast;
    }

    /// <summary>Casts if the patient is in range; otherwise walks towards them.</summary>
    private bool CastOrApproach(Spell spell, Mobile target, int mana)
    {
        if (Mobile.Mana < mana)
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

    /// <summary>Keeps out of melee, stays near the employer, and casts when mana allows.</summary>
    public override bool DoActionCombat()
    {
        var target = Mobile.Combatant;

        if (!IsValidFocusMob(target))
        {
            Mobile.Combatant = null;
            return true;
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

        // Keep a quarter of the mana pool for healing.
        if (Mobile.Spell == null && Mobile.Mana > Mobile.ManaMax / 4 && Mobile.InRange(target, 10) && Mobile.InLOS(target))
        {
            var spell = ChooseAttack(target);

            if (spell != null)
            {
                CastAt(spell, target);
            }
        }

        return true;
    }

    private Spell ChooseAttack(Mobile target)
    {
        var magery = Magery;
        var mana = Mobile.Mana;

        if (magery >= 50.0 && mana >= 9 && !target.Poisoned && target.Hits > target.HitsMax / 2 && Utility.RandomDouble() < 0.2)
        {
            return new PoisonSpell(Mobile);
        }

        return magery switch
        {
            >= 90.0 when mana >= 40 && Utility.RandomDouble() < 0.3 => new FlameStrikeSpell(Mobile),
            >= 80.0 when mana >= 20                                 => new EnergyBoltSpell(Mobile),
            >= 60.0 when mana >= 14 && target.Int < Mobile.Int      => new MindBlastSpell(Mobile),
            >= 40.0 when mana >= 11                                 => new LightningSpell(Mobile),
            >= 20.0 when mana >= 9                                  => new FireballSpell(Mobile),
            _ when mana >= 4                                        => new MagicArrowSpell(Mobile),
            _                                                       => null
        };
    }

    // ---------- Gates ----------

    private bool TryEscapeGate()
    {
        if (_gateRefused || Party.ActiveGate != null)
        {
            return false;
        }

        if (OpenGate(true))
        {
            return true;
        }

        _gateRefused = true;
        Companion.Speak(SpeechContext.NoGate, urgent: true);
        return false;
    }

    private bool OpenGate(bool escape)
    {
        if (_gateDeadline != 0 || Party.ActiveGate != null || Magery < GateSkill || Mobile.Mana < 40 ||
            !FindGateDestination(out var location, out var map, out var name) || !CanGate(location, map))
        {
            return false;
        }

        var spell = new GateTravelSpell(Mobile, new RunebookEntry(null, location, map, name));

        if (!CastAt(spell, null))
        {
            return false;
        }

        _gateDeadline = Core.TickCount + GateWaitMs;
        _gateIsEscape = escape;
        _gateDestination = name;
        return true;
    }

    /// <summary>Once the spell lands, the gate at our feet becomes the party's way out.</summary>
    private void CheckGateOpened()
    {
        foreach (var item in Mobile.Map.GetItemsInRange<GateTravelMoongate>(Mobile.Location, 1))
        {
            _gateDeadline = 0;
            Party.ActiveGate = item;
            Companion.Speak(_gateIsEscape ? SpeechContext.GateEscape : SpeechContext.GateTown, _gateDestination, urgent: true);
            return;
        }

        if (Core.TickCount - _gateDeadline >= 0)
        {
            _gateDeadline = 0;

            if (_gateIsEscape)
            {
                _gateRefused = true;
                Companion.Speak(SpeechContext.NoGate, urgent: true);
            }
        }
    }

    /// <summary>
    /// A marked rune the employer has given the mage takes priority. Otherwise the nearest public
    /// moongate on this facet, landing a couple of tiles off it.
    /// </summary>
    private bool FindGateDestination(out Point3D location, out Map map, out string name)
    {
        map = Mobile.Map;
        name = null;
        location = Point3D.Zero;

        var pack = Mobile.Backpack;

        if (pack != null)
        {
            foreach (var item in pack.Items)
            {
                if (item is RecallRune { Marked: true } rune && rune.TargetMap == map)
                {
                    location = rune.Target;
                    name = string.IsNullOrEmpty(rune.Description) ? "home" : rune.Description;
                    return true;
                }
            }
        }

        var list = map == Map.Trammel ? PMList.Trammel :
            map == Map.Felucca ? PMList.Felucca :
            map == Map.Ilshenar ? PMList.Ilshenar : null;

        if (list == null)
        {
            return false;
        }

        PMEntry nearest = null;
        var nearestDistance = double.MaxValue;

        foreach (var entry in list.Entries)
        {
            var distance = Mobile.GetDistanceToSqrt(entry.Location);

            if (distance < nearestDistance)
            {
                nearest = entry;
                nearestDistance = distance;
            }
        }

        if (nearest == null || !FindStandingSpot(map, nearest.Location, out location))
        {
            return false;
        }

        name = Region.Find(nearest.Location, map)?.Name ?? "town";
        return true;
    }

    /// <summary>
    /// The checks Gate Travel makes when the spell lands, made before casting so a gate that
    /// would fail never burns reagents.
    /// </summary>
    private bool CanGate(Point3D location, Map map) =>
        map == Mobile.Map &&
        SpellHelper.CheckTravel(Mobile, TravelCheckType.GateFrom, out _) &&
        SpellHelper.CheckTravel(Mobile, map, location, TravelCheckType.GateTo, out _) &&
        !SpellHelper.CheckCombat(Mobile) &&
        map.CanSpawnMobile(location.X, location.Y, location.Z) &&
        !SpellHelper.CheckMulti(location, map);

    private static bool FindStandingSpot(Map map, Point3D center, out Point3D spot)
    {
        for (var radius = 2; radius <= 4; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var x = center.X + dx;
                    var y = center.Y + dy;
                    var z = map.GetAverageZ(x, y);

                    if (map.CanSpawnMobile(x, y, z))
                    {
                        spot = new Point3D(x, y, z);
                        return true;
                    }
                }
            }
        }

        spot = Point3D.Zero;
        return false;
    }
}
