using System.IO;
using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class WorldTests
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

        [Test]
        public void PowderFallsAndPilesOnTheFloor()
        {
            var w = new World(1);
            w.Brush(200, 50, 5, E.Powder, 0, 1);
            int n = Count(w, E.Powder);
            Run(w, 400);
            Assert.AreEqual(n, Count(w, E.Powder), "powder is conserved");
            int floor = 0;
            for (int x = 0; x < W; x++) if (w.Type[(H - 1) * W + x] == E.Powder) floor++;
            Assert.Greater(floor, 5, "powder reached the floor");
        }

        [Test]
        public void WaterSpreadsIntoAFlatPool()
        {
            var w = new World(2);
            for (int k = 0; k < 20; k++) w.Brush(200, 40 + k, 8, E.Water, 0, 1);
            int n = Count(w, E.Water);
            Run(w, 1500);
            Assert.AreEqual(n, Count(w, E.Water), "water is conserved");
            int top = H;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Water) { top = i / W; break; }
            Assert.LessOrEqual(H - top, n / W + 4, "pool is flat");
        }

        [Test]
        public void OilFloatsOnWater()
        {
            var w = new World(3);
            for (int k = 0; k < 10; k++) w.Brush(200, 200 + k, 10, E.Oil, 0, 1);
            for (int k = 0; k < 10; k++) w.Brush(200, 100 + k, 10, E.Water, 0, 1);
            Run(w, 1500);
            double oilY = 0, waterY = 0;
            int no = 0, nw = 0;
            for (int i = 0; i < w.N; i++)
            {
                if (w.Type[i] == E.Oil) { oilY += i / W; no++; }
                if (w.Type[i] == E.Water) { waterY += i / W; nw++; }
            }
            Assert.Less(oilY / no, waterY / nw, "oil sits above water on average");
        }

        [Test]
        public void FireBurnsOil()
        {
            var w = new World(4);
            Fill(w, 150, 250, 280, H, E.Oil);
            Run(w, 300);
            int top = 0;
            while (w.Type[top * W + 200] != E.Oil) top++;
            Fill(w, 195, 205, top - 1, top, E.Fire);
            Run(w, 1500);
            Assert.Less(Count(w, E.Oil), 500, "most oil burned");
        }

        [Test]
        public void GunpowderChainReacts()
        {
            var w = new World(5);
            Fill(w, 100, 300, 270, H, E.Gunpowder);
            w.Set(269 * W + 200, E.Fire);
            Run(w, 300);
            Assert.Less(Count(w, E.Gunpowder), 300);
        }

        [Test]
        public void BombExplodesOnImpact()
        {
            var w = new World(6);
            w.Brush(200, 100, 2, E.Bomb, 0, 1);
            Fill(w, 0, W, 250, 251, E.Wall);
            Run(w, 200);
            Assert.AreEqual(0, Count(w, E.Bomb));
        }

        [Test]
        public void MagmaAndWaterMakeStone()
        {
            var w = new World(7);
            Fill(w, 150, 250, 280, H, E.Water);
            w.Brush(200, 200, 6, E.Magma, 0, 1);
            Run(w, 600);
            Assert.Greater(Count(w, E.Stone), 0);
        }

        [Test]
        public void SeedGrowsOnPowder()
        {
            var w = new World(8);
            Fill(w, 0, W, 290, H, E.Powder);
            for (int x = 100; x < 300; x += 20) w.Set(200 * W + x, E.Seed);
            Run(w, 800);
            Assert.Greater(Count(w, E.Plant), 50);
        }

        [Test]
        public void FanBlowsPowderSideways()
        {
            var w = new World(9);
            for (int y = 150; y < 200; y++)
            {
                for (int x = 40; x < 44; x++)
                {
                    w.Set(y * W + x, E.Fan);
                    w.Vx[y * W + x] = 1;
                }
            }
            Run(w, 120);
            w.Brush(60, 170, 6, E.Powder, 0, 1);
            Run(w, 120);
            double sx = 0;
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Powder) { sx += i % W; n++; }
            Assert.Greater(sx / n, 90, "powder drifted right");
        }

        [Test]
        public void LaserReflectsOffMetal()
        {
            var w = new World(10);
            Fill(w, 200, 201, 100, 200, E.Metal);
            w.Set(150 * W + 60, E.Laser);
            w.InitParticle(150 * W + 60, E.Laser, 1, 0);
            Run(w, 50);
            int laser = -1;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Laser) laser = i;
            Assert.GreaterOrEqual(laser, 0, "laser still alive");
            Assert.Less(laser % W, 200);
            Assert.AreEqual(-1f, w.Vx[laser], "travelling back left");
        }

        [Test]
        public void EveryElementSurvivesAStressRun()
        {
            var w = new World(11);
            for (int t = 1; t < Elements.Defs.Length; t++)
            {
                int x = 20 + (t - 1) % 12 * 30, y = 40 + (t - 1) / 12 * 60;
                for (int n = 0; n < 30; n++) w.Brush(x, y, 6, (byte)t, 1, 0);
            }
            w.SidesOpen = true;
            Run(w, 500);
            w.SidesOpen = false;
            Run(w, 500);
            for (int i = 0; i < w.AN; i++)
            {
                Assert.IsFalse(float.IsNaN(w.AirVx[i]) || float.IsNaN(w.AirVy[i]) || float.IsNaN(w.Pressure[i]), "air stays finite");
            }
        }

        [Test]
        public void SaveAndLoadRoundTrips()
        {
            var w = new World(12);
            w.Brush(100, 100, 10, E.Wall, 0, 1);
            w.Brush(200, 100, 3, E.Fan, 1, 0);
            var stream = new MemoryStream();
            w.Save(stream);
            stream.Position = 0;
            var other = new World(13);
            other.Load(stream);
            CollectionAssert.AreEqual(w.Type, other.Type);
            Assert.AreEqual(1f, other.Vx[100 * W + 200]);
        }
    }
}
