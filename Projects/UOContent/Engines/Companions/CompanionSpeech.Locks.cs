namespace Server.Engines.Companions;

/// <summary>Lines a mage says over a locked chest in a dungeon.</summary>
public static partial class CompanionSpeech
{
    private static void LockLines()
    {
        Common(SpeechContext.UnlockStart,
            "A locked chest. Let me see to that, {owner}.",
            "Hold a moment. That lock won't stand up to a spell.",
            "Locked, is it? Stand back."
        );
        Common(SpeechContext.Unlocked,
            "There. It's open, {owner}. Mind it isn't trapped.",
            "Unlocked. Careful how you open it, {owner}.",
            "The lock's done. I'd check for traps all the same."
        );
        Common(SpeechContext.LockTooHard,
            "That lock is beyond my magic, {owner}.",
            "My spell won't move that lock. It'll want a lockpick.",
            "No good. Whoever made that lock knew their trade."
        );

        Flavour(CompanionPersonality.Stoic, SpeechContext.Unlocked, "Open.", "Done. Watch for traps.");
        Flavour(CompanionPersonality.Stoic, SpeechContext.LockTooHard, "Beyond me.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.UnlockStart, "Ooh, a treasure chest! Let me!");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.Unlocked, "Ta-da! What's inside, {owner}?");
        Flavour(CompanionPersonality.Gruff, SpeechContext.LockTooHard, "Bah. Stubborn lock.", "Hmph. Find a thief for that one.");
        Flavour(CompanionPersonality.Witty, SpeechContext.Unlocked, "Locks are just doors that haven't met a mage.", "Open. Whatever's in there, I want my cut.");
        Flavour(CompanionPersonality.Witty, SpeechContext.LockTooHard, "That lock has more pride than I do.");
    }
}
