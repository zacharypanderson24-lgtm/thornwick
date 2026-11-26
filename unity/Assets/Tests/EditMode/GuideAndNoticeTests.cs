using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class GuideAndNoticeTests
    {
        static World Town() { var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign(); w.Tick(); return w; }

        [Test] public void SeverityHasThreeLevels()
        {
            Assert.AreEqual("Critical", NotificationManager.SeverityLabel(NoteKind.Danger));
            Assert.AreEqual("Warning", NotificationManager.SeverityLabel(NoteKind.Warning));
            Assert.AreEqual("Info", NotificationManager.SeverityLabel(NoteKind.Info));
            Assert.AreEqual("Info", NotificationManager.SeverityLabel(NoteKind.Success));
        }

        [Test] public void MessagesLinkToTheMenuTheyAreAbout()
        {
            Assert.AreEqual(MenuId.Military, NotificationManager.LinkFor("Raiders! A band of 12 is coming in from the north."));
            Assert.AreEqual(MenuId.Settlement, NotificationManager.LinkFor("The settlement is starving."));
            Assert.IsNull(NotificationManager.LinkFor("The harvest begins."));
        }

        [Test] public void MessageRowsShowCriticalInWordsAndCarryTheLink()
        {
            var n = new NotificationManager(); n.Add(5, "Raiders! A band of 12 is coming in from the north.", null, out _); n.Add(6, "The harvest begins.", null, out _);
            var rows = ManagementData.Messages(n, false);
            var raid = rows.First(r => r.Value.Contains("Raiders"));
            StringAssert.StartsWith("Critical:", raid.Value); Assert.AreEqual("open:Military", raid.Id);
            Assert.IsNull(rows.First(r => r.Value.Contains("harvest")).Id);
        }

        [Test] public void TheGuideStartsWithFoodAndFollowsTheRealTown()
        {
            var w = Town();
            foreach (var b in w.Blds.Where(b => new[] { "farm", "hunter", "fisher", "dock" }.Contains(b.T)).ToList()) w.Remove(b.X, b.Y);
            Assert.AreEqual("food", FirstYearGuide.Next(w)?.Id);
            var steps = FirstYearGuide.Steps(w);
            Assert.AreEqual(new[] { "food", "timber", "stone", "housing", "winter" }, steps.Select(s => s.Id).ToArray());
            Assert.IsTrue(steps.All(s => !string.IsNullOrEmpty(s.Why)), "every step says why");
        }

        [Test] public void WinterStepOnlyAsksOnceTheYearIsWellOnAndStoresAreShort()
        {
            var w = Town(); w.Day = 50;
            Assert.IsTrue(FirstYearGuide.Steps(w).First(s => s.Id == "winter").Done, "too early to worry");
            w.Day = 200; w.ResAmt[Res.Food] = 0; w.ResAmt[Res.SmokedMeat] = 0;
            if (w.DailyFoodDemand > 0) Assert.IsFalse(FirstYearGuide.Steps(w).First(s => s.Id == "winter").Done);
            w.ResAmt[Res.Food] = w.DailyFoodDemand * World.SeasonLength + 1;
            Assert.IsTrue(FirstYearGuide.Steps(w).First(s => s.Id == "winter").Done);
        }
    }
}
