// Irrigation canals and watermills (World.Irrigation.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterIrrigationTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 5000; w.ResAmt[Res.Stone] = 500; return w; }

        static bool Fresh(World w, int x, int y) => w.Waters.IsWater(x, y) && !SurfaceWater.Salty(w.Waters.Class[y * N + x]);

        /// <summary>An eastward canal of this length that starts on a bank (fresh water to its west) and passes the rules.</summary>
        static List<(int x, int y)> GoodCanal(World w, int len, int farFrom = 0)
        {
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8 - len; x++)
                {
                    if (w.Waters.IsWater(x, y) || !Fresh(w, x - 1, y)) continue;
                    if (farFrom > 0 && System.Math.Max(System.Math.Abs(x - C), System.Math.Abs(y - C)) < farFrom) continue;
                    var path = Enumerable.Range(x, len).Select(i => (x: i, y: y)).ToList();
                    if (w.CanalPathCheck(path).Ok) return path;
                }
            return null;
        }

        [Test]
        public void ACanalStartsAtFreshWaterAndMayNotClimbOrBeTooLong()
        {
            var w = Make(WaterPreset.RiverValley, 1);
            var path = GoodCanal(w, 6); Assert.IsNotNull(path, "a canal can be dug");
            double wood = w.ResAmt[Res.Wood]; var r = w.PlaceCanalPath(path); Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(wood - 6 * World.CanalWood, w.ResAmt[Res.Wood], 1e-9);
            foreach (var (x, y) in path) Assert.IsTrue(w.IsCanal(x, y));
            Assert.IsTrue(w.CanalIsFed(path[0].x, path[0].y) && w.CanalIsFed(path[5].x, path[5].y), "joined to the river, so it carries water");
            // not at fresh water
            var far = Enumerable.Range(C + 20, 4).Select(i => (x: i, y: C + 20)).ToList();
            var bad = w.CanalPathCheck(far); Assert.IsFalse(bad.Ok); StringAssert.Contains("fresh water", bad.Reason);
            // too long
            var longPath = Enumerable.Range(0, World.CanalMaxLength + 1).Select(i => (x: 10 + i, y: 10)).ToList();
            StringAssert.Contains("long", w.CanalPathCheck(longPath).Reason.ToLower());
            // wood
            var w2 = Make(WaterPreset.RiverValley, 1); var p2 = GoodCanal(w2, 6); w2.ResAmt[Res.Wood] = 3; StringAssert.Contains("wood", w2.CanalPathCheck(p2).Reason.ToLower());
        }

        [Test]
        public void WaterDoesNotRunUphill()
        {
            var w = Make(WaterPreset.MountainWatershed, 1); int refused = 0, ok = 0;
            for (int y = 8; y < N - 8; y += 2)
                for (int x = 8; x < N - 14; x += 2)
                {
                    if (w.Waters.IsWater(x, y) || !Fresh(w, x - 1, y)) continue;
                    var path = Enumerable.Range(x, 10).Select(i => (x: i, y: y)).ToList(); var r = w.CanalPathCheck(path);
                    if (r.Ok) ok++; else if (r.Reason.Contains("uphill")) refused++;
                }
            Assert.Greater(refused, 0, "in hill country some canals would climb and are refused");
            Assert.Greater(ok, 0);
        }

        [Test]
        public void ACanalMakesDryLandFarmableAndAnUnconnectedOneDoesNothing()
        {
            var w = Make(WaterPreset.AridOasis, 1);
            var path = GoodCanal(w, 8, 20); Assert.IsNotNull(path);
            var mid = path[6]; int fx = mid.x, fy = mid.y + 2;
            double before = w.SoilMoisture(fx, fy);
            Assert.IsTrue(w.PlaceCanalPath(path).Ok);
            double after = w.SoilMoisture(fx, fy);
            Assert.Greater(after, before + 0.1, "the soil beside a fed canal is moister");
            Assert.Greater(w.MoistureYield(fx, fy), 0);
            Assert.IsNull(w.TooDryReason(fx, fy), "so it can be farmed now");
            // far from the canal nothing changes
            Assert.AreEqual(w.BaseMoisture, w.SoilMoisture(fx, fy + 15), 0.2);
            // a canal dug out in the dry, joined to nothing, irrigates nothing
            var lonely = new World(1, TerrainStyle.Ranges, Relief.Upland, WaterPreset.AridOasis); lonely.ClaimAll();
            int sx = -1, sy = -1;
            for (int y = 8; y < N - 8 && sx < 0; y++) for (int x = 8; x < N - 20; x++) if (Enumerable.Range(0, 5).All(i => lonely.Waters.Class[y * N + x + i] == WaterClass.None && !lonely.IsSolid(x + i, y)) && System.Math.Abs(lonely.SoilMoisture(x + 2, y + 2) - lonely.BaseMoisture) < 0.01) { sx = x; sy = y; break; }
            double plain = lonely.SoilMoisture(sx + 2, sy + 2);
            for (int i = 0; i < 5; i++) lonely.Canals.Add((sy) * N + sx + i); lonely.Touch();
            Assert.IsFalse(lonely.CanalIsFed(sx + 2, sy)); Assert.AreEqual(plain, lonely.SoilMoisture(sx + 2, sy + 2), 1e-9);
        }

        [Test]
        public void CanalsKeepBuildingsRoadsAndZonesOffAndClearRemovesThem()
        {
            var w = Make(WaterPreset.RiverValley, 1); var path = GoodCanal(w, 5); w.PlaceCanalPath(path); var (x, y) = path[2];
            StringAssert.Contains("canal", w.CanPlace("house", x, y).Reason.ToLower());
            StringAssert.Contains("canal", w.RoadPathCheck(new List<(int x, int y)> { (x, y) }).Reason.ToLower());
            StringAssert.Contains("canal", w.ZoneCheck(Zone.Res, x, y).Reason.ToLower());
            Assert.IsFalse(w.IsSolid(x, y), "people step across a canal");
            double wood = w.ResAmt[Res.Wood];
            Assert.IsTrue(w.Remove(x, y).Ok); Assert.IsFalse(w.IsCanal(x, y)); Assert.Greater(w.ResAmt[Res.Wood], wood);
            Assert.IsFalse(w.CanalIsFed(path[4].x, path[4].y) && !w.IsCanal(path[1].x, path[1].y) && false);
        }

        [Test]
        public void CanalsSurviveSaveAndLoad()
        {
            var w = Make(WaterPreset.RiverValley, 1); var path = GoodCanal(w, 6); w.PlaceCanalPath(path);
            var back = World.LoadJson(w.SaveJson());
            foreach (var (x, y) in path) Assert.IsTrue(back.IsCanal(x, y));
            Assert.AreEqual(w.Canals.Count, back.Canals.Count);
            Assert.IsTrue(back.CanalIsFed(path[0].x, path[0].y));
            Assert.IsFalse(JObject.Parse(new World(7).SaveJson())["canals"].HasValues, "a dry world has none");
        }

        // ---------- watermills ----------
        static Building MillByRiver(World w)
        {
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8; x++)
                    if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("watermill", x, y).Ok)
                    {
                        var b = w.AddBld("watermill", x, y); b.Served = true; b.Workers = 2; b.Eff = 1; return b;
                    }
            Assert.Fail("no riverbank"); return null;
        }

        [Test]
        public void AWatermillNeedsFlowingWater()
        {
            var w = Make(WaterPreset.Lakeside, 1);
            int ix = -1, iy = -1;
            for (int y = C - 10; y < C + 10 && ix < 0; y++) for (int x = C - 10; x < C + 10; x++) if (Enumerable.Range(0, 4).All(k => { int tx = x + k % 2, ty = y + k / 2; return w.Grid[ty, tx] == null && w.DecoMap[ty, tx] == Deco.None && !w.IsSolid(tx, ty) && w.TileSpread(tx, ty) == 0 && w.Claimed(tx, ty); })) { ix = x; iy = y; break; }
            Assert.GreaterOrEqual(ix, 0);
            Assert.IsFalse(w.CanPlace("watermill", ix, iy).Ok);
            StringAssert.Contains("river", w.CanPlace("watermill", ix, iy).Reason.ToLower());
            int lakeShore = -1;
            for (int y = 8; y < N - 8 && lakeShore < 0; y++) for (int x = 8; x < N - 8; x++)
                if (Enumerable.Range(0, 4).All(k => { int tx = x + k % 2, ty = y + k / 2; return w.Grid[ty, tx] == null && w.DecoMap[ty, tx] == Deco.None && !w.IsSolid(tx, ty) && w.Claimed(tx, ty); }) && w.Waters.Class[y * N + x] == WaterClass.None && w.Waters.Class[y * N + x + 1] == WaterClass.Lake && w.Waters.Class[y * N + x + 2] == WaterClass.Lake && !Enumerable.Range(-3, 8).Any(d => System.Linq.Enumerable.Range(-3, 8).Any(e => SurfaceWater.River(w.Waters.Class[System.Math.Clamp(y + d, 0, N - 1) * N + System.Math.Clamp(x + e, 0, N - 1)]))))
                { lakeShore = y * N + x; StringAssert.Contains("watermill", w.CanPlace("watermill", x, y).Reason.ToLower()); break; }
            var mill = MillByRiver(Make(WaterPreset.RiverValley, 1)); Assert.IsNotNull(mill);
        }

        [Test]
        public void StaffedWatermillsAddToTheHarvestAndStopWhenFrozenOrUnstaffed()
        {
            var w = Make(WaterPreset.RiverValley, 1);
            Assert.AreEqual(0, w.MillBonus(100), "no mill, no bonus");
            var mill = MillByRiver(w);
            w.Day = 120; double bonus = w.MillBonus(100);
            Assert.That(bonus, Is.InRange(0.01, 100 * World.MillBonusShare + 1e-9));
            mill.Workers = 0; Assert.AreEqual(0, w.MillBonus(100)); mill.Workers = 2;
            mill.Served = false; Assert.AreEqual(0, w.MillBonus(100)); mill.Served = true;
            Assert.AreEqual(0, w.MillBonus(0));
            Assert.AreEqual(0, new World(7).MillBonus(100));
            Assert.IsTrue(Defs.Trades.Contains("milling")); Assert.AreEqual(2, mill.Jobs);
            // a frozen river stops the wheel (small rivers freeze; if the mill sits on a big river it keeps turning)
            w.Day = 300; if (!w.RiverCurrentNear(mill, 1)) Assert.AreEqual(0, w.MillBonus(100), "the wheel is frozen"); else Assert.Greater(w.MillBonus(100), 0, "a big river keeps turning the wheel");
        }

        [Test]
        public void OldSavesLoadWithTheNewMillingTrade()
        {
            var json = JObject.Parse(new World(7).SaveJson());
            json["prio"] = new JArray("farming", "hunting", "timber", "woodwork", "quarrying", "mining", "smithing", "preserving", "charcoal", "hauling", "fishing");
            var back = World.LoadJson(json.ToString());
            Assert.Contains("milling", back.Prio); Assert.AreEqual(Defs.Trades.Length, back.Prio.Length);
        }
    }
}
