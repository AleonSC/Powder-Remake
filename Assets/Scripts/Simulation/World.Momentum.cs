// Momentum for powders and liquids: falling accelerates, impacts splash and
// scatter, liquid running off a ledge keeps its sideways speed (so waterfalls
// arc), and blasts and players can throw loose material. Velocity lives in Vx/Vy, which no
// powder or liquid uses for anything else.
using System;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        const float FallGravity = 0.2f;   // added to falling speed each frame
        const float MaxLooseSpeed = 8;    // cells per frame
        const float SplashSpeed = 3;      // liquids landing at least this fast can splash

        static bool IsLoose(byte t) { return State[t] == MoveState.Powder || State[t] == MoveState.Liquid; }

        /// <summary>In flight: rising, or with only air (or gas) beneath it.</summary>
        bool Airborne(int y, int i)
        {
            if (Vy[i] < 0) return true;
            if (y + 1 >= H) return SidesOpen;
            byte b = Type[i + W];
            return b == 0 || (Movable[b] && Dens[b] < 1);
        }

        /// <summary>
        /// Flies a loose particle along its velocity under gravity. Landing liquids can
        /// splash back up; landing powders scatter a little. Returns like TryMove.
        /// </summary>
        int Ballistic(int x, int y, int i, bool liquid)
        {
            int ai = (y >> 2) * AW + (x >> 2);
            float vx = Vx[i] * 0.99f + AirVx[ai] * 0.02f;
            float vy = Vy[i] + FallGravity;
            if (vy > 0 && vy < 1) vy = 1; // never slower than a cell a frame when falling
            vx = Clamp(vx, -MaxLooseSpeed, MaxLooseSpeed);
            vy = Clamp(vy, -MaxLooseSpeed, MaxLooseSpeed);

            int ni = Fly(x, y, i, vx, vy);
            if (ni == -2) return -2;
            if (hit)
            {
                if (hitBY)
                {
                    float impact = vy;
                    vy = 0;
                    if (impact > 0)
                    {
                        if (liquid && impact >= SplashSpeed && Rand() < 0.5f)
                        {
                            vy = -impact * 0.35f;                                  // bounce up...
                            vx += (Rand() < 0.5f ? -1 : 1) * impact * 0.4f;        // ...and out
                        }
                        else if (!liquid && impact >= 2) vx += (Rand() - 0.5f) * impact * 0.5f;
                    }
                }
                if (hitBX) vx = liquid ? 0 : -vx * 0.3f;
            }
            Vx[ni] = vx;
            Vy[ni] = vy;
            return ni == i ? -1 : ni;
        }

        /// <summary>Powder skidding along the ground after landing or being thrown.</summary>
        int Skid(int x, int y, int i)
        {
            float vx = Vx[i];
            if (vx == 0) return -1;
            if (Math.Abs(vx) <= 0.3f) { Vx[i] = 0; return -1; }
            Vx[i] = vx * 0.5f;
            int r = TryMove(i, x + (vx > 0 ? 1 : -1), y);
            if (r == -1) Vx[i] = 0;
            return r;
        }

        /// <summary>Throws loose material outward from a blast at (cx, cy).</summary>
        void Kick(int j, int dx, int dy, int r, float power)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.5f) return;
            float s = power * (1 - d / (r + 1)) * 1.2f;
            Vx[j] = Clamp(Vx[j] + dx / d * s, -MaxLooseSpeed, MaxLooseSpeed);
            Vy[j] = Clamp(Vy[j] + dy / d * s - s * 0.5f, -MaxLooseSpeed, MaxLooseSpeed);
        }
    }
}
