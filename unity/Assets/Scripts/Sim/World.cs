// The world state and terrain generation, ported from src/sim.js (createWorld, genHeights, genTile, mountains, trees).
// Pure C#: no UnityEngine, so it runs in EditMode tests and can later move to a headless server.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public readonly int Seed;
        public readonly Building[,] Grid = new Building[N, N];     // [y, x]
        public readonly Deco[,] DecoMap = new Deco[N, N];          // [y, x]
        public readonly Zone[,] ZoneMap = new Zone[N, N];          // [y, x]
        public readonly bool[,] Fert = new bool[N, N];             // [y, x]
        public readonly int[] Hv = new int[(N + 1) * (N + 1)];     // vertex heights, (N+1)^2
        public readonly List<Building> Blds = new List<Building>();
        public Building Hall;

        // Sparse per-tile data, keyed by y * N + x (JS objects of the same names)
        public readonly Dictionary<int, int> RockAmtMap = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Elev = new Dictionary<int, int>();
        public readonly Dictionary<int, int> StumpT = new Dictionary<int, int>();
        public readonly Dictionary<int, int> SapT = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Chop = new Dictionary<int, int>();
        public readonly Dictionary<int, double> Dig = new Dictionary<int, double>();
        public readonly Dictionary<int, double> OreLeftMap = new Dictionary<int, double>();

        public readonly Dictionary<Res, double> ResAmt = new Dictionary<Res, double>
        {
            { Res.Food, 60 }, { Res.Wood, 120 }, { Res.Logs, 20 }, { Res.Stone, 30 }, { Res.Ore, 10 }, { Res.Gold, 80 }, { Res.Flax, 0 }, { Res.Metal, 0 },
            { Res.Charcoal, 0 }, { Res.SmokedMeat, 0 }, { Res.Wool, 0 }, { Res.Eggs, 0 },
        };
        public readonly Dictionary<Res, double> Rate = new Dictionary<Res, double>
        {
            { Res.Food, 0 }, { Res.Wood, 0 }, { Res.Logs, 0 }, { Res.Stone, 0 }, { Res.Ore, 0 }, { Res.Gold, 0 }, { Res.Flax, 0 }, { Res.Metal, 0 },
            { Res.Charcoal, 0 }, { Res.SmokedMeat, 0 }, { Res.Wool, 0 }, { Res.Eggs, 0 },
        };
        public int Peasants = 6, Soldiers, Trained, Day = 1;
        public double PopAcc;
        public bool Drafting = true, Starving;
        public string[] Prio = (string[])Trades.Clone();

        /// <summary>Bumped on every change to tiles or buildings, so views know when to redraw.</summary>
        public int Version { get; private set; }
        public void Touch() => Version++;

        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
        public static int Tk(int x, int y) => y * N + x;

        // ---------- Terrain heights ----------
        public int Vh(int a, int b)
        {
            a = a < 0 ? 0 : a > N ? N : a; b = b < 0 ? 0 : b > N ? N : b;
            return Hv[b * (N + 1) + a];
        }
        public double TileLevel(int x, int y) => (Vh(x, y) + Vh(x + 1, y) + Vh(x, y + 1) + Vh(x + 1, y + 1)) / 4.0;
        public int TileSpread(int x, int y)
        {
            int a = Vh(x, y), b = Vh(x + 1, y), c = Vh(x, y + 1), d = Vh(x + 1, y + 1);
            return Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d));
        }
        public bool TileFlat(int x, int y) => TileSpread(x, y) == 0;

        // ---------- Creation ----------
        public readonly TerrainStyle Style;
        public readonly Relief Relief;

        /// <param name="style">Ranges (the game) or Classic (the browser prototype's terrain, used by the parity tests).</param>
        /// <param name="relief">How mountainous the region is (Ranges only).</param>
        public World(int seed = 7, TerrainStyle style = TerrainStyle.Ranges, Relief relief = Relief.Upland) : this(seed, true, style, relief) { }

        /// <summary>createSettlement false: an empty world for World.LoadJson to fill in (terrain comes from the save).</summary>
        World(int seed, bool createSettlement, TerrainStyle style = TerrainStyle.Ranges, Relief relief = Relief.Upland)
        {
            Seed = seed == 0 ? 7 : seed;
            Style = style; Relief = relief;
            if (!createSettlement) return;
            ClaimStartDistrict();   // a 64 x 64 owned district round the Town Hall (World.Claims.cs); a save brings its own
            GenTerrain();
            GenAquifers();   // groundwater, World.Water.cs
            int c = Start;
            AddBld("hall", c - 1, c - 1, 3);
            for (int y = c + 2; y <= c + 6; y++) AddBld("road", c, y);
            AddBld("house", c - 1, c + 3).Zoned = true;
            AddBld("house", c + 1, c + 3).Zoned = true;
            // the starting farm: a 2x2 farmhouse (its plots come from AddStarterFarmFields); the browser-compatible Classic world
            // keeps the browser's 1x1 farm so the parity tests start from the same settlement
            if (Style == TerrainStyle.Classic) { AddBld("farm", c - 1, c + 5, 1).Zoned = true; ZoneMap[c + 5, c - 1] = Zone.Farm; }
            else { AddBld("farm", c - 2, c + 5, 2).Zoned = true; for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) ZoneMap[c + 5 + j, c - 2 + i] = Zone.Farm; }
            ZoneMap[c + 3, c - 1] = Zone.Res; ZoneMap[c + 3, c + 1] = Zone.Res;
            if (SeasonalFarming) ResAmt[Res.Food] = SeasonalTownFood;
            AddStarterFarmFields();
            Recalc();
            ComputeDemand();
        }

        void GenTerrain((int min, int max)? avoid = null)
        {
            // Classic (the browser's) can keep a loaded smaller map's square clear of new mountains; Ranges always keep the town valley open
            var hv = Style == TerrainStyle.Classic ? GenHeights(Seed, avoid) : GenRangeHeights(Seed, Relief);
            Array.Copy(hv, Hv, hv.Length);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) GenTile(x, y);
            GenDeposits();   // underground ore, World.Mining.cs
        }

        // ---------- Mountains ----------
        class Range { public double[][] Pts; public double Ph; }
        class Lone { public double Cx, Cy, Hp; public int Side; }

        static (List<Range>, List<Lone>) MountainLayout(int seed, (int min, int max)? avoid)
        {
            double A = (N / 64.0) * (N / 64.0);
            int n = 0;
            double Rnd() => Noise.Hash2(n++, 23, seed + 900);
            var ranges = new List<Range>(); var lone = new List<Lone>();
            bool Far(double x, double y, double r) => Math.Max(Math.Abs(x - Start), Math.Abs(y - Start)) > r;
            bool Clear(double x, double y, int margin) => !avoid.HasValue ||
                x < avoid.Value.min - margin || y < avoid.Value.min - margin ||
                x > avoid.Value.max + margin || y > avoid.Value.max + margin;
            int want = Noise.JsRound(A * (Rnd() < 0.3 ? 1.5 : 1));
            for (int r = 0; r < want; r++)
                for (int t = 0; t < 400; t++)
                {
                    double len = 14 + Rnd() * 9, ang = Rnd() * Math.PI * 2, sx = 6 + Rnd() * (N - 12), sy = 6 + Rnd() * (N - 12);
                    double bend = (Rnd() - 0.5) * 1.1;
                    var pts = new double[5][];
                    for (int i = 0; i <= 4; i++)
                    {
                        double a = ang + bend * (i / 4.0 - 0.5) * 2, f = (i / 4.0 - 0.5) * len;
                        pts[i] = new[] { sx + Math.Cos(a + bend * i * 0.25) * f, sy + Math.Sin(a + bend * i * 0.25) * f };
                    }
                    bool ok = true;
                    foreach (var p in pts) if (!(p[0] > 4 && p[1] > 4 && p[0] < N - 4 && p[1] < N - 4 && Far(p[0], p[1], 15) && Clear(p[0], p[1], 8))) { ok = false; break; }
                    if (!ok) continue;
                    bool near = false;
                    foreach (var o in ranges) foreach (var q in o.Pts) foreach (var p in pts) if (Noise.Hypot(p[0] - q[0], p[1] - q[1]) < 14) near = true;
                    if (near) continue;
                    ranges.Add(new Range { Pts = pts, Ph = Rnd() * 90 });
                    break;
                }
            int wantLone = Rnd() < 0.7 ? Noise.JsRound(A * 0.6) : 0;
            for (int t = 0; t < 400 && lone.Count < wantLone; t++)
            {
                double cx = 6 + Rnd() * (N - 12), cy = 6 + Rnd() * (N - 12);
                int side = 2 + (int)Math.Floor(Rnd() * 3);
                double hp = 5.2 + side * 0.5 + Rnd() * 1.2;
                if (!Far(cx, cy, 15) || !Clear(cx, cy, 10)) continue;
                bool near = false;
                foreach (var o in ranges) foreach (var q in o.Pts) if (Noise.Hypot(q[0] - cx, q[1] - cy) < 14) near = true;
                if (near) continue;
                lone.Add(new Lone { Cx = cx, Cy = cy, Side = side, Hp = hp });
            }
            return (ranges, lone);
        }

        static double MountainField((List<Range> ranges, List<Lone> lone) L, int seed, int a, int b)
        {
            double v = 0;
            foreach (var r in L.ranges)
            {
                double best = 1e9, bs = 0, acc = 0, total = 0;
                for (int i = 0; i + 1 < r.Pts.Length; i++) total += Noise.Hypot(r.Pts[i + 1][0] - r.Pts[i][0], r.Pts[i + 1][1] - r.Pts[i][1]);
                for (int i = 0; i + 1 < r.Pts.Length; i++)
                {
                    double x0 = r.Pts[i][0], y0 = r.Pts[i][1], x1 = r.Pts[i + 1][0], y1 = r.Pts[i + 1][1], dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
                    double t = Math.Max(0, Math.Min(1, ((a - x0) * dx + (b - y0) * dy) / l2)), d = Noise.Hypot(a - (x0 + dx * t), b - (y0 + dy * t));
                    if (d < best) { best = d; bs = acc + Math.Sqrt(l2) * t; }
                    acc += Math.Sqrt(l2);
                }
                double along = bs, endFade = Math.Min(1, 0.25 + Math.Min(along, total - along) / 4);
                double crest = 6.2 + 3.0 * Noise.VNoise(along * 0.42 + r.Ph, 3.3, seed + 31) - 0.4;
                double dd = best + (Noise.VNoise(a * 0.45 + r.Ph, b * 0.45, seed + 32) - 0.5) * 1.6;
                v = Math.Max(v, (crest * endFade) - Math.Max(0, dd - 0.4) * 1.9);
            }
            foreach (var m in L.lone)
            {
                double d = Noise.Hypot(a - m.Cx, b - m.Cy) + (Noise.VNoise(a * 0.5, b * 0.5, seed + 33) - 0.5) * 1.2;
                v = Math.Max(v, m.Hp - Math.Max(0, d - m.Side * 0.2) * 1.5);
            }
            return v;
        }

        public static int[] GenHeights(int seed) => GenHeights(seed, null);

        static int[] GenHeights(int seed, (int min, int max)? avoid)
        {
            int M = N + 1; var hv = new int[M * M]; var L = MountainLayout(seed, avoid);
            for (int b = 0; b < M; b++)
                for (int a = 0; a < M; a++)
                {
                    double f = Noise.VNoise(a * 0.05 + 30, b * 0.05 + 30, seed + 400) * 0.7 + Noise.VNoise(a * 0.12, b * 0.12, seed + 401) * 0.3;
                    double v = Math.Max(0, Math.Min(3.45, (f - 0.575) * 15));
                    double g = Noise.VNoise(a * 0.045 + 77, b * 0.045 + 77, seed + 500);
                    if (v < 0.5 && g < 0.27) v = -Math.Min(2.4, (0.27 - g) * 14);
                    v = Math.Max(v, MountainField(L, seed, a, b));
                    int dist = Math.Max(Math.Abs(a - Start), Math.Abs(b - Start));
                    v *= Math.Min(1, Math.Max(0, (dist - 7) / 7.0));
                    hv[b * M + a] = Math.Max(HMin, Math.Min(ClassicHMax, Noise.JsRound(v)));
                }
            RelaxHeights(hv, null, null, null);
            return hv;
        }

        bool VnMountainTouch(int a, int b)
        {
            foreach (var (i, j) in new[] { (a - 1, b - 1), (a, b - 1), (a - 1, b), (a, b) })
                if (i >= 0 && j >= 0 && i < N && j < N && DecoMap[j, i] == Deco.Rock && RockAmt(i, j) >= 3) return true;
            return false;
        }

        /// <summary>Keep neighbouring vertices within 1 level (2 on high ground). JS: relaxHeights. world may be null during generation.</summary>
        static void RelaxHeights(int[] hv, World world, int[] box, HashSet<int> pinned)
        {
            int M = N + 1, a0 = box?[0] ?? 0, b0 = box?[1] ?? 0, a1 = box?[2] ?? N, b1 = box?[3] ?? N;
            bool Movable(int a, int b) => !(pinned != null && pinned.Contains(b * M + a)) && !(world != null && world.VnMountainTouch(a, b));
            for (int pass = 0; pass < 24; pass++)
            {
                bool ch = false;
                for (int b = b0; b <= b1; b++)
                    for (int a = a0; a <= a1; a++)
                    {
                        int h = hv[b * M + a];
                        for (int k = 0; k < 4; k++)
                        {
                            int da = k == 0 ? 1 : k == 2 ? -1 : 0, db = k == 1 ? 1 : k == 3 ? -1 : 0;
                            int na = a + da, nb = b + db;
                            if (na < 0 || nb < 0 || na > N || nb > N) continue;
                            int o = hv[nb * M + na], lim = Math.Max(h, o) <= 3 ? 1 : 2;
                            if (h - o > lim)
                            {
                                if (Movable(na, nb)) { hv[nb * M + na] = h - lim; ch = true; }
                                else if (Movable(a, b)) { hv[b * M + a] = o + lim; ch = true; }
                            }
                        }
                    }
                if (!ch) break;
            }
        }

        /// <summary>Build up the hill: raise a footprint to its highest corner and ease the land around it. JS: levelFootprint.</summary>
        void LevelFootprint(Building bl)
        {
            int M = N + 1, top = HMin, a0 = N, b0 = N, a1 = 0, b1 = 0;
            var set = new HashSet<int>();
            foreach (var (x, y) in bl.Tiles())
                foreach (var (da, db) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                {
                    int k = (y + db) * M + x + da;
                    if (!set.Add(k)) continue;
                    top = Math.Max(top, Hv[k]);
                    a0 = Math.Min(a0, x + da); b0 = Math.Min(b0, y + db); a1 = Math.Max(a1, x + da); b1 = Math.Max(b1, y + db);
                }
            bool moved = false;
            foreach (int k in set) if (Hv[k] != top) { Hv[k] = top; moved = true; }
            if (!moved) return;
            int[] box = { Math.Max(0, a0 - 5), Math.Max(0, b0 - 5), Math.Min(N, a1 + 5), Math.Min(N, b1 + 5) };
            var pinned = new HashSet<int>(set);
            foreach (var o in Blds) if (o.T != "road") foreach (var (i, j) in o.Tiles()) foreach (var (da, db) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) pinned.Add((j + db) * M + i + da);
            RelaxHeights(Hv, this, box, pinned);
        }

        // ---------- Tiles: fertility, mountains, outcrops, ore, forest ----------
        void GenTile(int x, int y)
        {
            int dist = Math.Max(Math.Abs(x - Start), Math.Abs(y - Start));
            Fert[y, x] = Math.Max(Math.Abs(x - Start), Math.Abs(y - (Start + 3))) <= 7 || Noise.VNoise(x * 0.11 + 10, y * 0.11 + 10, Seed + 20) > 0.55;
            if (dist < 6) return;
            double f = Noise.VNoise(x * 0.16, y * 0.16, Seed) * 0.65 + Noise.VNoise(x * 0.4, y * 0.4, Seed + 3) * 0.35;
            double o = Noise.VNoise(x * 0.3 + 90, y * 0.3 + 90, Seed + 13);
            double lvl = TileLevel(x, y); int k = Tk(x, y);
            if (lvl >= MountLevel) { DecoMap[y, x] = Deco.Rock; RockAmtMap[k] = 3; Elev[k] = Noise.JsRound(lvl * 8); return; }
            bool flat = TileFlat(x, y) && lvl >= 0;
            double q = Noise.VNoise(x * 0.33 + 210, y * 0.33 + 210, Seed + 61);
            if (flat && q > 0.8 && Noise.Hash2(x, y, Seed + 62) > 0.25 && dist > 7) { DecoMap[y, x] = Deco.Rock; RockAmtMap[k] = q > 0.88 ? 2 : 1; return; }
            if (o > 0.72 && Noise.Hash2(x, y, Seed + 7) > 0.35) DecoMap[y, x] = Deco.Ore;
            else if (f > 0.56 && Noise.Hash2(x, y, Seed + 5) > 0.22) DecoMap[y, x] = Deco.Tree;
        }

        // ---------- Trees ----------
        public int TreeSpecies(int x, int y)
        {
            double r = Noise.Hash2(x, y, Seed + 810) < 0.12 ? Noise.Hash2(x, y, Seed + 811) : Noise.VNoise(x * 0.16 + 5, y * 0.16 + 5, Seed + 812) * 1.25 - 0.125;
            double a = 0, t = Math.Max(0, Math.Min(0.9999, r));
            for (int i = 0; i < TreeSpeciesWeights.Length; i++) { a += TreeSpeciesWeights[i]; if (t < a) return i; }
            return TreeSpeciesWeights.Length - 1;
        }
        public int TreeVariant(int x, int y)
        {
            var sp = TreeSpeciesVariants[TreeSpecies(x, y)];
            return sp[(int)Math.Floor(Noise.Hash2(x, y, Seed + 813) * sp.Length) % sp.Length];
        }
        /// <summary>How many trees this forest tile draws (1..4) and which grove art. JS: treeGrove.</summary>
        public (int n, string kind) TreeGrove(int x, int y)
        {
            if (!TreeGroveArt.TryGetValue(TreeVariant(x, y), out var kind)) return (1, null);
            int d = 0;
            for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) if ((i != 0 || j != 0) && Inb(x + i, y + j) && DecoMap[y + j, x + i] == Deco.Tree) d++;
            double r = Noise.Hash2(x, y, Seed + 820), r2 = Noise.Hash2(x, y, Seed + 821);
            int n = 1;
            if (d >= 6) { if (r2 < 0.92) n = 2 + (int)Math.Floor(r * 3); }
            else if (d >= 4) { if (r2 < 0.8) n = 2 + (int)Math.Floor(r * 3 * 0.75); }
            else if (d >= 2) { if (r2 < 0.5) n = 2 + (int)Math.Floor(r * 1.6); }
            else if (d >= 1) { if (r2 < 0.18) n = 2; }
            return (n, kind);
        }

        // ---------- Rock and ore ----------
        public int RockAmt(int x, int y) => RockAmtMap.TryGetValue(Tk(x, y), out var a) && a != 0 ? a : 1;
        public double DigDone(int x, int y) => Dig.TryGetValue(Tk(x, y), out var d) ? d : 0;
        public int ElevOf(int x, int y) => Elev.TryGetValue(Tk(x, y), out var e) && e != 0 ? e : 40;
        public double RockCap(int x, int y) { int a = RockAmt(x, y); return a >= 3 ? Noise.JsRound(MountCap * ElevOf(x, y) / 40.0) : (a + 3) * DigU; }
        public double RockLeft(int x, int y) => DecoMap[y, x] == Deco.Rock ? Math.Max(0, RockCap(x, y) - DigDone(x, y)) : 0;
        public int RockLevel(int x, int y) { int a = RockAmt(x, y); return a >= 3 ? 3 : Math.Max(-3, a - (int)Math.Floor(DigDone(x, y) / DigU)); }
        public double OreLeft(int x, int y) => DecoMap[y, x] == Deco.Ore ? (OreLeftMap.TryGetValue(Tk(x, y), out var o) ? o : OreCap) : 0;
        public bool IsMountain(int x, int y) => DecoMap[y, x] == Deco.Rock && RockAmt(x, y) >= 3;
        public int PitDepth(int x, int y) => Inb(x, y) && DecoMap[y, x] == Deco.Rock && RockAmt(x, y) < 3 ? Math.Max(0, -RockLevel(x, y)) : 0;
        public bool HasTree(int x, int y) => DecoMap[y, x] == Deco.Tree || DecoMap[y, x] == Deco.Sapling;
        public bool IsSolid(int x, int y) => DecoMap[y, x] == Deco.Ore || IsMountain(x, y) || PitDepth(x, y) > 0 || TileSpread(x, y) >= 3;
        public bool RockOpen(int x, int y)
        {
            if (!IsMountain(x, y)) return true;
            for (int k = 0; k < 4; k++)
            {
                int i = x + Nb[k, 0], j = y + Nb[k, 1];
                if (Inb(i, j) && (!IsMountain(i, j) || RockLeft(i, j) == 0)) return true;
            }
            return false;
        }
        public bool RockWorkable(int x, int y) => DecoMap[y, x] == Deco.Rock && RockLeft(x, y) > 0 && RockOpen(x, y);
    }
}
