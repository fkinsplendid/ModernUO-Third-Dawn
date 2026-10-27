using System;
using System.Collections.Generic;
using Server.Items;
using Server.Regions;
using Server.Spells;
using Server.Spells.Seventh;
using Server.Spells.Sixth;

namespace Server.Engines.Companions;

/// <summary>A place the mage can open a gate to.</summary>
internal readonly record struct GateDestination(Point3D Location, Map Map, string Name, bool IsDefault = false);

/// <summary>
/// Gate Travel and Mark: where the mage can take the party, and how it learns new places. The
/// mage knows the entries of its own runebook and any loose marked runes (when the book is full).
/// A gate only ever leads to a marked place: without a rune, there's no gate.
/// </summary>
public sealed partial class MageCompanionAI
{
    private const int GateMana = 40;
    private const string UnnamedPlace = "an unnamed spot";

    /// <summary>A destination this close is where we already are; a gate there would just lead next door.</summary>
    private const int AlreadyHereRange = 24;

    /// <summary>
    /// Opens a gate on the employer's request: to the place named in <paramref name="query"/> if
    /// given, otherwise to the usual destination. Returns why not, or null once casting.
    /// </summary>
    public string RequestGate(string query)
    {
        if (Mobile.Spell != null || _gateDeadline != 0)
        {
            return "Give me a moment, I'm already casting.";
        }

        GateDestination destination;

        if (string.IsNullOrWhiteSpace(query))
        {
            if (!FindGateDestination(out destination))
            {
                return UsualDestination() is { } usual && IsHere(usual)
                    ? $"We're already at {usual.Name}. Name somewhere else."
                    : "I've no marked rune to gate to. Give me a blank rune and I'll mark somewhere.";
            }
        }
        else if (FindNamedDestination(query) is { } named)
        {
            destination = named;
        }
        else
        {
            return $"I don't know a place called \"{query}\". {DescribeDestinations()}";
        }

        if (destination.Map != Mobile.Map)
        {
            return $"{destination.Name} lies in another land. I can't gate there from here.";
        }

        if (IsHere(destination))
        {
            return $"We're already at {destination.Name}.";
        }

        if (!Knows<GateTravelSpell>())
        {
            return $"Gate travel's not in my book yet. I'll learn it at {GateSkill:0} Magery.";
        }

        _gateFizzles = 0;
        _gateRequested = true;
        return OpenGate(false, destination) ? null : GateRefusal();
    }

    private string GateRefusal() =>
        Mobile.Mana < GateMana ? "I'm spent. Let me rest, then ask me again." :
        !HasReagents(new GateTravelSpell(Mobile).Info) ? "I'm short of reagents for a gate. It takes black pearl, mandrake root and sulfurous ash." :
        "Something blocks my gate magic here.";

    /// <summary>Makes the runebook entry best matching <paramref name="query"/> the usual destination.</summary>
    public void RequestUsual(string query)
    {
        var book = (Companion as CompanionMage)?.Runebook;

        if (book == null || book.Entries.Count == 0)
        {
            Mobile.Say("My runebook's empty. Give me a blank rune and I'll mark somewhere.");
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            Mobile.Say($"My usual is {PlaceName(book.Default?.Description)}. Say \"usual\" and a place to change it.");
            return;
        }

        var words = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        RunebookEntry best = null;
        var bestScore = 0;

        foreach (var entry in book.Entries)
        {
            var score = MatchScore(PlaceName(entry.Description), query.Trim(), words);

            if (score > bestScore)
            {
                best = entry;
                bestScore = score;
            }
        }

        if (best == null)
        {
            Mobile.Say($"I don't know a place called \"{query.Trim()}\". {DescribeDestinations()}");
            return;
        }

        book.Default = best;
        Mobile.Say($"Right. My gates will lead to {PlaceName(best.Description)} unless you say otherwise.");
    }

    /// <summary>The mage says where it can open gates to.</summary>
    public void SayDestinations() => Mobile.Say(DescribeDestinations());

    private string DescribeDestinations()
    {
        var known = new List<GateDestination>();
        KnownDestinations(known);

        if (known.Count == 0)
        {
            return "My runebook's empty, so I can't gate anywhere. Give me blank runes to mark.";
        }

        var names = new List<string>(known.Count);

        foreach (var place in known)
        {
            var name = place.IsDefault ? $"{place.Name} (my usual)" : place.Name;
            names.Add(place.Map == Mobile.Map ? name : $"{name} (another land)");
        }

        return $"I can gate to {string.Join(", ", names)}.";
    }

