// Fighters are drawn spread out inside their tile, so a squad on one path or a ring round one raider is a crowd (Game/BattleSpread.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Game;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class BattleSpreadTests
    {
        [Test]
        public void UnitsSharingATileGetDifferentSpots()
        {
            var s = new BattleSpread();
            var spots = new HashSet<(int, int)>();
            for (int i = 0; i < 8; i++) spots.Add(s.Place(i, 40, 40, false));
            Assert.GreaterOrEqual(spots.Count, 7, "eight soldiers on one tile are not drawn in the same place");
        }

        [Test]
        public void EveryOffsetStaysInsideTheTile()
        {
            var s = new BattleSpread();
            for (int i = 0; i < 40; i++)
            {
                var (dx, dy) = s.Place(i * 13, 5, 5, i % 5 == 0);
                Assert.That(dx, Is.InRange(-BattleSpread.MaxX, BattleSpread.MaxX));
                Assert.That(dy, Is.InRange(-BattleSpread.MaxY, BattleSpread.MaxY));
            }
        }

        [Test]
        public void ALoneUnitStillWobblesOffTheCentreLineAndDifferentUnitsDiffer()
        {
            var offs = Enumerable.Range(0, 30).Select(i => BattleSpread.Offset(i, 0)).Distinct().ToList();
            Assert.GreaterOrEqual(offs.Count, 8, "different units get different steady offsets");
            Assert.AreEqual(BattleSpread.Offset(7, 0), BattleSpread.Offset(7, 0), "an offset is steady frame to frame");
        }

        [Test]
        public void ResetStartsTheFrameAgainAndBodiesDoNotPushTheLiving()
        {
            var s = new BattleSpread();
            var first = s.Place(1, 9, 9, false);
            s.Place(2, 9, 9, true);   // a body on the same tile
            s.Reset();
            Assert.AreEqual(first, s.Place(1, 9, 9, false), "the same unit lands in the same spot next frame");
            var alone = new BattleSpread().Place(1, 9, 9, false);
            var s2 = new BattleSpread(); s2.Place(2, 9, 9, true);
            Assert.AreEqual(alone, s2.Place(1, 9, 9, false), "a corpse does not move the living soldier on its tile");
        }
    }
}
