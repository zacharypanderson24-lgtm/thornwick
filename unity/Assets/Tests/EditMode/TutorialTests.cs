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
    }
}
