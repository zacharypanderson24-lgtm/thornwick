// Seasonal water: ice, spring floods, drought (World.WaterSeasons.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterSeasonTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); return w; }

        static (int x, int y) Find(World w, WaterClass c)
        {
            for (int y = 3; y < N - 3; y++) for (int x = 3; x < N - 3; x++) if (w.Waters.Class[y * N + x] == c) return (x, y);
            Assert.Fail("no " + c); return (0, 0);
        }

        static int FirstDay(World w, System.Func<bool> pred, int from = 1, int to = 4000) { for (int d = from; d < to; d++) { w.Day = d; if (pred()) return d; } return -1; }

        [Test]
        public void SmallFreshWatersFreezeInWinterAndBearWalkers()
        {
            var w = Make(WaterPreset.Lakeside, 1); var (x, y) = Find(w, WaterClass.Lake);
            w.Day = 100; Assert.IsFalse(w.IceOn); Assert.IsTrue(w.IsSolid(x, y), "summer water blocks");
            w.Day = 300; Assert.IsTrue(w.IceOn); Assert.IsTrue(w.IceBears(x, y)); Assert.IsFalse(w.IsSolid(x, y), "ice bears people");
            Assert.IsFalse(double.IsPositiveInfinity(w.WalkCost(x, y, x + 1, y)) && !w.IsSolid(x + 1, y));
            w.Day = 271; Assert.IsTrue(w.IceOn); w.Day = 270; Assert.IsFalse(w.IceOn); w.Day = 361; Assert.IsFalse(w.IceOn, "spring thaws it");
            // building on ice is still refused
            w.Day = 300; StringAssert.Contains("water", w.CanPlace("house", x, y).Reason.ToLower());
        }

        [Test]
        public void TheSeaAndBigRiversNeverFreeze()
        {
            var coast = Make(WaterPreset.Coastal, 1); coast.Day = 300; var (sx, sy) = Find(coast, WaterClass.Ocean);
            Assert.IsTrue(coast.IsSolid(sx, sy)); Assert.IsFalse(coast.Crossable(sx, sy));
            var valley = Make(WaterPreset.RiverValley, 1); valley.Day = 300;
            for (int y = 3; y < N - 3; y++) for (int x = 3; x < N - 3; x++) { var c = valley.Waters.Class[y * N + x]; if (c == WaterClass.MajorRiver || c == WaterClass.Estuary) Assert.IsFalse(valley.IceBears(x, y)); }
        }

        [Test]
        public void IceChangesTheMapSoPathsAndDrawingRefresh()
        {
            var w = Make(WaterPreset.Lakeside, 1); w.Day = 268; w.Recalc(); w.Events.Clear();
            int vBefore = w.Version, ticks = 0; while (!w.IceOn && ticks++ < 6) w.Tick();
            w.Tick();   // the first tick of winter
            Assert.Greater(w.Version, vBefore, "winter touches the world");
            Assert.IsTrue(w.Events.Any(e => e.Contains("freeze")), "the player is told");
            int vWinter = w.Version; w.Tick(); Assert.AreEqual(vWinter, w.Version, "nothing changes mid-winter");
        }

        [Test]
        public void BoatsAreHauledOutInWinter()
        {
            var w = Make(WaterPreset.Lakeside, 1); int dx = -1, dy = -1;
            for (int y = 8; y < N - 8 && dx < 0; y++) for (int x = 8; x < N - 8; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && w.CanPlace("dock", x, y).Ok) { dx = x; dy = y; break; }
            var d = w.AddBld("dock", dx, dy); d.Served = true; d.Workers = 3;
            w.Day = 100; w.StepBoats(); Assert.Greater(w.Boats.Count, 0);
            w.Day = 300; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count);
            w.Day = 360; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count);
            w.Day = 400; w.StepBoats(); Assert.Greater(w.Boats.Count, 0, "back on the water in spring");
        }

        [Test]
        public void DroughtsComeSomeSummersAndDryTheStreams()
        {
            var w = Make(WaterPreset.BalancedInland, 1);
            int droughts = Enumerable.Range(0, 60).Count(w.DroughtYear);
            Assert.That(droughts, Is.InRange(3, 25), "about one summer in six");
            int year = Enumerable.Range(0, 60).First(w.DroughtYear);
            var (x, y) = Find(w, WaterClass.Stream);
            w.Day = year * 360 + 150; Assert.IsTrue(w.DroughtOn); Assert.IsTrue(w.StreamDry(x, y)); Assert.IsFalse(w.IsSolid(x, y), "a dry bed can be walked");
            w.Day = year * 360 + 300; Assert.IsFalse(w.DroughtOn); Assert.IsFalse(w.StreamDry(x, y));
            w.Day = year * 360 + 60; Assert.IsFalse(w.StreamDry(x, y), "the streams run in spring");
            // bigger rivers and lakes keep their water
            var (rx, ry) = Find(w, WaterClass.SmallRiver); w.Day = year * 360 + 150; Assert.IsTrue(w.IsSolid(rx, ry));
        }

        [Test]
        public void ArideCountryHasMoreDroughtsThanWetlands()
        {
            var arid = Make(WaterPreset.AridOasis, 1); var wet = Make(WaterPreset.Wetlands, 1);
            Assert.Greater(Enumerable.Range(0, 200).Count(arid.DroughtYear), Enumerable.Range(0, 200).Count(wet.DroughtYear));
        }

        [Test]
        public void DroughtLowersHarvestsAndFloodYearsLeaveSilt()
        {
            var w = Make(WaterPreset.RiverValley, 1);
            int dYear = Enumerable.Range(0, 80).First(w.DroughtYear), fYear = Enumerable.Range(0, 80).First(y => w.FloodYear(y) && !w.DroughtYear(y));
            int px = -1, py = -1;
            for (int y = 3; y < N - 3 && px < 0; y++) for (int x = 3; x < N - 3; x++) if (w.FloodPlain(x, y)) { px = x; py = y; break; }
            Assert.GreaterOrEqual(px, 0);
            w.Day = fYear * 360 + 120; double silt = w.MoistureYield(px, py);
            w.Day = fYear * 360 + 20; double before = w.MoistureYield(px, py);
            Assert.AreEqual(before * World.SiltYield, silt, 1e-9, "after the flood the flood plain's fields give more");
            int dry = -1; for (int y = 3; y < N - 3 && dry < 0; y++) for (int x = 3; x < N - 3; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && !w.FloodPlain(x, y)) { dry = y * N + x; break; }
            w.Day = dYear * 360 + 20; double normal = w.MoistureYield(dry % N, dry / N);
            w.Day = dYear * 360 + 120; Assert.AreEqual(normal * World.DroughtYield, w.MoistureYield(dry % N, dry / N), 1e-9);
            Assert.Less(Enumerable.Range(0, 80).Count(w.FloodYear), 70);
            Assert.IsFalse(Make(WaterPreset.AridOasis, 1).FloodYear(3), "dry country never floods");
        }

        [Test]
        public void SeasonsDoNothingOnDryWorlds()
        {
            var w = new World(7);
            foreach (int day in new[] { 50, 150, 300 }) { w.Day = day; Assert.IsFalse(w.IceOn); Assert.IsFalse(w.DroughtOn); Assert.IsFalse(w.Crossable(C, C)); Assert.IsNull(w.SeasonNote(C, C)); }
            Assert.IsFalse(w.DroughtYear(5)); Assert.IsFalse(w.FloodYear(5));
            w.Day = 300; w.Tick();   // the daily tick still runs
        }

        [Test]
        public void TheSeasonShowsOnTheWaterAndIsNeverSaved()
        {
            var w = Make(WaterPreset.Lakeside, 1); var (x, y) = Find(w, WaterClass.Lake);
            w.Day = 300; var text = string.Join("\n", w.DescribeWater(x, y)); StringAssert.Contains("frozen", text.ToLower());
            w.Day = 100; text = string.Join("\n", w.DescribeWater(x, y)); StringAssert.DoesNotContain("frozen over", text);
            Assert.IsFalse(w.SaveJson().ToLower().Contains("drought"));
        }
    }
}
