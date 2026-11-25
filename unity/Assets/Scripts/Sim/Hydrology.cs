// Hydrology: deterministic water generation from terrain (DESIGN_WATER.md sections 5 to 10, 15, 16).
// Order, as in the spec: elevation -> sea level -> ocean-connected low land -> inland depressions -> drainage directions -> flow
// accumulation -> river network -> lakes and ponds -> mouths, estuaries and deltas -> banks and wet ground -> depth and salinity.
// Pure C#, no UnityEngine. Everything is a function of (seed, preset, input heights), so the same inputs always give the same map.
//
// The terrain heights it is given are changed only where water needs them: the sea floor is raised to the sea surface, lake and river
// tiles are made flat at their surface level and the banks are eased to within one level of it. Existing worlds never run this.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>The knobs of DESIGN_WATER.md section 15. Presets fill them; every field can also be set by hand.</summary>
    public sealed class HydroParams
    {
        public double Coverage = 0.5;          // how much of the map is water: scales the sea and the lake count
        public double SeaLevel = double.NegativeInfinity;   // tile level below which edge-connected land is sea (-infinity: no sea)
        public double RiverFrequency = 1;      // more means more (and smaller) catchments become rivers
        public double RiverSize = 1;           // multiplies river widths
        public double LakeFrequency = 1;
        public double LakeSize = 1;
        public double Coastline = 0.6;         // 0 smooth, 1 bays, capes and peninsulas
        public double Islands = 0.3;           // archipelago: how many islands
        public double Rainfall = 1;
        public double Wetness = 0.25;          // share of the shore that is marsh
        public double Tilt = 0.010;            // regional slope in levels per tile, so flat plains still drain somewhere
        public double Valley = 0;              // > 0: a broad valley is cut across the map (a major river fills it)
        public double LakeBasins = 2;          // lake basins cut into the ground (scaled by LakeFrequency and map area)
        public double LakeBasinDepth = 2.6;
        public bool FreshwaterRequired = true; // the start must be within reach of fresh water
        public double Delta = 0.5;             // chance that a wide river mouth fans out into a delta
        public Relief SuggestedRelief = Relief.Upland;   // the mountain coverage this preset is meant for

        public static HydroParams For(WaterPreset p)
        {
            var h = new HydroParams();
            switch (p)
            {
                case WaterPreset.RiverValley: h.Valley = 1; h.Rainfall = 1.25; h.RiverSize = 1.25; h.SuggestedRelief = Relief.Upland; h.LakeBasins = 1; break;
                case WaterPreset.Lakeside: h.LakeBasins = 3; h.LakeSize = 1.5; h.LakeFrequency = 1.3; h.Rainfall = 1.15; h.SuggestedRelief = Relief.Lowland; break;
                case WaterPreset.Coastal: h.SeaLevel = -0.6; h.Coverage = 0.55; h.Coastline = 0.75; h.Rainfall = 1.1; h.LakeBasins = 1; h.SuggestedRelief = Relief.Lowland; break;
                case WaterPreset.Island: h.SeaLevel = -0.6; h.Coverage = 0.6; h.Coastline = 0.75; h.Rainfall = 1.35; h.RiverFrequency = 1.4; h.LakeBasins = 1; h.SuggestedRelief = Relief.Lowland; break;
                case WaterPreset.Archipelago: h.SeaLevel = -0.6; h.Coverage = 0.75; h.Islands = 0.7; h.Coastline = 0.85; h.Rainfall = 1.35; h.RiverFrequency = 1.5; h.LakeBasins = 0.5; h.SuggestedRelief = Relief.Lowland; break;
                case WaterPreset.Wetlands: h.Rainfall = 1.7; h.RiverFrequency = 1.9; h.RiverSize = 0.8; h.Tilt = 0.004; h.Wetness = 0.85; h.LakeBasins = 5; h.LakeSize = 0.55; h.LakeBasinDepth = 1.6; h.SuggestedRelief = Relief.Lowland; break;
                case WaterPreset.AridOasis: h.Rainfall = 0.3; h.RiverFrequency = 0.45; h.RiverSize = 0.7; h.LakeBasins = 1; h.LakeSize = 0.45; h.Wetness = 0.12; h.SuggestedRelief = Relief.Upland; break;
                case WaterPreset.MountainWatershed: h.Rainfall = 1.5; h.RiverFrequency = 1.3; h.RiverSize = 0.9; h.LakeBasins = 3; h.LakeSize = 0.7; h.LakeBasinDepth = 3.2; h.Tilt = 0.006; h.SuggestedRelief = Relief.Alpine; break;
                default: h.LakeBasins = 2; break;   // BalancedInland
            }
            return h;
        }
    }

    public sealed class HydroResult
    {
        public int[] Hv;              // vertex heights with the water carved in ((size + 1)^2)
        public SurfaceWater Water;
        public double MaxDrop;
        public int Clipped;           // river tiles dropped to keep the town's ground dry (such an attempt is rejected)
    }

    public static class Hydrology
    {
        const double Eps = 1e-5;
        public const int MaxAttempts = 8;

        /// <summary>Generate the water of a map. hv0 is the untouched terrain ((size + 1)^2 vertex heights); start is the Town Hall's tile.
        /// The result is the first attempt that gives a valid start (see StartCheck), else the best of MaxAttempts.</summary>
        public static HydroResult Generate(int seed, WaterPreset preset, int[] hv0, int size, int start, HydroParams p = null)
        {
            p ??= HydroParams.For(preset);
            HydroResult best = null; int bestScore = int.MinValue;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var r = new Builder(seed, attempt, preset, p, hv0, size, start).Run();
                r.Water.StartAttempt = attempt;
                int score = StartScore(r, size, start, p, preset);
                r.Water.StartValid = score >= 0;
                if (score > bestScore) { best = r; bestScore = score; }
                if (score >= 0) return r;
            }
            return best;
        }

        /// <summary>Plain rolling terrain with a couple of ridges, for tests and tuning at any map size (the game uses World.GenRangeHeights).</summary>
        public static int[] SyntheticHeights(int seed, int size)
        {
            int m = size + 1, c = size / 2; var hv = new int[m * m];
            double a1 = Noise.Hash2(1, 2, seed) * Math.PI, a2 = Noise.Hash2(3, 4, seed) * Math.PI;
            double o1 = (28 + size * 0.2 * Noise.Hash2(5, 6, seed)), o2 = -(28 + size * 0.2 * Noise.Hash2(7, 8, seed));
            for (int b = 0; b < m; b++)
                for (int a = 0; a < m; a++)
                {
                    double f = Noise.VNoise(a * 0.05 + 30, b * 0.05 + 30, seed + 400) * 0.7 + Noise.VNoise(a * 0.12, b * 0.12, seed + 401) * 0.3;
                    double v = Math.Max(0, Math.Min(3.45, (f - 0.575) * 15));
                    double g = Noise.VNoise(a * 0.045 + 77, b * 0.045 + 77, seed + 500);
                    if (v < 0.5 && g < 0.27) v = -Math.Min(2.4, (0.27 - g) * 14);
                    double dx = a - c, dy = b - c;
                    double d1 = Math.Abs(-dx * Math.Sin(a1) + dy * Math.Cos(a1) - o1), d2 = Math.Abs(-dx * Math.Sin(a2) + dy * Math.Cos(a2) - o2);
                    double ridge = Math.Max(10 + 4 * Noise.VNoise(a * 0.08, b * 0.08, seed + 600) - d1 * 1.5, 8 + 4 * Noise.VNoise(a * 0.08 + 9, b * 0.08, seed + 601) - d2 * 1.5);
                    v = Math.Max(v, ridge);
                    int dist = Math.Max(Math.Abs(a - c), Math.Abs(b - c));
                    v *= Math.Min(1, Math.Max(0, (dist - 7) / 7.0));
                    hv[b * m + a] = Math.Max(Defs.HMin, Math.Min(Defs.HMax, Noise.JsRound(v)));
                }
            return hv;
        }

        // ---------- Is this a good place to start? (section 16) ----------
        /// <summary>0 or more when the start is fine, negative with the number of problems otherwise.</summary>
        public static int StartScore(HydroResult r, int size, int start, HydroParams p, WaterPreset preset)
        {
            var w = r.Water; int bad = 0; string why = "";
            // no water, wet ground or steep ground where the Town Hall, its roads and its first farm go
            for (int y = start - 12; y <= start + 14; y++)
                for (int x = start - 12; x <= start + 12; x++)
                    if (w.Class[y * size + x] != WaterClass.None) { bad++; why += "water at the town (" + w.Class[y * size + x] + " at " + (x - start) + "," + (y - start) + "); "; y = start + 15; break; }
            // enough walkable land around the start, all of it connected
            var seen = new bool[size * size]; var q = new Queue<int>(); int reach = 0, dry = 0;
            for (int y = start - 32; y < start + 32; y++) for (int x = start - 32; x < start + 32; x++) if (!w.IsWater(x, y)) dry++;
            q.Enqueue(start * size + start); seen[start * size + start] = true;
            while (q.Count > 0)
            {
                int c = q.Dequeue(); reach++; int cx = c % size, cy = c / size;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + SurfaceWater.Dx[1 + k * 2], ny = cy + SurfaceWater.Dy[1 + k * 2];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size || seen[ny * size + nx] || w.IsWater(nx, ny)) continue;
                    seen[ny * size + nx] = true; q.Enqueue(ny * size + nx);
                }
            }
            if (reach < 3000) { bad++; why += "reach " + reach + "; "; }
            int inDistrict = 0; for (int y = start - 32; y < start + 32; y++) for (int x = start - 32; x < start + 32; x++) if (seen[y * size + x]) inDistrict++;
            if (inDistrict < dry * 0.9) { bad++; why += "cut-off pockets " + inDistrict + "/" + dry + "; "; }   // pockets of land cut off by water inside the first district
            if (p.FreshwaterRequired && NearestFresh(w, size, start, preset == WaterPreset.Island || preset == WaterPreset.Archipelago ? 60 : 44) < 0) { bad++; why += "no fresh water near; "; }
            if (r.Clipped > 0) { bad++; why += "a river runs through the town; "; }
            w.StartProblems = why;
            return bad == 0 ? 1 : -bad;
        }

        /// <summary>Distance in tiles to the nearest fresh open water within maxD (square rings), else -1.</summary>
        public static int NearestFresh(SurfaceWater w, int size, int cx, int maxD)
        {
            for (int d = 1; d <= maxD; d++)
                for (int y = Math.Max(0, cx - d); y <= Math.Min(size - 1, cx + d); y++)
                    for (int x = Math.Max(0, cx - d); x <= Math.Min(size - 1, cx + d); x++)
                    {
                        if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cx)) != d) continue;
                        int i = y * size + x;
                        if (SurfaceWater.OpenWater(w.Class[i]) && !SurfaceWater.Salty(w.Class[i]) && w.Salinity[i] < 40) return d;
                    }
            return -1;
        }

        sealed class Builder
        {
            readonly int seed, n, c, attempt, M; readonly int s0;
            readonly WaterPreset preset; readonly HydroParams p; readonly int[] hv0; readonly int start;
            double[] H, E, F, T, Rain, Acc;     // terrain level, hydrologic level, filled level, bias, rain, accumulation
            int[] Recv, Order;
            bool[] Ocean;
            int[] hv;
            SurfaceWater W;
            double drop;
            int seaS, clipped;

            public Builder(int seed, int attempt, WaterPreset preset, HydroParams p, int[] hv0, int size, int start)
            {
                this.seed = seed; this.attempt = attempt; this.preset = preset; this.p = p; this.hv0 = hv0; n = size; this.start = start; c = start; M = size + 1;
                s0 = seed * 31 + 7001 + attempt * 977;
            }

            double R(int k) => Noise.Hash2(k, 91, s0);
            double Smooth(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
            double Vn(double x, double y, int k, double f) => Noise.VNoise(x * f + k * 13.7, y * f + k * 7.3, s0 + k);

            public HydroResult Run()
            {
                hv = (int[])hv0.Clone();
                W = new SurfaceWater(n, seed, preset);
                seaS = (int)Math.Round(double.IsNegativeInfinity(p.SeaLevel) ? 0 : p.SeaLevel);   // -0.6 -> -1
                ShapeSea();                  // 1-2: elevation and sea level (cut the sea into the terrain)
                ComputeLevels();             // terrain tile levels and the drainage bias
                FindOcean();                 // 3
                Flood();                     // 4-5: inland depressions, drainage directions
                Accumulate();                // 6
                var lakeTiles = FindLakes(); // 8
                var surface = new short[n * n];
                Rivers(surface, lakeTiles);  // 7, 9, 10
                Deltas(surface);
                Classify(lakeTiles);
                Depths();
                Wetlands();
                Carve(surface);
                W.RebuildBodies();
                return new HydroResult { Hv = hv, Water = W, MaxDrop = drop, Clipped = clipped };
            }

            // ---------- 1-2. the sea: coastlines, bays, islands ----------
            double CoastWarp(double a, double b)
            {
                double k = p.Coastline;
                return (Vn(a, b, 11, 0.028) - 0.5) * 2 * 15 * k + (Vn(a, b, 12, 0.085) - 0.5) * 2 * 5.5 * k + (Vn(a, b, 13, 0.2) - 0.5) * 2 * 1.4 * k;
            }

            void ShapeSea()
            {
                if (double.IsNegativeInfinity(p.SeaLevel)) return;
                int side = (int)(R(1) * 4); int[] sx = { 1, 0, -1, 0 }, sy = { 0, 1, 0, -1 };
                double coastD = 66 + 12 * R(2) - 8 * (p.Coverage - 0.5);   // tiles from the centre to the coast (coastal)
                double islandR = n * (0.265 + 0.05 * R(3)) + 8 * (0.6 - p.Coverage);
                double thr = 0.63 - 0.12 * p.Islands;
                double bx = R(4) * 500, by = R(5) * 500;
                for (int b = 0; b < M; b++)
                    for (int a = 0; a < M; a++)
                    {
                        double t;   // tiles offshore (> 0 is sea)
                        double warp = CoastWarp(a, b);
                        double dx = a - c, dy = b - c;
                        if (preset == WaterPreset.Coastal) t = (dx * sx[side] + dy * sy[side]) - (coastD + warp);
                        else if (preset == WaterPreset.Island) t = Math.Sqrt(dx * dx + dy * dy) - (islandR + warp);
                        else
                        {
                            double main = Math.Sqrt(dx * dx + dy * dy) - (n * 0.2 + 0.4 * warp + 2);
                            double m = Noise.VNoise(a * 0.022 + bx, b * 0.022 + by, s0 + 21) * 0.65 + Noise.VNoise(a * 0.06 + by, b * 0.06 + bx, s0 + 22) * 0.35;
                            double blobs = (thr - m) * 52;
                            t = Math.Min(main, blobs);
                        }
                        if (t <= 0) continue;
                        double d = Math.Min(1, t / 4.5) * 5.2 + Math.Max(0, t - 4.5) * 0.18;
                        int k = b * M + a; int nv = Math.Max(Defs.HMin, (int)Math.Round(Math.Min(hv[k] - d, 1.5 - d * 1.4)));
                        drop = Math.Max(drop, hv[k] - nv); hv[k] = nv;
                    }
            }

            double Level(int x, int y) => (hv[y * M + x] + hv[y * M + x + 1] + hv[(y + 1) * M + x] + hv[(y + 1) * M + x + 1]) / 4.0;
            int Spread(int x, int y)
            {
                int a = hv[y * M + x], b = hv[y * M + x + 1], cc = hv[(y + 1) * M + x], d = hv[(y + 1) * M + x + 1];
                return Math.Max(Math.Max(a, b), Math.Max(cc, d)) - Math.Min(Math.Min(a, b), Math.Min(cc, d));
            }

            // ---------- 5 (setup). terrain levels plus the bias that makes flat land drain ----------
            void ComputeLevels()
            {
                int nn = n * n;
                H = new double[nn]; E = new double[nn]; T = new double[nn]; Rain = new double[nn];
                double ang = R(6) * Math.PI * 2; double tx = Math.Cos(ang), ty = Math.Sin(ang);
                if (preset == WaterPreset.Coastal) { int side = (int)(R(1) * 4); int[] sx = { 1, 0, -1, 0 }, sy = { 0, 1, 0, -1 }; tx = sx[side] * 0.8 + (R(7) - 0.5) * 0.5; ty = sy[side] * 0.8 + (R(8) - 0.5) * 0.5; }   // drain towards the coast
                // river valley: a curved trench across the map, 22 to 38 tiles beside the town
                double va = R(9) * Math.PI, vdx = Math.Cos(va), vdy = Math.Sin(va), voff = (38 + 12 * R(10)) * (R(11) < 0.5 ? -1 : 1), vph = R(12) * 90;
                if (p.Valley > 0) { double sg = R(13) < 0.5 ? 1 : -1; tx = vdx * sg; ty = vdy * sg; }
                var basins = PlanBasins();
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int i = y * n + x; double lv = Level(x, y); H[i] = lv;
                        double bias = -p.Tilt * ((x - c) * tx + (y - c) * ty);
                        bias += (Vn(x, y, 31, 0.012) - 0.5) * 2.6 + (Vn(x, y, 32, 0.035) - 0.5) * 1.1 + (Vn(x, y, 33, 0.1) - 0.5) * 0.35;
                        double dx = x - c, dy = y - c, dd = Math.Sqrt(dx * dx + dy * dy);
                        bias += 2.2 * Smooth(1 - dd / 30);   // a gentle rise under the town: water goes round it
                        if (p.Valley > 0)
                        {
                            double along = dx * vdx + dy * vdy, perp = -dx * vdy + dy * vdx;
                            double pw = perp - voff - (Vn(along, 0, 34, 0.02) - 0.5) * 22 - (Vn(along, 0, 35, 0.06) - 0.5) * 9;
                            bias -= 1.7 * p.Valley * Math.Exp(-(pw * pw) / (2 * 8.5 * 8.5));
                        }
                        foreach (var bs in basins)
                        {
                            double ex = x - bs.X, ey = y - bs.Y;
                            double rr = bs.R * (1 + (Vn(x, y, 36, 0.11) - 0.5) * 0.9);
                            double q = Math.Sqrt(ex * ex + ey * ey) / rr;
                            if (q < 1) bias -= bs.Depth * (1 - q * q) * (1 - q * q);
                        }
                        T[i] = bias; E[i] = lv + bias;
                        double rain = p.Rainfall * (0.75 + 0.5 * Vn(x, y, 37, 0.02));
                        rain *= 1 + 0.5 * Math.Max(0, Math.Min(1.6, lv / 3));
                        Rain[i] = rain;
                    }
            }

            struct Basin { public double X, Y, R, Depth; }
            List<Basin> PlanBasins()
            {
                var list = new List<Basin>();
                double count = p.LakeBasins * p.LakeFrequency * (n / 128.0) * (n / 128.0) * (0.55 + 0.9 * p.Coverage);
                int want = (int)Math.Round(count);
                if (want == 0 && count > 0.3) want = 1;
                for (int k = 0, tries = 0; list.Count < want && tries < 400; tries++)
                {
                    k++;
                    double x = 14 + R(100 + k * 3) * (n - 28), y = 14 + R(101 + k * 3) * (n - 28);
                    double rad = (7 + 12 * R(102 + k * 3) * R(103 + k)) * p.LakeSize * (0.75 + 0.5 * (n / 256.0));
                    if (preset == WaterPreset.Lakeside && list.Count == 0) { rad = 24 * p.LakeSize; double a = R(104) * 6.283, d = 48 + 14 * R(105); x = c + Math.Cos(a) * d; y = c + Math.Sin(a) * d; }
                    if (preset == WaterPreset.AridOasis && list.Count == 0) { rad = 7 + 3 * R(106); double a = R(104) * 6.283, d = 28 + 12 * R(105); x = c + Math.Cos(a) * d; y = c + Math.Sin(a) * d; }
                    if (x < 12 || y < 12 || x > n - 12 || y > n - 12) continue;
                    if (Math.Max(Math.Abs(x - c), Math.Abs(y - c)) < 24 + rad) continue;
                    bool near = false; foreach (var o in list) if (Math.Sqrt((o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y)) < (o.R + rad) * 0.9) near = true;
                    if (near) continue;
                    int ix = (int)x, iy = (int)y;
                    if (Level(ix, iy) >= Defs.MountLevel - 1.2 && preset != WaterPreset.MountainWatershed) continue;
                    if (Level(ix, iy) >= Defs.MountLevel - 0.3) continue;
                    if (Level(ix, iy) < p.SeaLevel + 1.5) continue;
                    list.Add(new Basin { X = x, Y = y, R = rad, Depth = p.LakeBasinDepth * (0.8 + 0.4 * R(107 + k)) });
                }
                return list;
            }

            // ---------- 3. the sea is every edge-connected tile below sea level ----------
            void FindOcean()
            {
                Ocean = new bool[n * n];
                if (double.IsNegativeInfinity(p.SeaLevel)) return;
                var q = new Queue<int>();
                for (int i = 0; i < n * n; i++)
                {
                    int x = i % n, y = i / n;
                    if ((x == 0 || y == 0 || x == n - 1 || y == n - 1) && H[i] < p.SeaLevel) { Ocean[i] = true; q.Enqueue(i); }
                }
                while (q.Count > 0)
                {
                    int cc = q.Dequeue(), x = cc % n, y = cc / n;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2];
                        if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                        int ni = ny * n + nx; if (Ocean[ni] || H[ni] >= p.SeaLevel) continue;
                        Ocean[ni] = true; q.Enqueue(ni);
                    }
                }
                // keep the shore from being one tile wide strip: tiny dry pockets inside the sea become sea too
                // (kept simple: pockets smaller than 3 tiles are rare because the sea floor is cut smoothly)
            }

            // ---------- 4-5. priority flood: fill depressions, give every tile a downhill neighbour ----------
            void Flood()
            {
                int nn = n * n; F = new double[nn]; Recv = new int[nn]; Order = new int[nn]; var done = new bool[nn];
                var heap = new Heap(nn);
                for (int i = 0; i < nn; i++)
                {
                    int x = i % n, y = i / n;
                    bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    if (Ocean[i]) { F[i] = p.SeaLevel; Recv[i] = -1; done[i] = true; heap.Push(F[i], i); }
                    else if (edge) { F[i] = E[i]; Recv[i] = -1; done[i] = true; heap.Push(F[i], i); }
                    else Recv[i] = -2;
                }
                int cnt = 0;
                while (heap.Count > 0)
                {
                    int cc = heap.Pop(out double key); Order[cnt++] = cc;
                    int x = cc % n, y = cc / n;
                    for (int k = 1; k <= 8; k++)
                    {
                        int nx = x + SurfaceWater.Dx[k], ny = y + SurfaceWater.Dy[k];
                        if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                        int ni = ny * n + nx; if (done[ni]) continue;
                        done[ni] = true; Recv[ni] = cc;
                        double step = (k & 1) == 0 ? Eps * 1.41 : Eps;
                        F[ni] = Math.Max(E[ni], key + step);
                        heap.Push(F[ni], ni);
                    }
                }
            }

            // ---------- 6. rain runs downhill and collects ----------
            void Accumulate()
            {
                int nn = n * n; Acc = new double[nn];
                for (int i = 0; i < nn; i++) Acc[i] = Ocean[i] ? 0 : Rain[i];
                for (int k = nn - 1; k >= 0; k--) { int i = Order[k], r = Recv[i]; if (r >= 0 && !Ocean[i]) Acc[r] += Acc[i]; }
            }

            // ---------- 8. lakes and ponds: filled depressions that are deep and big enough ----------
            sealed class Lake { public List<int> Tiles = new List<int>(); public double MaxDepth; public double SurfF; public int Surface; }
            List<Lake> lakes = new List<Lake>();
            int[] lakeOf;

            int[] FindLakes()
            {
                int nn = n * n; lakeOf = new int[nn]; for (int i = 0; i < nn; i++) lakeOf[i] = -1;
                var cand = new List<Lake>(); var seen = new bool[nn]; var q = new Queue<int>();
                for (int s = 0; s < nn; s++)
                {
                    if (seen[s] || Ocean[s] || F[s] - E[s] < 0.3 || H[s] >= Defs.MountLevel - 0.5) continue;
                    var lk = new Lake(); seen[s] = true; q.Enqueue(s);
                    while (q.Count > 0)
                    {
                        int cc = q.Dequeue(); lk.Tiles.Add(cc); lk.MaxDepth = Math.Max(lk.MaxDepth, F[cc] - E[cc]);
                        int x = cc % n, y = cc / n;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2];
                            if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1) continue;
                            int ni = ny * n + nx; if (seen[ni] || Ocean[ni] || F[ni] - E[ni] < 0.3 || H[ni] >= Defs.MountLevel - 0.5) continue;
                            seen[ni] = true; q.Enqueue(ni);
                        }
                    }
                    if (lk.Tiles.Count >= 4 && lk.MaxDepth >= 0.5 && lk.Tiles.Count <= 1600 * p.LakeSize * p.LakeSize * (n / 256.0) * (n / 256.0)) cand.Add(lk);   // a bigger flooded hollow is a plain, not a lake: it is the ridges damming it
                }
                // keep the most significant ones: how many follows the lake frequency and the map area
                cand.Sort((a, b) => (b.Tiles.Count * b.MaxDepth).CompareTo(a.Tiles.Count * a.MaxDepth));
                int keep = (int)Math.Ceiling((3 + 3 * p.LakeBasins) * p.LakeFrequency * Math.Min(1, p.Rainfall) * (n / 128.0) * (n / 128.0));
                for (int k = 0; k < cand.Count && lakes.Count < keep; k++)
                {
                    var lk = cand[k];
                    // the start area stays dry
                    bool nearStart = false; foreach (int t in lk.Tiles) if (Math.Max(Math.Abs(t % n - c), Math.Abs(t / n - c)) < 16) { nearStart = true; break; }
                    if (nearStart) continue;
                    bool nearSea = false; if (!double.IsNegativeInfinity(p.SeaLevel)) foreach (int t in lk.Tiles) { int tx = t % n, ty = t / n; for (int dy = -2; dy <= 2 && !nearSea; dy++) for (int dx = -2; dx <= 2; dx++) { int ux = tx + dx, uy = ty + dy; if (ux >= 0 && uy >= 0 && ux < n && uy < n && Ocean[uy * n + ux]) { nearSea = true; break; } } if (nearSea) break; }
                    if (nearSea) continue;   // water of two levels never touches
                    double sum = 0; foreach (int t in lk.Tiles) sum += F[t] - T[t];
                    lk.SurfF = sum / lk.Tiles.Count;
                    // water cannot stand higher than the lowest bank
                    int lowBank = int.MaxValue; var set = new HashSet<int>(lk.Tiles);
                    foreach (int t in lk.Tiles)
                    {
                        int x = t % n, y = t / n;
                        for (int kk = 0; kk < 4; kk++)
                        {
                            int nx = x + SurfaceWater.Dx[1 + kk * 2], ny = y + SurfaceWater.Dy[1 + kk * 2];
                            if (nx < 0 || ny < 0 || nx >= n || ny >= n || set.Contains(ny * n + nx)) continue;
                            lowBank = Math.Min(lowBank, (int)Math.Floor(H[ny * n + nx] + 0.5));
                        }
                    }
                    int surf = (int)Math.Round(lk.SurfF);
                    if (lowBank != int.MaxValue) surf = Math.Min(surf, lowBank);
                    lk.Surface = Math.Max(Defs.HMin + 1, surf);
                    if (!double.IsNegativeInfinity(p.SeaLevel) && lk.Surface <= seaS) continue;   // a lake below the sea would be sea
                    int id = lakes.Count; lakes.Add(lk);
                    foreach (int t in lk.Tiles) lakeOf[t] = id;
                }
                return lakeOf;
            }

            // ---------- 7, 9, 10. rivers: width from drainage area, smooth curves, junctions, mouths ----------
            double Thresh => 200.0 / Math.Max(0.2, p.RiverFrequency);
            double RiverWidth(double acc) => Math.Max(1, Math.Pow(acc / Thresh, 0.42) * p.RiverSize);
            double[] oceanDist;
            bool[] riverCell;

            void Rivers(short[] surface, int[] lakeTiles)
            {
                int nn = n * n; riverCell = new bool[nn];
                double thr = Thresh;
                // distance (in tiles) from the sea over land, for estuaries and salinity
                oceanDist = new double[nn]; for (int i = 0; i < nn; i++) oceanDist[i] = 99;
                if (!double.IsNegativeInfinity(p.SeaLevel))
                {
                    var q = new Queue<int>(); for (int i = 0; i < nn; i++) if (Ocean[i]) { oceanDist[i] = 0; q.Enqueue(i); }
                    while (q.Count > 0)
                    {
                        int cc = q.Dequeue(), x = cc % n, y = cc / n;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2]; if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                            int ni = ny * n + nx; if (oceanDist[ni] <= oceanDist[cc] + 1 || oceanDist[cc] >= 14) continue;
                            oceanDist[ni] = oceanDist[cc] + 1; q.Enqueue(ni);
                        }
                    }
                }
                // river surface levels, from the mouth upstream (a receiver is always popped before its tiles)
                var level = new int[nn];
                for (int k = 0; k < nn; k++)
                {
                    int i = Order[k]; if (Ocean[i] || Acc[i] < thr || H[i] >= Defs.MountLevel - 0.4) continue;
                    if (lakeTiles[i] >= 0) continue;
                    riverCell[i] = true;
                }
                // a river that would run through the town's ground is dried up from there upstream (a big one rejects the whole attempt)
                var doomed = new bool[nn];
                for (int k = 0; k < nn; k++)
                {
                    int i = Order[k]; if (!riverCell[i]) continue;
                    int r = Recv[i];
                    bool inZone = Math.Max(Math.Abs(i % n - c), Math.Abs(i / n - c)) < 16;
                    if (inZone || (r >= 0 && doomed[r])) { doomed[i] = true; if (Acc[i] > thr * 6) clipped++; }
                    else if (r >= 0 && !Ocean[r] && H[r] >= Defs.MountLevel - 0.4) doomed[i] = true;   // a stream that would run into a mountain wall starts below it instead
                }
                for (int i = 0; i < nn; i++) if (doomed[i]) riverCell[i] = false;
                for (int k = 0; k < nn; k++)
                {
                    int i = Order[k]; if (!riverCell[i]) continue;
                    int r = Recv[i]; int refMin = int.MinValue;
                    if (r >= 0) { if (Ocean[r]) refMin = seaS; else if (lakeTiles[r] >= 0) refMin = lakes[lakeTiles[r]].Surface; else if (riverCell[r]) refMin = level[r]; }
                    int baseL = (int)Math.Floor(H[i] + 0.5);
                    level[i] = refMin == int.MinValue ? baseL : Math.Max(refMin, Math.Min(baseL, refMin + 1));
                }
                // stamp: walk each river cell towards its receiver along a smoothly warped line, painting a disc as wide as the river
                var bestAcc = new double[nn];
                for (int k = nn - 1; k >= 0; k--)   // upstream first, so the widest (downstream) river wins where discs overlap
                {
                    int i = Order[k]; if (!riverCell[i]) continue;
                    int r = Recv[i]; if (r < 0) { StampEnd(i, surface, level, bestAcc); continue; }
                    StampSegment(i, r, surface, level, bestAcc);
                }
            }

            void Warp(double x, double y, out double wx, out double wy)
            {
                wx = (Vn(x, y, 41, 0.06) - 0.5) * 2.6 + (Vn(x, y, 42, 0.17) - 0.5) * 0.9;
                wy = (Vn(x, y, 43, 0.06) - 0.5) * 2.6 + (Vn(x, y, 44, 0.17) - 0.5) * 0.9;
                double f = Math.Min(1, Math.Min(Math.Min(x, y), Math.Min(n - x, n - y)) / 5.0); wx *= f; wy *= f;   // rivers meet the map edge squarely
            }

            void StampEnd(int i, short[] surface, int[] level, double[] bestAcc) => StampAt(i % n + 0.5, i / n + 0.5, i, i, surface, level, bestAcc);

            void StampSegment(int i, int r, short[] surface, int[] level, double[] bestAcc)
            {
                double x0 = i % n + 0.5, y0 = i / n + 0.5, x1 = r % n + 0.5, y1 = r / n + 0.5;
                double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)); int steps = (int)Math.Ceiling(len / 0.3);
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps, x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                    Warp(x, y, out double wx, out double wy);
                    // the warp fades out at the receiver tile centres of the sea so the mouth lands in the sea
                    StampAt(x + wx, y + wy, i, r, surface, level, bestAcc);
                }
            }

            void StampAt(double px, double py, int src, int recv, short[] surface, int[] level, double[] bestAcc)
            {
                double acc = Acc[src]; double w = RiverWidth(acc);
                int sx = src % n, sy = src / n;
                double od = oceanDist[src];
                if (od < 10 && w >= 3) w *= 1 + (1 - od / 10) * 0.9;    // the river widens into an estuary
                double rad = w / 2;
                int x0 = (int)Math.Floor(px - rad), x1 = (int)Math.Floor(px + rad), y0 = (int)Math.Floor(py - rad), y1 = (int)Math.Floor(py + rad);
                int dir = recv == src ? 0 : DirOf(sx, sy, recv % n, recv / n);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (x < 0 || y < 0 || x >= n || y >= n) continue;
                        double ddx = x + 0.5 - px, ddy = y + 0.5 - py;
                        bool inside = (x == (int)Math.Floor(px) && y == (int)Math.Floor(py)) || ddx * ddx + ddy * ddy <= rad * rad;
                        if (!inside) continue;
                        int j = y * n + x;
                        if (Ocean[j] || H[j] >= Defs.MountLevel - 0.3 || lakeOf[j] >= 0) continue;
                        if (Math.Max(Math.Abs(x - c), Math.Abs(y - c)) < 14) continue;   // never inside the town's own ground (rivers through it are dried up in Rivers())
                        if (acc < bestAcc[j]) continue;
                        bestAcc[j] = acc;
                        W.Class[j] = WaterClass.Stream;   // refined in Classify
                        W.Width[j] = (byte)Math.Min(255, Math.Round(w));
                        W.Flow[j] = (byte)dir;
                        W.Strength[j] = (byte)Math.Min(255, 40 + 215 * Math.Min(1, Math.Sqrt(acc / (Thresh * 400))));
                        surface[j] = (short)level[src];
                    }
            }

            static int DirOf(int x0, int y0, int x1, int y1)
            {
                int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
                for (int k = 1; k <= 8; k++) if (SurfaceWater.Dx[k] == dx && SurfaceWater.Dy[k] == dy) return k;
                return 0;
            }

            // ---------- 8 (mouths). a wide river mouth on low land can fan out into a delta ----------
            void Deltas(short[] surface)
            {
                if (double.IsNegativeInfinity(p.SeaLevel) || p.Delta <= 0) return;
                int nn = n * n; var mouths = new List<int>();
                for (int i = 0; i < nn; i++)
                    if (riverCell[i] && Recv[i] >= 0 && Ocean[Recv[i]] && RiverWidth(Acc[i]) >= 5 && H[i] <= 1.6) mouths.Add(i);
                mouths.Sort((a, b) => Acc[b].CompareTo(Acc[a]));
                int made = 0;
                foreach (int m in mouths)
                {
                    if (made >= 2 || R(300 + m % 97) > p.Delta) continue;
                    bool close = false; foreach (int o in deltaMouths) if (Math.Abs(o % n - m % n) + Math.Abs(o / n - m / n) < 30) close = true;
                    if (close) continue;
                    deltaMouths.Add(m); made++;
                    double w0 = RiverWidth(Acc[m]);
                    int mx = m % n, my = m / n, rx = Recv[m] % n, ry = Recv[m] / n;
                    double ang0 = Math.Atan2(ry - my, rx - mx);
                    int branches = 2 + (R(310 + m % 53) < 0.5 ? 1 : 0);
                    for (int bI = 0; bI < branches; bI++)
                    {
                        double spread = (bI - (branches - 1) / 2.0) * (0.62 + 0.15 * R(320 + bI));
                        double ang = ang0 + spread; double x = mx - Math.Cos(ang0) * 5 + 0.5, y = my - Math.Sin(ang0) * 5 + 0.5;   // start upstream of the mouth, so branches leave the main river
                        double w = Math.Max(1.5, w0 * 0.45);
                        for (int step = 0; step < 36; step++)
                        {
                            ang += (Vn(x, y, 51 + bI, 0.1) - 0.5) * 0.35;
                            x += Math.Cos(ang) * 0.6; y += Math.Sin(ang) * 0.6;
                            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                            if (ix < 1 || iy < 1 || ix >= n - 1 || iy >= n - 1) break;
                            int j = iy * n + ix;
                            if (Ocean[j] && step > 8) break;
                            double rad = w / 2 * (1 + 0.012 * step);
                            for (int yy = (int)Math.Floor(y - rad); yy <= (int)Math.Floor(y + rad); yy++)
                                for (int xx = (int)Math.Floor(x - rad); xx <= (int)Math.Floor(x + rad); xx++)
                                {
                                    if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                                    double ddx = xx + 0.5 - x, ddy = yy + 0.5 - y;
                                    if (!(xx == ix && yy == iy) && ddx * ddx + ddy * ddy > rad * rad) continue;
                                    int q = yy * n + xx;
                                    if (Ocean[q] || H[q] >= Defs.MountLevel - 0.3 || lakeOf[q] >= 0) continue;
                                    if (W.Class[q] != WaterClass.None && (W.Flags[q] & WaterFlags.Delta) == 0) continue;   // never narrow the main river
                                    W.Class[q] = WaterClass.Stream; W.Width[q] = (byte)Math.Max((int)W.Width[q], (int)Math.Round(w)); W.Flags[q] |= WaterFlags.Delta;
                                    W.Flow[q] = (byte)DirFromAngle(ang); W.Strength[q] = (byte)Math.Max((int)W.Strength[q], 90);
                                    surface[q] = (short)seaS;
                                }
                        }
                    }
                }
            }
            readonly List<int> deltaMouths = new List<int>();

            static int DirFromAngle(double a)
            {
                int k = (int)Math.Round(a / (Math.PI / 4)); k = ((k % 8) + 8) % 8;   // 0 = east (+x), 2 = south (+y)
                return k + 1;
            }

            // ---------- classification: stream, small river, major river, estuary, pond, lake, sea, ocean ----------
            void Classify(int[] lakeTiles)
            {
                int nn = n * n;
                for (int i = 0; i < nn; i++)
                {
                    if (Ocean[i]) { W.Class[i] = WaterClass.Ocean; W.Salinity[i] = 255; W.Surface[i] = (short)seaS; continue; }
                    if (lakeTiles[i] >= 0)
                    {
                        var lk = lakes[lakeTiles[i]];
                        W.Class[i] = lk.Tiles.Count >= 26 ? WaterClass.Lake : WaterClass.Pond; W.Surface[i] = (short)lk.Surface;
                        W.Flags[i] |= WaterFlags.Standing; continue;
                    }
                }
                // rivers
                for (int i = 0; i < nn; i++)
                {
                    if (W.Class[i] != WaterClass.Stream) continue;
                    int w = W.Width[i];
                    bool near = oceanDist[i] < 9 && w >= 3 && (W.Flags[i] & WaterFlags.Delta) == 0;
                    W.Class[i] = near ? WaterClass.Estuary : w <= 2 ? WaterClass.Stream : w < 9 ? WaterClass.SmallRiver : WaterClass.MajorRiver;
                    if ((W.Flags[i] & WaterFlags.Delta) != 0) W.Class[i] = w <= 2 ? WaterClass.Stream : WaterClass.SmallRiver;
                    double od = oceanDist[i]; if (od < 9) W.Salinity[i] = (byte)Math.Max((int)W.Salinity[i], (int)Math.Round(255 * (1 - od / 9.0) * (1 - od / 9.0)));
                }
                // marine bodies: Ocean is large, Sea is a smaller marine body
                var seen = new bool[nn]; var q = new Queue<int>();
                for (int s = 0; s < nn; s++)
                {
                    if (seen[s] || W.Class[s] != WaterClass.Ocean) continue;
                    var comp = new List<int>(); seen[s] = true; q.Enqueue(s);
                    while (q.Count > 0)
                    {
                        int cc = q.Dequeue(); comp.Add(cc); int x = cc % n, y = cc / n;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2]; if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                            int ni = ny * n + nx; if (seen[ni] || W.Class[ni] != WaterClass.Ocean) continue; seen[ni] = true; q.Enqueue(ni);
                        }
                    }
                    if (comp.Count < 8000 * (n / 256.0) * (n / 256.0)) foreach (int t in comp) W.Class[t] = WaterClass.Sea;
                }
            }

            // ---------- 12. depth: shallow shores, medium open water, deep channels and offshore ----------
            void Depths()
            {
                int nn = n * n; var dist = new int[nn]; var q = new Queue<int>();
                for (int i = 0; i < nn; i++) dist[i] = -1;
                for (int i = 0; i < nn; i++)
                {
                    if (!SurfaceWater.OpenWater(W.Class[i])) continue;
                    int x = i % n, y = i / n; bool shore = false;
                    for (int k = 0; k < 4 && !shore; k++) { int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2]; if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue; if (!SurfaceWater.OpenWater(W.Class[ny * n + nx])) shore = true; }
                    if (shore) { dist[i] = 1; q.Enqueue(i); }
                }
                while (q.Count > 0)
                {
                    int cc = q.Dequeue(), x = cc % n, y = cc / n;
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + SurfaceWater.Dx[1 + k], ny = y + SurfaceWater.Dy[1 + k]; if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                        int ni = ny * n + nx; if (dist[ni] >= 0 || !SurfaceWater.OpenWater(W.Class[ni])) continue;
                        dist[ni] = dist[cc] + 1; q.Enqueue(ni);
                    }
                }
                for (int i = 0; i < nn; i++)
                {
                    var cl = W.Class[i]; if (!SurfaceWater.OpenWater(cl)) continue;
                    int d = dist[i] < 0 ? 99 : dist[i]; WaterDepth dep;
                    int x = i % n, y = i / n;
                    switch (cl)
                    {
                        case WaterClass.Ocean:
                        case WaterClass.Sea:
                        {
                            double bed = seaS - H[i];   // how far the original sea floor lay below the sea surface
                            dep = d <= 2 || bed < 1.2 ? WaterDepth.Shallow : d <= 6 || bed < 3.5 ? WaterDepth.Medium : WaterDepth.Deep;
                            if (d <= 1) dep = WaterDepth.Shallow;
                            break;
                        }
                        case WaterClass.Pond: dep = d <= 2 ? WaterDepth.Shallow : WaterDepth.Medium; break;
                        case WaterClass.Lake:
                        {
                            var lk = lakes[lakeOf[i]]; double dd = lk.MaxDepth;
                            dep = d <= 2 ? WaterDepth.Shallow : (d >= 5 && dd >= 1.5 && lk.Tiles.Count >= 120) ? WaterDepth.Deep : WaterDepth.Medium;
                            break;
                        }
                        default:
                        {
                            int w = W.Width[i];
                            dep = w <= 2 ? WaterDepth.Shallow : d <= 1 ? WaterDepth.Shallow : (w >= 12 && d >= 3) ? WaterDepth.Deep : WaterDepth.Medium;
                            if (w >= 3 && w < 12 && d <= 1) dep = WaterDepth.Shallow;
                            break;
                        }
                    }
                    W.Depth[i] = dep;
                }
            }

            // ---------- wet ground along slow, low shores ----------
            void Wetlands()
            {
                int nn = n * n; double amount = p.Wetness;
                var mark = new List<int>();
                for (int y = 1; y < n - 1; y++)
                    for (int x = 1; x < n - 1; x++)
                    {
                        int i = y * n + x; if (W.Class[i] != WaterClass.None || H[i] >= 2.2 || Spread(x, y) > 1) continue;
                        if (Math.Max(Math.Abs(x - c), Math.Abs(y - c)) < 16) continue;
                        // nearest open water within 3 tiles that is fresh (marsh lines fresh shores, not open sea beaches)
                        int nearest = 99; bool fresh = true; double surf = 0;
                        for (int dy = -3; dy <= 3; dy++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                                int j = ny * n + nx; if (!SurfaceWater.OpenWater(W.Class[j])) continue;
                                int dd = Math.Max(Math.Abs(dx), Math.Abs(dy));
                                if (dd < nearest) { nearest = dd; fresh = !SurfaceWater.Salty(W.Class[j]); surf = W.Surface[j]; }
                            }
                        if (nearest > 3 || !fresh) continue;
                        if (H[i] > surf + 1.2) continue;
                        double reach = 1.2 + 2.2 * amount;   // how far marsh spreads from the water
                        double noise = Vn(x, y, 61, 0.18);
                        if (nearest <= reach * (0.35 + 0.9 * noise) && noise > 0.62 - 0.5 * amount) mark.Add(i);
                    }
                foreach (int i in mark) { W.Class[i] = WaterClass.Wetland; W.Flags[i] |= WaterFlags.Saturated; W.Depth[i] = WaterDepth.Shallow; }
            }

            // ---------- 10. carve the terrain: flat water, eased banks ----------
            void Carve(short[] riverSurface)
            {
                int nn = n * n; var pinned = new bool[M * M]; var want = new int[M * M]; for (int k = 0; k < want.Length; k++) want[k] = int.MaxValue;
                for (int i = 0; i < nn; i++)
                {
                    var cl = W.Class[i]; if (!SurfaceWater.OpenWater(cl)) continue;
                    int s = cl == WaterClass.Ocean || cl == WaterClass.Sea ? seaS : SurfaceWater.Standing(cl) ? W.Surface[i] : riverSurface[i];
                    if (SurfaceWater.River(cl)) W.Surface[i] = (short)s;
                    int x = i % n, y = i / n;
                    for (int dy = 0; dy <= 1; dy++) for (int dx = 0; dx <= 1; dx++) { int k = (y + dy) * M + x + dx; want[k] = Math.Min(want[k], s); }
                }
                for (int k = 0; k < want.Length; k++) if (want[k] != int.MaxValue) { hv[k] = want[k]; pinned[k] = true; }
                // ease the banks: no vertex more than one level (two on high ground) from its neighbour; water vertices never move
                for (int pass = 0; pass < 24; pass++)
                {
                    bool ch = false;
                    for (int b = 0; b < M; b++)
                        for (int a = 0; a < M; a++)
                        {
                            int k = b * M + a, h = hv[k];
                            for (int d = 0; d < 4; d++)
                            {
                                int na = a + (d == 0 ? 1 : d == 2 ? -1 : 0), nb = b + (d == 1 ? 1 : d == 3 ? -1 : 0);
                                if (na < 0 || nb < 0 || na >= M || nb >= M) continue;
                                int k2 = nb * M + na, o = hv[k2], lim = Math.Max(h, o) <= 3 ? 1 : 2;
                                if (h - o > lim)
                                {
                                    if (!pinned[k2]) { hv[k2] = h - lim; ch = true; }
                                    else if (!pinned[k]) { hv[k] = o + lim; ch = true; }
                                }
                            }
                        }
                    if (!ch) break;
                }
                for (int k = 0; k < hv.Length; k++) drop = Math.Max(drop, hv0[k] - hv[k]);
            }
        }

        // ---------- a small binary min-heap on (key, index): ties break on the index so results are deterministic ----------
        sealed class Heap
        {
            double[] key; int[] val; public int Count;
            public Heap(int cap) { key = new double[cap + 16]; val = new int[cap + 16]; }
            bool Less(int a, int b) => key[a] < key[b] || (key[a] == key[b] && val[a] < val[b]);
            public void Push(double k, int v)
            {
                if (Count == key.Length) { Array.Resize(ref key, Count * 2); Array.Resize(ref val, Count * 2); }
                int i = Count++; key[i] = k; val[i] = v;
                while (i > 0) { int pr = (i - 1) >> 1; if (!Less(i, pr)) break; Swap(i, pr); i = pr; }
            }
            public int Pop(out double k)
            {
                k = key[0]; int v = val[0]; Count--;
                if (Count > 0)
                {
                    key[0] = key[Count]; val[0] = val[Count]; int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1, r = l + 1, m = i;
                        if (l < Count && Less(l, m)) m = l; if (r < Count && Less(r, m)) m = r;
                        if (m == i) break; Swap(i, m); i = m;
                    }
                }
                return v;
            }
            void Swap(int a, int b) { (key[a], key[b]) = (key[b], key[a]); (val[a], val[b]) = (val[b], val[a]); }
        }
    }
}
