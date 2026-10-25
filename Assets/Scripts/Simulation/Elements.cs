// Material definitions and the lookup tables the simulation runs on.
// Pure C# (no UnityEngine) so it can be unit tested outside the editor.
using System;

namespace PowderGame.Simulation
{
    /// <summary>How a material moves.</summary>
    public enum MoveState : byte { None, Static, Powder, Liquid, Gas, Special }

    /// <summary>Material ids. Order must match <see cref="Elements.Defs"/>.</summary>
    public static class E
    {
        public const byte Empty = 0, Wall = 1, Powder = 2, Water = 3, Fire = 4, Seed = 5, Gunpowder = 6,
            Fan = 7, Ice = 8, SuperBall = 9, Clone = 10, Fireworks = 11, Oil = 12, C4 = 13, Stone = 14,
            Magma = 15, Virus = 16, Nitro = 17, Ant = 18, Torch = 19, Gas = 20, Soapy = 21, Thunder = 22,
            Metal = 23, Bomb = 24, Laser = 25, Acid = 26, Bubble = 27, Plant = 28, Steam = 29, Spark = 30,
            Rocket = 31, Bolt = 32, Sand = 33, Glass = 34, Salt = 35, SaltWater = 36, Snow = 37, Wood = 38,
            Ash = 39, Smoke = 40, Mercury = 41, Cloud = 42, Cryo = 43, Void = 44, Leaf = 45, Mud = 46;

        /// <summary>"Type" of anything outside the world.</summary>
        public const byte Out = 255;
    }

    public sealed class ElementDef
    {
        public byte Id;
        public string Key;
        /// <summary>Menu text; null for internal materials created only by reactions.</summary>
        public string Label;
        public MoveState State;
        /// <summary>0xRRGGBB.</summary>
        public uint Color;
        /// <summary>Heavier things sink through lighter movable things.</summary>
        public float Dens;
        /// <summary>Chance per frame that touching fire ignites it.</summary>
        public float Flam;
        /// <summary>How strongly wind pushes it around.</summary>
        public float Air;
        /// <summary>How far a liquid spreads sideways per frame.</summary>
        public int Disp;
        /// <summary>Blast radius when ignited (Power = blast strength).</summary>
        public int Explode;
        public float Power;
        /// <summary>Initial lifetime in frames: Life0 + random(LifeR). 0 = forever.</summary>
        public int Life0, LifeR;
        /// <summary>Stops air flow (solid walls).</summary>
        public bool Blocks;
        /// <summary>Fill rate when painting; 1 means one particle per brush call.</summary>
        public float Sparse;
    }

