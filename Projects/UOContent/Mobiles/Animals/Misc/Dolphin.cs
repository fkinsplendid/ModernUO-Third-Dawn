using ModernUO.Serialization;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Dolphin : BaseCreature
    {
        [Constructible]
        public Dolphin()
            : base(AIType.AI_Animal, FightMode.Evil)
        {
            Body = 0x97;
            BaseSoundID = 0x8A;

            SetStr(21, 49);
            SetDex(66, 85);
            SetInt(96, 110);

            SetHits(15, 27);

            SetDamage(3, 6);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 15, 20);
            SetResistance(ResistanceType.Fire, 70, 80);
            SetResistance(ResistanceType.Cold, 25, 30);
            SetResistance(ResistanceType.Poison, 10, 15);
            SetResistance(ResistanceType.Energy, 10, 15);

            SetSkill(SkillName.MagicResist, 15.1, 20.0);
            SetSkill(SkillName.Tactics, 19.2, 29.0);
            SetSkill(SkillName.Wrestling, 19.2, 29.0);

            Fame = 500;
            Karma = 2000;

            VirtualArmor = 16;
            CanSwim = true;
            CantWalk = true;
        }

        public override string CorpseName => "a dolphin corpse";
        public override string DefaultName => "a dolphin";

        public override int Meat => 1;

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                Jump();
            }
        }

        public virtual void Jump()
        {
            if (Utility.RandomBool())
            {
                Animate(3, 16, 1, true, false, 0);
            }
            else
            {
                Animate(4, 20, 1, true, false, 0);
            }
        }

        public override void OnThink()
        {
            if (Utility.RandomDouble() < .005) // slim chance to jump
            {
                Jump();
            }

            base.OnThink();
        }
    }
}
