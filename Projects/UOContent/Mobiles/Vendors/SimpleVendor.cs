using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SimpleVendor : BaseVendor
{
    private const string VendorName = "Sieben";


    // A list of items the vendor sells - required by BaseVendor
    private readonly List<SBInfo> _sbInfos = new();
    protected override List<SBInfo> SBInfos => _sbInfos;

    // The item we want to sell and its price
    private readonly Type _itemType = typeof(SkillBallPlus);
    private readonly int _price = 10000;

    // Add a HashSet to track which players have purchased
    [SerializableField(0)]
    private HashSet<Serial> _purchasedPlayers;

    [Constructible]
    public SimpleVendor() : base(VendorName)
    {
        Title = VendorName;

        // Initialize the HashSet
        _purchasedPlayers = new HashSet<Serial>();

        // Set up the vendor's appearance
        Body = 400; // Human male body
        Female = false;
        Hue = Race.RandomSkinHue();
        InitOutfit();
    }

    // Override the BuyItems method to implement purchase limit
    public override bool OnBuyItems(Mobile buyer, List<BuyItemResponse> list)
    {
        if (buyer == null)
            return false;

        if (_purchasedPlayers.Contains(buyer.Serial))
        {
            SayTo(buyer, "You have already purchased this item.");
            return false;
        }

        bool result = base.OnBuyItems(buyer, list);
        if (result)
        {
            _purchasedPlayers.Add(buyer.Serial);
        }
        return result;
    }

    // Required by BaseVendor but we won't use it
    public override void InitSBInfo()
    {
    }

    // This handles when someone double-clicks the vendor
    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(this.Location, 3))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        from.SendMessage($"I sell skill balls for {_price} gold each. Say 'buy' to purchase one.");
    }

    // Handle speech commands
    public override void OnSpeech(SpeechEventArgs e)
    {
        if (e?.Mobile == null)
        {
            return;
        }

        Mobile from = e.Mobile;

        if (e.Speech?.ToLower() == "buy")
        {
            if (!from.InRange(this.Location, 3))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            if (_purchasedPlayers.Contains(from.Serial))
            {
                SayTo(from, "You have already purchased this item.");
                return;
            }

            if (from.Backpack?.ConsumeTotal(typeof(Gold), _price) == true)
            {
                Item item = (Item)Activator.CreateInstance(_itemType);
                if (from.AddToBackpack(item))
                {
                    _purchasedPlayers.Add(from.Serial);
                    from.SendMessage($"You bought a {_itemType.Name} for {_price} gold.");
                    this.Say($"Thank you for your purchase, {from.Name}!");
                }
                else
                {
                    if (item != null)
                    {
                        item.Delete();
                    }

                    from.AddToBackpack(new Gold(_price)); // Return the gold
                    from.SendMessage("Your backpack is too full!");
                }
            }
            else
            {
                from.SendMessage($"You need {_price} gold to buy this item.");
            }
        }

        base.OnSpeech(e);
    }

    private void InitOutfit()
    {
        // Crimson robe
        AddItem(new Robe(0x485));  // Deep red

        // Gold-trimmed hat
        AddItem(new WizardsHat(0x8A5));  // Golden

        // Dark cloak
        AddItem(new Cloak(0x455));  // Dark color

    }
}
