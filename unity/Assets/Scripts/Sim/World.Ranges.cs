// Mountain ranges (Unity-only; the browser prototype keeps its small, rare mountains, reproduced by TerrainStyle.Classic).
// Decided 2026-11-16 (Zach): mountains span the future conquest map, so on a city map they are long ranges that enter and leave at the
// map edges, much taller (peaks up to level 16, was 8), with foothills, always at least one walkable pass, and how much of the map they
// cover depends on the region (Relief): a lowland city sees a single low ridge, an alpine one sits in a valley between big ranges.
//
//   Ranges    straight-ish ridgelines through the map (a random direction, offset from the town so a valley stays open around it),
//             extended past the edges and bent by low-frequency noise. Height at a vertex comes from its distance to the ridge:
//             full crest within the core, then a steep flank down to level 4 (the mountain edge), then gentler foothills (hills,
//             where mines can drive adits) down to the rolling ground.
//   Passes    saddles where the crest dips to PassLevel over a short stretch, spaced along each range's in-map length (at least one),
//             wide enough to walk through, so the map is never cut in two.
//   Massifs   for upland and higher relief, a few big lone mountains away from the ranges and the town.
//   Classic   TerrainStyle.Classic keeps the browser's own generator (World.cs GenHeights) bit for bit; the parity tests use it.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>Which terrain generator a world uses.</summary>
    public enum TerrainStyle { Ranges, Classic }

    /// <summary>How mountainous a city's region is (the conquest map will set this; new games use Upland).</summary>
    public enum Relief { Lowland, Upland, Highland, Alpine }

    public partial class World
    {
        public const double PassLevel = 2.2, PassHalfWidth = 9;
        public const int TownValley = 24;   // no ridge core within this many tiles of the Town Hall

        class Ridge { public readonly List<double[]> Pts = new List<double[]>(); public double[] Passes = Array.Empty<double>(); public double Peak, Core, Ph; }
        class Massif { public double Cx, Cy, R, Peak; }

        struct ReliefSpec
        {
            public int Ranges, Massifs; public double Peak, Core, MassifPeak, MassifR;
            public ReliefSpec(int ranges, double peak, double core, int massifs, double massifPeak, double massifR)
            { Ranges = ranges; Peak = peak; Core = core; Massifs = massifs; MassifPeak = massifPeak; MassifR = massifR; }
        }

        static ReliefSpec SpecOf(Relief r) => r switch
        {
            Relief.Lowland => new ReliefSpec(1, 9.5, 0.8, 0, 0, 0),
            Relief.Upland => new ReliefSpec(1, 14, 2.2, 1, 11, 3.5),
            Relief.Highland => new ReliefSpec(2, 17.5, 0.8, 1, 14, 4.0),
            _ => new ReliefSpec(3, 19, 3.0, 2, 16, 5.5),
        };

        static (List<Ridge>, List<Massif>) RangeLayout(int seed, Relief relief)
        {
            var spec = SpecOf(relief);
            int scale = Math.Max(1, N / 128);   // a range is as long as the map is wide and massifs fill area, so a bigger map gets proportionally more of them
            int n = 0;
            double Rnd() => Noise.Hash2(n++, 41, seed + 1300);
            var ridges = new List<Ridge>(); var massifs = new List<Massif>();
            double Clear(double x, double y)   // distance to the nearest existing ridge point
            {
                double best = 1e9;
                foreach (var r in ridges) foreach (var p in r.Pts) best = Math.Min(best, Noise.Hypot(p[0] - x, p[1] - y));
                return best;
            }
            for (int k = 0; k < spec.Ranges * scale; k++)
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    double ang = Rnd() * Math.PI, dx = Math.Cos(ang), dy = Math.Sin(ang), nx = -dy, ny = dx;
                    double off = (TownValley + spec.Core + 4 + Rnd() * (N * 0.36 - TownValley)) * (Rnd() < 0.5 ? -1 : 1);
                    double bendPh = Rnd() * 100, bendAmp = 4 + Rnd() * 7;
                    var r = new Ridge { Peak = spec.Peak * (0.88 + Rnd() * 0.12), Core = spec.Core, Ph = Rnd() * 90 };
                    bool ok = true; double inside = 0;
                    for (double t = -N; t <= N; t += 3)
                    {
                        double bend = (Noise.VNoise(t * 0.025 + bendPh, 5.5, seed + 1301) - 0.5) * 2 * bendAmp;
                        double x = Start + nx * (off + bend) + dx * t, y = Start + ny * (off + bend) + dy * t;
                        if (x < -14 || y < -14 || x > N + 14 || y > N + 14) continue;
                        if (Math.Max(Math.Abs(x - Start), Math.Abs(y - Start)) < TownValley + spec.Core) { ok = false; break; }
                        if (Clear(x, y) < 22 + spec.Core * 2) { ok = false; break; }
                        r.Pts.Add(new[] { x, y });
                        if (x >= 0 && y >= 0 && x <= N && y <= N) inside += 3;
                    }
                    if (!ok || inside < N * 0.5) continue;
                    // passes spaced along the in-map part of the ridge (at least one), never right at a map edge
                    int passes = Math.Max(1, (int)Math.Round(inside / 55));
                    var ps = new double[passes]; double s0 = 0, acc = 0;
                    var inMap = new List<double>();
                    for (int i = 0; i < r.Pts.Count; i++)
                    {
                        if (i > 0) acc += Noise.Hypot(r.Pts[i][0] - r.Pts[i - 1][0], r.Pts[i][1] - r.Pts[i - 1][1]);
                        double x = r.Pts[i][0], y = r.Pts[i][1];
                        if (x >= 8 && y >= 8 && x <= N - 8 && y <= N - 8) inMap.Add(acc);
                    }
                    if (inMap.Count == 0) continue;
                    s0 = inMap[0]; double s1 = inMap[inMap.Count - 1];
                    for (int p = 0; p < passes; p++) ps[p] = s0 + (s1 - s0) * (p + 0.5 + (Rnd() - 0.5) * 0.4) / passes;
                    r.Passes = ps;
                    ridges.Add(r);
                    break;
                }
            for (int k = 0; k < spec.Massifs * scale * scale; k++)
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    double cx = 10 + Rnd() * (N - 20), cy = 10 + Rnd() * (N - 20), rad = spec.MassifR * (0.8 + Rnd() * 0.4);
                    if (Math.Max(Math.Abs(cx - Start), Math.Abs(cy - Start)) < TownValley + rad) continue;
                    if (Clear(cx, cy) < 20 + rad) continue;
                    bool near = false; foreach (var m in massifs) if (Noise.Hypot(m.Cx - cx, m.Cy - cy) < 24) near = true;
                    if (near) continue;
                    massifs.Add(new Massif { Cx = cx, Cy = cy, R = rad, Peak = spec.MassifPeak * (0.85 + Rnd() * 0.15) });
                    break;
                }
            return (ridges, massifs);
        }

        /// <summary>A cross-section: full height within the core, a steep flank down to the mountain edge (level 4), gentle foothills below.</summary>
        static double Profile(double top, double d, double core)
        {
            if (d <= core) return top;
            const double Steep = 1.6, Gentle = 0.65;
            double edge = MountLevel;
            if (top <= edge) return top - (d - core) * Gentle;
            double dEdge = core + (top - edge) / Steep;
            return d <= dEdge ? top - (d - core) * Steep : edge - (d - dEdge) * Gentle;
        }

        static double RangeField((List<Ridge> ridges, List<Massif> massifs) L, int seed, int a, int b)
        {
            double v = 0;
            foreach (var r in L.ridges)
            {
                double best = 1e9, bs = 0, acc = 0;
                for (int i = 0; i + 1 < r.Pts.Count; i++)
                {
                    double x0 = r.Pts[i][0], y0 = r.Pts[i][1], dx = r.Pts[i + 1][0] - x0, dy = r.Pts[i + 1][1] - y0, l2 = dx * dx + dy * dy, len = Math.Sqrt(l2);
                    double t = Math.Max(0, Math.Min(1, ((a - x0) * dx + (b - y0) * dy) / l2)), d = Noise.Hypot(a - (x0 + dx * t), b - (y0 + dy * t));
                    if (d < best) { best = d; bs = acc + len * t; }
                    acc += len;
                }
                if (best > r.Core + 20) continue;
                // the crest rises and falls into separate summits, and dips to a saddle at each pass
                double crest = r.Peak * (0.72 + 0.28 * Noise.VNoise(bs * 0.09 + r.Ph, 1.7, seed + 1310));
                foreach (double p in r.Passes)
                {
                    double u = Math.Abs(bs - p) / PassHalfWidth;
                    if (u < 1) crest = Math.Min(crest, PassLevel + (crest - PassLevel) * u * u * u);   // flat-bottomed saddle
                }
                double dd = best + (Noise.VNoise(a * 0.3 + r.Ph, b * 0.3, seed + 1311) - 0.5) * 2.4;   // ragged flanks
                v = Math.Max(v, Profile(crest, dd, r.Core));
            }
            foreach (var m in L.massifs)
            {
                double d = Noise.Hypot(a - m.Cx, b - m.Cy) + (Noise.VNoise(a * 0.35, b * 0.35, seed + 1320) - 0.5) * 2.2;
                v = Math.Max(v, Profile(m.Peak, d, m.R * 0.35));
            }
            return v;
        }

        /// <summary>Vertex heights for the Ranges style: the browser's rolling hills and basins, with the ranges and massifs on top.</summary>
        public static int[] GenRangeHeights(int seed, Relief relief)
        {
            int M = N + 1; var hv = new int[M * M]; var L = RangeLayout(seed, relief);
            for (int b = 0; b < M; b++)
                for (int a = 0; a < M; a++)
                {
                    double f = Noise.VNoise(a * 0.05 + 30, b * 0.05 + 30, seed + 400) * 0.7 + Noise.VNoise(a * 0.12, b * 0.12, seed + 401) * 0.3;
                    double v = Math.Max(0, Math.Min(3.45, (f - 0.575) * 15));
                    double g = Noise.VNoise(a * 0.045 + 77, b * 0.045 + 77, seed + 500);
                    if (v < 0.5 && g < 0.27) v = -Math.Min(2.4, (0.27 - g) * 14);
                    v = Math.Max(v, RangeField(L, seed, a, b));
                    int dist = Math.Max(Math.Abs(a - Start), Math.Abs(b - Start));
                    v *= Math.Min(1, Math.Max(0, (dist - 7) / 7.0));
                    hv[b * M + a] = Math.Max(HMin, Math.Min(HMax, Noise.JsRound(v)));
                }
            RelaxHeights(hv, null, null, null);
            return hv;
        }

        /// <summary>Share of the map that is mountain (solid rock, level 4+).</summary>
        public double MountainShare()
        {
            int m = 0; for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (IsMountain(x, y)) m++;
            return m / (double)(N * N);
        }

        /// <summary>Walkable tiles reachable on foot from the Town Hall (WalkCost), and all walkable tiles. Used by tests and tuning.</summary>
        public (int reach, int walkable, bool[] seen) WalkReach()
        {
            var seen = new bool[N * N]; var q = new Queue<int>(); int reach = 0, walkable = 0;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (!IsSolid(x, y)) walkable++;
            int s = Tk(Start, Start + 2); seen[s] = true; q.Enqueue(s);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % N, cy = c / N; reach++;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1]; if (!Inb(nx, ny) || seen[Tk(nx, ny)]) continue;
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny))) continue;
                    seen[Tk(nx, ny)] = true; q.Enqueue(Tk(nx, ny));
                }
            }
            return (reach, walkable, seen);
        }
    }
}
