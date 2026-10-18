# Town Life — Living Towns

Towns have residents with names, trades, front doors and daily routines. On Trammel: Britain (28 residents),
Vesper (26: no farmers, it has no farmland) and Ocllo (15: a small island town, 6 of them bank lurkers).
Each town also has its own local talk (Vesper's canals and Minoc ore carts, Ocllo's pirates and quiet).

## What players see
- **A cast, not a spawn.** Britain has 28 residents: 2 farmers, 2 fishers, a smith's apprentice, 3 townsfolk,
  2 shoppers, a drunk, a minstrel, a night watchman, a beggar, a courier, a noble and 12 bank lurkers
  (idlers, hawkers, gamblers and dandies who hang about in the street in front of the main bank's door, the bank nearest
  the town's centre (in Britain, the west bank), most of the day, selling, boasting and dicing). Each has a name, a
  working outfit, a manner (friendly, gruff, nervous, chatty) and a front door they live behind.
- **A day by the game clock** (one UO day is about two real hours). Everyone in a town keeps the same hours.

  | Time | Most residents |
  |---|---|
  | Night (22–5) | Home. The streets thin out to the drunk, the night watch, the beggar, maybe the minstrel, and a few lurkers at the bank |
  | Dawn (5–8) | Farmers to the fields, fishers to the docks, the apprentice to the forge |
  | Morning (8–12) | Work, market, bank; the courier runs errands |
  | Midday (12–14) | The tavern or the market |
  | Afternoon (14–18) | Back to work, more shopping |
  | Evening (18–22) | The tavern, or home |

- **Work you can see.** Farmers hoe, fishers cast (with a splash), the apprentice hammers at the anvil,
  drinkers drink, bank-goers count coins, shoppers haggle, the minstrel plays.
- **Talk.** Remarks about their trade, the place they're at (naming the innkeeper or banker), or the time
  of day. They greet passers-by in their own manner, flinch from ghosts and murderers, beg from the
  passing, and say "Pardon me" when someone blocks the road.
- **Conversations.** Neighbours stop to chat in short back-and-forth exchanges, standing and facing each
  other; people walking past trade a quick word ("Morning, Seif!" / "Ilsa! Keeping well?"). Who's talking
  shapes what's said: the night watch moves the drunk along, the beggar works on the noble, the farmer and
  fisher swap turnips for fish, the smith's apprentice mends the farmer's plough. Some talk depends on the
  place (toasts in the tavern, prices at the market) or the hour, and townsfolk gossip about each other by
  name. They also have words with the vendors: haggling at the market, ordering ale from the innkeeper,
  checking on their gold with the banker.
  Only within earshot of a player, at most one new conversation every 12 seconds per town, and each person
  rests a few minutes between chats, so the town hums rather than chatters.
- **They notice you.** Each remark is made once per passer-by, not every time they're seen:
  - no clothes on ("Put some clothes on!"), a blade drawn in war mode, a spell being cast, someone running
    headlong past;
  - full plate ("Expecting a war?"), a nightmare or other exotic mount, an ordinary horse now and then;
  - someone walking with two or more hired companions ("Quite the entourage.");
  - a dragon or other fearsome pet makes them step back ("Is that thing TAME?!"); a cat or dog gets cooed at;
  - they keep out of a murderer's way.
  Gruff residents are hard to impress ("Tin can."); nervous ones startle at everything.
- **Your companions.** Residents talk to the companions walking with you: "Don't turn me into a toad!" to
  the mage, "Ever fought a troll?" to the warrior, and Britain's minstrel challenges a visiting bard to a
  contest at the market. A companion told to keep `quiet` is left alone, as is one in a fight.
- **The talk of the town.** Townsfolk gossip about what really happens on the shard, for two days
  afterwards (and across restarts):
  - a player slaying something fearsome: "Did you hear? Ana slew an ogre lord in Despise!"
  - players dying to monsters, players murdered by other players (and by whom);
  - someone becoming a Grandmaster: "Ana's a grandmaster of magery now. Some people.";
  - newcomers to Britannia, and adventurers losing a hired companion.
  It comes up in conversation (fresh news most of all) and in passing remarks. They also **recognise the
  people the news is about**: "You're the one who slew the ogre lord! An honour.", "Back from the dead, I see.",
  "You're new here, aren't you?"
- **The town remembers.** Kill a resident and their neighbours mourn them by name ("I still can't believe Tom
  is gone.") and know who did it: they call you out ("You! You killed Tom!") and keep out of your way for
  as long as the news lasts.
- **Danger.** Attacked, a resident runs and shouts for the guards. So does anyone who sees a monster loose
  in the street (anything hostile bigger than a rat) or blade spirits whirling about. Residents are ordinary innocents.
  A killed resident is mourned for 30 minutes before someone new takes up the trade.

## How it works
- **Places are found, not placed.** At start-up the town floods its streets with the real movement rules
  (doors count as open), which takes a few seconds for Britain. Vendors then mark the tavern, bank, smithy,
  docks, healer and shops; the town's named field regions mark the farmland; and **front doors** (open sky on
  one side within a few steps, past any porch or eaves, and a roof on the other) become homes.
- **Long walks are planned across town** and walked a short leg at a time, so they can cross Britain.
- **Only watched residents walk.** NPC AI sleeps when no player is near, so out of everyone's sight a
  resident simply arrives at the next place (or goes indoors). Going home means stepping through their front
  door and off the map until morning.
- Residents are saved with the world. After a restart they pick up their day where the clock now stands.

## GM commands
| Command | Effect |
|---|---|
| `[TownLife` | Status: street map, places found, time of day, residents (and how many are at home) |
| `[TownLife places` | Every place each town found, with coordinates |
| `[TownLife populate` | Fill any empty trades now, without the usual 30-minute wait |
| `[TownLife clear` | Remove every resident (`populate` brings a new cast) |

## Configuration (`Configuration/modernuo.json`)
| Setting | Default | Meaning |
|---|---|---|
| `townLife.enabled` | `true` | Turn the whole system on or off |
| `townLife.replacementDelay` | `00:30:00` | How long a dead resident's trade stays empty |
| `townLife.newsLifetime` | `2.00:00:00` | How long townsfolk keep talking about something that happened |

## Code
`Projects/UOContent/Engines/TownLife/`: `TownLifeSystem` (start-up, the routine timer, the command), `Town`
(places, cast), `StreetMap` (walkable tiles, long routes), `Resident` and `ResidentAI`, `ResidentSchedule`,
`ResidentSpeech`, `ResidentDialogues` (every conversation, with who/where/when it suits) and `Conversation`
(runs one), `TownNews` (what's happened, saved with the world; fed by creature and player death events,
character creation, and one line in `PlayerMobile.OnSkillChange`). Towns and their cast are listed in `TownLifeSystem.Configure`.

## Planned
1. More towns, then interaction: `news`/`rumors`, small errands.
