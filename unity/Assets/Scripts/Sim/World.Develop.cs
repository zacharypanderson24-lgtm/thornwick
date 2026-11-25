using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const double DemandThreshold = 0.05;
        public static readonly Zone[] ZoneKinds = { Zone.Res };
        public readonly Dictionary<Zone, double> Dem = new Dictionary<Zone, double> { [Zone.Res] = 0 };
        public readonly Dictionary<Zone, string> Note = new Dictionary<Zone, string> { [Zone.Res] = "", [Zone.Res2] = "" };

        public void ComputeDemand()
        {
            double occupancy = D.Civ > 0 ? (Peasants + Soldiers) / (double)D.Civ : 1;
            double food = ResAmt[Res.Food];
            double demand = Starving || food < 10 * FoodScale ? -0.3 : Math.Max(-1, Math.Min(1, (occupancy - 0.7) * 4));
            if (!SeasonalFarming && Rate[Res.Food] < -0.3 && food < 120) demand = Math.Min(demand, 0);
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
        public class Pick { public int X, Y, Sz; public string Type; public int Cost, Stone, Tier = 1; public double Score; }

        /// <summary>The best empty tile beside a connected road in a housing zone of this kind (Level 1 or Level 2), however poor the town is.
        /// Null when there is none.</summary>
        public Pick HomeSite(Zone kind)
        {
            if (kind != Zone.Res && kind != Zone.Res2) return null;
            bool level2 = kind == Zone.Res2;
            int cost = level2 ? House2Cost[Res.Wood] : Cost["house"][Res.Wood], stone = level2 ? House2Cost[Res.Stone] : 0;
            Pick best = null;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (ZoneMap[y, x] != kind || Grid[y, x] != null || IsSolid(x, y) || HasTree(x, y)) continue;
                    bool served = false;
                    for (int k = 0; k < 4 && !served; k++)
                    {
                        int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                        served = Inb(nx, ny) && (D.Conn[Tk(nx, ny)] || Grid[ny, nx] == Hall);
                    }
                    if (!served) continue;
                    double score = Math.Abs(x - Hall.X) + Math.Abs(y - Hall.Y) + Noise.Hash2(x, y, Day + 1) * 2.5;
                    if (best == null || score < best.Score)
                        best = new Pick { X = x, Y = y, Sz = 1, Type = "house", Cost = cost, Stone = stone, Tier = level2 ? 2 : 1, Score = score };
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

        public void Develop()
        {
            ComputeDemand();
            if (Dem[Zone.Res] <= DemandThreshold)
            {
                Note[Zone.Res] = "Homes grow once houses are nearly full and food is stocked.";
                return;
            }
            if (LastWater != null && LastWater.ShortHomes > 0)
            {
                Note[Zone.Res] = "Homes will not grow while homes are short of water. Build a well or a cistern near them.";
                return;
            }
            // two kinds of housing zone: Level 1 (cottages, wood) and Level 2 (stone houses, wood and stone). One home grows a day, from whichever
            // zone has a site the town can pay for; when both can, the one nearer the Town Hall. A zone short of materials says what it waits for.
            var site1 = HomeSite(Zone.Res); var site2 = HomeSite(Zone.Res2);
            bool ok1 = CanAffordHome(site1), ok2 = CanAffordHome(site2);
            Note[Zone.Res] = site1 == null ? "Needs an empty housing tile beside a connected road." : ok1 ? "" : $"Waiting for {site1.Cost} wood to build.";
            Note[Zone.Res2] = site2 == null ? "Needs an empty Level 2 housing tile beside a connected road." : ok2 ? "" : $"Waiting for {HomeWait(site2)} to build.";
            var site = ok1 && ok2 ? (site2.Score <= site1.Score ? site2 : site1) : ok1 ? site1 : ok2 ? site2 : null;
            if (site == null)
            {
                if (site2 != null && site1 == null) Note[Zone.Res] = Note[Zone.Res2];   // only Level 2 zones exist: that is the note that matters
                return;
            }
            ResAmt[Res.Wood] -= site.Cost; ResAmt[Res.Stone] -= site.Stone;
            var home = AddBld(site.Type, site.X, site.Y, site.Sz);
            home.Zoned = true; home.Tier = site.Tier;
            Recalc();
        }
    }
}
