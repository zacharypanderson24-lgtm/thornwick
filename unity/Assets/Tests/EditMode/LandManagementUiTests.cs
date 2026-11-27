// The extraction overhaul in the interface: the Build catalogue lists lodges, quarries and mines under Natural Resources and the clearing, protection and boundary tools
// under Land Management; selecting them opens the overlay that helps; the lodge panel changes a lodge's mode through a real click; no description still tells the player to paint a zone first.
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class LandManagementUiTests
    {
        static BuildCatalogue Real()
        {
            var c = new BuildCatalogue();
            foreach (var t in GameController.Tools.Where(t => BuildCatalogue.IsConstruction(t.Id)))
                c.Add(new CatalogueEntry { Id = t.Id, Label = t.Label, Description = t.Desc, Category = BuildCatalogue.CategoryOf(t.Id, t.Cat.ToString()) });
            return c;
        }

        [Test]
        public void ExtractionBuildingsAreNaturalResourcesAndLandToolsAreLandManagement()
        {
            var c = Real();
            var res = c.In(CatalogueCategory.Resources).Select(e => e.Id).ToList();
            CollectionAssert.IsSupersetOf(res, new[] { "lodge", "quarry", "mine", "forester" });
            CollectionAssert.DoesNotContain(res, "zoneWood"); CollectionAssert.DoesNotContain(res, "zoneMine");
            var land = c.In(CatalogueCategory.Land).Select(e => e.Id).ToList();
            CollectionAssert.IsSupersetOf(land, new[] { "clearLand", "protectWood", "unprotectWood" });
            var zoning = c.In(CatalogueCategory.Zoning).Select(e => e.Id).ToList();
            CollectionAssert.IsSupersetOf(zoning, new[] { "zoneRes", "zoneRes2", "zoneFarm", "zoneWood", "zoneMine", "zoneQuarry", "zoneHunt", "zoneFish" });
            CollectionAssert.DoesNotContain(land, "zoneWood");
            Assert.AreEqual(CatalogueCategory.Zoning, c.VisibleCategories()[0], "Zoning is the first section");
            Assert.AreEqual("Land Management", BuildCatalogue.Title(CatalogueCategory.Land));
            CollectionAssert.IsSupersetOf(c.In(CatalogueCategory.Industry).Select(e => e.Id).ToList(), new[] { "sawmill", "charcoal" });
        }

        [Test]
        public void ForestryToolsOpenTheForestOverlay()
        {
            var w = new World(7);
            foreach (var id in new[] { "lodge", "forester", "clearLand", "protectWood", "unprotectWood" }) Assert.AreEqual(OverlayId.Forest, OverlayRegistry.ForTool(id, w), id);
            Assert.AreEqual(OverlayId.Underground, OverlayRegistry.ForTool("mine", w));
            Assert.IsNull(OverlayRegistry.WhyLocked(OverlayId.Forest, w));
            Assert.IsNotNull(OverlayRegistry.Get(OverlayId.Forest));
        }

        [Test]
        public void NoDescriptionTellsThePlayerToPaintAZoneFirst()
        {
            foreach (var id in new[] { "lodge", "quarry", "mine" })
            {
                var t = GameController.Tools.First(x => x.Id == id);
                StringAssert.Contains("no zone to paint", t.Desc, id);
            }
            foreach (var id in new[] { "zoneFarm", "zoneWood", "zoneMine", "zoneQuarry", "zoneHunt", "zoneFish" }) { var zt = GameController.Tools.First(x => x.Id == id); foreach (var part in new[] { "Needs:", "Gives:", "Behaviour:", "Limits:" }) StringAssert.Contains(part, zt.Desc, id); }
            foreach (var op in new[] { "clear", "protect", "unprotect" }) Assert.AreEqual(1, GameController.Tools.Count(t => t.LandOp == op), op);
        }

        // ---- the lodge panel through the real Hud ----
        GameObject _go; Hud _hud; Camera _cam; World _w; Building _lodge;

        [SetUp]
        public void Up()
        {
            _w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); _w.StartCampaign(); _w.Tick();
            var spot = Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N)).First(t => _w.Claimed(t.x, t.y) && _w.CanPlace("wood", t.x, t.y, true).Ok);
            _lodge = _w.AddBld("wood", spot.x, spot.y, 2); _w.Recalc();
            _go = new GameObject("HudLodgeTest"); _hud = _go.AddComponent<Hud>();
            _hud.Build(GameController.Tools, GameController.Cats, new ArtLibrary());
            _hud.ForestModePicked += (b, m) => _w.SetForestMode(b, m);
            _hud.SetSettlement(_w);
            _hud.RefreshWorkforce(_w); _hud.RefreshTownMenus(_w);   // the game does these every frame
            _cam = UiHarness.Attach(_go, 1920, 1080);
        }

        [TearDown]
        public void Down() { UiHarness.Detach(_go, _cam); Object.DestroyImmediate(_go); }

        [Test]
        public void TheLodgePanelShowsTheModeAndChangesItWithARealClick()
        {
            _hud.ShowLodgePanel(_lodge); _hud.SetSettlement(_w);
            Assert.IsTrue(_hud.LodgePanelOpen);
            StringAssert.Contains("Sustainable", _hud.LodgeBodyText);
            Canvas.ForceUpdateCanvases();
            var res = UiHarness.ClickCentre(_hud.HudCanvas, _cam, _hud.LodgeModeButton(1));
            Assert.IsTrue(res.Delivered, "a click on the Intensive button reaches its handler: " + res);
            Assert.AreEqual(ForestMode.Intensive, _lodge.Mode);
            StringAssert.Contains("Intensive", _hud.LodgeBodyText);
            UiHarness.ClickCentre(_hud.HudCanvas, _cam, _hud.LodgeModeButton(2));
            Assert.AreEqual(ForestMode.Clearing, _lodge.Mode);
        }

        [Test]
        public void TheLodgePanelClosesWhenTheLodgeIsDemolishedAndOnEscape()
        {
            _hud.ShowLodgePanel(_lodge); _hud.SetSettlement(_w);
            Assert.IsTrue(_hud.EscapeManagement(false), "Escape closes the panel first");
            Assert.IsFalse(_hud.LodgePanelOpen);
            _hud.ShowLodgePanel(_lodge); _w.Remove(_lodge.X, _lodge.Y); _hud.SetSettlement(_w);
            Assert.IsFalse(_hud.LodgePanelOpen, "a demolished lodge has no panel");
        }
    }
}
