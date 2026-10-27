# Adventuring Bands — Design

Dungeons feel empty unless a player is fighting in them. Bands are NPC adventuring parties that
fight their own battles on each dungeon level, so a player walking in finds the place already
alive: steel and spell words ahead, banter, insults hurled at monsters, a band resting after a
fight. They are **NPC-only**: nobody hires, commands or trades with them.

Status: **stage 1 built** (one band, placed by staff with `[Add AdventurerBand`). Stages 2 and 3 to come.

## Built on the companion system

Bands reuse the companions' fighting code (`Engines/Companions/`): warrior, bard and mage tactics,
healing, fight reading, retreat, resurrection, formation and the speech engine. What bands leave out:
hiring, wages, regard, levelling, the guild ledger, spoken orders, gates and runebooks, mounts,
smith trips, and handing loot to anyone.

**The captain stands in for the employer.** The companion code follows and protects an employer,
which can be any creature, not only a player. Each band has a **captain**, an NPC fighter who:
- walks the band's patrol, from spawner to spawner on its dungeon level
- picks fights with the monsters he meets
- calls the retreat when the band is outmatched. The companions already say "Say the word and we
  fall back!"; the captain is the one who says the word.

The other members (a warrior, a bard and a mage) are companions in **band mode**, with the captain as
their employer. They follow him in formation, guard him, heal him and fall back with him.

## Rules

- **Players can't harm bands.** Bands are innocent, and Trammel's rules forbid attacking innocents.
  Only monsters can bring a band down.
- **No kill stealing.** A band never picks a monster that a player is fighting or is being fought
  by. If a player joins in on a band's foe, the band steps back and gives it up ("All yours,
  friend.").
- **Their kills are theirs.** Trammel already locks a monster's corpse to those who fought it for two
  minutes. A band member walks over and "loots" it: the items are deleted, not kept, so nothing builds
  up in the save. If the band is wiped out before collecting, the corpses open up to anyone after
  those two minutes.
- **A fallen band can be looted:** a magic item or two and a small amount of gold, made at the moment
  of death. Their worn gear stays on the body but can't be taken.
- **They don't attack pets** or anything else belonging to a player.
- **Their kit never wears out** (weapons, armour, shields, clothing), since a band has no smith in the
  dungeon. Only band members: hired companions' gear wears as usual.

## Long, even fights

- Blows bend towards an even fight, using the companions' own read of it (enemy strength, weighted by
  how hard they hit, over the band's). Against far weaker foes the band's blows are weakened, down to
  30% (`bands.minDamageDealtScale`), so a pushover becomes a scrap. Against far stronger foes
  monsters' blows are only nudged down, to 80% at most (`bands.minDamageTakenScale`): realism means
  some fights are won and some lost, and a hopeless one is lost or run from. Between equals nothing
  changes. (A first version halved damage both ways; a level-10 band beat four ogre lords.)
- Bands take on what they can handle, heal each other and pull back when hurt, so wipe-outs are rare.
- Band strength follows depth: novices on the upper levels of Despise, veterans deep in Fire and
  Destard.

## Only alive when watched

ModernUO puts creatures to sleep when no player is near, and bands sleep with them. A battle nobody
can see changes nothing, so this costs nothing and is exactly when it matters. Between visits a band
keeps a cheap record: kills, gold won, members lost.

## Talk (the main event)

Bands mostly exist to be seen and heard:
- **between members:** teasing, old stories, arguing over the route
- **in a fight:** insults at monsters, encouragement, "Heal me!", "On your left!", "Fall back!"
- **after a fight:** relief, bragging, mourning, grumbling about the loot
- **to passing players:** greetings, warnings ("Mind the lich past the bridge"), a sharp word if you
  stand in their way

The companions' lines about monsters and places can be reused.

## Stages

1. **One band on one level** (Despise level 1). A GM places a band marker; the captain and three
   companions in band mode appear, patrol, fight at reduced damage, retreat, and absorb their kills.
   No new speech yet.
2. **The band voice.** Banter, fight calls, insults, after-fight talk, lines for passing players.
3. **Spread out.** A band on every dungeon level, stronger with depth; loot on death; wipe-out and a
   new band arriving later; town crier news ("No word from the band that went into Destard").
