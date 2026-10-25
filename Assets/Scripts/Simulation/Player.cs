namespace PowderGame.Simulation
{
    /// <summary>
    /// A stick figure that lives outside the cell grid: either a keyboard-steered player
    /// or an AI-driven fighter.
    /// </summary>
    public sealed class Player
    {
        /// <summary>Body size in dots: rows Y-Height+1..Y, columns X-HalfWidth..X+HalfWidth.</summary>
        public const int Height = 10, HalfWidth = 1;
        public const int PlayerHp = 10, FighterHp = 3;

        /// <summary>Position of the feet in dots; the bottom-centre body cell is (floor(X), floor(Y)).</summary>
        public float X, Y;
        public float Vx, Vy;
        /// <summary>-1 facing left, 1 facing right.</summary>
        public int Facing = 1;
        /// <summary>Material the player is carrying (and shoots), 0 for none. Fighters carry nothing.</summary>
        public byte Held;
        /// <summary>Animation counter; advances while walking.</summary>
        public int Walk;
        public bool Grounded;

        public bool Fighter;
        public float Speed = 1;
        public int Hp = PlayerHp;
        /// <summary>Set when knocked out; the body bursts on the next step.</summary>
        public bool Dead;
        /// <summary>Frames until the next attack is possible.</summary>
        public int Cooldown;
        /// <summary>Frames left of the punch and hurt animations.</summary>
        public int Punch, Hurt;

        // Fighter AI: frames until it picks a new wander direction, and that direction.
        public int Think, WanderDir;

        // Controls, set by the input layer (players) or the AI (fighters) before each step.
        // Shoot also attacks whatever is in front.
        public bool Left, Right, Jump, Shoot;
    }
}
