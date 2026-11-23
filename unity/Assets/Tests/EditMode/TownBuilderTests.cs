using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>NPC settlements under attack are real towns built by the same rules as yours (World.TownBuilder.cs).</summary>
    public class TownBuilderTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);
        static List<Settlement> _towns;
        static List<Settlement> Towns => _towns ??= Settlements.Generate(Map);

        static World Home()
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            return w;
        }

        static (World home, Settlement s, CampSite site) Built(SettlementTier tier)
        {
            var w = Home();
            var s = w.Settlements.Where(o => o.Tier == tier).OrderBy(o => o.Id).First();
            var camp = w.TownCamp(s);
            return (w, s, camp.Site);
        }

        static int Cheb(int ax, int ay, int bx, int by) => System.Math.Max(System.Math.Abs(ax - bx), System.Math.Abs(ay - by));

        [TestCase(SettlementTier.Hamlet)]
        [TestCase(SettlementTier.Village)]
        [TestCase(SettlementTier.Town)]
        [TestCase(SettlementTier.City)]
        public void ASettlementIsARealTownJoinedByRoads(SettlementTier tier)
        {
            var (home, s, site) = Built(tier);
            var w = site.World; var plan = w.TownLayout;
            Assert.IsNotNull(plan, "a town plan");
            Assert.AreEqual(World.TownMapSize(tier), site.Map.N, "the map grows with the settlement");
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "hall"), "one town hall");
            Assert.Greater(plan.Homes, 0, "homes");
            Assert.Greater(w.Blds.Count(b => b.T == "road"), 4, "streets");
            Assert.GreaterOrEqual(plan.Farms, 1, "at least one farm");
            Assert.Greater(plan.Fields, 0, "with its fields");
            w.Recalc();
            foreach (var b in w.Blds.Where(b => b.T != "road" && b.T != "wall" && b.T != "watchtower"))
                Assert.IsTrue(b.Served, $"{b.T} at {b.X},{b.Y} is joined to the hall by road");
            Assert.LessOrEqual(w.Peasants, w.Blds.Where(b => b.T == "house").Sum(b => b.Capacity) + Defs.HallCap, "everyone has a home");
            Assert.GreaterOrEqual(w.Recalc().Bunks, w.Soldiers, "every soldier has a bunk");
            if (tier >= SettlementTier.Village)
                foreach (var t in new[] { "market", "well", "silo", "sawmill" })
                    Assert.IsTrue(w.Blds.Any(b => b.T == t), $"a {tier.ToString().ToLower()} has a {t}");
            if (tier >= SettlementTier.Town)
            {
                Assert.Greater(plan.WallTiles, 8, "a palisade");
                Assert.GreaterOrEqual(plan.Towers, 4, "with watchtowers");
                Assert.IsTrue(w.Blds.Any(b => b.T == "storehouse") && w.Blds.Any(b => b.T == "smithery"), "and the bigger workshops");
            }
            else Assert.AreEqual(0, plan.WallTiles, "a hamlet or village has no palisade");
        }

        [Test]
        public void BiggerSettlementsAreBiggerTowns()
        {
            var village = Built(SettlementTier.Village).site.World.TownLayout;
            var city = Built(SettlementTier.City).site.World.TownLayout;
            Assert.Greater(city.Homes, village.Homes);
            Assert.Greater(city.People, village.People);
            Assert.Greater(city.Barracks, village.Barracks - 1);
        }

        [Test]
        public void BarracksHoldSixteenSoldiers()
        {
            Assert.AreEqual(16, Defs.Bunks);
            var w = new World(7);
            int c = Defs.Start;
            var site = Enumerable.Range(0, 25 * 25).Select(i => (x: c - 12 + i % 25, y: c - 12 + i / 25)).First(t => w.CanPlace("barracks", t.x, t.y).Ok);
            Assert.IsTrue(w.Place("barracks", site.x, site.y).Ok);
            Assume.That(w.Blds.Single(b => b.T == "barracks").Served);
            Assert.AreEqual(16, w.Recalc().Bunks);
        }

        [Test]
        public void ATownsSoldiersWaitInTheirBarracksUntilItIsRoused()
        {
            var (home, s, site) = Built(SettlementTier.Town);
            var camp = home.TownCamp(s);
            Assert.Greater(site.Map.Barracks.Count, 0);
            var land = Landing(site.Map, 30);
            home.Soldiers = 30; home.Archers = 0;
            var b = home.BeginInvasion(camp, new[] { land });
            Assert.IsNotNull(b);
            var garrison = b.Units.Where(u => !u.Attacker && (u.Kind == BKind.Raider || u.Kind == BKind.RaiderArcher)).ToList();
            Assert.IsTrue(garrison.All(u => u.InBarracks), "they start inside");
            foreach (var u in garrison)
                Assert.LessOrEqual(site.Map.Barracks.Min(k => Cheb(u.X, u.Y, k.x, k.y)), 6, "beside a barracks");
            for (int i = 0; i < CampBattle.MaxTicks && !b.Alarm && !b.Over; i++) b.Step();
            Assume.That(b.Alarm, "the landing was noticed");
            b.Step();
            Assert.IsTrue(garrison.All(u => !u.InBarracks || !u.Alive), "roused, they pour out");
        }

        [TestCase(SettlementTier.Village)]
        [TestCase(SettlementTier.Town)]
        public void EverythingWorthFightingForCanBeReachedFromTheEdge(SettlementTier tier)
        {
            var m = Built(tier).site.Map;
            var seen = new bool[m.N, m.N]; var q = new Queue<(int x, int y)>();
            for (int i = 0; i < m.N; i++)
                foreach (var (x, y) in new[] { (i, 0), (i, m.N - 1), (0, i), (m.N - 1, i) })
                    if (!seen[y, x] && m.Passable(x, y)) { seen[y, x] = true; q.Enqueue((x, y)); }
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (m.In(nx, ny) && !seen[ny, nx] && m.Passable(nx, ny)) { seen[ny, nx] = true; q.Enqueue((nx, ny)); }
                }
            }
            bool Near((int x, int y) t) { for (int j = -2; j <= 3; j++) for (int i = -2; i <= 3; i++) if (m.In(t.x + i, t.y + j) && seen[t.y + j, t.x + i]) return true; return false; }
            Assert.IsTrue(Near(m.Chief), "the hall");
            Assert.IsTrue(m.Barracks.All(Near), "every barracks");
            Assert.Greater(m.Piles.Count, 0, "stores to plunder");
            Assert.IsTrue(m.Piles.All(p => seen[p.Y, p.X]), "and every store");
        }

        [Test]
        public void ItsPeopleWalkTheStreets()
        {
            var w = Built(SettlementTier.Town).site.World;
            for (int d = 0; d < 12; d++) w.AmbientDay();
            Assert.Greater(w.Folk.Count, 0, "townsfolk");
            Assert.IsTrue(w.Folk.Any(v => v.St != FolkState.Home), "some are out");
            Assert.IsTrue(w.Blds.Any(b => b.Workers > 0), "and at work");
        }

        [Test]
        public void TheSameSettlementIsAlwaysBuiltTheSameWay()
        {
            string Layout() => string.Join(";", Built(SettlementTier.Village).site.World.Blds.Select(b => $"{b.T}{b.X},{b.Y}"));
            Assert.AreEqual(Layout(), Layout());
        }

        static BattleGroup Landing(CampMap m, int inf)
        {
            for (int i = 0; i < m.N; i++)
                foreach (var (x, y) in new[] { (i, 0), (0, i), (i, m.N - 1), (m.N - 1, i) })
                    if (m.Passable(x, y)) return new BattleGroup { X = x, Y = y, Infantry = inf };
            Assert.Fail("no open edge"); return null;
        }
    }
}
