// Sand, glass, salt, snow, wood, smoke, mercury, cryo and void.
namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        /// <summary>Dissolves into water (making salt water) and melts ice and snow.</summary>
        void UpdSalt(int x, int y, int i)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            byte nt = At(nx, ny);
            if (nt == E.Water && Rand() < 0.05f)
            {
                Set(ny * W + nx, E.SaltWater);
                Del(i);
                return;
            }
            if ((nt == E.Ice || nt == E.Snow) && Rand() < 0.02f) Set(ny * W + nx, E.Water);
            MovePowder(x, y, i, E.Salt);
        }

        /// <summary>Flutters down and drifts in the wind; slowly melts in water.</summary>
        void UpdSnow(int x, int y, int i)
        {
            int k = Rnd(8);
            if (At(x + DX8[k], y + DY8[k]) == E.Water && Rand() < 0.002f) { Set(i, E.Water); return; }
            if (Rand() < 0.4f)
            {
                if (AirPush(x, y, i, E.Snow) == -1) TryMove(i, x + Rnd(3) - 1, y);
                return;
            }
            MovePowder(x, y, i, E.Snow);
        }

        /// <summary>
        /// Life &gt; 0 means burning: wood smoulders in place, throwing off fire and smoke,
        /// then crumbles to ash. It only burns down while it touches air, so blocks burn
        /// from the outside in. Water puts it out.
        /// </summary>
        void UpdWood(int x, int y, int i)
        {
            if (Life[i] == 0) return;
            bool air = false;
            for (int n = 0; n < 2; n++)
            {
                int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
                byte nt = At(nx, ny);
                if (nt == E.Out) continue;
                int j = ny * W + nx;
                if (nt == E.Water || nt == E.SaltWater) { Life[i] = 0; return; }
                if (nt == E.Empty || State[nt] == MoveState.Gas) air = true;
                if (nt == E.Empty)
                {
                    if (Rand() < 0.3f) { Set(j, E.Fire); Life[j] = (short)(5 + Rnd(10)); }
                    else if (Rand() < 0.05f) Set(j, E.Smoke);
                }
                else if (Flam[nt] != 0 && Rand() < 0.05f) Ignite(j);
            }
            if (air && --Life[i] == 0) Set(i, Rand() < 0.6f ? E.Ash : E.Empty);
        }

        /// <summary>A heavy liquid that conducts electricity like metal.</summary>
        void UpdMercury(int x, int y, int i)
        {
            Conduct(x, y, i);
            MoveLiquid(x, y, i, E.Mercury);
        }

        /// <summary>Liquid nitrogen: freezes what it touches, then boils away.</summary>
        void UpdCryo(int x, int y, int i)
        {
            if (--Life[i] <= 0) { Del(i); return; }
            for (int n = 0; n < 2; n++)
            {
                int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
                byte nt = At(nx, ny);
                if (nt == 0 || nt == E.Out) continue;
                int j = ny * W + nx;
                switch (nt)
                {
                    case E.Water: if (Rand() < 0.2f) Set(j, E.Ice); break;
                    case E.SaltWater: if (Rand() < 0.05f) Set(j, E.Ice); break;
                    case E.Steam: Set(j, E.Water); break;
                    case E.Fire: Del(j); break;
                    case E.Magma: Set(j, E.Stone); break;
                }
            }
            MoveLiquid(x, y, i, E.Cryo);
        }

        /// <summary>A black hole: swallows anything touching it and sucks in the surrounding air.</summary>
        void UpdVoid(int x, int y)
        {
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX8[k], ny = y + DY8[k];
                byte nt = At(nx, ny);
                if (nt == 0 || nt == E.Out || nt == E.Wall || nt == E.Void) continue;
                if (Rand() < 0.5f) Del(ny * W + nx);
            }
            int ai = (y >> 2) * AW + (x >> 2);
            Pressure[ai] = Clamp(Pressure[ai] - 0.2f, -PMax, PMax);
        }
    }
}
