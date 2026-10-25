# Powder Remake

A Unity remake of Dan-Ball's classic **Powder Game**: a falling-sand physics
toy where you paint materials with the mouse and watch them fall, flow, burn
and explode.

## Opening the project

1. Install **Unity 6** (6000.0 LTS) through Unity Hub. Other recent versions
   (2022.3+) should also work; Hub will offer to upgrade the project.
2. In Unity Hub choose **Add > Add project from disk** and pick this folder.
3. Open `Assets/Scenes/Main.unity` and press **Play**.

Builds start in full screen at the monitor's resolution. In the editor, use
the Game view's **Play Maximized** option (or press F11 while playing) to fill
the screen.

The game also starts itself in any scene (a `RuntimeInitializeOnLoadMethod`
bootstrap creates the `PowderGameApp` object if none exists), so pressing Play
in an empty scene works too. To make a standalone build use
**File > Build Profiles** (or **Build Settings**); `Main.unity` is already in the
scene list.

## Controls

| Input | Action |
| --- | --- |
| Left / right click a menu item | Assign it to the left / right mouse button (red / blue marker) |
| Drag on the play area | Paint with that button's material or tool |
| Mouse wheel | Change pen size |
| Space | Pause / resume |
| F11 | Toggle full screen (in the editor: maximize the Game view) |
| ← → / A D | Walk player 1 / player 2 |
| ↑ / W | Jump (swim up in liquids) |
| ↓ / S | Punch, and shoot the material the player is carrying |

The drag direction matters for **FAN** (blow direction), **LASER** (beam
direction) and **SuperBall** (launch velocity).

## Materials

| Material | Behaviour |
| --- | --- |
| POWDER | Falls and piles up; burns slowly |
| WATER | Flows and levels out; puts out fire; boils to steam on anything hot (fire, magma, torches, burning wood) |
| FIRE | Rises and flickers out; ignites anything flammable |
| SEED | Buried in powder (or ash) with water nearby, sprouts a branching tree with leaves; any part of the tree touching water drinks it and grows a new branch, so more water means more tree |
| GUNPOWDER | Explodes in small chain-reacting blasts |
| FAN | Blows air in the direction you drew it |
| ICE | Slowly freezes neighbouring water; melts near heat |
| SuperBall | Bounces forever |
| CLONE | Copies the first material that touches it |
| FIREWORKS | Launches a rocket when lit that bursts into coloured sparks |
| OIL | Floats on water; burns long |
| C-4 | Solid; huge explosion when lit |
| STONE | Heavy powder; formed when magma meets water |
| MAGMA | Slow, glowing liquid; ignites things; turns to stone in water |
| VIRUS | Spreads into anything it touches; burns |
| NITRO | Liquid that explodes violently |
| ANT | Walks along surfaces, climbs steps and digs through powder |
| TORCH | Permanent flame source |
| GAS | Drifts around; very flammable |
| SOAPY | Liquid that dissolves oil and makes bubbles in the wind |
| THUNDER | Lightning bolt; electrifies metal and ignites what it hits |
| METAL | Conducts electricity; reflects lasers |
| BOMB | Explodes on contact with anything |
| LASER | Beam that heats, melts and ignites; bounces off metal |
| ACID | Dissolves most things (slowly eats metal) |
| BUBBLE | Floats up and pops |
| SMOKE | Rises and fades; fires give it off as they die |
| SAND | Powder that melts into glass in magma or laser light; lightning fuses it into glass |
| GLASS | Solid that acid can't eat and lasers shine straight through |
| SALT | Dissolves in water into salt water (sinks under fresh water, never freezes, boils back to salt); melts ice and snow |
| SNOW | Light, fluttering powder that drifts in the wind, floats, and melts near heat |
| WOOD | Smoulders from the outside in, throwing off fire and smoke, then crumbles to ash (seeds grow in ash); water puts it out |
| MERCURY | Heaviest liquid (stone floats on it); conducts electricity like metal |
| CLOUD | Hangs in the air and drifts with the wind; soaks up steam and rains it back out (getting darker as it fills), and very wet clouds throw lightning. Dry clouds never rain |
| STEAM | Rises and drifts; clouds absorb it. Steam collecting under the ceiling (or any roof) condenses into new clouds, so boiled water comes back as rain |
| CRYO | Liquid nitrogen: freezes water, puts out fire, turns magma to stone, then boils away |
| VOID | Black hole: swallows anything that touches it and sucks in the air around it |

