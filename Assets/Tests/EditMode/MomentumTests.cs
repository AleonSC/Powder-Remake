using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class MomentumTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
            return n;
        }

        static int Find(World w, byte t)
        {
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) return i;
            return -1;
        }

        static void Fill(World w, int x0, int x1, int y0, int y1, byte t)
        {
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) w.Set(y * W + x, t);
        }

        [Test]
        public void FallingAccelerates()
        {
            var w = new World(1);
            w.Set(10 * W + 200, E.Sand);
            for (int k = 0; k < 20; k++) w.Step();
            Assert.Greater(Find(w, E.Sand) / W, 10 + 30, "fell well over a cell per frame");
        }

        [Test]
        public void FallSpeedIsCapped()
        {
            var w = new World(2);
            w.Set(0 * W + 200, E.Sand);
            float fastest = 0;
            for (int k = 0; k < 60; k++)
            {
                w.Step();
                int i = Find(w, E.Sand);
                if (w.Vy[i] > fastest) fastest = w.Vy[i];
            }
            Assert.LessOrEqual(fastest, 8);
        }

        [Test]
        public void FallingWaterSplashes()
        {
            var w = new World(3);
            Fill(w, 0, W, 290, H, E.Water); // a pool to land in
            bool splashed = false;
            for (int k = 0; k < 200 && !splashed; k++)
            {
                if (k < 60) w.Set(20 * W + 200, E.Water); // a stream from high up
                w.Step();
                for (int i = 0; i < w.N && !splashed; i++)
                    if (w.Type[i] == E.Water && w.Vy[i] < 0) splashed = true;
            }
            Assert.IsTrue(splashed, "drops bounced back up");
        }

        [Test]
        public void WaterfallsArcOffLedges()
        {
            var w = new World(4);
            Fill(w, 50, 200, 150, 152, E.Wall); // a shelf ending at x = 200
            int furthest = 0;
            for (int k = 0; k < 300; k++)
            {
                if (k < 150) w.Brush(70, 145, 2, E.Water, 0, 1);
                w.Step();
                for (int y = 170; y < 280; y++)       // water in mid-air below the shelf
                    for (int x = 200; x < W; x++)
                        if (w.Type[y * W + x] == E.Water && x > furthest) furthest = x;
            }
            Assert.Greater(furthest, 206, "the stream curved out away from the edge");
        }

        [Test]
        public void ExplosionsThrowSand()
        {
            var w = new World(5);
            Fill(w, 150, 250, 280, H, E.Sand);
            w.Set(279 * W + 200, E.Bomb);
            w.Set(278 * W + 200, E.Bomb);
            int highest = H;
            for (int k = 0; k < 30; k++)
            {
                w.Step();
                for (int i = 0; i < w.N; i++)
                    if (w.Type[i] == E.Sand && i / W < highest) highest = i / W;
            }
            Assert.Less(highest, 260, "sand was thrown well above the pile");
        }

        [Test]
        public void PlayersThrowWhatTheyShoot()
        {
            var w = new World(6);
            Player p = w.AddPlayer(100, H - 1);
            for (int k = 0; k < 100; k++) w.Step();
            p.Held = E.Sand;
            p.Facing = 1;
            p.Shoot = true;
            w.Step();
            p.Shoot = false;
            for (int k = 0; k < 60; k++) w.Step();
            int i = Find(w, E.Sand);
            Assert.Greater(i % W, 110, "landed well in front");
        }

        [Test]
        public void SandStillSinksSlowlyThroughWater()
        {
            var w = new World(7);
            Fill(w, 0, W, 200, H, E.Water);
            w.Set(100 * W + 200, E.Sand);
            int frames = 0;
            while (Find(w, E.Sand) / W < H - 1 && frames < 1000) { w.Step(); frames++; }
            Assert.Greater(frames, 90, "water slows it down");
            Assert.Less(frames, 1000, "but it gets there");
        }
    }
}
