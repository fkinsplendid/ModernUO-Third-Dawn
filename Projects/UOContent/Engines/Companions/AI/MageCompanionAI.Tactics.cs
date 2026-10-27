using System;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fifth;
using Server.Spells.Second;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;
using Server.Targeting;

namespace Server.Engines.Companions;

/// <summary>
/// What a Third Dawn mage does besides throwing damage: gets out from under melee (teleport
/// clear, or paralyze the attacker), keeps one defensive spell up, dispels summons, sets blade
/// spirits or an energy vortex on brutes (never with spellcasters about: they'd dispel them),
/// blocks a charging brute with a wall, catches a pack with a paralyze field, and casts Poison on
/// anyone not yet poisoned. Never Poison Field: the party kept blundering about around it.
/// </summary>
public sealed partial class MageCompanionAI
{
    private const int TeleportCooldownMs = 5000;
    private const int FieldCooldownMs = 6000;
    private const double MinDispelChance = 0.4;
    private const int PoisonReach = 6;
    private const int MaxPoisonAttempts = 2;
    private const int SummonCooldownMs = 8000;
    private const int MaxSummons = 2;


    private long _nextTeleport;
    private long _nextField;
    private long _nextSummon;
    private Mobile _poisonTarget;
    private int _poisonAttempts;

    // ---------- Defence ----------

    /// <summary>
    /// Gets out from under a melee attacker. Hurt or swarmed, teleports somewhere clear near the
    /// employer; otherwise paralyzes the attacker, unless the warrior is already dealing with it.
    /// </summary>
    private bool TryEscapeMelee()
    {
        var attacker = FindThreatAttacking(Mobile, 1);

        if (attacker == null)
        {
            return false;
        }

        var now = Core.TickCount;
        var cornered = Mobile.Hits < Mobile.HitsMax * 0.6 || CountAttackers(Mobile, 1) >= 2;

        if (cornered && Magery >= 30.0 && now - _nextTeleport >= 0 &&
            Affordable(new TeleportSpell(Mobile)) is { } teleport && FindSafeSpot(out var spot) &&
            CastAtLocation(teleport, spot))
        {
            _nextTeleport = now + TeleportCooldownMs;
            return true;
        }

        return Magery >= 50.0 && !attacker.Paralyzed && !IsWarriorOn(attacker) &&
               Affordable(new ParalyzeSpell(Mobile)) is { } paralyze && CastAt(paralyze, attacker);
    }

    /// <summary>Falling back with something on its heels, the mage teleports clear rather than outrunning it.</summary>
    protected override bool TryRetreatMove()
    {
        var now = Core.TickCount;

        if (Magery < 30.0 || now - _nextTeleport < 0 || !IsCloselyChased())
        {
            return false;
        }

        if (Affordable(new TeleportSpell(Mobile)) is { } teleport && FindSafeSpot(out var spot) &&
            CastAtLocation(teleport, spot))
        {
            _nextTeleport = now + TeleportCooldownMs;
            return true;
        }

        return false;
    }

