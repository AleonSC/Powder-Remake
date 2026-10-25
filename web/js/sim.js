// The particle world: a grid of cells plus a coarse air (wind/pressure) field.
(function (root) {
  'use strict';

  const EL = typeof module !== 'undefined' && module.exports ? require('./elements.js') : root.PG.elements;
  const { S, E, STATE, DENS, FLAM, AIRF, DISP, EXPLODE, POWER, LIFE0, LIFER, BLOCKS, MOVABLE, SPARSE } = EL;

  const W = 400, H = 300, N = W * H;
  const AS = 4, AW = W / AS, AH = H / AS, AN = AW * AH; // air cells are AS x AS pixels
  const OUT = 255; // "type" of anything outside the world

  const DX8 = [-1, 0, 1, -1, 1, -1, 0, 1];
  const DY8 = [-1, -1, -1, 0, 0, 1, 1, 1];
  const DX4 = [0, -1, 1, 0];
  const DY4 = [-1, 0, 0, 1];

  const rand = Math.random;
  const rnd = (n) => (rand() * n) | 0;
  const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

  const VMAX = 15, PMAX = 100, FAN_SPEED = 4;

  class Sim {
    constructor() {
      this.type = new Uint8Array(N);
      this.life = new Int16Array(N);
      this.vx = new Float32Array(N);
      this.vy = new Float32Array(N);
      this.shade = new Uint8Array(N);
      this.stamp = new Int32Array(N);
      this.frame = 1;
      this.sidesOpen = false;
      this.count = 0;

      this.avx = new Float32Array(AN);
      this.avy = new Float32Array(AN);
      this.ap = new Float32Array(AN);
      this.tvx = new Float32Array(AN);
      this.tvy = new Float32Array(AN);
      this.tp = new Float32Array(AN);
      this.ablock = new Uint8Array(AN);
      this.acount = new Uint16Array(AN);

      // Collision info written by fly().
      this.hit = false;
      this.hitJ = -1;
      this.hitBX = false;
      this.hitBY = false;
    }

    clear() {
      this.type.fill(0);
      this.life.fill(0);
      this.vx.fill(0);
      this.vy.fill(0);
      this.count = 0;
    }

    clearAir() {
      this.avx.fill(0);
      this.avy.fill(0);
      this.ap.fill(0);
    }

    // ---------------------------------------------------------------- cells

    at(x, y) {
      if (x < 0 || x >= W || y < 0 || y >= H) return OUT;
      return this.type[y * W + x];
    }

    set(i, t) {
      this.type[i] = t;
      this.life[i] = LIFER[t] ? LIFE0[t] + rnd(LIFER[t]) : LIFE0[t];
      this.vx[i] = 0;
      this.vy[i] = 0;
      this.shade[i] = rnd(256);
      this.stamp[i] = this.frame;
    }

    del(i) {
      this.type[i] = 0;
      this.life[i] = 0;
      this.vx[i] = 0;
      this.vy[i] = 0;
      this.stamp[i] = this.frame;
    }

    swap(i, j) {
      const { type, life, vx, vy, shade } = this;
      let t = type[i]; type[i] = type[j]; type[j] = t;
      t = life[i]; life[i] = life[j]; life[j] = t;
      t = vx[i]; vx[i] = vx[j]; vx[j] = t;
      t = vy[i]; vy[i] = vy[j]; vy[j] = t;
      t = shade[i]; shade[i] = shade[j]; shade[j] = t;
      this.stamp[i] = this.stamp[j] = this.frame;
    }

    // Moves particle i to (nx, ny) if the target is empty or lighter.
    // Returns the new index, -1 if blocked, or -2 if it fell out of the world.
    tryMove(i, nx, ny) {
      if (nx < 0 || nx >= W || ny < 0 || ny >= H) {
        if (this.sidesOpen) { this.del(i); return -2; }
        return -1;
      }
      const j = ny * W + nx, tt = this.type[j];
      if (tt === 0 || (MOVABLE[tt] && DENS[tt] < DENS[this.type[i]])) {
        this.swap(i, j);
        return j;
      }
      return -1;
    }

    // Index of (nx, ny) if a fast-moving particle may pass into it, -1 if it
    // is solid, -2 if it is outside an open world.
    enterable(nx, ny) {
      if (nx < 0 || nx >= W || ny < 0 || ny >= H) return this.sidesOpen ? -2 : -1;
      const j = ny * W + nx, t = this.type[j];
      return t === 0 || (MOVABLE[t] && DENS[t] < 1) ? j : -1;
    }

    // ----------------------------------------------------------------- step

    step() {
      this.frame++;
      this.stepAir();
      const type = this.type, stamp = this.stamp, f = this.frame;
      for (let y = H - 1; y >= 0; y--) {
        const ltr = rand() < 0.5;
        const row = y * W;
        for (let k = 0; k < W; k++) {
          const x = ltr ? k : W - 1 - k;
          const i = row + x;
          const t = type[i];
          if (t === 0 || stamp[i] === f) continue;
          this.update(t, x, y, i);
        }
      }
    }

    update(t, x, y, i) {
      // A lit explosive counts down its fuse, then goes off.
      if (EXPLODE[t] && this.life[i] > 0) {
        if (--this.life[i] === 0) this.detonate(x, y, i, t);
        return;
      }
      switch (t) {
        case E.POWDER: case E.STONE: case E.GUNPOWDER: case E.FIREWORKS:
          this.movePowder(x, y, i, t); break;
        case E.SEED: this.updSeed(x, y, i); break;
        case E.BOMB: this.updBomb(x, y, i); break;
        case E.WATER: case E.OIL: case E.NITRO:
          this.moveLiquid(x, y, i, t); break;
        case E.SOAPY: this.updSoapy(x, y, i); break;
        case E.ACID: this.updAcid(x, y, i); break;
        case E.MAGMA: this.updMagma(x, y, i); break;
        case E.FIRE: this.updFire(x, y, i); break;
        case E.GAS: this.moveGas(x, y, i, t, 0.3); break;
        case E.STEAM: this.updSteam(x, y, i); break;
        case E.BUBBLE: this.updBubble(x, y, i); break;
        case E.FAN: this.updFan(x, y, i); break;
        case E.TORCH: this.updTorch(x, y); break;
        case E.ICE: this.updIce(x, y); break;
        case E.CLONE: this.updClone(x, y, i); break;
        case E.VIRUS: this.updVirus(x, y); break;
        case E.METAL: this.updMetal(x, y, i); break;
        case E.PLANT: this.updPlant(x, y, i); break;
        case E.SUPERBALL: this.updSuperball(x, y, i); break;
        case E.ANT: this.updAnt(x, y, i); break;
        case E.THUNDER: this.updThunder(x, y, i); break;
        case E.LASER: this.updLaser(x, y, i); break;
        case E.SPARK: this.updSpark(x, y, i); break;
        case E.ROCKET: this.updRocket(x, y, i); break;
        case E.BOLT: if (--this.life[i] <= 0) this.del(i); break;
      }
    }

    // ------------------------------------------------------------- movement

    // Wind nudges light particles. Returns like tryMove.
    airPush(x, y, i, t) {
      const f = AIRF[t];
      if (!f) return -1;
      const ai = (y >> 2) * AW + (x >> 2);
      const vx = this.avx[ai], vy = this.avy[ai];
      let mx = 0, my = 0;
      if (rand() < Math.abs(vx) * f * 0.4) mx = vx > 0 ? 1 : -1;
      if (rand() < Math.abs(vy) * f * 0.4) my = vy > 0 ? 1 : -1;
      if (mx === 0 && my === 0) return -1;
      const r = this.tryMove(i, x + mx, y + my);
      if (r !== -1 || mx === 0 || my === 0) return r;
      return this.tryMove(i, x + mx, y);
    }

    movePowder(x, y, i, t) {
      let r = this.airPush(x, y, i, t);
      if (r !== -1) return r;
      r = this.tryMove(i, x, y + 1);
      if (r !== -1) return r;
      const d = rand() < 0.5 ? -1 : 1;
      r = this.tryMove(i, x + d, y + 1);
      if (r !== -1) return r;
      return this.tryMove(i, x - d, y + 1);
    }

    canFall(x, y, t) {
      if (x < 0 || x >= W || y < 0 || y >= H) return false;
      const tt = this.type[y * W + x];
      return tt === 0 || (MOVABLE[tt] && DENS[tt] < DENS[t]);
    }

    moveLiquid(x, y, i, t) {
      let r = this.airPush(x, y, i, t);
      if (r !== -1) return r;
      r = this.tryMove(i, x, y + 1);
      if (r !== -1) return r;
      let dir = rand() < 0.5 ? -1 : 1;
      r = this.tryMove(i, x + dir, y + 1);
      if (r !== -1) return r;
      r = this.tryMove(i, x - dir, y + 1);
      if (r !== -1) return r;
      // Spread sideways until blocked or there is room to fall.
      let ci = i, cx = x;
      const disp = DISP[t];
      for (let k = 0; k < disp; k++) {
        r = this.tryMove(ci, cx + dir, y);
        if (r === -2) return -2;
        if (r === -1) {
          if (k === 0) { dir = -dir; r = this.tryMove(ci, cx + dir, y); if (r === -2) return -2; }
          if (r === -1) break;
        }
        ci = r;
        cx += dir;
        if (this.canFall(cx, y + 1, t)) break;
      }
      return ci === i ? -1 : ci;
    }

    moveGas(x, y, i, t, up) {
      const r = this.airPush(x, y, i, t);
      if (r !== -1) return r;
      const dx = rnd(3) - 1;
      const dy = rand() < up ? -1 : rnd(3) - 1;
      if (dx === 0 && dy === 0) return -1;
      return this.tryMove(i, x + dx, y + dy);
    }

    // Moves a fast particle along (vx, vy), sub-stepping one cell at a time.
    // Returns its new index or -2 if it left the world; on collision sets
    // this.hit, the blocking cell this.hitJ and which axes were blocked.
    fly(x, y, i, vx, vy) {
      this.hit = false;
      this.hitJ = -1;
      this.hitBX = this.hitBY = false;
      const n = Math.ceil(Math.max(Math.abs(vx), Math.abs(vy)));
      if (n === 0) return i;
      const sx = vx / n, sy = vy / n;
      let fx = x + rand(), fy = y + rand();
      let cx = x, cy = y, ci = i;
      for (let k = 0; k < n; k++) {
        fx += sx;
        fy += sy;
        const nx = Math.floor(fx), ny = Math.floor(fy);
        if (nx === cx && ny === cy) continue;
        const j = this.enterable(nx, ny);
        if (j >= 0) { this.swap(ci, j); ci = j; cx = nx; cy = ny; continue; }
        if (j === -2) { this.del(ci); return -2; }
        this.hit = true;
        if (nx >= 0 && nx < W && ny >= 0 && ny < H) this.hitJ = ny * W + nx;
        this.hitBX = nx !== cx && this.enterable(nx, cy) === -1;
        this.hitBY = ny !== cy && this.enterable(cx, ny) === -1;
        if (!this.hitBX && !this.hitBY) { this.hitBX = nx !== cx; this.hitBY = ny !== cy; }
        break;
      }
      return ci;
    }

    // ------------------------------------------------------ fire & blasts

    ignite(j) {
      const t = this.type[j];
      if (EXPLODE[t]) {
        if (this.life[j] === 0) this.life[j] = 1 + rnd(3);
        return;
      }
      if (t === E.FIREWORKS) { this.set(j, E.ROCKET); return; }
      if (t === E.ICE) { this.set(j, E.WATER); return; }
      this.set(j, E.FIRE);
      if (t === E.OIL) this.life[j] += 60;
      else if (t === E.GAS) this.life[j] = 8 + rnd(8);
    }

    detonate(x, y, i, t) {
      this.set(i, E.FIRE);
      this.explode(x, y, EXPLODE[t], POWER[t]);
    }

    explode(cx, cy, r, power) {
      const type = this.type, r2 = r * r;
      for (let dy = -r; dy <= r; dy++) {
        const y = cy + dy;
        if (y < 0 || y >= H) continue;
        for (let dx = -r; dx <= r; dx++) {
          if (dx * dx + dy * dy > r2) continue;
          const x = cx + dx;
          if (x < 0 || x >= W) continue;
          const j = y * W + x, t = type[j];
          if (EXPLODE[t]) {
            if (this.life[j] === 0) this.life[j] = 1 + rnd(4);
            continue;
          }
          switch (t) {
            case E.EMPTY: case E.BOLT:
              if (rand() < 0.5) { this.set(j, E.FIRE); this.life[j] = 5 + rnd(15); }
              break;
            case E.WATER: if (rand() < 0.3) this.set(j, E.STEAM); break;
            case E.ICE: this.set(j, E.WATER); break;
            case E.FIREWORKS: this.ignite(j); break;
            case E.BUBBLE: this.del(j); break;
            default:
              if (FLAM[t]) {
                if (rand() < 0.7) { this.set(j, E.FIRE); this.life[j] = 5 + rnd(15); } else this.del(j);
              }
          }
        }
      }
      this.blast(cx, cy, r, power);
    }

    // Pushes air outward from a point.
    blast(cx, cy, r, power) {
      const acx = cx / AS, acy = cy / AS, ar = r / AS + 1.5;
      const x0 = Math.max(1, Math.floor(acx - ar)), x1 = Math.min(AW - 2, Math.ceil(acx + ar));
      const y0 = Math.max(1, Math.floor(acy - ar)), y1 = Math.min(AH - 2, Math.ceil(acy + ar));
      for (let ay = y0; ay <= y1; ay++) {
        for (let ax = x0; ax <= x1; ax++) {
          const ddx = ax + 0.5 - acx, ddy = ay + 0.5 - acy;
          const d = Math.sqrt(ddx * ddx + ddy * ddy);
          if (d > ar) continue;
          const ai = ay * AW + ax;
          if (this.ablock[ai]) continue;
          const s = power * (1 - d / ar);
          this.ap[ai] = clamp(this.ap[ai] + s, -PMAX, PMAX);
          if (d > 0.01) {
            this.avx[ai] = clamp(this.avx[ai] + (ddx / d) * s, -VMAX, VMAX);
            this.avy[ai] = clamp(this.avy[ai] + (ddy / d) * s, -VMAX, VMAX);
          }
        }
      }
    }

    // ------------------------------------------------------------ elements

    updFire(x, y, i) {
      if (--this.life[i] <= 0) { this.del(i); return; }
      const type = this.type;
      for (let k = 0; k < 8; k++) {
        const nx = x + DX8[k], ny = y + DY8[k];
        if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
        const j = ny * W + nx, nt = type[j];
        if (nt === 0 || nt === E.FIRE) continue;
        if (nt === E.WATER || nt === E.SOAPY) {
          if (rand() < 0.1) this.set(j, E.STEAM);
          this.del(i);
          return;
        }
        if (FLAM[nt] && rand() < FLAM[nt]) this.ignite(j);
        else if (nt === E.ICE && rand() < 0.05) this.set(j, E.WATER);
      }
      if (rand() < 0.7) this.moveGas(x, y, i, E.FIRE, 0.6);
    }

    updSeed(x, y, i) {
      if (this.movePowder(x, y, i, E.SEED) !== -1) return;
      if (rand() < 0.02) {
        const below = this.at(x, y + 1);
        if (below === E.POWDER || below === E.PLANT) {
          this.set(i, E.PLANT);
          this.life[i] = 30 + rnd(60);
        }
      }
    }

    plantNeighbors(x, y) {
      let n = 0;
      for (let k = 0; k < 8; k++) if (this.at(x + DX8[k], y + DY8[k]) === E.PLANT) n++;
      return n;
    }

    updPlant(x, y, i) {
      const life = this.life;
      if (life[i] > 0 && rand() < 0.2) {
        const dx = rnd(3) - 1, dy = rand() < 0.7 ? -1 : 0;
        if (dx !== 0 || dy !== 0) {
          const nx = x + dx, ny = y + dy;
          if (this.at(nx, ny) === E.EMPTY && this.plantNeighbors(nx, ny) <= 2) {
            const j = ny * W + nx;
            this.set(j, E.PLANT);
            life[j] = life[i] - 1;
            life[i] = rand() < 0.08 ? life[i] >> 1 : 0; // sometimes keep a bud to branch
          }
        }
      }
      // Drink water to keep growing.
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      if (this.at(nx, ny) === E.WATER && rand() < 0.05) {
        this.del(ny * W + nx);
        if (life[i] < 40) life[i] += 4;
      }
    }

    updBomb(x, y, i) {
      for (let k = 0; k < 4; k++) {
        const nt = this.at(x + DX4[k], y + DY4[k]);
        if (nt === 0 || nt === E.BOMB || nt === E.BOLT) continue;
        if (nt === OUT && (this.sidesOpen || DY4[k] !== 1)) continue;
        this.detonate(x, y, i, E.BOMB);
        return;
      }
      this.movePowder(x, y, i, E.BOMB);
    }

    updSoapy(x, y, i) {
      const r = this.moveLiquid(x, y, i, E.SOAPY);
      if (r === -2) return;
      if (r >= 0) { i = r; x = r % W; y = (r / W) | 0; }
      const ai = (y >> 2) * AW + (x >> 2);
      const sp = Math.abs(this.avx[ai]) + Math.abs(this.avy[ai]);
      if (sp > 1 && rand() < 0.02 * sp && this.at(x, y - 1) === E.EMPTY) this.set(i - W, E.BUBBLE);
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      if (this.at(nx, ny) === E.OIL && rand() < 0.05) this.del(ny * W + nx);
    }

    updAcid(x, y, i) {
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      const nt = this.at(nx, ny);
      if (nt !== 0 && nt !== OUT && nt !== E.ACID && nt !== E.WALL && nt !== E.WATER &&
          nt !== E.BOLT && nt !== E.FIRE && nt !== E.STEAM) {
        if (rand() < (nt === E.METAL ? 0.01 : 0.1)) {
          this.del(ny * W + nx);
          if (rand() < 0.3) { this.del(i); return; }
        }
      }
      this.moveLiquid(x, y, i, E.ACID);
    }

    updMagma(x, y, i) {
      for (let n = 0; n < 2; n++) {
        const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
        const nt = this.at(nx, ny);
        if (nt === 0 || nt === OUT) continue;
        const j = ny * W + nx;
        if (nt === E.WATER) { this.set(i, E.STONE); this.set(j, E.STEAM); return; }
        if (nt === E.ICE) this.set(j, E.WATER);
        else if (FLAM[nt] && rand() < FLAM[nt]) this.ignite(j);
      }
      if (rand() < 0.01 && this.at(x, y - 1) === E.EMPTY) this.set(i - W, E.FIRE);
      if (rand() < 0.5) this.moveLiquid(x, y, i, E.MAGMA);
      else this.tryMove(i, x, y + 1);
    }

    updSteam(x, y, i) {
      if (--this.life[i] <= 0) {
        if (rand() < 0.5) this.set(i, E.WATER); else this.del(i);
        return;
      }
      const k = rnd(8);
      if (this.at(x + DX8[k], y + DY8[k]) === E.ICE) { this.set(i, E.WATER); return; }
      this.moveGas(x, y, i, E.STEAM, 0.5);
    }

    updBubble(x, y, i) {
      if (--this.life[i] <= 0) { this.del(i); return; }
      if (rand() < 0.5) this.moveGas(x, y, i, E.BUBBLE, 0.6);
    }

    updFan(x, y, i) {
      const ai = (y >> 2) * AW + (x >> 2);
      this.avx[ai] += (this.vx[i] * FAN_SPEED - this.avx[ai]) * 0.1;
      this.avy[ai] += (this.vy[i] * FAN_SPEED - this.avy[ai]) * 0.1;
    }

    updTorch(x, y) {
      for (let dx = -1; dx <= 1; dx++) {
        if (rand() < 0.25 && this.at(x + dx, y - 1) === E.EMPTY) {
          const j = (y - 1) * W + x + dx;
          this.set(j, E.FIRE);
          this.life[j] = 10 + rnd(20);
        }
      }
    }

    updIce(x, y) {
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      if (this.at(nx, ny) === E.WATER && rand() < 0.003) this.set(ny * W + nx, E.ICE);
    }

    updClone(x, y, i) {
      const src = this.life[i];
      if (!src) {
        for (let k = 0; k < 8; k++) {
          const nt = this.at(x + DX8[k], y + DY8[k]);
          if (nt !== 0 && nt !== OUT && nt !== E.WALL && nt !== E.CLONE && nt !== E.BOLT) {
            this.life[i] = nt;
            return;
          }
        }
        return;
      }
      if (rand() > 0.2) return;
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      if (this.at(nx, ny) !== E.EMPTY) return;
      const j = ny * W + nx;
      this.set(j, src);
      this.initParticle(j, src, DX8[k], DY8[k]);
    }

    updVirus(x, y) {
      const k = rnd(8), nx = x + DX8[k], ny = y + DY8[k];
      const nt = this.at(nx, ny);
      if (nt !== 0 && nt !== OUT && nt !== E.WALL && nt !== E.VIRUS && nt !== E.CLONE &&
          nt !== E.FIRE && nt !== E.BOLT && rand() < 0.08) {
        this.set(ny * W + nx, E.VIRUS);
      }
    }

    // Metal carries electricity: life > 0 is charged, life < 0 is cooling down.
    updMetal(x, y, i) {
      const l = this.life[i];
      if (l > 0) {
        for (let k = 0; k < 4; k++) {
          const nx = x + DX4[k], ny = y + DY4[k];
          const nt = this.at(nx, ny);
          if (nt === 0 || nt === OUT) continue;
          const j = ny * W + nx;
          if (nt === E.METAL) { if (this.life[j] === 0) this.life[j] = 10; }
          else if (FLAM[nt] && rand() < 0.3) this.ignite(j);
        }
        this.life[i] = l === 1 ? -15 : l - 1;
      } else if (l < 0) {
        this.life[i] = l + 1;
      }
    }

    updSuperball(x, y, i) {
      const ai = (y >> 2) * AW + (x >> 2);
      let vx = this.vx[i] + this.avx[ai] * 0.05;
      let vy = this.vy[i] + this.avy[ai] * 0.05 + 0.12;
      const sp = Math.sqrt(vx * vx + vy * vy);
      if (sp > 6) { vx *= 6 / sp; vy *= 6 / sp; }
      const ni = this.fly(x, y, i, vx, vy);
      if (ni === -2) return;
      if (this.hitBX) vx = -vx;
      if (this.hitBY) vy = -vy;
      this.vx[ni] = vx;
      this.vy[ni] = vy;
    }

    updAnt(x, y, i) {
      if (this.tryMove(i, x, y + 1) !== -1) return; // falling
      if (rand() < 0.5) return;
      let dir = this.vx[i] || 1;
      if (rand() < 0.01) dir = -dir;
      this.vx[i] = dir;
      const fx = x + dir;
      if (this.tryMove(i, fx, y) !== -1) return;
      if (this.at(fx, y - 1) === E.EMPTY && this.tryMove(i, fx, y - 1) !== -1) return; // step up
      const ft = this.at(fx, y);
      if ((ft === E.POWDER || ft === E.SEED || ft === E.PLANT) && rand() < 0.1) {
        this.del(y * W + fx); // dig
        return;
      }
      if (this.at(x, y - 1) === E.EMPTY && rand() < 0.3) { this.tryMove(i, x, y - 1); return; }
      this.vx[i] = -dir;
    }

    setBolt(i, kind) {
      this.set(i, E.BOLT);
      this.vx[i] = kind;
      if (kind === 1) this.life[i] = 10 + rnd(6); // laser beams linger longer
    }

    updThunder(x, y, i) {
      if (--this.life[i] <= 0) { this.del(i); return; }
      let ci = i, cx = x, cy = y;
      for (let k = 0; k < 4; k++) {
        const nx = cx + rnd(3) - 1, ny = cy + 1;
        if (ny >= H || nx < 0 || nx >= W) { this.del(ci); return; }
        const j = ny * W + nx, nt = this.type[j];
        if (nt === 0 || (MOVABLE[nt] && DENS[nt] < 1)) {
          this.swap(ci, j);
          if (this.type[ci] === 0) this.setBolt(ci, 0);
          ci = j; cx = nx; cy = ny;
          continue;
        }
        if (nt === E.METAL) { if (this.life[j] >= 0) this.life[j] = 12; }
        else if (FLAM[nt] || EXPLODE[nt]) this.ignite(j);
        else if (nt === E.ICE) this.set(j, E.WATER);
        else if (nt === E.WATER && rand() < 0.5) this.set(j, E.STEAM);
        this.setBolt(ci, 0);
        return;
      }
    }

    updLaser(x, y, i) {
      if (--this.life[i] <= 0) { this.del(i); return; }
      let dx = this.vx[i], dy = this.vy[i];
      if (dx === 0 && dy === 0) dy = 1;
      let ci = i, cx = x, cy = y;
      for (let k = 0; k < 5; k++) {
        const nx = cx + dx, ny = cy + dy;
        const j = this.enterable(nx, ny);
        if (j === -2) { this.del(ci); return; }
        if (j >= 0) {
          this.swap(ci, j);
          if (this.type[ci] === 0) this.setBolt(ci, 1);
          ci = j; cx = nx; cy = ny;
          continue;
        }
        const nt = this.at(nx, ny);
        if (nt === E.METAL) { // mirror
          const bx = dx !== 0 && this.enterable(cx + dx, cy) === -1;
          const by = dy !== 0 && this.enterable(cx, cy + dy) === -1;
          if (bx) dx = -dx;
          if (by) dy = -dy;
          if (!bx && !by) { dx = -dx; dy = -dy; }
          continue;
        }
        if (nt !== OUT && nt !== E.WALL) this.heat(ny * W + nx, nt);
        this.del(ci);
        return;
      }
      this.vx[ci] = dx;
      this.vy[ci] = dy;
    }

    heat(j, t) {
      if (FLAM[t] || EXPLODE[t]) this.ignite(j);
      else if (t === E.ICE) this.set(j, E.WATER);
      else if (t === E.WATER || t === E.SOAPY) { if (rand() < 0.3) this.set(j, E.STEAM); }
      else if (t === E.STONE) { if (rand() < 0.3) this.set(j, E.MAGMA); }
      else if (STATE[t] === S.POWDER && rand() < 0.3) this.del(j);
    }

    updSpark(x, y, i) {
      if (--this.life[i] <= 0) { this.del(i); return; }
      const vx = this.vx[i] * 0.98, vy = this.vy[i] * 0.98 + 0.05;
      const ni = this.fly(x, y, i, vx, vy);
      if (ni === -2) return;
      if (this.hit) {
        if (this.hitJ >= 0 && FLAM[this.type[this.hitJ]] && rand() < 0.3) this.ignite(this.hitJ);
        this.del(ni);
        return;
      }
      this.vx[ni] = vx;
      this.vy[ni] = vy;
    }

    updRocket(x, y, i) {
      if (--this.life[i] <= 0) { this.burst(x, y, i); return; }
      let ci = i, cx = x, cy = y;
      for (let k = 0; k < 2; k++) {
        const nx = cx + (rand() < 0.15 ? rnd(3) - 1 : 0), ny = cy - 1;
        const j = this.enterable(nx, ny);
        if (j === -2) { this.del(ci); return; }
        if (j === -1) { this.burst(cx, cy, ci); return; }
        this.swap(ci, j);
        if (this.type[ci] === 0) { this.set(ci, E.FIRE); this.life[ci] = 4 + rnd(6); }
        ci = j; cx = nx; cy = ny;
      }
    }

    burst(x, y, i) {
      const hue = rnd(8);
      this.del(i);
      for (let dy = -3; dy <= 3; dy++) {
        for (let dx = -3; dx <= 3; dx++) {
          if (dx * dx + dy * dy > 9 || this.at(x + dx, y + dy) !== E.EMPTY || rand() > 0.85) continue;
          const j = (y + dy) * W + x + dx;
          this.set(j, E.SPARK);
          let d = Math.sqrt(dx * dx + dy * dy), ux = dx, uy = dy;
          if (d === 0) { const a = rand() * Math.PI * 2; ux = Math.cos(a); uy = Math.sin(a); d = 1; }
          const s = 1 + rand() * 2;
          this.vx[j] = (ux / d) * s + (rand() - 0.5) * 0.5;
          this.vy[j] = (uy / d) * s + (rand() - 0.5) * 0.5;
          this.shade[j] = hue;
        }
      }
      this.blast(x, y, 4, 0.5);
    }

    // ------------------------------------------------------------------ air

    stepAir() {
      const type = this.type, ablock = this.ablock, acount = this.acount;
      acount.fill(0);
      let count = 0;
      for (let y = 0; y < H; y++) {
        const row = y * W, arow = (y >> 2) * AW;
        for (let x = 0; x < W; x++) {
          const t = type[row + x];
          if (t) {
            count++;
            if (BLOCKS[t]) acount[arow + (x >> 2)]++;
          }
        }
      }
      this.count = count;
      for (let k = 0; k < AN; k++) ablock[k] = acount[k] >= (AS * AS) / 2 ? 1 : 0;

      const vx = this.avx, vy = this.avy, p = this.ap;
      // Pressure builds where air flows in (vx[i] is the flow between i and i+1).
      for (let y = 1; y < AH - 1; y++) {
        for (let x = 1; x < AW - 1; x++) {
          const i = y * AW + x;
          if (ablock[i]) continue;
          p[i] = p[i] * 0.9995 + (vx[i - 1] - vx[i] + vy[i - AW] - vy[i]) * 0.3;
        }
      }
      // Air accelerates from high to low pressure.
      for (let y = 1; y < AH - 1; y++) {
        for (let x = 1; x < AW - 1; x++) {
          const i = y * AW + x;
          let ux = vx[i] * 0.999 + (p[i] - p[i + 1]) * 0.4;
          let uy = vy[i] * 0.999 + (p[i] - p[i + AW]) * 0.4;
          if (ablock[i] || ablock[i + 1]) ux = 0;
          if (ablock[i] || ablock[i + AW]) uy = 0;
          vx[i] = ux;
          vy[i] = uy;
        }
      }
      // Smooth and advect (semi-Lagrangian) into the scratch buffers.
      const tvx = this.tvx, tvy = this.tvy, tp = this.tp;
      for (let y = 1; y < AH - 1; y++) {
        for (let x = 1; x < AW - 1; x++) {
          const i = y * AW + x;
          if (ablock[i]) { tvx[i] = 0; tvy[i] = 0; tp[i] = p[i]; continue; }
          let sx = 0, sy = 0, sp = 0, ws = 0;
          for (let j = -1; j <= 1; j++) {
            for (let k = -1; k <= 1; k++) {
              const n = i + j * AW + k;
              if (ablock[n]) continue;
              const w = (j === 0 ? 2 : 1) * (k === 0 ? 2 : 1);
              sx += vx[n] * w; sy += vy[n] * w; sp += p[n] * w; ws += w;
            }
          }
          sx /= ws; sy /= ws; sp /= ws;
          const tx = clamp(x - sx * 0.7, 0, AW - 1.001), ty = clamp(y - sy * 0.7, 0, AH - 1.001);
          const ix = tx | 0, iy = ty | 0, fx = tx - ix, fy = ty - iy;
          const a = iy * AW + ix;
          let ax = sx, ay = sy;
          if (!ablock[a] && !ablock[a + 1] && !ablock[a + AW] && !ablock[a + AW + 1]) {
            ax = (vx[a] * (1 - fx) + vx[a + 1] * fx) * (1 - fy) + (vx[a + AW] * (1 - fx) + vx[a + AW + 1] * fx) * fy;
            ay = (vy[a] * (1 - fx) + vy[a + 1] * fx) * (1 - fy) + (vy[a + AW] * (1 - fx) + vy[a + AW + 1] * fx) * fy;
          }
          tvx[i] = clamp(sx * 0.7 + ax * 0.3, -VMAX, VMAX);
          tvy[i] = clamp(sy * 0.7 + ay * 0.3, -VMAX, VMAX);
          tp[i] = clamp(sp, -PMAX, PMAX);
        }
      }
      // Open boundary: edges leak toward their inner neighbour.
      for (let x = 0; x < AW; x++) {
        this.edge(x, 0, x, 1);
        this.edge(x, AH - 1, x, AH - 2);
      }
      for (let y = 1; y < AH - 1; y++) {
        this.edge(0, y, 1, y);
        this.edge(AW - 1, y, AW - 2, y);
      }
      this.avx = tvx; this.tvx = vx;
      this.avy = tvy; this.tvy = vy;
      this.ap = tp; this.tp = p;
    }

    edge(x, y, ix, iy) {
      const i = y * AW + x, n = clamp(iy, 1, AH - 2) * AW + clamp(ix, 1, AW - 2);
      this.tvx[i] = this.tvx[n] * 0.9;
      this.tvy[i] = this.tvy[n] * 0.9;
      this.tp[i] = this.tp[n] * 0.9;
    }

    // ---------------------------------------------------------------- tools

    // Sets up direction-dependent state of a freshly placed particle.
    initParticle(j, t, dirx, diry) {
      if (t === E.FAN) {
        const d = Math.sqrt(dirx * dirx + diry * diry) || 1;
        this.vx[j] = dirx / d;
        this.vy[j] = diry / d;
      } else if (t === E.LASER) {
        this.vx[j] = Math.round(dirx);
        this.vy[j] = Math.round(diry);
        if (this.vx[j] === 0 && this.vy[j] === 0) this.vy[j] = 1;
      } else if (t === E.ANT) {
        this.vx[j] = rand() < 0.5 ? -1 : 1;
      } else if (t === E.SUPERBALL) {
        this.vx[j] = dirx * 2;
        this.vy[j] = diry * 2;
      }
    }

    // Fills empty cells within radius r of (cx, cy) with material t.
    // (dirx, diry) is the unit direction the cursor is moving in.
    brush(cx, cy, r, t, dirx, diry) {
      const sparse = SPARSE[t];
      if (sparse === 1) { // one particle per call, somewhere in the brush
        const a = rand() * Math.PI * 2, d = rand() * r;
        const x = Math.round(cx + Math.cos(a) * d), y = Math.round(cy + Math.sin(a) * d);
        if (this.at(x, y) !== E.EMPTY) return;
        const j = y * W + x;
        this.set(j, t);
        this.initParticle(j, t, dirx, diry);
        return;
      }
      const fill = sparse || (STATE[t] === S.STATIC ? 1 : 0.6);
      const r2 = (r + 0.5) * (r + 0.5);
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          if (dx * dx + dy * dy > r2) continue;
          const x = cx + dx, y = cy + dy;
          if (this.at(x, y) !== E.EMPTY) continue;
          if (r > 0 && rand() > fill) continue;
          const j = y * W + x;
          this.set(j, t);
          this.initParticle(j, t, dirx, diry);
        }
      }
    }

    erase(cx, cy, r) {
      const r2 = (r + 0.5) * (r + 0.5);
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          if (dx * dx + dy * dy > r2) continue;
          const x = cx + dx, y = cy + dy;
          if (x >= 0 && x < W && y >= 0 && y < H) this.del(y * W + x);
        }
      }
    }

    // Calls fn(ai) for every air cell under a brush.
    forAirCells(cx, cy, r, fn) {
      const acx = cx / AS, acy = cy / AS, ar = r / AS + 1;
      for (let ay = Math.max(0, Math.floor(acy - ar)); ay <= Math.min(AH - 1, Math.ceil(acy + ar)); ay++) {
        for (let ax = Math.max(0, Math.floor(acx - ar)); ax <= Math.min(AW - 1, Math.ceil(acx + ar)); ax++) {
          const ddx = ax + 0.5 - acx, ddy = ay + 0.5 - acy;
          if (ddx * ddx + ddy * ddy <= ar * ar) fn(ay * AW + ax);
        }
      }
    }

    wind(cx, cy, r, dx, dy) {
      this.forAirCells(cx, cy, r, (ai) => {
        this.avx[ai] = clamp(this.avx[ai] + dx * 0.3, -VMAX, VMAX);
        this.avy[ai] = clamp(this.avy[ai] + dy * 0.3, -VMAX, VMAX);
      });
    }

    airBlast(cx, cy, r) {
      this.forAirCells(cx, cy, r, (ai) => { this.ap[ai] = clamp(this.ap[ai] + 2, -PMAX, PMAX); });
    }

    // Drags particles under the brush along with the cursor.
    drag(cx, cy, r, dx, dy) {
      dx = Math.round(dx); dy = Math.round(dy);
      if (dx === 0 && dy === 0) return;
      const r2 = (r + 0.5) * (r + 0.5), cells = [];
      for (let oy = -r; oy <= r; oy++) {
        for (let ox = -r; ox <= r; ox++) {
          if (ox * ox + oy * oy > r2) continue;
          const t = this.at(cx + ox, cy + oy);
          if (t !== 0 && t !== OUT && t !== E.WALL) cells.push((cy + oy) * W + cx + ox);
        }
      }
      // Move the cells furthest along the drag direction first.
      const key = (j) => (j % W) * dx + ((j / W) | 0) * dy;
      cells.sort((a, b) => key(b) - key(a));
      for (const j of cells) {
        const nx = (j % W) + dx, ny = ((j / W) | 0) + dy;
        if (this.at(nx, ny) === E.EMPTY) this.swap(j, ny * W + nx);
      }
    }

    // ---------------------------------------------------------- save/load

    serialize() {
      const rle = (arr) => {
        const out = [];
        for (let i = 0; i < arr.length;) {
          const v = arr[i];
          let n = 1;
          while (i + n < arr.length && arr[i + n] === v) n++;
          out.push(v, n);
          i += n;
        }
        return out;
      };
      const dirs = [];
      for (let i = 0; i < N; i++) {
        if (this.type[i] && (this.vx[i] || this.vy[i])) dirs.push(i, +this.vx[i].toFixed(3), +this.vy[i].toFixed(3));
      }
      return JSON.stringify({ v: 1, w: W, h: H, type: rle(this.type), life: rle(this.life), dirs });
    }

    deserialize(str) {
      const d = JSON.parse(str);
      if (d.v !== 1 || d.w !== W || d.h !== H) throw new Error('Incompatible save');
      const unrle = (src, arr) => {
        let i = 0;
        for (let k = 0; k < src.length; k += 2) { arr.fill(src[k], i, i + src[k + 1]); i += src[k + 1]; }
      };
      this.clear();
      this.clearAir();
      unrle(d.type, this.type);
      unrle(d.life, this.life);
      for (let k = 0; k < d.dirs.length; k += 3) { this.vx[d.dirs[k]] = d.dirs[k + 1]; this.vy[d.dirs[k]] = d.dirs[k + 2]; }
      for (let i = 0; i < N; i++) this.shade[i] = rnd(256);
    }
  }

  Object.assign(Sim, { W, H, N, AS, AW, AH, OUT });

  if (typeof module !== 'undefined' && module.exports) module.exports = Sim;
  else root.PG.Sim = Sim;
})(this);