**PLAYER:** click to drop a stick figure (up to two; the first uses the arrow
keys, the second WASD). It walks up small ledges, swims, and gets thrown
around by wind and explosions. Touching a powder or liquid picks it up (the
head turns that colour) and shooting sprays it out in front. Fire, magma,
acid, lightning, lasers, virus, cryo and void are deadly, and ten hits from
fighters knock a player out. ERASE removes a player.

**FIGHTER:** click (or hold) to drop AI stick figures, up to 64. They chase
the nearest player, jumping over walls, and kick them; with no player nearby
they wander and brawl with each other. Three hits knock a fighter out, and
the same hazards as players kill them.

**Tools:** WIND (push air the way you drag), AIR (raise pressure, blowing
things away), DRAG (move particles), BLOCK (wall), ERASE, CLEAR.

**Settings:** VIEW toggles the air view (red = high pressure, blue = low,
green = wind speed). SIDE opens the screen edges so particles fall out of the
world. PEN-S, SPEED, START/STOP, SAVE/LOAD (a file in
`Application.persistentDataPath`) and RESET do what they say.

## Project layout

```
Assets/
  Scenes/Main.unity                 camera + Powder Game object
  Scripts/Simulation/               engine-independent simulation (no UnityEngine)
    Elements.cs                     material definitions and lookup tables
    World.cs                        cellular automaton + air field
    World.Materials.cs              sand, salt, wood, mercury, cryo, void...
    World.Weather.cs                boiling, steam, clouds and rain
    World.Plants.cs                 seeds and trees
    World.Players.cs, Player.cs     PLAYER stick figures (movement shared with fighters)
    World.Fighters.cs               FIGHTER AI and melee combat
  Scripts/Game/PowderGameApp.cs     rendering, IMGUI menu, input, main loop
  Tests/EditMode/WorldTests.cs      NUnit physics tests
tools/run-sim-tests.sh              runs the tests with Mono, without Unity
web/                                the original HTML5/JavaScript version
```

- **Simulation.** Each step scans the grid bottom-up in a random horizontal
  direction. Powders fall and slide, liquids also spread sideways, gases
  drift up, and heavier movable particles swap places with lighter ones. A
  coarse 100×75 air grid (pressure + velocity, smoothed and advected every
  step) carries wind from fans, explosions and the WIND tool, and nudges
  light particles around. The simulation runs at a fixed 60 steps per second.
- **Screen shape.** The sandbox takes the shape of the window: at least the
  original 400×300 dots, growing wider (or taller) to fill the screen, e.g.
  752×300 on a 16:9 display, up to 1024×640. Resizing the window rebuilds
  the world at the new size, keeping its contents anchored to the floor and
  centred (anything that no longer fits is cropped). Saves from a different
  size load the same way.
- **Rendering.** The world is written into a point-filtered `Texture2D` each
  frame and drawn, with the menu underneath, through IMGUI (`OnGUI`), scaled
  to fill the window. IMGUI needs
  no extra packages and works with both the old Input Manager and the new
  Input System.

## Tests

In Unity: **Window > General > Test Runner > EditMode > Run All**.

Without Unity (needs Mono's `mcs`): `tools/run-sim-tests.sh`.

## Web version

The original browser version lives in `web/` (it does not have PLAYER, FIGHTER
or the materials from SAND onwards). Open `web/index.html` directly,
and run its tests with `cd web && npm test`.

## Not yet implemented

WHEEL, BOX, TEXT, COPY/PASTE, UPLOAD, GRID and BG modes from
the original.
