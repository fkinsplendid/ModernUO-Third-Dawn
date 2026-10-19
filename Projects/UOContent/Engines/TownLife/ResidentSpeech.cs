using Server.Mobiles;

namespace Server.Engines.TownLife;

/// <summary>
/// What townsfolk say. An idle remark is about their trade, the place they're at, or the time
/// of day, or local to their town; greetings read the passer-by (a ghost, a murderer, someone famous).
/// Tokens: {keeper} is the vendor who runs the place they're at (lines needing it are skipped where
/// there's none); {town} is the town's name.
/// </summary>
public static class ResidentSpeech
{
    private static readonly string[][] _trade = new string[(int)ResidentTrade.Count][];
    private static readonly string[][] _place = new string[(int)PlaceKind.Count][];
    private static readonly string[][] _phase = new string[(int)DayPhase.Count][];
    private static readonly string[][] _greeting = new string[(int)ResidentManner.Count][];

    static ResidentSpeech()
    {
        _trade[(int)ResidentTrade.Farmer] =
        [
            "Rain'd do these fields a world of good.",
            "Crows had half the seed again.",
            "Turnips. Always turnips. Nobody ever thanks a man for turnips.",
            "Harvest'll be thin this year if the weather holds dry.",
            "My back's older than I am.",
            "Lord British eats bread from these fields, you know. Well. Probably."
        ];
        _trade[(int)ResidentTrade.Fisher] =
        [
            "Nothing biting but the flies.",
            "Saw a sea serpent off the point once. Nobody believes me.",
            "Tide's turning. Should pick up soon.",
            "Best catch is at first light, before the boats churn it all up.",
            "Smell that? That's the smell of an honest living.",
            "Lost a good line to something big yesterday."
        ];
        _trade[(int)ResidentTrade.Smith] =
        [
            "Master says my welds are getting better. Says it like it hurts.",
            "Iron's dear this season. Miners want the moon.",
            "Burnt my hand again. Third time this week.",
            "One day I'll forge a blade worth a name.",
            "Mind the sparks!",
            "Horseshoes, horseshoes, horseshoes..."
        ];
        _trade[(int)ResidentTrade.Townsfolk] =
        [
            "Did you hear? The miller's daughter is to be wed.",
            "Prices at the market are a scandal.",
            "I've not seen the guards this busy in years.",
            "They say Blackthorn's castle has more rooms than anyone's counted.",
            "My cousin went to Ilshenar. Never wrote, not once.",
            "Hard times, hard times..."
        ];
        _trade[(int)ResidentTrade.Shopper] =
        [
            "Now what was it I came for?",
            "Three loaves, a bit of cheese, and something for the cough.",
            "I'll not pay that much for a bolt of cloth.",
            "This list gets longer every week.",
            "The good stalls sell out by noon."
        ];
        _trade[(int)ResidentTrade.Drunk] =
        [
            "I'm not drunk. I'm... *hic* ...celebrating.",
            "Who moved the street?",
            "You're a good friend. My best friend. Who are you?",
            "One more and then home. Probably.",
            "*sings tunelessly*",
            "The ground keeps coming up at me."
        ];
        _trade[(int)ResidentTrade.Minstrel] =
        [
            "A song for a coin? A verse for a smile?",
            "I'm working on a ballad about the Shame dungeon. It's mostly screaming.",
            "Nothing rhymes with Britannia. I've tried.",
            "The Lord's court hasn't sent for me. Yet.",
            "Hum along if you know it!"
        ];
        _trade[(int)ResidentTrade.Watchman] =
        [
            "Keep to the lit streets after dark.",
            "Quiet night. I like quiet nights.",
            "Seen anyone lurking about the back alleys?",
            "All's well. So far.",
            "Lamp oil's low. Always is by the third watch."
        ];
        _trade[(int)ResidentTrade.Beggar] =
        [
            "Alms! Alms for the poor!",
            "A coin, for an old soldier?",
            "Spare a crust, friend?",
            "Compassion is a virtue, they say. Is it?",
            "Bless you, whoever you are."
        ];
        _trade[(int)ResidentTrade.Courier] =
        [
            "Can't stop, letters to deliver!",
            "Who writes this much? Honestly.",
            "Urgent, they said. They always say urgent.",
            "Past the bank, round the healer's... right.",
            "My feet know every cobble in {town}."
        ];
        _trade[(int)ResidentTrade.Noble] =
        [
            "One does so tire of the court.",
            "The castle kitchens have gone downhill.",
            "My tailor is a genius. An expensive genius.",
            "Lord British asked after my health. Well, he nodded.",
            "One must be seen in town, now and then."
        ];
        _trade[(int)ResidentTrade.Lurker] =
        [
            "Selling a fine bow! Barely used!",
            "Anyone heading to Despise? I'll hold the torch.",
            "Buying spider's silk! Fair prices!",
            "Black pearl, fresh from Moonglow! Who's buying?",
            "Best spot in {town}, this. Everyone comes through here eventually.",
            "Anyone got a rune to Yew? I'll pay.",
            "I'm not loitering. I'm waiting. It's a different thing.",
            "Trading a lockpick set for a decent cloak. Anyone?",
            "They say there's a dragon's hoard in Destard. Someone should go. Not me.",
            "Good things come through those doors. You just have to wait.",
            "Seen anything worth buying today?"
        ];

        _place[(int)PlaceKind.Tavern] =
        [
            "{keeper} waters the ale, I swear it.",
            "Another round!",
            "This stew's been on the fire since the last moon.",
            "To Lord British! And to me.",
            "Best seat in the house, this.",
            "Who's paying? Not me."
        ];
        _place[(int)PlaceKind.Bank] =
        [
            "Queue's out the door again.",
            "I keep my savings in a sock, myself. Don't tell anyone.",
            "{keeper} counts slower than anyone alive.",
            "Gold's safe here. Safer than under the mattress."
        ];
        _place[(int)PlaceKind.Market] =
        [
            "How much? For that?",
            "{keeper} gave me a fair price, for once.",
            "Fresh today, they say. They always say that.",
            "I'll take two. No, three.",
            "Mind your purse in the crowd."
        ];
        _place[(int)PlaceKind.Smithy] =
        [
            "Hot work, honest work.",
            "{keeper} works that forge from dawn till dusk.",
            "Hear that ring? That's good steel."
        ];
        _place[(int)PlaceKind.Docks] =
        [
            "Ship in on the morning tide.",
            "Smells like fish and tar. Smells like home.",
            "Watch the planks, some are rotten."
        ];
        _place[(int)PlaceKind.Fields] =
        [
            "Good soil, this. Black as a cat.",
            "Mind the furrows.",
            "Scarecrow's not scaring much."
        ];
        _place[(int)PlaceKind.Healer] =
        [
            "*coughs*",
            "{keeper} says it's nothing serious. Says that every time.",
            "Something's going round. Everyone's sneezing."
        ];
        _place[(int)PlaceKind.Street] =
        [
            "Fine day for it.",
            "Have you seen my dog? Brown, answers to nothing.",
            "Mind the cart!",
            "This town gets bigger every year."
        ];
        _place[(int)PlaceKind.Home] = [];

        _phase[(int)DayPhase.Dawn] = ["Up with the sun, as ever.", "Cold this early.", "Cock's crowed. Day's begun."];
        _phase[(int)DayPhase.Morning] = ["Busy morning.", "Half the day gone already."];
        _phase[(int)DayPhase.Midday] = ["I could eat a horse.", "Too hot to work. Too poor not to."];
        _phase[(int)DayPhase.Afternoon] = ["Long afternoon.", "Nearly done for the day."];
        _phase[(int)DayPhase.Evening] = ["Lamps are being lit.", "Time for a drink, I'd say.", "Long day."];
        _phase[(int)DayPhase.Night] = ["Late to be out.", "Strange folk about at this hour.", "The moons are bright tonight."];

        _greeting[(int)ResidentManner.Friendly] = ["Good {time}!", "Good {time} to you, traveller.", "Well met!"];
        _greeting[(int)ResidentManner.Gruff] = ["Hmph.", "Aye.", "*nods curtly*"];
        _greeting[(int)ResidentManner.Nervous] = ["Oh! You startled me.", "G-good {time}.", "*steps aside*"];
        _greeting[(int)ResidentManner.Chatty] =
        [
            "Good {time}! Lovely weather, isn't it? Well, it was earlier.",
            "Oh, hello! New in town? I know everyone. Everyone!",
            "Good {time}! Have you heard the news?"
        ];
    }

