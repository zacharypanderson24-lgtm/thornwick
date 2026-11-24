using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class FoodReserveTests
    {
        [Test]
        public void PreservedSuppliesCountAndFeedVillagersWhenFreshFoodIsEmpty()
        {
            var w = new World(7);
            w.Blds.RemoveAll(b => b.T != "hall" && b.T != "house"); // Keep homes; remove producers.
            w.Touch();
            w.Peasants = 10;
            w.Soldiers = 0;
            w.ResAmt[Res.Food] = 0;
            w.ResAmt[Res.SmokedMeat] = 10;
            w.ResAmt[Res.Eggs] = 8;
            Assert.That(w.EdibleFoodStored, Is.EqualTo(12));
            w.Tick();
            Assert.That(w.Starving, Is.False);
            Assert.That(w.EdibleFoodStored, Is.EqualTo(10).Within(0.0001));
            w.Tick();
            Assert.That(w.Starving, Is.False);
            Assert.That(w.EdibleFoodStored, Is.EqualTo(8).Within(0.0001));
        }

        [Test]
        public void MixedFoodReserveSurvivesSavingWithoutDoubleCountingFreshFood()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 17;
            w.ResAmt[Res.SmokedMeat] = 9;
            w.ResAmt[Res.Eggs] = 6;
            var loaded = World.LoadJson(w.SaveJson());
            Assert.That(loaded.EdibleFoodStored, Is.EqualTo(27.5));
            Assert.That(loaded.EdibleFoodCapacity, Is.EqualTo(w.EdibleFoodCapacity));
        }
    }
}
