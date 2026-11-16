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
        [Serializable] class Reference { public int N; public HashCase[] hash; public SeedCase[] seeds; }

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
            var w = new World(seed); var r = SeedOf(seed);
            Assert.AreEqual(r.hv.Length, w.Hv.Length);
            int bad = 0; string first = null;
            for (int i = 0; i < r.hv.Length; i++) if (r.hv[i] != w.Hv[i]) { bad++; first ??= $"vertex {i % (Defs.N + 1)},{i / (Defs.N + 1)}: js {r.hv[i]} c# {w.Hv[i]}"; }
            Assert.AreEqual(0, bad, $"{bad} height vertices differ, first {first}");
        }

        [TestCase(7)]
        [TestCase(1234)]
        public void TilesMatch(int seed)
        {
            var w = new World(seed); var r = SeedOf(seed);
            int N = Defs.N;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    char deco = w.DecoMap[y, x] switch { Deco.Tree => 'T', Deco.Rock => 'R', Deco.Ore => 'O', Deco.Stump => 'S', Deco.Sapling => 's', _ => '.' };
                    Assert.AreEqual(r.deco[i], deco, $"deco at {x},{y}");
                    Assert.AreEqual(r.fert[i] == '1', w.Fert[y, x], $"fertility at {x},{y}");
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
            var w = new World(seed); var r = SeedOf(seed);
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
            var w = new World(7);
            for (int d = 0; d < 1000; d++) { w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 100); w.Tick(); }
            Assert.Greater(w.Peasants, 0);
            Assert.AreEqual(1001, w.Day);
            foreach (var kv in w.ResAmt) Assert.That(kv.Value, Is.InRange(0, Defs.Storage), kv.Key.ToString());
        }

        [Test]
        public void WoodcutterFellsMarkedTreesIntoLogs()
        {
            var w = new World(7);
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

        static int CountTrees(World w) { int n = 0; foreach (var d in w.DecoMap) if (d == Deco.Tree) n++; return n; }
    }
}