    private static readonly string[][] _newsRemark =
    [
        // BigKill
        ["Did you hear? {who} slew {what}!", "{who} killed {what} in {where}. Imagine that.", "They'll be singing about {who} for weeks."],
        // Died
        ["Poor {who}. {what}, in {where}.", "{who} fell in {where}, I heard. Dangerous out there."],
        // Murder
        ["{who} was murdered. In {where}! What's the world coming to?", "Watch out for {what}. A killer, they say."],
        // Grandmaster
        ["{who}'s a grandmaster of {what} now. Some people.", "A grandmaster of {what}! {who}, of all people."],
        // Newcomer
        ["A new adventurer arrived, I hear. {who}.", "Another fresh face. {who}, they're called."],
        // ResidentKilled
        ["I still can't believe {what} is gone.", "Poor {what}. Murdered, in our own streets.", "If I ever see {who}..."],
        // CompanionLost
        ["{who} lost a hireling in {where}, I heard.", "Sad business about {who}'s companion."]
    ];

    /// <summary>A remark about something that really happened, if anything has.</summary>
    public static string NewsRemark(Resident resident)
    {
        var news = TownNews.PickFor(resident.TownName);
        return news == null ? null : Conversation.FillNews(_newsRemark[(int)news.Kind].RandomElement(), news);
    }

