# Journey Of Adventures

A 2D side-scrolling action-adventure built in Unity. Fight through a zombie-infested village, delve into a dark cave, push through a jungle temple, unlock a desert pyramid, and defeat the Kraken in a storm-tossed ocean to restore balance to the world.

---

## Gameplay

The game is a linear journey across five levels, each with its own objective:

- **Village** — Defeat all zombies to survive the attack and save the villagers
- **Cave** — Mine 5 diamonds from breakable rocks, unlock the crafting shrine, and forge the Diamond Sword
- **Jungle Temple** — Fight through monkeys and vine snakes, defeat the Jungle Guardian mini-boss, and read the temple inscription
- **Desert Pyramid** — Activate 3 ancient Obelisks (defeat the great Sandworm, cross a platform puzzle, survive an enemy wave), then claim the Magical Armor and Bow
- **The Ocean** — Battle the three-phase Kraken final boss, then watch balance return to the world

Combat is real-time: melee with a sword (`Left Click`) and ranged with the bow (`Right Click`, once obtained). The journey ends with an animated credits sequence.

---

## Controls

| Action | Input |
|---|---|
| Move | `W A S D` |
| Jump | `Space` |
| Dash | `Ctrl` + direction |
| Melee attack | `Left Click` |
| Bow / shoot arrow | `Right Click` *(after the Desert pyramid)* |
| Interact / activate | `E` |
| Equip / Unequip | `Left Click` on hotbar slot |

- Double-jump is not supported — but coyote time gives a small window to jump after walking off a ledge
- Melee attacks only damage enemies on the side you are facing
- Arrows fly toward the mouse cursor

---

## Features

- **Combat feel** — Hit-stop, camera shake, knockback, combo multipliers, floating hit markers
- **Five full levels** with distinct enemies, music and atmosphere
- **Bosses** — the two-phase Jungle Guardian mini-boss and the three-phase Kraken final boss, each with a screen boss-health bar
- **Ranged combat** — a Bow that fires arrows, required to destroy the Kraken's tentacles
- **Magical Armor** — absorbs one attack every 10 seconds (cooldown-based shield with a visible aura)
- **Obelisk puzzles** — three trials gate the desert pyramid
- **Moving & falling platforms** — platforming hazards over the ocean
- **Crafting** — collect diamonds to forge the Diamond Sword
- **Save system** — checkpoints at every level; "Continue" resumes from the main menu
- **Dynamic music** — ambient and combat tracks switch with gameplay state
- **Code-driven sprite animation** — every enemy and boss animates per state (idle / move / attack / hurt)
- **Cutscenes & dialogue** — intro, boss intros, the temple inscription, and the village-reunion finale

---

## Enemies & Bosses

| Enemy | Level | HP | Behaviour |
|---|---|---|---|
| Zombie | Village | 40 | Wanders, chases, climbs walls |
| Spider | Cave | 30 | Drops from the ceiling, pursues |
| Vine Snake | Jungle | 40 | Very fast ground chaser, lunging bite |
| Monkey | Jungle | 50 | Hops to keep distance, throws coconuts |
| Jungle Guardian | Jungle | 200 | Mini-boss — ground slam, jump attack, enraged phase 2 |
| Sandworm | Desert | 150 / 55 | Crawls and lunges; guards Obelisk 1 / forms the wave |
| Kraken | Ocean | 1000 | Final boss — 3 phases: tentacles, energy waves, exposed heart |

---

## Items & Weapons

| Item | Description |
|---|---|
| Rusty Sword | Starting weapon. Low damage, breaks after 20 hits. |
| Diamond Sword | Crafted in the Cave. 35 damage, unbreakable. |
| Bow & Arrow | Found in the Desert pyramid. Ranged attack, ~25 damage. |
| Magical Armor | Found in the Desert pyramid. Blocks one hit every 10 seconds. |
| Diamond | Cave resource — collect 5 to unlock crafting. |

---

## Built With

- **Unity** (2D, Universal Render Pipeline)
- **C#**
- **TextMeshPro** for all in-game UI
- **Unity PlayerPrefs** for save data persistence
- **ScriptableObjects** for item/weapon definitions

## Use 🎮 FULLSTÄNDIGT SPELMANUS & LEVELDOKUMENT (Swedish) to understand how the game should work and feel.
