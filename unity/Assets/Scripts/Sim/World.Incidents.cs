// Incidents and the schedule for events. From the second year, on the first day of a season (never during a raid), something happens:
// one season in four is quiet; otherwise a big SCENARIO is announced (World.Scenarios.cs) if none is running and the last one ended long
// enough ago, or a trader's CARAVAN stops by (a small card: sell your surplus, buy food, or send them on).
// A card left alone for IncidentWaitDays settles itself with its last choice (doing nothing). Like the campaign, events only run when
// CampaignOn is set (the game turns it on), so tests that tick worlds are unaffected.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public sealed class Incident
    {
        public string Id;
        public int RaisedDay;
    }

    public sealed class IncidentChoice
    {
        public string Label, Detail;
        public bool Enabled = true;
    }

    public partial class World
    {
        public const int IncidentWaitDays = 10, FirstIncidentYear = 2;
        public const int CaravanFoodGold = 30, CaravanFood = 30, CaravanSellMax = 50;

        /// <summary>The card waiting for an answer, or null.</summary>
        public Incident CurrentIncident { get; private set; }

        // ---------- scheduling ----------

        /// <summary>Called once a day at the end of the tick.</summary>
        void StepIncidents()
        {
            if (!CampaignOn || Defeated) return;
            StepScenarios();
            if (CurrentIncident != null)
            {
                if (Day - CurrentIncident.RaisedDay >= IncidentWaitDays)
                {
                    Events.Add("No one answered, so the traders moved on.");
                    ResolveIncident(IncidentChoices().Count - 1, forced: true);
                }
                return;
            }
            if (Year < FirstIncidentYear || (YearDay - 1) % SeasonLength != 0) return;
            if (Raid != null && !Raid.Over) return;
            double roll = Noise.Hash2(Day, Seed, 121);
            if (roll < 0.25) return;   // one season in four is quiet
            bool scenarioFree = CurrentScenario == null && Day - LastScenarioEnd >= ScenarioGapDays;
            if (roll < 0.60 && scenarioFree)
            {
                var options = new List<string>();
                string season = Season;
                if (season == "Spring" || season == "Summer") options.Add("famine");
                if (season == "Summer" || season == "Winter") options.Add("fire");
                if (season != "Summer") options.Add("host");
                RaiseScenario(options[(int)Math.Floor(Noise.Hash2(Day, Seed, 120) * options.Count)]);
                return;
            }
            if (CurrentScenario == null || CurrentScenario.Phase == ScenarioPhase.Warned) RaiseIncident("caravan");
        }

        /// <summary>Put this card on the table now (the scheduler uses it; tests and scenarios can too).</summary>
        public void RaiseIncident(string id)
        {
            CurrentIncident = new Incident { Id = id, RaisedDay = Day };
            Events.Add(IncidentTitle(id) + ": " + IncidentBody(CurrentIncident));
        }

        // ---------- the card ----------

        public static string IncidentTitle(string id) => id == "caravan" ? "A trader's caravan" : id;

        public string IncidentBody(Incident inc) => inc.Id == "caravan" ? "Traders have stopped outside the walls with goods to swap." : "";

        /// <summary>What the player can pick now, with what each costs. The last choice is always the passive one a timeout takes.</summary>
        public List<IncidentChoice> IncidentChoices()
        {
            var list = new List<IncidentChoice>();
            if (CurrentIncident == null) return list;
            var (sellRes, sellAmt, sellGold) = CaravanSale();
            list.Add(new IncidentChoice { Label = sellAmt > 0 ? $"Sell {sellAmt} {sellRes.ToString().ToLower()} (+{sellGold} gold)" : "Sell surplus", Detail = "They buy stone, wood or ore, whichever you have most of.", Enabled = sellAmt > 0 });
            list.Add(new IncidentChoice { Label = $"Buy food ({CaravanFoodGold} gold)", Detail = $"{CaravanFood} food for the stores.", Enabled = ResAmt[Res.Gold] >= CaravanFoodGold });
            list.Add(new IncidentChoice { Label = "Send them on", Detail = "Nothing changes." });
            return list;
        }

        /// <summary>What the caravan buys: the resource with the most spare stock (stone, wood, ore), up to CaravanSellMax, and what it pays.</summary>
        (Res res, int amount, int gold) CaravanSale()
        {
            Res best = Res.Stone; double most = 0;
            foreach (var r in new[] { Res.Stone, Res.Wood, Res.Ore })
                if (ResAmt[r] > most) { most = ResAmt[r]; best = r; }
            int amount = Math.Min(CaravanSellMax, (int)Math.Floor(most));
            if (amount < 10) return (best, 0, 0);
            return (best, amount, amount * (best == Res.Ore ? 3 : 2));
        }

        /// <summary>Answer the card. `forced` is the timeout settling it: a choice that cannot be afforded then does nothing.</summary>
        public Result ResolveIncident(int choice, bool forced = false)
        {
            if (CurrentIncident == null) return Result.No("Nothing needs deciding");
            var choices = IncidentChoices();
            if (choice < 0 || choice >= choices.Count) return Result.No("No such choice");
            if (!choices[choice].Enabled && !forced) return Result.No("You cannot afford that");
            CurrentIncident = null;
            if (choice == 0)
            {
                var (res, amount, gold) = CaravanSale();
                if (amount > 0) { ResAmt[res] -= amount; ResAmt[Res.Gold] += gold; Events.Add($"The traders took {amount} {res.ToString().ToLower()} and paid {gold} gold."); }
            }
            else if (choice == 1 && ResAmt[Res.Gold] >= CaravanFoodGold) { ResAmt[Res.Gold] -= CaravanFoodGold; RestoreFood(CaravanFood); Events.Add($"You bought {CaravanFood} food from the traders."); }
            else Events.Add("The traders move on.");
            Touch();
            return Result.Yes();
        }

        void RestoreFood(double amount)
        {
            if (amount <= 0) return;
            ResAmt[Res.Food] += amount; _wheat = WheatStored + amount;
        }

        /// <summary>A building lost to fire: gone, with nothing refunded (unlike demolishing it).</summary>
        void Burn(Building b)
        {
            foreach (var (i, j) in b.Tiles()) { Grid[j, i] = null; ZoneMap[j, i] = Zone.None; }
            Blds.Remove(b);
            Events.Add($"The {Names.GetValueOrDefault(b.T, b.T)} burned down.");
            Touch(); Recalc();
        }

        // ---------- saving ----------

        JObject SaveIncident() => CurrentIncident == null ? null : new JObject { ["id"] = CurrentIncident.Id, ["day"] = CurrentIncident.RaisedDay };

        void LoadIncident(JObject o)
        {
            CurrentIncident = o == null ? null : new Incident { Id = (string)o["id"], RaisedDay = (int)o["day"] };
        }
    }
}
