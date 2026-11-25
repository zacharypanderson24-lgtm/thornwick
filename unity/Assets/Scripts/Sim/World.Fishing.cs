// Fishing (DESIGN_WATER.md section 21): a fishing hut or dock stands on the shore and its fishermen bring fish into the ordinary food stock,
// where it counts as fresh meat (it spoils, and a smokehouse preserves it). The catch depends on the kind of water they can walk to, how much
// of it there is, how many fishermen work, the season, and how fished-out the water is. A dock reaches only boat-deep water and catches more.
// Not built yet: boats, ports, a fish-stock display on the map.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FishReach = 6, FishShoreNeeded = 2;
        public const int FishGroundsFull = 10;   // this many reachable water tiles give a full catch
        public const double DockBonus = 1.4;
        /// <summary>Every body of water keeps a fish stock from 0 (fished out) to 1 (untouched). A body holds StockPerTile food a tile; fish
        /// breed back slowly (2% a day of what is left, logistic) so a pond feeds about a fisherman, a lake a village and the sea a town.</summary>
        public const double StockPerTile = 6, StockRegrow = 0.02, StockTrickle = 0.002;

        /// <summary>Fish stock by water body id; a body not listed is untouched (1). Saved.</summary>
        public readonly Dictionary<int, double> FishStock = new Dictionary<int, double>();
        public double StockOf(int body) => FishStock.TryGetValue(body, out var s) ? s : 1.0;

        /// <summary>Food a fisherman brings in per day from this water, at full fishing grounds (pond, lake, river, sea in that order).</summary>
        public static double FishRate(WaterClass c) => c switch
        {
            WaterClass.Stream => 0.25, WaterClass.Pond => 0.4, WaterClass.SmallRiver => 0.6, WaterClass.MajorRiver => 0.8, WaterClass.Lake => 0.8,
            WaterClass.Estuary => 0.9, WaterClass.Ocean => 1.0, WaterClass.Sea => 1.0, _ => 0,
        };

        /// <summary>Winter ice and cold slow freshwater fishing most; the sea is steadier; summer is best.</summary>
        public double FishSeason(WaterClass kind)
        {
            bool salt = SurfaceWater.Salty(kind) || kind == WaterClass.Estuary;
            double drought = DroughtOn && !salt ? 0.8 : 1.0;   // low water in a drought summer
            switch (Season) { case "Winter": return salt ? 0.8 : 0.5; case "Summer": return 1.1 * drought; default: return 1.0; }
        }

        public static string FishLevel(double perWorker) => perWorker <= 0 ? "None" : perWorker < 0.35 ? "Poor" : perWorker < 0.65 ? "Fair" : perWorker < 0.9 ? "Good" : "High";

        /// <summary>True when open water (at least minDepth) lies within `reach` tiles of the footprint (a place a fishing hut or dock can stand).</summary>
        public bool NearOpenWater(Building fp, int reach, WaterDepth minDepth = WaterDepth.Shallow)
        {
            if (Waters == null) return false;
            for (int y = fp.Y - reach; y < fp.Y + fp.Sz + reach; y++)
                for (int x = fp.X - reach; x < fp.X + fp.Sz + reach; x++)
                    if (Waters.IsWater(x, y) && Waters.Depth[y * N + x] >= minDepth) return true;
            return false;
        }

        /// <summary>What a hut or dock can fish: the water tiles within FishReach whose bank a fisherman can walk to (a dock: only medium and deep
        /// water), the commonest kind, the body of water it mostly fishes, and the catch per fisherman a day at full stock and in an average season.</summary>
        public (int grounds, WaterClass kind, double perWorker, int body) FishingGrounds(Building b)
        {
            if (Waters == null) return (0, WaterClass.None, 0, 0);
            var R = Reach(b);   // the tiles a worker can walk to from the building (World.Crews.cs)
            bool dock = b.T == "dock";
            var counts = new int[16]; var bodies = new Dictionary<int, int>(); int total = 0; bool anyShore = false;
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
                    if (shore) anyShore = true;
                    if (dock) { if (Waters.Depth[y * N + x] < WaterDepth.Medium) continue; }   // a pier reaches out over the shallows to boat-deep water
                    else if (!shore) continue;
                    counts[(int)Waters.Class[y * N + x]]++; total++;
                    int id = Waters.Body[y * N + x]; bodies[id] = bodies.GetValueOrDefault(id) + 1;
                }
            if (total == 0 || (dock && !anyShore)) return (0, WaterClass.None, 0, 0);
            int best = 0; for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            int bestBody = 0, bestN = -1; foreach (var kv in bodies) if (kv.Value > bestN || (kv.Value == bestN && kv.Key < bestBody)) { bestN = kv.Value; bestBody = kv.Key; }
            var kind = (WaterClass)best;
            return (total, kind, FishRate(kind) * Math.Min(1, total / (double)FishGroundsFull) * (dock ? DockBonus : 1), bestBody);
        }

        /// <summary>Food this staffed, connected hut or dock would bring in today (season and stock included); changes nothing.</summary>
        public double FishCatch(Building b)
        {
            if (Waters == null || !(b.T == "fisher" || b.T == "dock") || !b.Served || b.Workers <= 0) return 0;
            var g = FishingGrounds(b);
            return b.Workers * g.perWorker * FishSeason(g.kind) * StockOf(g.body) * DockBoatFactor(b);
        }

        /// <summary>The day's catch from every staffed, road-connected hut and dock. Fishing draws the water's stock down and the fish breed back.</summary>
        public double StepFishing()
        {
            double fish = 0;
            if (Waters == null) return 0;
            var taken = new Dictionary<int, double>();
            foreach (var b in Blds)
            {
                if (!(b.T == "fisher" || b.T == "dock") || !b.Served || b.Workers <= 0) continue;
                var g = FishingGrounds(b); double c = b.Workers * g.perWorker * FishSeason(g.kind) * StockOf(g.body) * DockBoatFactor(b);
                fish += c; taken[g.body] = taken.GetValueOrDefault(g.body) + c;
            }
            foreach (var id in new List<int>(FishStock.Keys)) if (!taken.ContainsKey(id)) taken[id] = 0;   // waters that recover with nobody fishing them
            foreach (var kv in taken)
            {
                var body = kv.Key > 0 && kv.Key <= Waters.Bodies.Count ? Waters.Bodies[kv.Key - 1] : null; if (body == null) continue;
                double s = StockOf(kv.Key);
                s += StockRegrow * s * (1 - s) + StockTrickle;
                s -= kv.Value / (body.Tiles * StockPerTile);
                s = Math.Clamp(s, 0.02, 1.0);
                if (s >= 0.999) FishStock.Remove(kv.Key); else FishStock[kv.Key] = s;
            }
            return fish;
        }

        /// <summary>A dock's catch with no boats is only what its fishermen get from the pier (a quarter); with boats for all its crews (one for every two fishermen) it is full.</summary>
        public double DockBoatFactor(Building b) => b.T != "dock" ? 1.0 : 0.25 + 0.75 * Math.Min(1.0, DockBoatsInService(b) / (double)Math.Max(1, (b.Workers + 1) / 2));

        public static string StockWord(double s) => s >= 0.9 ? "plentiful" : s >= 0.6 ? "good" : s >= 0.3 ? "thinning" : "nearly fished out";
    }
}
