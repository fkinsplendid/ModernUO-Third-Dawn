using System;

namespace Server.Engines.Companions;

public enum SpeechContext
{
    Hired,
    Rehired,
    Greeting,
    IdleTown,
    IdleWild,
    IdleDungeon,
    IdleNight,
    Dawn,
    Dusk,
    Waiting,
    BanterReply,
    CombatStart,
    Kill,
    BigKill,
    Victory,
    LowHealth,
    Poisoned,
    EmployerHurt,
    EmployerDied,
    EmployerRevived,
    AllyDied,
    Revived,
    Healing,
    LevelUp,
    LootGold,
    LootFind,
    ShareLoot,
    Banked,
    Equip,
    ReceivedItem,
    Paid,
    WagesDue,
    LeavingUnpaid,
    Dismissed,
    LowSupplies,
    OutOfSupplies,
    Laden,
    GateEscape,
    GateTown,
    Retreat,
    ShakenOff,
    Outmatched,
    Summoning,
    ThroughTheGate,
    NoGate,
    NoRuneToGate,
    Fizzle,
    CannotCarry,
    GhostPlea,
    GhostAsksStaff,
    SteedGiven,
    SteedLost,
    SteedWanted,
    SteedSick,
    SteedReturned,
    Dismount,
    MountUp,
    GearWorn,
    GearFailing,
    GearSound,
    NoSmith,
    ToTheSmith,
    HandToSmith,
    Repaired,
    RepairDeclined,
    UnlockStart,
    Unlocked,
    LockTooHard,
    GreetingWarm,
    GreetingCold,
    RehiredWary,
    IdleWeary,
    IdleUnhappy,
    IdleDevoted,
    IdleDark,
    FearSighted,
    FearSteeled,
    FearBraved,
    FearConquered,
    FearOvercome,
    Fearless,
    Praised,
    PraisedAgain,
    Insulted,
    ApologyAccepted,
    ApologyUnneeded,
    StoryRefused,
    IssueIgnored,
    OnCredit,
    DebtSettled,
    NoticeWithdrawn,
    Quits,
    Count
}

/// <summary>
/// Everything a companion can say. Lines are picked at random from three pools: lines anyone
/// might say, lines in the companion's own manner (personality), and lines about its trade
/// (role). Places and foes have pools of their own. Tokens: {owner}, {name}, {arg}.
/// </summary>
public static partial class CompanionSpeech
{
    private const int PersonalityCount = 4;
    private const int RoleCount = 3;

    // How often a line comes from the companion's own manner or trade rather than the common pool.
    private const double PersonalityChance = 0.35;
    private const double RoleChance = 0.3;

    private static readonly string[][] _common = new string[(int)SpeechContext.Count][];
    private static readonly string[][][] _flavour = new string[PersonalityCount][][];
    private static readonly string[][][] _roleLines = new string[RoleCount][][];

    static CompanionSpeech()
    {
        for (var i = 0; i < _flavour.Length; i++)
        {
            _flavour[i] = new string[(int)SpeechContext.Count][];
        }

        for (var i = 0; i < _roleLines.Length; i++)
        {
            _roleLines[i] = new string[(int)SpeechContext.Count][];
        }

        CommonLines();
        PersonalityLines();
        RoleLines();
        SteedLines();
        PlaceLines();
        FoeLines();
        SmithingLines();
        LockLines();
        MindLines();
        MindContextLines();
    }

