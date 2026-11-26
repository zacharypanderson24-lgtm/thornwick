using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class FrontierTests
    {
        [Test] public void DestroyedCampsPayAMonthlyIncomeFromTheirStrength()
        {
            var map = WorldMap.Generate(7);
            var w = new World(7);
            w.SetCamps(RaiderCamps.Generate(map), map.HomeSite.X, map.HomeSite.Y);
            Assert.AreEqual(0, w.FrontierIncome, "no camp destroyed: no income");
            var a = w.Camps[0]; var b = w.Camps[1];
            w.DestroyedCamps.Add(a.Id);
            Assert.AreEqual(a.Strength * World.FrontierGoldPerStrength, w.FrontierIncome);
            w.DestroyedCamps.Add(b.Id);
            Assert.AreEqual((a.Strength + b.Strength) * World.FrontierGoldPerStrength, w.FrontierIncome);
            Assert.AreEqual(2, w.FrontierCamps);
        }

        [Test] public void TheIncomeSurvivesSaveAndLoad()
        {
            var map = WorldMap.Generate(7);
            var w = new World(7);
            w.SetCamps(RaiderCamps.Generate(map), map.HomeSite.X, map.HomeSite.Y);
            w.DestroyedCamps.Add(w.Camps[0].Id);
            var loaded = World.LoadJson(w.SaveJson());
            loaded.SetCamps(RaiderCamps.Generate(map), map.HomeSite.X, map.HomeSite.Y);
            Assert.AreEqual(w.FrontierIncome, loaded.FrontierIncome);
        }
    }
}
