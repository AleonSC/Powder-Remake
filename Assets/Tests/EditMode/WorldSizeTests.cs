using System;
using System.IO;
using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class WorldSizeTests
    {
        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
            return n;
        }

        [Test]
        public void RejectsSizesThatDontFitTheAirGrid()
        {
            bool threw = false;
            try { new World(401, 300); } catch (ArgumentException) { threw = true; }
            Assert.IsTrue(threw);
        }

        [Test]
        public void WideWorldSimulates()
        {
            var w = new World(756, 300, 1);
            for (int k = 0; k < 10; k++) w.Brush(700, 50 + k, 8, E.Water, 0, 1);
            w.Brush(30, 50, 6, E.Powder, 0, 1);
            int water = Count(w, E.Water), powder = Count(w, E.Powder);
            for (int k = 0; k < 600; k++) w.Step();
            Assert.AreEqual(water, Count(w, E.Water), "water conserved");
            Assert.AreEqual(powder, Count(w, E.Powder), "powder conserved");
            Assert.AreEqual(E.Water, w.At(700, w.H - 1), "water reached the floor at the far right");
        }

        [Test]
        public void ResizeKeepsContentOnTheFloorAndCentred()
        {
            var w = new World(400, 300, 2);
            w.Set(299 * 400 + 200, E.Stone);  // bottom centre
            w.Set(10 * 400 + 0, E.Wall);      // top-left corner
            Player p = w.AddPlayer(50, 299);

            var wide = w.Resized(800, 300);
            Assert.AreEqual(E.Stone, wide.At(400, 299));
            Assert.AreEqual(E.Wall, wide.At(200, 10));
            Assert.AreEqual(p.X + 200, wide.Players[0].X);

            var tall = w.Resized(400, 400);
            Assert.AreEqual(E.Stone, tall.At(200, 399), "anchored to the floor");
            Assert.AreEqual(E.Wall, tall.At(0, 110));

            var narrow = w.Resized(200, 300);
            Assert.AreEqual(E.Stone, narrow.At(100, 299));
            Assert.AreEqual(1, Count(narrow, E.Stone));
            Assert.AreEqual(0, Count(narrow, E.Wall), "cropped");
            Assert.IsTrue(narrow.Players[0] == null, "player outside the crop is dropped");
        }

        [Test]
        public void LoadsSavesFromOtherSizes()
        {
            var small = new World(400, 300, 3);
            small.Set(299 * 400 + 0, E.Metal);
            var ms = new MemoryStream();
            small.Save(ms);
            ms.Position = 0;
            var big = new World(600, 400, 4);
            big.Load(ms);
            Assert.AreEqual(E.Metal, big.At(100, 399));
        }
    }
}
