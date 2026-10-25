// Boiling, steam, clouds and rain: a small water cycle.
namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        /// <summary>Most steam a single cloud cell can hold.</summary>
        public const int CloudCapacity = 30;

        bool IsHotAt(int x, int y)
        {
            byte t = At(x, y);
            return t == E.Fire || t == E.Magma || t == E.Torch || (t == E.Wood && Life[y * W + x] > 0);
        }

        /// <summary>Turns water into steam; salt water leaves its salt behind half the time.</summary>
        void Boil(int j)
        {
            if (Type[j] == E.SaltWater && Rand() < 0.5f) Set(j, E.Salt);
            else Set(j, E.Steam);
        }

        /// <summary>Water and salt water flow, and boil when they touch something hot.</summary>
        void UpdWater(int x, int y, int i, byte t)
        {
            int k = Rnd(8);
            if (IsHotAt(x + DX8[k], y + DY8[k]) && Rand() < 0.3f) { Boil(i); return; }
            MoveLiquid(x, y, i, t);
        }

        /// <summary>
        /// Rises and drifts. Clouds soak it up. Steam that is still around when its time is
        /// up condenses into a new cloud if something is above it (it has collected under
        /// the ceiling or a roof); out in the open it fades away.
        /// </summary>
        void UpdSteam(int x, int y, int i)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            byte nt = At(nx, ny);
            if (nt == E.Cloud)
            {
                int j = ny * W + nx;
                if (Life[j] < CloudCapacity) { Life[j]++; Del(i); return; }
            }
            else if (nt == E.Ice) { Set(i, E.Water); return; }

            if (--Life[i] <= 0)
            {
                if (At(x, y - 1) != E.Empty) { Set(i, E.Cloud); Life[i] = 1; }
                else Del(i);
                return;
            }
            MoveGas(x, y, i, E.Steam, 0.5f);
        }

        /// <summary>
        /// Hangs in the air and drifts with the wind. Life is the steam it has absorbed:
        /// a dry cloud never rains; a wet one rains a drop per unit of steam, and a very
        /// wet one now and then throws lightning.
        /// </summary>
        void UpdCloud(int x, int y, int i)
        {
            int r = AirPush(x, y, i, E.Cloud);
            if (r == -2) return;
            if (r == -1 && Rand() < 0.02f) r = TryMove(i, x + (Rand() < 0.5f ? -1 : 1), y);
            if (r == -2) return;
            if (r >= 0) { i = r; x = r % W; y = r / W; }

            // Share moisture so it rains from the whole cloud, not just where the steam came in.
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            if (At(nx, ny) == E.Cloud)
            {
                int j = ny * W + nx;
                if (Life[i] > Life[j] + 1) { Life[i]--; Life[j]++; }
            }

            if (Life[i] <= 0 || At(x, y + 1) != E.Empty) return;
            if (Rand() < 0.01f)
            {
                Set(i + W, E.Water);
                Life[i]--;
            }
            else if (Life[i] >= CloudCapacity * 2 / 3 && Rand() < 0.0005f)
            {
                Set(i + W, E.Thunder);
            }
        }
    }
}
