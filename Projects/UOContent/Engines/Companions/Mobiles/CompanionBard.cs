using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

[SerializationGenerator(0)]
public partial class CompanionBard : BaseCompanion
{
    private static readonly CompanionProfile _profile = new()
    {
        Role = CompanionRole.Bard,
        Title = "the bard",
        Str = new StatCurve(45, 1.5),
        Dex = new StatCurve(55, 1.5),
        Int = new StatCurve(45, 1.5),
        BonusHitsPerLevel = 2,
        Skills =
        [
            SkillName.Musicianship, SkillName.Peacemaking, SkillName.Provocation, SkillName.Discordance,
            SkillName.Fencing, SkillName.Tactics, SkillName.Healing, SkillName.Anatomy
        ]
    };

    [Constructible]
    public CompanionBard()
    {
    }

    public override CompanionRole Role => CompanionRole.Bard;

    public override CompanionProfile Profile => _profile;

    protected override BaseAI CreateAI() => new BardCompanionAI(this);

    protected override bool IsSupply(Item item) => item is Bandage or BaseInstrument || base.IsSupply(item);

    /// <summary>Light armour only, so nothing slows the fingers. No shields: the off hand holds the instrument.</summary>
    public override double ScoreArmor(BaseArmor armor) =>
        armor is BaseShield || armor.MaterialType >= ArmorMaterialType.Ringmail ? -1 : base.ScoreArmor(armor);

    protected override void DressForRole()
    {
        AddItem(Issue<BaseWeapon>(Utility.RandomBool() ? new Kryss() : new Dagger()));
        AddItem(Issue(new FancyShirt(Utility.RandomBrightHue())));
        AddItem(Issue(new LeatherChest()));
        AddItem(Issue(new LeatherLegs()));
        AddItem(Issue(new LeatherGloves()));
        AddItem(Issue(new Boots()));
        AddItem(Issue(new FeatheredHat(Utility.RandomBrightHue())));
        AddItem(Issue(new Cloak(Utility.RandomBrightHue())));

        PackItem(
            Issue<BaseInstrument>(
                Utility.Random(4) switch
                {
                    0 => new Lute(),
                    1 => new Harp(),
                    2 => new Drums(),
                    _ => new Tambourine()
                }
            )
        );
        PackItem(new Bandage(25));
        PackItem(new HealPotion());
        PackItem(new HealPotion());
    }

    protected override void CheckSupplies()
    {
        var pack = Backpack;

        ReportShortage(0, "an instrument", pack?.FindItemByType<BaseInstrument>() == null ? 0 : 1, 1);
        ReportShortage(1, "bandages", pack?.GetAmount(typeof(Bandage)) ?? 0, 5);
    }
}
