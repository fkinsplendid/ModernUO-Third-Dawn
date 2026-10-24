using System.Collections.Generic;

namespace Server.Engines.Companions;

/// <summary>
/// What a companion says about themselves: moods, weariness, fears, fondnesses, the things they
/// bring up with their employer, and their story. {arg} is the fear ("spiders"), the hometown, or
/// whatever the line is about.
/// </summary>
public static partial class CompanionSpeech
{
    private static readonly string[][] _moodLines = new string[(int)CompanionMood.Devoted + 1][];
    private static readonly string[][] _opinionLines = new string[(int)CompanionMood.Devoted + 1][];
    private static readonly string[][] _nothingOnMind = new string[(int)CompanionMood.Devoted + 1][];
    private static readonly string[][] _fearConfessions = new string[(int)CompanionFear.TheDark + 1][];
    private static readonly string[][] _fondnessTalk = new string[(int)CompanionFondness.Home + 1][];
    private static readonly string[][] _fondnessMet = new string[(int)CompanionFondness.Home + 1][];
    private static readonly string[][] _issueLines = new string[(int)CompanionIssue.Notice + 1][];
    private static readonly string[][] _promiseKept = new string[(int)CompanionIssue.Notice + 1][];
    private static readonly string[][] _promiseBroken = new string[(int)CompanionIssue.Notice + 1][];
    private static readonly Dictionary<(CompanionIssue, IssueAnswer), string[]> _issueReplies = [];
    private static readonly string[][] _origins = new string[3][];

    private static readonly string[] _fearNames =
    [
        "nothing", "spiders", "the dead", "snakes", "rats", "daemons", "dragons", "slimes", "giants", "the dark"
    ];

    private static void MindLines()
    {
        MoodLines();
        FearLines();
        FondnessLines();
        IssueLines();
        StoryLines();
    }

    public static string FearName(CompanionFear fear) => _fearNames[(int)fear];

    /// <summary>The fear a foe answers to, or None.</summary>
    public static CompanionFear FearOf(Mobile foe) =>
        KindOf(foe) switch
        {
            FoeKind.Crawler => CompanionFear.Spiders,
            FoeKind.Undead  => CompanionFear.Undead,
            FoeKind.Reptile => CompanionFear.Snakes,
            FoeKind.Vermin  => CompanionFear.Rats,
            FoeKind.Daemon  => CompanionFear.Daemons,
            FoeKind.Dragon  => CompanionFear.Dragons,
            FoeKind.Ooze    => CompanionFear.Ooze,
            FoeKind.Giant   => CompanionFear.Giants,
            _               => CompanionFear.None
        };

    public static string MoodLine(CompanionMood mood) => _moodLines[(int)mood].RandomElement();

    public static string OpinionLine(CompanionMood mood) => _opinionLines[(int)mood].RandomElement();

    public static string NothingOnMind(CompanionMood mood, bool waitingOnPromise) =>
        waitingOnPromise && Utility.RandomBool()
            ? "Nothing new. I'm waiting to see you keep your word, that's all."
            : _nothingOnMind[(int)mood].RandomElement();

    public static string TiredLine() =>
        Utility.RandomList(
            "Tired, if I'm honest.",
            "My feet ache and my eyes are heavy.",
            "I could do with a rest before long.",
            "Weary. Nothing a night in a bed wouldn't fix.",
            "Flagging a little. I'll manage."
        );

    public static string ExhaustedLine() =>
        Utility.RandomList(
            "Dead on my feet, {owner}. Truly.",
            "Exhausted. I'm swinging slower than I'd like.",
            "I need a bed. Not soon. Now.",
            "I can barely keep my eyes open.",
            "Worn to the bone. We've been out far too long."
        );

    public static string FearConfession(CompanionFear fear) => _fearConfessions[(int)fear]?.RandomElement();

    public static string FearEasingLine() =>
        Utility.RandomList(
            "Less than I used to, mind. Facing them again and again helps.",
            "Though it's getting easier. I hardly shake at all now.",
            "I'm getting over it, I think. Slowly.",
            "But each time we come through it, it shrinks a little."
        );

