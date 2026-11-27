// Hunting and Fishing districts (DESIGN_ORGANIC_DEVELOPMENT.md, phase 5). Same engine as the other districts (DevGate, ReserveBlock, the player's CanPlace, StartProject).
// Both help the early game's food before the first harvest, so unlike mines and quarries they are NOT held back while the settlement is starving.
// They never create game or fish: they read the real stocks (the forest game reserve per tile, the fish stock per water body) and stop adding lodges when the grounds
// cannot carry another. Docks, ports and boats stay manual.
//
// Known limit: crews pick their game in World.Crews.cs (CrewMatch), which is not valid UTF-8 and is deliberately left alone, so a hunting lodge still hunts any
// claimed woodland within its reach, not only its district. The district decides where lodges are built and how many the grounds support; restricting the reach
// (and keeping hunters out of protected woodland) needs that file converted to UTF-8 first.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int HuntBuildDays = 3, FishBuildDays = 2, FirstHuntMinGame = 12, FirstFishMinGrounds = 3, HuntSpacing = 6, FishSpacing = 3;
        public const double FoodPlentyDays = 120, FishFirstMinStock = 0.4, FishMoreMinStock = 0.6, FishCapacityMargin = 1.6;

        /// <summary>What the town makes and eats a day, and how many days of edible food are stored.</summary>
        /// <summary>What the harvest really delivers compared with the nominal WheatYield per field (rain, frost, the sowing date): measured in the ten-year scenario run, which delivered about two thirds.</summary>
        public const double FarmRealism = 0.7;

        /// <summary>Food a day the farms can make over a whole year at full staffing, by the nominal yield times FarmRealism. Unlike FarmFood it does NOT drop to zero in winter when
        /// the farmers' jobs end, so an idle winter farm never looks like a shortage (and never makes the engine build another).</summary>
        public double FarmFoodCapacity()
        {
            if (!SeasonalFarming) return FarmFood();
            double food = 0; var fields = FarmFields();
            foreach (var farm in Blds.Where(b => b.T == "farm" && b.Served))
            {
                int mine = 0, wheat = 0;
                foreach (var f in fields) if (f.Value == farm) { mine++; if (!IsFlaxField(f.Key % N, f.Key / N)) wheat++; }
                double tended = Math.Min(1.0, farm.Jobs * FieldsPerFarmer / (double)Math.Max(1, mine));
                food += wheat * tended * WheatYield * FarmRealism * CharterMult("farm") / YearLength;
            }
            return food;
        }

        /// <summary>The year-round food the settlement makes: farm capacity, hunters at their real rate and fishing huts and docks at today's catch.</summary>
        public double FoodCapacityPerDay()
        {
            double hunting = 0, fishing = 0;
            foreach (var h in Blds.Where(h => h.T == "hunter" && h.Served)) hunting += HuntRate(h, Math.Min(h.Workers, h.Jobs));
            foreach (var f in Blds.Where(f => (f.T == "fisher" || f.T == "dock") && f.Served)) fishing += FishCatch(f);
            return FarmFoodCapacity() + hunting + fishing;
        }

        (int day, int version, double stock, (double, double, double, bool) value) _foodPressure = (-1, -1, 0, default);

        /// <summary>Will the store plus what hunters and fishers bring in feed everyone, with `extra` more mouths, until the next harvest is in? Seasonal farming only: the
        /// farms give nothing between harvests, so this is the check that stops a village growing in spring into a famine the following winter.</summary>
        public bool FoodCoversUntilHarvest(int extra = 0)
        {
            if (!SeasonalFarming) return true;
            double eat = (DailyFoodDemand + extra * FoodPerPerson) * 1.15;   // a margin of 15%: the store must reach the harvest with something to spare
            int horizon = YearDay <= 120 ? 121 - YearDay : YearLength - YearDay + 121;   // days until the next harvest has been brought in
            double nonFarm = FoodPressure().make - FarmFoodCapacity();
            return EdibleFoodStored + nonFarm * horizon >= eat * horizon;
        }

        public (double make, double eat, double days, bool short_) FoodPressure()
        {
            if (_foodPressure.day == Day && _foodPressure.version == Version && _foodPressure.stock == EdibleFoodStored) return _foodPressure.value;   // one answer a day: the hunters' route searches are not cheap
            double eat = DailyFoodDemand, make = FoodCapacityPerDay(), days = eat > 0 ? EdibleFoodStored / eat : 999;
            var result = (make, eat, days, days < FoodReserveDays || (make < eat * FoodSelfSufficiency && days < FoodPlentyDays));   // a big store is no emergency even when the year-average falls short
            _foodPressure = (Day, Version, EdibleFoodStored, result);
            return result;
        }

        public struct HuntingReading { public int GameTiles; public double Stock, RegrowPerDay; }

        public HuntingReading ReadHunting(District d)
        {
            var r = new HuntingReading();
            foreach (int k in d.Tiles)
            {
                int x = k % N, y = k / N;
                if (DecoMap[y, x] == Deco.Tree && Claimed(x, y)) { r.GameTiles++; r.Stock += HuntFoodRemaining(x, y); }
            }
            r.RegrowPerDay = r.GameTiles / (double)HuntingRecoveryDays;
            return r;
        }

        void EvaluateHunting(District d)
        {
            var lodges = d.Buildings.Where(b => b.T == "hunter").ToList();
            if (lodges.Count > 0 && Day0(d) % 7 != 0) return;
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            var g = ReadHunting(d);
            var food = FoodPressure();
            if (lodges.Count == 0)
            {
                if (g.GameTiles < FirstHuntMinGame) { d.Note = $"Limited by little game: {g.GameTiles} woodland tile{(g.GameTiles == 1 ? "" : "s")} in this district, a lodge needs {FirstHuntMinGame}."; return; }
            }
            else
            {
                int vacant = lodges.Sum(b => Math.Max(0, ActiveJobs(b) - b.Workers));
                if (vacant > 0) { d.Note = $"Additional workers needed: the existing lodge{(lodges.Count == 1 ? " has" : "s have")} {vacant} unfilled hunter position{(vacant == 1 ? "" : "s")}."; return; }
                if (!food.short_) { d.Note = $"No new lodge needed: food is secure ({food.make:0.#} made against {food.eat:0.#} eaten, {food.days:0} days in store)."; return; }
                if (g.Stock < 0.5 * g.GameTiles * ForestFoodReserve) { d.Note = $"Game is thinning: {g.Stock:0} food of game left in {g.GameTiles} tiles. More lodges would empty the woods."; return; }
                double used = lodges.Sum(b => HuntRate(b, Math.Min(b.Workers, b.Jobs))), more = Jobs["hunter"] * HuntingLoad / (double)(2 * 4 + WorkT + 1);
                if (used + more > g.RegrowPerDay * 0.9) { d.Note = $"Hunting grounds at capacity: the game regrows {g.RegrowPerDay:0.#} food a day, the lodges already take about {used:0.#}."; return; }
            }
            if (d.Limits.MaxEmployment > 0 && d.Jobs + Jobs["hunter"] > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("hunter", 2);
            int bx = -1, by = 0; List<int> bestClear = null; double best = double.MinValue; bool anyRoad = false;
            foreach (int key in NearDistrict(d, 2))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, 2)) continue;
                anyRoad = true;
                if (!PlanOpen(x, y, 2) || !SitePlaceable("hunter", x, y, 2, out var clear)) continue;
                if (lodges.Any(b => Math.Max(Math.Abs(b.X - x), Math.Abs(b.Y - y)) < HuntSpacing)) continue;
                int game = 0;
                for (int j = y - 8; j <= y + 9; j++) for (int i = x - 8; i <= x + 9; i++) if (Inb(i, j) && DecoMap[j, i] == Deco.Tree && _districtMap[Tk(i, j)] == d.Id) game++;
                double score = game + Noise.Hash2(x, y, 47) * 0.5 - ClearPenalty * clear.Count;
                if (game > 0 && score > best) { best = score; bx = x; by = y; bestClear = clear; }
            }
            if (bx < 0) { d.Note = anyRoad ? "No suitable lodge site: it needs free 2 x 2 ground beside a connected road within reach of the district's woods, away from other lodges." : "Development blocked: no connected road reaches this district."; return; }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            double rate = Jobs["hunter"] * HuntingLoad / (double)(2 * 4 + WorkT + 1);
            string reason = lodges.Count == 0 ? $"First hunting lodge ({g.GameTiles} woodland tiles of game)." : $"Food is short ({food.make:0.#} made against {food.eat:0.#} eaten) and the game can carry another lodge.";
            var (p, why) = StartProject(d, "hunter", bx, by, 2, 1, cost, HuntBuildDays, reason, $"Up to about {rate:0.#} food a day while game lasts (it regrows {g.RegrowPerDay:0.#} a day here).", bestClear);
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A hunting lodge is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed hunting lodge costs {cost[Res.Wood]} wood." : "A hunting lodge is being built here.";
        }

        // ================= Fishing =================
        /// <summary>Fish a body of water can give each day without being fished down: regrowth at the half-stock equilibrium, where the fish breed back fastest.</summary>
        public double SustainableFishPerDay(int bodyId)
        {
            var body = Waters != null && bodyId > 0 && bodyId <= Waters.Bodies.Count ? Waters.Bodies[bodyId - 1] : null;
            return body == null ? 0 : body.Tiles * StockPerTile * (StockRegrow * 0.5 * 0.5 + StockTrickle);
        }

        void EvaluateFishing(District d)
        {
            var huts = d.Buildings.Where(b => b.T == "fisher").ToList();
            if (huts.Count > 0 && Day0(d) % 7 != 0) return;
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            if (Waters == null) { d.Note = "No open water on this map."; return; }
            var food = FoodPressure();
            if (huts.Count > 0)
            {
                int vacant = huts.Sum(b => Math.Max(0, ActiveJobs(b) - b.Workers));
                if (vacant > 0) { d.Note = $"Additional workers needed: the existing hut{(huts.Count == 1 ? " has" : "s have")} {vacant} unfilled position{(vacant == 1 ? "" : "s")}."; return; }
                if (!food.short_) { d.Note = $"No new fishing hut needed: food is secure ({food.make:0.#} made against {food.eat:0.#} eaten, {food.days:0} days in store)."; return; }
            }
            if (d.Limits.MaxEmployment > 0 && d.Jobs + Jobs["fisher"] > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("fisher", 1);
            int bx = -1, by = 0; List<int> bestClear = null; double best = double.MinValue; bool anyRoad = false, anyWater = false, anyStock = false, anyCapacity = false; string thin = null;
            var bodyLoad = new Dictionary<int, double>();   // fish already taken from each water, worked out once per look instead of once per candidate
            foreach (var fb in Blds) if ((fb.T == "fisher" || fb.T == "dock") && fb.Served) { var fg = FishingGrounds(fb); bodyLoad[fg.body] = (bodyLoad.TryGetValue(fg.body, out var cur) ? cur : 0) + fb.Workers * fg.perWorker; }
            foreach (int key in NearDistrict(d, 1))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, 1)) continue;
                anyRoad = true;
                if (!PlanOpen(x, y, 1) || !SitePlaceable("fisher", x, y, 1, out var clear)) continue;
                if (huts.Any(b => Math.Max(Math.Abs(b.X - x), Math.Abs(b.Y - y)) < FishSpacing)) continue;
                var probe = new Building { T = "fisher", X = x, Y = y, Sz = 1 };
                var g = FishingGrounds(probe);
                if (g.grounds < FirstFishMinGrounds) continue;
                anyWater = true;
                double stock = StockOf(g.body);
                if (stock < (huts.Count == 0 ? FishFirstMinStock : FishMoreMinStock)) { thin = $"{StockWord(stock)} ({stock:0.00} of the original stock)"; continue; }
                anyStock = true;
                double existing = (bodyLoad.TryGetValue(g.body, out var ld) ? ld : 0);
                double added = Jobs["fisher"] * g.perWorker;
                if (existing + added > SustainableFishPerDay(g.body) * FishCapacityMargin) continue;
                anyCapacity = true;
                double score = g.perWorker * stock * 100 + Noise.Hash2(x, y, 53) - ClearPenalty * clear.Count;
                if (score > best) { best = score; bx = x; by = y; bestClear = clear; }
            }
            if (bx < 0)
            {
                d.Note = !anyRoad ? "Development blocked: no connected road reaches this shore."
                    : !anyWater ? $"No suitable fishing site: a hut needs a free spot beside a road within {FishShoreNeeded} tiles of open water that a fisherman can walk to along at least {FirstFishMinGrounds} tiles."
                    : !anyStock ? $"Fish stock too low: the water is {thin}. New huts wait for it to recover."
                    : !anyCapacity ? "The water already carries all the fishing it can sustain: more huts would fish it out."
                    : "No suitable fishing site.";
                return;
            }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            var gg = FishingGrounds(new Building { T = "fisher", X = bx, Y = by, Sz = 1 });
            string reason = huts.Count == 0 ? $"First fishing hut ({gg.grounds} tiles of {gg.kind.ToString().ToLowerInvariant()}, stock {StockWord(StockOf(gg.body))})." : $"Food is short ({food.make:0.#} made against {food.eat:0.#} eaten) and the water can carry another hut.";
            var (p, why) = StartProject(d, "fisher", bx, by, 1, 1, cost, FishBuildDays, reason, $"About {Jobs["fisher"] * gg.perWorker:0.#} food a day in an average season.", bestClear);
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A fishing hut is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed fishing hut costs {cost[Res.Wood]} wood." : "A fishing hut is being built here.";
        }
    }
}
