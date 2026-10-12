using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MageReagentBag : Bag
{
    [Constructible]
    public MageReagentBag()
    {
        Hue = 0x485;
        DropItem(new BlackPearl(50));
        DropItem(new Bloodmoss(50));
        DropItem(new Garlic(50));
        DropItem(new Ginseng(50));
        DropItem(new MandrakeRoot(50));
        DropItem(new Nightshade(50));
        DropItem(new SpidersSilk(50));
        DropItem(new SulfurousAsh(50));
    }
}
