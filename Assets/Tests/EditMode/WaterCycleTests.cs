using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class WaterCycleTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
            return n;
        }

        /// <summary>Every drop of water, wherever it currently is.</summary>
        static int TotalWater(World w)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++)
            {
                byte t = w.Type[i];
                if (t == E.Water || t == E.Steam || t == E.Mud) n++;
                else if (t == E.Cloud) n += w.Life[i];
            }
            return n;
        }

        static void Run(World w, int steps)
        {
            for (int k = 0; k < steps; k++) w.Step();
        }

        static void Fill(World w, int x0, int x1, int y0, int y1, byte t)
        {
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) w.Set(y * W + x, t);
        }

        /// <summary>A walled pool of water on the floor.</summary>
        static World Lake(uint seed, World.SunLevel sun)
        {
            var w = new World(seed) { Sun = sun };
            Fill(w, 96, 100, 270, H, E.Wall);
            Fill(w, 300, 304, 270, H, E.Wall);
            Fill(w, 100, 300, 285, H, E.Water);
            return w;
        }

        [Test]
        public void NoSunNoEvaporation()
        {
            var w = Lake(1, World.SunLevel.Off);
            Run(w, 1000);
            Assert.AreEqual(3000, Count(w, E.Water));
        }

        [Test]
        public void SunEvaporatesOpenWater()
        {
            var w = Lake(2, World.SunLevel.Low);
            Run(w, 300);
            Assert.Less(Count(w, E.Water), 3000);
            Assert.Greater(Count(w, E.Steam), 0);
        }

        [Test]
        public void HighSunEvaporatesFaster()
        {
            var low = Lake(3, World.SunLevel.Low);
            var high = Lake(3, World.SunLevel.High);
            Run(low, 300);
            Run(high, 300);
            Assert.Greater(3000 - Count(high, E.Water), 2 * (3000 - Count(low, E.Water)));
        }

        [Test]
        public void ShadeStopsEvaporation()
        {
            var w = Lake(4, World.SunLevel.High);
            Fill(w, 90, 310, 200, 202, E.Wall); // a roof well above the water
            Run(w, 600);
            Assert.AreEqual(3000, Count(w, E.Water));
        }

        [Test]
        public void PowderSoaksUpWaterWhichSeepsDown()
        {
            var w = new World(5);
            Fill(w, 96, 100, 250, H, E.Wall);
            Fill(w, 300, 304, 250, H, E.Wall);
            Fill(w, 100, 300, 270, H, E.Powder);
            Fill(w, 100, 300, 266, 270, E.Water);
            int total = TotalWater(w);
            Run(w, 1500);
            Assert.AreEqual(0, Count(w, E.Water), "all soaked in");
            Assert.AreEqual(total, Count(w, E.Mud), "every drop is in the ground");
            int deepest = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Mud && i / W > deepest) deepest = i / W;
            Assert.Greater(deepest, 280, "seeped down");
        }

        [Test]
        public void MudDriesInTheSun()
        {
            // The box is closed, so that steam will eventually rain back down; just check
            // the surface gives up its water.
            var w = new World(6) { Sun = World.SunLevel.High };
            Fill(w, 100, 300, 290, H, E.Mud);
            Run(w, 600);
            Assert.Greater(Count(w, E.Powder), 100, "dried");
            Assert.Greater(Count(w, E.Steam) + Count(w, E.Cloud), 0, "into the air");
        }

        [Test]
        public void SunMeltsSnow()
        {
            var w = new World(7) { Sun = World.SunLevel.High };
            Fill(w, 100, 300, 295, H, E.Snow);
            Run(w, 1000);
            Assert.Greater(Count(w, E.Water) + Count(w, E.Steam), 50);
        }

        [Test]
        public void SeedSproutsFromDampSoil()
        {
            var w = new World(8);
            Fill(w, 150, 250, 280, H, E.Mud);   // rain-soaked hill, no pools anywhere
            w.Set(283 * W + 200, E.Seed);
            Run(w, 1500);
            Assert.Greater(Count(w, E.Plant), 10);
        }

        [Test]
        public void SteamCloudsFadeWhenDryButPaintedOnesStay()
        {
            var w = new World(9);
            Fill(w, 50, 100, 50, 55, E.Cloud);   // painted
            Fill(w, 250, 300, 100, 110, E.Steam);
            Run(w, 5000);
            Assert.AreEqual(0, Count(w, E.Steam));
            int painted = 0, natural = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (w.Type[y * W + x] == E.Cloud) { if (x < 150) painted++; else natural++; }
            Assert.AreEqual(250, painted);
            Assert.Less(natural, 20, "natural clouds rained out and faded");
        }

        [Test]
        public void WaterIsConservedThroughTheWholeCycle()
        {
            var w = new World(10) { Sun = World.SunLevel.High };
            Fill(w, 96, 100, 260, H, E.Wall);
            Fill(w, 300, 304, 260, H, E.Wall);
            Fill(w, 100, 200, 280, H, E.Powder);
            Fill(w, 200, 300, 280, H, E.Water);
            int total = TotalWater(w);
            bool clouds = false, rain = false;
            for (int k = 0; k < 6000; k++)
            {
                w.Step();
                if (k % 100 != 0) continue;
                if (Count(w, E.Cloud) > 0) clouds = true;
                for (int i = 0; i < W * 200 && !rain; i++) if (w.Type[i] == E.Water) rain = true;
            }
            Assert.IsTrue(clouds, "clouds formed");
            Assert.IsTrue(rain, "it rained");
            Assert.Greater(Count(w, E.Mud), 0, "rain soaked into the ground");
            Assert.AreEqual(total, TotalWater(w), "not a drop created or lost");
        }
    }
}
