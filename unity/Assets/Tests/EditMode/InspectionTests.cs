// The tile and building inspector layout (Game/UI/InspectionData.cs). The sample lines are the wording GameController.TileInfo produces.
using System.Linq;
using CrownAndHearth.Game.UI;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class InspectionTests
    {
        [Test]
        public void TheFirstLineIsTheHeadingAndEmptyTextGivesNothing()
        {
            var ins = InspectionData.Parse("FARMHOUSE\nConnected to the Town Hall\nWorkers 1/1 needed");
            Assert.AreEqual("FARMHOUSE", ins.Title); Assert.AreEqual(2, ins.Rows.Count);
            Assert.AreEqual("", InspectionData.Parse("").Title); Assert.IsEmpty(InspectionData.Parse(null).Rows);
        }

        [Test]
        public void ShortLabelledFactsBecomeRowsAndSentencesStayParagraphs()
        {
            var ins = InspectionData.Parse("PORT\nPort: running\nLogs waiting to be sawn: 12\nGives your trade one ship (0 of 1 ships at sea). It stands on lake water: boats sail there.");
            Assert.AreEqual("Port", ins.Rows[0].Label); Assert.AreEqual("running", ins.Rows[0].Value); Assert.IsFalse(ins.Rows[0].Wide);
            Assert.AreEqual("Logs waiting to be sawn", ins.Rows[1].Label); Assert.AreEqual("12", ins.Rows[1].Value);
            Assert.IsTrue(ins.Rows[2].Wide);
        }

        [Test]
        public void ProblemsAreMarkedAndTheConnectionIsGood()
        {
            var ins = InspectionData.Parse("HOUSE\nConnected to the Town Hall\nNot connected: needs a road to the Town Hall\nNo keeper: the animals give nothing and do not breed.\nHomes 4 villagers");
            Assert.AreEqual(Tone.Good, ins.Rows[0].Tone);
            Assert.AreEqual(Tone.Bad, ins.Rows[1].Tone);
            Assert.AreEqual(Tone.Bad, ins.Rows[2].Tone);
            Assert.AreEqual(Tone.Normal, ins.Rows[3].Tone);
            Assert.IsTrue(ins.Rows[1].Wide, "a problem keeps its whole sentence");
        }

        [Test]
        public void TheRichTextKeepsEveryWordAndNothingIsLost()
        {
            string raw = "WELL\nConnected to the Town Hall\nFish stock: plentiful (80%)\nA well here could tap it.";
            var ins = InspectionData.Parse(raw);
            string body = InspectionData.Body(ins);
            foreach (var word in new[] { "Connected to the Town Hall", "Fish stock", "plentiful (80%)", "A well here could tap it." }) StringAssert.Contains(word, body);
            Assert.AreEqual(3, body.Split('\n').Length);
        }
    }
}