    /// <summary>
    /// How they greet someone they've heard about: the one who killed their neighbour, a dragon
    /// slayer, a new grandmaster, a newcomer, someone back from the dead. Null if nothing's known.
    /// </summary>
    public static string Recognise(Resident resident, PlayerMobile player)
    {
        if (TownNews.About(player, NewsKind.ResidentKilled, resident.TownName) is { } killing)
        {
            return Conversation.FillNews(
                Utility.RandomList("You! You killed {what}!", "Murderer! {what} never hurt anyone!", "*spits* {what}'s blood is on your hands."),
                killing
            );
        }

        if (TownNews.About(player, NewsKind.BigKill) is { } kill && Utility.RandomDouble() < 0.6)
        {
            return Conversation.FillNews(
                Utility.RandomList("You're the one who slew {what}! An honour.", "Is it true? You killed {what}?", "*stares* That's the one who killed {what}!"),
                kill
            );
        }

        if (TownNews.About(player, NewsKind.Grandmaster) is { } master && Utility.RandomDouble() < 0.5)
        {
            return Conversation.FillNews(
                Utility.RandomList("A grandmaster of {what}, walking among us!", "Congratulations on your mastery!"),
                master
            );
        }

        if (TownNews.About(player, NewsKind.Murder) != null)
        {
            return Utility.RandomList("Glad to see you on your feet, after what happened.", "Terrible business. Are you well?");
        }

        if (TownNews.About(player, NewsKind.Died) is { } died && Utility.RandomDouble() < 0.5)
        {
            return Conversation.FillNews(Utility.RandomList("Back from the dead, I see.", "Heard {what} got you. You look well, considering."), died);
        }

        if (TownNews.About(player, NewsKind.Newcomer) != null && Utility.RandomDouble() < 0.5)
        {
            return Utility.RandomList("You're new here, aren't you? Welcome to Britannia.", "A new face! Welcome, welcome.");
        }

        return null;
    }

    /// <summary>A remark about their trade, their place, the time of day, or the news. Null if nothing fits.</summary>
    public static string Idle(Resident resident)
    {
        if (Utility.RandomDouble() < 0.2 && NewsRemark(resident) is { } news)
        {
            return news;
        }

        var roll = Utility.RandomDouble();
        var keeper = resident.GoalPlace?.Keeper?.Name;
        var town = resident.Town;

        string line = null;

        if (roll < 0.15 && town?.LocalTalk.Length > 0)
        {
            line = town.LocalTalk.RandomElement();
        }
        else if (roll < 0.45)
        {
            line = Pick(_place[(int)resident.GoalKind], keeper);
        }
        else if (roll < 0.8)
        {
            line = Pick(_trade[(int)resident.Trade], keeper);
        }

        // Not yet planned a day, so no time of day to remark on.
        line ??= resident.Phase < DayPhase.Count ? Pick(_phase[(int)resident.Phase], keeper) : null;
        return line?.Replace("{town}", town?.Name ?? "town");
    }

    /// <summary>How they greet someone passing close: warily, fearfully, or in their own manner.</summary>
    public static string Greeting(Resident resident, PlayerMobile player)
    {
        if (!player.Alive)
        {
            return Utility.RandomList("A s-spirit!", "*shivers*", "Go to the healer, poor soul.");
        }

        if (Recognise(resident, player) is { } recognised)
        {
            return recognised;
        }

        if (player.Kills >= 5)
        {
            return Utility.RandomList("Keep away from me!", "Murderer...", "*backs away*", "Guards'll have you yet.");
        }

        if (player.Criminal)
        {
            return Utility.RandomList("I've got my eye on you.", "*clutches purse*");
        }

        if (resident.Trade == ResidentTrade.Beggar)
        {
            return Utility.RandomList("Alms, friend? Anything at all?", "A coin for the needy?");
        }

        if (player.Fame >= 10000 && Utility.RandomDouble() < 0.5)
        {
            return Utility.RandomList("Is that... it is! An honour.", "*bows*", "Your servant.");
        }

        var line = _greeting[(int)resident.Manner].RandomElement();
        return line.Replace("{time}", TimeWord(resident.Phase));
    }

