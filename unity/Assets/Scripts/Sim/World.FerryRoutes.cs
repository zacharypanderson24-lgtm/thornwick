// Ferry routes (DESIGN_WATER.md, transport stabilization): the player links two ferry landings into a route. Nothing pairs automatically, so
// building a third landing never changes an existing ferry, and the choice of destination is the player's.
//
// A route is checked by the real sailing length over boat-deep water (not the straight-line distance) against FerryMaxRoute tiles. It needs a
// crew: a landing that is on a road to the Town Hall employs 2 ferrymen (trade "Ferrying"); with no crew the ferry does not run. It joins the
// roads at its two landings only while it is running (see FerryState). It is saved as its two landing tiles; rebuilding a destroyed landing on
// the same tile brings the same route back, and no route is ever made without a player's choice (old saves with automatically paired ferries
// are converted once, with a grace period for the crew).
//
// Capacity (aggregate, no individual crossings): a basic ferry carries FerryPassengers people a trip and FerryCargo cargo units a trip; trips a day
// fall with the route length. The load a route carries is the people and jobs of the buildings that are served ONLY through it. Where that exceeds
// the capacity the player is warned; the economy is not yet slowed by it (see DESIGN_WATER.md for what is still to do).
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum FerryState { Operational, WaitingForCrew, SuspendedByPlayer, FrozenRoute, MissingLanding, NoRoute, MissingVessel, VesselUnderRepair }

    public sealed class FerryRoute
    {
        public int Id, AX, AY, BX, BY;
        public bool Suspended;          // switched off by the player
        public int GraceUntil;          // migrated routes run without a crew until this day
        public bool MilitaryAllowed = true;   // reserved for military crossings (a later step); saved
    }

    public partial class World
    {
        public const int FerryMaxRoute = 20, FerryCrew = 2, FerryPassengers = 8, FerryCargo = 20, FerryRouteCost = 10, FerryMigrationGraceDays = 30;

        public readonly List<FerryRoute> FerryRoutes = new List<FerryRoute>();
        int _nextRouteId = 1;

        internal bool PendingFerryMigration;
        internal void RestoreRoute(FerryRoute r) { FerryRoutes.Add(r); _nextRouteId = Math.Max(_nextRouteId, r.Id + 1); }

        Building LandingAt(int x, int y) => Inb(x, y) && Grid[y, x]?.T == "ferry" ? Grid[y, x] : null;

        public FerryRoute RouteOf(Building landing) => landing == null ? null : FerryRoutes.FirstOrDefault(r => (r.AX == landing.X && r.AY == landing.Y) || (r.BX == landing.X && r.BY == landing.Y));
        public (Building a, Building b) Ends(FerryRoute r) => (LandingAt(r.AX, r.AY), LandingAt(r.BX, r.BY));

        /// <summary>The sailing route (tile indices) between two landings over boat water, or null; and its length in tiles.</summary>
        public List<int> SailingRoute(Building a, Building b)
        {
            var la = LaunchTile(a); var lb = LaunchTile(b); if (!la.HasValue || !lb.HasValue) return null;
            if (Waters.NavRegion[la.Value.y * N + la.Value.x] != Waters.NavRegion[lb.Value.y * N + lb.Value.x]) return null;
            return BoatRoute(la.Value, lb.Value);
        }

        /// <summary>Whether a route could be made between two landings now, and if not why, in words the player can act on.</summary>
        public Result CanRoute(Building a, Building b)
        {
            if (Waters == null) return Result.No("There is no water here");
            if (a == null || b == null || a.T != "ferry" || b.T != "ferry") return Result.No("A ferry route joins two ferry landings");
            if (a == b) return Result.No("Choose a different landing for the other end");
            if (RouteOf(a) != null) return Result.No("This landing already has a ferry route. Remove it first to choose another destination");
            if (RouteOf(b) != null) return Result.No("That landing is already part of another ferry route");
            var la = LaunchTile(a); var lb = LaunchTile(b);
            if (!la.HasValue || !lb.HasValue) return Result.No("A landing has no water deep enough for a ferry boat");
            if (Waters.NavRegion[la.Value.y * N + la.Value.x] != Waters.NavRegion[lb.Value.y * N + lb.Value.x]) return Result.No("Different water bodies: the two landings are not on the same connected water");
            var route = BoatRoute(la.Value, lb.Value);
            if (route == null) return Result.No("No navigable route joins the two landings");
            int length = route.Count - 1;
            if (length > FerryMaxRoute) return Result.No($"The sailing route is {length} tiles, longer than a ferry can run ({FerryMaxRoute})");
            if (ResAmt[Res.Gold] < FerryRouteCost) return Result.No($"Setting up a ferry route costs {FerryRouteCost} gold for licences and moorings; you have {ResAmt[Res.Gold]:0}");
            return Result.Yes(length);
        }

        public Result EstablishRoute(Building a, Building b)
        {
            var c = CanRoute(a, b); if (!c.Ok) return c;
            ResAmt[Res.Gold] -= FerryRouteCost;
            FerryRoutes.Add(new FerryRoute { Id = _nextRouteId++, AX = a.X, AY = a.Y, BX = b.X, BY = b.Y });
            Touch(); Recalc();
            return c;
        }

        public bool RemoveRoute(FerryRoute r)
        {
            if (!FerryRoutes.Remove(r)) return false;
            Boats.RemoveAll(bt => bt.Ferry && bt.RouteId == r.Id);
            Touch(); Recalc(); return true;
        }

        public void SetRouteSuspended(FerryRoute r, bool suspended) { if (r.Suspended == suspended) return; r.Suspended = suspended; Touch(); Recalc(); }

        // ---------- what the route is doing ----------
        public int CrewOf(FerryRoute r) { var (a, b) = Ends(r); return Math.Max(a?.Workers ?? 0, b?.Workers ?? 0); }

        public FerryState StateOf(FerryRoute r)
        {
            var (a, b) = Ends(r);
            if (a == null || b == null) return FerryState.MissingLanding;
            if (r.Suspended) return FerryState.SuspendedByPlayer;
            var route = SailingRoute(a, b);
            if (route == null || route.Count - 1 > FerryMaxRoute) return FerryState.NoRoute;
            if (IceOn && route.Any(t => Freezes(Waters.Class[t]) && Waters.Salinity[t] < 40)) return FerryState.FrozenRoute;
            if (CrewOf(r) < FerryCrew && Day >= r.GraceUntil) return FerryState.WaitingForCrew;
            var vessel = VesselOfRoute(r);
            if (vessel == null) return FerryState.MissingVessel;
            if (!InService(vessel)) return FerryState.VesselUnderRepair;
            return FerryState.Operational;
        }

        public static string StateText(FerryState s) => s switch
        {
            FerryState.Operational => "Operational",
            FerryState.WaitingForCrew => "Waiting for crew: needs 2 ferrymen at a landing on a road to the Town Hall",
            FerryState.SuspendedByPlayer => "Suspended by you",
            FerryState.FrozenRoute => "Temporarily suspended: the water on its route is frozen",
            FerryState.MissingLanding => "Suspended: a landing is missing (rebuild it on the same tile)",
            FerryState.MissingVessel => "Suspended: no ferry boat assigned (order one at a Boatyard)",
            FerryState.VesselUnderRepair => "Suspended: its ferry boat is worn out and needs a Boatyard to repair it",
            _ => "No valid route",
        };

        /// <summary>The routes whose two landings stand, as pairs (running or not).</summary>
        public List<(Building a, Building b)> FerryPairs()
        {
            var list = new List<(Building, Building)>();
            foreach (var r in FerryRoutes) { var (a, b) = Ends(r); if (a != null && b != null) list.Add((a, b)); }
            return list;
        }

        public Building FerryPartner(Building landing)
        {
            var r = RouteOf(landing); if (r == null) return null;
            var (a, b) = Ends(r); return a == landing ? b : a;
        }

        /// <summary>True when this pair's route is running now (staffed, not suspended, not frozen).</summary>
        public bool FerryOperating((Building a, Building b) pair)
        {
            var r = RouteOf(pair.a); return r != null && StateOf(r) == FerryState.Operational;
        }

        // ---------- capacity and load (aggregate) ----------
        public static int TripsPerDay(int lengthTiles) => Math.Clamp((int)Math.Round(12.0 / (1 + lengthTiles / 5.0)), 1, 6);
        public static int PassengerCapacityPerDay(int lengthTiles) => FerryPassengers * TripsPerDay(lengthTiles);
        public static int CargoCapacityPerDay(int lengthTiles) => FerryCargo * TripsPerDay(lengthTiles);

        /// <summary>The people and jobs that depend on this route: the served buildings that would lose their road to the Town Hall without it.
        /// Demand counts each worker once and a quarter of the residents (errands and visits). Computed on demand, not every tick.</summary>
        public (int buildings, int people, double demand) RouteLoad(FerryRoute r)
        {
            var d = Recalc();
            var without = ConnWithoutRoute(r);
            int buildings = 0, people = 0; double demand = 0;
            foreach (var b in Blds)
            {
                if (b.T == "road" || b.T == "ferry" || !b.Served) continue;
                if (ServedWith(b, without)) continue;   // still served without the ferry
                buildings++;
                if (b.T == "house") { people += b.Capacity; demand += 0.25 * b.Capacity; }
                else { people += b.Workers; demand += b.Workers; }
            }
            return (buildings, people, demand);
        }

        bool ServedWith(Building b, bool[] conn)
        {
            foreach (var (tx, ty) in b.Tiles())
                for (int k = 0; k < 4; k++) { int x = tx + Nb[k, 0], y = ty + Nb[k, 1]; if (Inb(x, y) && conn[Tk(x, y)]) return true; }
            return false;
        }

        bool[] ConnWithoutRoute(FerryRoute skip)
        {
            var conn = new bool[N * N]; var stack = new Stack<(int, int)>();
            bool IsRoad(int x, int y) => Inb(x, y) && Grid[y, x] != null && Grid[y, x].T == "road";
            foreach (var (hx, hy) in Hall.Tiles())
                for (int k = 0; k < 4; k++) { int x = hx + Nb[k, 0], y = hy + Nb[k, 1]; if (IsRoad(x, y) && !conn[Tk(x, y)]) { conn[Tk(x, y)] = true; stack.Push((x, y)); } }
            void Flood() { while (stack.Count > 0) { var (cx, cy) = stack.Pop(); for (int k = 0; k < 4; k++) { int x = cx + Nb[k, 0], y = cy + Nb[k, 1]; if (IsRoad(x, y) && !conn[Tk(x, y)]) { conn[Tk(x, y)] = true; stack.Push((x, y)); } } } }
            Flood();
            var routes = FerryRoutes.Where(rt => rt != skip && StateOf(rt) == FerryState.Operational).Select(Ends).ToList();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var (a, b) in routes)
                    foreach (var (from, to) in new[] { (a, b), (b, a) })
                    {
                        if (!RoadsBeside(from).Any(p => conn[Tk(p.x, p.y)])) continue;
                        foreach (var (x, y) in RoadsBeside(to).ToList()) if (!conn[Tk(x, y)]) { conn[Tk(x, y)] = true; stack.Push((x, y)); grew = true; Flood(); }
                    }
            }
            return conn;
        }

        /// <summary>How well a route copes: capacity a day against what depends on it (1 = enough or nothing depends on it).</summary>
        public double RouteService(FerryRoute r, int lengthTiles, double demand) => demand <= 0 ? 1.0 : Math.Min(1.0, PassengerCapacityPerDay(lengthTiles) / demand);

        // ---------- the monthly check and the migration of old saves ----------
        int _lastFerryWarning = -999;

        void StepFerryRoutes()
        {
            if (Waters == null || FerryRoutes.Count == 0 || Day - _lastFerryWarning < 30) return;
            foreach (var r in FerryRoutes)
            {
                if (StateOf(r) != FerryState.Operational) continue;
                var (a, b) = Ends(r); int len = (SailingRoute(a, b)?.Count ?? 1) - 1;
                var load = RouteLoad(r); double service = RouteService(r, len, load.demand);
                if (service < 0.75) { Events.Add($"The ferry between {r.AX},{r.AY} and {r.BX},{r.BY} is crowded: {load.people} people and jobs across the water depend on it and it can carry about {PassengerCapacityPerDay(len)} a day. Build another ferry route or a bridge."); _lastFerryWarning = Day; }
            }
        }

        /// <summary>Old saves (before routes) have landings paired automatically, nearest first: turn each such pair into a route, with a grace period
        /// for crews so no district is cut off by the change. Runs once, on load.</summary>
        public int MigrateAutomaticFerries(int graceDays = FerryMigrationGraceDays)
        {
            if (Waters == null) return 0;
            var landings = Blds.Where(b => b.T == "ferry").ToList(); var used = new HashSet<Building>(); int made = 0;
            foreach (var a in landings)
            {
                if (used.Contains(a)) continue;
                Building best = null; int bestD = int.MaxValue;
                foreach (var b in landings)
                {
                    if (b == a || used.Contains(b)) continue;
                    int d = Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
                    if (d < bestD && SailingRoute(a, b) is List<int> route && route.Count - 1 <= FerryMaxRoute) { best = b; bestD = d; }
                }
                if (best == null) continue;
                used.Add(a); used.Add(best);
                FerryRoutes.Add(new FerryRoute { Id = _nextRouteId++, AX = a.X, AY = a.Y, BX = best.X, BY = best.Y, GraceUntil = Day + graceDays });
                made++;
            }
            if (made > 0) Events.Add($"Ferries now have routes you control, and need a crew of {FerryCrew}. {made} existing ferr{(made == 1 ? "y keeps" : "ies keep")} running for {graceDays} days while you staff {(made == 1 ? "it" : "them")}.");
            return made;
        }
    }
}
