using ModernUO.Serialization;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class BrownBear : BaseCreature
    {
        [Constructible]
        public BrownBear() : base(AIType.AI_Animal, FightMode.Evil)
        {
            Body = 167;
            BaseSoundID = 0xA3;

            SetStr(76, 100);
            SetDex(26, 45);
            SetInt(23, 47);

            SetHits(46, 60);
            SetMana(0);

            SetDamage(6, 12);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 20, 30);
            SetResistance(ResistanceType.Cold, 15, 20);
            SetResistance(ResistanceType.Poison, 10, 15);

            SetSkill(SkillName.MagicResist, 25.1, 35.0);
            SetSkill(SkillName.Tactics, 40.1, 60.0);
            SetSkill(SkillName.Wrestling, 40.1, 60.0);

            Fame = 450;
            Karma = 0;

            VirtualArmor = 24;

            Tamable = true;
            ControlSlots = 1;
            MinTameSkill = 41.1;
        }

        public override string CorpseName => "a bear corpse";
        public override string DefaultName => "a brown bear";

        public override int Meat => 1;
        public override int Hides => 12;
        public override FoodType FavoriteFood => FoodType.Fish | FoodType.FruitsAndVeggies | FoodType.Meat;
        public override PackInstinct PackInstinct => PackInstinct.Bear;
    }
}
