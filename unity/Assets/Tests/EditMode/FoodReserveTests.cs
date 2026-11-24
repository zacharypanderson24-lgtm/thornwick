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
            w.ResAmt[Res.SmokedMeat] = 50;
            w.ResAmt[Res.Eggs] = 40;
            Assert.That(w.EdibleFoodStored, Is.EqualTo(90));   // 50 smoked meat + 40 eggs (an egg is a food)
            w.Tick();
            Assert.That(w.Starving, Is.False);
            Assert.That(w.EdibleFoodStored, Is.EqualTo(80).Within(0.0001));   // ten villagers eat 10 a day
            w.Tick();
            Assert.That(w.Starving, Is.False);
            Assert.That(w.EdibleFoodStored, Is.EqualTo(70).Within(0.0001));
        }

        [Test]
        public void MixedFoodReserveSurvivesSavingWithoutDoubleCountingFreshFood()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 85;
            w.ResAmt[Res.SmokedMeat] = 45;
            w.ResAmt[Res.Eggs] = 6;
            var loaded = World.LoadJson(w.SaveJson());
            Assert.That(loaded.EdibleFoodStored, Is.EqualTo(136));   // 85 + 45 + 6 eggs
            Assert.That(loaded.EdibleFoodCapacity, Is.EqualTo(w.EdibleFoodCapacity));
        }
    }
}
