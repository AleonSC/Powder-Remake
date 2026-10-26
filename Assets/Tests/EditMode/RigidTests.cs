using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class RigidTests
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

        static int Bottom(World w, byte t)
        {
            int b = -1;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t && i / W > b) b = i / W;
            return b;
        }

        [Test]
        public void FallsWithGravityAndLands()
        {
            var w = new World(1);
            Fill(w, 190, 210, 50, 55, E.Metal);
            Run(w, 20);
            Assert.Greater(Bottom(w, E.Metal), 54 + 25, "accelerating, faster than a dot a frame");
            Run(w, 100);
            Assert.AreEqual(H - 1, Bottom(w, E.Metal));
            Assert.AreEqual(100, Count(w, E.Metal));
        }

        [Test]
        public void WeldedToABlockItHangs()
        {
            var w = new World(2);
            w.Set(49 * W + 200, E.Wall);
            Fill(w, 190, 210, 50, 55, E.Metal);
            Run(w, 100);
            Assert.AreEqual(54, Bottom(w, E.Metal));
        }

        [Test]
        public void RestsOnAPile()
        {
            var w = new World(3);
            Fill(w, 150, 250, 280, H, E.Dirt);
            Fill(w, 190, 210, 100, 105, E.Metal);
            Run(w, 200);
            Assert.Less(Bottom(w, E.Metal), 280, "sitting on the dirt");
        }

        [Test]
        public void MetalSinksThroughWater()
        {
            var w = new World(4);
            Fill(w, 98, 100, 200, H, E.Wall);
            Fill(w, 300, 302, 200, H, E.Wall);
            Fill(w, 100, 300, 250, H, E.Water);
            Fill(w, 190, 210, 150, 155, E.Metal);
            int water = Count(w, E.Water);
            Run(w, 300);
            Assert.AreEqual(H - 1, Bottom(w, E.Metal), "sank to the bottom");
            Assert.AreEqual(water, Count(w, E.Water), "the water it pushed aside is all still there");
        }

        [Test]
        public void WoodAndIceFloat()
        {
            var w = new World(5);
            Fill(w, 98, 100, 200, H, E.Wall);
            Fill(w, 300, 302, 200, H, E.Wall);
            Fill(w, 100, 300, 250, H, E.Water);
            Fill(w, 120, 140, 150, 155, E.Wood);
            Fill(w, 220, 240, 150, 155, E.Ice);
            Run(w, 300);
            Assert.AreEqual(249, Bottom(w, E.Wood), "wood floats on top");
            Assert.Less(Bottom(w, E.Ice), 260, "so does ice (which also freezes some of the water under it)");
        }

        [Test]
        public void FallsOutOfAnOpenWorld()
        {
            var w = new World(6) { SidesOpen = true };
            Fill(w, 190, 210, 50, 55, E.Metal);
            Run(w, 200);
            Assert.AreEqual(0, Count(w, E.Metal));
        }

        [Test]
        public void GlassBoxCarriesItsWaterDown()
        {
            var w = new World(7);
            Fill(w, 150, 152, 100, 140, E.Glass);
            Fill(w, 180, 182, 100, 140, E.Glass);
            Fill(w, 150, 182, 140, 142, E.Glass);
            Fill(w, 152, 180, 120, 140, E.Water);
            int water = Count(w, E.Water);
            Run(w, 300);
            Assert.AreEqual(H - 1, Bottom(w, E.Glass), "the box landed");
            int inside = 0;
            for (int y = 0; y < H; y++)
                for (int x = 152; x < 180; x++)
                    if (w.Type[y * W + x] == E.Water) inside++;
            Assert.Greater(inside, water * 8 / 10, "with most of its water still in it (some sloshes out on landing)");
        }
    }
}
