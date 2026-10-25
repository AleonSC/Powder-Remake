// Seeds and the trees they grow into.
using System;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        const int SeedEnergy = 30;

        static bool IsSoil(byte t) { return t == E.Powder || t == E.Ash; }

        /// <summary>Falls like powder; sprouts once it is buried in powder (or ash) with water nearby.</summary>
        void UpdSeed(int x, int y, int i)
        {
            if (MovePowder(x, y, i, E.Seed) != -1 || Rand() > 0.05f) return;
            int soil = 0;
            for (int k = 0; k < 8; k++) if (IsSoil(At(x + DX8[k], y + DY8[k]))) soil++;
            if (soil < 4) return; // needs to be covered, not just sitting on top
            int water = FindNear(x, y, 3, E.Water);
            if (water < 0) return;
            Del(water);
            Set(i, E.Plant);
            Life[i] = (short)(SeedEnergy + Rnd(15));
            Vy[i] = -1;
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
        /// A tree. Life &gt; 0 marks a growing tip carrying that much energy and heading in
        /// direction (Vx, Vy). Any other part of the tree that touches water drinks it and
        /// buds a new branch there, so water makes it grow more.
        /// </summary>
        void UpdPlant(int x, int y, int i)
        {
            if (Life[i] > 0)
            {
                if (Rand() < 0.25f) Grow(x, y, i);
                return;
            }
            int k = Rnd(8), wx = x + DX8[k], wy = y + DY8[k];
            if (At(wx, wy) == E.Water && Rand() < 0.03f)
            {
                Del(wy * W + wx);
                Life[i] = (short)(6 + Rnd(8));
                SetDirection(i, -Math.PI / 2 + (Rand() - 0.5f) * 2.4f); // mostly upward
            }
        }

        void SetDirection(int i, double angle)
        {
            Vx[i] = (float)Math.Cos(angle);
            Vy[i] = (float)Math.Sin(angle);
        }

        void Grow(int x, int y, int i)
        {
            int energy = Life[i];
            // Wander a little, with a pull upwards.
            double a = Math.Atan2(Vy[i], Vx[i]) + (Rand() - 0.5f) * 0.8f;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a) - 0.15f;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            dx /= len;
            dy /= len;
            a = Math.Atan2(dy, dx);

            int nx = x + (int)Math.Round(dx), ny = y + (int)Math.Round(dy);
            if (!CanGrowInto(nx, ny, x, y))
            {
                // Blocked: try another way next time, at a small cost.
                SetDirection(i, -Math.PI / 2 + (Rand() - 0.5f) * 3);
                Life[i] = (short)(energy - 1);
                if (Life[i] == 0) Leaves(x, y, 0.4f);
                return;
            }

            int j = ny * W + nx;
            if (Type[j] == E.Water) energy += 4; // drinks as it grows
            Set(j, E.Plant);
            Life[i] = 0;
            energy--;
            if (energy <= 0) { Leaves(nx, ny, 0.5f); return; }

            // Fork: the old cell becomes a second tip heading off at an angle.
            if (energy > 6 && Rand() < 0.12f)
            {
                int half = energy / 2;
                energy -= half;
                Life[i] = (short)half;
                SetDirection(i, a + (Rand() < 0.5f ? 1 : -1) * (0.6f + Rand() * 0.6f));
            }
            Life[j] = (short)energy;
            Vx[j] = dx;
            Vy[j] = dy;
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
    }
}
