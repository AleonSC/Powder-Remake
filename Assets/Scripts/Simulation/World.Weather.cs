// The water cycle: sunlight and heat evaporate water, steam rises and condenses
// into clouds, clouds rain, and rain soaks into the ground as mud.
namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        /// <summary>Most steam a single cloud cell can hold.</summary>
        public const int CloudCapacity = 30;

        public enum SunLevel { Off, Low, High }

        /// <summary>Sunlight strength: evaporates open water, dries mud, melts snow and ice.</summary>
        public SunLevel Sun = SunLevel.Off;

        /// <summary>
        /// Per column, the row of the first thing sunlight hits (H if it reaches the floor).
        /// Only kept up to date while the sun is on.
        /// </summary>
        public readonly int[] SunTop;

        /// <summary>Chance per frame that a sunlit water surface cell evaporates.</summary>
        float SunRate { get { return Sun == SunLevel.High ? 0.002f : Sun == SunLevel.Low ? 0.0004f : 0; } }

        /// <summary>Steam above this row condenses into clouds.</summary>
        int CloudLine { get { return H / 4; } }

        // Clouds made from steam carry this flag in Vy and fade away once they rain dry;
        // painted clouds don't.
        const float NaturalCloud = 1;

        /// <summary>Sunlight passes through empty space, gases and glass.</summary>
        static bool Transparent(byte t) { return t == 0 || State[t] == MoveState.Gas || t == E.Glass; }

        void ComputeSunlight()
        {
            if (Sun == SunLevel.Off) return;
            for (int x = 0; x < W; x++)
            {
                int y = 0;
                while (y < H && Transparent(Type[y * W + x])) y++;
                SunTop[x] = y;
            }
        }

        /// <summary>Whether (x, y) is the first thing the sun hits in its column.</summary>
        bool Sunlit(int x, int y) { return Sun != SunLevel.Off && SunTop[x] == y; }

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

        /// <summary>
        /// Water and salt water flow, boil when they touch something hot, and slowly
        /// evaporate where sunlight hits their surface.
        /// </summary>
        void UpdWater(int x, int y, int i, byte t)
        {
            int k = Rnd(8);
            if (IsHotAt(x + DX8[k], y + DY8[k]) && Rand() < 0.3f) { Boil(i); return; }
            if (Sunlit(x, y) && Rand() < SunRate) { Boil(i); return; }
            MoveLiquid(x, y, i, t);
        }

        /// <summary>Powder soaks up water it touches, turning into mud.</summary>
        void UpdPowder(int x, int y, int i)
        {
            int k = Rnd(8), nx = x + DX8[k], ny = y + DY8[k];
            if (At(nx, ny) == E.Water && Rand() < 0.05f)
            {
                Del(ny * W + nx);
                Set(i, E.Mud);
                return;
            }
            MovePowder(x, y, i, E.Powder);
        }

        // Where mud passes its moisture: below and to the sides, never up.
        static readonly int[] SeepX = { 0, -1, 1, -1, 1 };
        static readonly int[] SeepY = { 1, 1, 1, 0, 0 };

        /// <summary>
        /// Powder holding one drop of water. The water seeps on into dry powder below and
        /// beside it, and dries out as steam in sunlight (wicking up to a sunlit surface)
        /// or near heat. Mud is sticky: it falls straight down instead of sliding off piles.
        /// </summary>
        void UpdMud(int x, int y, int i)
        {
            bool drying = Sunlit(x, y) ? Rand() < SunRate : Rand() < 0.1f && IsHotAt(x + DX8[Rnd(8)], y + DY8[Rnd(8)]);
            if (drying && At(x, y - 1) == E.Empty)
            {
                Set(i - W, E.Steam);
                Set(i, E.Powder);
                return;
            }
            // A sunlit dry surface wicks moisture up from below, so soil dries all the way down.
            if (At(x, y - 1) == E.Powder && Sunlit(x, y - 1) && Rand() < 0.02f)
            {
                Set(i - W, E.Mud);
                Set(i, E.Powder);
                return;
            }
            int k = Rnd(SeepX.Length), nx = x + SeepX[k], ny = y + SeepY[k];
            if (At(nx, ny) == E.Powder && Rand() < 0.05f)
            {
                Set(ny * W + nx, E.Mud);
                Set(i, E.Powder);
                return;
            }
            if (AirPush(x, y, i, E.Mud) == -1) TryMove(i, x, y + 1);
        }

        /// <summary>
        /// Rises and drifts. Clouds soak it up, and it condenses back into droplets on cool
        /// glass and ice. Steam that reaches the top quarter of the screen, or is still
        /// around when its time is up, condenses into a new cloud.
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
            else if (nt == E.Glass && Rand() < 0.05f) { Set(i, E.Water); return; } // fogs up and drips

            // It condenses into cloud once it rises high enough, or wherever it is when its time runs out.
            if (--Life[i] <= 0 || (y < CloudLine && Rand() < 0.003f)) { Condense(i); return; }
            MoveGas(x, y, i, E.Steam, 0.5f);
        }

        void Condense(int i)
        {
            Set(i, E.Cloud);
            Life[i] = 1;
            Vy[i] = NaturalCloud;
        }

        /// <summary>
        /// Hangs in the air and drifts with the wind. Life is the steam it has absorbed:
        /// a dry cloud never rains; a wet one rains a drop per unit of steam, and a very
        /// wet one now and then throws lightning. Clouds formed from steam fade away once
        /// they have rained dry; painted ones stay.
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

            if (Life[i] <= 0)
            {
                if (Vy[i] == NaturalCloud && Rand() < 0.002f) Del(i); // rained dry: fade away
                return;
            }
            if (At(x, y + 1) != E.Empty) return;
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
