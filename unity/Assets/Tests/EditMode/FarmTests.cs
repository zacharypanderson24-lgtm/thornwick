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
            var plots = w.FarmFields().Where(p => p.Value == farm).Select(p => p.Key).ToArray();
            farm.Workers = 0;
            Assert.AreEqual(0, w.FarmFood(), 1e-9, "no farmers, no food");
            farm.Workers = 1;
            Assert.AreEqual(0.8, w.FarmFood(), 1e-9, "one farmer tends 2 plots");
            farm.Workers = 10;
            Assert.AreEqual(plots.Length * 0.4, w.FarmFood(), 1e-9, "plenty of farmers: the plots are the limit");
            w.ZoneMap[plots[0] / N, plots[0] % N] = Zone.None;
            Assert.AreEqual((plots.Length - 1) * 0.4, w.FarmFood(), 1e-9, "an unzoned tile is no longer a plot");
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
            int x0 = farm.X - World.FarmFieldRange, y0 = farm.Y - World.FarmFieldRange;
            int x1 = farm.X + farm.Sz - 1 + World.FarmFieldRange, y1 = farm.Y + farm.Sz - 1 + World.FarmFieldRange;
            int before = w.FarmFields().Count; Assert.Greater(before, 0);
            int blds = w.Blds.Count;
            int cleared = w.ClearZoneRect(x1, y1, x0, y0);   // corners in either order
            Assert.Greater(cleared, 0);
            Assert.AreEqual(0, w.FarmFields().Count, "every field in reach of the farmhouse is unzoned");
            Assert.AreEqual(blds, w.Blds.Count, "no building is removed");
            Assert.AreEqual(0, w.ClearZoneRect(x0, y0, x1, y1), "unzoning it again changes nothing");
            Assert.AreEqual(0, w.ClearZoneRect(-5, -5, -1, -1), "off the map is ignored");
        }
    }
}
