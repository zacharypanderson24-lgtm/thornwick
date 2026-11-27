// Agricultural and Forestry districts (Sim/World.DevEconomy.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 3).
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class DevEconomyTests
    {
        /// <summary>A new road spur of `len` tiles leading away from the town over free ground, and a strip of free, claimed ground of `depth` tiles along one side.
        /// Returns the spur's start, its direction and the sideways direction. Everything here is test scenery built through the normal world calls.</summary>
        static (int x, int y, int dx, int dy, int px, int py) Spur(World w, int len = 10, int depth = 6)
        {
            foreach (var road in w.Blds.Where(b => b.T == "road" && b.Served).OrderBy(b => b.Y).ThenBy(b => b.X).ToList())
                for (int k = 0; k < 4; k++)
                {
                    int dx = Defs.Nb[k, 0], dy = Defs.Nb[k, 1], px = -dy, py = dx;
                    foreach (var side in new[] { 1, -1 })
                    {
                        bool ok = true;
                        for (int s = 1; s <= len && ok; s++)
                            for (int t = 0; t <= depth && ok; t++)
                            {
                                int x = road.X + dx * s + px * side * t, y = road.Y + dy * s + py * side * t;
                                ok = World.Inb(x, y) && w.Grid[y, x] == null && w.Claimed(x, y) && !w.IsSolid(x, y) && w.ZoneMap[y, x] == Zone.None && w.DecoMap[y, x] != Deco.Rock && w.DecoMap[y, x] != Deco.Ore && w.PenAt(x, y) == null;
                            }
                        if (!ok) continue;
                        for (int s2 = 1; s2 <= len; s2++) for (int t = 0; t <= depth; t++) w.DecoMap[road.Y + dy * s2 + py * side * t, road.X + dx * s2 + px * side * t] = Deco.None;   // scenery for the test: bare ground
                        for (int s = 1; s <= len; s++) w.AddBld("road", road.X + dx * s, road.Y + dy * s);
                        w.Recalc();
                        return (road.X, road.Y, dx, dy, px * side, py * side);
                    }
                }
            Assert.Fail("no room for a road spur");
            return default;
        }

        static void Forest(World w, (int x, int y, int dx, int dy, int px, int py) s, int len = 10, int treeFrom = 3)
        {
            for (int a = 1; a <= len; a++)
                for (int t = 1; t <= 6; t++)
                {
                    int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t;
                    if (t >= treeFrom) w.DecoMap[y, x] = Deco.Tree;
                    w.ZoneMap[y, x] = Zone.Wood;
                }
            w.Touch();
        }

        static void Farmland(World w, (int x, int y, int dx, int dy, int px, int py) s, int len = 10, int from = 3)
        {
            for (int a = 1; a <= len; a++)
                for (int t = from; t <= 6; t++)
                {
                    int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t;
                    w.Fert[y, x] = true; w.ZoneMap[y, x] = Zone.Farm;
                }
            w.Touch();
        }

        static World Town()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = 400; w.ResAmt[Res.Stone] = 100;
            w.Recalc();
            return w;
        }

        /// <summary>Fertility is derived from the land and the water; the test scenery asserts it again each day so the fields stay fields.</summary>
        static void KeepFertile(World w) { for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.ZoneMap[y, x] == Zone.Farm) w.Fert[y, x] = true; }

        static void Run(World w, int days)
        {
            for (int i = 0; i < days; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 2000); KeepFertile(w); w.Tick(); }
        }

        [Test]
        public void AForestryDistrictBuildsItsFirstLodgeWithoutAnyShortage()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            var d = w.Districts.Single(q => q.Type == DistrictType.Forestry && q.Area > 50);
            Assume.That(w.D.Idle > 0);
            Run(w, 12);
            var lodges = w.Blds.Where(b => b.T == "wood").ToList();
            Assert.AreEqual(1, lodges.Count, w.DistrictById(d.Id).Note);
            Assert.IsTrue(lodges[0].Organic); Assert.IsTrue(lodges[0].Zoned);
            Assert.AreEqual(1, w.Projects.Count(p => p.Type == "wood" && p.Status == ProjectStatus.Done));
            Assert.IsTrue(w.Blds.Single(b => b.T == "wood").Served, "it stands beside the spur road");
        }

        [Test]
        public void ThereIsNeverASecondLodgeWhileTheFirstHasEmptyJobsOrTimberIsStocked()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            Run(w, 12);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5);
            // a stocked store: no new lodge, and the district says why
            w.ResAmt[Res.Wood] = 500;
            for (int i = 0; i < 40; i++) { w.ResAmt[Res.Wood] = 500; w.ResAmt[Res.Food] = 2000; w.Tick(); }
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("timber is stocked").Or.Contain("Additional workers"));
        }

        [Test]
        public void AnUnstaffedLodgeBlocksAnother()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            Run(w, 12);
            var lodge = w.Blds.Single(b => b.T == "wood");
            w.ResAmt[Res.Wood] = 5; w.ResAmt[Res.Logs] = 0;   // short of timber
            lodge.Workers = 0;
            var d = w.DistrictAt(lodge.X, lodge.Y);
            // look on a day the district is due
            while ((w.Day + d.Id) % 7 != 0) { w.Day++; }
            w.Develop();
            if (lodge.Workers < lodge.Jobs) Assert.That(w.DistrictById(d.Id).Note, Does.Contain("Additional workers").Or.Contain("Limited by").Or.Contain("No suitable"));
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"));
        }

        [Test]
        public void ASparseForestIsRefusedWithTheRealNumbers()
        {
            var w = Town(); var s = Spur(w);
            for (int a = 1; a <= 6; a++) for (int t = 1; t <= 4; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; w.ZoneMap[y, x] = Zone.Wood; if (t == 4 && a < 4) w.DecoMap[y, x] = Deco.Tree; }
            w.Touch();
            var d = w.DistrictAt(s.x + s.dx + s.px, s.y + s.dy + s.py);
            w.Develop();
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("insufficient mature trees").And.Contain("3 in this district"));
        }

        [Test]
        public void ADisabledPausedOrApprovalDistrictObeysItsSettings()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            var d = w.Districts.Single(q => q.Type == DistrictType.Forestry && q.Area > 50);
            w.SetDistrictMode(d.Id, DevMode.Paused);
            Run(w, 10);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood")); Assert.That(w.DistrictById(d.Id).Note, Does.Contain("paused"));
            w.SetDistrictMode(d.Id, DevMode.ApprovalRequired);
            Run(w, 3);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood"));
            var proposal = w.Projects.Single(p => p.Type == "wood");
            Assert.AreEqual(ProjectStatus.Proposed, proposal.Status);
            Assert.IsTrue(w.ApproveProject(proposal.Id).Ok);
            Run(w, 8);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"));
        }

        [Test]
        public void ADistrictLodgeWorksOnlyItsDistrictAndKeepsTheSustainableRules()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            Run(w, 12);
            var lodge = w.Blds.Single(b => b.T == "wood");
            Assert.IsFalse(w.UsesZone(lodge, Zone.Wood), "no painted-boundary felling order");
            int inX = s.x + s.dx * 5 + s.px * 5, inY = s.y + s.dy * 5 + s.py * 5;
            Assert.IsTrue(w.InWorkArea(lodge, Zone.Wood, inX, inY));
            Assert.IsFalse(w.InWorkArea(lodge, Zone.Wood, s.x + s.dx * 5 - s.px * 2, s.y + s.dy * 5 - s.py * 2), "outside the district it may not cut");
            Assert.AreEqual(ForestMode.Sustainable, lodge.Mode);
            Assert.IsTrue(w.MayHarvest(lodge), "a dense forest is above the floor");
        }

        [Test]
        public void ALodgeBuiltByHandInsideADistrictFollowsTheSameRules()
        {
            var w = Town(); var s = Spur(w); Forest(w, s, treeFrom: 4);
            w.Develop();   // (a project may be started; the manual one below is separate)
            foreach (var p in w.OpenProjects.ToList()) w.CancelProject(p.Id);
            Result placed = Result.No("none"); int x = 0, y = 0;
            for (int a = 3; a <= 9 && !placed.Ok; a++) for (int t = 1; t <= 2 && !placed.Ok; t++) { x = s.x + s.dx * a + s.px * t; y = s.y + s.dy * a + s.py * t; placed = w.CanPlace("wood", x, y); }
            Assume.That(placed.Ok, placed.Reason);
            Assert.IsTrue(w.Place("wood", x, y).Ok);
            Assert.IsTrue(w.Grid[y, x].Organic);
            Assert.AreEqual(1, w.DistrictAt(x, y).Buildings.Count(b => b.T == "wood"), "counted toward the district");
        }

        [Test]
        public void ALodgePlacedOutsideAnyDistrictKeepsTheOldBehaviour()
        {
            var w = Town();
            var b = w.AddBld("wood", Defs.Start + 10, Defs.Start + 10, 2);
            Assert.IsFalse(b.Organic);
            Assert.IsTrue(w.IsLegacyBuilding(b));
        }

        [Test]
        public void AFarmlandDistrictBuildsOneFarmhouseThatDoesNotEatTheFields()
        {
            var w = Town(); foreach (var f in w.Blds.Where(b => b.T == "farm").ToList()) w.Remove(f.X, f.Y); var s = Spur(w); Farmland(w, s);   // the starting farm would already work these fields
            var d = w.Districts.Single(q => q.Type == DistrictType.Agricultural && q.Tiles.Contains(Tk(s.x + s.dx * 3 + s.px * 4, s.y + s.dy * 3 + s.py * 4)));
            Assume.That(w.D.Idle > 0);
            Run(w, 14);
            var farms = w.DistrictById(d.Id).Buildings.Where(b => b.T == "farm").ToList();
            Assert.AreEqual(1, farms.Count, w.DistrictById(d.Id).Note);
            Assert.IsFalse(farms[0].Tiles().Any(t => w.ZoneMap[t.y, t.x] == Zone.Farm), "the farmhouse stands on the strip beside the road, not on a field");
            for (int i = 0; i < 40; i++) { w.ResAmt[Res.Food] = 3000; KeepFertile(w); w.Tick(); }
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "farm"), "no more farmhouses while food is plentiful");
        }

        static int Tk(int x, int y) => World.Tk(x, y);

        [Test]
        public void ATooSmallFieldIsRefusedAndSaysSo()
        {
            var w = Town(); var s = Spur(w);
            for (int a = 1; a <= 2; a++) for (int t = 3; t <= 4; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; w.Fert[y, x] = true; w.ZoneMap[y, x] = Zone.Farm; }
            w.Touch();
            var d = w.DistrictAt(s.x + s.dx + s.px * 3, s.y + s.dy + s.py * 3);
            Assume.That(d != null);
            w.Develop();
            Assert.AreEqual(0, w.DistrictById(d.Id).Buildings.Count(b => b.T == "farm"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("Not enough open farmland"));
        }

        [Test]
        public void ADistrictWithNoRoadSaysItIsBlocked()
        {
            var w = Town(); var s = Spur(w);
            // forest only on the far half, away from every road (no spur road: the spur ground is bare)
            for (int a = 6; a <= 10; a++) for (int t = 2; t <= 6; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; w.ZoneMap[y, x] = Zone.Wood; if (t >= 4) w.DecoMap[y, x] = Deco.Tree; }
            for (int a = 1; a <= 10; a++) { var rd = w.Grid[s.y + s.dy * a, s.x + s.dx * a]; if (rd != null && rd.T == "road") w.Remove(rd.X, rd.Y); }
            w.Touch(); w.Recalc();
            var d = w.DistrictAt(s.x + s.dx * 8 + s.px * 5, s.y + s.dy * 8 + s.py * 5);
            Assume.That(d != null);
            w.Develop();
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("no connected road").Or.Contain("No suitable").Or.Contain("insufficient"));
        }

        [Test]
        public void AProjectInProgressSurvivesSaveAndLoadAndFinishesOnce()
        {
            var w = Town(); var s = Spur(w); Forest(w, s);
            Assume.That(w.D.Idle > 0);
            Run(w, 3);
            var open = w.OpenProjects.Single(p => p.Type == "wood");
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, loaded.OpenProjects.Count(p => p.Type == "wood"));
            Assert.AreEqual(w.ResAmt[Res.Wood], loaded.ResAmt[Res.Wood], 1e-9);
            Run(loaded, 10);
            Assert.AreEqual(1, loaded.Blds.Count(b => b.T == "wood"));
            Assert.IsTrue(loaded.Blds.Single(b => b.T == "wood").Organic);
            var again = World.LoadJson(loaded.SaveJson());
            Assert.IsTrue(again.Blds.Single(b => b.T == "wood").Organic, "the organic flag is saved");
        }

        [Test]
        public void TheSameWorldGrowsTheSameWay()
        {
            string Go()
            {
                var w = Town(); var s = Spur(w); Forest(w, s);
                Run(w, 20);
                return string.Join("|", w.Blds.Where(b => b.T == "wood" || b.T == "farm").Select(b => $"{b.T}{b.X},{b.Y}")) + $"/{w.ResAmt[Res.Wood]:F1}";
            }
            Assert.AreEqual(Go(), Go());
        }
    }
}
