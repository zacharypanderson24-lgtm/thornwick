using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class MenuGroupTests
    {
        [Test] public void SixGroupsHoldEveryPrimaryMenuExactlyOnce()
        {
            Assert.AreEqual(6, MenuRegistry.Groups.Count);
            var all = MenuRegistry.Groups.SelectMany(g => g.Members).ToList();
            CollectionAssert.AreEquivalent(MenuRegistry.Primary.Select(m => m.Id), all, "no menu lost, none listed twice");
        }

        [Test] public void GroupsMatchTheBrief()
        {
            Assert.AreEqual(new[] { MenuGroup.Settlement, MenuGroup.Economy, MenuGroup.Government, MenuGroup.Military, MenuGroup.Technology, MenuGroup.World }, MenuRegistry.Groups.Select(g => g.Group).ToArray());
            Assert.AreEqual(MenuGroup.Economy, MenuRegistry.GroupOf(MenuId.Trade));
            Assert.AreEqual(MenuGroup.World, MenuRegistry.GroupOf(MenuId.Diplomacy));
            Assert.AreEqual(MenuGroup.Settlement, MenuRegistry.GroupOf(MenuId.Population));
            Assert.IsNull(MenuRegistry.GroupOf(MenuId.Notifications)); Assert.IsNull(MenuRegistry.GroupOf(MenuId.System));
        }

        [Test] public void AGroupOpensTheLastUsedMenuOrTheFirstAvailable()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign();
            Assert.AreEqual(MenuId.Settlement, MenuRegistry.EntryOf(MenuGroup.Settlement, w));
            Assert.AreEqual(MenuId.Population, MenuRegistry.EntryOf(MenuGroup.Settlement, w, MenuId.Population));
            Assert.AreEqual(MenuId.Settlement, MenuRegistry.EntryOf(MenuGroup.Settlement, w, MenuId.Trade), "a menu from another group is ignored");
            Assert.AreEqual(MenuId.World, MenuRegistry.EntryOf(MenuGroup.World, w, MenuId.Diplomacy), "Diplomacy is locked, so the world map opens");
        }

        [Test] public void AGroupWhoseMenusAreAllLockedSaysWhy()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign();
            Assert.IsNull(MenuRegistry.EntryOf(MenuGroup.Technology, w));
            StringAssert.Contains("tech tree", MenuRegistry.WhyGroupLocked(MenuGroup.Technology, w));
            Assert.IsNull(MenuRegistry.WhyGroupLocked(MenuGroup.Economy, w));
        }
    }
}
