// Work crews and townsfolk, ported from src/sim_crews.js.
// Crews: woodcutters, quarrymen and miners live at their building, walk out to a marked tile, work, and carry the load home. Output only counts when a
// load is delivered, so farther sites make slower trips. Townsfolk: ambient villagers who stroll the roads; purely cosmetic, the economy never reads them.
// Both are runtime only (never saved): after a load everybody starts at home, as in the browser game.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>Down: a miner underground in the mine's shaft (Unity-only, World.Mining.cs); not drawn.</summary>
    public enum CrewState : byte { Home, Out, Work, Back, Down, Hide, Shelter }
    public enum FolkState : byte { Home, Walk, Stay, Back, Hide, Shelter }

    /// <summary>One worker of a crew (JS: the objects in crewOf(b)).</summary>
    public class Worker
    {
        public CrewState St;
        public int X, Y, Px, Py;            // tile now and tile at the start of the day (for drawing in between)
        public readonly List<int> Path = new List<int>();   // tile indices y * N + x, building -> standing tile (reversed for the way back)
        public int I, T, Tx, Ty, Stand = -1;
        public double Carry;
        public bool Survey, Dig;            // miners: out surveying a zone tile / underground digging the shaft
        public int Level;                   // miners: the ore band they are working (0 while digging)
        public int Need;                    // miners: where they work, 0 = the adit, else that shaft level
    }

    /// <summary>One ambient villager (JS: the objects in folkOf(S)).</summary>
    public class Villager
    {
        public int Id, V;                   // V: tunic colour 0..FolkVariants-1
        public FolkState St;
        public Building Home;
        public int X, Y, Px, Py, I, T;
        public readonly List<int> Path = new List<int>();
    }

    /// <summary>The walkable area around a work building, cached until the world changes (JS: crewReach).</summary>
    class CrewReach
    {
        public readonly short[] Dist = new short[World.ReachW * World.ReachW];
        public readonly int[] Par = new int[World.ReachW * World.ReachW];
        public int Ox, Oy, Stamp = -1;
    }

    public partial class World
    {
        public const int WorkT = 2, CrewDepth = 2 * WorkR + 4, ReachW = 2 * CrewDepth + 1;   // a crew walks up to CrewDepth steps (a corner of the work area is 2 x WorkR away)
        public const int FolkMax = 90, FolkDepth = 40, FolkVariants = 6;
        public const double FolkPerPeasant = 0.6;

        public const int HuntingRange = WorkR, ForestFoodReserve = 60, HuntingLoad = 15, HuntingRecoveryDays = 6;   // food units: one villager eats 1 a day (World.Farms.cs FoodPerPerson)
        // Only harvested tiles have an entry. Each date is when the reserve will be full again.
        readonly Dictionary<int, int> _gameRecoveryDay = new Dictionary<int, int>();

        public int HuntFoodRemaining(int x, int y)
        {
            if (!Inb(x, y) || DecoMap[y, x] != Deco.Tree) return 0;
            int daysToFull = Math.Max(0, _gameRecoveryDay.GetValueOrDefault(Tk(x, y)) - Day);
            int missingFood = (daysToFull + HuntingRecoveryDays - 1) / HuntingRecoveryDays;
            return Math.Max(0, ForestFoodReserve - missingFood);
        }

        public static Deco CrewKind(string t) => t == "wood" || t == "hunter" ? Deco.Tree : t == "quarry" ? Deco.Rock : t == "mine" ? Deco.Ore : Deco.None;
        static Res CrewRes(string type, Deco kind) => type == "hunter" ? Res.Food : kind == Deco.Tree ? Res.Logs : kind == Deco.Rock ? Res.Stone : Res.Ore;

        /// <summary>Movement rule (JS walkCost): blocked into or out of solid tiles and across steps of 1.75 levels or more; slopes cost extra.</summary>
        public double WalkCost(int x, int y, int nx, int ny)
        {
            if (!Inb(x, y) || !Inb(nx, ny) || IsSolid(x, y) || IsSolid(nx, ny)) return double.PositiveInfinity;
            if (PenAt(x, y) != PenAt(nx, ny)) return double.PositiveInfinity;
            if (IsShutGate(x, y) || IsShutGate(nx, ny)) return double.PositiveInfinity;   // World.Walls.cs: a shut gate blocks crews too
            double d = System.Math.Abs(TileLevel(nx, ny) - TileLevel(x, y));
            return d >= 1.75 ? double.PositiveInfinity : 1 + d * 1.5;
        }

        bool CrewMatch(Building b, Deco kind, int x, int y, bool hunting = false)
        {
            if (!Claimed(x, y)) return false;
            if (hunting) return HuntFoodRemaining(x, y) > 0;
            var dc = DecoMap[y, x];
            if (kind == Deco.Tree) return dc == Deco.Tree && TreeWorkable(b, x, y);   // World.Extraction.cs: the lodge's automatic area (or its painted boundary)
            if (kind == Deco.Rock) return dc == Deco.Rock && InWorkArea(b, Zone.Mine, x, y) && RockWorkable(x, y);
            return dc == Deco.Ore;
        }

        // ---------- Crew pathfinding ----------
        readonly int[] _reachQ = new int[ReachW * ReachW];

        CrewReach Reach(Building b)
        {
            var R = b.Reach ??= new CrewReach();
            if (R.Stamp == Version && R.Ox == b.X - CrewDepth && R.Oy == b.Y - CrewDepth) return R;
            int D = CrewDepth, ox = b.X - D, oy = b.Y - D;
            R.Stamp = Version; R.Ox = ox; R.Oy = oy;
            var dist = R.Dist; var par = R.Par;
            for (int k = 0; k < dist.Length; k++) dist[k] = -1;
            int l0 = (b.Y - oy) * ReachW + (b.X - ox);
            dist[l0] = 0; par[l0] = -1; _reachQ[0] = b.Y * N + b.X; int qn = 1;
            for (int h = 0; h < qn; h++)
            {
                int c = _reachQ[h], cx = c % N, cy = c / N, dd = dist[(cy - oy) * ReachW + (cx - ox)];
                if (dd >= D) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1]; if (!Inb(nx, ny)) continue;
                    int nl = (ny - oy) * ReachW + (nx - ox); if (dist[nl] >= 0) continue;
                    var g = Grid[ny, nx]; if (g != null && g != b && g.T != "road") continue;
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny))) continue;
                    dist[nl] = (short)(dd + 1); par[nl] = c; _reachQ[qn++] = ny * N + nx;
                }
            }
            return R;
        }

        /// <summary>Nearest reachable work tile within the building’s work range. Fills path (building -> standing tile).</summary>
        bool CrewTarget(Building b, Deco kind, int salt, List<int> used, List<int> path, out int tx, out int ty, bool survey = false)
        {
            var R = Reach(b); var dist = R.Dist; int ox = R.Ox, oy = R.Oy;
            int bi = -1, bj = -1, bn = -1; double bs = 1e9;
            bool hunting = b.T == "hunter";
            int range = hunting ? HuntingRange : WorkR;
            for (int j = b.Y - range; j <= b.Y + b.Sz - 1 + range; j++)
                for (int i = b.X - range; i <= b.X + b.Sz - 1 + range; i++)
                {
                    if (kind == Deco.Tree && !hunting && _reservedTrees.Contains(Tk(i, j))) continue;   // another worker already has this tree (any lodge)
                    if (!Inb(i, j) || !(survey ? ZoneMap[j, i] == Zone.Mine && !Surveyed[Tk(i, j)] && Claimed(i, j) : CrewMatch(b, kind, i, j, hunting))) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = i + Nb[k, 0], ny = j + Nb[k, 1]; if (!Inb(nx, ny)) continue;
                        int ni = ny * N + nx, dd = dist[(ny - oy) * ReachW + (nx - ox)];
                        if (dd < 0 || Grid[ny, nx] == b) continue;   // workers step out of the building, never work on its roof
                        double sc = dd + Noise.Hash2(i, j, 31 + salt) * 0.9 + (used.Contains(ni) ? 2.5 : 0);   // spread over equally near tiles
                        if (sc < bs) { bs = sc; bi = i; bj = j; bn = ni; }
                    }
                }
            tx = bi; ty = bj;
            if (bn < 0) return false;
            path.Clear();
            for (int c = bn; c != -1; c = R.Par[(c / N - oy) * ReachW + (c % N - ox)]) path.Add(c);
            path.Reverse();
            return true;
        }

        double CrewAct(Building b, Deco kind, Worker a)
        {
            int tx = a.Tx, ty = a.Ty;
            if (kind == Deco.Tree)
            {
                if (!CrewMatch(b, kind, tx, ty) || !MayHarvest(b)) return 0;   // the floor is checked again at the last chop
                int k = Tk(tx, ty); Chop[k] = (Chop.TryGetValue(k, out var c) ? c : 0) + 1;
                if (Chop[k] >= ChopsPerTree) FellAt(tx, ty, b);
                return Load["wood"];
            }
            return MineRock(tx, ty, 0, Load["quarry"]);   // mines work underground (World.Mining.cs)
        }

        readonly List<int> _used = new List<int>(8);

        /// <summary>Advance every crew one day (JS stepCrews). `got` collects what was delivered today.</summary>
        readonly HashSet<int> _reservedTrees = new HashSet<int>();   // trees a woodcutter is walking to or chopping: no other lodge targets them (runtime)

        void StepCrews(Dictionary<Res, double> got)
        {
            _reservedTrees.Clear();
            foreach (var lodge in Blds) if (lodge.T == "wood") foreach (var w in lodge.Crew) if (w.St != CrewState.Home && w.Stand >= 0) _reservedTrees.Add(Tk(w.Tx, w.Ty));
            for (int bi = 0; bi < Blds.Count; bi++)
            {
                var b = Blds[bi]; var kind = CrewKind(b.T); if (kind == Deco.None) continue;
                var crew = b.Crew; int want = b.Workers; var res = CrewRes(b.T, kind);
                bool hunting = res == Res.Food;
                for (int i = crew.Count - 1; i >= 0 && crew.Count > want; i--) if (crew[i].St == CrewState.Home) crew.RemoveAt(i);   // resting surplus leaves; those out finish first
                while (crew.Count < want) crew.Add(new Worker { St = CrewState.Home, X = b.X, Y = b.Y, Px = b.X, Py = b.Y });
                if (b.T == "mine") { StepMine(b, crew, got); continue; }   // underground mining, World.Mining.cs
                for (int ai = 0; ai < crew.Count; ai++)
                {
                    var a = crew[ai];
                    a.Px = a.X; a.Py = a.Y; if (ShelterWorker(b, a)) continue;
                    if (a.St == CrewState.Home)
                    {
                        if (!b.Served) continue;
                        if (b.T == "wood" && !MayHarvest(b)) continue;   // World.Forestry.cs: a sustainable lodge leaves its standing trees
                        if (hunting && CropStorage(res) - ResAmt[res] - got[res] < 1) continue;
                        if (kind != Deco.Tree && ResAmt[res] + got[res] >= Storage) continue;   // full stockpile: stay home, keep the reserve in the ground
                        _used.Clear();
                        foreach (var o in crew) if (o != a && o.St != CrewState.Home && o.Stand >= 0) _used.Add(o.Stand);
                        if (!CrewTarget(b, kind, ai * 7 + Day, _used, a.Path, out a.Tx, out a.Ty)) continue;
                        if (kind == Deco.Tree && !hunting) _reservedTrees.Add(Tk(a.Tx, a.Ty));
                        a.Stand = a.Path[a.Path.Count - 1]; a.I = 0; a.Carry = 0;
                        if (a.Path.Count > 1) { a.I = 1; SetPos(a, a.Path[1]); }
                        a.St = a.I == a.Path.Count - 1 ? CrewState.Work : CrewState.Out; if (a.St == CrewState.Work) a.T = WorkT;
                    }
                    else if (a.St == CrewState.Out)
                    {
                        a.I++; SetPos(a, a.Path[a.I]);
                        if (a.I == a.Path.Count - 1) { a.St = CrewState.Work; a.T = WorkT; }
                    }
                    else if (a.St == CrewState.Work)
                    {
                        if (--a.T <= 0) { a.Carry = hunting ? Math.Min(HuntingLoad, HuntFoodRemaining(a.Tx, a.Ty)) : CrewAct(b, kind, a); a.Path.Reverse(); a.I = 0; a.St = CrewState.Back; }
                    }
                    else if (a.I < a.Path.Count - 1) { a.I++; SetPos(a, a.Path[a.I]); }
                    if (a.St == CrewState.Back && a.I >= a.Path.Count - 1)
                    {
                        if (hunting && a.Carry > 0)
                        {
                            // Claims settle at delivery so concurrent hunters share the reserve
                            // and saving mid-trip cannot spend undelivered game.
                            int key = Tk(a.Tx, a.Ty);
                            int room = (int)Math.Floor(Math.Max(0, CropStorage(res) - ResAmt[res] - got[res]));
                            int delivered = Math.Min((int)a.Carry, Math.Min(room, HuntFoodRemaining(a.Tx, a.Ty)));
                            if (delivered > 0)
                                _gameRecoveryDay[key] = Math.Max(Day, _gameRecoveryDay.GetValueOrDefault(key))
                                    + delivered * HuntingRecoveryDays;
                            a.Carry = delivered;
                        }
                        got[res] += a.Carry; a.Carry = 0; a.St = CrewState.Home; a.Path.Clear(); a.Stand = -1; a.X = b.X; a.Y = b.Y;
                    }
                }
            }
        }

        static void SetPos(Worker a, int c) { a.X = c % N; a.Y = c / N; }
        static void SetPos(Villager w, int c) { w.X = c % N; w.Y = c / N; }

        // ---------- Townsfolk ----------
        public readonly List<Villager> Folk = new List<Villager>();
        readonly int[] _roadSeen = new int[N * N], _roadPar = new int[N * N];
        readonly short[] _roadDist = new short[N * N];
        readonly List<int> _roadQ = new List<int>(256);
        int _roadStamp;
        readonly List<Building> _folkHouses = new List<Building>(), _folkPois = new List<Building>();
        readonly HashSet<Building> _folkHouseSet = new HashSet<Building>();
        int _folkPlacesStamp = -1;

        bool IsRoadAt(int x, int y) => Inb(x, y) && Grid[y, x] != null && Grid[y, x].T == "road" && !IsShutGate(x, y);   // a shut gate blocks townsfolk too

        /// <summary>A road tile touching the building, preferring the one its door faces (JS roadBeside). -1 when none.</summary>
        public int RoadBeside(Building b)
        {
            int f = FaceOf(b);
            foreach (var (tx, ty) in b.Tiles()) { int x = tx + FaceDirs[f, 0], y = ty + FaceDirs[f, 1]; if (IsRoadAt(x, y)) return y * N + x; }
            foreach (var (tx, ty) in b.Tiles()) for (int k = 0; k < 4; k++) if (IsRoadAt(tx + Nb[k, 0], ty + Nb[k, 1])) return (ty + Nb[k, 1]) * N + tx + Nb[k, 0];
            return -1;
        }

        /// <summary>Breadth-first walk of the road network from one road tile (JS roadSearch); fills _roadQ in visiting order.</summary>
        void RoadSearch(int from)
        {
            _roadStamp++; _roadSeen[from] = _roadStamp; _roadDist[from] = 0; _roadPar[from] = -1;
            _roadQ.Clear(); _roadQ.Add(from);
            for (int h = 0; h < _roadQ.Count; h++)
            {
                int c = _roadQ[h], cx = c % N, cy = c / N, d = _roadDist[c]; if (d >= FolkDepth) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1]; if (!IsRoadAt(nx, ny)) continue;
                    int ni = ny * N + nx; if (_roadSeen[ni] == _roadStamp) continue;
                    _roadSeen[ni] = _roadStamp; _roadDist[ni] = (short)(d + 1); _roadPar[ni] = c; _roadQ.Add(ni);
                }
            }
        }

        void RoadPathTo(int to, List<int> path) { path.Clear(); for (int c = to; c != -1; c = _roadPar[c]) path.Add(c); path.Reverse(); }

        // Houses that can send out a villager and the places they walk to. Rebuilt only after the world changed (JS folkPlaces, keyed on topology).
        void FolkPlaces()
        {
            if (_folkPlacesStamp == Version) return;
            _folkPlacesStamp = Version;
            _folkHouses.Clear(); _folkHouseSet.Clear(); _folkPois.Clear();
            foreach (var b in Blds)
            {
                if (b.T == "house" && b.Served && RoadBeside(b) >= 0) { _folkHouses.Add(b); _folkHouseSet.Add(b); }
                if (b.T == "well" || b.T == "hall" || b.T == "sawmill" || b.T == "barracks") _folkPois.Add(b);
            }
        }

        /// <summary>Advance the townsfolk one day (JS stepFolk). Cosmetic only.</summary>
        void StepFolk()
        {
            FolkPlaces();
            var folk = Folk; var houses = _folkHouses;
            int want = houses.Count > 0 ? System.Math.Min(FolkMax, Noise.JsRound(Peasants * FolkPerPeasant)) : 0;
            for (int i = folk.Count - 1; i >= 0 && folk.Count > want; i--) if (folk[i].St == FolkState.Home || !_folkHouseSet.Contains(folk[i].Home)) folk.RemoveAt(i);
            int nextId = 0; foreach (var w in folk) nextId = System.Math.Max(nextId, w.Id + 1);
            while (folk.Count < want && houses.Count > 0)
            {
                var h = houses[(int)System.Math.Floor(Noise.Hash2(nextId, Day, 91) * houses.Count)];
                int id = nextId++;   // as in the JS object literal: t and v hash the incremented id
                folk.Add(new Villager
                {
                    Id = id, St = FolkState.Home, Home = h, X = h.X, Y = h.Y, Px = h.X, Py = h.Y,
                    T = 1 + (int)System.Math.Floor(Noise.Hash2(nextId, 5, 7) * 8), V = (int)System.Math.Floor(Noise.Hash2(nextId, 3, 11) * FolkVariants),
                });
            }
            var pois = _folkPois;
            foreach (var w in folk)
            {
                w.Px = w.X; w.Py = w.Y; if (ShelterFolk(w)) continue;
                if (w.St == FolkState.Home)
                {
                    if (--w.T > 0) continue;
                    int door = RoadBeside(w.Home); if (door < 0) { w.T = 6; continue; }
                    RoadSearch(door);
                    if (_roadQ.Count < 2) { w.T = 6 + (int)System.Math.Floor(Noise.Hash2(w.Id, Day, 13) * 6); continue; }   // no road to walk on
                    int dest = -1;
                    double r = Noise.Hash2(w.Id, Day, 17);
                    if (r < 0.6 && pois.Count > 0)
                    {
                        var p = pois[(int)System.Math.Floor(Noise.Hash2(w.Id, Day, 19) * pois.Count)]; int rb = RoadBeside(p);
                        if (rb >= 0 && _roadSeen[rb] == _roadStamp && rb != door) dest = rb;
                    }
                    if (dest < 0) dest = _roadQ[1 + (int)System.Math.Floor(Noise.Hash2(w.Id, Day, 23) * (_roadQ.Count - 1))];   // a wander to some reachable road tile
                    RoadPathTo(dest, w.Path); w.I = 0; SetPos(w, w.Path[0]); w.St = FolkState.Walk;
                }
                else if (w.St == FolkState.Walk)
                {
                    if (w.I < w.Path.Count - 1) { w.I++; SetPos(w, w.Path[w.I]); }
                    if (w.I >= w.Path.Count - 1) { w.St = FolkState.Stay; w.T = 2 + (int)System.Math.Floor(Noise.Hash2(w.Id, Day, 29) * 5); }
                }
                else if (w.St == FolkState.Stay)
                {
                    if (--w.T <= 0) { w.Path.Reverse(); w.I = 0; w.St = FolkState.Back; }
                }
                else
                {
                    if (w.I < w.Path.Count - 1) { w.I++; SetPos(w, w.Path[w.I]); }
                    if (w.I >= w.Path.Count - 1) { w.St = FolkState.Home; w.Path.Clear(); w.X = w.Home.X; w.Y = w.Home.Y; w.T = 3 + (int)System.Math.Floor(Noise.Hash2(w.Id, Day, 31) * 10); }
                }
            }
        }
    }
}
