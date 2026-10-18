using System.Collections.Generic;
using Server.Engines.Companions;

namespace Server.Engines.TownLife;

/// <summary>Who a resident is talking to.</summary>
public enum ListenerKind
{
    Resident,
    Keeper,
    Companion
}

/// <summary>The other side of a conversation: a neighbour's trade, the vendor, or a hired companion's role.</summary>
public readonly record struct Listener(ListenerKind Kind, ResidentTrade? Trade = null, CompanionRole? Role = null)
{
    public static Listener Keeper => new(ListenerKind.Keeper);

    public static Listener Neighbour(ResidentTrade trade) => new(ListenerKind.Resident, trade);

    public static Listener Hireling(CompanionRole role) => new(ListenerKind.Companion, Role: role);
}

/// <summary>
/// A short exchange between two people. Lines alternate: the opener (A) says the first, the
/// other (B) the second, and so on. Tokens: {a} and {b} are their names; {c} is another
/// resident of the town, for gossip. A dialogue can be tied to a pair of trades, a kind of
/// place, the vendor who keeps the place, a passing companion and their role, or a time of day.
/// </summary>
public sealed class Dialogue
{
    public Dialogue(string[] lines) => Lines = lines;

    public string[] Lines { get; }

    public PlaceKind? Place { get; init; }

    public ResidentTrade? TradeA { get; init; }

    public ResidentTrade? TradeB { get; init; }

    /// <summary>Who B is: another resident, the vendor who keeps the place, or someone's hired companion.</summary>
    public ListenerKind With { get; init; }

    public bool WithKeeper => With == ListenerKind.Keeper;

    /// <summary>For talk with a companion: their role, or null for any.</summary>
    public CompanionRole? Role { get; init; }

    /// <summary>A quick word in passing, said without stopping.</summary>
    public bool Passing { get; init; }

    public DayPhase[] Phases { get; init; }

    public bool NeedsGossip { get; init; }

    /// <summary>Only in the town of this name: local talk.</summary>
    public string Town { get; init; }

    /// <summary>About something that really happened: needs a piece of news of this kind. Tokens {who}, {what}, {where}.</summary>
    public NewsKind? News { get; init; }
}

public static class ResidentDialogues
{
    private static readonly List<Dialogue> _all = [];

    private static readonly DayPhase[] _late = [DayPhase.Evening, DayPhase.Night];
    private static readonly DayPhase[] _early = [DayPhase.Dawn, DayPhase.Morning];

    public static IReadOnlyList<Dialogue> All => _all;

    static ResidentDialogues()
    {
        PassingWords();
        TradePairs();
        PlaceTalk();
        KeeperTalk();
        Gossip();
        Anywhere();
        LocalTalk();
        CompanionTalk();
        NewsTalk();
    }

    // ---------- Selection ----------

    /// <summary>
    /// Something fitting for a resident of <paramref name="aTrade"/> to start with <paramref name="listener"/>
    /// at <paramref name="place"/>. Talk about who they both are is likeliest, then talk about the
    /// place or the hour, then small talk. Null if nothing fits.
    /// </summary>
    public static Dialogue Choose(
        ResidentTrade aTrade, Listener listener, PlaceKind place, DayPhase phase, bool passing, bool canGossip,
        NewsKind? news = null, string town = null
    )
    {
        Dialogue chosen = null;
        var total = 0;

        foreach (var dialogue in _all)
        {
            var weight = Weigh(dialogue, aTrade, listener, place, phase, passing, canGossip, news, town);

            if (weight == 0)
            {
                continue;
            }

            // One pass, weighted: each candidate replaces the pick with odds weight/total.
            total += weight;

            if (Utility.Random(total) < weight)
            {
                chosen = dialogue;
            }
        }

        return chosen;
    }

