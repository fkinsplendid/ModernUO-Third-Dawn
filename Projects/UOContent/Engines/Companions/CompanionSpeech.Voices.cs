namespace Server.Engines.Companions;

/// <summary>
/// Lines in a companion's own voice: its manner (stoic, cheerful, gruff, witty) and its trade
/// (warrior, bard, mage). Also everything said about mounts.
/// </summary>
public static partial class CompanionSpeech
{
    private static void PersonalityLines()
    {
        StoicLines();
        CheerfulLines();
        GruffLines();
        WittyLines();
    }

    private static void StoicLines()
    {
        const CompanionPersonality p = CompanionPersonality.Stoic;

        Flavour(p, SpeechContext.Greeting, "Ready.", "{owner}.", "I am rested. We may go.");
        Flavour(p, SpeechContext.IdleTown, "Crowds. Watch your purse.", "We should not linger.", "Resupply, then move on.");
        Flavour(p, SpeechContext.IdleWild, "...", "Keep moving.", "Stay alert.", "Tracks. Two days old.", "Wind's changing.");
        Flavour(p, SpeechContext.IdleDungeon, "Quiet. Listen.", "Single file here.", "Something passed this way.");
        Flavour(p, SpeechContext.IdleNight, "Night watch. I'll take the first.", "Eyes open.");
        Flavour(p, SpeechContext.Waiting, "I can wait.", "...", "When you are ready, {owner}.");
        Flavour(p, SpeechContext.CombatStart, "Engaging.", "On me.", "Here.");
        Flavour(p, SpeechContext.Kill, "Done.", "It is finished.", "Next.");
        Flavour(p, SpeechContext.BigKill, "A worthy foe. It is done.", "That {arg} will trouble no one again.");
        Flavour(p, SpeechContext.Victory, "Clear.", "Area secure.", "Count heads. Good.");
        Flavour(p, SpeechContext.LowHealth, "I am wounded.", "Need aid.");
        Flavour(p, SpeechContext.Poisoned, "Poisoned. It will pass.", "Poison.");
        Flavour(p, SpeechContext.Revived, "I return.", "Thank you. I will not forget it.");
        Flavour(p, SpeechContext.EmployerRevived, "Good. You live.", "Welcome back, {owner}.");
        Flavour(p, SpeechContext.LevelUp, "I improve. Level {arg}.", "Practice. It tells.");
        Flavour(p, SpeechContext.LootGold, "{arg} gold.", "Coin.");
        Flavour(p, SpeechContext.Paid, "Received. Thank you.", "{arg} gold. My thanks.");
        Flavour(p, SpeechContext.BanterReply, "...", "Focus.", "Enough talk.");
        Flavour(p, SpeechContext.GhostPlea, "{owner}. A healer. Please.", "...I wait, {owner}.");
    }

