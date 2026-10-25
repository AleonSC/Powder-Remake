// The particle world: a grid of cells plus a coarse air (wind/pressure) field.
// Pure C# (no UnityEngine) so it can be unit tested outside the editor.
using System;
using System.Collections.Generic;
using System.IO;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        public const int DefaultWidth = 400, DefaultHeight = 300;
        /// <summary>Air cells are AS x AS pixels; world sizes must be multiples of AS.</summary>
        public const int AS = 4;

        /// <summary>Size in dots (W x H = N cells) and of the air grid (AW x AH = AN cells).</summary>
        public readonly int W, H, N, AW, AH, AN;

        const float VMax = 15, PMax = 100, FanSpeed = 4;

        static readonly int[] DX8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] DY8 = { -1, -1, -1, 0, 0, 1, 1, 1 };
        static readonly int[] DX4 = { 0, -1, 1, 0 };
        static readonly int[] DY4 = { -1, 0, 0, 1 };

        static readonly MoveState[] State = Elements.State;
        static readonly float[] Dens = Elements.Dens;
        static readonly float[] Flam = Elements.Flam;
        static readonly float[] AirF = Elements.AirF;
        static readonly int[] Disp = Elements.Disp;
        static readonly int[] Explode = Elements.Explode;
        static readonly float[] Power = Elements.Power;
        static readonly bool[] Movable = Elements.Movable;

        public readonly byte[] Type;
        public readonly short[] Life;
        public readonly float[] Vx;
        public readonly float[] Vy;
        public readonly byte[] Shade;
        readonly int[] stamp;

        public float[] AirVx, AirVy, Pressure;
        float[] tvx, tvy, tp;
        readonly bool[] ablock;
        readonly ushort[] acount;

        public int Frame = 1;
        /// <summary>When true, particles leaving the screen are deleted instead of hitting the edge.</summary>
        public bool SidesOpen;
        /// <summary>Number of particles, updated every step.</summary>
        public int Count;
        int plantCells;

        // Collision info written by Fly().
        bool hit, hitBX, hitBY;
        int hitJ = -1;

        uint rng;

        public World(uint seed = 0) : this(DefaultWidth, DefaultHeight, seed) { }

        public World(int width, int height, uint seed = 0)
        {
            if (width < 16 || height < 16 || width % AS != 0 || height % AS != 0)
                throw new ArgumentException("World size must be at least 16 and a multiple of " + AS);
            W = width;
            H = height;
            N = W * H;
            AW = W / AS;
            AH = H / AS;
            AN = AW * AH;
            Type = new byte[N];
            Life = new short[N];
            Vx = new float[N];
            Vy = new float[N];
            Shade = new byte[N];
            stamp = new int[N];
            AirVx = new float[AN];
            AirVy = new float[AN];
            Pressure = new float[AN];
            tvx = new float[AN];
            tvy = new float[AN];
            tp = new float[AN];
            ablock = new bool[AN];
            acount = new ushort[AN];
            SunTop = new int[W];
            rng = seed != 0 ? seed : (uint)Environment.TickCount | 1u;
        }

        /// <summary>
        /// A copy of this world at a new size. Contents stay anchored to the floor and
        /// centred horizontally; anything that no longer fits is cropped.
        /// </summary>
        public World Resized(int width, int height)
        {
            var copy = new World(width, height);
            using (var ms = new MemoryStream())
            {
                Save(ms);
                ms.Position = 0;
                copy.Load(ms);
            }
            copy.SidesOpen = SidesOpen;
            copy.Sun = Sun;
            return copy;
        }

        // ------------------------------------------------------------ random

        float Rand()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng >> 8) * (1f / 16777216f);
        }

        int Rnd(int n) { return (int)(Rand() * n); }

        static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

        // ------------------------------------------------------------- cells

        public void Clear()
        {
            Array.Clear(Type, 0, N);
            Array.Clear(Life, 0, N);
            Array.Clear(Vx, 0, N);
            Array.Clear(Vy, 0, N);
            Array.Clear(Players, 0, MaxPlayers);
            Fighters.Clear();
            PlantWater = 0;
            Count = 0;
        }

        public void ClearAir()
        {
            Array.Clear(AirVx, 0, AN);
            Array.Clear(AirVy, 0, AN);
            Array.Clear(Pressure, 0, AN);
        }

        public byte At(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return E.Out;
            return Type[y * W + x];
        }

        public void Set(int i, byte t)
        {
            Type[i] = t;
            int lr = Elements.LifeR[t];
            Life[i] = (short)(lr != 0 ? Elements.Life0[t] + Rnd(lr) : Elements.Life0[t]);
            Vx[i] = 0;
            Vy[i] = 0;
            Shade[i] = (byte)Rnd(256);
            stamp[i] = Frame;
        }

        public void Del(int i)
        {
            Type[i] = 0;
            Life[i] = 0;
            Vx[i] = 0;
            Vy[i] = 0;
            stamp[i] = Frame;
        }

        void Swap(int i, int j)
        {
            byte tb = Type[i]; Type[i] = Type[j]; Type[j] = tb;
            short ts = Life[i]; Life[i] = Life[j]; Life[j] = ts;
            float tf = Vx[i]; Vx[i] = Vx[j]; Vx[j] = tf;
            tf = Vy[i]; Vy[i] = Vy[j]; Vy[j] = tf;
            tb = Shade[i]; Shade[i] = Shade[j]; Shade[j] = tb;
            stamp[i] = stamp[j] = Frame;
        }

        /// <summary>
        /// Moves particle i to (nx, ny) if the target is empty or lighter.
        /// Returns the new index, -1 if blocked, or -2 if it fell out of the world.
        /// </summary>
        int TryMove(int i, int nx, int ny)
        {
            if (nx < 0 || nx >= W || ny < 0 || ny >= H)
            {
                if (SidesOpen) { Del(i); return -2; }
                return -1;
            }
            int j = ny * W + nx;
            byte tt = Type[j];
            if (tt == 0 || (Movable[tt] && Dens[tt] < Dens[Type[i]]))
            {
                Swap(i, j);
                return j;
            }
            return -1;
        }

        /// <summary>
        /// Index of (nx, ny) if a fast-moving particle may pass into it, -1 if it
        /// is solid, -2 if it is outside an open world.
        /// </summary>
        int Enterable(int nx, int ny)
        {
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) return SidesOpen ? -2 : -1;
            int j = ny * W + nx;
            byte t = Type[j];
            return t == 0 || (Movable[t] && Dens[t] < 1) ? j : -1;
        }

        // -------------------------------------------------------------- step

        public void Step()
        {
            Frame++;
            StepAir();  // also counts the plant cells
            ComputeSunlight();
            ReleaseOrphanedPlantWater(plantCells);
            int f = Frame;
            for (int y = H - 1; y >= 0; y--)
            {
                bool ltr = Rand() < 0.5f;
                int row = y * W;
                for (int k = 0; k < W; k++)
                {
                    int x = ltr ? k : W - 1 - k;
                    int i = row + x;
                    byte t = Type[i];
                    if (t == 0 || stamp[i] == f) continue;
                    Update(t, x, y, i);
                }
            }
            StepPlayers();
        }

        void Update(byte t, int x, int y, int i)
        {
            // A lit explosive counts down its fuse, then goes off.
            if (Explode[t] != 0 && Life[i] > 0)
            {
                if (--Life[i] == 0) Detonate(x, y, i, t);
                return;
            }
            switch (t)
            {
                case E.Powder: UpdPowder(x, y, i); break;
                case E.Mud: UpdMud(x, y, i); break;
                case E.Dust: UpdDust(x, y, i); break;
                case E.Leaf: UpdLeaf(x, y, i); break;
                case E.Stone: case E.Gunpowder: case E.Fireworks:
                    MovePowder(x, y, i, t); break;
                case E.Seed: UpdSeed(x, y, i); break;
                case E.Bomb: UpdBomb(x, y, i); break;
                case E.Water: case E.SaltWater: UpdWater(x, y, i, t); break;
                case E.Oil: case E.Nitro: MoveLiquid(x, y, i, t); break;
                case E.Soapy: UpdSoapy(x, y, i); break;
                case E.Acid: UpdAcid(x, y, i); break;
                case E.Magma: UpdMagma(x, y, i); break;
                case E.Fire: UpdFire(x, y, i); break;
                case E.Gas: MoveGas(x, y, i, t, 0.3f); break;
                case E.Steam: UpdSteam(x, y, i); break;
                case E.Bubble: UpdBubble(x, y, i); break;
                case E.Fan: UpdFan(x, y, i); break;
                case E.Torch: UpdTorch(x, y); break;
                case E.Ice: UpdIce(x, y, i); break;
                case E.Clone: UpdClone(x, y, i); break;
                case E.Virus: UpdVirus(x, y); break;
                case E.Metal: UpdMetal(x, y, i); break;
                case E.Plant: UpdPlant(x, y, i); break;
                case E.SuperBall: UpdSuperBall(x, y, i); break;
                case E.Ant: UpdAnt(x, y, i); break;
                case E.Thunder: UpdThunder(x, y, i); break;
                case E.Laser: UpdLaser(x, y, i); break;
                case E.Spark: UpdSpark(x, y, i); break;
                case E.Rocket: UpdRocket(x, y, i); break;
                case E.Bolt: if (--Life[i] <= 0) Del(i); break;
                case E.Sand: case E.Ash: MovePowder(x, y, i, t); break;
                case E.Salt: UpdSalt(x, y, i); break;
                case E.Snow: UpdSnow(x, y, i); break;
                case E.Wood: UpdWood(x, y, i); break;
                case E.Smoke: if (--Life[i] <= 0) Del(i); else MoveGas(x, y, i, t, 0.6f); break;
                case E.Mercury: UpdMercury(x, y, i); break;
                case E.Cloud: UpdCloud(x, y, i); break;
                case E.Cryo: UpdCryo(x, y, i); break;
                case E.Void: UpdVoid(x, y); break;
            }
        }

        // ---------------------------------------------------------- movement

        /// <summary>Wind nudges light particles. Returns like TryMove.</summary>
        int AirPush(int x, int y, int i, byte t)
        {
            float f = AirF[t];
            if (f == 0) return -1;
            int ai = (y >> 2) * AW + (x >> 2);
            float vx = AirVx[ai], vy = AirVy[ai];
            int mx = 0, my = 0;
            if (Rand() < Math.Abs(vx) * f * 0.4f) mx = vx > 0 ? 1 : -1;
            if (Rand() < Math.Abs(vy) * f * 0.4f) my = vy > 0 ? 1 : -1;
            if (mx == 0 && my == 0) return -1;
            int r = TryMove(i, x + mx, y + my);
            if (r != -1 || mx == 0 || my == 0) return r;
            return TryMove(i, x + mx, y);
        }

        int MovePowder(int x, int y, int i, byte t)
        {
            int r = AirPush(x, y, i, t);
            if (r != -1) return r;
            r = TryMove(i, x, y + 1);
            if (r != -1) return r;
            int d = Rand() < 0.5f ? -1 : 1;
            r = TryMove(i, x + d, y + 1);
            if (r != -1) return r;
            return TryMove(i, x - d, y + 1);
        }

        bool CanFall(int x, int y, byte t)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return false;
            byte tt = Type[y * W + x];
            return tt == 0 || (Movable[tt] && Dens[tt] < Dens[t]);
        }

        int MoveLiquid(int x, int y, int i, byte t)
        {
            int r = AirPush(x, y, i, t);
            if (r != -1) return r;
            r = TryMove(i, x, y + 1);
            if (r != -1) return r;
            int dir = Rand() < 0.5f ? -1 : 1;
            r = TryMove(i, x + dir, y + 1);
            if (r != -1) return r;
            r = TryMove(i, x - dir, y + 1);
            if (r != -1) return r;
            // Spread sideways until blocked or there is room to fall.
            int ci = i, cx = x, disp = Disp[t];
            for (int k = 0; k < disp; k++)
            {
                r = TryMove(ci, cx + dir, y);
                if (r == -2) return -2;
                if (r == -1)
                {
                    if (k == 0)
                    {
                        dir = -dir;
                        r = TryMove(ci, cx + dir, y);
                        if (r == -2) return -2;
                    }
                    if (r == -1) break;
                }
                ci = r;
                cx += dir;
                if (CanFall(cx, y + 1, t)) break;
            }
            return ci == i ? -1 : ci;
        }

        int MoveGas(int x, int y, int i, byte t, float up)
        {
            int r = AirPush(x, y, i, t);
            if (r != -1) return r;
            int dx = Rnd(3) - 1;
            int dy = Rand() < up ? -1 : Rnd(3) - 1;
            if (dx == 0 && dy == 0) return -1;
            return TryMove(i, x + dx, y + dy);
        }

        /// <summary>
        /// Moves a fast particle along (vx, vy), sub-stepping one cell at a time.
        /// Returns its new index or -2 if it left the world; on collision sets
        /// hit, the blocking cell hitJ and which axes were blocked.
        /// </summary>
        int Fly(int x, int y, int i, float vx, float vy)
        {
            hit = false;
            hitJ = -1;
            hitBX = hitBY = false;
            int n = (int)Math.Ceiling(Math.Max(Math.Abs(vx), Math.Abs(vy)));
            if (n == 0) return i;
            float sx = vx / n, sy = vy / n;
            float fx = x + Rand(), fy = y + Rand();
            int cx = x, cy = y, ci = i;
            for (int k = 0; k < n; k++)
            {
                fx += sx;
                fy += sy;
                int nx = (int)Math.Floor(fx), ny = (int)Math.Floor(fy);
                if (nx == cx && ny == cy) continue;
                int j = Enterable(nx, ny);
                if (j >= 0) { Swap(ci, j); ci = j; cx = nx; cy = ny; continue; }
                if (j == -2) { Del(ci); return -2; }
                hit = true;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H) hitJ = ny * W + nx;
                hitBX = nx != cx && Enterable(nx, cy) == -1;
                hitBY = ny != cy && Enterable(cx, ny) == -1;
                if (!hitBX && !hitBY) { hitBX = nx != cx; hitBY = ny != cy; }
                break;
            }
            return ci;
        }

        // ---------------------------------------------------- fire & blasts

        void Ignite(int j)
        {
            byte t = Type[j];
            if (Explode[t] != 0)
            {
                if (Life[j] == 0) Life[j] = (short)(1 + Rnd(3));
                return;
            }
            if (t == E.Fireworks) { Set(j, E.Rocket); return; }
            if (t == E.Plant || t == E.Leaf) ReleasePlantWater(j);
            if (t == E.Ice || t == E.Snow) { Set(j, E.Water); return; }
            if (t == E.Wood) { if (Life[j] == 0) Life[j] = (short)(80 + Rnd(80)); return; }
            Set(j, E.Fire);
            if (t == E.Oil) Life[j] += 60;
            else if (t == E.Gas) Life[j] = (short)(8 + Rnd(8));
        }

        void Detonate(int x, int y, int i, byte t)
        {
            Set(i, E.Fire);
            DoExplode(x, y, Explode[t], Power[t]);
        }

        void DoExplode(int cx, int cy, int r, float power)
        {
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= H) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx;
                    if (x < 0 || x >= W) continue;
                    int j = y * W + x;
                    byte t = Type[j];
                    if (Explode[t] != 0)
                    {
                        if (Life[j] == 0) Life[j] = (short)(1 + Rnd(4));
                        continue;
                    }
                    switch (t)
                    {
                        case E.Empty: case E.Bolt:
                            if (Rand() < 0.5f) { Set(j, E.Fire); Life[j] = (short)(5 + Rnd(15)); }
                            break;
                        case E.Water: if (Rand() < 0.3f) Set(j, E.Steam); break;
                        case E.Ice: case E.Snow: Set(j, E.Water); break;
                        case E.Fireworks: Ignite(j); break;
                        case E.Bubble: Del(j); break;
                        default:
                            if (t == E.Plant || t == E.Leaf) ReleasePlantWater(j);
                            if (Flam[t] != 0)
                            {
                                if (Rand() < 0.7f) { Set(j, E.Fire); Life[j] = (short)(5 + Rnd(15)); }
                                else Del(j);
                            }
                            break;
                    }
                }
            }
            Blast(cx, cy, r, power);
        }

        /// <summary>Pushes air outward from a point.</summary>
        void Blast(int cx, int cy, int r, float power)
        {
            float acx = (float)cx / AS, acy = (float)cy / AS, ar = (float)r / AS + 1.5f;
            int x0 = Math.Max(1, (int)Math.Floor(acx - ar)), x1 = Math.Min(AW - 2, (int)Math.Ceiling(acx + ar));
            int y0 = Math.Max(1, (int)Math.Floor(acy - ar)), y1 = Math.Min(AH - 2, (int)Math.Ceiling(acy + ar));
            for (int ay = y0; ay <= y1; ay++)
            {
                for (int ax = x0; ax <= x1; ax++)
                {
                    float ddx = ax + 0.5f - acx, ddy = ay + 0.5f - acy;
                    float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d > ar) continue;
                    int ai = ay * AW + ax;
                    if (ablock[ai]) continue;
                    float s = power * (1 - d / ar);
                    Pressure[ai] = Clamp(Pressure[ai] + s, -PMax, PMax);
                    if (d > 0.01f)
                    {
                        AirVx[ai] = Clamp(AirVx[ai] + ddx / d * s, -VMax, VMax);
                        AirVy[ai] = Clamp(AirVy[ai] + ddy / d * s, -VMax, VMax);
                    }
                }
            }
        }

        // ---------------------------------------------------------- elements

        void UpdFire(int x, int y, int i)
        {
            if (--Life[i] <= 0)
            {
                if (Rand() < 0.1f) Set(i, E.Smoke); else Del(i);
                return;
            }
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX8[k], ny = y + DY8[k];
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                int j = ny * W + nx;
                byte nt = Type[j];
                if (nt == 0 || nt == E.Fire) continue;
                if (nt == E.Water || nt == E.Soapy || nt == E.SaltWater || nt == E.Cryo)
                {
                    if (nt == E.Cryo) { if (Rand() < 0.1f) Del(j); }
                    else if (Rand() < 0.3f) Boil(j);
                    Del(i);
                    return;
                }
                if (Flam[nt] != 0 && Rand() < Flam[nt]) Ignite(j);
                else if ((nt == E.Ice && Rand() < 0.05f) || (nt == E.Snow && Rand() < 0.2f)) Set(j, E.Water);
            }
            if (Rand() < 0.7f) MoveGas(x, y, i, E.Fire, 0.6f);
        }

        void UpdBomb(int x, int y, int i)
        {
            for (int k = 0; k < 4; k++)
            {
                byte nt = At(x + DX4[k], y + DY4[k]);
                if (nt == 0 || nt == E.Bomb || nt == E.Bolt) continue;
                if (nt == E.Out && (SidesOpen || DY4[k] != 1)) continue;
                Detonate(x, y, i, E.Bomb);
                return;
            }
            MovePowder(x, y, i, E.Bomb);
        }

        void UpdSoapy(int x, int y, int i)
        {
            int r = MoveLiquid(x, y, i, E.Soapy);
            if (r == -2) return;
            if (r >= 0) { i = r; x = r % W; y = r / W; }
            int ai = (y >> 2) * AW + (x >> 2);
            float sp = Math.Abs(AirVx[ai]) + Math.Abs(AirVy[ai]);
            if (sp > 1 && Rand() < 0.02f * sp && At(x, y - 1) == E.Empty) Set(i - W, E.Bubble);
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            if (At(nx, ny) == E.Oil && Rand() < 0.05f) Del(ny * W + nx);
        }

        void UpdAcid(int x, int y, int i)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            byte nt = At(nx, ny);
            if (nt != 0 && nt != E.Out && nt != E.Acid && nt != E.Wall && nt != E.Glass && nt != E.Water &&
                nt != E.Bolt && nt != E.Fire && nt != E.Steam)
            {
                if (Rand() < (nt == E.Metal ? 0.01f : 0.1f))
                {
                    Del(ny * W + nx);
                    if (Rand() < 0.3f) { Del(i); return; }
                }
            }
            MoveLiquid(x, y, i, E.Acid);
        }

        void UpdMagma(int x, int y, int i)
        {
            for (int n = 0; n < 2; n++)
            {
                int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
                byte nt = At(nx, ny);
                if (nt == 0 || nt == E.Out) continue;
                int j = ny * W + nx;
                if (nt == E.Water || nt == E.SaltWater || nt == E.Cryo)
                {
                    if (nt == E.Cryo) Del(j); else Boil(j);
                    if (nt == E.Cryo || Rand() < 0.25f) { Set(i, E.Stone); return; }
                    continue;
                }
                if (nt == E.Ice || nt == E.Snow) Set(j, E.Water);
                else if (nt == E.Sand) { if (Rand() < 0.05f) Set(j, E.Glass); }
                else if (Flam[nt] != 0 && Rand() < Flam[nt]) Ignite(j);
            }
            if (Rand() < 0.01f && At(x, y - 1) == E.Empty) Set(i - W, E.Fire);
            if (Rand() < 0.5f) MoveLiquid(x, y, i, E.Magma);
            else TryMove(i, x, y + 1);
        }

        void UpdBubble(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Del(i); return; }
            if (Rand() < 0.5f) MoveGas(x, y, i, E.Bubble, 0.6f);
        }

        void UpdFan(int x, int y, int i)
        {
            int ai = (y >> 2) * AW + (x >> 2);
            AirVx[ai] += (Vx[i] * FanSpeed - AirVx[ai]) * 0.1f;
            AirVy[ai] += (Vy[i] * FanSpeed - AirVy[ai]) * 0.1f;
        }

        void UpdTorch(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (Rand() < 0.25f && At(x + dx, y - 1) == E.Empty)
                {
                    int j = (y - 1) * W + x + dx;
                    Set(j, E.Fire);
                    Life[j] = (short)(10 + Rnd(20));
                }
            }
        }

        void UpdIce(int x, int y, int i)
        {
            if (Sunlit(x, y) && Rand() < SunRate * 0.5f) { Set(i, E.Water); return; }
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            if (At(nx, ny) == E.Water && Rand() < 0.003f) Set(ny * W + nx, E.Ice);
        }

        /// <summary>Clone remembers the first material that touches it (in Life) and copies it.</summary>
        void UpdClone(int x, int y, int i)
        {
            byte src = (byte)Life[i];
            if (src == 0)
            {
                for (int k = 0; k < 8; k++)
                {
                    byte nt = At(x + DX8[k], y + DY8[k]);
                    if (nt != 0 && nt != E.Out && nt != E.Wall && nt != E.Clone && nt != E.Bolt)
                    {
                        Life[i] = nt;
                        return;
                    }
                }
                return;
            }
            if (Rand() > 0.2f) return;
            int kk = Rnd(8), nx = x + DX8[kk], ny = y + DY8[kk];
            if (At(nx, ny) != E.Empty) return;
            int j = ny * W + nx;
            Set(j, src);
            InitParticle(j, src, DX8[kk], DY8[kk]);
        }

        void UpdVirus(int x, int y)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            byte nt = At(nx, ny);
            if (nt != 0 && nt != E.Out && nt != E.Wall && nt != E.Virus && nt != E.Clone &&
                nt != E.Fire && nt != E.Bolt && Rand() < 0.08f)
            {
                Set(ny * W + nx, E.Virus);
            }
        }

        /// <summary>
        /// Electricity in metal and mercury: Life &gt; 0 is charged, Life &lt; 0 is cooling down.
        /// A charge spreads to touching conductors and ignites flammable neighbours.
        /// </summary>
        void Conduct(int x, int y, int i)
        {
            int l = Life[i];
            if (l > 0)
            {
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + DX4[k], ny = y + DY4[k];
                    byte nt = At(nx, ny);
                    if (nt == 0 || nt == E.Out) continue;
                    int j = ny * W + nx;
                    if (nt == E.Metal || nt == E.Mercury) { if (Life[j] == 0) Life[j] = 10; }
                    else if (Flam[nt] != 0 && Rand() < 0.3f) Ignite(j);
                }
                Life[i] = (short)(l == 1 ? -15 : l - 1);
            }
            else if (l < 0)
            {
                Life[i] = (short)(l + 1);
            }
        }

        void UpdMetal(int x, int y, int i) { Conduct(x, y, i); }

        void UpdSuperBall(int x, int y, int i)
        {
            int ai = (y >> 2) * AW + (x >> 2);
            float vx = Vx[i] + AirVx[ai] * 0.05f;
            float vy = Vy[i] + AirVy[ai] * 0.05f + 0.12f;
            float sp = (float)Math.Sqrt(vx * vx + vy * vy);
            if (sp > 6) { vx *= 6 / sp; vy *= 6 / sp; }
            int ni = Fly(x, y, i, vx, vy);
            if (ni == -2) return;
            if (hitBX) vx = -vx;
            if (hitBY) vy = -vy;
            Vx[ni] = vx;
            Vy[ni] = vy;
        }

        void UpdAnt(int x, int y, int i)
        {
            if (TryMove(i, x, y + 1) != -1) return; // falling
            if (Rand() < 0.5f) return;
            float dir = Vx[i] != 0 ? Vx[i] : 1;
            if (Rand() < 0.01f) dir = -dir;
            Vx[i] = dir;
            int fx = x + (int)dir;
            if (TryMove(i, fx, y) != -1) return;
            if (At(fx, y - 1) == E.Empty && TryMove(i, fx, y - 1) != -1) return; // step up
            byte ft = At(fx, y);
            if ((ft == E.Powder || ft == E.Seed || ft == E.Plant) && Rand() < 0.1f)
            {
                Del(y * W + fx); // dig
                return;
            }
            if (At(x, y - 1) == E.Empty && Rand() < 0.3f) { TryMove(i, x, y - 1); return; }
            Vx[i] = -dir;
        }

        /// <summary>Leaves a short-lived glowing trail cell. kind 0 = lightning, 1 = laser.</summary>
        void SetBolt(int i, int kind)
        {
            Set(i, E.Bolt);
            Vx[i] = kind;
            if (kind == 1) Life[i] = (short)(10 + Rnd(6)); // laser beams linger longer
        }

        void UpdThunder(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Del(i); return; }
            int ci = i, cx = x, cy = y;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + Rnd(3) - 1, ny = cy + 1;
                if (ny >= H || nx < 0 || nx >= W) { Del(ci); return; }
                int j = ny * W + nx;
                byte nt = Type[j];
                if (nt == 0 || (Movable[nt] && Dens[nt] < 1))
                {
                    Swap(ci, j);
                    if (Type[ci] == 0) SetBolt(ci, 0);
                    ci = j; cx = nx; cy = ny;
                    continue;
                }
                if (nt == E.Metal || nt == E.Mercury) { if (Life[j] >= 0) Life[j] = 12; }
                else if (nt == E.Sand) Set(j, E.Glass);
                else if (Flam[nt] != 0 || Explode[nt] != 0) Ignite(j);
                else if (nt == E.Ice) Set(j, E.Water);
                else if (nt == E.Water && Rand() < 0.5f) Set(j, E.Steam);
                SetBolt(ci, 0);
                return;
            }
        }

        void UpdLaser(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Del(i); return; }
            int dx = (int)Vx[i], dy = (int)Vy[i];
            if (dx == 0 && dy == 0) dy = 1;
            int ci = i, cx = x, cy = y;
            for (int k = 0; k < 5; k++)
            {
                int nx = cx + dx, ny = cy + dy;
                int j = Enterable(nx, ny);
                if (j == -2) { Del(ci); return; }
                if (j >= 0)
                {
                    Swap(ci, j);
                    if (Type[ci] == 0) SetBolt(ci, 1);
                    ci = j; cx = nx; cy = ny;
                    continue;
                }
                byte nt = At(nx, ny);
                if (nt == E.Glass) // shine through to the far side
                {
                    int gx = nx, gy = ny;
                    while (At(gx, gy) == E.Glass) { gx += dx; gy += dy; }
                    int g = Enterable(gx, gy);
                    if (g >= 0) { Swap(ci, g); ci = g; cx = gx; cy = gy; continue; }
                }
                if (nt == E.Metal) // mirror
                {
                    bool bx = dx != 0 && Enterable(cx + dx, cy) == -1;
                    bool by = dy != 0 && Enterable(cx, cy + dy) == -1;
                    if (bx) dx = -dx;
                    if (by) dy = -dy;
                    if (!bx && !by) { dx = -dx; dy = -dy; }
                    continue;
                }
                if (nt != E.Out && nt != E.Wall) Heat(ny * W + nx, nt);
                Del(ci);
                return;
            }
            Vx[ci] = dx;
            Vy[ci] = dy;
        }

        void Heat(int j, byte t)
        {
            if (Flam[t] != 0 || Explode[t] != 0) Ignite(j);
            else if (t == E.Ice || t == E.Snow) Set(j, E.Water);
            else if (t == E.Water || t == E.Soapy) { if (Rand() < 0.3f) Set(j, E.Steam); }
            else if (t == E.SaltWater) { if (Rand() < 0.3f) Set(j, E.Salt); }
            else if (t == E.Sand) { if (Rand() < 0.3f) Set(j, E.Glass); }
            else if (t == E.Stone) { if (Rand() < 0.3f) Set(j, E.Magma); }
            else if (State[t] == MoveState.Powder && Rand() < 0.3f) Del(j);
        }

        void UpdSpark(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Del(i); return; }
            float vx = Vx[i] * 0.98f, vy = Vy[i] * 0.98f + 0.05f;
            int ni = Fly(x, y, i, vx, vy);
            if (ni == -2) return;
            if (hit)
            {
                if (hitJ >= 0 && Flam[Type[hitJ]] != 0 && Rand() < 0.3f) Ignite(hitJ);
                Del(ni);
                return;
            }
            Vx[ni] = vx;
            Vy[ni] = vy;
        }

        void UpdRocket(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Burst(x, y, i); return; }
            int ci = i, cx = x, cy = y;
            for (int k = 0; k < 2; k++)
            {
                int nx = cx + (Rand() < 0.15f ? Rnd(3) - 1 : 0), ny = cy - 1;
                int j = Enterable(nx, ny);
                if (j == -2) { Del(ci); return; }
                if (j == -1) { Burst(cx, cy, ci); return; }
                Swap(ci, j);
                if (Type[ci] == 0) { Set(ci, E.Fire); Life[ci] = (short)(4 + Rnd(6)); }
                ci = j; cx = nx; cy = ny;
            }
        }

        /// <summary>Firework explosion: a ring of sparks sharing one colour (stored in Shade).</summary>
        void Burst(int x, int y, int i)
        {
            byte hue = (byte)Rnd(8);
            Del(i);
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    if (dx * dx + dy * dy > 9 || At(x + dx, y + dy) != E.Empty || Rand() > 0.85f) continue;
                    int j = (y + dy) * W + x + dx;
                    Set(j, E.Spark);
                    float d = (float)Math.Sqrt(dx * dx + dy * dy), ux = dx, uy = dy;
                    if (d == 0)
                    {
                        double a = Rand() * Math.PI * 2;
                        ux = (float)Math.Cos(a);
                        uy = (float)Math.Sin(a);
                        d = 1;
                    }
                    float s = 1 + Rand() * 2;
                    Vx[j] = ux / d * s + (Rand() - 0.5f) * 0.5f;
                    Vy[j] = uy / d * s + (Rand() - 0.5f) * 0.5f;
                    Shade[j] = hue;
                }
            }
            Blast(x, y, 4, 0.5f);
        }

        // --------------------------------------------------------------- air

        void StepAir()
        {
            Array.Clear(acount, 0, AN);
            int count = 0, plants = 0;
            for (int y = 0; y < H; y++)
            {
                int row = y * W, arow = (y >> 2) * AW;
                for (int x = 0; x < W; x++)
                {
                    byte t = Type[row + x];
                    if (t == 0) continue;
                    count++;
                    if (t == E.Plant || t == E.Leaf) plants++;
                    if (Elements.Blocks[t]) acount[arow + (x >> 2)]++;
                }
            }
            Count = count;
            plantCells = plants;
            for (int k = 0; k < AN; k++) ablock[k] = acount[k] >= AS * AS / 2;

            float[] vx = AirVx, vy = AirVy, p = Pressure;
            // Pressure builds where air flows in (vx[i] is the flow between i and i+1).
            for (int y = 1; y < AH - 1; y++)
            {
                for (int x = 1; x < AW - 1; x++)
                {
                    int i = y * AW + x;
                    if (ablock[i]) continue;
                    p[i] = p[i] * 0.9995f + (vx[i - 1] - vx[i] + vy[i - AW] - vy[i]) * 0.3f;
                }
            }
            // Air accelerates from high to low pressure.
            for (int y = 1; y < AH - 1; y++)
            {
                for (int x = 1; x < AW - 1; x++)
                {
                    int i = y * AW + x;
                    float ux = vx[i] * 0.999f + (p[i] - p[i + 1]) * 0.4f;
                    float uy = vy[i] * 0.999f + (p[i] - p[i + AW]) * 0.4f;
                    if (ablock[i] || ablock[i + 1]) ux = 0;
                    if (ablock[i] || ablock[i + AW]) uy = 0;
                    vx[i] = ux;
                    vy[i] = uy;
                }
            }
            // Smooth and advect (semi-Lagrangian) into the scratch buffers.
            for (int y = 1; y < AH - 1; y++)
            {
                for (int x = 1; x < AW - 1; x++)
                {
                    int i = y * AW + x;
                    if (ablock[i]) { tvx[i] = 0; tvy[i] = 0; tp[i] = p[i]; continue; }
                    float sx = 0, sy = 0, sp = 0, ws = 0;
                    for (int j = -1; j <= 1; j++)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int n = i + j * AW + k;
                            if (ablock[n]) continue;
                            float w = (j == 0 ? 2 : 1) * (k == 0 ? 2 : 1);
                            sx += vx[n] * w; sy += vy[n] * w; sp += p[n] * w; ws += w;
                        }
                    }
                    sx /= ws; sy /= ws; sp /= ws;
                    float tx = Clamp(x - sx * 0.7f, 0, AW - 1.001f), ty = Clamp(y - sy * 0.7f, 0, AH - 1.001f);
                    int ix = (int)tx, iy = (int)ty;
                    float fx = tx - ix, fy = ty - iy;
                    int a = iy * AW + ix;
                    float ax = sx, ay = sy;
                    if (!ablock[a] && !ablock[a + 1] && !ablock[a + AW] && !ablock[a + AW + 1])
                    {
                        ax = (vx[a] * (1 - fx) + vx[a + 1] * fx) * (1 - fy) + (vx[a + AW] * (1 - fx) + vx[a + AW + 1] * fx) * fy;
                        ay = (vy[a] * (1 - fx) + vy[a + 1] * fx) * (1 - fy) + (vy[a + AW] * (1 - fx) + vy[a + AW + 1] * fx) * fy;
                    }
                    tvx[i] = Clamp(sx * 0.7f + ax * 0.3f, -VMax, VMax);
                    tvy[i] = Clamp(sy * 0.7f + ay * 0.3f, -VMax, VMax);
                    tp[i] = Clamp(sp, -PMax, PMax);
                }
            }
            // Open boundary: edges leak toward their inner neighbour.
            for (int x = 0; x < AW; x++)
            {
                Edge(x, 0, x, 1);
                Edge(x, AH - 1, x, AH - 2);
            }
            for (int y = 1; y < AH - 1; y++)
            {
                Edge(0, y, 1, y);
                Edge(AW - 1, y, AW - 2, y);
            }
            AirVx = tvx; tvx = vx;
            AirVy = tvy; tvy = vy;
            Pressure = tp; tp = p;
        }

        void Edge(int x, int y, int ix, int iy)
        {
            int i = y * AW + x;
            int n = Math.Min(Math.Max(iy, 1), AH - 2) * AW + Math.Min(Math.Max(ix, 1), AW - 2);
            tvx[i] = tvx[n] * 0.9f;
            tvy[i] = tvy[n] * 0.9f;
            tp[i] = tp[n] * 0.9f;
        }

        // ------------------------------------------------------------- tools

        /// <summary>Sets up direction-dependent state of a freshly placed particle.</summary>
        public void InitParticle(int j, byte t, float dirx, float diry)
        {
            switch (t)
            {
                case E.Fan:
                {
                    float d = (float)Math.Sqrt(dirx * dirx + diry * diry);
                    if (d == 0) d = 1;
                    Vx[j] = dirx / d;
                    Vy[j] = diry / d;
                    break;
                }
                case E.Laser:
                    Vx[j] = (float)Math.Round(dirx);
                    Vy[j] = (float)Math.Round(diry);
                    if (Vx[j] == 0 && Vy[j] == 0) Vy[j] = 1;
                    break;
                case E.Ant:
                    Vx[j] = Rand() < 0.5f ? -1 : 1;
                    break;
                case E.SuperBall:
                    Vx[j] = dirx * 2;
                    Vy[j] = diry * 2;
                    break;
            }
        }

        /// <summary>
        /// Fills empty cells within radius r of (cx, cy) with material t.
        /// (dirx, diry) is the unit direction the cursor is moving in.
        /// </summary>
        public void Brush(int cx, int cy, int r, byte t, float dirx, float diry)
        {
            float sparse = Elements.Sparse[t];
            if (sparse == 1) // one particle per call, somewhere in the brush
            {
                double a = Rand() * Math.PI * 2;
                float d = Rand() * r;
                int px = (int)Math.Round(cx + Math.Cos(a) * d), py = (int)Math.Round(cy + Math.Sin(a) * d);
                if (At(px, py) != E.Empty) return;
                int j0 = py * W + px;
                Set(j0, t);
                InitParticle(j0, t, dirx, diry);
                return;
            }
            float fill = sparse != 0 ? sparse : State[t] == MoveState.Static ? 1 : 0.6f;
            float r2 = (r + 0.5f) * (r + 0.5f);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx, y = cy + dy;
                    if (At(x, y) != E.Empty) continue;
                    if (r > 0 && Rand() > fill) continue;
                    int j = y * W + x;
                    Set(j, t);
                    InitParticle(j, t, dirx, diry);
                }
            }
        }

        public void Erase(int cx, int cy, int r)
        {
            float r2 = (r + 0.5f) * (r + 0.5f);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < W && y >= 0 && y < H) Del(y * W + x);
                }
            }
            ErasePlayers(cx, cy, r);
        }

        /// <summary>Calls fn(ai) for every air cell under a brush.</summary>
        void ForAirCells(int cx, int cy, int r, Action<int> fn)
        {
            float acx = (float)cx / AS, acy = (float)cy / AS, ar = (float)r / AS + 1;
            int y0 = Math.Max(0, (int)Math.Floor(acy - ar)), y1 = Math.Min(AH - 1, (int)Math.Ceiling(acy + ar));
            int x0 = Math.Max(0, (int)Math.Floor(acx - ar)), x1 = Math.Min(AW - 1, (int)Math.Ceiling(acx + ar));
            for (int ay = y0; ay <= y1; ay++)
            {
                for (int ax = x0; ax <= x1; ax++)
                {
                    float ddx = ax + 0.5f - acx, ddy = ay + 0.5f - acy;
                    if (ddx * ddx + ddy * ddy <= ar * ar) fn(ay * AW + ax);
                }
            }
        }

        public void Wind(int cx, int cy, int r, float dx, float dy)
        {
            ForAirCells(cx, cy, r, ai =>
            {
                AirVx[ai] = Clamp(AirVx[ai] + dx * 0.3f, -VMax, VMax);
                AirVy[ai] = Clamp(AirVy[ai] + dy * 0.3f, -VMax, VMax);
            });
        }

        public void AirBlast(int cx, int cy, int r)
        {
            ForAirCells(cx, cy, r, ai => { Pressure[ai] = Clamp(Pressure[ai] + 2, -PMax, PMax); });
        }

        /// <summary>Drags particles under the brush along with the cursor.</summary>
        public void Drag(int cx, int cy, int r, float fdx, float fdy)
        {
            int dx = (int)Math.Round(fdx), dy = (int)Math.Round(fdy);
            if (dx == 0 && dy == 0) return;
            float r2 = (r + 0.5f) * (r + 0.5f);
            var cells = new List<int>();
            for (int oy = -r; oy <= r; oy++)
            {
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r2) continue;
                    byte t = At(cx + ox, cy + oy);
                    if (t != 0 && t != E.Out && t != E.Wall) cells.Add((cy + oy) * W + cx + ox);
                }
            }
            // Move the cells furthest along the drag direction first.
            cells.Sort((a, b) => (b % W * dx + b / W * dy).CompareTo(a % W * dx + a / W * dy));
            foreach (int j in cells)
            {
                int nx = j % W + dx, ny = j / W + dy;
                if (At(nx, ny) == E.Empty) Swap(j, ny * W + nx);
            }
        }

        // --------------------------------------------------------- save/load

        /// <summary>Shifts a loaded figure by (ox, oy); false if its body no longer fits in the world.</summary>
        bool Place(Player p, int ox, int oy)
        {
            p.X += ox;
            p.Y += oy;
            int px = (int)Math.Floor(p.X), py = (int)Math.Floor(p.Y);
            return px - Player.HalfWidth >= 0 && px + Player.HalfWidth < W && py - Player.Height + 1 >= 0 && py < H;
        }

        const int SaveMagic = 0x52445750; // "PWDR"
        const int SaveVersion = 4; // 2 added players, 3 fighters, 4 plant water

        public void Save(Stream stream)
        {
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                w.Write(SaveMagic);
                w.Write(SaveVersion);
                w.Write(W);
                w.Write(H);
                w.Write(Type);
                for (int i = 0; i < N; i++) w.Write(Life[i]);
                for (int i = 0; i < N; i++) w.Write(Vx[i]);
                for (int i = 0; i < N; i++) w.Write(Vy[i]);
                for (int s = 0; s < MaxPlayers; s++)
                {
                    Player p = Players[s];
                    w.Write(p != null);
                    if (p == null) continue;
                    w.Write(p.X);
                    w.Write(p.Y);
                    w.Write(p.Held);
                }
                w.Write(Fighters.Count);
                foreach (Player f in Fighters)
                {
                    w.Write(f.X);
                    w.Write(f.Y);
                    w.Write(f.Hp);
                }
                w.Write(PlantWater);
            }
        }

        /// <summary>
        /// Loads a save. Saves of a different size are anchored to the floor and centred
        /// horizontally; anything that doesn't fit is cropped.
        /// </summary>
        public void Load(Stream stream)
        {
            using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                if (r.ReadInt32() != SaveMagic) throw new InvalidDataException("Not a save file");
                int version = r.ReadInt32();
                int sw = r.ReadInt32(), sh = r.ReadInt32();
                if (version < 1 || version > SaveVersion || sw <= 0 || sh <= 0 || sw > 8192 || sh > 8192)
                    throw new InvalidDataException("Incompatible save file");
                int sn = sw * sh;
                byte[] types = r.ReadBytes(sn);
                if (types.Length != sn) throw new EndOfStreamException();
                var life = new short[sn];
                var vx = new float[sn];
                var vy = new float[sn];
                for (int i = 0; i < sn; i++) life[i] = r.ReadInt16();
                for (int i = 0; i < sn; i++) vx[i] = r.ReadSingle();
                for (int i = 0; i < sn; i++) vy[i] = r.ReadSingle();
                var players = new Player[MaxPlayers];
                if (version >= 2)
                {
                    for (int s = 0; s < MaxPlayers; s++)
                    {
                        if (!r.ReadBoolean()) continue;
                        players[s] = new Player { X = r.ReadSingle(), Y = r.ReadSingle(), Held = r.ReadByte() };
                    }
                }
                var fighters = new List<Player>();
                if (version >= 3)
                {
                    int count = r.ReadInt32();
                    if (count < 0 || count > MaxFighters) throw new InvalidDataException("Bad fighter count");
                    for (int k = 0; k < count; k++)
                    {
                        fighters.Add(new Player
                        {
                            X = r.ReadSingle(), Y = r.ReadSingle(), Hp = r.ReadInt32(),
                            Fighter = true, Speed = FighterSpeed,
                        });
                    }
                }
                int plantWater = version >= 4 ? r.ReadInt32() : 0;

                Clear();
                ClearAir();
                int ox = (W - sw) / 2, oy = H - sh;
                for (int sy = 0; sy < sh; sy++)
                {
                    int y = sy + oy;
                    if (y < 0 || y >= H) continue;
                    for (int sx = 0; sx < sw; sx++)
                    {
                        int x = sx + ox;
                        if (x < 0 || x >= W) continue;
                        int i = y * W + x, j = sy * sw + sx;
                        Type[i] = types[j];
                        Life[i] = life[j];
                        Vx[i] = vx[j];
                        Vy[i] = vy[j];
                    }
                }
                for (int i = 0; i < N; i++) Shade[i] = (byte)Rnd(256);
                for (int s = 0; s < MaxPlayers; s++)
                {
                    if (players[s] != null && Place(players[s], ox, oy)) Players[s] = players[s];
                }
                foreach (Player f in fighters)
                {
                    if (Place(f, ox, oy)) Fighters.Add(f);
                }
                PlantWater = plantWater;
            }
        }
    }
}