    private static int Weigh(
        Dialogue d, ResidentTrade aTrade, Listener listener, PlaceKind place, DayPhase phase, bool passing,
        bool canGossip, NewsKind? news, string town
    )
    {
        if (d.News != null && d.News != news || d.Town != null && d.Town != town)
        {
            return 0;
        }

        if (d.Passing != passing || d.With != listener.Kind || d.NeedsGossip && !canGossip ||
            d.Place != null && d.Place != place || d.TradeA != null && d.TradeA != aTrade ||
            d.TradeB != null && d.TradeB != listener.Trade || d.Role != null && d.Role != listener.Role)
        {
            return 0;
        }

        if (d.Phases != null && System.Array.IndexOf(d.Phases, phase) < 0)
        {
            return 0;
        }

        var weight = 1;

        if (d.TradeA != null || d.TradeB != null || d.Role != null)
        {
            weight += 4;
        }

        if (d.Place != null)
        {
            weight += 2;
        }

        if (d.Phases != null)
        {
            weight += 2;
        }

        if (d.Town != null)
        {
            weight += 2;
        }

        // Real news is what everyone wants to talk about.
        if (d.News != null)
        {
            weight += 8;
        }

        return weight;
    }

    // ---------- Builders ----------

    private static void Add(Dialogue d) => _all.Add(d);

    private static void Passing(params string[] lines) => Add(new Dialogue(lines) { Passing = true });

    private static void PassingAt(DayPhase[] phases, params string[] lines) =>
        Add(new Dialogue(lines) { Passing = true, Phases = phases });

    private static void Pair(ResidentTrade a, ResidentTrade b, params string[] lines) =>
        Add(new Dialogue(lines) { TradeA = a, TradeB = b });

    private static void Opener(ResidentTrade a, params string[] lines) => Add(new Dialogue(lines) { TradeA = a });

    private static void At(PlaceKind place, params string[] lines) => Add(new Dialogue(lines) { Place = place });

    private static void AtWhen(PlaceKind place, DayPhase[] phases, params string[] lines) =>
        Add(new Dialogue(lines) { Place = place, Phases = phases });

    private static void Keeper(PlaceKind place, params string[] lines) =>
        Add(new Dialogue(lines) { Place = place, With = ListenerKind.Keeper });

    private static void KeeperAs(PlaceKind place, ResidentTrade a, params string[] lines) =>
        Add(new Dialogue(lines) { Place = place, With = ListenerKind.Keeper, TradeA = a });

    private static void Hireling(CompanionRole? role, params string[] lines) =>
        Add(new Dialogue(lines) { With = ListenerKind.Companion, Role = role });

    private static void HirelingFrom(ResidentTrade a, CompanionRole? role, params string[] lines) =>
        Add(new Dialogue(lines) { With = ListenerKind.Companion, Role = role, TradeA = a });

    private static void HirelingWhen(DayPhase[] phases, params string[] lines) =>
        Add(new Dialogue(lines) { With = ListenerKind.Companion, Phases = phases });

    private static void GossipLine(params string[] lines) => Add(new Dialogue(lines) { NeedsGossip = true });

    private static void Small(params string[] lines) => Add(new Dialogue(lines));

    private static void SmallWhen(DayPhase[] phases, params string[] lines) => Add(new Dialogue(lines) { Phases = phases });

    // ---------- Content ----------

    private static void PassingWords()
    {
        Passing("Morning, {b}!", "{a}! Keeping well?");
        Passing("{b}.", "{a}.");
        Passing("Afternoon, {b}.", "Can't stop, {a}!");
        Passing("Still alive, {b}?", "Last I checked, {a}.");
        Passing("{b}! You owe me a drink.", "Put it on my tab, {a}!");
        Passing("Mind how you go, {b}.", "And you, {a}.");
        PassingAt(_early, "You're up early, {b}.", "Never went to bed, {a}.");
        PassingAt(_early, "Good morrow, {b}.", "Is it? Ask me after breakfast.");
        PassingAt(_late, "Evening, {b}.", "Evening, {a}. Home?", "Eventually.");
        PassingAt([DayPhase.Night], "Late for you, {b}.", "Couldn't sleep. You?", "Never do.");
    }