    private static void CommonLines()
    {
        Common(SpeechContext.Hired,
            "At your service, {owner}.",
            "Lead on. I'll be right behind you.",
            "Coin's agreed. Where are we headed?",
            "{name}, at your side. Let's see what the road holds.",
            "You've hired well, {owner}. You'll see.",
            "A new contract! Let me fetch my pack.",
            "Fair wages and a good cause. What more could I ask?"
        );
        Common(SpeechContext.Rehired,
            "Back in harness, then. Good to see you, {owner}.",
            "I was hoping you'd come for me.",
            "The guild hall was getting dull. Let's go.",
            "You again! I'd hoped so.",
            "Missed me, did you?"
        );
        Common(SpeechContext.Greeting,
            "There you are, {owner}.",
            "Rested and ready.",
            "Morning, or whatever hour it is. Ready when you are.",
            "Ah, {owner}. Where to today?",
            "I've sharpened, mended and packed. Let's go.",
            "Back again? Good. I was getting restless."
        );
        Common(SpeechContext.IdleTown,
            "The market's busy today.",
            "I could do with a hot meal and a soft bed.",
            "Smell that? Someone's baking bread.",
            "Keep a hand on your purse in crowds like this.",
            "Town guards everywhere. Makes a body feel safe, for once.",
            "I wonder what the town crier's shouting about today.",
            "That smith's been hammering since dawn.",
            "Prices have gone up again, I swear it.",
            "Someone's selling fish. Strongly.",
            "A bath. That's what I want. A proper hot bath.",
            "I heard two merchants arguing over a cart of wool. Very heated.",
            "If you need to buy anything, {owner}, now's the time."
        );
        Common(SpeechContext.IdleWild,
            "Quiet out here. Too quiet, some would say.",
            "Watch the treeline.",
            "Good weather for walking, at least.",
            "I heard there are ruins somewhere near here.",
            "Did you hear that? ...No, nothing.",
            "Deer tracks. Fresh ones.",
            "Smell the pine. Better than any town.",
            "My grandmother said never to trust a quiet road.",
            "Is it me, or are the birds following us?",
            "Brigands like country like this. Keep your eyes open.",
            "I could walk these roads forever. Well, until supper.",
            "There's a stream nearby. I can hear it."
        );
        Common(SpeechContext.IdleDungeon,
            "Stay close. These halls twist.",
            "Something's watching us. I can feel it.",
            "Mind the bones on the floor.",
            "I hate the damp in these places.",
            "Torches won't last forever, {owner}.",
            "Did that wall just move?",
            "Listen... dripping. Or chewing.",
            "Whoever built this place wasn't planning on visitors.",
            "Watch your step. Traps love a careless boot.",
            "The air's foul down here. Breathe through your mouth.",
            "If we get lost, I'm blaming you, {owner}.",
            "Echoes. I hate echoes. You never know which one's answering back."
        );
        Common(SpeechContext.IdleNight,
            "The stars are bright tonight.",
            "Night's when the worst things come out.",
            "We should find a fire soon.",
            "Can't see a blessed thing out here.",
            "Both moons are up. Trammel and Felucca, side by side.",
            "Owls. Or something pretending to be owls.",
            "I don't like the sound of those wolves.",
            "A clear night. Cold, though.",
            "Keep close. Easy to lose each other in the dark."
        );
        Common(SpeechContext.Dawn,
            "Sun's coming up. Good. I never liked the dark.",
            "First light. Best time for walking.",
            "Dawn already? We've been at this all night.",
            "Listen to the birds. The world's waking up.",
            "Mist on the ground. It'll burn off soon."
        );
        Common(SpeechContext.Dusk,
            "Sun's going down. Let's not be caught in the open.",
            "Getting dark. Stay close.",
            "Look at that sky. Red as a dragon's belly.",
            "Evening already. Where does the day go?",
            "The shadows are getting long. So is my patience with this road."
        );
        Common(SpeechContext.Waiting,
            "Are we waiting for someone, {owner}?",
            "I'll just... stand here, then.",
            "My legs are going stiff.",
            "Shall we move on, {owner}?",
            "*taps foot*",
            "I could have mended my boots in the time we've stood here.",
            "Lost, are we? I won't tell anyone."
        );
        Common(SpeechContext.BanterReply,
            "Ha! Speak for yourself.",
            "I was thinking the same.",
            "If you say so.",
            "Hush, the both of you.",
            "Mm.",
            "You say that every time.",
            "Is that so? Fascinating.",
            "Not this again.",
            "For once, I agree."
        );
        Common(SpeechContext.CombatStart,
            "Here they come!",
            "To arms!",
            "Watch your flank, {owner}!",
            "I'll take this one!",
            "Steady now!",
            "Look lively!",
            "Ambush!",
            "Back to back!"
        );
        Common(SpeechContext.Kill,
            "That's one less {arg}.",
            "Down it goes.",
            "And stay down.",
            "Next!",
            "Got it!",
            "That's that.",
            "One down!"
        );
        Common(SpeechContext.BigKill,
            "Did you see the size of that {arg}?!",
            "We felled that {arg}! Someone tell the bards!",
            "That {arg} is dead! I can scarcely believe it.",
            "They'll be singing about this {arg} in every tavern.",
            "Look at it. Just look at it. We did that."
        );
        Common(SpeechContext.Victory,
            "That's the last of them.",
            "Everyone still in one piece?",
            "Well fought.",
            "Let's catch our breath.",
            "Is it over? I think it's over.",
            "Check yourselves for wounds.",
            "Ha! Not today."
        );
        Common(SpeechContext.LowHealth,
            "I'm hurt badly!",
            "I can't take much more of this!",
            "Need healing here!",
            "I'm bleeding out!",
            "Help me!"
        );
        Common(SpeechContext.Poisoned,
            "Poison! I can feel it in my veins!",
            "Ugh, I'm poisoned...",
            "Something's in my blood. Poison!",
            "My head's spinning. Poison!"
        );
        Common(SpeechContext.EmployerHurt,
            "{owner}, you're bleeding!",
            "Hold on, {owner}!",
            "Fall back to me, {owner}!",
            "{owner}! Get behind me!",
            "Mind yourself, {owner}!"
        );
        Common(SpeechContext.EmployerDied,
            "{owner}! No!",
            "They've cut down {owner}!",
            "Protect {owner}'s body!",
            "Avenge {owner}!",
            "We've lost {owner}! Hold the line!"
        );
        Common(SpeechContext.EmployerRevived,
            "{owner}! Welcome back to the living!",
            "Thank the Virtues. Don't do that again, {owner}.",
            "There you are. We thought we'd lost you.",
            "Back on your feet! Good. Your share of the work was piling up.",
            "{owner} lives! Ha!"
        );
        Common(SpeechContext.AllyDied,
            "{arg} is down!",
            "We've lost {arg}!",
            "Someone help {arg}!",
            "No! {arg}!",
            "{arg} has fallen!"
        );
        Common(SpeechContext.Revived,
            "I... I'm alive?",
            "Back from the brink. Thank you.",
            "That was far too close.",
            "I saw a light... and then your ugly face. No offence.",
            "Cold. It was so cold. Thank you.",
            "Right. Where was I? Ah yes. Losing."
        );
        Common(SpeechContext.Healing,
            "Hold still.",
            "Let me see to that.",
            "This'll sting.",
            "Deep, but clean.",
            "Bite on something.",
            "You'll live."
        );
        Common(SpeechContext.LevelUp,
            "I feel stronger. Level {arg}!",
            "I've learned a thing or two out here.",
            "Every fight teaches something.",
            "Level {arg}! The guild will hear of this.",
            "I'm getting good at this. Level {arg}."
        );
        Common(SpeechContext.LootGold,
            "Coin! {arg} pieces.",
            "There's gold here.",
            "{arg} gold. Not a bad haul.",
            "Clink clink! {arg} gold.",
            "{arg} gold. It won't be needing it now."
        );
        Common(SpeechContext.LootFind,
            "Ooh, what's this?",
            "This might be worth something.",
            "Found something!",
            "Now that's interesting.",
            "Someone's going to want this.",
            "Well, well. Look here."
        );
        Common(SpeechContext.ShareLoot,
            "Here's your share, {owner}.",
            "Spoils of the road. Take these.",
            "I'll pass these along to you.",
            "This lot's yours, {owner}.",
            "Fair's fair. Here you go."
        );
        Common(SpeechContext.Banked,
            "I've put the spoils in your bank box, {owner}.",
            "Your share's safe with the banker.",
            "Banked. The clerk gave me a funny look, but it's done."
        );
        Common(SpeechContext.Equip,
            "This'll do nicely.",
            "Much better.",
            "Now that's proper gear.",
            "Fits like it was made for me.",
            "How do I look?"
        );
        Common(SpeechContext.ReceivedItem,
            "Thank you, {owner}.",
            "I'll put this to good use.",
            "Much obliged.",
            "For me? You shouldn't have.",
            "I'll take good care of it."
        );
        Common(SpeechContext.Paid,
            "{arg} gold. Thank you, {owner}.",
            "My purse thanks you.",
            "That'll keep me going a while.",
            "{arg} gold! You're a fair employer.",
            "Coin well spent, I promise you."
        );
        Common(SpeechContext.WagesDue,
            "My wages are due in {arg} minutes, {owner}, and my purse is light.",
            "{owner}, I'll need paying soon.",
            "A word, {owner}: I'm owed coin before long.",
            "Not to be indelicate, {owner}, but my purse is near empty."
        );
        Common(SpeechContext.LeavingUnpaid,
            "No coin, no service. I'll be at the guild if you need me.",
            "I can't work for free, {owner}. Find me at the guild hall.",
            "My purse is empty. I'm heading back to the guild.",
            "Sorry, {owner}. Even heroes have to eat."
        );
        Common(SpeechContext.Dismissed,
            "As you wish. You know where to find me.",
            "Back to the guild, then. Fare well, {owner}.",
            "Call on me again.",
            "It's been an honour, {owner}.",
            "Until the road brings us together again."
        );
        Common(SpeechContext.LowSupplies,
            "I'm running low on {arg}.",
            "{owner}, could you spare some {arg}?",
            "Not much {arg} left.",
            "We'll want more {arg} soon, {owner}."
        );
        Common(SpeechContext.OutOfSupplies,
            "I'm out of {arg}!",
            "No more {arg}, {owner}!",
            "I need {arg}, and I need it now!"
        );
        Common(SpeechContext.Laden,
            "My pack's near bursting.",
            "We're weighed down with loot, {owner}.",
            "I can't carry much more.",
            "One more trinket and my back gives out.",
            "I'm clanking like a tinker's cart."
        );
        Common(SpeechContext.GateEscape,
            "Too many! I'm opening a gate. Go, {owner}, go!",
            "We can't win this. Through the gate, now!",
            "Retreat! The gate's open!",
            "Into the gate! Now!"
        );
        Common(SpeechContext.Retreat,
            "Fall back! Stay with {owner}!",
            "Right behind you, {owner}!",
            "Disengage! Go, go!",
            "Back! Everyone back!",
            "Running! Don't look back!",
            "Breaking off!"
        );
        Common(SpeechContext.ShakenOff,
            "I think we lost them.",
            "Nothing behind us. Catch your breath.",
            "That was too close.",
            "They've given up the chase.",
            "Well. Discretion, valour, and so on."
        );
        Common(SpeechContext.Outmatched,
            "That {arg} is more than we can take, {owner}. Say the word and we fall back!",
            "{owner}, we're outmatched here! Call the retreat if it turns!",
            "This is going badly. Shout 'retreat' and we run!",
            "Too much for us, {owner}! Your call: fight or flee?"
        );
        Common(SpeechContext.Summoning,
            "Blades, find them!",
            "Something sharp for our friend there.",
            "Let the spirits deal with it!",
            "I'm calling help. Stand clear of the whirlwind!"
        );
        Common(SpeechContext.GateTown,
            "We're laden down. I've opened a gate to {arg}.",
            "Gate's open to {arg}. Let's sell this lot.",
            "Time to head back. Gate to {arg} is open."
        );
        Common(SpeechContext.ThroughTheGate,
            "Right behind you!",
            "Through we go.",
            "Coming!",
            "Mind the step. It's a long one.",
            "I'll never get used to that feeling."
        );
        Common(SpeechContext.NoGate,
            "Something blocks my gate magic here!",
            "I can't open a gate in this place. Fall back!",
            "The gate won't form. Run!"
        );
        Common(SpeechContext.NoRuneToGate,
            "I've no rune to gate us out. Run!",
            "No marked place to gate to. Fall back!",
            "If only I'd a rune for this. Run for it!"
        );
        Common(SpeechContext.Fizzle,
            "Blast, it fizzled! Again...",
            "The words slipped. Once more!",
            "Hold on, let me try that again.",
            "Curses, lost the thread of it. Again!"
        );
        Common(SpeechContext.CannotCarry,
            "Your pack's too heavy, {owner}. I'll hold onto this for now.",
            "You can't carry any more. I'll keep it."
        );

        Common(SpeechContext.GhostPlea,
            "{owner}... I need a healer...",
            "*a faint voice* Raise me, {owner}...",
            "It's cold here, {owner}. Find me a healer.",
            "I'm not done yet, {owner}. Get me back on my feet.",
            "*the ghost drifts close, flickering*",
            "Everything's grey, {owner}. Please...",
            "oOoOo... a healer, {owner}... oOoOo..."
        );
        Common(SpeechContext.GhostAsksStaff,
            "Please... grant me life again.",
            "You have the power. Raise me, I beg you.",
            "*the ghost reaches out a pale hand*",
            "I've more to give. Let me live again."
        );
    }

