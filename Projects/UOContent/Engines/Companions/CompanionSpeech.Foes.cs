using System;

namespace Server.Engines.Companions;

/// <summary>Kinds of foe a companion recognises and has something particular to say about.</summary>
public enum FoeKind
{
    None,
    Undead,
    Dragon,
    Daemon,
    Orc,
    Crawler,
    Giant,
    Troll,
    Reptile,
    Elemental,
    Ooze,
    Gargoyle,
    Harpy,
    Vermin,
    Beast,
    Human,
    Count
}

/// <summary>
/// Battle cries and kill remarks for particular kinds of foe. {arg} is the foe's name without
/// its article ("lich lord").
/// </summary>
public static partial class CompanionSpeech
{
    // Matched against the start of each word of a foe's name, in order: the first kind with a
    // matching word wins, so "giant spider" is a crawler and not a giant.
    private static readonly (FoeKind Kind, string[] Words)[] _foeWords =
    [
        (FoeKind.Undead, ["skelet", "zombie", "ghoul", "lich", "mummy", "spectre", "spectral", "wraith", "shade", "revenant", "bone", "rotting", "ghost", "banshee", "restless"]),
        (FoeKind.Dragon, ["dragon", "drake", "wyrm", "wyvern", "hydra"]),
        (FoeKind.Daemon, ["daemon", "demon", "balron", "imp", "succubus", "fiend", "hell", "moloch"]),
        (FoeKind.Orc, ["orc"]),
        (FoeKind.Crawler, ["spider", "terathan", "scorpion", "solen", "beetle", "ant"]),
        (FoeKind.Troll, ["troll"]),
        (FoeKind.Giant, ["ettin", "ogre", "cyclop", "titan", "minotaur"]),
        (FoeKind.Reptile, ["lizard", "serpent", "snake", "ophidian", "alligator", "serpentine"]),
        (FoeKind.Elemental, ["elemental", "efreet", "golem"]),
        (FoeKind.Ooze, ["slime", "ooze", "jelly", "plague", "maggots"]),
        (FoeKind.Gargoyle, ["gargoyle"]),
        (FoeKind.Harpy, ["harpy", "harpies"]),
        (FoeKind.Vermin, ["rat", "ratman", "mongbat", "bat", "gibberling"]),
        (FoeKind.Beast, ["wolf", "bear", "grizzly", "cougar", "panther", "boar", "hound", "lion", "hart"])
    ];

    private static readonly string[][] _foeBattleCries = new string[(int)FoeKind.Count][];
    private static readonly string[][] _foeKills = new string[(int)FoeKind.Count][];

