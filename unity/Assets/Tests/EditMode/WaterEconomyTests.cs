// Soil moisture, farm yield, fishing seasons and stock, fishing docks (World.Moisture.cs, World.Fishing.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterEconomyTests
    {
        const int N = Defs.N, C = Defs.Start;
        static readonly Dictionary<(WaterPreset, int), World> Cache = new Dictionary<(WaterPreset, int), World>();
        static World Get(WaterPreset p, int seed = 1)
        {
            if (!Cache.TryGetValue((p, seed), out var w)) { w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); Cache[(p, seed)] = w; }
            return w;
        }

        /// <summary>A dry, walkable land tile with no building whose 4-neighbour is water of this class (and at least this depth).</summary>
        static (int x, int y)? Shore(World w, System.Func<WaterClass, bool> kind, WaterDepth minDepth = WaterDepth.Shallow, bool noFreshNear = false)
        {
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8; x++)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None || w.IsSolid(x, y) || w.Grid[y, x] != null || w.HasTree(x, y)) continue;
                    if (noFreshNear) { bool fresh = false; for (int dy = -9; dy <= 9 && !fresh; dy++) for (int dx = -9; dx <= 9; dx++) { int j = (y + dy) * N + x + dx; if (j >= 0 && j < N * N && w.Waters.Class[j] != WaterClass.None && !SurfaceWater.Salty(w.Waters.Class[j])) { fresh = true; break; } } if (fresh) continue; }
                    for (int k = 0; k < 4; k++)
                    {
                        int i = (y + Defs.Nb[k, 1]) * N + x + Defs.Nb[k, 0];
                        if (kind(w.Waters.Class[i]) && w.Waters.Depth[i] >= minDepth) return (x, y);
                    }
                }
            return null;
        }

        [Test]
        public void DryWorldsHaveNoMoistureLayerAndFarmsYieldWhatTheyAlwaysDid()
        {
            var w = new World(7);
            Assert.AreEqual(1.0, w.MoistureYield(C, C + 5));
            Assert.AreEqual(0.4, w.SoilMoisture(C + 20, C + 20));
            Assert.IsFalse(w.FloodPlain(C + 20, C + 20));
            Assert.IsNull(w.TooDryReason(C + 40, C + 40));
        }

        [Test]
        public void FreshWaterRaisesMoistureSaltWaterLowersItAndMarshIsWet()
        {
            var w = Get(WaterPreset.RiverValley);
            double bank = -1, far = 2;
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8; x++)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None) continue;
                    if (w.Waters.Class[y * N + x + 1] == WaterClass.SmallRiver && bank < 0) bank = w.SoilMoisture(x, y);
                }
            for (int y = C - 30; y < C + 30; y++) for (int x = C - 30; x < C + 30; x++) if (w.Waters.Class[y * N + x] == WaterClass.None) far = System.Math.Min(far, w.SoilMoisture(x, y));
            Assert.Greater(bank, w.BaseMoisture + 0.2, "beside a river the soil is wetter");
            Assert.AreEqual(w.BaseMoisture, far, 0.01, "away from water it is the region's own moisture");

            var coast = Get(WaterPreset.Coastal);
            var beach = Shore(coast, c => c == WaterClass.Ocean, WaterDepth.Shallow, true); Assert.IsNotNull(beach);
            Assert.Less(coast.SoilMoisture(beach.Value.x, beach.Value.y), coast.BaseMoisture, "salt shore soil is poorer than the region's average");

            var wet = Get(WaterPreset.Wetlands);
            int marsh = 0, wetCount = 0;
            for (int i = 0; i < N * N; i++) if (wet.Waters.Class[i] == WaterClass.Wetland) { marsh++; if (wet.SoilMoisture(i % N, i / N) > 0.99) wetCount++; }
            Assert.Greater(marsh, 0); Assert.AreEqual(marsh, wetCount);
        }

        [Test]
        public void RainfallSetsTheBaseAndYieldFollowsMoisture()
        {
            Assert.Less(Get(WaterPreset.AridOasis).BaseMoisture, Get(WaterPreset.BalancedInland).BaseMoisture);
            Assert.Less(Get(WaterPreset.BalancedInland).BaseMoisture, Get(WaterPreset.Wetlands).BaseMoisture);
            var w = Get(WaterPreset.RiverValley);
            double lo = 9, hi = 0;
            for (int y = 3; y < N - 3; y += 3) for (int x = 3; x < N - 3; x += 3) { double f = w.MoistureYield(x, y); lo = System.Math.Min(lo, f); hi = System.Math.Max(hi, f); }
            Assert.That(lo, Is.InRange(0.55, 1.1)); Assert.That(hi, Is.InRange(1.15, 1.3));
            var arid = Get(WaterPreset.AridOasis);
            double dryYield = 9; for (int y = 3; y < N - 3 && dryYield == 9; y += 3) for (int x = 3; x < N - 3; x += 3) if (arid.Waters.Class[y * N + x] == WaterClass.None && System.Math.Abs(arid.SoilMoisture(x, y) - arid.BaseMoisture) < 0.01) { dryYield = arid.MoistureYield(x, y); break; }
            Assert.Less(dryYield, 0.8, "dry country away from water yields poorly");
        }

        [Test]
        public void DryCountryCannotBeFarmedAwayFromWaterButCanBeBesideIt()
        {
            var w = Get(WaterPreset.AridOasis); int dryHere = 0, okHere = 0;
            for (int y = 3; y < N - 3; y += 2)
                for (int x = 3; x < N - 3; x += 2)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None) continue;
                    if (w.TooDryReason(x, y) != null) dryHere++; else okHere++;
                }
            Assert.Greater(dryHere, okHere, "most of an arid map is too dry to farm");
            Assert.Greater(okHere, 0, "land beside the water can be farmed");
            var (x0, y0) = Shore(w, c => SurfaceWater.OpenWater(c)).Value;
            Assert.IsNull(w.TooDryReason(x0, y0));
            var wet = Get(WaterPreset.BalancedInland);
            Assert.IsNull(wet.TooDryReason(C + 10, C + 10), "ordinary country is never too dry");
        }

        [Test]
        public void FloodPlainsAreLowLandBesideRealRivers()
        {
            var w = Get(WaterPreset.RiverValley); int plains = 0;
            for (int y = 3; y < N - 3; y++)
                for (int x = 3; x < N - 3; x++)
                    if (w.FloodPlain(x, y))
                    {
                        plains++;
                        bool river = false;
                        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { int i = (y + dy) * N + x + dx; if (SurfaceWater.River(w.Waters.Class[i]) && w.Waters.Width[i] >= 3) river = true; }
                        Assert.IsTrue(river);
                    }
            Assert.Greater(plains, 0, "a river valley has flood plains");
            Assert.IsFalse(Get(WaterPreset.AridOasis).FloodPlain(C, C));
        }

        // ---------- fishing ----------
        [Test]
        public void FishingFollowsTheSeasons()
        {
            var w = Get(WaterPreset.Lakeside);
            w.Day = 10; Assert.AreEqual("Spring", w.Season);
            double spring = w.FishSeason(WaterClass.Lake), seaSpring = w.FishSeason(WaterClass.Ocean);
            w.Day = 100; double summer = w.FishSeason(WaterClass.Lake);
            w.Day = 300; double winter = w.FishSeason(WaterClass.Lake), seaWinter = w.FishSeason(WaterClass.Ocean);
            w.Day = 1;
            Assert.Greater(summer, spring); Assert.Less(winter, spring);
            Assert.Less(seaWinter / seaSpring, 1.0); Assert.Greater(seaWinter, winter, "the sea fishes better than a frozen lake in winter");
        }

        static Building HutBesidePond(World w, string type = "fisher")
        {
            var s = Shore(w, c => c == WaterClass.Pond, type == "dock" ? WaterDepth.Medium : WaterDepth.Shallow); Assert.IsNotNull(s, "a pond exists");
            var b = w.AddBld(type, s.Value.x, s.Value.y); b.Served = true; b.Workers = 3; return b;
        }

        [Test]
        public void OverFishingThinsASmallWaterAndItRecoversWhenLeftAlone()
        {
            var w = new World(2, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Wetlands); w.ClaimAll();
            var hut = HutBesidePond(w); var g = w.FishingGrounds(hut); Assert.Greater(g.grounds, 0);
            double first = w.FishCatch(hut); Assert.Greater(first, 0);
            int body = g.body; var tiles = w.Waters.Bodies[body - 1].Tiles;
            for (int d = 0; d < 150; d++) { hut.Served = true; hut.Workers = 3; w.StepFishing(); }
            double low = w.StockOf(body);
            Assert.Less(low, 0.95, $"a hut on a {tiles}-tile water draws it down");
            Assert.Less(w.FishCatch(hut), first, "so the catch falls");
            hut.Workers = 0;
            for (int d = 0; d < 400; d++) w.StepFishing();
            Assert.Greater(w.StockOf(body), low, "left alone the fish breed back");
        }

        [Test]
        public void ABigLakeIsNotFishedOutByOneHut()
        {
            var w = Get(WaterPreset.Lakeside, 3);
            var s = Shore(w, c => c == WaterClass.Lake); Assert.IsNotNull(s);
            var hut = w.AddBld("fisher", s.Value.x, s.Value.y); hut.Served = true; hut.Workers = 2;
            var g = w.FishingGrounds(hut);
            for (int d = 0; d < 200; d++) { hut.Served = true; hut.Workers = 2; w.StepFishing(); }
            Assert.Greater(w.StockOf(g.body), 0.9);
            w.Blds.Remove(hut); w.Grid[s.Value.y, s.Value.x] = null;
        }

        [Test]
        public void FishingDocksNeedDeepWaterAndCatchMoreThanHuts()
        {
            var w = Get(WaterPreset.Lakeside);
            Assert.IsFalse(w.CanPlace("dock", C + 12, C + 12).Ok);
            (int x, int y)? deep = null;
            for (int y = 8; y < N - 8 && deep == null; y++) for (int x = 8; x < N - 8; x++) { var tile = new Building { T = "dock", X = x, Y = y, Sz = 1 }; if (w.Waters.Class[y * N + x] == WaterClass.None && w.CanPlace("dock", x, y).Ok) { deep = (x, y); break; } }
            var shallowOnly = Shore(w, c => c == WaterClass.Stream, WaterDepth.Shallow);
            Assert.IsNotNull(deep);
            // a dock stands only where medium or deep water is right beside it
            Assert.IsTrue(w.CanPlace("dock", deep.Value.x, deep.Value.y).Ok);
            if (shallowOnly != null) { var r = w.CanPlace("dock", shallowOnly.Value.x, shallowOnly.Value.y); if (!r.Ok) StringAssert.Contains("dock", r.Reason.ToLower()); }
            var hut = w.AddBld("fisher", deep.Value.x, deep.Value.y); hut.Served = true; hut.Workers = 2;
            var gh = w.FishingGrounds(hut);
            var dock = w.AddBld("dock", deep.Value.x, deep.Value.y); dock.Served = true; dock.Workers = 2;
            var gd = w.FishingGrounds(dock);
            Assert.Greater(gd.grounds, 0, "a dock fishes the boat-deep water its pier reaches");
            Assert.AreEqual(FishRateOf(gd.kind) * System.Math.Min(1, gd.grounds / (double)World.FishGroundsFull) * World.DockBonus, gd.perWorker, 1e-9);
            Assert.AreEqual(3, dock.Jobs);
            w.Blds.Remove(hut); w.Blds.Remove(dock); w.Grid[deep.Value.y, deep.Value.x] = null;
        }

        static double FishRateOf(WaterClass c) => World.FishRate(c);

        [Test]
        public void FishStockSurvivesSaveAndLoad()
        {
            var w = new World(2, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Wetlands); w.ClaimAll();
            var hut = HutBesidePond(w); int body = w.FishingGrounds(hut).body;
            for (int d = 0; d < 100; d++) { hut.Served = true; hut.Workers = 3; w.StepFishing(); }
            double stock = w.StockOf(body); Assert.Less(stock, 1.0);
            w.Blds.Remove(hut); w.Grid[hut.Y, hut.X] = null;
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(stock, back.StockOf(body), 1e-3);
            Assert.AreEqual(1.0, new World(7).StockOf(1));
        }
    }
}
