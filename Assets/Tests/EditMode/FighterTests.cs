using System.IO;
using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class FighterTests
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

        [Test]
        public void ChasesThePlayer()
        {
            var w = new World(1);
            w.AddPlayer(100, H - 1);
            Player f = w.AddFighter(250, H - 1);
            Run(w, 250);
            Assert.Less(f.X, 120, "fighter closed in");
        }

        [Test]
        public void JumpsOverAWallToReachThePlayer()
        {
            var w = new World(2);
            Fill(w, 180, 184, H - 8, H, E.Wall);
            w.AddPlayer(100, H - 1);
            Player f = w.AddFighter(250, H - 1);
            Run(w, 400);
            Assert.Less(f.X, 180, "got past the wall");
        }

        [Test]
        public void BeatsUpAPlayerWhoDoesNotFightBack()
        {
            var w = new World(3);
            w.AddPlayer(200, H - 1);
            w.AddFighter(206, H - 1);
            Run(w, 2000);
            Assert.IsTrue(w.Players[0] == null, "player knocked out");
            Assert.AreEqual(1, w.Fighters.Count);
        }

        [Test]
        public void PlayerCanPunchFightersOut()
        {
            var w = new World(4);
            Player p = w.AddPlayer(200, H - 1);
            w.AddFighter(206, H - 1);
            p.Shoot = true; // keep swinging
            for (int k = 0; k < 600 && w.Fighters.Count > 0; k++)
            {
                if (w.Fighters.Count > 0) p.Facing = w.Fighters[0].X > p.X ? 1 : -1;
                w.Step();
            }
            Assert.AreEqual(0, w.Fighters.Count, "fighter knocked out");
            Assert.IsTrue(w.Players[0] != null, "player survived");
        }

        [Test]
        public void FightersBrawlWithEachOther()
        {
            var w = new World(5);
            w.AddFighter(200, H - 1);
            w.AddFighter(206, H - 1);
            Run(w, 3000);
            Assert.Less(w.Fighters.Count, 2);
        }

        [Test]
        public void HitsKnockBack()
        {
            var w = new World(6);
            Player p = w.AddPlayer(200, H - 1);
            Player f = w.AddFighter(205, H - 1);
            Run(w, 5);
            p.Facing = 1;
            p.Shoot = true;
            w.Step();
            Assert.AreEqual(Player.FighterHp - 1, f.Hp);
            Assert.Greater(f.Vx, 0, "pushed away");
        }

        [Test]
        public void FireKillsFighters()
        {
            var w = new World(7);
            w.AddFighter(200, H - 1);
            w.Brush(203, H - 5, 1, E.Fire, 0, 1);
            Run(w, 5);
            Assert.AreEqual(0, w.Fighters.Count);
        }

        [Test]
        public void SpawnLimitsAndErase()
        {
            var w = new World(8);
            for (int k = 0; k < World.MaxFighters; k++) Assert.IsTrue(w.AddFighter(10 + k * 6, 100) != null);
            Assert.IsTrue(w.AddFighter(200, 50) == null, "capped");
            w.Erase(100, 95, 20);
            Assert.Less(w.Fighters.Count, World.MaxFighters);
            w.Clear();
            Assert.AreEqual(0, w.Fighters.Count);
        }

        [Test]
        public void SurviveSaveAndLoad()
        {
            var w = new World(9);
            w.AddFighter(100, H - 1).Hp = 2;
            w.AddFighter(300, H - 1);
            var ms = new MemoryStream();
            w.Save(ms);
            ms.Position = 0;
            var other = new World(10);
            other.Load(ms);
            Assert.AreEqual(2, other.Fighters.Count);
            Assert.AreEqual(2, other.Fighters[0].Hp);
            Assert.IsTrue(other.Fighters[0].Fighter);
        }
    }
}
