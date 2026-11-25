// Fishing (DESIGN_WATER.md section 21, first step): a fishing hut stands on the shore and its fishermen bring fish into the ordinary food
// stock, where it counts as fresh meat (it spoils, and a smokehouse preserves it). A hut's catch depends on the kind of water it can reach on
// foot, how much of it there is, and how many fishermen it has. Not built yet: seasons, fish stocks that run down, boats, docks.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FishReach = 6, FishShoreNeeded = 2;
        public const int FishGroundsFull = 10;   // this many reachable water tiles give a full catch

        /// <summary>Food a fisherman brings in per day from this water, at full fishing grounds (pond, lake, river, sea in that order).</summary>
        public static double FishRate(WaterClass c) => c switch
        {
            WaterClass.Stream => 0.25, WaterClass.Pond => 0.4, WaterClass.SmallRiver => 0.6, WaterClass.MajorRiver => 0.8, WaterClass.Lake => 0.8,
            WaterClass.Estuary => 0.9, WaterClass.Ocean => 1.0, WaterClass.Sea => 1.0, _ => 0,
        };

        public static string FishLevel(double perWorker) => perWorker <= 0 ? "None" : perWorker < 0.35 ? "Poor" : perWorker < 0.65 ? "Fair" : perWorker < 0.9 ? "Good" : "High";

        /// <summary>True when open water lies within `reach` tiles of the footprint (a place a fishing hut can stand).</summary>
        public bool NearOpenWater(Building fp, int reach)
        {
            if (Waters == null) return false;
            for (int y = fp.Y - reach; y < fp.Y + fp.Sz + reach; y++)
                for (int x = fp.X - reach; x < fp.X + fp.Sz + reach; x++)
                    if (Waters.IsWater(x, y)) return true;
            return false;
        }

        /// <summary>What a hut can fish: the water tiles within FishReach whose bank a fisherman can walk to, the commonest kind, and the catch per fisherman a day.</summary>
        public (int grounds, WaterClass kind, double perWorker) FishingGrounds(Building b)
        {
            if (Waters == null) return (0, WaterClass.None, 0);
            var R = Reach(b);   // the tiles a worker can walk to from the building (World.Crews.cs)
            var counts = new int[16]; int total = 0;
            for (int y = b.Y - FishReach; y < b.Y + b.Sz + FishReach; y++)
                for (int x = b.X - FishReach; x < b.X + b.Sz + FishReach; x++)
                {
                    if (!Waters.IsWater(x, y)) continue;
                    bool shore = false;
                    for (int k = 0; k < 4 && !shore; k++)
                    {
                        int lx = x + Nb[k, 0] - R.Ox, ly = y + Nb[k, 1] - R.Oy;
                        if (lx >= 0 && ly >= 0 && lx < ReachW && ly < ReachW && R.Dist[ly * ReachW + lx] >= 0) shore = true;
                    }
                    if (!shore) continue;
                    counts[(int)Waters.Class[y * N + x]]++; total++;
                }
            if (total == 0) return (0, WaterClass.None, 0);
            int best = 0; for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            var kind = (WaterClass)best;
            return (total, kind, FishRate(kind) * Math.Min(1, total / (double)FishGroundsFull));
        }

        /// <summary>The day's catch from every staffed, road-connected fishing hut.</summary>
        public double StepFishing()
        {
            double fish = 0;
            if (Waters == null) return 0;
            foreach (var b in Blds)
                if (b.T == "fisher" && b.Served && b.Workers > 0) fish += b.Workers * FishingGrounds(b).perWorker;
            return fish;
        }
    }
}
