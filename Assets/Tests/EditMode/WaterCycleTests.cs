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

        /// <summary>Every drop of water, wherever it currently is, including inside plants.</summary>
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
        public void DirtSoaksUpWaterWhichSeepsDown()
        {
            var w = new World(5);
            Fill(w, 96, 100, 250, H, E.Wall);
            Fill(w, 300, 304, 250, H, E.Wall);
            Fill(w, 100, 300, 270, H, E.Dirt);
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
            Assert.Greater(Count(w, E.Dirt), 100, "dried");
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
            Fill(w, 100, 200, 280, H, E.Dirt);
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

        [Test]
        public void SteamCondensesOnGlass()
        {
            var w = new World(11);
            Fill(w, 100, 300, 150, 152, E.Glass); // a glass roof
            Fill(w, 150, 250, 200, 210, E.Steam);
            int steam = Count(w, E.Steam);
            Run(w, 400);
            Assert.Greater(Count(w, E.Water), steam / 2, "condensed into droplets");
            Assert.AreEqual(0, Count(w, E.Cloud), "under glass it fogs up instead of making clouds");
        }

        [Test]
        public void SealedTerrariumRecyclesItsWater()
        {
            // Sun shines through a sealed glass box: water evaporates, condenses on the
            // glass, drips back into the soil, and waters a buried seed.
            var w = new World(12) { Sun = World.SunLevel.High };
            Fill(w, 140, 260, 200, 202, E.Glass);   // lid
            Fill(w, 140, 142, 200, H, E.Glass);     // walls
            Fill(w, 258, 260, 200, H, E.Glass);
            Fill(w, 142, 258, 280, H, E.Dirt);
            w.Set(284 * W + 230, E.Seed);
            Fill(w, 142, 180, 270, 280, E.Water);   // a pond at the far end from the seed
            int total = TotalWater(w);
            bool fogged = false;
            for (int k = 0; k < 8000; k++)
            {
                w.Step();
                if (!fogged)
                    for (int x = 142; x < 258; x++)
                        if (w.Type[202 * W + x] == E.Water) { fogged = true; break; }
            }
            Assert.IsTrue(fogged, "droplets formed on the lid");
            int outside = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool inside = x >= 142 && x < 258 && y >= 202;
                    byte t = w.Type[y * W + x];
                    if (!inside && (t == E.Water || t == E.Steam || t == E.Mud || t == E.Cloud)) outside++;
                }
            Assert.AreEqual(0, outside, "nothing escaped the glass");
            Assert.AreEqual(total, TotalWater(w), "not a drop created or lost");
            Assert.Greater(Count(w, E.Plant), 10, "the seed got watered and grew");
        }

        // ------------------------------------------------------------ snapping

        /// <summary>A horizontal branch hanging in mid-air, numbered outwards from a root at x0.</summary>
        static void Branch(World w, int x0, int y, int length)
        {
            for (int k = 0; k < length; k++)
            {
                int i = y * W + x0 + k;
                w.Set(i, E.Plant);
                w.Vx[i] = k;
            }
        }

        [Test]
        public void LongBranchesSnapIntoDust()
        {
            var w = new World(20);
            Branch(w, 100, 150, 120);
            Run(w, 2000);
            Assert.Less(Count(w, E.Plant), 120, "snapped");
            Assert.GreaterOrEqual(Count(w, E.Plant), World.SnapLength, "only beyond the snap length");
            Assert.Greater(Count(w, E.Dust) + Count(w, E.Dirt) + Count(w, E.Seed), 0, "crumbled into dust");
        }

        [Test]
        public void ShortBranchesHold()
        {
            var w = new World(21);
            Branch(w, 100, 150, World.SnapLength);
            Run(w, 3000);
            Assert.AreEqual(World.SnapLength, Count(w, E.Plant));
        }

        [Test]
        public void SupportedBranchesHold()
        {
            var w = new World(22);
            Fill(w, 90, 260, 151, 152, E.Wall); // resting along a shelf
            Branch(w, 100, 150, 120);
            Run(w, 3000);
            Assert.AreEqual(120, Count(w, E.Plant));
        }

        [Test]
        public void SnappedBranchesSometimesDropSeeds()
        {
            int seeds = 0;
            for (uint s = 0; s < 6; s++)
            {
                var w = new World(30 + s);
                for (int b = 0; b < 4; b++) Branch(w, 20 + b * 2, 40 + b * 40, 150);
                Run(w, 3000);
                seeds += Count(w, E.Seed);
            }
            Assert.Greater(seeds, 0);
        }

        [Test]
        public void DustDecomposesIntoSoilAndReturnsPlantWater()
        {
            var w = new World(23);
            Fill(w, 100, 300, 290, H, E.Dust);
            w.Set(10 * W + 10, E.Plant); // a plant still alive elsewhere, holding the water
            w.PlantWater = 100;
            Run(w, 3000);
            Assert.Less(Count(w, E.Dust), 4000 / 2, "decomposing");
            Assert.Greater(Count(w, E.Dirt), 0, "into soil");
            Assert.AreEqual(100, Count(w, E.Mud) + w.PlantWater, "plant water came back as damp soil");
        }

        [Test]
        public void LeavesBreatheOutPlantWater()
        {
            var w = new World(24);
            Fill(w, 100, 300, 200, 201, E.Leaf);
            w.PlantWater = 50;
            Run(w, 2000);
            Assert.Less(w.PlantWater, 50, "released");
            Assert.AreEqual(50, TotalWater(w), "as steam/clouds, none lost");
        }

        [Test]
        public void BurningPlantsGiveOffTheirWater()
        {
            var w = new World(25);
            Branch(w, 100, 150, 40);
            w.PlantWater = 20;
            Fill(w, 100, 140, 151, 152, E.Fire);
            Run(w, 300);
            Assert.AreEqual(0, Count(w, E.Plant), "burned");
            Assert.AreEqual(20, TotalWater(w), "water went up as steam");
        }

        [Test]
        public void TerrariumSustainsItself()
        {
            // A long run of a sealed, sunlit terrarium: the tree overgrows and snaps, its
            // dust turns back into soil, and the water stays in the box the whole time.
            var w = new World(26) { Sun = World.SunLevel.High };
            Fill(w, 100, 300, 150, 152, E.Glass);
            Fill(w, 100, 102, 150, H, E.Glass);
            Fill(w, 298, 300, 150, H, E.Glass);
            Fill(w, 102, 298, 275, H, E.Dirt);
            Fill(w, 102, 298, 265, 275, E.Water);
            w.Set(278 * W + 200, E.Seed);
            int total = TotalWater(w);
            bool snapped = false;
            for (int k = 0; k < 30000; k++)
            {
                w.Step();
                if (!snapped && k % 50 == 0 && (Count(w, E.Dust) > 0)) snapped = true;
            }
            Assert.IsTrue(snapped, "branches snapped into dust");
            Assert.Greater(Count(w, E.Plant), 20, "plants still alive");
            Assert.AreEqual(total, TotalWater(w), "every drop accounted for");
        }
    }
}
