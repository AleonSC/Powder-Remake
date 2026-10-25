// Menu, input handling, rendering and the main loop.
(function () {
  'use strict';

  const { elements: EL, Sim } = window.PG;
  const { E, DEFS, COLOR } = EL;
  const { W, H, AW } = Sim;

  const canvas = document.getElementById('screen');
  const ctx = canvas.getContext('2d');
  const img = ctx.createImageData(W, H);
  const buf = new Uint32Array(img.data.buffer);
  const sim = new Sim();

  const PEN_SIZES = [0, 1, 2, 3, 4, 6, 8, 12, 16];
  const SPEEDS = [1, 2, 0.5];
  const TOOLS = {
    WIND: { label: 'WIND', color: 0x6688ff },
    AIR: { label: 'AIR', color: 0xc0c0ff },
    DRAG: { label: 'DRAG', color: 0xe0e0e0 },
    ERASE: { label: 'ERASE', color: 0xa0a0a0 },
  };

  const state = {
    left: 'POWDER',
    right: 'ERASE',
    pen: 3,
    speed: 0,
    running: true,
    airView: false,
  };

  // ------------------------------------------------------------------ menu

  // Mirrors the original layout: elements, tools, then controls.
  const MENU = [
    ['POWDER', 'FIREWORKS', 'GAS', 'WIND', 'WALL', 'SAVE'],
    ['WATER', 'OIL', 'SOAPY', 'AIR', 'ERASE', 'LOAD'],
    ['FIRE', 'C4', 'THUNDER', 'DRAG', 'CLEAR', ''],
    ['SEED', 'STONE', 'METAL', 'BUBBLE', '', ''],
    ['GUNPOWDER', 'MAGMA', 'BOMB', '', '#VIEW', '#SIDE'],
    ['FAN', 'VIRUS', 'LASER', '', '#PEN', ''],
    ['ICE', 'NITRO', 'ACID', '', '#SPEED', ''],
    ['SUPERBALL', 'ANT', '', '', '#START', ''],
    ['CLONE', 'TORCH', '', '', '', '#RESET'],
  ];

  const hex = (c) => '#' + c.toString(16).padStart(6, '0');
  const brighten = (c) => {
    const ch = (s) => Math.min(255, Math.round(((c >> s) & 255) * 0.7 + 90));
    return (ch(16) << 16) | (ch(8) << 8) | ch(0);
  };

  const menuEl = document.getElementById('menu');
  const buttons = {};

  function actionLabel(key) {
    switch (key) {
      case 'SAVE': return 'SAVE';
      case 'LOAD': return 'LOAD';
      case 'CLEAR': return 'CLEAR';
      case '#VIEW': return state.airView ? 'VIEW AIR' : 'VIEW DOT';
      case '#SIDE': return sim.sidesOpen ? 'SIDE-ON' : 'SIDE-OFF';
      case '#PEN': return 'PEN-S ' + PEN_SIZES[state.pen];
      case '#SPEED': return 'SPEED' + (SPEEDS[state.speed] < 1 ? '/2' : 'x' + SPEEDS[state.speed]);
      case '#START': return state.running ? 'STOP' : 'START';
      case '#RESET': return 'RESET';
    }
    return key;
  }

  function buildMenu() {
    for (const row of MENU) {
      for (const key of row) {
        const b = document.createElement('button');
        b.type = 'button';
        if (!key) { b.disabled = true; b.className = 'blank'; menuEl.appendChild(b); continue; }
        let color = 0xff9090;
        if (key in E) { b.textContent = DEFS[E[key]].label; color = brighten(COLOR[E[key]]); b.dataset.pick = key; }
        else if (key in TOOLS) { b.textContent = TOOLS[key].label; color = TOOLS[key].color; b.dataset.pick = key; }
        else { b.textContent = actionLabel(key); b.dataset.action = key; }
        b.style.color = hex(color);
        buttons[key] = b;
        menuEl.appendChild(b);
      }
    }
    menuEl.addEventListener('click', (e) => onMenu(e, 'left'));
    menuEl.addEventListener('contextmenu', (e) => { e.preventDefault(); onMenu(e, 'right'); });
    refreshMenu();
  }

  function onMenu(e, side) {
    const b = e.target.closest('button');
    if (!b || b.disabled) return;
    if (b.dataset.pick) state[side] = b.dataset.pick;
    else doAction(b.dataset.action);
    refreshMenu();
  }

  function doAction(key) {
    switch (key) {
      case 'CLEAR': sim.clear(); break;
      case '#RESET': sim.clear(); sim.clearAir(); state.speed = 0; state.running = true; break;
      case '#VIEW': state.airView = !state.airView; break;
      case '#SIDE': sim.sidesOpen = !sim.sidesOpen; break;
      case '#PEN': state.pen = (state.pen + 1) % PEN_SIZES.length; break;
      case '#SPEED': state.speed = (state.speed + 1) % SPEEDS.length; break;
      case '#START': state.running = !state.running; break;
      case 'SAVE':
        try { localStorage.setItem('powder-remake-save', sim.serialize()); flash('SAVED'); }
        catch (err) { flash('SAVE FAILED'); }
        break;
      case 'LOAD':
        try {
          const s = localStorage.getItem('powder-remake-save');
          if (s) { sim.deserialize(s); flash('LOADED'); } else flash('NO SAVE');
        } catch (err) { flash('LOAD FAILED'); }
        break;
    }
  }

  function refreshMenu() {
    for (const [key, b] of Object.entries(buttons)) {
      if (b.dataset.action) b.textContent = actionLabel(key);
      b.classList.toggle('sel-l', state.left === key);
      b.classList.toggle('sel-r', state.right === key);
    }
  }

  let flashText = '', flashUntil = 0;
  function flash(text) { flashText = text; flashUntil = performance.now() + 1200; }

  // ----------------------------------------------------------------- input

  const mouse = { x: -100, y: -100, px: 0, py: 0, button: -1, dirx: 0, diry: 1 };

  function toSim(e) {
    const r = canvas.getBoundingClientRect();
    return [Math.floor(((e.clientX - r.left) / r.width) * W), Math.floor(((e.clientY - r.top) / r.height) * H)];
  }

  canvas.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    canvas.setPointerCapture(e.pointerId);
    [mouse.x, mouse.y] = toSim(e);
    mouse.px = mouse.x; mouse.py = mouse.y;
    mouse.button = e.button === 2 ? 2 : 0;
  });
  canvas.addEventListener('pointermove', (e) => { [mouse.x, mouse.y] = toSim(e); });
  const up = () => { mouse.button = -1; };
  canvas.addEventListener('pointerup', up);
  canvas.addEventListener('pointercancel', up);
  canvas.addEventListener('pointerleave', (e) => { if (e.pointerType === 'mouse' && mouse.button < 0) mouse.x = -100; });
  canvas.addEventListener('contextmenu', (e) => e.preventDefault());
  canvas.addEventListener('wheel', (e) => {
    e.preventDefault();
    state.pen = Math.max(0, Math.min(PEN_SIZES.length - 1, state.pen + (e.deltaY < 0 ? 1 : -1)));
    refreshMenu();
  }, { passive: false });
  window.addEventListener('keydown', (e) => {
    if (e.code === 'Space') { e.preventDefault(); state.running = !state.running; refreshMenu(); }
  });

  // Applies the current tool between the last and current cursor positions.
  function applyInput() {
    const dx = mouse.x - mouse.px, dy = mouse.y - mouse.py;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len > 0.5) { mouse.dirx = dx / len; mouse.diry = dy / len; }
    if (mouse.button >= 0) {
      const tool = mouse.button === 2 ? state.right : state.left;
      const r = PEN_SIZES[state.pen];
      if (tool === 'WIND') sim.wind(mouse.x, mouse.y, r, dx, dy);
      else if (tool === 'AIR') sim.airBlast(mouse.x, mouse.y, r);
      else if (tool === 'DRAG') sim.drag(mouse.px, mouse.py, r, dx, dy);
      else {
        const t = E[tool];
        const lone = tool !== 'ERASE' && EL.SPARSE[t] === 1;
        const steps = lone ? 0 : Math.ceil(len / Math.max(1, r * 0.5));
        for (let k = 0; k <= steps; k++) {
          const x = Math.round(mouse.px + (steps ? (dx * k) / steps : dx));
          const y = Math.round(mouse.py + (steps ? (dy * k) / steps : dy));
          if (tool === 'ERASE') sim.erase(x, y, r);
          else sim.brush(x, y, r, t, mouse.dirx, mouse.diry);
        }
      }
    }
    mouse.px = mouse.x;
    mouse.py = mouse.y;
  }

  // ---------------------------------------------------------------- render

  const abgr = (rgb, k) => {
    const c = (s) => Math.max(0, Math.min(255, Math.round(((rgb >> s) & 255) * k)));
    return ((255 << 24) | (c(0) << 16) | (c(8) << 8) | c(16)) >>> 0;
  };
  const lerp = (a, b, f) => {
    const c = (s) => Math.round(((a >> s) & 255) * (1 - f) + ((b >> s) & 255) * f);
    return (c(16) << 16) | (c(8) << 8) | c(0);
  };

  const LUT = new Uint32Array(256 * 16);
  for (let t = 0; t < DEFS.length; t++) {
    for (let s = 0; s < 16; s++) LUT[t * 16 + s] = abgr(COLOR[t], 0.86 + (s / 15) * 0.28);
  }
  const FIRE_LUT = new Uint32Array(64);
  for (let l = 0; l < 64; l++) {
    const f = l / 63;
    FIRE_LUT[l] = abgr(f < 0.4 ? lerp(0x601000, 0xff4010, f / 0.4) : lerp(0xff4010, 0xffe070, (f - 0.4) / 0.6), 1);
  }
  const HUES = [0xff4848, 0x48ff48, 0x5090ff, 0xffff48, 0xff48ff, 0x48ffff, 0xffa030, 0xffffff];
  const SPARK_LUT = new Uint32Array(8 * 16);
  for (let h = 0; h < 8; h++) for (let l = 0; l < 16; l++) SPARK_LUT[h * 16 + l] = abgr(HUES[h], 0.3 + (l / 15) * 0.7);
  const BG = 0xff000000;
  const airBg = new Uint32Array(Sim.AW * Sim.AH);

  function render() {
    const { type, life, shade } = sim;
    const f = sim.frame;
    if (state.airView) {
      const { ap, avx, avy } = sim;
      for (let i = 0; i < airBg.length; i++) {
        const p = ap[i], v = Math.sqrt(avx[i] * avx[i] + avy[i] * avy[i]);
        const r = Math.min(255, p > 0 ? p * 12 : 0), b = Math.min(255, p < 0 ? -p * 12 : 0);
        const g = Math.min(255, v * 25);
        airBg[i] = ((255 << 24) | (b << 16) | (g << 8) | r) >>> 0;
      }
    }
    for (let y = 0, i = 0; y < H; y++) {
      const arow = (y >> 2) * AW;
      for (let x = 0; x < W; x++, i++) {
        const t = type[i];
        let c;
        switch (t) {
          case 0: c = state.airView ? airBg[arow + (x >> 2)] : BG; break;
          case E.FIRE: c = FIRE_LUT[Math.min(63, life[i] * 2)]; break;
          case E.MAGMA: c = LUT[t * 16 + (((shade[i] + f) >> 2) & 15)]; break;
          case E.METAL: c = life[i] > 0 ? 0xffc0ffff : LUT[t * 16 + (shade[i] >> 4)]; break;
          case E.SPARK: c = SPARK_LUT[(shade[i] & 7) * 16 + Math.min(15, life[i])]; break;
          case E.BOLT: c = sim.vx[i] ? 0xff4040ff : 0xffc0ffff; break;
          default: c = LUT[t * 16 + (shade[i] >> 4)];
        }
        buf[i] = c;
      }
    }
    drawCursor();
    ctx.putImageData(img, 0, 0);
  }

  function drawCursor() {
    if (mouse.x < 0 || mouse.x >= W || mouse.y < 0 || mouse.y >= H) return;
    const r = PEN_SIZES[state.pen];
    const ro = (r + 0.5) * (r + 0.5), ri = r > 0 ? (r - 0.5) * (r - 0.5) : -1;
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        const d2 = dx * dx + dy * dy;
        if (d2 > ro || d2 <= ri) continue;
        const x = mouse.x + dx, y = mouse.y + dy;
        if (x >= 0 && x < W && y >= 0 && y < H && !sim.type[y * W + x]) buf[y * W + x] = 0xff909090;
      }
    }
  }

  // ------------------------------------------------------------------ loop

  const fpsEl = document.getElementById('fps');
  const dotEl = document.getElementById('dots');
  const msgEl = document.getElementById('msg');
  let last = performance.now(), acc = 0, frames = 0, fpsTime = last, steps = 0;

  function loop(now) {
    const dt = Math.min(100, now - last);
    last = now;
    applyInput();
    if (state.running) {
      acc += dt * SPEEDS[state.speed];
      let n = 0;
      while (acc >= 1000 / 60 && n < 4) { sim.step(); acc -= 1000 / 60; n++; steps++; }
      if (n === 4) acc = 0;
    } else acc = 0;
    render();

    frames++;
    if (now - fpsTime >= 500) {
      fpsEl.textContent = Math.round((steps * 1000) / (now - fpsTime)) + 'fps';
      frames = 0; steps = 0; fpsTime = now;
    }
    dotEl.textContent = 'dot' + sim.count;
    msgEl.textContent = now < flashUntil ? flashText : state.running ? 'POWDER REMAKE' : 'PAUSED';
    requestAnimationFrame(loop);
  }

  buildMenu();
  requestAnimationFrame(loop);
  window.PG.sim = sim; // handy for poking at from the console
})();