    public static class Elements
    {
        public static readonly ElementDef[] Defs =
        {
            D(E.Empty, "EMPTY", null, MoveState.None, 0x000000),
            D(E.Wall, "WALL", "BLOCK", MoveState.Static, 0x808080, blocks: true),
            D(E.Powder, "POWDER", "POWDER", MoveState.Powder, 0xdcb486, dens: 3, flam: 0.06f, air: 0.7f),
            D(E.Water, "WATER", "WATER", MoveState.Liquid, 0x3c50ff, dens: 2, air: 0.5f, disp: 5),
            D(E.Fire, "FIRE", "FIRE", MoveState.Gas, 0xff5a1e, dens: 0.05f, air: 1.2f, life0: 20, lifeR: 30),
            D(E.Seed, "SEED", "SEED", MoveState.Powder, 0xa0c878, dens: 3, flam: 0.1f, air: 0.6f),
            D(E.Gunpowder, "GUNPOWDER", "GUNPOWDER", MoveState.Powder, 0xc0906a, dens: 3, flam: 0.9f, air: 0.6f, explode: 4, power: 1.5f),
            D(E.Fan, "FAN", "FAN", MoveState.Static, 0x6e7cff),
            D(E.Ice, "ICE", "ICE", MoveState.Static, 0xc8f0ff, blocks: true),
            D(E.SuperBall, "SUPERBALL", "SuperBall", MoveState.Special, 0xff64ff, dens: 5, sparse: 0.03f),
            D(E.Clone, "CLONE", "CLONE", MoveState.Static, 0xc8a032, blocks: true),
            D(E.Fireworks, "FIREWORKS", "FIREWORKS", MoveState.Powder, 0xff8c50, dens: 3, flam: 0.3f, air: 0.6f),
            D(E.Oil, "OIL", "OIL", MoveState.Liquid, 0x7a3c14, dens: 1.5f, flam: 0.3f, air: 0.5f, disp: 3),
            D(E.C4, "C4", "C-4", MoveState.Static, 0xfff0c8, flam: 0.5f, explode: 16, power: 8, blocks: true),
            D(E.Stone, "STONE", "STONE", MoveState.Powder, 0x8c8c8c, dens: 5, air: 0.2f),
            D(E.Magma, "MAGMA", "MAGMA", MoveState.Liquid, 0xff6428, dens: 4, air: 0.2f, disp: 2),
            D(E.Virus, "VIRUS", "VIRUS", MoveState.Static, 0xa028c8, flam: 0.2f),
            D(E.Nitro, "NITRO", "NITRO", MoveState.Liquid, 0x40b040, dens: 2.1f, flam: 0.9f, air: 0.5f, disp: 4, explode: 10, power: 5),
            D(E.Ant, "ANT", "ANT", MoveState.Special, 0xe0b4b4, dens: 3, flam: 0.3f, sparse: 0.1f),
            D(E.Torch, "TORCH", "TORCH", MoveState.Static, 0xff8030, blocks: true),
            D(E.Gas, "GAS", "GAS", MoveState.Gas, 0xb48cb4, dens: 0.1f, flam: 0.6f, air: 1),
            D(E.Soapy, "SOAPY", "SOAPY", MoveState.Liquid, 0xdcdcff, dens: 1.8f, air: 0.5f, disp: 4),
            D(E.Thunder, "THUNDER", "THUNDER", MoveState.Special, 0xffff80, life0: 100, sparse: 1),
            D(E.Metal, "METAL", "METAL", MoveState.Static, 0x7888a8, blocks: true),
            D(E.Bomb, "BOMB", "BOMB", MoveState.Powder, 0x989848, dens: 3, air: 0.4f, explode: 12, power: 6),
            D(E.Laser, "LASER", "LASER", MoveState.Special, 0xff3c3c, life0: 400, sparse: 1),
            D(E.Acid, "ACID", "ACID", MoveState.Liquid, 0xb4ff3c, dens: 2.05f, air: 0.5f, disp: 4),
            D(E.Bubble, "BUBBLE", "BUBBLE", MoveState.Gas, 0xb4dcff, dens: 0.04f, air: 1.2f, life0: 300, lifeR: 600, sparse: 0.05f),
            D(E.Plant, "PLANT", null, MoveState.Static, 0x7a5030, flam: 0.08f, blocks: true),
            D(E.Steam, "STEAM", "STEAM", MoveState.Gas, 0xb4c3d2, dens: 0.08f, air: 1, life0: 800, lifeR: 800),
            D(E.Spark, "SPARK", null, MoveState.Special, 0xffffff, life0: 20, lifeR: 25),
            D(E.Rocket, "ROCKET", null, MoveState.Special, 0xffe0a0, life0: 20, lifeR: 25),
            D(E.Bolt, "BOLT", null, MoveState.Gas, 0xffffc0, life0: 3, lifeR: 3),
            D(E.Sand, "SAND", "SAND", MoveState.Powder, 0xf0dc82, dens: 3.2f, air: 0.5f),
            D(E.Glass, "GLASS", "GLASS", MoveState.Static, 0x9ac8c8, blocks: true),
            D(E.Salt, "SALT", "SALT", MoveState.Powder, 0xf4f4f4, dens: 3, air: 0.5f),
            D(E.SaltWater, "SALTWATER", null, MoveState.Liquid, 0x4a6ee8, dens: 2.1f, air: 0.5f, disp: 5),
            D(E.Snow, "SNOW", "SNOW", MoveState.Powder, 0xf8f8ff, dens: 1.2f, air: 1.2f),
            D(E.Wood, "WOOD", "WOOD", MoveState.Static, 0x8a5a2b, flam: 0.03f, blocks: true),
            D(E.Ash, "ASH", null, MoveState.Powder, 0x6e6e6e, dens: 3, air: 1),
            D(E.Smoke, "SMOKE", "SMOKE", MoveState.Gas, 0x5a5a5a, dens: 0.06f, air: 1, life0: 60, lifeR: 90),
            D(E.Mercury, "MERCURY", "MERCURY", MoveState.Liquid, 0xb4b4c8, dens: 6, air: 0.1f, disp: 3),
            D(E.Cloud, "CLOUD", "CLOUD", MoveState.Special, 0xd2d2e1, air: 0.8f),
            D(E.Cryo, "CRYO", "CRYO", MoveState.Liquid, 0xa0e8ff, dens: 1.6f, air: 0.5f, disp: 4, life0: 300, lifeR: 300),
            D(E.Void, "VOID", "VOID", MoveState.Static, 0x3c1450),
            D(E.Leaf, "LEAF", null, MoveState.Static, 0x50b43c, flam: 0.15f),
            D(E.Mud, "MUD", null, MoveState.Powder, 0x8c6e4b, dens: 3, air: 0.3f),
        };

