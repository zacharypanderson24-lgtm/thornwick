using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class StewardTests
    {
        static World Town() { var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign(); w.Tick(); return w; }

        [Test] public void EveryProposalWithASiteIsReallyPlaceable()
        {
            var w = Town();
            foreach (var p in Steward.Advise(w, 6).Where(p => p.Type != null))
            {
                var r = w.CanPlace(p.Type, p.X, p.Y);
                Assert.IsTrue(r.Ok || r.Reason.StartsWith("Not enough"), $"{p.Id} at {p.X},{p.Y}: {r.Reason}");
                Assert.IsTrue(w.WouldConnect(p.Type, p.X, p.Y), p.Id + " must touch a connected road");
                Assert.AreEqual(r.Ok, p.Affordable);
            }
        }

        [Test] public void AdviceIsRankedMostUrgentFirstAndCapped()
        {
            var w = Town();
            var list = Steward.Advise(w, 3);
            Assert.LessOrEqual(list.Count, 3);
            for (int i = 1; i < list.Count; i++) Assert.GreaterOrEqual(list[i - 1].Severity, list[i].Severity);
        }

        [Test] public void NoFoodAndNoFieldsAsksForFieldsFirst()
        {
            var w = Town();
            foreach (var b in w.Blds.Where(b => b.T == "farm" || b.T == "hunter" || b.T == "fisher" || b.T == "dock").ToList()) w.Remove(b.X, b.Y);
            var food = Steward.Advise(w, 6).FirstOrDefault(p => p.Id == "fields" || p.Id == "food");
            Assert.IsNotNull(food);
            Assert.AreEqual(3, food.Severity);
        }

        [Test] public void BuildingTheProposalPlacesIt()
        {
            var w = Town();
            var p = Steward.Advise(w, 6).FirstOrDefault(x => x.Type != null && x.Affordable);
            if (p == null) Assert.Ignore("nothing affordable to propose in this seed");
            int before = w.Blds.Count(b => b.T == p.Type);
            Assert.IsTrue(w.Place(p.Type, p.X, p.Y).Ok);
            Assert.AreEqual(before + 1, w.Blds.Count(b => b.T == p.Type));
        }

        [Test] public void EveryPathGivesAdviceAndSurvivalStaysFirst()
        {
            foreach (Focus f in System.Enum.GetValues(typeof(Focus)))
            {
                var list = Steward.Advise(Town(), 3, f);
                for (int i = 1; i < list.Count; i++) Assert.GreaterOrEqual(list[i - 1].Severity, list[i].Severity, f.ToString());
                foreach (var p in list.Where(p => p.Type != null)) Assert.IsTrue(Defs.PlacementSize(p.Type) == p.Size && Town().CanPlace(p.Type, p.X, p.Y).Reason != "Outside the map");
            }
        }

        [Test] public void FoodAdviceNamesOtherWaysForEachPath()
        {
            var w = Town();
            StringAssert.Contains("buy grain", Steward.FoodAlternatives(Focus.Trader, w));
            StringAssert.Contains("raider camp", Steward.FoodAlternatives(Focus.Warlord, w));
            StringAssert.Contains("fishing", Steward.FoodAlternatives(Focus.Seafarer, w));
        }

        [Test] public void CostTextNamesTheMaterials() => StringAssert.Contains("wood", Steward.CostText("farm"));

        [Test] public void TheFieldsPackageZonesRealFertileFarmland()
        {
            var w = Town();
            foreach (var b in w.Blds.Where(b => b.T == "farm" || b.T == "hunter" || b.T == "fisher" || b.T == "dock").ToList()) w.Remove(b.X, b.Y);
            var p = Steward.FieldsPackage(w, 3, "test");
            if (p == null) Assert.Ignore("no fertile patch beside a road in this seed");
            Assert.IsTrue(p.HasRect && p.HasSite);
            var (zoned, skipped, _) = w.ZoneRect(p.ZoneKind, p.ZX0, p.ZY0, p.ZX1, p.ZY1);
            Assert.GreaterOrEqual(zoned, (p.ZX1 - p.ZX0 + 1) * (p.ZY1 - p.ZY0 + 1) * 0.85);
            Assert.AreEqual(Zone.Farm, w.ZoneMap[p.ZY0, p.ZX0] == Zone.Farm ? Zone.Farm : w.ZoneMap[p.ZY1, p.ZX1]);
        }
    }
}
