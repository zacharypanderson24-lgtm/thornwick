// Mountain ranges (World.Ranges.cs, Unity-only): long, tall ranges that cross the city map, scaled by the region's relief,
// with passes so the map is never cut in two and a valley left open for the town. The browser's own terrain is kept as
// TerrainStyle.Classic and checked tile for tile by SimParityTests.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class MountainTests
    {
        const int N = Defs.N;
        static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };
        static readonly Dictionary<(int, Relief), World> Cache = new Dictionary<(int, Relief), World>();
        static World Get(int seed, Relief r) { if (!Cache.TryGetValue((seed, r), out var w)) Cache[(seed, r)] = w = new World(seed, TerrainStyle.Ranges, r); return w; }

        [Test]
        public void TheGameUsesRangesAndUplandReliefByDefault()
        {
            var w = new World(7);
            Assert.AreEqual(TerrainStyle.Ranges, w.Style);
            Assert.AreEqual(Relief.Upland, w.Relief);
        }

        [Test]
        public void MoreMountainousRegionsHaveMoreMountains()
        {
            var avg = Enum.GetValues(typeof(Relief)).Cast<Relief>().ToDictionary(r => r, r => Seeds.Average(s => Get(s, r).MountainShare()));
            Assert.Less(avg[Relief.Lowland], avg[Relief.Upland]);
            Assert.Less(avg[Relief.Upland], avg[Relief.Highland]);
            Assert.Less(avg[Relief.Highland], avg[Relief.Alpine]);
            Assert.That(avg[Relief.Lowland], Is.InRange(0.01, 0.07), "lowland: a single low ridge");
            Assert.That(avg[Relief.Upland], Is.InRange(0.05, 0.13), "upland: about a tenth of the map");
            Assert.That(avg[Relief.Alpine], Is.InRange(0.18, 0.35), "alpine: a quarter or more, the town in a valley");
            foreach (var s in Seeds) Assert.Greater(Get(s, Relief.Lowland).MountainShare(), 0, $"seed {s}: even a lowland map has its ridge");
        }

        [Test]
        public void MountainsAreFarTallerThanTheBrowsers()
        {
            foreach (var s in Seeds)
            {
                Assert.LessOrEqual(Get(s, Relief.Alpine).Hv.Max(), Defs.HMax);
                Assert.GreaterOrEqual(Get(s, Relief.Alpine).Hv.Max(), 15, $"seed {s}: alpine peaks");
                Assert.GreaterOrEqual(Get(s, Relief.Highland).Hv.Max(), 14, $"seed {s}: highland peaks");
                Assert.Greater(Get(s, Relief.Upland).Hv.Max(), Defs.ClassicHMax, $"seed {s}: upland peaks beat the browser's tallest");
                Assert.LessOrEqual(new World(s, TerrainStyle.Classic).Hv.Max(), Defs.ClassicHMax, $"seed {s}: classic terrain keeps the browser's ceiling");
            }
            Assert.IsTrue(Seeds.Any(s => AllTiles().Any(t => Get(s, Relief.Highland).IsMountain(t.x, t.y) && Get(s, Relief.Highland).TileLevel(t.x, t.y) >= Defs.SnowLevel)),
                          "highland maps have snow-capped peaks");
        }

        [Test]
        public void RangesRunOffTheMapEdges()
        {
            foreach (Relief r in Enum.GetValues(typeof(Relief)))
                foreach (var s in Seeds)
                {
                    var w = Get(s, r);
                    bool edge = Enumerable.Range(0, N).Any(i => w.IsMountain(i, 0) || w.IsMountain(i, N - 1) || w.IsMountain(0, i) || w.IsMountain(N - 1, i));
                    Assert.IsTrue(edge, $"{r} seed {s}: no range reaches a map edge");
                }
        }

        [Test]
        public void TheTownSitsInAnOpenValley()
        {
            foreach (Relief r in Enum.GetValues(typeof(Relief)))
                foreach (var s in Seeds)
                {
                    var w = Get(s, r);
                    for (int y = Defs.Start - 15; y <= Defs.Start + 15; y++)
                        for (int x = Defs.Start - 15; x <= Defs.Start + 15; x++)
                            Assert.IsFalse(w.IsMountain(x, y), $"{r} seed {s}: mountain at {x},{y}, within 15 tiles of the Town Hall");
                    Assert.IsTrue(w.Hall.Served && w.Blds.Count(b => b.T == "house") == 2, $"{r} seed {s}: the starting settlement is intact");
                }
        }

        [Test]
        public void PassesKeepTheWholeMapWithinWalkingReach()
        {
            foreach (Relief r in Enum.GetValues(typeof(Relief)))
                foreach (var s in Seeds)
                {
                    var w = Get(s, r);
                    var (reach, walkable, seen) = w.WalkReach();
                    Assert.GreaterOrEqual(reach, walkable * 0.97, $"{r} seed {s}: only {reach} of {walkable} walkable tiles reachable from the Town Hall");
                    bool Edge(Func<int, int> k) => Enumerable.Range(0, N).Any(i => seen[k(i)]);
                    Assert.IsTrue(Edge(i => i) && Edge(i => (N - 1) * N + i) && Edge(i => i * N) && Edge(i => i * N + N - 1), $"{r} seed {s}: some map edge cannot be walked to");
                }
        }

        [Test]
        public void SameSeedAndReliefGiveTheSameMountains()
        {
            foreach (Relief r in Enum.GetValues(typeof(Relief)))
                CollectionAssert.AreEqual(new World(3, TerrainStyle.Ranges, r).Hv, new World(3, TerrainStyle.Ranges, r).Hv, r.ToString());
        }

        static IEnumerable<(int x, int y)> AllTiles() { for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) yield return (x, y); }
    }
}
