namespace Server.Engines.Companions;

/// <summary>
/// Lines that depend on how a companion feels: greetings warm and cold, idle grumbling and
/// contentment, fear in a fight, praise, insults, apologies, and leaving.
/// </summary>
public static partial class CompanionSpeech
{
    private static void MindContextLines()
    {
        Common(SpeechContext.GreetingWarm,
            "{owner}! I was hoping you'd be along.",
            "There you are. The day's better already.",
            "Ready when you are, {owner}. Always.",
            "Good to see you, friend."
        );
        Common(SpeechContext.GreetingCold,
            "Oh. You.",
            "Back, are you.",
            "{owner}.",
            "Let's get this over with."
        );
        Common(SpeechContext.RehiredWary,
            "Back again, then. Don't make me regret it.",
            "Fine. A fresh start. Don't waste it.",
            "I came back for the coin, {owner}. Earn the rest."
        );
        Common(SpeechContext.IdleWeary,
            "*yawns*",
            "My legs are lead.",
            "How long have we been out here?",
            "I'd sell my sword for a featherbed right now.",
            "*rubs eyes* Still awake. Mostly.",
            "Is it me, or does the road get longer every hour?"
        );
        Common(SpeechContext.IdleUnhappy,
            "*mutters under their breath*",
            "Another day, another miserable wage.",
            "The guild never said it'd be like this.",
            "Some employers say thank you, you know.",
            "*sighs heavily*",
            "I could be guarding a nice quiet warehouse right now."
        );
        Common(SpeechContext.IdleDevoted,
            "I'm glad I signed on with you, {owner}. Just so you know.",
            "Wherever we're going, I'm with you.",
            "Best company I've ever kept.",
            "You know, I tell everyone at the guild about you.",
            "Whatever comes, we'll face it together."
        );
        Common(SpeechContext.IdleDark,
            "*breathing fast* It's so close in here.",
            "The walls. Are the walls getting nearer?",
            "How much rock is over our heads? Don't answer that.",
            "Keep the light up. Please keep the light up.",
            "I hate this. I hate this. I'm fine."
        );

        Common(SpeechContext.FearSighted,
            "Oh no. That {arg}. Give me a moment...",
            "*goes pale* Not that {arg}. Anything but that.",
            "I... I can't. Not that {arg}.",
            "*backs away* You take the {arg}. I'll... I'll be here.",
            "That {arg}! My hands are shaking!"
        );
        Common(SpeechContext.FearSteeled,
            "Alright. Alright! I can do this.",
            "Deep breath. Here I come.",
            "Face it. Just face it.",
            "*grits teeth* Fine!"
        );
        Common(SpeechContext.FearBraved,
            "Get away from {owner}! I don't care what you are!",
            "Not {owner}! Never!",
            "Afraid or not, you'll not touch {owner}!"
        );
        Common(SpeechContext.FearConquered,
            "You know what? I'm not afraid of {arg} any more. Not one bit.",
            "{arg}. I used to shake at the thought. Not now. Thank you for standing by me, {owner}.",
            "I faced {arg} so often the fear just... wore out. I feel ten feet tall.",
            "No more fear of {arg}. I've beaten it."
        );
        Common(SpeechContext.FearOvercome,
            "I used to be terrified of {arg}. Not any more, thanks to you.",
            "Nothing now. I was afraid of {arg}, once. I've faced too many to care.",
            "{arg}, once. I got over it. You were there."
        );
        Common(SpeechContext.Fearless,
            "Fear? Losing my nerve, maybe. Nothing else.",
            "Nothing that walks, crawls or flies.",
            "Being useless when I'm needed. That's all."
        );

        Common(SpeechContext.Praised,
            "Thank you, {owner}. That means a lot.",
            "Why, thank you!",
            "Just doing my job. But thank you.",
            "Kind of you to say."
        );
        Common(SpeechContext.PraisedAgain,
            "You said. Thank you.",
            "Careful, I'll get a big head.",
            "Twice in one day? Steady on."
        );
        Common(SpeechContext.Insulted,
            "There's no call for that.",
            "I'll remember you said that.",
            "Charming.",
            "Say that again and see what happens."
        );
        Common(SpeechContext.ApologyAccepted,
            "Alright. Apology accepted.",
            "Thank you. Let's put it behind us.",
            "It's forgotten. Well, nearly."
        );
        Common(SpeechContext.ApologyUnneeded,
            "Sorry? For what?",
            "Nothing to forgive, {owner}.",
            "Hm? We're fine."
        );
        Common(SpeechContext.StoryRefused,
            "That's my business.",
            "Why would I tell you?",
            "Ask me when you've earned it."
        );
        Common(SpeechContext.IssueIgnored,
            "...Nothing to say? Fine.",
            "I'll take that as a no, then.",
            "Don't all answer at once."
        );

        Common(SpeechContext.OnCredit,
            "My purse is empty, {owner}. I'll work on trust, just this once. You owe me {arg} gold.",
            "No pay today, but I'll stay on credit. {arg} gold, and I'll want it soon.",
            "I trust you, so I'll carry it. {arg} gold owed. Don't make me regret it."
        );
        Common(SpeechContext.DebtSettled,
            "Debt settled. Thank you, {owner}. We're square.",
            "Paid back in full. I knew you were good for it.",
            "That's what I'm owed. Good."
        );
        Common(SpeechContext.NoticeWithdrawn,
            "Alright. Things are better. I'll stay.",
            "You've turned it around. I'm not going anywhere.",
            "Forget what I said about leaving."
        );
        Common(SpeechContext.Quits,
            "That's it. I've had enough. Find someone else, {owner}.",
            "I quit. The guild can find you another fool.",
            "I'm done. Don't come looking for me.",
            "Goodbye, {owner}. I'd say it's been a pleasure."
        );

        MoodFlavour();
    }

