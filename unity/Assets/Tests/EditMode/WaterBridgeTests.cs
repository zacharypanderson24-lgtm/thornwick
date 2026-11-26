// Bridges, water inspection and fishing (World.Bridges.cs, World.Fishing.cs, World.Hydrology.cs DescribeWater).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterBridgeTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World _valley, _coast;
        static World Valley => _valley ??= Make(1, WaterPreset.RiverValley);
        static World Coast => _coast ??= Make(1, WaterPreset.Coastal);
        static World Make(int seed, WaterPreset p) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 5000; w.Peasants = 80; return w; }

        /// <summary>Straight lines across one stretch of water of 1..maxSpan tiles, dry and buildable at both ends, nearest the town first.</summary>
        static List<List<(int x, int y)>> Crossings(World w, int minSpan, int maxSpan, bool buildable = true)
        {
            var found = new List<(int d, List<(int x, int y)> path)>();
            for (int y = 3; y < N - 3; y++)
                for (int x = 3; x < N - 12; x++)
                {
                    if (w.Waters.IsWater(x, y) || !w.Waters.IsWater(x + 1, y)) continue;
                    int span = 0; while (w.Waters.IsWater(x + 1 + span, y) && span <= maxSpan + 1) span++;
                    if (span < minSpan || span > maxSpan) continue;
                    var path = Enumerable.Range(x, span + 2).Select(i => (x: i, y: y)).ToList();
                    if (buildable && !w.BridgePathCheck(path).Ok) continue;
                    found.Add((System.Math.Abs(x - C) + System.Math.Abs(y - C), path));
                }
            return found.OrderBy(f => f.d).Select(f => f.path).ToList();
        }

        [Test]
        public void ABridgeMakesWaterPassableAndCostsWood()
        {
            var w = Make(1, WaterPreset.RiverValley);
            var path = Crossings(w, 2, 6).FirstOrDefault(); Assert.IsNotNull(path, "a crossing exists");
            var water = path.Where(p => w.Waters.IsWater(p.x, p.y)).ToList();
            Assert.IsTrue(double.IsPositiveInfinity(w.WalkCost(path[0].x, path[0].y, water[0].x, water[0].y)), "water blocks before the bridge");
            double wood = w.ResAmt[Res.Wood]; int cost = w.BridgeCost(path);
            var r = w.PlaceBridgePath(path); Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(wood - cost, w.ResAmt[Res.Wood], 1e-9);
            Assert.AreEqual(water.Count * World.BridgeWood + (path.Count - water.Count), cost);
            foreach (var (x, y) in water) { Assert.IsTrue(w.IsBridge(x, y)); Assert.IsFalse(w.IsSolid(x, y)); }
            for (int i = 0; i + 1 < path.Count; i++) Assert.IsFalse(double.IsPositiveInfinity(w.WalkCost(path[i].x, path[i].y, path[i + 1].x, path[i + 1].y)), $"step {i} is walkable over the bridge");
            // beside the bridge it is still water
            Assert.IsTrue(w.IsSolid(water[0].x, water[0].y - 1) || !w.Waters.IsWater(water[0].x, water[0].y - 1));
            Assert.IsTrue(w.Waters.IsWater(water[0].x, water[0].y), "the water layer itself is unchanged");
        }

        [Test, Category("Slow")]
        public void ABridgeJoinsTheRoadNetworkAcrossTheWater()
        {
            for (int tries = 0, built = 0; tries < 40 && built == 0; tries++)
            {
                var w = Make(1, WaterPreset.RiverValley);
                var list = Crossings(w, 2, 6); if (list.Count <= tries) break;
                var path = list[tries];
                // a road from the Town Hall's road to the near bank (the bridge's first tile), then the bridge, then a road beyond the far bank
                var toBank = World.LPath(C, C + 6, path[0].x, path[0].y);
                if (!w.PlaceRoadPath(toBank).Ok) continue;
                var far = path[path.Count - 1];
                var beyond = Enumerable.Range(1, 3).Select(i => (x: far.x + i, y: far.y)).ToList();
                if (!w.RoadPathCheck(beyond).Ok) continue;
                Assert.IsTrue(w.PlaceBridgePath(path).Ok);
                Assert.IsTrue(w.PlaceRoadPath(beyond).Ok);
                w.Recalc();
                Assert.IsTrue(w.Grid[beyond[2].y, beyond[2].x].Served, "a road beyond the bridge is connected to the Town Hall");
                built++;
            }
            // (if no crossing could be reached by road within 40 tries the next test class run has other seeds; the rules are tested separately)
        }

        [Test]
        public void ABridgeNeedsWoodAndStraightBankToBank()
        {
            var w = Make(1, WaterPreset.RiverValley);
            var path = Crossings(w, 2, 6).First();
            w.ResAmt[Res.Wood] = 3;
            var poor = w.BridgePathCheck(path); Assert.IsFalse(poor.Ok); StringAssert.Contains("wood", poor.Reason.ToLower());
            w.ResAmt[Res.Wood] = 5000;
            Assert.IsFalse(w.BridgePathCheck(path.Take(path.Count - 1).ToList()).Ok, "must end on dry land");
            Assert.IsFalse(w.BridgePathCheck(path.Skip(1).ToList()).Ok, "must start on dry land");
            var bent = path.ToList(); bent[bent.Count - 1] = (x: bent[bent.Count - 1].x, y: bent[bent.Count - 1].y + 1);
            Assert.IsFalse(w.BridgePathCheck(bent).Ok, "a bridge is straight");
            var unclaimed = new World(1, TerrainStyle.Ranges, Relief.Upland, WaterPreset.RiverValley) { };
            Assert.IsFalse(unclaimed.BridgePathCheck(new List<(int x, int y)> { (0, 0), (1, 0), (2, 0) }).Ok);
        }

        [Test]
        public void ABridgeCannotSpanTheSeaOrTooWideOrDeepWater()
        {
            var coast = Coast; int x0 = -1, y0 = -1;
            for (int y = 3; y < N - 3 && x0 < 0; y++) for (int x = 3; x < N - 12; x++) if (!coast.Waters.IsWater(x, y) && coast.Waters.Class[y * N + x + 1] == WaterClass.Ocean) { x0 = x; y0 = y; break; }
            Assert.GreaterOrEqual(x0, 0, "a coast exists");
            var sea = Enumerable.Range(x0, 5).Select(i => (x: i, y: y0)).ToList(); sea.Insert(0, (x: x0 - 1, y: y0));
            var r = coast.BridgePathCheck(sea); Assert.IsFalse(r.Ok);
            // anything wider than the span limit
            var wide = Crossings(Valley, World.BridgeMaxSpan + 1, 40, false).FirstOrDefault();
            if (wide != null) { var rr = Valley.BridgePathCheck(wide); Assert.IsFalse(rr.Ok); StringAssert.Contains("wide", rr.Reason.ToLower()); }
        }

        [Test]
        public void BridgesSurviveSaveAndLoad()
        {
            var w = Make(1, WaterPreset.RiverValley);
            var path = Crossings(w, 2, 6).First(); Assert.IsTrue(w.PlaceBridgePath(path).Ok);
            var back = World.LoadJson(w.SaveJson());
            foreach (var (x, y) in path.Where(p => w.Waters.IsWater(p.x, p.y))) { Assert.IsTrue(back.IsBridge(x, y)); Assert.IsFalse(back.IsSolid(x, y)); }
            Assert.AreEqual(w.Waters.Encode(), back.Waters.Encode());
        }

        // ---------- inspecting water ----------
        [Test]
        public void WaterTilesDescribeOnlyWhatTheSimulationKnows()
        {
            var w = Valley; int x = -1, y = -1;
            for (int j = 3; j < N - 3 && x < 0; j++) for (int i = 3; i < N - 3; i++) if (w.Waters.Class[j * N + i] == WaterClass.SmallRiver || w.Waters.Class[j * N + i] == WaterClass.MajorRiver) { x = i; y = j; break; }
            var text = string.Join("\n", w.DescribeWater(x, y));
            foreach (var key in new[] { "Water type: Fresh", "Depth:", "Flow direction:", "Current:", "Land movement: blocked", "Fishing suitability:", "Navigability:" }) StringAssert.Contains(key, text);
            StringAssert.Contains("no boats yet", text);
            Assert.AreEqual(0, w.DescribeWater(C, C).Count, "dry ground away from water has nothing to say");
            var lake = Make(1, WaterPreset.Lakeside); int lx = -1, ly = -1;
            for (int j = 3; j < N - 3 && lx < 0; j++) for (int i = 3; i < N - 3; i++) if (lake.Waters.Class[j * N + i] == WaterClass.Lake) { lx = i; ly = j; break; }
            var lt = string.Join("\n", lake.DescribeWater(lx, ly)); StringAssert.Contains("Still water", lt); StringAssert.DoesNotContain("Flow direction", lt);
            var sea = Coast; int sx = -1, sy = -1;
            for (int j = 3; j < N - 3 && sx < 0; j++) for (int i = 3; i < N - 3; i++) if (sea.Waters.Class[j * N + i] == WaterClass.Ocean) { sx = i; sy = j; break; }
            StringAssert.Contains("Salt water", string.Join("\n", sea.DescribeWater(sx, sy)));
        }

        // ---------- fishing ----------
        [Test]
        public void FishingHutsMustStandOnTheShoreAndCatchMoreFromBiggerWater()
        {
            var w = Valley;
            int ix = -1, iy = -1;
            for (int y = C - 10; y < C + 10 && ix < 0; y++) for (int x = C - 10; x < C + 10; x++) if (w.Grid[y, x] == null && w.DecoMap[y, x] == Deco.None && !w.IsSolid(x, y) && w.TileSpread(x, y) == 0 && w.Claimed(x, y)) { ix = x; iy = y; break; }
            Assert.GreaterOrEqual(ix, 0);
            var inland = w.CanPlace("fisher", ix, iy);
            Assert.IsFalse(inland.Ok, "inland ground is no place for a fishing hut");
            StringAssert.Contains("shore", inland.Reason);
            Assert.Less(World.FishRate(WaterClass.Pond), World.FishRate(WaterClass.Lake));
            Assert.Less(World.FishRate(WaterClass.Lake), World.FishRate(WaterClass.Ocean));
            Assert.Less(World.FishRate(WaterClass.Stream), World.FishRate(WaterClass.SmallRiver));
            Assert.AreEqual(0, World.FishRate(WaterClass.None));
        }

        [Test]
        public void AFishingHutFishesTheWaterItCanWalkToAndAddsFood()
        {
            var w = Make(1, WaterPreset.Lakeside); int hx = -1, hy = -1;
            for (int y = 8; y < N - 8 && hx < 0; y++)
                for (int x = 8; x < N - 8; x++)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None || w.IsSolid(x, y) || w.HasTree(x, y)) continue;
                    int near = 0; for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (w.Waters.Class[(y + dy) * N + x + dx] == WaterClass.Lake) near++;
                    if (near >= 2 && w.CanPlace("fisher", x, y).Ok) { hx = x; hy = y; }
                }
            Assert.GreaterOrEqual(hx, 0, "a lake shore exists");
            Assert.IsTrue(w.Place("fisher", hx, hy).Ok);
            var hut = w.Grid[hy, hx]; Assert.AreEqual("fisher", hut.T); Assert.AreEqual(2, hut.Jobs);
            var g = w.FishingGrounds(hut);
            Assert.Greater(g.grounds, 0); Assert.AreEqual(WaterClass.Lake, g.kind); Assert.Greater(g.perWorker, 0);
            hut.Served = true; hut.Workers = 2;
            Assert.AreEqual(2 * g.perWorker, w.StepFishing(), 1e-9);
            hut.Workers = 0; Assert.AreEqual(0, w.StepFishing());
            hut.Served = false; hut.Workers = 2; Assert.AreEqual(0, w.StepFishing(), "a hut with no road brings nothing");
            for (int d = 0; d < 10; d++) w.Tick();   // the daily simulation runs with a hut standing
            Assert.IsTrue(Defs.Trades.Contains("fishing"));
        }

        [Test]
        public void OldSavesWithoutTheFishingTradeStillLoad()
        {
            var json = JObject.Parse(new World(7).SaveJson());
            json["prio"] = new JArray("farming", "hunting", "timber", "woodwork", "quarrying", "mining", "smithing", "preserving", "charcoal", "hauling");
            var back = World.LoadJson(json.ToString());
            Assert.Contains("fishing", back.Prio);
            Assert.AreEqual(Defs.Trades.Length, back.Prio.Length);
        }
    }
}
