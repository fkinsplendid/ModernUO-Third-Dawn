using System;
using Server.Spells;

namespace Server.Engines.Companions;

/// <summary>
/// The mage's guild-issued spellbook grows with their Magery, a circle at a time, until it is full
/// at 90. A circle is learned once it can be cast at about even odds, so new spells come in usable:
/// gate travel at 80, resurrection at 90. A mage casts nothing that isn't in the book.
/// </summary>
public partial class CompanionMage
{
    public const int Circles = 8;
    private const int SpellsPerCircle = 8;

    private static readonly string[] _circleNames =
        ["first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth"];

    private static readonly string[] _circleNews =
    [
        "Magic arrows and healing, for a start.",
        "I can cure poison now.",
        "Bless, fireball and teleport are mine now.",
        "Greater heal and lightning, at last.",
        "I can paralyze, reflect spells and call blade spirits now.",
        "I can mark runes now, and cast energy bolts.",
        "I can open gates now, and call down flame strikes.",
        "That's the whole book. I can raise the dead now."
    ];

    /// <summary>The Magery at which a circle (1-8) goes into the book.</summary>
    public static double CircleSkill(int circle) => 10.0 * circle + 10.0;

    /// <summary>How many circles a mage of this Magery has mastered.</summary>
    public static int CirclesAt(double magery) => Math.Clamp((int)((magery + 0.05 - 10.0) / 10.0), 0, Circles);

    public static string CircleName(int circle) => _circleNames[circle - 1];

    /// <summary>The guild-issued spellbook, in hand or (while dead) in the pack.</summary>
    public CompanionSpellbook Spellbook
    {
        get
        {
            if (FindItemOnLayer(Layer.OneHanded) is CompanionSpellbook held)
            {
                return held;
            }

            var pack = Backpack;

            if (pack == null)
            {
                return null;
            }

            foreach (var item in pack.Items)
            {
                if (item is CompanionSpellbook book && IsIssued(book))
                {
                    return book;
                }
            }

            return null;
        }
    }

    /// <summary>How many circles are in the book.</summary>
    public int CirclesKnown => (Spellbook?.SpellCount ?? 0) / SpellsPerCircle;

    public bool Knows(Spell spell) => Knows(spell.GetType());

    public bool Knows(Type spellType) => Spellbook?.HasSpell(SpellRegistry.GetRegistryNumber(spellType)) == true;

    private void IssueSpellbook()
    {
        AddItem(Issue(new CompanionSpellbook()));
        LearnSpells(false);
    }

    /// <summary>
    /// Writes every circle the mage's Magery has reached into the book. A mage never forgets a
    /// circle. With <paramref name="announce"/>, tells the employer what the new circle brings.
    /// </summary>
    private void LearnSpells(bool announce)
    {
        var book = Spellbook;

        if (book == null)
        {
            return;
        }

        var known = book.SpellCount / SpellsPerCircle;
        var circles = CirclesAt(Skills.Magery.Value);

        if (circles <= known)
        {
            return;
        }

        book.Content = circles == Circles ? ulong.MaxValue : (1UL << (circles * SpellsPerCircle)) - 1;

        if (announce)
        {
            Say($"I've mastered the {CircleName(circles)} circle of magic! {_circleNews[circles - 1]}");
            Employer?.SendMessage(0x59, $"{Name} has learned the {CircleName(circles)} circle of magic.");
        }
    }

    public override void OnSkillChange(SkillName skill, double oldBase)
    {
        base.OnSkillChange(skill, oldBase);

        if (skill == SkillName.Magery)
        {
            LearnSpells(Employer != null && !IsDeadPet);
        }
    }

    /// <summary>
    /// Mages hired before the guild issued its own spellbooks carried a plain, full one. That goes
    /// back to the guild and a new book holds what they have truly mastered. Only the living: a
    /// ghost's book is swapped on revival.
    /// </summary>
    private void IssueMissingSpellbook()
    {
        if (Deleted || IsDeadPet)
        {
            return;
        }

        if (FindItemOnLayer(Layer.OneHanded) is Server.Items.Spellbook old and not CompanionSpellbook && IsIssued(old))
        {
            DestroyIssued(old);
        }

        if (Spellbook == null)
        {
            IssueSpellbook();
        }
        else
        {
            LearnSpells(false);
        }
    }
}
