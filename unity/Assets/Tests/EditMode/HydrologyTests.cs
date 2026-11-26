// Surface water generation (Hydrology.cs, SurfaceWater.cs, World.Hydrology.cs). The underground drinking-water system has its own tests (WaterTests).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class HydrologyTests
    {
        const int N = Defs.N, C = Defs.Start;
        static readonly WaterPreset[] Presets = Enum.GetValues(typeof(WaterPreset)).Cast<WaterPreset>().Where(p => p != WaterPreset.None).ToArray();
        static readonly int[] Seeds = { 1, 2, 3 };
        static readonly Dictionary<(WaterPreset, int), HydroResult> Cache = new Dictionary<(WaterPreset, int), HydroResult>();

        static HydroResult Get(WaterPreset p, int seed)
        {
            if (!Cache.TryGetValue((p, seed), out var r))
            {
                var relief = HydroParams.For(p).SuggestedRelief;
                Cache[(p, seed)] = r = Hydrology.Generate(seed, p, World.GenRangeHeights(seed, relief), N, C);
            }
            return r;
        }

        static IEnumerable<(WaterPreset p, int seed)> All() { foreach (var p in Presets) foreach (var s in Seeds) yield return (p, s); }

        static bool Land(SurfaceWater w, int x, int y) => w.Inb(x, y) && !w.IsWater(x, y);
        static double Level(int[] hv, int size, int x, int y) { int m = size + 1; return (hv[y * m + x] + hv[y * m + x + 1] + hv[(y + 1) * m + x] + hv[(y + 1) * m + x + 1]) / 4.0; }

        // ---------- existing worlds are untouched ----------
        [Test]
        public void ADefaultWorldHasNoSurfaceWater()
        {
            var w = new World(7);
            Assert.IsNull(w.Waters);
            Assert.AreEqual(WaterPreset.None, w.WaterKind);
            Assert.IsFalse(w.IsWaterTile(C, C));
            Assert.IsTrue(JToken.Parse(w.SaveJson())["water"].Type == JTokenType.Null);
        }

        [Test]
        public void ADryWorldKeepsExactlyTheTerrainGeneratorsHeights()
        {
            var w = new World(5);
            Assert.AreEqual(World.GenRangeHeights(5, Relief.Upland), w.Hv);
        }

        [Test]
        public void OlderSavesWithoutAWaterEntryLoadDry()
        {
            var json = JObject.Parse(new World(7).SaveJson()); json.Remove("water");
            var loaded = World.LoadJson(json.ToString());
            Assert.IsNull(loaded.Waters);
        }

        // ---------- determinism and variety ----------
        [Test]
        public void TheSameSeedAndPresetGiveTheSameWater()
        {
            foreach (var p in new[] { WaterPreset.RiverValley, WaterPreset.Coastal, WaterPreset.Archipelago })
            {
                var a = Get(p, 1); var b = Hydrology.Generate(1, p, World.GenRangeHeights(1, HydroParams.For(p).SuggestedRelief), N, C);
                Assert.AreEqual(a.Water.Encode(), b.Water.Encode(), p.ToString());
                Assert.AreEqual(a.Hv, b.Hv, p.ToString());
            }
        }

        [Test, Category("Slow")]
        public void DifferentSeedsGiveDifferentWater()
        {
            foreach (var p in Presets)
            {
                var layouts = Seeds.Select(s => Get(p, s).Water.Encode()).Distinct().Count();
                Assert.AreEqual(Seeds.Length, layouts, $"{p}: every seed has its own geography");
            }
        }

        // ---------- rivers ----------
        [Test]
        public void RiversFlowDownhill()
        {
            int pairs = 0, up = 0;
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                for (int y = 1; y < N - 1; y++)
                    for (int x = 1; x < N - 1; x++)
                    {
                        int i = y * N + x; if (!SurfaceWater.River(w.Class[i]) || w.Flow[i] == 0) continue;
                        int j = (y + SurfaceWater.Dy[w.Flow[i]]) * N + x + SurfaceWater.Dx[w.Flow[i]];
                        if (!SurfaceWater.River(w.Class[j])) continue;
                        pairs++; if (w.Surface[j] > w.Surface[i]) up++;
                    }
            }
            Assert.Greater(pairs, 5000, "there are rivers to check");
            Assert.Less(up / (double)pairs, 0.01, $"{up} of {pairs} river steps climb");
        }

        [Test]
        public void EveryRiverEndsInSeaLakeOrTheMapEdge()
        {
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                // a river system is one connected stretch of open water: it must reach standing water or leave the map
                var ok = new HashSet<int>();
                foreach (var b in w.Bodies) if (b.Standing || b.TouchesEdge) ok.Add(b.Id);
                var netOk = new HashSet<int>();
                for (int i = 0; i < N * N; i++) if (SurfaceWater.OpenWater(w.Class[i]) && w.Body[i] != 0 && ok.Contains(w.Body[i])) netOk.Add(w.Network[i]);
                int stranded = 0, total = 0;
                var seen = new HashSet<int>();
                for (int i = 0; i < N * N; i++)
                    if (SurfaceWater.River(w.Class[i]) && seen.Add(w.Network[i])) { total++; if (!netOk.Contains(w.Network[i])) stranded++; }
                Assert.AreEqual(0, stranded, $"{p} seed {s}: {stranded} of {total} river systems end in dry land");
            }
        }

        [Test]
        public void WidthsFollowTheClassesAndTheDrainage()
        {
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water; int rivers = 0, big = 0, maxW = 0;
                for (int i = 0; i < N * N; i++)
                {
                    var c = w.Class[i]; if (!SurfaceWater.River(c)) continue;
                    rivers++; maxW = Math.Max(maxW, w.Width[i]);
                    if (c == WaterClass.Stream) Assert.LessOrEqual(w.Width[i], 2);
                    if (c == WaterClass.MajorRiver) { Assert.GreaterOrEqual(w.Width[i], 9); big++; }
                }
                Assert.Greater(rivers, 0, $"{p} seed {s} has rivers");
                Assert.LessOrEqual(maxW, 40, $"{p} seed {s}: widest river");
                Assert.Less(big / (double)rivers, 0.5, $"{p} seed {s}: large rivers are uncommon");
            }
        }

        [Test]
        public void RiverFlowDirectionsAreSetOnRiversOnly()
        {
            var w = Get(WaterPreset.RiverValley, 1).Water; int withFlow = 0, rivers = 0;
            for (int i = 0; i < N * N; i++)
            {
                if (SurfaceWater.Standing(w.Class[i])) Assert.AreEqual(0, w.Flow[i], "still water has no current");
                if (w.Class[i] == WaterClass.None) Assert.AreEqual(0, w.Flow[i]);
                if (SurfaceWater.River(w.Class[i])) { rivers++; if (w.Flow[i] != 0) withFlow++; }
            }
            Assert.Greater(withFlow / (double)rivers, 0.95);
        }

        // ---------- lakes, ponds, seas ----------
        [Test]
        public void StandingWaterHasOneSurfaceLevelPerBody()
        {
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                var lo = new Dictionary<int, int>(); var hi = new Dictionary<int, int>();
                for (int i = 0; i < N * N; i++)
                {
                    if (!SurfaceWater.Standing(w.Class[i])) continue;
                    int b = w.Body[i], sf = w.Surface[i];
                    lo[b] = lo.TryGetValue(b, out var l) ? Math.Min(l, sf) : sf; hi[b] = hi.TryGetValue(b, out var h) ? Math.Max(h, sf) : sf;
                }
                foreach (var b in lo.Keys) Assert.AreEqual(lo[b], hi[b], $"{p} seed {s}: standing body {b} has surfaces {lo[b]}..{hi[b]}");
            }
        }

        [Test]
        public void LakesAreNotSingleTilesAndNeverStandAboveTheirBanks()
        {
            foreach (var (p, s) in All())
            {
                var r = Get(p, s); var w = r.Water;
                foreach (var b in w.Bodies.Where(b => b.Standing && (b.Kind == WaterClass.Lake || b.Kind == WaterClass.Pond)))
                    Assert.GreaterOrEqual(b.Tiles, 4, $"{p} seed {s}: no single-tile puddles");
                for (int i = 0; i < N * N; i++)
                {
                    if (w.Class[i] != WaterClass.Lake && w.Class[i] != WaterClass.Pond) continue;
                    int x = i % N, y = i / N;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2];
                        if (!Land(w, nx, ny)) continue;
                        Assert.LessOrEqual(w.Surface[i], Level(r.Hv, N, nx, ny) + 1.01, $"{p} seed {s}: lake surface {w.Surface[i]} above its bank at {nx},{ny}");
                    }
                }
            }
        }

        [Test]
        public void TheSeaIsConnectedSaltWaterThatReachesTheMapEdge()
        {
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                foreach (var b in w.Bodies.Where(b => b.Kind == WaterClass.Ocean || b.Kind == WaterClass.Sea))
                    Assert.IsTrue(b.TouchesEdge && b.Salt, $"{p} seed {s}: marine body {b.Id} must be salt water connected to the map edge");
            }
        }

        [Test]
        public void SaltWaterAppearsOnlyWherePresetsHaveASea()
        {
            foreach (var p in new[] { WaterPreset.BalancedInland, WaterPreset.RiverValley, WaterPreset.Lakeside, WaterPreset.Wetlands, WaterPreset.AridOasis, WaterPreset.MountainWatershed })
                foreach (var s in Seeds)
                {
                    var w = Get(p, s).Water;
                    Assert.IsFalse(w.Class.Any(c => c == WaterClass.Ocean || c == WaterClass.Sea || c == WaterClass.Estuary), $"{p} seed {s}: landlocked");
                }
        }

        [Test]
        public void PresetsProduceTheirGeography()
        {
            foreach (var s in Seeds)
            {
                Assert.That(Share(WaterPreset.Coastal, s, c => c == WaterClass.Ocean || c == WaterClass.Sea), Is.InRange(0.06, 0.6), $"coastal {s}: sea on one side");
                Assert.Greater(Share(WaterPreset.Island, s, c => c == WaterClass.Ocean || c == WaterClass.Sea), 0.25, $"island {s}: surrounded by sea");
                Assert.Greater(Share(WaterPreset.Archipelago, s, c => c == WaterClass.Ocean || c == WaterClass.Sea), 0.3, $"archipelago {s}: mostly sea");
                Assert.Greater(Share(WaterPreset.Lakeside, s, c => c == WaterClass.Lake), 0.012, $"lakeside {s}: a big lake");
                Assert.Greater(Share(WaterPreset.Wetlands, s, c => c == WaterClass.Wetland), 0.003, $"wetlands {s}: marsh");
                Assert.Less(Share(WaterPreset.AridOasis, s, c => SurfaceWater.OpenWater(c)), 0.08, $"arid {s}: little water");
                Assert.Greater(Share(WaterPreset.RiverValley, s, c => SurfaceWater.River(c)), 0.03, $"river valley {s}: rivers");
            }
            Assert.Greater(Seeds.Max(s => Get(WaterPreset.RiverValley, s).Water.Width.Max()), 6, "a river valley has a broad river");
        }

        static double Share(WaterPreset p, int s, Func<WaterClass, bool> pred) => Get(p, s).Water.Class.Count(pred) / (double)(N * N);

        [Test]
        public void ArchipelagosHaveSeveralIslandsAndIslandsAreIslands()
        {
            foreach (var s in Seeds)
            {
                Assert.GreaterOrEqual(Islands(Get(WaterPreset.Archipelago, s).Water, 60), 3, $"archipelago seed {s}");
                Assert.AreEqual(1, Islands(Get(WaterPreset.Island, s).Water, 400), $"island seed {s}: one big landmass");
            }
        }

        static int Islands(SurfaceWater w, int minTiles)
        {
            var seen = new bool[N * N]; int count = 0; var q = new Queue<int>();
            for (int s = 0; s < N * N; s++)
            {
                if (seen[s] || w.IsWaterAt(s)) continue;
                int size = 0; seen[s] = true; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); size++; int x = c % N, y = c / N;
                    for (int k = 0; k < 4; k++) { int nx = x + SurfaceWater.Dx[1 + k * 2], ny = y + SurfaceWater.Dy[1 + k * 2]; if (nx < 0 || ny < 0 || nx >= N || ny >= N || seen[ny * N + nx] || w.IsWater(nx, ny)) continue; seen[ny * N + nx] = true; q.Enqueue(ny * N + nx); }
                }
                if (size >= minTiles) count++;
            }
            return count;
        }

        // ---------- depth, shores, navigation ----------
        [Test, Category("Slow")]
        public void EveryOpenWaterTileHasADepthAndTheSeaShelvesFromTheShore()
        {
            var seen = new HashSet<WaterDepth>();
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                for (int i = 0; i < N * N; i++)
                {
                    if (!SurfaceWater.OpenWater(w.Class[i])) { Assert.AreEqual(w.Class[i] == WaterClass.Wetland ? WaterDepth.Shallow : WaterDepth.None, w.Depth[i]); continue; }
                    Assert.AreNotEqual(WaterDepth.None, w.Depth[i]);
                    seen.Add(w.Depth[i]);
                    if ((w.Flags[i] & WaterFlags.Shore) != 0 && SurfaceWater.Salty(w.Class[i])) Assert.AreEqual(WaterDepth.Shallow, w.Depth[i], "the sea shelves");
                }
            }
            CollectionAssert.AreEquivalent(new[] { WaterDepth.Shallow, WaterDepth.Medium, WaterDepth.Deep }, seen);
        }

        [Test]
        public void ANavigableRegionLiesInOneConnectedWater()
        {
            foreach (var p in new[] { WaterPreset.Lakeside, WaterPreset.Archipelago })
            {
                var w = Get(p, 1).Water;
                var perRegion = new Dictionary<int, HashSet<int>>();
                for (int i = 0; i < N * N; i++)
                    if (w.NavRegion[i] != 0) { if (!perRegion.TryGetValue(w.NavRegion[i], out var set)) perRegion[w.NavRegion[i]] = set = new HashSet<int>(); set.Add(w.Network[i]); }
                Assert.Greater(perRegion.Count, 0);
                foreach (var kv in perRegion) Assert.AreEqual(1, kv.Value.Count, "boats cannot cross land");
            }
        }

        [Test, Category("Slow")]
        public void OnlyWaterCarriesWaterData()
        {
            foreach (var (p, s) in All())
            {
                var w = Get(p, s).Water;
                for (int i = 0; i < N * N; i++)
                {
                    if (w.Class[i] == WaterClass.None || w.Class[i] == WaterClass.Wetland) { Assert.AreEqual(0, w.Width[i]); Assert.AreEqual(0, w.Strength[i]); }
                    if (SurfaceWater.Salty(w.Class[i])) Assert.AreEqual(255, w.Salinity[i]);
                    if (w.Class[i] == WaterClass.Pond || w.Class[i] == WaterClass.Lake) Assert.AreEqual(0, w.Salinity[i]);
                }
            }
        }

        // ---------- the terrain the water sits in ----------
        [Test]
        public void WaterTilesAreNearlyFlat()
        {
            foreach (var (p, s) in All())
            {
                var r = Get(p, s); var w = r.Water; int rough = 0, wet = 0;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        if (!w.IsWater(x, y)) continue; wet++;
                        int m = N + 1, a = r.Hv[y * m + x], b = r.Hv[y * m + x + 1], c = r.Hv[(y + 1) * m + x], d = r.Hv[(y + 1) * m + x + 1];
                        if (Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d)) > 1) rough++;
                    }
                Assert.Less(rough, wet * 0.02 + 3, $"{p} seed {s}: {rough} of {wet} water tiles are not nearly flat");
            }
        }

        // ---------- the start ----------
        [Test]
        public void EveryPresetAndSeedHasAValidStart()
        {
            foreach (var (p, s) in All())
            {
                var r = Get(p, s);
                Assert.IsTrue(r.Water.StartValid, $"{p} seed {s}: no valid start after {Hydrology.MaxAttempts} attempts: {r.Water.StartProblems}");
                for (int y = C - 12; y <= C + 14; y++) for (int x = C - 12; x <= C + 12; x++) Assert.IsFalse(r.Water.IsWater(x, y) || r.Water.IsWetland(x, y), $"{p} seed {s}: the Town Hall's ground is wet at {x},{y}");
            }
        }

        [Test]
        public void AWaterWorldCanBeCreatedAndTheTownStandsOnLand()
        {
            foreach (var p in new[] { WaterPreset.RiverValley, WaterPreset.Coastal, WaterPreset.Island })
            {
                var w = new World(1, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p);
                Assert.IsNotNull(w.Waters, p.ToString());
                Assert.IsNotNull(w.Hall);
                foreach (var (x, y) in w.Hall.Tiles()) Assert.IsFalse(w.IsWaterTile(x, y));
                Assert.Greater(w.Blds.Count, 3, "the starting village is built");
                Assert.AreEqual(0, w.Blds.Count(b => b.Tiles().Any(t => w.IsWaterTile(t.Item1, t.Item2))), "no building starts in the water");
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (w.IsWaterTile(x, y)) { Assert.AreEqual(Deco.None, w.DecoMap[y, x], "nothing grows in water"); Assert.IsFalse(w.Fert[y, x]); }
                w.Recalc(); w.ComputeDemand();
                for (int d = 0; d < 20; d++) w.Tick();   // the daily simulation runs on a water map
            }
        }

        [Test]
        public void FreshWaterRaisesFertilityButTheSeaDoesNot()
        {
            var w = new World(2, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Coastal);
            int nearFresh = 0, nearFreshFert = 0, beach = 0, beachFert = 0;
            for (int y = 3; y < N - 3; y++)
                for (int x = 3; x < N - 3; x++)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None) continue;
                    bool fresh = false, salt = false;
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { var c = w.Waters.Class[(y + dy) * N + x + dx]; if (SurfaceWater.Salty(c)) salt = true; else if (SurfaceWater.OpenWater(c)) fresh = true; }
                    if (fresh && !salt && Math.Max(Math.Abs(x - C), Math.Abs(y - C)) > 8) { nearFresh++; if (w.Fert[y, x]) nearFreshFert++; }
                    if (salt && !fresh) { beach++; if (w.Fert[y, x]) beachFert++; }
                }
            Assert.Greater(nearFresh, 100);
            Assert.AreEqual(0, beachFert, "salt shores are never fertile from water");
            Assert.Greater(nearFreshFert / (double)nearFresh, 0.4, "river valleys are fertile");
            Assert.Greater(beach, 50);
        }

        // ---------- saving, expanding ----------
        [Test]
        public void SaveAndLoadKeepTheGeography()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Coastal);
            var json = w.SaveJson();
            var back = World.LoadJson(json);
            Assert.IsNotNull(back.Waters);
            Assert.AreEqual(w.Waters.Encode(), back.Waters.Encode());
            Assert.AreEqual(w.Hv, back.Hv);
            Assert.AreEqual(w.Waters.Bodies.Count, back.Waters.Bodies.Count);
            CollectionAssert.AreEqual(w.Waters.Network, back.Waters.Network);
            CollectionAssert.AreEqual(w.Waters.Body, back.Waters.Body);
            Assert.AreEqual(json, back.SaveJson(), "nothing drifts across a round trip");
        }

        [Test]
        public void ClaimingLandNeverMovesWater()
        {
            var w = new World(1, TerrainStyle.Ranges, Relief.Upland, WaterPreset.RiverValley);
            var before = w.Waters.Encode(); var hv = (int[])w.Hv.Clone();
            for (int px = 0; px < World.ParcelsPerSide; px++) for (int py = 0; py < World.ParcelsPerSide; py++) w.ClaimParcel(px, py);
            w.ClaimAll();
            Assert.AreEqual(before, w.Waters.Encode());
            Assert.AreEqual(hv, w.Hv);
        }

        [Test]
        public void TheMainRiverIsOneBodyThatRunsOffTheMap()
        {
            // the whole 256 x 256 map is generated at once, so a river crossing a claim boundary is one connected body
            int runOff = 0;
            foreach (var s in Seeds)
            {
                var w = Get(WaterPreset.RiverValley, s).Water;
                var big = w.Bodies.Where(b => !b.Standing).OrderByDescending(b => b.Tiles).First();
                if (big.Tiles > 300 && big.TouchesEdge) runOff++;
            }
            Assert.GreaterOrEqual(runOff, 2, "in a river valley the main river is one long body that runs off the map");
        }

        // ---------- sizes and speed ----------
        [Test]
        public void WorksAtEveryMapSize()
        {
            foreach (int size in new[] { 64, 128, 256, 512 })
                foreach (var p in new[] { WaterPreset.RiverValley, WaterPreset.Island, WaterPreset.Coastal })
                {
                    var hv = Hydrology.SyntheticHeights(4, size); int c = size / 2;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var r = Hydrology.Generate(4, p, hv, size, c); sw.Stop();
                    Debug.Log($"water {p} {size}x{size}: {sw.ElapsedMilliseconds} ms, {r.Water.WaterTiles} water tiles, {r.Water.Bodies.Count} bodies, start valid {r.Water.StartValid} (attempt {r.Water.StartAttempt})");
                    Assert.Greater(r.Water.WaterTiles, size * size / 60, $"{p} {size}");
                    Assert.IsFalse(r.Water.IsWater(c, c), $"{p} {size}: the start is dry");
                    foreach (var b in r.Water.Bodies.Where(b => b.Kind == WaterClass.Ocean)) Assert.IsTrue(b.TouchesEdge);
                    Assert.Less(sw.ElapsedMilliseconds, 30000, $"{p} {size}");
                }
        }

        [Test]
        public void WaterGenerationIsFastEnoughOnTheGameMap()
        {
            var hv = World.GenRangeHeights(1, Relief.Upland); var sw = System.Diagnostics.Stopwatch.StartNew();
            Hydrology.Generate(1, WaterPreset.RiverValley, hv, N, C); sw.Stop();
            Debug.Log($"water generation 256x256 RiverValley: {sw.ElapsedMilliseconds} ms");
            Assert.Less(sw.ElapsedMilliseconds, 8000);
        }

        // ---------- pictures of the generated maps, for looking at (unity/Logs/water-previews) ----------
        [Test]
        public void WritePreviewPictures()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "water-previews"); Directory.CreateDirectory(dir);
            foreach (var p in Presets) Preview(Path.Combine(dir, $"{p}_1.png"), Get(p, 1));
            Preview(Path.Combine(dir, "RiverValley_2.png"), Get(WaterPreset.RiverValley, 2));
            Preview(Path.Combine(dir, "Coastal_2.png"), Get(WaterPreset.Coastal, 2));
            Assert.IsTrue(File.Exists(Path.Combine(dir, "Island_1.png")));
        }

        static void Preview(string path, HydroResult r)
        {
            const int sc = 2; int W = N * sc; var w = r.Water; var tex = new Texture2D(W, W, TextureFormat.RGB24, false); var px = new Color32[W * W];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    double lv = Level(r.Hv, N, x, y); var cl = w.Class[y * N + x]; var d = (int)w.Depth[y * N + x];
                    double sh = Math.Max(0, Math.Min(1, (lv + 2) / 14.0)); byte R = (byte)(90 + sh * 120), G = (byte)(140 + sh * 60), B = (byte)(70 + sh * 130);
                    if (lv >= 4) R = G = B = (byte)Math.Min(255, 140 + (lv - 4) * 8);
                    if (cl == WaterClass.Wetland) { R = 70; G = 130; B = 110; }
                    else if (SurfaceWater.Salty(cl)) { R = (byte)(90 - d * 20); G = (byte)(150 - d * 30); B = (byte)(210 - d * 20); }
                    else if (cl == WaterClass.Lake || cl == WaterClass.Pond) { R = (byte)(60 - d * 10); G = (byte)(130 - d * 15); B = (byte)(215 - d * 15); }
                    else if (cl == WaterClass.Estuary) { R = 70; G = 160; B = 200; }
                    else if (cl != WaterClass.None) { R = 40; G = (byte)(110 + (cl == WaterClass.Stream ? 80 : 0) - d * 10); B = (byte)(230 - d * 20); if (cl == WaterClass.Stream) R = 110; }
                    if ((Math.Abs(x - C) <= 3 && y == C) || (Math.Abs(y - C) <= 3 && x == C)) { R = 255; G = B = 0; }
                    for (int yy = 0; yy < sc; yy++) for (int xx = 0; xx < sc; xx++) px[(W - 1 - (y * sc + yy)) * W + x * sc + xx] = new Color32(R, G, B, 255);
                }
            tex.SetPixels32(px); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
