// Material definitions and the lookup tables the simulation runs on.
(function (root) {
  'use strict';

  // How a material moves.
  const S = { NONE: 0, STATIC: 1, POWDER: 2, LIQUID: 3, GAS: 4, SPECIAL: 5 };

  // Every material the simulation knows about, in id order. Entries with a
  // `label` can be picked from the menu; the rest only appear via reactions.
  //   dens    heavier things sink through lighter movable things
  //   flam    chance per frame that touching fire ignites it
  //   air     how strongly wind pushes it around
  //   disp    how far a liquid spreads sideways per frame
  //   explode blast radius when ignited (power = blast strength)
  //   life    [base, random] initial lifetime in frames (0 = forever)
  //   blocks  stops air flow (solid walls)
  //   sparse  painted one particle at a time instead of filling the brush
  const DEFS = [
    { key: 'EMPTY', state: S.NONE, color: 0x000000 },
    { key: 'WALL', label: 'BLOCK', state: S.STATIC, color: 0x808080, blocks: true },
    { key: 'POWDER', label: 'POWDER', state: S.POWDER, color: 0xdcb486, dens: 3, flam: 0.06, air: 0.7 },
    { key: 'WATER', label: 'WATER', state: S.LIQUID, color: 0x3c50ff, dens: 2, air: 0.5, disp: 5 },
    { key: 'FIRE', label: 'FIRE', state: S.GAS, color: 0xff5a1e, dens: 0.05, air: 1.2, life: [20, 30] },
    { key: 'SEED', label: 'SEED', state: S.POWDER, color: 0xa0c878, dens: 3, flam: 0.1, air: 0.6 },
    { key: 'GUNPOWDER', label: 'GUNPOWDER', state: S.POWDER, color: 0xc0906a, dens: 3, flam: 0.9, air: 0.6, explode: 4, power: 1.5 },
    { key: 'FAN', label: 'FAN', state: S.STATIC, color: 0x6e7cff },
    { key: 'ICE', label: 'ICE', state: S.STATIC, color: 0xc8f0ff, blocks: true },
    { key: 'SUPERBALL', label: 'SuperBall', state: S.SPECIAL, color: 0xff64ff, dens: 5, sparse: 0.03 },
    { key: 'CLONE', label: 'CLONE', state: S.STATIC, color: 0xc8a032, blocks: true },
    { key: 'FIREWORKS', label: 'FIREWORKS', state: S.POWDER, color: 0xff8c50, dens: 3, flam: 0.3, air: 0.6 },
    { key: 'OIL', label: 'OIL', state: S.LIQUID, color: 0x7a3c14, dens: 1.5, flam: 0.3, air: 0.5, disp: 3 },
    { key: 'C4', label: 'C-4', state: S.STATIC, color: 0xfff0c8, flam: 0.5, explode: 16, power: 8, blocks: true },
    { key: 'STONE', label: 'STONE', state: S.POWDER, color: 0x8c8c8c, dens: 5, air: 0.2 },
    { key: 'MAGMA', label: 'MAGMA', state: S.LIQUID, color: 0xff6428, dens: 4, air: 0.2, disp: 2 },
    { key: 'VIRUS', label: 'VIRUS', state: S.STATIC, color: 0xa028c8, flam: 0.2 },
    { key: 'NITRO', label: 'NITRO', state: S.LIQUID, color: 0x40b040, dens: 2.1, flam: 0.9, air: 0.5, disp: 4, explode: 10, power: 5 },
    { key: 'ANT', label: 'ANT', state: S.SPECIAL, color: 0xe0b4b4, dens: 3, flam: 0.3, sparse: 0.1 },
    { key: 'TORCH', label: 'TORCH', state: S.STATIC, color: 0xff8030, blocks: true },
    { key: 'GAS', label: 'GAS', state: S.GAS, color: 0xb48cb4, dens: 0.1, flam: 0.6, air: 1 },
    { key: 'SOAPY', label: 'SOAPY', state: S.LIQUID, color: 0xdcdcff, dens: 1.8, air: 0.5, disp: 4 },
    { key: 'THUNDER', label: 'THUNDER', state: S.SPECIAL, color: 0xffff80, life: [100, 0], sparse: 1 },
    { key: 'METAL', label: 'METAL', state: S.STATIC, color: 0x7888a8, blocks: true },
    { key: 'BOMB', label: 'BOMB', state: S.POWDER, color: 0x989848, dens: 3, air: 0.4, explode: 12, power: 6 },
    { key: 'LASER', label: 'LASER', state: S.SPECIAL, color: 0xff3c3c, life: [400, 0], sparse: 1 },
    { key: 'ACID', label: 'ACID', state: S.LIQUID, color: 0xb4ff3c, dens: 2.05, air: 0.5, disp: 4 },
    { key: 'BUBBLE', label: 'BUBBLE', state: S.GAS, color: 0xb4dcff, dens: 0.04, air: 1.2, life: [300, 600], sparse: 0.05 },
    { key: 'PLANT', state: S.STATIC, color: 0x3cb43c, flam: 0.08, blocks: true },
    { key: 'STEAM', state: S.GAS, color: 0xb4c3d2, dens: 0.08, air: 1, life: [200, 300] },
    { key: 'SPARK', state: S.SPECIAL, color: 0xffffff, life: [20, 25] },
    { key: 'ROCKET', state: S.SPECIAL, color: 0xffe0a0, life: [20, 25] },
    { key: 'BOLT', state: S.GAS, color: 0xffffc0, dens: 0, life: [3, 3] },
  ];

  const n = 256;
  const E = {};
  const STATE = new Uint8Array(n);
  const DENS = new Float32Array(n);
  const FLAM = new Float32Array(n);
  const AIRF = new Float32Array(n);
  const DISP = new Uint8Array(n);
  const EXPLODE = new Uint8Array(n);
  const POWER = new Float32Array(n);
  const LIFE0 = new Int16Array(n);
  const LIFER = new Int16Array(n);
  const BLOCKS = new Uint8Array(n);
  const MOVABLE = new Uint8Array(n);
  const SPARSE = new Float32Array(n);
  const COLOR = new Uint32Array(n);

  DEFS.forEach((d, id) => {
    d.id = id;
    E[d.key] = id;
    STATE[id] = d.state;
    DENS[id] = d.dens || 0;
    FLAM[id] = d.flam || 0;
    AIRF[id] = d.air || 0;
    DISP[id] = d.disp || 0;
    EXPLODE[id] = d.explode || 0;
    POWER[id] = d.power || 0;
    LIFE0[id] = d.life ? d.life[0] : 0;
    LIFER[id] = d.life ? d.life[1] : 0;
    BLOCKS[id] = d.blocks ? 1 : 0;
    MOVABLE[id] = d.state === S.POWDER || d.state === S.LIQUID || d.state === S.GAS ? 1 : 0;
    SPARSE[id] = d.sparse || 0;
    COLOR[id] = d.color;
  });

  const api = { S, E, DEFS, STATE, DENS, FLAM, AIRF, DISP, EXPLODE, POWER, LIFE0, LIFER, BLOCKS, MOVABLE, SPARSE, COLOR };

  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else (root.PG = root.PG || {}).elements = api;
})(this);
