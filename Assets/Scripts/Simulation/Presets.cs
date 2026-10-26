// Ready-made scenes that show off what the simulation can do. Each is built to
// fit whatever size the world is: centred, standing on the floor, and spread
// across the width where that makes sense.
using System;

namespace PowderGame.Simulation
{
    public static class Presets
    {
        public static readonly string[] Names =
        {
            "TERRARIUM", "WATER CYCLE", "FOUNTAIN", "U-TUBES", "WATERFALL", "VOLCANO", "FIREWORKS",
            "LASER LAB", "ARENA", "DEMOLITION", "BLACK HOLE", "SALT FLATS", "FOREST", "METAL DROP",
            "THUNDERSTORM",
        };

        /// <summary>Clears the world and builds preset number index (wrapping around).</summary>
        public static void Load(World w, int index)
        {
            index = ((index % Names.Length) + Names.Length) % Names.Length;
            w.Clear();
            w.ClearAir();
            w.Sun = World.SunLevel.Off;
            w.SidesOpen = false;
            var b = new Builder(w);
            switch (index)
            {
                case 0: Terrarium(b); break;
                case 1: WaterCycle(b); break;
                case 2: Fountain(b); break;
                case 3: UTubes(b); break;
                case 4: Waterfall(b); break;
                case 5: Volcano(b); break;
                case 6: Fireworks(b); break;
                case 7: LaserLab(b); break;
                case 8: Arena(b); break;
                case 9: Demolition(b); break;
                case 10: BlackHole(b); break;
                case 11: SaltFlats(b); break;
                case 12: Forest(b); break;
                case 13: MetalDrop(b); break;
                case 14: Thunderstorm(b); break;
            }
        }

        /// <summary>A sealed glass box with soil, a pond and a seed. Sunlight keeps its water cycling.</summary>
        static void Terrarium(Builder b)
        {
            int x0 = b.Cx - 90, x1 = b.Cx + 90, top = b.H - 120;
            b.Fill(x0, x1, top, top + 2, E.Glass);
            b.Fill(x0, x0 + 2, top, b.H, E.Glass);
            b.Fill(x1 - 2, x1, top, b.H, E.Glass);
            b.Fill(x0 + 2, x1 - 2, b.H - 25, b.H, E.Dirt);
            b.Fill(x0 + 2, x0 + 72, b.H - 14, b.H - 12, E.Glass);   // a glass-lined pond, so it doesn't just soak away
            b.Fill(x0 + 70, x0 + 72, b.H - 27, b.H - 12, E.Glass);
            b.Fill(x0 + 2, x0 + 70, b.H - 27, b.H - 14, E.Water);
            b.Seed(b.Cx + 40, b.H - 21);
            b.W.Sun = World.SunLevel.High;
        }

        /// <summary>A lake beside a hill of soil with seeds in it, under the sun.</summary>
        static void WaterCycle(Builder b)
        {
            int lakeEdge = b.Width * 2 / 5;
            b.Fill(0, b.Width, b.H - 8, b.H, E.Dirt);
            for (int x = lakeEdge; x < b.Width; x++)
            {
                double t = Math.Abs(x - b.Width * 0.72) / (b.Width * 0.3);
                int height = 15 + (int)(70 * Math.Max(0, 1 - t));
                b.Fill(x, x + 1, b.H - 8 - height, b.H - 8, E.Dirt);
            }
            b.Fill(0, lakeEdge, b.H - 40, b.H - 8, E.Water);
            for (int k = 0; k < 4; k++)
            {
                int x = lakeEdge + 30 + k * (b.Width - lakeEdge - 50) / 4;
                b.Seed(x, b.SurfaceY(x) + 4);
            }
            b.W.Sun = World.SunLevel.High;
        }

        /// <summary>A tall tank feeding a pipe whose mouth is far below the tank's surface.</summary>
        static void Fountain(Builder b)
        {
            int a = b.Cx - 110, tankRight = b.Cx - 50, pipe = b.Cx - 20, top = b.H - 200, mouth = b.H - 80;
            b.Fill(a - 2, a, top, b.H, E.Wall);                    // tank walls
            b.Fill(tankRight, tankRight + 2, top, b.H - 12, E.Wall);
            b.Fill(a - 2, pipe + 6, b.H - 2, b.H, E.Wall);         // floor
            b.Fill(tankRight, pipe - 2, b.H - 14, b.H - 12, E.Wall); // tunnel roof
            b.Fill(pipe - 2, pipe, mouth, b.H - 12, E.Wall);        // pipe walls
            b.Fill(pipe + 4, pipe + 6, mouth, b.H - 2, E.Wall);
            b.Fill(a, tankRight, top + 10, b.H - 2, E.Water);
            b.Fill(tankRight, pipe + 4, b.H - 12, b.H - 2, E.Water);
        }

