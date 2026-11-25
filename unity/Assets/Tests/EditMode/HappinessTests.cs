// Happiness (Sim/World.Happiness.cs), festivals, and uprisings (Sim/World.Uprising.cs).
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class HappinessTests
    {
        static World Town(int peasants = 30)
        {
            var w = new World(7);
            w.StartCampaign();
            w.Peasants = peasants;
            w.Day = 100;
            w.ResAmt[Res.Food] = 4000; w.ResAmt[Res.Gold] = 200;
            return w;
        }

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 4000); w.Tick(); } }

        // ---------- the number ----------

        [Test]
        public void ANewTownStartsContent()
        {
            var w = new World(7);
            Assert.AreEqual(HappinessTuning.Start, w.Happiness);
            Assert.That(w.MoodName, Is.EqualTo("Content"));
        }

        [Test]
        public void TheLedgerListsWhatIsMovingHappinessBiggestFirst()
        {
            var w = Town(); w.Recalc();
            var ledger = w.HappinessLedger();
            Assert.IsNotEmpty(ledger);
            for (int i = 1; i < ledger.Count; i++) Assert.GreaterOrEqual(System.Math.Abs(ledger[i - 1].Delta), System.Math.Abs(ledger[i].Delta));
            Assert.IsTrue(ledger.Any(e => e.Text.Contains("granaries")), string.Join(" | ", ledger.Select(e => e.Text)));
        }

        [Test]
        public void StarvingTurnsTheTownMiserableAndAFedTownStaysHappy()
        {
            var fed = Town(8); var starving = Town(8);   // a small town: its spring is enough and the work factor does not apply yet
            starving.ResAmt[Res.Food] = 0;
            for (int i = 0; i < 15; i++) { fed.ResAmt[Res.Food] = 4000; fed.Tick(); starving.ResAmt[Res.Food] = 0; starving.Tick(); }
            Assert.Greater(fed.Happiness, 55, "a well-fed town is content");
            Assert.Less(starving.Happiness, HappinessTuning.Unrest, "a starving one is restless");
            Assert.IsTrue(starving.HappinessLedger().Any(e => e.Text.Contains("starving")));
        }

        [Test]
        public void ItDriftsGraduallyNotInAJump()
        {
            var w = Town(); w.Recalc();
            double before = w.Happiness;
            w.ResAmt[Res.Food] = 0; w.Tick();
            Assert.That(before - w.Happiness, Is.InRange(0, 10), "one day moves it a little");
        }

        [Test]
        public void ItDoesNotMoveWithoutTheCampaign()
        {
            var w = new World(7); w.Peasants = 30; w.ResAmt[Res.Food] = 0;
            for (int i = 0; i < 20; i++) w.Tick();
            Assert.AreEqual(HappinessTuning.Start, w.Happiness);
        }

        // ---------- the three kinds of cause ----------

        [Test]
        public void ATemporaryMoodCountsUntilItEndsAndCanBeReplaced()
        {
            var w = Town(); w.Recalc();
            double target = w.HappinessTarget();
            w.AddMood("test", +15, 5, "A good omen");
            Assert.AreEqual(System.Math.Min(100, target + 15), w.HappinessTarget(), 1e-9);
            Assert.IsTrue(w.HappinessLedger().Any(e => e.Text == "A good omen"));
            w.AddMood("test", +5, 5, "A smaller omen");
            Assert.AreEqual(1, w.Moods.Count, "same id replaces");
            Days(w, 6);
            Assert.IsFalse(w.HappinessLedger().Any(e => e.Text.Contains("omen")), "it wore off");
            Assert.AreEqual(0, w.Moods.Count);
        }

        [Test]
        public void AOneOffShiftMovesItAtOnceThenFadesBack()
        {
            var w = Town(); w.Recalc();
            double before = w.Happiness;
            w.HappinessShift(-30, "A disaster");
            Assert.AreEqual(before - 30, w.Happiness, 1e-9);
            Assert.AreEqual("A disaster", w.HappinessLog.Last().text);
            Days(w, 30);
            Assert.Greater(w.Happiness, before - 30 + 10, "it recovers toward what the town is really like");
        }

        [Test]
        public void ShiftsStayInRange()
        {
            var w = Town();
            w.HappinessShift(+500, "x"); Assert.AreEqual(100, w.Happiness);
            w.HappinessShift(-500, "x"); Assert.AreEqual(0, w.Happiness);
        }

        // ---------- festivals ----------

        [Test]
        public void AFestivalCostsGoldAndFoodAndLiftsTheMood()
        {
            var w = Town(); w.Recalc();
            w.HappinessShift(-30, "low");
            double mood = w.Happiness, gold = w.ResAmt[Res.Gold], food = w.ResAmt[Res.Food];
            Assert.IsTrue(w.CanHoldFestival().Ok);
            Assert.IsTrue(w.HoldFestival().Ok);
            Assert.AreEqual(gold - World.FestivalGold, w.ResAmt[Res.Gold], 1e-9);
            Assert.AreEqual(food - World.FestivalFood, w.ResAmt[Res.Food], 1e-9);
            Assert.AreEqual(mood + World.FestivalShift, w.Happiness, 1e-9);
            Assert.IsTrue(w.Moods.Any(m => m.Id == "festival"), "a lasting glow too");
        }

        [Test]
        public void FestivalsHaveACooldownAndNeedMoney()
        {
            var w = Town();
            Assert.IsTrue(w.HoldFestival().Ok);
            var again = w.CanHoldFestival();
            Assert.IsFalse(again.Ok);
            StringAssert.Contains("recover", again.Reason);
            Days(w, World.FestivalCooldown);
            Assert.IsTrue(w.CanHoldFestival().Ok, "ready again after the cooldown");
            w.ResAmt[Res.Gold] = 0;
            Assert.IsFalse(w.CanHoldFestival().Ok);
            StringAssert.Contains("gold", w.CanHoldFestival().Reason);
        }

        // ---------- uprisings ----------

        [Test]
        public void NoRiskWhenContentAndRisingRiskWhenNot()
        {
            var w = Town();
            w.HappinessShift(100, "x");
            Assert.AreEqual(0, w.UprisingRisk());
            w.HappinessShift(-100, "x");
            Assert.AreEqual(World.RiskAtZero, w.UprisingRisk(), 1e-9);
            w.HappinessShift(HappinessTuning.Unrest - 10, "x");
            Assert.That(w.UprisingRisk(), Is.GreaterThan(0).And.LessThan(World.RiskAtZero));
        }

        [Test]
        public void TheWorkersRiseAndMarchOnTheTownHall()
        {
            var w = Town(40); w.Soldiers = 0; w.Drafting = false;
            w.Recalc();
            int before = w.Peasants;
            var r = w.BeginUprising(8);
            Assert.IsTrue(r.IsUprising);
            Assert.AreEqual(8, r.Raiders.Count);
            Assert.IsTrue(r.Raiders.All(a => a.Rebel));
            Assert.IsTrue(r.Raiders.All(a => !string.IsNullOrEmpty(a.Trade)));
            Assert.AreEqual(before - 8, w.Peasants, "each rebel is a peasant fewer");
            Assert.AreEqual("within", r.From);
            Assert.IsTrue(w.Sheltering || w.Raid != null);
        }

        [Test]
        public void WithNoSoldiersTheRebelsDestroyTheHallAndYouLose()
        {
            var w = Town(40); w.Soldiers = 0; w.Drafting = false;
            w.Recalc();
            w.BeginUprising(10);
            for (int i = 0; i < 60 && !w.Defeated; i++) Days(w, 1);
            Assert.IsTrue(w.Defeated, "an undefended Hall falls");
            Assert.AreEqual(DefeatReason.HallDestroyed, w.Defeat);
        }

        [Test]
        public void SoldiersAtHomePutTheUprisingDown()
        {
            var w = Town(40); w.Soldiers = 24; w.Drafting = false;
            w.Recalc();
            var r = w.BeginUprising(8);
            for (int i = 0; i < 80 && !(r.Over && w.Raid == r && r.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead)); i++) Days(w, 1);
            Assert.IsFalse(w.Defeated, "the garrison held the Hall");
            Assert.IsTrue(r.Over);
            Assert.Greater(r.Killed, 0);
            Assert.Greater(w.HallHp, 0);
        }

        [Test]
        public void MiseryAlwaysEndsInAnUprisingButNotInTheFirstDays()
        {
            var w = Town(40); w.Soldiers = 0; w.Drafting = false;
            w.Day = 10;
            w.HappinessShift(-100, "x");
            for (int i = 0; i < 40 && w.Day < World.UprisingFirstDay - 1; i++)
            {
                w.HappinessShift(-100, "x"); w.ResAmt[Res.Food] = 4000; w.Tick();
                Assert.IsFalse(w.Raid != null && w.Raid.IsUprising, "not before day " + World.UprisingFirstDay + ", day " + w.Day);
            }
            var w2 = Town(40); w2.Soldiers = 0; w2.Drafting = false;
            bool rose = false;
            for (int i = 0; i < 60 && !rose; i++)
            {
                w2.HappinessShift(-100, "x"); w2.Tick(); w2.ResAmt[Res.Food] = 4000;
                rose = w2.Raid != null && w2.Raid.IsUprising;
            }
            Assert.IsTrue(rose, "a miserable town rises");
        }

        [Test]
        public void ThereIsACooldownBetweenUprisings()
        {
            var w = Town(60); w.Soldiers = 30; w.Drafting = false; w.Recalc();
            w.BeginUprising(6);
            int first = w.LastUprisingDay;
            Days(w, 20);
            Assert.AreEqual(first, w.LastUprisingDay);
        }

        [Test]
        public void RebelsWearTheirTradesMilitiaClothes()
        {
            Assert.AreEqual("farm", World.RebelTrade("farm"));
            Assert.AreEqual("mine", World.RebelTrade("mine"));
            Assert.IsNull(World.RebelTrade("market"));
        }

        // ---------- saving ----------

        [Test]
        public void HappinessMoodsAndFestivalAreSaved()
        {
            var w = Town(); w.Recalc();
            w.HappinessShift(-12, "x");
            w.AddMood("test", +7, 30, "A good omen");
            w.HoldFestival();
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(w.Happiness, back.Happiness, 0.01);
            Assert.AreEqual(w.LastFestivalDay, back.LastFestivalDay);
            Assert.IsTrue(back.Moods.Any(m => m.Id == "test" && m.Delta == 7));
            Assert.IsFalse(back.CanHoldFestival().Ok, "the cooldown carries over");
        }

        [Test]
        public void AnOldSaveStartsContent()
        {
            var o = JObject.Parse(new World(7).SaveJson());
            o.Remove("happiness");
            var w = World.LoadJson(o.ToString());
            Assert.AreEqual(HappinessTuning.Start, w.Happiness);
            Assert.AreEqual(0, w.Moods.Count);
        }
    }
}
