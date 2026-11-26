// People and soldiers on ferries (DESIGN_WATER.md, transport stabilization). A ferry is a physical service: it carries limited loads, takes time,
// and only certain kinds of cargo.
//
//   WHAT IT CARRIES  A ferry boat takes civilians, infantry and goods; a cargo barge also horses and wagons; nothing carries siege equipment yet
//                    (Cargo, Carries). Classes are data on the vessel type, not rules scattered through other systems.
//   SOLDIERS         Your soldiers (defenders in a raid) cross by an operating, military-allowed route when the way to their goal (a raider, or the
//                    barracks they are returning to) is cut by water and no land route exists: they walk to the landing, wait for room (a route
//                    carries PassengerCapacityPerDay people a day), sail for CrossDays and arrive on the far side. They are out of the fight while
//                    on the water. Raiders never use your ferries. A route can be closed to military use (Alt-click its landing with the Ferry Route
//                    tool). A company larger than a trip crosses in groups (CompanyCrossing says how long).
//   CIVILIANS        The economy is aggregate (no individual commutes), so what you see is consistent with it rather than a crowd: the ferry boat carries
//                    as many riders as the people depending on the route would fill a trip with (PassengersOn), drawn as a few passengers aboard.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum Cargo : byte { Civilian, Infantry, Goods, Horse, Wagon, Siege }

    public partial class World
    {
        public static bool Carries(VesselType t, Cargo c) => t switch
        {
            VesselType.FerryBoat => c == Cargo.Civilian || c == Cargo.Infantry || c == Cargo.Goods,
            VesselType.CargoBarge => c == Cargo.Civilian || c == Cargo.Infantry || c == Cargo.Goods || c == Cargo.Horse || c == Cargo.Wagon,
            VesselType.FishingBoat => c == Cargo.Goods,
            _ => false,
        };

        public bool RouteCarries(FerryRoute r, Cargo c) { var v = VesselOfRoute(r); return v != null && InService(v) && Carries(v.Type, c); }

        public static int CrossDays(int lengthTiles) => 1 + lengthTiles / 12;

        /// <summary>Soldiers may use a route that is running, open to the military and has a vessel that carries infantry.</summary>
        public bool MilitaryCanUse(FerryRoute r) => r.MilitaryAllowed && StateOf(r) == FerryState.Operational && RouteCarries(r, Cargo.Infantry);

        public void SetMilitaryAccess(FerryRoute r, bool allowed) { r.MilitaryAllowed = allowed; Touch(); }

        // ---------- the day's room on a route ----------
        readonly Dictionary<int, int> _crossedToday = new Dictionary<int, int>(); int _crossedDay = -1;

        void RollCrossingDay() { if (_crossedDay != Day) { _crossedToday.Clear(); _crossedDay = Day; } }

        public int RouteLength(FerryRoute r) { var (a, b) = Ends(r); if (a == null || b == null) return 0; var route = SailingRoute(a, b); return route == null ? 0 : route.Count - 1; }

        public int CrossingRoomToday(FerryRoute r) { RollCrossingDay(); return Math.Max(0, PassengerCapacityPerDay(Math.Max(1, RouteLength(r))) - _crossedToday.GetValueOrDefault(r.Id)); }

        /// <summary>Take places for people crossing today; false (nothing taken) when the route has not got the room.</summary>
        public bool ReserveCrossing(FerryRoute r, int people)
        {
            if (CrossingRoomToday(r) < people) return false;
            _crossedToday[r.Id] = _crossedToday.GetValueOrDefault(r.Id) + people; return true;
        }

        /// <summary>How a company of this size gets over: in groups of one trip, as many trips a day as the route runs; the last group lands after the
        /// crossing time. (trips, days until the whole company is across).</summary>
        public (int trips, int days) CompanyCrossing(FerryRoute r, int size)
        {
            int len = Math.Max(1, RouteLength(r)); int trips = (size + FerryPassengers - 1) / FerryPassengers;
            int perDay = TripsPerDay(len);
            return (trips, (trips + perDay - 1) / perDay + CrossDays(len) - 1);
        }

        // ---------- passengers shown on the ferry boat ----------
        readonly Dictionary<int, (int version, int riders)> _riders = new Dictionary<int, (int, int)>();

        /// <summary>How many people ride the boat on an average trip: the people and jobs that depend on the route, spread over its trips, at most a boatload.</summary>
        public int PassengersOn(FerryRoute r)
        {
            if (_riders.TryGetValue(r.Id, out var c) && c.version == Version) return c.riders;
            int len = Math.Max(1, RouteLength(r)); var load = RouteLoad(r);
            int riders = Math.Clamp((int)Math.Round(load.demand / (2.0 * TripsPerDay(len))), 0, FerryPassengers);
            _riders[r.Id] = (Version, riders); return riders;
        }

        // ---------- soldiers crossing (called from the raid's fighter step) ----------
        readonly Dictionary<int, int[]> _fullFields = new Dictionary<int, int[]>(); int _fullFieldDay = -1, _fullFieldVersion = -1;

        /// <summary>Steps from every tile to a goal over ground soldiers can cross (no distance limit); cached for the day.</summary>
        int[] FullField(int gx, int gy)
        {
            if (_fullFieldDay != Day || _fullFieldVersion != Version) { _fullFields.Clear(); _fullFieldDay = Day; _fullFieldVersion = Version; }
            int key = Tk(gx, gy); if (_fullFields.TryGetValue(key, out var cached)) return cached;
            var dist = new int[N * N]; for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var q = new Queue<int>(); dist[key] = 0; q.Enqueue(key);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % N, cy = c / N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1];
                    if (!Inb(nx, ny) || dist[ny * N + nx] >= 0 || !FighterPass(nx, ny)) continue;
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny))) continue;
                    dist[ny * N + nx] = dist[c] + 1; q.Enqueue(ny * N + nx);
                }
            }
            return _fullFields[key] = dist;
        }

        /// <summary>Steps from every tile to a goal over ground soldiers can cross (exposed so tests can find water-cut places).</summary>
        public int[] SoldierField(int gx, int gy) => FullField(gx, gy);

        /// <summary>The soldiers' day for a raid report (the daily tick does this; exposed so tests can drive a fight).</summary>
        public void StepFightersNow(RaidReport r) => StepFighters(r);

        /// <summary>Why a soldier at his place can or cannot use a ferry to reach a goal (for tests and tuning).</summary>
        public string HopDiagnosis(Fighter f, int goalX, int goalY)
        {
            var sb = new System.Text.StringBuilder(); var goal = FullField(goalX, goalY);
            sb.Append($"fighter at {f.X},{f.Y} can walk to goal: {goal[f.Y * N + f.X] >= 0}; routes {FerryRoutes.Count}");
            foreach (var r in FerryRoutes)
            {
                var (a, b) = Ends(r); sb.Append($"; route {r.Id} state {StateOf(r)} military {MilitaryCanUse(r)}");
                if (a == null || b == null) continue;
                foreach (var (from, to) in new[] { (a, b), (b, a) })
                {
                    var (fx, fy) = StandBeside(to); var (nx, ny) = StandBeside(from);
                    sb.Append($" [{from.X},{from.Y}->{to.X},{to.Y}: far stand {fx},{fy} goalfield {goal[fy * N + fx]}; near stand {nx},{ny} fighter-to-near {FullField(nx, ny)[f.Y * N + f.X]}]");
                }
            }
            return sb.ToString();
        }

        /// <summary>A soldier who cannot walk to his goal: find a ferry that joins his side to a side that can, walk to its landing, wait for room and
        /// board. True when he did something (walked, boarded or is waiting for room); false when no ferry helps.</summary>
        bool TryFerryHop(Fighter f, int goalX, int goalY)
        {
            if (Waters == null || FerryRoutes.Count == 0) return false;
            var goal = FullField(goalX, goalY);
            if (goal[f.Y * N + f.X] >= 0) return false;   // he can walk there: no ferry needed
            FerryRoute bestRoute = null; Building bestNear = null, bestFar = null; int bestD = int.MaxValue; int[] bestField = null;
            foreach (var r in FerryRoutes)
            {
                if (!MilitaryCanUse(r)) continue;
                var (a, b) = Ends(r);
                foreach (var (from, to) in new[] { (a, b), (b, a) })
                {
                    var (fx, fy) = StandBeside(to); if (goal[fy * N + fx] < 0) continue;       // the far side must lead to the goal
                    var (nx, ny) = StandBeside(from);
                    var toLanding = FullField(nx, ny); int d = toLanding[f.Y * N + f.X]; if (d < 0) continue;   // and he must be able to reach the landing
                    if (d < bestD) { bestD = d; bestRoute = r; bestNear = from; bestFar = to; bestField = toLanding; }
                }
            }
            if (bestRoute == null) return false;
            if (bestD > 1)
            {
                for (int s = 0; s < FighterSpeed; s++) { if (bestField[f.Y * N + f.X] <= 1) break; if (!StepDown(f, bestField)) break; }
                if (bestField[f.Y * N + f.X] > 1) return true;
            }
            if (!ReserveCrossing(bestRoute, 1)) return true;   // no room today: he waits at the landing
            var (tx, ty) = StandBeside(bestFar);
            f.St = FighterState.Crossing; f.CrossDay = Day + CrossDays(Math.Max(1, RouteLength(bestRoute))); f.CrossX = tx; f.CrossY = ty; f.CrossRoute = bestRoute.Id;
            return true;
        }
    }
}