    private bool IsCloselyChased()
    {
        for (var i = 0; i < Threats.Count; i++)
        {
            var threat = Threats[i];

            if (!threat.Paralyzed && !threat.Frozen && Mobile.InRange(threat, 2))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One defensive spell at a time, as pre-AOS rules allow: magic reflection against casters,
    /// otherwise protection while being hit so spells aren't knocked out of the hands.
    /// </summary>
    private bool TryShield()
    {
        if (Threats.Count == 0 || !Mobile.CanBeginAction<DefensiveSpell>() || Mobile.MagicDamageAbsorb > 0 ||
            Mobile.MeleeDamageAbsorb > 0 || ProtectionSpell.Registry.ContainsKey(Mobile))
        {
            return false;
        }

        Spell shield = null;

        if (Magery >= 50.0 && FacesCasters())
        {
            shield = Affordable(new MagicReflectSpell(Mobile));
        }
        else if (Magery >= 20.0 && FindThreatAttacking(Mobile, 1) != null)
        {
            shield = Affordable(new ProtectionSpell(Mobile));
        }

        return shield != null && CastAt(shield, null);
    }

    private bool FacesCasters()
    {
        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i].Skills.Magery.Value >= 40.0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A clear tile within teleport range, well away from every threat and close to the employer.</summary>
    private bool FindSafeSpot(out Point3D spot)
    {
        var map = Mobile.Map;
        var employer = Employer;
        var anchor = employer?.Map == map && Mobile.InRange(employer, 10) ? employer.Location : Mobile.Location;

        spot = default;
        var bestScore = double.MinValue;

        for (var d = 0; d < 8; d++)
        {
            foreach (var (from, distance) in (ReadOnlySpan<(Point3D, int)>)[(Mobile.Location, 6), (Mobile.Location, 4), (anchor, 2)])
            {
                var x = from.X;
                var y = from.Y;
                Movement.Movement.Offset((Direction)d, ref x, ref y, distance);

                var land = new LandTarget(new Point3D(x, y, 0), map);
                var candidate = land.Location;

                if (!Mobile.InRange(candidate, 10) || !map.CanSpawnMobile(x, y, land.Z) || !Mobile.InLOS(land) ||
                    SpellHelper.CheckMulti(candidate, map) || IsFieldAt(candidate))
                {
                    continue;
                }

                var nearestThreat = int.MaxValue;

                for (var i = 0; i < Threats.Count; i++)
                {
                    nearestThreat = Math.Min(nearestThreat, Distance(candidate, Threats[i].Location));
                }

                if (nearestThreat < 4)
                {
                    continue;
                }

                var score = Math.Min(nearestThreat, 8) * 2.0 - Distance(candidate, anchor) * 0.5;

                if (score > bestScore)
                {
                    spot = candidate;
                    bestScore = score;
                }
            }
        }

        return bestScore > double.MinValue;
    }

    // ---------- Battlefield ----------

    /// <summary>Dispels, fields and poison, in that order. Only with mana to spare beyond healing.</summary>
    private bool TryTactics(Mobile target)
    {
        if (Mobile.Mana <= Mobile.ManaMax / 4)
        {
            return false;
        }

        var now = Core.TickCount;
        var fieldReady = now - _nextField >= 0;

        if (TryDispel())
        {
            return true;
        }

        if (now - _nextSummon >= 0 && TrySummon())
        {
            _nextSummon = now + SummonCooldownMs;
            return true;
        }

        if (fieldReady && (TryWall() || TryParalyzeField()))
        {
            _nextField = now + FieldCooldownMs;
            return true;
        }

        return TryPoison(target);
    }

    /// <summary>Unmakes an enemy summon it has a fair chance against; several together, all at once.</summary>
    private bool TryDispel()
    {
        if (Magery < 65.0)
        {
            return false;
        }

        BaseCreature best = null;
        var bestChance = MinDispelChance;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is BaseCreature { IsDispellable: true } summon && InSpellRange(summon))
            {
                var chance = DispelChance(summon);

                if (chance >= bestChance)
                {
                    best = summon;
                    bestChance = chance;
                }
            }
        }

        if (best == null)
        {
            return false;
        }

        if (Magery >= 75.0 && CountSummonsNear(best.Location, 8) >= 2 &&
            Affordable(new MassDispelSpell(Mobile)) is { } massDispel)
        {
            return CastAtLocation(massDispel, best.Location);
        }

        return Affordable(new DispelSpell(Mobile)) is { } dispel && CastAt(dispel, best);
    }

    /// <summary>
    /// Brutes and no spellcasters: blades (or a vortex) beside the strongest brute in reach. They
    /// pick their own targets, favouring the strongest thing near them. The cast is long, so only
    /// when nothing is hitting the mage.
    /// </summary>
    private bool TrySummon()
    {
        if (BruteCount == 0 || CasterCount > 0 || CountSummons() >= MaxSummons ||
            Mobile.Followers + 1 > Mobile.FollowersMax || FindThreatAttacking(Mobile, 1) != null)
        {
            return false;
        }

        Mobile brute = null;

        for (var i = 0; i < Threats.Count; i++)
        {
            var threat = Threats[i];

            if (IsBrute(threat) && InSpellRange(threat) && (brute == null || threat.Hits > brute.Hits))
            {
                brute = threat;
            }
        }

        if (brute == null || !FindSummonSpot(brute, out var spot))
        {
            return false;
        }

        var spell = Confident(new EnergyVortexSpell(Mobile)) ?? Affordable(new BladeSpiritsSpell(Mobile));

        if (spell == null || !CastAtLocation(spell, spot))
        {
            return false;
        }

        if (Utility.RandomDouble() < 0.5)
        {
            Companion.Speak(SpeechContext.Summoning);
        }

        return true;
    }

