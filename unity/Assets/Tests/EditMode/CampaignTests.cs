// The campaign goals, losing the game (Town Hall destroyed, or no people left), and civilians sheltering in the Town Hall during a raid.
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class CampaignTests
    {
        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); } }

        // ---------- losing ----------

        [Test]
        public void ADestroyedTownHallEndsTheGame()
        {
            var w = new World(7);
            Assert.IsFalse(w.Defeated);
            Assert.AreEqual(World.HallMaxHp, w.HallHp);
            for (int i = 0; i < 30 && !w.Defeated; i++) w.HitHall(World.RaiderHallHit);
            Assert.IsTrue(w.Defeated);
            Assert.AreEqual(DefeatReason.HallDestroyed, w.Defeat);
            Assert.AreEqual(0, w.HallHp);
            StringAssert.Contains("Town Hall", w.DefeatText);
        }

        [Test]
        public void AHurtHallMendsBetweenRaids()
        {
            var w = new World(7);
            w.HitHall(40);
            Assert.AreEqual(60, w.HallHp, 1e-9);
            Days(w, 5);
            Assert.AreEqual(60 + 5 * World.HallMendPerDay, w.HallHp, 1e-9);
            Days(w, 30);
            Assert.AreEqual(World.HallMaxHp, w.HallHp, "it never mends past full");
        }

        [Test]
        public void TheLastVillagerLeavingEndsTheGame()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 400;
            w.Peasants = 0; w.Soldiers = 0;
            w.Tick();
            Assert.IsTrue(w.Defeated);
            Assert.AreEqual(DefeatReason.NoPeople, w.Defeat);
        }

        [Test]
        public void ATownWithPeopleIsNotDefeated()
        {
            var w = new World(7);
            Days(w, 40);
            Assert.IsFalse(w.Defeated);
        }

        [Test]
        public void DefeatAndHallHealthAreSaved()
        {
            var w = new World(7);
            w.HitHall(35);
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(65, back.HallHp, 0.01);
            Assert.IsFalse(back.Defeated);
            for (int i = 0; i < 30 && !w.Defeated; i++) w.HitHall(20);
            back = World.LoadJson(w.SaveJson());
            Assert.IsTrue(back.Defeated);
            Assert.AreEqual(DefeatReason.HallDestroyed, back.Defeat);
            Assert.AreEqual(w.DefeatDay, back.DefeatDay);
        }

        [Test]
        public void AnOldSaveWithoutTheNewFieldsLoadsHealthyAndWithoutACampaign()
        {
            var o = JObject.Parse(new World(7).SaveJson());
            foreach (var key in new[] { "hallHp", "defeat", "defeatDay", "campaign", "campaignOn" }) o.Remove(key);
            var w = World.LoadJson(o.ToString());
            Assert.AreEqual(World.HallMaxHp, w.HallHp);
            Assert.IsFalse(w.Defeated);
            Assert.IsFalse(w.CampaignOn);
        }

        // ---------- goals ----------

        [Test]
        public void TheCampaignIsOffUntilTheGameTurnsItOn()
        {
            var w = new World(7); w.Day = 400;
            Days(w, 3);
            Assert.AreEqual(0, w.MilestonesDone, "tests and tools tick worlds without rewards");
        }

        [Test]
        public void AGoalPaysItsRewardOnceAndMovesOn()
        {
            var plain = new World(7); plain.Day = 361;
            var play = new World(7); play.StartCampaign(); play.Day = 361;   // turn it on first: starting it late marks what is already beaten as done
            Assert.AreEqual("year", play.CurrentGoal.Id);
            Days(plain, 1); Days(play, 1);
            Assert.IsTrue(play.MilestoneDone("year"));
            Assert.AreEqual(plain.ResAmt[Res.Gold] + 40, play.ResAmt[Res.Gold], 1e-9, "40 gold, once");
            Assert.AreEqual("village", play.CurrentGoal.Id);
            Days(plain, 3); Days(play, 3);
            Assert.AreEqual(plain.ResAmt[Res.Gold] + 40, play.ResAmt[Res.Gold], 1e-9, "not paid again");
        }

        [Test]
        public void StartingTheCampaignOnATownCreditsWhatItAlreadyBeatsWithoutPaying()
        {
            var w = new World(7); w.Day = 500;
            double gold = w.ResAmt[Res.Gold];
            w.StartCampaign();
            Assert.IsTrue(w.MilestoneDone("year"));
            Assert.AreEqual(gold, w.ResAmt[Res.Gold], "no reward for what was already done");
        }

        [Test]
        public void GoalProgressReportsHaveAndNeed()
        {
            var w = new World(7); w.StartCampaign(); w.Day = 101;
            var (have, need) = w.GoalProgress();
            Assert.AreEqual(100, have);
            Assert.AreEqual(360, need);
        }

        [Test]
        public void CampaignProgressIsSaved()
        {
            var w = new World(7); w.Day = 361; w.StartCampaign();
            Days(w, 1);
            Assert.IsTrue(w.MilestoneDone("year"));
            var back = World.LoadJson(w.SaveJson());
            Assert.IsTrue(back.CampaignOn);
            Assert.IsTrue(back.MilestoneDone("year"));
            Assert.AreEqual(w.MilestonesDone, back.MilestonesDone);
        }

        [Test]
        public void EveryGoalHasATitleAHintAndARewardAndIdsAreUnique()
        {
            Assert.AreEqual(World.Milestones.Length, World.Milestones.Select(m => m.Id).Distinct().Count());
            foreach (var m in World.Milestones) { Assert.IsNotEmpty(m.Title); Assert.IsNotEmpty(m.Hint); Assert.Greater(m.Reward, 0); Assert.Greater(m.Need, 0); }
        }

        // ---------- sheltering ----------

        [Test]
        public void CiviliansRunIntoTheTownHallDuringARaidAndComeOutAfter()
        {
            var w = new World(7);
            Days(w, 12);   // the starting huts send their people out
            Assume.That(w.Folk.Count > 0, "townsfolk exist on this seed");
            Assert.IsFalse(w.Sheltering);
            w.BeginRaid(7);
            int most = 0; bool sawHiding = false;
            for (int i = 0; i < 120 && !(w.Raid.Over && !w.Defeated && w.Raid.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead)); i++)
            {
                Days(w, 1);
                if (w.Defeated) break;
                most = System.Math.Max(most, w.Sheltered);
                sawHiding |= w.Folk.Any(f => f.St == FolkState.Hide);
            }
            Assert.IsTrue(w.Raid.Over, "the raid ended");
            Assert.Greater(most, 0, "people hid in the Hall");
            Assert.IsTrue(sawHiding || most > 0);
            Days(w, 3);
            Assert.AreEqual(0, w.Sheltered, "everyone comes out when it is over");
            Assert.IsFalse(w.Folk.Any(f => f.St == FolkState.Hide || f.St == FolkState.Shelter));
        }

        [Test]
        public void NoOneShelterWhenThereIsNoRaid()
        {
            var w = new World(7);
            Days(w, 30);
            Assert.IsFalse(w.Sheltering);
            Assert.AreEqual(0, w.Sheltered);
        }
    }
}
