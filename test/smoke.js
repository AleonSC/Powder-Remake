// Headless sanity checks for the simulation: node test/smoke.js
'use strict';
const assert = require('assert');
const Sim = require('../js/sim.js');
const { E, DEFS } = require('../js/elements.js');
const { W, H } = Sim;

function count(sim, t) {
  let n = 0;
  for (let i = 0; i < sim.type.length; i++) if (sim.type[i] === t) n++;
  return n;
}
function run(sim, n) { for (let k = 0; k < n; k++) sim.step(); }

const tests = {
  'powder falls and piles on the floor'() {
    const sim = new Sim();
    sim.brush(200, 50, 5, E.POWDER, 0, 1);
    const n = count(sim, E.POWDER);
    run(sim, 400);
    assert.strictEqual(count(sim, E.POWDER), n, 'powder is conserved');
    let low = 0;
    for (let x = 0; x < W; x++) if (sim.type[(H - 1) * W + x] === E.POWDER) low++;
    assert.ok(low > 5, 'powder reached the floor');
  },

  'water spreads into a flat pool'() {
    const sim = new Sim();
    for (let k = 0; k < 20; k++) sim.brush(200, 40 + k, 8, E.WATER, 0, 1);
    const n = count(sim, E.WATER);
    run(sim, 1500);
    assert.strictEqual(count(sim, E.WATER), n, 'water is conserved');
    let maxY = H, minY = H;
    for (let i = 0; i < sim.type.length; i++) {
      if (sim.type[i] === E.WATER) minY = Math.min(minY, (i / W) | 0);
    }
    assert.ok(H - minY <= Math.ceil(n / W) + 3, `pool is flat (height ${H - minY} for ${n} drops)`);
  },

  'oil floats on water'() {
    const sim = new Sim();
    for (let k = 0; k < 10; k++) sim.brush(200, 200 + k, 10, E.OIL, 0, 1);
    for (let k = 0; k < 10; k++) sim.brush(200, 100 + k, 10, E.WATER, 0, 1);
    run(sim, 1500);
    let oilY = 0, waterY = 0, no = 0, nw = 0;
    for (let i = 0; i < sim.type.length; i++) {
      if (sim.type[i] === E.OIL) { oilY += (i / W) | 0; no++; }
      if (sim.type[i] === E.WATER) { waterY += (i / W) | 0; nw++; }
    }
    assert.ok(oilY / no < waterY / nw, 'oil sits above water on average');
  },

  'fire burns oil away'() {
    const sim = new Sim();
    for (let x = 150; x < 250; x++) for (let y = 280; y < H; y++) sim.set(y * W + x, E.OIL);
    run(sim, 300);
    let top = 0;
    while (sim.type[top * W + 200] !== E.OIL) top++;
    for (let x = 195; x < 205; x++) sim.set((top - 1) * W + x, E.FIRE);
    run(sim, 1500);
    assert.ok(count(sim, E.OIL) < 500, 'most oil burned');
  },

  'gunpowder chain-reacts'() {
    const sim = new Sim();
    for (let x = 100; x < 300; x++) for (let y = 270; y < H; y++) sim.set(y * W + x, E.GUNPOWDER);
    sim.set(269 * W + 200, E.FIRE);
    run(sim, 300);
    assert.ok(count(sim, E.GUNPOWDER) < 300, `gunpowder exploded (${count(sim, E.GUNPOWDER)} left)`);
  },

  'bomb explodes on impact'() {
    const sim = new Sim();
    sim.brush(200, 100, 2, E.BOMB, 0, 1);
    for (let x = 0; x < W; x++) sim.set(250 * W + x, E.WALL);
    run(sim, 200);
    assert.strictEqual(count(sim, E.BOMB), 0);
  },

  'magma meets water makes stone'() {
    const sim = new Sim();
    for (let x = 150; x < 250; x++) for (let y = 280; y < H; y++) sim.set(y * W + x, E.WATER);
    sim.brush(200, 200, 6, E.MAGMA, 0, 1);
    run(sim, 600);
    assert.ok(count(sim, E.STONE) > 0);
  },

  'seed grows on powder'() {
    const sim = new Sim();
    for (let x = 0; x < W; x++) for (let y = 290; y < H; y++) sim.set(y * W + x, E.POWDER);
    for (let x = 100; x < 300; x += 20) sim.set(200 * W + x, E.SEED);
    run(sim, 800);
    assert.ok(count(sim, E.PLANT) > 50, `plants grew (${count(sim, E.PLANT)})`);
  },

  'fan blows powder sideways'() {
    const sim = new Sim();
    for (let y = 150; y < 200; y++) for (let x = 40; x < 44; x++) { sim.set(y * W + x, E.FAN); sim.vx[y * W + x] = 1; }
    for (let x = 0; x < 30; x++) for (let y = 280; y < H; y++) sim.set(y * W + x, E.WALL);
    run(sim, 120);
    sim.brush(60, 170, 6, E.POWDER, 0, 1);
    run(sim, 120);
    let sx = 0, n = 0;
    for (let i = 0; i < sim.type.length; i++) if (sim.type[i] === E.POWDER) { sx += i % W; n++; }
    assert.ok(sx / n > 90, `powder drifted right (mean x ${(sx / n).toFixed(1)})`);
  },

  'every element survives a stress run'() {
    const sim = new Sim();
    const placeable = DEFS.filter((d) => d.id > 0).map((d) => d.id);
    placeable.forEach((t, k) => {
      const x = 20 + (k % 12) * 30, y = 40 + Math.floor(k / 12) * 60;
      for (let n = 0; n < 30; n++) sim.brush(x, y, 6, t, 1, 0);
    });
    sim.sidesOpen = true;
    run(sim, 500);
    sim.sidesOpen = false;
    run(sim, 500);
    for (const a of [sim.avx, sim.avy, sim.ap]) for (const v of a) assert.ok(Number.isFinite(v), 'air stays finite');
  },

  'save and load round-trips'() {
    const sim = new Sim();
    sim.brush(100, 100, 10, E.WALL, 0, 1);
    sim.brush(200, 100, 3, E.FAN, 1, 0);
    const before = Array.from(sim.type);
    const data = sim.serialize();
    const other = new Sim();
    other.deserialize(data);
    assert.deepStrictEqual(Array.from(other.type), before);
    assert.strictEqual(other.vx[100 * W + 200], 1);
  },
};

let failed = 0;
for (const [name, fn] of Object.entries(tests)) {
  const t0 = Date.now();
  try { fn(); console.log(`ok   ${name} (${Date.now() - t0}ms)`); }
  catch (err) { failed++; console.log(`FAIL ${name}\n     ${err.message}`); }
}
process.exit(failed ? 1 : 0);
