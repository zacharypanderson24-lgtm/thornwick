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

        /// <summary>A built settlement of this tier for other test files (site is null when the world has none).</summary>
        internal static (World home, Settlement s, CampSite site) BuiltForTests(SettlementTier tier)
        {
            var w = Home();
            var s = w.Settlements.Where(o => o.Tier == tier).OrderBy(o => o.Id).FirstOrDefault();
            return s == null ? (w, null, null) : (w, s, w.TownCamp(s).Site);
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
            Assert.AreEqual(World.TownMapSize(s), site.Map.N, "the map grows with the settlement");
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
            else if (!World.Walled(s)) Assert.AreEqual(0, plan.WallTiles, "an open village or hamlet has no palisade");
        }

        static (World w, Settlement s, CampSite site) BuildOf(Settlement s)
        {
            var w = Home();
            var o = w.SettlementById(s.Id);
            return (w, o, w.TownCamp(o).Site);
        }

        static int Zoned(World w, Zone z)
        {
            int c = 0;
            for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.ZoneMap[y, x] == z) c++;
            return c;
        }

        [Test]
        public void TheMapGrowsFromThirtyTwoToTwoHundredFiftySix()
        {
            Assert.AreEqual(32, World.TownMapSize(new Settlement { Population = 12 }));
            Assert.AreEqual(256, World.TownMapSize(new Settlement { Population = 12000 }));
            int last = 0;
            foreach (int pop in new[] { 10, 40, 100, 200, 500, 1000, 2000, 4000, 8000, 12000 })
            {
                int n = World.TownMapSize(new Settlement { Population = pop });
                Assert.GreaterOrEqual(n, last, $"{pop} people"); Assert.AreEqual(0, n % 8); last = n;
            }
            Assert.Greater(Towns.Select(World.TownMapSize).Distinct().Count(), 6, "settlements of many sizes");
        }

        [Test]
        public void SettlementsAreLaidOutInManyWays()
        {
            // a spread of villages and towns of different kinds: their plans, facings and sizes differ
            var picks = Towns.Where(o => o.Tier == SettlementTier.Village || o.Tier == SettlementTier.Town)
                             .GroupBy(o => (o.Kind, World.StreetsOf(o))).Select(g => g.OrderBy(o => o.Id).First()).Take(8).ToList();
            var plans = new HashSet<StreetPlan>(); var layouts = new HashSet<string>();
            foreach (var t in picks)
            {
                var (_, s, site) = BuildOf(t);
                var w = site.World; var plan = w.TownLayout;
                plans.Add(plan.Streets);
                Assert.AreEqual(s.Kind, plan.Kind);
                Assert.AreEqual(World.Walled(s), plan.Walls != null, $"{s.Name}: walls by its rules");
                w.Recalc();
                foreach (var b in w.Blds.Where(b => b.T != "road" && b.T != "wall" && b.T != "watchtower"))
                    Assert.IsTrue(b.Served, $"{s.Name} ({s.Kind}, {plan.Streets}): {b.T} at {b.X},{b.Y} is joined to the hall by road");
                var hall = w.Hall;   // the shape of its streets, relative to its hall
                layouts.Add(string.Join(";", w.Blds.Where(b => b.T == "road").Select(b => $"{b.X - hall.X},{b.Y - hall.Y}").OrderBy(k => k)));
            }
            Assert.GreaterOrEqual(plans.Count, 2, "more than one kind of street plan");
            Assert.AreEqual(picks.Count, layouts.Count, "no two street layouts alike");
            Assert.AreEqual(4, Towns.Select(World.StreetsOf).Distinct().Count(), "every street plan is used somewhere");
        }

        [Test]
        public void WhatItProducesShapesItsTown()
        {
            Settlement Pick(System.Func<Settlement, bool> f) =>
                Towns.Where(o => (o.Tier == SettlementTier.Village || o.Tier == SettlementTier.Town) && f(o)).OrderBy(o => o.Population).FirstOrDefault();
            var timber = Pick(o => o.Produces(Res.Wood));
            if (timber != null)
            {
                var w = BuildOf(timber).site.World;
                Assert.IsTrue(w.Blds.Any(b => b.T == "wood"), "woods: a woodcutter lodge");
                Assert.Greater(Zoned(w, Zone.Wood), 0, "with trees marked for felling");
            }
            var stone = Pick(o => o.Produces(Res.Stone));
            if (stone != null)
            {
                var w = BuildOf(stone).site.World;
                Assert.IsTrue(w.Blds.Any(b => b.T == "quarry"), "stone: a quarry");
                Assert.Greater(Zoned(w, Zone.Mine), 0, "with rock to cut");
            }
            var wool = Pick(o => o.Produces(Res.Wool));
            if (wool != null) Assert.Greater(BuildOf(wool).site.World.TownLayout.Pens, 0, "wool: sheep pens");
            var flax = Pick(o => o.Produces(Res.Flax));
            if (flax != null) Assert.Greater(BuildOf(flax).site.World.TownLayout.FlaxFields, 0, "flax: fields of flax");
            var metal = Towns.Where(o => o.Produces(Res.Metal) && o.Tier == SettlementTier.Town).OrderBy(o => o.Population).FirstOrDefault();
            if (metal != null) Assert.IsTrue(BuildOf(metal).site.World.Blds.Any(b => b.T == "smithery"), "metal: a smithery");
            Assert.GreaterOrEqual(new[] { timber, stone, wool, flax }.Count(o => o != null), 3, "the world has places living by different goods");
        }

        [Test]
        public void ThePalisadeHasGatesWhereTheRoadsGoOut()
        {
            var (_, s, site) = Built(SettlementTier.Town);
            var w = site.World; var (x0, y0, x1, y1) = w.TownLayout.Walls.Value;
            bool Wall(int x, int y) => w.Grid[y, x] is Building b && (b.T == "wall" || b.T == "watchtower");
            int gates = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x != x0 && x != x1 && y != y0 && y != y1) continue;
                    if (w.Grid[y, x]?.T != "road") continue;
                    Assert.IsTrue((Wall(x - 1, y) && Wall(x + 1, y)) || (Wall(x, y - 1) && Wall(x, y + 1)), $"the road at {x},{y} passes through the wall as a gate");
                    gates++;
                }
            Assert.GreaterOrEqual(gates, 2, "at least two gates");
            foreach (var b in w.Blds.Where(b => b.T == "house" || b.T == "barracks"))
                Assert.IsTrue(b.X > x0 && b.Y > y0 && b.X + b.Sz - 1 < x1 && b.Y + b.Sz - 1 < y1, "homes and barracks are inside the walls");
            foreach (var b in w.Blds.Where(b => b.T == "farm"))
                Assert.IsFalse(b.X >= x0 && b.Y >= y0 && b.X <= x1 && b.Y <= y1, "farms are outside");
        }

        [Test]
        public void AGreatCapitalIsBuiltOnABigMapInGoodTime()
        {
            var big = Towns.OrderByDescending(o => o.Population).First();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (_, s, site) = BuildOf(big);
            watch.Stop();
            var plan = site.World.TownLayout;
            UnityEngine.Debug.Log($"capital {s.Name}: {s.Population:0} people on {site.Map.N} tiles, built in {watch.Elapsed.TotalSeconds:0.0} s: {plan.Kind}, {plan.Streets}, {plan.Homes} homes, {plan.Farms} farms, {plan.People} people housed");
            Assert.Less(watch.Elapsed.TotalSeconds, 30, "built in good time");
            Assert.Greater(site.Map.N, 128, $"{s.Population:0} people need a big map");
            Assert.Greater(plan.Homes, 100);
            Assert.GreaterOrEqual(plan.Barracks * Defs.Bunks, plan.Soldiers);
            site.World.Recalc();
            var cut = site.World.Blds.Where(b => b.T != "road" && b.T != "wall" && b.T != "watchtower" && !b.Served).Select(b => $"{b.T} {b.X},{b.Y}").ToList();
            Assert.IsEmpty(cut, "everything joined by roads: " + string.Join("; ", cut));
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
