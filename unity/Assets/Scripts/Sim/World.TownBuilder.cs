// An NPC settlement under attack is a real town, built on its own terrain by the same rules as yours: a Town Hall, streets joined to it,
// homes enough for its people, barracks for its soldiers (16 to a barracks), wells, markets, silos and storehouses, the workshops and
// works for every good it produces, farms with their fields, and for a town (or a wary village) a palisade with watchtowers. Its people
// are the game's own: its villagers walk the streets and its crews go out to work (World.AmbientDay), drawn by the usual renderers. The
// battle is fought on it: the buildings block the way, its soldiers muster at their barracks, the lord is at the hall and its stores can
// be plundered.
//
// No two are alike. The rules:
//   - Size: the map grows with its people (TownMapSize), from 32 x 32 for a hamlet to 256 x 256 for the greatest capitals.
//   - Streets (StreetPlan), chosen from its size and kind with a roll of its own seed: a hamlet or farming village strings along one road
//     (Row); a working village grows lanes off its main street (Lanes); a market town or city lays out a grid (Grid); a fortress or a
//     capital rings its hall with roads and spokes (Radial). Which way it faces, how long its streets run and where the lanes branch all
//     come from its seed.
//   - What it lives by (Settlement.Kind and Produce): every good it produces gets its works, more for its main goods: farms and fields (some
//     sown with flax), woodcutter lodges in the woods with their trees marked, foresters, sawmills and charcoal clamps, quarries on rock,
//     mines by the hills, smitheries, sheep pens. A market town has more markets and storehouses and a square round its hall; a fortress
//     has more watchtowers on its walls and its barracks by them; a timber town stands in thick woods.
//   - Inside and outside: homes, barracks, markets, stores and workshops stand in the town (inside the palisade if it has one); farms, pens,
//     lodges, quarries and mines stand outside along the roads out, which lead to the map's edges (country lanes branch off them).
//   - The palisade follows the built-up town (a margin round its streets); the roads out pass through it as gates.
//
// A big city or capital may have more people than its map can house; the town is built as far as it fits.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>How a settlement's streets are laid out (World.TownBuilder.cs).</summary>
    public enum StreetPlan : byte { Row, Lanes, Grid, Radial }

    public partial class World
    {
        /// <summary>The side of a settlement's battle map, in tiles: 32 for a hamlet, about 56 for a town of 200, about 96 for a city of 1000,
        /// up to 256 for a capital of 11000 or more. Rounded to 8.</summary>
        public static int TownMapSize(Settlement s) => Math.Clamp((int)Math.Round((24 + 2.2 * Math.Sqrt(Math.Max(0, s.Population))) / 8) * 8, 32, N);

        /// <summary>Which street plan a settlement has (its size, its kind and its own seed).</summary>
        public static StreetPlan StreetsOf(Settlement s)
        {
            double roll = Noise.Hash2(s.Id, 3, s.Seed + 4411);
            var t = s.Tier;
            if (t == SettlementTier.Hamlet) return roll < 0.75 ? StreetPlan.Row : StreetPlan.Lanes;
            if (t == SettlementTier.Village)
                return s.Kind is TownKind.Farming or TownKind.Pastoral ? (roll < 0.55 ? StreetPlan.Row : StreetPlan.Lanes) : (roll < 0.2 ? StreetPlan.Row : StreetPlan.Lanes);
            if (s.Kind == TownKind.Fortress) return roll < 0.65 ? StreetPlan.Radial : StreetPlan.Grid;
            if (s.Kind == TownKind.Market) return roll < 0.8 ? StreetPlan.Grid : StreetPlan.Radial;
            if (t == SettlementTier.Capital) return roll < 0.55 ? StreetPlan.Radial : StreetPlan.Grid;
            if (t == SettlementTier.City) return roll < 0.25 ? StreetPlan.Radial : StreetPlan.Grid;
            return roll < 0.5 ? StreetPlan.Lanes : StreetPlan.Grid;   // a working town: grown lanes or a laid-out grid
        }

        /// <summary>Whether it has a palisade: every town and bigger, and a wary or warlike village now and then.</summary>
        public static bool Walled(Settlement s) => s.Tier >= SettlementTier.Town ||
            (s.Tier == SettlementTier.Village && s.Personality is Personality.Cautious or Personality.Proud or Personality.Warlike && Noise.Hash2(s.Id, 4, s.Seed + 4411) < 0.5);

        /// <summary>What was built, for the battle map, the HUD and the tests.</summary>
        public sealed class TownPlan
        {
            public int People, Soldiers, Homes, Farms, Fields, FlaxFields, Barracks, Towers, WallTiles, Pens;
            public TownKind Kind;
            public StreetPlan Streets;
            /// <summary>The palisade's corners in world tiles (when it has one).</summary>
            public (int x0, int y0, int x1, int y1)? Walls;
            public readonly List<string> Built = new List<string>();
            public int Count(string type) => Built.Count(t => t == type);
        }

        /// <summary>The plan of the town this world holds (null for a raider camp or your settlement).</summary>
        public TownPlan TownLayout { get; private set; }

        public static CampSite CreateTown(RaiderCamp camp)
        {
            var s = camp.Town; int n = TownMapSize(s);
            var w = MakeBattleGround(camp.Seed, camp.Relief, n, Math.Min(n / 3, 40), out int x0, out int y0);
            w.TownLayout = w.BuildTown(s, x0, y0, n);
            var map = w.TownMap(s, x0, y0, n);
            return new CampSite { World = w, Map = map, X0 = x0, Y0 = y0 };
        }

        // ---------- building it ----------

        bool InSquare(int x, int y, int x0, int y0, int n) => x >= x0 && y >= y0 && x < x0 + n && y < y0 + n;

        /// <summary>A footprint that is free to build on: open, flat enough, inside the square away from its landing edge.</summary>
        bool LotFree(int x, int y, int sz, int x0, int y0, int n)
        {
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sz; i++)
                {
                    int tx = x + i, ty = y + j;
                    if (!InSquare(tx, ty, x0 + 3, y0 + 3, n - 6) || Grid[ty, tx] != null || ZoneMap[ty, tx] != Zone.None || IsSolid(tx, ty) || TileSpread(tx, ty) > 1) return false;
                }
            return true;
        }

        Building Raise(TownPlan plan, string type, (int x, int y)? at, int sz = 0)
        {
            if (!at.HasValue) return null;
            var (x, y) = at.Value;
            int size = sz != 0 ? sz : PlacementSize(type);
            for (int j = 0; j < size; j++) for (int i = 0; i < size; i++) DecoMap[y + j, x + i] = Deco.None;
            var b = AddBld(type, x, y, size);
            plan.Built.Add(type);
            return b;
        }

        bool LayRoad(int x, int y, int x0, int y0, int n)
        {
            if (!InSquare(x, y, x0 + 2, y0 + 2, n - 4)) return false;
            if (Grid[y, x]?.T == "road") return true;
            if (Grid[y, x] != null || IsSolid(x, y) || IsMountain(x, y)) return false;
            DecoMap[y, x] = Deco.None; RockAmtMap.Remove(Tk(x, y));
            AddBld("road", x, y);
            return true;
        }

        /// <summary>Lots beside the town's roads, nearest the hall first, for quick finding (a city has thousands).</summary>
        sealed class LotList
        {
            public readonly List<(int x, int y, int d)> List = new List<(int x, int y, int d)>();
            public int Head;
        }

        /// <summary>The town as it is being built: its square, its middle, its lots and what counts as inside it.</summary>
        sealed class Site
        {
            public int X0, Y0, N, Cx, Cy;
            public readonly LotList[] Lots = { null, new LotList(), new LotList() };
            /// <summary>The built-up town (its streets and the hall), and the palisade if it has one.</summary>
            public int Bx0, By0, Bx1, By1;
            public (int x0, int y0, int x1, int y1)? Wall;
            /// <summary>A footprint inside the town (within its palisade, or near its streets when it has none).</summary>
            public bool Inside(int x, int y, int sz)
            {
                if (Wall is var (a, b, c, d)) return x > a && y > b && x + sz - 1 < c && y + sz - 1 < d;
                return x >= Bx0 - 3 && y >= By0 - 3 && x + sz - 1 <= Bx1 + 3 && y + sz - 1 <= By1 + 3;
            }
            /// <summary>A footprint clear of the town (outside its palisade with a tile to spare, or away from its streets).</summary>
            public bool Outside(int x, int y, int sz)
            {
                int a, b, c, d;
                if (Wall is var (wa, wb, wc, wd)) { a = wa - 1; b = wb - 1; c = wc + 1; d = wd + 1; }
                else { a = Bx0 - 4; b = By0 - 4; c = Bx1 + 4; d = By1 + 4; }
                return x + sz - 1 < a || y + sz - 1 < b || x > c || y > d;
            }
            /// <summary>How far a tile is beyond the town (its palisade, or its built-up area).</summary>
            public int Beyond(int x, int y)
            {
                var (a, b, c, d) = Wall ?? (Bx0 - 3, By0 - 3, Bx1 + 3, By1 + 3);
                return Math.Max(Math.Max(a - x, x - c), Math.Max(b - y, y - d));
            }
        }

        void ListLots(Site site)
        {
            var seen = new HashSet<long>();
            foreach (var b in Blds)
            {
                if (b.T != "road" || !b.Served) continue;
                for (int sz = 1; sz <= 2; sz++)
                    for (int k = 0; k < sz; k++)
                        foreach (var (x, y) in new[] { (b.X - k, b.Y - sz), (b.X - k, b.Y + 1), (b.X - sz, b.Y - k), (b.X + 1, b.Y - k) })
                        {
                            if (!seen.Add(((long)sz * N + y + 2) * (N + 4) + x + 2)) continue;
                            if (!LotFree(x, y, sz, site.X0, site.Y0, site.N)) continue;
                            int d = Math.Max(Math.Abs(x + sz / 2 - site.Cx), Math.Abs(y + sz / 2 - site.Cy));
                            site.Lots[sz].List.Add((x, y, d));
                        }
            }
            foreach (var l in site.Lots.Skip(1)) l.List.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        }

        /// <summary>A free lot beside the town's roads: the nearest to the hall (or to `near`), or with far the farthest, that passes `also`.</summary>
        (int x, int y)? FindLot(Site site, int sz, Func<int, int, bool> also = null, bool far = false, (int x, int y)? near = null)
        {
            var lots = site.Lots[sz];
            // lots only ever get taken: skip the taken ones at the front for good
            while (lots.Head < lots.List.Count && !LotFree(lots.List[lots.Head].x, lots.List[lots.Head].y, sz, site.X0, site.Y0, site.N)) lots.Head++;
            if (!far && near == null)
            {
                for (int i = lots.Head; i < lots.List.Count; i++)
                {
                    var (x, y, _) = lots.List[i];
                    if ((also == null || also(x, y)) && LotFree(x, y, sz, site.X0, site.Y0, site.N)) return (x, y);
                }
                return null;
            }
            (int x, int y)? best = null; int bestD = 0;
            for (int i = lots.Head; i < lots.List.Count; i++)
            {
                var (x, y, d) = lots.List[i];
                if (near is var (nx, ny)) d = Math.Max(Math.Abs(x - nx), Math.Abs(y - ny));
                if (best != null && (far ? d <= bestD : d >= bestD)) continue;
                if ((also == null || also(x, y)) && LotFree(x, y, sz, site.X0, site.Y0, site.N)) { best = (x, y); bestD = d; }
            }
            return best;
        }

        // ---------- streets ----------

        /// <summary>The streets of a plan, in the town's own frame (u along its main street, v across it; the hall at 0,0, its main street
        /// at v = 2 just past the hall). R is how far it reaches along u, Rv across.</summary>
        static List<(int u, int v)> PlanStreets(StreetPlan plan, int R, int Rv, Func<int, int, double> rnd, bool square)
        {
            var t = new List<(int u, int v)>();
            void Line(int u0, int v0, int u1, int v1)   // a 4-connected line (a staircase on a slant)
            {
                int u = u0, v = v0;
                t.Add((u, v));
                while (u != u1 || v != v1)
                {
                    // step along whichever axis keeps closest to the straight line
                    double ru = u1 - u0, rv = v1 - v0;
                    int su = Math.Sign(u1 - u), sv = Math.Sign(v1 - v);
                    double offU = su == 0 ? double.MaxValue : Math.Abs((u + su - u0) * rv - (v - v0) * ru);
                    double offV = sv == 0 ? double.MaxValue : Math.Abs((u - u0) * rv - (v + sv - v0) * ru);
                    if (offU <= offV) u += su; else v += sv;
                    t.Add((u, v));
                }
            }
            if (square) { Line(-2, -2, 2, -2); Line(2, -2, 2, 2); Line(2, 2, -2, 2); Line(-2, 2, -2, -2); }   // a square round the hall
            switch (plan)
            {
                case StreetPlan.Row:
                {
                    Line(-R, 2, R, 2);
                    // a crossing street or two, off the middle
                    int crossings = R >= 9 ? 1 + (rnd(1, 1) < 0.4 && R >= 14 ? 1 : 0) : (rnd(1, 2) < 0.5 ? 1 : 0);
                    int side = rnd(1, 3) < 0.5 ? -1 : 1;
                    for (int c = 0; c < crossings; c++, side = -side)
                    {
                        int u = side * (4 + (int)(rnd(1, 4 + c) * Math.Max(1, R / 2 - 3)));
                        int up = (int)Math.Round(Rv * (0.4 + 0.6 * rnd(1, 6 + c))), down = (int)Math.Round(Rv * (0.4 + 0.6 * rnd(1, 8 + c)));
                        Line(u, 2 - up, u, 2 + down);
                    }
                    break;
                }
                case StreetPlan.Lanes:
                {
                    Line(-R, 2, R, 2);
                    // lanes branch off the main street at uneven spacing, some one way, some both, of uneven length
                    int k = 0;
                    for (int u = -R + (int)(rnd(2, 0) * 3); u <= R; u += 4 + (int)(rnd(2, ++k) * 4))
                    {
                        if (Math.Abs(u) <= 2) continue;
                        double dir = rnd(3, k);
                        if (dir < 0.15) continue;
                        int len = Math.Max(3, (int)Math.Round(Rv * (0.35 + 0.65 * rnd(4, k))));
                        if (dir < 0.5 || dir >= 0.8) Line(u, 2, u, 2 + len);
                        if (dir >= 0.5) Line(u, 2, u, 2 - Math.Max(3, (int)Math.Round(len * (0.5 + 0.5 * rnd(5, k)))));
                        // now and then a back lane runs on from a lane's end
                        if ((dir < 0.5 || dir >= 0.8) && rnd(6, k) < 0.3 && u + 6 <= R) Line(u, 2 + len, u + 4 + (int)(rnd(7, k) * 3), 2 + len);
                    }
                    break;
                }
                case StreetPlan.Grid:
                {
                    int S = rnd(8, 0) < 0.35 ? 6 : 5;
                    int ku = Math.Max(1, R / S), kv = Math.Max(1, Rv / S);
                    for (int j = -kv; j <= kv; j++)
                        for (int i = -ku; i < ku; i++)
                            if (j == 0 || Math.Abs(j) == kv || rnd(9, (j + 50) * 200 + i + 100) > 0.12) Line(i * S, 2 + j * S, (i + 1) * S, 2 + j * S);   // a few blocks run together
                    for (int i = -ku; i <= ku; i++)
                        for (int j = -kv; j < kv; j++)
                            if (Math.Abs(i) == ku || rnd(10, (i + 50) * 200 + j + 100) > 0.12) Line(i * S, 2 + j * S, i * S, 2 + (j + 1) * S);
                    break;
                }
                default:   // Radial: spokes from a square round the hall, and rings round it
                {
                    Line(-2, -2, 2, -2); Line(2, -2, 2, 2); Line(2, 2, -2, 2); Line(-2, 2, -2, -2);
                    Line(2, 0, R, 0); Line(-2, 0, -R, 0); Line(0, 2, 0, R); Line(0, -2, 0, -R);
                    int d = (int)Math.Round(R * 0.72);
                    foreach (var (su, sv, k) in new[] { (1, 1, 0), (1, -1, 1), (-1, 1, 2), (-1, -1, 3) })
                        if (rnd(11, k) < 0.75) Line(2 * su, 2 * sv, d * su, d * sv);
                    int gap = 6 + (int)(rnd(12, 0) * 2);
                    for (int r = gap; r <= R; r += gap)
                    {
                        int steps = Math.Max(16, r * 8);
                        int pu = r, pv = 0;
                        for (int a = 1; a <= steps; a++)
                        {
                            double ang = 2 * Math.PI * a / steps;
                            int nu = (int)Math.Round(r * Math.Cos(ang)), nv = (int)Math.Round(r * Math.Sin(ang));
                            if (nu != pu || nv != pv) { Line(pu, pv, nu, nv); pu = nu; pv = nv; }
                        }
                    }
                    break;
                }
            }
            return t;
        }

        TownPlan BuildTown(Settlement s, int x0, int y0, int n)
        {
            var plan = new TownPlan { Kind = s.Kind, Streets = StreetsOf(s) };
            var tier = s.Tier;
            double Rnd(int a, int b) => Noise.Hash2(a, b, s.Seed + 4423);

            // the hall near the middle (on the nearest ground that takes a 3 x 3)
            int mx = x0 + n / 2, my = y0 + n / 2;
            (int x, int y) hall = (mx - 1, my - 1);
            for (int r = 0; r < Math.Max(8, n / 6); r++)
            {
                bool ok = false;
                for (int dy = -r; dy <= r && !ok; dy++) for (int dx = -r; dx <= r && !ok; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && LotFree(mx - 1 + dx, my - 1 + dy, 3, x0, y0, n)) { hall = (mx - 1 + dx, my - 1 + dy); ok = true; }
                if (ok) break;
            }
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++) DecoMap[hall.y + j, hall.x + i] = Deco.None;
            AddBld("hall", hall.x, hall.y, 3);
            int cx = hall.x + 1, cy = hall.y + 1;
            var site = new Site { X0 = x0, Y0 = y0, N = n, Cx = cx, Cy = cy };

            // how many: soldiers in barracks of 16, everyone else in homes
            int soldiers = Garrison(s);
            int people = (int)Math.Round(s.Population);
            int civilians = Math.Max(2, people - soldiers);
            bool bigHomes = tier >= SettlementTier.Town;
            int homeCap = bigHomes ? HouseCap2[2] : HouseCap[2];
            int homesWanted = (civilians + homeCap - 1) / homeCap;
            int barracksWanted = (soldiers + Bunks - 1) / Bunks;
            int workshops = 3 + (tier >= SettlementTier.Village ? 4 : 0) + (tier >= SettlementTier.Town ? 4 : 0) + people / 150;
            int area = homesWanted * (bigHomes ? 4 : 1) + barracksWanted * 4 + workshops * 4;

            // which way it faces (u along the main street): four ways
            int rot = (int)(Rnd(1, 0) * 4);
            (int x, int y) P(int u, int v) => rot switch { 0 => (cx + u, cy + v), 1 => (cx + v, cy + u), 2 => (cx - u, cy - v), _ => (cx - v, cy - u) };
            bool walled = Walled(s);
            int rMax = n / 2 - (walled ? 10 : 7);
            double aspect = plan.Streets switch
            {
                StreetPlan.Row => 0.45 + 0.2 * Rnd(2, 0), StreetPlan.Lanes => 0.45 + 0.3 * Rnd(2, 0), StreetPlan.Grid => 0.6 + 0.4 * Rnd(2, 0), _ => 1,
            };
            bool square = s.Kind is TownKind.Market or TownKind.Fortress || tier >= SettlementTier.City;
            double yieldPerTile = plan.Streets == StreetPlan.Radial ? 2.4 : 2.8;   // building tiles each street tile serves (both sides)
            List<(int x, int y)> core = null;
            for (int R = 5; ; R++)
            {
                int Rv = Math.Max(3, Math.Min(rMax, (int)Math.Round(R * aspect)));
                var uv = PlanStreets(plan.Streets, R, Rv, Rnd, square);
                core = uv.Select(t => P(t.u, t.v)).Distinct()
                         .Where(t => InSquare(t.x, t.y, x0 + 6, y0 + 6, n - 12) && !IsSolid(t.x, t.y) && Grid[t.y, t.x]?.T != "hall").ToList();
                if (core.Count * yieldPerTile >= area * 1.15 || R >= rMax) break;
            }
            foreach (var (x, y) in core.OrderBy(t => t.y).ThenBy(t => t.x)) LayRoad(x, y, x0, y0, n);
            var laid = core.Where(t => Grid[t.y, t.x]?.T == "road").ToList();
            if (laid.Count == 0) { LayRoad(cx, cy + 2, x0, y0, n); laid.Add((cx, cy + 2)); }
            site.Bx0 = Math.Min(hall.x, laid.Min(t => t.x)); site.By0 = Math.Min(hall.y, laid.Min(t => t.y));
            site.Bx1 = Math.Max(hall.x + 2, laid.Max(t => t.x)); site.By1 = Math.Max(hall.y + 2, laid.Max(t => t.y));

            // the palisade's line: a ring round the built-up town with room for the lots along its outer streets
            if (walled)
            {
                const int m = 4;
                site.Wall = (Math.Max(x0 + 4, site.Bx0 - m), Math.Max(y0 + 4, site.By0 - m), Math.Min(x0 + n - 5, site.Bx1 + m), Math.Min(y0 + n - 5, site.By1 + m));
                plan.Walls = site.Wall;
            }

            // roads out to the map's edges: along the main street both ways, and across it (more for a bigger town)
            var exits = new List<(int u, int v, int du, int dv)>();
            if (plan.Streets == StreetPlan.Radial) exits.AddRange(new[] { (0, 0, 1, 0), (0, 0, -1, 0), (0, 0, 0, 1), (0, 0, 0, -1) });
            else
            {
                exits.Add((0, 2, 1, 0)); exits.Add((0, 2, -1, 0));
                // across: along the street nearest the hall that crosses the main street
                var onMain = new HashSet<(int, int)>(laid);
                var crossing = laid.Select(t => Unrot(t.x - cx, t.y - cy, rot)).Where(t => Math.Abs(t.u) >= 3 && t.v != 2 && onMain.Contains(P(t.u, 2)))
                                   .Select(t => t.u).Distinct().OrderBy(u => Math.Abs(u)).ThenBy(u => u).ToList();
                if (crossing.Count > 0)
                {
                    exits.Add((crossing[0], 2, 0, 1));
                    if (tier >= SettlementTier.Town || Rnd(5, 0) < 0.4) exits.Add((crossing[0], 2, 0, -1));
                }
            }
            var outRoads = new List<List<(int x, int y)>>();
            foreach (var (u, v, du, dv) in exits)
            {
                var road = new List<(int x, int y)>();
                for (int k = 1; k < n; k++)
                {
                    var (x, y) = P(u + du * k, v + dv * k);
                    if (!InSquare(x, y, x0 + 2, y0 + 2, n - 4)) break;
                    if (Grid[y, x]?.T == "road") continue;
                    if (Grid[y, x] != null) { if (k < 4) continue; break; }   // the hall: the road starts past it
                    if (!LayRoad(x, y, x0, y0, n)) break;
                    road.Add((x, y));
                }
                outRoads.Add(road);
            }

            // country lanes off the roads out, beyond the town: farms and pens are reached by them
            double laneChance = s.Kind is TownKind.Farming or TownKind.Pastoral ? 0.75 : 0.4;
            int laneNo = 0;
            foreach (var road in outRoads)
                for (int i = 0; i + 1 < road.Count; i++)
                {
                    var (x, y) = road[i];
                    if (site.Beyond(x, y) < 6 || i % 11 != 5) continue;
                    laneNo++;
                    if (Rnd(20, laneNo) > laneChance) continue;
                    bool acrossX = road[i + 1].y != y;   // the road runs along y, so the lane runs along x
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Rnd(21, laneNo * 2 + side) < 0.25) continue;
                        int len = 5 + (int)(Rnd(22, laneNo * 2 + side) * 9);
                        for (int k = 1; k <= len; k++)
                        {
                            int lx = acrossX ? x + side * k : x, ly = acrossX ? y : y + side * k;
                            if (site.Beyond(lx, ly) < 3 || Grid[ly, lx]?.T == "road" || !LayRoad(lx, ly, x0, y0, n)) break;
                        }
                    }
                }

            // the town has cleared its own streets: no woods or loose boulders between the houses (mountains stay)
            var (bx0, by0, bx1, by1) = site.Wall ?? (site.Bx0 - 3, site.By0 - 3, site.Bx1 + 3, site.By1 + 3);
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                    if (Inb(x, y) && Grid[y, x] == null && !IsMountain(x, y) && DecoMap[y, x] is Deco.Tree or Deco.Sapling or Deco.Stump or Deco.Rock)
                    { DecoMap[y, x] = Deco.None; RockAmtMap.Remove(Tk(x, y)); }
            // a timber town stands among woods: the land round it is thick with trees
            if (s.Kind == TownKind.Timber)
                for (int y = y0 + 3; y < y0 + n - 3; y++)
                    for (int x = x0 + 3; x < x0 + n - 3; x++)
                        if (Grid[y, x] == null && DecoMap[y, x] == Deco.None && !IsSolid(x, y) && site.Beyond(x, y) >= 3 && !BesideRoadTile(x, y)
                            && Noise.VNoise(x * 0.12 + 7, y * 0.12 + 7, s.Seed + 4431) > 0.42 && Noise.Hash2(x, y, s.Seed + 4432) > 0.2)
                            DecoMap[y, x] = Deco.Tree;
            Recalc();

            // the palisade, its towers at the corners (and along the walls of a bigger town or a fortress), gaps where the roads go through
            // (gates: a road tile between two wall tiles)
            if (site.Wall is var (wx0, wy0, wx1, wy1))
            {
                int every = s.Kind == TownKind.Fortress ? 6 : tier >= SettlementTier.City ? 10 : tier >= SettlementTier.Town ? 16 : 0;
                bool NearRoad(int x, int y) => BesideRoadTile(x, y) || BesideRoadTile(x - 1, y) || BesideRoadTile(x + 1, y) || BesideRoadTile(x, y - 1) || BesideRoadTile(x, y + 1);
                // the corners are palisade; each corner's tower stands on the tile beside it along the wall (turning the same way round
                // the perimeter), or on the other side if a road or something else is in the way
                var cornerTowers = new HashSet<(int, int)>();
                foreach (var (a, b) in new[]
                {
                    ((wx0 + 1, wy0), (wx0, wy0 + 1)), ((wx1, wy0 + 1), (wx1 - 1, wy0)),
                    ((wx1 - 1, wy1), (wx1, wy1 - 1)), ((wx0, wy1 - 1), (wx0 + 1, wy1)),
                })
                {
                    bool Free((int x, int y) t) => Inb(t.x, t.y) && Grid[t.y, t.x] == null && !IsSolid(t.x, t.y) && !NearRoad(t.x, t.y);
                    if (Free(a)) cornerTowers.Add(a); else if (Free(b)) cornerTowers.Add(b);
                }
                for (int y = wy0; y <= wy1; y++)
                    for (int x = wx0; x <= wx1; x++)
                    {
                        if (x != wx0 && x != wx1 && y != wy0 && y != wy1) continue;
                        if (Grid[y, x] != null || IsSolid(x, y)) continue;
                        bool corner = (x == wx0 || x == wx1) && (y == wy0 || y == wy1);
                        int along = x == wx0 || x == wx1 ? y - wy0 : x - wx0;
                        bool nearRoad = NearRoad(x, y);
                        bool tower = !corner && (cornerTowers.Contains((x, y)) || (every > 0 && along % every == every / 2 && !nearRoad));
                        DecoMap[y, x] = Deco.None;
                        if (tower) { AddBld("watchtower", x, y); plan.Towers++; }
                        else { AddBld("wall", x, y); plan.WallTiles++; }
                    }
            }
            ListLots(site);

            bool In1(int x, int y) => site.Inside(x, y, 1);
            bool In2(int x, int y) => site.Inside(x, y, 2);
            bool Out2(int x, int y) => site.Outside(x, y, 2);
            double unit = Math.Max(1.0, people / 120.0);
            int CountFor(Res r, int most) => !s.Produces(r) ? 0 : Math.Clamp((int)Math.Round(unit * s.ShareOf(r) * 1.5), 1, most);

            // in the town: barracks (a fortress keeps them by its walls), markets, stores, workshops, wells, then homes for everyone
            bool fort = s.Kind == TownKind.Fortress;
            for (int i = 0; i < barracksWanted; i++)
                if (Raise(plan, "barracks", FindLot(site, 2, In2, far: fort)) != null) plan.Barracks++;
            int markets = tier < SettlementTier.Village ? 0 : 1 + people / 1500 + (s.Kind == TownKind.Market ? 1 + people / 500 : 0);
            for (int i = 0; i < Math.Min(markets, 8); i++) Raise(plan, "market", FindLot(site, 2, In2));
            int silos = tier < SettlementTier.Village ? 0 : 1 + people / 900 + (s.Produces(Res.Food) && s.ShareOf(Res.Food) > 0.35 ? 1 : 0);
            for (int i = 0; i < Math.Min(silos, 8); i++) Raise(plan, "silo", FindLot(site, 1, In1, far: i > 0));
            int stores = tier < SettlementTier.Town ? 0 : 1 + people / 1500 + (s.Kind == TownKind.Market ? 1 : 0);
            for (int i = 0; i < Math.Min(stores, 6); i++) Raise(plan, "storehouse", FindLot(site, 2, In2));
            int smiths = s.Produces(Res.Metal) ? CountFor(Res.Metal, 6) : tier >= SettlementTier.Town ? 1 : 0;
            for (int i = 0; i < smiths; i++) Raise(plan, "smithery", FindLot(site, 2, In2, far: true));
            int sawmills = tier < SettlementTier.Village ? 0 : 1 + (s.Produces(Res.Wood) ? CountFor(Res.Wood, 6) / 3 : 0);
            for (int i = 0; i < sawmills; i++) Raise(plan, "sawmill", FindLot(site, 2, In2, far: true));
            for (int i = 0; i < CountFor(Res.Charcoal, 5); i++) Raise(plan, "charcoal", FindLot(site, 2, In2, far: true));
            if (tier >= SettlementTier.Village && (s.Kind == TownKind.Pastoral || (s.Produces(Res.Food) && Rnd(30, 0) < 0.4)))
                Raise(plan, "smokehouse", FindLot(site, 2, In2, far: true));
            int wells = Math.Min(Math.Max(1, people / 80), 16);
            for (int i = 0; i < wells; i++)
            {
                double a = i * 2.4 + Rnd(31, 0) * 6;   // spread the wells round the town
                double rr = 0.25 + 0.5 * (i * 0.618 % 1);
                var near = (cx + (int)(Math.Cos(a) * (site.Bx1 - site.Bx0) * rr / 2), cy + (int)(Math.Sin(a) * (site.By1 - site.By0) * rr / 2));
                Raise(plan, "well", FindLot(site, 1, In1, near: near));
            }
            // homes: the bigger kind in a town (a few small cottages among them), the small kind in a village (a few big ones)
            int housed = 0;
            for (int i = 0; housed < civilians; i++)
            {
                bool big = bigHomes ? Rnd(32, i) > 0.15 : Rnd(32, i) > 0.9;
                var b = Raise(plan, "house", FindLot(site, big ? 2 : 1, big ? In2 : In1), big ? 2 : 1);
                if (b == null && big) { big = false; b = Raise(plan, "house", FindLot(site, 1, In1), 1); }
                if (b == null) break;
                b.Lvl = 2; b.Tier = 2; plan.Homes++;
                housed += (big ? HouseCap2 : HouseCap)[2];
            }

            // outside: the woods, the rock and the hills are worked where they are
            bool Near(int x, int y, int r, Func<int, int, bool> what)
            {
                for (int j = -r; j <= r + 1; j++) for (int i = -r; i <= r + 1; i++) if (InSquare(x + i, y + j, x0, y0, n) && what(x + i, y + j)) return true;
                return false;
            }
            void MarkAround(Building b, Func<int, int, bool> what, Zone zone)
            {
                for (int j = -8; j <= 9; j++)
                    for (int i = -8; i <= 9; i++)
                    {
                        int x = b.X + i, y = b.Y + j;
                        if (InSquare(x, y, x0 + 2, y0 + 2, n - 4) && Grid[y, x] == null && ZoneMap[y, x] == Zone.None && what(x, y)) ZoneMap[y, x] = zone;
                    }
            }
            bool Tree(int x, int y) => DecoMap[y, x] == Deco.Tree;
            bool Rock(int x, int y) => DecoMap[y, x] == Deco.Rock;
            int lodges = s.Produces(Res.Wood) || s.Produces(Res.Charcoal) ? Math.Max(1, CountFor(Res.Wood, 8)) : tier >= SettlementTier.Village ? 1 : 0;
            for (int i = 0; i < lodges; i++)
            {
                var lodge = Raise(plan, "wood", FindLot(site, 2, (x, y) => Out2(x, y) && Near(x, y, 4, Tree)));
                if (lodge == null) break;
                MarkAround(lodge, Tree, Zone.Wood);
            }
            if (s.Kind == TownKind.Timber || (s.Produces(Res.Wood) && s.ShareOf(Res.Wood) > 0.4))
                for (int i = 0; i < Math.Max(1, lodges / 3); i++) Raise(plan, "forester", FindLot(site, 2, Out2));
            if (tier <= SettlementTier.Village && s.Produces(Res.Wood) && Rnd(33, 0) < 0.5)
                Raise(plan, "hunter", FindLot(site, 2, (x, y) => Out2(x, y) && Near(x, y, 4, Tree)));
            for (int i = 0; i < CountFor(Res.Stone, 6); i++)
            {
                var q = Raise(plan, "quarry", FindLot(site, 2, (x, y) => Out2(x, y) && Near(x, y, 5, Rock)) ?? FindLot(site, 2, Out2, far: i % 2 == 1));
                if (q == null) break;
                if (!Near(q.X, q.Y, 6, Rock))   // stone the land gives: outcrops break the ground round the quarry
                    for (int k = 0; k < 40; k++)
                    {
                        int x = q.X - 5 + (int)(Rnd(34, i * 100 + k) * 12), y = q.Y - 5 + (int)(Rnd(35, i * 100 + k) * 12);
                        if (!InSquare(x, y, x0 + 3, y0 + 3, n - 6) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None || IsSolid(x, y) || BesideRoadTile(x, y) || !site.Outside(x, y, 1)) continue;
                        DecoMap[y, x] = Deco.Rock; RockAmtMap[Tk(x, y)] = Rnd(36, k) < 0.3 ? 2 : 1;
                    }
                MarkAround(q, Rock, Zone.Mine);
            }
            for (int i = 0; i < CountFor(Res.Ore, 6); i++)
                Raise(plan, "mine", FindLot(site, 2, (x, y) => Out2(x, y) && MineFaceAt(x, y, 2) >= 0 && MineDoorOnRoad(x, y) && SurveyMineSite(x, y, 2).Reserve > 0));

            // sheep on the open pasture (a herding place more), and now and then pigs or hens on a farming place
            int flocks = s.Produces(Res.Wool) ? CountFor(Res.Wool, 10) + (s.Kind == TownKind.Pastoral ? 1 : 0) : 0;
            int smallStock = s.Kind == TownKind.Farming && Rnd(37, 0) < 0.6 ? 1 + (int)(unit / 6) : 0;
            for (int i = 0; i < Math.Min(flocks + smallStock, 14); i++)
            {
                var kind = i < flocks ? Zone.Sheep : Rnd(38, i) < 0.5 ? Zone.Pigs : Zone.Chickens;
                int pw = kind == Zone.Chickens ? 3 : 5 + (int)(Rnd(39, i) * 2), ph = kind == Zone.Chickens ? 3 : 4;
                bool placed = false;
                var lots = site.Lots[2].List;
                for (int k = 0, tries = 0; k < lots.Count && !placed && tries < 800; k++)
                {
                    var (lx, ly, _) = lots[k];
                    if (!Out2(lx, ly)) continue;
                    tries++;
                    foreach (var (ax, ay) in new[] { (lx, ly), (lx - pw + 2, ly), (lx, ly - ph + 2), (lx - pw + 2, ly - ph + 2) })
                    {
                        if (!InSquare(ax, ay, x0 + 3, y0 + 3, n - 6) || !InSquare(ax + pw - 1, ay + ph - 1, x0 + 3, y0 + 3, n - 6) || !site.Outside(ax, ay, Math.Max(pw, ph))) continue;
                        if (!TownPen(kind, ax, ay, pw, ph)) continue;
                        plan.Pens++; plan.Built.Add(kind.ToString().ToLower());
                        placed = true; break;
                    }
                }
            }

            // farms along the roads out, each with its fields round it (one farmer feeds about ten); a place that lives by other goods buys
            // some of its food and farms less; flax is sown where it produces flax
            double foodShare = s.Produces(Res.Food) ? 1 : 0.55;
            int farmers = Math.Max(1, (int)Math.Ceiling(people / 10.0 * foodShare + (s.Produces(Res.Flax) ? people / 20.0 * s.ShareOf(Res.Flax) : 0)));
            int farmsWanted = Math.Clamp((farmers + 9) / 10, 1, 48);
            double flaxPart = !s.Produces(Res.Flax) ? 0
                : Math.Clamp(s.ShareOf(Res.Flax) / (s.ShareOf(Res.Flax) + (s.Produces(Res.Food) ? s.ShareOf(Res.Food) : 0.3)), 0.15, 0.7);
            var farms = new List<Building>();
            for (int i = 0; i < farmsWanted; i++)
            {
                bool Apart(int x, int y) => farms.All(f => Math.Max(Math.Abs(f.X - x), Math.Abs(f.Y - y)) >= 7);
                var spot = FindLot(site, 2, (x, y) => Out2(x, y) && Fert[y, x] && Apart(x, y))
                        ?? FindLot(site, 2, (x, y) => Out2(x, y) && Apart(x, y))
                        ?? FindLot(site, 2, Out2);   // crowded or poor land: farm it anyway
                var farm = Raise(plan, "farm", spot, 2);
                if (farm == null) break;
                farms.Add(farm); plan.Farms++;
                int want = Math.Max(1, Math.Min(farm.Jobs, farmers - i * 10)) * FieldsPerFarmer;
                var spots = new List<(int d, int x, int y)>();
                for (int pass = 0; pass < 2 && spots.Count == 0; pass++)   // fertile ground first; poor ground only if there is none
                    for (int j = -FarmReach; j <= FarmReach + 1; j++)
                        for (int k = -FarmReach; k <= FarmReach + 1; k++)
                        {
                            int x = farm.X + k, y = farm.Y + j;
                            if (!InSquare(x, y, x0 + 3, y0 + 3, n - 6) || !site.Outside(x, y, 1) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None) continue;
                            if ((pass == 0 && !Fert[y, x]) || IsSolid(x, y) || DecoMap[y, x] == Deco.Rock) continue;
                            spots.Add((Math.Max(Math.Abs(k), Math.Abs(j)), x, y));
                        }
                foreach (var (_, x, y) in spots.OrderBy(p => p.d).ThenBy(p => p.y * N + p.x).Take(want))
                {
                    DecoMap[y, x] = Deco.None; ZoneMap[y, x] = Zone.Farm; plan.Fields++;
                    if (flaxPart > 0 && Noise.Hash2(x, y, s.Seed + 4441) < flaxPart) { FlaxFields[Tk(x, y)] = true; plan.FlaxFields++; }
                }
            }

            // levelling a footprint can now and then tip a road too steep and cut a building off: such a building was never built
            Recalc();
            foreach (var b in Blds.Where(b => b.T != "road" && b.T != "wall" && b.T != "watchtower" && b.T != "hall" && !b.Served).ToList())
            {
                Blds.Remove(b);
                foreach (var (x, y) in b.Tiles()) if (Grid[y, x] == b) Grid[y, x] = null;
                plan.Built.Remove(b.T);
                if (b.T == "barracks") plan.Barracks--;
                if (b.T == "farm") plan.Farms--;
            }
            Touch();

            // its people: soldiers in their barracks, the rest at home; enough food in store that the shown town is not starving
            Soldiers = Math.Min(soldiers, plan.Barracks * Bunks);
            Peasants = Math.Max(2, Blds.Where(b => b.T == "house").Sum(b => b.Capacity));
            plan.Soldiers = Soldiers; plan.Homes = Blds.Count(b => b.T == "house"); plan.People = Peasants + Soldiers;
            ResAmt[Res.Food] = CropStorage(Res.Food);
            Touch();
            Recalc();
            return plan;
        }

        /// <summary>A mine here would have its entrance on a served road (its mouth faces away from the hill it digs into).</summary>
        bool MineDoorOnRoad(int x, int y)
        {
            int face = MineFaceAt(x, y, 2);
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    var (ex, ey) = EntranceTile(x + i, y + j, face);
                    if (Inb(ex, ey) && Grid[ey, ex] is Building r && (r == Hall || (r.T == "road" && r.Served))) return true;
                }
            return false;
        }

        /// <summary>A livestock pen the town has had for years: clear ground beside a road, its fence up and full of animals.</summary>
        bool TownPen(Zone kind, int ax, int ay, int pw, int ph)
        {
            var pen = new LivestockPen(kind, ax, ay, ax + pw - 1, ay + ph - 1);
            foreach (var (x, y) in pen.Tiles())
                if (!Inb(x, y) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None || IsSolid(x, y) || TileSpread(x, y) > 1 || DecoMap[y, x] == Deco.Rock) return false;
            if (!pen.Tiles().Any(t => BesideRoadTile(t.x, t.y))) return false;
            foreach (var (x, y) in pen.Tiles()) DecoMap[y, x] = Deco.None;
            pen.Count = pen.Capacity;
            AddPen(pen);
            return true;
        }

        /// <summary>A tile offset from the hall back in the town's own frame (the inverse of BuildTown's P).</summary>
        static (int u, int v) Unrot(int dx, int dy, int rot) => rot switch { 0 => (dx, dy), 1 => (dy, dx), 2 => (-dx, -dy), _ => (-dy, -dx) };

        bool BesideRoadTile(int x, int y)
        {
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                if (Inb(nx, ny) && Grid[ny, nx]?.T == "road") return true;
            }
            return false;
        }

        /// <summary>The battle map of a built town: every building blocks the way (roads, fields and gaps in the palisade are open), the lord
        /// waits at the hall, soldiers muster at the barracks, and the stores hold what can be plundered.</summary>
        CampMap TownMap(Settlement s, int x0, int y0, int n)
        {
            var m = new CampMap(n) { IsTownMap = true };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int wx = x0 + x, wy = y0 + y;
                    var b = Grid[wy, wx];
                    m.Ground[y, x] = HasTree(wx, wy) ? CampTile.Tree : (IsSolid(wx, wy) || DecoMap[wy, wx] == Deco.Rock) ? CampTile.Rock : CampTile.Open;
                    m.Blocked[y, x] = b != null && b.T != "road";
                    m.Buildable[y, x] = m.Ground[y, x] == CampTile.Open && b == null && TileSpread(wx, wy) == 0;
                }
            var hall = Hall;
            m.Chief = (hall.X + 1 - x0, hall.Y + 1 - y0);
            foreach (var b in Blds)
            {
                if (b.T == "barracks") m.Barracks.Add((b.X - x0, b.Y - y0));
                if (b.T == "house") m.Tents.Add((b.X - x0, b.Y - y0));
                if (b.T == "watchtower") m.Towers.Add((b.X - x0, b.Y - y0));
                if (b.T == "wall" && InSquare(b.X, b.Y, x0, y0, n)) m.BarrierHp[b.Y - y0, b.X - x0] = CampBattle.BattleWallHp;
                if (b.T == "road" && InSquare(b.X, b.Y, x0, y0, n) && IsGateway(b.X, b.Y)) { m.Gates.Add((b.Y - y0) * n + b.X - x0); m.BarrierHp[b.Y - y0, b.X - x0] = CampBattle.BattleGateHp; }
            }
            // what can be carried off, in the open beside the buildings that hold it
            double pop = s.Population;
            void Store(Building b, Res res, int amount)
            {
                if (b == null || amount <= 0) return;
                for (int k = 0; k < b.Sz * 4 + 4; k++)
                {
                    int side = k % 4, along = k / 4 % Math.Max(1, b.Sz);
                    var (tx, ty) = side switch
                    {
                        0 => (b.X + along, b.Y + b.Sz), 1 => (b.X - 1, b.Y + along), 2 => (b.X + along, b.Y - 1), _ => (b.X + b.Sz, b.Y + along),
                    };
                    int lx = tx - x0, ly = ty - y0;
                    if (m.Passable(lx, ly) && m.PileAt(lx, ly) == null) { m.Piles.Add(new SupplyPile { X = lx, Y = ly, Res = res, Amount = amount }); return; }
                }
            }
            Store(hall, Res.Gold, (int)Math.Round(pop / 12) + 10);
            foreach (var market in Blds.Where(b => b.T == "market").Take(3)) Store(market, Res.Gold, (int)Math.Round(pop / 20));
            foreach (var silo in Blds.Where(b => b.T == "silo").Take(4)) Store(silo, Res.Food, ((int)Math.Round(pop / 4) + 10) * TradeUnit(Res.Food));
            // what it produces, at its storehouses (or its market): a pile of each good
            var stores = Blds.Where(b => b.T == "storehouse").Concat(Blds.Where(b => b.T == "market")).ToList();
            var goods = SplitGoods(s, (int)Math.Round(pop / 6) + 10 * Math.Max(1, s.Produce.Count));
            for (int i = 0; i < goods.Count; i++) Store(stores.Count > 0 ? stores[i % stores.Count] : hall, goods[i].res, goods[i].n);
            if (m.Piles.Count == 0) Store(hall, Res.Food, ((int)Math.Round(pop / 4) + 10) * TradeUnit(Res.Food));
            return m;
        }

        /// <summary>A day in an NPC settlement you are looking at: its crews go out and its villagers walk about. Nothing else of its economy
        /// runs (it is a picture of the town, not a second simulation).</summary>
        public void AmbientDay()
        {
            var got = new Dictionary<Res, double> { { Res.Logs, 0 }, { Res.Stone, 0 }, { Res.Ore, 0 }, { Res.Food, 0 } };
            Recalc();
            StepCrews(got);
            StepFolk();
            Day++;
        }
    }
}
