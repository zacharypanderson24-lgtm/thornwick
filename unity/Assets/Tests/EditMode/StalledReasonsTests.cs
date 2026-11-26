using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class StalledReasonsTests
    {
        static World Town() { var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.StartCampaign(); w.Tick(); return w; }

        [Test] public void AHealthyOrJoblessBuildingHasNoStalledReason()
        {
            var w = Town();
            var home = w.Blds.First(b => b.T == "house");
            Assert.IsEmpty(StalledReasons.For(w, home));
            Assert.IsEmpty(StalledReasons.For(w, null));
        }

        [Test] public void ADisconnectedWorkBuildingSaysNoRoadAndOffersFixes()
        {
            var w = Town();
            var b = w.AddBld("wood", Defs.Start + 20, Defs.Start + 20, 2); w.Recalc();
            Assert.IsFalse(b.Served);
            var r = StalledReasons.For(w, b);
            Assert.AreEqual(1, r.Count);
            StringAssert.Contains("no road", r[0].Why);
            Assert.GreaterOrEqual(r[0].Fixes.Count, 2);
        }

        [Test] public void NoIdleVillagersOffersFixesFromDifferentPaths()
        {
            var w = Town();
            var b = w.Blds.FirstOrDefault(x => x.Jobs > 0 && x.Served && w.ActiveJobs(x) > x.Workers);
            if (b == null) Assert.Ignore("every work building is fully staffed in this seed");
            var r = StalledReasons.For(w, b).First(x => x.Why.Contains("work"));
            if (w.D.Idle <= 0)
            {
                Assert.IsTrue(r.Fixes.Any(f => f.Contains("housing")) && r.Fixes.Any(f => f.Contains("Trade menu")) && r.Fixes.Any(f => f.Contains("Workers list")));
            }
        }

        [Test] public void TheLinesStartWithStalledThenFixes()
        {
            var w = Town();
            var b = w.AddBld("wood", Defs.Start + 20, Defs.Start + 20, 2); w.Recalc();
            var lines = StalledReasons.Lines(w, b);
            StringAssert.StartsWith("Stalled:", lines[0]);
            Assert.IsTrue(lines.Skip(1).All(l => l.StartsWith("Fix:")));
            var ins = InspectionData.Parse("WOODCUTTER LODGE\n" + string.Join("\n", lines));
            Assert.AreEqual(Tone.Bad, ins.Rows[0].Tone);
        }
    }
}
