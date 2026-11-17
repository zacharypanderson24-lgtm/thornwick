// Game constants, ported from the top of src/sim.js. Keep names and values in step with the browser prototype.
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>What sits on a tile besides buildings (JS: S.deco[y][x]).</summary>
    public enum Deco : byte { None, Tree, Rock, Ore, Stump, Sapling }

    /// <summary>Painted zones (JS: S.zone[y][x]).</summary>
    public enum Zone : byte { None, Res, Farm, Comm, Wood, Mine }

    /// <summary>Resource keys (JS: S.res).</summary>
    public enum Res { Food, Wood, Logs, Stone, Ore, Gold }

    public static class Defs
    {
        public const int N = 128;                 // a city map is N x N tiles
        public const int Start = N >> 1;          // the Town Hall is centred here

        // Terrain heights
        public const int HMin = -8, HMax = 16, MountLevel = 4, LvlPx = 7;
        /// <summary>The browser's height ceiling: TerrainStyle.Classic clamps to it so it stays identical to the prototype.</summary>
        public const int ClassicHMax = 8;
        /// <summary>Mountain tiles at or above this level are drawn as snow.</summary>
        public const double SnowLevel = 12;

        // Economy
        public const int HallCap = 6, Bunks = 6, WellR = 4, WorkR = 3, MarketR = 5, Storage = 400;
        public const double SawRate = 4, HallSaw = 1, HallYield = 0.5;
        public const int StumpDays = 8;
        public const int PlantEvery = 4, SaplingDays = 3 * 365, ForesterR = 4;

        // Stone and ore are finite
        public const int DigU = 10, MountCap = 120, OreCap = 30;

        // Work crews (World.Crews.cs): 4 chops x 6 logs = 24 logs per tree
        public const int ChopsPerTree = 4;
        public static readonly Dictionary<string, int> Load = new Dictionary<string, int> { { "wood", 6 }, { "quarry", 5 }, { "mine", 4 } };

        public static readonly int[] HouseCap = { 0, 4, 6 };
        public static readonly int[] HouseCap2 = { 0, 14, 20 };

        public static readonly Dictionary<string, Dictionary<Res, int>> Cost = new Dictionary<string, Dictionary<Res, int>>
        {
            { "road", new Dictionary<Res, int> { { Res.Wood, 1 } } },
            { "house", new Dictionary<Res, int> { { Res.Wood, 10 } } },
            { "farm", new Dictionary<Res, int> { { Res.Wood, 8 } } },
            { "wood", new Dictionary<Res, int> { { Res.Wood, 6 } } },
            { "quarry", new Dictionary<Res, int> { { Res.Wood, 10 } } },
            { "mine", new Dictionary<Res, int> { { Res.Wood, 12 } } },
            { "market", new Dictionary<Res, int> { { Res.Wood, 12 } } },
            { "sawmill", new Dictionary<Res, int> { { Res.Wood, 15 } } },
            { "forester", new Dictionary<Res, int> { { Res.Wood, 8 } } },
            { "well", new Dictionary<Res, int> { { Res.Wood, 5 }, { Res.Stone, 5 } } },
            { "barracks", new Dictionary<Res, int> { { Res.Wood, 20 }, { Res.Stone, 15 }, { Res.Gold, 20 } } },
        };
        public static readonly Dictionary<string, Dictionary<Res, int>> Cost2 = new Dictionary<string, Dictionary<Res, int>>
        {
            { "house", new Dictionary<Res, int> { { Res.Wood, 30 } } },
            { "farm", new Dictionary<Res, int> { { Res.Wood, 24 } } },
            { "market", new Dictionary<Res, int> { { Res.Wood, 32 } } },
        };
        static readonly Dictionary<Res, int> NoCost = new Dictionary<Res, int>();
        public static Dictionary<Res, int> CostOf(string type, int sz)
        {
            if (sz == 2 && Cost2.TryGetValue(type, out var c2)) return c2;
            return Cost.TryGetValue(type, out var c) ? c : NoCost;
        }

        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "road", "Dirt Road" }, { "house", "Timber House" }, { "farm", "Farm" }, { "wood", "Woodcutter Lodge" }, { "quarry", "Quarry" },
            { "mine", "Iron Mine" }, { "market", "Market" }, { "sawmill", "Sawmill" }, { "forester", "Forester" }, { "well", "Well" },
            { "barracks", "Barracks" }, { "hall", "Town Hall" },
        };

        public static readonly Dictionary<string, int> Jobs = new Dictionary<string, int>
        {
            { "farm", 4 }, { "wood", 3 }, { "quarry", 3 }, { "mine", 3 }, { "market", 2 }, { "sawmill", 3 }, { "forester", 2 },
        };

        public static readonly Dictionary<string, string> TradeOf = new Dictionary<string, string>
        {
            { "farm", "farming" }, { "wood", "timber" }, { "forester", "timber" }, { "sawmill", "woodwork" }, { "quarry", "quarrying" }, { "mine", "mining" }, { "market", "trade" },
        };
        public static readonly string[] Trades = { "farming", "timber", "woodwork", "quarrying", "mining", "trade" };
        public static readonly Dictionary<string, string> TradeNames = new Dictionary<string, string>
        {
            { "farming", "Farming" }, { "timber", "Timber" }, { "woodwork", "Woodworking" }, { "quarrying", "Quarrying" }, { "mining", "Mining" }, { "trade", "Trade" },
        };

        /// <summary>Tree species: which tree_N variants each species uses, and its share of forests. tree_1 is retired (6 old oak takes its share).</summary>
        public static readonly int[][] TreeSpeciesVariants = { new[] { 0, 6, 6 }, new[] { 2, 3, 7 }, new[] { 4, 9 }, new[] { 5 }, new[] { 8 } };
        public static readonly double[] TreeSpeciesWeights = { 0.30, 0.30, 0.18, 0.08, 0.14 };
        /// <summary>Grove art (tree_&lt;kind&gt;_N) for each single-tree variant.</summary>
        public static readonly Dictionary<int, string> TreeGroveArt = new Dictionary<int, string>
        {
            { 0, "oak" }, { 2, "pine" }, { 3, "spruce" }, { 4, "birch" }, { 5, "willow" }, { 6, "old_oak" }, { 7, "fir" }, { 8, "maple" }, { 9, "aspen" },
        };

        public const int HutVariants = 8;
        public const string TreeMsg = "Trees are in the way. Paint a Lumber zone over them and let woodcutters clear the land first";

        /// <summary>Door directions: 0 = +y, 1 = -x, 2 = -y, 3 = +x.</summary>
        public static readonly int[,] FaceDirs = { { 0, 1 }, { -1, 0 }, { 0, -1 }, { 1, 0 } };
        public static readonly int[,] Nb = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        public static int DefaultSize(string type) => type == "hall" ? 3 : type == "barracks" ? 2 : 1;
    }
}
