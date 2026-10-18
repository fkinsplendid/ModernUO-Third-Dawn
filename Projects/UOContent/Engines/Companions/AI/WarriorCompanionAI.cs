using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Front line. Peels enemies off the mage and bard, taunts creatures away from them, rushes
/// spellcasters (a blow spoils a spell), and bandages the party between (and during) swings.
/// </summary>
public sealed class WarriorCompanionAI : CompanionAI
{
    private const int TauntCooldownMs = 10000;

    private long _nextTaunt;

    public WarriorCompanionAI(BaseCompanion companion) : base(companion)
    {
    }

    protected override bool TryRoleSupport() =>
        TryRevive() || TryBandage(Threats.Count > 0 ? 0.6 : 0.85) || TryTaunt();

    /// <summary>
    /// Whatever is hitting the backline first, then a spellcaster (staying on one already in hand),
    /// then the current target, then whatever is on the employer.
    /// </summary>
    protected override Mobile SelectTarget(bool holdPosition)
    {
        var range = holdPosition ? 2 : 12;

        var peel = FindThreatAttacking(Party[CompanionRole.Mage], range) ??
                   FindThreatAttacking(Party[CompanionRole.Bard], range);

        if (peel != null)
        {
            return peel;
        }

        var current = Mobile.Combatant;
        var currentValid = current != null && Threats.Contains(current) && (!holdPosition || Mobile.InRange(current, 2));

        if (CasterCount > 0 && !holdPosition && !(currentValid && IsSpellcaster(current)) &&
            FindNearestCaster(range) is { } caster)
        {
            return caster;
        }

        if (currentValid)
        {
            return current;
        }

        return FindThreatAttacking(Employer, range) ?? base.SelectTarget(holdPosition);
    }

    private Mobile FindNearestCaster(int range)
    {
        Mobile best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < Threats.Count; i++)
        {
            var threat = Threats[i];
            var distance = Mobile.GetDistanceToSqrt(threat);

            if (distance <= range && distance < bestDistance && IsSpellcaster(threat) && Mobile.InLOS(threat))
            {
                best = threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Draws a creature's attention away from a softer ally. Doesn't spend the tick.</summary>
    private bool TryTaunt()
    {
        var now = Core.TickCount;

        if (Threats.Count == 0 || now - _nextTaunt < 0)
        {
            return false;
        }

        var target = FindThreatAttacking(Party[CompanionRole.Mage], 8) ??
                     FindThreatAttacking(Party[CompanionRole.Bard], 8);

        if (target is not BaseCreature { Controlled: false } creature)
        {
            return false;
        }

        _nextTaunt = now + TauntCooldownMs;

        if (Mobile.Skills.Tactics.Value / 120.0 > Utility.RandomDouble())
        {
            Mobile.Emote($"*bellows a challenge at {creature.Name}*");
            creature.Combatant = Mobile;
            Mobile.Combatant = creature;
        }

        return false;
    }
}