    private static void MoodFlavour()
    {
        Flavour(CompanionPersonality.Stoic, SpeechContext.Praised, "...Thank you.", "Noted. Thank you.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.Insulted, "...", "Careful.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.FearSighted, "*goes very still*", "That {arg}. ...Give me a moment.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.Quits, "I am leaving. Goodbye.", "Our contract is ended.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.IdleUnhappy, "...", "*says nothing, pointedly*");
        Flavour(CompanionPersonality.Stoic, SpeechContext.IdleDevoted, "I am glad to serve you.", "We do good work, {owner}.");

        Flavour(CompanionPersonality.Cheerful, SpeechContext.Praised, "Really? Oh, thank you!", "*beams* Did you hear that? {owner} said well done!");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.Insulted, "Oh. That... that was unkind.", "*bottom lip wobbles*");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.FearSighted, "Eek! Look at that {arg}! Nope, nope, nope!", "Oh gods, that {arg}! Someone else first!");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.Quits, "I tried so hard to like this job. I really did. Goodbye.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.IdleUnhappy, "I'm trying to stay cheerful. It's not working.", "Even I can't smile about this.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.IdleDevoted, "Best. Job. Ever.", "I love our little band! Do you love our little band?");

        Flavour(CompanionPersonality.Gruff, SpeechContext.Praised, "Hmph. About time someone noticed.", "Aye. Well. Thanks.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.Insulted, "Say that again. I dare you.", "Watch your mouth.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.FearSighted, "Gah! That {arg} is somebody else's problem!", "Not that {arg}. Damn it all.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.Quits, "I'm off. Good riddance.", "Find yourself another mug.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.IdleUnhappy, "Bah.", "Worst contract I ever signed.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.IdleDevoted, "You're alright, {owner}. Don't let it go to your head.", "Wouldn't follow anyone else. Don't repeat that.");

        Flavour(CompanionPersonality.Witty, SpeechContext.Praised, "I know. But do go on.", "Flattery! My one weakness. Well, one of several.");
        Flavour(CompanionPersonality.Witty, SpeechContext.Insulted, "And you dress like a scarecrow, but I'm too polite to say.", "Ouch. I'll weep later.");
        Flavour(CompanionPersonality.Witty, SpeechContext.FearSighted, "Ah. That {arg}. I'll just be over here, being strategic.", "That {arg}? I've just remembered an urgent appointment.");
        Flavour(CompanionPersonality.Witty, SpeechContext.Quits, "I'd say it's been fun, but I promised myself I'd stop lying.", "I resign. Effective immediately. Effective yesterday, ideally.");
        Flavour(CompanionPersonality.Witty, SpeechContext.IdleUnhappy, "I've had more fun at a tax collector's funeral. His, I mean.", "Remind me why I signed on? No, really.");
        Flavour(CompanionPersonality.Witty, SpeechContext.IdleDevoted, "I'd follow you anywhere, {owner}. Even somewhere sensible.", "Best employer in Britannia. Faint praise, but sincere.");
    }
}