    private static readonly string[][] _reaction =
    [
        // Naked
        ["Put some clothes on!", "My eyes!", "*averts gaze*", "Lost a bet, did you?", "Guards! There's a naked person!"],
        // Armed
        ["Put that away, there's folk about.", "Easy with the blade!", "*eyes the weapon nervously*", "No brawling in the street!"],
        // Casting
        ["Mind where you point that!", "No magic in the street, please!", "*ducks*", "If you set my hair alight again..."],
        // ExoticMount
        ["What in Britannia is THAT?", "Does it bite?", "*stares at the beast*", "Keep that thing away from the horses."],
        // Plate
        ["Expecting a war?", "You'll cook in that, come summer.", "Clank clank. We hear you coming.", "Off to fight a dragon?"],
        // Running
        ["Slow down!", "Where's the fire?", "Watch it!", "*jumps aside*"],
        // Entourage
        ["Quite the entourage.", "Hired swords, eh?", "Off to war with your friends?", "Make way, make way for the important one."],
        // Horse
        ["Fine animal.", "Mind the horse!", "Don't let it eat the cabbages."],
        // ScaryPet
        ["Is that thing TAME?!", "Keep it away from me!", "*backs away from the beast*", "Guards! Guards! Oh. It's on a leash. Sort of."],
        // CutePet
        ["Aww, who's a good one?", "What a sweet thing.", "*coos at the animal*", "Can I pet it?"],
        // Monster
        ["Monster! Monster in the streets!", "Run! Guards!", "It's loose! Run for it!", "Help! Help! A beast!"],
        // Killer
        ["Murderer!", "Keep away from us!", "*backs away, glaring*"]
    ];

    // Gruff folk are harder to impress.
    private static readonly string[][] _gruffReaction =
    [
        ["Disgraceful.", "Hmph. Cover yourself."],
        ["Sheathe it.", "Try it and see."],
        ["Showoff.", "Witchery. Bah."],
        ["Ugly brute.", "Whatever it is, it's not a horse."],
        ["Tin can.", "All that iron and still no manners."],
        ["Walk, like a decent person.", "Idiot."],
        ["Hired help. Hmph.", "Can't fight your own battles?"],
        ["Seen better.", "Old nag."],
        ["Get that thing out of town.", "*glares at the creature*"],
        ["Hmph. Vermin.", "*ignores the animal*"],
        ["Monster! Guards!", "Run, you fools!"],
        ["Get out of our town.", "*glares*"]
    ];

    // Nervous folk startle at everything.
    private static readonly string[][] _nervousReaction =
    [
        ["Oh! Oh my.", "*squeaks and looks away*"],
        ["P-please don't.", "*trembles*"],
        ["Don't hurt me!", "*cowers*"],
        ["It's looking at me!", "*hides behind a post*"],
        ["S-soldiers? Is there a war?", "*shrinks back*"],
        ["Oh! You frightened me!", "*clutches chest*"],
        ["So many of you...", "*steps well aside*"],
        ["*edges round the horse*", "It's very big."],
        ["Help! Help!", "*runs*"],
        ["It won't bite, will it?", "*keeps a careful distance*"],
        ["AAAH!", "Help! Mother! Guards!"],
        ["D-don't come near me!", "*flees from the killer*"]
    ];

    /// <summary>A remark about something noticed, in this resident's manner.</summary>
    public static string Reaction(Resident resident, ReactionKind kind)
    {
        var pool = resident.Manner switch
        {
            ResidentManner.Gruff when Utility.RandomBool()   => _gruffReaction[(int)kind],
            ResidentManner.Nervous when Utility.RandomBool() => _nervousReaction[(int)kind],
            _                                                => _reaction[(int)kind]
        };

        return pool.RandomElement();
    }

    public static string Pardon(Resident resident) =>
        resident.Manner switch
        {
            ResidentManner.Gruff   => Utility.RandomList("Out of the way.", "Move, would you?"),
            ResidentManner.Nervous => Utility.RandomList("S-sorry, may I...?", "Excuse me..."),
            _                      => Utility.RandomList("Pardon me.", "Excuse me, friend.", "Coming through!")
        };

    public static string CryForHelp(Resident resident) =>
        Utility.RandomList("Help! Guards!", "Guards! Guards!", "Murder! Help!", "Somebody help me!");

    private static string TimeWord(DayPhase phase) =>
        phase switch
        {
            DayPhase.Dawn or DayPhase.Morning => "morrow",
            DayPhase.Midday or DayPhase.Afternoon => "day",
            DayPhase.Evening => "evening",
            _ => "night"
        };

    private static string Pick(string[] lines, string keeper)
    {
        if (lines == null || lines.Length == 0)
        {
            return null;
        }

        var line = lines.RandomElement();

        if (!line.Contains("{keeper}"))
        {
            return line;
        }

        return keeper == null ? null : line.Replace("{keeper}", keeper);
    }
}
