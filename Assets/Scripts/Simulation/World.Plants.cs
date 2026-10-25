// Seeds, the trees they grow into, and the dust overgrown branches crumble into.
using System;
using System.Collections.Generic;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        const int SeedEnergy = 30;
        /// <summary>Branch cells further than this from the root can snap off under their own weight.</summary>
        public const int SnapLength = 45;
        /// <summary>Most cells one snap can break off.</summary>
        const int MaxSnap = 400;

        /// <summary>
        /// Water held inside living plants (drunk from pools and damp soil). Plants give
        /// it back as steam from their leaves, as damp soil when their dust decomposes,
        /// and as steam when they burn, so water is never lost.
        /// </summary>
        public int PlantWater;

        static bool IsSoil(byte t) { return t == E.Powder || t == E.Ash || t == E.Mud || t == E.Dust; }

        /// <summary>
        /// Falls like powder; sprouts once it is buried in soil (powder, ash or dust) and
        /// has water: damp soil (mud) around it, or a pool within a few dots.
        /// </summary>
        void UpdSeed(int x, int y, int i)
        {
            if (MovePowder(x, y, i, E.Seed) != -1 || Rand() > 0.05f) return;
            int soil = 0, mud = -1;
            for (int k = 0; k < 8; k++)
            {
                byte t = At(x + DX8[k], y + DY8[k]);
                if (IsSoil(t)) soil++;
                if (t == E.Mud) mud = (y + DY8[k]) * W + x + DX8[k];
            }
            if (soil < 4) return; // needs to be covered, not just sitting on top
            if (mud >= 0) Set(mud, E.Powder); // drink from the damp soil
            else
            {
                int water = FindNear(x, y, 3, E.Water);
                if (water < 0) return;
                Del(water);
            }
            PlantWater++;
            Set(i, E.Plant);
            Life[i] = (short)(SeedEnergy + Rnd(15));
            Vx[i] = 0;                      // distance from the root
            Vy[i] = (float)(-Math.PI / 2);  // growing straight up
        }

        /// <summary>Index of a cell of type t within r dots of (x, y), or -1.</summary>
        int FindNear(int x, int y, int r, byte t)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (At(x + dx, y + dy) == t) return (y + dy) * W + x + dx;
            return -1;
        }

        /// <summary>
        /// A tree cell. Vx is its distance along the branches from the root. Life &gt; 0 marks
        /// a growing tip carrying that much energy and heading at angle Vy. Any other part
        /// of the tree that touches water or damp soil drinks it and buds a new branch
        /// there, so water makes it grow more. Overhanging parts too far from the root snap.
        /// </summary>
        void UpdPlant(int x, int y, int i)
        {
            if (Life[i] > 0)
            {
                if (Rand() < 0.25f) Grow(x, y, i);
                return;
            }
            int k = Rnd(8), wx = x + DX8[k], wy = y + DY8[k];
            byte wt = At(wx, wy);
            if ((wt == E.Water || wt == E.Mud) && Rand() < 0.03f)
            {
                if (wt == E.Mud) Set(wy * W + wx, E.Powder); else Del(wy * W + wx);
                PlantWater++;
                Life[i] = (short)(6 + Rnd(8));
                Vy[i] = (float)(-Math.PI / 2 + (Rand() - 0.5f) * 2.4f); // mostly upward
                return;
            }
            if (Vx[i] >= SnapLength && Rand() < 0.0005f && Unsupported(x, y)) Snap(x, y, i);
        }

        /// <summary>Nothing solid underneath: the branch is hanging in the air (or water).</summary>
        bool Unsupported(int x, int y)
        {
            byte t = At(x, y + 1);
            return t == E.Empty || State[t] == MoveState.Gas || State[t] == MoveState.Liquid;
        }

        void Grow(int x, int y, int i)
        {
            int energy = Life[i];
            float length = Vx[i];
            // Wander a little, with a pull upwards.
            double a = Vy[i] + (Rand() - 0.5f) * 0.8f;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a) - 0.15f;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            dx /= len;
            dy /= len;
            a = Math.Atan2(dy, dx);

            int nx = x + (int)Math.Round(dx), ny = y + (int)Math.Round(dy);
            if (!CanGrowInto(nx, ny, x, y))
            {
                // Blocked: try another way next time, at a small cost.
                Vy[i] = (float)(-Math.PI / 2 + (Rand() - 0.5f) * 3);
                Life[i] = (short)(energy - 1);
                if (Life[i] == 0) Leaves(x, y, 0.4f);
                return;
            }

            int j = ny * W + nx;
            byte drunk = Type[j];
            if (drunk == E.Water || drunk == E.Mud) // drinks as it grows
            {
                energy += 4;
                PlantWater++;
            }
            Set(j, E.Plant);
            Vx[j] = length + 1;
            Life[i] = 0;
            energy--;
            if (energy <= 0) { Leaves(nx, ny, 0.5f); return; }

            // Fork: the old cell becomes a second tip heading off at an angle.
            if (energy > 6 && Rand() < 0.12f)
            {
                int half = energy / 2;
                energy -= half;
                Life[i] = (short)half;
                Vy[i] = (float)(a + (Rand() < 0.5f ? 1 : -1) * (0.6f + Rand() * 0.6f));
            }
            Life[j] = (short)energy;
            Vy[j] = (float)a;
            if (Rand() < 0.08f) Leaves(x, y, 0.15f);
        }

        /// <summary>Branches stay thin: the target may touch at most one tree cell besides its parent.</summary>
        bool CanGrowInto(int nx, int ny, int fromX, int fromY)
        {
            byte t = At(nx, ny);
            if (t != E.Empty && t != E.Water && t != E.Leaf && !IsSoil(t)) return false;
            int n = 0;
            for (int k = 0; k < 8; k++)
            {
                int px = nx + DX8[k], py = ny + DY8[k];
                if (px == fromX && py == fromY) continue;
                if (At(px, py) == E.Plant) n++;
            }
            return n <= 1;
        }

        void Leaves(int x, int y, float chance)
        {
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX8[k], ny = y + DY8[k];
                if (At(nx, ny) == E.Empty && Rand() < chance) Set(ny * W + nx, E.Leaf);
            }
        }

        /// <summary>Leaves breathe out the water the plants have drunk, as steam.</summary>
        void UpdLeaf(int x, int y, int i)
        {
            if (PlantWater > 0 && Rand() < 0.002f && At(x, y - 1) == E.Empty)
            {
                Set(i - W, E.Steam);
                PlantWater--;
            }
        }

        readonly Queue<int> snapQueue = new Queue<int>();
        readonly List<int> snapped = new List<int>();

        /// <summary>
        /// Breaks the branch at (x, y): this cell and everything further out along the
        /// branch, with its leaves, crumbles into falling dust. Sometimes it drops a seed.
        /// </summary>
        void Snap(int x, int y, int i)
        {
            snapped.Clear();
            snapQueue.Clear();
            snapQueue.Enqueue(i);
            Type[i] = E.Dust; // mark as visited
            while (snapQueue.Count > 0 && snapped.Count < MaxSnap)
            {
                int c = snapQueue.Dequeue();
                snapped.Add(c);
                int cx = c % W, cy = c / W;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX8[k], ny = cy + DY8[k];
                    byte t = At(nx, ny);
                    int n = ny * W + nx;
                    // Further out along the branch, or a leaf hanging off it.
                    if ((t == E.Plant && Vx[n] > Vx[c]) || t == E.Leaf)
                    {
                        Type[n] = E.Dust;
                        if (t == E.Plant) snapQueue.Enqueue(n); else snapped.Add(n);
                    }
                }
            }
            foreach (int c in snapQueue) snapped.Add(c);
            foreach (int c in snapped) Set(c, E.Dust);
            if (Rand() < 0.4f) Set(snapped[Rnd(snapped.Count)], E.Seed); // fruit
        }

        /// <summary>
        /// Crumbled plant matter: falls and blows about like light powder, soaks up water
        /// like powder, and slowly decomposes into soil, damp if the plants are holding
        /// water. Seeds grow in it.
        /// </summary>
        void UpdDust(int x, int y, int i)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            if (At(nx, ny) == E.Water && Rand() < 0.05f)
            {
                Del(ny * W + nx);
                Set(i, E.Mud);
                return;
            }
            if (MovePowder(x, y, i, E.Dust) != -1) return;
            if (Rand() < 0.001f)
            {
                if (PlantWater > 0) { PlantWater--; Set(i, E.Mud); }
                else Set(i, E.Powder);
            }
        }

        /// <summary>A burning plant gives off the water it held as steam.</summary>
        void ReleasePlantWater(int j)
        {
            if (PlantWater <= 0) return;
            int x = j % W, y = j / W;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX8[k], ny = y + DY8[k];
                if (At(nx, ny) != E.Empty) continue;
                Set(ny * W + nx, E.Steam);
                PlantWater--;
                return;
            }
        }

        /// <summary>
        /// With no plants left, nothing can give their water back, so it returns to the
        /// air as steam, a drop per frame wherever there is room.
        /// </summary>
        void ReleaseOrphanedPlantWater(int plantCells)
        {
            if (plantCells > 0 || PlantWater <= 0) return;
            for (int tries = 0; tries < 20; tries++)
            {
                int j = Rnd(N);
                if (Type[j] != E.Empty) continue;
                Set(j, E.Steam);
                PlantWater--;
                return;
            }
        }
    }
}