        /// <summary>Three connected arms of different widths, water poured into one.</summary>
        static void UTubes(Builder b)
        {
            int[] widths = { 12, 40, 6 };
            const int gap = 30;
            int total = widths[0] + widths[1] + widths[2] + 2 * gap;
            int x = b.Cx - total / 2, top = b.H - 200, channel = b.H - 30;
            b.Fill(x - 2, x + total + 2, top, b.H, E.Wall);
            int ax = x;
            foreach (int width in widths)
            {
                b.Fill(ax, ax + width, top, b.H - 2, E.Empty);
                ax += width + gap;
            }
            b.Fill(x, x + total, channel, b.H - 2, E.Water);          // the channel joining the arms...
            b.Fill(x, x + widths[0], top + 10, channel, E.Water);     // ...and water poured into the first arm
        }

        /// <summary>Water from a spring runs along a ledge, arcs off the end, and spills off the screen.</summary>
        static void Waterfall(Builder b)
        {
            int ledge = b.H - 150;
            b.W.SidesOpen = true;
            b.Fill(0, b.Cx, ledge, ledge + 4, E.Wall);
            b.Fill(0, 4, ledge - 40, ledge, E.Wall);
            b.Spring(4, ledge - 20, 4, 20, E.Water);
            b.Fill(b.Cx + 20, b.Cx + 140, b.H - 2, b.H, E.Wall); // a pool under the falls
            b.Fill(b.Cx + 20, b.Cx + 22, b.H - 30, b.H, E.Wall);
            b.Fill(b.Cx + 138, b.Cx + 140, b.H - 30, b.H, E.Wall);
            b.Fill(b.Cx + 22, b.Cx + 138, b.H - 20, b.H - 2, E.Dirt);
            b.Seed(b.Cx + 80, b.H - 16);
        }

        /// <summary>A rock mountain with lava welling out of its crater and running down into lakes.</summary>
        static void Volcano(Builder b)
        {
            int peak = b.H - 140, half = 130;
            for (int x = b.Cx - half; x <= b.Cx + half; x++)
            {
                int y = peak + (int)((double)Math.Abs(x - b.Cx) / half * (b.H - peak));
                b.Fill(x, x + 1, y, b.H, E.Wall);
            }
            b.Fill(b.Cx - 25, b.Cx + 25, b.H - 40, b.H - 10, E.Magma); // chamber
            b.Fill(b.Cx - 3, b.Cx + 3, peak, b.H - 40, E.Magma);       // vent
            b.Spring(b.Cx - 3, peak + 1, 6, 1, E.Magma);                 // welling up into the crater
            b.Fill(b.Cx - 3, b.Cx + 3, peak - 4, peak + 1, E.Empty);
            b.Fill(0, b.Cx - half, b.H - 30, b.H, E.Water);
            b.Fill(b.Cx + half + 1, b.Width, b.H - 30, b.H, E.Water);
            b.W.Sun = World.SunLevel.Low;
        }

        /// <summary>A burning fuse along the ground sets off fireworks one after another.</summary>
        static void Fireworks(Builder b)
        {
            int ground = b.H - 10;
            b.Fill(0, b.Width, ground, b.H, E.Dirt);
            b.Fill(10, 12, ground - 2, ground, E.Torch);
            b.Fill(12, b.Width - 10, ground - 1, ground, E.Gunpowder);
            for (int x = 40; x < b.Width - 20; x += 35) b.Fill(x, x + 4, ground - 4, ground - 1, E.Fireworks);
        }

        /// <summary>A laser emitter, metal mirrors, a glass pane and targets that burn and melt.</summary>
        static void LaserLab(Builder b)
        {
            int x0 = b.Cx - 180, x1 = b.Cx + 180, top = b.H - 220;
            b.Box(x0, top, x1, b.H - 1, 3, E.Wall);
            int y = top + 60;
            // Emitter: a clone cell copying lasers, boxed in except to the right.
            b.Fill(x0 + 3, x0 + 12, y - 4, y + 5, E.Wall);
            b.Spring(x0 + 10, y, 1, 1, E.Laser);
            b.Fill(x0 + 11, x0 + 12, y, y + 1, E.Empty);
            // A diagonal mirror sends it down to the floor, a pane of glass lets it through on the way.
            b.Line(b.Cx - 20, y - 20, b.Cx + 20, y + 20, 0, E.Metal); // one dot thick, so it reads as a clean diagonal
            b.Fill(b.Cx - 21, b.Cx - 19, y - 24, y - 20, E.Wall);
            b.Fill(b.Cx - 80, b.Cx - 77, top + 3, y + 30, E.Glass);
            b.Fill(b.Cx - 30, b.Cx + 30, b.H - 30, b.H - 4, E.Sand);
            b.Fill(b.Cx + 60, b.Cx + 90, b.H - 20, b.H - 4, E.Ice);
            b.Fill(b.Cx + 120, b.Cx + 150, b.H - 12, b.H - 4, E.Gunpowder);
        }