    // ---------- Gates ----------

    private bool TryEscapeGate()
    {
        // A band stands its ground or falls back on foot with its captain.
        if (Companion.InBand || _gateRefused || Party.ActiveGate != null)
        {
            return false;
        }

        _gateRequested = false;

        if (OpenGate(true))
        {
            return true;
        }

        _gateRefused = true;
        Companion.Speak(FindGateDestination(out _) ? SpeechContext.NoGate : SpeechContext.NoRuneToGate, urgent: true);
        return false;
    }

    /// <summary>Casts Gate Travel to <paramref name="destination"/>, or the usual place if none is given.</summary>
    private bool OpenGate(bool escape, GateDestination? destination = null)
    {
        if (_gateDeadline != 0 || Party.ActiveGate != null || !Knows<GateTravelSpell>() || Mobile.Mana < GateMana)
        {
            return false;
        }

        GateDestination place;

        if (destination != null)
        {
            place = destination.Value;
        }
        else if (!FindGateDestination(out place))
        {
            return false;
        }

        if (IsHere(place) || !CanGate(place.Location, place.Map))
        {
            return false;
        }

        var spell = new GateTravelSpell(Mobile, new RunebookEntry(null, place.Location, place.Map, place.Name));

        if (!CastAt(spell, null))
        {
            return false;
        }

        _gateSpell = spell;
        _gateDeadline = Core.TickCount + GateWaitMs;
        _gateIsEscape = escape;
        _gateTarget = destination;
        _gateDestination = place.Name;
        return true;
    }

    /// <summary>
    /// Once the spell lands, the gate at our feet becomes the party's way out. If it fizzled or was
    /// knocked out of our hands, try again, up to <see cref="MaxGateAttempts"/> casts in all.
    /// </summary>
    private void CheckGateOpened()
    {
        foreach (var item in Mobile.Map.GetItemsInRange<GateTravelMoongate>(Mobile.Location, 1))
        {
            _gateDeadline = 0;
            _gateSpell = null;
            _gateFizzles = 0;
            _gateTarget = null;
            Party.ActiveGate = item;
            AnnounceGate();
            return;
        }

        var stillCasting = _gateSpell != null && (_gateSpell.State != SpellState.None || Mobile.Spell == _gateSpell);

        if (stillCasting && Core.TickCount - _gateDeadline < 0)
        {
            return;
        }

        _gateDeadline = 0;
        _gateSpell = null;

        if (++_gateFizzles < MaxGateAttempts)
        {
            // An escape is retried by TryEscapeGate on the next tick; a town gate from TryRoleIdle.
            _retryTownGate = !_gateIsEscape;
            Companion.Speak(SpeechContext.Fizzle, urgent: true);
            return;
        }

        _gateFizzles = 0;
        _gateTarget = null;
        _retryTownGate = false;

        if (_gateIsEscape)
        {
            _gateRefused = true;
            Companion.Speak(SpeechContext.NoGate, urgent: true);
        }
        else
        {
            Mobile.Say("The gate won't come to me today. We'll have to walk.");
        }
    }

    /// <summary>
    /// Says where the gate leads. Never left unsaid, even straight after another line: the employer
    /// needs to know before stepping through.
    /// </summary>
    private void AnnounceGate()
    {
        if (_gateRequested)
        {
            Mobile.Say($"The gate's open. It leads to {_gateDestination}.");
        }
        else if (!Companion.Speak(_gateIsEscape ? SpeechContext.GateEscape : SpeechContext.GateTown, _gateDestination, urgent: true))
        {
            Mobile.Say($"Gate's open to {_gateDestination}!");
        }
    }

    /// <summary>
    /// The usual destination: the runebook's default entry, else the newest loose marked rune.
    /// Only places on this facet, and never where we already are.
    /// </summary>
    internal bool FindGateDestination(out GateDestination destination)
    {
        if (UsualDestination() is { } usual && !IsHere(usual))
        {
            destination = usual;
            return true;
        }

        // A book with no default set doesn't choose for us; a loose rune does.
        if (LooseRuneDestination() is { } loose)
        {
            destination = loose;
            return true;
        }

        destination = default;
        return false;
    }

