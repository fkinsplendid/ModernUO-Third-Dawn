using System;
using System.Collections.Generic;
using Server.Regions;

namespace Server.Engines.Companions;

/// <summary>
/// What companions say about the places they visit: a remark on arriving, and local chatter while
/// there. Towns and dungeons without lines of their own get general ones. Tokens as elsewhere;
/// {arg} is the place's name.
/// </summary>
public static partial class CompanionSpeech
{
    private sealed record PlaceTalk(string[] Arrive, string[] Idle);

    private static readonly Dictionary<string, PlaceTalk> _places = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _townArrive =
    [
        "So this is {arg}.",
        "{arg}. Let's see what it has to offer.",
        "Ah, {arg}. I could use a drink.",
        "{arg}! Civilisation, at last."
    ];

    private static readonly string[] _dungeonArrive =
    [
        "{arg}. Weapons ready.",
        "{arg}. Stay sharp in here.",
        "So this is {arg}. Smells about how I imagined.",
        "Into {arg}, then. Stay close, {owner}."
    ];

    private static readonly string[] _shrineArrive =
    [
        "The {arg}. Speak softly here.",
        "I always feel lighter at a shrine.",
        "The {arg}. Take a moment, {owner}. We've earned it.",
        "Whatever the road holds, the Virtues hold firm."
    ];

