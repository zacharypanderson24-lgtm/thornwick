// Town health cards (Sim/World.Health.cs): Work and Safety, each green / amber / red with a short warning and a tip.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class HealthTests
    {
        [Test]
        public void AFreshTownIsHealthy()
        {
            var w = new World(7);
            w.Recalc();
            var work = w.WorkHealth();
            var safety = w.SafetyHealth();
            Assert.AreEqual(HealthLevel.Good, work.Level, work.Detail);
            Assert.AreEqual(HealthLevel.Good, safety.Level, safety.Detail);
            Assert.AreEqual("", work.Warning);
            Assert.AreEqual("", safety.Warning);
            StringAssert.Contains("Too small", safety.Detail, "a tiny town is not raided yet");
        }

        [Test]
        public void ManyIdlePeasantsTurnWorkRed()
        {
            var w = new World(7);
            w.Peasants = 60;
            w.Recalc();
            Assume.That(w.D.Idle >= 3 && w.D.Idle >= 0.4 * w.Peasants, "the start town has few jobs, so most of 60 peasants are idle");
            var work = w.WorkHealth();
            Assert.AreEqual(HealthLevel.Bad, work.Level);
            Assert.AreEqual("Many idle", work.Warning);
            StringAssert.Contains($"{w.D.Idle} of 60", work.Detail);
        }

        [Test]
        public void ASmallIdleShareOnlyAsksForAWatch()
        {
            var work = World.WorkCard(peasants: 40, idle: 8, jobs: 40, employed: 32);   // 20% idle
            Assert.AreEqual(HealthLevel.Watch, work.Level);
            Assert.AreEqual("Idle hands", work.Warning);
            Assert.AreEqual(HealthLevel.Good, World.WorkCard(40, 2, 40, 38).Level, "two idle is normal");
            Assert.AreEqual(HealthLevel.Bad, World.WorkCard(40, 16, 24, 24).Level, "40% idle is red");
        }

        [Test]
        public void ASmallVillageIsNeverFlaggedForIdleHands()
        {
            var work = World.WorkCard(peasants: World.WorkMinPeasants - 1, idle: World.WorkMinPeasants - 2, jobs: 1, employed: 1);
            Assert.AreEqual(HealthLevel.Good, work.Level, "the opening village has idle hands by design");
            Assert.AreEqual("", work.Warning);
        }

        [Test]
        public void UnfilledJobsAreAmberWhenNobodyIsIdle()
        {
            var work = World.WorkCard(peasants: 20, idle: 0, jobs: 26, employed: 20);   // 6 jobs open
            Assert.AreEqual(HealthLevel.Watch, work.Level);
            Assert.AreEqual("Jobs unfilled", work.Warning);
            Assert.AreEqual(HealthLevel.Good, World.WorkCard(20, 0, 22, 20).Level, "two open jobs is normal");
        }

        [Test]
        public void ABigTownWithNoSoldiersIsAmber()
        {
            var w = new World(7);
            w.Peasants = w.RaidMinPopulation + 5; w.Soldiers = 0;
            var s = w.SafetyHealth();
            Assert.AreEqual(HealthLevel.Watch, s.Level);
            Assert.AreEqual("No defenders", s.Warning);
            Assert.AreEqual("No troops", s.Value);
            w.Soldiers = 4;
            s = w.SafetyHealth();
            Assert.AreEqual(HealthLevel.Good, s.Level, "soldiers at home make it green");
            Assert.AreEqual("4 troops", s.Value);
        }

        [Test]
        public void ASpottedWarbandTurnsSafetyRedWithADayCount()
        {
            var w = new World(7);
            w.Drafting = false;
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            var camp = new RaiderCamp { Id = 4, X = 300, Y = 390, Strength = 2, Seed = 12347, Name = "Test camp" };   // 90 km south: 6 days of marching
            w.SetCamps(new[] { camp }, 300, 300);
            bool red = false; string warning = "", detail = "";
            for (int i = 0; i < 400 && !red; i++)
            {
                w.ResAmt[Res.Food] = 400;
                w.Tick();
                if (w.Warband != null && w.Warband.Spotted)
                {
                    var s = w.SafetyHealth();
                    red = s.Level == HealthLevel.Bad; warning = s.Warning; detail = s.Detail;
                }
            }
            Assert.IsTrue(red, "safety goes red once the warband is spotted");
            StringAssert.StartsWith("Raiders in", warning);
            StringAssert.Contains("south", detail);
        }
    }
}
