// Agricultural and Forestry districts (DESIGN_ORGANIC_DEVELOPMENT.md, phase 3): the zone decides WHERE, the simulation decides HOW MANY.
// Both go through the shared engine of phase 2: DevGate and ReserveBlock for the player's settings, the same CanPlace the player's tool uses,
// StartProject for cost, reservation, construction and approval. Every refusal and every wait is written in plain words into District.Note.
//
// Rhythm (stateless, so a save/load gives the same days): a district with no productive building of its kind is looked at every day (initial development
// represents intent: the first farmhouse or lodge needs no crisis); an established district is looked at once a week, on the day (Day + Id) % 7 == 0.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int LodgeBuildDays = 4, FarmBuildDays = 5, LargeFarmBuildDays = 8, LodgeSpacing = 8, FirstFarmMinFields = 6, SiteSearchPad = 3, LargeFarmFields = 54;
        public const double FoodSelfSufficiency = 1.1, FoodReserveDays = 40;
        public const int TimberShortLogs = 80;   // planks + logs in store under this counts as short of timber

        /// <summary>One look at every Agricultural and Forestry district. Called once a day from Develop().</summary>
        public void DevelopDistricts()
        {
            foreach (var d in Districts.ToList())
            {
                if (d.Type == DistrictType.Forestry) EvaluateForestry(d);
                else if (d.Type == DistrictType.Agricultural && SeasonalFarming) EvaluateFarmland(d);
                else if (d.Type == DistrictType.Quarrying) EvaluateQuarrying(d);
                else if (d.Type == DistrictType.Mining) EvaluateMining(d);
            }
        }

        static Dictionary<Res, int> CostFor(string type, int sz) => new Dictionary<Res, int>(CostOf(type, sz));

        bool SitePlaceable(string type, int x, int y, int sz)
        {
            if (sz == PlacementSize(type))
            {
                var c = CanPlace(type, x, y);
                return c.Ok || c.Reason.StartsWith("Not enough", StringComparison.Ordinal);   // the cost is the engine's business (reserve, waiting), not the site's
            }
            // a smaller footprint than the player's tool places (the 1 x 1 farmhouse): the same ground rules, checked tile by tile
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sz; i++)
                {
                    int tx = x + i, ty = y + j;
                    if (!SiteFree(tx, ty, 1) || PenAt(tx, ty) != null || WaterBlock(tx, ty, true) != null || CanalBlock(tx, ty) != null || MoatBlock(tx, ty) != null) return false;
                }
            return true;
        }

        /// <summary>Candidate anchor tiles: the district's tiles and the ground within SiteSearchPad of them, in ascending tile order (deterministic).</summary>
        IEnumerable<int> NearDistrict(District d, int sz)
        {
            for (int y = Math.Max(0, d.MinY - SiteSearchPad); y <= Math.Min(N - sz, d.MaxY + SiteSearchPad); y++)
                for (int x = Math.Max(0, d.MinX - SiteSearchPad); x <= Math.Min(N - sz, d.MaxX + SiteSearchPad); x++)
                {
                    bool near = false;
                    for (int j = y - 1; j <= y + sz && !near; j++)
                        for (int i = x - 1; i <= x + sz && !near; i++)
                            near = Inb(i, j) && _districtMap[Tk(i, j)] == d.Id;
                    if (near) yield return Tk(x, y);
                }
        }

        bool FootprintBesideRoad(int x, int y, int sz)
        {
            for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) if (BesideConnectedRoad(x + i, y + j)) return true;
            return false;
        }

        bool PlanOpen(int x, int y, int sz) => !Projects.Any(p => p.Open && x < p.X + p.Sz && x + sz > p.X && y < p.Y + p.Sz && y + sz > p.Y);

        int Day0(District d) => Day + d.Id;

        // ================= Forestry =================
        public struct ForestryReading { public int Mature, Saplings, Protected; }

        public ForestryReading ReadForestry(District d)
        {
            var r = new ForestryReading();
            foreach (int k in d.Tiles)
            {
                int x = k % N, y = k / N;
                if (DecoMap[y, x] == Deco.Tree && Claimed(x, y)) { if (ProtectedWood.Contains(k)) r.Protected++; else if (!ClearOrders.Contains(k)) r.Mature++; }
                else if (DecoMap[y, x] == Deco.Sapling) r.Saplings++;
            }
            return r;
        }

        public bool TimberShort => ResAmt[Res.Wood] + ResAmt[Res.Logs] < TimberShortLogs;

        void EvaluateForestry(District d)
        {
            var lodges = d.Buildings.Where(b => b.T == "wood").ToList();
            if (lodges.Count > 0 && Day0(d) % 7 != 0) return;
            if (Starving) { d.Note = "Food comes first: no new lodge while the settlement is starving."; return; }
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            var f = ReadForestry(d);
            int need = SustainFloor + 6;
            if (lodges.Count == 0)
            {
                if (f.Mature < need)
                {
                    d.Note = $"Limited by insufficient mature trees: {f.Mature} in this district{(f.Saplings > 0 ? $" ({f.Saplings} still growing)" : "")}; a lodge needs more than {need} to cut sustainably (it leaves {SustainFloor} standing)" + (f.Protected > 0 ? $", and {f.Protected} protected tree{(f.Protected == 1 ? " is" : "s are")} will not be cut." : ".");
                    return;
                }
            }
            else
            {
                var vacant = lodges.Sum(b => Math.Max(0, b.Jobs - b.Workers));
                if (vacant > 0) { d.Note = $"Additional workers needed: the existing lodge{(lodges.Count == 1 ? " has" : "s have")} {vacant} unfilled position{(vacant == 1 ? "" : "s")}. No new lodge until they are staffed."; return; }
                if (!TimberShort) { d.Note = $"No new lodge needed: timber is stocked ({ResAmt[Res.Wood]:0} planks, {ResAmt[Res.Logs]:0} logs)."; return; }
                double cutting = lodges.Count * LodgeDailyEstimate(Jobs["wood"]) , sustainable = SustainableLogsPerDay(f.Mature + f.Saplings);
                if (cutting >= sustainable * 1.15) { d.Note = $"Limited by insufficient mature trees: the forest regrows about {sustainable:0.#} logs a day and the existing lodges can already cut {cutting:0.#}."; return; }
                if (f.Mature < need + 12 * lodges.Count) { d.Note = $"Limited by insufficient mature trees: {f.Mature} for {lodges.Count + 1} lodges."; return; }
            }
            if (d.Limits.MaxEmployment > 0 && d.Jobs + Jobs["wood"] > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("wood", 2);
            int bx = -1, by = 0; double bestScore = double.MinValue; bool anyRoad = false;
            foreach (int key in NearDistrict(d, 2))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, 2)) continue;
                anyRoad = true;
                if (!PlanOpen(x, y, 2) || !SitePlaceable("wood", x, y, 2)) continue;
                if (lodges.Any(b => Math.Max(Math.Abs(b.X - x), Math.Abs(b.Y - y)) < LodgeSpacing)) continue;
                int trees = 0;
                for (int j = y - 6; j <= y + 7; j++) for (int i = x - 6; i <= x + 7; i++) if (Inb(i, j) && DecoMap[j, i] == Deco.Tree && ZoneMap[j, i] == Zone.Wood && !ProtectedWood.Contains(Tk(i, j))) trees++;
                double score = trees + Noise.Hash2(x, y, 31) * 0.5;
                if (score > bestScore) { bestScore = score; bx = x; by = y; }
            }
            if (bx < 0) { d.Note = anyRoad ? "No suitable lodge site: it needs a free 2 x 2 patch of ground beside a connected road, away from other lodges (clear a few trees with Clear Land)." : "Development blocked: no connected road reaches this district."; return; }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            string reason = lodges.Count == 0 ? $"First lodge for this forest ({f.Mature} mature trees)." : $"Timber is short ({ResAmt[Res.Wood]:0} planks, {ResAmt[Res.Logs]:0} logs) and the forest can supply more.";
            var (p, why) = StartProject(d, "wood", bx, by, 2, 1, cost, LodgeBuildDays, reason, $"About {LodgeDailyEstimate(Jobs["wood"]):0.#} logs a day while it works.");
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A lodge is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed woodcutter lodge costs {cost[Res.Wood]} wood." : "A lodge is being built here.";
        }

        // ================= Agriculture =================
        public struct FarmReading { public int Fields, Unserved; }

        public FarmReading ReadFarmland(District d)
        {
            var served = FarmFields(); var r = new FarmReading();
            foreach (int k in d.Tiles)
                if (IsFarmField(k % N, k / N)) { r.Fields++; if (!served.ContainsKey(k)) r.Unserved++; }
            return r;
        }

        int UnservedFieldsReach(int x, int y, int sz, HashSet<int> skip, Dictionary<int, Building> served)
        {
            int n = 0;
            for (int j = y - FarmReach; j <= y + sz - 1 + FarmReach; j++)
                for (int i = x - FarmReach; i <= x + sz - 1 + FarmReach; i++)
                    if (IsFarmField(i, j) && !served.ContainsKey(Tk(i, j)) && !skip.Contains(Tk(i, j))) n++;
            return n;
        }

        void EvaluateFarmland(District d)
        {
            var farms = d.Buildings.Where(b => b.T == "farm").ToList();
            if (farms.Count > 0 && Day0(d) % 7 != 0) return;
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            var r = ReadFarmland(d);
            double eat = DailyFoodDemand, make = FoodIncomePerDay(), days = eat > 0 ? EdibleFoodStored / eat : 999;
            if (farms.Count == 0)
            {
                if (r.Unserved < FirstFarmMinFields) { d.Note = $"Not enough open farmland yet for a farmhouse: {r.Unserved} fertile open tiles, it needs {FirstFarmMinFields}."; return; }
            }
            else
            {
                var vacant = farms.Sum(b => Math.Max(0, b.Jobs - b.Workers));
                if (vacant > 0) { d.Note = $"Additional workers needed: the existing farmhouse{(farms.Count == 1 ? " has" : "s have")} {vacant} unfilled position{(vacant == 1 ? "" : "s")}. No new farmhouse until they are staffed."; return; }
                if (r.Unserved < FieldsPerFarmer) { d.Note = $"No new farmhouse needed: the farms already work the farmland ({r.Unserved} fields left over, a farmer works {FieldsPerFarmer})."; return; }
                if (make >= eat * FoodSelfSufficiency && days >= FoodReserveDays) { d.Note = $"No new farmhouse needed: the farms make {make:0.#} food a day against {eat:0.#} eaten, with {days:0} days in store."; return; }
            }
            int sz = r.Unserved >= LargeFarmFields ? 2 : 1;
            var probe = new Building { T = "farm", Sz = sz };
            if (d.Limits.MaxEmployment > 0 && d.Jobs + probe.Jobs > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("farm", sz);
            var served = FarmFields(); bool anyRoad = false; int bx = -1, by = 0; double best = double.MinValue;
            foreach (int key in NearDistrict(d, sz))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, sz)) continue;
                anyRoad = true;
                if (!PlanOpen(x, y, sz) || !SitePlaceable("farm", x, y, sz)) continue;
                var eaten = new HashSet<int>(); int eatenFields = 0;
                for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) { eaten.Add(Tk(x + i, y + j)); if (IsFarmField(x + i, y + j)) eatenFields++; }
                int reach = Math.Min(UnservedFieldsReach(x, y, sz, eaten, served), probe.Jobs * FieldsPerFarmer);
                double score = reach - 4.0 * eatenFields + Noise.Hash2(x, y, 37) * 0.5;
                if (reach >= Math.Min(FieldsPerFarmer, r.Unserved) && score > best) { best = score; bx = x; by = y; }
            }
            if (bx < 0) { d.Note = anyRoad ? "No suitable farmhouse site: it needs free ground beside a connected road with open fields in reach, without covering the fields." : "Development blocked: no connected road reaches this farmland."; return; }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            string reason = farms.Count == 0 ? $"First farmhouse for this farmland ({r.Unserved} open fields)." : $"Food is short: farms make {make:0.#} a day against {eat:0.#} eaten, {days:0} days in store, and {r.Unserved} fields are not worked.";
            var (p, why) = StartProject(d, "farm", bx, by, sz, 1, cost, sz == 2 ? LargeFarmBuildDays : FarmBuildDays, reason, $"Works up to {probe.Jobs * FieldsPerFarmer} fields.");
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A farmhouse is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed farmhouse costs {cost[Res.Wood]} wood." : "A farmhouse is being built here.";
        }
    }
}
