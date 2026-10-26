using System.Collections.Generic;
using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class ShapeTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
            return n;
        }

        static HashSet<int> Trace(Shape s, int x0, int y0, int x1, int y1, int mx = 0, int my = 0)
        {
            var dots = new HashSet<int>();
            World.TraceShape(s, x0, y0, x1, y1, mx, my, (x, y) => dots.Add(y * W + x));
            return dots;
        }

        [Test]
        public void LinesAreSolidAndHitBothEnds()
        {
            var dots = Trace(Shape.Line, 100, 100, 200, 150);
            Assert.IsTrue(dots.Contains(100 * W + 100) && dots.Contains(150 * W + 200), "both ends");
            Assert.AreEqual(101, dots.Count, "one dot per column, no gaps");
        }

        [Test]
        public void CurvesPassThroughTheBendPoint()
        {
            var dots = Trace(Shape.Curve, 100, 200, 200, 200, 150, 150);
            Assert.IsTrue(dots.Contains(150 * W + 150), "through the bend point");
            Assert.IsTrue(dots.Contains(200 * W + 100) && dots.Contains(200 * W + 200), "both ends");
            Assert.IsFalse(dots.Contains(200 * W + 150), "actually curved");
        }

        [Test]
        public void BoxesAndCirclesAreClosedOutlines()
        {
            Assert.AreEqual(2 * 51 + 2 * 49, Trace(Shape.Box, 100, 100, 150, 150).Count);
            var circle = Trace(Shape.Circle, 200, 150, 230, 150);
            Assert.IsTrue(circle.Contains(150 * W + 230) && circle.Contains(120 * W + 200), "radius 30");
            Assert.Greater(circle.Count, 150, "about 2πr dots around");
            Assert.IsFalse(circle.Contains(150 * W + 200), "hollow");
        }

        [Test]
        public void ThicknessFollowsThePen()
        {
            var w = new World(1);
            w.DrawShape(Shape.Line, 100, 100, 200, 100, 0, 0, 2, E.Wall, false);
            Assert.AreEqual(E.Wall, w.At(150, 98));
            Assert.AreEqual(E.Wall, w.At(150, 102));
            Assert.AreEqual(E.Empty, w.At(150, 104));
        }

        [Test]
        public void MetalCurvesHoldTheirShape()
        {
            var w = new World(2);
            w.DrawShape(Shape.Line, 98, 150, 98, 160, 0, 0, 1, E.Wall, false);  // block posts the ends are welded to
            w.DrawShape(Shape.Line, 302, 150, 302, 160, 0, 0, 1, E.Wall, false);
            w.DrawShape(Shape.Curve, 100, 150, 300, 150, 200, 100, 1, E.Metal, false);
            int before = Count(w, E.Metal);
            for (int k = 0; k < 300; k++) w.Step();
            Assert.AreEqual(before, Count(w, E.Metal));
            Assert.AreEqual(E.Metal, w.At(200, 100), "still arched");
        }

        [Test]
        public void UnweldedMetalFallsInOnePiece()
        {
            var w = new World(6);
            w.DrawShape(Shape.Curve, 100, 150, 300, 150, 200, 100, 1, E.Metal, false);
            int before = Count(w, E.Metal), height = Height(w);
            for (int k = 0; k < 200; k++) w.Step();
            Assert.AreEqual(before, Count(w, E.Metal), "nothing broke off");
            Assert.AreEqual(E.Empty, w.At(200, 100), "it fell");
            Assert.AreEqual(height, Height(w), "still the same arch");
            Assert.AreEqual(E.Metal, w.At(100, H - 1), "resting on the floor");
        }

        /// <summary>Rows spanned by the metal.</summary>
        static int Height(World w)
        {
            int top = H, bottom = -1;
            for (int i = 0; i < w.N; i++)
            {
                if (w.Type[i] != E.Metal) continue;
                top = System.Math.Min(top, i / W);
                bottom = System.Math.Max(bottom, i / W);
            }
            return bottom - top + 1;
        }

        [Test]
        public void WaterLinesFallApart()
        {
            var w = new World(3);
            w.DrawShape(Shape.Line, 100, 100, 300, 100, 0, 0, 1, E.Water, false);
            int drawn = Count(w, E.Water);
            for (int k = 0; k < 200; k++) w.Step();
            int stillThere = 0;
            for (int x = 100; x <= 300; x++) if (w.At(x, 100) == E.Water) stillThere++;
            Assert.AreEqual(0, stillThere, "nothing left on the line");
            Assert.AreEqual(drawn, Count(w, E.Water), "it all fell");
        }

        [Test]
        public void ShapesOnlyFillEmptySpaceAndCanErase()
        {
            var w = new World(4);
            w.Set(100 * W + 150, E.Stone);
            w.DrawShape(Shape.Line, 100, 100, 200, 100, 0, 0, 0, E.Metal, false);
            Assert.AreEqual(E.Stone, w.At(150, 100), "didn't overwrite");
            w.DrawShape(Shape.Box, 90, 90, 210, 110, 0, 0, 0, E.Wall, false);
            w.DrawShape(Shape.Line, 100, 100, 200, 100, 0, 0, 0, 0, true);
            Assert.AreEqual(0, Count(w, E.Metal));
            Assert.Greater(Count(w, E.Wall), 0, "erasing the line left the box");
        }

        [Test]
        public void FansDrawnAsALinePointAlongIt()
        {
            var w = new World(5);
            w.DrawShape(Shape.Line, 100, 100, 100, 200, 0, 0, 0, E.Fan, false);
            Assert.AreEqual(1f, w.Vy[150 * W + 100], "blowing down the line");
        }
    }
}
