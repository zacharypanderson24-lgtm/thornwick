// Quarrying without painted zones (World.Quarry.cs, World.Extraction.cs): a quarry works the rock around it automatically, a painted boundary restricts it,
// the stone is finite and shared, and the pits it digs persist.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class QuarryAutoTests
    {
        const int N = Defs.N;

        static void Day(World w)
        {
            w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 300); w.ResAmt[Res.Wood] = Math.Max(w.ResAmt[Res.Wood], 200); w.ResAmt[Res.Gold] = Math.Max(w.ResAmt[Res.Gold], 100);
            w.Tick();
        }

        static double DigSum(World w) => w.Dig.Values.Sum();

        /// <summary>The test town with a quarry beside open rock and a road to it.</summary>
        static (World w, Building q) QuarryTown(int skip = 0)
        {
            var w = new World(7);
            SimParityTests.Layout(w, 10);
            w.Prio = new[] { "quarrying", "farming", "timber", "woodwork", "mining", "trade" };
            w.Drafting = false;
            foreach (var old in w.Blds.Where(o => o.T == "quarry" || o.T == "mine").ToList()) w.Remove(old.X, old.Y);   // the town builder's own, and its painted zones
            for (int zy = 0; zy < N; zy++) for (int zx = 0; zx < N; zx++) if (w.ZoneMap[zy, zx] == Zone.Mine) w.ZoneMap[zy, zx] = Zone.None;
            w.Touch(); w.Recalc();
            var sites = Enumerable.Range(0, N * N).Select(k => (x: k % N, y: k / N))
                .Where(t => Math.Max(Math.Abs(t.x - Defs.Start), Math.Abs(t.y - Defs.Start)) <= 40)
                .OrderBy(t => Math.Abs(t.x - Defs.Start) + Math.Abs(t.y - Defs.Start)).ThenBy(t => t.y * N + t.x);
            foreach (var (x, y) in sites)
            {
                if (!w.CanPlace("quarry", x, y).Ok) continue;
                var s = w.SurveyQuarrySite(x, y);
                if (s.DigTiles < 3 || s.Level == World.SiteLevel.Invalid) continue;
                if (skip > 0) { skip--; continue; }
                if (!MiningTests.RoadTo(w, x - 1, y, x, y) && !MiningTests.RoadTo(w, x, y + 2, x, y)) continue;
                var b = w.AddBld("quarry", x, y, 2); w.Recalc();
                if (!b.Served) { w.Remove(x, y); continue; }
                return (w, b);
            }
            return (w, null);
        }

        [Test]
        public void AQuarryWorksNearbyRockWithNoZoneAtAll()
        {
            var (w, q) = QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            Assert.IsFalse(w.UsesZone(q, Zone.Mine));
            Assert.Greater(w.ActiveJobs(q), 0, "rock in its area gives it work");
            double stone0 = w.ResAmt[Res.Stone], dug0 = DigSum(w);
            for (int d = 0; d < 300; d++) Day(w);
            Assert.Greater(DigSum(w), dug0, "rock was cut out of the ground");
            Assert.Greater(w.ResAmt[Res.Stone], stone0, "stone arrived");
            StringAssert.Contains("automatic", w.QuarryStatus(q));
        }

        [Test]
        public void AQuarryNeverTouchesRockOutsideItsAutomaticArea()
        {
            var (w, q) = QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            var before = new Dictionary<int, double>(w.Dig);
            for (int d = 0; d < 400; d++) Day(w);
            foreach (var kv in w.Dig)
            {
                if (before.TryGetValue(kv.Key, out var o) && Math.Abs(o - kv.Value) < 1e-9) continue;
                int x = kv.Key % N, y = kv.Key / N;
                Assert.LessOrEqual(Math.Max(Math.Max(q.X - x, x - (q.X + q.Sz - 1)), Math.Max(Math.Max(q.Y - y, y - (q.Y + q.Sz - 1)), 0)), Defs.QuarryAutoR, $"dug {x},{y} beyond the quarry's {Defs.QuarryAutoR}-tile area");
            }
        }

        [Test]
        public void APaintedBoundaryRestrictsTheQuarryToThoseTiles()
        {
            var (w, q) = QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            var one = w.QuarryArea(q).Where(t => t.state == World.QuarryTile.Dig).Select(t => (t.x, t.y)).FirstOrDefault();
            Assume.That(one != default, "no workable rock");
            w.ZoneMap[one.y, one.x] = Zone.Mine; w.Touch(); w.Recalc();
            Assert.IsTrue(w.UsesZone(q, Zone.Mine));
            var before = new Dictionary<int, double>(w.Dig);
            for (int d = 0; d < 200; d++) Day(w);
            foreach (var kv in w.Dig)
                if (!before.TryGetValue(kv.Key, out var o) || Math.Abs(o - kv.Value) > 1e-9)
                    Assert.AreEqual(World.Tk(one.x, one.y), kv.Key, "only the painted tile was worked");
        }

        [Test]
        public void ARockFreeSpotCannotTakeAQuarry()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var w = new World(seed); w.ClaimAll(); w.ResAmt[Res.Wood] = 500;
                for (int y = 5; y < N - 5; y += 2)
                    for (int x = 5; x < N - 5; x += 2)
                    {
                        if (w.Grid[y, x] != null || w.HasTree(x, y) || w.IsSolid(x, y) || w.SurveyQuarrySite(x, y).Level != World.SiteLevel.Invalid) continue;
                        var r = w.CanPlace("quarry", x, y);
                        if (r.Ok || !(r.Reason ?? "").StartsWith("No rock")) continue;   // another rule (water, slope) refused it first
                        return;
                    }
            }
            Assert.Inconclusive("no rock-free open ground in twelve test worlds");
        }

        [Test]
        public void TwoQuarriesShareTheSameFiniteStone()
        {
            var (w, q) = QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            double before = w.QuarryReach(q).StoneLeft;
            var (w2, q2) = (w, w.AddBld("quarry", q.X + 3, q.Y, 2));
            if (w.Grid[q.Y, q.X + 3] != q2) Assert.Inconclusive("no room for a second quarry");
            w.Recalc();
            Assert.AreEqual(before, w.QuarryReach(q).StoneLeft, 1e-9, "another quarry does not create stone");
            double dug0 = DigSum(w);
            for (int d = 0; d < 300; d++) Day(w);
            double after = w.QuarryReach(q).StoneLeft, dug = DigSum(w) - dug0;
            Assert.Greater(dug, 0);
            Assert.LessOrEqual(before - after, dug + 1e-6, "the stone gone from the area is what the crews dug, no more");
        }

        [Test]
        public void TheExcavationAndStoneLeftSurviveSaveAndLoad()
        {
            var (w, q) = QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            for (int d = 0; d < 300; d++) Day(w);
            Assume.That(DigSum(w) > 0, "nothing dug yet");
            var loaded = World.LoadJson(w.SaveJson());
            var lq = loaded.Grid[q.Y, q.X];
            Assert.AreEqual(DigSum(w), DigSum(loaded), 1e-9, "a loaded save does not refill the rock");
            Assert.AreEqual(w.QuarryReach(q).StoneLeft, loaded.QuarryReach(lq).StoneLeft, 1e-9);
            Assert.AreEqual(w.QuarryReach(q).Pits, loaded.QuarryReach(lq).Pits);
        }

        [Test]
        public void ASiteSurveyExplainsEveryWarning()
        {
            var w = new World(7); w.ClaimAll();
            int shown = 0;
            for (int y = 10; y < N - 10 && shown < 6; y += 4)
                for (int x = 10; x < N - 10 && shown < 6; x += 4)
                {
                    var s = w.SurveyQuarrySite(x, y);
                    if (s.Level == World.SiteLevel.Valid) continue;
                    Assert.Greater(s.Notes.Count, 0, $"site {x},{y} is {s.Level} with no explanation");
                    shown++;
                }
            Assume.That(shown > 0);
        }
    }
}
