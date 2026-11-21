// Mining and lumber should explain themselves and not leave the player waiting in the dark.
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ClarityTests
    {
        [Test]
        public void AMineBringsUpOreWhileItsZoneIsStillBeingSurveyed()
        {
            var (w, mine) = MiningTests.MineTown(true);
            StringAssert.Contains("Surveying", w.MineStatus(mine));
            double ore = w.ResAmt[Res.Ore];
            int first = 0;
            for (int day = 1; day <= 120 && first == 0; day++)
            {
                w.ResAmt[Res.Food] = 300; w.Tick();
                if (w.ResAmt[Res.Ore] > ore + 0.5) first = day;
            }
            Assert.Greater(first, 0, "ore arrives");
            Assert.LessOrEqual(first, 70, "within about a minute at normal speed, not after the whole zone is surveyed (it took about 90 days)");
            string status = w.MineStatus(mine);
            Assert.That(status, Does.Contain("Mining ore").Or.Contain("Digging the adit"));
        }

        [Test]
        public void ALowlandMineSaysWhyItFindsNothing()
        {
            var (w, mine) = MiningTests.MineTown(false);
            for (int day = 1; day <= 120; day++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            StringAssert.Contains("foot of a hill", w.MineStatus(mine));
        }

        [Test]
        public void PilingUpLogsPointsAtTheSawmillOnce()
        {
            var w = new World(7);
            foreach (var s in w.Blds.Where(b => b.T == "sawmill").ToList()) w.Remove(s.X, s.Y);
            w.Events.Clear();
            w.ResAmt[Res.Logs] = 60;
            w.Tick();
            Assert.IsTrue(w.Events.Any(e => e.Contains("Sawmill")), "the hint appears");
            w.Events.Clear();
            w.Tick();
            Assert.IsFalse(w.Events.Any(e => e.Contains("Sawmill")), "only once");
        }
    }
}
