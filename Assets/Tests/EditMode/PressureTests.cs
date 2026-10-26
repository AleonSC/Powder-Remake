using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class PressureTests
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

        /// <summary>The highest row holding water between x0 and x1.</summary>
        static int Surface(World w, int x0, int x1)
        {
            for (int y = 0; y < H; y++)
                for (int x = x0; x < x1; x++)
                    if (w.Type[y * W + x] == E.Water) return y;
            return H;
        }

        /// <summary>Two 10-wide arms joined at the bottom, water only in the left one.</summary>
        static World UTube(uint seed)
        {
            var w = new World(seed);
            Fill(w, 140, 142, 100, H, E.Wall);
            Fill(w, 152, 154, 100, 280, E.Wall);
            Fill(w, 180, 182, 100, 280, E.Wall);
            Fill(w, 192, 194, 100, H, E.Wall);
            Fill(w, 154, 180, 100, 102, E.Wall); // cap the middle so it is a solid block
            Fill(w, 154, 180, 102, 280, E.Wall);
            Fill(w, 142, 192, 298, H, E.Wall);
            Fill(w, 142, 152, 140, 298, E.Water);
            Fill(w, 152, 192, 280, 298, E.Water);
            return w;
        }

        [Test]
        public void UTubeLevelsOut()
        {
            var w = UTube(1);
            int water = Count(w, E.Water);
            Run(w, 1200);
            int left = Surface(w, 142, 152), right = Surface(w, 182, 192);
            Assert.LessOrEqual(System.Math.Abs(left - right), 2, "both arms at the same level (" + left + " vs " + right + ")");
            Assert.Less(right, 270, "the right arm filled up");
            Assert.AreEqual(water, Count(w, E.Water), "no water made or lost");
        }

        [Test]
        public void WideArmsFillCalmly()
        {
            var w = UTube(2);
            int highest = H;
            for (int k = 0; k < 1200; k++)
            {
                w.Step();
                int s = Surface(w, 182, 192);
                if (s < highest) highest = s;
            }
            Assert.GreaterOrEqual(highest, Surface(w, 142, 152) - 3, "no spray shooting up the wide arm");
        }

        [Test]
        public void FlatPoolStaysPut()
        {
            var w = new World(3);
            Fill(w, 98, 100, 250, H, E.Wall);
            Fill(w, 300, 302, 250, H, E.Wall);
            Fill(w, 100, 300, 270, H, E.Water);
            for (int k = 0; k < 300; k++)
            {
                w.Step();
                Assert.GreaterOrEqual(Surface(w, 100, 300), 269, "nothing rises above a level pool");
            }
        }

        [Test]
        public void PipeFromATankMakesAFountain()
        {
            // A tall tank whose bottom feeds a narrow pipe that ends far below the tank's surface.
            var w = new World(4);
            Fill(w, 100, 102, 120, H, E.Wall);
            Fill(w, 160, 162, 120, 290, E.Wall);
            Fill(w, 100, 200, 298, H, E.Wall);
            Fill(w, 166, 168, 230, 298, E.Wall);
            Fill(w, 160, 168, 290, 292, E.Wall);
            Fill(w, 102, 160, 130, 298, E.Water);
            Fill(w, 162, 166, 232, 298, E.Water);
            int highest = H;
            for (int k = 0; k < 300; k++)
            {
                w.Step();
                for (int y = 0; y < 230; y++)
                    for (int x = 160; x < 200; x++)
                        if (w.Type[y * W + x] == E.Water && y < highest) highest = y;
            }
            Assert.Less(highest, 200, "water jetted well above the pipe's mouth (row 230)");
        }

        [Test]
        public void HoleLowInATankSquirts()
        {
            var w = new World(5);
            Fill(w, 100, 102, 100, H, E.Wall);
            Fill(w, 160, 162, 100, H, E.Wall);
            Fill(w, 100, 162, 298, H, E.Wall);
            Fill(w, 102, 160, 110, 298, E.Water);
            Fill(w, 160, 162, 250, 251, E.Empty); // a hole 48 rows below the surface
            int furthest = 0;
            for (int k = 0; k < 120; k++)
            {
                w.Step();
                for (int y = 250; y < 290; y++)
                    for (int x = 162; x < W; x++)
                        if (w.Type[y * W + x] == E.Water && x > furthest) furthest = x;
            }
            Assert.Greater(furthest, 190, "the jet arced well out from the wall");
        }

        [Test]
        public void EachLiquidFindsItsOwnLevel()
        {
            // Water under a layer of oil isn't pushed up through the oil.
            var w = new World(6);
            Fill(w, 98, 100, 200, H, E.Wall);
            Fill(w, 300, 302, 200, H, E.Wall);
            Fill(w, 100, 300, 280, H, E.Water);
            Fill(w, 100, 300, 270, 280, E.Oil);
            Run(w, 300);
            Assert.GreaterOrEqual(Surface(w, 100, 300), 278, "water stays under the oil");
        }
    }
}
