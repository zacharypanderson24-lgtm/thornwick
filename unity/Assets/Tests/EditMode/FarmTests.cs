// Farming (Unity-only; ChatGPT's crop fields from codex/working-farm-fields, reworked 2026-11-16 to Zach's design): farmhouses are 2x2
// and are manually placed; the 1x1 Farmland tiles on fertile soil around a farmhouse are its plots; each farmer tends 2 plots.
// Zoning is click-click: one corner, then the opposite corner zones the whole rectangle (World.ZoneRect).
using System;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class FarmTests
    {
        const int N = Defs.N;

        [Test]
        public void SeasonalFarmersWorkFromSowingToHarvestAndCropsRipenOnlyAfterMaturity()
        {
            var w = new World(7);
            var farm = w.Blds.Single(b => b.T == "farm");
            var fields = w.FarmFields().Keys.ToArray();
            int crew = (fields.Length + World.FieldsPerFarmer - 1) / World.FieldsPerFarmer;   // one farmer for every FieldsPerFarmer fields
            Assert.That(w.Peasants, Is.GreaterThanOrEqualTo(crew), "the start has enough people for its farm");
            w.Tick();
            int planted = fields.Count(key => w.SownOn(key) == 1);
            Assert.That(planted, Is.InRange(1, fields.Length - 1));
            Assert.AreEqual(crew, farm.Workers, "a farmer for every three fields");

            w = World.LoadJson(w.SaveJson());
            farm = w.Blds.Single(b => b.T == "farm");
            w.SetFlaxPercent(100);
            for (int i = 0; i < 20 && fields.Any(key => w.SownOn(key) == 0); i++) w.Tick();   // each farmer sows one field a day
            Assert.That(fields.Count(key => w.SownOn(key) >= 2 && w.PlantedFlax(key)), Is.EqualTo(fields.Length - planted), "the rest are sown with flax");
            Assert.AreEqual(crew, farm.Workers, "farmers stay at work while the crops grow");
            Assert.That(fields.Count(key => w.SownOn(key) == 1 && !w.PlantedFlax(key)), Is.EqualTo(planted));

            w.Day = 100;
            double before = w.ResAmt[Res.Food];
            w.Tick();
            Assert.That(w.ResAmt[Res.Food], Is.LessThan(before));
            Assert.AreEqual(crew, farm.Workers);
            before = w.ResAmt[Res.Food];
            w.Tick();
            Assert.That(w.ResAmt[Res.Food], Is.GreaterThan(before), "ripe wheat is reaped");
            Assert.That(w.WheatStored, Is.GreaterThan(0).And.LessThan(w.ResAmt[Res.Food]),
                "the wheat breakdown counts harvested grain separately from the existing food reserve");
            Assert.That(w.RecentFoodProduction.GetValueOrDefault(), Is.GreaterThan(0));
            var restored = World.LoadJson(w.SaveJson());
            Assert.AreEqual(w.WheatStored, restored.WheatStored, 1e-9);
            Assert.AreEqual(w.RecentFoodProduction, restored.RecentFoodProduction);

            w.Day = 181;
            w.Tick();
            w.Tick();
            w.Day = 271;
            w.Recalc();
            Assert.That(w.Season, Is.EqualTo("Winter"));
            Assert.That(farm.Workers, Is.Zero, "farmers are free in winter");
            Assert.That(fields.All(key => w.SownOn(key) == 0), Is.True);
            w.Day = 361;
            w.Recalc();
            Assert.That(w.Year, Is.EqualTo(2));
            Assert.That(farm.Workers, Is.EqualTo(Math.Min(w.Peasants, crew)), "Prepared fields can be sown again.");
        }

        [Test]
        public void AFarmWorksOnlyNearbyFieldsAndOnlyAsManyAsItsFarmersCanTend()
        {
            var w = new World(7);
            var farm = w.Blds.Single(b => b.T == "farm");
            int c = Defs.Start;
            // paint every fertile open tile around the town as Farmland
            int zoned = 0;
            for (int y = c - 24; y <= c + 24; y++)
                for (int x = c - 24; x <= c + 24; x++)
                    if (World.Inb(x, y) && w.Fert[y, x] && w.Grid[y, x] == null && w.ZoneMap[y, x] == Zone.None && w.DecoMap[y, x] == Deco.None && !w.IsSolid(x, y))
                    { w.ZoneMap[y, x] = Zone.Farm; zoned++; }
            w.Touch();
            Assume.That(zoned, Is.GreaterThan(farm.Jobs * World.FieldsPerFarmer + 10), "seed 7 has plenty of fertile ground");
            var fields = w.FarmFields();
            int Dist(int key)
            {
                int x = key % N, y = key / N;
                return Math.Max(Math.Max(0, Math.Max(farm.X - x, x - (farm.X + farm.Sz - 1))), Math.Max(0, Math.Max(farm.Y - y, y - (farm.Y + farm.Sz - 1))));
            }
            int inReach = 0;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                if (w.ZoneMap[y, x] == Zone.Farm && w.Fert[y, x] && w.Grid[y, x] == null && w.DecoMap[y, x] == Deco.None && !w.IsSolid(x, y) && Dist(World.Tk(x, y)) <= World.FarmReach) inReach++;
            int cap = farm.Jobs * World.FieldsPerFarmer;
            Assert.AreEqual(Math.Min(cap, inReach), w.FieldsOf(farm), "a farm works the fields in its reach, at most FieldsPerFarmer per job");
            foreach (var key in fields.Keys) Assert.LessOrEqual(Dist(key), World.FarmReach, "only fields within reach of the farmhouse");
            Assert.Greater(w.FallowFields(), 0, "the rest of the Farmland lies fallow");
            w.Recalc();
            int crew = (w.FieldsOf(farm) + World.FieldsPerFarmer - 1) / World.FieldsPerFarmer;
            Assert.AreEqual(Math.Min(w.Peasants, Math.Min(farm.Jobs, crew)), farm.Workers, "one farmer for every FieldsPerFarmer fields");
        }

        [Test]
        public void AShortHandedFarmSowsOnlyWhatItsFarmersCanTend()
        {
            var w = new World(7) { Peasants = 1 };
            var farm = w.Blds.Single(b => b.T == "farm");
            // more Farmland in the farm's reach than one farmer can tend
            int c = Defs.Start;
            for (int y = c - 12; y <= c + 12; y++)
                for (int x = c - 12; x <= c + 12; x++)
                    if (w.Fert[y, x] && w.Grid[y, x] == null && w.ZoneMap[y, x] == Zone.None && w.DecoMap[y, x] == Deco.None && !w.IsSolid(x, y)) w.ZoneMap[y, x] = Zone.Farm;
            w.Touch();
            var fields = w.FarmFields().Keys.ToArray();
            Assume.That(fields.Length, Is.GreaterThan(World.FieldsPerFarmer + 5));
            for (int i = 0; i < 30; i++) { w.Peasants = 1; w.ResAmt[Res.Food] = 200; w.Tick(); }
            Assert.AreEqual(World.FieldsPerFarmer, fields.Count(key => w.SownOn(key) >= 1), "one farmer sows only the fields one farmer can tend");
        }


        [Test]
        public void ANewGameStartsWithA2x2FarmhouseAndItsPlots()
        {
            var w = new World(7);
            var farm = w.Blds.Single(b => b.T == "farm");
            Assert.AreEqual(2, farm.Sz, "the starting farmhouse is 2x2");
            Assert.IsTrue(farm.Served, "beside the starting road");
            var plots = w.FarmFields().Where(p => p.Value == farm).Select(p => p.Key).ToList();
            Assert.AreEqual(8, plots.Count, "8 starting plots");
            foreach (int k in plots) { Assert.IsTrue(w.Fert[k / N, k % N], "plots are fertile"); Assert.IsNull(w.Grid[k / N, k % N], "plots are open 1x1 tiles"); }
            Assert.Greater(w.FarmFood(), (w.Peasants + w.Soldiers * 1.5) * 0.2, "the start feeds the first settlers with food to spare");
            Assert.AreEqual(1, new World(7, TerrainStyle.Classic).Blds.Single(b => b.T == "farm").Sz, "the browser-compatible world keeps the browser's 1x1 farm");
        }

        [Test]
        public void FarmersAndPlotsBothLimitFood()
        {
            var w = new World(7);
            var farm = w.Blds.Single(b => b.T == "farm");
            // seasonal farming: a field gives WheatYield once a year; FarmFood spreads that over the year
            int c = Defs.Start, added = 0;
            for (int y = c - 8; y <= c + 8 && added < 20; y++)   // a few more plots than one farmer can tend, fewer than the farm can
                for (int x = c - 8; x <= c + 8 && added < 20; x++)
                    if (w.Fert[y, x] && w.Grid[y, x] == null && w.ZoneMap[y, x] == Zone.None && w.DecoMap[y, x] == Deco.None && !w.IsSolid(x, y)) { w.ZoneMap[y, x] = Zone.Farm; added++; }
            w.Touch();
            w.SetFlaxPercent(0);   // all wheat, so every plot is food
            var plots = w.FarmFields().Where(p => p.Value == farm).Select(p => p.Key).ToArray();
            Assume.That(plots.Length, Is.GreaterThan(World.FieldsPerFarmer));
            double perField = World.WheatYield / (double)World.YearLength;
            farm.Workers = 0;
            Assert.AreEqual(0, w.FarmFood(), 1e-9, "no farmers, no food");
            farm.Workers = 1;
            Assert.AreEqual(World.FieldsPerFarmer * perField, w.FarmFood(), 1e-9, "one farmer tends FieldsPerFarmer plots");
            farm.Workers = farm.Jobs;
            Assert.AreEqual(Math.Min(plots.Length, farm.Jobs * World.FieldsPerFarmer) * perField, w.FarmFood(), 1e-9, "plenty of farmers: the plots are the limit");
            Assume.That(plots.Length, Is.LessThan(farm.Jobs * World.FieldsPerFarmer), "the farm has room for one more plot");
            w.ZoneMap[plots[0] / N, plots[0] % N] = Zone.None;
            w.Touch();   // direct edits must report themselves (the field list is cached per world change)
            Assert.AreEqual((plots.Length - 1) * perField, w.FarmFood(), 1e-9, "an unzoned tile is no longer a plot");
            Assert.IsFalse(w.FarmFields().ContainsKey(plots[0]));
        }

        [Test]
        public void CropZonesRemainFieldsUntilAFarmhouseIsPlaced()
        {
            var w = new World(7);
            int c = Defs.Start;
            var starter = w.Blds.Single(b => b.T == "farm");
            var field = w.FarmFields().Keys.First();
            Assert.IsTrue(w.Remove(starter.X, starter.Y).Ok);
            Assert.AreEqual(Zone.Farm, w.ZoneMap[field / N, field % N]);
            w.Develop();
            Assert.IsFalse(w.Blds.Any(b => b.T == "farm"), "crop zones do not place buildings");
            Assert.IsTrue(w.Place("farm", c - 2, c + 5).Ok);
            var farm = w.Grid[c + 5, c - 2];
            Assert.AreEqual(2, farm.Sz);
            Assert.AreSame(farm, w.FarmFields()[field], "the surviving field belongs to the manually placed farmhouse");
            Assert.IsNull(w.Grid[field / N, field % N], "a field is not a building");
        }

        [Test]
        public void ZoningARectangleZonesEveryAllowedTileBetweenTheTwoCorners()
        {
            var w = new World(7);
            w.ClaimAll();   // the blocks picked below can lie anywhere on the map
            // an open, unbuilt 6x4 block away from the town
            var (x0, y0) = Enumerable.Range(0, N * N).Select(k => (x: k % N, y: k / N))
                .First(t => t.x + 5 < N && t.y + 3 < N && Math.Abs(t.x - Defs.Start) > 12 &&
                            Enumerable.Range(0, 24).All(i => w.Grid[t.y + i / 6, t.x + i % 6] == null && w.ZoneCheck(Zone.Res, t.x + i % 6, t.y + i / 6).Ok));
            var (zoned, skipped, _) = w.ZoneRect(Zone.Res, x0 + 5, y0 + 3, x0, y0);   // corners in either order
            Assert.AreEqual(24, zoned); Assert.AreEqual(0, skipped);
            for (int y = y0; y <= y0 + 3; y++) for (int x = x0; x <= x0 + 5; x++) Assert.AreEqual(Zone.Res, w.ZoneMap[y, x]);
            Assert.AreEqual(0, w.ZoneRect(Zone.Res, x0, y0, x0 + 5, y0 + 3).zoned, "zoning it again changes nothing");

            // Farmland only takes the fertile tiles of a rectangle and says why it skipped the rest
            var (fx, fy) = Enumerable.Range(0, N * N).Select(k => (x: k % N, y: k / N))
                .First(t => t.x + 7 < N && t.y + 7 < N && Enumerable.Range(0, 64).Any(i => w.Fert[t.y + i / 8, t.x + i % 8]) &&
                            Enumerable.Range(0, 64).Any(i => !w.Fert[t.y + i / 8, t.x + i % 8] && w.Grid[t.y + i / 8, t.x + i % 8] == null && !w.IsSolid(t.x + i % 8, t.y + i / 8) && !w.HasTree(t.x + i % 8, t.y + i / 8)));
            var (fz, fs, why) = w.ZoneRect(Zone.Farm, fx, fy, fx + 7, fy + 7);
            Assert.Greater(fz, 0); Assert.Greater(fs, 0);
            for (int y = fy; y <= fy + 7; y++) for (int x = fx; x <= fx + 7; x++)
                if (w.ZoneMap[y, x] == Zone.Farm && w.Grid[y, x] == null) Assert.IsTrue(w.Fert[y, x], $"Farmland on infertile {x},{y}");
            Assert.IsNotEmpty(why);
        }

        [Test]
        public void UnzoningARectangleClearsOpenTilesAndLeavesBuildingsAlone()
        {
            var w = new World(7);
            var farm = w.Blds.Single(b => b.T == "farm");
            const int starterRange = 3;   // the starting plots lie within 3 tiles of the starting farmhouse (farms themselves have no radius)
            int x0 = farm.X - starterRange, y0 = farm.Y - starterRange;
            int x1 = farm.X + farm.Sz - 1 + starterRange, y1 = farm.Y + farm.Sz - 1 + starterRange;
            int before = w.FarmFields().Count; Assert.Greater(before, 0);
            int blds = w.Blds.Count;
            int cleared = w.ClearZoneRect(x1, y1, x0, y0);   // corners in either order
            Assert.Greater(cleared, 0);
            Assert.AreEqual(0, w.FarmFields().Count, "every starting field is unzoned");
            Assert.AreEqual(blds, w.Blds.Count, "no building is removed");
            Assert.AreEqual(0, w.ClearZoneRect(x0, y0, x1, y1), "unzoning it again changes nothing");
            Assert.AreEqual(0, w.ClearZoneRect(-5, -5, -1, -1), "off the map is ignored");
        }
    }
}
