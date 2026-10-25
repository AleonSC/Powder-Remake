// Player movement, hazards, picking up and shooting materials.
using System;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        public const int MaxPlayers = 2;
        const float WalkSpeed = 1f, JumpSpeed = 2.8f, Gravity = 0.2f;

        /// <summary>Player slots; slot 0 is driven by the arrow keys, slot 1 by WASD. Null when empty.</summary>
        public readonly Player[] Players = new Player[MaxPlayers];

        /// <summary>Places a player with its feet at (x, y). Returns null if there is no free slot or no room.</summary>
        public Player AddPlayer(int x, int y)
        {
            for (int s = 0; s < MaxPlayers; s++)
            {
                if (Players[s] != null) continue;
                if (BoxBlocked(x, y)) return null;
                Players[s] = new Player { X = x + 0.5f, Y = y + 0.5f };
                return Players[s];
            }
            return null;
        }

        bool SolidForPlayer(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return !SidesOpen;
            MoveState s = State[Type[y * W + x]];
            return s == MoveState.Powder || s == MoveState.Static;
        }

        /// <summary>Whether a body with its feet at (fx, fy) would overlap anything solid.</summary>
        bool BoxBlocked(int fx, int fy)
        {
            for (int y = fy - Player.Height + 1; y <= fy; y++)
                for (int x = fx - Player.HalfWidth; x <= fx + Player.HalfWidth; x++)
                    if (SolidForPlayer(x, y)) return true;
            return false;
        }

        static bool IsDeadly(byte t)
        {
            return t == E.Fire || t == E.Magma || t == E.Acid || t == E.Thunder || t == E.Laser ||
                   t == E.Bolt || t == E.Virus;
        }

        static bool CanCarry(byte t)
        {
            return State[t] == MoveState.Powder || State[t] == MoveState.Liquid || t == E.Gas;
        }

        void StepPlayers()
        {
            for (int s = 0; s < MaxPlayers; s++)
            {
                if (Players[s] != null && !StepPlayer(Players[s])) Players[s] = null;
            }
        }

        /// <returns>False if the player died or left the world.</returns>
        bool StepPlayer(Player p)
        {
            int cx = (int)Math.Floor(p.X), cy = (int)Math.Floor(p.Y);

            // Check everything touching the body (one dot of margin, plus the ground).
            int liquid = 0;
            for (int y = cy - Player.Height; y <= cy + 1; y++)
            {
                for (int x = cx - Player.HalfWidth - 1; x <= cx + Player.HalfWidth + 1; x++)
                {
                    byte t = At(x, y);
                    if (t == 0 || t == E.Out) continue;
                    if (IsDeadly(t)) { KillPlayer(cx, cy); return false; }
                    if (State[t] == MoveState.Liquid) liquid++;
                    if (y <= cy && CanCarry(t)) p.Held = t;
                }
            }
            bool inLiquid = liquid > 4;
            p.Grounded = BoxBlocked(cx, cy + 1);

            // Wind and blasts throw the player around.
            int ai = (Math.Min(Math.Max(cy - 5, 0), H - 1) >> 2) * AW + (Math.Min(Math.Max(cx, 0), W - 1) >> 2);
            p.Vx += AirVx[ai] * 0.05f;
            p.Vy += AirVy[ai] * 0.05f;

            int dir = (p.Right ? 1 : 0) - (p.Left ? 1 : 0);
            if (dir != 0) p.Facing = dir;
            p.Vx += (dir * WalkSpeed - p.Vx) * (p.Grounded ? 0.5f : 0.1f);
            if (p.Jump && p.Grounded) p.Vy = -JumpSpeed;
            else if (p.Jump && inLiquid) p.Vy = Math.Max(p.Vy - 0.3f, -1.5f); // swim up
            p.Vy += inLiquid ? 0.04f : Gravity;
            if (inLiquid) { p.Vx *= 0.9f; p.Vy *= 0.9f; }
            p.Vx = Clamp(p.Vx, -4, 4);
            p.Vy = Clamp(p.Vy, -6, 6);
            if (p.Grounded && Math.Abs(p.Vx) > 0.2f) p.Walk++;

            // Horizontal, stepping up ledges of up to 2 dots while on the ground.
            float nx = p.X + p.Vx;
            int tx = (int)Math.Floor(nx);
            bool blocked = false, climbed = false;
            while (cx != tx)
            {
                int sx = tx > cx ? 1 : -1;
                if (!BoxBlocked(cx + sx, cy)) cx += sx;
                else if (p.Grounded && !BoxBlocked(cx + sx, cy - 1)) { cx += sx; cy -= 1; climbed = true; }
                else if (p.Grounded && !BoxBlocked(cx + sx, cy - 2)) { cx += sx; cy -= 2; climbed = true; }
                else { blocked = true; break; }
            }
            if (blocked) { p.X = cx + 0.5f; p.Vx = 0; }
            else p.X = nx;
            if (climbed) p.Y = cy + 0.5f;

            // Vertical.
            float ny = p.Y + p.Vy;
            int ty = (int)Math.Floor(ny);
            blocked = false;
            while (cy != ty)
            {
                int sy = ty > cy ? 1 : -1;
                if (!BoxBlocked(cx, cy + sy)) cy += sy;
                else { blocked = true; break; }
            }
            if (blocked) { p.Y = cy + 0.5f; p.Vy = 0; }
            else p.Y = ny;

            if (cx < 0 || cx >= W || cy - Player.Height >= H) return false; // fell out of an open world

            // Shoot the carried material from the hand.
            if (p.Shoot && p.Held != 0)
            {
                int sx = cx + p.Facing * (Player.HalfWidth + 2), sy = cy - 5;
                if (At(sx, sy) == E.Empty)
                {
                    int j = sy * W + sx;
                    Set(j, p.Held);
                    InitParticle(j, p.Held, p.Facing, 0);
                }
            }
            return true;
        }

        /// <summary>The body bursts into white sparks.</summary>
        void KillPlayer(int cx, int cy)
        {
            for (int y = cy - Player.Height + 1; y <= cy; y++)
            {
                for (int x = cx - Player.HalfWidth - 1; x <= cx + Player.HalfWidth + 1; x++)
                {
                    if (At(x, y) != E.Empty || Rand() < 0.4f) continue;
                    int j = y * W + x;
                    Set(j, E.Spark);
                    Vx[j] = (Rand() - 0.5f) * 3;
                    Vy[j] = -Rand() * 2;
                    Shade[j] = 7; // white
                }
            }
        }

        void ErasePlayers(int cx, int cy, int r)
        {
            for (int s = 0; s < MaxPlayers; s++)
            {
                Player p = Players[s];
                if (p == null) continue;
                int px = (int)Math.Floor(p.X), py = (int)Math.Floor(p.Y);
                // Distance from the brush centre to the nearest point of the body.
                int dx = cx - Math.Min(Math.Max(cx, px - Player.HalfWidth), px + Player.HalfWidth);
                int dy = cy - Math.Min(Math.Max(cy, py - Player.Height + 1), py);
                if (dx * dx + dy * dy <= (r + 0.5f) * (r + 0.5f)) Players[s] = null;
            }
        }
    }
}
