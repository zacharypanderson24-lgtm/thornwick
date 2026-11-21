// The in-game tutorial (Sim/Tutorial.cs): its steps finish when the player really does the thing in the world, counted from when the
// step began; explanations wait for Next; Back, Skip and Close work.
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TutorialTests
    {
        static int IndexOf(string id) => Tutorial.Steps.Select((s, i) => (s, i)).First(p => p.s.Id == id).i;

        [Test]
        public void EveryStepExplainsItselfAndSaysWhatFinishesIt()
        {
            Assert.That(Tutorial.Steps.Count, Is.GreaterThanOrEqualTo(12));
            Assert.AreEqual(Tutorial.Steps.Count, Tutorial.Steps.Select(s => s.Id).Distinct().Count(), "step ids are unique");
            foreach (var s in Tutorial.Steps)
            {
                Assert.IsNotEmpty(s.Title, s.Id); Assert.IsNotEmpty(s.Body, s.Id); Assert.IsNotEmpty(s.Goal, s.Id);
                Assert.That(s.Body.Length, Is.LessThan(560), $"{s.Id}: keep the text short enough for the panel");
            }
            // the mechanics the game has today are all covered
            foreach (var id in new[] { "camera", "time", "road", "housing", "farmland", "workers", "lumber", "sawmill", "services", "stone", "underground", "danger", "save" })
                Assert.IsTrue(Tutorial.Steps.Any(s => s.Id == id), id);
        }

        [Test]
        public void ExplanationsWaitForNextAndTheTutorialEndsAfterTheLastStep()
        {
            var w = new World(7); var t = new Tutorial();
            t.Start(w);
            Assert.IsTrue(t.Active); Assert.AreEqual("welcome", t.Current.Id);
            Assert.IsNull(t.Check(w), "an explanation does not finish by itself");
            t.Next(w); Assert.AreEqual("camera", t.Current.Id);
            t.Back(w); Assert.AreEqual("welcome", t.Current.Id);
            for (int i = 0; i < Tutorial.Steps.Count; i++) t.Next(w);
            Assert.IsTrue(t.Finished); Assert.IsFalse(t.Active);
            t.Start(w, 3); t.Close(); Assert.IsFalse(t.Active); Assert.AreEqual(3, t.Index, "closing keeps the place");
        }

        [Test]
        public void CameraAndTimeStepsFinishFromThePlayersActions()
        {
            var w = new World(7); var t = new Tutorial();
            t.Start(w, IndexOf("camera"));
            t.Panned = true; Assert.IsNull(t.Check(w), "panning alone is not enough");
            t.Rotated = true; Assert.AreEqual("camera", t.Check(w)?.Id);
            Assert.AreEqual("time", t.Current.Id);
            Assert.IsFalse(t.Panned, "signals reset for each step");
            t.SpeedChanged = true; Assert.AreEqual("time", t.Check(w)?.Id);
        }

        [Test]
        public void RoadAndHousingStepsFinishWhenTheTownChanges()
        {
            var w = new World(7); var t = new Tutorial();
            int C = Defs.Start;
            t.Start(w, IndexOf("road"));
            Assert.IsNull(t.Check(w));
            w.ResAmt[Res.Wood] = 400;
            // extend the starting street (x = C, y = C+2..C+6) by three tiles, clearing anything in the way
            var path = World.LPath(C, C + 7, C, C + 9);
            foreach (var (x, y) in path) if (w.Grid[y, x] == null && w.DecoMap[y, x] != Deco.None) w.Remove(x, y);
            Assume.That(w.PlaceRoadPath(path).Ok, "the street can be extended on this seed");
            w.Recalc();
            Assert.AreEqual("road", t.Check(w)?.Id, "3 connected road tiles finish the road step");

            Assert.AreEqual("housing", t.Current.Id);
            for (int y = C + 7; y <= C + 9; y++) foreach (int x in new[] { C - 1, C + 1 }) if (w.Grid[y, x] == null && w.DecoMap[y, x] != Deco.None) w.Remove(x, y);   // clear trees first
            var (zoned, _, _)= w.ZoneRect(Zone.Res, C + 1, C + 7, C + 1, C + 9);   // three tiles beside the new road
            w.ZoneRect(Zone.Res, C - 1, C + 7, C - 1, C + 9);
            Assume.That(zoned > 0);
            Assert.AreEqual("housing", t.Check(w)?.Id, "4+ Housing tiles beside a connected road finish the housing step");
        }

        [Test]
        public void HousingStepShowsProgressAndCountsHomesThatGrowOnTheZone()
        {
            var w = new World(7); var t = new Tutorial();
            int C = Defs.Start;
            t.Start(w, IndexOf("housing"));
            Assert.AreEqual("0 / 4", t.Progress(w));
            // two zoned tiles beside the street, then a home grows on one of them: still 2 of 4, not 1
            int zoned = 0;
            for (int y = C + 2; y <= C + 6 && zoned < 2; y++)
                if (w.Grid[y, C + 1] == null && w.BesideConnectedRoad(C + 1, y) && w.ZoneRect(Zone.Res, C + 1, y, C + 1, y).zoned == 1) zoned++;
            Assume.That(zoned == 2, "two open tiles beside the starting street on this seed");
            Assert.AreEqual("2 / 4", t.Progress(w));
            var tile = Enumerable.Range(C + 2, 5).First(y => w.Grid[y, C + 1] == null && w.ZoneMap[y, C + 1] == Zone.Res);
            w.AddBld("house", C + 1, tile, 1); w.Recalc();
            Assert.AreEqual("2 / 4", t.Progress(w), "a home on a zoned tile keeps the count");
            Assert.IsNull(t.Check(w));
            Assert.IsNull(new Tutorial().Progress(w), "no progress line when the tutorial is not running");
        }

        [Test]
        public void ZoningHelpersSeeExistingZonesAndConnectedRoads()
        {
            var w = new World(7);
            int C = Defs.Start;
            int y = Enumerable.Range(C + 2, 5).First(j => w.Grid[j, C + 1] == null && w.ZoneCheck(Zone.Res, C + 1, j).Ok);
            Assert.IsTrue(w.BesideConnectedRoad(C + 1, y), "beside the starting street");
            Assert.IsFalse(w.AlreadyZoned(Zone.Res, C + 1, y));
            w.ZoneRect(Zone.Res, C + 1, y, C + 1, y);
            Assert.IsTrue(w.AlreadyZoned(Zone.Res, C + 1, y));
            Assert.AreEqual(1, w.ZonedUnder("silo", C + 1, y), "a 1x1 building there would cover the zoned tile");
            Assert.IsTrue(w.WouldConnect("silo", C + 1, y));
            Assert.IsFalse(w.WouldConnect("silo", 2, 2), "the map corner is nowhere near a road");
        }

        [Test]
        public void ProgressMadeBeforeAStepBeganDoesNotCount()
        {
            var w = new World(7); var t = new Tutorial();
            int C = Defs.Start;
            w.ZoneRect(Zone.Res, C + 1, C + 2, C + 1, C + 2);
            var farmZoned = w.ZoneRect(Zone.Farm, C - 12, C - 12, C + 12, C + 12).zoned;   // lots of Farmland before the step
            Assume.That(farmZoned >= 6);
            t.Start(w, IndexOf("farmland"));
            Assert.IsNull(t.Check(w), "zoning done before the step began does not finish it");
        }

        [Test]
        public void TheRaidStepSendsRaidersOnceAndSoldiersHoldThemOff()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Gold] = 100; w.ResAmt[Res.Food] = 100;
            var t = new Tutorial();
            t.Start(w, IndexOf("raid") - 1);
            Assert.IsNull(t.Raid);
            t.Next(w);   // onto the raid step
            Assert.IsNotNull(t.Raid);
            Assert.AreEqual(0, t.Raid.Defenders);
            Assert.AreEqual(80, w.ResAmt[Res.Wood], 1, "an undefended town loses 60% of its wood");
            StringAssert.Contains("raiders", t.BodyText);
            StringAssert.Contains("wood", t.BodyText);
            double wood = w.ResAmt[Res.Wood];
            t.Back(w); t.Next(w);
            Assert.AreEqual(wood, w.ResAmt[Res.Wood], 1e-9, "the raid comes once");

            var defended = new World(7);
            defended.ResAmt[Res.Wood] = 200;
            defended.Soldiers = 12;
            var held = defended.Raid(Tutorial.RaidBand);
            Assert.AreEqual(1.0, held.HeldOff, 1e-9);
            Assert.AreEqual(200, defended.ResAmt[Res.Wood], 1e-9, "a fully held band steals nothing");
            Assert.AreEqual(9, defended.Soldiers, "a quarter of the defenders fell");
        }
    }
}