    private static void PlaceLines()
    {
        // ---------- Britannia's towns ----------

        Place("Britain",
            [
                "Britain! Lord British's own city.",
                "The capital. Mind the crowds, {owner}.",
                "Britain. Every road in the land ends here eventually."
            ],
            "Lord British's castle never looks any smaller, does it?",
            "The Blue Boar does a fine stew, if we've the time.",
            "Half the bards in Britannia are playing in this city. Badly.",
            "They say Blackthorn's castle is just up the road. Strange man, Blackthorn.",
            "So many shops. So little gold.",
            "Have you seen the graveyard east of town at night? Don't."
        );
        Place("Trinsic",
            [
                "Trinsic. The city of Honor.",
                "Trinsic's walls! Nothing gets in here without the paladins knowing.",
                "Ah, Trinsic. Stand up straight, {owner}, the paladins are watching."
            ],
            "The paladins here train every morning. You can hear the drills from the gate.",
            "Honor. It's carved on everything here, and they mean it.",
            "Finest walls in Britannia. Built to be tested.",
            "Even the merchants are honest in Trinsic. It's unnerving."
        );
        Place("Minoc",
            [
                "Minoc! City of Sacrifice. And forges.",
                "Minoc. Hear that? Every smith in the north, all at once.",
                "Ah, Minoc. Smell the coal smoke."
            ],
            "Miners come down from those mountains with ore in their beards.",
            "The tinkers here can make anything. Mostly things that explode.",
            "Covetous lies up in the hills. Nobody here talks about it much.",
            "Minoc folk give all they have for each other. That's Sacrifice for you.",
            "Best steel in the land comes out of these forges."
        );
        Place("Yew",
            [
                "Yew. City of Justice, deep in the woods.",
                "Yew. The trees here are older than kingdoms.",
                "Ah, Yew. Smell that? Wine from the abbey."
            ],
            "The monks of Empath Abbey make the finest wine in Britannia.",
            "The court of Yew judges fairly. They say. I'd rather not find out.",
            "Druids walk these woods. You'll never see one unless they wish it.",
            "Yew's not so much a town as a forest with opinions.",
            "The prison here is no joke. Justice is taken seriously in Yew."
        );
        Place("Moonglow",
            [
                "Moonglow. City of Honesty, and of mages.",
                "Moonglow! The Lycaeum is just north of here.",
                "Verity Isle. Mind your words, {owner}. Honesty's the custom here."
            ],
            "The Lycaeum holds more books than anyone could read in ten lifetimes.",
            "Every second person in Moonglow is a mage. The other half are pretending.",
            "There's a great telescope at the Lycaeum. I'd love a look through it.",
            "Reagents are cheap here. Mages buy them by the barrel."
        );
        Place("Vesper",
            [
                "Vesper. Bridges, bridges and more bridges.",
                "Ah, Vesper. Mind the canals, {owner}.",
                "Vesper! A merchant's town if ever there was one."
            ],
            "More water than street in this town.",
            "Someone fell in the canal last time I was here. It wasn't me. Mostly.",
            "Vesper merchants could sell sand to a desert.",
            "The tinkers of Vesper are almost as good as Minoc's. Don't tell them I said that."
        );
        Place("Skara Brae",
            [
                "Skara Brae. City of Spirituality, out on its island.",
                "Skara Brae! Rangers' country.",
                "The ferry's worth it. Skara Brae's lovely this time of year."
            ],
            "The rangers here can track a squirrel across bare rock.",
            "So many farms. Skara Brae feeds half the west.",
            "It's peaceful here. Too peaceful. I keep looking over my shoulder.",
            "The rangers' guild trains the best archers in the land."
        );
        Place("Jhelom",
            [
                "Jhelom. City of Valor.",
                "Jhelom! Keep your hand off your sword, unless you want a duel.",
                "Ah, Jhelom. Everyone here wants to fight you. Politely."
            ],
            "The dueling pits! Shall we watch a bout, {owner}?",
            "Valor, they call it. Picking fights, I call it.",
            "Every mercenary worth their salt trained here once.",
            "Mind who you look at in Jhelom. Some take it as a challenge."
        );
        Place("Magincia",
            [
                "Magincia. City of Humility.",
                "Magincia. Once the proudest city in the land, they say.",
                "Ah, Magincia. Walk humbly here, {owner}."
            ],
            "They say Magincia was brought low for its pride, long ago.",
            "Humility. Hard lesson for a city to learn. Harder for a person.",
            "The merchants here still dress like kings. Old habits."
        );
        Place("Cove",
            [
                "Cove. Small, quiet, and far too close to orcs.",
                "Cove. Keep an eye out, {owner}. The orcs are just up the hill."
            ],
            "The orc fort's not far from here. Cove sleeps with one eye open.",
            "Quiet little place. Fishermen and guards, mostly."
        );
        Place("Serpent's Hold",
            [
                "Serpent's Hold. The knights' island.",
                "Serpent's Hold! Fortress of Courage."
            ],
            "Every knight in the land has trained here at some point.",
            "The Hold has never fallen. Never.",
            "Look at their armour. You could shave in it."
        );
        Place("Buccaneer's Den",
            [
                "Buccaneer's Den. Hands on your purses.",
                "The Den. Pirates, thieves and worse. Stay close, {owner}.",
                "Buccaneer's Den. Don't drink anything you didn't see poured."
            ],
            "Everyone here's either a pirate or lying about not being one.",
            "Count your fingers after you shake hands in the Den.",
            "There's no guard in this town worth the name. Watch yourself."
        );
        Place("Nujel'm",
            [
                "Nujel'm! Look at the domes!",
                "Nujel'm. The richest island in Britannia.",
                "Ah, Nujel'm. Silk, spice and sea air."
            ],
            "Have you seen the giant chessboard? I lost a game on it once. Badly.",
            "The palace here makes Lord British's castle look plain.",
            "Everything's for sale in Nujel'm, and everything costs double."
        );
        Place("Papua",
            [
                "Papua. Out in the Lost Lands.",
                "Papua. Swamp and stilts and mosquitoes."
            ],
            "Mind where you step. The swamp eats boots.",
            "Folk here live on the edge of the world. It shows."
        );
        Place("Delucia",
            [
                "Delucia. The last town before the wild of the Lost Lands.",
                "Delucia! Trappers, traders and tall tales."
            ],
            "Terathans roam not far from here. Delucia keeps its walls high.",
            "Everyone here has a story about something huge in the woods."
        );
        Place("Wind",
            [
                "Wind! The hidden city of mages.",
                "Wind. Few ever find their way in here."
            ],
            "Mind your manners. Everyone in Wind could turn you into a newt.",
            "The libraries here hold secrets even the Lycaeum doesn't have."
        );
        Place("Haven",
            [
                "Haven. Where every adventurer starts out.",
                "Haven. I remember my first day here. I was terrible."
            ],
            "Look at all the fresh faces. Were we ever that young?",
            "Haven's a good place to learn. Or to come back to."
        );
        Place("New Haven", [ "New Haven. Where every adventurer starts out." ], "Look at all the fresh faces. Were we ever that young?");

        // ---------- Ilshenar ----------

        Place("Gargoyle City",
            [
                "The gargoyles' city. Show respect, {owner}. They're kinder than they look.",
                "Gargoyle City. Few humans ever see it."
            ],
            "The gargoyles have a virtue of their own. Something about Principle.",
            "I used to think gargoyles were monsters. I was wrong.",
            "Look at the stonework. Older than any city in Britannia."
        );
        Place("Lake Shire",
            [
                "Lake Shire. Calm water, kind folk.",
                "Lake Shire. I could retire here."
            ],
            "Peaceful, isn't it? Hard to believe Ilshenar's so dangerous."
        );
        Place("Mistas",
            [
                "Mistas. What's left of it.",
                "Mistas. Stay close. The dead don't rest here."
            ],
            "A whole city, fallen. I wonder what happened.",
            "Every shadow in Mistas seems to move."
        );
        Place("Montor",
            [
                "Montor. Ilshenar's crossroads."
            ],
            "Strange folk in Montor. Stranger roads out of it."
        );

        // ---------- Dungeons ----------

        Place("Despise",
            [
                "Despise. A dungeon born of hatred for Compassion.",
                "Despise. Lizardmen, and worse further down.",
                "Despise. Keep your shield up, {owner}."
            ],
            "Hear the river? It runs right through the dark down here.",
            "The deeper we go, the bigger they get. That's the rule in Despise.",
            "Despise. Odd name. I'll think of a ruder one before we leave."
        );
        Place("Deceit",
            [
                "Deceit. The dead walk here.",
                "Deceit. Nothing in here is what it seems.",
                "Deceit. The opposite of Honesty, and it shows."
            ],
            "Skeletons, liches, worse. Deceit's full of things that should be dead.",
            "Don't trust the floors in Deceit. Or the walls. Or anything.",
            "I've heard the dead whisper in here. I'd rather not hear them again."
        );
        Place("Destard",
            [
                "Destard. Dragons. Actual dragons.",
                "Destard. I hope you know what you're doing, {owner}.",
                "Destard. Smell the brimstone? That's dragon."
            ],
            "Every scorch mark in here was made by something with wings.",
            "Valor brought us here. Let's hope it gets us out.",
            "If we hear wings, we run. Agreed?"
        );
        Place("Shame",
            [
                "Shame. Elementals and worse.",
                "Shame. Stay close. The walls themselves move here."
            ],
            "Earth, fire, water, air. Shame has an elemental for every occasion.",
            "Honor's opposite. No wonder it feels so wrong down here.",
            "I swear that pillar just looked at me."
        );
        Place("Wrong",
            [
                "Wrong. An old prison. Some of the prisoners never left.",
                "Wrong. Fitting name."
            ],
            "Cells everywhere. I wonder what they did to end up here.",
            "Justice was turned on its head in this place.",
            "Listen. Chains. Something's still locked up down here."
        );
        Place("Covetous",
            [
                "Covetous. Mind the harpies.",
                "Covetous. Greed dug these tunnels."
            ],
            "Harpies nest up here. You'll hear them before you see them.",
            "There's a lake further in. Don't drink from it.",
            "Sacrifice's opposite. Everything in here wants what you have."
        );
        Place("Hythloth",
            [
                "Hythloth. Daemons. Gargoyles. Balrons. I may have mentioned this before we came in.",
                "Hythloth. The deepest dungeon in Britannia.",
                "Hythloth. If anything shouts 'Kal Vas Flam', duck."
            ],
            "The heat down here. It's like breathing inside a forge.",
            "They say Hythloth goes all the way down to the underworld.",
            "Daemons don't sleep. Don't let your guard down for a moment."
        );
        Place("Ice",
            [
                "Ice. I can see my breath already.",
                "The Ice dungeon. Should've brought a thicker cloak."
            ],
            "Frost trolls. Ice fiends. Even the monsters look cold.",
            "My fingers are going numb. Talk to me so I know I'm alive.",
            "Mind the floor. Slippery doesn't begin to cover it."
        );
        Place("Fire",
            [
                "The Fire dungeon. Watch your step. Really.",
                "Fire. Well, it does what it says."
            ],
            "Lava rivers. Lovely. I'm sweating through my boots.",
            "Fire elementals. Don't stand too close to them, or to anything.",
            "One wrong step here and they'll be scraping us off the rocks."
        );
        Place("Orc Cave",
            [
                "Orc caves. Smell that? Orcs.",
                "The orc caves. Loud, smelly and full of axes."
            ],
            "Orcs keep their treasure deep. Their stink, they keep everywhere.",
            "Mind your head. Orcs don't build for anyone taller than an orc."
        );
        Place("Terathan Keep",
            [
                "Terathan Keep. Spiders and snake-men, at war forever.",
                "Terathan Keep. Keep away from the webs."
            ],
            "Terathans and ophidians hate each other more than they hate us. Small comfort.",
            "Eight legs, venom, and armour. Who designed these things?"
        );

        // ---------- Ilshenar dungeons ----------

        Place("Spectre Dungeon", [ "Spectre Dungeon. Cold as a grave in here." ], "Spectres can pass through walls. Keep your back to something solid anyway.");
        Place("Spider Cave", [ "The spider cave. I hate this already." ], "Webs everywhere. Don't touch them. Don't touch anything.");
        Place("Blood Dungeon", [ "Blood Dungeon. I'm not going to ask why it's called that." ], "The walls are wet. I'm telling myself it's water.");
        Place("Wisp Dungeon", [ "The wisp dungeon. Look at the lights!" ], "Wisps are clever. Don't follow them.");
        Place("Ankh Dungeon", [ "The Ankh dungeon. Built in the shape of a holy sign. Full of unholy things." ], "Strange to find a sign of the Virtues somewhere so dark.");
        Place("Sorcerer's Dungeon", [ "Sorcerer's dungeon. Magic's thick in the air." ], "Every sorcerer down here thinks they're the cleverest. Let's prove them wrong.");
        Place("Rock Dungeon", [ "Rock dungeon. Imaginative name." ], "Every pile of rocks in here might get up and hit us.");
        Place("Ancient Lair", [ "The Ancient Lair. Something very old sleeps here." ], "Quiet. Very quiet. If it wakes, we run.");
    }

