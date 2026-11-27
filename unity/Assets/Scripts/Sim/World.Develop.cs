using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    // Residential development, now the first user of the shared engine (DESIGN_ORGANIC_DEVELOPMENT.md phase 2): demand and the site choice are the original
    // rules, unchanged for a settlement that never touches district settings; the choice now goes through districts (priority, mode, limits, reserve) and the
    // result is a DevProject (World.Projects.cs). Housing projects take 0 build days, so a home still appears the day it is funded.
    public partial class World
    {
        public const double DemandThreshold = 0.05;
        public static readonly Zone[] ZoneKinds = { Zone.Res };
        public readonly Dictionary<Zone, double> Dem = new Dictionary<Zone, double> { [Zone.Res] = 0 };
        public readonly Dictionary<Zone, string> Note = new Dictionary<Zone, string> { [Zone.Res] = "", [Zone.Res2] = "" };

        /// <summary>True when homes are held back because the food does not add up (see ComputeDemand): the Districts panel and the demand report say so.</summary>
        public bool HomesWaitForFood { get; private set; }

        public void ComputeDemand()
        {
            double occupancy = D.Civ > 0 ? (Peasants + Soldiers) / (double)D.Civ : 1;
            double food = ResAmt[Res.Food];
            double demand = Starving || food < 10 * FoodScale ? -0.3 : Math.Max(-1, Math.Min(1, (occupancy - 0.7) * 4));
            if (!SeasonalFarming && Rate[Res.Food] < -0.3 && food < 120) demand = Math.Min(demand, 0);
            HomesWaitForFood = false;
            if (SeasonalFarming && demand > 0)   // food security: no new homes when the village already eats more than it makes and the store would not cover a year of the gap
            {
                var fp = FoodPressure();
                if ((fp.make < fp.eat * 1.1 && EdibleFoodStored < (fp.eat * 1.1 - fp.make) * YearLength) || !FoodCoversUntilHarvest(1)) { demand = Math.Min(demand, 0); HomesWaitForFood = true; }   // the year-round capacity must cover the village and the store must reach the next harvest
            }
            Dem[Zone.Res] = demand;
        }

        static double ChebPt(Building b, double x, double y) =>
            Math.Max(Math.Abs(b.X + b.Ctr - x), Math.Abs(b.Y + b.Ctr - y));

        public int CountZoned(int x, int y, int range, Deco kind, Zone zone)
        {
            int count = 0;
            for (int j = y - range; j <= y + range; j++)
                for (int i = x - range; i <= x + range; i++)
                    if (Inb(i, j) && DecoMap[j, i] == kind && ZoneMap[j, i] == zone &&
                        (kind != Deco.Rock || RockWorkable(i, j))) count++;
            return count;
        }

        /// <summary>A place a home would grow. Cost is its wood, Stone its stone (only Level 2 homes need stone), Tier the level it is built as.</summary>
        public class Pick { public int X, Y, Sz; public string Type; public int Cost, Stone, Tier = 1; public double Score; public District Dist; }

        /// <summary>The best empty tile beside a connected road in a housing zone of this kind (Level 1 or Level 2), however poor the town is.
        /// Searches the residential districts' tiles only (not the whole map); <paramref name="allow"/> skips districts that may not develop. A district with a
        /// higher development priority wins over a nearer site in a lower one; with equal priorities the nearest to the Town Hall wins (ties: lower tile).
        /// Null when there is none.</summary>
        public Pick HomeSite(Zone kind, Func<District, bool> allow = null)
        {
            if (kind != Zone.Res && kind != Zone.Res2) return null;
            bool level2 = kind == Zone.Res2;
            int cost = level2 ? House2Cost[Res.Wood] : Cost["house"][Res.Wood], stone = level2 ? House2Cost[Res.Stone] : 0;
            Pick best = null; int bestKey = 0;
            foreach (var d in Districts)
            {
                if (d.Type != DistrictType.Residential || (allow != null && !allow(d))) continue;
                foreach (int key in d.Tiles)
                {
                    int x = key % N, y = key / N;
                    if (ZoneMap[y, x] != kind || Grid[y, x] != null || IsSolid(x, y) || HasTree(x, y) || ProtectedWood.Contains(key)) continue;   // protected land is never built on
                    bool served = false;
                    for (int k = 0; k < 4 && !served; k++)
                    {
                        int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                        served = Inb(nx, ny) && (D.Conn[Tk(nx, ny)] || Grid[ny, nx] == Hall);
                    }
                    if (!served) continue;
                    double score = Math.Abs(x - Hall.X) + Math.Abs(y - Hall.Y) + Noise.Hash2(x, y, Day + 1) * 2.5;
                    bool better = best == null || d.Priority > best.Dist.Priority || (d.Priority == best.Dist.Priority && (score < best.Score || (score == best.Score && key < bestKey)));
                    if (better) { best = new Pick { X = x, Y = y, Sz = 1, Type = "house", Cost = cost, Stone = stone, Tier = level2 ? 2 : 1, Score = score, Dist = d }; bestKey = key; }
                }
            }
            return best;
        }

        public bool CanAffordHome(Pick p) => p != null && ResAmt[Res.Wood] >= p.Cost && ResAmt[Res.Stone] >= p.Stone;

        /// <summary>The Level 1 zone's home as the browser game had it: (the pick, 0) or, when the town cannot pay the wood, (null, wood needed).</summary>
        public (Pick pick, int wait) Choose(Zone kind)
        {
            var best = HomeSite(kind);
            return best != null && !CanAffordHome(best) ? (null, best.Cost) : (best, 0);
        }

        /// <summary>What a home is waiting for, e.g. "6 stone", "12 wood and 6 stone".</summary>
        string HomeWait(Pick p)
        {
            int wood = Math.Max(0, (int)Math.Ceiling(p.Cost - ResAmt[Res.Wood])), stone = Math.Max(0, (int)Math.Ceiling(p.Stone - ResAmt[Res.Stone]));
            return wood > 0 && stone > 0 ? $"{wood} wood and {stone} stone" : wood > 0 ? $"{wood} wood" : $"{stone} stone";
        }

        static Dictionary<Res, int> HomeCost(Pick p)
        {
            var c = new Dictionary<Res, int> { [Res.Wood] = p.Cost };
            if (p.Stone > 0) c[Res.Stone] = p.Stone;
            return c;
        }

        public void Develop()
        {
            StepProjects();
            DevelopDistricts();   // World.DevEconomy.cs: farmland and forestry districts
            StepDemandNotes();   // World.DevReports.cs: one message when a demand becomes high or eases
            ComputeDemand();
            foreach (var d in Districts) if (d.Type == DistrictType.Residential) d.Note = "";
            if (Dem[Zone.Res] <= DemandThreshold)
            {
                var fpn = FoodPressure();
                Note[Zone.Res] = HomesWaitForFood ? $"Homes wait: the village eats {fpn.eat:0.#} food a day and makes {fpn.make:0.#}, and the store would not last a year. Zone more farmland or add hunting and fishing." : "Homes grow once houses are nearly full and food is stocked.";
                foreach (var d in Districts) if (d.Type == DistrictType.Residential) d.Note = Note[Zone.Res];
                return;
            }
            if (LastWater != null && LastWater.ShortHomes > 0)
            {
                Note[Zone.Res] = "Homes will not grow while homes are short of water. Build a well or a cistern near them.";
                foreach (var d in Districts) if (d.Type == DistrictType.Residential) d.Note = Note[Zone.Res];
                return;
            }
            // two kinds of housing zone: Level 1 (cottages, wood) and Level 2 (stone houses, wood and stone). One home grows a day, from whichever
            // zone has a site the town can pay for; when both can, the one nearer the Town Hall. A zone short of materials says what it waits for.
            // Districts may refuse (disabled, paused, limits, an open project, the construction reserve); each says why in its own Note.
            Func<District, bool> allow = d => { var why = DevGate(d); if (why != null) d.Note = why; return why == null; };
            var site1 = HomeSite(Zone.Res, allow); var site2 = HomeSite(Zone.Res2, allow);
            bool ok1 = CanAffordHome(site1), ok2 = CanAffordHome(site2);
            string hold1 = ok1 ? ReserveBlock(site1.Dist, HomeCost(site1)) : null, hold2 = ok2 ? ReserveBlock(site2.Dist, HomeCost(site2)) : null;
            if (hold1 != null) { ok1 = false; site1.Dist.Note = hold1; }
            if (hold2 != null) { ok2 = false; site2.Dist.Note = hold2; }
            Note[Zone.Res] = site1 == null ? "Needs an empty housing tile beside a connected road." : hold1 != null ? hold1 : ok1 ? "" : $"Waiting for {site1.Cost} wood to build.";
            Note[Zone.Res2] = site2 == null ? "Needs an empty Level 2 housing tile beside a connected road." : hold2 != null ? hold2 : ok2 ? "" : $"Waiting for {HomeWait(site2)} to build.";
            if (site1 != null && !ok1 && hold1 == null) site1.Dist.Note = Note[Zone.Res];
            if (site2 != null && !ok2 && hold2 == null) site2.Dist.Note = Note[Zone.Res2];
            Pick site;
            if (ok1 && ok2) site = site2.Dist.Priority != site1.Dist.Priority ? (site2.Dist.Priority > site1.Dist.Priority ? site2 : site1) : (site2.Score <= site1.Score ? site2 : site1);
            else site = ok1 ? site1 : ok2 ? site2 : null;
            if (site == null)
            {
                if (site2 != null && site1 == null) Note[Zone.Res] = Note[Zone.Res2];   // only Level 2 zones exist: that is the note that matters
                return;
            }
            var (project, why2) = StartProject(site.Dist, site.Type, site.X, site.Y, site.Sz, site.Tier, HomeCost(site), 0,
                $"Homes are in demand ({(int)Math.Round(Dem[Zone.Res] * 100)}% over the threshold).", $"Room for {(site.Tier == 2 ? HouseCap[2] : HouseCap[1])} more people.");
            if (project == null) { site.Dist.Note = why2; return; }
            site.Dist.Note = project.Status == ProjectStatus.Proposed ? "A home is proposed. Approve it in the development queue." : "";
        }
    }
}
