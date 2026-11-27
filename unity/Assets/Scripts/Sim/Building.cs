// One placed building or road tile (JS: the objects in S.blds).
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    public partial class Building
    {
        public string T;          // type: road, house, farm, wood, quarry, mine, market, sawmill, forester, well, cistern, silo, barracks, hall
        public int X, Y;          // top-left (lowest x, lowest y) footprint tile
        public int Sz = 1;        // footprint size (1, 2 or 3)
        public double Prog;       // forester planting / barracks training progress
        public bool Served;       // connected to the Town Hall by road
        public int Lvl = 1;       // house level: Tier in the Unity game, the browser rule (served and watered) in Classic
        /// <summary>The kind of house it was built as (1: cottage from a Level 1 housing zone, 2: stone house from a Level 2 zone). Saved; houses in older saves are tier 2.</summary>
        public int Tier = 1;
        public int Var;           // hut look 1..8, sticky
        public int Face = -1;     // door direction (-1 = not decided yet)
        public bool Zoned;
        public int Workers;       // derived each recalc
        public double Eff;        // workers / jobs, derived each recalc
        public readonly List<Worker> Crew = new List<Worker>();   // work crew (wood, quarry, mine); runtime only, never saved
        internal CrewReach Reach;  // cached walkable area around the building (World.Crews.cs)
        // mines (World.Mining.cs); saved state once saves land
        public readonly List<int> Tunnel = new List<int>();      // tiles (y * N + x) the mine has dug, in order
        public readonly HashSet<int> Shored = new HashSet<int>(); // the dug tiles that are shored with timber
        public int DigProg;        // worker-days dug towards the next tunnel tile
        public int Closed;         // days of repairs left after a cave-in
        public int CaveIns;
        public bool Organic;       // lodges: built in (or placed inside) a Forestry district: works that district's trees under the sustainable rules, keeps the zone (DESIGN_ORGANIC_DEVELOPMENT.md phase 3; saved)
        public ForestMode Mode;    // woodcutter lodges: Sustainable (default), Intensive or Clearing (World.Forestry.cs; saved)
        public int ReserveStage;   // mines: 0 fine, 1 low-ore warning given, 2 out-of-ore warning given (runtime; a loaded save may warn once more)


        public int Jobs => Defs.Jobs.TryGetValue(T, out var j) ? Noise.JsRound(j * (Sz == 2 && T == "farm" ? 2.5 : 1)) : 0;
        public double Food => 3 * (Sz == 2 ? 2.6 : 1);
        public int Capacity => Sz == 2 ? Defs.HouseCap2[Lvl] : Defs.HouseCap[Lvl];
        public string Name => Sz == 2 && T == "house" ? "Large House" : Sz == 2 && T == "farm" ? "Large Farm"
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
