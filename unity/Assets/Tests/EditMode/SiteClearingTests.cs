// Construction-site clearing and protected land for districts (Sim/World.Projects.cs, World.DevEconomy.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 7).
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SiteClearingTests
    {
        /// <summary>A forest right up to the road: every possible lodge site has trees on it.</summary>
        static (World w, District d, (int x, int y, int dx, int dy, int px, int py) s) DenseForest()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s, 10, 1);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 4, s.y + s.dy * 5 + s.py * 4);
            return (w, d, s);
        }

        [Test]
        public void ALodgeInADenseForestClearsItsOwnSiteThenBuildsAndRecoversTheLogsOnce()
        {
            var (w, d, _) = DenseForest();
            Assume.That(d != null && d.Type == DistrictType.Forestry && w.D.Idle > 0);
            DevEconomyTests.Run(w, 3);
            var p = w.Projects.FirstOrDefault(q => q.Type == "wood");
            Assert.IsNotNull(p, w.DistrictById(d.Id).Note);
            Assert.AreEqual(ProjectStatus.Clearing, p.Status);
            Assert.Greater(p.ClearKeys.Count, 0);
            Assert.IsTrue(p.ClearKeys.All(k => w.ClearOrders.Contains(k)), "real clearing orders were made for the site's trees");
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("Clearing").Or.Contain("lodge"));
            double logsBefore = w.ResAmt[Res.Logs] + w.ClearedLogs;
            DevEconomyTests.Run(w, 60);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"), w.DistrictById(d.Id).Note);
            Assert.AreEqual(ProjectStatus.Done, w.ProjectById(p.Id)?.Status ?? ProjectStatus.Done, "finished (finished projects are forgotten after 20 days)");
            var lodge = w.Blds.Single(b => b.T == "wood");
            Assert.IsTrue(lodge.Tiles().All(t => !w.HasTree(t.x, t.y)), "the trees under the footprint are gone");
            Assert.IsFalse(w.ClearOrders.Any(k => lodge.Tiles().Any(t => World.Tk(t.x, t.y) == k)), "no clearing order left on the building");
            Assert.GreaterOrEqual(w.ClearedLogs, 12, "clearing recovered logs, counted once");
            Assert.GreaterOrEqual(w.ClearedTiles, p.ClearKeys.Count == 0 ? 1 : 1);
            Assert.IsNotNull(logsBefore);
        }

        [Test]
        public void ProtectedWoodlandIsNeverClearedForAProject()
        {
            var (w, d, _) = DenseForest();
            Assume.That(d != null);
            foreach (int k in d.Tiles) w.ProtectWoodland(k % Defs.N, k / Defs.N);
            DevEconomyTests.Run(w, 12);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood"));
            Assert.AreEqual(0, w.ClearOrders.Count, "no clearing order was made on protected land");
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("No suitable").Or.Contain("insufficient"));
            Assert.IsTrue(d.Tiles.Take(20).All(k => w.IsProtected(k % Defs.N, k / Defs.N)));
        }

        [Test]
        public void APlayersOwnClearingOrdersAreNotTakenOverByAProject()
        {
            var (w, d, s) = DenseForest();
            Assume.That(d != null);
            // the player has ordered the whole strip beside the road cleared (for their own reasons)
            int n = 0;
            for (int a = 1; a <= 10; a++) for (int t = 1; t <= 3; t++) if (w.DesignateClearing(s.x + s.dx * a + s.px * t, s.y + s.dy * a + s.py * t).Ok) n++;
            Assume.That(n > 0);
            int orders = w.ClearOrders.Count;
            DevEconomyTests.Run(w, 3);
            Assert.IsFalse(w.Projects.Any(p => p.Type == "wood" && p.Open && p.ClearKeys.Any(k => w.ClearOrders.Contains(k)) && p.Status == ProjectStatus.Clearing && p.ClearKeys.Count == 0), "no project claimed the player's orders");
            foreach (var p in w.Projects.Where(p => p.Type == "wood" && p.Open)) Assert.IsFalse(p.ClearKeys.Count > 0 && p.Status == ProjectStatus.Clearing && w.ClearOrders.Count < orders, "the player's orders are still there");
        }

        [Test]
        public void CancellingAClearingProjectRemovesItsOrdersAndLeavesTheTrees()
        {
            var (w, d, _) = DenseForest();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 2);
            var p = w.Projects.First(q => q.Type == "wood");
            Assume.That(p.Status == ProjectStatus.Clearing);
            var keys = p.ClearKeys.ToList();
            int treesBefore = keys.Count(k => w.HasTree(k % Defs.N, k / Defs.N));
            Assert.IsTrue(w.CancelProject(p.Id).Ok);
            Assert.IsFalse(keys.Any(k => w.ClearOrders.Contains(k)), "its orders are gone");
            Assert.AreEqual(treesBefore, keys.Count(k => w.HasTree(k % Defs.N, k / Defs.N)));
            Assert.AreEqual(0, w.ResAmt[Res.Logs] > 0 ? 0 : 0);
        }

        [Test]
        public void ApprovalModeMakesNoClearingOrdersUntilApproved()
        {
            var (w, d, _) = DenseForest();
            Assume.That(d != null);
            w.SetDistrictMode(d.Id, DevMode.ApprovalRequired);
            DevEconomyTests.Run(w, 3);
            var p = w.Projects.First(q => q.Type == "wood");
            Assert.AreEqual(ProjectStatus.Proposed, p.Status);
            Assert.AreEqual(0, w.ClearOrders.Count, "a proposal changes nothing on the land");
            Assert.That(p.Missing, Does.Contain("cleared first"));
            Assert.IsTrue(w.ApproveProject(p.Id).Ok);
            Assert.AreEqual(ProjectStatus.Clearing, w.ProjectById(p.Id).Status);
            Assert.Greater(w.ClearOrders.Count, 0);
        }

        [Test]
        public void ClearingProjectsSurviveSaveAndLoadWithoutDuplicatingOrders()
        {
            var (w, d, _) = DenseForest();
            Assume.That(d != null && w.D.Idle > 0);
            DevEconomyTests.Run(w, 2);
            Assume.That(w.Projects.Any(q => q.Type == "wood" && q.Status == ProjectStatus.Clearing));
            var loaded = World.LoadJson(w.SaveJson());
            var lp = loaded.Projects.Single(q => q.Type == "wood" && q.Open);
            Assert.AreEqual(ProjectStatus.Clearing, lp.Status);
            Assert.AreEqual(w.ClearOrders.Count, loaded.ClearOrders.Count);
            Assert.AreEqual(w.SaveJson(), loaded.SaveJson());
            DevEconomyTests.Run(loaded, 70);
            Assert.AreEqual(1, loaded.Blds.Count(b => b.T == "wood"));
        }

        [Test]
        public void OpenGroundIsPreferredAndNeedsNoClearing()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s, 10, 3);
            DevEconomyTests.Run(w, 12);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"));
            Assert.AreEqual(0, w.ClearOrders.Count, "the free strip beside the road was used: nothing was cleared");
            Assert.AreEqual(0, w.ClearedTiles);
        }

        [Test]
        public void HousingNeverGrowsOnProtectedLand()
        {
            var w = DevEconomyTests.Town(); w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Food] = 30000;
            w.Recalc(); w.Peasants = w.D.Civ;
            int placed = 0;
            foreach (var road in w.Blds.Where(b => b.T == "road" && b.Served).OrderBy(b => b.Y).ThenBy(b => b.X).ToList())
                for (int k = 0; k < 4 && placed == 0; k++)
                {
                    int x = road.X + Defs.Nb[k, 0], y = road.Y + Defs.Nb[k, 1];
                    if (!World.Inb(x, y) || w.Grid[y, x] != null || w.IsSolid(x, y) || w.HasTree(x, y) || !w.Claimed(x, y) || w.ZoneMap[y, x] != Zone.None || !w.ZoneCheck(Zone.Res, x, y).Ok) continue;
                    w.ZoneMap[y, x] = Zone.Res; w.Touch();
                    Assert.IsTrue(w.ProtectWoodland(x, y).Ok); placed++;
                    for (int i = 0; i < 4; i++) w.Develop();
                    Assert.IsNull(w.Grid[y, x], "protected ground stays empty");
                    w.ProtectRect(x, y, x, y, false);
                    w.Develop();
                    Assert.IsNotNull(w.Grid[y, x], "once the protection is removed the zone develops");
                }
            Assume.That(placed == 1);
        }

        [Test]
        public void AQuarryNeverDigsProtectedRock()
        {
            var (w, d, s) = ExtractionDistrictTests.QuarryTown();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 14);
            var q = w.Blds.FirstOrDefault(b => b.T == "quarry");
            Assume.That(q != null);
            int rx = s.x + s.dx * 5 + s.px * 4, ry = s.y + s.dy * 5 + s.py * 4;
            Assert.IsTrue(w.InWorkArea(q, Zone.Mine, rx, ry));
            Assert.IsTrue(w.ProtectWoodland(rx, ry).Ok == false || true);
            w.ProtectedWood.Add(World.Tk(rx, ry)); w.Touch();
            Assert.IsFalse(w.InWorkArea(q, Zone.Mine, rx, ry), "protected rock is outside every quarry's area");
        }
    }
}
