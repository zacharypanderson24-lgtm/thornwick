using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    // Phase 1 of DESIGN_GOVERNMENT.md: characters, traits, notable pools, aging, saves.
    public class CharacterTests
    {
        static PoolContext Pool(int sid, int pop, int seed = 7)
        {
            var c = new PoolContext { SettlementId = sid, Population = pop, Seed = seed };
            double[] w = { 30, 15, 10, 10, 6, 8, 3 };
            Array.Copy(w, c.Weights, 7);
            return c;
        }

        static World Fresh() => new World(1234);

        [Test]
        public void NewWorldStartsWithThreeToFiveFounders()
        {
            var w = Fresh();
            Assert.That(w.NotableCount, Is.InRange(3, 5));
            Assert.That(w.Characters.All.All(c => c.Alive && c.AgeYears(w.Day) >= 18 && c.Settlement == World.CapitalId), Is.True);
            Assert.That(w.Characters.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(w.Characters.Count));
        }

        [Test]
        public void SameSeedGivesTheSamePeopleAndADifferentSeedDoesNot()
        {
            Assert.That(new World(77).Characters.Save().ToString(), Is.EqualTo(new World(77).Characters.Save().ToString()));
            Assert.That(new World(78).Characters.Save().ToString(), Is.Not.EqualTo(new World(77).Characters.Save().ToString()));
        }

        [Test]
        public void ThereAreExactlyFiveAttributes()
        {
            Assert.That(Enum.GetValues(typeof(Attr)).Length, Is.EqualTo(5));
            Assert.That(Enum.GetValues(typeof(Trait)).Length, Is.EqualTo(16));
            Assert.That(Traits.All.Length, Is.EqualTo(16));
            for (int i = 0; i < 16; i++) Assert.That((int)Traits.All[i].Id, Is.EqualTo(i), "the trait table is in enum order");
        }

        [Test]
        public void AttributesAreWeightedExceptionalIsRareAndBackgroundLeansButDoesNotDecide()
        {
            var ctx = Pool(3, 1000);
            var people = Enumerable.Range(0, 4000).Select(i => CharacterGenerator.Create(i + 1, ctx, i, 1)).ToList();
            var all = people.SelectMany(c => c.Attr).ToList();
            Assert.That(all.All(v => v >= 0 && v <= 100));
            Assert.That(all.Average(), Is.InRange(33, 46), "most people are ordinary");
            double high = all.Count(v => v >= 80) / (double)all.Count;
            Assert.That(high, Is.LessThan(0.06), "80 or more is rare");
            Assert.That(high, Is.GreaterThan(0.004), "but it does happen");
            // a merchant's Economics beats a farmer's by a good margin, yet some farmers are better merchants than most merchants
            double merchantEco = people.Where(c => c.Background == Background.Merchant).Average(c => c.Attr[(int)Attr.Economics]);
            double farmerEco = people.Where(c => c.Background == Background.Farmer).Average(c => c.Attr[(int)Attr.Economics]);
            double veteranMil = people.Where(c => c.Background == Background.Veteran).Average(c => c.Attr[(int)Attr.Military]);
            double merchantMil = people.Where(c => c.Background == Background.Merchant).Average(c => c.Attr[(int)Attr.Military]);
            Assert.That(merchantEco - farmerEco, Is.GreaterThan(5));
            Assert.That(veteranMil - merchantMil, Is.GreaterThan(8));
            Assert.That(people.Where(c => c.Background == Background.Farmer).Max(c => c.Attr[(int)Attr.Economics]), Is.GreaterThan(merchantEco), "background does not guarantee");
        }

        [Test]
        public void TraitsAreOneToThreeAndNeverOpposed()
        {
            var ctx = Pool(3, 1000);
            var seen = new HashSet<Trait>();
            for (int i = 0; i < 2000; i++)
            {
                var c = CharacterGenerator.Create(i + 1, ctx, i, 1);
                Assert.That(c.Traits.Count, Is.InRange(1, 3));
                Assert.That(c.Traits.Distinct().Count(), Is.EqualTo(c.Traits.Count));
                foreach (var a in c.Traits) { seen.Add(a); foreach (var b in c.Traits) Assert.That(Traits.Get(a).Opposes == b, Is.False, $"{a} with {b}"); }
            }
            Assert.That(seen.Count, Is.EqualTo(16), "every trait appears");
        }

        [Test]
        public void TraitsGiveModestUnderstandableModifiers()
        {
            foreach (var t in Traits.All)
            {
                Assert.That(t.Text, Is.Not.Empty);
                Assert.That(t.Attr.All(m => Math.Abs(m) <= 5), $"{t.Name} attribute modifiers are small");
                Assert.That(Math.Abs(t.SalaryPct), Is.LessThanOrEqualTo(15));
                Assert.That(Math.Abs(t.Loyalty), Is.LessThanOrEqualTo(15));
            }
            var c = new Character();
            c.Attr[(int)Attr.Military] = 50; c.Traits.Add(Trait.Brave);
            Assert.That(c.Effective(Attr.Military), Is.EqualTo(54));
            c.Attr[(int)Attr.Military] = 98;
            Assert.That(c.Effective(Attr.Military), Is.EqualTo(100), "clamped");
            c.Traits.Add(Trait.Greedy); c.Traits.Add(Trait.Extravagant);
            Assert.That(c.SalaryTraitFactor(), Is.EqualTo(1.30).Within(1e-9));
        }

        [TestCase(50, 2, 4)] [TestCase(200, 4, 8)] [TestCase(500, 8, 15)] [TestCase(1000, 15, 25)]
        [TestCase(5000, 40, 65)] [TestCase(10000, 65, 100)] [TestCase(50000, 150, 250)]
        public void NotableTargetsMatchTheDesign(int pop, int lo, int hi)
        {
            Assert.That(CharacterBalance.TargetNotables(pop), Is.InRange(lo, hi));
            Assert.That(CharacterBalance.TargetNotables(pop + 1), Is.GreaterThanOrEqualTo(CharacterBalance.TargetNotables(pop)), "never shrinks as the town grows");
        }

        [Test]
        public void PoolGrowsTowardsItsTargetAndKeepsEveryoneItAlreadyHas()
        {
            var reg = new CharacterRegistry();
            var ctx = Pool(0, 1000);
            reg.CreateFounders(ctx, 1);
            Assert.That(reg.PoolCount(0), Is.InRange(3, 5));
            var before = reg.All.Select(c => (c.Id, c.Name, c.Look.Seed)).ToList();
            for (int m = 1; m <= 12; m++) reg.StepMonth(m * 30, new[] { ctx });
            Assert.That(reg.PoolCount(0), Is.InRange(15, 25), "a town of 1,000 reaches 15-25 notables in a year or two");
            foreach (var (id, name, seed) in before) { var c = reg.Get(id); Assert.That(c.Name, Is.EqualTo(name)); Assert.That(c.Look.Seed, Is.EqualTo(seed)); }
            Assert.That(reg.Journal.Any(e => e.Kind == CharEventKind.Arrived));
        }

        [Test]
        public void TheEconomyShapesWhoArrives()
        {
            var farm = new World(5); var trade = new World(5);
            var wf = farm.CharacterPool().Weights; var before = trade.CharacterPool().Weights.ToArray();
            trade.AddBld("market", Defs.Start + 4, Defs.Start - 6); trade.Recalc();
            Assert.That(trade.CharacterPool().Weights[(int)Background.Merchant], Is.GreaterThan(before[(int)Background.Merchant]));
            Assert.That(wf[(int)Background.Farmer], Is.GreaterThan(wf[(int)Background.Foreign]));
        }

        [Test]
        public void PeopleAgeChangeStageRetireAndDieButStayInTheWorldRecord()
        {
            var reg = new CharacterRegistry();
            var ctx = Pool(0, 1000);
            reg.FillToTarget(ctx, 1);
            var firstIds = reg.All.Select(c => c.Id).ToList();
            var seeds = reg.All.ToDictionary(c => c.Id, c => c.Look.Seed);
            for (int m = 1; m <= 12 * 50; m++) reg.StepMonth(m * 30, new[] { ctx });
            int end = 50 * 360;
            Assert.That(reg.All.Any(c => c.Employment == Employment.Deceased), Is.True, "50 years kill someone");
            Assert.That(reg.All.Any(c => c.RetiredDay > 0), Is.True, "people retire (and later die)");
            Assert.That(reg.Journal.Any(e => e.Kind == CharEventKind.StageChanged));
            foreach (var id in firstIds)
            {
                var c = reg.Get(id);
                Assert.That(c, Is.Not.Null, "nobody is ever removed");
                Assert.That(c.Look.Seed, Is.EqualTo(seeds[id]), "aging never changes the face");
                if (!c.Alive) { Assert.That(c.Employment, Is.EqualTo(Employment.Deceased)); Assert.That(c.History.Last().text, Does.Contain("Died")); Assert.That(c.Post, Is.EqualTo(Post.None)); }
            }
            Assert.That(reg.All.Where(c => c.Alive).All(c => c.AgeYears(end) < 110), "nobody lives forever");
            Assert.That(reg.PoolCount(0), Is.InRange(14, 28), "the pool is replenished");
            Assert.That(reg.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(reg.Count), "ids are never reused");
            Assert.That(reg.All.Where(c => c.Alive).All(c => c.Stage == c.StageAt(end - end % 30)), "stages follow the calendar");
        }

        [Test]
        public void AgeStagesFollowTheDesignedAges()
        {
            Assert.That(CharacterBalance.StageOfAge(18), Is.EqualTo(AgeStage.Young));
            Assert.That(CharacterBalance.StageOfAge(29), Is.EqualTo(AgeStage.Young));
            Assert.That(CharacterBalance.StageOfAge(30), Is.EqualTo(AgeStage.Adult));
            Assert.That(CharacterBalance.StageOfAge(44), Is.EqualTo(AgeStage.Adult));
            Assert.That(CharacterBalance.StageOfAge(45), Is.EqualTo(AgeStage.MiddleAged));
            Assert.That(CharacterBalance.StageOfAge(59), Is.EqualTo(AgeStage.MiddleAged));
            Assert.That(CharacterBalance.StageOfAge(60), Is.EqualTo(AgeStage.Elderly));
            Assert.That(CharacterBalance.StageOfAge(74), Is.EqualTo(AgeStage.Elderly));
            Assert.That(CharacterBalance.StageOfAge(75), Is.EqualTo(AgeStage.Venerable));
            Assert.That(CharacterBalance.StageIds, Is.EqualTo(new[] { "young", "adult", "middle_aged", "elderly", "venerable" }), "the portrait library's age stage ids");
        }

        [Test]
        public void ExperienceGrowsWithWorkAndSlowsAtTheTop()
        {
            var reg = new CharacterRegistry();
            var a = reg.Add(CharacterGenerator.Create(1, Pool(0, 1000), 1, 1, Background.Educated, 30, 32));
            var b = reg.Add(CharacterGenerator.Create(2, Pool(0, 1000), 2, 1, Background.Educated, 30, 32));
            a.Employment = Employment.Government; b.Employment = Employment.Government;
            a.Health = b.Health = 100;
            a.ExperienceHundredths = 1000; b.ExperienceHundredths = 9000;
            int a0 = a.ExperienceHundredths, b0 = b.ExperienceHundredths;
            for (int m = 1; m <= 24; m++) reg.StepMonth(m * 30, new PoolContext[0]);
            Assert.That(a.ExperienceHundredths - a0, Is.GreaterThan(b.ExperienceHundredths - b0), "improvement slows at high skill");
            Assert.That(b.ExperienceHundredths, Is.LessThanOrEqualTo(10000));
        }

        [Test]
        public void ChangingJobOrClothesNeverChangesTheFace()
        {
            var c = CharacterGenerator.Create(1, Pool(0, 500), 1, 1);
            int seed = c.Look.Seed; int grey = c.Look.GreyAge;
            c.Occupation = "Scribe"; c.Look.Outfit = "robe"; c.Look.Archetype = "diplomatic"; c.Employment = Employment.Government;
            Assert.That(c.Look.Seed, Is.EqualTo(seed));
            Assert.That(c.Look.GreyAge, Is.EqualTo(grey));
            var clone = c.Look.Clone(); clone.Outfit = "armor";
            Assert.That(c.Look.Outfit, Is.EqualTo("robe"));
        }

        [Test]
        public void RetiringOrDyingLeavesAPostVacantAndTellsThePlayer()
        {
            var reg = new CharacterRegistry();
            var a = reg.Add(CharacterGenerator.Create(1, Pool(0, 500), 1, 1)); a.Post = Post.Chancellor; a.PostSettlement = 0; a.Employment = Employment.Government;
            var b = reg.Add(CharacterGenerator.Create(2, Pool(0, 500), 2, 1)); b.Post = Post.Governor; b.Employment = Employment.Government;
            reg.Retire(a, 100); reg.Die(b, 120);
            Assert.That(a.Post, Is.EqualTo(Post.None)); Assert.That(a.Employment, Is.EqualTo(Employment.Retired)); Assert.That(a.Alive);
            Assert.That(b.Alive, Is.False); Assert.That(b.Employment, Is.EqualTo(Employment.Deceased)); Assert.That(b.Post, Is.EqualTo(Post.None));
            Assert.That(reg.Journal.Select(e => e.Text), Has.Some.Contain("post of Chancellor is vacant"));
            Assert.That(reg.Journal.Select(e => e.Text), Has.Some.Contain("post of Governor is vacant"));
            Assert.That(a.History.Last().text, Does.Contain("Chancellor"), "history is kept");
            reg.Die(b, 130); Assert.That(b.DeathDay, Is.EqualTo(120), "dying twice changes nothing");
        }

        [Test]
        public void CharactersSurviveASaveAndTheLoadedWorldContinuesIdentically()
        {
            var w = Fresh();
            for (int d = 0; d < 65; d++) w.Tick();   // two month steps have run
            Assert.That(w.Characters.Journal.Count, Is.GreaterThanOrEqualTo(0));
            var json = w.SaveJson();
            Assert.That(JObject.Parse(json)["characters"], Is.Not.Null);
            var l = World.LoadJson(json);
            Assert.That(l.Characters.Save().ToString(), Is.EqualTo(w.Characters.Save().ToString()));
            Assert.That(l.SaveJson(), Is.EqualTo(json));
            for (int d = 0; d < 40; d++) { w.Tick(); l.Tick(); }   // crosses a month boundary
            Assert.That(l.Characters.Save().ToString(), Is.EqualTo(w.Characters.Save().ToString()));
        }

        [Test]
        public void AnAgeStageChangeAcrossASaveHappensOnceAndTheSame()
        {
            var reg = new CharacterRegistry();
            var c = reg.Add(CharacterGenerator.Create(1, Pool(0, 500), 1, 1, null, 29, 29));
            c.Health = 100; c.BirthDay = 300 - 30 * 360 + 20;   // turns 30 on day 320, so the step on day 330 changes the stage
            c.Stage = c.StageAt(300);
            Assert.That(c.Stage, Is.EqualTo(AgeStage.Young));
            var saved = reg.Save().ToString();
            var reg2 = CharacterRegistry.Load(JObject.Parse(saved));
            reg.StepMonth(330, new PoolContext[0]); reg2.StepMonth(330, new PoolContext[0]);
            Assert.That(reg.Get(1).Stage, Is.EqualTo(AgeStage.Adult));
            Assert.That(reg.Save().ToString(), Is.EqualTo(reg2.Save().ToString()));
            Assert.That(reg.Journal.Count(e => e.Kind == CharEventKind.StageChanged), Is.EqualTo(1));
            reg.StepMonth(360, new PoolContext[0]);
            Assert.That(reg.Journal.Count(e => e.Kind == CharEventKind.StageChanged), Is.EqualTo(1), "only once");
        }

        [Test]
        public void AnOlderSaveWithoutCharactersIsMigratedDeterministically()
        {
            var w = Fresh();
            for (int d = 0; d < 10; d++) w.Tick();
            var o = JObject.Parse(w.SaveJson()); o.Remove("characters");
            var a = World.LoadJson(o.ToString()); var b = World.LoadJson(o.ToString());
            Assert.That(a.NotableCount, Is.GreaterThanOrEqualTo(CharacterBalance.FoundersMin));
            Assert.That(a.Characters.Save().ToString(), Is.EqualTo(b.Characters.Save().ToString()));
            Assert.That(World.LoadJson(a.SaveJson()).Characters.Save().ToString(), Is.EqualTo(a.Characters.Save().ToString()), "and saved from then on");
        }

        [Test]
        public void ThousandsOfNotablesStepQuickly()
        {
            var reg = new CharacterRegistry();
            var pools = Enumerable.Range(0, 50).Select(i => Pool(i, 10000, 9)).ToList();
            foreach (var p in pools) reg.FillToTarget(p, 1);
            Assert.That(reg.Count, Is.GreaterThan(3000));
            var sw = Stopwatch.StartNew();
            for (int m = 1; m <= 12; m++) reg.StepMonth(m * 30, pools);
            sw.Stop();
            TestContext.WriteLine($"50 settlements, {reg.Count} notables, 12 monthly steps: {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / 12.0:0.0} ms a month)");
            Assert.That(sw.ElapsedMilliseconds / 12.0, Is.LessThan(250));
        }
    }
}
