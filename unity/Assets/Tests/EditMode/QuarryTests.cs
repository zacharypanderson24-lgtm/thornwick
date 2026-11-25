// Quarry feedback (World.Quarry.cs): what a quarry will dig, what is locked inside a mountain, and the plain-words status.
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class QuarryTests
    {
        const int N = Defs.N;

        static Building QuarryAt(int x, int y) => new Building { T = "quarry", X = x, Y = y, Sz = 2 };

        static (int x, int y)? Find(World w, System.Func<int, int, bool> ok)
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (ok(x, y)) return (x, y);
            return null;
        }

        [Test]
        public void ZonedOpenRockCountsAsDiggable()
        {
            var w = new World(7);
            var t = Find(w, (x, y) => w.DecoMap[y, x] == Deco.Rock && !w.IsMountain(x, y) && w.RockLeft(x, y) > 0);
            Assert.IsNotNull(t, "the map has some hill or outcrop rock");
            var (x, y) = t.Value;
            var q = QuarryAt(x, y);
            Assert.AreEqual(World.QuarryTile.None, w.QuarryTileState(x, y), "unzoned rock is not marked");
            w.ZoneMap[y, x] = Zone.Mine;
            Assert.AreEqual(World.QuarryTile.Dig, w.QuarryTileState(x, y));
            var r = w.QuarryReach(q);
            Assert.GreaterOrEqual(r.Dig, 1);
            Assert.Greater(r.StoneLeft, 0);
            StringAssert.Contains("Digging", w.QuarryStatus(q));
        }

        [Test]
        public void NoZoneSaysWhatToDo()
        {
            var w = new World(7);
            var t = Find(w, (x, y) => w.DecoMap[y, x] == Deco.Rock && !w.IsMountain(x, y));
            Assert.IsNotNull(t);
            var q = QuarryAt(t.Value.x, t.Value.y);
            foreach (var (i, j) in Enumerable.Range(0, N * N).Select(k => (k % N, k / N))) if (w.ZoneMap[j, i] == Zone.Mine) w.ZoneMap[j, i] = Zone.None;
            StringAssert.Contains("Nothing to quarry", w.QuarryStatus(q));
        }

        [Test]
        public void ZonedMountainInteriorIsLockedUntilTheEdgeIsCut()
        {
            var w = new World(7);
            var t = Find(w, (x, y) => w.IsMountain(x, y) && w.RockLeft(x, y) > 0 && !w.RockOpen(x, y));
            if (t == null) Assert.Inconclusive("this seed has no mountain tile fully inside a range");
            var (x, y) = t.Value;
            w.ZoneMap[y, x] = Zone.Mine;
            Assert.AreEqual(World.QuarryTile.Locked, w.QuarryTileState(x, y));
        }

        [Test]
        public void DugPitsAreCounted()
        {
            var w = new World(7);
            var t = Find(w, (x, y) => w.DecoMap[y, x] == Deco.Rock && !w.IsMountain(x, y));
            Assert.IsNotNull(t);
            var (x, y) = t.Value;
            // take enough stone out of the tile that it has become a pit
            w.Dig[World.Tk(x, y)] = w.RockCap(x, y) - 1;
            Assume.That(w.PitDepth(x, y) > 0, "the tile is deep enough to be a pit");
            Assert.GreaterOrEqual(w.QuarryReach(QuarryAt(x, y)).Pits, 1);
        }
    }
}
