// One placed building or road tile (JS: the objects in S.blds).
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    public partial class Building
    {
        public string T;          // type: road, house, farm, wood, quarry, mine, market, sawmill, forester, well, barracks, hall
        public int X, Y;          // top-left (lowest x, lowest y) footprint tile
        public int Sz = 1;        // footprint size (1, 2 or 3)
        public double Prog;       // forester planting / barracks training progress
        public bool Served;       // connected to the Town Hall by road
        public int Lvl = 1;       // house level (2 with a well in range)
        public int Var;           // hut look 1..8, sticky
        public int Face = -1;     // door direction (-1 = not decided yet)
        public bool Covered;      // house within a served market's reach
        public bool Zoned;
        public int Workers;       // derived each recalc
        public double Eff;        // workers / jobs, derived each recalc
        public readonly List<Worker> Crew = new List<Worker>();   // work crew (wood, quarry, mine); runtime only, never saved
        internal CrewReach Reach;  // cached walkable area around the building (World.Crews.cs)
        // mines (World.Mining.cs); saved state once saves land
        public bool Adit, AditShored;   // the level tunnel into the high ground, and its timber
        public int AditProg;       // worker-days driven towards the adit
        public int Depth;          // shaft levels dug, 0..World.MaxDepth
        public int Shored;         // levels shored with timber, 0..Depth
        public int DigProg;        // worker-days dug towards the next level
        public int Closed;         // days of repairs left after a cave-in
        public int CaveIns;

        public int Jobs => Defs.Jobs.TryGetValue(T, out var j) ? Noise.JsRound(j * (Sz == 2 ? 2.5 : 1)) : 0;
        public double Food => 3 * (Sz == 2 ? 2.6 : 1);
        public int MarketRange => Sz == 2 ? 7 : 5;
        public int Capacity => Sz == 2 ? Defs.HouseCap2[Lvl] : Defs.HouseCap[Lvl];
        public string Name => Sz == 2 && T == "house" ? "Large House" : Sz == 2 && T == "farm" ? "Large Farm" : Sz == 2 && T == "market" ? "Bazaar"
            : (Defs.Names.TryGetValue(T, out var n) ? n : T);

        /// <summary>Footprint tiles (JS: tilesOf).</summary>
        public IEnumerable<(int x, int y)> Tiles()
        {
            for (int j = 0; j < Sz; j++) for (int i = 0; i < Sz; i++) yield return (X + i, Y + j);
        }

        /// <summary>Centre offset used for distances (JS: ctr).</summary>
        public double Ctr => Sz > 1 ? (Sz - 1) / 2.0 : 0;

        /// <summary>Chebyshev distance between building centres (JS: cheb).</summary>
        public static double Cheb(Building a, Building b) =>
            System.Math.Max(System.Math.Abs(a.X + a.Ctr - b.X - b.Ctr), System.Math.Abs(a.Y + a.Ctr - b.Y - b.Ctr));
    }
}