        public static readonly MoveState[] State = new MoveState[256];
        public static readonly float[] Dens = new float[256];
        public static readonly float[] Flam = new float[256];
        public static readonly float[] AirF = new float[256];
        public static readonly int[] Disp = new int[256];
        public static readonly int[] Explode = new int[256];
        public static readonly float[] Power = new float[256];
        public static readonly int[] Life0 = new int[256];
        public static readonly int[] LifeR = new int[256];
        public static readonly bool[] Blocks = new bool[256];
        public static readonly bool[] Movable = new bool[256];
        public static readonly float[] Sparse = new float[256];
        public static readonly uint[] Color = new uint[256];

        static Elements()
        {
            for (int id = 0; id < Defs.Length; id++)
            {
                ElementDef d = Defs[id];
                if (d.Id != id) throw new InvalidOperationException("Element " + d.Key + " is out of order");
                State[id] = d.State;
                Dens[id] = d.Dens;
                Flam[id] = d.Flam;
                AirF[id] = d.Air;
                Disp[id] = d.Disp;
                Explode[id] = d.Explode;
                Power[id] = d.Power;
                Life0[id] = d.Life0;
                LifeR[id] = d.LifeR;
                Blocks[id] = d.Blocks;
                Movable[id] = d.State == MoveState.Powder || d.State == MoveState.Liquid || d.State == MoveState.Gas;
                Sparse[id] = d.Sparse;
                Color[id] = d.Color;
            }
        }

        /// <summary>Looks up a material by its key (e.g. "WATER"); returns -1 if unknown.</summary>
        public static int IdOf(string key)
        {
            for (int i = 0; i < Defs.Length; i++) if (Defs[i].Key == key) return i;
            return -1;
        }

        static ElementDef D(byte id, string key, string label, MoveState state, uint color,
            float dens = 0, float flam = 0, float air = 0, int disp = 0, int explode = 0, float power = 0,
            int life0 = 0, int lifeR = 0, bool blocks = false, float sparse = 0)
        {
            return new ElementDef
            {
                Id = id, Key = key, Label = label, State = state, Color = color, Dens = dens, Flam = flam,
                Air = air, Disp = disp, Explode = explode, Power = power, Life0 = life0, LifeR = lifeR,
                Blocks = blocks, Sparse = sparse,
            };
        }
    }
}
