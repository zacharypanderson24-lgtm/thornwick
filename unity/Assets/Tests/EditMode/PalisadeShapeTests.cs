// How a palisade is drawn and laid out: it runs right up to its gates, and a town's watchtowers stand beside the corners, not on them.
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class PalisadeShapeTests
    {
        static void Lay(World w, string type, int x, int y) { w.DecoMap[y, x] = Deco.None; w.AddBld(type, x, y); }

        [Test]
        public void APalisadeJoinsTheGateInItsOwnLineAndNothingElse()
        {
            var w = new World(7);
            const int x = 100, y = 100;
            Lay(w, "wall", x - 1, y); Lay(w, "road", x, y); Lay(w, "wall", x + 1, y);
            Assert.IsTrue(w.IsGateway(x, y), "a road between two walls is a gate");
            Assert.IsTrue(w.WallJoins(x - 1, y, x, y), "the wall on one side runs up to the gate");
            Assert.IsTrue(w.WallJoins(x + 1, y, x, y), "and the one on the other side");
            Assert.IsFalse(w.WallJoins(x - 1, y, x - 2, y), "an empty tile is not joined");
            Lay(w, "wall", x, y - 1);   // a wall beside the gate, not in line with it
            Assert.IsFalse(w.WallJoins(x, y - 1, x, y), "a wall at the side of the gate does not reach across it");
            Lay(w, "wall", x - 1, y - 1);
            Assert.IsTrue(w.WallJoins(x - 1, y, x - 1, y - 1), "plain wall to wall joining is unchanged");
        }

        [Test]
        public void AGateRunningNorthSouthJoinsItsWallsToo()
        {
            var w = new World(7);
            const int x = 110, y = 110;
            Lay(w, "wall", x, y - 1); Lay(w, "road", x, y); Lay(w, "wall", x, y + 1);
            Assert.IsTrue(w.WallJoins(x, y - 1, x, y));
            Assert.IsTrue(w.WallJoins(x, y + 1, x, y));
            Lay(w, "wall", x - 1, y);
            Assert.IsFalse(w.WallJoins(x - 1, y, x, y), "side wall is not in the gate's line");
        }

        [Test]
        public void ATownsCornersArePalisadeAndEachHasATowerBesideIt()
        {
            foreach (var tier in new[] { SettlementTier.Town, SettlementTier.City })
            {
                var (_, s, site) = TownBuilderTests.BuiltForTests(tier);
                if (site == null) continue;
                var w = site.World; var (x0, y0, x1, y1) = w.TownLayout.Walls.Value;
                int corners = 0;
                foreach (var (cx, cy) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) })
                {
                    var b = w.Grid[cy, cx];
                    if (b == null) continue;   // a corner on rock or water has nothing
                    corners++;
                    Assert.AreEqual("wall", b.T, $"{s.Name}: the corner at {cx},{cy} is palisade, not a tower");
                    bool towerBeside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => w.Grid[cy + d.Item2, cx + d.Item1]?.T == "watchtower");
                    Assert.IsTrue(towerBeside, $"{s.Name}: a watchtower stands beside the corner at {cx},{cy}");
                }
                Assert.GreaterOrEqual(corners, 3, "the palisade has its corners");
                Assert.IsFalse(w.Blds.Any(b => b.T == "watchtower" && (b.X == x0 || b.X == x1) && (b.Y == y0 || b.Y == y1)), "no tower on a corner tile");
            }
        }
    }
}
