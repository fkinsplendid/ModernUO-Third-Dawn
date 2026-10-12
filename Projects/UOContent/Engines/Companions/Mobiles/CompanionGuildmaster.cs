using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Keeps the companion guild's ledger. Double-click, or say "hire" or "companion" nearby,
/// to hire, rehire, resurrect or release companions.
/// </summary>
[SerializationGenerator(0)]
public partial class CompanionGuildmaster : BaseCreature
{
    [Constructible]
    public CompanionGuildmaster() : base(AIType.AI_Vendor, FightMode.None, 2)
    {
        Female = Utility.RandomBool();
        Body = Female ? 0x191 : 0x190;
        Name = NameList.RandomName(Female ? "female" : "male");
        Title = "the companion guildmaster";
        Hue = Race.Human.RandomSkinHue();
        CantWalk = true;

        Utility.AssignRandomHair(this);

        SetStr(100);
        SetDex(100);
        SetInt(100);

        AddItem(new FancyShirt(0x47E));
        AddItem(new LongPants(0x455));
        AddItem(new BodySash(0x48D));
        AddItem(new Boots());
        AddItem(new HalfApron(0x48D));
    }

    public override bool IsInvulnerable => true;

    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(this, 4) && from.Alive)
        {
            from.SendGump(new CompanionGuildGump(from, this));
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }

    public override bool HandlesOnSpeech(Mobile from) => from.Player && from.InRange(this, 4);

    public override void OnSpeech(SpeechEventArgs e)
    {
        var from = e.Mobile;

        if (!e.Handled && from.Alive && from.InRange(this, 4) &&
            (e.Speech.InsensitiveContains("hire") || e.Speech.InsensitiveContains("companion")))
        {
            e.Handled = true;
            SayTo(from, "Looking for good company on the road? Let's see the ledger.");
            from.SendGump(new CompanionGuildGump(from, this));
            return;
        }

        base.OnSpeech(e);
    }
}