    public static string FondnessTalk(CompanionFondness fondness) => _fondnessTalk[(int)fondness]?.RandomElement();

    public static string FondnessLine(CompanionFondness fondness) => _fondnessMet[(int)fondness]?.RandomElement();

    public static string HomecomingLine() =>
        Utility.RandomList(
            "{arg}. Home. It's been too long.",
            "Ah, {arg}. I grew up on these streets.",
            "Home again. {arg} hasn't changed a bit.",
            "{arg}! I could walk these streets with my eyes shut.",
            "Back in {arg}. I should look in on my family while we're here."
        );

    public static string IssueLine(CompanionIssue issue) => _issueLines[(int)issue]?.RandomElement();

    public static string IssueReply(CompanionIssue issue, IssueAnswer answer) =>
        _issueReplies.TryGetValue((issue, answer), out var lines) ? lines.RandomElement() : answer switch
        {
            IssueAnswer.Yes => Utility.RandomList("Thank you, {owner}.", "Good. I'll hold you to that.", "That's all I ask."),
            IssueAnswer.No  => Utility.RandomList("...As you say.", "I see.", "Fine. I won't ask again. Not soon, anyway."),
            _               => Utility.RandomList("Later, then. I'll remember.", "Soon, I hope.", "Alright. But I'll ask again.")
        };

    public static string PromiseLine(CompanionIssue issue, bool kept) =>
        (kept ? _promiseKept : _promiseBroken)[(int)issue]?.RandomElement() ??
        (kept ? "You kept your word. I won't forget it." : "You gave me your word, {owner}.");

    public static string OriginLine(CompanionRole role, int story)
    {
        var lines = _origins[(int)role];
        return lines[story % lines.Length];
    }

    public static string MemoryLine(int story) => _memories[story / 7 % _memories.Length];

    public static string DreamLine(int story) => _dreams[story / 53 % _dreams.Length];

    private static void MoodLines()
    {
        _moodLines[(int)CompanionMood.Bitter] =
        [
            "How am I? Ask yourself how you've treated me.",
            "Miserable, since you ask. Not that you care.",
            "Counting the days until this contract's done.",
            "I've had better employers. I've had worse, but not many."
        ];
        _moodLines[(int)CompanionMood.Unhappy] =
        [
            "I've been better.",
            "Getting by. Just about.",
            "Could be worse. Could be a good deal better.",
            "Not my best days, these."
        ];
        _moodLines[(int)CompanionMood.Content] =
        [
            "Well enough, thank you.",
            "Fair to middling. Can't complain.",
            "Fit and ready.",
            "Good, all told. The work's honest and the pay's fair."
        ];
        _moodLines[(int)CompanionMood.Loyal] =
        [
            "Good! I like this work, and I like who I'm doing it for.",
            "In fine spirits, {owner}. Thank you for asking.",
            "Never better. Well, rarely better.",
            "Happy, honestly. It's a good company we keep."
        ];
        _moodLines[(int)CompanionMood.Devoted] =
        [
            "With you, {owner}? Always well.",
            "Happier than I've been in years. I mean that.",
            "I'd not trade this for any post in the land.",
            "Glad you asked. Gladder still that I signed on with you."
        ];

        _opinionLines[(int)CompanionMood.Bitter] =
        [
            "You want the truth? You're the worst employer I've had.",
            "I think you treat your hirelings like old boots.",
            "Honestly? I'm only here for the coin, and barely that.",
            "I think I made a mistake signing on with you."
        ];
        _opinionLines[(int)CompanionMood.Unhappy] =
        [
            "You could treat us better, {owner}. That's all I'll say.",
            "You're... alright. Some days.",
            "I've no great complaint. I've a few small ones.",
            "You pay. Mostly. Let's leave it there."
        ];
        _opinionLines[(int)CompanionMood.Content] =
        [
            "You're fair, and that's more than most.",
            "A decent employer. I've no complaints.",
            "You pay what you promise. I respect that.",
            "We'll see. So far, so good."
        ];
        _opinionLines[(int)CompanionMood.Loyal] =
        [
            "I trust you, {owner}. I don't say that lightly.",
            "You're a good leader. I'd follow you most places.",
            "You look after your own. That's rare.",
            "I'm proud to be in your company."
        ];
        _opinionLines[(int)CompanionMood.Devoted] =
        [
            "I'd walk into the Abyss for you, {owner}.",
            "You're more than an employer. You're a friend.",
            "Best thing I ever did, signing on with you.",
            "Wherever you go, I go. That's what I think of you."
        ];

        _nothingOnMind[(int)CompanionMood.Bitter] =
        [
            "Plenty. None of it you'd want to hear.",
            "Nothing I'd waste my breath on."
        ];
        _nothingOnMind[(int)CompanionMood.Unhappy] =
        [
            "Nothing worth raising. Not today.",
            "Nothing new."
        ];
        _nothingOnMind[(int)CompanionMood.Content] =
        [
            "Nothing at all. All's well.",
            "Not a thing. Lead on.",
            "Nothing troubling me."
        ];
        _nothingOnMind[(int)CompanionMood.Loyal] =
        [
            "Nothing, {owner}. We're doing well.",
            "Not a care in the world. Well, a few small ones, but none worth mentioning."
        ];
        _nothingOnMind[(int)CompanionMood.Devoted] =
        [
            "Only that I'm glad to be here.",
            "Nothing. You've seen to everything, as usual."
        ];
    }

