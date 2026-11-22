using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class PalisadeTests
    {
        static (World w, int x, int y) OpenRow(int length)
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 200;
            int c = Defs.Start;
            var site = Enumerable.Range(0, 31 * 31).Select(i => (x: c - 15 + i % 31, y: c - 15 + i / 31))
                .First(t => Enumerable.Range(0, length).All(i => w.Grid[t.y, t.x + i] == null && !w.HasTree(t.x + i, t.y) && !w.IsSolid(t.x + i, t.y) && w.Claimed(t.x + i, t.y)));
            return (w, site.x, site.y);
        }

        [Test]
        public void APalisadeLineCostsWoodAndBuildsATilePerStep()
        {
            var (w, x, y) = OpenRow(5);
            var path = World.LPath(x, y, x + 4, y);
            Assert.IsTrue(w.WallPathCheck(path).Ok);
            double wood = w.ResAmt[Res.Wood];
            var r = w.PlaceWallPath(path);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(5, r.Count);
            Assert.AreEqual(wood - 10, w.ResAmt[Res.Wood], 1e-9, "2 wood a tile");
            for (int i = 0; i < 5; i++) Assert.AreEqual("wall", w.Grid[y, x + i].T);
            Assert.AreEqual("Palisade", w.Grid[y, x].Name);
            Assert.AreEqual(0, w.PlaceWallPath(path).Count, "building it again costs nothing");
        }

        [Test]
        public void ARoadTileInTheLineIsAGateAndOtherBuildingsBlockIt()
        {
            var (w, x, y) = OpenRow(5);
            w.AddBld("road", x + 2, y);
            var path = World.LPath(x, y, x + 4, y);
            Assert.IsTrue(w.WallPathCheck(path).Ok, "a road tile is a gate");
            w.PlaceWallPath(path);
            Assert.AreEqual("road", w.Grid[y, x + 2].T, "the gate stays a road");
            Assert.AreEqual(4, Enumerable.Range(0, 5).Count(i => w.Grid[y, x + i].T == "wall"));
            var hall = w.Hall;
            var through = World.LPath(hall.X - 1, hall.Y, hall.X + 1, hall.Y);
            Assert.IsFalse(w.WallPathCheck(through).Ok, "a palisade cannot cross a building");
        }

        [Test]
        public void WallsNeedWoodSaveAndLoadAndGiveSomeBack()
        {
            var (w, x, y) = OpenRow(3);
            w.ResAmt[Res.Wood] = 3;
            StringAssert.Contains("Not enough wood", w.WallPathCheck(World.LPath(x, y, x + 2, y)).Reason);
            w.ResAmt[Res.Wood] = 100;
            w.PlaceWallPath(World.LPath(x, y, x + 2, y));
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual("wall", loaded.Grid[y, x + 1].T);
            double wood = loaded.ResAmt[Res.Wood];
            Assert.IsTrue(loaded.Remove(x + 1, y).Ok);
            Assert.Greater(loaded.ResAmt[Res.Wood], wood - 1e-9, "demolishing returns part of the wood");
            Assert.IsNull(loaded.Grid[y, x + 1]);
        }

        [Test]
        public void WallsDoNotLevelTheGroundOrCreateJobs()
        {
            var (w, x, y) = OpenRow(2);
            int jobs = w.D.Jobs;
            double lvl = w.TileLevel(x, y);
            w.PlaceWallPath(World.LPath(x, y, x + 1, y));
            Assert.AreEqual(jobs, w.D.Jobs);
            Assert.AreEqual(lvl, w.TileLevel(x, y), 1e-9, "palisades follow the slope");
        }
    }
}
