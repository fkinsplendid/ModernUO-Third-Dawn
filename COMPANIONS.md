# Companions — Setup & Player Guide

Players can hire up to three companions: a **Warrior**, a **Bard** and a **Mage**. They follow,
fight, heal, loot, share, level up and talk. Code lives in `Projects/UOContent/Engines/Companions/`.

---

## GM setup

Place a guildmaster in each town where companions should be hired:

```
[Add CompanionGuildmaster
```

They stand still and are invulnerable. Players double-click them, or say **"hire"** or
**"companion"** nearby, to open the guild ledger.

### Configuration

All values are in `Distribution/Configuration/modernuo.json` (written on first boot):

| Setting | Default | Meaning |
|---|---|---|
| `companions.hireFee` | 1000 | Gold to hire; includes the first day's wage |
| `companions.minDailyWage` | 50 | Wage per day at level 1 |
| `companions.maxDailyWage` | 150 | Wage per day at max level |
| `companions.wageDayMinutes` | 30 | Real minutes in a wage "day" (only counted while out with their employer) |
| `companions.maxLevel` | 30 | Level cap |
| `companions.experienceScale` | 1.0 | Multiplier on experience from kills |
| `companions.goldShare` | 0.1 | Fraction of looted gold a companion keeps towards their wages |
| `companions.resurrectFee` | 500 | Guild fee to resurrect a fallen companion |
| `companions.catchUpDelay` | 00:00:20 | How long a companion is out of sight before catching up |
| `companions.ghostPatience` | 00:01:00 | How long a ghost waits, once the fighting stops, before seeking a healer or GM |

Useful `[Props` on a companion: `Level` (setting it applies stats/skills), `Purse`, `Experience`,
`Personality`, `Quiet`, `State`, `Employer`.

---

## For players

### Hiring and wages
- Hire from a companion guildmaster. Each companion uses **1 follower slot**.
- Wages are paid from the companion's **purse**. Hand them gold (or a bank check) to top it up.
  They also keep a small cut of any gold they loot.
- They warn you a few minutes before a wage is due if the purse is short. If it's empty when
  the day ends they **go back to the guild** — nothing is lost, rehire them from the ledger.
- Wages only tick while they're out with you. Log out and they wait for you, unpaid and safe.
- Their **starting kit belongs to the guild**: you can't take it off them, and anything they
  replace goes back to the guild. Gear and supplies you give them stay yours.
