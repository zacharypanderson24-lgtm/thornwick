// Reports for the district UI (DESIGN_ORGANIC_DEVELOPMENT.md, phase 8): compact demand indicators and a plain-words outlook for a district. Read-only: they compute from the
// real stocks, rates and land and change nothing, so the HUD, the Districts panel, the Economy menu and a governor all read the same numbers.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>How much the settlement wants more of something right now: None, Some, High, Critical.</summary>
    public enum DemandLevel : byte { None, Some, High, Critical }

    public sealed class DemandItem
    {
        public string Key, Label, Reason; public DemandLevel Level;
        public string Word => Level == DemandLevel.None ? "Balanced" : Level == DemandLevel.Some ? "Wanted" : Level == DemandLevel.High ? "High" : "Critical";
    }

    public partial class World
    {
        /// <summary>The five indicators: housing, food, timber, stone, minerals. Each says why in one sentence with real numbers.</summary>
        public List<DemandItem> DemandReport()
        {
            var list = new List<DemandItem>();
            ComputeDemand();
            // housing
            double occ = D.Civ > 0 ? (Peasants + Soldiers) / (double)D.Civ : 1, dem = Dem[Zone.Res];
            var housing = new DemandItem { Key = "housing", Label = "Housing" };
            if (Starving || ResAmt[Res.Food] < 10 * FoodScale) { housing.Level = DemandLevel.None; housing.Reason = "Homes wait: the settlement is short of food, so more homes would not help."; }
            else if (dem > 0.5) { housing.Level = DemandLevel.High; housing.Reason = $"Homes are {occ * 100:0}% full: villagers need more room."; }
            else if (dem > DemandThreshold) { housing.Level = DemandLevel.Some; housing.Reason = $"Homes are {occ * 100:0}% full: new homes will grow soon."; }
            else { housing.Level = DemandLevel.None; housing.Reason = $"Homes are {occ * 100:0}% full: there is room."; }
            list.Add(housing);
            // food
            var f = FoodPressure(); var food = new DemandItem { Key = "food", Label = "Food" };
            food.Level = f.days < 10 ? DemandLevel.Critical : f.days < FoodReserveDays ? DemandLevel.High : f.short_ ? DemandLevel.Some : DemandLevel.None;
            food.Reason = $"The settlement makes {f.make:0.#} food a day and eats {f.eat:0.#}; {f.days:0} days are in store.";
            list.Add(food);
            // timber
            double timber = ResAmt[Res.Wood] + ResAmt[Res.Logs]; var wood = new DemandItem { Key = "timber", Label = "Timber" };
            wood.Level = timber < 20 ? DemandLevel.Critical : timber < TimberShortLogs ? DemandLevel.High : timber < 2 * TimberShortLogs ? DemandLevel.Some : DemandLevel.None;
            wood.Reason = $"{ResAmt[Res.Wood]:0} planks and {ResAmt[Res.Logs]:0} logs in store" + (Rate[Res.Wood] < -0.05 ? $", planks falling by {-Rate[Res.Wood]:0.#} a day." : ".");
            list.Add(wood);
            // stone
            double stone = ResAmt[Res.Stone]; var st = new DemandItem { Key = "stone", Label = "Stone" };
            st.Level = stone < 15 ? DemandLevel.Critical : stone < StoneShortStock ? DemandLevel.High : stone < 2 * StoneShortStock ? DemandLevel.Some : DemandLevel.None;
            st.Reason = $"{stone:0} stone in store" + (Rate[Res.Stone] < -0.05 ? $", falling by {-Rate[Res.Stone]:0.#} a day." : ".");
            list.Add(st);
            // minerals
            int smitheries = Blds.Count(b => b.T == "smithery" && b.Served); var ore = new DemandItem { Key = "minerals", Label = "Minerals" };
            double use = OreUsePerDay(smitheries), cap = Blds.Where(b => b.T == "mine").Sum(b => MineDailyEstimate(Math.Min(b.Workers, b.Jobs)));
            if (smitheries == 0) { ore.Level = DemandLevel.None; ore.Reason = "No smithery needs ore yet."; }
            else { ore.Level = ResAmt[Res.Ore] < 10 && cap < use ? DemandLevel.High : cap < use && ResAmt[Res.Ore] < MineOreStockEnough ? DemandLevel.Some : DemandLevel.None; ore.Reason = $"{smitheries} smithery{(smitheries == 1 ? "" : "ies")} use up to {use:0.#} ore a day; the mines bring up about {cap:0.#}; {ResAmt[Res.Ore]:0} in store."; }
            list.Add(ore);
            return list;
        }

        readonly Dictionary<string, DemandLevel> _lastDemand = new Dictionary<string, DemandLevel>();

        /// <summary>Once a week: say so (one message) when a demand moves to or from High or Critical, with the reason. Runtime only, so a loaded game may repeat one message.</summary>
        void StepDemandNotes()
        {
            if (Day % 7 != 0) return;
            foreach (var item in DemandReport())
            {
                bool had = _lastDemand.TryGetValue(item.Key, out var prev);
                _lastDemand[item.Key] = item.Level;
                if (!had) continue;
                bool wasBig = prev >= DemandLevel.High, isBig = item.Level >= DemandLevel.High;
                if (wasBig == isBig) continue;
                Events.Add(isBig ? $"{item.Label} demand is now {item.Word.ToLowerInvariant()}. {item.Reason}" : $"{item.Label} demand has eased. {item.Reason}");
            }
        }

        /// <summary>What a district will do, in plain words, before or between the engine's own looks. Used right after zoning and when a district has nothing to report yet.</summary>
        public string DistrictOutlook(District d)
        {
            if (!d.Tiles.Any(k => BesideConnectedRoad(k % N, k / N) || NearConnectedRoad(k % N, k / N, 3)))
                return "No connected road reaches it yet: build a road from the Town Hall to this land and it will start developing.";
            switch (d.Type)
            {
                case DistrictType.Residential: return "Homes grow here, one a day, when houses are nearly full and food is stocked.";
                case DistrictType.Agricultural:
                    var f = ReadFarmland(d); return f.Fields < FirstFarmMinFields ? $"Only {f.Fields} open fertile field{(f.Fields == 1 ? "" : "s")}: it needs {FirstFarmMinFields} before a farmhouse is built." : $"{f.Fields} fertile fields: a farmhouse will be built beside the road ({f.Unserved} not yet worked).";
                case DistrictType.Forestry:
                    var r = ReadForestry(d); int need = SustainFloor + 6;
                    return r.Mature < need ? $"{r.Mature} mature trees: a lodge needs more than {need} to cut sustainably." : $"{r.Mature} mature trees: a woodcutter lodge will be built, cutting sustainably and replanting.";
                case DistrictType.Mining: return "A mine opens where ore lies under the ground, at a spot beside a road whose entrance faces a hill. No smithery is needed for the first one.";
                case DistrictType.Quarrying:
                    int rock = d.Tiles.Count(k => RockWorkable(k % N, k / N));
                    return rock < FirstQuarryMinDig ? $"{rock} workable rock tile{(rock == 1 ? "" : "s")}: a quarry needs {FirstQuarryMinDig}." : $"{rock} workable rock tiles: a quarry will be built beside the road.";
                case DistrictType.Hunting:
                    var h = ReadHunting(d);
                    return h.GameTiles < FirstHuntMinGame ? $"{h.GameTiles} woodland tile{(h.GameTiles == 1 ? "" : "s")} of game: a lodge needs {FirstHuntMinGame}." : $"{h.GameTiles} tiles of game: a hunting lodge will be built; the woods regrow {h.RegrowPerDay:0.#} food a day.";
                default: return "A fishing hut will be built where a road reaches the shore and the water holds fish.";
            }
        }

        bool NearConnectedRoad(int x, int y, int r)
        {
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++) if (Inb(i, j) && (D.Conn[Tk(i, j)] || Grid[j, i] == Hall)) return true;
            return false;
        }
    }
}