    private static void FearLines()
    {
        _fearConfessions[(int)CompanionFear.Spiders] =
        [
            "Spiders. All those legs. All those eyes. I can't bear them.",
            "Spiders, if you must know. I wake up brushing them off me.",
            "Anything with eight legs. Don't laugh."
        ];
        _fearConfessions[(int)CompanionFear.Undead] =
        [
            "The dead that walk. Things that should be in the ground and aren't.",
            "The undead. I saw a lich once, as a child. I still see it when I close my eyes.",
            "Skeletons, zombies, all of it. The dead should stay dead."
        ];
        _fearConfessions[(int)CompanionFear.Snakes] =
        [
            "Snakes. The way they move. No legs, no warning.",
            "Serpents. A viper bit my brother when we were small. He lived. I've never been the same.",
            "Anything that slithers."
        ];
        _fearConfessions[(int)CompanionFear.Rats] =
        [
            "Rats. Laugh if you like. I grew up in a cellar full of them.",
            "Rats, and ratmen worse. The squeaking, the teeth.",
            "Vermin. Rats most of all. Their little hands."
        ];
        _fearConfessions[(int)CompanionFear.Daemons] =
        [
            "Daemons. Not the beast part. The way they smile.",
            "Anything out of the Abyss. They know your name before you tell them.",
            "Daemons. My grandmother said they collect the souls of cowards. I'm trying not to be one."
        ];
        _fearConfessions[(int)CompanionFear.Dragons] =
        [
            "Dragons. Who isn't? But I mean really. I freeze.",
            "Dragons. I watched one burn a village from a hillside once.",
            "Anything with wings and fire. My legs turn to water."
        ];
        _fearConfessions[(int)CompanionFear.Ooze] =
        [
            "Slimes. Things you can't cut properly. Things that eat your boots.",
            "Ooze. Jellies. Anything that wobbles at me.",
            "Slimes. I've seen what they do to a man's armour, and the man inside it."
        ];
        _fearConfessions[(int)CompanionFear.Giants] =
        [
            "Giants. Ettins, ogres, cyclopes. Anything that big shouldn't be able to move that fast.",
            "Big things. An ogre nearly stepped on me once. Nearly.",
            "Giants. You can't parry a tree trunk."
        ];
        _fearConfessions[(int)CompanionFear.TheDark] =
        [
            "The dark. Deep places. Tunnels that close in around you.",
            "Being underground. The weight of all that rock over my head.",
            "Dungeons. The dark, and what waits in it. I keep it hidden, mostly."
        ];
    }

