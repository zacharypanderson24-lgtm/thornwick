// Tunnel mining (Unity-only, World.Mining.cs): ore in hills and mountains, a tunnel dug tile by tile through the Mining zone,
// ore taken from its walls, timber supports and cave-ins. These are rule tests, not browser parity (the browser still mines surface ore).
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
        const int PatchR = 3;   // the Mining zone these tests paint around a mine (a mine itself can work much farther, Defs.WorkR)
        static IEnumerable<(int x, int y)> Patch(int x, int y)
        {
            for (int j = y - PatchR; j <= y + PatchR; j++) for (int i = x - PatchR; i <= x + PatchR; i++) if (World.Inb(i, j)) yield return (i, j);
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
        /// The test town (SimParityTests.Layout), miners hired first, and one mine with a small patch around it painted as a Mining zone.
        /// wantOre: the nearest lot at the foot of high ground with ore its adit can reach (a road is laid to it).
        /// Otherwise: a lowland lot in the town with no ore at any band anywhere in reach.
        /// </summary>
        internal static (World w, Building mine) MineTown(bool wantOre)
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
                var cp = w.CanPlace("mine", x, y); if ((!cp.Ok && !(cp.Reason ?? "").StartsWith("No ore within")) || w.GenLevel(x, y) >= 1) continue;   // the mine stands at the foot, not on the hill (an ore-free lot is fine for the no-ore case)
                var reach = Patch(x, y).ToList();
                if (wantOre)
                {
                    if (reach.Count(t => OreUnder(w, t.x, t.y)) < 4) continue;   // ore within the tunnel's zone
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
            Assert.Less(showings, 600 * (N / 128) * (N / 128), "showings should be hints, not a carpet");
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
            w.ClaimAll();   // the tiles picked below can lie anywhere on the map
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
        public void QuarryingAMountainDoesNotMoveOrDestroyItsOre()
        {
            var w = new World(7);
            var t = AllTiles().First(p => w.IsMountain(p.x, p.y) && OreUnder(w, p.x, p.y));
            var before = Enumerable.Range(1, World.Bands).Select(l => (w.OreAt(t.x, t.y, l), w.GenLevel(t.x, t.y))).ToArray();
            // quarry the tile's stone out and lower its corners, as heavy quarrying would
            w.Dig[World.Tk(t.x, t.y)] = w.RockCap(t.x, t.y);
            for (int dy = 0; dy <= 1; dy++) for (int dx = 0; dx <= 1; dx++) w.Hv[(t.y + dy) * (N + 1) + t.x + dx] = 0;
            w.Touch();
            var after = Enumerable.Range(1, World.Bands).Select(l => (w.OreAt(t.x, t.y, l), w.GenLevel(t.x, t.y))).ToArray();
            CollectionAssert.AreEqual(before, after);
        }

        static bool Touches(Building mine, int k)
        {
            int x = k % N, y = k / N;
            foreach (var (mx, my) in mine.Tiles()) if (Math.Abs(mx - x) + Math.Abs(my - y) == 1) return true;
            return false;
        }

        [Test]
        public void ATunnelStartsAtTheMineGrowsThroughTheZoneAndBringsUpOre()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no lot at the foot of ore-bearing high ground that a road can reach");
            w.CaveInRiskScale = 0;
            var showings = AllTiles().Where(t => w.DecoMap[t.y, t.x] == Deco.Ore).ToList();
            bool sawDown = false;
            for (int d = 0; d < 400; d++) { Day(w); sawDown |= mine.Crew.Any(a => a.St == CrewState.Down); }
            Assert.IsTrue(sawDown, "nobody went underground");
            Assert.Greater(mine.Tunnel.Count, 0, "no tunnel was dug");
            Assert.IsTrue(Touches(mine, mine.Tunnel[0]), "the tunnel starts at the mine's walls");
            for (int i = 0; i < mine.Tunnel.Count; i++)
            {
                int k = mine.Tunnel[i], x = k % N, y = k / N;
                Assert.AreEqual(Zone.Mine, w.ZoneMap[y, x], "the tunnel stays inside the Mining zone");
                Assert.IsTrue(w.IsTunnel(x, y));
                Assert.IsTrue(w.IsSolid(x, y), "a tunnel tile is not open ground");
                if (i > 0)
                {
                    bool joined = Touches(mine, k);
                    for (int j = 0; j < i && !joined; j++) joined = Math.Abs(mine.Tunnel[j] % N - x) + Math.Abs(mine.Tunnel[j] / N - y) == 1;
                    Assert.IsTrue(joined, $"tunnel tile {i} is joined to the mine or the tunnel before it");
                }
                for (int j = y - World.RevealR; j <= y + World.RevealR; j++)
                    for (int ii = x - World.RevealR; ii <= x + World.RevealR; ii++)
                        if (World.Inb(ii, j)) Assert.IsTrue(w.IsSurveyed(ii, j), "digging reveals the ground around the tunnel");
            }
            var mineTaken = w.OreTaken.Where(kv => { int k = kv.Key % (N * N); return Math.Max(Math.Abs(k % N - mine.X), Math.Abs(k / N - mine.Y)) <= Defs.WorkR; }).ToList();
            Assert.Greater(mineTaken.Sum(kv => kv.Value), 0, "no ore was brought up");
            foreach (var kv in mineTaken)
            {
                int k = kv.Key % (N * N), x = k % N, y = k / N;
                Assert.IsTrue(mine.Tunnel.Any(t => Math.Max(Math.Abs(t % N - x), Math.Abs(t / N - y)) <= World.WallR), $"ore at {x},{y} was taken from outside the tunnel's walls");
            }
            foreach (var (x, y) in showings) Assert.IsTrue(w.DecoMap[y, x] == Deco.Ore || w.Grid[y, x] != null, $"the showing at {x},{y} was mined away");
            foreach (var m in w.Blds.Where(b => b.T == "mine" && b != mine))
            {
                int f = w.MineFaceAt(m.X, m.Y); if (f < 0) continue;
                var (ex, ey) = World.EntranceTile(m.X, m.Y, f);
                Assert.AreEqual("road", w.Grid[ey, ex]?.T, $"the mine grown at {m.X},{m.Y} has no road at its entrance {ex},{ey}");
            }
        }

        [Test]
        public void TheTunnelOnlyRunsThroughZonedGroundThatTouchesTheMine()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            foreach (var (i, j) in Reach(mine.X, mine.Y)) if (w.ZoneMap[j, i] == Zone.Mine) w.ZoneMap[j, i] = Zone.None;
            // a zoned ring two tiles out from the mine's walls, with an unzoned gap between
            int placed = 0;
            foreach (var (i, j) in Reach(mine.X, mine.Y))
            {
                double d = Math.Max(Math.Abs(i - mine.X - 0.5), Math.Abs(j - mine.Y - 0.5));
                if (d >= 3.5 && d <= 5 && w.Grid[j, i] == null && w.ZoneCheck(Zone.Mine, i, j).Ok) { w.SetZone(Zone.Mine, i, j); placed++; }
            }
            Assume.That(placed >= 6, "no ring of zonable ground around the mine");
            w.Recalc();
            for (int d = 0; d < 120; d++) Day(w);
            Assert.AreEqual(0, mine.Tunnel.Count, "a zone that does not touch the mine cannot be dug");
            StringAssert.Contains("Nothing to dig", w.MineStatus(mine));
        }

        [Test]
        public void ATunnelIsNotQuarriedAndSealsWhenTheMineIsPulledDown()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            for (int d = 0; d < 200 && mine.Tunnel.Count < 2; d++) Day(w);
            Assume.That(mine.Tunnel.Count >= 2, "no tunnel was dug");
            var tiles = mine.Tunnel.ToList();
            foreach (int k in tiles)
            {
                Assert.AreEqual(World.QuarryTile.None, w.QuarryTileState(k % N, k / N), "a quarry does not dig a tunnel tile");
                Assert.IsFalse(w.RockWorkable(k % N, k / N), "a tunnel tile is not workable rock");
            }
            w.Remove(mine.X, mine.Y);
            w.Recalc();
            foreach (int k in tiles) Assert.IsFalse(w.IsTunnel(k % N, k / N), "pulling the mine down seals its tunnel");
        }

        [Test]
        public void TheTunnelDoesNotEatTheWholeZoneWhileThereIsOreToTake()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            for (int d = 0; d < 400; d++) Day(w);
            Assert.Greater(mine.Tunnel.Count, 0);
            Assert.Less(mine.Tunnel.Count, Reach(mine.X, mine.Y).Count(t => w.ZoneMap[t.y, t.x] == Zone.Mine), "the tunnel does not dig out the whole zone while there is ore to take");
        }

        [Test]
        public void RicherBandsPayMoreForTheSameTrip()
        {
            for (int l = 2; l <= World.Bands; l++)
            {
                Assert.Greater(World.MineLoad[l], World.MineLoad[l - 1], "load");
                Assert.Greater(World.LevelCapMax[l], World.LevelCapMax[l - 1], "richest tile");
                Assert.Greater(World.MineLoad[l] / (double)(World.TripBase + l + 2), World.MineLoad[l - 1] / (double)(World.TripBase + l - 1 + 2), "ore per worker-day at the same distance");
            }
        }

        [Test]
        public void CaveInRiskRisesWithLengthAndCrowdingAndSupportsCutIt()
        {
            var w = new World(7);
            var b = new Building { T = "mine" };
            for (int i = 0; i < 2; i++) b.Tunnel.Add(i);
            double shortTunnel = w.CaveInChance(b, 1);
            for (int i = 2; i < 12; i++) b.Tunnel.Add(i);
            Assert.Greater(w.CaveInChance(b, 1), shortTunnel, "a longer tunnel is riskier");
            Assert.Greater(w.CaveInChance(b, 3), w.CaveInChance(b, 1), "crowded");
            double unshored = w.CaveInChance(b, 2);
            foreach (int k in b.Tunnel) b.Shored.Add(k);
            Assert.Less(w.CaveInChance(b, 2), unshored * 0.5, "supports cut the risk sharply");
            Assert.Greater(w.CaveInChance(b, 2), 0, "supports never make a mine perfectly safe");
        }

        [Test]
        public void ACaveInClosesTheMineSmashesFarSupportsAndReopens()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            int d = 0;
            while (d++ < 600 && !(mine.Shored.Count >= 2 && mine.Crew.Any(a => a.St == CrewState.Down))) Day(w);
            Assume.That(mine.Shored.Count >= 2 && mine.Crew.Any(a => a.St == CrewState.Down), "no shored tunnel with a miner in it in 600 days");
            int peasants = w.Peasants, shoredBefore = mine.Shored.Count;
            w.Events.Clear();
            w.PopAcc = -3;   // no births today: the assert below is about the cave-in, not about growth
            w.CaveInRiskScale = 1e9;   // the next worker-day underground caves in
            Day(w);
            Assert.AreEqual(1, mine.CaveIns, "exactly one cave-in at this mine");
            Assert.AreEqual(World.RepairDays, mine.Closed, "closed for repairs");
            Assert.IsFalse(mine.Crew.Any(a => a.St == CrewState.Down), "everyone came up");
            Assert.Less(mine.Shored.Count, shoredBefore, "the supports at the far end were smashed");
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
        public void RequiredSupportsKeepTheTunnelAtMostOneTileAheadOfTheTimber()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            w.SetMineSupportsRequired(true);
            for (int d = 0; d < 300; d++)
            {
                Day(w);
                Assert.LessOrEqual(mine.Tunnel.Count - mine.Shored.Count, 1, $"day {d}: the tunnel ran more than a tile ahead of its supports");
            }
            Assert.Greater(mine.Tunnel.Count, 1, "the tunnel still advances when timber is there");
        }

        [Test]
        public void AnUnshoredTunnelWaitsForTimberWhenSupportsAreRequired()
        {
            var (w, mine) = MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.SetMineSupportsRequired(true);
            foreach (var (i, j) in Reach(mine.X, mine.Y)) if (Touches(mine, World.Tk(i, j)) && w.ZoneMap[j, i] == Zone.Mine && w.Grid[j, i] == null) { mine.Tunnel.Add(World.Tk(i, j)); break; }
            Assume.That(mine.Tunnel.Count == 1, "no zoned tile beside the mine");
            w.Touch();
            StringAssert.Contains("Waiting for timber", w.MineStatus(mine));
            w.SetMineSupportsRequired(false);
            StringAssert.DoesNotContain("Waiting for timber", w.MineStatus(mine));
        }

        [Test]
        public void AMineOnLowlandDigsItsZoneAndFindsNothing()
        {
            var (w, mine) = MineTown(wantOre: false);
            Assume.That(mine != null, "no barren road-side lot in the test town");
            w.CaveInRiskScale = 0;
            w.Events.Clear(); var events = new List<string>();
            for (int d = 0; d < 500; d++) { Day(w); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.AreEqual(0, w.OreTaken.Count(kv => { int k = kv.Key % (N * N); return Math.Max(Math.Abs(k % N - mine.X), Math.Abs(k / N - mine.Y)) <= Defs.WorkR; }), "no ore taken");
            StringAssert.Contains("foot of a hill", w.MineStatus(mine));
            Assert.IsTrue(events.Any(e => e.Contains("found no ore")), "the player is told");
        }
    }
}
