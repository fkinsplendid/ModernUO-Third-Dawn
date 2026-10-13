using System;
using System.Text;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

[SerializationGenerator(0)]
public partial class CompanionMage : BaseCompanion
{
    private const int LowReagents = 5;

    private static readonly CompanionProfile _profile = new()
    {
        Role = CompanionRole.Mage,
        Title = "the mage",
        Str = new StatCurve(55, 1.0),
        Dex = new StatCurve(40, 1.0),
        Int = new StatCurve(85, 1.5),
        BonusHitsPerLevel = 2,
        Skills =
        [
            new(SkillName.Magery, 10), SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist, SkillName.Wrestling
        ]
    };

    private static readonly (Type Type, string Name, Func<int, Item> Create)[] _reagents =
    [
        (typeof(BlackPearl), "black pearl", amount => new BlackPearl(amount)),
        (typeof(Bloodmoss), "blood moss", amount => new Bloodmoss(amount)),
        (typeof(Garlic), "garlic", amount => new Garlic(amount)),
        (typeof(Ginseng), "ginseng", amount => new Ginseng(amount)),
        (typeof(MandrakeRoot), "mandrake root", amount => new MandrakeRoot(amount)),
        (typeof(Nightshade), "nightshade", amount => new Nightshade(amount)),
        (typeof(SulfurousAsh), "sulfurous ash", amount => new SulfurousAsh(amount)),
        (typeof(SpidersSilk), "spiders' silk", amount => new SpidersSilk(amount))
    ];

    [Constructible]
    public CompanionMage()
    {
    }

    public override CompanionRole Role => CompanionRole.Mage;

    public override CompanionProfile Profile => _profile;

    protected override BaseAI CreateAI() => new MageCompanionAI(this);

    protected override bool IsSupply(Item item) => item is BaseReagent || base.IsSupply(item);

    /// <summary>Spells clear the hands, so no weapons; armour only if it allows meditation.</summary>
    public override double ScoreWeapon(BaseWeapon weapon) => -1;

    public override double ScoreArmor(BaseArmor armor) =>
        armor is BaseShield || armor.MeditationAllowance != ArmorMeditationAllowance.All ? -1 : base.ScoreArmor(armor);

    protected override void DressForRole()
    {
        AddItem(Issue(new Robe(Utility.RandomBlueHue())));
        AddItem(Issue(new WizardsHat(Utility.RandomBlueHue())));
        AddItem(Issue(new Sandals()));
        AddItem(Issue(new Spellbook(ulong.MaxValue) { LootType = LootType.Blessed }));

        foreach (var (_, _, create) in _reagents)
        {
            PackItem(create(30));
        }

        PackItem(new HealPotion());
        PackItem(new CurePotion());
    }

    protected override void CheckSupplies()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return;
        }

        var lowest = int.MaxValue;
        StringBuilder names = null;

        foreach (var (type, name, _) in _reagents)
        {
            var amount = pack.GetAmount(type);

            if (amount >= LowReagents)
            {
                continue;
            }

            lowest = Math.Min(lowest, amount);

            if (names == null)
            {
                names = new StringBuilder(name);
            }
            else
            {
                names.Append(", ").Append(name);
            }
        }

        if (names != null)
        {
            ReportShortage(0, names.ToString(), lowest, LowReagents);
        }
    }
}
