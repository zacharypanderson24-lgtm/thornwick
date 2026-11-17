// Underground mining (Unity-only; the browser prototype still mines surface ore tiles). Design: DESIGN_TERRAIN.md section 3 and
// "Mining decisions (Zach)": ore lives in a hidden underground layer, mines dig down through depth levels (deeper = richer), cave-ins
// are a real risk unless timber supports are supplied, and crowding raises the risk. Decided 2026-11-16: miners survey Mining zones
// (no prospector building), a cave-in closes the mine and can kill a miner, and the old surface ore tiles stay as faint hints.
//
//   Deposits   every tile has iron at levels 1..MaxDepth, generated once from the seed (clustered, richer under hills and mountains,
//              deeper levels hold more). Only what has been taken (OreTaken) and what has been surveyed (Surveyed) is state; the
//              capacities are regenerated from the seed, so saves only need those two plus each mine's shaft.
//   Survey     a mine's miners walk to unsurveyed Mining-zone tiles in reach and survey them (WorkT days), revealing all levels.
//   Shaft      miners dig the shaft down one level at a time (DigWork worker-days per level), then go underground and bring up
//              loads that grow with depth (MineLoad), taking ore from surveyed Mining-zone tiles within WorkR at that level.
//   Supports   each dug level is shored with wood (SupportWood) as soon as the town has it.
//   Cave-ins   every worker-day underground rolls CaveInChance: base x level x crowding, much lower on a shored level.
//              A cave-in closes the mine for RepairDays, smashes that level's supports and can kill the miner.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int MaxDepth = 3;
        /// <summary>Ore a miner brings up per trip from level 1..3 (index 0 unused). Level 1 matches the old surface mine load.</summary>
        public static readonly int[] MineLoad = { 0, 4, 6, 8 };
        /// <summary>Days underground per trip at level 1..3; with the loads above that is about 0.5 / 0.6 / 0.67 ore per worker-day.</summary>
        public static readonly int[] MineTripDays = { 0, 8, 10, 12 };
        /// <summary>Worker-days to dig the shaft down to level 1..3.</summary>
        public static readonly int[] DigWork = { 0, 12, 24, 40 };
        /// <summary>Wood to shore up level 1..3.</summary>
        public static readonly int[] SupportWood = { 0, 6, 10, 16 };
        /// <summary>Most ore a tile can hold at level 1..3.</summary>
        public static readonly int[] LevelCapMax = { 0, 30, 50, 80 };
        public const int DigStint = 4, RepairDays = 20;
        public const double CaveBase = 0.0008, CrowdStep = 0.5, ShoredFactor = 0.15, KillChanceShored = 0.25, KillChanceUnshored = 0.6;
        /// <summary>Multiplies every cave-in chance (1 in play; tests raise it to force a cave-in).</summary>
        public double CaveInRiskScale = 1;

        // capacities per level (index 1..MaxDepth), generated at world creation; taken amounts and survey marks are the state
        readonly short[][] _oreCap = new short[MaxDepth + 1][];
        public readonly Dictionary<int, double> OreTaken = new Dictionary<int, double>();   // key: level * N * N + y * N + x
        public readonly bool[] Surveyed = new bool[N * N];
        public int MinerDeaths, CaveIns;
        /// <summary>Messages for the player (cave-ins, new levels, empty surveys). The game shows and clears them.</summary>
        public readonly List<string> Events = new List<string>();

        void GenDeposits()
        {
            for (int l = 1; l <= MaxDepth; l++) _oreCap[l] = new short[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k = Tk(x, y);
                    // the same field that places the surface ore hints (World.GenTile), so hints sit over the shallow cores
                    double o = Noise.VNoise(x * 0.3 + 90, y * 0.3 + 90, Seed + 13);
                    double high = Math.Max(0, Math.Min(1, TileLevel(x, y) / MountLevel));   // hills and mountains sit over more ore
                    double f1 = o - 0.58;
                    double f2 = Noise.VNoise(x * 0.22 + 40, y * 0.22 + 40, Seed + 71) * 0.7 + o * 0.3 + high * 0.15 - 0.6;
                    double f3 = Noise.VNoise(x * 0.15 + 7, y * 0.15 + 7, Seed + 97) * 0.6 + o * 0.2 + high * 0.3 - 0.52;
                    _oreCap[1][k] = Cap(f1, 0.42, 1); _oreCap[2][k] = Cap(f2, 0.35, 2); _oreCap[3][k] = Cap(f3, 0.4, 3);
                    if (DecoMap[y, x] == Deco.Ore && _oreCap[1][k] < 8) _oreCap[1][k] = 8;   // a surface showing always has something under it
                }
        }
        static short Cap(double f, double span, int level) => f <= 0 ? (short)0 : (short)Math.Max(4, Math.Min(LevelCapMax[level], Math.Round(f / span * LevelCapMax[level])));

        static int OreKey(int level, int k) => level * N * N + k;
        /// <summary>Ore still in the ground at a tile and level (whether or not anyone knows it).</summary>
        public double OreAt(int x, int y, int level)
        {
            int k = Tk(x, y), cap = _oreCap[level][k];
            return cap <= 0 ? 0 : cap - (OreTaken.TryGetValue(OreKey(level, k), out var t) ? t : 0);
        }
        public bool IsSurveyed(int x, int y) => Surveyed[Tk(x, y)];
        /// <summary>Ore the player knows about at a tile and level: 0 until the tile is surveyed.</summary>
        public double KnownOre(int x, int y, int level) => Surveyed[Tk(x, y)] ? OreAt(x, y, level) : 0;
        public bool HasKnownOre(int x, int y) { for (int l = 1; l <= MaxDepth; l++) if (KnownOre(x, y, l) > 1e-9) return true; return false; }

        /// <summary>Chance that one worker-day at this level of this mine ends in a cave-in.</summary>
        public double CaveInChance(Building b, int level, int underground)
        {
            double crowd = 1 + CrowdStep * Math.Max(0, underground - 1);
            return CaveBase * level * crowd * (level <= b.Shored ? ShoredFactor : 1) * CaveInRiskScale;
        }

        // ---------- Where the mine's ore is ----------
        // The tiles a mine works underground: Mining-zone tiles within WorkR (Chebyshev) of the shaft.
        bool InMineReach(Building b, int x, int y) => Inb(x, y) && ZoneMap[y, x] == Zone.Mine && Claimed(x, y);

        /// <summary>The deepest dug level (<= shaft depth) that still has surveyed ore in reach, or 0.</summary>
        int WorkableLevel(Building b)
        {
            for (int l = b.Depth; l >= 1; l--)
                for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                    for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                        if (InMineReach(b, i, j) && KnownOre(i, j, l) > 1e-9) return l;
            return 0;
        }
        bool DeeperOreKnown(Building b)
        {
            if (b.Depth >= MaxDepth) return false;
            for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                    if (InMineReach(b, i, j)) for (int l = b.Depth + 1; l <= MaxDepth; l++) if (KnownOre(i, j, l) > 1e-9) return true;
            return false;
        }
        int UnsurveyedInReach(Building b)
        {
            int n = 0;
            for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                    if (InMineReach(b, i, j) && !Surveyed[Tk(i, j)]) n++;
            return n;
        }

        /// <summary>Take up to `amount` ore at one level from the richest surveyed tiles in the mine's reach (like mineRock, up to 4 tiles).</summary>
        double TakeOre(Building b, int level, double amount)
        {
            double got = 0;
            for (int n = 0; n < 4 && amount > 1e-9; n++)
            {
                int bi = -1, bj = -1; double best = 1e-9;
                for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                    for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                        if (InMineReach(b, i, j)) { double o = KnownOre(i, j, level); if (o > best) { best = o; bi = i; bj = j; } }
                if (bi < 0) break;
                double e = Math.Min(amount, best); int key = OreKey(level, Tk(bi, bj));
                OreTaken[key] = (OreTaken.TryGetValue(key, out var t) ? t : 0) + e; got += e; amount -= e;
                Touch();
            }
            return got;
        }

        // ---------- The mine's crew ----------
        void StepMine(Building b, List<Worker> crew, Dictionary<Res, double> got)
        {
            if (b.Closed > 0) b.Closed--;
            // shore up dug levels as soon as there is wood for it
            if (b.Served && b.Shored < b.Depth && ResAmt[Res.Wood] >= SupportWood[b.Shored + 1]) { ResAmt[Res.Wood] -= SupportWood[b.Shored + 1]; b.Shored++; }
            int down = 0; foreach (var o in crew) if (o.St == CrewState.Down) down++;   // crowding counts who started the day underground

            for (int ai = 0; ai < crew.Count; ai++)
            {
                var a = crew[ai];
                a.Px = a.X; a.Py = a.Y;
                switch (a.St)
                {
                    case CrewState.Home:
                    {
                        if (!b.Served || b.Closed > 0) break;
                        // 1. survey the zone first, so the crew knows what lies below
                        _used.Clear();
                        foreach (var o in crew) if (o != a && o.St != CrewState.Home && o.Stand >= 0) _used.Add(o.Stand);
                        if (CrewTarget(b, Deco.None, ai * 7 + Day, _used, a.Path, out a.Tx, out a.Ty, survey: true))
                        {
                            a.Survey = true; a.Stand = a.Path[a.Path.Count - 1]; a.I = 0; a.Carry = 0;
                            if (a.Path.Count > 1) { a.I = 1; SetPos(a, a.Path[1]); }
                            a.St = a.I == a.Path.Count - 1 ? CrewState.Work : CrewState.Out; if (a.St == CrewState.Work) a.T = WorkT;
                            break;
                        }
                        if (ResAmt[Res.Ore] + got[Res.Ore] >= Storage) break;   // full stockpile: the ore stays in the ground
                        // 2. bring up ore from the deepest dug level that still has some
                        int lvl = WorkableLevel(b);
                        if (lvl > 0) { a.St = CrewState.Down; a.Dig = false; a.Level = lvl; a.T = MineTripDays[lvl]; break; }
                        // 3. otherwise dig the shaft deeper towards known ore
                        if (DeeperOreKnown(b)) { a.St = CrewState.Down; a.Dig = true; a.Level = b.Depth + 1; a.T = DigStint; }
                        break;
                    }
                    case CrewState.Down:
                    {
                        if (Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4001) < CaveInChance(b, a.Level, down))
                        {
                            if (CaveIn(b, crew, ai)) ai--;   // the miner died and left the crew
                            break;
                        }
                        if (a.Dig && b.Depth < a.Level && ++b.DigProg >= DigWork[a.Level])
                        {
                            b.Depth = a.Level; b.DigProg = 0; Touch();
                            Events.Add($"The {b.Name} at {b.X},{b.Y} has dug down to level {b.Depth}.");
                        }
                        if (--a.T > 0) break;
                        if (!a.Dig) got[Res.Ore] += TakeOre(b, a.Level, MineLoad[a.Level]);
                        a.St = CrewState.Home;
                        break;
                    }
                    case CrewState.Out:
                        a.I++; SetPos(a, a.Path[a.I]);
                        if (a.I == a.Path.Count - 1) { a.St = CrewState.Work; a.T = WorkT; }
                        break;
                    case CrewState.Work:
                        if (--a.T <= 0)
                        {
                            int k = Tk(a.Tx, a.Ty);
                            if (!Surveyed[k])
                            {
                                Surveyed[k] = true; Touch();
                                if (UnsurveyedInReach(b) == 0 && !HasOreInReach(b))
                                    Events.Add($"The {b.Name} at {b.X},{b.Y} has surveyed its whole zone and found no ore.");
                            }
                            a.Path.Reverse(); a.I = 0; a.St = CrewState.Back;
                        }
                        break;
                    default:   // back from a survey
                        if (a.I < a.Path.Count - 1) { a.I++; SetPos(a, a.Path[a.I]); }
                        break;
                }
                if (a.St == CrewState.Back && a.I >= a.Path.Count - 1)
                {
                    a.St = CrewState.Home; a.Survey = false; a.Path.Clear(); a.Stand = -1; a.X = b.X; a.Y = b.Y;
                }
            }
        }

        bool TookOreInReach(Building b)
        {
            for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                    if (InMineReach(b, i, j)) for (int l = 1; l <= MaxDepth; l++) if (OreTaken.ContainsKey(OreKey(l, Tk(i, j)))) return true;
            return false;
        }

        bool HasOreInReach(Building b)
        {
            for (int j = b.Y - WorkR; j <= b.Y + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + WorkR; i++)
                    if (InMineReach(b, i, j) && HasKnownOre(i, j)) return true;
            return false;
        }

        /// <summary>A cave-in on worker ai's level: the mine closes, everyone underground comes up, the level's supports are smashed,
        /// and the miner may die. Returns true when the worker died (and was removed from the crew).</summary>
        bool CaveIn(Building b, List<Worker> crew, int ai)
        {
            var a = crew[ai]; int level = a.Level; bool shored = level <= b.Shored;
            CaveIns++;
            b.Closed = RepairDays; b.CaveIns++;
            if (shored) b.Shored = level - 1;
            foreach (var o in crew) if (o.St == CrewState.Down) { o.St = CrewState.Home; o.X = b.X; o.Y = b.Y; }
            bool dead = Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4002) < (shored ? KillChanceShored : KillChanceUnshored);
            if (dead)
            {
                crew.RemoveAt(ai);
                if (Peasants > 0) Peasants--;
                MinerDeaths++;
            }
            Events.Add($"Cave-in at the {b.Name} ({b.X},{b.Y}), level {level}{(shored ? "" : ", unshored")}! " +
                       (dead ? "A miner was killed. " : "The miners escaped. ") + $"The mine is closed for {RepairDays} days of repairs.");
            Touch();
            return dead;
        }

        /// <summary>What a mine is doing, for the info panel.</summary>
        public string MineStatus(Building b)
        {
            string s = $"Shaft: level {b.Depth} of {MaxDepth}" + (b.Depth > 0 ? $", {b.Shored} shored with timber" : "");
            if (b.Depth < MaxDepth && b.DigProg > 0) s += $". Digging to level {b.Depth + 1}: {100 * b.DigProg / DigWork[b.Depth + 1]}%";
            if (b.Closed > 0) s += $"\nClosed for repairs after a cave-in: {b.Closed} days left";
            else if (b.Shored < b.Depth) s += $"\nUnshored level: needs {SupportWood[b.Shored + 1]} wood for supports (cave-ins are far more likely)";
            int unsurveyed = UnsurveyedInReach(b);
            if (unsurveyed > 0) s += $"\n{unsurveyed} zone tiles still to survey";
            else if (!HasOreInReach(b))
                s += TookOreInReach(b) ? "\nWorked out: no ore left under this mine's zone. Zone fresh ground nearby or remove the mine"
                                       : "\nNo ore found under this mine's zone. Zone other ground or remove the mine";
            if (b.CaveIns > 0) s += $"\nCave-ins so far: {b.CaveIns}";
            return s;
        }
    }
}
