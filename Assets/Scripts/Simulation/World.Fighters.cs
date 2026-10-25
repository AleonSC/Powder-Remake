// FIGHTER: AI stick figures that hunt players and brawl with each other, plus
// the melee combat shared with players.
using System;
using System.Collections.Generic;

namespace PowderGame.Simulation
{
    public sealed partial class World
    {
        public const int MaxFighters = 64;
        const float FighterSpeed = 0.7f;            // a bit slower than players, so you can run
        const int AttackCooldown = 20, Reach = 4;   // frames between swings; dots in front
        const float ChaseRangeX = 200, ChaseRangeY = 80, BrawlRangeX = 60, BrawlRangeY = 20;

        public readonly List<Player> Fighters = new List<Player>();

        /// <summary>Places a fighter with its feet at (x, y). Returns null if there are too many or no room.</summary>
        public Player AddFighter(int x, int y)
        {
            if (Fighters.Count >= MaxFighters || BoxBlocked(x, y)) return null;
            var f = new Player
            {
                X = x + 0.5f, Y = y + 0.5f, Fighter = true, Speed = FighterSpeed, Hp = Player.FighterHp,
                Facing = Rand() < 0.5f ? -1 : 1,
            };
            Fighters.Add(f);
            return f;
        }

        /// <summary>Chases the nearest player, or brawls with a nearby fighter, or wanders.</summary>
        void ThinkFighter(Player f)
        {
            Player target = null;
            float best = float.MaxValue;
            for (int s = 0; s < MaxPlayers; s++)
                Consider(f, Players[s], ChaseRangeX, ChaseRangeY, ref target, ref best);
            if (target == null)
                for (int k = 0; k < Fighters.Count; k++)
                    Consider(f, Fighters[k], BrawlRangeX, BrawlRangeY, ref target, ref best);

            f.Shoot = false;
            if (target != null)
            {
                float dx = target.X - f.X, dy = target.Y - f.Y;
                bool close = Math.Abs(dx) <= Reach - 1;
                f.Left = !close && dx < 0;
                f.Right = !close && dx > 0;
                if (close) f.Facing = dx >= 0 ? 1 : -1;
                f.Shoot = Math.Abs(dx) <= Reach + 1 && Math.Abs(dy) <= 6 && Rand() < 0.5f;
            }
            else
            {
                if (--f.Think <= 0)
                {
                    f.Think = 60 + Rnd(90);
                    f.WanderDir = Rnd(3) - 1;
                }
                f.Left = f.WanderDir < 0;
                f.Right = f.WanderDir > 0;
            }

            // Jump over anything too tall to step up, or up towards a target above.
            int cx = (int)Math.Floor(f.X), cy = (int)Math.Floor(f.Y);
            int ahead = f.Right ? 1 : f.Left ? -1 : 0;
            bool wall = ahead != 0 && BoxBlocked(cx + ahead * 2, cy - 2);
            bool above = target != null && target.Y < f.Y - 15;
            f.Jump = (wall && Rand() < 0.2f) || (above && Rand() < 0.03f);
            if (wall && f.Grounded && target == null && Rand() < 0.05f) f.WanderDir = -f.WanderDir;
        }

        static void Consider(Player f, Player o, float rx, float ry, ref Player target, ref float best)
        {
            if (o == null || o == f || o.Dead) return;
            float dx = o.X - f.X, dy = o.Y - f.Y;
            if (Math.Abs(dx) > rx || Math.Abs(dy) > ry) return;
            float d = dx * dx + dy * dy;
            if (d < best) { best = d; target = o; }
        }

        /// <summary>Swings at whatever is in front while Shoot is held, on a cooldown.</summary>
        void Attack(Player p)
        {
            if (p.Punch > 0) p.Punch--;
            if (p.Hurt > 0) p.Hurt--;
            if (p.Cooldown > 0) { p.Cooldown--; return; }
            if (!p.Shoot || p.Dead) return;
            p.Cooldown = AttackCooldown;
            p.Punch = 6;
            float hx = p.X + p.Facing * Reach;
            for (int s = 0; s < MaxPlayers; s++) TryHit(p, Players[s], hx);
            for (int k = 0; k < Fighters.Count; k++) TryHit(p, Fighters[k], hx);
        }

        static void TryHit(Player p, Player o, float hx)
        {
            if (o == null || o == p || o.Dead) return;
            if (Math.Abs(o.X - hx) > Reach || Math.Abs(o.Y - p.Y) > 6) return;
            o.Hp--;
            o.Hurt = 8;
            o.Vx = p.Facing * 2.5f;
            o.Vy = -1.8f;
            if (o.Hp <= 0) o.Dead = true;
        }
    }
}
