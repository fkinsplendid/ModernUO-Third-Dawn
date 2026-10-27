# Shard Commands — Admin Reference

Commands added for this Third Dawn shard, on top of the standard ModernUO ones. Type them
in game with the `[` prefix. The level shown is the lowest staff level that can use them.

## Everyday

| Command | Level | What it does |
|---|---|---|
| `[TownLife` | GameMaster | Living towns status: street map, places found, time of day, residents and how many are at home |
| `[TownLife who` | GameMaster | Where every resident is and what they're doing |
| `[TownLife places` | GameMaster | Every place each town found (homes, taverns, banks…), with coordinates |
| `[TownLife populate` | GameMaster | Fill empty trades now instead of waiting the usual 30 minutes |
| `[TownLife clear` | GameMaster | Remove every resident (`populate` brings a new cast) |
| `[FullSpellbook` | Administrator | Puts a spellbook with all 64 spells in your backpack |
| `[Toolbar` | Counselor | Reopens the staff toolbar (it also opens on login for staff) |
| `[PowerHour` | Player | Shows your power hour: double skill gain for one hour, once a day, starting at login |

## One-off world cleanup

These change the saved world. Each shows a confirmation window listing what it will delete
first, and logs what it did. Run `[save` afterwards.

| Command | Level | What it does |
|---|---|---|
| `[RemoveAnachronisms` | Administrator | Deletes creatures from after Third Dawn (Mondain's Legacy, Samurai Empire, Age of Shadows, Solen, Jwilson). It deletes spawners that only held them and strips them from mixed spawners. Players and pets are left alone. LBR creatures are kept on purpose. |
| `[RemoveQuestGivers` | Administrator | Deletes every quest giver, escort NPC and quest item in the world and strips them from spawners. Items in containers or houses are left alone. |
| `[RemoveUnreachableSpawners` | Administrator | Deletes every spawner in Felucca and in Trammel's east strip (x 6144 and up, Mondain's Legacy land no player can reach), along with the creatures and NPCs there. Players, pets, companions, player vendors and anything in a house are left alone. |
| `[ThirdDawnMapCleanup` | Administrator | After switching to the Third Dawn map: removes decoration, spawners, teleporters and NPCs on ground that changed or no longer exists, and moves players and pets standing there to Britain. Houses on changed ground are only reported. Needs `Data/third-dawn-map.json`. |

All four are safe to run again: when there's nothing left to do, they say so.

### Rebuilding a world on the Third Dawn map

From a world that still has the old map's content, in this order:

```
[ThirdDawnMapCleanup
[Decorate
[SignGen
[TelGen
[DoorGen
[GenerateSpawners Data/Spawns/shared/trammel/Ocllo.json
[RemoveQuestGivers
[RemoveAnachronisms
[RemoveUnreachableSpawners
[save
```

`[DoorGen` is what restores Ocllo's doors.

## Placing shard NPCs

| To place | Command |
|---|---|
| Companion guildmaster (hires warriors, bards and mages) | `[Add CompanionGuildmaster` |
| Master Blacksmith (repairs gear for a fee; companions bring worn gear to him) | `[Add MasterBlacksmith`, or `[GenerateSpawners Data/Spawns/shared/trammel/MasterBlacksmith.json` |

## More detail

- **Companions** — hiring, wages, settings and everything players can say to them:
  [COMPANIONS.md](COMPANIONS.md)
- **Town Life** — how living towns work and their settings: [TOWN_LIFE.md](TOWN_LIFE.md)
- **Ambient NPCs** — patrols and waypoints: [AMBIENT_NPCS.md](AMBIENT_NPCS.md)

## Shard rules that aren't commands

- **Trammel only.** Moongates never list Felucca. Jail releases and the help menu's "I'm stuck"
  option always send players to Trammel. Runes marked in Felucca before this change still work.
- **Hidden magic.** Magic weapon, armour and shield bonuses work but never show, as in Third Dawn.
- **Veteran rewards.** Monster statuettes and special dye tubs, one per year of account age
  *and* per 100 hours played (whichever gives fewer). The skill cap stays at 700.
- Settings live in `Distribution/Configuration/modernuo.json` (`companions.*`, `townLife.*`,
  `vetRewards.*`). That file isn't tracked by git, so change it on the live server too.
