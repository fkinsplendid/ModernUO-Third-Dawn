using System;
using System.Collections.Generic;
using Server.Items;
using Server.Spells;
using Server.Spells.Seventh;
using Server.Spells.Sixth;

namespace Server.Engines.Companions;

/// <summary>A place the mage can open a gate to.</summary>
internal readonly record struct GateDestination(Point3D Location, Map Map, string Name, bool IsDefault = false);

/// <summary>
/// Gate Travel and Mark: where the mage can take the party, and how it learns new places. The
/// mage knows the entries of its own runebook, any loose marked runes (when the book is full),
/// and the public moongates.
/// </summary>
public sealed partial class MageCompanionAI
{
    private const int GateMana = 40;
    private const string UnnamedPlace = "an unnamed spot";

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
                return "I don't know of anywhere to open a gate to from here.";
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

        if (Magery < GateSkill)
        {
            return "I haven't the skill to open a gate yet.";
        }

        _gateFizzles = 0;
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
            return "My runebook's empty, so it's the nearest town or nowhere. Give me blank runes to mark.";
        }

        var names = new List<string>(known.Count);

        foreach (var place in known)
        {
            var name = place.IsDefault ? $"{place.Name} (my usual)" : place.Name;
            names.Add(place.Map == Mobile.Map ? name : $"{name} (another land)");
        }

        return $"I can gate to {string.Join(", ", names)}, or the nearest town.";
    }

    // ---------- Gates ----------

    private bool TryEscapeGate()
    {
        if (_gateRefused || Party.ActiveGate != null)
        {
            return false;
        }

        if (OpenGate(true))
        {
            return true;
        }

        _gateRefused = true;
        Companion.Speak(SpeechContext.NoGate, urgent: true);
        return false;
    }

    /// <summary>Casts Gate Travel to <paramref name="destination"/>, or the usual place if none is given.</summary>
    private bool OpenGate(bool escape, GateDestination? destination = null)
    {
        if (_gateDeadline != 0 || Party.ActiveGate != null || Magery < GateSkill || Mobile.Mana < GateMana)
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

        if (!CanGate(place.Location, place.Map))
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
            Companion.Speak(_gateIsEscape ? SpeechContext.GateEscape : SpeechContext.GateTown, _gateDestination, urgent: true);
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
    /// The usual destination: the runebook's default entry, else the newest loose marked rune,
    /// else the nearest public moongate. Only places on this facet.
    /// </summary>
    internal bool FindGateDestination(out GateDestination destination)
    {
        var known = new List<GateDestination>();
        KnownDestinations(known);

        foreach (var place in known)
        {
            if (place.IsDefault && place.Map == Mobile.Map)
            {
                destination = place;
                return true;
            }
        }

        // A book with no default set doesn't choose for us; a loose rune does.
        if (LooseRuneDestination() is { } loose)
        {
            destination = loose;
            return true;
        }

        return FindNearestMoongate(out destination);
    }

    /// <summary>The newest marked rune lying loose in the pack, on this facet.</summary>
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
            if (item is RecallRune { Marked: true } rune && rune.TargetMap == Mobile.Map)
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
    internal void KnownDestinations(List<GateDestination> known)
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
            if (item is RecallRune { Marked: true } rune && rune.TargetMap != null)
            {
                known.Add(new GateDestination(rune.Target, rune.TargetMap, PlaceName(rune.Description)));
            }
        }
    }

    /// <summary>
    /// The known place that best matches what the employer said: an exact name first, then one
    /// starting with it, then one containing every word of it. "Town" means the nearest moongate.
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

        if (best == null && query.InsensitiveEquals("town") && FindNearestMoongate(out var moongate))
        {
            return moongate;
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

    /// <summary>The nearest public moongate on this facet, landing a couple of tiles off it.</summary>
    private bool FindNearestMoongate(out GateDestination destination)
    {
        destination = default;
        var map = Mobile.Map;

        var list = map == Map.Trammel ? PMList.Trammel :
            map == Map.Felucca ? PMList.Felucca :
            map == Map.Ilshenar ? PMList.Ilshenar : null;

        if (list == null)
        {
            return false;
        }

        PMEntry nearest = null;
        var nearestDistance = double.MaxValue;

        foreach (var entry in list.Entries)
        {
            var distance = Mobile.GetDistanceToSqrt(entry.Location);

            if (distance < nearestDistance)
            {
                nearest = entry;
                nearestDistance = distance;
            }
        }

        if (nearest == null || !FindStandingSpot(map, nearest.Location, out var location))
        {
            return false;
        }

        destination = new GateDestination(location, map, Region.Find(nearest.Location, map)?.Name ?? "town");
        return true;
    }

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
        _markName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
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

        if (CastChance(spell, Magery) <= 0)
        {
            return "I haven't the skill to cast Mark yet.";
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

        if (name != null)
        {
            rune.Description = name;
        }

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

    private static bool FindStandingSpot(Map map, Point3D center, out Point3D spot)
    {
        for (var radius = 2; radius <= 4; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var x = center.X + dx;
                    var y = center.Y + dy;
                    var z = map.GetAverageZ(x, y);

                    if (map.CanSpawnMobile(x, y, z))
                    {
                        spot = new Point3D(x, y, z);
                        return true;
                    }
                }
            }
        }

        spot = Point3D.Zero;
        return false;
    }
}
