using NUnit.Framework;
using PowderGame.Simulation;

namespace PowderGame.Tests
{
    public class MaterialTests
    {
        const int W = World.DefaultWidth, H = World.DefaultHeight;

        static int Count(World w, byte t)
        {
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) n++;
            return n;
        }

        static double MeanY(World w, byte t)
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == t) { sum += i / W; n++; }
            return sum / n;
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
        public void SaltDissolvesIntoSaltWaterThatSinks()
        {
            var w = new World(1);
            Fill(w, 150, 250, 270, H, E.Water);
            Fill(w, 190, 210, 200, 210, E.Salt);
            Run(w, 800);
            Assert.Greater(Count(w, E.SaltWater), 50);
            Assert.Greater(MeanY(w, E.SaltWater), MeanY(w, E.Water), "salt water sits under fresh water");
        }

        [Test]
        public void SaltWaterDoesNotFreezeButWaterDoes()
        {
            var w = new World(2);
            Fill(w, 98, 100, 280, H, E.Wall);
            Fill(w, 150, 152, 280, H, E.Wall);
            Fill(w, 248, 250, 280, H, E.Wall);
            Fill(w, 300, 302, 280, H, E.Wall);
            Fill(w, 100, 150, 290, H, E.SaltWater);
            Fill(w, 250, 300, 290, H, E.Water);
            Fill(w, 100, 150, 288, 290, E.Ice);
            Fill(w, 250, 300, 288, 290, E.Ice);
            Run(w, 1500);
            Assert.AreEqual(500, Count(w, E.SaltWater));
            Assert.Less(Count(w, E.Water), 500);
        }

        [Test]
        public void MagmaMeltsSandIntoGlass()
        {
            var w = new World(3);
            Fill(w, 150, 250, 280, H, E.Sand);
            w.Brush(200, 260, 6, E.Magma, 0, 1);
            Run(w, 600);
            Assert.Greater(Count(w, E.Glass), 0);
        }

        [Test]
        public void LaserShinesThroughGlass()
        {
            var w = new World(4);
            Fill(w, 200, 205, 100, 200, E.Glass);
            w.Set(150 * W + 60, E.Laser);
            w.InitParticle(150 * W + 60, E.Laser, 1, 0);
            Run(w, 40);
            int laser = -1;
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Laser) laser = i;
            Assert.Greater(laser % W, 205, "laser is beyond the glass");
            Assert.AreEqual(500, Count(w, E.Glass), "glass untouched");
        }

        [Test]
        public void AcidCannotEatGlass()
        {
            var w = new World(5);
            Fill(w, 150, 250, 296, H, E.Glass);
            Fill(w, 150, 250, 280, 296, E.Acid);
            Run(w, 500);
            Assert.AreEqual(400, Count(w, E.Glass));
        }

        [Test]
        public void SnowMeltsNearFire()
        {
            var w = new World(6);
            Fill(w, 150, 250, 290, H, E.Snow);
            w.Set(280 * W + 160, E.Torch);
            Fill(w, 150, 250, 280, 281, E.Wall);
            Fill(w, 150, 250, 289, 290, E.Fire);
            Run(w, 5);
            Assert.Greater(Count(w, E.Water), 0);
        }

        [Test]
        public void WoodBurnsToAshAndSmoke()
        {
            var w = new World(7);
            Fill(w, 180, 220, 250, 290, E.Wood);
            Fill(w, 180, 220, 249, 250, E.Fire);
            bool smoke = false;
            for (int k = 0; k < 3000; k++)
            {
                w.Step();
                if (Count(w, E.Smoke) > 0) smoke = true;
            }
            Assert.IsTrue(smoke, "gave off smoke");
            Assert.Less(Count(w, E.Wood), 400, "mostly burned");
            Assert.Greater(Count(w, E.Ash), 0, "left ash");
        }

        [Test]
        public void WaterPutsOutBurningWood()
        {
            var w = new World(8);
            Fill(w, 168, 170, 200, H, E.Wall);
            Fill(w, 230, 232, 200, H, E.Wall);
            Fill(w, 180, 220, 280, H, E.Wood);
            for (int i = 0; i < w.N; i++) if (w.Type[i] == E.Wood) w.Life[i] = 100; // all burning
            Fill(w, 170, 230, 200, 260, E.Water);
            Run(w, 400);
            Assert.Greater(Count(w, E.Wood), 600, "most wood survived");
        }

        [Test]
        public void StoneFloatsOnMercury()
        {
            var w = new World(9);
            Fill(w, 150, 250, 270, H, E.Mercury);
            Fill(w, 190, 210, 200, 210, E.Stone);
            Run(w, 600);
            Assert.Less(MeanY(w, E.Stone), MeanY(w, E.Mercury));
        }

        [Test]
        public void MercuryCarriesElectricity()
        {
            var w = new World(10);
            Fill(w, 100, 300, 290, H, E.Mercury);
            Fill(w, 100, 300, 289, 290, E.Wall);  // lid so it can't splash
            Fill(w, 300, 302, 280, H, E.Metal);
            w.Life[295 * W + 101] = 10;           // charge one end
            bool reached = false;
            for (int k = 0; k < 120 && !reached; k++)
            {
                w.Step();
                reached = w.Life[290 * W + 300] > 0 || w.Life[290 * W + 300] < 0;
            }
            Assert.IsTrue(reached, "charge crossed the mercury into the metal");
        }

        [Test]
        public void CryoFreezesWaterAndBoilsAway()
        {
            var w = new World(12);
            Fill(w, 150, 250, 285, H, E.Water);
            Fill(w, 150, 250, 270, 285, E.Cryo);
            Run(w, 1000);
            Assert.Greater(Count(w, E.Ice), 100);
            Assert.AreEqual(0, Count(w, E.Cryo), "evaporated");
        }

        [Test]
        public void CryoQuenchesMagma()
        {
            var w = new World(13);
            Fill(w, 150, 250, 290, H, E.Magma);
            Fill(w, 150, 250, 270, 290, E.Cryo);
            Run(w, 300);
            Assert.Greater(Count(w, E.Stone), 200);
        }

        [Test]
        public void VoidSwallowsThingsAndPullsAir()
        {
            var w = new World(14);
            Fill(w, 190, 210, 280, 290, E.Void);
            w.Brush(200, 200, 8, E.Powder, 0, 1);
            int before = Count(w, E.Powder);
            Run(w, 300);
            Assert.Less(Count(w, E.Powder), before / 4);
            Assert.Less(w.Pressure[(285 / World.AS) * w.AW + 200 / World.AS], 0, "low pressure at the void");
        }

    }
}