    private static void FoeLines()
    {
        Foe(FoeKind.Undead,
            [
                "Undead! Break their bones!",
                "The dead walk! Steady, {owner}!",
                "Ugh, the smell! Undead!",
                "Back to your grave, {arg}!",
                "Don't let the {arg} touch you. Cold goes right through you."
            ],
            "Rest properly this time.",
            "Back to the dust.",
            "That's twice it's died. Let's hope it sticks."
        );
        Foe(FoeKind.Dragon,
            [
                "Dragon! Spread out, don't bunch up!",
                "Mind the fire! Mind the fire!",
                "That's a real {arg}! Oh, this is bad. This is very bad.",
                "Keep moving! Never stand in front of it!"
            ],
            "The {arg} is down! I can't believe it!",
            "Scales and teeth, and it still died."
        );
        Foe(FoeKind.Daemon,
            [
                "A daemon! Virtues protect us!",
                "Hellspawn! Don't look in its eyes!",
                "Look at that {arg}! Fight it with everything!",
                "Smell the brimstone? Here it comes!"
            ],
            "Back to the abyss with you!",
            "Begone, fiend!"
        );
        Foe(FoeKind.Orc,
            [
                "Orcs! Smell them before you see them!",
                "Greenskins! Come on, then!",
                "Orcs! Mind the axes!",
                "Here come the orcs. Loud as ever."
            ],
            "One less orc in the world.",
            "That orc's done shouting."
        );
        Foe(FoeKind.Crawler,
            [
                "Spiders! I hate spiders!",
                "Watch for venom!",
                "Too many legs! Far too many legs!",
                "Don't get caught in the webs!"
            ],
            "Squashed.",
            "Dead, and still twitching. Disgusting."
        );
        Foe(FoeKind.Giant,
            [
                "Big one! Go for the legs!",
                "Watch that club! One hit and you're done!",
                "Look at the size of that {arg}! Bigger they are, harder they fall!",
                "Stay out of its reach!"
            ],
            "Timber!",
            "And down comes the {arg}!"
        );
        Foe(FoeKind.Troll,
            [
                "Troll! Keep hitting it, they heal!",
                "Don't let that {arg} close!",
                "Trolls! Mind the claws!"
            ],
            "And it's not getting up this time.",
            "Done with the troll."
        );
        Foe(FoeKind.Reptile,
            [
                "Scales! Aim for the soft underbelly!",
                "Watch for the tail!",
                "Snakes. Why is it always snakes?"
            ],
            "Cold-blooded, and now just cold.",
            "That one won't be shedding its skin again."
        );
        Foe(FoeKind.Elemental,
            [
                "An elemental! Magic will hurt it more than steel!",
                "Living {arg}! Careful!",
                "The very earth's come to life! Stand firm!"
            ],
            "Back to the elements with you.",
            "Scattered."
        );
        Foe(FoeKind.Ooze,
            [
                "Slime! Don't let it touch your gear!",
                "Ugh, that thing'll eat through steel!",
                "Disgusting! Kill it, quickly!"
            ],
            "I'm going to be cleaning that off for a week.",
            "Splat."
        );
        Foe(FoeKind.Gargoyle,
            [
                "Gargoyles! Watch above!",
                "Stone skin! Hit hard!",
                "A gargoyle! These aren't the friendly sort!"
            ],
            "Rubble.",
            "Back to stone."
        );
        Foe(FoeKind.Harpy,
            [
                "Harpies! Mind your eyes!",
                "Shrieking hags! Bring them down!",
                "Watch above, {owner}! Harpies!"
            ],
            "Grounded.",
            "That's one less screaming bird-woman."
        );
        Foe(FoeKind.Vermin,
            [
                "Vermin! Squash them!",
                "Rats! Big ones!",
                "Pests! Clear them out!"
            ],
            "Squeak no more.",
            "Pest control, done."
        );
        Foe(FoeKind.Beast,
            [
                "Beasts! Mind the teeth!",
                "It's hungry! Don't let it pull you down!",
                "Wild {arg}! Stand together!"
            ],
            "Poor creature. It was only hungry.",
            "That'll be a fine pelt."
        );
        Foe(FoeKind.Human,
            [
                "Brigands! Protect the coin!",
                "You picked the wrong travellers, friend!",
                "Bandits! Draw!",
                "Drop your weapon and run, and you might live!"
            ],
            "Should have chosen honest work.",
            "Crime doesn't pay. Clearly.",
            "Another rogue off the roads."
        );
    }

    private static void Foe(FoeKind kind, string[] battleCries, params string[] kills)
    {
        _foeBattleCries[(int)kind] = battleCries;
        _foeKills[(int)kind] = kills;
    }

    /// <summary>What kind of foe <paramref name="foe"/> is, by its name, or a human by its shape.</summary>
    public static FoeKind KindOf(Mobile foe)
    {
        if (foe == null)
        {
            return FoeKind.None;
        }

        var words = BareName(foe.Name).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var (kind, keys) in _foeWords)
        {
            foreach (var word in words)
            {
                foreach (var key in keys)
                {
                    if (word.StartsWith(key, StringComparison.Ordinal))
                    {
                        return kind;
                    }
                }
            }
        }

        return foe.Body.IsHuman ? FoeKind.Human : FoeKind.None;
    }

    /// <summary>A battle cry for this kind of foe, or null to use a general one.</summary>
    public static string BattleCry(FoeKind kind) => _foeBattleCries[(int)kind]?.RandomElement();

    /// <summary>A remark on killing this kind of foe, or null to use a general one.</summary>
    public static string KillLine(FoeKind kind) => _foeKills[(int)kind]?.RandomElement();
}
