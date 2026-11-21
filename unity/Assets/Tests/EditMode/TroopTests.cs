using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TroopTests
    {
        static World Barracks()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 300; w.ResAmt[Res.Stone] = 100; w.ResAmt[Res.Gold] = 300; w.ResAmt[Res.Ore] = 50; w.ResAmt[Res.Flax] = 50; w.ResAmt[Res.Food] = 300;
            int c = Defs.Start;
            var site = Enumerable.Range(0, 25 * 25).Select(i => (x: c - 12 + i % 25, y: c - 12 + i / 25)).First(t => w.CanPlace("barracks", t.x, t.y).Ok);
            Assert.IsTrue(w.Place("barracks", site.x, site.y).Ok);
            return w;
        }

        static void Run(World w, int days) { for (int i = 0; i < days; i++) { w.ResAmt[Res.Food] = 300; w.Tick(); } }

        [Test]
        public void ByDefaultOnlyInfantryAreTrainedWithTheOldCost()
        {
            var w = Barracks();
            Assert.AreEqual(0, w.ArcherPercent);
            Run(w, 12);
            Assert.Greater(w.Soldiers, 0);
            Assert.AreEqual(0, w.Archers);
            Assert.AreEqual(w.Soldiers, w.Infantry);
            Assert.AreEqual(w.Trained, w.Soldiers);
        }

        [Test]
        public void ArcherShareSetsTheMixOfNewRecruitsAndArchersCostFlax()
        {
            var w = Barracks();
            w.SetArcherPercent(100);
            double flax = w.ResAmt[Res.Flax];
            Run(w, 12);
            Assert.Greater(w.Archers, 0);
            Assert.AreEqual(w.Soldiers, w.Archers, "only archers at 100%");
            Assert.Less(w.ResAmt[Res.Flax], flax, "each archer takes flax");

            var half = Barracks();
            half.SetArcherPercent(50);
            Run(half, 40);
            Assert.GreaterOrEqual(half.Soldiers, 4);
            Assert.That(half.Archers, Is.InRange(half.Soldiers / 2 - 1, half.Soldiers / 2 + 1));
            Assert.AreEqual(half.Soldiers, half.Infantry + half.Archers);
        }

        [Test]
        public void ArchersWaitWithoutFlaxAndTheShareIsClampedAndSaved()
        {
            var w = Barracks();
            w.SetArcherPercent(250);
            Assert.AreEqual(100, w.ArcherPercent);
            w.ResAmt[Res.Flax] = 0;
            for (int i = 0; i < 12; i++) { w.ResAmt[Res.Flax] = 0; w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.AreEqual(0, w.Soldiers, "no flax, no archers (no fallback to infantry)");
            w.SetArcherPercent(30);
            Run(w, 20);
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(30, loaded.ArcherPercent);
            Assert.AreEqual(w.Archers, loaded.Archers);
            Assert.AreEqual(w.Soldiers, loaded.Soldiers);
            Assert.AreEqual(w.Infantry, loaded.Infantry);
        }

        [Test]
        public void OldSavesLoadAsAllInfantry()
        {
            var w = Barracks();
            Run(w, 12);
            var json = Newtonsoft.Json.Linq.JObject.Parse(w.SaveJson());
            json.Remove("archers"); json.Remove("archerPercent");
            var loaded = World.LoadJson(json.ToString());
            Assert.AreEqual(w.Soldiers, loaded.Infantry);
            Assert.AreEqual(0, loaded.Archers);
        }

        [Test]
        public void TroopsAreAStandingArmyTakenFromTheWorkforceAndNotRefilledByBirths()
        {
            var w = Barracks();
            w.Tick();
            w.Peasants = w.D.Civ;   // a full town
            int workers = w.Peasants;
            w.Soldiers = 0; w.Archers = 0;
            w.Blds.First(b => b.T == "barracks").Prog = World.TrainDays;
            Run(w, 1);
            Assert.AreEqual(1, w.Soldiers, "one recruit");
            Assert.AreEqual(workers - 1, w.Peasants, "the recruit left the workforce");
            Run(w, 60);
            Assert.LessOrEqual(w.Peasants + w.Soldiers, w.D.Civ, "troops keep their place in the population");
            Assert.Less(w.Peasants, workers, "births did not refill the workforce past the housing cap");
        }

        [Test]
        public void StarvationAndMissingBunksLoseTheBiggerGroup()
        {
            var w = Barracks();
            w.Soldiers = 5; w.Archers = 3;
            w.Peasants = 0;
            w.ResAmt[Res.Food] = 0;
            w.Tick();
            Assert.AreEqual(4, w.Soldiers);
            Assert.AreEqual(2, w.Archers, "archers were the bigger group");
            Assert.AreEqual(2, w.Infantry);
        }
    }
}
