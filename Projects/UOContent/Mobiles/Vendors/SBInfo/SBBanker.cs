using System.Collections.Generic;
using Server.Items;
using Server.Multis;

namespace Server.Mobiles
{
    public class SBBanker : SBInfo
    {
        public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();

        public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

        public class InternalBuyInfo : List<GenericBuyInfo>
        {
            public InternalBuyInfo()
            {
                Add(new GenericBuyInfo("1041243", typeof(ContractOfEmployment), 1252, 20, 0x14F0, 0));

                if (BaseHouse.NewVendorSystem)
                {
                    Add(new GenericBuyInfo("1062332", typeof(VendorRentalContract), 1252, 20, 0x14F0, 0x672));
                }

                // No commodity deeds on this shard.
            }
        }

        public class InternalSellInfo : GenericSellInfo
        {
        }
    }
}
