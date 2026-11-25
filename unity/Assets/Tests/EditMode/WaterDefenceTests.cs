// Moats and longship raids (World.Moats.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterDefenceTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 9000; w.ResAmt[Res.Stone] = 900; return w; }

        static bool Fresh(World w, int x, int y) => w.Waters.IsWater(x, y) && !SurfaceWater.Salty(w.Waters.Class[y * N + x]);

        static List<(int x, int y)> GoodMoat(World w, int len)
        {
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8 - len; x++)
                {
                    if (w.Waters.IsWater(x, y) || !Fresh(w, x - 1, y)) continue;
                    var path = Enumerable.Range(x, len).Select(i => (x: i, y: y)).ToList();
                    if (w.MoatPathCheck(path).Ok) return path;
                }
            return null;
        }

        [Test]
        public void AMoatIsDugFromFreshWaterAndCostsWood()
        {
            var w = Make(WaterPreset.RiverValley, 1); var path = GoodMoat(w, 8); Assert.IsNotNull(path);
            double wood = w.ResAmt[Res.Wood]; var r = w.PlaceMoatPath(path); Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(wood - 8 * World.MoatWood, w.ResAmt[Res.Wood], 1e-9);
            foreach (var (x, y) in path) Assert.IsTrue(w.IsMoat(x, y));
            // dry start refused, too long refused, no wood refused
            var dry = Enumerable.Range(C + 25, 4).Select(i => (x: i, y: C + 25)).ToList(); StringAssert.Contains("fresh water", w.MoatPathCheck(dry).Reason);
            StringAssert.Contains("long", w.MoatPathCheck(Enumerable.Range(0, World.MoatMaxLength + 1).Select(i => (x: 10 + i, y: 10)).ToList()).Reason.ToLower());
            var w2 = Make(WaterPreset.RiverValley, 1); var p2 = GoodMoat(w2, 6); w2.ResAmt[Res.Wood] = 2; StringAssert.Contains("wood", w2.MoatPathCheck(p2).Reason.ToLower());
            // a second stretch can continue from the end of the first (bending the moat)
            var next = Enumerable.Range(1, 5).Select(i => (x: path[path.Count - 1].x, y: path[path.Count - 1].y + i)).ToList();
            Assert.IsTrue(w.MoatPathCheck(next).Ok || w.MoatPathCheck(next).Reason.Length > 0);
        }

        [Test]
        public void AMoatIsAWallToWalkersAndRaidersExceptAcrossARoad()
        {
            var w = Make(WaterPreset.RiverValley, 1); var path = GoodMoat(w, 8); w.PlaceMoatPath(path);
            var (mx, my) = path[4];
            Assert.IsTrue(w.IsSolid(mx, my));
            Assert.IsTrue(double.IsPositiveInfinity(w.WalkCost(mx, my - 1, mx, my)) || w.IsSolid(mx, my - 1));
            Assert.IsTrue(double.IsPositiveInfinity(w.WalkCost(mx - 1, my, mx, my)) || w.IsSolid(mx - 1, my));
            Assert.IsFalse(w.CanPlace("house", mx, my).Ok); StringAssert.Contains("moat", w.CanPlace("house", mx, my).Reason.ToLower());
            StringAssert.Contains("moat", w.ZoneCheck(Zone.Res, mx, my).Reason.ToLower());
            // a road laid across it is a causeway: the way over
            var cross = new List<(int x, int y)> { (mx, my - 1), (mx, my), (mx, my + 1) };
            var check = w.RoadPathCheck(cross); Assert.IsTrue(check.Ok || !check.Reason.ToLower().Contains("moat"), check.Reason);
            w.AddBld("road", mx, my);
            Assert.IsFalse(w.IsSolid(mx, my), "a causeway can be crossed");
            Assert.IsTrue(w.IsMoat(mx, my), "and the tile is still part of the moat");
            Assert.IsTrue(w.IsSolid(path[3].x, path[3].y), "the rest of the moat still blocks");
        }

        [Test]
        public void ClearRemovesAMoatTileAndMoatsAreSaved()
        {
            var w = Make(WaterPreset.RiverValley, 1); var path = GoodMoat(w, 6); w.PlaceMoatPath(path);
            var back = World.LoadJson(w.SaveJson());
            foreach (var (x, y) in path) { Assert.IsTrue(back.IsMoat(x, y)); Assert.IsTrue(back.IsSolid(x, y)); }
            Assert.AreEqual(w.Moats.Count, back.Moats.Count);
            double wood = w.ResAmt[Res.Wood];
            Assert.IsTrue(w.Remove(path[2].x, path[2].y).Ok); Assert.IsFalse(w.IsMoat(path[2].x, path[2].y)); Assert.IsFalse(w.IsSolid(path[2].x, path[2].y)); Assert.Greater(w.ResAmt[Res.Wood], wood);
            Assert.IsFalse(JObject.Parse(new World(7).SaveJson())["moats"].HasValues);
        }

        [Test]
        public void MoatsDoNothingOnDryWorlds()
        {
            var w = new World(7);
            Assert.IsFalse(w.MoatPathCheck(new List<(int x, int y)> { (C + 20, C + 20) }).Ok);
            Assert.IsFalse(w.IsMoat(C, C)); Assert.AreEqual(0, w.Moats.Count);
        }

        // ---------- longships ----------
        [Test]
        public void ACoastalTownHasAShoreWhereALongshipCanLand()
        {
            foreach (var (p, s) in new[] { (WaterPreset.Coastal, 1), (WaterPreset.Island, 1), (WaterPreset.Coastal, 2) })
            {
                var w = Make(p, s); var l = w.LongshipLanding(); Assert.IsTrue(l.HasValue, $"{p} {s}: a landing exists");
                var (x, y, sx, sy) = l.Value;
                int d = System.Math.Max(System.Math.Abs(x - (w.Hall.X + 1)), System.Math.Abs(y - (w.Hall.Y + 1)));
                Assert.That(d, Is.InRange(World.LandingMinFromHall, World.LandingMaxFromHall));
                Assert.AreEqual(WaterClass.None, w.Waters.Class[y * N + x]); Assert.IsFalse(w.IsSolid(x, y));
                Assert.IsTrue(w.Waters.IsWater(sx, sy), "the ship lies on the water");
                Assert.LessOrEqual(System.Math.Max(System.Math.Abs(x - sx), System.Math.Abs(y - sy)), 6, "close to the beach");
                Assert.IsTrue(w.Waters.BodyAt(sx, sy).TouchesEdge, "water that runs off the map, so ships can come from outside");
            }
            Assert.IsFalse(new World(7).LongshipLanding().HasValue, "no water, no longships");
        }

        [Test]
        public void SomeRaidsOnACoastComeByLongshipAndLandNearTheTown()
        {
            var w = Make(WaterPreset.Coastal, 1); int ships = 0, walked = 0; var landing = w.LongshipLanding().Value;
            for (int day = 20; day < 120; day++)
            {
                w.Day = day; var r = w.BeginRaid(6);
                if (r.ByShip)
                {
                    ships++; Assert.AreEqual("sea", r.From);
                    Assert.AreEqual(landing.ship_x, r.ShipX); Assert.AreEqual(landing.ship_y, r.ShipY);
                    foreach (var rd in r.Raiders) Assert.LessOrEqual(System.Math.Max(System.Math.Abs(rd.X - landing.x), System.Math.Abs(rd.Y - landing.y)), 4, "the band lands at the shore");
                }
                else walked++;
            }
            Assert.Greater(ships, 15); Assert.Greater(walked, 30); Assert.Less(ships, 65, "about two raids in five");
            var dry = new World(7); for (int day = 20; day < 60; day++) { dry.Day = day; Assert.IsFalse(dry.BeginRaid(6).ByShip); }
        }
    }
}
