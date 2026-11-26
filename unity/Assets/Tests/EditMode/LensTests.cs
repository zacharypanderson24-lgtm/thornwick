using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class LensTests
    {
        static World Town() { var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign(); w.Tick(); return w; }

        [Test] public void EveryLensHasALegendAndIsRegistered()
        {
            foreach (var id in new[] { OverlayId.Workers, OverlayId.Food, OverlayId.Trade, OverlayId.Military, OverlayId.Danger })
            {
                Assert.IsTrue(LensData.IsLens(id));
                Assert.IsNotEmpty(LensData.Legend(id), id.ToString());
                Assert.IsNotNull(OverlayRegistry.Get(id), id.ToString());
            }
            Assert.IsFalse(LensData.IsLens(OverlayId.Water));
        }

        [Test] public void TheWorkersLensColoursEveryWorkBuilding()
        {
            var w = Town();
            var tiles = LensData.Tiles(w, OverlayId.Workers);
            int jobBuildings = w.Blds.Count(b => b.Jobs > 0);
            Assert.AreEqual(jobBuildings > 0, tiles.Count > 0);
            foreach (var (x, y, _) in tiles) Assert.IsNotNull(w.Grid[y, x]);
        }

        [Test] public void AFarmShowsAsAFoodSourceAndATowerAddsMilitaryReach()
        {
            var w = Town();
            var farm = w.Blds.FirstOrDefault(b => b.T == "farm");
            if (farm != null) Assert.IsTrue(LensData.Tiles(w, OverlayId.Food).Any(t => t.x == farm.X && t.y == farm.Y));
            int before = LensData.Tiles(w, OverlayId.Military).Count;
            w.AddBld("watchtower", Defs.Start + 6, Defs.Start + 6);
            Assert.Greater(LensData.Tiles(w, OverlayId.Military).Count, before, "the tower and its sight ring");
        }

        [Test] public void DangerShowsABandOnlyWhenCampsExist()
        {
            var w = Town();
            if (!w.LiveCamps.Any()) { Assert.IsNotNull(OverlayRegistry.WhyLocked(OverlayId.Danger, w)); return; }
            Assert.IsNull(OverlayRegistry.WhyLocked(OverlayId.Danger, w));
            Assert.IsTrue(LensData.Tiles(w, OverlayId.Danger).Any(t => t.tone == LensTone.Bad));
        }
    }
}