    private int CountSummons()
    {
        var count = 0;

        foreach (var m in Mobile.Map.GetMobilesInRange(Mobile.Location, SpellRange + 8))
        {
            if (m is BaseCreature { Summoned: true } summon && summon.SummonMaster == Mobile &&
                summon is BladeSpirits or EnergyVortex)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>A clear tile beside <paramref name="brute"/> that the mage can see, nearest the mage.</summary>
    private bool FindSummonSpot(Mobile brute, out Point3D spot)
    {
        var map = Mobile.Map;
        spot = default;
        var best = int.MaxValue;

        for (var d = 0; d < 8; d++)
        {
            var x = brute.X;
            var y = brute.Y;
            Movement.Movement.Offset((Direction)d, ref x, ref y);

            var land = new LandTarget(new Point3D(x, y, 0), map);

            if (!map.CanSpawnMobile(x, y, land.Z) || !Mobile.InRange(land.Location, SpellRange) || !Mobile.InLOS(land))
            {
                continue;
            }

            var distance = Distance(Mobile.Location, land.Location);

            if (distance < best)
            {
                spot = land.Location;
                best = distance;
            }
        }

        return best != int.MaxValue;
    }

    /// <summary>The same odds the dispel spells roll.</summary>
    internal double DispelChance(BaseCreature summon) =>
        Math.Clamp((50.0 + 100 * (Magery - summon.DispelDifficulty) / (summon.DispelFocus * 2)) / 100, 0.0, 1.0);

    private int CountSummonsNear(Point3D location, int range)
    {
        var count = 0;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i] is BaseCreature { IsDispellable: true } summon && summon.InRange(location, range))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A brute charging the mage with nobody to stop it gets a wall in its face: energy field
    /// for a skilled mage, wall of stone otherwise. Never where it would cut the party off.
    /// </summary>
    private bool TryWall()
    {
        if (Magery < 30.0)
        {
            return false;
        }

        var energy = Knows<EnergyFieldSpell>();
        var half = energy ? 2 : 1;

        for (var i = 0; i < Threats.Count; i++)
        {
            var brute = Threats[i];
            var distance = Distance(Mobile.Location, brute.Location);

            if (brute.Combatant != Mobile || distance is < 3 or > 6 || IsWarriorOn(brute) || IsCaster(brute) ||
                !InSpellRange(brute))
            {
                continue;
            }

            var center = Step(Mobile.Location, brute.Location, 2);

            if (!FieldIsClear(center, half, 1, true))
            {
                continue;
            }

            var wall = energy ? Affordable(new EnergyFieldSpell(Mobile)) : Affordable(new WallOfStoneSpell(Mobile));

            return wall != null && CastAtLocation(wall, center);
        }

        return false;
    }

    /// <summary>A pack closing in that nobody has engaged yet walks into a paralyze field.</summary>
    private bool TryParalyzeField()
    {
        if (Magery < 65.0 || !FindCluster(IsApproaching, out var seed))
        {
            return false;
        }

        var center = Step(seed.Location, Mobile.Location, 1);

        return FieldIsClear(center, 2, -1, true) && Affordable(new ParalyzeFieldSpell(Mobile)) is { } field &&
               CastAtLocation(field, center);
    }

    /// <summary>
    /// Poisons the current target, or the nearest enemy in reach who isn't poisoned yet. Potency falls
    /// away past three tiles, so nearer is better. A target that resists twice is left alone.
    /// </summary>
    private bool TryPoison(Mobile target)
    {
        if (Magery < 30.0)
        {
            return false;
        }

        // It took: the count of resisted attempts starts over.
        if (_poisonTarget?.Poisoned == true)
        {
            _poisonTarget = null;
            _poisonAttempts = 0;
        }

        var victim = IsPoisonable(target) && Mobile.InRange(target, PoisonReach) && InSpellRange(target) ? target : null;

        if (victim == null)
        {
            var nearest = double.MaxValue;

            for (var i = 0; i < Threats.Count; i++)
            {
                var threat = Threats[i];
                var distance = Mobile.GetDistanceToSqrt(threat);

                if (distance <= PoisonReach && distance < nearest && IsPoisonable(threat) && InSpellRange(threat))
                {
                    victim = threat;
                    nearest = distance;
                }
            }
        }

        if (victim == null || Affordable(new PoisonSpell(Mobile)) is not { } poison || !CastAt(poison, victim))
        {
            return false;
        }

        if (victim != _poisonTarget)
        {
            _poisonTarget = victim;
            _poisonAttempts = 0;
        }

        _poisonAttempts++;
        return true;
    }

    internal bool IsPoisonable(Mobile m) =>
        m is { Alive: true, Poisoned: false, Paralyzed: false } && m is not BaseCreature { PoisonImmune: not null } &&
        m.Hits > m.HitsMax / 4 && (m != _poisonTarget || _poisonAttempts < MaxPoisonAttempts);

    private bool IsApproaching(Mobile m) =>
        !m.Paralyzed && !m.Frozen && Distance(Mobile.Location, m.Location) >= 4 && !IsEngaged(m);

    /// <summary>A threat with at least one other matching threat within two tiles, the biggest group first.</summary>
    private bool FindCluster(Func<Mobile, bool> matches, out Mobile seed)
    {
        seed = null;
        var best = 1;

        for (var i = 0; i < Threats.Count; i++)
        {
            var candidate = Threats[i];

            if (!matches(candidate) || !InSpellRange(candidate))
            {
                continue;
            }

            var count = 0;

            for (var j = 0; j < Threats.Count; j++)
            {
                if (matches(Threats[j]) && Distance(candidate.Location, Threats[j].Location) <= 2)
                {
                    count++;
                }
            }

            if (count > best)
            {
                seed = candidate;
                best = count;
            }
        }

        return seed != null;
    }

    /// <summary>
    /// The tiles a five-wide (or three-wide) field would cover, as the field spells lay them: across
    /// the line from the caster. No friend may be within <paramref name="friendClearance"/> tiles
    /// (fields that block, like walls, must not cut anyone off; -1 skips the check) and the mage
    /// must not be standing in or beside it.
    /// </summary>
    internal bool FieldIsClear(Point3D center, int half, int friendClearance, bool keepSelfClear)
    {
        var eastToWest = SpellHelper.GetEastToWest(Mobile.Location, center);
        var reach = half + Math.Max(friendClearance, 1);

        foreach (var m in Mobile.Map.GetMobilesInRange(center, reach))
        {
            int clearance;

            if (m == Mobile)
            {
                if (!keepSelfClear)
                {
                    continue;
                }

                clearance = 1;
            }
            else if (friendClearance >= 0 && Party.IsFriendly(m))
            {
                clearance = friendClearance;
            }
            else
            {
                continue;
            }

            for (var i = -half; i <= half; i++)
            {
                var tile = eastToWest ? new Point3D(center.X + i, center.Y, center.Z) : new Point3D(center.X, center.Y + i, center.Z);

                if (Distance(m.Location, tile) <= clearance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ---------- Helpers ----------

    private bool IsWarriorOn(Mobile enemy) =>
        Party[CompanionRole.Warrior] is { } warrior && CompanionParty.IsFighting(warrior) && warrior.Combatant == enemy &&
        warrior.InRange(enemy, 1);

    /// <summary>Someone in the party is already toe to toe with it.</summary>
    internal bool IsEngaged(Mobile enemy)
    {
        if (Employer is { Alive: true } employer && employer.InRange(enemy, 1))
        {
            return true;
        }

        foreach (var member in Party.Members)
        {
            if (CompanionParty.IsFighting(member) && member.InRange(enemy, 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCaster(Mobile m) => m.Skills.Magery.Value >= 40.0 || m is BaseCreature { RangeFight: > 2 };

    private int CountAttackers(Mobile victim, int range)
    {
        var count = 0;

        for (var i = 0; i < Threats.Count; i++)
        {
            if (Threats[i].Combatant == victim && Threats[i].InRange(victim, range))
            {
                count++;
            }
        }

        return count;
    }

    private static Point3D Step(Point3D from, Point3D towards, int steps)
    {
        var x = from.X;
        var y = from.Y;
        Movement.Movement.Offset(Utility.GetDirection(from, towards), ref x, ref y, steps);
        return new Point3D(x, y, from.Z);
    }

    private static int Distance(Point3D a, Point3D b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
