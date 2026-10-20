using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fourth;
using Server.Spells.Seventh;
using Server.Spells.Sixth;

namespace Server.Engines.Companions;

/// <summary>
/// One companion's trained skills, highest first. For a mage, also the odds of casting the spells
/// an employer most wants to know about, and which of them the mage will cast unprompted.
/// </summary>
public class CompanionSkillsGump : DynamicGump
{
    private const int Width = 360;
    private const int LineHeight = 20;
    private const int MaxDestinationLines = 16;

    private readonly Mobile _employer;
    private readonly BaseCompanion _companion;

    public CompanionSkillsGump(Mobile employer, BaseCompanion companion) : base(80, 80)
    {
        _employer = employer;
        _companion = companion;
    }

    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var skills = TrainedSkills(_companion);
        var spells = _companion.Role == CompanionRole.Mage ? SpellOdds(_companion) : [];
        var destinations = new List<GateDestination>();
        (_companion.AIObject as MageCompanionAI)?.KnownDestinations(destinations);

        // One line per place, plus a line for an empty book or the places that didn't fit.
        var destinationLines = Math.Min(destinations.Count, MaxDestinationLines) + 1;
        var height = 95 + Math.Max(skills.Count, 1) * LineHeight;

        if (spells.Length > 0)
        {
            height += 30 + (spells.Length + 1) * LineHeight;
            height += 30 + destinationLines * LineHeight;
        }

        builder.AddPage();
        builder.AddBackground(0, 0, Width, height, 9270);
        builder.AddAlphaRegion(10, 10, Width - 20, height - 20);
        builder.AddHtml(20, 18, Width - 40, 20, $"{_companion.Name} {_companion.Title}", "#FFC000", 4, align: TextAlignment.Center);
        builder.AddHtml(20, 38, Width - 40, 20, $"Level {_companion.Level}", "#C0C0C0", align: TextAlignment.Center);

        var y = 65;

        if (skills.Count == 0)
        {
            builder.AddHtml(20, y, Width - 40, 20, "No trained skills.", "#FFFFFF");
            y += LineHeight;
        }

        foreach (var skill in skills)
        {
            builder.AddHtml(30, y, 200, 20, skill.Name, "#FFFFFF");
            builder.AddHtml(220, y, 110, 20, $"{skill.Value:0.0} / {skill.Cap:0}", "#C0C0C0", align: TextAlignment.Right);
            y += LineHeight;
        }

        if (spells.Length > 0)
        {
            y += 10;
            builder.AddHtml(20, y, Width - 40, 20, "Chance to cast", "#FFC000");
            y += LineHeight;
            builder.AddHtml(30, y, Width - 60, 20, BookProgress((CompanionMage)_companion), "#A0A0A0");
            y += LineHeight;

            foreach (var (name, chance, note) in spells)
            {
                var colour = chance >= 0.75 ? "#80FF80" : chance > 0 ? "#FFFF80" : "#FF4040";

                builder.AddHtml(30, y, 120, 20, name, "#FFFFFF");
                builder.AddHtml(150, y, 50, 20, $"{chance:P0}", colour, align: TextAlignment.Right);
                builder.AddHtml(210, y, Width - 230, 20, note, "#A0A0A0");
                y += LineHeight;
            }

            y += 10;
            builder.AddHtml(20, y, Width - 40, 20, "Gate destinations", "#FFC000");
            builder.AddHtml(Width - 160, y, 100, 20, "Runebook", "#FFFFFF", align: TextAlignment.Right);
            builder.AddButton(Width - 50, y - 2, 4011, 4013, 2);
            y += LineHeight;

            for (var i = 0; i < destinations.Count && i < MaxDestinationLines; i++)
            {
                var place = destinations[i];
                var note = place.Map != _companion.Map ? "another land" : place.IsDefault ? "usual" : "";

                builder.AddHtml(30, y, 230, 20, place.Name, place.Map == _companion.Map ? "#FFFFFF" : "#808080");
                builder.AddHtml(260, y, 70, 20, note, "#A0A0A0", align: TextAlignment.Right);
                y += LineHeight;
            }

            var more = destinations.Count - MaxDestinationLines;
            var footer = destinations.Count == 0 ? "None. Hand the mage a blank rune to mark." : more > 0 ? $"...and {more} more" : "";
            builder.AddHtml(30, y, Width - 60, 20, footer, "#A0A0A0");
        }

        builder.AddButton(Width - 50, height - 35, 4014, 4016, 1);
    }

    /// <summary>The back button returns to the party status window; a mage's runebook button opens the book.</summary>
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_employer.Deleted)
        {
            return;
        }

        if (info.ButtonID == 1)
        {
            _employer.SendGump(new CompanionStatusGump(_employer));
        }
        else if (info.ButtonID == 2)
        {
            CompanionRunebookGump.DisplayTo(_employer, _companion);
        }
    }

    private static List<Skill> TrainedSkills(Mobile m)
    {
        var list = new List<Skill>();

        for (var i = 0; i < m.Skills.Length; i++)
        {
            var skill = m.Skills[i];

            if (skill.Base > 0)
            {
                list.Add(skill);
            }
        }

        list.Sort((a, b) => b.Base.CompareTo(a.Base));
        return list;
    }

    /// <summary>How far through the book the mage is. The book itself is private.</summary>
    private static string BookProgress(CompanionMage mage)
    {
        var known = mage.CirclesKnown;

        return known switch
        {
            CompanionMage.Circles => "Spellbook: all eight circles.",
            0                     => $"Spellbook: empty. The first circle at {CompanionMage.CircleSkill(1):0}.",
            _                     => $"Spellbook: circles 1-{known} of 8. Next at {CompanionMage.CircleSkill(known + 1):0}."
        };
    }

    private static (string Name, double Chance, string Note)[] SpellOdds(BaseCompanion companion)
    {
        var mage = (CompanionMage)companion;
        var magery = mage.Skills.Magery.Value;

        return
        [
            Odds(mage, new RecallSpell(mage), 4, "Not cast by companions"),
            Odds(mage, new MarkSpell(mage), 6, "Marks your blank runes"),
            Odds(mage, new GateTravelSpell(mage), 7, "Opens gates for you"),
            Odds(mage, new ResurrectionSpell(mage), 8, "Raises the fallen")
        ];

        (string, double, string) Odds(CompanionMage m, Spell spell, int circle, string use) =>
            m.Knows(spell)
                ? (spell.Name, CastChance(spell, magery), use)
                : (spell.Name, 0, $"Not in the book until {CompanionMage.CircleSkill(circle):0}");
    }

    private static double CastChance(Spell spell, double skill) => MageCompanionAI.CastChance(spell, skill);
}