    private static void Common(SpeechContext context, params string[] lines) => _common[(int)context] = lines;

    private static void Flavour(CompanionPersonality personality, SpeechContext context, params string[] lines) =>
        _flavour[(int)personality][(int)context] = lines;

    private static void Trade(CompanionRole role, SpeechContext context, params string[] lines) =>
        _roleLines[(int)role][(int)context] = lines;

    /// <summary>The only things a ghost has to say.</summary>
    public static bool IsGhostLine(SpeechContext context) =>
        context is SpeechContext.GhostPlea or SpeechContext.GhostAsksStaff;

    public static string Pick(SpeechContext context, CompanionPersonality personality, CompanionRole role)
    {
        var common = _common[(int)context];
        var flavour = _flavour[(int)personality][(int)context];
        var trade = _roleLines[(int)role][(int)context];

        if (flavour != null && (common == null && trade == null || Utility.RandomDouble() < PersonalityChance))
        {
            return flavour.RandomElement();
        }

        if (trade != null && (common == null || Utility.RandomDouble() < RoleChance))
        {
            return trade.RandomElement();
        }

        return common?.RandomElement();
    }

    public static string Format(string line, BaseCompanion speaker, string arg)
    {
        if (line.Contains('{'))
        {
            line = line
                .Replace("{owner}", speaker.Employer?.Name ?? "friend", StringComparison.Ordinal)
                .Replace("{name}", speaker.Name, StringComparison.Ordinal)
                .Replace("{arg}", arg ?? "", StringComparison.Ordinal);
        }

        return line;
    }

    /// <summary>"a lich lord" becomes "lich lord", so lines can say "that lich lord". Proper names are kept.</summary>
    public static string BareName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "beast";
        }

        name = name.Trim();

        foreach (var article in (ReadOnlySpan<string>)["a ", "an ", "the "])
        {
            if (name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                return name[article.Length..];
            }
        }

        return name;
    }
}
