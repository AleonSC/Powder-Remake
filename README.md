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
| ↓ / S | Shoot the material the player is carrying |

The drag direction matters for **FAN** (blow direction), **LASER** (beam
direction) and **SuperBall** (launch velocity).

## Materials

| Material | Behaviour |
| --- | --- |
| POWDER | Falls and piles up; burns slowly |
| WATER | Flows and levels out; puts out fire; boils to steam |
| FIRE | Rises and flickers out; ignites anything flammable |
| SEED | Falls; sprouts a plant when resting on powder; plants drink water |
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

**PLAYER:** click to drop a stick figure (up to two; the first uses the arrow
keys, the second WASD). It walks up small ledges, swims, and gets thrown
around by wind and explosions. Touching a powder or liquid picks it up (the
head turns that colour) and shooting sprays it out in front. Fire, magma,
acid, lightning, lasers and virus are deadly. ERASE removes a player.

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
    World.cs                        400x300 cellular automaton + air field
    World.Players.cs, Player.cs     the PLAYER stick figures
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
- **Rendering.** The world is written into a 400×300 point-filtered
  `Texture2D` each frame and drawn, with the menu, through IMGUI (`OnGUI`).
  The layout is scaled to fill the window: on wide screens the menu sits in a
  panel beside the play area (1200×600 design size), otherwise it sits
  underneath like the original (800×850). IMGUI needs
  no extra packages and works with both the old Input Manager and the new
  Input System.

## Tests

In Unity: **Window > General > Test Runner > EditMode > Run All**.

Without Unity (needs Mono's `mcs`): `tools/run-sim-tests.sh`.

## Web version

The original browser version lives in `web/` (it does not have PLAYER). Open `web/index.html` directly,
and run its tests with `cd web && npm test`.

## Not yet implemented

WHEEL, FIGHTER, BOX, TEXT, COPY/PASTE, UPLOAD, GRID and BG modes from
the original.
