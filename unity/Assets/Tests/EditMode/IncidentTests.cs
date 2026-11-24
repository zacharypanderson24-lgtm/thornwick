// Event scheduling and the caravan card (Sim/World.Incidents.cs). The big disasters are in ScenarioTests.
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class IncidentTests
    {
        static World Town(int day = 365)
        {
            var w = new World(7);
            w.Day = day;
            w.StartCampaign();
            w.ResAmt[Res.Food] = 200;
            return w;
        }

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 100); w.Tick(); } }

        [Test]
        public void NothingHappensInTheFirstYear()
        {
            var w = new World(7); w.StartCampaign();
            for (int i = 0; i < 359; i++)
            {
                Days(w, 1);
                Assert.IsNull(w.CurrentIncident, "day " + w.Day);
                Assert.IsNull(w.CurrentScenario, "day " + w.Day);
            }
        }

        [Test]
        public void EventsComeOnSeasonStartsFromTheSecondYear()
        {
            var w = new World(7); w.StartCampaign();
            int raised = 0; object seen = null;
            for (int i = 0; i < 360 * 3; i++)
            {
                Days(w, 1);
                object now = (object)w.CurrentScenario ?? w.CurrentIncident;
                if (now != null && now != seen)
                {
                    seen = now; raised++;
                    int day = now is Scenario s ? s.WarnedDay : ((Incident)now).RaisedDay;
                    Assert.GreaterOrEqual(day, 361, "not in the first year");
                    Assert.AreEqual(0, (day - 1) % World.SeasonLength, "on the first day of a season");
                }
            }
            Assert.GreaterOrEqual(raised, 2, "about three of every four seasons bring something, over two years");
        }

        [Test]
        public void WithoutTheCampaignNothingIsRaised()
        {
            var w = new World(7); w.Day = 361;
            for (int i = 0; i < 400; i++) { Days(w, 1); Assert.IsNull(w.CurrentIncident); Assert.IsNull(w.CurrentScenario); }
        }

        [Test]
        public void ACaravanLeftAloneMovesOn()
        {
            var w = Town(); w.ResAmt[Res.Gold] = 500;
            w.RaiseIncident("caravan");
            w.Events.Clear();
            Assert.IsNotNull(w.CurrentIncident);
            Days(w, World.IncidentWaitDays);
            Assert.IsNull(w.CurrentIncident);
            Assert.IsFalse(w.Events.Any(e => e.Contains("traders took") || e.Contains("You bought")), "the passive choice trades nothing: " + string.Join(" | ", w.Events));
            Assert.IsTrue(w.Events.Any(e => e.Contains("traders moved on")), string.Join(" | ", w.Events));
        }

        [Test]
        public void ACaravanBuysYourSurplusOrSellsYouFood()
        {
            var sell = Town(); sell.ResAmt[Res.Stone] = 100; sell.ResAmt[Res.Wood] = 20; sell.ResAmt[Res.Ore] = 5;
            sell.RaiseIncident("caravan");
            double gold = sell.ResAmt[Res.Gold];
            Assert.IsTrue(sell.ResolveIncident(0).Ok);
            Assert.AreEqual(50, 100 - sell.ResAmt[Res.Stone], 1e-9, "sold up to 50 of the biggest pile");
            Assert.AreEqual(gold + 100, sell.ResAmt[Res.Gold], 1e-9);

            var buy = Town(); buy.ResAmt[Res.Gold] = 100;
            buy.RaiseIncident("caravan");
            Assert.IsTrue(buy.ResolveIncident(1).Ok);
            Assert.AreEqual(70, buy.ResAmt[Res.Gold], 1e-9);
            Assert.AreEqual(230, buy.ResAmt[Res.Food], 1e-9);

            var nothing = Town(); nothing.ResAmt[Res.Stone] = 0; nothing.ResAmt[Res.Wood] = 0; nothing.ResAmt[Res.Ore] = 0;
            nothing.RaiseIncident("caravan");
            Assert.IsFalse(nothing.IncidentChoices()[0].Enabled, "nothing to sell");
        }

        [Test]
        public void YouCannotPickAChoiceYouCannotAfford()
        {
            var w = Town(); w.ResAmt[Res.Gold] = 0;
            w.RaiseIncident("caravan");
            Assert.IsFalse(w.IncidentChoices()[1].Enabled);
            Assert.IsFalse(w.ResolveIncident(1).Ok);
            Assert.IsNotNull(w.CurrentIncident, "the card stays until you answer");
        }

        [Test]
        public void ACardWaitingForAnAnswerIsSaved()
        {
            var w = Town();
            w.RaiseIncident("caravan");
            var back = World.LoadJson(w.SaveJson());
            Assert.IsNotNull(back.CurrentIncident);
            Assert.AreEqual("caravan", back.CurrentIncident.Id);
            Assert.AreEqual(w.CurrentIncident.RaisedDay, back.CurrentIncident.RaisedDay);
            Assert.AreEqual(3, back.IncidentChoices().Count);
            Assert.IsNull(World.LoadJson(Town().SaveJson()).CurrentIncident);
        }
    }
}
