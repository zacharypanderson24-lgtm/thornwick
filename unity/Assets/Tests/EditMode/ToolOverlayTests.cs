using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ToolOverlayTests
    {
        [Test] public void ToolsAskForTheOverlayThatHelpsThem()
        {
            Assert.AreEqual(OverlayId.Fertility, OverlayRegistry.ForTool("farmhouse", null));
            Assert.AreEqual(OverlayId.Fertility, OverlayRegistry.ForTool("zoneFarm", null));
            Assert.AreEqual(OverlayId.Water, OverlayRegistry.ForTool("well", null));
            Assert.AreEqual(OverlayId.Underground, OverlayRegistry.ForTool("mine", null));
            Assert.AreEqual(OverlayId.Sailing, OverlayRegistry.ForTool("dock", null));
        }

        [Test] public void ToolsWithNoNeedAskForNothing()
        {
            foreach (var id in new[] { "look", "road", "sawmill", "barracks", "clear" }) Assert.AreEqual(OverlayId.None, OverlayRegistry.ForTool(id, null), id);
        }

        [Test] public void AnOverlayThatCannotShowAnythingIsNotSwitchedOn()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None);
            if (OverlayRegistry.WhyLocked(OverlayId.Sailing, w) != null) Assert.AreEqual(OverlayId.None, OverlayRegistry.ForTool("dock", w), "no water, no sailing overlay");
            else Assert.AreEqual(OverlayId.Sailing, OverlayRegistry.ForTool("dock", w));
            Assert.AreEqual(OverlayId.Water, OverlayRegistry.ForTool("well", w));
        }
    }
}
