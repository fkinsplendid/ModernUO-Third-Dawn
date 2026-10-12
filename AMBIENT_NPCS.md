# Ambient NPCs — Setup Guide

## Peasant Ambient Speech

Peasants will randomly say things to nearby players. No setup required — just spawn them.

- Speech fires every ~30 seconds if a player is within 8 tiles
- Silent if no players are nearby, or if being escorted
- Speech persists across server restarts

---

## Placing NPC Spawners In-Game

Log in as GM and run:
```
[Add Spawner
```
This places a spawner at your feet. Target it and use `[Props` to configure:

| Property    | Description                                  | Example        |
|-------------|----------------------------------------------|----------------|
| `Entries`   | NPC type(s) to spawn                         | `Peasant`      |
| `Count`     | Max alive at once from this spawner          | `3`            |
| `HomeRange` | Wander radius in tiles (0 = stationary)      | `8`            |
| `MinDelay`  | Minimum respawn time                         | `00:05:00`     |
| `MaxDelay`  | Maximum respawn time                         | `00:10:00`     |
| `WayPoint`  | First waypoint of a patrol route (optional)  | target a WayPoint |

Use `[Go <town>` to teleport between locations, e.g. `[Go Britain`.

---

## Patrol Routes (Waypoint Loops)

### 1. Place waypoints

```
[WayPointSeq
```

Click each location in order to lay down a chain of waypoints. When you reach the last one, **target the first waypoint you placed** to close the loop.

### 2. Assign to a spawner

Target the spawner → `[Props` → set `WayPoint` to the first waypoint in the chain.

Spawned NPCs will walk the loop indefinitely.

---

## Dwell Time (Optional Pause at a Waypoint)

To make an NPC linger at a specific point (e.g. a market stall, a doorway):

1. Single-click the waypoint to select it
2. `[Props` → set `DwellSeconds` to the desired pause in seconds

The NPC will stand still at that waypoint for that duration before moving on.
Single-clicking a waypoint shows its status: `(Linked: (X Y Z), Dwell: 10s)`

`DwellSeconds` defaults to `0` (no pause). Existing waypoints are unaffected.

---

## Available Human NPC Types

| Type               | Description                        |
|--------------------|------------------------------------|
| `Peasant`          | Common townsfolk, ambient speech   |
| `Noble`            | Well-dressed, carries a sword      |
| `Merchant`         | Trader type                        |
| `Gypsy`            | Wanderer                           |
| `Artist`           | Creative type                      |
| `Actor`            | Performer                          |
| `Sculptor`         | Artisan                            |
| `Messenger`        | Runner/courier type                |
| `Banker`           | Financial type                     |
| `HarborMaster`     | Dockside NPC                       |

All of the above support patrol routes and dwell time.

---

## Backup Quest Spawners

The original quest NPC spawner `.cfg` files have been removed from the decoration system
and backed up to:

```
Distribution/Data/Decoration/_backup_quest_spawners/
```

To restore, move the files back to their original subfolder under `Distribution/Data/Decoration/`
and run `[Decorate`.

Quick checklist:

  1. Single-click all 3 waypoints — point 3 should show (Linked: <coords of point 1>)
  2. If unlinked — double-click point 3 and target point 1 to close the loop
  3. Kill/respawn the existing NPCs so they pick up the waypoint assignment
  4. HomeRange of 50 is fine and won't interfere — waypoint following takes priority over random home wandering in the AI

  Increase NPC speeds:

  Option 1: Per-NPC via [Props] (live, in-game)

  Target the NPC → [Props] → set PassiveSpeed (patrol/wander speed) or ActiveSpeed (combat speed). These are double
  values in seconds between steps.

  Current defaults:

  ┌──────────────────┬──────────────┬─────────────┐
  │      Level       │ PassiveSpeed │ ActiveSpeed │
  ├──────────────────┼──────────────┼─────────────┤
  │ VerySlow         │ 0.8          │ 0.4         │
  ├──────────────────┼──────────────┼─────────────┤
  │ Slow             │ 0.6          │ 0.3         │
  ├──────────────────┼──────────────┼─────────────┤
  │ Medium (default) │ 0.5          │ 0.25        │
  ├──────────────────┼──────────────┼─────────────┤
  │ Fast             │ 0.4          │ 0.2         │
  ├──────────────────┼──────────────┼─────────────┤
  │ VeryFast         │ 0.30         │ 0.125       │
  └──────────────────┴──────────────┴─────────────┘

  Note: this only affects already-spawned NPCs. Freshly spawned ones will revert to their type's default from the JSON.

  ---
  Option 2: Edit npc-speeds.json (persistent)

  Distribution/Data/npc-speeds.json — add your ambient NPC types to a faster speed level, or tune the passive value of
  the Medium level directly. For example, to add Peasant to Fast:

  {
    "level": "Fast",
    "active": 0.2,
    "passive": 0.4,
    "types": [
      "Peasant",
      ...
    ]
  }

  Or if you want a custom speed (e.g. slightly faster than Medium but not quite Fast), you can edit the passive value of
  the Medium entry — just be aware that affects all Medium-speed NPCs.

  Requires a server restart to take effect. Newly spawned NPCs will pick it up automatically from then on.

