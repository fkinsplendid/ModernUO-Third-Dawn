using ModernUO.Serialization;
using Server.Items;

namespace Server.Engines.Companions;

/// <summary>
/// A companion mage's own spellbook, issued by the guild. It holds only the circles the mage has
/// mastered and fills as their Magery grows. It is theirs alone: nobody else opens it, counts its
/// pages or writes scrolls into it.
/// </summary>
[SerializationGenerator(0)]
public partial class CompanionSpellbook : Spellbook
{
    [Constructible]
    public CompanionSpellbook()
    {
    }

    public override bool ShowsSpellCount => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster)
        {
            DisplayTo(from);
        }
        else
        {
            from.SendMessage("A mage's spellbook is private. You may not read it.");
        }
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster)
        {
            return base.OnDragDrop(from, dropped);
        }

        from.SendMessage("A mage learns new spells by study as their skill grows, not from scrolls.");
        return false;
    }
}
