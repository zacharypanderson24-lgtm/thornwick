using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>The soldier counts the raid, camp and conquest code read (World.Soldiers / Archers) and how they follow the companies.</summary>
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
        public void ABarracksAloneNeverMakesSoldiers()
        {
            var w = Barracks();
            Run(w, 40);
            Assert.AreEqual(0, w.Soldiers, "soldiers only come from recruitment into a company");
            Assert.AreEqual(0, w.Mil.Companies.Count);
        }

        [Test]
        public void OldSavesLoadAsAllInfantryInGarrisonCompanies()
        {
            var w = Barracks();
            w.Soldiers = 5; w.Recalc();
            var json = Newtonsoft.Json.Linq.JObject.Parse(w.SaveJson());
            json.Remove("archers"); json.Remove("archerPercent"); json.Remove("military");
            var loaded = World.LoadJson(json.ToString());
            Assert.AreEqual(5, loaded.Infantry);
            Assert.AreEqual(0, loaded.Archers);
            Assert.AreEqual(5, loaded.Mil.Professionals, "the old soldiers became company members, nobody was added");
            Assert.AreEqual(w.Peasants, loaded.Peasants);
        }

        [Test]
        public void TroopsAreAStandingArmyTakenFromTheWorkforceAndNotRefilledByBirths()
        {
            var w = Barracks();
            w.Tick();
            w.Peasants = w.D.Civ;   // a full town
            int workers = w.Peasants;
            Assert.IsTrue(w.Mil.CreateCompany(MilitaryUnitType.Spearman).Ok);
            Assert.IsTrue(w.Mil.SetTarget(w.Mil.Companies[0].Id, 2).Ok);
            Run(w, 1);
            Assert.AreEqual(2, w.Soldiers, "two recruits arrive in a day");
            Assert.AreEqual(workers - 2, w.Peasants, "the recruits left the workforce");
            Run(w, 60);
            Assert.LessOrEqual(w.Peasants + w.Soldiers, w.D.Civ, "troops keep their place in the population");
            Assert.Less(w.Peasants, workers, "births did not refill the workforce past the housing cap");
        }

        [Test]
        public void StarvationLosesTheBiggerGroup()
        {
            var w = Barracks();
            w.Soldiers = 5; w.Archers = 3;
            w.Peasants = 0;
            w.ResAmt[Res.Food] = 0;
            w.Tick();
            Assert.AreEqual(4, w.Soldiers);
            Assert.AreEqual(2, w.Archers, "archers were the bigger group");
            Assert.AreEqual(2, w.Infantry);
            Assert.AreEqual(w.Soldiers, w.Mil.Professionals, "the companies lost the same men");
        }
    }
}
