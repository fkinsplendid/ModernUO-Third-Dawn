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
- **Fight**: defend you and each other from anything attacking the party.
  - *Warrior*: front line; peels enemies off the mage and bard and taunts them away.
  - *Bard*: calms (peacemaking) whatever threatens the backline, turns enemies on each other
    (provocation) when outnumbered, and weakens your target (discordance).
  - *Mage*: keeps its distance and casts, holding mana back for healing.
- **Heal**: bandages (warrior, bard) and spells (mage) — poison first, worst wounds next, you
  slightly ahead of everyone else. Potions when they're in trouble.
- **Revive**: skilled healers (Healing and Anatomy 80+) bring fallen companions back after a
  fight and offer you resurrection if you die. A high-level mage casts Resurrection on you.
- **Gear up**: they wear the best weapons and armour in their packs. Hand them gear and
  they'll use it if it suits them (mages refuse metal armour and weapons, bards refuse shields
  and heavy armour).
- **Loot**: after a fight they search the corpses of what the party killed for gold, gems,
  reagents, bandages, potions, scrolls, jewellery and magic gear.
- **Share**: when you're close and not fighting, they hand you the spoils. Supplies go to
  whichever companion uses them; gear goes to whichever companion it suits. Near a banker in
  town, they deposit straight into your bank box.
- **Ask for supplies**: they tell you when bandages, reagents or the bard's instrument run low.
- **Gate out**: a mage (Magery 70+) opens a gate when the party is losing badly, or when
  everyone is weighed down with loot. Step through and they follow. Give the mage a **marked
  recall rune** to set where their gates go; otherwise it's the nearest town moongate.
- **Catch up**: if they lose you (teleporters, boats, a fast horse) they catch up shortly.
- **Talk**: idle chatter about the surroundings, reactions in combat, banter between them.
  Each has a personality (stoic, cheerful, gruff or witty).

### Dying
A companion who dies becomes a ghost and follows you. Revive them with bandages
(Healing/Anatomy 80+), let another companion do it, or pay the guildmaster.

### Talking to them
Address one by name (`Aldric, status`) or everyone at once (`all share`, `companions, quiet`).

| Say | Effect |
|---|---|
| `status` / `report` | Party status window: level, experience, health, purse, wages |
| `share` / `spoils` / `loot` | Hand over everything worth handing over now |
| `pack` / `inventory` | Open their backpack (you can also use the context menu) |
| `quiet` / `hush` | Stop idle chatter (they still warn you about danger) |
| `speak freely` / `chatter` | Resume chatter |
| `gate` / `town` | The mage opens a gate to town (or their marked rune) |
| `dismiss` | Send them back to the guild |

All the usual pet commands still work: `all follow me`, `all guard me`, `all kill`, `all stay`, etc.
`Release` (spoken or from the context menu) sends a companion back to the guild instead of freeing them.

Looting follows your rights: they only search corpses of creatures the party killed, never a kill
you have no right to, and never anything that would be a criminal act.
