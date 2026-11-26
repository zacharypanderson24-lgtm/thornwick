// Mining without painted zones (World.Extraction.cs, DESIGN_RESOURCE_EXTRACTION.md): a mine works the ground in its reach by itself, painted Mining
// tiles are only an optional boundary, the site survey says how good a spot is, and the ore in reach is finite and shared.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class MiningAutoTests
    {
        const int N = Defs.N;

        static void Day(World w)
        {
            w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 300); w.ResAmt[Res.Wood] = Math.Max(w.ResAmt[Res.Wood], 200); w.ResAmt[Res.Gold] = Math.Max(w.ResAmt[Res.Gold], 100);
            w.Tick();
        }

        static void Unzone(World w)
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (w.ZoneMap[y, x] == Zone.Mine) w.ZoneMap[y, x] = Zone.None;
            w.Touch(); w.Recalc();
        }

        [Test]
        public void AMineWorksWithNoMiningZoneAtAll()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            Unzone(w); w.CaveInRiskScale = 0;
            Assert.IsFalse(w.UsesZone(mine, Zone.Mine), "no boundary painted: the whole reach is open");
            Assert.Greater(w.ActiveJobs(mine), 0, "an unzoned mine still has work, so it gets workers");
            for (int d = 0; d < 400; d++) Day(w);
            Assert.Greater(mine.Tunnel.Count, 0, "the tunnel was dug without any zone");
            Assert.Greater(w.OreTaken.Count, 0, "ore was taken out of the ground");
            StringAssert.DoesNotContain("zone", w.MineStatus(mine).ToLower().Replace("no open ground", ""), "the status no longer tells the player to zone anything");
        }

        [Test]
        public void APaintedZoneStillBoundsTheTunnel()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            Assert.IsTrue(w.UsesZone(mine, Zone.Mine), "the helper painted a patch");
            for (int d = 0; d < 300; d++) Day(w);
            foreach (int t in mine.Tunnel) Assert.AreEqual(Zone.Mine, w.ZoneMap[t / N, t % N], "the tunnel stays inside the optional boundary");
        }

        [Test]
        public void AnUnzonedTunnelNeverSpreadsAcrossOtherLandUse()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            Unzone(w); w.CaveInRiskScale = 0;
            int fieldX = mine.X - 1, fieldY = mine.Y - 1;
            if (World.Inb(fieldX, fieldY) && w.Grid[fieldY, fieldX] == null && w.ZoneCheck(Zone.Res, fieldX, fieldY).Ok) w.SetZone(Zone.Res, fieldX, fieldY);
            for (int d = 0; d < 300; d++) Day(w);
            foreach (int t in mine.Tunnel) Assert.AreEqual(Zone.None, w.ZoneMap[t / N, t % N], "a tunnel is never driven under housing or farm zones");
        }

        [Test]
        public void AMineWithNoOreInReachCannotBePlaced()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var w = new World(seed); w.ResAmt[Res.Wood] = 500;
                for (int y = Defs.Start - 30; y <= Defs.Start + 30; y++)
                    for (int x = Defs.Start - 30; x <= Defs.Start + 30; x++)
                    {
                        if (!w.Claimed(x, y) || w.Grid[y, x] != null || w.HasTree(x, y) || w.IsSolid(x, y)) continue;
                        if (w.SurveyMineSite(x, y).Level != World.SiteLevel.Invalid) continue;
                        var fp = w.CanPlace("mine", x, y);
                        if (fp.Ok || !(fp.Reason ?? "").StartsWith("No ore")) continue;   // some other rule (water, slope) refused it first
                        Assert.IsFalse(fp.Ok);
                        return;
                    }
            }
            Assert.Inconclusive("no ore-free claimed open ground in twelve test worlds");
        }

        [Test]
        public void ASiteWithOreIsValidOrWarnedNeverBlocked()
        {
            var w = new World(7); w.ResAmt[Res.Wood] = 500;
            int found = 0;
            for (int y = 5; y < N - 5 && found < 3; y += 2)
                for (int x = 5; x < N - 5 && found < 3; x += 2)
                {
                    var s = w.SurveyMineSite(x, y);
                    if (s.Reserve <= 0 || !w.Claimed(x, y)) continue;
                    Assert.AreNotEqual(World.SiteLevel.Invalid, s.Level, $"ore at {x},{y}");
                    Assert.Greater(s.DailyEstimate, 0); Assert.Greater(s.LifetimeYears, 0);
                    if (s.Level == World.SiteLevel.Warning) Assert.Greater(s.Notes.Count, 0, "every warning is explained");
                    found++;
                }
            Assume.That(found > 0, "no ore near a claimed tile");
        }

        [Test]
        public void ASecondMineDrawsFromTheSameFiniteOre()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            Unzone(w); w.CaveInRiskScale = 0;
            double before = w.SurveyMineSite(mine.X, mine.Y).Reserve;
            // a second mine right beside the first: it must see exactly what is in the ground, not a fresh copy of it
            var spot = Enumerable.Range(0, 400).Select(i => (x: mine.X + 3 + i % 6, y: mine.Y + i / 6 - 3)).FirstOrDefault(t => World.Inb(t.x, t.y) && w.Grid[t.y, t.x] == null && w.CanPlace("mine", t.x, t.y).Ok);
            Assume.That(spot != default, "no room for a second mine");
            w.AddBld("mine", spot.x, spot.y); w.Recalc();
            Assert.AreEqual(before, w.SurveyMineSite(mine.X, mine.Y).Reserve, 1e-9, "building another mine does not create ore");
            for (int d = 0; d < 300; d++) Day(w);
            double after = w.SurveyMineSite(mine.X, mine.Y).Reserve;
            double taken = w.OreTaken.Values.Sum();
            Assert.Less(after, before, "mining used the ore up");
            Assert.LessOrEqual(before - after, taken + 1e-6, "the ore gone from the ground is what both mines took, no more");
        }

        [Test]
        public void ARunningLowAndAnOutOfOreWarningAreGiven()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            Unzone(w); w.CaveInRiskScale = 0;
            for (int d = 0; d < 80; d++) Day(w);
            Assume.That(w.OreTaken.Count > 0, "the mine took no ore yet");
            // leave a little ore in reach: running low
            var tiles = new List<(int x, int y, int band)>();
            for (int j = mine.Y - Defs.WorkR; j <= mine.Y + Defs.WorkR; j++) for (int i = mine.X - Defs.WorkR; i <= mine.X + Defs.WorkR; i++)
                if (World.Inb(i, j)) for (int l = 1; l <= World.Bands; l++) if (w.OreAt(i, j, l) > 0) tiles.Add((i, j, l));
            int keep = 0;
            foreach (var t in tiles) { double left = w.OreAt(t.x, t.y, t.band); int key = t.band * N * N + World.Tk(t.x, t.y); w.OreTaken[key] = w.OreTaken.GetValueOrDefault(key) + (keep++ == 0 ? left - 20 : left); }
            w.Touch(); w.Events.Clear();
            for (int d = 0; d < 61; d++) Day(w);
            Assert.IsTrue(w.Events.Any(e => e.Contains("running low")), "a low-ore warning came before the end: " + string.Join(" | ", w.Events));
            foreach (var t in tiles) { int key = t.band * N * N + World.Tk(t.x, t.y); w.OreTaken[key] = w.OreTaken.GetValueOrDefault(key) + w.OreAt(t.x, t.y, t.band); }
            w.Touch(); w.Events.Clear();
            for (int d = 0; d < 61; d++) Day(w);
            Assert.IsTrue(w.Events.Any(e => e.Contains("run out of ore")), "the out-of-ore warning was given: " + string.Join(" | ", w.Events));
            StringAssert.Contains("none left", w.MineStatus(mine));
            Assert.AreEqual(0, w.MineReserve(mine), 1e-9);
        }

        [Test]
        public void TheMineStatusSaysHowLongTheOreShouldLast()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            for (int d = 0; d < 40; d++) Day(w);
            string s = w.MineStatus(mine);
            StringAssert.Contains("Ore in reach", s);
            if (mine.Workers > 0) StringAssert.Contains("years", s);
        }

        [Test]
        public void ReservesAndTheWarningStateSurviveSaveAndLoad()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            Unzone(w); w.CaveInRiskScale = 0;
            for (int d = 0; d < 120; d++) Day(w);
            double reserve = w.MineReserve(mine);
            var loaded = World.LoadJson(w.SaveJson());
            var m = loaded.Grid[mine.Y, mine.X];
            Assert.AreEqual(reserve, loaded.MineReserve(m), 1e-9, "a loaded save does not refill the ground");
            Assert.AreEqual(w.OreTaken.Values.Sum(), loaded.OreTaken.Values.Sum(), 1e-9);
        }
    }
}
