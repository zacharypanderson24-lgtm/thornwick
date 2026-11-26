using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TollTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static World Town()
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            Assert.That(w.Place("market", Defs.Start + 1, Defs.Start + 4).Ok, Is.True);
            w.ResAmt[Res.Gold] = 2000;
            return w;
        }

        static int GoldAfterSelling(int towers)
        {
            var w = Town();
            for (int i = 0; i < towers; i++) w.AddBld("watchtower", Defs.Start + 8 + 2 * i, Defs.Start + 8);
            var p = w.Settlements.Where(s => s.WillTrade).OrderBy(w.SettlementKm).First();
            w.ResAmt[p.Needs] = 100;
            Assert.IsTrue(w.SellTo(p, 30).Ok);
            var c = w.Caravans.Single();
            while (w.Day < c.ArriveDay) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            double before = w.ResAmt[Res.Gold];
            w.StepCaravansNow();
            return (int)(w.ResAmt[Res.Gold] - before);
        }

        [Test] public void TowersSetTheTollRateUpToFour()
        {
            var w = Town();
            Assert.AreEqual(0, w.TollRate, 1e-9);
            for (int i = 0; i < 6; i++) w.AddBld("watchtower", Defs.Start + 8 + 2 * i, Defs.Start + 8);
            Assert.AreEqual(4, w.RoadTowers);
            Assert.AreEqual(0.16, w.TollRate, 1e-9);
        }

        [Test] public void WatchtowersAddTollGoldToEveryTradeThatComesHome()
        {
            int none = GoldAfterSelling(0), two = GoldAfterSelling(2);
            Assert.Greater(two, none, "two towers collect a toll on top of the sale");
        }
    }
}
