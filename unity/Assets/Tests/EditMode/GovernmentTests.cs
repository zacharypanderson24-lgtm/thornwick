using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    // Phase 2 of DESIGN_GOVERNMENT.md: appointments, recruitment, salaries.
    public class GovernmentTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static World Town(int era = 5)
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            w.Era = era;
            w.ResAmt[Res.Gold] = 2000;
            return w;
        }

        static Character Person(World w, Background bg = Background.Educated, int seedBump = 0)
        {
            var ctx = w.CharacterPool();
            var c = CharacterGenerator.Create(w.Characters.NextId, ctx, 5000 + seedBump + w.Characters.NextId, w.Day, bg, 30, 45);
            c.Health = 100;
            return w.Characters.Add(c);
        }

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); } }

        [Test]
        public void ErasUnlockThePostsAndGovernorNeedsAnotherSettlement()
        {
            var w = Town(1); var p = Person(w);
            StringAssert.Contains("Era II", w.CanAppoint(p.Id, Post.Chancellor).Reason);
            Assert.That(w.CanAppoint(p.Id, Post.Governor, 3).Ok, Is.False);
            StringAssert.Contains("no other settlement", w.CanAppoint(p.Id, Post.Governor, 3).Reason);
            w.Era = 2;
            Assert.That(w.CanAppoint(p.Id, Post.Chancellor).Ok);
            Assert.That(w.CanAppoint(p.Id, Post.Steward).Ok);
            StringAssert.Contains("Era III", w.CanAppoint(p.Id, Post.Treasurer).Reason);
            StringAssert.Contains("Era III", w.CanAppoint(p.Id, Post.Marshal).Reason);
            w.Era = 3; Assert.That(w.CanAppoint(p.Id, Post.Marshal).Ok);
            StringAssert.Contains("Era IV", w.CanAppoint(p.Id, Post.Diplomat).Reason);
            w.Era = 4; Assert.That(w.CanAppoint(p.Id, Post.Diplomat).Ok);
        }

        [Test]
        public void SuitabilityIsPerPostAndExplainedInPlainWords()
        {
            var c = new Character { BirthDay = -30 * 360 };
            c.Attr[(int)Attr.Economics] = 80; c.Attr[(int)Attr.Military] = 20; c.Attr[(int)Attr.Administration] = 50; c.Attr[(int)Attr.Stewardship] = 40; c.Attr[(int)Attr.Diplomacy] = 40;
            c.ExperienceHundredths = 3000; c.Loyalty = 50; c.Health = 90;
            Assert.That(Positions.Suitability(c, Post.Treasurer), Is.GreaterThan(Positions.Suitability(c, Post.Marshal) + 25), "no universal rating");
            var good = Positions.Reasons(c, Post.Treasurer, 1);
            var bad = Positions.Reasons(c, Post.Marshal, 1);
            Assert.That(good, Has.Some.Contain("Strong Economics (80)"));
            Assert.That(bad, Has.Some.Contain("Weak Military (20)"));
            Assert.That(Positions.Rating(80), Is.EqualTo("Exceptional")); Assert.That(Positions.Rating(20), Is.EqualTo("Poor"));
            foreach (var d in Positions.All) Assert.That(d.Needs.Sum(n => n.weight), Is.EqualTo(1.0).Within(1e-9), d.Title);
        }

        [Test]
        public void OneMajorAppointmentEachAndPostsHaveOneHolder()
        {
            var w = Town(); var a = Person(w); var b = Person(w);
            Assert.That(w.Appoint(a.Id, Post.Steward).Ok);
            Assert.That(a.Employment, Is.EqualTo(Employment.Government)); Assert.That(a.Post, Is.EqualTo(Post.Steward));
            var r = w.Appoint(a.Id, Post.Chancellor);
            Assert.That(r.Ok, Is.False); StringAssert.Contains("one major appointment", r.Reason);
            r = w.Appoint(b.Id, Post.Steward);
            Assert.That(r.Ok, Is.False); StringAssert.Contains(a.Name, r.Reason);
            Assert.That(w.PostHolder(Post.Steward), Is.SameAs(a));
            Assert.That(w.Promote(a.Id, Post.Chancellor).Ok);
            Assert.That(w.PostHolder(Post.Steward), Is.Null, "the old post is vacant"); Assert.That(w.PostHolder(Post.Chancellor), Is.SameAs(a));
            Assert.That(w.Promote(a.Id, Post.Chancellor).Ok, Is.False);
        }

        [Test]
        public void DismissedPeopleStayInTheWorldAndReplaceIsAllOrNothing()
        {
            var w = Town(); var a = Person(w); var b = Person(w); var dead = Person(w);
            w.Appoint(a.Id, Post.Treasurer);
            dead.Alive = false; dead.Employment = Employment.Deceased;
            Assert.That(w.Replace(dead.Id, Post.Treasurer).Ok, Is.False);
            Assert.That(w.PostHolder(Post.Treasurer), Is.SameAs(a), "a refused replacement changes nothing");
            Assert.That(w.Replace(b.Id, Post.Treasurer).Ok);
            Assert.That(w.PostHolder(Post.Treasurer), Is.SameAs(b));
            Assert.That(a.Alive && a.Employment == Employment.Available && a.Post == Post.None, "dismissed, not gone");
            Assert.That(w.Characters.Get(a.Id), Is.Not.Null);
            Assert.That(a.History.Select(h => h.text), Has.Some.Contain("Dismissed"));
            Assert.That(w.FindCandidates(Post.Treasurer).Select(x => x.C.Id), Does.Contain(a.Id), "and can be hired again");
            Assert.That(w.Dismiss(a.Id).Ok, Is.False);
        }

        [Test]
        public void CandidatesFilterAndSortWithoutChangingAnything()
        {
            var w = Town(); for (int i = 0; i < 12; i++) Person(w, (Background)(i % 7), i);
            int count = w.Characters.Count; string before = w.Characters.Save().ToString();
            var all = w.FindCandidates(Post.Treasurer);
            Assert.That(all.Count, Is.GreaterThan(10));
            Assert.That(all.Select(x => x.Suitability), Is.Ordered.Descending);
            var byEco = w.FindCandidates(Post.Treasurer, null, CandidateSort.Economics);
            Assert.That(byEco.Select(x => x.C.Effective(Attr.Economics)), Is.Ordered.Descending);
            var young = w.FindCandidates(Post.Treasurer, null, CandidateSort.Age, false);
            Assert.That(young.Select(x => x.C.AgeYears(w.Day)), Is.Ordered.Ascending);
            var f = new CandidateFilter { MaxSalary = all.Min(x => x.Salary) };
            Assert.That(w.FindCandidates(Post.Treasurer, f).All(x => x.Salary <= f.MaxSalary));
            var strong = new CandidateFilter(); strong.MinAttr[(int)Attr.Economics] = 60;
            Assert.That(w.FindCandidates(Post.Treasurer, strong).All(x => x.C.Effective(Attr.Economics) >= 60));
            Assert.That(w.FindCandidates(Post.Treasurer, new CandidateFilter { Source = CandidateSource.International }), Is.Empty);
            Assert.That(w.Characters.Count, Is.EqualTo(count)); Assert.That(w.Characters.Save().ToString(), Is.EqualTo(before), "browsing never rerolls");
            var officialsExcluded = w.FindCandidates(Post.Marshal).Select(x => x.C.Id);
            w.Appoint(all[0].C.Id, Post.Treasurer);
            Assert.That(w.FindCandidates(Post.Marshal).Select(x => x.C.Id), Has.None.EqualTo(all[0].C.Id), "the appointed are not candidates");
        }

        [Test]
        public void SalaryFollowsSkillExperienceSizeTraitsAndLevel()
        {
            var w = Town(); var c = Person(w);
            c.Traits.Clear(); c.Traits.Add(Trait.Honest);
            int std = w.SalaryOf(c, Post.Treasurer), low = w.SalaryOf(c, Post.Treasurer, PayLevel.Low), gen = w.SalaryOf(c, Post.Treasurer, PayLevel.Generous);
            Assert.That(low, Is.LessThan(std)); Assert.That(gen, Is.GreaterThan(std)); Assert.That(low, Is.GreaterThan(0));
            c.ExperienceHundredths = 9000; int expert = w.SalaryOf(c, Post.Treasurer); Assert.That(expert, Is.GreaterThan(std));
            c.Traits.Add(Trait.Greedy); Assert.That(w.SalaryOf(c, Post.Treasurer), Is.GreaterThan(expert));
            int small = Pay.Monthly(c, Post.Treasurer, 50, PayLevel.Standard), big = Pay.Monthly(c, Post.Treasurer, 5000, PayLevel.Standard);
            Assert.That(big, Is.GreaterThan(small));
            Assert.That(w.SalaryOf(c, Post.Treasurer), Is.EqualTo(w.SalaryOf(c, Post.Treasurer)), "deterministic");
        }

        [Test]
        public void PaydayTakesExactlyThePayrollFromTheTreasury()
        {
            var w = Town(); var a = Person(w); var b = Person(w);
            w.Appoint(a.Id, Post.Chancellor); w.Appoint(b.Id, Post.Treasurer); w.SetPay(b.Id, PayLevel.Generous);
            int payroll = w.MonthlyPayroll; Assert.That(payroll, Is.EqualTo(a.MonthlySalary + b.MonthlySalary));
            double before = w.ResAmt[Res.Gold];
            w.PayOfficials();
            Assert.That(before - w.ResAmt[Res.Gold], Is.EqualTo(payroll).Within(1e-9), "money leaves the treasury, none is created");
            Assert.That(w.LastPayrollPaid, Is.EqualTo(payroll)); Assert.That(w.LastPayrollDue, Is.EqualTo(payroll));
        }

        [Test]
        public void UnpaidOfficialsLoseLoyaltyThenResign()
        {
            var w = Town(); var a = Person(w); a.Loyalty = 60;
            w.Appoint(a.Id, Post.Chancellor);
            w.ResAmt[Res.Gold] = 0;
            w.PayOfficials(); Assert.That(a.MissedPayments, Is.EqualTo(1)); Assert.That(a.Loyalty, Is.EqualTo(60 - Pay.MissedLoyalty));
            w.PayOfficials(); Assert.That(a.Post, Is.EqualTo(Post.Chancellor));
            w.PayOfficials();
            Assert.That(a.Post, Is.EqualTo(Post.None)); Assert.That(a.Employment, Is.EqualTo(Employment.Available));
            Assert.That(w.Events.Last(), Does.Contain("never paid"));
            Assert.That(w.ResAmt[Res.Gold], Is.EqualTo(0), "never negative, never created");
            // a missed month followed by a paid one starts the count again
            var b = Person(w); w.Appoint(b.Id, Post.Steward);
            w.PayOfficials(); Assert.That(b.MissedPayments, Is.EqualTo(1));
            w.ResAmt[Res.Gold] = 500; w.PayOfficials(); Assert.That(b.MissedPayments, Is.EqualTo(0));
        }

        [Test]
        public void PayLevelMovesLoyaltyAMonthAtATime()
        {
            var w = Town(); var a = Person(w); var b = Person(w); var c = Person(w);
            a.Loyalty = b.Loyalty = c.Loyalty = 50;
            w.Appoint(a.Id, Post.Chancellor); w.Appoint(b.Id, Post.Steward); w.Appoint(c.Id, Post.Treasurer);
            w.SetPay(a.Id, PayLevel.Low); w.SetPay(c.Id, PayLevel.Generous);
            w.PayOfficials(); w.PayOfficials();
            Assert.That(a.Loyalty, Is.EqualTo(48)); Assert.That(b.Loyalty, Is.EqualTo(50)); Assert.That(c.Loyalty, Is.EqualTo(52));
            Assert.That(a.MonthlySalary, Is.LessThan(b.MonthlySalary + 100));
        }

        [Test]
        public void AnAgedOfficialEventuallyLeavesAndThePlayerIsTold()
        {
            var w = Town(); var a = Person(w);
            a.BirthDay = w.Day - 80 * 360; a.Health = 5;
            w.Appoint(a.Id, Post.Chancellor);
            for (int d = 0; d < 360 && a.Post != Post.None; d++) { w.ResAmt[Res.Food] = 150; w.ResAmt[Res.Gold] = 2000; w.Tick(); }
            Assert.That(a.Post, Is.EqualTo(Post.None), "an 80 year old in poor health does not serve for ever");
            Assert.That(a.Employment, Is.EqualTo(Employment.Retired).Or.EqualTo(Employment.Deceased));
            Assert.That(w.Events.Any(e => e.Contains("post of Chancellor is vacant")));
            Assert.That(w.PostHolder(Post.Chancellor), Is.Null);
        }

        [Test]
        public void RecruitingAbroadCostsGoldAndTimeAndGivesOnePersonNoRerolls()
        {
            var w = Town(3);
            var friendly = w.Settlements.Where(s => s.WillTrade).OrderBy(w.SettlementKm).First();
            var cold = w.Settlements.First(s => !s.WillTrade);
            StringAssert.Contains("not on speaking terms", w.CanSearchAbroad(Post.Treasurer, cold.Id).Reason);
            double gold = w.ResAmt[Res.Gold];
            Assert.That(w.SearchAbroad(Post.Treasurer, friendly.Id).Ok);
            Assert.That(gold - w.ResAmt[Res.Gold], Is.EqualTo(World.ForeignSearchGold));
            Assert.That(w.Searches.Count, Is.EqualTo(1));
            StringAssert.Contains("already looking", w.CanSearchAbroad(Post.Treasurer, friendly.Id).Reason);
            Assert.That(w.FindCandidates(Post.Treasurer, new CandidateFilter { Source = CandidateSource.International }), Is.Empty, "it takes time");
            int days = w.SearchDays(friendly.Id); Assert.That(days, Is.GreaterThanOrEqualTo(World.ForeignBaseDays));
            int people = w.Characters.Count;
            Days(w, days + 1);
            Assert.That(w.Searches, Is.Empty);
            var foreign = w.FindCandidates(Post.Treasurer, new CandidateFilter { Source = CandidateSource.International });
            Assert.That(foreign.Count, Is.EqualTo(1), "exactly one person comes back");
            Assert.That(w.Characters.Count - people, Is.GreaterThanOrEqualTo(1));
            var f = foreign[0].C;
            Assert.That(f.Civilization, Is.EqualTo(1 + friendly.Id)); Assert.That(f.Settlement, Is.EqualTo(friendly.Id));
            StringAssert.Contains("Treasurer", w.Characters.Get(f.Id).History.Last().text);
            w.ResAmt[Res.Gold] = 2000;
            StringAssert.Contains("lately", w.CanSearchAbroad(Post.Treasurer, friendly.Id).Reason);   // no endless rerolls
            Assert.That(w.CanSearchAbroad(Post.Marshal, friendly.Id).Ok, "another post is a different search");
            Assert.That(w.Appoint(f.Id, Post.Treasurer).Ok);
            Assert.That(f.Civilization, Is.EqualTo(0)); Assert.That(f.Settlement, Is.EqualTo(World.CapitalId));
        }

        [Test]
        public void ASearchIsTheSameInTwoWorldsAndSurvivesASaveMidJourney()
        {
            var a = Town(3); var b = Town(3);
            var p = a.Settlements.Where(s => s.WillTrade).OrderBy(a.SettlementKm).First();
            a.SearchAbroad(Post.Marshal, p.Id); b.SearchAbroad(Post.Marshal, p.Id);
            Days(a, 5); Days(b, 5);
            var loaded = World.LoadJson(a.SaveJson());
            Assert.That(loaded.Searches.Count, Is.EqualTo(1));
            Assert.That(loaded.Searches[0].ReadyDay, Is.EqualTo(a.Searches[0].ReadyDay));
            loaded.SetSettlements(Settlements.Generate(Map));
            int n = a.SearchDays(p.Id);
            Days(a, n); Days(b, n); Days(loaded, n);
            Assert.That(a.Searches, Is.Empty); Assert.That(loaded.Searches, Is.Empty);
            Character foreignOf(World w) => w.Characters.All.Last(c => c.Civilization != 0);
            string name(World w) => foreignOf(w).Name;
            Assert.That(name(a), Is.EqualTo(name(b)), "deterministic");
            Assert.That(name(loaded), Is.EqualTo(name(a)), "the same person after load");
            Assert.That(foreignOf(loaded).Look.Seed, Is.EqualTo(foreignOf(a).Look.Seed));
        }

        [Test]
        public void OfficialsPayAndSearchesSurviveASaveOnPaydayAndOldSavesStillLoad()
        {
            var w = Town(); var a = Person(w); a.Loyalty = 40;
            w.Appoint(a.Id, Post.Chancellor); w.SetPay(a.Id, PayLevel.Generous);
            w.ResAmt[Res.Gold] = 0; w.PayOfficials();   // an unpaid month
            Assert.That(a.MissedPayments, Is.EqualTo(1));
            var json = w.SaveJson();
            var l = World.LoadJson(json);
            l.SetSettlements(Settlements.Generate(Map));
            var la = l.Characters.Get(a.Id);
            Assert.That(la.Post, Is.EqualTo(Post.Chancellor)); Assert.That(la.Pay, Is.EqualTo(PayLevel.Generous)); Assert.That(la.MissedPayments, Is.EqualTo(1));
            Assert.That(la.AppointedDay, Is.EqualTo(a.AppointedDay)); Assert.That(la.MonthlySalary, Is.EqualTo(a.MonthlySalary));
            Assert.That(l.LastPayrollDue, Is.EqualTo(w.LastPayrollDue)); Assert.That(l.LastPayday, Is.EqualTo(w.LastPayday));
            Assert.That(l.SaveJson(), Is.EqualTo(json));
            Days(w, 35); Days(l, 35);   // crosses a payday
            Assert.That(l.SaveJson(), Is.EqualTo(w.SaveJson()));
            var o = JObject.Parse(json); o.Remove("government");   // a phase 1 save has no government block
            var old = World.LoadJson(o.ToString());
            Assert.That(old.Searches, Is.Empty); Assert.That(old.PostHolder(Post.Chancellor), Is.Not.Null, "appointments live in the character rows");
        }

        // ---------- the Government menu (GovernmentData) ----------
        [Test]
        public void GovernmentMenuOpensWhenAPostExistsAndShowsRealRows()
        {
            var w = Town(1);
            Assert.That(Game.UI.MenuRegistry.WhyLocked(Game.UI.MenuId.Government, w), Is.Not.Null);
            w.Era = 2;
            Assert.That(Game.UI.MenuRegistry.WhyLocked(Game.UI.MenuId.Government, w), Is.Null);
            var s = new Game.UI.GovernmentUiState();
            var council = Game.UI.GovernmentData.Rows(w, 0, s);
            Assert.That(council.Count(r => r.Id != null && r.Id.StartsWith("gov:post:")), Is.EqualTo(6));
            Assert.That(council.First(r => r.Label.Contains("Treasurer")).Value, Does.Contain("Era III"));
            Assert.That(council.First(r => r.Label.Contains("Chancellor")).Value, Does.Contain("Vacant"));
            Assert.That(council.First(r => r.Label.Contains("Governor")).Value, Does.Contain("No other settlement"));
            var recruit = Game.UI.GovernmentData.Rows(w, 1, s);
            Assert.That(recruit.Count(r => r.Id != null && r.Id.StartsWith("gov:cand:")), Is.EqualTo(System.Math.Min(w.FindCandidates(Post.Chancellor).Count, Game.UI.GovernmentData.CandidateRows)));
        }

        [Test]
        public void ClickingThroughTheGovernmentMenuAppointsAndPays()
        {
            var w = Town(2);
            var s = new Game.UI.GovernmentUiState();
            Game.UI.GovernmentData.Click(w, s, "gov:post:Steward", out int tab); Assert.That(tab, Is.EqualTo(1)); Assert.That(s.Post, Is.EqualTo(Post.Steward));
            var best = Game.UI.GovernmentData.CandidatesFor(w, s)[0];
            Assert.That(Game.UI.GovernmentData.Rows(w, 1, s).First(r => r.Id == "gov:cand:" + best.C.Id).Value, Does.Contain(best.Rating));
            Game.UI.GovernmentData.Click(w, s, "gov:cand:" + best.C.Id, out tab); Assert.That(tab, Is.EqualTo(2));
            var details = Game.UI.GovernmentData.Rows(w, 2, s);
            Assert.That(details.Count, Is.LessThanOrEqualTo(Game.UI.ManagementData.MaxRows));
            Assert.That(details.Any(r => r.Label == "Administration" && r.Bar >= 0)); Assert.That(details.Any(r => r.Label.StartsWith("As Steward")));
            Assert.That(details[0].RightPad, Is.GreaterThan(0), "room for the portrait");
            Assert.That(Game.UI.GovernmentData.AppointSelected(w, s).Ok);
            Assert.That(w.PostHolder(Post.Steward).Id, Is.EqualTo(best.C.Id));
            var pay = Game.UI.GovernmentData.Rows(w, 3, s);
            Assert.That(pay[0].Value, Does.Contain(w.MonthlyPayroll.ToString()));
            Game.UI.GovernmentData.Click(w, s, "gov:pay:" + best.C.Id, out tab);
            Assert.That(best.C.Pay, Is.EqualTo(PayLevel.Generous));
            Assert.That(Game.UI.GovernmentData.DismissSelected(w, s).Ok); Assert.That(w.PostHolder(Post.Steward), Is.Null);
            Assert.That(Game.UI.GovernmentData.AppointSelected(w, new Game.UI.GovernmentUiState()).Ok, Is.False, "nothing selected, nothing appointed");
        }
    }
}
