using System;
using Server.Collections;
using Server.Items;

namespace Server.Engines.Companions;

/// <summary>
/// Companions keep themselves equipped with the best gear they own. Each role scores weapons and
/// armour its own way (a mage refuses plate, a bard refuses swords), and the hands are solved as a
/// whole so a two-hander is weighed against a one-hander plus shield.
/// </summary>
public abstract partial class BaseCompanion
{
    private const double UpgradeMargin = 0.5;
    private const long GearRecheckMs = 60000;

    private long _nextGearCheck;

    public bool GearDirty { get; set; } = true;

    /// <summary>Value of a weapon to this companion. Zero or less means it won't use it.</summary>
    public virtual double ScoreWeapon(BaseWeapon weapon)
    {
        if (weapon is BaseRanged || Str < weapon.StrRequirement)
        {
            return -1;
        }

        var skill = Skills[weapon.Skill].Value;

        if (skill <= 0)
        {
            return -1;
        }

        var damageLevel = (int)weapon.DamageLevel;
        var average = (weapon.MinDamage + weapon.MaxDamage) * 0.5 + (damageLevel > 0 ? damageLevel * 2 - 1 : 0);
        var accuracy = 1.0 + (int)weapon.AccuracyLevel * 0.04;
        var quality = weapon.Quality switch
        {
            WeaponQuality.Exceptional => 1.1,
            WeaponQuality.Low         => 0.9,
            _                         => 1.0
        };

        // Before ML, Speed is a rating (higher is faster); from ML on it is seconds per swing.
        var pace = Core.ML ? 10.0 / Math.Max(weapon.Speed, 0.25) : weapon.Speed / 10.0;

        return average * pace * accuracy * quality * (0.3 + 0.7 * skill / 100.0);
    }

    /// <summary>Value of a piece of armour (or a shield) to this companion.</summary>
    public virtual double ScoreArmor(BaseArmor armor) => Str < armor.StrRequirement ? -1 : armor.ArmorRating;

    /// <summary>How much a point of shield armour is worth against a point of weapon damage.</summary>
    protected virtual double ShieldWeight => 1.5;

    private double ScoreItem(Item item) =>
        item switch
        {
            BaseWeapon w => ScoreWeapon(w),
            BaseArmor a  => ScoreArmor(a),
            _            => 0
        };

    /// <summary>True if this item would be an upgrade for this companion right now.</summary>
    public bool IsUpgrade(Item item)
    {
        var score = ScoreItem(item);

        if (score <= 0)
        {
            return false;
        }

        var current = CurrentHandsScore();

        return item switch
        {
            BaseShield               => WornOneHandedScore() + score * ShieldWeight > current + UpgradeMargin,
            BaseWeapon { Layer: Layer.TwoHanded } => score > current + UpgradeMargin,
            BaseWeapon               => score + WornShieldScore() > current + UpgradeMargin,
            _                        => score > ScoreItem(FindItemOnLayer(item.Layer)) + UpgradeMargin
        };
    }

    /// <summary>
    /// Why this companion would turn down a weapon or piece of armour handed to them, or null if
    /// they'd wear it. Anything else they took would only be passed straight back with the loot.
    /// </summary>
    public string GearRefusal(Item item)
    {
        if (item is not (BaseWeapon or BaseArmor) || IsUpgrade(item) || item is BaseWeapon spare && IsBetterSpare(spare))
        {
            return null;
        }

        var ally = FindAllyToWear(item);
        var reason = item switch
        {
            BaseWeapon { StrRequirement: var str } when Str < str => "That's too heavy for me to swing.",
            BaseArmor { StrRequirement: var str } when Str < str  => "That's too heavy for me to wear.",
            BaseWeapon w when ScoreWeapon(w) <= 0                 => WeaponRefusal(w),
            BaseArmor a when ScoreArmor(a) <= 0                   => "I can't fight in that.",
            BaseWeapon                                            => "I've a spare as good as that already. Keep it.",
            _                                                     => "What I have is as good as that. Keep it."
        };

        return ally != null ? $"{reason} {ally.Name} could use it, though." : reason;
    }

