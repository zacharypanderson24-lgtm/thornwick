// District controls (Sim/World.DistrictControls.cs, Game/Hud.Districts.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 6): the advanced limits, the yearly spending limit, the
// production target, the district's harvest setting, and the Districts panel driven by real clicks through the Hud.
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class DistrictControlTests
    {
        static (World w, District d, (int x, int y, int dx, int dy, int px, int py) s) ForestTown()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5);
            return (w, d, s);
        }

        [Test]
        public void AYearlySpendingLimitStopsAProjectAndSaysHowMuchIsLeft()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null && d.Type == DistrictType.Forestry);
            w.SetDistrictLimits(d.Id, new DistrictLimits { MaxSpend = 3 });   // a lodge costs 6
            for (int i = 0; i < 4; i++) w.Develop();
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "wood"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("Spending limit reached").And.Contain("0 of 3"));
            w.SetDistrictLimits(d.Id, new DistrictLimits { MaxSpend = 10 });
            DevEconomyTests.Run(w, 8);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"));
            Assert.AreEqual(Defs.Cost["wood"][Res.Wood], w.SpentThisYear(w.DistrictById(d.Id)));
            w.Day += 400;
            Assert.AreEqual(0, w.SpentThisYear(w.DistrictById(d.Id)), "the log forgets spending older than a year");
        }

        [Test]
        public void AProductionTargetStopsGrowthOnceReached()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            w.SetDistrictLimits(d.Id, new DistrictLimits { ProductionTarget = 1 });
            DevEconomyTests.Run(w, 10);
            Assume.That(w.Blds.Count(b => b.T == "wood") == 1);
            var lodge = w.Blds.Single(b => b.T == "wood"); lodge.Workers = lodge.Jobs;
            w.Touch();
            lodge.Workers = lodge.Jobs;
            Assert.GreaterOrEqual(w.DistrictOutputPerDay(w.DistrictById(d.Id)), 1.0 - 1e-9);
            Assert.That(w.DevGate(w.DistrictById(d.Id)), Does.Contain("Production target reached"));
        }

        [Test]
        public void TheDistrictHarvestSettingIsGivenToNewLodgesAndClearingIsRefused()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            Assert.IsFalse(w.SetDistrictLimits(d.Id, new DistrictLimits { HarvestMode = ForestMode.Clearing }).Ok, "permanent clearing is never a silent default");
            Assert.IsTrue(w.SetDistrictLimits(d.Id, new DistrictLimits { HarvestMode = ForestMode.Intensive }).Ok);
            DevEconomyTests.Run(w, 10);
            Assert.AreEqual(ForestMode.Intensive, w.Blds.Single(b => b.T == "wood").Mode);
            Assert.That(w.DistrictProblems(w.DistrictById(d.Id)), Has.Some.Contain("Intensive"));
        }

        [Test]
        public void LimitsHarvestSettingAndSpendingSurviveSaveAndLoad()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            w.SetDistrictLimits(d.Id, new DistrictLimits { MaxBuildings = 4, MaxSpend = 50, HarvestMode = ForestMode.Intensive });
            DevEconomyTests.Run(w, 10);
            var loaded = World.LoadJson(w.SaveJson());
            var ld = loaded.DistrictById(d.Id);
            Assert.AreEqual(4, ld.Limits.MaxBuildings); Assert.AreEqual(50, ld.Limits.MaxSpend); Assert.AreEqual(ForestMode.Intensive, ld.Limits.HarvestMode);
            Assert.AreEqual(w.SpentThisYear(w.DistrictById(d.Id)), loaded.SpentThisYear(ld));
            Assert.AreEqual(w.SaveJson(), loaded.SaveJson());
        }

        [Test]
        public void ProblemsAreListedInPlainWords()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 10);
            var dd = w.DistrictById(d.Id);
            w.SetDistrictMode(dd.Id, DevMode.Paused);
            Assert.That(w.DistrictProblems(dd), Has.Some.Contain("paused"));
        }

        // ---------------- the panel ----------------
        GameObject _go; Hud _hud; Camera _cam;

        void OpenPanel(World w)
        {
            _go = new GameObject("DistrictPanelTest"); _hud = _go.AddComponent<Hud>();
            _hud.Build(GameController.Tools, GameController.Cats, new ArtLibrary());
            _hud.SetSettlement(w); _hud.RefreshWorkforce(w); _hud.RefreshTownMenus(w);
            _cam = UiHarness.Attach(_go, 1920, 1080);
        }

        [TearDown]
        public void Down() { if (_go != null) { UiHarness.Detach(_go, _cam); Object.DestroyImmediate(_go); _go = null; } }

        bool Click(string control)
        {
            var rt = _hud.DistrictControl(control); Assert.IsNotNull(rt, "no control named " + control);
            return UiHarness.ClickCentre(_hud.HudCanvas, _cam, rt).Delivered;
        }

        [Test]
        public void ThePanelListsTheDistrictAndRealClicksChangePriorityAndMode()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            OpenPanel(w);
            _hud.ToggleDistricts(d.Id);
            Assert.IsTrue(_hud.DistrictsOpen); Assert.AreEqual(d.Id, _hud.DistrictSelected);
            Assert.IsNotNull(_hud.DistrictControl("Row:" + d.Id), "the district is in the list");
            Assert.IsTrue(Click("Prio:High")); Assert.AreEqual(DevPriority.High, w.DistrictById(d.Id).Priority);
            _hud.SetSettlement(w);
            Assert.IsTrue(Click("Mode:Paused")); Assert.AreEqual(DevMode.Paused, w.DistrictById(d.Id).Mode);
            _hud.SetSettlement(w);
            Assert.IsTrue(Click("Mode:Automatic")); Assert.AreEqual(DevMode.Automatic, w.DistrictById(d.Id).Mode);
        }

        [Test]
        public void TypedNumbersAndNamesAreAppliedExactlyAndRefusalsAreShown()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            OpenPanel(w); _hud.ToggleDistricts(d.Id);
            _hud.DistrictField("MaxBuildings").text = "3"; _hud.DistrictField("MaxSpend").text = "120"; _hud.DistrictField("Target").text = "7";
            Assert.IsTrue(Click("ApplyLimits"));
            var lim = w.DistrictById(d.Id).Limits;
            Assert.AreEqual(3, lim.MaxBuildings); Assert.AreEqual(120, lim.MaxSpend); Assert.AreEqual(7, lim.ProductionTarget);
            _hud.SetSettlement(w);
            Assert.AreEqual("3", _hud.DistrictField("MaxBuildings").text, "the field shows the saved value");
            _hud.DistrictField("Name").text = "The Kings Wood";
            Assert.IsTrue(Click("Rename"));
            Assert.AreEqual("The Kings Wood", w.DistrictById(d.Id).Name);
            _hud.SetSettlement(w);
            Assert.IsTrue(Click("Rename:Auto"));
            Assert.That(w.DistrictById(d.Id).Name, Does.Contain("Forestry District"));
            _hud.DistrictField("ResWood").text = "55"; Assert.IsTrue(Click("ApplyReserve"));
            Assert.AreEqual(55, w.Reserve.KeepWood);
        }

        [Test]
        public void ApprovalProposalsAreApprovedAndRejectedFromThePanel()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            w.SetDistrictMode(d.Id, DevMode.ApprovalRequired);
            DevEconomyTests.Run(w, 3);
            var p = w.Projects.Single(q => q.Type == "wood");
            OpenPanel(w); _hud.ToggleDistricts(d.Id);
            Assert.IsNotNull(_hud.DistrictControl("Approve:" + p.Id), "the proposal is listed with approve and reject");
            Assert.IsTrue(Click("Reject:" + p.Id));
            Assert.AreEqual(ProjectStatus.Rejected, w.ProjectById(p.Id).Status);
        }

        [Test]
        public void RemovingTheZoneFromThePanelKeepsBuildings()
        {
            var (w, d, _) = ForestTown();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 10);
            Assume.That(w.Blds.Any(b => b.T == "wood"));
            OpenPanel(w); _hud.ToggleDistricts(d.Id);
            Assert.IsTrue(Click("RemoveZone"));
            Assert.IsTrue(w.Blds.Any(b => b.T == "wood"), "the lodge stays");
        }
    }
}
