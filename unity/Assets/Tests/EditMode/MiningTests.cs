// Underground mining (Unity-only, World.Mining.cs): hidden deposits, surveying by miners, digging down through levels,
// timber supports and cave-ins. These are rule tests, not browser parity (the browser still mines surface ore).
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class MiningTests
    {
        const int N = Defs.N;

        static IEnumerable<(int x, int y)> AllTiles() { for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) yield return (x, y); }
        static IEnumerable<(int x, int y)> Reach(int x, int y)
        {
            for (int j = y - Defs.WorkR; j <= y + Defs.WorkR; j++) for (int i = x - Defs.WorkR; i <= x + Defs.WorkR; i++) if (World.Inb(i, j)) yield return (i, j);
        }

        /// <summary>
        /// The test town (SimParityTests.Layout), miners hired first, and one mine on a road-side lot whose reach is painted as a
        /// Mining zone. wantOre picks a lot over a surface showing (true) or over ground with no ore at any level (false).
        /// </summary>
        static (World w, Building mine) MineTown(bool wantOre)
        {
            var w = new World(7);
            SimParityTests.Layout(w, 10);
            w.Prio = new[] { "mining", "farming", "timber", "woodwork", "quarrying", "trade" };
            w.Drafting = false;   // keep the ore in the store for the asserts
            bool RoadNext(int x, int y) => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => World.Inb(x + d.Item1, y + d.Item2) && w.Grid[y + d.Item2, x + d.Item1]?.T == "road");
            bool OreUnder(int x, int y) => Enumerable.Range(1, World.MaxDepth).Any(l => w.OreAt(x, y, l) > 0);
            foreach (var (x, y) in AllTiles())
            {
                if (Math.Max(Math.Abs(x - Defs.Start), Math.Abs(y - Defs.Start)) > 12) continue;
                if (!w.CanPlace("mine", x, y).Ok || !RoadNext(x, y)) continue;
                var reach = Reach(x, y).ToList();
                bool ok = wantOre ? reach.Any(t => w.DecoMap[t.y, t.x] == Deco.Ore) : reach.All(t => !OreUnder(t.x, t.y)) && reach.Count(t => w.ZoneCheck(Zone.Mine, t.x, t.y).Ok && w.Grid[t.y, t.x] == null) >= 6;
                if (!ok) continue;
                var b = w.AddBld("mine", x, y);
                foreach (var (i, j) in reach) if (w.Grid[j, i] == null && w.DecoMap[j, i] != Deco.Rock) w.SetZone(Zone.Mine, i, j);
                w.Recalc();
                if (!b.Served) { w.Remove(x, y); continue; }
                return (w, b);
            }
            return (w, null);
        }

        static void Day(World w)
        {
            w.ResAmt[Res.Food] = Math.Max(w.ResAmt[Res.Food], 300); w.ResAmt[Res.Wood] = Math.Max(w.ResAmt[Res.Wood], 200); w.ResAmt[Res.Gold] = Math.Max(w.ResAmt[Res.Gold], 100);
            w.Tick();
        }

        [Test]
        public void DepositsAreDeterministicAndSitUnderTheShowings()
        {
            var a = new World(7); var b = new World(7);
            int hidden = 0; var total = new double[World.MaxDepth + 1];
            foreach (var (x, y) in AllTiles())
                for (int l = 1; l <= World.MaxDepth; l++)
                {
                    double o = a.OreAt(x, y, l);
                    Assert.AreEqual(o, b.OreAt(x, y, l), $"deposit {x},{y} L{l} differs between two worlds of the same seed");
                    Assert.That(o, Is.InRange(0, World.LevelCapMax[l]));
                    total[l] += o;
                    if (o > 0 && a.DecoMap[y, x] != Deco.Ore) hidden++;
                }
            foreach (var (x, y) in AllTiles()) if (a.DecoMap[y, x] == Deco.Ore) Assert.Greater(a.OreAt(x, y, 1), 0, $"showing at {x},{y} has no ore under it");
            for (int l = 1; l <= World.MaxDepth; l++) Assert.Greater(total[l], 0, $"no ore at level {l}");
            Assert.Greater(hidden, 0, "some deposits should have no surface showing");
            Assert.Greater(total[3], total[1], "deeper levels should hold more ore");
        }

        [Test]
        public void HigherGroundSitsOverMoreDeepOre()
        {
            var w = new World(7);
            double hi = 0, lo = 0; int nh = 0, nl = 0;
            foreach (var (x, y) in AllTiles())
            {
                double o = w.OreAt(x, y, 3);
                if (w.TileLevel(x, y) >= 2) { hi += o; nh++; } else if (w.TileLevel(x, y) <= 0) { lo += o; nl++; }
            }
            Assume.That(nh > 0 && nl > 0);
            Assert.Greater(hi / nh, lo / nl, "hills and mountains should average more deep ore than low ground");
        }

        [Test]
        public void OreIsHiddenUntilSurveyed()
        {
            var w = new World(7);
            foreach (var (x, y) in AllTiles())
            {
                Assert.IsFalse(w.IsSurveyed(x, y));
                Assert.IsFalse(w.HasKnownOre(x, y), $"{x},{y} known before any survey");
            }
        }

        [Test]
        public void MiningZonesGoOnRockShowingsAndOpenGroundButNotForest()
        {
            var w = new World(7);
            var open = AllTiles().First(t => w.Grid[t.y, t.x] == null && w.DecoMap[t.y, t.x] == Deco.None && !w.IsSolid(t.x, t.y));
            var tree = AllTiles().First(t => w.DecoMap[t.y, t.x] == Deco.Tree);
            var ore = AllTiles().First(t => w.DecoMap[t.y, t.x] == Deco.Ore);
            Assert.IsTrue(w.ZoneCheck(Zone.Mine, open.x, open.y).Ok, "open ground");
            Assert.IsTrue(w.ZoneCheck(Zone.Mine, ore.x, ore.y).Ok, "ore showing");
            Assert.IsFalse(w.ZoneCheck(Zone.Mine, tree.x, tree.y).Ok, "forest");
        }

        [Test]
        public void MinersSurveyThenDigDownShoreAndBringUpOre()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site beside a road and a showing in the test town");
            w.CaveInRiskScale = 0;
            var showings = AllTiles().Where(t => w.DecoMap[t.y, t.x] == Deco.Ore).ToList();
            int zoned = Reach(mine.X, mine.Y).Count(t => w.ZoneMap[t.y, t.x] == Zone.Mine);
            bool sawDown = false, sawSurvey = false; int firstOreDay = -1;
            for (int d = 0; d < 400; d++)
            {
                Day(w);
                sawDown |= mine.Crew.Any(a => a.St == CrewState.Down);
                sawSurvey |= mine.Crew.Any(a => a.Survey && a.St != CrewState.Home);
                if (firstOreDay < 0 && w.OreTaken.Count > 0) firstOreDay = d;
            }
            Assert.IsTrue(sawSurvey, "miners never walked out to survey");
            Assert.AreEqual(zoned, Reach(mine.X, mine.Y).Count(t => w.ZoneMap[t.y, t.x] == Zone.Mine && w.IsSurveyed(t.x, t.y)), "every zone tile in reach is surveyed");
            Assert.IsTrue(sawDown, "nobody went underground");
            Assert.GreaterOrEqual(mine.Depth, 1, "the shaft was never dug");
            Assert.AreEqual(mine.Depth, mine.Shored, "with wood in store every dug level is shored");
            Assert.Greater(w.OreTaken.Values.Sum(), 0, "no ore was brought up");
            Assert.Greater(firstOreDay, 0, "ore can only come up after the survey and the first level are done");
            foreach (var (x, y) in showings) Assert.IsTrue(w.DecoMap[y, x] == Deco.Ore || w.Grid[y, x] != null, $"the showing at {x},{y} was mined away (only a building on top may cover it)");
            foreach (var kv in w.OreTaken)
            {
                int level = kv.Key / (N * N), k = kv.Key % (N * N);
                Assert.IsTrue(w.IsSurveyed(k % N, k / N), "ore was taken from an unsurveyed tile");
                Assert.LessOrEqual(level, mine.Depth, "ore was taken below the shaft");
            }
        }

        [Test]
        public void DeeperLevelsCostMoreButBringUpMore()
        {
            for (int l = 2; l <= World.MaxDepth; l++)
            {
                Assert.Greater(World.MineLoad[l], World.MineLoad[l - 1], "load");
                Assert.Greater(World.MineLoad[l] / (double)World.MineTripDays[l], World.MineLoad[l - 1] / (double)World.MineTripDays[l - 1], "ore per worker-day");
                Assert.Greater(World.DigWork[l], World.DigWork[l - 1], "digging work");
                Assert.Greater(World.SupportWood[l], World.SupportWood[l - 1], "timber for supports");
                Assert.Greater(World.LevelCapMax[l], World.LevelCapMax[l - 1], "richest tile");
            }
        }

        [Test]
        public void CaveInRiskRisesWithDepthAndCrowdingAndSupportsCutIt()
        {
            var w = new World(7);
            var b = new Building { T = "mine", Depth = 3, Shored = 0 };
            Assert.Greater(w.CaveInChance(b, 2, 1), w.CaveInChance(b, 1, 1), "deeper");
            Assert.Greater(w.CaveInChance(b, 1, 3), w.CaveInChance(b, 1, 1), "crowded");
            double unshored = w.CaveInChance(b, 2, 2);
            b.Shored = 2;
            Assert.Less(w.CaveInChance(b, 2, 2), unshored * 0.5, "supports cut the risk sharply");
            Assert.Greater(w.CaveInChance(b, 2, 2), 0, "supports never make a mine perfectly safe");
        }

        [Test]
        public void ACaveInClosesTheMineSmashesSupportsAndCanKill()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test town");
            w.CaveInRiskScale = 0;
            int d = 0;
            while (d++ < 600 && !mine.Crew.Any(a => a.St == CrewState.Down && !a.Dig && a.Level <= mine.Shored)) Day(w);
            Assume.That(mine.Crew.Any(a => a.St == CrewState.Down), "nobody went underground in 600 days");
            int level = mine.Crew.First(a => a.St == CrewState.Down).Level, shoredBefore = mine.Shored;
            int peasants = w.Peasants, deaths = w.MinerDeaths;
            w.Events.Clear();
            w.CaveInRiskScale = 1e9;   // the next worker-day underground caves in
            Day(w);
            Assert.AreEqual(1, mine.CaveIns, "exactly one cave-in at this mine");
            Assert.AreEqual(World.RepairDays, mine.Closed, "closed for repairs");
            Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), "everyone came up");
            Assert.Less(mine.Shored, shoredBefore, "the supports of that level were smashed");
            Assert.IsTrue(w.Events.Any(e => e.Contains("Cave-in")), "the player is told");
            Assert.LessOrEqual(w.Peasants, peasants, "nobody is born out of a cave-in");

            // while closed nobody goes down, even at zero risk
            w.CaveInRiskScale = 0;
            for (int i = 0; i < World.RepairDays - 1; i++) { Day(w); Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), $"day {i} of repairs"); }
            for (int i = 0; i < 60 && !mine.Crew.Any(a => a.St == CrewState.Down); i++) Day(w);
            Assert.IsTrue(mine.Crew.Any(a => a.St == CrewState.Down), "the mine reopens after repairs");
        }

        [Test]
        public void ACaveInCanKillAMiner()
        {
            // many forced cave-ins: some must kill (60% unshored, 25% shored), and each death removes a villager and a crew member
            int deaths = 0, caveIns = 0;
            for (int seedTry = 0; seedTry < 12 && deaths == 0; seedTry++)
            {
                var (w, mine) = MineTown(wantOre: true);
                Assume.That(mine != null);
                w.CaveInRiskScale = 0;
                for (int d = 0; d < 300 && !mine.Crew.Any(a => a.St == CrewState.Down); d++) Day(w);
                if (!mine.Crew.Any(a => a.St == CrewState.Down)) continue;
                w.Day += seedTry * 37;   // different rolls each try
                var mines = w.Blds.Where(b => b.T == "mine").ToList(); int crew = mines.Sum(b => b.Crew.Count), dead0 = w.MinerDeaths;
                w.CaveInRiskScale = 1e9; Day(w);
                caveIns += w.CaveIns; deaths += w.MinerDeaths;
                Assert.AreEqual(crew - (w.MinerDeaths - dead0), mines.Sum(b => b.Crew.Count), "every dead miner left their crew");
            }
            Assert.Greater(caveIns, 0);
            Assert.Greater(deaths, 0, "no cave-in ever killed anyone");
        }

        [Test]
        public void AMineOnBarrenGroundSurveysAndFindsNothing()
        {
            var (w, mine) = MineTown(wantOre: false);
            Assume.That(mine != null, "no barren road-side lot in the test town");
            w.CaveInRiskScale = 0;
            w.Events.Clear(); var events = new List<string>();
            for (int d = 0; d < 200; d++) { Day(w); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(Reach(mine.X, mine.Y).Where(t => w.ZoneMap[t.y, t.x] == Zone.Mine).All(t => w.IsSurveyed(t.x, t.y)), "the zone was surveyed");
            Assert.AreEqual(0, mine.Depth, "no shaft is dug where there is no ore");
            Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), "nobody goes down");
            StringAssert.Contains("No ore found", w.MineStatus(mine));
            Assert.IsTrue(events.Any(e => e.Contains("found no ore")), "the player is told");
        }
    }
}
