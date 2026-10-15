using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Engines.Companions;

/// <summary>
/// The mage's runebook, as read over its shoulder: every place in it with a hint of where it is,
/// a name the employer can rewrite, and a button to make it the usual destination. The real
/// runebook window isn't used, since it would let the employer travel on the mage's book.
/// </summary>
public class CompanionRunebookGump : DynamicGump
{
    private const int Width = 460;
    private const int LineHeight = 26;
    private const int MaxNameLength = 40;
    private const int ReadRange = 12;

    private const int SaveButton = 1;
    private const int BackButton = 2;
    private const int UsualButtonBase = 100;

    private readonly Mobile _employer;
    private readonly CompanionMage _mage;
    private readonly Runebook _book;

    // The entries as shown, so an answer applies to the places it was written against even if
    // the book changed in between.
    private readonly List<RunebookEntry> _entries;

    internal CompanionRunebookGump(Mobile employer, CompanionMage mage, Runebook book) : base(80, 80)
    {
        _employer = employer;
        _mage = mage;
        _book = book;
        _entries = [..book.Entries];
    }

    public override bool Singleton => true;

    /// <summary>Shows the mage's runebook, or has the mage say why not.</summary>
    public static void DisplayTo(Mobile employer, BaseCompanion companion)
    {
        if (companion is not CompanionMage mage)
        {
            companion.Say("I don't carry a runebook. That's for the mage.");
            return;
        }

        if (mage.Runebook is not { } book)
        {
            mage.Say("My runebook's gone. I'll be issued a new one when I'm back on my feet.");
            return;
        }

        if (!CanRead(employer, mage))
        {
            return;
        }

        employer.SendGump(new CompanionRunebookGump(employer, mage, book));
    }

    private static bool CanRead(Mobile employer, CompanionMage mage) =>
        !employer.Deleted && !mage.Deleted && mage.Employer == employer && mage.State == CompanionState.Active &&
        employer.Map == mage.Map && employer.InRange(mage, ReadRange);

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var rows = Math.Max(_entries.Count, 1);
        var height = 145 + rows * LineHeight;

        builder.AddPage();
        builder.AddBackground(0, 0, Width, height, 9270);
        builder.AddAlphaRegion(10, 10, Width - 20, height - 20);
        builder.AddHtml(20, 18, Width - 40, 20, $"{_mage.Name}'s runebook", "#FFC000", 4, align: TextAlignment.Center);
        builder.AddHtml(20, 40, Width - 40, 20, "Rename a place by writing over it, then save.", "#C0C0C0", align: TextAlignment.Center);

        var y = 68;

        if (_entries.Count == 0)
        {
            builder.AddHtml(20, y, Width - 40, 20, "Empty. Hand the mage a blank rune and say \"mark\".", "#FFFFFF", align: TextAlignment.Center);
        }
        else
        {
            builder.AddHtml(24, y, 190, 20, "Name", "#FFC000");
            builder.AddHtml(230, y, 150, 20, "Where", "#FFC000");
            builder.AddHtml(Width - 75, y, 55, 20, "Usual", "#FFC000", align: TextAlignment.Right);
            y += 22;
        }

        var usual = _book.Default;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];

            builder.AddImageTiled(20, y, 200, 22, 2624);
            builder.AddTextEntryLimited(24, y + 1, 192, 20, 0x480, i, MageCompanionAI.PlaceName(entry.Description), MaxNameLength);
            builder.AddHtml(230, y + 1, 150, 20, Whereabouts(entry), "#A0A0A0");

            if (entry == usual)
            {
                builder.AddHtml(Width - 75, y + 1, 55, 20, "usual", "#80FF80", align: TextAlignment.Right);
            }
            else
            {
                builder.AddButton(Width - 45, y, 4005, 4007, UsualButtonBase + i);
            }

            y += LineHeight;
        }

        builder.AddButton(20, height - 40, 4014, 4016, BackButton);
        builder.AddHtml(55, height - 38, 60, 20, "Back", "#FFFFFF");

        if (_entries.Count > 0)
        {
            builder.AddButton(Width - 140, height - 40, 4023, 4025, SaveButton);
            builder.AddHtml(Width - 105, height - 38, 80, 20, "Save names", "#FFFFFF");
        }
    }

    /// <summary>Where a place is in words ("Outside Britain"), or that it's in another land.</summary>
    private string Whereabouts(RunebookEntry entry) =>
        entry.Map != _mage.Map ? "another land" : MageCompanionAI.DescribeWhereabouts(entry.Location, entry.Map) ?? "";

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var button = info.ButtonID;

        if (button == 0 || !CanRead(_employer, _mage) || _mage.Runebook != _book)
        {
            return;
        }

        if (button == BackButton)
        {
            _employer.SendGump(new CompanionSkillsGump(_employer, _mage));
            return;
        }

        // Making a place the usual keeps any names written in the meantime.
        var renamed = Rename(info);
        var usual = button - UsualButtonBase;

        if (usual >= 0 && usual < _entries.Count && _book.Entries.Contains(_entries[usual]))
        {
            _book.Default = _entries[usual];
            _mage.Say($"Right. My gates will lead to {MageCompanionAI.PlaceName(_entries[usual].Description)} unless you say otherwise.");
        }
        else if (renamed > 0)
        {
            _mage.Say(renamed == 1 ? "There, I've written the new name in." : "There, I've written the new names in.");
        }

        _employer.SendGump(new CompanionRunebookGump(_employer, _mage, _book));
    }

    /// <summary>Writes the new names into the book. Blank answers leave a name as it was.</summary>
    private int Rename(in RelayInfo info)
    {
        var renamed = 0;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            var name = info.GetTextEntry(i)?.Trim();

            if (string.IsNullOrEmpty(name) || !_book.Entries.Contains(entry))
            {
                continue;
            }

            if (name.Length > MaxNameLength)
            {
                name = name[..MaxNameLength];
            }

            if (name != MageCompanionAI.PlaceName(entry.Description))
            {
                entry.Description = name;
                renamed++;
            }
        }

        return renamed;
    }
}
