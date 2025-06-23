using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SimpleVendor : BaseVendor
{
    private readonly string[] _name = { "the Simple Vendor" };
    
    // A list of items the vendor sells - required by BaseVendor
    private readonly List<SBInfo> _sbInfos = new();
    protected override List<SBInfo> SBInfos => _sbInfos;
    
    // The item we want to sell and its price
    private readonly Type _itemType = typeof(Bandage);
    private readonly int _price = 5;

    [Constructible]
    public SimpleVendor() : base(_name)
    {
        Title = "the Simple Vendor";
        
        // Set up the vendor's appearance
        Body = 400; // Human male body
        Female = false;
        Hue = Race.RandomSkinHue();
        InitOutfit();
    }

    // Required by BaseVendor but we won't use it
    protected override void InitSBInfo()
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

        from.SendMessage($"I sell bandages for {_price} gold each. Say 'buy' to purchase one.");
    }

    // Handle speech commands
    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        Mobile from = e.Mobile;

        if (!e.Handled && e.Speech.ToLower() == "buy")
        {
            if (!from.InRange(this.Location, 3))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            if (from.Backpack?.ConsumeTotal(typeof(Gold), _price) == true)
            {
                Item item = (Item)Activator.CreateInstance(_itemType);
                if (from.AddToBackpack(item))
                {
                    from.SendMessage($"You bought a {item.Name} for {_price} gold.");
                    this.Say($"Thank you for your purchase, {from.Name}!");
                }
                else
                {
                    item.Delete();
                    from.AddToBackpack(new Gold(_price)); // Return the gold
                    from.SendMessage("Your backpack is too full!");
                }
            }
            else
            {
                from.SendMessage($"You need {_price} gold to buy this item.");
            }
        }
    }

    private void InitOutfit()
    {
        AddItem(new FancyShirt(Utility.RandomDyedHue()));
        AddItem(new LongPants(Utility.RandomNeutralHue()));
        AddItem(new Boots());
    }
}