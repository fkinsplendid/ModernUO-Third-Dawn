using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Bands;

/// <summary>
/// Leads an adventuring band. To the companion code the captain is simply the employer: the band's
/// warrior, bard and mage follow, guard, heal and fall back with them. The captain walks the patrol,
/// picks the fights (never one a player has started) and calls the retreat when the band is losing.
/// Innocent, so under Trammel's rules no player can attack the band.
/// </summary>
[SerializationGenerator(0)]
public partial class BandCaptain : BaseCreature
{
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private AdventurerBand _band;

    private BaseAI _ai;

    [Constructible]
    public BandCaptain() : this(null, 10)
    {
    }

    public BandCaptain(AdventurerBand band, int level) : base(AIType.AI_Melee, FightMode.Evil, 12, 1)
    {
        _band = band;
        level = Math.Clamp(level, 1, 30);

        Team = BandSystem.Team;
        Title = "the captain";
        Hue = Race.Human.RandomSkinHue();

        if (Female = Utility.RandomBool())
        {
            Body = 0x191;
            Name = NameList.RandomName("female");
        }
        else
        {
            Body = 0x190;
            Name = NameList.RandomName("male");
            Utility.AssignRandomFacialHair(this, HairHue);
        }

        Utility.AssignRandomHair(this);

        ActiveSpeed = 0.2;
        PassiveSpeed = 0.4;
        CurrentSpeed = 0.4;

        // Three followers: the band's warrior, bard and mage.
        FollowersMax = 5;

        SetStr(90 + level * 2);
        SetDex(60 + level);
        SetInt(40);
        SetHits(90 + level * 6);
        SetDamage(6, 12);

        var skill = Math.Min(100.0, 50.0 + level * 2.0);

        SetSkill(SkillName.Swords, skill);
        SetSkill(SkillName.Tactics, skill);
        SetSkill(SkillName.Parry, skill);
        SetSkill(SkillName.Anatomy, skill);
        SetSkill(SkillName.MagicResist, skill);
        SetSkill(SkillName.Wrestling, skill);

        Karma = 2000;

        Dress();
    }

    protected override BaseAI ForcedAI => _ai ??= new BandCaptainAI(this);

    public override bool InitialInnocent => true;
    public override bool AlwaysMurderer => false;
    public override bool ShowFameTitle => false;
    public override bool CanRummageCorpses => false;
    public override bool CanBeDistracted => false;

    /// <summary>Plate and a blade, none of it lootable: a fallen captain's kit stays with them.</summary>
    private void Dress()
    {
        Item weapon = Utility.Random(3) switch
        {
            0 => new Broadsword(),
            1 => new Katana(),
            _ => new VikingSword()
        };

        Item helm = Utility.RandomBool() ? new CloseHelm() : new NorseHelm();

        Wear(weapon);
        Wear(new MetalKiteShield());
        Wear(new PlateChest());
        Wear(new PlateLegs());
        Wear(new PlateArms());
        Wear(new PlateGloves());
        Wear(new PlateGorget());
        Wear(helm);
        Wear(new Cloak(Utility.RandomNondyedHue()));
        Wear(new Boots());
    }

    private void Wear(Item item)
    {
        item.Movable = false;
        AddItem(item);
    }

    public override void GenerateLoot()
    {
    }

    /// <summary>Never a player, nothing of a player's, nobody else's band, and no fight a player started.</summary>
    public override bool IsEnemy(Mobile m) =>
        !BandRules.IsOffLimits(m) && !BandRules.IsBandMember(m) && !BandRules.IsClaimedByPlayer(m) && base.IsEnemy(m);

    /// <summary>A stray blow from a bandmate is shrugged off, never answered.</summary>
    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        if (!BandRules.AreBandmates(this, aggressor))
        {
            base.AggressiveAction(aggressor, criminal);
        }
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        base.AlterMeleeDamageFrom(from, ref damage);
        BandRules.ScaleTaken(from, ref damage);
    }

    public override void AlterMeleeDamageTo(Mobile to, ref int damage)
    {
        base.AlterMeleeDamageTo(to, ref damage);
        BandRules.ScaleDealt(to, ref damage);
    }

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        base.AlterSpellDamageFrom(from, ref damage);
        BandRules.ScaleTaken(from, ref damage);
    }

    public override void AlterSpellDamageTo(Mobile to, ref int damage)
    {
        base.AlterSpellDamageTo(to, ref damage);
        BandRules.ScaleDealt(to, ref damage);
    }

    /// <summary>Handing them gold would otherwise fall to the townsfolk's gift handler, which keeps it.</summary>
    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from.Player)
        {
            SayTo(from, "Keep it, friend. We want for nothing.");
        }

        return false;
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        _band?.OnCaptainFell(this);
    }

    public override void OnAfterDelete()
    {
        _band = null;
        _ai = null;
        base.OnAfterDelete();
    }
}