- **Ending a contract** (guild ledger, while they're resting at the guild) is permanent: their
  experience is lost, but everything of yours they were carrying goes to your bank box.

### What they do on their own
- **Read the fight**: they sort enemies into *brutes* (big melee beasts like ogre lords) and
  *spellcasters* (liches and the like), and weigh whether the party is outmatched. When it is, one of
  them says so ("Say the word and we fall back!"), but only you call the retreat.
- **Fight**: defend you and each other from anything attacking the party.
  - *Warrior*: front line; peels enemies off the mage and bard and taunts them away. With the
    backline safe, rushes spellcasters, since a blow spoils a spell.
  - *Bard*: turns enemies on each other (provocation) whenever there are two to pair up,
    calms (peacemaking) whatever threatens the backline when it can't, and weakens your
    target (discordance). Peace comes first only to save a badly wounded bard or mage. When brutes
    are winning, provocation comes before everything and is worth a long shot (15% odds, not 30%).
  - *Mage*: fights like a Third Dawn mage, holding a quarter of its mana back for healing.
    It only casts what is in its **own spellbook**, issued by the guild, which grows with its
    Magery a circle at a time. Each circle goes in at the point where it can be cast at about
    even odds: the Nth circle at 10×N + 10 Magery (5th at 60, Mark's 6th at 70, Gate's 7th at
    80, Resurrection's 8th at 90, the whole book). A new hire knows circles 1-5. The mage
    announces each new circle. The book is private: it can't be taken, opened, counted or
    written into with scrolls. The `skills` window shows how far through it the mage is.
    - *Defence*: teleports clear of melee when hurt or swarmed, or paralyzes a lone attacker the
      warrior isn't dealing with. Keeps one defensive spell up: Magic Reflection against
      spellcasters, otherwise Protection while being hit.
    - *Summons*: dispels enemy summons it has a fair chance against; Mass Dispel for several.
      Against brutes, with no spellcasters about to dispel them, puts Blade Spirits (Magery 65+) or
      an Energy Vortex beside the strongest one, up to two at a time. Once the vortex is in the
      book (Magery 90) the mage tries it about as often as it expects to land it: half the time at
      90, three times in four at grandmaster, and a fizzle costs the reagents. The cast is long,
      so only when nothing is hitting the mage. Like any player's, they choose their own targets.
    - *Control*: puts an Energy Field (or Wall of Stone) in front of a brute charging it, and a
      Paralyze Field in the path of a pack nobody has engaged. Walls never cut off the party.
    - *Poison*: Poison Field on groups, and the Poison spell on every enemy in reach that isn't
      already poisoned (skipping the immune, and anything that resists twice). Mages train
      Poisoning, which strengthens the spell: Regular on hire, Greater around level 13, now and
      then Deadly at grandmaster. Strength drops off beyond three tiles.
    - Then the strongest damage spell it has the mana and reagents for, each tried about as
      often as the mage expects to land it. A newly learned circle gets chanced and often
      fizzles (gaining Magery all the same) while the old reliable spells fill in; with skill,
      the new spells take over. It won't break a
      paralysis nobody is fighting. Out of mana, it meditates where it stands, and between fights
      whenever the party stops.
    - *Bless*: keeps you and the party blessed during a hunt, recasting in the lulls between
      fights. Once a minute passes with no fighting it stops, so a walk through town costs no
      reagents.
- **Heal**: bandages (warrior, bard) and spells (mage) — poison first, worst wounds next, you
  slightly ahead of everyone else. Potions when they're in trouble.
- **Potions**: in a fight they drink heal, cure, refresh, strength and agility potions, but
  only ones you (or the guild) gave them. Potions they loot are yours and are handed over with
  the spoils; pass one back and they'll keep it. They won't take potions they've no use for
  (explosion, poison, night sight).
- **Revive**: skilled healers (Healing and Anatomy 80+) bring fallen companions back after a
  fight and offer you resurrection if you die. A mage with the 8th circle (Magery 90) casts
  Resurrection on you and on fallen companions once the fighting stops. A fallen companion
  stands still while someone in the party is raising it.
- **Gear up**: they wear the best weapons and armour in their packs. Hand them gear and
  they'll use it if it suits them (mages refuse metal armour and weapons, bards refuse shields
  and heavy armour).
- **Loot**: after a fight they search the corpses of what the party killed for gold, gems,
  reagents, bandages, potions, scrolls, jewellery and magic gear.
- **Share**: when you're close and not fighting, they hand you the spoils. Supplies go to
  whichever companion uses them (looted potions always come to you); gear goes to whichever companion it suits. Near a banker in
  town, they deposit straight into your bank box.
- **Ask for supplies**: they tell you when bandages, reagents or the bard's instrument run low.
- **Gate out**: a mage with the 7th circle (Magery 80+) opens a gate when the party is losing badly, or when
  everyone is weighed down with loot. Step through and they follow. Where the gate leads:
  - Every mage carries its **own runebook**, issued by the guild and empty at first. Give it
    a blank rune and say `mark` (or `mark Minoc mine` to name it): it marks where it stands
    and files the rune in the book. Unnamed, the mage writes where it is: the region's name,
    "Outside Minoc" near a town, or "North-east of Minoc" in the wilds. Filler is ignored
    ("mark this rune as Minoc mine" names it "Minoc mine"); a name already in the book gets a
    number ("Minoc 2"). Hand it an already-marked rune and that goes in the book
    too. The book holds 16; past that, runes stay loose in its pack.
  - Its gates go to the book's **usual** place: the first one filed, or whichever you pick
    with `usual <place>`. Say `gate <place>` or `gate to <place>` for any other (partial names
    work: "gate minoc" finds "Minoc Mine Entrance"). It only gates within the land it's in, and
    never to a place you're already standing at (within about a screen).
  - **A gate only ever leads to a marked rune.** No rune, no gate: a mage with an empty book
    can't gate the party out of a lost fight or back to town, so mark somewhere safe early.
  - Say `runebook` to read the book over the mage's shoulder: each place with a hint of where it
    is ("Outside Britain"). Write over a name and press save to rename it, or press the button
    beside a place to make it the usual. You can't travel with the mage's book yourself.
  - **If the mage dies, the runebook burns with it**, every place in it lost. It's raised with
    a fresh, empty book. Keep your mage alive.
  - The book can't be taken from the mage's pack. Blank runes are kept, not shared back. At 80 Magery about half of gate casts
  fizzle; the mage tries up to three times, spending reagents on each. Resurrection (Magery
  90+) fizzles as often at first, and the mage tries again a moment later.
- **Mind the fields**: no companion walks into a fire, poison or paralysis field, theirs or
  the enemy's. They step around it, or wait if it blocks the way; caught in one, they walk out.
- **Keep their own space**: walking or riding, each has a place around you: the warrior at
  your right flank, the bard behind on your right, the mage behind on your left. They keep one
  clear tile between themselves, you and each other, mounted or not, but always stay within two
  tiles of you so you can hand them things. When you stop they stay where they are instead of
  shuffling, and in corridors they fall in behind you.
- **Catch up**: if you recall, gate, take a teleporter or change facet, they rejoin you within a
  few seconds. If they're simply outrun or stuck behind something, they find their way back, or
  turn up after `companions.catchUpDelay` (20 seconds) unless they're busy fighting where they
  are. They bring their mount with them.
- **Talk**: idle chatter about the surroundings, reactions in combat, banter between them.
  Each has a personality (stoic, cheerful, gruff or witty) and talks about its own trade (the
  bard hums, the mage frets about reagents). They remark on arriving in a town, dungeon or
  shrine and chat about local lore while there, shout battle cries suited to the foe (undead,
  dragons, orcs, spiders and more), boast after felling something notable, notice dawn and dusk,
  complain when you stand about too long, and cheer when you're raised from the dead.

### Retreating
Only you can judge when a fight is lost, so they take their cue from you. In the middle of a fight,
any of these makes the whole party drop what it's doing and run with you:
- say `retreat`, `fall back`, `run`, `run for it` or `flee` (no name or `all` needed);
- say `all follow me` (or `<name> follow me`), the classic way to pull pets out of a fight;
- or just **run for it**: once you're running, at least 8 tiles clear of every enemy and pulling
  further away, they see you've gone and follow.

While falling back nobody casts, sings or bandages anyone else; they drink potions on the run, and a
mage with something on its heels teleports clear. Once nothing has been close behind for about five
seconds they settle down ("I think we lost them"). For twenty seconds after that they defend themselves
against anything that followed but won't go back for the rest. `all kill` or `all guard` ends a retreat.

### Mounts
Give a companion a mount the way you'd give a pet to another player: use **Transfer** (context
menu, or say `<pet name> transfer`) and target the companion. From then on it's theirs:

