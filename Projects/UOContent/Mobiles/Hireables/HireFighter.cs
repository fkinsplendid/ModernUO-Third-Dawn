using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0)]
public partial class HireFighter : BaseHire
{
    public override bool ShowFameTitle => true; // Shows the "the fighter" title
    public override bool ClickTitle => true; // Show the proper title



    [Constructible]
    public HireFighter() : base(AIType.AI_Melee) // Use the built-in Melee AI type
    {
        SpeechHue = Utility.RandomDyedHue();
        Hue = Race.Human.RandomSkinHue();

        Title = "the fighter";
        HairItemID = Race.RandomHair(Female);
        HairHue = Race.RandomHairHue();
        Race.RandomFacialHair(this);

        // Improve stats for better following and combat
        SetStr(110, 150);
        SetDex(110, 150);
        SetInt(50, 50);

        SetDamage(45, 65);

        SetSkill(SkillName.Tactics, 99, 100);
        SetSkill(SkillName.Healing, 99, 100);
        SetSkill(SkillName.Swords, 99, 100);
        SetSkill(SkillName.Parry, 99, 100);
        SetSkill(SkillName.Macing, 99, 100);
        SetSkill(SkillName.Anatomy, 99, 100);
        SetSkill(SkillName.Wrestling, 40, 50);

        // Add following-specific properties
        ActiveSpeed = 0.1; // Faster movement when active
        PassiveSpeed = 0.1; // Faster movement when passive

        // Improve following settings
        //CurrentSpeed = 5;
        //RangePerception = 2;
        //RangeFight = 2;

        // Rest of the existing constructor code...

        Fame = 100;
        Karma = 100;

        if (Female = Utility.RandomBool())
        {
            Body = 0x191;
            Name = NameList.RandomName("female");
        }
        else
        {
            Body = 0x190;
            Name = NameList.RandomName("male");
        }

        switch (Utility.Random(2))
        {
            case 0:
                {
                    EquipItem(new Shoes(Utility.RandomNeutralHue()));
                    break;
                }
            case 1:
                {
                    EquipItem(new Boots(Utility.RandomNeutralHue()));
                    break;
                }
        }

        EquipItem(new Shirt());

        // Pick a random sword
        BaseWeapon weapon = Utility.Random(5) switch
        {
            1 => new Broadsword(),
            2 => new VikingSword(),
            3 => new BattleAxe(),
            4 => new TwoHandedAxe(),
            _ => new Longsword()
        };

        EquipItem(weapon);

        // Pick a random shield
        if (FindItemOnLayer(Layer.TwoHanded) == null)
        {
            BaseShield shield = Utility.Random(6) switch
            {
                1 => new HeaterShield(),
                2 => new MetalKiteShield(),
                3 => new MetalShield(),
                4 => new WoodenKiteShield(),
                5 => new WoodenShield(),
                _ => new BronzeShield()
            };

            EquipItem(shield);
        }

        BaseArmor helm = Utility.Random(5) switch
        {
            1 => new Bascinet(),
            2 => new CloseHelm(),
            3 => new NorseHelm(),
            4 => new Helmet(),
            _ => null
        };

        EquipItem(helm);

        // Pick some armour
        switch (Utility.Random(4))
        {
            case 0: // Leather
                {
                    EquipItem(new LeatherChest());
                    EquipItem(new LeatherArms());
                    EquipItem(new LeatherGloves());
                    EquipItem(new LeatherGorget());
                    EquipItem(new LeatherLegs());
                    break;
                }
            case 1: // Studded Leather
                {
                    EquipItem(new StuddedChest());
                    EquipItem(new StuddedArms());
                    EquipItem(new StuddedGloves());
                    EquipItem(new StuddedGorget());
                    EquipItem(new StuddedLegs());
                    break;
                }
            case 2: // Ringmail
                {
                    EquipItem(new RingmailChest());
                    EquipItem(new RingmailArms());
                    EquipItem(new RingmailGloves());
                    EquipItem(new RingmailLegs());
                    break;
                }
            case 3: // Chain
                {
                    EquipItem(new ChainChest());
                    //EquipItem(new ChainCoif());
                    EquipItem(new ChainLegs());
                    break;
                }
        }

        PackGold(25, 100);
        var pack = new Backpack();
        AddItem(pack);

        // Add 100 bandages to the backpack
        pack.DropItem(new Bandage(100));

    }



public override void OnThink()
{
    base.OnThink();

    // If we have a master and we're not in combat
    if (ControlMaster != null && Combatant == null)
    {
        // Get distance to master
        var distance = GetDistanceToSqrt(ControlMaster);

        // If too far from master, run to catch up
        if (distance > 4)
        {
            CurrentSpeed = 3.0; // Run faster
            ActiveSpeed = 0.2;  // Also increase active movement speed
            PassiveSpeed = 0.2; // And passive movement speed

            ControlOrder = OrderType.Come;
        }

        else
        {
            CurrentSpeed = 0.5; // Normal speed when at good distance
        }

        // If on different map, move to master
        if (Map != ControlMaster.Map)
        {
            Map = ControlMaster.Map;
            Location = ControlMaster.Location;
        }
    }
}
}