    private static void CheerfulLines()
    {
        const CompanionPersonality p = CompanionPersonality.Cheerful;

        Flavour(p, SpeechContext.Greeting, "{owner}! There you are! Adventure awaits!", "Oh, good, it's you! Where are we off to?");
        Flavour(p, SpeechContext.IdleTown,
            "Ooh, I want to look at every stall!",
            "Everyone's so friendly here!",
            "Do you think they sell honey cakes?",
            "Look, a juggler! Oh, he dropped one."
        );
        Flavour(p, SpeechContext.IdleWild,
            "What a lovely day for an adventure!",
            "I bet there's treasure over the next hill!",
            "Do you think we'll see a dragon? From very far away, I mean.",
            "Look at the flowers! Can we pick some? No? Alright.",
            "I'm going to write all of this down when we get home!"
        );
        Flavour(p, SpeechContext.IdleDungeon,
            "Spooky! I love it. Well, I mostly love it.",
            "I'm not scared. I'm excited. It looks the same."
        );
        Flavour(p, SpeechContext.IdleNight, "Look at all the stars! You can see the Hunter tonight.", "Night walks are so romantic. And terrifying.");
        Flavour(p, SpeechContext.Dawn, "Good morning, sun! Good morning, {owner}!", "A brand new day! Anything could happen!");
        Flavour(p, SpeechContext.Waiting, "Ooh, are we having a rest? Lovely.", "I'll pick some daisies while we wait!");
        Flavour(p, SpeechContext.CombatStart, "Ooh, here we go!", "Let's show them!", "For glory! And loot!");
        Flavour(p, SpeechContext.Kill, "Got it! Did you see?", "Hah! Take that!", "Whoops, sorry! Not really.");
        Flavour(p, SpeechContext.BigKill, "We did it! We actually killed that {arg}! Hug, anyone?", "I'm shaking! In a good way!");
        Flavour(p, SpeechContext.Victory, "We did it! Ha!", "Hooray! Everyone's alright?", "Wasn't that exciting?");
        Flavour(p, SpeechContext.LowHealth, "Ow, ow, ow! That really hurts!", "I'm not feeling very cheerful right now!");
        Flavour(p, SpeechContext.Revived, "I'm back! Did I miss anything?", "Ooh, that was strange. Thank you!");
        Flavour(p, SpeechContext.EmployerRevived, "{owner}! Oh, thank goodness! I was so worried!");
        Flavour(p, SpeechContext.LevelUp, "Level {arg}! Wait till I tell my mother!", "I'm getting better! Did you notice?");
        Flavour(p, SpeechContext.LootGold, "Gold! Shiny, shiny gold! {arg} of them!", "We're rich! Well, a bit richer.");
        Flavour(p, SpeechContext.LootFind, "Ooh, pretty!", "Treasure! Real treasure!");
        Flavour(p, SpeechContext.Paid, "{arg} gold! You're the best, {owner}!", "Ooh, thank you!");
        Flavour(p, SpeechContext.ReceivedItem, "A present! For me?", "Oh, thank you, thank you!");
        Flavour(p, SpeechContext.BanterReply, "Ha! That's a good one!", "Oh, cheer up!", "You're funny. Sometimes.");
        Flavour(p, SpeechContext.GhostPlea, "{owner}? It's very grey in here. Could we fix that?");
    }

    private static void GruffLines()
    {
        const CompanionPersonality p = CompanionPersonality.Gruff;

        Flavour(p, SpeechContext.Greeting, "Hmph. You took your time.", "About time. Let's get on with it.");
        Flavour(p, SpeechContext.IdleTown,
            "Too many people.",
            "Where's the nearest tavern?",
            "Bah. Merchants. Thieves with signs.",
            "If one more urchin tugs my sleeve..."
        );
        Flavour(p, SpeechContext.IdleWild,
            "My feet hurt.",
            "If one more fly bites me...",
            "Bah. Walking, always walking.",
            "Rain's coming. My knee says so.",
            "Trees. More trees. Wonderful."
        );
        Flavour(p, SpeechContext.IdleDungeon, "Stinks down here.", "Rats. Why is it always rats?", "Bah. Holes in the ground.");
        Flavour(p, SpeechContext.IdleNight, "Cold. Dark. Wet. Marvellous.", "Should've stayed at the inn.");
        Flavour(p, SpeechContext.Dawn, "Too early.", "Sun's up. Don't expect me to be cheerful.");
        Flavour(p, SpeechContext.Waiting, "Are we going or not?", "Standing about. Paid to stand about.", "Hurry up, {owner}.");
        Flavour(p, SpeechContext.CombatStart, "Finally, something to hit.", "Come on, then!", "Bah. More of them.");
        Flavour(p, SpeechContext.Kill, "Hmph. Barely worth the effort.", "Good riddance.", "Stay down, curse you.");
        Flavour(p, SpeechContext.BigKill, "Hmph. Big, wasn't it. Still died.", "That {arg}'ll make a decent rug.");
        Flavour(p, SpeechContext.Victory, "Bah. Is that all?", "Done. My back aches.");
        Flavour(p, SpeechContext.LowHealth, "Blast it, I'm cut up!", "Somebody patch me up, and quick!");
        Flavour(p, SpeechContext.Poisoned, "Poison. Wonderful. Just wonderful.", "Ugh. Who poisons a blade? Cowards.");
        Flavour(p, SpeechContext.Revived, "Hmph. Don't let it happen again.", "Death. Overrated.");
        Flavour(p, SpeechContext.EmployerRevived, "Hmph. Back, are you? Try not to die again, it's tiresome.");
        Flavour(p, SpeechContext.LevelUp, "Level {arg}. About time.", "Hmph. Getting better. Don't make a fuss.");
        Flavour(p, SpeechContext.LootGold, "{arg} gold. Hmph. Something, at least.", "Coin. Good.");
        Flavour(p, SpeechContext.Paid, "Hmph. Keep it coming.", "{arg}. About time.");
        Flavour(p, SpeechContext.Laden, "I'm not a pack mule, {owner}!", "Carry your own junk!");
        Flavour(p, SpeechContext.BanterReply, "Nobody asked you.", "Bah.", "Keep talking. See what happens.");
        Flavour(p, SpeechContext.LowSupplies, "We're short of {arg}. Again.", "Who used all the {arg}? Oh. Me.");
        Flavour(p, SpeechContext.GhostPlea, "Hmph. Dead. Get me a healer, {owner}, and be quick about it.");
    }

