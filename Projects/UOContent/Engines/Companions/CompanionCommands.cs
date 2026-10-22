using System;
using System.Collections.Generic;
using Server.Gumps;

namespace Server.Engines.Companions;

/// <summary>
/// Things an employer can say to their companions, on top of the usual pet orders.
/// Address one by name ("Aldric, status") or the whole party ("all share", "companions, quiet").
///
///   status / report        party status window
///   skills / spells        that companion's skills (a mage also shows its casting odds)
///   share / spoils / loot  hand over everything worth handing over
///   pack / inventory       open the companion's backpack
///   quiet / hush           stop idle chatter (they still warn you of danger)
///   speak freely / chatter resume idle chatter
///   gate / town            the mage opens a gate to its usual runebook destination
///   gate [to] <place>      the mage opens a gate to a runebook entry or rune by name
///   destinations           the mage lists the places it can gate to
///   runebook / runes       read the mage's runebook: rename places, pick the usual one
///   mark [name]            the mage marks a blank rune where it stands (filed in its runebook)
///   usual <place>          the mage's gates lead to that runebook entry unless told otherwise
///   dismount               get down and leave the mount standing
///   mount up / ride        climb back on
///   return your horse      hand their mount back to the employer
///   repair / mend / smith  take worn metal gear to a master blacksmith nearby (the employer pays)
///   dismiss                send back to the guild (progress is kept)
///   why / tactics          how they read the fight, and the last thing each decided to do
///   retreat / fall back    in a fight: everyone drops it and runs with the employer (also "run",
///                          "flee", or "all follow me"; "retreat" works without a name or "all")
/// </summary>
public static class CompanionCommands
{
    private static readonly string[] _partyPrefixes = ["all ", "companions", "everyone", "party"];

    public static bool TryHandle(BaseCompanion companion, string speech)
    {
        var employer = companion.Employer;
        var party = companion.Party;

        // Shouted in a hurry, so no name or "all" needed. Out of a fight, it's just talk.
        if ((IsRetreatCall(speech) ||
             TryGetCommand(companion, speech, out var call, out _) && IsRetreatCall(call)) &&
            party != null && companion.AIObject is CompanionAI { InFight: true } companionAI)
        {
            companionAI.OrderRetreat();
            return true;
        }

        if (!TryGetCommand(companion, speech, out var command, out var wholeParty))
        {
            return false;
        }

        if (Verb(command, out _) is "why" or "tactics" or "plan" or "explain")
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                if (c.AIObject is CompanionAI ai && !c.IsDeadPet)
                {
                    c.SayTo(employer, ai.Explain());
                }
            }

            return true;
        }

        if (Has(command, "status") || Has(command, "report"))
        {
            employer.SendGump(new CompanionStatusGump(employer));
            return true;
        }

        if (Has(command, "skills") || Has(command, "spells") || Has(command, "abilities"))
        {
            employer.SendGump(
                wholeParty ? new CompanionStatusGump(employer) : new CompanionSkillsGump(employer, companion)
            );
            return true;
        }

        // Before "mount up": "dismount" contains it.
        if ((Has(command, "return") || Has(command, "give")) &&
            (Has(command, "horse") || Has(command, "mount") || Has(command, "steed")))
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                if (!wholeParty || c.Steed != null)
                {
                    c.HandBackSteed();
                }
            }

            return true;
        }

        if (Has(command, "dismount"))
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                c.Dismount(!wholeParty);
            }

            return true;
        }

        if (Has(command, "mount up") || Has(command, "ride") || Has(command, "saddle up"))
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                c.MountUp(!wholeParty);
            }

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

        // Mark first: the name given to a rune may well contain "gate" or "town".
        if (Verb(command, out var rest) == "mark")
        {
            var mage = wholeParty ? party?[CompanionRole.Mage] : companion;

            if (mage?.AIObject is MageCompanionAI ai && CompanionParty.IsFighting(mage))
            {
                ai.RequestMark(rest);
            }
            else
            {
                companion.Say(mage is CompanionMage ? "I can't do that right now." : "Marking runes is a mage's work.");
            }

            return true;
        }

        if (Verb(command, out rest) is "usual" or "default")
        {
            var mage = party?[CompanionRole.Mage];

            if (mage?.AIObject is MageCompanionAI ai && CompanionParty.IsFighting(mage))
            {
                ai.RequestUsual(rest);
            }
            else
            {
                companion.Say("We've no mage with us to open gates.");
            }

            return true;
        }

        if (Has(command, "runebook") || Has(command, "runes"))
        {
            var mage = wholeParty ? party?[CompanionRole.Mage] : companion;
            CompanionRunebookGump.DisplayTo(employer, mage ?? companion);
            return true;
        }

        if (Has(command, "destinations") || Has(command, "where") && Has(command, "gate"))
        {
            var mage = party?[CompanionRole.Mage];

            if (mage?.AIObject is MageCompanionAI ai && CompanionParty.IsFighting(mage))
            {
                ai.SayDestinations();
            }
            else
            {
                companion.Say("We've no mage with us to open gates.");
            }

            return true;
        }

        if (Has(command, "gate") || Has(command, "town"))
        {
            var mage = party?[CompanionRole.Mage];

            if (mage?.AIObject is not MageCompanionAI ai || !CompanionParty.IsFighting(mage))
            {
                companion.Say("I can't open a gate right now.");
                return true;
            }

            var refusal = ai.RequestGate(GatePlace(command));

            if (refusal != null)
            {
                mage.Say(refusal);
            }

            return true;
        }

        if (Has(command, "repair") || Has(command, "mend") || Has(command, "smith"))
        {
            foreach (var c in Targets(companion, party, wholeParty))
            {
                if (c.AIObject is CompanionAI ai && CompanionParty.IsFighting(c))
                {
                    ai.RequestRepair();
                }
            }

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

    private static readonly HashSet<string> _gateFillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "to", "us", "me", "for", "a", "the", "back", "over", "into", "please"
    };

    /// <summary>
    /// The place named after "gate": "gate to minoc mine", "gate us to minoc mine" and "gate minoc
    /// mine" all name "minoc mine". Null for a bare "gate", which goes the usual way.
    /// </summary>
    internal static string GatePlace(string command)
    {
        var words = command.Split([' ', ',', '!', '.', '?'], StringSplitOptions.RemoveEmptyEntries);
        var start = Array.FindIndex(words, w => w.InsensitiveEquals("gate")) + 1;

        if (start == 0)
        {
            return null;
        }

        while (start < words.Length && _gateFillerWords.Contains(words[start]))
        {
            start++;
        }

        // "gate us to minoc, please"
        var end = words.Length;

        while (end > start && _gateFillerWords.Contains(words[end - 1]))
        {
            end--;
        }

        return start < end ? string.Join(' ', words, start, end - start) : null;
    }

    internal static bool IsRetreatCall(string speech)
    {
        var verb = Verb(speech, out var rest);

        return verb is "retreat" or "flee" && rest.Length == 0 ||
               verb == "run" && (rest.Length == 0 || rest.StartsWith("for it", StringComparison.OrdinalIgnoreCase)) ||
               verb == "fall" && rest.StartsWith("back", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Has(string command, string keyword) => command.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>The command's first word, lower case, and the rest of it ("mark Minoc mine").</summary>
    private static string Verb(string command, out string rest)
    {
        var text = command.TrimStart(' ', ',', ':', '!', '.');
        var space = text.IndexOf(' ');

        rest = space < 0 ? "" : text[(space + 1)..].Trim();
        return (space < 0 ? text : text[..space]).TrimEnd(',', '!', '.', '?').ToLowerInvariant();
    }

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
