using ModernUO.Serialization;
using Server.Items;

namespace Server.Engines.Companions;

/// <summary>
/// The mage's own runebook, issued by the guild. Every rune the mage marks or is handed goes into
/// it, and it is what the mage gates from. It burns with the mage: a mage that dies loses the book
/// and every place in it, and is raised with an empty one.
/// </summary>
public partial class CompanionMage
{
    public const int RunebookCapacity = 16;

    /// <summary>The guild-issued runebook in the pack, or null while dead.</summary>
    public Runebook Runebook
    {
        get
        {
            var pack = Backpack;

            if (pack == null)
            {
                return null;
            }

            foreach (var item in pack.Items)
            {
                if (item is Runebook book && IsIssued(book))
                {
                    return book;
                }
            }

            return null;
        }
    }

    private void IssueRunebook()
    {
        // Held in the hand before AOS; this one stays in the pack, so revival doesn't equip it.
        PackItem(Issue(new Runebook { Layer = Layer.Invalid }));
    }

    /// <summary>
    /// Files a marked rune into the runebook, using it up. The first place filed becomes the
    /// book's default. Returns false if there is no book or it is full; the rune stays loose.
    /// </summary>
    public bool FileRune(RecallRune rune)
    {
        var book = Runebook;

        if (book == null || book.Entries.Count >= RunebookCapacity || !rune.Marked || !book.OnDragDrop(this, rune))
        {
            return false;
        }

        book.Default ??= book.Entries[^1];
        return true;
    }

    /// <summary>A marked rune handed over goes straight into the book.</summary>
    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (!base.OnDragDrop(from, dropped))
        {
            return false;
        }

        if (dropped is RecallRune { Marked: true, Deleted: false } rune)
        {
            var place = MageCompanionAI.PlaceName(rune.Description);

            if (FileRune(rune))
            {
                Say($"I'll keep that in my runebook as {place}.");
            }
            else if (Runebook != null)
            {
                Say("My runebook's full. I'll keep the rune loose.");
            }
        }

        return true;
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (IsDeadPet && Runebook is { } book)
        {
            DestroyIssued(book);
            Employer?.SendMessage(0x22, $"{Name}'s runebook is lost, and every rune in it.");
        }
    }

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();

        if (Runebook == null)
        {
            IssueRunebook();
            Say("I'll have to start a new runebook. Every place in the old one is gone.");
        }
    }

    /// <summary>Mages hired before runebooks were issued get theirs on load.</summary>
    [AfterDeserialization(false)]
    private void IssueMissingRunebook()
    {
        if (!Deleted && !IsDeadPet && Runebook == null)
        {
            IssueRunebook();
        }
    }
}
