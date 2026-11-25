using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>The goal-style commands and the plain sentences behind the Military panel.</summary>
    public class MilitaryUiTests
    {
        static void Days(World w, int n) { for (int i = 0; i < n; i++) w.Mil.StepDaily(0); w.Recalc(); }

        [Test]
        public void SoldierGoalRecruitsTowardsTheNumberAndLowerDischarges()
        {
            var w = MilitaryTests.Town(30);
            Assert.IsTrue(w.Mil.SetSoldierGoal(6, MilitaryUnitType.Spearman).Ok);
            Assert.AreEqual(6, w.Mil.SoldierGoal); Assert.AreEqual(0, w.Soldiers, "recruits arrive over days");
            Days(w, 3);
            Assert.AreEqual(6, w.Soldiers);
            int peasants = w.Peasants;
            Assert.IsTrue(w.Mil.SetSoldierGoal(2, MilitaryUnitType.Spearman).Ok);
            Assert.AreEqual(2, w.Soldiers); Assert.AreEqual(2, w.Mil.SoldierGoal); Assert.AreEqual(peasants + 4, w.Peasants);
            Assert.IsTrue(w.Mil.SetSoldierGoal(0, MilitaryUnitType.Spearman).Ok);
            Assert.AreEqual(0, w.Mil.Companies.Count(c => c.Category == MilitaryCategory.Professional), "empty companies are removed");
        }

        [Test]
        public void SoldierGoalNeedsABarracksAndSpillsIntoMoreCompanies()
        {
            var bare = new World(7);
            Assert.IsFalse(bare.Mil.SetSoldierGoal(5, MilitaryUnitType.Spearman).Ok);
            var w = MilitaryTests.Town(100);
            w.Mil.SetSoldierGoal(50, MilitaryUnitType.Spearman);
            Assert.AreEqual(50, w.Mil.SoldierGoal);
            Assert.AreEqual(2, w.Mil.Companies.Count(c => c.Category == MilitaryCategory.Professional), "a company holds at most 40 spearmen");
            Assert.IsFalse(w.Mil.SetSoldierGoal(60, MilitaryUnitType.PeasantLevy).Ok, "the levy is not full-time");
        }

        [Test]
        public void RollGoalAddsAndRemovesMilitiaAndTheCallUpButtonCountsEveryone()
        {
            var w = MilitaryTests.Town(30);
            Assert.IsTrue(w.Mil.SetRoll(12).Ok); Assert.AreEqual(12, w.Mil.Registered);
            Assert.IsTrue(w.Mil.SetRoll(5).Ok); Assert.AreEqual(5, w.Mil.Registered);
            Assert.AreEqual(5, w.Mil.MobiliseAll());
            Assert.AreEqual(5, w.Mil.MobilisedCount);
            Assert.IsTrue(w.Mil.DemobiliseCount(2).Ok); Assert.AreEqual(3, w.Mil.MobilisedCount);
            Assert.IsTrue(w.Mil.MobiliseCount(2).Ok); Assert.AreEqual(5, w.Mil.MobilisedCount);
            Assert.AreEqual(5, w.Mil.Registered, "nobody created or lost");
        }

        [Test]
        public void SentencesSayWhatWillHappen()
        {
            var w = MilitaryTests.Town(30);
            int workers = w.Mil.CivilianWorkforce;
            StringAssert.Contains("Build a barracks", MilitaryAdvice.SoldiersLine(new World(7)));
            w.Mil.SetSoldierGoal(4, MilitaryUnitType.Spearman);
            string s = MilitaryAdvice.SoldiersLine(w);
            StringAssert.Contains($"workers {workers} -> {workers - 4}", s);
            w.Mil.SetRoll(10);
            StringAssert.Contains("keep working", MilitaryAdvice.RollLine(w));
            string call = MilitaryAdvice.CallUpLine(w);
            StringAssert.Contains($"{w.Mil.CivilianWorkforce} -> {w.Mil.CivilianWorkforce - 10}", call);
            w.Mil.MobiliseAll();
            StringAssert.Contains("Sending them home puts 10 workers back", MilitaryAdvice.CallUpLine(w));
        }

        [Test]
        public void SentenceWarnsWhenNotEnoughBunks()
        {
            var w = MilitaryTests.Town(100);
            w.Mil.SetSoldierGoal(30, MilitaryUnitType.Spearman);
            StringAssert.Contains("more bunks", MilitaryAdvice.SoldiersLine(w));
        }


        [Test]
        public void CreateCompanyInOneStepWithAGeneratedOrGivenName()
        {
            var w = MilitaryTests.Town(30);
            var r = w.Mil.CreateCompany(MilitaryUnitType.PeasantLevy, "", 12);
            Assert.IsTrue(r.Ok, r.Reason);
            var c = w.Mil.Find(r.Count);
            Assert.AreEqual("Village Militia", c.Name); Assert.AreEqual(12, c.Personnel); Assert.IsTrue(c.IsMilitia);
            var p = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.Spearman, "Hall Guard", 6).Count);
            Assert.AreEqual("Hall Guard", p.Name); Assert.AreEqual(6, p.Target); Assert.AreEqual(0, p.Personnel, "professionals arrive over days");
            Assert.AreEqual("2nd Village Militia", MilitaryUi.DefaultName(w, MilitaryUnitType.PeasantLevy));
            Assert.AreEqual("Thornwick Spearmen".Replace("Thornwick", "1st Thornwick"), MilitaryUi.DefaultName(w, MilitaryUnitType.Spearman));
            var bare = new World(7);
            Assert.AreEqual("Muster Ground required", bare.Mil.CreateCompany(MilitaryUnitType.PeasantLevy, "x", 5).Reason);
            Assert.AreEqual("Barracks required", bare.Mil.CreateCompany(MilitaryUnitType.Spearman, "x", 5).Reason);
        }

        [Test]
        public void ActionsAreDisabledWithAClearReason()
        {
            var w = MilitaryTests.Town(30);
            var bare = new World(7);
            var levy = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.PeasantLevy, "", 5).Count);
            var acts = MilitaryUi.Actions(w, levy);
            Assert.IsTrue(acts.First(a => a.Id == "register").Enabled);
            Assert.IsTrue(acts.First(a => a.Id == "mobilise").Enabled);
            Assert.IsTrue(acts.First(a => a.Id == "disband").Danger);
            var pro = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.Spearman, "", 4).Count);
            w.Mil.SetRoll(0);
            Assert.IsTrue(MilitaryUi.Actions(w, pro).First(a => a.Id == "recruit").Enabled);
            Assert.AreEqual("Insufficient personnel", MilitaryUi.Actions(w, pro).First(a => a.Id == "discharge").Reason);
            w.Mil.SetRoll(5); w.Mil.MobiliseAll();
            var mob = w.Mil.Companies.First(c => c.IsMilitia);
            var ma = MilitaryUi.Actions(w, mob);
            Assert.IsTrue(ma.Any(a => a.Id == "demobilise" && a.Enabled)); Assert.IsFalse(ma.Any(a => a.Id == "mobilise"));
            w.Mil.DemobiliseAll(); w.Mil.SetRoll(w.Mil.MusterCapacity);
            var reg = MilitaryUi.Actions(w, w.Mil.Companies.First(c => c.IsMilitia)).First(a => a.Id == "register");
            Assert.IsFalse(reg.Enabled); Assert.IsNotEmpty(reg.Reason);
            // no barracks beds left
            var full = MilitaryTests.Town(100);
            var big = full.Mil.Find(full.Mil.CreateCompany(MilitaryUnitType.Spearman, "", MilitaryConfig.BarracksCapacity + 4).Count);
            for (int i = 0; i < 20; i++) full.Mil.StepDaily(0);
            full.Recalc();
            Assert.AreEqual(MilitaryConfig.BarracksCapacity, full.Soldiers);
            Assert.AreEqual("No free barracks accommodation", MilitaryUi.Actions(full, big).First(a => a.Id == "recruit").Reason);
            Assert.AreEqual(0, MilitaryUi.MaxMore(full, big));
        }

        [Test]
        public void FiltersOnlyListKindsThatExistAndMatchRight()
        {
            var w = MilitaryTests.Town(40);
            Assert.AreEqual(new[] { CompanyFilter.All }, MilitaryUi.AvailableFilters(w).ToArray());
            var levy = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.PeasantLevy, "", 5).Count);
            var arch = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.HunterArcher, "", 4).Count);
            var f = MilitaryUi.AvailableFilters(w);
            CollectionAssert.Contains(f, CompanyFilter.Reserve); CollectionAssert.Contains(f, CompanyFilter.Professional); CollectionAssert.Contains(f, CompanyFilter.Ranged);
            CollectionAssert.DoesNotContain(f, CompanyFilter.Mobilised);
            Assert.IsTrue(MilitaryUi.Matches(arch, CompanyFilter.Ranged)); Assert.IsFalse(MilitaryUi.Matches(arch, CompanyFilter.Infantry));
            Assert.IsTrue(MilitaryUi.Matches(levy, CompanyFilter.Infantry)); Assert.IsTrue(MilitaryUi.Matches(levy, CompanyFilter.Reserve));
            w.Mil.MobiliseAll();
            Assert.IsTrue(MilitaryUi.Matches(levy, CompanyFilter.Mobilised)); Assert.IsFalse(MilitaryUi.Matches(levy, CompanyFilter.Reserve));
        }

        [Test]
        public void ConsequenceSentencesCarryTheRealNumbers()
        {
            var w = MilitaryTests.Town(30);
            int workers = w.Mil.CivilianWorkforce;
            var pro = w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.Spearman, "", 8).Count);
            string rec = MilitaryUi.RecruitSentence(w, pro, 8);
            StringAssert.Contains($"Civilian workforce: {workers} → {workers - 8}", rec);
            StringAssert.Contains($"Barracks occupancy: 0/{w.Mil.BunkCapacity} → 8/{w.Mil.BunkCapacity}", rec);
            StringAssert.Contains("keep their civilian jobs until mobilized", MilitaryUi.RegisterSentence(w, 5));
            StringAssert.Contains($"from {workers} to {workers - 24}", MilitaryUi.MobiliseSentence(w, 24));
            StringAssert.Contains("Its training", MilitaryUi.DisbandSentence(w.Mil.Find(w.Mil.CreateCompany(MilitaryUnitType.PeasantLevy, "", 5).Count)));
            var tip = MilitaryUi.StatTip("Training", pro);
            StringAssert.Contains("Training:", tip.title); StringAssert.Contains("Professional soldiers train every day", tip.body);
        }
        [Test]
        public void StatusWordsAreOneOfAFewPlainWords()
        {
            var w = MilitaryTests.Town(30);
            w.Mil.SetSoldierGoal(4, MilitaryUnitType.Spearman);
            var c = w.Mil.Companies.First(x => x.Category == MilitaryCategory.Professional);
            Assert.AreEqual("Recruiting", MilitaryAdvice.StatusWord(c, w));
            Days(w, 3);
            Assert.AreEqual("Training", MilitaryAdvice.StatusWord(c, w));
            c.Training = 70; Assert.AreEqual("Ready", MilitaryAdvice.StatusWord(c, w));
            c.Morale = 10; Assert.AreEqual("Low spirits", MilitaryAdvice.StatusWord(c, w));
            w.Mil.SetRoll(5);
            var m = w.Mil.Companies.First(x => x.IsMilitia);
            Assert.AreEqual("On the roll", MilitaryAdvice.StatusWord(m, w));
            w.Mil.MobiliseAll();
            Assert.AreEqual("Gathering", MilitaryAdvice.StatusWord(m, w));
        }
    }
}
