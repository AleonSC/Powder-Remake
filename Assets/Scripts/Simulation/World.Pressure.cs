// Liquid pressure: a connected, settled body of liquid finds its level. Drops
// move from its highest surface to its lowest opening, so U-tubes level out,
// pipes fill up to the level of their tank, and openings well below the
// surface squirt jets (fountains, holes in a tank wall).
using System;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        const int PressureInterval = 2;  // frames between pressure passes
        const int PressureMoves = 16;    // most drops one body can move per pass
        const int MinHead = 2;           // smaller height differences are left to ordinary flow
        const int JetWidth = 4;          // openings this narrow or narrower spout a jet

        int[] pressureMark, pressureQueue;
        int pressureStamp;

        // The best few candidates found in the body being levelled: its highest
        // surface drops, and its lowest openings (with the direction they face).
        readonly int[] srcIdx = new int[PressureMoves], srcY = new int[PressureMoves];
        readonly int[] outIdx = new int[PressureMoves], outY = new int[PressureMoves], outDir = new int[PressureMoves];
        int srcN, outN;

        /// <summary>Settled liquid of type t at index n (not drops in flight).</summary>
        bool InBody(int n, byte t) { return Type[n] == t && Vy[n] == 0; }

        static bool Open(byte t) { return t == 0 || State[t] == MoveState.Gas; }

        void StepPressure()
        {
            if (Frame % PressureInterval != 0) return;
            if (pressureMark == null) { pressureMark = new int[N]; pressureQueue = new int[N]; }
            pressureStamp++;
            for (int i = 0; i < N; i++)
            {
                byte t = Type[i];
                if (State[t] != MoveState.Liquid || pressureMark[i] == pressureStamp || Vy[i] != 0) continue;
                Level(i, t);
            }
        }

        /// <summary>Flood-fills the body containing start, then moves drops from its top to its lowest openings.</summary>
        void Level(int start, byte t)
        {
            int head = 0, tail = 0;
            pressureQueue[tail++] = start;
            pressureMark[start] = pressureStamp;
            srcN = outN = 0;
            while (head < tail)
            {
                int c = pressureQueue[head++], x = c % W, y = c / W;
                if (y > 0)
                {
                    int u = c - W;
                    if (InBody(u, t)) Visit(u, ref tail);
                    else if (Open(Type[u]))
                    {
                        AddSource(c, y);
                        AddOutlet(u, y - 1, 0); // the top of a pipe or a lower arm
                    }
                }
                if (y < H - 1 && InBody(c + W, t)) Visit(c + W, ref tail);
                for (int s = -1; s <= 1; s += 2)
                {
                    int nx = x + s;
                    if (nx < 0 || nx >= W) continue;
                    int n = c + s;
                    if (InBody(n, t)) Visit(n, ref tail);
                    else if (Open(Type[n])) AddOutlet(n, y, s); // a hole in the side
                }
            }
            if (srcN == 0 || outN == 0) return;

            SortBy(srcIdx, srcY, null, srcN, false);  // highest surface first
            SortBy(outIdx, outY, outDir, outN, true); // lowest opening first
            for (int k = 0; k < srcN && k < outN; k++)
            {
                int s = srcIdx[k], o = outIdx[k];
                int depth = outY[k] - srcY[k];
                if (depth < MinHead) break;
                if (s == o || Type[s] != t || !Open(Type[o])) continue;
                Swap(s, o);
                // A narrow opening squirts as fast as the water above would have fallen
                // (v = sqrt(2gh)); a wide surface just rises calmly.
                float speed = Math.Min(MaxLooseSpeed, (float)Math.Sqrt(2 * FallGravity * depth));
                if (outDir[k] != 0) { Vx[o] = outDir[k] * speed; Vy[o] = -0.01f; } // hole in the side: airborne, so it flies
                else if (OpeningWidth(o, t) <= JetWidth) { Vx[o] = 0; Vy[o] = -speed; }
            }
        }

        /// <summary>
        /// How wide the channel feeding an upward opening is: the run of liquid or open
        /// cells, between solid walls, on the row just below it (up to JetWidth + 1).
        /// </summary>
        int OpeningWidth(int o, byte t)
        {
            int b = o + W;
            if (b >= N) return JetWidth + 1;
            int x = b % W, width = 1;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 1; k <= JetWidth; k++)
                {
                    int nx = x + s * k;
                    if (nx < 0 || nx >= W) break;
                    byte n = Type[b + s * k];
                    if (n != t && !Open(n)) break;
                    if (++width > JetWidth) return width;
                }
            }
            return width;
        }

        void Visit(int n, ref int tail)
        {
            if (pressureMark[n] == pressureStamp) return;
            pressureMark[n] = pressureStamp;
            pressureQueue[tail++] = n;
        }

        /// <summary>Keeps the PressureMoves highest surface drops (smallest y).</summary>
        void AddSource(int c, int y)
        {
            if (srcN < PressureMoves) { srcIdx[srcN] = c; srcY[srcN++] = y; return; }
            int worst = 0;
            for (int k = 1; k < srcN; k++) if (srcY[k] > srcY[worst]) worst = k;
            if (y < srcY[worst] || (y == srcY[worst] && Rand() < 0.3f)) { srcIdx[worst] = c; srcY[worst] = y; }
        }

        /// <summary>Keeps the PressureMoves lowest openings (largest y).</summary>
        void AddOutlet(int c, int y, int dir)
        {
            if (outN < PressureMoves) { outIdx[outN] = c; outY[outN] = y; outDir[outN++] = dir; return; }
            int worst = 0;
            for (int k = 1; k < outN; k++) if (outY[k] < outY[worst]) worst = k;
            if (y > outY[worst] || (y == outY[worst] && Rand() < 0.3f))
            {
                outIdx[worst] = c;
                outY[worst] = y;
                outDir[worst] = dir;
            }
        }

        /// <summary>Insertion sort of the few candidates by y.</summary>
        static void SortBy(int[] idx, int[] ys, int[] dirs, int n, bool descending)
        {
            for (int a = 1; a < n; a++)
            {
                for (int b = a; b > 0; b--)
                {
                    bool swap = descending ? ys[b] > ys[b - 1] : ys[b] < ys[b - 1];
                    if (!swap) break;
                    int tmp = idx[b]; idx[b] = idx[b - 1]; idx[b - 1] = tmp;
                    tmp = ys[b]; ys[b] = ys[b - 1]; ys[b - 1] = tmp;
                    if (dirs != null) { tmp = dirs[b]; dirs[b] = dirs[b - 1]; dirs[b - 1] = tmp; }
                }
            }
        }
    }
}
