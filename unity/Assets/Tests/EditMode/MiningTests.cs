// Underground mining (Unity-only, World.Mining.cs): ore in hills and mountains, surveying by miners, adits into high ground,
// shafts for what lies below, timber supports and cave-ins. These are rule tests, not browser parity (the browser still mines surface ore).
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
        static bool OreUnder(World w, int x, int y) => Enumerable.Range(1, World.Bands).Any(l => w.OreAt(x, y, l) > 0);

        /// <summary>Lay a road along World.LPath from the start settlement's street to (x, y), clearing trees on the way, never over
        /// (avoidX, avoidY). False when the path is blocked by a building, solid ground or the avoided tile.</summary>
        static bool RoadTo(World w, int x, int y, int avoidX, int avoidY)
        {
            var path = World.LPath(Defs.Start, Defs.Start + 2, x, y);
            foreach (var (i, j) in path)
            {
                if (!World.Inb(i, j) || (i == avoidX && j == avoidY)) return false;
                var g = w.Grid[j, i];
                if (g != null) { if (g.T == "road") continue; return false; }
                if (w.DecoMap[j, i] == Deco.Tree || w.DecoMap[j, i] == Deco.Stump || w.DecoMap[j, i] == Deco.Sapling) w.Remove(i, j);
                if (w.IsSolid(i, j) || w.DecoMap[j, i] != Deco.None) return false;
            }
            foreach (var (i, j) in path) if (w.Grid[j, i] == null) w.AddBld("road", i, j);
            return true;
        }

        /// <summary>
        /// The test town (SimParityTests.Layout), miners hired first, and one mine with its reach painted as a Mining zone.
        /// wantOre: the nearest lot at the foot of high ground with ore its adit can reach (a road is laid to it).
        /// Otherwise: a lowland lot in the town with no ore at any band anywhere in reach.
        /// </summary>
        static (World w, Building mine) MineTown(bool wantOre)
        {
            var w = new World(7);
            SimParityTests.Layout(w, 10);
            w.Prio = new[] { "mining", "farming", "timber", "woodwork", "quarrying", "trade" };
            w.Drafting = false;   // keep the ore in the store for the asserts
            bool RoadNext(int x, int y) => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => World.Inb(x + d.Item1, y + d.Item2) && w.Grid[y + d.Item2, x + d.Item1]?.T == "road");
            var sites = AllTiles().Where(t => Math.Max(Math.Abs(t.x - Defs.Start), Math.Abs(t.y - Defs.Start)) <= 40)
                                  .OrderBy(t => Math.Abs(t.x - Defs.Start) + Math.Abs(t.y - Defs.Start)).ThenBy(t => t.y * N + t.x);
            foreach (var (x, y) in sites)
            {
                if (!w.CanPlace("mine", x, y).Ok || w.GenLevel(x, y) >= 1) continue;   // the mine stands at the foot, not on the hill
                var reach = Reach(x, y).ToList();
                if (wantOre)
                {
                    var probe = new Building { T = "mine", X = x, Y = y };
                    int aditOre = reach.Count(t => Enumerable.Range(1, World.Bands).Any(l => w.OreAt(t.x, t.y, l) > 0 && w.NeedShaft(probe, t.x, t.y, l) == 0));
                    if (aditOre < 4) continue;
                    int face = w.MineFaceAt(x, y); if (face < 0) continue;
                    var (ex, ey) = World.EntranceTile(x, y, face);   // the road must reach the entrance
                    if (!World.Inb(ex, ey) || (w.Grid[ey, ex]?.T != "road" && !RoadTo(w, ex, ey, x, y))) continue;
                }
                else
                {
                    if (!RoadNext(x, y) || w.MineFaceAt(x, y) >= 0 || reach.Any(t => OreUnder(w, t.x, t.y)) || reach.Count(t => w.Grid[t.y, t.x] == null && w.ZoneCheck(Zone.Mine, t.x, t.y).Ok) < 6) continue;
                }
                var b = w.AddBld("mine", x, y);
                foreach (var (i, j) in reach) if (w.Grid[j, i] == null) w.SetZone(Zone.Mine, i, j);
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
        public void DepositsAreDeterministicAndShowingsSitOverThem()
        {
            var a = new World(7); var b = new World(7);
            int hidden = 0, showings = 0; var total = new double[World.Bands + 1];
            foreach (var (x, y) in AllTiles())
                for (int l = 1; l <= World.Bands; l++)
                {
                    double o = a.OreAt(x, y, l);
                    Assert.AreEqual(o, b.OreAt(x, y, l), $"deposit {x},{y} band {l} differs between two worlds of the same seed");
                    Assert.That(o, Is.InRange(0, World.LevelCapMax[l]));
                    total[l] += o;
                    if (l == 1 && o > 0 && a.DecoMap[y, x] != Deco.Ore) hidden++;
                }
            foreach (var (x, y) in AllTiles())
                if (a.DecoMap[y, x] == Deco.Ore)
                {
                    showings++;
                    Assert.Greater(a.OreAt(x, y, 1), 0, $"showing at {x},{y} has no ore under it");
                    Assert.GreaterOrEqual(a.GenLevel(x, y), 1, $"showing at {x},{y} is not on a hill");
                }
            Assert.Greater(showings, 20, "too few showings to find ore by");
            Assert.Less(showings, 600, "showings should be hints, not a carpet");
            Assert.Greater(hidden, showings, "most ore has no surface showing");
            for (int l = 1; l <= World.Bands; l++) Assert.Greater(total[l], 0, $"no ore in band {l}");
            Assert.Greater(total[3], total[1], "the deepest band should hold the most ore");
        }

        [Test]
        public void OreLiesInHillsAndMountainsNotLowland()
        {
            var w = new World(7);
            double low = 0, hill = 0, mtn = 0; int nh = 0, nm = 0;
            foreach (var (x, y) in AllTiles())
            {
                double o = Enumerable.Range(1, World.Bands).Sum(l => w.OreAt(x, y, l));
                if (w.IsMountain(x, y)) { mtn += o; nm++; }
                else if (w.GenLevel(x, y) >= 1) { hill += o; nh++; }
                else if (w.GenLevel(x, y) < 0.75) low += o;
            }
            Assert.AreEqual(0, low, "lowland holds no ore");
            Assume.That(nh > 0 && nm > 0);
            Assert.Greater(hill, 0, "hills hold ore");
            Assert.Greater(mtn / nm, hill / nh, "mountains are richer per tile than hills");
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
            var mtn = AllTiles().First(t => w.IsMountain(t.x, t.y) && w.RockLeft(t.x, t.y) > 0);
            Assert.IsTrue(w.ZoneCheck(Zone.Mine, open.x, open.y).Ok, "open ground");
            Assert.IsTrue(w.ZoneCheck(Zone.Mine, ore.x, ore.y).Ok, "ore showing");
            Assert.IsTrue(w.ZoneCheck(Zone.Mine, mtn.x, mtn.y).Ok, "mountain");
            Assert.IsFalse(w.ZoneCheck(Zone.Mine, tree.x, tree.y).Ok, "forest");
        }

        [Test]
        public void AnAditReachesBandsThatRiseAboveTheMineAndShaftsTheRest()
        {
            var w = new World(7);
            // a lowland lot next to the highest ground within reach
            (int x, int y, int hx, int hy) best = default; double rise = 0;
            foreach (var (x, y) in AllTiles())
            {
                if (w.GenLevel(x, y) > 0.25 || w.IsSolid(x, y)) continue;
                foreach (var (i, j) in Reach(x, y)) { double r = w.GenLevel(i, j) - w.GenLevel(x, y); if (r > rise) { rise = r; best = (x, y, i, j); } }
            }
            Assume.That(rise >= 3, "no lot below ground 3+ levels higher");
            var b = new Building { T = "mine", X = best.x, Y = best.y };
            for (int l = 1; l <= World.Bands; l++) Assert.AreEqual(0, w.NeedShaft(b, best.hx, best.hy, l), $"band {l} of ground {rise:0.#} levels up is reached by the adit");
            for (int l = 1; l <= World.Bands; l++) Assert.AreEqual(l, w.NeedShaft(b, best.x, best.y, l), $"band {l} under the mine itself needs shaft level {l}");
        }

        [Test]
        public void QuarryingAMountainDoesNotMoveOrDestroyItsOre()
        {
            var w = new World(7);
            var t = AllTiles().First(p => w.IsMountain(p.x, p.y) && OreUnder(w, p.x, p.y));
            var foot = AllTiles().First(p => !w.IsSolid(p.x, p.y) && Math.Max(Math.Abs(p.x - t.x), Math.Abs(p.y - t.y)) <= Defs.WorkR);
            var b = new Building { T = "mine", X = foot.x, Y = foot.y };
            var before = Enumerable.Range(1, World.Bands).Select(l => (w.OreAt(t.x, t.y, l), w.NeedShaft(b, t.x, t.y, l))).ToArray();
            // quarry the tile's stone out and lower its corners, as heavy quarrying would
            w.Dig[World.Tk(t.x, t.y)] = w.RockCap(t.x, t.y);
            for (int dy = 0; dy <= 1; dy++) for (int dx = 0; dx <= 1; dx++) w.Hv[(t.y + dy) * (N + 1) + t.x + dx] = 0;
            w.Touch();
            var after = Enumerable.Range(1, World.Bands).Select(l => (w.OreAt(t.x, t.y, l), w.NeedShaft(b, t.x, t.y, l))).ToArray();
            CollectionAssert.AreEqual(before, after);
        }

        [Test]
        public void AMineAtTheFootSurveysDrivesAnAditAndBringsUpOre()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no lot at the foot of ore-bearing high ground that a road can reach");
            w.CaveInRiskScale = 0;
            var showings = AllTiles().Where(t => w.DecoMap[t.y, t.x] == Deco.Ore).ToList();
            int zoned = Reach(mine.X, mine.Y).Count(t => w.ZoneMap[t.y, t.x] == Zone.Mine);
            bool sawDown = false, sawSurvey = false;
            for (int d = 0; d < 400; d++)
            {
                Day(w);
                sawDown |= mine.Crew.Any(a => a.St == CrewState.Down);
                sawSurvey |= mine.Crew.Any(a => a.Survey && a.St != CrewState.Home);
            }
            Assert.IsTrue(sawSurvey, "miners never walked out to survey");
            Assert.AreEqual(zoned, Reach(mine.X, mine.Y).Count(t => w.ZoneMap[t.y, t.x] == Zone.Mine && w.IsSurveyed(t.x, t.y)), "every zone tile in reach is surveyed");
            Assert.IsTrue(sawDown, "nobody went underground");
            Assert.IsTrue(mine.Adit, "the adit was never driven");
            Assert.IsTrue(mine.AditShored, "with wood in store the adit is shored");
            Assert.AreEqual(mine.Depth, mine.Shored, "with wood in store every shaft level is shored");
            var mineTaken = w.OreTaken.Where(kv => { int k = kv.Key % (N * N); return Math.Max(Math.Abs(k % N - mine.X), Math.Abs(k / N - mine.Y)) <= Defs.WorkR; }).ToList();
            Assert.Greater(mineTaken.Sum(kv => kv.Value), 0, "no ore was brought up");
            foreach (var kv in mineTaken)
            {
                int band = kv.Key / (N * N), k = kv.Key % (N * N), x = k % N, y = k / N;
                Assert.IsTrue(w.IsSurveyed(x, y), "ore was taken from an unsurveyed tile");
                int need = w.NeedShaft(mine, x, y, band);
                if (w.Grid[mine.Y, mine.X] == mine) Assert.IsTrue(need == 0 ? mine.Adit : need <= mine.Depth, $"ore at {x},{y} band {band} was out of the mine's reach");
            }
            foreach (var (x, y) in showings) Assert.IsTrue(w.DecoMap[y, x] == Deco.Ore || w.Grid[y, x] != null, $"the showing at {x},{y} was mined away (only a building on top may cover it)");
            // every mine the town grew by itself opens onto its road
            foreach (var m in w.Blds.Where(b => b.T == "mine" && b != mine))
            {
                int f = w.MineFaceAt(m.X, m.Y); if (f < 0) continue;
                var (ex, ey) = World.EntranceTile(m.X, m.Y, f);
                Assert.AreEqual("road", w.Grid[ey, ex]?.T, $"the mine grown at {m.X},{m.Y} has no road at its entrance {ex},{ey}");
            }
        }

        [Test]
        public void AMineOpensAwayFromTheHighGround()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            int f = w.MineFaceAt(mine.X, mine.Y);
            Assert.AreEqual(f, mine.Face, "the mine's door is its entrance");
            var (ex, ey) = World.EntranceTile(mine.X, mine.Y, f);
            var (bx, by) = World.EntranceTile(mine.X, mine.Y, (f + 2) % 4);   // the tile behind it
            Assert.Less(w.GenLevel(ex, ey), w.GenLevel(bx, by), "the entrance faces downhill, the back faces the hill");
            // the uphill side holds more high ground than the entrance side
            double Side(int dx, int dy) { double s = 0; for (int k = 1; k <= Defs.WorkR; k++) if (World.Inb(mine.X + dx * k, mine.Y + dy * k)) s += w.GenLevel(mine.X + dx * k, mine.Y + dy * k); return s; }
            Assert.Greater(Side(-Defs.FaceDirs[f, 0], -Defs.FaceDirs[f, 1]), Side(Defs.FaceDirs[f, 0], Defs.FaceDirs[f, 1]));
        }

        [Test]
        public void AMineNeedsItsRoadAtTheEntrance()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            int f = w.MineFaceAt(mine.X, mine.Y);
            var (ex, ey) = World.EntranceTile(mine.X, mine.Y, f);
            Assert.IsTrue(mine.Served, "served with its road at the entrance");
            // a connected road on another side does not count
            for (int g = 0; g < 4; g++)
            {
                if (g == f) continue;
                var (sx, sy) = World.EntranceTile(mine.X, mine.Y, g);
                if (World.Inb(sx, sy) && w.Grid[sy, sx] == null && !w.IsSolid(sx, sy) && !w.HasTree(sx, sy) && w.DecoMap[sy, sx] == Deco.None) { w.AddBld("road", sx, sy); break; }
            }
            w.Remove(ex, ey);
            w.Recalc();
            Assert.IsFalse(mine.Served, "without a road at the entrance the mine does not work");
            StringAssert.Contains("Needs a road", w.MineStatus(mine));
            w.ResAmt[Res.Wood] = 400; w.AddBld("road", ex, ey); w.Recalc();
            Assert.IsTrue(mine.Served, "the road back at the entrance reconnects it");
        }

        [Test]
        public void TunnellingIsCheaperAndSaferThanSinkingAndDeeperBandsPayMore()
        {
            Assert.Less(World.AditWork, World.DigWork[1], "an adit is less work than the first shaft level");
            Assert.LessOrEqual(World.AditWood, World.SupportWood[1], "an adit needs no more timber than the first shaft level");
            Assert.Less(World.AditRisk, 1, "an adit is safer than the first shaft level");
            for (int l = 2; l <= World.Bands; l++)
            {
                Assert.Greater(World.MineLoad[l], World.MineLoad[l - 1], "load");
                Assert.Greater(World.MineLoad[l] / (double)World.MineTripDays[l], World.MineLoad[l - 1] / (double)World.MineTripDays[l - 1], "ore per worker-day");
                Assert.Greater(World.LevelCapMax[l], World.LevelCapMax[l - 1], "richest tile");
            }
            for (int l = 2; l <= World.MaxDepth; l++)
            {
                Assert.Greater(World.DigWork[l], World.DigWork[l - 1], "digging work");
                Assert.Greater(World.SupportWood[l], World.SupportWood[l - 1], "timber for supports");
            }
        }

        [Test]
        public void CaveInRiskRisesWithDepthAndCrowdingAndSupportsCutIt()
        {
            var w = new World(7);
            var b = new Building { T = "mine", Adit = true, Depth = 3, Shored = 0 };
            Assert.Less(w.CaveInChance(b, 0, 1), w.CaveInChance(b, 1, 1), "the adit is safer than the shaft");
            Assert.Greater(w.CaveInChance(b, 2, 1), w.CaveInChance(b, 1, 1), "deeper");
            Assert.Greater(w.CaveInChance(b, 1, 3), w.CaveInChance(b, 1, 1), "crowded");
            double unshored = w.CaveInChance(b, 2, 2), aditUnshored = w.CaveInChance(b, 0, 2);
            b.Shored = 2; b.AditShored = true;
            Assert.Less(w.CaveInChance(b, 2, 2), unshored * 0.5, "supports cut the shaft risk sharply");
            Assert.Less(w.CaveInChance(b, 0, 2), aditUnshored * 0.5, "supports cut the adit risk sharply");
            Assert.Greater(w.CaveInChance(b, 2, 2), 0, "supports never make a mine perfectly safe");
        }

        [Test]
        public void ACaveInClosesTheMineSmashesSupportsAndReopens()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            int d = 0;
            while (d++ < 600 && !mine.Crew.Any(a => a.St == CrewState.Down && !a.Dig && w.ShoredFor(mine, a.Need))) Day(w);
            var miner = mine.Crew.FirstOrDefault(a => a.St == CrewState.Down);
            Assume.That(miner != null, "nobody went underground in 600 days");
            int need = miner.Need, peasants = w.Peasants;
            Assume.That(w.ShoredFor(mine, need), "the miner's workings were never shored");
            w.Events.Clear();
            w.CaveInRiskScale = 1e9;   // the next worker-day underground caves in
            Day(w);
            Assert.AreEqual(1, mine.CaveIns, "exactly one cave-in at this mine");
            Assert.AreEqual(World.RepairDays, mine.Closed, "closed for repairs");
            Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), "everyone came up");
            Assert.IsFalse(w.ShoredFor(mine, need), "the supports where it happened were smashed");
            Assert.IsTrue(w.Events.Any(e => e.Contains("Cave-in")), "the player is told");
            Assert.LessOrEqual(w.Peasants, peasants, "nobody is born out of a cave-in");

            // while closed nobody goes down, even at zero risk; then work resumes
            w.CaveInRiskScale = 0;
            for (int i = 0; i < World.RepairDays - 1; i++) { Day(w); Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), $"day {i} of repairs"); }
            for (int i = 0; i < 60 && !mine.Crew.Any(a => a.St == CrewState.Down); i++) Day(w);
            Assert.IsTrue(mine.Crew.Any(a => a.St == CrewState.Down), "the mine reopens after repairs");
        }

        [Test]
        public void ACaveInCanKillAMiner()
        {
            // forced cave-ins on different days: some must kill (60% unshored, 25% shored), and each death leaves its crew
            int deaths = 0, caveIns = 0;
            for (int attempt = 0; attempt < 12 && deaths == 0; attempt++)
            {
                var (w, mine) = MineTown(wantOre: true);
                Assume.That(mine != null);
                w.CaveInRiskScale = 0;
                for (int d = 0; d < 300 && !mine.Crew.Any(a => a.St == CrewState.Down); d++) Day(w);
                if (!mine.Crew.Any(a => a.St == CrewState.Down)) continue;
                w.Day += attempt * 37;   // different rolls each attempt
                var mines = w.Blds.Where(b => b.T == "mine").ToList(); int crew = mines.Sum(b => b.Crew.Count), dead0 = w.MinerDeaths;
                w.CaveInRiskScale = 1e9; Day(w);
                caveIns += w.CaveIns; deaths += w.MinerDeaths;
                Assert.AreEqual(crew - (w.MinerDeaths - dead0), mines.Sum(b => b.Crew.Count), "every dead miner left their crew");
            }
            Assert.Greater(caveIns, 0);
            Assert.Greater(deaths, 0, "no cave-in ever killed anyone");
        }

        [Test]
        public void AMineOnLowlandSurveysAndFindsNothing()
        {
            var (w, mine) = MineTown(wantOre: false);
            Assume.That(mine != null, "no barren road-side lot in the test town");
            w.CaveInRiskScale = 0;
            w.Events.Clear(); var events = new List<string>();
            for (int d = 0; d < 200; d++) { Day(w); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(Reach(mine.X, mine.Y).Where(t => w.ZoneMap[t.y, t.x] == Zone.Mine).All(t => w.IsSurveyed(t.x, t.y)), "the zone was surveyed");
            Assert.IsFalse(mine.Adit, "no adit is driven where there is no ore");
            Assert.AreEqual(0, mine.Depth, "no shaft is sunk where there is no ore");
            Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), "nobody goes down");
            StringAssert.Contains("No ore found", w.MineStatus(mine));
            Assert.IsTrue(events.Any(e => e.Contains("found no ore")), "the player is told");
        }
    }
}
