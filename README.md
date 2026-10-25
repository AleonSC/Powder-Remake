# Powder Remake

A browser remake of Dan-Ball's classic **Powder Game**: a falling-sand physics
toy where you paint materials with the mouse and watch them fall, flow, burn
and explode.

No build step or dependencies. Open `index.html` in a browser, or serve the
folder (`npm start`, then visit http://localhost:8000).

## Controls

| Input | Action |
| --- | --- |
| Left / right click a menu item | Assign it to the left / right mouse button (red / blue marker) |
| Drag on the canvas | Paint with that button's material or tool |
| Mouse wheel | Change pen size |
| Space | Pause / resume |

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

### Tools

- **WIND** – push air in the direction you drag
- **AIR** – raise air pressure under the cursor (blows things away)
- **DRAG** – move particles with the cursor
- **BLOCK** – indestructible wall
- **ERASE** / **CLEAR** – remove particles under the cursor / everywhere

### Settings

**VIEW** toggles the air view (red = high pressure, blue = low, green = wind
speed). **SIDE** makes the screen edges open so particles fall out of the
world. **PEN-S**, **SPEED**, **START/STOP**, **SAVE/LOAD** (browser local
storage) and **RESET** do what they say.

## How it works

- `js/elements.js` – material definitions (density, flammability, wind
  response, lifetime…) compiled into typed-array lookup tables.
- `js/sim.js` – the 400×300 cellular automaton. Each frame scans bottom-up in
  a random horizontal direction; powders fall and slide, liquids also spread
  sideways, gases drift up, and heavier movable things swap places with
  lighter ones. A coarse 100×75 air grid (pressure + velocity, smoothed and
  advected each frame) carries wind from fans, explosions and the WIND tool,
  and nudges light particles around.
- `js/main.js` – menu, mouse input, rendering to a pixel buffer, main loop.

Run the headless physics checks with `npm test` (Node 18+).

## Not yet implemented

WHEEL, FIGHTER, BOX, PLAYER, TEXT, COPY/PASTE, UPLOAD, GRID and BG modes from
the original.
