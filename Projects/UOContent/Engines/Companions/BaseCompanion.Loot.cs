using System;
using System.Collections.Generic;
using Server.Collections;
using Server.Items;

namespace Server.Engines.Companions;

/// <summary>
/// Looting and sharing. Companions only search corpses of creatures their party killed, take
/// what is worth carrying, keep the supplies they use, pass gear to whichever ally it suits,
/// take a cut of the gold towards their wages, and hand the rest to their employer (or bank it).
/// </summary>
public abstract partial class BaseCompanion
{
    private const int MaxGoldStack = 60000;
    private const double LadenRatio = 0.85;

    private static readonly HashSet<Type> _gemTypes =
    [
        typeof(Amber), typeof(Amethyst), typeof(Citrine), typeof(Diamond), typeof(Emerald),
        typeof(Ruby), typeof(Sapphire), typeof(StarSapphire), typeof(Tourmaline)
    ];

    private readonly long[] _supplyCallouts = new long[4];
    private long _nextUpkeep;

    public int CarryCapacity => 40 + (int)(3.5 * Str);

    public bool IsLaden => TotalWeight >= CarryCapacity * LadenRatio;

    public bool CanCarry(Item item) => TotalWeight + item.TotalWeight + item.PileWeight <= CarryCapacity;

    /// <summary>Things this companion uses up itself. Kept, never shared.</summary>
    protected virtual bool IsSupply(Item item) => item is BasePotion potion && Drinks(potion) && !IsLooted(potion);

    /// <summary>The potions a companion knows when to drink.</summary>
    public static bool Drinks(BasePotion potion) =>
        potion is BaseHealPotion or BaseCurePotion or BaseRefreshPotion or BaseStrengthPotion or BaseAgilityPotion;

    /// <summary>True for a potion picked up off a corpse: loot for the employer, not for drinking.</summary>
    public bool IsLooted(Item item) => _lootedPotions?.Contains(item) == true;

    /// <summary>Remembers a potion just taken off a corpse as loot.</summary>
    internal void NoteLooted(Item item)
    {
        // Deleted means it stacked onto a potion of their own.
        if (item is not BasePotion || item.Deleted)
        {
            return;
        }

        PruneLooted();
        (_lootedPotions ??= []).Add(item);
        this.MarkDirty();
    }

    /// <summary>Stops treating <paramref name="item"/>, or the potions in it, as loot.</summary>
    private void ForgetLooted(Item item)
    {
        if (_lootedPotions == null || _lootedPotions.Count == 0)
        {
            return;
        }

        var forgot = _lootedPotions.Remove(item);

        if (item is Container bag)
        {
            foreach (var potion in bag.FindItemsByType<BasePotion>())
            {
                forgot |= _lootedPotions.Remove(potion);
            }
        }

        if (forgot)
        {
            this.MarkDirty();
        }
    }

    /// <summary>Drops looted potions that have left the pack (shared, taken, or used up).</summary>
    private void PruneLooted()
    {
        if (_lootedPotions == null)
        {
            return;
        }

        var pack = Backpack;

        if (_lootedPotions.RemoveAll(item => item.Deleted || pack == null || !item.IsChildOf(pack)) > 0)
        {
            this.MarkDirty();
        }
    }

