# Journey Of Adventures

A 2D side-scrolling action-adventure game built in Unity. Fight through a zombie-infested village, delve into a dark cave to mine diamonds, craft powerful weapons, and push deeper into the world.

---

## Gameplay

The game is split into distinct levels, each with its own objective:

- **Village** — Defeat all zombies to survive the attack and move on
- **Cave** — Mine 5 diamonds from breakable rocks, unlock the crafting shrine, and forge the Diamond Sword to escape

Combat is real-time melee using a sword. Weapons have durability and will break after enough uses. A single-slot hotbar lets you equip and swap your current weapon.

---

## Controls

| Action | Input |
|---|---|
| Move | `W A S D` |
| Jump | `Space` |
| Dash | `Ctrl` + direction |
| Attack | `Left Click` |
| Equip / Unequip | `Left Click` on hotbar slot |

- Double-jump is not supported — but coyote time gives a small window to jump after walking off a ledge
- Attacks only damage enemies in the direction you are facing

---

## Features

- **Combat feel** — Hit-stop (0.05× time scale), camera shake, knockback, and combo multipliers on consecutive hits
- **Enemy AI** — Zombies wander, chase, and climb walls; spiders hang from the ceiling and drop when you get close
- **Crafting** — Collect diamonds to unlock a crafting shrine and forge the Diamond Sword
- **Save system** — Save your position, health, and equipped weapon at any time; loads automatically on next launch
- **Weapon durability** — Weapons break after a fixed number of uses; the Diamond Sword is more durable than the starting Rusty Sword
- **Music system** — Ambient and combat tracks switch dynamically based on gameplay state
- **Procedural effects** — Dust on landing, wind streaks on dash, shard bursts when breaking rocks, all generated at runtime

---

## Enemies

### Zombie *(Village)*
Wanders until it detects you within 8 units, then chases and attacks. Can climb walls using multi-height raycasts. Hits harder on consecutive attacks thanks to a combo knockback multiplier.

### Spider *(Cave)*
Hangs motionless from the ceiling until you walk underneath. Drops and pursues on detection. Damages on contact.

---

## Items & Weapons

| Item | Description |
|---|---|
| Rusty Sword | Starting weapon in the Cave. Low damage, limited durability. |
| Diamond Sword | Crafted at the shrine after collecting 5 diamonds. Higher damage and durability. |
| Diamond | Collectible resource found inside cave rocks. Collect 5 to unlock crafting. |

---

## Built With

- **Unity** (2D, Universal Render Pipeline)
- **C#**
- **TextMeshPro** for all in-game UI
- **Unity PlayerPrefs** for save data persistence
- **ScriptableObjects** for item/weapon definitions
