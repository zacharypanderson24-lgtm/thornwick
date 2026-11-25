// Work areas: farms have no radius (all Farmland is worked by the served farmhouses together), lodges, quarries, mines and foresters
// work a large square around the building (Defs.WorkR), and a building's jobs are capped by the work there is to do.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WorkReachTests
    {
        const int N = Defs.N;
        static readonly int C = Defs.Start;

        static IEnumerable<(int x, int y)> AllTiles() { for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) yield return (x, y); }

        static void Road(World w, IEnumerable<(int x, int y)> tiles)
        {
            foreach (var (x, y) in tiles)
            {
                if (w.Grid[y, x] != null) continue;
                w.DecoMap[y, x] = Deco.None;
                w.AddBld("road", x, y);
            }
            w.Recalc();
        }

        /// <summary>Open, gentle ground: no solid tile and no steep step within `r` tiles of (x, y).</summary>
        static bool Gentle(World w, int x, int y, int r)
        {
            for (int j = y - r; j <= y + r; j++)
                for (int i = x - r; i <= x + r; i++)
                    if (!World.Inb(i, j) || w.IsSolid(i, j) || w.TileSpread(i, j) > 1 || w.Grid[j, i] != null) return false;
            return true;
        }

        [Test]
        public void NewForestersAndSawmillsReserveTwoTilesPerSideAndLegacyFootprintsSurvive()
        {
            var w = new World(7);
            Array.Clear(w.Hv, 0, w.Hv.Length);
            int x = C + 1, y = C + 4;
            for (int row = y; row < y + 4; row++)
                for (int column = x; column < x + 5; column++)
                    w.DecoMap[row, column] = Deco.None;

            Assert.That(w.Place("forester", x, y).Ok, Is.True);
            Assert.That(w.Place("sawmill", x, y + 2).Ok, Is.True);
            Assert.That(w.Grid[y + 1, x + 1], Is.SameAs(w.Grid[y, x]));
            Assert.That(w.Grid[y + 3, x + 1], Is.SameAs(w.Grid[y + 2, x]));
            Assert.That(w.Grid[y, x].Served && w.Grid[y + 2, x].Served, Is.True);
            Assert.That(Defs.PlacementSize("well"), Is.EqualTo(1));

            w.AddBld("forester", x + 3, y, 1);
            w.AddBld("sawmill", x + 3, y + 2, 1);
            var saved = World.LoadJson(w.SaveJson());
            Assert.That(saved.Grid[y, x].Sz, Is.EqualTo(2));
            Assert.That(saved.Grid[y + 2, x].Sz, Is.EqualTo(2));
            Assert.That(saved.Grid[y, x + 3].Sz, Is.EqualTo(1));
            Assert.That(saved.Grid[y + 2, x + 3].Sz, Is.EqualTo(1));
        }

        [Test]
        public void FarmlandIsWorkedOnlyWithinReachOfAServedFarmhouse()
        {
            var w = new World(7); w.ClaimAll();   // these tests build far from the town
            var first = w.Blds.Single(b => b.T == "farm");
            int starter = w.FarmFields().Count;
            // fertile open land 30+ tiles from every farmhouse
            var far = AllTiles().Where(t => Math.Max(Math.Abs(t.x - first.X), Math.Abs(t.y - first.Y)) >= 30 && w.Fert[t.y, t.x] && w.DecoMap[t.y, t.x] == Deco.None && w.ZoneCheck(Zone.Farm, t.x, t.y).Ok).Take(60).ToList();
            Assume.That(far.Count >= 40, "enough fertile land far away");
            foreach (var (x, y) in far) w.SetZone(Zone.Farm, x, y);
            w.Recalc();
            var fields = w.FarmFields();
            Assert.AreEqual(starter, fields.Count, "far Farmland is out of every farm's reach");
            Assert.IsFalse(far.Any(t => fields.ContainsKey(World.Tk(t.x, t.y))));
            Assert.GreaterOrEqual(w.FallowFields(), far.Count, "it lies fallow");
            // a second farmhouse beside the street: every field goes to a farm within reach, none over its limit
            Road(w, World.LPath(C, C + 7, C, C + 10));
            var second = w.AddBld("farm", C + 1, C + 8, 2); w.Recalc();
            Assume.That(second.Served);
            fields = w.FarmFields();
            int a = fields.Values.Count(f => f == first), b = fields.Values.Count(f => f == second);
            Assert.AreEqual(fields.Count, a + b);
            foreach (var kv in fields)
            {
                var f = kv.Value; int x = kv.Key % Defs.N, y = kv.Key / Defs.N;
                int d = Math.Max(Math.Max(0, Math.Max(f.X - x, x - (f.X + f.Sz - 1))), Math.Max(0, Math.Max(f.Y - y, y - (f.Y + f.Sz - 1))));
                Assert.LessOrEqual(d, World.FarmReach, "worked by a farm within reach");
            }
            Assert.LessOrEqual(a, first.Jobs * World.FieldsPerFarmer); Assert.LessOrEqual(b, second.Jobs * World.FieldsPerFarmer);
            // an unserved farmhouse works nothing
            var lone = w.AddBld("farm", C + 8, C - 9, 2); w.Recalc();
            Assert.IsFalse(lone.Served);
            Assert.IsFalse(w.FarmFields().Values.Contains(lone));
        }

        [Test]
        public void FarmersNeededFollowTheFieldsThatNeedWork()
        {
            var w = new World(7); w.ClaimAll();   // these tests build far from the town
            var farm = w.Blds.Single(b => b.T == "farm");
            Assert.AreEqual(10, farm.Jobs, "the 2x2 farmhouse allows 10 farmers");
            w.Tick();   // spring: the starting fields are sown
            Assert.That(w.ActiveJobs(farm), Is.LessThanOrEqualTo(farm.Jobs));
            // no fields at all: nobody is needed
            foreach (var key in w.FarmFields().Keys.ToList()) w.ZoneMap[key / N, key % N] = Zone.None;
            w.Touch(); w.Recalc();
            Assert.AreEqual(0, w.ActiveJobs(farm), "no Farmland, no farmers needed");
        }

        [Test]
        public void ALodgeNeedsOnlyAsManyWoodcuttersAsThereAreMarkedTrees()
        {
            var w = new World(7); w.ClaimAll();   // these tests build far from the town
            var site = AllTiles().Where(t => Math.Abs(t.x - C) + Math.Abs(t.y - C) is > 14 and < 30 && Gentle(w, t.x, t.y, 12)).OrderBy(t => Math.Abs(t.x - C) + Math.Abs(t.y - C)).FirstOrDefault();
            Assume.That(site.x > 0, "open ground for a lodge");
            var lodge = w.AddBld("wood", site.x, site.y, 2);
            Road(w, World.LPath(C, C + 7, site.x, site.y + 2));
            Assume.That(lodge.Served);
            Assert.AreEqual(3, lodge.Jobs);
            Assert.AreEqual(0, w.ActiveJobs(lodge), "nothing marked: nobody needed");
            var trees = new List<(int x, int y)>();
            for (int i = 0; i < 5; i++) trees.Add((site.x + 10, site.y - 3 + i));
            for (int n = 1; n <= 5; n++)
            {
                var (tx, ty) = trees[n - 1];
                w.DecoMap[ty, tx] = Deco.Tree; w.ZoneMap[ty, tx] = Zone.Wood; w.Touch(); w.Recalc();
                Assert.AreEqual(Math.Min(3, n), w.ActiveJobs(lodge), $"{n} marked tree(s), 10 tiles from the lodge");
            }
            // a tree beyond the work area does not count
            w.DecoMap[site.y, site.x + 1 + Defs.WorkR + 2] = Deco.Tree; w.ZoneMap[site.y, site.x + 1 + Defs.WorkR + 2] = Zone.Wood;
            for (int i = 0; i < 5; i++) { var (tx, ty) = trees[i]; w.DecoMap[ty, tx] = Deco.None; w.ZoneMap[ty, tx] = Zone.None; }
            w.Touch(); w.Recalc();
            Assert.AreEqual(0, w.ActiveJobs(lodge), "out of the work area");
        }

        [Test]
        public void WoodcuttersWalkToTreesTenTilesAwayAndFellThem()
        {
            var w = new World(7); w.ClaimAll();   // these tests build far from the town
            var site = AllTiles().Where(t => Math.Abs(t.x - C) + Math.Abs(t.y - C) is > 14 and < 30 && Gentle(w, t.x, t.y, 12)).OrderBy(t => Math.Abs(t.x - C) + Math.Abs(t.y - C)).FirstOrDefault();
            Assume.That(site.x > 0, "open ground for a lodge");
            w.Prio = new[] { "timber", "farming", "woodwork", "quarrying", "mining" };
            var lodge = w.AddBld("wood", site.x, site.y, 2);
            Road(w, World.LPath(C, C + 7, site.x, site.y + 2));
            Assume.That(lodge.Served);
            int tx = site.x + 10, ty = site.y;
            w.DecoMap[ty, tx] = Deco.Tree; w.ZoneMap[ty, tx] = Zone.Wood; w.Touch();
            double logs0 = w.ResAmt[Res.Logs] + 0;
            for (int day = 0; day < 400 && w.DecoMap[ty, tx] == Deco.Tree; day++)
            {
                w.ResAmt[Res.Food] = 300; w.ResAmt[Res.Wood] = 300; w.ResAmt[Res.Logs] = Math.Min(w.ResAmt[Res.Logs], 20);
                w.Tick();
            }
            Assert.AreNotEqual(Deco.Tree, w.DecoMap[ty, tx], "the crew walked 10 tiles out and felled the marked tree");
        }

        [Test]
        public void AQuarryAndAMineNeedWorkersOnlyWhereTilesAreZoned()
        {
            var w = new World(7); w.ClaimAll();   // these tests build far from the town
            // a quarry beside rock: zone one rock tile, then none
            var rockSite = AllTiles().Where(t => w.DecoMap[t.y, t.x] == Deco.Rock && !w.IsMountain(t.x, t.y) && w.RockWorkable(t.x, t.y)).FirstOrDefault();
            Assume.That(rockSite.x > 0, "an outcrop");
            var spot = AllTiles().Where(t => w.CanPlace("quarry", t.x, t.y).Ok && Math.Max(Math.Abs(t.x - rockSite.x), Math.Abs(t.y - rockSite.y)) is > 3 and < 10)
                .OrderBy(t => Math.Max(Math.Abs(t.x - rockSite.x), Math.Abs(t.y - rockSite.y))).FirstOrDefault();
            Assume.That(spot.x > 0);
            var quarry = w.AddBld("quarry", spot.x, spot.y, 2); w.Recalc();
            Assert.AreEqual(0, w.ActiveJobs(quarry), "no Mining zone, no quarrymen needed");
            w.ZoneMap[rockSite.y, rockSite.x] = Zone.Mine; w.Touch(); w.Recalc();
            Assert.AreEqual(1, w.ActiveJobs(quarry), "one zoned rock tile, 3-10 tiles away, needs one quarryman");
        }

        [Test]
        public void ForestersPlantAcrossTheWholeWorkArea()
        {
            Assert.AreEqual(Defs.WorkR, Defs.ForesterR, "foresters work the same large area as lodges");
            Assert.That(Defs.WorkR, Is.GreaterThanOrEqualTo(10), "work areas are large, not the old 3 tiles");
            Assert.That(World.CrewDepth, Is.GreaterThanOrEqualTo(2 * Defs.WorkR), "crews can walk to the far corners of the work area");
        }

        [Test]
        public void HandSetWorkerLimitsCapATradeAreSavedAndAutoFillsTheRest()
        {
            var w = new World(7); w.ClaimAll();
            var site = AllTiles().Where(t => Math.Abs(t.x - C) + Math.Abs(t.y - C) is > 14 and < 30 && Gentle(w, t.x, t.y, 12)).OrderBy(t => Math.Abs(t.x - C) + Math.Abs(t.y - C)).FirstOrDefault();
            Assume.That(site.x > 0, "open ground for a lodge");
            var lodge = w.AddBld("wood", site.x, site.y, 2);
            Road(w, World.LPath(C, C + 7, site.x, site.y + 2));
            Assume.That(lodge.Served);
            for (int i = 0; i < 3; i++) { int tx = site.x + 6, ty = site.y - 1 + i; w.DecoMap[ty, tx] = Deco.Tree; w.ZoneMap[ty, tx] = Zone.Wood; }
            w.Touch(); w.Peasants = 40; w.Recalc();
            Assume.That(w.ActiveJobs(lodge) == 3, "three marked trees, three woodcutter jobs");
            Assert.AreEqual(3, lodge.Workers, "automatic: every job filled");

            w.SetTradeTarget("timber", 1);
            Assert.AreEqual(1, lodge.Workers, "hand-set limit of 1");
            w.AdjustTradeTarget("timber", 1);
            Assert.AreEqual(2, lodge.Workers, "+ raises it by one");
            w.AdjustTradeTarget("timber", -1); w.AdjustTradeTarget("timber", -1); w.AdjustTradeTarget("timber", -1);
            Assert.AreEqual(0, lodge.Workers, "- cannot go below zero");

            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(0, loaded.TradeTarget["timber"], "the limit is saved");

            w.AdjustTradeTarget("timber", 1); w.AdjustTradeTarget("timber", 1); w.AdjustTradeTarget("timber", 1);
            Assert.IsFalse(w.TradeTarget.ContainsKey("timber"), "+ up to the jobs goes back to automatic");
            Assert.AreEqual(3, lodge.Workers);
            w.SetTradeTarget("timber", 2); w.SetTradeTarget("timber", null);
            Assert.AreEqual(3, lodge.Workers, "Auto clears the limit");
        }
    }
}
