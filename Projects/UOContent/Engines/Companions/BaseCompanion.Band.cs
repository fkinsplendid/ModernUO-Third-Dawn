using System;
using Server.Engines.Bands;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Band mode: a companion serving an adventuring band's captain instead of a player. The fighting,
/// healing and talk are the same; the hiring is gone. No wages, no regard, no levelling, no orders,
/// no gates or smith trips, nothing handed over. What they loot they keep out of the world, and
/// the band restocks its own supplies.
/// </summary>
public abstract partial class BaseCompanion
{
    /// <summary>Serving a band captain rather than a player.</summary>
    public bool InBand => Employer is BandCaptain;

    /// <summary>Joins <paramref name="captain"/>'s band and steps into the world beside them.</summary>
    public bool JoinBand(BandCaptain captain)
    {
        Employer = captain;
        Team = captain.Team;

        if (!AttachToEmployer())
        {
            Employer = null;
            return false;
        }

        State = CompanionState.Active;
        CompanionSystem.Register(this);
        MoveToWorld(captain.Location, captain.Map);
        return true;
    }

    // Potions each band member always has to hand.
    private const int BandHealPotions = 2;
    private const int BandCurePotions = 1;

    /// <summary>Called once a minute in place of the wage clock.</summary>
    private void OnBandMinute() => Loyalty = MaxLoyalty;

    /// <summary>
    /// A band never runs dry: called every few seconds, in a fight or out of one, it puts back
    /// whatever reagents, bandages and potions were used since.
    /// </summary>
    public void RestockForBand()
    {
        if (IsDeadPet)
        {
            return;
        }

        RestockRoleSupplies();
        TopUpPotions<HealPotion>(BandHealPotions);
        TopUpPotions<CurePotion>(BandCurePotions);
    }

    /// <summary>The role's own supplies: bandages, reagents.</summary>
    protected virtual void RestockRoleSupplies()
    {
    }

    /// <summary>Potions don't stack in this era, so they are counted one bottle at a time.</summary>
    private void TopUpPotions<T>(int count) where T : BasePotion, new()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return;
        }

        for (var have = pack.GetAmount(typeof(T)); have < count; have++)
        {
            PackItem(new T());
        }
    }

    /// <summary>Brings a supply in the pack back up to <paramref name="amount"/>.</summary>
    protected void TopUp(Type type, int amount, Func<int, Item> create)
    {
        var pack = Backpack;

        if (pack == null)
        {
            return;
        }

        var missing = amount - pack.GetAmount(type);

        if (missing > 0)
        {
            PackItem(create(missing));
        }
    }

    /// <summary>
    /// Absorbs one item from a band kill: picked up and gone, so nothing piles up in the world.
    /// A corpse a player also fought over is left alone for them. False once nothing is left.
    /// </summary>
    private bool AbsorbOneItem(Corpse corpse)
    {
        if (BandRules.HasPlayerClaim(corpse))
        {
            return false;
        }

        var items = corpse.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (WantsLoot(item))
            {
                item.Delete();
                PlaySound(item is Gold ? 0x2E6 : 0x48);
                return true;
            }
        }

        return false;
    }

    /// <summary>A stray blow from a bandmate is shrugged off, never answered.</summary>
    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        if (!BandRules.AreBandmates(this, aggressor))
        {
            base.AggressiveAction(aggressor, criminal);
        }
    }

    /// <summary>
    /// Undoes an order to attack a bandmate, and drops a bandmate as a combatant. Bands from before
    /// bandmates were protected can still carry one in the save.
    /// </summary>
    internal void ForgiveBandmates()
    {
        if (ControlOrder == OrderType.Attack && BandRules.AreBandmates(this, ControlTarget))
        {
            ControlTarget = Employer;
            ControlOrder = OrderType.Follow;
        }

        if (BandRules.AreBandmates(this, Combatant))
        {
            Combatant = null;
        }
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        base.AlterMeleeDamageFrom(from, ref damage);

        if (InBand)
        {
            BandRules.ScaleTaken(from, ref damage);
        }
    }

    public override void AlterMeleeDamageTo(Mobile to, ref int damage)
    {
        base.AlterMeleeDamageTo(to, ref damage);

        if (InBand)
        {
            BandRules.ScaleDealt(to, ref damage);
        }
    }

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        base.AlterSpellDamageFrom(from, ref damage);

        if (InBand)
        {
            BandRules.ScaleTaken(from, ref damage);
        }
    }

    public override void AlterSpellDamageTo(Mobile to, ref int damage)
    {
        base.AlterSpellDamageTo(to, ref damage);

        if (InBand)
        {
            BandRules.ScaleDealt(to, ref damage);
        }
    }
}