    private static void Place(string name, string[] arrive, params string[] idle) => _places[name] = new PlaceTalk(arrive, idle);

    /// <summary>
    /// The place a region belongs to, for remarks: a place with lines of its own, a shrine, or a
    /// named town or dungeon. Null for the open road and for fields and farms.
    /// </summary>
    public static string PlaceOf(Region region)
    {
        for (var r = region; r != null; r = r.Parent)
        {
            var name = r.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (_places.ContainsKey(name) || IsShrine(name))
            {
                return name;
            }

            // Fields and farms ("A Wheatfield in Britain 1") are towns too, but not worth a remark.
            if (r is DungeonRegion && name != "Misc Dungeons" ||
                r is TownRegion && !name.StartsWith("A ", StringComparison.Ordinal) && !name.StartsWith("An ", StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    private static bool IsShrine(string place) => place.StartsWith("Shrine of ", StringComparison.Ordinal);

    /// <summary>A remark on arriving at <paramref name="place"/>.</summary>
    public static string ArrivalLine(string place, bool dungeon)
    {
        if (_places.TryGetValue(place, out var talk))
        {
            return talk.Arrive.RandomElement();
        }

        return (IsShrine(place) ? _shrineArrive : dungeon ? _dungeonArrive : _townArrive).RandomElement();
    }

    /// <summary>Local chatter about <paramref name="place"/>, or null if it has none.</summary>
    public static string PlaceIdleLine(string place) =>
        _places.TryGetValue(place, out var talk) && talk.Idle.Length > 0 ? talk.Idle.RandomElement() : null;
}