    private static void TradePairs()
    {
        Pair(ResidentTrade.Watchman, ResidentTrade.Drunk,
            "Evening, {b}. Steady now.",
            "Steady as a... *hic* ...rock, {a}.",
            "Rocks don't sway. Home with you, before I carry you."
        );
        Pair(ResidentTrade.Drunk, ResidentTrade.Watchman,
            "{b}! My dearest friend in all {town}!",
            "I arrested you on Tuesday, {a}.",
            "And I've forgiven you!"
        );
        Pair(ResidentTrade.Beggar, ResidentTrade.Noble,
            "Spare a coin, your grace?",
            "Find honest work, {a}.",
            "I had honest work. The orcs burnt it down."
        );
        Pair(ResidentTrade.Noble, ResidentTrade.Beggar,
            "Here. Buy yourself something warm.",
            "Bless you, {a}! Bless you!",
            "Yes, yes. Don't tell anyone."
        );
        Pair(ResidentTrade.Farmer, ResidentTrade.Fisher,
            "Trade you a sack of turnips for a fish, {b}?",
            "Two fish. They're big turnips, I've seen them.",
            "Done."
        );
        Pair(ResidentTrade.Fisher, ResidentTrade.Farmer,
            "Rain's coming, {b}. I can smell it.",
            "From your lips to the sky's ears, {a}.",
            "Ears? The sky has ears now?"
        );
        Pair(ResidentTrade.Smith, ResidentTrade.Farmer,
            "{b}, your plough's mended.",
            "Already? You're a marvel, {a}.",
            "Master did the hard bit. I did the swearing."
        );
        Pair(ResidentTrade.Farmer, ResidentTrade.Smith,
            "Hoe's snapped again, {b}.",
            "What are you hitting, rocks?",
            "{town}'s full of rocks, {b}. Rocks and taxes."
        );
        Pair(ResidentTrade.Courier, ResidentTrade.Noble,
            "Letter for you, {b}. Sealed, very grand.",
            "From the castle? At last!",
            "It's your tailor's bill.",
            "...Burn it."
        );
        Pair(ResidentTrade.Watchman, ResidentTrade.Beggar,
            "Not on the bank steps, {b}. You know the rule.",
            "Rules are for people with roofs, {a}.",
            "...Fine. Five minutes."
        );
        Pair(ResidentTrade.Noble, ResidentTrade.Minstrel,
            "Play something cheerful, would you?",
            "For a coin, {a}, I'll play something ecstatic.",
            "Cheerful will do."
        );
        Pair(ResidentTrade.Drunk, ResidentTrade.Minstrel,
            "Play the one about the sheep, {b}!",
            "I am never playing that one again, {a}.",
            "Then I'll sing it! *ahem*",
            "Please don't."
        );
        Pair(ResidentTrade.Fisher, ResidentTrade.Fisher,
            "Anything, {b}?",
            "Boot, two weeds, and a crab with a grudge.",
            "Better than me."
        );
        Pair(ResidentTrade.Farmer, ResidentTrade.Farmer,
            "Crows got at your rows too, {b}?",
            "Every last one. Clever devils.",
            "We should hire them. They work harder than my nephew."
        );
        Pair(ResidentTrade.Shopper, ResidentTrade.Shopper,
            "Fish is dear today, {b}.",
            "Everything's dear today, {a}.",
            "Was it ever cheap?",
            "When we were young. Everything was."
        );

        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "Still here, {b}?",
            "Where else would I be, {a}?",
            "Fair point."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "Buy my sword, {b}?",
            "Is it the one you tried to sell me yesterday?",
            "...It's had a polish since."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "Who's that coming in?",
            "Adventurer. Fresh from a dungeon, by the smell.",
            "Think they'll sell?",
            "Only one way to find out."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "Fancy a game of dice, {b}?",
            "With your dice? Never again.",
            "They're perfectly fair!",
            "They've two sixes on one side, {a}."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "Nice cloak, {b}.",
            "Dyed it myself.",
            "That explains the colour."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Lurker,
            "I'm off to Despise tomorrow, {b}.",
            "You said that last week.",
            "And I meant it then, too."
        );
        Add(
            new Dialogue(["Heard {c} found a magic ring.", "{c}? Where?", "Wouldn't say. Which means it's stolen."])
            {
                TradeA = ResidentTrade.Lurker, NeedsGossip = true
            }
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Beggar,
            "Not you again, {b}.",
            "Spare a coin, {a}? You've plenty.",
            "That's why I've plenty."
        );
        Pair(ResidentTrade.Beggar, ResidentTrade.Lurker,
            "You stand here all day, {b}. Same as me.",
            "I'm doing business, {a}.",
            "So am I. Mine's just honest."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Noble,
            "Fine rings, {b}. Selling any?",
            "Certainly not.",
            "Buying, then? I've a lovely one here."
        );
        Pair(ResidentTrade.Watchman, ResidentTrade.Lurker,
            "Move along, {b}.",
            "It's a public bank, {a}.",
            "It's a public street too. Move along it."
        );
        Pair(ResidentTrade.Lurker, ResidentTrade.Courier,
            "Anything for me, {b}?",
            "Nothing, {a}. Same as yesterday.",
            "Someone's bound to write eventually."
        );