        /// <summary>A walled ring with player 1 against a crowd of fighters.</summary>
        static void Arena(Builder b)
        {
            int x0 = b.Cx - 160, x1 = b.Cx + 160;
            b.Box(x0, b.H - 160, x1, b.H - 1, 3, E.Wall);
            b.Fill(b.Cx - 20, b.Cx + 20, b.H - 10, b.H - 4, E.Sand);
            b.W.AddPlayer(x0 + 30, b.H - 5);
            for (int k = 0; k < 6; k++) b.W.AddFighter(x1 - 30 - k * 12, b.H - 5);
        }

        /// <summary>A metal girder and wooden planks on wooden pillars packed with C-4, with a lit fuse.</summary>
        static void Demolition(Builder b)
        {
            int ground = b.H - 6, x0 = b.Cx - 120;
            b.Fill(0, b.Width, ground, b.H, E.Wall);
            for (int k = 0; k < 4; k++)
            {
                int px = x0 + k * 80;
                b.Fill(px, px + 16, ground - 60, ground, E.Wood);
                b.Fill(px + 5, px + 11, ground - 12, ground - 4, E.C4);
            }
            b.Fill(x0 - 4, x0 + 260, ground - 64, ground - 60, E.Metal);
            b.Fill(x0 + 10, x0 + 250, ground - 70, ground - 64, E.Wood);
            b.Fill(x0 + 40, x0 + 220, ground - 90, ground - 70, E.Sand);
            b.Fill(x0 - 40, x0 - 38, ground - 2, ground, E.Torch);
            b.Fill(x0 - 38, x0 + 260, ground - 1, ground, E.Gunpowder);
        }

        /// <summary>A small black hole swallowing the sand and water pouring down around it.</summary>
        static void BlackHole(Builder b)
        {
            int cy = b.H / 2;
            b.Disc(b.Cx, cy, 6, E.Void);
            b.Spring(b.Cx - 30, 20, 6, 3, E.Sand);
            b.Spring(b.Cx + 24, 20, 6, 3, E.Water);
            b.Fill(b.Cx - 60, b.Cx + 60, b.H - 20, b.H, E.Dirt);
        }

        /// <summary>Shallow pans of salt water drying out in strong sun.</summary>
        static void SaltFlats(Builder b)
        {
            int ground = b.H - 10;
            b.Fill(0, b.Width, ground, b.H, E.Wall);
            for (int x = 20; x + 60 < b.Width; x += 80)
            {
                b.Fill(x, x + 60, ground - 2, ground, E.Wall);
                b.Fill(x, x + 2, ground - 8, ground, E.Wall);
                b.Fill(x + 58, x + 60, ground - 8, ground, E.Wall);
                b.Fill(x + 2, x + 58, ground - 6, ground - 2, E.SaltWater);
            }
            b.W.Sun = World.SunLevel.High;
        }

        /// <summary>Rolling hills of soil full of seeds, under rain clouds.</summary>
        static void Forest(Builder b)
        {
            for (int x = 0; x < b.Width; x++)
            {
                int height = 25 + (int)(15 * Math.Sin(x * 0.03) + 8 * Math.Sin(x * 0.011 + 1));
                b.Fill(x, x + 1, b.H - height, b.H, E.Dirt);
            }
            for (int x = 25; x < b.Width - 10; x += 45) b.Seed(x, b.SurfaceY(x) + 4);
            for (int x = 30; x < b.Width - 60; x += 110) b.Cloud(x, 25 + (x / 110 % 2) * 10, 50, 8, World.CloudCapacity / 2); // rain, not storms
            b.W.Sun = World.SunLevel.Low;
        }