    /// <summary>The runebook's default entry, if it lies on this facet.</summary>
    private GateDestination? UsualDestination()
    {
        var known = new List<GateDestination>();
        KnownDestinations(known);

        foreach (var place in known)
        {
            if (place.IsDefault && place.Map == Mobile.Map)
            {
                return place;
            }
        }

        return null;
    }

    /// <summary>True if <paramref name="place"/> is close enough that a gate there would go nowhere.</summary>
    private bool IsHere(GateDestination place) =>
        place.Map == Mobile.Map && Mobile.InRange(place.Location, AlreadyHereRange);

    /// <summary>The newest marked rune lying loose in the pack, on this facet and not leading here.</summary>
    private GateDestination? LooseRuneDestination()
    {
        GateDestination? newest = null;
        var pack = Mobile.Backpack;

        if (pack == null)
        {
            return null;
        }

        foreach (var item in pack.Items)
        {
            if (item is RecallRune { Marked: true } rune && rune.TargetMap == Mobile.Map &&
                !Mobile.InRange(rune.Target, AlreadyHereRange))
            {
                newest = new GateDestination(rune.Target, rune.TargetMap, PlaceName(rune.Description));
            }
        }

        return newest;
    }

    /// <summary>
    /// Every place the mage holds a mark for: runebook entries first (the book's default flagged),
    /// then loose marked runes, in pack order.
    /// </summary>
    internal void KnownDestinations(List<GateDestination> known, RecallRune except = null)
    {
        var pack = Mobile.Backpack;

        if (pack == null)
        {
            return;
        }

        if ((Companion as CompanionMage)?.Runebook is { } book)
        {
            var defaultEntry = book.Default;

            foreach (var entry in book.Entries)
            {
                if (entry.Map != null && entry.Map != Map.Internal)
                {
                    known.Add(new GateDestination(entry.Location, entry.Map, PlaceName(entry.Description), entry == defaultEntry));
                }
            }
        }

        foreach (var item in pack.Items)
        {
            if (item is RecallRune { Marked: true } rune && rune.TargetMap != null && rune != except)
            {
                known.Add(new GateDestination(rune.Target, rune.TargetMap, PlaceName(rune.Description)));
            }
        }
    }

    /// <summary>
    /// The known place that best matches what the employer said: an exact name first, then one
    /// starting with it, then one containing every word of it.
    /// </summary>
    internal GateDestination? FindNamedDestination(string query)
    {
        var known = new List<GateDestination>();
        KnownDestinations(known);

        query = query.Trim();
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        GateDestination? best = null;
        var bestScore = 0;

        foreach (var place in known)
        {
            var score = MatchScore(place.Name, query, words);

            // Prefer a place on this facet when two match equally well.
            if (score > bestScore || score == bestScore && score > 0 && place.Map == Mobile.Map && best?.Map != Mobile.Map)
            {
                best = place;
                bestScore = score;
            }
        }

        return best;
    }

    private static int MatchScore(string name, string query, string[] words)
    {
        if (name.InsensitiveEquals(query))
        {
            return 3;
        }

        if (name.InsensitiveStartsWith(query))
        {
            return 2;
        }

        foreach (var word in words)
        {
            if (!name.InsensitiveContains(word))
            {
                return 0;
            }
        }

        return 1;
    }

    internal static string PlaceName(string description) =>
        string.IsNullOrWhiteSpace(description) ? UnnamedPlace : description.Trim();

    /// <summary>
    /// The checks Gate Travel makes when the spell lands, made before casting so a gate that
    /// would fail never burns reagents.
    /// </summary>
    private bool CanGate(Point3D location, Map map) =>
        map == Mobile.Map &&
        SpellHelper.CheckTravel(Mobile, TravelCheckType.GateFrom, out _) &&
        SpellHelper.CheckTravel(Mobile, map, location, TravelCheckType.GateTo, out _) &&
        !SpellHelper.CheckCombat(Mobile) &&
        map.CanSpawnMobile(location.X, location.Y, location.Z) &&
        !SpellHelper.CheckMulti(location, map);

    // ---------- Marking runes ----------