    /// <summary>
    /// True for this companion's own supplies, and for a bag holding nothing else
    /// (a bag of reagents handed to the mage stays with the mage).
    /// </summary>
    public bool IsOwnSupply(Item item)
    {
        if (item is not Container bag)
        {
            return IsSupply(item);
        }

        if (bag.Items.Count == 0)
        {
            return false;
        }

        foreach (var content in bag.Items)
        {
            if (!IsSupply(content))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Calls out for supplies that are running low. Called every few seconds.</summary>
    protected abstract void CheckSupplies();

    private void ThinkUpkeep()
    {
        var now = Core.TickCount;

        ThinkChatter(now);

        if (now - _nextUpkeep < 0)
        {
            return;
        }

        _nextUpkeep = now + 5000;

        if (Combatant == null && Spell == null)
        {
            CheckGear(now);
        }

        ThinkMind(now);

        CheckSupplies();
        CheckSteed(now);
    }

    /// <summary>Announces a shortage at most every few minutes per supply slot.</summary>
    protected void ReportShortage(int slot, string supplyName, int have, int lowThreshold)
    {
        if (have >= lowThreshold)
        {
            return;
        }

        var now = Core.TickCount;

        if (now - _supplyCallouts[slot] < 0)
        {
            return;
        }

        var empty = have == 0;

        if (Speak(empty ? SpeechContext.OutOfSupplies : SpeechContext.LowSupplies, supplyName, urgent: empty))
        {
            _supplyCallouts[slot] = now + (empty ? 120000 : 300000);
        }
    }

    // ---------- Looting ----------

    public static bool IsMagic(Item item) =>
        item switch
        {
            BaseWeapon w => w.DamageLevel != WeaponDamageLevel.Regular || w.AccuracyLevel != WeaponAccuracyLevel.Regular ||
                            w.DurabilityLevel != WeaponDurabilityLevel.Regular || w.Slayer != SlayerName.None,
            BaseArmor a => a.ProtectionLevel != ArmorProtectionLevel.Regular ||
                           a.Durability != ArmorDurabilityLevel.Regular,
            _ => false
        };

    /// <summary>Whether an item on a corpse is worth picking up for this party.</summary>
    public bool WantsLoot(Item item)
    {
        if (!item.Movable || !item.Visible || item.LootType == LootType.Blessed || item is Container)
        {
            return false;
        }

        return item switch
        {
            Gold or BankCheck or BaseReagent or Bandage or BasePotion or SpellScroll or BaseJewel
                or BaseInstrument or TreasureMap or Arrow or Bolt => true,
            BaseWeapon or BaseArmor => IsMagic(item) || IsUpgradeForParty(item),
            _ => _gemTypes.Contains(item.GetType())
        };
    }

    private bool IsUpgradeForParty(Item item)
    {
        if (Party == null)
        {
            return IsUpgrade(item);
        }

        foreach (var ally in Party.Members)
        {
            if (CompanionParty.IsFighting(ally) && ally.IsUpgrade(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes one item from the corpse, as a person rummaging would. False once nothing is left worth taking.</summary>
    public bool LootOneItem(Corpse corpse)
    {
        var pack = Backpack;

        if (pack == null)
        {
            return false;
        }

        var items = corpse.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (!WantsLoot(item))
            {
                continue;
            }

            // Companions loot with their employer's rights: never someone else's kill, never a crime.
            if (Employer == null || corpse.IsCriminalAction(Employer) || !corpse.CanLoot(Employer, item))
            {
                return false;
            }

            if (!CanCarry(item))
            {
                Speak(SpeechContext.Laden);
                return false;
            }

            var amount = item.Amount;
            var isGold = item is Gold;

            if (!pack.TryDropItem(this, item, false))
            {
                continue;
            }

            NoteLooted(item);

            if (isGold)
            {
                Speak(SpeechContext.LootGold, amount.ToString());
            }
            else
            {
                if (item is BaseWeapon or BaseArmor)
                {
                    GearDirty = true;
                }

                if (IsMagic(item))
                {
                    OnTreasureFound();
                }

                if (Utility.RandomDouble() < 0.4)
                {
                    Speak(SpeechContext.LootFind);
                }
            }

            return true;
        }

        return false;
    }

    // ---------- Sharing ----------

    /// <summary>True if the pack holds anything that belongs with the employer or another companion.</summary>
    public bool HasShareables()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return false;
        }

        var spare = FindSpareWeapon();

        foreach (var item in pack.Items)
        {
            if (item != spare && !IsOwnSupply(item) && !IsUpgrade(item) && !IsIssued(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hands out the pack's contents: the companion's cut of the gold to their purse, supplies and
    /// gear to the ally who uses them, everything else to the employer (or their bank box).
    /// </summary>
    public bool ShareLoot(bool toBank)
    {
        var employer = Employer;
        var pack = Backpack;

        if (employer?.Backpack == null || pack == null)
        {
            return false;
        }

        var shared = ShareGold(employer, toBank);
        var refused = false;

        using var items = PooledRefList<Item>.Create();
        var spare = FindSpareWeapon();

        foreach (var item in pack.Items)
        {
            // Upgrades for ourselves are about to be worn, not given away; one spare weapon is kept.
            if (item is not Gold && item != spare && !IsOwnSupply(item) && !IsUpgrade(item) && !IsIssued(item))
            {
                items.Add(item);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // Looted potions are the employer's, even ones an ally could drink.
            var ally = item is BasePotion ? null : FindAllyFor(item);

            if (ally != null)
            {
                if (ally.Backpack?.TryDropItem(ally, item, false) == true)
                {
                    ally.GearDirty = true;
                }

                continue;
            }

            if (GiveToEmployer(employer, item, toBank))
            {
                shared = true;
            }
            else
            {
                refused = true;
            }
        }

        PruneLooted();

        if (shared)
        {
            Speak(toBank ? SpeechContext.Banked : SpeechContext.ShareLoot, urgent: true);
        }
        else if (refused)
        {
            Speak(SpeechContext.CannotCarry);
        }

        return shared;
    }

    /// <summary>
    /// Hands over the employer's portion of the gold and keeps the companion's cut, but only on
    /// what was actually handed over. Anything the employer can't take stays in the pack, uncut.
    /// </summary>
    private bool ShareGold(Mobile employer, bool toBank)
    {
        var pack = Backpack;
        var total = pack.GetAmount(typeof(Gold));

        if (total <= 0)
        {
            return false;
        }

        var share = CompanionSettings.GoldShare;
        var owed = total - (int)(total * share);
        var delivered = 0;

        while (delivered < owed)
        {
            var gold = new Gold(Math.Min(owed - delivered, MaxGoldStack));
            var amount = gold.Amount;

            if (!GiveToEmployer(employer, gold, toBank))
            {
                gold.Delete();
                break;
            }

            delivered += amount;
        }

        var cut = delivered == owed ? total - owed : (int)(delivered * share / (1.0 - share));

        if (delivered + cut > 0)
        {
            pack.ConsumeTotal(typeof(Gold), delivered + cut);
            Purse += cut;
        }

        return delivered > 0;
    }

    private bool GiveToEmployer(Mobile employer, Item item, bool toBank)
    {
        if (toBank)
        {
            return employer.BankBox.TryDropItem(employer, item, false);
        }

        if (employer.TotalWeight + item.TotalWeight + item.PileWeight > employer.MaxWeight)
        {
            return false;
        }

        return employer.Backpack.TryDropItem(employer, item, false);
    }

    /// <summary>The nearby ally who would use this item themselves, if any.</summary>
    private BaseCompanion FindAllyFor(Item item)
    {
        if (Party == null)
        {
            return null;
        }

        foreach (var ally in Party.Members)
        {
            if (ally == this || !CompanionParty.IsFighting(ally) || !InRange(ally, 3) || !ally.CanCarry(item))
            {
                continue;
            }

            if (ally.IsOwnSupply(item) || item is BaseWeapon or BaseArmor && ally.IsUpgrade(item))
            {
                return ally;
            }
        }

        return null;
    }
}
