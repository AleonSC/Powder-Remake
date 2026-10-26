// Rigid solids: connected pieces of metal, glass, wood and ice fall as one,
// keeping their shape, unless they are welded to a fixed anchor (BLOCK or
// another fixed thing such as a torch, fan or tree) or resting on something.
// Metal and glass are heavy and sink through liquids, pushing the liquid up
// around them; wood and ice float.
using System;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        const float MaxRigidSpeed = 8;

        int[] rigidBody, rigidQueue;
        int rigidId;
        int[] bodyCells = new int[256];
        int[] bodyKeys = new int[256];

        static bool IsRigid(byte t) { return t == E.Metal || t == E.Glass || t == E.Wood || t == E.Ice; }

        /// <summary>Things a rigid piece can be welded to: blocks and any other fixed material.</summary>
        static bool IsAnchor(byte t) { return t == E.Wall || (State[t] == MoveState.Static && !IsRigid(t)); }

        void StepRigid()
        {
            if (rigidBody == null) { rigidBody = new int[N]; rigidQueue = new int[N]; }
            int firstId = rigidId; // bodies gathered this frame get ids above this
            for (int i = 0; i < N; i++)
            {
                if (IsRigid(Type[i]) && rigidBody[i] <= firstId) FallBody(i);
            }
        }

        /// <summary>Gathers the piece containing start and lets it fall if nothing holds it up.</summary>
        void FallBody(int start)
        {
            int id = ++rigidId;
            int head = 0, tail = 0;
            rigidQueue[tail++] = start;
            rigidBody[start] = id;
            bool anchored = false, heavy = false;
            while (head < tail)
            {
                int c = rigidQueue[head++], x = c % W, y = c / W;
                byte t = Type[c];
                if (t == E.Metal || t == E.Glass) heavy = true;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + DX8[k], ny = y + DY8[k];
                    if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                    int n = ny * W + nx;
                    byte nt = Type[n];
                    if (IsRigid(nt))
                    {
                        if (rigidBody[n] != id) { rigidBody[n] = id; rigidQueue[tail++] = n; }
                    }
                    else if (IsAnchor(nt)) anchored = true;
                }
            }
            if (anchored) return;

            // Bottom cells first, so whatever the piece falls through moves up past it.
            if (bodyCells.Length < tail) { bodyCells = new int[tail * 2]; bodyKeys = new int[tail * 2]; }
            for (int k = 0; k < tail; k++) { bodyCells[k] = rigidQueue[k]; bodyKeys[k] = -rigidQueue[k]; }
            Array.Sort(bodyKeys, bodyCells, 0, tail);

            float v = Math.Min(MaxRigidSpeed, Math.Max(1, Vy[start] + FallGravity));
            int steps = (int)v, moved = 0;
            for (; moved < steps; moved++)
            {
                if (!CanDrop(tail, id, heavy)) break;
                for (int k = 0; k < tail; k++)
                {
                    int c = bodyCells[k];
                    if (c + W >= N) { Del(c); continue; } // out of an open world
                    Swap(c, c + W);
                    rigidBody[c + W] = id;
                    bodyCells[k] = c + W;
                }
            }
            float speed = moved == steps ? v : 0; // landed if it stopped short
            for (int k = 0; k < tail; k++) if (Type[bodyCells[k]] != 0) Vy[bodyCells[k]] = speed;
        }

        /// <summary>Whether every cell under the piece is open (or, for heavy pieces, liquid).</summary>
        bool CanDrop(int count, int id, bool heavy)
        {
            for (int k = 0; k < count; k++)
            {
                int c = bodyCells[k];
                if (c + W >= N)
                {
                    if (!SidesOpen) return false;
                    continue;
                }
                int b = c + W;
                if (rigidBody[b] == id && IsRigid(Type[b])) continue;
                byte t = Type[b];
                if (t == 0 || State[t] == MoveState.Gas) continue;
                if (heavy && State[t] == MoveState.Liquid) continue;
                return false;
            }
            return true;
        }
    }
}
