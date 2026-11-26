using System.Linq;
using CrownAndHearth.Game.UI;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class InspectionAdviceTests
    {
        [Test] public void AProblemWithNoStatedFixGetsOne()
        {
            var ins = InspectionData.Parse("FARMHOUSE\nNot connected: needs a road to the Town Hall");
            var fix = ins.Rows.Last();
            Assert.AreEqual("To fix", fix.Label); StringAssert.Contains("road", fix.Value);
        }

        [Test] public void NoProblemMeansNoAdvice()
        {
            var ins = InspectionData.Parse("FARMHOUSE\nConnected to the Town Hall\nPort: running");
            Assert.IsFalse(ins.Rows.Any(r => r.Label == "To fix"));
        }

        [Test] public void TheSameFixIsShownOnce()
        {
            var ins = InspectionData.Parse("HOUSE\nNot connected: needs a road to the Town Hall\nNot connected again");
            Assert.AreEqual(1, ins.Rows.Count(r => r.Label == "To fix"));
        }

        [Test] public void AdviceComesAfterTheFacts()
        {
            var ins = InspectionData.Parse("MILL\nNo flowing river beside it (frozen or dry): the wheel is still.\nStock: 3");
            Assert.AreEqual("To fix", ins.Rows.Last().Label);
            Assert.AreEqual(Tone.Bad, ins.Rows[0].Tone, "the problem line keeps its place and colour");
        }
    }
}