    private static void FondnessLines()
    {
        _fondnessTalk[(int)CompanionFondness.Music] =
        [
            "Music. A good song makes the miles go faster.",
            "A lute by a fire, and a tune I half know. That's happiness.",
            "Music. I can't play a note, but I'd walk ten leagues to hear one."
        ];
        _fondnessTalk[(int)CompanionFondness.OpenRoad] =
        [
            "The open road. Sky above, nothing between me and the horizon.",
            "Walking. Just walking, through good country, nowhere to be.",
            "Fresh air and open fields. Towns crowd me."
        ];
        _fondnessTalk[(int)CompanionFondness.Treasure] =
        [
            "Treasure! The glint of something rare at the bottom of a chest.",
            "Finding things. Old things, magic things. Every corpse might hold a wonder.",
            "A good find. Nothing beats it. Not even gold, quite."
        ];
        _fondnessTalk[(int)CompanionFondness.Glory] =
        [
            "Glory. Bringing down something the bards will sing about.",
            "A worthy foe. The bigger the better.",
            "Making a name. I want people in taverns to argue about what I did."
        ];
        _fondnessTalk[(int)CompanionFondness.Home] =
        [
            "Home. {arg}. The smell of it, the noise of it.",
            "{arg}, and the people in it. I miss it more than I let on.",
            "Going home to {arg} with stories to tell."
        ];

        _fondnessMet[(int)CompanionFondness.Music] =
        [
            "Listen to that playing. That's lifted my heart.",
            "I could listen to that all day.",
            "That tune! My mother used to hum that one."
        ];
        _fondnessMet[(int)CompanionFondness.OpenRoad] =
        [
            "Now this is living. Open sky, open road.",
            "Breathe that air, {owner}. Nothing like it.",
            "I'd walk forever through country like this."
        ];
        _fondnessMet[(int)CompanionFondness.Treasure] =
        [
            "Now that's a find! This is why I do this.",
            "Magic! Real magic! Look at it shine.",
            "Oh, I do love a good find."
        ];
        _fondnessMet[(int)CompanionFondness.Glory] =
        [
            "Now THAT'S one for the songs!",
            "Did you see it fall? They'll be talking about that in every tavern.",
            "That's what I signed on for. Glory!"
        ];
        _fondnessMet[(int)CompanionFondness.Home] =
        [
            "{arg}! Home! Oh, it's good to be back.",
            "Home sweet {arg}. I feel ten years younger.",
            "Back in {arg}. Give me a moment to take it in."
        ];
    }

