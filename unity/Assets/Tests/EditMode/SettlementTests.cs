using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SettlementTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);
        static List<RaiderCamp> _camps;
        static List<RaiderCamp> Camps => _camps ??= RaiderCamps.Generate(Map);
        static int Cheb(int ax, int ay, int bx, int by) => System.Math.Max(System.Math.Abs(ax - bx), System.Math.Abs(ay - by));

        [Test]
        public void TheWorldHasSettlementsOfEverySizeMostlySmall()
        {
            var list = Settlements.Generate(Map, Camps);
            Assert.Greater(list.Count, 150, "a populated world");
            Assert.AreEqual(Enumerable.Range(0, list.Count), list.Select(s => s.Id), "ids run 0, 1, 2...");
            foreach (SettlementTier t in System.Enum.GetValues(typeof(SettlementTier)))
                Assert.Greater(list.Count(s => s.Tier == t), 0, $"there are {t}s");
            int capitals = list.Count(s => s.Tier == SettlementTier.Capital);
            Assert.That(capitals, Is.InRange(2, Settlements.Capitals), "a handful of capitals");
            Assert.Greater(list.Count(s => s.Tier <= SettlementTier.Village), list.Count / 2, "most are hamlets and villages");
            Assert.Less(list.Count(s => s.Tier >= SettlementTier.City), list.Count / 10, "cities are rare");
            Assert.Greater(list.Max(s => s.Population) / list.Min(s => s.Population), 100, "a very wide range of development");
            var home = Map.HomeSite;
            foreach (var s in list)
            {
                Assert.IsTrue(Map.IsLand(s.X, s.Y), s.ToString());
                Assert.GreaterOrEqual(Cheb(s.X, s.Y, home.X, home.Y), Settlements.HomeClear, "not on top of your settlement");
                Assert.IsTrue(Camps.All(c => Cheb(c.X, c.Y, s.X, s.Y) >= Settlements.CampClear), "not on a raider camp");
                Assert.That(s.Population, Is.InRange(Settlements.TierFloor[(int)s.Tier], Settlements.TierCeil[(int)s.Tier]));
            }
            Assert.AreEqual(list.Count, list.Select(s => s.Name).Distinct().Count(), "every settlement has its own name");
            Assert.AreEqual(string.Join(";", list), string.Join(";", Settlements.Generate(Map, Camps)), "the same world always has the same settlements");
        }

        [Test]
        public void RealmsArePyramidsOfSmallerSettlementsAnsweringToBiggerOnes()
        {
            var list = Settlements.Generate(Map, Camps);
            foreach (var s in list)
            {
                if (s.Liege < 0) continue;
                var l = list[s.Liege];
                Assert.Greater(l.Tier, s.Tier, $"{s} answers to a bigger place");
                int reach = l.Tier switch
                {
                    SettlementTier.Capital => Settlements.CapitalReach, SettlementTier.City => Settlements.CityReach,
                    SettlementTier.Town => Settlements.TownReach, _ => Settlements.VillageReach,
                };
                Assert.LessOrEqual(Cheb(s.X, s.Y, l.X, l.Y), reach, $"{s} is within {l}'s reach");
                var top = s; while (top.Liege >= 0) top = list[top.Liege];
                Assert.AreEqual(top.Id, s.Realm, "its realm is the seat at the top of its chain");
            }
            foreach (var cap in list.Where(s => s.IsSeat))
            {
                Assert.AreEqual(-1, cap.Liege, "a capital answers to nobody");
                Assert.AreEqual(cap.Id, cap.Realm);
            }
            // a capital's realm runs several levels deep
            int Depth(Settlement s) { int d = 0; while (s.Liege >= 0) { s = list[s.Liege]; d++; } return d; }
            Assert.GreaterOrEqual(list.Max(Depth), 2, "hamlets answer to villages that answer to towns...");
            Assert.Greater(list.Count(s => s.Liege < 0 && !s.IsSeat), 0, "some settlements answer to nobody");
            Assert.Greater(list.Count(s => s.Realm >= 0), list.Count / 3, "much of the land belongs to a realm");
        }

        [Test]
        public void SettlementsHaveAPersonalityAnAttitudeAndGoods()
        {
            var list = Settlements.Generate(Map, Camps);
            foreach (Personality p in System.Enum.GetValues(typeof(Personality)))
                Assert.Greater(list.Count(s => s.Personality == p), 0, $"some are {p}");
            foreach (var s in list)
            {
                Assert.That(s.Attitude, Is.InRange(-100, 100));
                Assert.AreNotEqual(s.Makes, s.Needs, "a place does not need what it makes");
                Assert.AreEqual(s.Tier >= SettlementTier.Village && s.Attitude >= Settlements.TradeThreshold(s.Personality), s.WillTrade);
            }
            Assert.IsFalse(list.Any(s => s.Tier == SettlementTier.Hamlet && s.WillTrade), "a hamlet is too small to trade");
            Assert.Greater(list.Count(s => s.WillTrade), 0, "some will trade with you from the start");
            Assert.Greater(list.Count(s => !s.WillTrade && s.Tier >= SettlementTier.Village), 0, "and some will not");
            Assert.Greater(list.Where(s => s.Personality == Personality.Mercantile).Average(s => s.Attitude),
                list.Where(s => s.Personality == Personality.Warlike).Average(s => s.Attitude), "warlike places are colder than mercantile ones");
            // realms have a character: subjects share their liege's personality more often than chance
            var subjects = list.Where(s => s.Liege >= 0).ToList();
            double same = subjects.Count(s => s.Personality == list[s.Liege].Personality) / (double)subjects.Count;
            Assert.Greater(same, 0.4);
            Assert.AreEqual("Hostile", Settlements.AttitudeName(-60)); Assert.AreEqual("Trusting", Settlements.AttitudeName(70));
        }

        [Test]
        public void SettlementsGrowTowardsWhatTheirLandCanHold()
        {
            var list = Settlements.Generate(Map, Camps);
            double before = list.Sum(s => s.Population);
            var grew = new List<Settlement>();
            for (int month = 0; month < 12 * 30; month++) grew.AddRange(Settlements.Grow(list));
            Assert.Greater(list.Sum(s => s.Population), before, "the world grows over the years");
            foreach (var s in list) Assert.LessOrEqual(s.Population, s.Capacity + 1, $"{s} stays within what its land can hold");
            Assert.Greater(grew.Count, 0, "some places grow into a bigger size");
            Assert.IsTrue(list.Where(s => !s.IsSeat).All(s => s.Tier <= SettlementTier.City), "only a realm's seat is called a capital");
        }

        [Test]
        public void ASettlementsPeopleAndAttitudeAreSavedEvenBeforeTheMapExists()
        {
            var w = new World(7);
            w.SetSettlements(Settlements.Generate(Map, Camps));
            var s = w.Settlements[5];
            s.Population = 777; s.Attitude = -42;

            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(0, loaded.Settlements.Count, "the settlements come with the world map, after loading");
            var again = World.LoadJson(loaded.SaveJson());   // saved again before the map arrived: the state is kept
            again.SetSettlements(Settlements.Generate(Map, Camps));
            Assert.AreEqual(777, again.Settlements[5].Population, 1e-6);
            Assert.AreEqual(-42, again.Settlements[5].Attitude);
            Assert.AreEqual(Settlements.Generate(Map, Camps)[6].Population, again.Settlements[6].Population, 1e-6, "the others are as generated");

            // a month on, they have grown
            var t = new World(7);
            t.SetSettlements(Settlements.Generate(Map, Camps));
            double pop = t.Settlements.Sum(x => x.Population);
            for (int i = 0; i < World.SettlementMonth + 1; i++) { t.ResAmt[Res.Food] = 200; t.Tick(); }
            Assert.Greater(t.Settlements.Sum(x => x.Population), pop);
        }
    }
}
