// Forestry without painted zones (World.Forestry.cs, World.Extraction.cs): a lodge works the forest around it by itself, in three modes; a sustainable
// lodge replants and leaves a floor of standing trees; woodland can be protected; saplings grow only on growing days; no tree is worked by two crews.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ForestryAutoTests
    {
        const int N = Defs.N;
        static readonly int C = Defs.Start;

        static IEnumerable<(int x, int y)> AllTiles() { for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) yield return (x, y); }

        static bool Gentle(World w, int x, int y, int r)
        {
            for (int j = y - r; j <= y + r; j++)
                for (int i = x - r; i <= x + r; i++)
                    if (!World.Inb(i, j) || w.IsSolid(i, j) || w.TileSpread(i, j) > 1 || w.Grid[j, i] != null) return false;
            return true;
        }

        static void Day(World w)
        {
            w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 400); w.ResAmt[Res.Wood] = Math.Max(w.ResAmt[Res.Wood], 200); w.ResAmt[Res.Gold] = Math.Max(w.ResAmt[Res.Gold], 100);
            w.Tick();
        }

        /// <summary>A lodge on open ground with a road to the town and `trees` trees in a block five tiles away, no zone painted anywhere.</summary>
        static (World w, Building lodge, List<(int x, int y)> trees) Forest(int trees, int seed = 7, int blockX = 5)
        {
            var w = new World(seed); w.ClaimAll(); w.Drafting = false;
            var site = AllTiles().Where(t => Math.Abs(t.x - C) + Math.Abs(t.y - C) is > 14 and < 34 && Gentle(w, t.x, t.y, 14)).OrderBy(t => Math.Abs(t.x - C) + Math.Abs(t.y - C)).FirstOrDefault();
            Assume.That(site.x > 0, "open ground for a lodge");
            for (int j = site.y - Defs.WorkR - 2; j <= site.y + 2 + Defs.WorkR; j++) for (int i = site.x - Defs.WorkR - 2; i <= site.x + 2 + Defs.WorkR; i++)
                if (World.Inb(i, j) && (w.HasTree(i, j) || w.DecoMap[j, i] == Deco.Stump)) w.DecoMap[j, i] = Deco.None;
            var lodge = w.AddBld("wood", site.x, site.y, 2);
            foreach (var (x, y) in World.LPath(C, C + 7, site.x, site.y + 2)) { if (w.Grid[y, x] == null) { w.DecoMap[y, x] = Deco.None; w.AddBld("road", x, y); } }
            w.Recalc();
            Assume.That(lodge.Served, "the lodge could not be connected");
            var list = new List<(int, int)>();
            for (int n = 0; n < trees; n++) list.Add((site.x + blockX + n / 7, site.y - 3 + n % 7));
            foreach (var (x, y) in list) { w.DecoMap[y, x] = Deco.Tree; }
            w.Prio = new[] { "timber", "farming", "woodwork", "quarrying", "mining", "trade" };
            w.Peasants = 60; w.Touch(); w.Recalc();
            return (w, lodge, list);
        }

        static int Count(World w, Deco d, List<(int x, int y)> tiles) => tiles.Count(t => w.DecoMap[t.y, t.x] == d);

        [Test]
        public void ALodgeFellsTreesWithNoLumberZone()
        {
            var (w, lodge, trees) = Forest(40);
            Assert.IsFalse(w.UsesZone(lodge, Zone.Wood));
            Assert.Greater(w.ActiveJobs(lodge), 0, "trees in reach give it work");
            double logs0 = w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood];
            for (int d = 0; d < 250; d++) Day(w);
            Assert.Less(Count(w, Deco.Tree, trees), 40, "trees were felled");
            Assert.Greater(w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood], logs0 + 20, "logs arrived");
        }

        [Test]
        public void ASustainableLodgeLeavesAFloorOfStandingTrees()
        {
            var (w, lodge, trees) = Forest(14);
            for (int d = 0; d < 700; d++) { Day(w); Assert.GreaterOrEqual(w.ForestAround(lodge).Mature, World.SustainFloor, $"day {d}: the floor was breached"); }
            Assert.Less(Count(w, Deco.Tree, trees), 14, "it did cut down to the floor");
            StringAssert.Contains("Limited sustainable production", w.LodgeStatus(lodge));
        }

        [Test]
        public void ASustainableLodgeReplantsWhatItFells()
        {
            var (w, lodge, trees) = Forest(40);
            for (int d = 0; d < 260; d++) Day(w);
            int saplings = Count(w, Deco.Sapling, trees), stumps = Count(w, Deco.Stump, trees), mature = Count(w, Deco.Tree, trees);
            Assert.Greater(saplings, 0, "felled tiles were replanted once the stumps rotted");
            Assert.AreEqual(40, saplings + stumps + mature, "no tile of forest was lost: every felled tile is a stump or a sapling");
        }

        [Test]
        public void AnIntensiveLodgeStripsTheAreaAndDoesNotReplant()
        {
            var (w, lodge, trees) = Forest(14);
            Assert.IsTrue(w.SetForestMode(lodge, ForestMode.Intensive).Ok);
            for (int d = 0; d < 700; d++) Day(w);
            Assert.AreEqual(0, Count(w, Deco.Tree, trees), "no floor: everything was cut");
            Assert.AreEqual(0, Count(w, Deco.Sapling, trees), "and nothing was planted");
            StringAssert.Contains("Intensive", w.LodgeStatus(lodge));
        }

        [Test]
        public void IntensiveGivesMoreLogsEarlyThanSustainableFromTheSameSmallForest()
        {
            double Logs(ForestMode mode)
            {
                var (w, lodge, trees) = Forest(14);
                w.SetForestMode(lodge, mode);
                double start = w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood];
                for (int d = 0; d < 400; d++) Day(w);
                return w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood] - start;
            }
            Assert.Greater(Logs(ForestMode.Intensive), Logs(ForestMode.Sustainable));
        }

        [Test]
        public void ALodgeInClearingModeWorksOnlyDesignatedLandAndDoesNotReplant()
        {
            var (w, lodge, trees) = Forest(40);
            w.SetForestMode(lodge, ForestMode.Clearing);
            Assert.AreEqual(0, w.ActiveJobs(lodge), "nothing designated: nobody needed");
            var marked = trees.Take(6).ToList();
            foreach (var (x, y) in marked) Assert.IsTrue(w.DesignateClearing(x, y).Ok);
            w.Recalc();
            Assert.Greater(w.ActiveJobs(lodge), 0);
            for (int d = 0; d < 300; d++) Day(w);
            Assert.AreEqual(0, Count(w, Deco.Tree, marked), "the designated trees are gone");
            Assert.AreEqual(0, Count(w, Deco.Sapling, marked), "cleared land is not replanted");
            Assert.AreEqual(34, Count(w, Deco.Tree, trees.Skip(6).ToList()), "nothing else was touched");
            Assert.AreEqual(0, w.ClearOrders.Count, "finished orders are removed");
        }

        [Test]
        public void ProtectedWoodlandIsNeverCutAndNeedsConfirmationToClear()
        {
            var (w, lodge, trees) = Forest(40);
            w.SetForestMode(lodge, ForestMode.Intensive);
            var safe = trees.Take(20).ToList();
            foreach (var (x, y) in safe) Assert.IsTrue(w.ProtectWoodland(x, y).Ok);
            for (int d = 0; d < 1100; d++) Day(w);
            Assert.AreEqual(20, Count(w, Deco.Tree, safe), "protected trees were not harvested");
            Assert.AreEqual(0, Count(w, Deco.Tree, trees.Skip(20).ToList()), "the rest was cut by the intensive lodge");
            var (px, py) = safe[0];
            var denied = w.DesignateClearing(px, py);
            Assert.IsFalse(denied.Ok); StringAssert.Contains("protected", denied.Reason);
            Assert.IsTrue(w.DesignateClearing(px, py, confirmProtected: true).Ok);
            Assert.IsFalse(w.IsProtected(px, py), "confirming lifts the protection");
        }

        [Test]
        public void SaplingsGrowOnlyOnGrowingDays()
        {
            var w = new World(7); w.ClaimAll(); w.Drafting = false;
            var spot = AllTiles().First(t => Gentle(w, t.x, t.y, 2) && Math.Abs(t.x - C) + Math.Abs(t.y - C) > 20);
            int k = World.Tk(spot.x, spot.y);
            w.DecoMap[spot.y, spot.x] = Deco.Sapling; w.SapT[k] = 100; w.Touch();
            w.Day = 271;   // the first day of winter
            for (int d = 0; d < 20; d++) Day(w);
            Assert.AreEqual(100, w.SapT[k], "nothing grows in winter");
            w.Day = 100;   // summer
            for (int d = 0; d < 20; d++) Day(w);
            Assert.Less(w.SapT[k], 100, "it grows in summer");
            Assert.Greater(w.SapT[k], 70);
        }

        [Test]
        public void TwoLodgesNeverWorkTheSameTreeAtOnce()
        {
            var (w, lodge, trees) = Forest(12);
            var site = (x: lodge.X + 4, y: lodge.Y);
            var second = Enumerable.Range(0, 30).Select(i => (x: lodge.X + 2 + i % 5, y: lodge.Y + 3 + i / 5)).Where(t => w.CanPlace("wood", t.x, t.y).Ok).FirstOrDefault();
            Assume.That(second != default, "no room for a second lodge");
            var lodge2 = w.AddBld("wood", second.x, second.y, 2);
            foreach (var (x, y) in World.LPath(lodge.X, lodge.Y + 2, second.x, second.y)) if (w.Grid[y, x] == null && !w.IsSolid(x, y) && w.DecoMap[y, x] == Deco.None) w.AddBld("road", x, y);
            w.SetForestMode(lodge, ForestMode.Intensive); w.SetForestMode(lodge2, ForestMode.Intensive);
            w.Recalc();
            for (int d = 0; d < 200; d++)
            {
                Day(w);
                var seen = new HashSet<int>();
                foreach (var l in new[] { lodge, lodge2 })
                    foreach (var a in l.Crew)
                        if (a.St != CrewState.Home && a.Stand >= 0) Assert.IsTrue(seen.Add(World.Tk(a.Tx, a.Ty)), $"day {d}: two woodcutters are on the tree at {a.Tx},{a.Ty}");
            }
        }

        [Test]
        public void ForestSettingsAndOrdersSurviveSaveAndLoad()
        {
            var (w, lodge, trees) = Forest(20);
            w.SetForestMode(lodge, ForestMode.Intensive);
            w.ProtectWoodland(trees[0].x, trees[0].y); w.DesignateClearing(trees[1].x, trees[1].y);
            w.Replant.Add(World.Tk(trees[2].x, trees[2].y));
            var loaded = World.LoadJson(w.SaveJson());
            var l = loaded.Grid[lodge.Y, lodge.X];
            Assert.AreEqual(ForestMode.Intensive, l.Mode);
            Assert.IsTrue(loaded.IsProtected(trees[0].x, trees[0].y));
            Assert.IsTrue(loaded.IsClearing(trees[1].x, trees[1].y));
            Assert.IsTrue(loaded.Replant.Contains(World.Tk(trees[2].x, trees[2].y)));
            Assert.AreEqual(loaded.SaveJson(), World.LoadJson(loaded.SaveJson()).SaveJson(), "load, save, load, save is stable");
        }

        [Test]
        public void AnOldSaveWithNoModeLoadsAsSustainableAndAPaintedBoundaryKeepsTheOldRules()
        {
            var (w, lodge, trees) = Forest(8);
            foreach (var (x, y) in trees) w.ZoneMap[y, x] = Zone.Wood;
            w.Touch(); w.Recalc();
            Assert.IsTrue(w.UsesZone(lodge, Zone.Wood));
            Assert.IsTrue(w.MayHarvest(lodge), "painted Lumber tiles are an explicit felling order: no floor");
            Assert.AreEqual(ForestMode.Sustainable, World.LoadJson(w.SaveJson()).Grid[lodge.Y, lodge.X].Mode);
            for (int d = 0; d < 400; d++) Day(w);
            Assert.AreEqual(0, Count(w, Deco.Tree, trees), "all the painted trees were felled");
            Assert.AreEqual(0, w.Replant.Count, "a painted boundary does not replant (the old rule)");
        }

        [Test]
        public void ASiteSurveyCountsTreesProtectionAndNeighbours()
        {
            var (w, lodge, trees) = Forest(30);
            var s = w.SurveyLodgeSite(lodge.X, lodge.Y);
            Assert.AreEqual(30, s.Mature);
            Assert.Greater(s.SustainablePerDay, 0); Assert.Greater(s.ShortTermPerDay, 0);
            w.ProtectRect(trees[0].x, trees[0].y, trees[9].x, trees[9].y);
            var p = w.SurveyLodgeSite(lodge.X, lodge.Y);
            Assert.Less(p.Mature, 30, "protected trees are not counted as harvestable");
            Assert.Greater(p.ProtectedTrees, 0);
            var empty = new World(7); empty.ClaimAll();
            var far = AllTiles().First(t => Gentle(empty, t.x, t.y, 2) && Math.Abs(t.x - C) + Math.Abs(t.y - C) > 20);
            for (int j = far.y - Defs.WorkR - 2; j <= far.y + 2 + Defs.WorkR; j++) for (int i = far.x - Defs.WorkR - 2; i <= far.x + 2 + Defs.WorkR; i++)
                if (World.Inb(i, j) && empty.HasTree(i, j)) empty.DecoMap[j, i] = Deco.None;
            empty.Touch();
            var none = empty.SurveyLodgeSite(far.x, far.y);
            Assert.AreEqual(World.SiteLevel.Warning, none.Level); Assert.Greater(none.Notes.Count, 0);
        }
    }
}