        /// <summary>Metal, glass, wood and ice shapes left in mid-air: they fall, sink, float or hang.</summary>
        static void MetalDrop(Builder b)
        {
            int ground = b.H - 4;
            b.Fill(0, b.Width, ground, b.H, E.Wall);
            int pool = b.Cx + 40;
            b.Fill(pool, pool + 2, ground - 60, ground, E.Wall);
            b.Fill(pool + 150, pool + 152, ground - 60, ground, E.Wall);
            b.Fill(pool + 2, pool + 150, ground - 50, ground, E.Water);
            b.Fill(b.Cx - 170, b.Cx - 90, ground - 30, ground, E.Dirt);
            b.W.DrawShape(Shape.Curve, b.Cx - 170, 80, b.Cx - 90, 80, b.Cx - 130, 50, 1, E.Metal, false);
            b.W.DrawShape(Shape.Box, b.Cx - 60, 60, b.Cx - 10, 100, 0, 0, 1, E.Glass, false);
            b.W.DrawShape(Shape.Circle, pool + 40, 70, pool + 52, 70, 0, 0, 1, E.Wood, false);
            b.Fill(pool + 90, pool + 120, 60, 70, E.Metal);
            b.Fill(pool + 60, pool + 80, 100, 106, E.Ice);
            b.Fill(b.Cx + 10, b.Cx + 12, 20, 24, E.Wall);              // a hook...
            b.Fill(b.Cx + 4, b.Cx + 18, 24, 60, E.Metal);              // ...with a metal weight welded to it
        }

        /// <summary>Wet storm clouds over a metal lightning rod with gunpowder at its foot.</summary>
        static void Thunderstorm(Builder b)
        {
            int ground = b.H - 6;
            b.Fill(0, b.Width, ground, b.H, E.Wall);
            b.Fill(b.Cx - 1, b.Cx + 2, ground - 180, ground, E.Metal);
            b.Fill(b.Cx + 2, b.Cx + 30, ground - 8, ground, E.Gunpowder);
            b.Fill(b.Cx - 30, b.Cx - 1, ground - 8, ground, E.Wood);
            for (int x = 10; x < b.Width - 60; x += 70) b.Cloud(x, 20 + (x / 70 % 2) * 6, 60, 10, World.CloudCapacity);
        }

        /// <summary>Clipped drawing helpers for building scenes.</summary>
        sealed class Builder
        {
            public readonly World W;
            public readonly int Width, H, Cx;

            public Builder(World w)
            {
                W = w;
                Width = w.W;
                H = w.H;
                Cx = w.W / 2;
            }

            public void Fill(int x0, int x1, int y0, int y1, byte t)
            {
                for (int y = Math.Max(0, y0); y < Math.Min(H, y1); y++)
                    for (int x = Math.Max(0, x0); x < Math.Min(Width, x1); x++)
                    {
                        if (t == E.Empty) W.Del(y * Width + x);
                        else W.Set(y * Width + x, t);
                    }
            }

            /// <summary>Hollow rectangle with walls thick dots thick.</summary>
            public void Box(int x0, int y0, int x1, int y1, int thick, byte t)
            {
                Fill(x0, x1 + 1, y0, y0 + thick, t);
                Fill(x0, x1 + 1, y1 + 1 - thick, y1 + 1, t);
                Fill(x0, x0 + thick, y0, y1 + 1, t);
                Fill(x1 + 1 - thick, x1 + 1, y0, y1 + 1, t);
            }

            public void Disc(int cx, int cy, int r, byte t)
            {
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                        if (x * x + y * y <= r * r) Fill(cx + x, cx + x + 1, cy + y, cy + y + 1, t);
            }

            public void Line(int x0, int y0, int x1, int y1, int r, byte t)
            {
                W.DrawShape(Shape.Line, x0, y0, x1, y1, 0, 0, r, t, false);
            }

            /// <summary>A block of CLONE cells that keep producing material t.</summary>
            public void Spring(int x, int y, int w, int h, byte t)
            {
                Fill(x, x + w, y, y + h, E.Clone);
                for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                    for (int xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
                        W.Life[yy * Width + xx] = t;
            }

            /// <summary>A seed buried at (x, y) in damp soil, ready to sprout.</summary>
            public void Seed(int x, int y)
            {
                Fill(x - 2, x + 3, y - 2, y + 3, E.Mud);
                Fill(x, x + 1, y, y + 1, E.Seed);
            }

            /// <summary>A cloud holding moisture steam per cell, so it rains (and, if full, storms).</summary>
            public void Cloud(int x, int y, int w, int h, int moisture)
            {
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        double dx = (xx - x - w / 2.0) / (w / 2.0), dy = (yy - y - h / 2.0) / (h / 2.0);
                        if (dx * dx + dy * dy > 1 || xx < 0 || xx >= Width || yy < 0 || yy >= H) continue;
                        int i = yy * Width + xx;
                        W.Set(i, E.Cloud);
                        W.Life[i] = (short)moisture;
                    }
            }

            /// <summary>Row of the topmost solid thing in column x (H if none).</summary>
            public int SurfaceY(int x)
            {
                for (int y = 0; y < H; y++)
                    if (W.Type[y * Width + x] != E.Empty) return y;
                return H;
            }
        }
    }
}
