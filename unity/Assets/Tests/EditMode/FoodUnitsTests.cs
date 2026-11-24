// One villager eats 1 food a day: the units, and loading a save made in the old units (Sim/World.FoodUnits.cs).
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class FoodUnitsTests
    {
        [Test]
        public void AVillagerEatsOneFoodADayAndASoldierOneAndAHalf()
        {
            var w = new World(7);
            w.Peasants = 20; w.Soldiers = 4;
            Assert.AreEqual(20 + 4 * 1.5, w.DailyFoodDemand, 1e-9);
        }

        [Test]
        public void ClassicStyleKeepsTheOldFigureForTheBrowserParityTests()
        {
            var w = new World(7, TerrainStyle.Classic);
            w.Peasants = 20; w.Soldiers = 0;
            Assert.AreEqual(20 * 0.2, w.DailyFoodDemand, 1e-9);
        }

        [Test]
        public void ANewSaveKeepsItsFoodExactly()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 321; w.ResAmt[Res.SmokedMeat] = 40;
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(321, back.ResAmt[Res.Food], 1e-9);
            Assert.AreEqual(40, back.ResAmt[Res.SmokedMeat], 1e-9);
        }

        [Test]
        public void ASaveFromBeforeTheRescaleGetsItsFoodMultipliedByFive()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 100; w.ResAmt[Res.SmokedMeat] = 8;
            var o = JObject.Parse(w.SaveJson());
            o.Remove("foodUnits");   // what a save from before the change looks like
            var old = World.LoadJson(o.ToString());
            Assert.AreEqual(500, old.ResAmt[Res.Food], 1e-9, "100 old food is 500 new food: the same number of days of eating");
            Assert.AreEqual(40, old.ResAmt[Res.SmokedMeat], 1e-9);
            // and it saves in the new units, so loading it again does not multiply it again
            var again = World.LoadJson(old.SaveJson());
            Assert.AreEqual(500, again.ResAmt[Res.Food], 1e-9);
        }

        [Test]
        public void AnOldSavesDaysOfFoodAreUnchanged()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 12;
            w.Peasants = 6;
            // in the old units 6 villagers ate 1.2 a day; 12 food lasted 10 days
            var o = JObject.Parse(w.SaveJson());
            o.Remove("foodUnits");
            var old = World.LoadJson(o.ToString());
            Assert.AreEqual(10, old.ResAmt[Res.Food] / old.DailyFoodDemand, 0.01);
        }

        [Test]
        public void ClassicSavesAreNotRescaled()
        {
            var w = new World(7, TerrainStyle.Classic);
            w.ResAmt[Res.Food] = 77;
            var o = JObject.Parse(w.SaveJson());
            o.Remove("foodUnits");
            Assert.AreEqual(77, World.LoadJson(o.ToString()).ResAmt[Res.Food], 1e-9);
        }
    }
}
