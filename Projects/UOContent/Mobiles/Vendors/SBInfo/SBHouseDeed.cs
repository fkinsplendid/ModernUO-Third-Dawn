using System.Collections.Generic;
using Server.Multis.Deeds;

namespace Server.Mobiles
{
    public class SBHouseDeed : SBInfo
    {
        public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();

        public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

        public class InternalBuyInfo : List<GenericBuyInfo>
        {
            public InternalBuyInfo()
            {
                Add(
                    new GenericBuyInfo(
                        "deed to a stone-and-plaster house",
                        typeof(StonePlasterHouseDeed),
                        87600,
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a field stone house", typeof(FieldStoneHouseDeed), 87600, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a small brick house", typeof(SmallBrickHouseDeed), 87600, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a wooden house", typeof(WoodHouseDeed), 87600, 20, 0x14F0, 0));
                Add(
                    new GenericBuyInfo(
                        "deed to a wood-and-plaster house",
                        typeof(WoodPlasterHouseDeed),
                        87600,
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(
                    new GenericBuyInfo(
                        "deed to a thatched-roof cottage",
                        typeof(ThatchedRoofCottageDeed),
                        87600,
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a brick house", typeof(BrickHouseDeed), 289000, 20, 0x14F0, 0));
                Add(
                    new GenericBuyInfo(
                        "deed to a two-story wood-and-plaster house",
                        typeof(TwoStoryWoodPlasterHouseDeed),
                        384800,
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a tower", typeof(TowerDeed), 866400, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a small stone keep", typeof(KeepDeed), 1330400, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a castle", typeof(CastleDeed), 2045600, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a large house with patio", typeof(LargePatioDeed), 305600, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a marble house with patio", typeof(LargeMarbleDeed), 384000, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a small stone tower", typeof(SmallTowerDeed), 177000, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a two story log cabin", typeof(LogCabinDeed), 195600, 20, 0x14F0, 0));
                Add(
                    new GenericBuyInfo(
                        "deed to a sandstone house with patio",
                        typeof(SandstonePatioDeed),
                        181800,
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a two story villa", typeof(VillaDeed), 273000, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a small stone workshop", typeof(StoneWorkshopDeed), 121200, 20, 0x14F0, 0));
                Add(new GenericBuyInfo("deed to a small marble workshop", typeof(MarbleWorkshopDeed), 126000, 20, 0x14F0, 0));
            }
        }

        public class InternalSellInfo : GenericSellInfo
        {
        }
    }
}
