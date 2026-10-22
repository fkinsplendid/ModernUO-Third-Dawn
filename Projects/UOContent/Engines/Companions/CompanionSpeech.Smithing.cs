namespace Server.Engines.Companions;

/// <summary>
/// Lines about worn gear and trips to the smith. {arg} is what the piece is called ("blade",
/// "gauntlets"), singular or plural, so lines never lean on "is" or "it" for it.
/// </summary>
public static partial class CompanionSpeech
{
    private static void SmithingLines()
    {
        Common(SpeechContext.GearWorn,
            "My {arg} won't take much more of this.",
            "{owner}, my {arg} could do with a smith's hammer.",
            "We'll want a smith for my {arg} before long.",
            "Feel that dent? My {arg} needs mending."
        );
        Common(SpeechContext.GearFailing,
            "{owner}, my {arg} won't last another fight!",
            "I need a smith for my {arg}, and soon!",
            "One more good knock and my {arg} will be in pieces!"
        );
        Common(SpeechContext.GearSound,
            "My gear's sound enough, {owner}.",
            "Nothing needs mending.",
            "All in good order."
        );
        Common(SpeechContext.NoSmith,
            "There's no smith about who could do it.",
            "We'd need a master smith for that, {owner}."
        );
        Common(SpeechContext.ToTheSmith,
            "Give me a moment, {owner}. I'll have the smith see to my {arg}.",
            "A smith! My {arg} could use one.",
            "Hold here a moment, {owner}. My {arg} needs the smith."
        );
        Common(SpeechContext.HandToSmith,
            "Could you look at my {arg}?",
            "Here, master smith. My {arg} needs mending.",
            "See what you can do with my {arg}."
        );
        Common(SpeechContext.Repaired,
            "Much better.",
            "Good as new. Thanks, {owner}.",
            "My {arg} feels right again.",
            "That'll hold. Thank you, {owner}."
        );
        Common(SpeechContext.RepairDeclined,
            "Fair enough. My {arg} will hold a while yet.",
            "As you say, {owner}.",
            "Another time, then."
        );

        Flavour(CompanionPersonality.Stoic, SpeechContext.Repaired, "Good.", "It will serve.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.RepairDeclined, "Understood.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.Repaired, "Oh, lovely work! Shiny again!", "Thank you, {owner}! I feel brand new.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.ToTheSmith, "Ooh, a smith! Back in a tick, {owner}!");
        Flavour(CompanionPersonality.Gruff, SpeechContext.GearWorn, "My {arg}? A wreck. Bah.", "Patched together with spit, my {arg}.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.RepairDeclined, "Hmph. Cheap.", "Bah. Don't blame me when it gives.");
        Flavour(CompanionPersonality.Gruff, SpeechContext.Repaired, "Hmph. About time.", "Decent work. For a townsman.");
        Flavour(CompanionPersonality.Witty, SpeechContext.GearWorn, "My {arg}: held together by optimism.", "I'm not saying my {arg} needs work, but I creak when I think.");
        Flavour(CompanionPersonality.Witty, SpeechContext.RepairDeclined, "Thrift! The adventurer's first virtue.", "Right. I'll just dodge more, then.");
        Flavour(CompanionPersonality.Witty, SpeechContext.Repaired, "I look almost respectable.", "Money well spent. Your money, admittedly.");

        Trade(CompanionRole.Warrior, SpeechContext.ToTheSmith,
            "Steel looked after is steel that saves your life. Back in a moment.",
            "A smith! Let's get this steel sorted before the next fight."
        );
        Trade(CompanionRole.Warrior, SpeechContext.GearWorn, "Took a beating, this {arg}. So did I.");
    }
}
