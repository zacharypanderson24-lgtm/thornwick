// The development queue and the shared engine's first user, housing (Sim/Projects.cs, World.Projects.cs, World.Develop.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 2).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ProjectTests
    {
        static World Crowded(double wood = 1000, double stone = 0)
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = wood; w.ResAmt[Res.Stone] = stone;
            w.Recalc();
            w.Peasants = w.D.Civ;   // full: homes are wanted
            return w;
        }

        /// <summary>Zone a free tile beside a served road at least `apart` tiles from every tile in `avoid`.</summary>
        static (int x, int y) ZoneBeside(World w, Zone kind, int apart = 0, params (int x, int y)[] avoid)
        {
            foreach (var road in w.Blds.Where(b => b.T == "road" && b.Served).OrderByDescending(b => b.Y).ThenBy(b => b.X))
                for (int k = 0; k < 4; k++)
                {
                    int x = road.X + Defs.Nb[k, 0], y = road.Y + Defs.Nb[k, 1];
                    if (!World.Inb(x, y) || w.Grid[y, x] != null || w.IsSolid(x, y) || w.HasTree(x, y) || !w.Claimed(x, y) || w.ZoneMap[y, x] != Zone.None) continue;
                    if (avoid.Any(a => System.Math.Max(System.Math.Abs(a.x - x), System.Math.Abs(a.y - y)) < apart)) continue;
                    if (!w.ZoneCheck(kind, x, y).Ok) continue;
                    w.ZoneMap[y, x] = kind; w.Touch();
                    return (x, y);
                }
            Assert.Fail("no free tile beside a served road");
            return (0, 0);
        }

        static Dictionary<Res, int> Wood(int n) => new Dictionary<Res, int> { [Res.Wood] = n };

        [Test]
        public void HousingStillAppearsTheDayItIsFundedAndGoesThroughAProject()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.Develop();
            Assert.IsNotNull(w.Grid[y, x], w.Note[Zone.Res]);
            var p = w.Projects.Single();
            Assert.AreEqual(ProjectStatus.Done, p.Status); Assert.AreEqual("house", p.Type);
            Assert.IsNotEmpty(p.Reason);
            Assert.AreEqual(100 - Defs.Cost["house"][Res.Wood], w.ResAmt[Res.Wood], 1e-9, "paid once");
            Assert.IsEmpty(p.Held);
        }

        [Test]
        public void ADisabledOrPausedDistrictBuildsNothingNewAndSaysWhy()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            var d = w.DistrictAt(x, y);
            w.SetDistrictPriority(d.Id, DevPriority.Disabled);
            w.Develop();
            Assert.IsNull(w.Grid[y, x]); Assert.That(d.Note, Does.Contain("disabled"));
            w.SetDistrictPriority(d.Id, DevPriority.Normal); w.SetDistrictMode(d.Id, DevMode.Paused);
            w.Develop();
            Assert.IsNull(w.Grid[y, x]); Assert.That(w.DistrictAt(x, y).Note, Does.Contain("paused"));
            w.SetDistrictMode(w.DistrictAt(x, y).Id, DevMode.Automatic);
            w.Develop();
            Assert.IsNotNull(w.Grid[y, x]);
        }

        [Test]
        public void ApprovalModeProposesFirstAndOnlyApprovalBuildsAndSpends()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.SetDistrictMode(w.DistrictAt(x, y).Id, DevMode.ApprovalRequired);
            w.Develop();
            var p = w.Projects.Single();
            Assert.AreEqual(ProjectStatus.Proposed, p.Status);
            Assert.IsNull(w.Grid[y, x]); Assert.AreEqual(100, w.ResAmt[Res.Wood], 1e-9, "a proposal holds nothing");
            w.Develop(); w.Develop();
            Assert.AreEqual(1, w.Projects.Count, "one proposal per district, not a pile of them");
            Assert.IsTrue(w.ApproveProject(p.Id).Ok);
            Assert.IsNotNull(w.Grid[y, x]);
            Assert.AreEqual(100 - Defs.Cost["house"][Res.Wood], w.ResAmt[Res.Wood], 1e-9);
            Assert.IsFalse(w.ApproveProject(p.Id).Ok, "approving twice does nothing");
        }

        [Test]
        public void ARejectedProposalDoesNotComeBackForAWhileAndSpendsNothing()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.SetDistrictMode(w.DistrictAt(x, y).Id, DevMode.ApprovalRequired);
            w.Develop();
            Assert.IsTrue(w.RejectProject(w.Projects.Single().Id).Ok);
            for (int i = 0; i < 5; i++) w.Develop();
            Assert.AreEqual(1, w.Projects.Count); Assert.IsNull(w.Grid[y, x]); Assert.AreEqual(100, w.ResAmt[Res.Wood], 1e-9);
        }

        [Test]
        public void ABuildingLimitCountsManualAndAutomaticBuildings()
        {
            var w = Crowded(wood: 200);
            var (x, y) = ZoneBeside(w, Zone.Res);
            var (x2, y2) = (x, y);
            for (int k = 0; k < 4; k++) { int nx = x + Defs.Nb[k, 0], ny = y + Defs.Nb[k, 1]; if (w.ZoneCheck(Zone.Res, nx, ny).Ok && w.Grid[ny, nx] == null && w.ZoneMap[ny, nx] == Zone.None) { w.ZoneMap[ny, nx] = Zone.Res; x2 = nx; y2 = ny; break; } }
            Assume.That((x2, y2) != (x, y));
            w.Touch();
            w.SetDistrictLimits(w.DistrictAt(x, y).Id, new DistrictLimits { MaxBuildings = 1 });
            w.Develop(); w.Develop(); w.Develop();
            Assert.AreEqual(1, new[] { w.Grid[y, x], w.Grid[y2, x2] }.Count(b => b != null), "the limit of one holds");
            Assert.That(w.DistrictAt(x, y).Note, Does.Contain("limit"));
        }

        [Test]
        public void TheConstructionReserveHoldsBackWood()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.Reserve.KeepWood = 95;
            w.Develop();
            Assert.IsNull(w.Grid[y, x]); Assert.That(w.DistrictAt(x, y).Note, Does.Contain("reserve")); Assert.AreEqual(100, w.ResAmt[Res.Wood], 1e-9);
            w.Reserve.KeepWood = 90; w.Develop();
            Assert.IsNotNull(w.Grid[y, x]);
        }

        [Test]
        public void ADistrictWithHigherPriorityWinsOverANearerOne()
        {
            var w = Crowded(wood: 100);
            var a = ZoneBeside(w, Zone.Res);
            var b = ZoneBeside(w, Zone.Res, 4, a);
            var da = w.DistrictAt(a.x, a.y); var db = w.DistrictAt(b.x, b.y);
            Assume.That(da.Id != db.Id);
            w.SetDistrictPriority(da.Id, DevPriority.Low); w.SetDistrictPriority(db.Id, DevPriority.Critical);
            w.Develop();
            Assert.IsNull(w.Grid[a.y, a.x]); Assert.IsNotNull(w.Grid[b.y, b.x], "the critical district grows first");
        }

        [Test]
        public void ACancelNeverCreatesResourcesAndReturnsOnlyWhatWasNotUsed()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            Assume.That(w.D.Idle > 0);
            var d = w.DistrictAt(x, y);
            var (p, why) = w.StartProject(d, "house", x, y, 1, 1, Wood(10), 4, "test", "");
            Assert.IsNotNull(p, why);
            Assert.AreEqual(ProjectStatus.Building, p.Status); Assert.AreEqual(90, w.ResAmt[Res.Wood], 1e-9); Assert.AreEqual(10, p.Held[Res.Wood]);
            w.StepProjects(); w.StepProjects();
            Assert.AreEqual(2, p.Progress);
            Assert.IsTrue(w.CancelProject(p.Id).Ok);
            Assert.AreEqual(95, w.ResAmt[Res.Wood], 1e-9, "half the work was done: half the wood is gone, half comes back");
            Assert.IsEmpty(p.Held); Assert.IsFalse(w.CancelProject(p.Id).Ok, "a second cancel returns nothing");
            Assert.AreEqual(95, w.ResAmt[Res.Wood], 1e-9);
            Assert.IsNull(w.Grid[y, x]);
        }

        [Test]
        public void ACancelBeforeWorkStartsReturnsEverything()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            var p = w.StartProject(w.DistrictAt(x, y), "house", x, y, 1, 1, Wood(10), 4, "test", "").project;
            w.CancelProject(p.Id);
            Assert.AreEqual(100, w.ResAmt[Res.Wood], 1e-9);
        }

        [Test]
        public void TwoProjectsCannotShareASiteOrTheSameMaterials()
        {
            var w = Crowded(wood: 15);
            var (x, y) = ZoneBeside(w, Zone.Res);
            var d = w.DistrictAt(x, y);
            var first = w.StartProject(d, "house", x, y, 1, 1, Wood(10), 4, "a", "");
            Assert.IsNotNull(first.project);
            Assert.IsNull(w.StartProject(d, "house", x, y, 1, 1, Wood(10), 4, "b", "").project, "same site");
            var (x2, y2) = ZoneBeside(w, Zone.Res, 3, (x, y));
            var second = w.StartProject(w.DistrictAt(x2, y2), "house", x2, y2, 1, 1, Wood(10), 4, "c", "").project;
            Assert.AreEqual(ProjectStatus.Waiting, second.Status, "only 5 wood is left: the first project already holds the rest");
            Assert.That(second.Missing, Does.Contain("5 wood"));
            Assert.AreEqual(5, w.ResAmt[Res.Wood], 1e-9);
        }

        [Test]
        public void SavingMidConstructionKeepsOneProjectItsHeldMaterialsAndFinishesOnce()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            Assume.That(w.D.Idle > 0);
            var p = w.StartProject(w.DistrictAt(x, y), "house", x, y, 1, 1, Wood(10), 3, "test", "").project;
            w.StepProjects();
            string json = w.SaveJson();
            var loaded = World.LoadJson(json);
            var lp = loaded.Projects.Single();
            Assert.AreEqual(ProjectStatus.Building, lp.Status); Assert.AreEqual(1, lp.Progress); Assert.AreEqual(10, lp.Held[Res.Wood]);
            Assert.AreEqual(w.ResAmt[Res.Wood], loaded.ResAmt[Res.Wood], 1e-9, "loading replenishes nothing");
            Assert.AreEqual(json, loaded.SaveJson());
            loaded.StepProjects(); loaded.StepProjects(); loaded.StepProjects();
            Assert.IsNotNull(loaded.Grid[y, x]); Assert.AreEqual(ProjectStatus.Done, loaded.Projects.Single().Status);
            Assert.AreEqual(1, loaded.Blds.Count(b => b.X == x && b.Y == y));
            Assert.AreEqual(w.ResAmt[Res.Wood], loaded.ResAmt[Res.Wood], 1e-9, "the held wood was used, not spent twice");
        }

        [Test]
        public void AProposalSurvivesSaveAndLoad()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.SetDistrictMode(w.DistrictAt(x, y).Id, DevMode.ApprovalRequired);
            w.Develop();
            var loaded = World.LoadJson(w.SaveJson());
            var lp = loaded.Projects.Single();
            Assert.AreEqual(ProjectStatus.Proposed, lp.Status);
            Assert.IsTrue(loaded.ApproveProject(lp.Id).Ok);
            Assert.IsNotNull(loaded.Grid[y, x]);
        }

        [Test]
        public void ThePlayerCanReorderAndPauseTheQueue()
        {
            var w = Crowded(wood: 100);
            Assume.That(w.D.Idle > 1);
            var a = ZoneBeside(w, Zone.Res); var b = ZoneBeside(w, Zone.Res, 4, a);
            var pa = w.StartProject(w.DistrictAt(a.x, a.y), "house", a.x, a.y, 1, 1, Wood(10), 3, "a", "").project;
            var pb = w.StartProject(w.DistrictAt(b.x, b.y), "house", b.x, b.y, 1, 1, Wood(10), 3, "b", "").project;
            Assert.AreEqual(pa.Id, w.DevQueue()[0].Id);
            w.RankProject(pb.Id, 2);
            Assert.AreEqual(pb.Id, w.DevQueue()[0].Id);
            w.PauseProject(pa.Id, true);
            int before = pa.Progress; w.StepProjects();
            Assert.AreEqual(before, pa.Progress, "a paused project does not advance"); Assert.AreEqual(1, pb.Progress);
            Assert.That(w.ProjectLine(pa), Does.Contain("paused"));
        }

        [Test]
        public void TheSameDaysGiveTheSameHomes()
        {
            string Run()
            {
                var w = Crowded(wood: 300);
                ZoneBeside(w, Zone.Res); ZoneBeside(w, Zone.Res); ZoneBeside(w, Zone.Res2);
                for (int i = 0; i < 12; i++) { w.Develop(); w.Day++; w.Recalc(); w.Peasants = w.D.Civ; }
                return string.Join("|", w.Blds.Where(b => b.T == "house").OrderBy(b => b.Y).ThenBy(b => b.X).Select(b => $"{b.X},{b.Y},{b.Tier}")) + $"/{w.ResAmt[Res.Wood]:F1}";
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
