using System;
using System.Collections.Generic;
using Server.Gumps;

namespace Server.Engines.Companions;

/// <summary>
/// Things an employer can say to their companions, on top of the usual pet orders.
/// Address one by name ("Aldric, status") or the whole party ("all share", "companions, quiet").
///
///   status / report        party status window
///   share / spoils / loot  hand over everything worth handing over
///   pack / inventory       open the companion's backpack
///   quiet / hush           stop idle chatter (they still warn you of danger)
///   speak freely / chatter resume idle chatter
///   gate / town            the mage opens a gate to the nearest town or their marked rune
///   dismiss                send back to the guild (progress is kept)
/// </summary>
public static class CompanionCommands
{
    private static readonly string[] _partyPrefixes = ["all ", "companions", "everyone", "party"];

    public static bool TryHandle(BaseCompanion companion, string speech)
    {
        if (!TryGetCommand(companion, speech, out var command, out var wholeParty))
        {
            return false;
        }

        var employer = companion.Employer;
        var party = companion.Party;

        if (Has(command, "status") || Has(command, "report"))
        {
            employer.SendGump(new CompanionStatusGump(employer));
            return true;
        }

        if (Has(command, "share") || Has(command, "spoils") || Has(command, "loot"))
        {
            var shared = false;

            foreach (var c in Targets(companion, party, wholeParty))
            {
                shared |= c.ShareLoot(false);
            }

            if (!shared)
            {
                companion.Say("We've nothing for you right now.");
            }

            return true;
        }

        if (Has(command, "pack") || Has(command, "inventory"))
        {
            if (companion.HasPackAccess(employer))
            {
                companion.Backpack?.DisplayTo(employer);
            }

            return true;
        }

        if (Has(command, "quiet") || Has(command, "hush") || Has(command, "silence"))
        {
            SetQuiet(companion, party, wholeParty, true);
            companion.Say("As you wish.");
            return true;
        }

        if (Has(command, "speak freely") || Has(command, "chatter"))
        {
            SetQuiet(companion, party, wholeParty, false);
            companion.Say("Ah, good. I was getting bored.");
            return true;
        }

        if (Has(command, "gate") || Has(command, "town"))
        {
            var mage = party?[CompanionRole.Mage];

            if (mage?.AIObject is MageCompanionAI ai && CompanionParty.IsFighting(mage) && ai.RequestTownGate())
            {
                return true;
            }

            (CompanionParty.IsFighting(mage) ? mage : companion).Say("I can't open a gate right now.");
            return true;
        }

        if (Has(command, "dismiss"))
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                c.ReturnToGuild(SpeechContext.Dismissed);
            }

            return true;
        }

        return false;
    }

    private static bool TryGetCommand(BaseCompanion companion, string speech, out string command, out bool wholeParty)
    {
        var name = companion.Name;

        if (!string.IsNullOrEmpty(name) && speech.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        {
            command = speech[name.Length..];
            wholeParty = false;
            return true;
        }

        foreach (var prefix in _partyPrefixes)
        {
            if (speech.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                command = speech[prefix.Length..];
                wholeParty = true;
                return true;
            }
        }

        command = null;
        wholeParty = false;
        return false;
    }

    private static bool Has(string command, string keyword) => command.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static void SetQuiet(BaseCompanion companion, CompanionParty party, bool wholeParty, bool quiet)
    {
        foreach (var c in Targets(companion, party, wholeParty))
        {
            c.Quiet = quiet;
        }
    }

    /// <summary>The addressed companion, or every working companion in the party.</summary>
    private static List<BaseCompanion> Targets(BaseCompanion companion, CompanionParty party, bool wholeParty)
    {
        if (!wholeParty || party == null)
        {
            return [companion];
        }

        var targets = new List<BaseCompanion>(3);

        foreach (var member in party.Members)
        {
            if (CompanionParty.IsInWorld(member))
            {
                targets.Add(member);
            }
        }

        return targets;
    }
}
