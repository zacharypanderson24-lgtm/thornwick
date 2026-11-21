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
        public readonly Dictionary<Zone, string> Note = new Dictionary<Zone, string> { [Zone.Res] = "" };

        public void ComputeDemand()
        {
            double occupancy = D.Civ > 0 ? (Peasants + Soldiers) / (double)D.Civ : 1;
            double food = ResAmt[Res.Food];
            double demand = Starving || food < 10 ? -0.3 : Math.Max(-1, Math.Min(1, (occupancy - 0.7) * 4));
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

        public class Pick { public int X, Y, Sz; public string Type; public int Cost; public double Score; }

        public (Pick pick, int wait) Choose(Zone kind)
        {
            if (kind != Zone.Res) return (null, 0);
            int cost = Cost["house"][Res.Wood];
            Pick best = null;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (ZoneMap[y, x] != Zone.Res || Grid[y, x] != null || IsSolid(x, y) || HasTree(x, y)) continue;
                    bool served = false;
                    for (int k = 0; k < 4 && !served; k++)
                    {
                        int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                        served = Inb(nx, ny) && (D.Conn[Tk(nx, ny)] || Grid[ny, nx] == Hall);
                    }
                    if (!served) continue;
                    double score = Math.Abs(x - Hall.X) + Math.Abs(y - Hall.Y) + Noise.Hash2(x, y, Day + 1) * 2.5;
                    if (best == null || score < best.Score)
                        best = new Pick { X = x, Y = y, Sz = 1, Type = "house", Cost = cost, Score = score };
                }
            return best != null && ResAmt[Res.Wood] < cost ? (null, cost) : (best, 0);
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
            var (site, wait) = Choose(Zone.Res);
            if (site == null)
            {
                Note[Zone.Res] = wait > 0 ? $"Waiting for {wait} wood to build." : "Needs an empty housing tile beside a connected road.";
                return;
            }
            Note[Zone.Res] = "";
            ResAmt[Res.Wood] -= site.Cost;
            AddBld(site.Type, site.X, site.Y, site.Sz).Zoned = true;
            Recalc();
        }
    }
}
