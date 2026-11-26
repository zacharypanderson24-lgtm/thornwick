using CrownAndHearth.Game.UI;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SpeedStepTests
    {
        [Test] public void PlusClimbsAndStopsAtFour()
        {
            Assert.AreEqual(2, SpeedSteps.Next(1, 1));
            Assert.AreEqual(4, SpeedSteps.Next(2, 1));
            Assert.AreEqual(4, SpeedSteps.Next(4, 1));
        }

        [Test] public void MinusFallsAndStopsAtOne()
        {
            Assert.AreEqual(2, SpeedSteps.Next(4, -1));
            Assert.AreEqual(1, SpeedSteps.Next(2, -1));
            Assert.AreEqual(1, SpeedSteps.Next(1, -1));
        }

        [Test] public void EitherKeyResumesAPause() => Assert.AreEqual(1, SpeedSteps.Next(0, 1) + SpeedSteps.Next(0, -1) - 1);

        [Test] public void ControlsListMentionsTheSpeedKeys() => Assert.IsTrue(ManagementData.SystemControls().Exists(r => r.Label == "+ and -"));
    }
}
