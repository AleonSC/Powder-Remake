namespace PowderGame.Simulation
{
    /// <summary>A stick figure steered with the keyboard. Lives outside the cell grid.</summary>
    public sealed class Player
    {
        /// <summary>Body size in dots: rows Y-Height+1..Y, columns X-HalfWidth..X+HalfWidth.</summary>
        public const int Height = 10, HalfWidth = 1;

        /// <summary>Position of the feet in dots; the bottom-centre body cell is (floor(X), floor(Y)).</summary>
        public float X, Y;
        public float Vx, Vy;
        /// <summary>-1 facing left, 1 facing right.</summary>
        public int Facing = 1;
        /// <summary>Material the player is carrying (and shoots), 0 for none.</summary>
        public byte Held;
        /// <summary>Animation counter; advances while walking.</summary>
        public int Walk;
        public bool Grounded;

        // Controls, set by the input layer before each step.
        public bool Left, Right, Jump, Shoot;
    }
}
