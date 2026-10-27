using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

[SerializationGenerator(0)]
public partial class CompanionWarrior : BaseCompanion
{
    private static readonly CompanionProfile _profile = new()
    {
        Role = CompanionRole.Warrior,
        Title = "the warrior",
        Str = new StatCurve(85, 2.0),
        Dex = new StatCurve(60, 1.5),
        Int = new StatCurve(25, 0.5),
        BonusHitsPerLevel = 3,
        Skills =
        [
            SkillName.Swords, SkillName.Macing, SkillName.Fencing, new(SkillName.Tactics, 10), new(SkillName.Parry, 5),
            SkillName.Anatomy, SkillName.Healing, SkillName.MagicResist
        ]
    };

    [Constructible]
    public CompanionWarrior()
    {
    }

    public override CompanionRole Role => CompanionRole.Warrior;

    public override CompanionProfile Profile => _profile;

    protected override BaseAI CreateAI() => new WarriorCompanionAI(this);

    protected override bool IsSupply(Item item) => item is Bandage || base.IsSupply(item);

    protected override void DressForRole()
    {
        var weapon = Utility.Random(3) switch
        {
            0 => (Item)new Broadsword(),
            1 => new WarMace(),
            _ => new Longsword()
        };

        AddItem(Issue(weapon));
        AddItem(Issue(new HeaterShield()));
        AddItem(Issue(new RingmailChest()));
        AddItem(Issue(new RingmailLegs()));
        AddItem(Issue(new RingmailArms()));
        AddItem(Issue(new RingmailGloves()));
        AddItem(Issue(new ChainCoif()));
        AddItem(Issue(new ThighBoots()));
        AddItem(Issue(new Cloak(Utility.RandomRedHue())));

        PackItem(new Bandage(50));
        PackItem(new HealPotion());
        PackItem(new HealPotion());
    }

    protected override void CheckSupplies() =>
        ReportShortage(0, "bandages", Backpack?.GetAmount(typeof(Bandage)) ?? 0, 10);

    protected override void RestockRoleSupplies() => TopUp(typeof(Bandage), 50, amount => new Bandage(amount));
}