    private static void WittyLines()
    {
        const CompanionPersonality p = CompanionPersonality.Witty;

        Flavour(p, SpeechContext.Greeting, "Ah, my favourite employer. Don't tell the others.", "Shall we go and annoy something large?");
        Flavour(p, SpeechContext.IdleTown,
            "I love a town. So many people to disappoint.",
            "That merchant's scales are crooked. So's his nose.",
            "The guards here look like they've never seen a fight. Lucky them."
        );
        Flavour(p, SpeechContext.IdleWild,
            "If I had a gold piece for every tree we've passed...",
            "Adventure, they said. Glory, they said.",
            "I'm composing an epic about this walk. It's very short.",
            "Do you suppose the trees get bored of us too?",
            "A fine day for walking. Or for not walking. I'd take either."
        );
        Flavour(p, SpeechContext.IdleDungeon,
            "Lovely decor. Very 'tomb chic'.",
            "Who builds a castle and puts the treasure in the basement?",
            "I've stayed at inns with worse smells. Not many.",
            "Rule one of dungeons: the quiet bits are the worst bits."
        );
        Flavour(p, SpeechContext.IdleNight, "Night: when everything with teeth gets up for breakfast.", "Dark enough to hide my good looks. Tragic.");
        Flavour(p, SpeechContext.Dusk, "Ah, dusk. When the monsters put their boots on.");
        Flavour(p, SpeechContext.Waiting, "Are we waiting for the grass to grow? I'm told it's thrilling.", "I'll alert the bards: 'and then they stood there'.");
        Flavour(p, SpeechContext.CombatStart, "Oh good, visitors.", "Who invited them?", "Right. Pointy end goes in them.");
        Flavour(p, SpeechContext.Kill, "And the crowd goes wild.", "Next contestant!", "Exit, pursued by me.");
        Flavour(p, SpeechContext.BigKill, "That {arg} picked the wrong party.", "I'd like to thank my sword, my armour, and my employer.");
        Flavour(p, SpeechContext.Victory, "And that's why they pay us.", "Flawless. Mostly. Somewhat.");
        Flavour(p, SpeechContext.LowHealth, "I seem to be leaking!", "This is not my best day!");
        Flavour(p, SpeechContext.Poisoned, "Poison. How rude.", "Well, that's not the wine talking.");
        Flavour(p, SpeechContext.Revived, "Death was dull. I'm back for the jokes.", "Did anyone loot me? Be honest.");
        Flavour(p, SpeechContext.EmployerRevived, "Back from the dead! Show-off.", "{owner}! I've been keeping your corpse warm. Figuratively.");
        Flavour(p, SpeechContext.LevelUp, "Level {arg}. Hold your applause. No, really, hold it.", "I'm improving. Terrifying, isn't it?");
        Flavour(p, SpeechContext.LootGold, "{arg} gold. It was going to waste on a dead thing.", "Gold! My natural habitat.");
        Flavour(p, SpeechContext.Paid, "A pleasure doing business, {owner}.", "{arg} gold. You remembered!");
        Flavour(p, SpeechContext.Laden, "I've become a walking shop.", "If I fall over, just roll me home.");
        Flavour(p, SpeechContext.BanterReply, "Says the one who tripped on a rock earlier.", "Truly, the wisdom of the ages.", "Write that down. Then burn it.");
        Flavour(p, SpeechContext.GhostPlea, "{owner}, I appear to be see-through. Bit of a problem.");
    }