    /// <summary>
    /// Marks a blank rune from the mage's pack where it stands, on the employer's request, naming
    /// it <paramref name="name"/> if given. Says why not if it can't, so no reagents are spent on a
    /// mark that would fail anyway. A carried runebook with room gets the rune filed into it.
    /// </summary>
    public void RequestMark(string name)
    {
        var spell = new MarkSpell(Mobile);
        var refusal = MarkRefusal(spell, out var rune);

        if (refusal == null && !CastOnItem(spell, rune))
        {
            refusal = "I can't cast just now.";
        }

        if (refusal != null)
        {
            Mobile.Say(refusal);
            return;
        }

        _markSpell = spell;
        _markRune = rune;
        _markName = CleanRuneName(name);
    }

    private string MarkRefusal(MarkSpell spell, out RecallRune rune)
    {
        rune = null;

        if (Threats.Count > 0)
        {
            return "Not in the middle of a fight!";
        }

        if (Mobile.Spell != null || _gateDeadline != 0 || _markSpell != null)
        {
            return "Give me a moment, I'm already casting.";
        }

        if (!KnowsSpell(spell))
        {
            return $"Mark's not in my book yet. I'll learn it at {CompanionMage.CircleSkill(6):0} Magery.";
        }

        rune = FindBlankRune();

        if (rune == null)
        {
            return "Hand me a blank recall rune first.";
        }

        if (!HasReagents(spell.Info))
        {
            return "I'm short of reagents for that. It takes black pearl, blood moss and mandrake root.";
        }

        if (Mobile.Mana < spell.GetMana())
        {
            return "I'm spent. Let me rest, then ask me again.";
        }

        if (!SpellHelper.CheckTravel(Mobile, TravelCheckType.Mark, out _) ||
            SpellHelper.CheckMulti(Mobile.Location, Mobile.Map, !Core.AOS))
        {
            return "This place won't hold a mark.";
        }

        return null;
    }