    /// <summary>
    /// The best weapon in the pack this companion can fight with, kept back from sharing in case
    /// the one in hand breaks (or is away at the smith).
    /// </summary>
    public BaseWeapon FindSpareWeapon()
    {
        BaseWeapon spare = null;
        var best = 0.0;

        if (Backpack is not { } pack)
        {
            return null;
        }

        foreach (var item in pack.Items)
        {
            if (item is BaseWeapon weapon && ScoreWeapon(weapon) is var score && score > best)
            {
                spare = weapon;
                best = score;
            }
        }

        return spare;
    }

    private bool IsBetterSpare(BaseWeapon weapon)
    {
        var score = ScoreWeapon(weapon);
        return score > 0 && (FindSpareWeapon() is not { } spare || score > ScoreWeapon(spare));
    }

    /// <summary>A weapon left the hands (broke, or went to the smith): draw the spare straight away, fight or no fight.</summary>
    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);

        if (item is BaseWeapon && item.Layer is Layer.OneHanded or Layer.TwoHanded)
        {
            Timer.DelayCall(DrawSpare);
        }
    }

    private void DrawSpare()
    {
        if (Deleted || IsDeadPet || Map == Map.Internal || FindItemOnLayer(Layer.TwoHanded) is BaseWeapon ||
            FindItemOnLayer(Layer.OneHanded) is BaseWeapon)
        {
            return;
        }

        if (EquipBestHands())
        {
            Speak(SpeechContext.Equip, urgent: true);
        }
    }

    private string WeaponRefusal(BaseWeapon weapon) =>
        Role == CompanionRole.Mage
            ? "My hands are for spellcraft, not steel."
            : weapon switch
            {
                BaseRanged                            => "I was never any good with a bow.",
                _ when weapon.Skill == SkillName.Swords  => "I was never trained with a blade.",
                _ when weapon.Skill == SkillName.Macing  => "I was never trained with a mace.",
                _ when weapon.Skill == SkillName.Fencing => "I was never trained with a spear or a dagger.",
                _                                        => "I wouldn't know what to do with that."
            };

    private BaseCompanion FindAllyToWear(Item item)
    {
        if (Party == null)
        {
            return null;
        }

        foreach (var ally in Party.Members)
        {
            if (ally != this && ally is { IsDeadPet: false } && ally.IsUpgrade(item))
            {
                return ally;
            }
        }

        return null;
    }

    private double WornOneHandedScore() => FindItemOnLayer(Layer.OneHanded) is BaseWeapon w ? ScoreWeapon(w) : 0;

    private double WornShieldScore() =>
        FindItemOnLayer(Layer.TwoHanded) is BaseShield s ? Math.Max(0, ScoreArmor(s)) * ShieldWeight : 0;

    private double CurrentHandsScore() =>
        FindItemOnLayer(Layer.TwoHanded) is BaseWeapon two
            ? ScoreWeapon(two)
            : WornOneHandedScore() + WornShieldScore();

    internal void CheckGear(long now)
    {
        if (!GearDirty && now - _nextGearCheck < 0)
        {
            return;
        }

        GearDirty = false;
        _nextGearCheck = now + GearRecheckMs;

        var changed = EquipBestArmor() | EquipBestHands();

        if (changed)
        {
            Speak(SpeechContext.Equip);
        }
    }

    private bool EquipBestArmor()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return false;
        }

        using var candidates = PooledRefList<BaseArmor>.Create();

        foreach (var item in pack.Items)
        {
            if (item is BaseArmor armor and not BaseShield)
            {
                candidates.Add(armor);
            }
        }

        var changed = false;

        // Greedy per layer: each candidate is compared with whatever is worn at that moment,
        // so the best piece for every layer wins regardless of order.
        for (var i = 0; i < candidates.Count; i++)
        {
            var armor = candidates[i];
            var score = ScoreArmor(armor);

            if (score <= 0)
            {
                continue;
            }

            var worn = FindItemOnLayer(armor.Layer);

            if (score > ScoreItem(worn) + UpgradeMargin && Swap(armor, worn))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Compares the best one-hander plus shield against the best two-hander, counting what is
    /// already worn, and re-dresses the hands only if the winner beats the current setup.
    /// </summary>
    private bool EquipBestHands()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return false;
        }

        BaseWeapon best1 = null, best2 = null;
        BaseShield bestShield = null;
        double score1 = 0, score2 = 0, scoreShield = 0;

        Consider(FindItemOnLayer(Layer.OneHanded));
        Consider(FindItemOnLayer(Layer.TwoHanded));

        foreach (var item in pack.Items)
        {
            Consider(item);
        }

        var current = CurrentHandsScore();

        if (score2 > score1 + scoreShield)
        {
            return score2 > current + UpgradeMargin && EquipHands(best2, null);
        }

        return score1 + scoreShield > current + UpgradeMargin && EquipHands(best1, bestShield);

        void Consider(Item item)
        {
            switch (item)
            {
                case BaseShield shield:
                    {
                        var s = ScoreArmor(shield) * ShieldWeight;

                        if (s > scoreShield)
                        {
                            bestShield = shield;
                            scoreShield = s;
                        }

                        break;
                    }
                case BaseWeapon { Layer: Layer.TwoHanded } w:
                    {
                        var s = ScoreWeapon(w);

                        if (s > score2)
                        {
                            best2 = w;
                            score2 = s;
                        }

                        break;
                    }
                case BaseWeapon w:
                    {
                        var s = ScoreWeapon(w);

                        if (s > score1)
                        {
                            best1 = w;
                            score1 = s;
                        }

                        break;
                    }
            }
        }
    }

    /// <summary>
    /// Re-dresses both hands. Displaced items go back into the pack. A null slot only clears what is
    /// there if it is a weapon or shield, so a mage's spellbook stays in hand.
    /// </summary>
    private bool EquipHands(BaseWeapon weapon, BaseShield shield)
    {
        var worn1 = FindItemOnLayer(Layer.OneHanded);
        var worn2 = FindItemOnLayer(Layer.TwoHanded);
        var twoHanded = weapon?.Layer == Layer.TwoHanded;

        Item want1 = twoHanded ? null : weapon;
        Item want2 = twoHanded ? weapon : shield;

        if (worn1 != want1 && worn1 != null && (want1 != null || twoHanded || worn1 is BaseWeapon))
        {
            StowInPack(worn1);
        }

        if (worn2 != want2 && worn2 != null && (want2 != null || worn2 is BaseWeapon or BaseShield))
        {
            StowInPack(worn2);
        }

        var changed = false;

        if (want1 != null && want1.Parent != this)
        {
            changed |= EquipItem(want1);
        }

        if (want2 != null && want2.Parent != this)
        {
            changed |= EquipItem(want2);
        }

        return changed;
    }

    /// <summary>
    /// Takes off a worn item. Guild-issued gear goes back to the guild rather than cluttering the
    /// pack; everything else goes in the pack regardless of its limits, never on the ground.
    /// </summary>
    private void StowInPack(Item worn)
    {
        if (IsIssued(worn))
        {
            worn.Delete();
            return;
        }

        if (Backpack is { } pack)
        {
            pack.DropItem(worn);
        }
    }

    private bool Swap(Item replacement, Item worn)
    {
        if (worn != null)
        {
            StowInPack(worn);
        }

        if (EquipItem(replacement))
        {
            return true;
        }

        if (worn?.Deleted == false)
        {
            EquipItem(worn);
        }

        return false;
    }
}
