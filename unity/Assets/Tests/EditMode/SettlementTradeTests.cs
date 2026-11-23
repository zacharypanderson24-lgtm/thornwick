using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SettlementTradeTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        /// <summary>A town that knows its world's settlements, optionally with a road-connected market.</summary>
        static World Town(bool market = true)
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            if (market) Assert.That(w.Place("market", Defs.Start + 1, Defs.Start + 4).Ok, Is.True);
            w.ResAmt[Res.Gold] = 2000;
            return w;
        }

        /// <summary>The nearest settlement that will trade with you (so the caravan is quick).</summary>
        static Settlement Partner(World w) => w.Settlements.Where(s => s.WillTrade).OrderBy(w.SettlementKm).First();

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); } }

        [Test]
        public void TradeNeedsAMarketAndAWillingSettlement()
        {
            var bare = Town(market: false);
            var p = Partner(bare);
            StringAssert.Contains("Market", bare.CanTrade(p).Reason);
            var w = Town();
            Assert.IsTrue(w.CanTrade(Partner(w)).Ok);
            var cold = w.Settlements.First(s => !s.WillTrade && s.Tier >= SettlementTier.Village);
            StringAssert.Contains("will not trade", w.CanTrade(cold).Reason);
            var hamlet = w.Settlements.First(s => s.Tier == SettlementTier.Hamlet);
            StringAssert.Contains("too small", w.CanTrade(hamlet).Reason);
        }

        [Test]
        public void BuyingPaysNowAndTheGoodsArriveAfterTheMarch()
        {
            var w = Town();
            var p = Partner(w);
            w.ResAmt[p.Makes] = 0;
            int attitude = p.Attitude, days = w.SettlementMarchDays(p);
            double price = World.PriceToBuy(p);
            var r = w.BuyFrom(p, 50);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(50, r.Count);
            Assert.AreEqual(2000 - (int)System.Math.Ceiling(50 * price), w.ResAmt[Res.Gold], 1e-9, "paid up front");
            Assert.AreEqual(1, w.Caravans.Count);
            Assert.AreEqual(w.Day + days, w.Caravans[0].ArriveDay);
            Assert.IsFalse(w.BuyFrom(p, 10).Ok, "one market sends one caravan at a time");
            Assert.AreEqual(World.MonthlyTrade(p) - 50, World.LeftToBuy(p));

            w.Events.Clear();
            var events = new List<string>();
            for (int i = 0; i < days + 1 && w.Caravans.Count > 0; i++) { w.ResAmt[Res.Food] = p.Makes == Res.Food ? 0 : 150; w.Tick(); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.AreEqual(0, w.Caravans.Count, "the caravan is home");
            Assert.IsTrue(events.Any(e => e.Contains($"home with 50 {World.GoodName(p.Makes)}")), string.Join(" | ", events));
            Assert.Greater(p.Attitude, attitude, "a finished trade warms them");
        }

        [Test]
        public void SellingSendsTheGoodsNowAndTheGoldComesBackLater()
        {
            var w = Town();
            var p = Partner(w);
            w.ResAmt[p.Needs] = 120;
            int gold = (int)System.Math.Floor(40 * World.PriceToSell(p));
            var r = w.SellTo(p, 40);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(80, w.ResAmt[p.Needs], 1e-9, "the goods leave at once");
            Assert.AreEqual(gold, w.Caravans.Single().Gold);
            double before = w.ResAmt[Res.Gold];
            int days = w.SettlementMarchDays(p);
            var events = new List<string>();
            for (int i = 0; i < days + 1 && w.Caravans.Count > 0; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(events.Any(e => e.Contains($"with {gold} gold for 40")), string.Join(" | ", events));
            Assert.AreEqual(0, w.Caravans.Count, "the caravan is back, the market is free again");
            w.ResAmt[p.Needs] = 5;
            Assert.IsFalse(w.SellTo(p, 50).Ok, "you cannot sell more than you have");
        }

        [Test]
        public void PricesFollowAttitudeAndTradeIsLimitedEachMonth()
        {
            var w = Town();
            var p = Partner(w);
            p.Attitude = 0; double buy0 = World.PriceToBuy(p), sell0 = World.PriceToSell(p);
            p.Attitude = 60;
            Assert.Less(World.PriceToBuy(p), buy0, "friends sell to you cheaper");
            Assert.Greater(World.PriceToSell(p), sell0, "and pay you more");
            Assert.Less(World.PriceToBuy(p), World.BasePrice(p.Makes), "what it makes is cheap there");
            Assert.Greater(World.PriceToSell(p), World.BasePrice(p.Needs), "what it needs is dear there");

            int month = World.MonthlyTrade(p);
            p.SoldThisMonth = month;
            Assert.IsFalse(w.BuyFrom(p, 10).Ok, "nothing left to sell this month");
            Assert.AreEqual(0, World.LeftToBuy(p));
            while (w.Day % World.SettlementMonth != 0) Days(w, 1);
            Days(w, 1);
            Assert.AreEqual(month, World.LeftToBuy(p), "a new month, a new supply");
            Assert.That(World.MonthlyTrade(w.Settlements.OrderByDescending(s => s.Population).First()), Is.GreaterThan(World.MonthlyTrade(p) - 1), "bigger places trade more");
        }

        [Test]
        public void GiftsWarmASettlementThatWillNotTradeYet()
        {
            var w = Town(market: false);
            var cold = w.Settlements.Where(s => !s.WillTrade && s.Tier >= SettlementTier.Village).OrderBy(w.SettlementKm).First();
            int before = cold.Attitude;
            Assert.IsTrue(w.SendGift(cold).Ok, "gifts need no market");
            Assert.AreEqual(2000 - World.GiftGold, w.ResAmt[Res.Gold], 1e-9);
            Days(w, w.SettlementMarchDays(cold) + 1);
            Assert.AreEqual(System.Math.Min(World.GiftCap, before + World.GiftWarmth), cold.Attitude);
            for (int i = 0; i < 60 && w.SendGift(cold).Ok; i++) { }
            Days(w, w.SettlementMarchDays(cold) + 1);
            Assert.LessOrEqual(cold.Attitude, World.GiftCap, "gifts alone only go so far");
            Assert.IsFalse(w.SendGift(cold).Ok);
            Assert.IsTrue(cold.WillTrade || Settlements.TradeThreshold(cold.Personality) > World.GiftCap, "enough gifts win it over");
        }

        [Test]
        public void CaravansAndThisMonthsTradeAreSaved()
        {
            var w = Town();
            var p = Partner(w);
            w.ResAmt[p.Needs] = 100;
            Assert.IsTrue(w.SellTo(p, 30).Ok);
            var c = w.Caravans.Single();
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, loaded.Caravans.Count);
            var lc = loaded.Caravans[0];
            Assert.AreEqual((c.SettlementId, c.Kind, c.Res, c.Amount, c.Gold, c.ArriveDay), (lc.SettlementId, lc.Kind, lc.Res, lc.Amount, lc.Gold, lc.ArriveDay));
            loaded.SetSettlements(Settlements.Generate(Map));
            Assert.AreEqual(30, loaded.SettlementById(p.Id).BoughtThisMonth, "what it bought this month is remembered");
        }
    }
}
