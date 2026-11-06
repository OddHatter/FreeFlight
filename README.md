# FreeFlight

Fly from place to place. Press **H** (configurable) to toggle flight, or on a controller press **both thumbsticks in together** (L3 + R3).

- **Controls:** steer with normal movement. Jump (Space) climbs, Crouch (Ctrl) descends, and Run (Shift) flies faster. On a controller: A climbs, holding the crouch stick-click descends, and run is faster.
- **Free:** flying costs no stamina and works while overweight, so you can carry ore and metal.
- **Safe landing:** no fall damage when you turn flight off in the air.
- **Still a normal player:** this isn't the debug fly cheat. You can be hit, your stats work, and enemies drop normal loot.
- **Harpoon towing:** harpoon an enemy, then fly. It's dragged through the air on the line below you. Release it the vanilla way, by attacking or blocking. Bosses can't be harpooned, as in vanilla.
- **Client-side:** only you need it, and it works on multiplayer servers. When you harpoon an enemy, your game takes control of it so the tow works even if another player's game was running it.
- **Live config reload.**

Flight turns off automatically when you die, sit, or take a ship's controls.

## Config

The file is `BepInEx/config/com.oddhatter.freeflight.cfg`.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. |
| `ToggleKey` | `H` | Key that toggles flight. |
| `GamepadToggle` | `true` | Controller: L3 + R3 toggles flight. |
| `FlySpeed` | `20` | Speed in m/s (normal running is about 7). |
| `SprintMultiplier` | `2.5` | Speed multiplier while holding Run. |
| `DragHarpooned` | `true` | Tow harpooned enemies while flying. |
| `RopeLength` | `5` | How far away a towed enemy hangs, in meters. |

## Install

In r2modman, go to Settings → Profile → Import local mod, pick `Oddhatter-FreeFlight-<version>.zip`, and set the author to `Oddhatter`.