- It no longer takes one of your follower slots. Any bond it had with you is broken.
- They walk over and climb on, ride everywhere, and fight from the saddle as a player does.
  Mounted, they move at a mounted player's pace, and they hurry to keep up when you pull ahead.
- They keep it fed, take it along when you log out or dismiss them, and lead it through gates.
- They turn down a pet that isn't a mount, a second mount, a mount that won't carry them
  (a unicorn won't take a man, a ki-rin won't take a woman), and any beast they couldn't control
  at least half the time. With no taming skill that means horses, ostards and llamas
  are fine, but a nightmare or a ridgeback is not.
- If they die, the mount throws them and stays with their ghost. It can be killed then. When
  they're revived they get back on. If it's killed they grieve, and later ask you for another.
- A poisoned mount isn't ridden until it recovers.
- Ending the contract gives the mount back to you. If you have no follower room, it's left at
  the stables for you to claim from any animal trainer.

### Dying
A companion who dies becomes a ghost and follows you, keeping everything they carry. Revive them
with the **Resurrection** spell, with bandages (Healing/Anatomy 80+), by letting another
companion do it (a high-level mage casts Resurrection; skilled healers use bandages), or by
paying the guildmaster.

If nobody raises them, a ghost doesn't just wait. About a minute after the fighting stops it
goes to the nearest **wandering healer** within sight (any free healer) and is resurrected there.
If there's no healer but a **GM** is nearby, it goes to them and asks; the GM gets a message
and can use `[res` on it. Otherwise it follows you and reminds you from time to time.

### Skill
New hires start capable (about 52 in their trained skills) and are guaranteed grandmaster by
level 25. Each role is a little ahead in its specialty: the bard's Musicianship (+20) and bard
skills (+10), the warrior's Tactics (+10) and Parry (+5), the mage's Magery (+10) and Poisoning (+20). The bard
also weighs its odds before playing, and won't waste a song on a creature it can't sway. Skills also rise with use, up to a cap that reaches 100 at level 20.

### Talking to them
Address one by name (`Aldric, status`) or everyone at once (`all share`, `companions, quiet`).

| Say | Effect |
|---|---|
| `status` / `report` | Party status window: level, experience, health, purse, wages. The button by each name shows their skills |
| `skills` / `spells` | That companion's skills, highest first. A mage also shows its chance to cast Recall, Mark, Gate Travel and Resurrection, and the places it can gate to |
| `share` / `spoils` / `loot` | Hand over everything worth handing over now |
| `pack` / `inventory` | Open their backpack (you can also use the context menu) |
| `quiet` / `hush` | Stop idle chatter (they still warn you about danger) |
| `speak freely` / `chatter` | Resume chatter |
| `gate` / `town` | The mage opens a gate to its usual place |
| `gate <place>` / `gate to <place>` | The mage opens a gate to a runebook entry or rune by name |
| `destinations` / `where can you gate` | The mage lists the places it can gate to |
| `runebook` / `runes` | Read the mage's runebook: rename places and pick the usual one (also a button on the mage's skills window) |
| `mark` / `mark <name>` | The mage marks a blank rune where it stands, naming it if you say a name, and files it in its runebook |
| `usual <place>` | The mage's gates lead to that runebook entry from now on (`usual` alone says which it is) |
| `dismount` | Get down and leave the mount standing where it is (e.g. before a boat) |
| `mount up` / `ride` | Get back on |
| `return your horse` / `give me your mount` | Hand their mount back to you (needs follower room) |
| `dismiss` | Send them back to the guild |
| `why` / `tactics` | Each says how they read the fight and the last thing they decided (and why a bard is holding its songs) |
| `retreat` / `fall back` / `run` / `flee` | In a fight: everyone breaks off and runs with you (see *Retreating*) |

All the usual pet commands still work: `all follow me`, `all guard me`, `all kill`, `all stay`, etc.
`Release` (spoken or from the context menu) sends a companion back to the guild instead of freeing them.

Looting follows your rights: they only search corpses of creatures the party killed, never a kill
you have no right to, and never anything that would be a criminal act.