    private static void IssueLines()
    {
        Issue(CompanionIssue.Weary,
            [
                "{owner}, we've been at this for hours. Can we find a town and rest?",
                "I'm dead on my feet. Could we head somewhere with a bed?",
                "I'm no good to you this tired. A night's rest in town?",
                "We need to rest, {owner}. Will you take us back to town?"
            ],
            kept: ["At last. A bed, a meal, a roof. Thank you, {owner}.", "Town! You kept your word."],
            broken: ["You said we'd rest, {owner}. We haven't.", "So much for that rest you promised."]
        );
        Issue(CompanionIssue.WagesShort,
            [
                "My wages are due in {arg} minutes and my purse is light. Will you see to it?",
                "A word about pay, {owner}. Wages are due soon and I've not enough. Will you cover it?",
                "I hate to ask, but my purse won't cover tomorrow. Will you pay me?"
            ],
            kept: ["Paid in full. Thank you, {owner}."],
            broken: ["You said you'd pay me, {owner}. You didn't."]
        );
        Issue(CompanionIssue.EmployerReckless,
            [
                "{owner}, you've died {arg} times this past hour. Will you please be more careful?",
                "I can't keep raising you from the dead, {owner}. Promise me you'll take more care?",
                "You're throwing yourself at death. Will you slow down, for all our sakes?"
            ],
            kept: ["You've been more careful. Thank you. I sleep better for it.", "Not a single death. See? Much better."],
            broken: ["You promised you'd be careful!", "Again, {owner}? You gave me your word."]
        );
        Issue(CompanionIssue.LeftDead,
            [
                "You left me lying dead a long while back there, {owner}. Will you at least say sorry?",
                "I was a ghost for an age. Do you know how cold it is? An apology wouldn't go amiss.",
                "Next time I fall, will you come for me sooner? Promise me."
            ]
        );
        Issue(CompanionIssue.WantsMount,
            [
                "Everyone's riding but me. Could you find me a horse, {owner}?",
                "My boots are wearing through. A mount, perhaps? Will you get me one?",
                "I'm slowing you down on foot. Would you find me a horse?"
            ],
            kept: ["A horse of my own! Thank you, {owner}!", "Now we're travelling properly. Thank you."],
            broken: ["Still walking, I see. You did say you'd find me a horse."]
        );
        Issue(CompanionIssue.FacingFear,
            [
                "{owner}... could we stay clear of {arg} for a while? Please?",
                "I've had my fill of {arg} today. Can we hunt something else?",
                "I'm shaking still. Must we keep fighting {arg}?"
            ],
            kept: ["Thank you for keeping us clear of {arg}. I needed that.", "I feel steadier now. Thank you."],
            broken: ["You said we'd keep away from {arg}!", "{arg} again. You promised."]
        );
        Issue(CompanionIssue.MissesHome,
            [
                "I've not seen {arg} in so long. Could we stop there sometime?",
                "I miss home, {owner}. Might we pass through {arg} soon?",
                "Any chance we'll be near {arg}? I'd love to see my family."
            ],
            kept: ["You brought me home. I won't forget that, {owner}.", "{arg}. Thank you for bringing me."],
            broken: ["We never did get to {arg}, did we.", "So much for seeing home."]
        );
        Issue(CompanionIssue.Notice,
            [
                "I'll be honest, {owner}. I'm close to walking away. Will you do better by me?",
                "This isn't working. Unless things change, I'm going back to the guild. Will they change?",
                "I'm giving you fair warning: treat me better, or I'm gone. Well?"
            ]
        );

        Reply(CompanionIssue.Weary, IssueAnswer.Yes, "Thank you. Lead the way.", "Bless you. A bed sounds like heaven.");
        Reply(CompanionIssue.Weary, IssueAnswer.No, "...I'll keep going, then. Somehow.", "Hard master. Very well.");
        Reply(CompanionIssue.WagesShort, IssueAnswer.Yes, "Good. A purse keeps a hireling honest.", "Thank you. I'll not ask twice.");
        Reply(CompanionIssue.WagesShort, IssueAnswer.No, "Then don't be surprised if I'm gone when the coin is.", "No pay, no work. That's the guild rule, not mine.");
        Reply(CompanionIssue.EmployerReckless, IssueAnswer.Yes, "Thank you. I'd hate to lose you.", "Good. Let's all come home tonight.");
        Reply(CompanionIssue.EmployerReckless, IssueAnswer.No, "Then I'll keep my bandages handy.", "Your life. Your choice. I don't like it.");
        Reply(CompanionIssue.LeftDead, IssueAnswer.Yes, "Thank you. That means something.", "Alright. Forgiven.");
        Reply(CompanionIssue.LeftDead, IssueAnswer.No, "I see. Good to know where I stand.", "Hm. I'll remember that.");
        Reply(CompanionIssue.WantsMount, IssueAnswer.Yes, "Wonderful! Something sturdy, if you can.", "Thank you! I'll take good care of it.");
        Reply(CompanionIssue.WantsMount, IssueAnswer.No, "Walking it is. Again.", "My poor feet.");
        Reply(CompanionIssue.FacingFear, IssueAnswer.Yes, "Thank you. Truly.", "I owe you for that.");
        Reply(CompanionIssue.FacingFear, IssueAnswer.No, "...Then I'll face them. Somebody has to.", "Right. Deep breaths.");
        Reply(CompanionIssue.MissesHome, IssueAnswer.Yes, "Really? Oh, thank you!", "I'll show you the best tavern in {arg}.");
        Reply(CompanionIssue.MissesHome, IssueAnswer.No, "No. Of course. The work comes first.", "Another time, maybe.");
        Reply(CompanionIssue.Notice, IssueAnswer.Yes, "Then prove it. Words are cheap.", "We'll see. I'm watching.");
        Reply(CompanionIssue.Notice, IssueAnswer.No, "Then we're done. I'm going.", "So be it. I'll go once it's safe.");
        Reply(CompanionIssue.Notice, IssueAnswer.Later, "Later? There may not be a later.", "Don't take too long.");
    }