    private static readonly HashSet<string> _fillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "this", "that", "it", "my", "our", "rune", "runes", "here", "spot", "place",
        "location", "please", "now", "for", "me", "us", "as", "called", "named", "and", "name"
    };

    /// <summary>
    /// The name in "mark this rune as Minoc mine", without the filler around it. Null if nothing is
    /// left ("mark rune", "mark here").
    /// </summary>
    internal static string CleanRuneName(string said)
    {
        if (string.IsNullOrWhiteSpace(said))
        {
            return null;
        }

        var words = said.Trim().TrimEnd('.', '!', '?').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var start = 0;

        while (start < words.Length && _fillerWords.Contains(words[start].Trim(',', ':')))
        {
            start++;
        }

        return start < words.Length ? string.Join(' ', words, start, words.Length - start).Trim(',', ':', ' ') : null;
    }

    /// <summary>
    /// Names a marked rune before it goes in the book: the name given, else its own description,
    /// else where it leads in words. Mark only names runes in the few regions that carry a rune
    /// name, so most need the mage to write one, as a player would. Numbered if already taken.
    /// </summary>
    internal void NameRune(RecallRune rune, string name)
    {
        var description = name ??
                          (string.IsNullOrWhiteSpace(rune.Description) ? null : rune.Description.Trim()) ??
                          DescribeWhereabouts(rune.Target, rune.TargetMap);

        rune.Description = UniqueName(description, rune);
    }

    /// <summary>
    /// A place in words: the named region it's in, else the direction from the nearest town
    /// ("North-east of Minoc"), or "Outside Minoc" within a short walk of its edge.
    /// </summary>
    internal static string DescribeWhereabouts(Point3D at, Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        for (var region = Region.Find(at, map); region != null; region = region.Parent)
        {
            if (region.Name == MoongateRegionName)
            {
                return MoongateName(at, map);
            }

            if (!string.IsNullOrWhiteSpace(region.Name) && region != map.DefaultRegion)
            {
                return region.Name;
            }
        }

        var town = NearestTown(at, map, out var townDistance);

        if (town == null)
        {
            return null;
        }

        if (townDistance <= OutsideTownRange)
        {
            return $"Outside {town.Name}";
        }

        var dx = at.X - town.GoLocation.X;
        var dy = at.Y - town.GoLocation.Y;

        // Map Y grows southwards.
        var northSouth = dy < 0 ? "North" : "South";
        var eastWest = dx > 0 ? "East" : "West";

        var bearing = Math.Abs(dx) > 2 * Math.Abs(dy) ? eastWest :
            Math.Abs(dy) > 2 * Math.Abs(dx) ? northSouth :
            $"{northSouth}-{eastWest.ToLowerInvariant()}";

        return $"{bearing} of {town.Name}";
    }

    private const int OutsideTownRange = 40;

    // Every public moongate stands in a region of this name, which says nothing about where it is.
    private const string MoongateRegionName = "Moongates";

    /// <summary>"Yew moongate": a public moongate named after the town it serves.</summary>
    private static string MoongateName(Point3D at, Map map) =>
        NearestTown(at, map, out _) is { } town ? $"{town.Name} moongate" : "the moongate";

    private static Region NearestTown(Point3D at, Map map, out double distance)
    {
        Region town = null;
        distance = double.MaxValue;

        foreach (var region in map.Regions.Values)
        {
            if (region is TownRegion && !string.IsNullOrWhiteSpace(region.Name) && region.GoLocation != Point3D.Zero)
            {
                var d = DistanceToArea(at, region);

                if (d < distance)
                {
                    town = region;
                    distance = d;
                }
            }
        }

        return town;
    }

    /// <summary>Tiles from <paramref name="at"/> to the nearest edge of the region; 0 inside it.</summary>
    private static double DistanceToArea(Point3D at, Region region)
    {
        var best = double.MaxValue;

        foreach (var rect in region.Area)
        {
            var dx = Math.Max(Math.Max(rect.Start.X - at.X, 0), at.X - (rect.End.X - 1));
            var dy = Math.Max(Math.Max(rect.Start.Y - at.Y, 0), at.Y - (rect.End.Y - 1));
            best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy));
        }

        return best;
    }

    /// <summary>
    /// Adds a number if the book already holds a place of that name ("Minoc 2"). The rune being
    /// named is already in the pack, so it doesn't count against itself.
    /// </summary>
    private string UniqueName(string name, RecallRune naming)
    {
        if (name == null)
        {
            return null;
        }

        var known = new List<GateDestination>();
        KnownDestinations(known, naming);

        var candidate = name;

        for (var n = 2; Contains(known, candidate); n++)
        {
            candidate = $"{name} {n}";
        }

        return candidate;

        static bool Contains(List<GateDestination> places, string name)
        {
            foreach (var place in places)
            {
                if (place.Name.InsensitiveEquals(name))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private RecallRune FindBlankRune()
    {
        var pack = Mobile.Backpack;

        if (pack == null)
        {
            return null;
        }

        foreach (var item in pack.Items)
        {
            if (item is RecallRune { Marked: false } rune)
            {
                return rune;
            }
        }

        return null;
    }

    /// <summary>
    /// Once Mark has run its course, names the rune and files it in the runebook. If the book is
    /// full it stays loose at the back of the pack, so gates use it over any older loose runes. A
    /// fizzle leaves the rune blank.
    /// </summary>
    private void CheckMarkLanded()
    {
        if (_markSpell == null || _markSpell.State != SpellState.None || Mobile.Spell == _markSpell)
        {
            return;
        }

        var rune = _markRune;
        var name = _markName;
        _markSpell = null;
        _markRune = null;
        _markName = null;

        if (rune?.Deleted != false)
        {
            return;
        }

        if (!rune.Marked)
        {
            Mobile.Say("It fizzled. The rune's still blank; ask me again.");
            return;
        }

        NameRune(rune, name);

        var place = PlaceName(rune.Description);

        if ((Companion as CompanionMage)?.FileRune(rune) == true)
        {
            Mobile.Say($"Marked. It's in my runebook as {place}.");
            return;
        }

        var pack = Mobile.Backpack;

        if (rune.Parent == pack)
        {
            pack.RemoveItem(rune);
            pack.AddItem(rune);
        }

        Mobile.Say($"Marked {place}, but my runebook is full. I'll keep the rune loose.");
    }

    /// <summary>The odds a spell rolls against when cast from memory (no scroll) at this skill.</summary>
    internal static double CastChance(Spell spell, double skill)
    {
        spell.GetCastSkills(out var min, out var max);
        return Math.Clamp((skill - min) / (max - min), 0.0, 1.0);
    }
}
