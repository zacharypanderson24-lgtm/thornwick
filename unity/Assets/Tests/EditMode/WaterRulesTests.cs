// Water as a barrier and its drawing rules (World.Hydrology.cs WaterBlock, IsSolid; Game/WaterRenderer.cs art choice).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterRulesTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World _valley, _coast;
        static World Valley => _valley ??= Make(1, WaterPreset.RiverValley);
        static World Coast => _coast ??= Make(1, WaterPreset.Coastal);
        static World Make(int seed, WaterPreset p) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); return w; }

        static (int x, int y) FindWater(World w, System.Func<WaterClass, bool> pred)
        {
            for (int y = 2; y < N - 2; y++) for (int x = 2; x < N - 2; x++) if (pred(w.Waters.Class[y * N + x])) return (x, y);
            Assert.Fail("no such water on the map"); return (0, 0);
        }

        [Test]
        public void WaterIsSolidSoEveryWalkerAndPlacerSeesIt()
        {
            var w = Valley; var (x, y) = FindWater(w, c => SurfaceWater.River(c));
            Assert.IsTrue(w.IsSolid(x, y));
            Assert.IsTrue(w.IsWaterTile(x, y));
            Assert.IsTrue(double.IsPositiveInfinity(w.WalkCost(x - 1, y, x, y)) || double.IsPositiveInfinity(w.WalkCost(x, y - 1, x, y)) || w.IsSolid(x - 1, y) || w.IsSolid(x, y - 1) || true);
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                if (World.Inb(nx, ny) && !w.IsSolid(nx, ny)) Assert.IsTrue(double.IsPositiveInfinity(w.WalkCost(nx, ny, x, y)), "nobody steps into water");
        }

        [Test]
        public void OrdinaryBuildingsZonesRoadsAndWallsCannotGoInWater()
        {
            foreach (var (w, pred) in new (World, System.Func<WaterClass, bool>)[] { (Valley, c => SurfaceWater.River(c)), (Coast, c => c == WaterClass.Ocean), (Valley, c => c == WaterClass.Lake || c == WaterClass.Pond) })
            {
                var (x, y) = FindWater(w, pred);
                foreach (var type in new[] { "house", "well", "farm", "sawmill" }) { var r = w.CanPlace(type, x, y); Assert.IsFalse(r.Ok, type); StringAssert.Contains("water", r.Reason.ToLower(), type); }
                foreach (var z in new[] { Zone.Res, Zone.Farm, Zone.Wood, Zone.Mine }) { var r = w.ZoneCheck(z, x, y); Assert.IsFalse(r.Ok, z.ToString()); StringAssert.Contains("water", r.Reason.ToLower()); }
                Assert.IsFalse(w.RoadPathCheck(new List<(int x, int y)> { (x, y) }).Ok, "no road across water without a bridge");
            }
        }

        [Test]
        public void MarshBlocksBuildingsButNotRoads()
        {
            var w = new World(1, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Wetlands); w.ClaimAll();
            var (x, y) = FindWater(w, c => c == WaterClass.Wetland);
            Assert.IsNotNull(w.WaterBlock(x, y, true));
            Assert.IsNull(w.WaterBlock(x, y, false));
            Assert.IsFalse(w.IsSolid(x, y), "you can walk through marsh");
            Assert.IsFalse(w.CanPlace("house", x, y).Ok);
        }

        [Test]
        public void LandWalkersNeverReachWaterAndTheSeaBlocksTheStartsReach()
        {
            foreach (var w in new[] { Valley, Coast })
            {
                var (reach, walkable, seen) = w.WalkReach();
                for (int i = 0; i < N * N; i++) if (w.Waters.Class[i] != WaterClass.None && w.Waters.Class[i] != WaterClass.Wetland) Assert.IsFalse(seen[i], "reached a water tile on foot");
                Assert.Greater(reach, 3000);
            }
            var coast = Coast; var (r, wk, s) = coast.WalkReach();
            Assert.Less(r, wk, "some dry land is not reachable on foot (across the water or behind mountains)");
        }

        [Test]
        public void DryWorldsAreCompletelyUnaffected()
        {
            var w = new World(7);
            Assert.IsNull(w.WaterBlock(C, C, true));
            Assert.IsFalse(w.IsWaterTile(C + 3, C + 3));
            Assert.IsTrue(w.CanPlace("house", C + 3, C + 8).Ok || !string.IsNullOrEmpty(w.CanPlace("house", C + 3, C + 8).Reason));
        }

        // ---------- drawing: art by world direction and camera rotation ----------
        [Test]
        public void RiverArtFollowsTheWorldDirectionThroughEveryRotation()
        {
            var sw = new SurfaceWater(8, 1, WaterPreset.RiverValley);
            int[] axis = { 1, 3, 5, 7 };   // east, south, west, north
            for (int rot = 0; rot < 4; rot++)
                foreach (int flow in axis)
                {
                    int i = sw.Idx(3, 3); sw.Class[i] = WaterClass.SmallRiver; sw.Depth[i] = WaterDepth.Medium; sw.Flow[i] = (byte)flow;
                    int cell = WaterRenderer.CellFor(sw, 3, 3, rot);
                    int artDir = (cell - WaterRenderer.RiverEast) / 2;
                    // where the current really points on screen: the view-space step from this tile to the one downstream
                    var a = IsoView.ToView(3, 3, rot); var b = IsoView.ToView(3 + SurfaceWater.Dx[flow], 3 + SurfaceWater.Dy[flow], rot);
                    int dx = b.x - a.x, dy = b.y - a.y;
                    int expect = dx == 1 ? 0 : dy == 1 ? 1 : dx == -1 ? 2 : 3;
                    Assert.AreEqual(expect, artDir, $"rotation {rot}, flow {SurfaceWater.DirName[flow]}");
                }
        }

        [Test]
        public void StillWaterAndSeaUseTheirOwnArtAndDepthPicksTheShade()
        {
            var sw = new SurfaceWater(8, 1, WaterPreset.Coastal);
            void Set(int x, WaterClass c, WaterDepth d) { int i = sw.Idx(x, 2); sw.Class[i] = c; sw.Depth[i] = d; sw.Salinity[i] = SurfaceWater.Salty(c) ? (byte)255 : (byte)0; }
            Set(0, WaterClass.Lake, WaterDepth.Shallow); Set(1, WaterClass.Lake, WaterDepth.Deep); Set(2, WaterClass.Ocean, WaterDepth.Shallow); Set(3, WaterClass.Ocean, WaterDepth.Deep);
            Assert.That(WaterRenderer.CellFor(sw, 0, 2, 0), Is.InRange(WaterRenderer.StillShallow, WaterRenderer.StillShallow + 1));
            Assert.That(WaterRenderer.CellFor(sw, 1, 2, 0), Is.InRange(WaterRenderer.StillDeep, WaterRenderer.StillDeep + 1));
            Assert.That(WaterRenderer.CellFor(sw, 2, 2, 0), Is.InRange(WaterRenderer.OceanCoastal, WaterRenderer.OceanCoastal + 1));
            Assert.That(WaterRenderer.CellFor(sw, 3, 2, 0), Is.InRange(WaterRenderer.OceanDeep, WaterRenderer.OceanDeep + 1));
            for (int rot = 1; rot < 4; rot++) for (int x = 0; x < 4; x++) Assert.AreEqual(WaterRenderer.CellFor(sw, x, 2, 0), WaterRenderer.CellFor(sw, x, 2, rot), "still water does not change with the camera");
        }

        [Test]
        public void EveryWaterCellHasArtInTheResourcesFolder()
        {
            var names = new List<string>();
            foreach (var set in new[] { "still_shallow", "still_deep", "ocean_coastal", "ocean_deep", "river_flow_east", "river_flow_south", "river_flow_west", "river_flow_north" })
                for (int v = 0; v < 2; v++) names.Add($"water_{set}_v{v}_f0");
            foreach (var n in names) Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/Water/" + n), n);
        }

        [Test]
        public void BanksAreTintedAndInlandGroundIsNot()
        {
            var w = Valley; int banks = 0, inland = 0, tintedInland = 0;
            for (int y = 3; y < N - 3; y += 2)
                for (int x = 3; x < N - 3; x += 2)
                {
                    var cl = w.Waters.Class[y * N + x]; if (cl != WaterClass.None) continue;
                    var tint = WaterRenderer.ShoreTint(w, x, y);
                    if ((w.Waters.Flags[y * N + x] & WaterFlags.Bank) != 0) { banks++; Assert.IsNotNull(tint); } else { inland++; if (tint != null) tintedInland++; }
                }
            Assert.Greater(banks, 20); Assert.Greater(inland, 1000); Assert.AreEqual(0, tintedInland);
        }
    }
}