    private static void Issue(CompanionIssue issue, string[] raise, string[] kept = null, string[] broken = null)
    {
        _issueLines[(int)issue] = raise;
        _promiseKept[(int)issue] = kept;
        _promiseBroken[(int)issue] = broken;
    }

    private static void Reply(CompanionIssue issue, IssueAnswer answer, params string[] lines) =>
        _issueReplies[(issue, answer)] = lines;

    private static readonly string[] _memories =
    [
        "My father kept bees. I still can't hear buzzing without thinking of him.",
        "I had a dog called Turnip. Best friend I ever had, before you lot.",
        "I was nearly married once. Nearly. She ran off with a fisherman.",
        "When I was small I stowed away on a ship to Jhelom. They sent me home in a crate.",
        "My mother still thinks I'm a clerk. Don't tell her.",
        "I had a twin. She's a priestess now. She prays for me. I suspect I need it.",
        "The first thing I ever killed was a rabbit. I cried for a week.",
        "I won a pie-eating contest in Vesper once. Twelve pies. I don't like to talk about the night after.",
        "I grew up poor. Bread and onions, every day. I still can't look at an onion.",
        "I saw Lord British once, at a festival. He waved. At me, I swear it.",
        "There was a fire when I was young. I got my little brother out. I don't remember how.",
        "An old soldier taught me to whittle. I still carry the knife he gave me."
    ];

    private static readonly string[] _dreams =
    [
        "One day I'll buy a little farm and never see the inside of a dungeon again.",
        "I want to open a tavern. Good ale, honest prices, no brawling. Well, some brawling.",
        "I'd like to sail. All the way round the world, just to see it.",
        "I want my name in a book. Any book. Even a bad one.",
        "I'm saving to buy my mother a house. A proper one, with glass in the windows.",
        "Someday I'll train youngsters, the way someone once trained me.",
        "I want to see every shrine of the Virtues before I die.",
        "Honestly? I just want to grow old. In this line of work, that's ambition enough."
    ];

    private static void StoryLines()
    {
        _origins[(int)CompanionRole.Warrior] =
        [
            "I'm from {arg}. Smith's youngest. Swung hammers till I could swing a sword.",
            "{arg} born and bred. Joined the town guard at fifteen, left at twenty when the pay ran dry.",
            "I grew up in {arg}. Fought my way out of every alley in it, then decided I might as well be paid for it.",
            "{arg}. My father was a soldier, and his father. I never thought to be anything else.",
            "From {arg}. A mercenary captain took me on as a page. I learned the rest by not dying."
        ];
        _origins[(int)CompanionRole.Bard] =
        [
            "I'm from {arg}. Sang for coppers in the market before I could read.",
            "{arg}. My mother played the harp at the tavern. I learned at her knee.",
            "Born in {arg}. Ran away with a troupe of players when I was twelve.",
            "From {arg}. I wanted to be a hero. Turns out I'm better at singing about them.",
            "{arg}. Apprenticed to a lute maker. Played more than I made, so he threw me out."
        ];
        _origins[(int)CompanionRole.Mage] =
        [
            "I'm from {arg}. Set my teacher's beard alight with my first spell. He took me on anyway.",
            "{arg}. I spent my childhood in the library there. Then I read the wrong book.",
            "Born in {arg}, schooled in Moonglow. The Lyceum and I parted on... complicated terms.",
            "{arg}. A wandering wizard saw me light a candle without touching it. That was that.",
            "From {arg}. My family are all scholars. I wanted to see magic do something besides sit on a page."
        ];
    }
}
