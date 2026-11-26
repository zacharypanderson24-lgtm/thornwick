// PLACEMENT FORECAST (Zach's rule: it should be easy to see how placing something will affect everything). Before you click, the tip beside the
// mouse says what the building would produce right where the cursor is, in the same units the town eats in (one villager eats 1 food a day, so
// "4.3 food a day" means "feeds about 4 villagers"), and what that does to the town's food balance. It follows the cursor, so moving
// around shows which spots are good. Nothing here changes the world: a building that is not placed yet is a plain Building object that is
// never added to the grid, and the crew pathfinding runs on it exactly as it would run on the real one.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public sealed class Forecast
    {
        public readonly List<string> Lines = new List<string>();
        /// <summary>Food this building would add per day with a full crew (0 when it makes no food).</summary>
        public double FoodPerDay;
        /// <summary>The forecast is bad news (nothing to work, no road...): the tip turns amber.</summary>
        public bool Warn;
        public string Text => string.Join("\n", Lines);
    }

    public partial class World
    {
        static string Villagers(double food) => food < 0.5 ? "less than one villager" : Math.Round(food) == 1 ? "1 villager" : $"{Math.Round(food):0} villagers";

        /// <summary>What this building would make if placed here now. Types that make nothing measurable return no lines.</summary>
        public Forecast ForecastPlacement(string type, int x, int y)
        {
            var f = new Forecast();
            int sz = PlacementSize(type);
            var b = new Building { T = type, X = x, Y = y, Sz = sz, Face = 0 };
            switch (type)
            {
                case "hunter": ForecastHunter(f, b); break;
                case "farm": ForecastFarm(f, b); break;
                case "wood": ForecastCrew(f, b, Deco.Tree, "logs", "woodcutters", "Trees in reach", Defs.Load["wood"]); ForecastLodgeForest(f, x, y, sz); break;
                case "quarry": ForecastCrew(f, b, Deco.Rock, "stone", "quarrymen", "Workable rock in its area", Defs.Load["quarry"]); break;
                case "mine": ForecastMine(f, x, y, sz); break;
                case "silo": ForecastSilo(f); break;
            }
            if (f.FoodPerDay > 0) AddFoodBalance(f);
            return f;
        }

        /// <summary>The idle villagers a new building could hire right now (it needs a road and workers).</summary>
        string WorkersLine(Building b, int jobs)
        {
            int idle = D.Idle;
            return idle >= jobs ? $"{jobs} jobs; {idle} villagers are idle and could fill them" : idle > 0 ? $"{jobs} jobs; only {idle} villager{(idle == 1 ? " is" : "s are")} idle to fill them" : $"{jobs} jobs; no one is idle to fill them yet (grow the town or free workers)";
        }

        // ---------- hunting ----------

        /// <summary>Food a day for `workers` hunters working from this lodge (real or planned): each walks to the nearest game, spends WorkT days,
        /// carries HuntingLoad home, and starts again the next day: a trip is 2 x steps + WorkT + 1 days.</summary>
        double HuntRate(Building b, int workers, List<int> stepsOut = null)
        {
            var used = new List<int>(); var path = new List<int>();
            double perDay = 0;
            for (int i = 0; i < workers; i++)
            {
                if (!CrewTarget(b, Deco.Tree, i * 7 + Day, used, path, out _, out _)) break;
                int steps = path.Count - 1;
                used.Add(path[path.Count - 1]);
                stepsOut?.Add(steps);
                perDay += HuntingLoad / (double)(2 * steps + WorkT + 1);
            }
            return perDay;
        }

        void ForecastHunter(Forecast f, Building b)
        {
            int jobs = b.Jobs;
            var steps = new List<int>();
            double perDay = HuntRate(b, jobs, steps);
            // all the game in reach, reachable or not: what it holds now and what grows back each day
            int tiles = 0; double stock = 0;
            for (int j = b.Y - HuntingRange; j <= b.Y + b.Sz - 1 + HuntingRange; j++)
                for (int i = b.X - HuntingRange; i <= b.X + b.Sz - 1 + HuntingRange; i++)
                    if (Inb(i, j) && Claimed(i, j) && DecoMap[j, i] == Deco.Tree) { tiles++; stock += HuntFoodRemaining(i, j); }
            double regrow = tiles / (double)HuntingRecoveryDays;
            if (steps.Count == 0)
            {
                f.Lines.Add("Hunting lodge: no game in reach here. Hunters need trees on your land within " + HuntingRange + " tiles.");
                f.Warn = true;
                return;
            }
            f.FoodPerDay = perDay;
            f.Lines.Add($"Hunting lodge here: about {perDay:0.#} food a day with {steps.Count} hunters = feeds {Villagers(perDay)}");
            f.Lines.Add($"Nearest game is {string.Join(" and ", steps.Select(s => s + (s == 1 ? " step" : " steps")))} away: each trip is {2 * steps.Min() + WorkT + 1} days for {HuntingLoad} food");
            if (steps.Count < jobs) f.Lines.Add($"Only {steps.Count} of its {jobs} hunters would find game");
            if (perDay > regrow * 1.0 && regrow > 0)
            {
                f.Lines.Add($"Game: {stock:0} food now, regrowing {regrow:0.#} a day. Hunting {perDay:0.#} a day uses it up in about {stock / Math.Max(0.01, perDay - regrow):0} days");
                f.Warn = true;
            }
            else f.Lines.Add($"Game: {stock:0} food in {tiles} woodland tiles, regrowing {regrow:0.#} a day: this lodge can hunt for ever");
            f.Lines.Add(WorkersLine(b, jobs));
        }

        // ---------- farming ----------

        void ForecastFarm(Forecast f, Building b)
        {
            if (!SeasonalFarming) return;
            int wheat = 0, flax = 0;
            for (int y = b.Y - FarmReach; y <= b.Y + b.Sz - 1 + FarmReach; y++)
                for (int x = b.X - FarmReach; x <= b.X + b.Sz - 1 + FarmReach; x++)
                {
                    if (!IsFarmField(x, y)) continue;
                    var owner = FarmFields().TryGetValue(Tk(x, y), out var o) ? o : null;
                    if (owner != null && RectDist(owner, x, y) <= RectDist(b, x, y)) continue;   // a nearer farm already works it
                    if (IsFlaxField(x, y)) flax++; else wheat++;
                }
            int jobs = b.Jobs, canTend = jobs * FieldsPerFarmer, fields = wheat + flax;
            if (fields == 0)
            {
                f.Lines.Add($"Farmhouse here: no free Farmland within {FarmReach} tiles. Zone Farmland (3) near it first: it works only the fields in reach.");
                f.Warn = true;
                return;
            }
            int tended = Math.Min(fields, canTend);
            double wheatTended = fields == 0 ? 0 : wheat * Math.Min(1.0, tended / (double)fields);
            double perDay = wheatTended * WheatYield / YearLength;
            f.FoodPerDay = perDay;
            f.Lines.Add($"Farmhouse here: {wheat} wheat" + (flax > 0 ? $" and {flax} flax" : "") + $" fields in reach (it can tend {canTend}): about {perDay:0.#} food a day = feeds {Villagers(perDay)}");
            f.Lines.Add($"Each wheat field gives {WheatYield} food a year at harvest (day 100 to 180); a farmer tends {FieldsPerFarmer} fields = feeds {Villagers(FieldsPerFarmer * WheatYield / (double)YearLength)}");
            f.Lines.Add($"It needs {(tended + FieldsPerFarmer - 1) / FieldsPerFarmer} farmers" + (fields > canTend ? $"; {fields - canTend} more fields would lie fallow (build another farmhouse)" : ""));
            f.Lines.Add(WorkersLine(b, Math.Min(jobs, (tended + FieldsPerFarmer - 1) / FieldsPerFarmer)));
        }

        static int RectDist(Building f, int x, int y)
        {
            int dx = Math.Max(0, Math.Max(f.X - x, x - (f.X + f.Sz - 1))), dy = Math.Max(0, Math.Max(f.Y - y, y - (f.Y + f.Sz - 1)));
            return Math.Max(dx, dy);
        }

        // ---------- lodges and quarries ----------

        void ForecastCrew(Forecast f, Building b, Deco kind, string goods, string who, string what, int load)
        {
            int jobs = b.Jobs;
            var used = new List<int>(); var path = new List<int>();
            var steps = new List<int>();
            double perDay = 0;
            for (int i = 0; i < jobs; i++)
            {
                if (!CrewTarget(b, kind, i * 7 + Day, used, path, out _, out _)) break;
                int s = path.Count - 1; used.Add(path[path.Count - 1]); steps.Add(s);
                perDay += load / (double)(2 * s + WorkT + 1);
            }
            string name = Names.GetValueOrDefault(b.T, b.T);
            if (steps.Count == 0)
            {
                f.Lines.Add($"{name} here: {what.ToLower()}: none. {(kind == Deco.Tree ? $"Woodcutters need forest on your land within {WorkR} tiles" : $"A quarry works rock within {QuarryAutoR} tiles of it (a painted Mining boundary widens that to {WorkR})")}.");
                f.Warn = true;
                return;
            }
            f.Lines.Add($"{name} here: about {perDay:0.#} {goods} a day with {steps.Count} {who}");
            f.Lines.Add($"Work is {string.Join(" and ", steps.Select(s => s + (s == 1 ? " step" : " steps")))} away: each trip is {2 * steps.Min() + WorkT + 1} days for {load} {goods}");
            if (steps.Count < jobs) f.Lines.Add($"Only {steps.Count} of its {jobs} {who} would find work");
            f.Lines.Add(WorkersLine(b, jobs));
        }

        void ForecastLodgeForest(Forecast f, int x, int y, int sz)
        {
            var s = SurveyLodgeSite(x, y, sz);
            f.Lines.Add($"Forest in reach: {s.Mature} mature trees ({s.StockLogs:0} logs), {s.Saplings} saplings. Sustainable output about {s.SustainablePerDay:0.#} logs a day for ever; the first years cut faster");
            foreach (var n in s.Notes) f.Lines.Add(n);
            if (s.Level == SiteLevel.Warning) f.Warn = true;
        }

        void ForecastMine(Forecast f, int x, int y, int sz)
        {
            var s = SurveyMineSite(x, y, sz);
            if (s.Level == SiteLevel.Invalid) { f.Lines.Add("Mine here: " + s.Notes[0]); f.Warn = true; return; }
            f.Lines.Add($"Iron Mine here: about {s.Reserve:0} ore in reach (an estimate), richest band {s.RichestBand:0}");
            f.Lines.Add($"With {Defs.Jobs["mine"]} miners about {s.DailyEstimate:0.#} ore a day (not guaranteed: the tunnel has to be dug first), so roughly {s.LifetimeYears:0.#} years of ore");
            foreach (var n in s.Notes) f.Lines.Add(n);
            if (s.Level == SiteLevel.Warning) f.Warn = true;
            f.Lines.Add(WorkersLine(new Building { T = "mine", X = x, Y = y, Sz = sz }, Defs.Jobs["mine"]));
        }

        // ---------- silos ----------

        void ForecastSilo(Forecast f)
        {
            if (!SeasonalFarming) return;
            double days = DailyFoodDemand > 0 ? SiloFood / DailyFoodDemand : 0;
            f.Lines.Add($"Silo: adds {SiloFood:0} food storage ({CropStorage(Res.Food):0} now, {CropStorage(Res.Food) + SiloFood:0} with it). It makes no food, it keeps what the farms bring in.");
            if (DailyFoodDemand > 0) f.Lines.Add($"That is {days:0} days of food at today's {DailyFoodDemand:0.#} a day");
        }

        // ---------- the town's food balance ----------

        /// <summary>Food the town makes on an average day today (farms spread over the year, plus what its hunters bring in).</summary>
        public double FoodIncomePerDay()
        {
            double hunting = 0;
            foreach (var h in Blds.Where(h => h.T == "hunter" && h.Served)) hunting += HuntRate(h, Math.Min(h.Workers, h.Jobs));
            return FarmFood() + hunting;
        }

        void AddFoodBalance(Forecast f)
        {
            double eat = DailyFoodDemand, have = FoodIncomePerDay(), after = have + f.FoodPerDay;
            string now = $"Town food: eats {eat:0.#} a day ({Peasants + Soldiers} villagers), makes {have:0.#} now";
            string then = after - eat >= 0 ? $"{after:0.#} with this: {after - eat:0.#} spare, room for {Villagers(after - eat)} more" : $"{after:0.#} with this: still {eat - after:0.#} short";
            f.Lines.Add($"{now}; {then}");
            if (after < eat) f.Warn = true;
        }
    }
}
