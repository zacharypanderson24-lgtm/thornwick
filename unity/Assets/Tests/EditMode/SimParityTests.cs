// Checks the C# simulation against the JavaScript prototype, tile for tile.
// The reference comes from the real browser code: run `node unity/ParityReference/make_reference.js` from the repo root to refresh it.
using System;
using System.IO;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class SimParityTests
    {
        [Serializable] class HashCase { public int x, y, s; public double v; }
        [Serializable] class ElevCase { public int k, v; }
        [Serializable] class BldCase { public string t; public int x, y, sz, hut, face, lvl; public bool served; }
        [Serializable] class SeedCase
        {
            public int seed; public int[] hv; public string deco, fert, amt, variants, groves;
            public ElevCase[] elev; public BldCase[] blds; public int civ, jobs, idle, employed;
        }
        [Serializable] class DevStep { public int day; public double wood; public double[] dem; public BldCase[] added; }
        [Serializable] class DevCase { public int seed, R; public DevStep[] steps; }
        [Serializable] class TickDay { public int day, peasants, blds, trees, stumps, ore; public double[] res; public string crews, folk; }
        [Serializable] class TickCase { public int seed, R; public string lodges; public TickDay[] days; }
        [Serializable] class Reference { public int N; public HashCase[] hash; public SeedCase[] seeds; public DevCase develop; public TickCase ticks; }

        static Reference _ref;
        static Reference Ref
        {
            get
            {
                if (_ref != null) return _ref;
                string path = Path.Combine(Application.dataPath, "..", "ParityReference", "reference.json");
                Assert.IsTrue(File.Exists(path), "Missing " + path + ". Run node unity/ParityReference/make_reference.js from the repo root.");
                return _ref = JsonUtility.FromJson<Reference>(File.ReadAllText(path));
            }
        }

        static SeedCase SeedOf(int seed) => Ref.seeds.First(s => s.seed == seed);

        [Test]
        public void MapSizeMatches() => Assert.AreEqual(Ref.N, Defs.N);

        [Test]
        public void Hash2MatchesJavaScript()
        {
            foreach (var c in Ref.hash) Assert.AreEqual(c.v, Noise.Hash2(c.x, c.y, c.s), 0, $"hash2({c.x},{c.y},{c.s})");
        }

        [TestCase(7)]
        [TestCase(1234)]
        public void HeightsMatch(int seed)
        {
            var w = new World(seed, TerrainStyle.Classic); var r = SeedOf(seed);
            Assert.AreEqual(r.hv.Length, w.Hv.Length);
            int bad = 0; string first = null;
            for (int i = 0; i < r.hv.Length; i++) if (r.hv[i] != w.Hv[i]) { bad++; first ??= $"vertex {i % (Defs.N + 1)},{i / (Defs.N + 1)}: js {r.hv[i]} c# {w.Hv[i]}"; }
            Assert.AreEqual(0, bad, $"{bad} height vertices differ, first {first}");
        }

        [TestCase(7)]
        [TestCase(1234)]
        public void TilesMatch(int seed)
        {
            var w = new World(seed, TerrainStyle.Classic); var r = SeedOf(seed);
            int N = Defs.N;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    // ore showings are Unity's own since the mining rework (World.GenDeposits puts them on hill flanks over real ore),
                    // so an ore tile on either side counts as open ground here; MiningTests covers where they go
                    char deco = w.DecoMap[y, x] switch { Deco.Tree => 'T', Deco.Rock => 'R', Deco.Stump => 'S', Deco.Sapling => 's', _ => '.' };
                    Assert.AreEqual(r.deco[i] == 'O' ? '.' : r.deco[i], deco, $"deco at {x},{y}");
                    // the starting farm's fields are made fertile (World.AddStarterFarmFields)
                    Assert.AreEqual(r.fert[i] == '1' || w.ZoneMap[y, x] == Zone.Farm && w.Grid[y, x] == null, w.Fert[y, x], $"fertility at {x},{y}");
                    Assert.AreEqual(r.amt[i], deco == 'R' ? (char)('0' + w.RockAmt(x, y)) : '0', $"rock amount at {x},{y}");
                    if (deco == 'T')
                    {
                        Assert.AreEqual(r.variants[i] - '0', w.TreeVariant(x, y), $"tree variant at {x},{y}");
                        Assert.AreEqual(r.groves[i] - '0', w.TreeGrove(x, y).n, $"grove size at {x},{y}");
                    }
                }
            foreach (var e in r.elev) Assert.AreEqual(e.v, w.Elev[e.k], $"mountain elevation at tile {e.k}");
            Assert.AreEqual(r.elev.Length, w.Elev.Count);
        }

        [TestCase(7)]
        [TestCase(1234)]
        public void StartingSettlementMatches(int seed)
        {
            var w = new World(seed, TerrainStyle.Classic); var r = SeedOf(seed);
            Assert.AreEqual(r.blds.Length, w.Blds.Count);
            for (int i = 0; i < r.blds.Length; i++)
            {
                var e = r.blds[i]; var b = w.Blds[i];
                Assert.AreEqual(e.t, b.T); Assert.AreEqual(e.x, b.X); Assert.AreEqual(e.y, b.Y); Assert.AreEqual(e.sz, b.Sz);
                Assert.AreEqual(e.hut, b.Var, $"{b.T} hut look"); Assert.AreEqual(e.served, b.Served, $"{b.T} served"); Assert.AreEqual(e.lvl, b.Lvl);
                if (e.t != "road") Assert.AreEqual(e.face, b.Face, $"{b.T} at {b.X},{b.Y} face");
            }
            Assert.AreEqual(r.civ, w.D.Civ, "housing"); Assert.AreEqual(r.jobs, w.D.Jobs, "jobs");
            Assert.AreEqual(r.idle, w.D.Idle, "idle"); Assert.AreEqual(r.employed, w.D.Employed, "employed");
        }

        [Test]
        public void TownSurvivesAThousandDays()
        {
            var w = new World(7, TerrainStyle.Classic);
            for (int d = 0; d < 1000; d++) { w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 100); w.Tick(); }
            Assert.Greater(w.Peasants, 0);
            Assert.AreEqual(1001, w.Day);
            foreach (var kv in w.ResAmt) Assert.That(kv.Value, Is.InRange(0, Defs.Storage), kv.Key.ToString());
        }

        [Test]
        public void WoodcutterFellsMarkedTreesIntoLogs()
        {
            var w = new World(7, TerrainStyle.Classic);
            // find a tree near the road end and put a woodcutter lodge on open ground next to a road
            int c = Defs.Start;
            bool TreeNear(int x, int y)
            {
                for (int j = y - Defs.WorkR; j <= y + Defs.WorkR; j++) for (int i = x - Defs.WorkR; i <= x + Defs.WorkR; i++)
                    if (World.Inb(i, j) && w.DecoMap[j, i] == Deco.Tree) return true;
                return false;
            }
            var site = Enumerable.Range(c - 12, 25).SelectMany(y => Enumerable.Range(c - 12, 25).Select(x => (x, y)))
                .FirstOrDefault(p => p.y != c + 2 && Math.Abs(p.x - c) > 2 && w.CanPlace("wood", p.x, p.y).Ok && TreeNear(p.x, p.y));
            Assume.That(site != default, "no woodcutter site near the start on this seed");
            Assert.IsTrue(w.Place("wood", site.x, site.y).Ok);
            // a road from the existing street (x=c, y=c+2) to the tile beside the lodge on the street's side
            var road = World.LPath(c, c + 2, site.x, site.y + (site.y < c + 2 ? 1 : -1));
            for (int y = site.y - Defs.WorkR; y <= site.y + Defs.WorkR; y++)
                for (int x = site.x - Defs.WorkR; x <= site.x + Defs.WorkR; x++)
                    if (World.Inb(x, y) && w.DecoMap[y, x] == Deco.Tree) w.SetZone(Zone.Wood, x, y);
            // connect it: clear the path by hand, then road it
            foreach (var (x, y) in road) if (w.Grid[y, x] == null && w.DecoMap[y, x] != Deco.None) w.Remove(x, y);
            w.ResAmt[Res.Wood] = 400;
            w.PlaceRoadPath(road);
            w.Recalc();
            Assume.That(w.Grid[site.y, site.x].Served, "could not connect the test lodge");
            int trees = CountTrees(w);
            for (int d = 0; d < 60; d++) { w.ResAmt[Res.Food] = 200; w.Tick(); }
            Assert.Less(CountTrees(w), trees, "no tree was felled");
        }

        /// <summary>tests/lib.js layout(): a comb of roads around the Town Hall with zones painted beside them.</summary>
        internal static void Layout(World w, int R)
        {
            int N = Defs.N, C = Defs.Start, gap = 4;
            bool Free(int x, int y) => World.Inb(x, y) && w.Grid[y, x] == null && !w.HasTree(x, y) && !w.IsSolid(x, y);
            void Road(int x, int y)
            {
                if (World.Inb(x, y) && w.Grid[y, x] == null && (w.DecoMap[y, x] == Deco.Tree || w.DecoMap[y, x] == Deco.Stump || w.DecoMap[y, x] == Deco.Sapling)) w.Remove(x, y);
                if (Free(x, y)) w.AddBld("road", x, y);
            }
            for (int y = C - R; y <= C + R; y += gap) for (int x = C - R; x <= C + R; x++) Road(x, y);
            for (int x = C - R; x <= C + R; x += gap) for (int y = C - R; y <= C + R; y++) Road(x, y);
            w.Recalc();
            for (int y = C - R; y <= C + R; y++)
                for (int x = C - R; x <= C + R; x++)
                {
                    if (x < 0 || y < 0 || x >= N || y >= N || w.Grid[y, x] != null) continue;
                    var dc = w.DecoMap[y, x]; double r = Noise.Hash2(x, y, 4242);
                    if (dc == Deco.Tree) { if (r < 0.7) w.SetZone(Zone.Wood, x, y); continue; }
                    if (dc == Deco.Rock || dc == Deco.Ore) { w.SetZone(Zone.Mine, x, y); continue; }
                    if (w.Fert[y, x] && r < 0.35) { w.SetZone(Zone.Farm, x, y); continue; }
                    if (r < 0.5) w.SetZone(Zone.Res, x, y); else if (r < 0.58) w.SetZone(Zone.Comm, x, y);
                }
        }

        [Test]
        public void ZoneDevelopmentMatchesJavaScript()
        {
            var dev = Ref.develop;
            var w = new World(dev.seed, TerrainStyle.Classic);
            StripOre(w); Layout(w, dev.R); StripFarms(w);
            int total = 0;
            for (int i = 0; i < dev.steps.Length; i++)
            {
                var step = dev.steps[i];
                w.Recalc();
                w.ResAmt[Res.Food] = 5 + (i * 37) % 260; w.ResAmt[Res.Wood] = 20 + (i * 53) % 170; w.ResAmt[Res.Logs] = (i * 11) % 30;
                w.ResAmt[Res.Stone] = (i * 13) % 80; w.ResAmt[Res.Ore] = (i * 7) % 40; w.ResAmt[Res.Gold] = 100;
                w.Peasants = Math.Max(1, Noise.JsRound(w.D.Civ * (0.55 + 0.05 * ((i * 17) % 10))));
                w.Rate[Res.Food] = ((i % 5) - 2) * 0.2; w.Starving = i % 23 == 0;
                w.Recalc();
                Assert.AreEqual(step.day, w.Day, "day");
                int before = w.Blds.Count;
                w.Develop();
                for (int k = 0; k < World.ZoneKinds.Length; k++)
                    Assert.AreEqual(step.dem[k], w.Dem[World.ZoneKinds[k]], 1e-9, $"step {i}: demand for {World.ZoneKinds[k]}");
                var added = w.Blds.Skip(before).ToList();
                Assert.AreEqual(step.added.Length, added.Count, $"step {i}: buildings grown, js: " + string.Join(", ", step.added.Select(b => $"{b.t}@{b.x},{b.y}x{b.sz}")) +
                                                                " c#: " + string.Join(", ", added.Select(b => $"{b.T}@{b.X},{b.Y}x{b.Sz}")));
                for (int k = 0; k < added.Count; k++)
                {
                    var e = step.added[k]; var b = added[k];
                    Assert.AreEqual($"{e.t}@{e.x},{e.y}x{e.sz}", $"{b.T}@{b.X},{b.Y}x{b.Sz}", $"step {i}: building {k}");
                    Assert.AreEqual(e.hut, b.Var, $"step {i}: hut look");
                }
                Assert.AreEqual(step.wood, w.ResAmt[Res.Wood], 1e-9, $"step {i}: wood left");
                total += added.Count;
                w.Day++;
            }
            Assert.Greater(total, 20, "the scenario should grow plenty of buildings");
        }

        /// <summary>make_reference.js tick scenario: two Woodcutter Lodges on the first road-side sites with 3+ marked trees in reach, 5+ tiles apart.</summary>
        static string PlaceLodges(World w)
        {
            int C = Defs.Start; var lodges = new System.Collections.Generic.List<(int x, int y)>();
            bool RoadAt(int x, int y) => World.Inb(x, y) && w.Grid[y, x] != null && w.Grid[y, x].T == "road";
            for (int y = C - 13; y <= C + 13 && lodges.Count < 2; y++)
                for (int x = C - 13; x <= C + 13 && lodges.Count < 2; x++)
                {
                    if (!w.CanPlace("wood", x, y).Ok || w.CountZoned(x, y, 3, Deco.Tree, Zone.Wood) < 3) continue;
                    if (!(RoadAt(x + 1, y) || RoadAt(x - 1, y) || RoadAt(x, y + 1) || RoadAt(x, y - 1))) continue;
                    if (lodges.Any(l => Math.Max(Math.Abs(l.x - x), Math.Abs(l.y - y)) < 5)) continue;
                    w.AddBld("wood", x, y); lodges.Add((x, y));
                }
            w.Recalc();
            return string.Join(" ", lodges.Select(l => l.x + "," + l.y));
        }

        static readonly string[] CrewSt = { "h", "o", "w", "b" }, FolkSt = { "h", "k", "s", "b" };
        static string CrewDigest(World w) => string.Join("|", w.Blds.Where(b => World.CrewKind(b.T) != Deco.None).Select(b => $"{b.T}@{b.X},{b.Y}:" +
            string.Join(" ", b.Crew.Select(a => $"{CrewSt[(int)a.St]}{a.X},{a.Y},{Noise.JsRound(a.Carry * 1000)}"))));
        static string FolkDigest(World w) => string.Join(" ", w.Folk.Select(v => $"{v.Id}{FolkSt[(int)v.St]}{v.X},{v.Y},{v.V}"));

        /// <summary>
        /// 300 whole days through Tick() against the browser's tick(): walking work crews (paths, standing tiles, loads, felling, digging),
        /// townsfolk (who is out, where, which tunic), resources, population and development must agree every day.
        /// </summary>
        [Test]
        public void CrewsAndTownsfolkMatchJavaScriptDayByDay()
        {
            var tc = Ref.ticks;
            Assert.IsNotNull(tc, "reference.json has no tick scenario. Run node unity/ParityReference/make_reference.js from the repo root.");
            var w = new World(tc.seed, TerrainStyle.Classic);
            StripOre(w); Layout(w, tc.R); StripFarms(w);
            Assert.AreEqual(tc.lodges, PlaceLodges(w), "woodcutter lodge sites");
            var keys = new[] { Res.Food, Res.Wood, Res.Logs, Res.Stone, Res.Ore, Res.Gold };
            bool sawWork = false, sawFolkOut = false;
            for (int i = 0; i < tc.days.Length; i++)
            {
                var e = tc.days[i];
                w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 300); w.ResAmt[Res.Wood] = Math.Max(w.ResAmt[Res.Wood], 200); w.ResAmt[Res.Gold] = Math.Max(w.ResAmt[Res.Gold], 100);
                w.Tick();
                string at = $"day {e.day} (step {i})";
                Assert.AreEqual(e.day, w.Day, at);
                Assert.AreEqual(e.crews, CrewDigest(w), at + ": crews");
                Assert.AreEqual(e.folk, FolkDigest(w), at + ": townsfolk");
                for (int k = 0; k < keys.Length; k++) Assert.AreEqual(e.res[k], w.ResAmt[keys[k]], 1e-6, $"{at}: {keys[k]}");
                Assert.AreEqual(e.peasants, w.Peasants, at + ": peasants");
                Assert.AreEqual(e.blds, w.Blds.Count, at + ": buildings");
                int trees = 0, stumps = 0, ore = 0;
                foreach (var d in w.DecoMap) { if (d == Deco.Tree) trees++; else if (d == Deco.Stump) stumps++; else if (d == Deco.Ore) ore++; }
                Assert.AreEqual(e.trees, trees, at + ": trees"); Assert.AreEqual(e.stumps, stumps, at + ": stumps"); Assert.AreEqual(e.ore, ore, at + ": ore tiles");
                sawWork |= System.Text.RegularExpressions.Regex.IsMatch(e.crews, @"[: ]w\d");
                sawFolkOut |= System.Text.RegularExpressions.Regex.IsMatch(e.folk, @"\dk\d");
            }
            Assert.IsTrue(sawWork && sawFolkOut, "the scenario should have crews at work and villagers walking");
        }

        /// <summary>
        /// Mining is underground in Unity but still surface ore in the browser, so the day-by-day scenarios remove every ore showing and its
        /// zone first (make_reference.js stripOre does the same). Mining itself is covered by MiningTests.
        /// </summary>
        /// <summary>Farming is Unity's own (farmhouses with zoned crop plots), so the scenarios remove the starting farm and every Farmland
        /// zone after laying out the town (make_reference.js stripFarms does the same). Farming is covered by FarmTests.</summary>
        static void StripFarms(World w)
        {
            foreach (var f in w.Blds.Where(b => b.T == "farm").ToList()) w.Remove(f.X, f.Y);
            for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.ZoneMap[y, x] == Zone.Farm) w.ZoneMap[y, x] = Zone.None;
            w.Touch(); w.Recalc();
        }

        static void StripOre(World w)
        {
            for (int y = 0; y < Defs.N; y++)
                for (int x = 0; x < Defs.N; x++)
                    if (w.DecoMap[y, x] == Deco.Ore) { w.DecoMap[y, x] = Deco.None; w.ZoneMap[y, x] = Zone.None; }
            w.Touch(); w.Recalc();
        }

        static int CountTrees(World w) { int n = 0; foreach (var d in w.DecoMap) if (d == Deco.Tree) n++; return n; }
    }
}
