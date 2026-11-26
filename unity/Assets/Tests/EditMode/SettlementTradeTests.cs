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
            int amt = System.Math.Min(50, World.LeftToBuy(p));
            var r = w.BuyFrom(p, 50);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(amt, r.Count, "as much as it has to sell this month, up to what was asked");
            Assert.AreEqual(2000 - (int)System.Math.Ceiling(amt * price), w.ResAmt[Res.Gold], 1e-9, "paid up front");
            Assert.AreEqual(1, w.Caravans.Count);
            Assert.AreEqual(w.Day + days, w.Caravans[0].ArriveDay);
            Assert.IsFalse(w.BuyFrom(p, 10).Ok, "one market sends one caravan at a time");
            Assert.AreEqual(World.MonthlyTrade(p, p.Makes) - amt, World.LeftToBuy(p));

            w.Events.Clear();
            var events = new List<string>();
            for (int i = 0; i < days + 1 && w.Caravans.Count > 0; i++) { w.ResAmt[Res.Food] = p.Makes == Res.Food ? 0 : 150; w.Tick(); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.AreEqual(0, w.Caravans.Count, "the caravan is home");
            Assert.IsTrue(events.Any(e => e.Contains($"home with {amt} {World.GoodName(p.Makes)}")), string.Join(" | ", events));
            Assert.Greater(p.Attitude, attitude, "a finished trade warms them");
        }

        [Test]
        public void SellingSendsTheGoodsNowAndTheGoldComesBackLater()
        {
            var w = Town();
            var p = Partner(w);
            w.ResAmt[p.Needs] = 120;
            int amt = System.Math.Min(40, World.LeftToSell(p));
            int gold = (int)System.Math.Floor(amt * World.PriceToSell(p));
            var r = w.SellTo(p, 40);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(120 - amt, w.ResAmt[p.Needs], 1e-9, "the goods leave at once");
            Assert.AreEqual(gold, w.Caravans.Single().Gold);
            double before = w.ResAmt[Res.Gold];
            int days = w.SettlementMarchDays(p);
            var events = new List<string>();
            for (int i = 0; i < days + 1 && w.Caravans.Count > 0; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(events.Any(e => e.Contains($"with {gold} gold for {amt}")), string.Join(" | ", events));
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

            p.SoldOf[p.Makes] = World.MonthlyTrade(p, p.Makes);
            Assert.IsFalse(w.BuyFrom(p, 10).Ok, "nothing left to sell this month");
            Assert.AreEqual(0, World.LeftToBuy(p));
            while (w.Day % World.SettlementMonth != 0) Days(w, 1);
            Days(w, 1);
            Assert.AreEqual(World.MonthlyTrade(p, p.Makes), World.LeftToBuy(p), "a new month, a new supply (it may have grown a little)");
            Assert.That(World.MonthlyTrade(w.Settlements.OrderByDescending(s => s.Population).First()), Is.GreaterThan(World.MonthlyTrade(p) - 1), "bigger places trade more");
        }

        [Test]
        public void ASettlementTradesInEachOfItsGoods()
        {
            var w = Town();
            var p = w.Settlements.Where(s => s.WillTrade && s.Produce.Count >= 2 && s.Wants.Count >= 2).OrderBy(w.SettlementKm).First();
            var second = p.Produce[1].res;
            Assert.AreNotEqual(p.Makes, second);
            w.ResAmt[second] = 0;
            int left = World.LeftToBuy(p, second), main = World.LeftToBuy(p, p.Makes);
            Assert.Greater(left, 0, "it sells its side goods too");
            var r = w.BuyFrom(p, 5, second);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(second, w.Caravans.Single().Res);
            Assert.AreEqual(left - 5, World.LeftToBuy(p, second), "each good has its own monthly amount");
            Assert.AreEqual(main, World.LeftToBuy(p, p.Makes), "buying one good leaves the others alone");
            Assert.Less(World.PriceToBuy(p, p.Makes) / World.BasePrice(p.Makes), World.PriceToBuy(p, second) / World.BasePrice(second) + 1e-9,
                "its main good is the cheapest for its worth");

            var w2 = Town(); var q = w2.SettlementById(p.Id);
            var need = q.Wants[1].res;
            w2.ResAmt[need] = 50;
            Assert.IsTrue(w2.SellTo(q, 5, need).Ok, "it buys its side needs too");
            w2.Caravans.Clear();   // free the market's caravan for the checks below
            var notWanted = new[] { Res.Food, Res.Wood, Res.Stone, Res.Ore, Res.Flax, Res.Wool, Res.Metal, Res.Charcoal }.First(g => !q.Wanting(g));
            w2.ResAmt[notWanted] = 50;
            StringAssert.Contains("does not need", w2.SellTo(q, 5, notWanted).Reason);
            var notMade = new[] { Res.Food, Res.Wood, Res.Stone, Res.Ore, Res.Flax, Res.Wool, Res.Metal, Res.Charcoal }.First(g => !q.Produces(g));
            StringAssert.Contains("does not produce", w2.BuyFrom(q, 5, notMade).Reason);
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
            w.ResAmt[Res.Gold] = 2000;   // gold is capped by storage each day: top it up for the gifts
            for (int i = 0; i < 60 && w.SendGift(cold).Ok; i++) { }
            Days(w, w.SettlementMarchDays(cold) + 1);
            Assert.LessOrEqual(cold.Attitude, World.GiftCap, "gifts alone only go so far");
            Assert.IsFalse(w.SendGift(cold).Ok);
            Assert.IsTrue(cold.WillTrade || Settlements.TradeThreshold(cold.Personality) > World.GiftCap,
                $"enough gifts win it over: attitude {cold.Attitude}, needs {Settlements.TradeThreshold(cold.Personality)}, {cold.Tier}, {cold.Personality}, {w.Caravans.Count} still on the road, day {w.Day}");
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
            int amt = System.Math.Min(30, World.MonthlyTrade(p, p.Needs));
            Assert.AreEqual(amt, loaded.SettlementById(p.Id).BoughtThisMonth, "what it bought this month is remembered");
            Assert.AreEqual(amt, loaded.SettlementById(p.Id).BoughtOf[p.Needs], "good by good");
            Assert.AreEqual(World.LeftToSell(p), World.LeftToSell(loaded.SettlementById(p.Id)));
        }

        [Test]
        public void EachMarketBeyondTheFirstRaisesWhatPartnersTradeWithYou()
        {
            var w = Town();
            var p = Partner(w);
            w.RefreshTradeBoost();
            Assert.AreEqual(0, p.TradeBoost, 1e-9, "one market: the normal amount");
            int one = World.MonthlyTrade(p, p.Makes);
            p.TradeBoost = World.BoostPerMarket;   // two markets
            Assert.Greater(World.MonthlyTrade(p, p.Makes), one);
            p.TradeBoost = World.MaxTradeBoost;     // four markets or more: the ceiling, three times
            Assert.GreaterOrEqual(World.MonthlyTrade(p, p.Makes), one * 3 - World.TradeUnit(p.Makes));
            Assert.AreEqual(World.MonthlyTrade(p) , World.MonthlyTrade(p), "the tribute base amount ignores the boost");
        }
    }
}
