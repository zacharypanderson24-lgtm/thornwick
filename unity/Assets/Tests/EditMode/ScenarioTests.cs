// Scenarios (Sim/World.Scenarios.cs): the big disasters you can prepare for. Each is announced ahead with a checklist and readiness figure,
// and its damage comes out of the real simulation: a prepared town loses little, an unprepared one a lot.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ScenarioTests
    {
        static readonly int C = Defs.Start;

        static World Town(int day = 365)
        {
            var w = new World(7);
            w.Day = day;
            w.StartCampaign();
            w.ResAmt[Res.Food] = 300;
            return w;
        }

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 150); w.Tick(); } }

        /// <summary>A block of 5 x 3 houses two tiles apart (a fire can jump the gap), with everything else flammable taken away.</summary>
        static World Village(bool wells)
        {
            var w = Town();
            foreach (var b in w.Blds.ToList())
                if (b.T != "hall" && b.T != "road") w.Remove(b.X, b.Y);
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 5; i++)
                {
                    int x = C + 8 + 2 * i, y = C + 8 + 2 * j;
                    w.DecoMap[y, x] = Deco.None;
                    w.AddBld("house", x, y);
                }
            if (wells)
                foreach (var (x, y) in new[] { (C + 10, C + 7), (C + 14, C + 7), (C + 12, C + 13) })
                {
                    w.DecoMap[y, x] = Deco.None;
                    w.AddBld("well", x, y);
                }
            w.Recalc();
            return w;
        }

        static int BurnTheVillage(World w)
        {
            w.RaiseScenario("fire", 0);
            var s = w.CurrentScenario;
            for (int i = 0; i < 60 && w.CurrentScenario != null; i++) Days(w, 1);
            Assert.IsNull(w.CurrentScenario, "the fire ended");
            return s.Burned;
        }

        // ---------- the announcement ----------

        [Test]
        public void AScenarioIsAnnouncedAheadAndDoesNothingUntilItArrives()
        {
            var w = Town();
            w.RaiseScenario("famine");
            Assert.AreEqual(ScenarioPhase.Warned, w.CurrentScenario.Phase);
            Assert.AreEqual(w.Day + World.ScenarioLeadDays, w.CurrentScenario.ArriveDay);
            Assert.IsTrue(w.Events.Any(e => e.Contains("WARNING") && e.Contains("Failed harvest")), string.Join(" | ", w.Events));
            Days(w, World.ScenarioLeadDays - 2);
            Assert.AreEqual(ScenarioPhase.Warned, w.CurrentScenario.Phase);
            Assert.AreEqual(1.0, w.CropYield, "no harm yet");
        }

        [Test]
        public void TheTownIsToldHowReadyItIsAsTheDayComes()
        {
            var w = Town(); w.Events.Clear();
            w.RaiseScenario("host", 15);
            w.Events.Clear();
            Days(w, 1);
            Assert.IsTrue(w.Events.Any(e => e.Contains("in 14 days") && e.Contains("% ready")), string.Join(" | ", w.Events));
        }

        [Test]
        public void ReadinessFollowsWhatTheTownHasNow()
        {
            var w = Town(); w.Soldiers = 0;
            w.RaiseScenario("host");
            int bare = w.ScenarioReadiness();
            w.Soldiers = 20;
            int guarded = w.ScenarioReadiness();
            Assert.Greater(guarded, bare, "soldiers count");
            w.DecoMap[C + 9, C + 9] = Deco.None; w.AddBld("watchtower", C + 9, C + 9); w.Recalc();
            Assert.Greater(w.ScenarioReadiness(), guarded, "so does a watchtower");
            Assert.AreEqual(4, w.ScenarioChecks().Count);
            Assert.That(w.ScenarioReadiness(), Is.InRange(0, 100));
        }

        // ---------- famine ----------

        [Test]
        public void AFamineCutsTheHarvestForNinetyDaysAndThenEnds()
        {
            var w = Town();
            w.RaiseScenario("famine", 1);
            Days(w, 1);
            Assert.AreEqual(ScenarioPhase.Hitting, w.CurrentScenario.Phase);
            Assert.AreEqual(World.FamineYield, w.CropYield);
            Days(w, 80);
            Assert.AreEqual(World.FamineYield, w.CropYield);
            Days(w, 20);
            Assert.AreEqual(1.0, w.CropYield);
            Assert.IsNull(w.CurrentScenario);
            Assert.That(w.LastScenarioEnd, Is.InRange(w.Day - 20, w.Day), "remembered, so the next one is spaced out");
        }

        [Test]
        public void StoringFoodMakesTheTownReadyForAFamine()
        {
            var w = Town(); w.ResAmt[Res.Food] = 0;
            w.RaiseScenario("famine");
            int empty = w.ScenarioReadiness();
            w.ResAmt[Res.Food] = 400;
            Assert.Greater(w.ScenarioReadiness(), empty);
        }

        [Test]
        public void AStockedTownDoesNotStarveInAFamineButABareOneDoes()
        {
            var stocked = Town(); var bare = Town();
            foreach (var w in new[] { stocked, bare }) { w.Peasants = 12; w.RaiseScenario("famine", 1); }
            bare.ResAmt[Res.Food] = 5;
            int fed = 0, hungry = 0;
            for (int i = 0; i < 90; i++)
            {
                stocked.ResAmt[Res.Food] = System.Math.Max(stocked.ResAmt[Res.Food], 400); stocked.Tick();   // a full granary
                bare.Tick();   // nothing laid by: there are no farms here, so food runs out
                if (bare.Starving) hungry++;
                if (stocked.Starving) fed++;
            }
            Assert.AreEqual(0, fed, "a stocked town never starves");
            Assert.Greater(hungry, 0, "an empty larder starves");
        }

        // ---------- fire ----------

        [Test]
        public void WellsAndGapsMakeAFireMuchLessDestructive()
        {
            int without = BurnTheVillage(Village(false));
            int with = BurnTheVillage(Village(true));
            Assert.GreaterOrEqual(without, 5, "with no wells it eats the village");
            Assert.Less(with, without, "wells save buildings");
        }

        [Test]
        public void WellCoverageIsOnTheChecklist()
        {
            var bare = Village(false); var wet = Village(true);
            bare.RaiseScenario("fire"); wet.RaiseScenario("fire");
            Assert.Greater(wet.ScenarioReadiness(), bare.ScenarioReadiness());
            Assert.IsTrue(wet.ScenarioChecks()[0].Ok, wet.ScenarioChecks()[0].Text);
            Assert.IsFalse(bare.ScenarioChecks()[0].Ok);
        }

        [Test]
        public void BulldozingABuildingInTheWayStopsTheFireThere()
        {
            var w = Village(false);
            var first = w.Grid[C + 8, C + 8];
            w.RaiseScenario("fire", 0);
            w.Ignite(first);
            Assert.IsTrue(w.IsBurning(first));
            w.Remove(first.X, first.Y);
            Days(w, 1);
            Assert.IsFalse(w.Burning.Contains(first), "a building that is gone does not burn");
        }

        // ---------- the host ----------

        [Test]
        public void TheHostIsBigAndBeginsARaidWhenItArrives()
        {
            var w = Town();
            w.RaiseScenario("host", 1);
            int size = w.CurrentScenario.Size;
            Assert.GreaterOrEqual(size, 12, "far bigger than a camp's band of 3 to 13");
            Days(w, 1);
            Assert.AreEqual(ScenarioPhase.Hitting, w.CurrentScenario.Phase);
            Assert.IsNotNull(w.Raid);
            Assert.AreEqual(size, w.Raid.Raiders.Count);
        }

        [Test]
        public void AnUndefendedTownTakesFarMoreDamageFromTheHostThanADefendedOne()
        {
            double bareHall = RunHost(0), guardedHall = RunHost(30);
            Assert.Less(bareHall, World.HallMaxHp, "the host hit the Town Hall");
            Assert.Greater(guardedHall, bareHall, "soldiers keep the Hall safer");
        }

        static double RunHost(int soldiers)
        {
            var w = Town(); w.ResAmt[Res.Gold] = 300; w.Soldiers = soldiers; w.Drafting = false;
            w.RaiseScenario("host", 1);
            double lowest = World.HallMaxHp;
            for (int i = 0; i < 80 && !w.Defeated && (w.CurrentScenario != null); i++)
            {
                w.ResAmt[Res.Food] = 300; w.Tick();
                lowest = System.Math.Min(lowest, w.HallHp);
            }
            return lowest;
        }

        // ---------- saving ----------

        [Test]
        public void AScenarioInProgressIsSaved()
        {
            var w = Village(false);
            w.RaiseScenario("fire", 0);
            Days(w, 1);
            Assume.That(w.CurrentScenario != null && w.Burning.Any(), "a fire is burning");
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual("fire", back.CurrentScenario.Id);
            Assert.AreEqual(ScenarioPhase.Hitting, back.CurrentScenario.Phase);
            Assert.AreEqual(w.Burning.Count(), back.Burning.Count());
            Assert.AreEqual(w.CurrentScenario.Burned, back.CurrentScenario.Burned);
        }

        [Test]
        public void AWarningIsSaved()
        {
            var w = Town();
            w.RaiseScenario("host");
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual("host", back.CurrentScenario.Id);
            Assert.AreEqual(w.CurrentScenario.ArriveDay, back.CurrentScenario.ArriveDay);
            Assert.AreEqual(w.CurrentScenario.Size, back.CurrentScenario.Size);
            Assert.AreEqual(w.LastScenarioEnd, back.LastScenarioEnd);
        }

        [Test]
        public void AnOldSaveHasNoScenario()
        {
            var o = Newtonsoft.Json.Linq.JObject.Parse(new World(7).SaveJson());
            o.Remove("scenario"); o.Remove("lastScenarioEnd");
            var w = World.LoadJson(o.ToString());
            Assert.IsNull(w.CurrentScenario);
            Assert.AreEqual(1.0, w.CropYield);
        }
    }
}
