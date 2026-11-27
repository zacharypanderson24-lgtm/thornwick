// Mining and Quarrying districts (DESIGN_ORGANIC_DEVELOPMENT.md, phase 4). Same engine as phase 3 (DevGate, ReserveBlock, the player's own CanPlace, StartProject).
//
// What is kept: finite per-tile ore and stone (so any number of mines or quarries share one reserve and building one never creates any), the hidden underground
// ore revealed by tunnels, pits and mountain edges, the site surveys. What is new: the zone starts the activity, and a building built in (or beside) its own district
// is ORGANIC: a district quarry digs only its Quarrying district's rock; a district mine chooses its own tunnel (no separate extraction zone) from its reach, never under
// other land use and never through another district's ground.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int QuarryBuildDays = 5, MineBuildDays = 6, FirstQuarryMinDig = 6, FirstMineMinReserve = 60, StoneShortStock = 60, MineOreStockEnough = 150;

        /// <summary>Would a lodge, quarry or mine placed here belong to a district of its own kind (stands in it or touches it)? Then it follows the organic rules.</summary>
        public bool OrganicHere(string type, int x, int y)
        {
            DistrictType? want = type switch { "wood" => DistrictType.Forestry, "quarry" => DistrictType.Quarrying, "mine" => DistrictType.Mining, _ => null };
            if (want == null || !Inb(x, y)) return false;
            EnsureDistricts();
            var fp = new Building { T = type, X = x, Y = y, Sz = PlacementSize(type) };
            return OwnerDistrict(fp, _districtMap) != null;
        }

        /// <summary>Bring a lodge, quarry or mine from an older save under the district rules (sustainable felling, the district's rock, a self-chosen tunnel).
        /// It must stand in or beside a district of its own kind. Never automatic: changing how a working building behaves is the player's (or a governor's) decision.</summary>
        public Result AdoptLegacy(Building b)
        {
            if (b == null || (b.T != "wood" && b.T != "quarry" && b.T != "mine")) return Result.No("Only lodges, quarries and mines can be adopted");
            if (b.Organic) return Result.No("It already follows its district's rules");
            EnsureDistricts();
            if (OwnerDistrict(b, _districtMap) == null) return Result.No($"Zone {(b.T == "wood" ? "Forestry" : b.T == "quarry" ? "Quarrying" : "Mining")} around it first");
            b.Organic = true; Touch();
            return Result.Yes();
        }

        /// <summary>Turn the open rock tiles of a Mining district into a Quarrying zone (the old painted Mining boundary was also how rock was marked for quarries).
        /// Buildings and the zone under them stay. Quarries standing there become unbounded by the old boundary: adopt them to put them under the district rules.
        /// Returns the number of tiles converted.</summary>
        public int ConvertMiningRockToQuarrying(int districtId)
        {
            var d = DistrictById(districtId);
            if (d == null || d.Type != DistrictType.Mining) return 0;
            int n = 0;
            foreach (int k in d.Tiles.ToList())
            {
                int x = k % N, y = k / N;
                if (Grid[y, x] != null || DecoMap[y, x] != Deco.Rock || RockLeft(x, y) <= 0) continue;
                ZoneMap[y, x] = Zone.Quarry; n++;
            }
            if (n > 0) Touch();
            return n;
        }

        public bool StoneShort => ResAmt[Res.Stone] < StoneShortStock;
        public static double OreUsePerDay(int smitheries) => smitheries * SmeltRate * OrePerMetal;

        // ================= Quarrying =================
        void EvaluateQuarrying(District d)
        {
            var quarries = d.Buildings.Where(b => b.T == "quarry").ToList();
            if (quarries.Count == 0 ? Day0(d) % 3 != 0 : Day0(d) % 7 != 0) return;
            if (Starving) { d.Note = "Food comes first: no new quarry while the settlement is starving."; return; }
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            int workable = 0; double stone = 0;
            foreach (int k in d.Tiles) { int x = k % N, y = k / N; if (RockWorkable(x, y) && Claimed(x, y)) { workable++; stone += RockLeft(x, y); } }
            if (quarries.Count == 0)
            {
                if (workable < FirstQuarryMinDig) { d.Note = workable == 0 ? "No surface stone left to quarry in this district (stone does not grow back)." : $"Limited by little surface stone: {workable} workable rock tile{(workable == 1 ? "" : "s")}, a quarry needs {FirstQuarryMinDig}."; return; }
            }
            else
            {
                int vacant = quarries.Sum(b => Math.Max(0, b.Jobs - b.Workers));
                if (vacant > 0) { d.Note = $"Additional workers needed: the existing quarry has {vacant} unfilled position{(vacant == 1 ? "" : "s")}. No new quarry until they are staffed."; return; }
                if (!StoneShort) { d.Note = $"No new quarry needed: stone is stocked ({ResAmt[Res.Stone]:0})."; return; }
                if (stone < 30) { d.Note = $"Limited by little stone left: about {stone:0} in the district."; return; }
            }
            if (d.Limits.MaxEmployment > 0 && d.Jobs + Jobs["quarry"] > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("quarry", 2);
            int bx = -1, by = 0; List<int> bestClear = null; double best = double.MinValue; bool anyRoad = false;
            foreach (int key in NearDistrict(d, 2))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, 2)) continue;
                anyRoad = true;
                if (!PlanOpen(x, y, 2) || !SitePlaceable("quarry", x, y, 2, out var clear)) continue;
                var s = SurveyQuarrySite(x, y);
                if (s.Level == SiteLevel.Invalid || s.DigTiles < FirstQuarryMinDig) continue;
                if (s.OverlappingQuarries > 0 && s.DigTiles < 16 * (s.OverlappingQuarries + 1)) continue;   // other quarries already work that rock
                double score = s.Reserve - 40.0 * s.OverlappingQuarries + Noise.Hash2(x, y, 41) - ClearPenalty * clear.Count;
                if (score > best) { best = score; bx = x; by = y; bestClear = clear; }
            }
            if (bx < 0) { d.Note = anyRoad ? "No suitable quarry site: it needs free ground beside a connected road within reach of open rock that other quarries do not already work." : "Development blocked: no connected road reaches this district."; return; }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            var site = SurveyQuarrySite(bx, by);
            string reason = quarries.Count == 0 ? $"First quarry for this stone ({workable} workable rock tiles)." : $"Stone is short ({ResAmt[Res.Stone]:0}) and the rock can supply more.";
            var (p, why) = StartProject(d, "quarry", bx, by, 2, 1, cost, QuarryBuildDays, reason, $"About {site.DailyEstimate:0.#} stone a day; roughly {site.Reserve:0} stone in reach.", bestClear);
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A quarry is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed quarry costs {cost[Res.Wood]} wood." : "A quarry is being built here.";
        }

        // ================= Mining =================
        void EvaluateMining(District d)
        {
            var mines = d.Buildings.Where(b => b.T == "mine").ToList();
            if (mines.Count == 0 ? Day0(d) % 3 != 0 : Day0(d) % 7 != 0) return;
            if (Starving) { d.Note = "Food comes first: no new mine while the settlement is starving."; return; }
            var gate = DevGate(d); if (gate != null) { d.Note = gate; return; }
            int smitheries = Blds.Count(b => b.T == "smithery" && b.Served);
            if (mines.Count > 0)
            {
                int vacant = mines.Sum(b => Math.Max(0, b.Jobs - b.Workers));
                if (vacant > 0) { d.Note = $"No new mines required: the existing mine{(mines.Count == 1 ? " has" : "s have")} {vacant} unfilled worker position{(vacant == 1 ? "" : "s")}."; return; }
                double capacity = mines.Count * MineDailyEstimate(Jobs["mine"]), demand = OreUsePerDay(smitheries);
                if (demand <= 0) { d.Note = "No new mines required: no smithery needs more ore. Build a smithery and a district can open more mines."; return; }
                if (capacity >= demand || ResAmt[Res.Ore] >= MineOreStockEnough) { d.Note = $"No new mines required: the mines bring up about {capacity:0.#} ore a day against {demand:0.#} used, with {ResAmt[Res.Ore]:0} in store."; return; }
            }
            if (d.Limits.MaxEmployment > 0 && d.Jobs + Jobs["mine"] > d.Limits.MaxEmployment) { d.Note = $"Employment limit reached ({d.Limits.MaxEmployment} jobs)."; return; }
            var cost = CostFor("mine", 2);
            int bx = -1, by = 0; List<int> bestClear = null; double best = double.MinValue; bool anyRoad = false, anyFace = false;
            foreach (int key in NearDistrict(d, 2))
            {
                int x = key % N, y = key / N;
                if (!FootprintBesideRoad(x, y, 2)) continue;
                anyRoad = true;
                int face = MineFaceAt(x, y, 2);
                if (face < 0) continue;   // the entrance must turn towards a hill
                var (ex, ey) = EntranceTile(x + (face == 3 ? 1 : 0), y + (face == 0 ? 1 : 0), face);
                if (!Inb(ex, ey) || Grid[ey, ex]?.T != "road" || !D.Conn[Tk(ex, ey)]) continue;   // and a road must reach the entrance
                anyFace = true;
                if (!PlanOpen(x, y, 2) || !SitePlaceable("mine", x, y, 2, out var clear)) continue;
                var s = SurveyMineSite(x, y);
                if (s.Level == SiteLevel.Invalid || s.Reserve < FirstMineMinReserve) continue;
                double score = s.Reserve - 40.0 * s.OverlappingMines + Noise.Hash2(x, y, 43) - ClearPenalty * clear.Count;
                if (score > best) { best = score; bx = x; by = y; bestClear = clear; }
            }
            if (bx < 0)
            {
                d.Note = !anyRoad ? "Development blocked: no connected road reaches this district."
                    : !anyFace ? "No suitable mine entrance in the area: a mine needs a spot whose entrance faces a hill and has a connected road in front of it."
                    : $"No suitable mine site: no spot beside a road reaches at least {FirstMineMinReserve} ore (the real reserve is shared with mines already working it).";
                return;
            }
            if (ReserveBlock(d, cost) is string held) { d.Note = held; return; }
            var site = SurveyMineSite(bx, by);
            string reason = mines.Count == 0 ? $"First mine for this ground (about {site.Reserve:0} ore in reach)." : $"Ore is short ({ResAmt[Res.Ore]:0}) for {smitheries} smithery{(smitheries == 1 ? "" : "ies")} and the ground has more.";
            var (p, why) = StartProject(d, "mine", bx, by, 2, 1, cost, MineBuildDays, reason, $"About {site.DailyEstimate:0.#} ore a day for about {site.LifetimeYears:0.#} years (an estimate).", bestClear);
            d.Note = p == null ? why : p.Status == ProjectStatus.Proposed ? "A mine is proposed. Approve it in the development queue." : p.Missing.Length > 0 ? $"Development waiting: {p.Missing} The proposed mine costs {cost[Res.Wood]} wood." : "A mine is being built here.";
        }
    }
}