    private static void RoleLines()
    {
        Trade(CompanionRole.Warrior, SpeechContext.IdleTown,
            "I'll have the smith look at this dent before we leave.",
            "Good place to buy arrows, if we need them."
        );
        Trade(CompanionRole.Warrior, SpeechContext.IdleWild,
            "Good ground for a fight, this. Open, firm footing.",
            "My shield arm's itching. That's never good.",
            "I trained for years for this. Mostly for the walking, it turns out."
        );
        Trade(CompanionRole.Warrior, SpeechContext.IdleDungeon,
            "Tight corridors. Good. They can only come at us one at a time.",
            "I'll go first. That's what the armour's for."
        );
        Trade(CompanionRole.Warrior, SpeechContext.CombatStart,
            "Behind me, {owner}!",
            "Shields up!",
            "Let them come. I'll hold them here."
        );
        Trade(CompanionRole.Warrior, SpeechContext.Kill, "Steel wins.", "One stroke. Clean.");
        Trade(CompanionRole.Warrior, SpeechContext.Victory, "Wipe your blades. Rust is the real enemy.", "Good work. My arm's numb.");
        Trade(CompanionRole.Warrior, SpeechContext.Equip, "Good balance on this.", "Solid. I'll wear it in.");
        Trade(CompanionRole.Warrior, SpeechContext.Healing, "Hold still, I've bound worse.", "Bandage first, complaints later.");

        Trade(CompanionRole.Bard, SpeechContext.IdleTown,
            "I could play here tonight. A full tavern pays well.",
            "*hums* ...and the miller's daughter danced till dawn...",
            "The acoustics in this square are dreadful."
        );
        Trade(CompanionRole.Bard, SpeechContext.IdleWild,
            "*hums* ...over the hills where the dragons sleep...",
            "I'm working on a ballad about you, {owner}. It needs a better ending.",
            "Listen to that wind in the trees. Like a harp nobody's tuned.",
            "*whistles a jaunty tune*"
        );
        Trade(CompanionRole.Bard, SpeechContext.IdleDungeon,
            "Every song needs a dark chapter. This is ours.",
            "*whispers* Not the place for music, is it?"
        );
        Trade(CompanionRole.Bard, SpeechContext.IdleNight, "A night like this wants a slow song.", "*softly hums a lullaby*");
        Trade(CompanionRole.Bard, SpeechContext.CombatStart, "A battle hymn, then!", "Let me find the right tune for this...", "Music to their ears. Or not.");
        Trade(CompanionRole.Bard, SpeechContext.Kill, "That's a verse for the chorus.", "And the music swells!");
        Trade(CompanionRole.Bard, SpeechContext.BigKill, "The {arg}'s fall! This is my masterpiece!", "I'll need a longer song for this.");
        Trade(CompanionRole.Bard, SpeechContext.Victory, "This'll make a fine verse.", "And so the heroes stood victorious... too much? Too much.");
        Trade(CompanionRole.Bard, SpeechContext.Healing, "Hold still. I've stitched up drunk minstrels with less.", "There. Good as a new lute string.");
        Trade(CompanionRole.Bard, SpeechContext.LevelUp, "Level {arg}! My fingers are finally keeping up.", "My playing's never been better.");

        Trade(CompanionRole.Mage, SpeechContext.IdleTown,
            "I must visit the reagent shop. Nightshade's gone up again, no doubt.",
            "Mages' guild is near. I can smell the sulfurous ash."
        );
        Trade(CompanionRole.Mage, SpeechContext.IdleWild,
            "Mandrake grows wild out here, if you know where to look.",
            "The ley lines are strong here. Can you feel it?",
            "When I studied at the Lycaeum, we never walked anywhere.",
            "*mutters incantations under breath*"
        );
        Trade(CompanionRole.Mage, SpeechContext.IdleDungeon,
            "There's old magic in these walls.",
            "I'd prefer not to cast a fireball in here. The ceiling looks tired."
        );
        Trade(CompanionRole.Mage, SpeechContext.IdleNight, "The moons are bright. A good night for spellwork.", "I could light our way, but it'd draw every eye for a mile.");
        Trade(CompanionRole.Mage, SpeechContext.CombatStart, "Stand back. I'll need room to work.", "Kal... no, wait. Let me think.", "Cover me while I cast!");
        Trade(CompanionRole.Mage, SpeechContext.Kill, "Magic wins out.", "Burned to a crisp.", "The words never fail. Well. Rarely.");
        Trade(CompanionRole.Mage, SpeechContext.Victory, "I need a moment. And some mana.", "Not bad, for a scholar.");
        Trade(CompanionRole.Mage, SpeechContext.Healing, "Hold still, let the magic work.", "In Mani. There.");
        Trade(CompanionRole.Mage, SpeechContext.LowHealth, "I'm a scholar, not a shield!", "Keep them off me!");
        Trade(CompanionRole.Mage, SpeechContext.LevelUp, "Level {arg}. The weave bends a little easier now.", "I can feel my power growing.");
    }

