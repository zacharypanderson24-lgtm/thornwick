// The conquest map (Sim/WorldMap.cs): deterministic from the seed, a heat map from cold (top) to hot (bottom), a plausible subset of the 16
// biomes, rivers that reach the sea or a lake, 576 regions each with a name and a city site, and a home region for the player's city.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WorldMapTests
    {
        const int S = WorldMap.Size;
        static readonly int[] Seeds = { 1, 7, 42, 99, 1234, 2024 };
        static readonly Dictionary<int, WorldMap> Cache = new Dictionary<int, WorldMap>();
        static WorldMap Map(int seed) { if (!Cache.TryGetValue(seed, out var m)) Cache[seed] = m = WorldMap.Generate(seed); return m; }

        static IEnumerable<int> Cells() => Enumerable.Range(0, S * S);

        [Test]
        public void TheMapIs600KmWith576RegionsAndTheCityScaleIsKnown()
        {
            Assert.AreEqual(600, S);
            Assert.AreEqual(576, Map(7).RegionList.Length);
            Assert.AreEqual(25, WorldMap.RegionCells);
            Assert.AreEqual(2.048, WorldMap.CityKm, 1e-9, "a 256 x 256 city of 8 m tiles is about 2 km");
            Assert.AreEqual(WorldMap.RegionCells * WorldMap.Regions, S);
        }

        [Test]
        public void SameSeedSameWorldDifferentSeedDifferentWorld()
        {
            var a = WorldMap.Generate(7); var b = WorldMap.Generate(7); var c = WorldMap.Generate(8);
            CollectionAssert.AreEqual(a.Elevation, b.Elevation);
            CollectionAssert.AreEqual(a.Rainfall, b.Rainfall);
            CollectionAssert.AreEqual(a.Biomes, b.Biomes);
            CollectionAssert.AreEqual(a.Features, b.Features);
            Assert.AreEqual(a.HomeRegion, b.HomeRegion);
            Assert.AreEqual(a.RegionList.Select(r => r.Name), b.RegionList.Select(r => r.Name));
            Assert.AreNotEqual(a.Elevation, c.Elevation, "a different seed is a different world");
            Assert.That(Enumerable.Range(0, S * S).Count(i => a.Elevation[i] > 0 != (c.Elevation[i] > 0)), Is.GreaterThan(S * S / 20));
        }

        [Test]
        public void GenerationIsFastEnoughToRunInTheBackground()
        {
            var sw = Stopwatch.StartNew(); WorldMap.Generate(31337);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(8000), "a world takes well under a few seconds");
        }

        [TestCaseSource(nameof(Seeds))]
        public void EveryWorldIsPartLandPartSea(int seed)
        {
            var m = Map(seed);
            int land = Cells().Count(i => m.Elevation[i] > 0);
            Assert.That(land / (double)(S * S), Is.InRange(0.30, 0.75), "land share");
            foreach (int i in Cells())
            {
                if (m.Elevation[i] > 0) Assert.That(m.Elevation[i], Is.GreaterThanOrEqualTo(2f)); else Assert.AreEqual(Biome.Ocean, m.Biomes[i]);
            }
            Assert.That(m.RegionList.Count(r => r.IsLand), Is.InRange(150, 450));
        }

        [TestCaseSource(nameof(Seeds))]
        public void TheWorldIsAHeatMapColdAtTheTopAndHotAtTheBottom(int seed)
        {
            var m = Map(seed);
            double Mean(int row0, int row1) { double s = 0; int n = 0; for (int y = row0; y < row1; y++) for (int x = 0; x < S; x++) { int i = y * S + x; if (m.Elevation[i] > 0) { s += m.Temperature[i]; n++; } } return n == 0 ? double.NaN : s / n; }
            double top = Mean(0, S / 3), mid = Mean(S / 3, 2 * S / 3), bottom = Mean(2 * S / 3, S);
            Assert.That(top, Is.LessThan(mid - 3), "the top third of the land is colder than the middle");
            Assert.That(mid, Is.LessThan(bottom - 3), "the middle is colder than the bottom third");
            // the sea follows the same gradient
            Assert.That(m.Temperature[5 * S + 300], Is.LessThan(m.Temperature[(S - 6) * S + 300] - 20));
            double MeanRow(params Biome[] bs) { double s = 0; int n = 0; for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) if (bs.Contains(m.Biomes[y * S + x])) { s += y; n++; } return n < 400 ? double.NaN : s / n; }
            double cold = MeanRow(Biome.Tundra, Biome.BorealForest), temperate = MeanRow(Biome.TemperateForest, Biome.TemperateGrassland);
            double hot = MeanRow(Biome.Desert, Biome.Savanna, Biome.TropicalRainforest, Biome.TropicalSeasonalForest);
            Assert.That(cold, Is.LessThan(temperate), "tundra and boreal forest lie north of temperate land");
            if (!double.IsNaN(hot)) Assert.That(hot, Is.GreaterThan(temperate), "desert, savanna and tropical forest lie south of it");
            Assert.That(MeanRow(Biome.Tundra), Is.LessThan(S * 0.4), "tundra is in the top bands");
        }

        [TestCaseSource(nameof(Seeds))]
        public void EachWorldHasAPlausibleSubsetOfTheSixteenBiomes(int seed)
        {
            var m = Map(seed);
            var present = Enumerable.Range(2, 16).Where(k => Cells().Count(i => (int)m.Biomes[i] == k) > 50).ToList();
            Assert.That(present.Count, Is.InRange(8, 16), "a varied world, not a single patch");
            Assert.That(present, Has.Member((int)Biome.TemperateForest).Or.Member((int)Biome.Steppe).Or.Member((int)Biome.SemiAridScrub));
            // altitude, not only latitude: the highest ground is alpine or tropical highland, never lowland biomes
            foreach (int i in Cells().Where(i => m.Elevation[i] > 2000))
                Assert.That(m.Biomes[i], Is.EqualTo(Biome.AlpineMountains).Or.EqualTo(Biome.TropicalHighlands).Or.EqualTo(Biome.Lake));
        }

        [Test]
        public void MountainsRobTheirLeeOfRain()
        {
            // a west wind (northern temperate): rain on the windward side, a shadow behind the range
            var m = WorldMap.Generate(7, Relief.Upland, MapSetting.NorthernTemperate);
            double wind = 0, lee = 0; int n = 0;
            for (int y = 10; y < S - 10; y += 2)
                for (int x = 24; x < S - 24; x += 2)
                {
                    int i = y * S + x;
                    if (m.Elevation[i] < 1500 || m.Biomes[i] == Biome.Lake) continue;
                    int w = y * S + x - 14, e = y * S + x + 14;
                    if (m.Elevation[w] <= 0 || m.Elevation[e] <= 0 || m.Elevation[w] > 1200 || m.Elevation[e] > 1200) continue;
                    wind += m.Rainfall[w]; lee += m.Rainfall[e]; n++;
                }
            Assume.That(n > 15, "enough ridge samples");
            Assert.That(wind / n, Is.GreaterThan(lee / n * 1.15), "windward slopes are wetter than the lee of the range");
        }

        [TestCaseSource(nameof(Seeds))]
        public void RiversFlowToTheSeaOrALake(int seed)
        {
            var m = Map(seed);
            var river = Cells().Where(i => (m.Features[i] & MapFeature.River) != 0).ToList();
            Assert.That(river.Count, Is.GreaterThan(500), "there are rivers");
            Assert.That(river.Count / (double)Cells().Count(i => m.Elevation[i] > 0), Is.InRange(0.005, 0.08));
            bool Mouth(int i)
            {
                int x = i % S, y = i / S;
                if (x == 0 || y == 0 || x == S - 1 || y == S - 1) return true;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { var b = m.Biomes[(y + dy) * S + x + dx]; if (b == Biome.Ocean || b == Biome.Lake) return true; }
                return false;
            }
            var seen = new HashSet<int>(); var stack = new Stack<int>();
            foreach (int i in river.Where(Mouth)) if (seen.Add(i)) stack.Push(i);
            while (stack.Count > 0)
            {
                int c = stack.Pop(), x = c % S, y = c / S;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                    int n = ny * S + nx; if ((m.Features[n] & MapFeature.River) != 0 && seen.Add(n)) stack.Push(n);
                }
            }
            Assert.AreEqual(river.Count, seen.Count, "every river cell is connected downstream to a mouth at the sea or a lake");
            // water gathers downstream: a major river is always a river
            foreach (int i in Cells().Where(i => (m.Features[i] & MapFeature.MajorRiver) != 0)) Assert.IsTrue((m.Features[i] & MapFeature.River) != 0);
        }

        [TestCaseSource(nameof(Seeds))]
        public void EveryLandRegionHasANameAndACitySiteOnLand(int seed)
        {
            var m = Map(seed);
            var names = m.RegionList.Select(r => r.Name).ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count(), "region names are unique");
            foreach (var r in m.RegionList)
            {
                Assert.IsNotEmpty(r.Name);
                if (!r.IsLand) { Assert.IsNull(r.Site); continue; }
                Assert.IsNotNull(r.Site, $"{r.Name} has a city site");
                Assert.AreEqual(r.Id, WorldMap.RegionIndex(r.Site.X, r.Site.Y), "the site lies inside its region");
                Assert.IsTrue(m.IsLand(r.Site.X, r.Site.Y), "on land, not in a lake");
                Assert.IsNotEmpty(r.Site.Name);
                int mx = r.Site.X - r.Col * WorldMap.RegionCells, my = r.Site.Y - r.Row * WorldMap.RegionCells;
                Assert.That(Math.Min(Math.Min(mx, my), Math.Min(WorldMap.RegionCells - 1 - mx, WorldMap.RegionCells - 1 - my)), Is.GreaterThanOrEqualTo(3), "not on the region's edge");
            }
            var sites = m.RegionList.Where(r => r.Site != null).Select(r => r.Site.Name).ToList();
            Assert.AreEqual(sites.Count, sites.Distinct().Count(), "city names are unique");
            // region summaries add up
            foreach (var r in m.RegionList.Take(40))
                Assert.AreEqual(WorldMap.RegionCells * WorldMap.RegionCells, r.BiomeCells.Sum());
        }

        [TestCaseSource(nameof(Seeds))]
        public void ThePlayersSettlementHasAHomeRegionNearTheMiddleOfTheWorld(int seed)
        {
            var m = Map(seed);
            Assert.GreaterOrEqual(m.HomeRegion, 0);
            var home = m.Home;
            Assert.That(home.Land, Is.GreaterThanOrEqualTo(0.7), "the home region is mostly land");
            Assert.IsNotNull(m.HomeSite);
            Assert.AreEqual(Relief.Upland, home.Relief, "the new game's city is Upland, so its home region is too");
            double dx = home.Col - (WorldMap.Regions - 1) / 2.0, dy = home.Row - (WorldMap.Regions - 1) / 2.0;
            Assert.That(Math.Sqrt(dx * dx + dy * dy), Is.LessThanOrEqualTo(6), "close to the centre of the map");
            Assert.That(home.Fertile, Is.GreaterThanOrEqualTo(0.25), "a place a settlement can feed itself");
            Assert.That(home.Temperature, Is.InRange(2.0, 20.0), "a temperate climate, not tundra or desert");
        }

        [Test]
        public void TheHomeRegionFollowsTheCitysRelief()
        {
            foreach (Relief relief in Enum.GetValues(typeof(Relief)))
            {
                var m = WorldMap.Generate(7, relief);
                Assert.GreaterOrEqual(m.HomeRegion, 0);
                bool exists = m.RegionList.Any(r => r.Site != null && r.Land >= 0.7 && r.Relief == relief);
                if (exists) Assert.AreEqual(relief, m.Home.Relief, $"a {relief} city has a {relief} home region when the world has one");
                Assert.That(m.Home.Land, Is.GreaterThanOrEqualTo(0.7));
            }
            // all four reliefs occur across the map
            var all = Enum.GetValues(typeof(Relief)).Cast<Relief>().ToList();
            var seen = Map(7).RegionList.Where(r => r.IsLand).Select(r => r.Relief).Distinct().ToList();
            CollectionAssert.IsSubsetOf(seen, all);
            Assert.GreaterOrEqual(seen.Count, 3);
        }

        [Test]
        public void TheCampaignSettingComesFromTheSeedAndCanBeChosen()
        {
            var settings = Enumerable.Range(1, 40).Select(s => WorldMap.Generate(s).Setting).Distinct().ToList();
            Assert.GreaterOrEqual(settings.Count, 3, "the seed picks among the campaign settings");
            Assert.AreEqual(MapSetting.Monsoon, WorldMap.Generate(7, Relief.Upland, MapSetting.Monsoon).Setting);
            var wet = WorldMap.Generate(7, Relief.Upland, MapSetting.Monsoon); var dry = WorldMap.Generate(7, Relief.Upland, MapSetting.CentralAsian);
            double Rain(WorldMap m) { double s = 0; int n = 0; foreach (int i in Cells()) if (m.Elevation[i] > 0) { s += m.Rainfall[i]; n++; } return s / n; }
            Assert.That(Rain(wet), Is.GreaterThan(Rain(dry) * 1.5), "a monsoon world is much wetter than a central Asian one");
        }
    }
}
