// Water transport (DESIGN_WATER.md sections 18, 19 and 25): ferries and ports.
//
// FERRY: two ferry landings on the shores of the same boat-deep water, at most FerryRange tiles apart, form a ferry. A ferry joins the road
// network across the water (a bridge cannot cross deep water, a wide river or a bay): a road beside one landing counts as connected to the
// road beside the other, so homes, farms and workplaces across the water are served from the Town Hall. A ferry boat shuttles between the
// two landings (in winter, with the boats hauled out, the link still holds). People and crews do not walk through a ferry: only roads are
// linked (use a bridge, or the winter ice, to walk across).
//
// PORT: a trading harbour on a shore with a sea, lake or river big enough for ships. It gives your trade one ship (World.MaritimeTrade.cs): trade
// goes by water only to settlements the world map shows on the same sea or lake, and the travel time comes from the water route.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FerryRange = 20, PortWaterNeeded = 100;

        /// <summary>The navigable region (boat-deep water) a landing or port launches into, or 0.</summary>
        public int RegionOf(Building b)
        {
            var launch = LaunchTile(b); return launch.HasValue ? Waters.NavRegion[launch.Value.y * N + launch.Value.x] : 0;
        }

        /// <summary>Roads standing beside a landing (the ones a ferry joins to the other shore).</summary>
        IEnumerable<(int x, int y)> RoadsBeside(Building landing)
        {
            foreach (var (tx, ty) in landing.Tiles())
                for (int k = 0; k < 4; k++)
                {
                    int x = tx + Nb[k, 0], y = ty + Nb[k, 1];
                    if (Inb(x, y) && Grid[y, x] != null && Grid[y, x].T == "road" && Grid[y, x] != landing) yield return (x, y);
                }
        }

        /// <summary>Called by Recalc after the road flood: wherever the network reaches one landing of a ferry, the roads beside the other landing join it.
        /// Returns the newly joined road tiles for the flood to carry on from.</summary>
        void FerryJoin(bool[] conn, Stack<(int, int)> stack)
        {
            if (Waters == null) return;
            var pairs = FerryRoutes.Where(r => StateOf(r) == FerryState.Operational).Select(Ends).ToList(); if (pairs.Count == 0) return;   // a ferry that is not running joins nothing
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var (a, b) in pairs)
                    foreach (var (from, to) in new[] { (a, b), (b, a) })
                    {
                        bool reached = false; foreach (var (x, y) in RoadsBeside(from)) if (conn[Tk(x, y)]) { reached = true; break; }
                        if (!reached) continue;
                        foreach (var (x, y) in RoadsBeside(to))
                            if (!conn[Tk(x, y)])
                            {
                                conn[Tk(x, y)] = true; stack.Push((x, y)); grew = true;
                                while (stack.Count > 0)
                                {
                                    var (cx, cy) = stack.Pop();
                                    for (int k = 0; k < 4; k++) { int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1]; if (Inb(nx, ny) && Grid[ny, nx] != null && Grid[ny, nx].T == "road" && !conn[Tk(nx, ny)]) { conn[Tk(nx, ny)] = true; stack.Push((nx, ny)); } }
                                }
                            }
                    }
            }
        }

        /// <summary>What a landing is doing, for its panel (the route's state; see World.FerryRoutes.cs).</summary>
        public string FerryStatus(Building landing)
        {
            var r = RouteOf(landing); return r == null ? "No ferry route: choose a destination with the Ferry Route tool" : StateText(StateOf(r));
        }

        // ---------- ferry boats ----------
        void StepFerryBoats()
        {
            if (Waters == null) return;
            var running = FerryRoutes.Where(r => StateOf(r) == FerryState.Operational).ToList();
            Boats.RemoveAll(b => b.Ferry && !running.Any(r => r.Id == b.RouteId));
            foreach (var r in running)
            {
                if (Boats.Any(b => b.Ferry && b.RouteId == r.Id)) continue;
                var (a, partner) = Ends(r);
                var la = LaunchTile(a); var lb = LaunchTile(partner); if (!la.HasValue || !lb.HasValue) continue;
                var path = BoatRoute(la.Value, lb.Value); if (path == null) continue;
                Boats.Add(new Boat { Id = ++_boatId, Home = a, Ferry = true, RouteId = r.Id, X = la.Value.x, Y = la.Value.y, Px = la.Value.x, Py = la.Value.y, LaunchX = la.Value.x, LaunchY = la.Value.y,
                    Region = Waters.NavRegion[la.Value.y * N + la.Value.x], Path = path, I = 0, St = BoatState.Moored, Timer = 0 });
            }
        }

        /// <summary>Shortest route over boat water between two tiles (tile indices, start first), or null.</summary>
        List<int> BoatRoute((int x, int y) from, (int x, int y) to)
        {
            int s = from.y * N + from.x, t = to.y * N + to.x; var parent = new Dictionary<int, int> { { s, -1 } }; var q = new Queue<int>(); q.Enqueue(s);
            while (q.Count > 0)
            {
                int c = q.Dequeue(); if (c == t) break; int x = c % N, y = c / N;
                for (int k = 0; k < 4; k++) { int nx = x + Nb[k, 0], ny = y + Nb[k, 1]; if (!BoatWater(nx, ny)) continue; int ni = ny * N + nx; if (parent.ContainsKey(ni)) continue; parent[ni] = c; q.Enqueue(ni); }
            }
            if (!parent.ContainsKey(t)) return null;
            var path = new List<int>(); for (int c = t; c != -1; c = parent[c]) path.Add(c); path.Reverse(); return path;
        }

        // ---------- ports and trade by water ----------
        public int Ports => Blds.Count(b => b.T == "port" && b.Served);

        /// <summary>Boat-deep water joined to this region: how big a body of water the port opens on (tiles).</summary>
        public int NavigableTilesInRegion(int region)
        {
            if (Waters == null || region == 0) return 0;
            int n = 0; for (int i = 0; i < Waters.NavRegion.Length; i++) if (Waters.NavRegion[i] == region) n++; return n;
        }
    }
}
