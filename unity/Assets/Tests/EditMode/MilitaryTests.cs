using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>Phase 1 of the military overhaul: population-based recruitment, militia, professionals, companies, mobilisation, saves.</summary>
    public class MilitaryTests
    {
        static void Place(World w, string type)
        {
            int c = Defs.Start;
            var site = Enumerable.Range(0, 31 * 31).Select(i => (x: c - 15 + i % 31, y: c - 15 + i / 31)).First(t => w.CanPlace(type, t.x, t.y).Ok);
            Assert.IsTrue(w.Place(type, site.x, site.y).Ok, type);
        }

        /// <summary>A town with a barracks and a muster ground and this many peasants (set directly, so no tick trims them to the housing cap).</summary>
        static World Town(int peasants = 30)
        {
            var w = new World(7);
            foreach (var r in Enum.GetValues(typeof(Res)).Cast<Res>()) w.ResAmt[r] = 500;
            Place(w, "barracks"); Place(w, "muster");
            // fixed-size jobs (storehouses, smokehouses, charcoal clamps) so the workforce maths has work to fill whatever the season
            foreach (var type in new[] { "storehouse", "storehouse", "smokehouse", "smokehouse", "charcoal", "charcoal" })
            {
                int c0 = Defs.Start;
                var spot = Enumerable.Range(0, 31 * 31).Select(i => (x: c0 - 15 + i % 31, y: c0 - 15 + i / 31)).Cast<(int x, int y)?>().FirstOrDefault(t => w.CanPlace(type, t.Value.x, t.Value.y).Ok);
                if (spot != null) w.Place(type, spot.Value.x, spot.Value.y);
            }
            w.Peasants = peasants; w.Recalc();
            Assert.GreaterOrEqual(w.D.Jobs, 4, "the test town has work to do");
            return w;
        }

        static int WorkersTotal(World w) => w.Blds.Sum(b => b.Workers);
        static void Recruit(World w, MilitaryCompany c, int n) { w.Mil.SetTarget(c.Id, n); for (int i = 0; i < 20; i++) w.Mil.StepDaily(0); w.Recalc(); }
        static MilitaryCompany Spears(World w) { Assert.IsTrue(w.Mil.CreateCompany(MilitaryUnitType.Spearman).Ok); return w.Mil.Companies.Last(); }

        static void Invariants(World w, string when)
        {
            var m = w.Mil;
            foreach (var c in m.Companies) { Assert.GreaterOrEqual(c.Personnel, 0, when + ": personnel"); Assert.LessOrEqual(c.Personnel, c.Max, when + ": company size"); }
            Assert.AreEqual(w.Soldiers, m.Professionals, when + ": professionals match the population's soldiers");
            Assert.LessOrEqual(m.Registered, w.Peasants, when + ": militia are civilians");
            Assert.LessOrEqual(m.MobilisedCount, m.Registered, when + ": mobilised are registered");
            Assert.AreEqual(m.Companies.Count, m.Companies.Select(c => c.Id).Distinct().Count(), when + ": unique ids");
            Assert.GreaterOrEqual(w.Peasants, 0); Assert.GreaterOrEqual(w.Soldiers, 0); Assert.LessOrEqual(w.Archers, w.Soldiers);
        }

        [Test]
        public void RegisteringMilitiaDoesNotReduceTheWorkforce()
        {
            var w = Town(30);
            int workers = WorkersTotal(w), idle = w.D.Idle, force = w.Mil.CivilianWorkforce;
            var r = w.Mil.RegisterMilitia(12);
            Assert.IsTrue(r.Ok, r.Reason); Assert.AreEqual(12, r.Count);
            Assert.AreEqual(12, w.Mil.Registered);
            Assert.AreEqual(force, w.Mil.CivilianWorkforce);
            Assert.AreEqual(workers, WorkersTotal(w)); Assert.AreEqual(idle, w.D.Idle);
        }

        [Test]
        public void MobilisingCutsCivilianLabourByExactlyTheCompanySize()
        {
            var w = Town();
            w.Mil.RegisterMilitia(10);
            int force = w.Mil.CivilianWorkforce;
            var c = w.Mil.Companies[0];
            Assert.IsTrue(w.Mil.Mobilise(c.Id).Ok);
            Assert.AreEqual(force - 10, w.Mil.CivilianWorkforce);
            Assert.AreEqual(w.Peasants - 10, w.D.Employed + w.D.Idle, "the workforce Recalc hands out is the civilians left");
            Assert.AreEqual(w.Peasants, 30, "mobilised militia are still residents");
            Assert.AreEqual(w.Mil.MobilisedCount, 10);
        }

        [Test]
        public void DemobilisingRestoresTheSameJobsInTheSameBuildings()
        {
            var w = Town();
            w.Peasants = w.D.Jobs; w.Recalc();   // every worker is needed
            Assert.IsTrue(w.Mil.RegisterMilitia(2).Ok);
            var before = w.Blds.Select(b => b.Workers).ToList(); var eff = w.Blds.Select(b => b.Eff).ToList();
            w.Mil.MobiliseAll();
            Assert.Less(WorkersTotal(w), before.Sum(), "somebody's job went unfilled while they were called up");
            w.Mil.DemobiliseAll();
            CollectionAssert.AreEqual(before, w.Blds.Select(b => b.Workers).ToList());
            CollectionAssert.AreEqual(eff, w.Blds.Select(b => b.Eff).ToList());
        }

        [Test]
        public void CivilianProductionRespondsToMobilisation()
        {
            var w = Town();
            w.Peasants = w.D.Jobs; w.Recalc();
            w.Mil.RegisterMilitia(2);
            double effBefore = w.Blds.Where(b => b.Jobs > 0).Sum(b => b.Eff);
            w.Mil.MobiliseAll();
            Assert.Less(w.Blds.Where(b => b.Jobs > 0).Sum(b => b.Eff), effBefore, "buildings run below full staffing, so they produce less");
        }

        [Test]
        public void ProfessionalRecruitmentRemovesWorkersAndTakesTheUnemployedFirst()
        {
            var w = Town();
            w.Peasants = w.D.Jobs + 5; w.Recalc();
            int employed = w.D.Employed;
            Assert.AreEqual(5, w.D.Idle);
            var c = Spears(w);
            Recruit(w, c, 4);
            Assert.AreEqual(4, w.Soldiers); Assert.AreEqual(4, c.Personnel);
            Assert.AreEqual(employed, w.D.Employed, "the jobless were recruited, nobody lost a job");
            Assert.AreEqual(1, w.D.Idle);
            Recruit(w, c, 8);   // now the jobless run out and workers go
            Assert.Less(w.D.Employed, employed);
            Assert.AreEqual(w.Peasants, w.Mil.CivilianWorkforce);
        }

        [Test]
        public void RecruitsArriveOverDaysNotAllAtOnce()
        {
            var w = Town();
            var c = Spears(w);
            w.Mil.SetTarget(c.Id, 8);
            Assert.AreEqual(0, c.Personnel, "setting a target recruits nobody yet");
            w.Mil.StepDaily(0); Assert.AreEqual(MilitaryConfig.RecruitPerDay, c.Personnel);
            w.Mil.StepDaily(0); Assert.AreEqual(MilitaryConfig.RecruitPerDay * 2, c.Personnel);
        }

        [Test]
        public void DischargeGivesTheWorkforceBack()
        {
            var w = Town();
            var c = Spears(w);
            Recruit(w, c, 6);
            int peasants = w.Peasants, free = w.Mil.AvailableToServe;
            Assert.IsTrue(w.Mil.Discharge(c.Id, 4).Ok);
            Assert.AreEqual(peasants + 4, w.Peasants); Assert.AreEqual(2, w.Soldiers); Assert.AreEqual(2, c.Personnel);
            Assert.AreEqual(free + 4, w.Mil.AvailableToServe, "discharged soldiers are eligible again");
            Invariants(w, "discharge");
        }

        [Test]
        public void RecruitmentCannotExceedTheEligiblePopulation()
        {
            var w = Town(10);
            var c = Spears(w);
            Recruit(w, c, 40);
            Assert.LessOrEqual(w.Soldiers, 10 - MilitaryConfig.MinCivilians, "the town keeps a few civilians");
            Assert.GreaterOrEqual(w.Peasants, MilitaryConfig.MinCivilians);
            Invariants(w, "eligible");
        }

        [Test]
        public void BarracksCapacityIsEnforced()
        {
            var w = Town(100);
            var a = Spears(w); var b = Spears(w);
            Recruit(w, a, 40); Recruit(w, b, 40);
            Assert.AreEqual(MilitaryConfig.BarracksCapacity, w.Soldiers, "one barracks houses 16");
            Assert.AreEqual(0, w.Mil.BunksFree);
            Assert.AreEqual(0, w.Mil.RecruitableNow());
        }

        [Test]
        public void ProfessionalsMustComeFromCiviliansNotOnTheMilitiaRoll()
        {
            var w = Town(20);
            Assert.IsTrue(w.Mil.RegisterMilitia(15).Ok);
            var c = Spears(w);
            Recruit(w, c, 10);
            Assert.AreEqual(3, w.Soldiers, "20 civilians - 15 militia - 2 kept = 3 free; nobody is in two companies");
            Assert.AreEqual(15, w.Mil.Registered);
            Assert.IsFalse(w.Mil.RegisterMilitia(5).Ok, "no civilians are left to register");
        }

        [Test]
        public void NobodyIsCountedTwiceInPopulationOrFood()
        {
            var w = Town(30);
            double food = w.DailyFoodDemand; int pop = w.Peasants + w.Soldiers;
            w.Mil.RegisterMilitia(10); w.Mil.MobiliseAll();
            Assert.AreEqual(food, w.DailyFoodDemand, 1e-9, "militia eat as the civilians they are, registered or mobilised");
            Assert.AreEqual(pop, w.Peasants + w.Soldiers);
            var c = Spears(w); Recruit(w, c, 4);
            Assert.AreEqual(pop, w.Peasants + w.Soldiers, "a recruit moves from civilians to soldiers, the population is unchanged");
            Assert.AreEqual(food + 4 * (World.SoldierFoodFactor - 1) * (w.SeasonalFarming ? World.FoodPerPerson : World.ClassicFoodPerPerson), w.DailyFoodDemand, 1e-9, "only the soldiers' bigger appetite is added");
        }

        [Test]
        public void CasualtiesReduceTheRealPopulation()
        {
            var w = Town(30);
            w.Mil.RegisterMilitia(10); w.Mil.MobiliseAll();
            var c = Spears(w); Recruit(w, c, 6);
            int pop = w.Peasants + w.Soldiers;
            w.Soldiers -= 2; w.Recalc();   // two professionals fall in a raid
            Assert.AreEqual(4, c.Personnel); Assert.AreEqual(pop - 2, w.Peasants + w.Soldiers);
            w.Peasants = 7; w.Recalc();   // wounded and starved civilians: the roll cannot be bigger than the civilians
            Assert.AreEqual(7, w.Mil.Registered);
            Invariants(w, "casualties");
        }

        [Test]
        public void MobilisingAndDemobilisingNeverCreatesOrLosesMen()
        {
            var w = Town();
            w.Mil.RegisterMilitia(10); var c = w.Mil.Companies[0];
            double training = c.Training;
            for (int i = 0; i < 20; i++) { w.Mil.Mobilise(c.Id); w.Mil.Demobilise(c.Id); }
            Assert.AreEqual(10, c.Personnel); Assert.AreEqual(0, c.Equipped); Assert.AreEqual(10, w.Mil.Registered);
            Assert.AreEqual(training, c.Training, 1e-9);
            Assert.IsFalse(w.Mil.Demobilise(c.Id).Ok, "demobilising twice does nothing");
        }

        [Test]
        public void DestroyingTheBarracksDoesNotDeleteItsSoldiers()
        {
            var w = Town();
            var c = Spears(w); Recruit(w, c, 6);
            var b = w.Blds.First(x => x.T == "barracks");
            Assert.IsTrue(w.Remove(b.X, b.Y).Ok);
            Assert.AreEqual(0, w.Mil.BunkCapacity);
            w.ResAmt[Res.Food] = 500; w.Tick();
            Assert.AreEqual(6, w.Soldiers); Assert.AreEqual(6, c.Personnel);
            Assert.AreEqual(6, w.Mil.Unhoused);
            Assert.AreEqual(0, w.Mil.RecruitableNow(), "no new recruits without bunks");
        }

        [Test]
        public void MilitaryStateSurvivesSaveAndLoad()
        {
            var w = Town(30);
            w.Mil.RegisterMilitia(12); w.Mil.Rename(w.Mil.Companies[0].Id, "Hall Watch"); w.Mil.Mobilise(w.Mil.Companies[0].Id);
            var c = Spears(w); Recruit(w, c, 6); w.Mil.Order(c.Id, MilitaryOrderKind.HoldPosition, 70, 71);
            for (int i = 0; i < 5; i++) { w.ResAmt[Res.Food] = 500; w.Tick(); }
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(w.Mil.Companies.Count, loaded.Mil.Companies.Count);
            for (int i = 0; i < w.Mil.Companies.Count; i++)
            {
                var a = w.Mil.Companies[i]; var b = loaded.Mil.Companies[i];
                Assert.AreEqual((a.Id, a.Name, a.Category, a.Unit, a.Personnel, a.Target, a.Mobilised, a.Order, a.OrderX, a.OrderY),
                                (b.Id, b.Name, b.Category, b.Unit, b.Personnel, b.Target, b.Mobilised, b.Order, b.OrderX, b.OrderY));
                Assert.AreEqual(a.Training, b.Training, 0.01); Assert.AreEqual(a.Morale, b.Morale, 0.01); Assert.AreEqual(a.Experience, b.Experience, 0.01);
            }
            Assert.AreEqual(w.Mil.CivilianWorkforce, loaded.Mil.CivilianWorkforce);
            Assert.AreEqual(w.D.Employed, loaded.D.Employed, "mobilised militia are still off the job after loading");
            Assert.AreEqual(w.Peasants + w.Soldiers, loaded.Peasants + loaded.Soldiers);
            Invariants(loaded, "loaded");
        }

        [Test]
        public void ACorruptSaveIsRepairedNotTrusted()
        {
            var w = Town(30);
            w.Mil.RegisterMilitia(10); var c = Spears(w); Recruit(w, c, 4);
            var json = JObject.Parse(w.SaveJson());
            var comps = (JArray)json["military"]["companies"];
            comps[0]["personnel"] = 999; comps[1]["personnel"] = -5; comps[1]["category"] = "Nonsense";
            var loaded = World.LoadJson(json.ToString());
            Invariants(loaded, "repaired");
        }

        [Test]
        public void NoProfessionalsWithoutABarracksAndNoSoldiersFromNowhere()
        {
            var w = new World(7);
            Assert.IsFalse(w.Mil.CreateCompany(MilitaryUnitType.Spearman).Ok, "professionals need a barracks");
            Assert.IsFalse(w.Mil.RegisterMilitia(5).Ok, "militia need a muster ground");
            for (int i = 0; i < 120; i++) { w.ResAmt[Res.Food] = 500; w.Tick(); }
            Assert.AreEqual(0, w.Soldiers); Assert.AreEqual(0, w.Mil.Companies.Count);
        }

        [Test]
        public void MusterGroundIsCheapAndIsEraOne()
        {
            var d = MilitaryBuildings.Get("muster");
            Assert.AreEqual(1, d.Era); Assert.AreEqual(2, d.Size); Assert.AreEqual(30, d.Capacity);
            var w = new World(7); w.ResAmt[Res.Wood] = 14;
            int c = Defs.Start;
            var site = Enumerable.Range(0, 31 * 31).Select(i => (x: c - 15 + i % 31, y: c - 15 + i / 31)).First(t => w.CanPlace("muster", t.x, t.y).Ok);
            Assert.IsTrue(w.Place("muster", site.x, site.y).Ok);
            Assert.AreEqual(0, w.ResAmt[Res.Wood]);
            Assert.AreEqual(30, w.Mil.MusterCapacity);
            Assert.AreEqual(1, MilitaryBuildings.All.Count(x => x.Type == "armory"), "the planned buildings are listed as data");
            Assert.IsFalse(MilitaryBuildings.Get("armory").Implemented);
        }

        [Test]
        public void ImpactPreviewWarnsWhenFoodWorkWouldGoUnfilled()
        {
            var w = Town();
            w.Peasants = w.D.Jobs; w.Recalc();
            var im = w.Mil.Impact(0, 0);
            Assert.AreEqual(im.Before, im.After);
            var cut = w.Mil.Impact(w.Peasants - 1, 0);
            Assert.AreEqual(w.Peasants, cut.Before); Assert.AreEqual(1, cut.After);
            Assert.IsNotEmpty(cut.Warning, "nearly everyone leaving work must raise a warning");
        }

        [Test]
        public void MilitiaTrainSlowlyAndProfessionalsMuchFaster()
        {
            var w = Town();
            w.Mil.RegisterMilitia(10); var c = Spears(w); Recruit(w, c, 4);
            var m = w.Mil.Companies.First(x => x.IsMilitia);
            double t0m = m.Training, t0p = c.Training;
            for (int i = 0; i < 60; i++) w.Mil.StepDaily(0);
            double gm = m.Training - t0m, gp = c.Training - t0p;
            Assert.Greater(gm, 0); Assert.Greater(gp, gm * 3, "professionals train much faster than militia");
            Assert.LessOrEqual(m.Training, MilitaryConfig.MilitiaTrainCap + 1e-6);
        }

        [Test]
        public void NewcomersDiluteACompanysExperienceAndTraining()
        {
            var w = Town();
            var c = Spears(w); Recruit(w, c, 4);
            c.Experience = 60; c.Training = 70;
            w.Mil.SetTarget(c.Id, 8); w.Mil.StepDaily(0); w.Mil.StepDaily(0);
            Assert.AreEqual(8, c.Personnel);
            Assert.Less(c.Experience, 60); Assert.Less(c.Training, 70);
        }

        [Test]
        public void RandomCommandsAtAnySpeedKeepEveryInvariantAndAreDeterministic()
        {
            string Run(int ticksPerStep)
            {
                var w = Town(30); var rng = new System.Random(5);
                for (int step = 0; step < 150; step++)
                {
                    var m = w.Mil;
                    switch (rng.Next(9))
                    {
                        case 0: m.RegisterMilitia(rng.Next(1, 8)); break;
                        case 1: if (m.Companies.Count > 0) m.Mobilise(m.Companies[rng.Next(m.Companies.Count)].Id); break;
                        case 2: m.DemobiliseAll(); break;
                        case 3: m.CreateCompany(rng.Next(2) == 0 ? MilitaryUnitType.Spearman : MilitaryUnitType.HunterArcher); break;
                        case 4: if (m.Companies.Count > 0) { var c = m.Companies[rng.Next(m.Companies.Count)]; m.SetTarget(c.Id, rng.Next(0, 41)); } break;
                        case 5: if (m.Companies.Count > 0) m.Discharge(m.Companies[rng.Next(m.Companies.Count)].Id, rng.Next(1, 6)); break;
                        case 6: if (m.Companies.Count > 0) m.Disband(m.Companies[rng.Next(m.Companies.Count)].Id); break;
                        case 7: w.Soldiers = Math.Max(0, w.Soldiers - rng.Next(0, 3)); break;
                        default: w.Peasants = Math.Max(3, w.Peasants - rng.Next(0, 3)); break;
                    }
                    for (int i = 0; i < ticksPerStep; i++) { w.ResAmt[Res.Food] = 500; w.Tick(); }
                    Invariants(w, $"step {step}");
                }
                return w.Mil.Save().ToString();
            }
            Assert.AreEqual(Run(1), Run(1), "same actions, same result");
            Run(7);   // fast speed: several days between commands
        }

        [Test]
        public void MobiliseThenDemobiliseLeavesJobAllocationExactlyAsIfNeverMobilised()
        {
            var control = Town(); var test = Town();
            control.Peasants = test.Peasants = Math.Min(control.D.Civ, control.D.Jobs);
            control.Recalc(); test.Recalc();
            test.Mil.RegisterMilitia(3);
            for (int day = 1; day <= 30; day++)
            {
                if (day == 5) test.Mil.MobiliseAll();
                if (day == 12) test.Mil.DemobiliseAll();
                foreach (var w in new[] { control, test }) { w.ResAmt[Res.Food] = 500; w.Tick(); }
            }
            Assert.AreEqual(control.Peasants, test.Peasants);
            CollectionAssert.AreEqual(control.Blds.Select(b => b.Workers).ToList(), test.Blds.Select(b => b.Workers).ToList());
        }

        [Test]
        public void MobilisedMilitiaFightRaidersAndAddToDefence()
        {
            var w = Town(30);
            w.Mil.RegisterMilitia(20);
            double none = w.Mil.MilitiaPower();
            Assert.AreEqual(0, none, "militia on the roll but not called up add nothing");
            w.Mil.MobiliseAll();
            for (int i = 0; i < 5; i++) w.Mil.StepDaily(0);
            double power = w.Mil.MilitiaPower();
            Assert.Greater(power, 0);
            Assert.Less(power, 20 * 0.5, "untrained militia are far weaker than the same number of professionals");
            Assert.Greater(w.Mil.FightDamage(2), 0);
        }

        [Test]
        public void ExcessiveRecruitmentCostsRealWorkers()
        {
            var w = Town(30);
            w.Peasants = w.D.Jobs; w.Recalc();
            int employed = w.D.Employed;
            var c = Spears(w); Recruit(w, c, 12); int recruited = w.Soldiers; Assert.Greater(recruited, 0);
            Assert.AreEqual(employed - recruited, w.D.Employed, "every recruit beyond the jobless is a job left unfilled");
            Assert.Less(w.D.Ratio, 1.0);
        }

        [Test]
        public void MobilisingAnExactNumberSplitsADetachmentOff()
        {
            var w = Town();
            w.Mil.RegisterMilitia(10);
            int force = w.Mil.CivilianWorkforce;
            var c = w.Mil.Companies[0];
            Assert.IsTrue(w.Mil.Mobilise(c.Id, 4).Ok);
            Assert.AreEqual(2, w.Mil.Companies.Count);
            Assert.AreEqual(6, c.Personnel); Assert.IsFalse(c.Mobilised);
            Assert.AreEqual(4, w.Mil.MobilisedCount);
            Assert.AreEqual(force - 4, w.Mil.CivilianWorkforce);
            var part = w.Mil.Companies.First(x => x.Mobilised);
            Assert.IsTrue(w.Mil.Demobilise(part.Id, 1).Ok);
            Assert.AreEqual(3, w.Mil.MobilisedCount);
            Assert.AreEqual(10, w.Mil.Registered, "nobody lost or created");
            Invariants(w, "split");
        }

        [Test]
        public void ShieldInfantryCountAsInfantryInTheWorldTotals()
        {
            var w = Town();
            Assert.IsTrue(w.Mil.CreateCompany(MilitaryUnitType.ShieldInfantry).Ok);
            var c = w.Mil.Companies.Last();
            Recruit(w, c, 4);
            Assert.AreEqual(4, w.Infantry); Assert.AreEqual(0, w.Archers);
            Assert.IsTrue(w.Mil.CreateCompany(MilitaryUnitType.HunterArcher).Ok);
            Recruit(w, w.Mil.Companies.Last(), 3);
            Assert.AreEqual(3, w.Archers); Assert.AreEqual(4, w.Infantry);
            Invariants(w, "mixed");
        }
    }
}
