// Boats (DESIGN_WATER.md section 25, first step): fishing boats that sail from a fishing dock over the water it fishes and come home again.
//
// Boats use the water layer's navigable regions: a boat moves only over medium and deep water (shallows are too shallow for boats, they are
// for fords and shore fishing) and only within the connected region its dock launches into, so a boat can never sail between two separate
// lakes or across land. They are runtime agents like townsfolk and work crews: never saved, rebuilt from the docks, and for now they do not
// change the catch (a dock's catch is still worked out per dock, World.Fishing.cs). Not built yet: cargo, barges, ferries, ports, warships.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum BoatState { Moored, Out, Fishing, Back }

    public sealed class Boat
    {
        public int Id, X, Y, Px, Py;     // tile now and at the start of the day (the view slides between them)
        public Building Home;            // the dock it works from
        public int LaunchX, LaunchY, Region;
        public BoatState St = BoatState.Moored;
        public int Timer;
        public List<int> Path = new List<int>(); public int I;   // tiles (y * N + x) of the trip, I = where it is on it
    }

    public partial class World
    {
        public const int BoatsPerDock = 2, BoatRange = FishReach + 3, FishingDays = 3, MooredDays = 1;
        public readonly List<Boat> Boats = new List<Boat>();
        int _boatId;

        /// <summary>The boats working from this dock.</summary>
        public int BoatsOf(Building dock) { int n = 0; foreach (var b in Boats) if (b.Home == dock) n++; return n; }

        bool BoatWater(int x, int y) => Inb(x, y) && Waters != null && (Waters.Flags[y * N + x] & WaterFlags.Navigable) != 0 && !IsBridge(x, y);

        /// <summary>The nearest tile of boat-deep water to a dock (within 3 tiles), where its boats are launched; null if there is none.</summary>
        public (int x, int y)? LaunchTile(Building dock)
        {
            if (Waters == null) return null;
            (int x, int y)? best = null; int bestD = int.MaxValue;
            for (int y = dock.Y - 3; y < dock.Y + dock.Sz + 3; y++)
                for (int x = dock.X - 3; x < dock.X + dock.Sz + 3; x++)
                {
                    if (!BoatWater(x, y)) continue;
                    int d = Math.Max(Math.Max(dock.X - x, x - (dock.X + dock.Sz - 1)), Math.Max(dock.Y - y, y - (dock.Y + dock.Sz - 1)));
                    if (d < bestD || (d == bestD && best.HasValue && (y < best.Value.y || (y == best.Value.y && x < best.Value.x)))) { bestD = d; best = (x, y); }
                }
            return best;
        }

        /// <summary>The boats' day: docks with fishermen launch boats (up to BoatsPerDock; one for each two fishermen), boats sail out to a spot on
        /// the water, fish for a few days and sail home, one tile a day. Call once a day (the daily tick does).</summary>
        public void StepBoats()
        {
            if (Waters == null || IceOn) { Boats.Clear(); return; }   // in winter the boats are hauled out
            Boats.RemoveAll(b => !Blds.Contains(b.Home) || !b.Home.Served || b.Home.Workers <= 0);
            foreach (var dock in Blds)
            {
                if (dock.T != "dock" || !dock.Served || dock.Workers <= 0) continue;
                int want = Math.Min(BoatsPerDock, (dock.Workers + 1) / 2), have = BoatsOf(dock);
                if (have >= want) continue;
                var launch = LaunchTile(dock); if (!launch.HasValue) continue;
                var (lx, ly) = launch.Value;
                Boats.Add(new Boat { Id = ++_boatId, Home = dock, X = lx, Y = ly, Px = lx, Py = ly, LaunchX = lx, LaunchY = ly, Region = Waters.NavRegion[ly * N + lx], Timer = have });
            }
            foreach (var b in Boats)
            {
                b.Px = b.X; b.Py = b.Y;
                switch (b.St)
                {
                    case BoatState.Moored:
                        if (b.Timer-- > 0) break;
                        if (PlanTrip(b)) b.St = BoatState.Out; else b.Timer = MooredDays;
                        break;
                    case BoatState.Out:
                        if (Advance(b)) { b.St = BoatState.Fishing; b.Timer = FishingDays; }
                        break;
                    case BoatState.Fishing:
                        if (--b.Timer <= 0) { b.Path.Reverse(); b.I = 0; b.St = BoatState.Back; }
                        break;
                    case BoatState.Back:
                        if (Advance(b)) { b.St = BoatState.Moored; b.Timer = MooredDays; }
                        break;
                }
            }
        }

        bool Advance(Boat b)
        {
            if (b.I + 1 >= b.Path.Count) return true;
            b.I++; int t = b.Path[b.I]; b.X = t % N; b.Y = t / N;
            return b.I + 1 >= b.Path.Count;
        }

        /// <summary>Choose a fishing spot (a boat-deep tile of the same connected water, a few tiles from the dock) and the route there.</summary>
        bool PlanTrip(Boat b)
        {
            // every tile reachable over boat water within BoatRange steps of the launch tile, with how to get back
            var parent = new Dictionary<int, int>(); var order = new List<int>(); var q = new Queue<int>();
            int start = b.LaunchY * N + b.LaunchX; parent[start] = -1; q.Enqueue(start); var depth = new Dictionary<int, int> { { start, 0 } };
            while (q.Count > 0)
            {
                int c = q.Dequeue(); order.Add(c); if (depth[c] >= BoatRange) continue;
                int x = c % N, y = c / N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + Nb[k, 0], ny = y + Nb[k, 1]; if (!BoatWater(nx, ny)) continue;
                    int ni = ny * N + nx; if (parent.ContainsKey(ni)) continue;
                    parent[ni] = c; depth[ni] = depth[c] + 1; q.Enqueue(ni);
                }
            }
            var far = order.FindAll(t => depth[t] >= 3); if (far.Count == 0) far = order.FindAll(t => depth[t] >= 1);
            if (far.Count == 0) return false;
            int pick = far[(int)(Noise.Hash2(Day, b.Id, Seed + 5151) * far.Count) % far.Count];
            var path = new List<int>(); for (int t = pick; t != -1; t = parent[t]) path.Add(t);
            path.Reverse(); b.Path = path; b.I = 0;
            return true;
        }
    }
}
