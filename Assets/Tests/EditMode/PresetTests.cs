using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class PresetTests
    {
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

        static int IndexOf(string name)
        {
            return System.Array.IndexOf(Presets.Names, name);
        }

        [Test]
        public void EveryPresetLoadsAndRunsAtAnySize()
        {
            foreach (var size in new[] { new[] { 400, 300 }, new[] { 752, 300 }, new[] { 400, 584 } })
            {
                for (int k = 0; k < Presets.Names.Length; k++)
                {
                    var w = new World(size[0], size[1], (uint)(k + 1));
                    Presets.Load(w, k);
                    Assert.Greater(Count(w, 0), 0, Presets.Names[k]);
                    Assert.Less(Count(w, 0), w.N, Presets.Names[k] + " has something in it");
                    Run(w, 200);
                }
            }
        }

        [Test]
        public void LoadingReplacesTheOldScene()
        {
            var w = new World(1);
            w.Brush(200, 100, 10, E.Virus, 0, 1);
            w.AddPlayer(50, 50);
            Presets.Load(w, IndexOf("FOUNTAIN"));
            Assert.AreEqual(0, Count(w, E.Virus));
            Assert.IsTrue(w.Players[0] == null);
            Presets.Load(w, -1); // wraps around
            Presets.Load(w, Presets.Names.Length);
        }

        [Test]
        public void VolcanoErupts()
        {
            var w = new World(2);
            Presets.Load(w, IndexOf("VOLCANO"));
            int magma = Count(w, E.Magma);
            Run(w, 600);
            Assert.Greater(Count(w, E.Magma) + Count(w, E.Stone), magma + 50, "lava kept coming");
            Assert.Greater(Count(w, E.Steam) + Count(w, E.Cloud), 0, "and boiled the lakes");
        }

        [Test]
        public void DemolitionBringsTheGirderDown()
        {
            var w = new World(3);
            Presets.Load(w, IndexOf("DEMOLITION"));
            int girderRow = w.H - 6 - 62;
            Run(w, 900);
            int high = 0;
            for (int x = 0; x < w.W; x++) if (w.Type[girderRow * w.W + x] == E.Metal) high++;
            Assert.Less(high, 50, "the girder is no longer up on its pillars");
        }

        [Test]
        public void ThunderstormThrowsLightning()
        {
            var w = new World(4);
            Presets.Load(w, IndexOf("THUNDERSTORM"));
            bool bolt = false;
            for (int k = 0; k < 1500 && !bolt; k++)
            {
                w.Step();
                bolt = Count(w, E.Thunder) > 0;
            }
            Assert.IsTrue(bolt);
        }

        [Test]
        public void LaserLabBeamTurnsAtTheMirror()
        {
            var w = new World(5);
            Presets.Load(w, IndexOf("LASER LAB"));
            bool down = false;
            for (int k = 0; k < 600 && !down; k++)
            {
                w.Step();
                for (int i = 0; i < w.N && !down; i++)
                    if (w.Type[i] == E.Laser && w.Vy[i] == 1 && w.Vx[i] == 0) down = true;
            }
            Assert.IsTrue(down, "a beam heading down after the mirror");
        }

        [Test]
        public void DiagonalMirrorsTurnLasers()
        {
            var w = new World(6);
            w.DrawShape(Shape.Line, 180, 130, 220, 170, 0, 0, 0, E.Metal, false);
            w.Set(129 * w.W + 180, E.Wall);
            w.Set(150 * w.W + 60, E.Laser);
            w.InitParticle(150 * w.W + 60, E.Laser, 1, 0);
            Run(w, 50);
            int laser = -1;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Laser) laser = i;
            Assert.GreaterOrEqual(laser, 0);
            Assert.AreEqual(0f, w.Vx[laser]);
            Assert.AreEqual(1f, w.Vy[laser], "turned to head down");
            Assert.Greater(laser / w.W, 160);
        }
    }
}
