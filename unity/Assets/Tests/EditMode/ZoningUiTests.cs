// The Zoning section, contextual overlays, demand indicators and the actionable outlook (DESIGN_ORGANIC_DEVELOPMENT.md phase 8).
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ZoningUiTests
    {
        static BuildCatalogue Real()
        {
            var c = new BuildCatalogue();
            foreach (var t in GameController.Tools.Where(t => BuildCatalogue.IsConstruction(t.Id)))
                c.Add(new CatalogueEntry { Id = t.Id, Label = t.Label, Description = t.Desc, Category = BuildCatalogue.CategoryOf(t.Id, t.Cat.ToString()) });
            return c;
        }

        [Test]
        public void ZoningIsTheFirstCatalogueSectionAndHoldsEveryEconomicZone()
        {
            var c = Real();
            Assert.AreEqual(CatalogueCategory.Zoning, c.VisibleCategories()[0]);
            Assert.AreEqual("Zoning", BuildCatalogue.Title(CatalogueCategory.Zoning));
            var ids = c.In(CatalogueCategory.Zoning).Select(e => e.Id).ToList();
            CollectionAssert.IsSupersetOf(ids, new[] { "zoneRes", "zoneRes2", "zoneFarm", "zoneWood", "zoneMine", "zoneQuarry", "zoneHunt", "zoneFish" });
            foreach (var t in GameController.Tools.Where(t => t.Zone != Zone.None && !World.IsLivestockZone(t.Zone))) CollectionAssert.Contains(ids, t.Id, "every paint-a-zone tool is in Zoning");
        }

        [Test]
        public void ManualBuildingsStayInTheirOwnCategoriesAndZoneToolsAreNotPlacement()
        {
            var c = Real();
            Assert.AreEqual(CatalogueCategory.Resources, BuildCatalogue.CategoryOf("lodge", "Forestry"));
            Assert.AreEqual(CatalogueCategory.Resources, BuildCatalogue.CategoryOf("quarry", "Mining"));
            Assert.AreEqual(CatalogueCategory.Resources, BuildCatalogue.CategoryOf("mine", "Mining"));
            Assert.AreEqual(CatalogueCategory.Food, BuildCatalogue.CategoryOf("farmhouse", "Farming"));
            Assert.AreEqual(CatalogueCategory.Food, BuildCatalogue.CategoryOf("hunter", "Hunting"));
            Assert.AreEqual(CatalogueCategory.Food, BuildCatalogue.CategoryOf("fisher", "Water"));
            foreach (var e in c.In(CatalogueCategory.Zoning)) Assert.IsTrue(GameController.Tools.First(t => t.Id == e.Id).Build == null, e.Id + " paints a zone, it does not place a building");
        }

        [Test]
        public void EveryEconomicZoneCardSaysWhatDevelopsWhatItNeedsGivesAndLimits()
        {
            foreach (var id in new[] { "zoneFarm", "zoneWood", "zoneMine", "zoneQuarry", "zoneHunt", "zoneFish" })
            {
                var t = GameController.Tools.First(x => x.Id == id);
                foreach (var part in new[] { "Needs:", "Gives:", "Behaviour:", "Limits:" }) StringAssert.Contains(part, t.Desc, id);
                StringAssert.DoesNotContain("optional", t.Label.ToLower(), id);
            }
        }

        [Test]
        public void EachZoneToolOpensItsOwnContextualOverlayWithRealData()
        {
            var w = new World(7);
            Assert.AreEqual(OverlayId.Water, OverlayRegistry.ForTool("zoneRes", w));
            Assert.AreEqual(OverlayId.Fertility, OverlayRegistry.ForTool("zoneFarm", w));
            Assert.AreEqual(OverlayId.Forest, OverlayRegistry.ForTool("zoneWood", w));
            Assert.AreEqual(OverlayId.Underground, OverlayRegistry.ForTool("zoneMine", w));
            Assert.AreEqual(OverlayId.Stone, OverlayRegistry.ForTool("zoneQuarry", w));
            Assert.AreEqual(OverlayId.Game, OverlayRegistry.ForTool("zoneHunt", w));
            Assert.IsNotNull(OverlayRegistry.Get(OverlayId.Districts));
            Assert.IsNotNull(OverlayRegistry.WhyLocked(OverlayId.Fishing, w), "no water: the fishing overlay is locked with a reason");
            Assert.AreEqual(OverlayId.None, OverlayRegistry.ForTool("zoneFish", w), "so the tool just works without it");
            var wet = new World(2, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Wetlands);
            Assert.AreEqual(OverlayId.Fishing, OverlayRegistry.ForTool("zoneFish", wet));
        }

        [Test]
        public void DemandIndicatorsAreComputedFromRealStocksWithAReason()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = 500; w.ResAmt[Res.Stone] = 500;
            var calm = w.DemandReport();
            Assert.AreEqual(new[] { "housing", "food", "timber", "stone", "minerals" }, calm.Select(i => i.Key).ToArray());
            Assert.AreEqual(DemandLevel.None, calm.Single(i => i.Key == "stone").Level);
            Assert.AreEqual(DemandLevel.None, calm.Single(i => i.Key == "minerals").Level, "no smithery, no ore demand");
            w.ResAmt[Res.Stone] = 5; w.ResAmt[Res.Wood] = 3; w.ResAmt[Res.Logs] = 0; w.ResAmt[Res.Food] = 5;
            var tight = w.DemandReport();
            Assert.AreEqual(DemandLevel.Critical, tight.Single(i => i.Key == "stone").Level);
            Assert.AreEqual(DemandLevel.Critical, tight.Single(i => i.Key == "timber").Level);
            Assert.AreEqual(DemandLevel.Critical, tight.Single(i => i.Key == "food").Level);
            foreach (var i in tight) Assert.IsNotEmpty(i.Reason, i.Key);
            StringAssert.Contains("5 stone", tight.Single(i => i.Key == "stone").Reason);
        }

        [Test]
        public void ASignificantDemandChangeProducesOneMessageWithTheReason()
        {
            var w = new World(7); w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Stone] = 500;
            w.Day = 7; w.Develop();
            int before = w.Events.Count;
            w.ResAmt[Res.Stone] = 3;
            w.Day = 14; w.Develop();
            Assert.That(w.Events.Skip(before), Has.Some.Contain("Stone demand is now"));
            int after = w.Events.Count;
            w.Day = 21; w.Develop();
            Assert.That(w.Events.Skip(after), Has.None.Contain("Stone demand"), "no repeat while it stays high");
            w.ResAmt[Res.Stone] = 900; w.Day = 28; w.Develop();
            Assert.That(w.Events, Has.Some.Contain("Stone demand has eased"));
        }

        [Test]
        public void TheEconomyMenuListsDemandWithReasons()
        {
            var w = new World(7);
            var rows = ManagementData.Stockpiles(w);
            Assert.AreEqual(5, rows.Count(r => r.Label.StartsWith("Demand: ")));
            Assert.IsTrue(rows.Where(r => r.Label.StartsWith("Demand: ")).All(r => r.Wide && r.Value.Length > 10));
        }

        [Test]
        public void ADistrictOutlookGivesRealNumbersAndSaysWhenThereIsNoRoad()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w);
            for (int a = 1; a <= 4; a++) for (int t = 1; t <= 3; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; w.ZoneMap[y, x] = Zone.Wood; if (t == 3) w.DecoMap[y, x] = Deco.Tree; }
            w.Touch();
            var d = w.DistrictAt(s.x + s.dx + s.px, s.y + s.dy + s.py);
            Assert.IsNotNull(d);
            StringAssert.Contains("mature trees", w.DistrictOutlook(d));
            StringAssert.Contains("4 mature trees", w.DistrictOutlook(d));
            // far from any road
            var far = (x: Defs.Start + 40, y: Defs.Start + 40);
            if (w.Claimed(far.x, far.y) && w.ZoneCheck(Zone.Hunt, far.x, far.y).Ok) { w.ZoneMap[far.y, far.x] = Zone.Hunt; w.Touch(); StringAssert.Contains("No connected road", w.DistrictOutlook(w.DistrictAt(far.x, far.y))); }
        }
    }
}