    private static void SteedLines()
    {
        Common(SpeechContext.SteedGiven,
            "A fine animal. I'll take good care of it.",
            "Now that's more like it. Thank you, {owner}.",
            "My feet thank you, {owner}.",
            "Easy there... good. We'll get along, you and I."
        );
        Common(SpeechContext.SteedLost,
            "No! They've killed my mount!",
            "Poor beast. It deserved better than that.",
            "My mount... gone.",
            "Curse them. That was a good animal."
        );
        Common(SpeechContext.SteedWanted,
            "I'd keep pace better with a mount under me, {owner}.",
            "My feet ache. A horse wouldn't go amiss.",
            "If you come across a spare mount, I'd be glad of it."
        );
        Common(SpeechContext.SteedSick,
            "My mount's poisoned. I'll not ride it like this.",
            "The poor thing's sick. I'll walk until it's better."
        );
        Common(SpeechContext.SteedReturned,
            "Here, {owner}. Look after it.",
            "Back to you. It's been a good mount."
        );
        Common(SpeechContext.Dismount,
            "I'll leave it here.",
            "Whoa, easy. Stay.",
            "On foot it is."
        );
        Common(SpeechContext.MountUp,
            "Back in the saddle.",
            "Right you are.",
            "Up we go."
        );

        Flavour(CompanionPersonality.Stoic, SpeechContext.SteedGiven, "It will serve.", "Good. We'll move faster.");
        Flavour(CompanionPersonality.Cheerful, SpeechContext.SteedGiven, "Oh, how lovely! Does it have a name?");
        Flavour(CompanionPersonality.Gruff, SpeechContext.SteedLost, "Bah. I'll walk, then.");
        Flavour(CompanionPersonality.Witty, SpeechContext.SteedGiven, "Finally, someone else can do the walking.");
        Flavour(CompanionPersonality.Witty, SpeechContext.SteedWanted, "I'm wearing out boots faster than you're paying for them.");
    }
}
