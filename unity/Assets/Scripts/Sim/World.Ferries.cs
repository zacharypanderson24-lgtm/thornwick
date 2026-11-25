// Water transport (DESIGN_WATER.md sections 18, 19 and 25): ferries and ports.
//
// FERRY: two ferry landings on the shores of the same boat-deep water, at most FerryRange tiles apart, form a ferry. A ferry joins the road
// network across the water (a bridge cannot cross deep water, a wide river or a bay): a road beside one landing counts as connected to the
// road beside the other, so homes, farms and workplaces across the water are served from the Town Hall. A ferry boat shuttles between the
// two landings (in winter, with the boats hauled out, the link still holds). People and crews do not walk through a ferry: only roads are
// linked (use a bridge, or the winter ice, to walk across).
//
// PORT: a trading harbour on a shore with a sea, lake or river big enough for ships. It counts as a market (a caravan can set out from it) and
// goods go by water, so a caravan sent while a port is free arrives in 60% of the usual time.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FerryRange = 20, PortWaterNeeded = 100;
        public const double PortSpeed = 0.6;

        /// <summary>The navigable region (boat-deep water) a landing or port launches into, or 0.</summary>
        public int RegionOf(Building b)
        {
            var launch = LaunchTile(b); return launch.HasValue ? Waters.NavRegion[launch.Value.y * N + launch.Value.x] : 0;
        }

        /// <summary>The ferries: landings paired two by two, nearest first, within FerryRange tiles and on the same boat-deep water. Each landing is in at most one.</summary>
        public List<(Building a, Building b)> FerryPairs()
        {
            var pairs = new List<(Building, Building)>();
            if (Waters == null) return pairs;
            var landings = Blds.Where(b => b.T == "ferry").ToList(); if (landings.Count < 2) return pairs;
            var region = landings.ToDictionary(b => b, RegionOf); var used = new HashSet<Building>();
            foreach (var a in landings)
            {
                if (used.Contains(a) || region[a] == 0) continue;
                Building best = null; int bestD = int.MaxValue;
                foreach (var b in landings)
                {
                    if (b == a || used.Contains(b) || region[b] != region[a]) continue;
                    int d = Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
                    if (d <= FerryRange && d < bestD) { best = b; bestD = d; }
                }
                if (best != null) { used.Add(a); used.Add(best); pairs.Add((a, best)); }
            }
            return pairs;
        }

        public Building FerryPartner(Building landing)
        {
            foreach (var (a, b) in FerryPairs()) { if (a == landing) return b; if (b == landing) return a; }
            return null;
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
            var pairs = FerryPairs(); if (pairs.Count == 0) return;
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

        // ---------- ferry boats ----------
        void StepFerryBoats()
        {
            if (Waters == null) return;
            var pairs = FerryPairs();
            Boats.RemoveAll(b => b.Ferry && !pairs.Any(p => p.a == b.Home));
            foreach (var (a, partner) in pairs)
            {
                if (Boats.Any(b => b.Ferry && b.Home == a)) continue;
                var la = LaunchTile(a); var lb = LaunchTile(partner); if (!la.HasValue || !lb.HasValue) continue;
                var path = BoatRoute(la.Value, lb.Value); if (path == null) continue;
                Boats.Add(new Boat { Id = ++_boatId, Home = a, Ferry = true, X = la.Value.x, Y = la.Value.y, Px = la.Value.x, Py = la.Value.y, LaunchX = la.Value.x, LaunchY = la.Value.y,
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

        /// <summary>Days a caravan to this settlement takes: while a port has a free ship the goods go by water, in 60% of the time.</summary>
        public int TradeMarchDays(Settlement s)
        {
            int days = SettlementMarchDays(s);
            return Ports > 0 && TradeCaravansOut < Ports ? Math.Max(1, (int)Math.Ceiling(days * PortSpeed)) : days;
        }

        /// <summary>Boat-deep water joined to this region: how big a body of water the port opens on (tiles).</summary>
        public int NavigableTilesInRegion(int region)
        {
            if (Waters == null || region == 0) return 0;
            int n = 0; for (int i = 0; i < Waters.NavRegion.Length; i++) if (Waters.NavRegion[i] == region) n++; return n;
        }
    }
}