        Opener(ResidentTrade.Minstrel,
            "Any requests, {b}?",
            "Anything but the one about the sheep.",
            "That's my best one!"
        );
        Opener(ResidentTrade.Minstrel,
            "I've written a new verse about you, {b}.",
            "Is it flattering?",
            "It rhymes, which is more than most can say."
        );
        Opener(ResidentTrade.Courier,
            "Letter for you, {b}!",
            "Who from?",
            "Didn't say. Smells of lavender.",
            "...Give it here."
        );
        Add(
            new Dialogue(["Have you seen {c}? Got a parcel.", "Not since this morning.", "Typical. Third trip."])
            {
                TradeA = ResidentTrade.Courier, NeedsGossip = true
            }
        );
        Opener(ResidentTrade.Beggar,
            "A coin for an old soldier, {b}?",
            "You were never a soldier, {a}.",
            "I was very nearly a soldier."
        );
        Opener(ResidentTrade.Watchman,
            "All quiet your way, {b}?",
            "Saw a shadow by the graveyard.",
            "Probably a cat. Probably."
        );
        Opener(ResidentTrade.Smith,
            "Look, {b}. Burnt myself in the shape of a horseshoe.",
            "Is that... a skill?",
            "Master says it's a lesson."
        );
    }

    private static void PlaceTalk()
    {
        At(PlaceKind.Tavern, "To Lord British!", "To Lord British! And to whoever's paying.", "Not me.");
        At(PlaceKind.Tavern, "This ale's watered, {b}.", "So drink twice as much.", "There's wisdom in that.");
        At(PlaceKind.Tavern, "Did I ever tell you about the time I fought a troll?", "Every night, {a}.", "It gets bigger every time.");
        At(PlaceKind.Tavern, "What's in the stew today?", "Don't ask, {a}. Just eat.", "...It moved.");
        AtWhen(PlaceKind.Tavern, _late, "One more?", "One more. Then home.", "You said that an hour ago, {b}.");
        AtWhen(PlaceKind.Tavern, _late, "Sing us a song, {b}!", "I only know sad ones.", "Perfect. It's that sort of night.");

        At(PlaceKind.Market, "Have you seen these prices, {b}?", "Robbery. Daylight robbery.", "I bought two.", "So did I.");
        At(PlaceKind.Market, "Fresh, they say.", "Fresh last week, maybe.", "Still cheaper than the inn.");
        At(PlaceKind.Market, "Mind your purse, {b}. Cutpurses about.", "I keep it in my boot.", "That explains the walk.");

        At(PlaceKind.Bank, "Queue's out the door again.", "Everyone's rich but us, {a}.", "Speak for yourself. I'm very nearly rich.");
        At(PlaceKind.Bank, "Putting it in or taking it out, {b}?", "Out. Always out.", "Same.");

        At(PlaceKind.Docks, "Ship in from Trinsic?", "Aye, and one from Vesper by nightfall.", "Busy week.");
        At(PlaceKind.Docks, "Smell that sea air, {b}.", "That's not the sea, {a}. That's the fish market.");

        At(PlaceKind.Fields, "Soil's good this year.", "Soil's always good. It's the weather that's wicked.");
        At(PlaceKind.Fields, "See that scarecrow, {b}?", "Aye.", "Crow sitting on its head.", "Doing a grand job, then.");

        At(PlaceKind.Smithy, "Hot enough for you, {b}?", "Hotter than the Fire Dungeon in here.", "Have you been to the Fire Dungeon?", "...No.");

        At(PlaceKind.Healer, "What are you in for, {b}?", "Back. You?", "Everything.");
        At(PlaceKind.Healer, "*coughs* Don't get close, {b}.", "I wasn't planning to.");

        At(PlaceKind.Street, "Lovely day, {b}.", "Is it? I hadn't looked up.", "You should. It's free.");
        AtWhen(PlaceKind.Street, [DayPhase.Night], "Strange folk about tonight.", "There always are.", "Stranger than usual, {b}.");
    }

    private static void KeeperTalk()
    {
        Keeper(PlaceKind.Tavern, "Ale, {b}, if you'd be so kind.", "Coin first, {a}.", "You wound me.", "Coin heals all wounds.");
        Keeper(PlaceKind.Tavern, "What's good tonight, {b}?", "The stew.", "And what's good that isn't the stew?", "The door.");
        KeeperAs(PlaceKind.Tavern, ResidentTrade.Drunk, "Another, {b}!", "You've had six, {a}.", "Then this'll be seven. Count with me.");

        Keeper(PlaceKind.Market, "How much for this?", "Twelve gold.", "Twelve! I'll give you eight.", "Ten, and you're robbing me.", "Done.");
        Keeper(PlaceKind.Market, "Is this fresh, {b}?", "Fresh this morning, {a}.", "Which morning?", "...A morning.");
        Keeper(PlaceKind.Market, "Got any of those honey cakes left?", "Last two, just for you.", "You say that to everyone.", "And it works on everyone.");

        Keeper(PlaceKind.Bank, "Deposit, please, {b}.", "Of course, {a}. Every coin counted.", "Count them twice.", "We count them three times.");
        Keeper(PlaceKind.Bank, "Is my money still there, {b}?", "Every coin, {a}. Same as yesterday.", "Just checking.");
        KeeperAs(PlaceKind.Bank, ResidentTrade.Noble, "I'd like to see my vault.", "Right this way, {a}.", "Do keep the dust off my gold.");
        KeeperAs(PlaceKind.Bank, ResidentTrade.Lurker, "Just checking my box again, {b}.", "Third time this hour, {a}.", "Things go missing.");
        KeeperAs(PlaceKind.Bank, ResidentTrade.Lurker, "Any chance of a loan, {b}?", "None whatsoever.", "Worth asking.");

        Keeper(PlaceKind.Smithy, "Is my knife sharpened yet, {b}?", "Tomorrow.", "You said that yesterday.", "And I'll say it tomorrow.");
        KeeperAs(PlaceKind.Smithy, ResidentTrade.Smith, "Finished the horseshoes, {b}.", "All of them?", "...Most of them.", "Back to the anvil.");

        Keeper(PlaceKind.Healer, "It's my back again, {b}.", "Stop lifting barrels, then.", "But the barrels need lifting!");
        Keeper(PlaceKind.Healer, "I think I'm dying, {b}.", "You've a cold, {a}.", "A dying cold.");

        Keeper(PlaceKind.Docks, "Anything in today, {b}?", "Mackerel, mostly.", "Mackerel again.", "The sea gives what it gives.");
        KeeperAs(PlaceKind.Docks, ResidentTrade.Fisher, "Want to buy a fish, {b}?", "I sell fish, {a}.", "Then you'll know a good one.");
    }

    private static void Gossip()
    {
        GossipLine("Did you hear about {c}?", "No! What?", "Neither did I. That's what's suspicious.");
        GossipLine("I saw {c} at the tavern again.", "Again? Before noon?", "Before breakfast.");
        GossipLine("{c} owes me three gold.", "{c} owes everyone three gold.", "Then {c} is a very rich person.");
        GossipLine("They say {c} has a secret sweetheart.", "Who?", "If I knew, it wouldn't be a secret.");
        GossipLine("{c} was asking after you, {b}.", "Good asking or bad asking?", "Couldn't tell.");
        GossipLine("Is it true {c} saw a dragon?", "{c} says a lot of things.", "That's a no, then.");
    }

    private static void Anywhere()
    {
        Small("How's the family, {b}?", "Loud. Yours?", "Louder.");
        Small("Heard there's trouble in Minoc.", "There's always trouble in Minoc.", "Fair.");
        Small("They say the Lord's been unwell.", "They said that last year.", "And he's still Lord British.");
        Small("Ever been to Ilshenar, {b}?", "Through those gates? Not for all the gold in {town}.", "My cousin went.", "And?", "And I've not seen my cousin since.");
        Small("Think it'll rain?", "Always does, eventually.", "Comforting.");
        Small("Blackthorn's castle has a new tower, they say.", "Blackthorn's castle has more towers than sense.");
        Small("Adventurers everywhere these days, {b}.", "Good for trade.", "Bad for the graveyard.");
        SmallWhen(_late, "Long day, {b}?", "The longest. Yours?", "Longer.");
        SmallWhen(_early, "Sleep well, {b}?", "Like a log. A log with a bad back.");
    }

    private static void Local(string town, params string[] lines) => Add(new Dialogue(lines) { Town = town });

    private static void LocalTalk()
    {
        Local("Britain", "Castle's lit up tonight, {b}.", "Feast for some lord or other.", "And not a crumb for us.");
        Local("Britain", "Busier every year, Britain.", "Too busy, {a}. I can't hear myself think.", "Is that what that noise is?");

        Local("Vesper", "Bridge out on the east side again, {b}.", "Again? They only mended it last week.", "Ore carts. Heavy ones.");
        Local("Vesper", "Fell in the canal last night, {b}.", "Drunk?", "Pushed. By the canal.");
        Local("Vesper", "Minoc lot are in town.", "Hide the ale.", "Too late, {b}.");
        Local("Vesper", "Ship in from Moonglow?", "Aye. Reagents and mages, the usual.", "Mind your pockets. And your eyebrows.");

        Local("Ocllo", "Ship on the horizon, {b}.", "Pirates?", "Fishermen. Probably.", "Probably.");
        Local("Ocllo", "Anything happen today, {b}?", "A gull stole my bread.", "Big news, for Ocllo.");
        Local("Ocllo", "Ever think of moving to Britain, {b}?", "Every day.", "And?", "Then I see the prices.");
        Local("Ocllo", "Magincia folk were over again.", "Looking down their noses?", "From a great height, {b}.");
    }

    private static void CompanionTalk()
    {
        Hireling(null, "Off on an adventure?", "Something like that.", "Bring me back a dragon's tooth!");
        Hireling(null, "You're not from round here.", "Not originally.", "Thought not. You walk like you expect trouble.");
        Hireling(null, "Who's paying you, then?", "Someone with more gold than sense.", "Lucky you.");
        Hireling(null, "Seen any monsters, {b}?", "More than I'd like.", "*shudders* Better you than me.");
        HirelingWhen(_late, "Bit late for adventuring.", "Monsters keep odd hours.", "So do drunks. Mind the both of them.");

        Hireling(CompanionRole.Warrior, "That's a lot of steel for a stroll through town.", "You never know.", "I do know. It's {town}.");
        Hireling(CompanionRole.Warrior, "Ever fought a troll?", "Three. Ugly, all of them.", "Ooh. Bigger than the one in the tavern?", "...There's one in the tavern?");
        Hireling(CompanionRole.Warrior, "Don't scratch the cobbles with that thing.", "It's a sword, not a plough.", "Tell that to the cobbles.");

        Hireling(CompanionRole.Bard, "Play us something, bard!", "Later. I'm working.", "Working? You're walking.");
        Hireling(CompanionRole.Bard, "Know any good songs?", "Hundreds.", "Know any short ones?");
        HirelingFrom(ResidentTrade.Minstrel, CompanionRole.Bard,
            "Another lute in {town}? There's barely room for mine.",
            "Then play louder.",
            "Oh, it's like that, is it?",
            "Market square. Noon. Bring your best verse."
        );

        Hireling(CompanionRole.Mage, "Don't turn me into a toad!", "I wasn't planning to.", "...Planning to?");
        Hireling(CompanionRole.Mage, "Can you turn lead into gold?", "Not today.", "What good are you, then?");
        Hireling(CompanionRole.Mage, "Is it true you can read minds?", "No.", "That's exactly what a mind reader would say.");

        HirelingFrom(ResidentTrade.Watchman, null, "Keep your weapons sheathed in town, {b}.", "Always.", "Good. I've enough on my hands.");
        HirelingFrom(ResidentTrade.Beggar, null, "Spare a coin for the needy, {b}?", "I'm hired help. I am the needy.", "Fair.");
        HirelingFrom(ResidentTrade.Lurker, null, "Selling anything, {b}?", "Not mine to sell.", "Pity. I'd have given you a good price.");
        HirelingFrom(ResidentTrade.Lurker, CompanionRole.Warrior, "Nice blade. What'd it cost?", "Not for sale.", "Everything's for sale, friend.");
        HirelingFrom(ResidentTrade.Lurker, CompanionRole.Mage, "Got any spare reagents?", "Not for you.", "Charming.");
        HirelingFrom(ResidentTrade.Drunk, null, "You! You look like you've seen things!", "I have.", "Tell me all of it. Slowly. *hic*");
    }

    private static void NewsAbout(NewsKind kind, params string[] lines) => Add(new Dialogue(lines) { News = kind });

    private static void NewsTalk()
    {
        NewsAbout(NewsKind.BigKill, "Did you hear? {who} slew {what} in {where}!", "Never!", "Heard it from the smith, who heard it from a guard.");
        NewsAbout(NewsKind.BigKill, "They're saying {who} killed {what}.", "{who}? Really?", "That's what they're saying.");
        NewsAbout(NewsKind.BigKill, "{who} brought down {what}, out in {where}.", "Brave fool.", "Or a lucky one.");
        NewsAbout(NewsKind.BigKill, "Know what {who} did? Killed {what}. In {where}!", "I'd have run.", "You run from geese, {b}.");

        NewsAbout(NewsKind.Died, "Heard {who} fell in {where}. {what} got them.", "Poor soul.", "The healers will see to it. They always do.");
        NewsAbout(NewsKind.Died, "{what} killed {who}, out in {where}.", "Again? That's someone every week.", "Adventurers. Never learn.");

        NewsAbout(NewsKind.Murder, "{who} was murdered, out in {where}.", "Murdered! By who?", "{what}, they say.", "Then {what} had better not show their face here.");
        NewsAbout(NewsKind.Murder, "Keep an eye out for {what}. Killed {who}, I hear.", "In cold blood?", "Is there any other kind?");

        NewsAbout(NewsKind.Grandmaster, "{who} is a grandmaster of {what} now, they say.", "Grandmaster! All that practice.", "Or witchcraft.");
        NewsAbout(NewsKind.Grandmaster, "Have you heard? {who} mastered {what}.", "I can't even master breakfast.");

        NewsAbout(NewsKind.Newcomer, "New face in Britannia, I hear. {who}, the name was.", "They'll learn.", "We all did.");
        NewsAbout(NewsKind.Newcomer, "Another adventurer, fresh off the boat. {who}.", "Give it a week. They'll be in Despise with the rest.");

        NewsAbout(NewsKind.ResidentKilled, "I still can't believe {what} is gone.", "Killed in the street. Here!", "They say it was {who}.", "If I ever see {who}...");
        NewsAbout(NewsKind.ResidentKilled, "Did you know {what}?", "Everyone knew {what}.", "Who'd do such a thing?", "{who}. That's who.");

        NewsAbout(NewsKind.CompanionLost, "{who} lost a hired sword in {where}. {what}, the name was.", "Dangerous work, that.", "Pays well, though.");
    }
}
