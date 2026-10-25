using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class WeatherAndPlantTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
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

        // ------------------------------------------------------------ weather

        [Test]
        public void WaterBoilsOnMagma()
        {
            var w = new World(1);
            Fill(w, 150, 250, 295, H, E.Magma);
            Fill(w, 150, 250, 285, 295, E.Water);
            Run(w, 60);
            Assert.Greater(Count(w, E.Steam), 20);
        }

        [Test]
        public void WaterBoilsOnATorch()
        {
            var w = new World(2);
            Fill(w, 190, 210, 290, 291, E.Torch);
            Fill(w, 188, 190, 270, 291, E.Wall);
            Fill(w, 210, 212, 270, 291, E.Wall);
            Fill(w, 190, 210, 280, 290, E.Water);
            Run(w, 300);
            Assert.Greater(Count(w, E.Steam), 5);
        }

        [Test]
        public void CloudsDoNotRainOnTheirOwn()
        {
            var w = new World(3);
            Fill(w, 150, 250, 50, 60, E.Cloud);
            Run(w, 1000);
            Assert.AreEqual(0, Count(w, E.Water));
        }

        [Test]
        public void CloudsAbsorbSteamThenRain()
        {
            var w = new World(4);
            Fill(w, 100, 300, 40, 50, E.Cloud);
            Fill(w, 140, 260, 60, 80, E.Steam);
            int steam = Count(w, E.Steam);
            Run(w, 600);
            Assert.Less(Count(w, E.Steam), steam / 4, "steam soaked up");
            int water = Count(w, E.Water);
            Assert.Greater(water, 100, "and rained back down");
            Assert.LessOrEqual(water, steam, "no more rain than steam absorbed");
        }

        /// <summary>Every drop of water, wherever it currently is.</summary>
        static int TotalWater(World w)
        {
            int n = w.PlantWater;
            for (int i = 0; i < w.N; i++)
            {
                byte t = w.Type[i];
                if (t == E.Water || t == E.Steam || t == E.Mud) n++;
                else if (t == E.Cloud) n += w.Life[i];
            }
            return n;
        }

        [Test]
        public void RisingSteamFormsCloudsThatRain()
        {
            var w = new World(5);
            Fill(w, 150, 250, 100, 120, E.Steam);
            int total = TotalWater(w);
            bool clouds = false;
            for (int k = 0; k < 3000; k++)
            {
                w.Step();
                if (Count(w, E.Cloud) > 0) clouds = true;
            }
            Assert.IsTrue(clouds, "clouds formed");
            Assert.AreEqual(0, Count(w, E.Steam), "all steam condensed");
            Assert.Greater(Count(w, E.Water), total / 2, "and most of it rained back down");
        }

        [Test]
        public void BoiledWaterComesBackAsRain()
        {
            // Full cycle: boil a pool, let the steam form clouds, wait for the rain.
            var w = new World(6);
            Fill(w, 150, 250, 296, H, E.Magma);
            Fill(w, 148, 150, 270, H, E.Wall);
            Fill(w, 250, 252, 270, H, E.Wall);
            Fill(w, 150, 250, 286, 296, E.Water);
            bool rained = false;
            for (int k = 0; k < 4000 && !rained; k++)
            {
                w.Step();
                for (int y = 20; y < 200 && !rained; y++)
                    for (int x = 0; x < W; x++)
                        if (w.Type[y * W + x] == E.Water) { rained = true; break; }
            }
            Assert.IsTrue(rained, "water fell from the sky");
        }

        // ------------------------------------------------------------- plants

        /// <summary>A mound of powder with a seed buried near the top, and optionally water on it.</summary>
        static World Garden(uint seed, int waterRows)
        {
            var w = new World(seed);
            Fill(w, 150, 250, 280, H, E.Powder);
            Fill(w, 146, 150, 260, H, E.Wall);
            Fill(w, 250, 254, 260, H, E.Wall);
            w.Set(282 * W + 200, E.Seed);
            if (waterRows > 0) Fill(w, 150, 250, 280 - waterRows, 280, E.Water);
            return w;
        }

        [Test]
        public void SeedNeedsWater()
        {
            var w = Garden(7, 0);
            Run(w, 600);
            Assert.AreEqual(0, Count(w, E.Plant));
            Assert.AreEqual(1, Count(w, E.Seed));
        }

        [Test]
        public void SeedNeedsToBeBuried()
        {
            var w = new World(8);
            Fill(w, 150, 250, 290, H, E.Powder);
            Fill(w, 146, 150, 270, H, E.Wall);
            Fill(w, 250, 254, 270, H, E.Wall);
            w.Set(289 * W + 200, E.Seed); // sitting on top
            Fill(w, 160, 170, 285, 289, E.Water);
            Run(w, 600);
            Assert.AreEqual(0, Count(w, E.Plant));
        }

        [Test]
        public void BuriedWateredSeedGrowsABranchingTree()
        {
            var w = Garden(9, 4);
            Run(w, 2000);
            int top = H, plants = 0;
            for (int i = 0; i < w.N; i++)
            {
                if (w.Type[i] != E.Plant) continue;
                plants++;
                if (i / W < top) top = i / W;
            }
            Assert.Greater(plants, 30, "grew");
            Assert.Less(top, 260, "grew up out of the powder");
            Assert.Greater(Count(w, E.Leaf), 3, "has leaves");
        }

        [Test]
        public void MoreWaterMeansMoreGrowth()
        {
            int dry = 0, wet = 0;
            for (uint s = 0; s < 3; s++)
            {
                var a = Garden(10 + s, 1);
                var b = Garden(10 + s, 10);
                Run(a, 2500);
                Run(b, 2500);
                dry += Count(a, E.Plant);
                wet += Count(b, E.Plant);
            }
            Assert.Greater(wet, dry * 3 / 2);
        }
    }
}
