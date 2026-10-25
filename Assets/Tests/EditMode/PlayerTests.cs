using System.IO;
using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class PlayerTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static void Run(World w, int steps)
        {
            for (int k = 0; k < steps; k++) w.Step();
        }

        static void Fill(World w, int x0, int x1, int y0, int y1, byte t)
        {
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) w.Set(y * W + x, t);
        }

        static Player Landed(World w, int x)
        {
            Player p = w.AddPlayer(x, 100);
            Run(w, 150);
            return p;
        }

        [Test]
        public void FallsAndLandsOnTheFloor()
        {
            var w = new World(1);
            Player p = Landed(w, 200);
            Assert.AreEqual(p, w.Players[0], "still alive");
            Assert.AreEqual(H - 1, (int)p.Y);
            Assert.IsTrue(p.Grounded);
        }

        [Test]
        public void AtMostTwoPlayers()
        {
            var w = new World(2);
            Assert.IsTrue(w.AddPlayer(100, 100) != null);
            Assert.IsTrue(w.AddPlayer(200, 100) != null);
            Assert.IsTrue(w.AddPlayer(300, 100) == null);
        }

        [Test]
        public void CannotBePlacedInsideAWall()
        {
            var w = new World(3);
            Fill(w, 190, 210, 90, 110, E.Wall);
            Assert.IsTrue(w.AddPlayer(200, 100) == null);
        }

        [Test]
        public void WalksAndTurns()
        {
            var w = new World(4);
            Player p = Landed(w, 100);
            p.Right = true;
            Run(w, 100);
            Assert.Greater(p.X, 180);
            Assert.AreEqual(1, p.Facing);
            p.Right = false;
            p.Left = true;
            Run(w, 50);
            Assert.Less(p.X, 160);
            Assert.AreEqual(-1, p.Facing);
        }

        [Test]
        public void ClimbsSmallStepsButNotWalls()
        {
            var w = new World(5);
            Fill(w, 150, W, H - 2, H, E.Wall);          // 2-dot step
            Fill(w, 250, 255, H - 30, H - 2, E.Wall);   // tall wall on top of it
            Player p = Landed(w, 100);
            p.Right = true;
            Run(w, 300);
            Assert.Greater(p.X, 160, "climbed the step");
            Assert.Less(p.X, 250, "stopped at the wall");
            Assert.AreEqual(H - 3, (int)p.Y);
        }

        [Test]
        public void Jumps()
        {
            var w = new World(6);
            Player p = Landed(w, 200);
            p.Jump = true;
            float top = p.Y;
            for (int k = 0; k < 30; k++) { w.Step(); if (p.Y < top) top = p.Y; }
            Assert.Greater(H - 1 - top, 12, "jumped more than a body height");
            p.Jump = false;
            Run(w, 60);
            Assert.AreEqual(H - 1, (int)p.Y, "landed again");
        }

        [Test]
        public void FireKillsThePlayer()
        {
            var w = new World(7);
            Landed(w, 200);
            w.Brush(203, H - 5, 1, E.Fire, 0, 1);
            Run(w, 5);
            Assert.IsTrue(w.Players[0] == null);
        }

        [Test]
        public void PicksUpAndShootsMaterial()
        {
            var w = new World(8);
            Player p = Landed(w, 200);
            w.Set((H - 4) * W + 202, E.Water);
            w.Step();
            Assert.AreEqual(E.Water, p.Held);
            p.Shoot = true;
            Run(w, 30);
            int water = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Water) water++;
            Assert.Greater(water, 10, "shot water");
            Assert.AreEqual(E.Water, w.At(203, H - 6), "from the hand it faces");
        }

        [Test]
        public void EraserRemovesPlayer()
        {
            var w = new World(9);
            Landed(w, 200);
            w.Erase(200, H - 5, 2);
            Assert.IsTrue(w.Players[0] == null);
        }

        [Test]
        public void SurvivesSaveAndLoad()
        {
            var w = new World(10);
            Player p = Landed(w, 123);
            p.Held = E.Oil;
            var stream = new MemoryStream();
            w.Save(stream);
            stream.Position = 0;
            var other = new World(11);
            other.Load(stream);
            Assert.IsTrue(other.Players[0] != null);
            Assert.AreEqual(p.X, other.Players[0].X);
            Assert.AreEqual(E.Oil, other.Players[0].Held);
        }
    }
}
