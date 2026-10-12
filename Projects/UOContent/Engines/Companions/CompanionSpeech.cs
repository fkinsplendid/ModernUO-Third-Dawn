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
    BanterReply,
    CombatStart,
    Kill,
    Victory,
    LowHealth,
    EmployerHurt,
    EmployerDied,
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
    ThroughTheGate,
    NoGate,
    CannotCarry,
    Count
}

/// <summary>
/// Everything a companion can say. Lines are picked at random, mixing common lines with ones
/// specific to the companion's personality. Tokens: {owner}, {name}, {arg}.
/// </summary>
public static class CompanionSpeech
{
    private static readonly string[][] _common = new string[(int)SpeechContext.Count][];
    private static readonly string[][][] _flavour = new string[4][][];

    static CompanionSpeech()
    {
        for (var i = 0; i < _flavour.Length; i++)
        {
            _flavour[i] = new string[(int)SpeechContext.Count][];
        }

        Common(SpeechContext.Hired,
            "At your service, {owner}.",
            "Lead on. I'll be right behind you.",
            "Coin's agreed. Where are we headed?",
            "{name}, at your side. Let's see what the road holds."
        );
        Common(SpeechContext.Rehired,
            "Back in harness, then. Good to see you, {owner}.",
            "I was hoping you'd come for me.",
            "The guild hall was getting dull. Let's go."
        );
        Common(SpeechContext.Greeting,
            "There you are, {owner}.",
            "Rested and ready.",
            "Morning, or whatever hour it is. Ready when you are."
        );
        Common(SpeechContext.IdleTown,
            "The market's busy today.",
            "I could do with a hot meal and a soft bed.",
            "Smell that? Someone's baking bread.",
            "Keep a hand on your purse in crowds like this.",
            "Town guards everywhere. Makes a body feel safe, for once."
        );
        Common(SpeechContext.IdleWild,
            "Quiet out here. Too quiet, some would say.",
            "Watch the treeline.",
            "Good weather for walking, at least.",
            "I heard there are ruins somewhere near here.",
            "Did you hear that? ...No, nothing."
        );
        Common(SpeechContext.IdleDungeon,
            "Stay close. These halls twist.",
            "Something's watching us. I can feel it.",
            "Mind the bones on the floor.",
            "I hate the damp in these places.",
            "Torches won't last forever, {owner}."
        );
        Common(SpeechContext.IdleNight,
            "The stars are bright tonight.",
            "Night's when the worst things come out.",
            "We should find a fire soon.",
            "Can't see a blessed thing out here."
        );
        Common(SpeechContext.BanterReply,
            "Ha! Speak for yourself.",
            "I was thinking the same.",
            "If you say so.",
            "Hush, the both of you.",
            "Mm."
        );
        Common(SpeechContext.CombatStart,
            "Here they come!",
            "To arms!",
            "Watch your flank, {owner}!",
            "I'll take this one!"
        );
        Common(SpeechContext.Kill,
            "That's one less {arg}.",
            "Down it goes.",
            "And stay down.",
            "Next!"
        );
        Common(SpeechContext.Victory,
            "That's the last of them.",
            "Everyone still in one piece?",
            "Well fought.",
            "Let's catch our breath."
        );
        Common(SpeechContext.LowHealth,
            "I'm hurt badly!",
            "I can't take much more of this!",
            "Need healing here!"
        );
        Common(SpeechContext.EmployerHurt,
            "{owner}, you're bleeding!",
            "Hold on, {owner}!",
            "Fall back to me, {owner}!"
        );
        Common(SpeechContext.EmployerDied,
            "{owner}! No!",
            "They've cut down {owner}!",
            "Protect {owner}'s body!"
        );
        Common(SpeechContext.AllyDied,
            "{arg} is down!",
            "We've lost {arg}!",
            "Someone help {arg}!"
        );
        Common(SpeechContext.Revived,
            "I... I'm alive?",
            "Back from the brink. Thank you.",
            "That was far too close."
        );
        Common(SpeechContext.Healing,
            "Hold still.",
            "Let me see to that.",
            "This'll sting."
        );
        Common(SpeechContext.LevelUp,
            "I feel stronger. Level {arg}!",
            "I've learned a thing or two out here.",
            "Every fight teaches something."
        );
        Common(SpeechContext.LootGold,
            "Coin! {arg} pieces.",
            "There's gold here.",
            "{arg} gold. Not a bad haul."
        );
        Common(SpeechContext.LootFind,
            "Ooh, what's this?",
            "This might be worth something.",
            "Found something!"
        );
        Common(SpeechContext.ShareLoot,
            "Here's your share, {owner}.",
            "Spoils of the road. Take these.",
            "I'll pass these along to you."
        );
        Common(SpeechContext.Banked,
            "I've put the spoils in your bank box, {owner}.",
            "Your share's safe with the banker."
        );
        Common(SpeechContext.Equip,
            "This'll do nicely.",
            "Much better.",
            "Now that's proper gear."
        );
        Common(SpeechContext.ReceivedItem,
            "Thank you, {owner}.",
            "I'll put this to good use.",
            "Much obliged."
        );
        Common(SpeechContext.Paid,
            "{arg} gold. Thank you, {owner}.",
            "My purse thanks you.",
            "That'll keep me going a while."
        );
        Common(SpeechContext.WagesDue,
            "My wages are due in {arg} minutes, {owner}, and my purse is light.",
            "{owner}, I'll need paying soon.",
            "A word, {owner}: I'm owed coin before long."
        );
        Common(SpeechContext.LeavingUnpaid,
            "No coin, no service. I'll be at the guild if you need me.",
            "I can't work for free, {owner}. Find me at the guild hall.",
            "My purse is empty. I'm heading back to the guild."
        );
        Common(SpeechContext.Dismissed,
            "As you wish. You know where to find me.",
            "Back to the guild, then. Fare well, {owner}.",
            "Call on me again."
        );
        Common(SpeechContext.LowSupplies,
            "I'm running low on {arg}.",
            "{owner}, could you spare some {arg}?",
            "Not much {arg} left."
        );
        Common(SpeechContext.OutOfSupplies,
            "I'm out of {arg}!",
            "No more {arg}, {owner}!",
            "I need {arg}, and I need it now!"
        );
        Common(SpeechContext.Laden,
            "My pack's near bursting.",
            "We're weighed down with loot, {owner}.",
            "I can't carry much more."
        );
        Common(SpeechContext.GateEscape,
            "Too many! I'm opening a gate. Go, {owner}, go!",
            "We can't win this. Through the gate, now!",
            "Retreat! The gate's open!"
        );
        Common(SpeechContext.GateTown,
            "We're laden down. I've opened a gate to {arg}.",
            "Gate's open to {arg}. Let's sell this lot.",
            "Time to head back. Gate to {arg} is open."
        );
        Common(SpeechContext.ThroughTheGate,
            "Right behind you!",
            "Through we go.",
            "Coming!"
        );
        Common(SpeechContext.NoGate,
            "Something blocks my gate magic here!",
            "I can't open a gate in this place. Fall back!",
            "The gate won't form. Run!"
        );
        Common(SpeechContext.CannotCarry,
            "Your pack's too heavy, {owner}. I'll hold onto this for now.",
            "You can't carry any more. I'll keep it."
        );

        Flavour(CompanionPersonality.Stoic, SpeechContext.IdleWild, "...", "Keep moving.", "Stay alert.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.Kill, "Done.", "It is finished.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.Victory, "Clear.", "Area secure.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.BanterReply, "...", "Focus.");

        Flavour(CompanionPersonality.Cheerful, SpeechContext.IdleWild,
            "What a lovely day for an adventure!",
            "I bet there's treasure over the next hill!",
            "Do you think we'll see a dragon? From very far away, I mean."
        );
        Flavour(CompanionPersonality.Cheerful, SpeechContext.IdleTown,
            "Ooh, I want to look at every stall!",
            "Everyone's so friendly here!"
        );
        Flavour(CompanionPersonality.Cheerful, SpeechContext.Victory, "We did it! Ha!", "Hooray! Everyone's alright?");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.BanterReply, "Ha! That's a good one!", "Oh, cheer up!");

        Flavour(CompanionPersonality.Gruff, SpeechContext.IdleWild,
            "My feet hurt.",
            "If one more fly bites me...",
            "Bah. Walking, always walking."
        );
        Flavour(CompanionPersonality.Gruff, SpeechContext.IdleTown, "Too many people.", "Where's the nearest tavern?");
        Flavour(CompanionPersonality.Gruff, SpeechContext.Kill, "Hmph. Barely worth the effort.", "Good riddance.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.BanterReply, "Nobody asked you.", "Bah.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.LowSupplies, "We're short of {arg}. Again.");

        Flavour(CompanionPersonality.Witty, SpeechContext.IdleWild,
            "If I had a gold piece for every tree we've passed...",
            "Adventure, they said. Glory, they said.",
            "I'm composing an epic about this walk. It's very short."
        );
        Flavour(CompanionPersonality.Witty, SpeechContext.IdleDungeon,
            "Lovely decor. Very 'tomb chic'.",
            "Who builds a castle and puts the treasure in the basement?"
        );
        Flavour(CompanionPersonality.Witty, SpeechContext.Kill, "And the crowd goes wild.", "Next contestant!");
        Flavour(CompanionPersonality.Witty, SpeechContext.BanterReply, "Says the one who tripped on a rock earlier.", "Truly, the wisdom of the ages.");
    }

    private static void Common(SpeechContext context, params string[] lines) => _common[(int)context] = lines;

    private static void Flavour(CompanionPersonality personality, SpeechContext context, params string[] lines) =>
        _flavour[(int)personality][(int)context] = lines;

    public static string Pick(SpeechContext context, CompanionPersonality personality)
    {
        var common = _common[(int)context];
        var flavour = _flavour[(int)personality][(int)context];

        if (flavour != null && (common == null || Utility.RandomDouble() < 0.4))
        {
            return flavour.RandomElement();
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
}
