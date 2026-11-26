// Universal land clearing (DESIGN_RESOURCE_EXTRACTION.md): designate removable obstacles (trees, saplings, stumps, outcrop rocks) and idle villagers clear them as
// real work orders, recovering what they can. Building or roading through woodland queues the clearing it needs and places the building when the ground is ready.
// Demolition (bulldozer) removes buildings; clearing removes natural obstacles only and never touches a building. Data in, data out: no UI.
//   Labour   up to ClearCrewMax idle villagers a day, one order each; a tile takes ClearDays[obstacle] worker-days.
//   Recovery a felled tree gives LogsFromClearing logs (a lodge crew gets the full 24: hacking down a tree by hand wastes half), a sapling 2, a stump 1, an outcrop up to RockRecovery stone.
//   Orders   that cannot be reached (no walkable way to a road within ClearReachSteps) are skipped and dropped after StuckDays so they never hold workers for ever.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum Obstacle { None, Tree, Sapling, Stump, Rock }
    public enum PlaceState { Invalid, ValidNow, ValidAfterClearing }

    public class PendingBuild
    {
        public string Type; public int X, Y;
        public List<int> Path;   // road tiles (y * N + x) for a pending road, else null
        public bool IsRoad => Path != null;
    }

    public class PlacementPlan
    {
        public PlaceState State; public Result Block;
        public readonly List<(int x, int y)> ToClear = new List<(int, int)>();
        public readonly List<(int x, int y)> ProtectedTiles = new List<(int, int)>();
        public string Summary => State == PlaceState.ValidNow ? "Valid" : State == PlaceState.ValidAfterClearing ? $"Valid after clearing {ToClear.Count} tile{(ToClear.Count == 1 ? "" : "s")}" + (ProtectedTiles.Count > 0 ? $" ({ProtectedTiles.Count} protected: confirmation needed)" : "") : Block.Reason;
    }

    public partial class World
    {
        public const int ClearCrewMax = 6, ClearReachSteps = 24, StuckDays = 30, LogsFromClearing = 12, SaplingLogs = 2, StumpLogs = 1, RockRecovery = 8;
        public static readonly Dictionary<Obstacle, int> ClearDays = new Dictionary<Obstacle, int> { { Obstacle.Tree, 6 }, { Obstacle.Sapling, 1 }, { Obstacle.Stump, 2 }, { Obstacle.Rock, 8 } };

        public readonly Dictionary<int, int> ClearProgress = new Dictionary<int, int>();   // worker-days put into an order so far (saved)
        public readonly List<PendingBuild> Pending = new List<PendingBuild>();             // buildings and roads waiting for their ground to be cleared (saved)
        public int ClearedTiles, ClearedLogs, ClearedStone;                                 // lifetime totals (saved)
        public int ClearWorkersToday { get; private set; }
        readonly Dictionary<int, int> _clearStuck = new Dictionary<int, int>();             // runtime: days an unreachable order has waited

        // ---------- what can be cleared ----------
        public Obstacle ObstacleAt(int x, int y)
        {
            if (!Inb(x, y) || Grid[y, x] != null) return Obstacle.None;
            switch (DecoMap[y, x])
            {
                case Deco.Tree: return Obstacle.Tree;
                case Deco.Sapling: return Obstacle.Sapling;
                case Deco.Stump: return Obstacle.Stump;
                case Deco.Rock: return RockAmt(x, y) < 3 && PitDepth(x, y) == 0 && !IsTunnel(x, y) ? Obstacle.Rock : Obstacle.None;
                default: return Obstacle.None;
            }
        }

        /// <summary>Why a tile cannot be designated (null when it can).</summary>
        public string WhyNotClearable(int x, int y)
        {
            if (!Inb(x, y)) return "Outside the map";
            if (!Claimed(x, y)) return NotYours;
            if (Grid[y, x] != null) return "A building is here. Clearing removes only trees and rocks: use the bulldozer for buildings";
            if (DecoMap[y, x] == Deco.Rock && ObstacleAt(x, y) == Obstacle.None) return RockAmt(x, y) >= 3 ? "A mountain cannot be cleared. Quarry it" : PitDepth(x, y) > 0 ? "A quarry pit cannot be filled in" : "That rock cannot be cleared";
            if (ObstacleAt(x, y) == Obstacle.None) return "Nothing to clear here";
            return null;
        }

        /// <summary>Designate every clearable tile in the rectangle. Protected woodland is only included with confirmProtected.</summary>
        public (int ordered, int skipped, int protectedSkipped, string why) ClearRect(int ax, int ay, int bx, int by, bool confirmProtected = false)
        {
            int ordered = 0, skipped = 0, prot = 0; var why = new Dictionary<string, int>();
            for (int y = Math.Min(ay, by); y <= Math.Max(ay, by); y++)
                for (int x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++)
                {
                    if (!Inb(x, y) || ClearOrders.Contains(Tk(x, y))) continue;
                    var r = DesignateClearing(x, y, confirmProtected);
                    if (r.Ok) { ordered++; continue; }
                    if (r.Reason.StartsWith("This woodland is protected")) { prot++; continue; }
                    if (r.Reason == "Nothing to clear here") continue;   // open ground in a dragged rectangle is not a failure
                    skipped++; why[r.Reason] = why.GetValueOrDefault(r.Reason) + 1;
                }
            return (ordered, skipped, prot, why.Count == 0 ? "" : why.OrderByDescending(p => p.Value).First().Key);
        }

        public int CancelClearingRect(int ax, int ay, int bx, int by)
        {
            int n = 0;
            for (int y = Math.Min(ay, by); y <= Math.Max(ay, by); y++)
                for (int x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++)
                    if (Inb(x, y) && ClearOrders.Remove(Tk(x, y))) { ClearProgress.Remove(Tk(x, y)); n++; }
            if (n > 0) Touch();
            return n;
        }

        // ---------- the work ----------
        bool ClearReachable(int x, int y)
        {
            var seen = new HashSet<int> { Tk(x, y) }; var q = new Queue<(int x, int y, int d)>(); q.Enqueue((x, y, 0));
            while (q.Count > 0)
            {
                var (cx, cy, d) = q.Dequeue();
                if (d > 0 && Grid[cy, cx] != null && (Grid[cy, cx].T == "road" || Grid[cy, cx] == Hall)) return true;
                if (d >= ClearReachSteps) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1];
                    if (!Inb(nx, ny) || !seen.Add(Tk(nx, ny))) continue;
                    var g = Grid[ny, nx];
                    if (g != null) { if (g.T == "road" || g == Hall) q.Enqueue((nx, ny, d + 1)); continue; }
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny))) continue;
                    q.Enqueue((nx, ny, d + 1));
                }
            }
            return false;
        }

        void StepClearing(Dictionary<Res, double> got)
        {
            StepPending();
            ClearWorkersToday = 0;
            if (ClearOrders.Count == 0) return;
            int workers = Math.Min(ClearCrewMax, D.Idle);
            if (workers <= 0) return;
            int hx = Hall?.X ?? Start, hy = Hall?.Y ?? Start;
            var order = ClearOrders.OrderBy(k => Math.Abs(k % N - hx) + Math.Abs(k / N - hy)).ThenBy(k => k).ToList();
            int used = 0; bool changed = false;
            foreach (int k in order)
            {
                if (used >= workers) break;
                int x = k % N, y = k / N; var ob = ObstacleAt(x, y);
                if (ob == Obstacle.None) { ClearOrders.Remove(k); ClearProgress.Remove(k); changed = true; continue; }   // already gone (built on, felled by a lodge)
                if (_reservedTrees.Contains(k)) continue;                       // a woodcutter is on it: one crew per tree
                if (!ClearReachable(x, y))
                {
                    int days = _clearStuck[k] = _clearStuck.GetValueOrDefault(k) + 1;
                    if (days >= StuckDays) { ClearOrders.Remove(k); ClearProgress.Remove(k); _clearStuck.Remove(k); changed = true; Events.Add($"A clearing order at {x},{y} was dropped: workers cannot reach it. Lay a road or path nearby."); }
                    continue;
                }
                used++;
                int prog = ClearProgress[k] = ClearProgress.GetValueOrDefault(k) + 1;
                if (prog < ClearDays[ob]) continue;
                // done: recover what is usable, remove the obstacle for good
                double logs = ob == Obstacle.Tree ? LogsFromClearing : ob == Obstacle.Sapling ? SaplingLogs : ob == Obstacle.Stump ? StumpLogs : 0;
                double stone = ob == Obstacle.Rock ? Math.Min(RockRecovery, RockLeft(x, y)) : 0;
                DecoMap[y, x] = Deco.None; Chop.Remove(k); StumpT.Remove(k); SapT.Remove(k); Replant.Remove(k);
                if (ob == Obstacle.Rock) { RockAmtMap.Remove(k); Dig.Remove(k); Elev.Remove(k); OreLeftMap.Remove(k); }
                ClearOrders.Remove(k); ClearProgress.Remove(k); _clearStuck.Remove(k);
                got[Res.Logs] = got.GetValueOrDefault(Res.Logs) + logs; got[Res.Stone] = got.GetValueOrDefault(Res.Stone) + stone;
                ClearedTiles++; ClearedLogs += (int)logs; ClearedStone += (int)stone;
                changed = true;
            }
            ClearWorkersToday = used;
            if (changed) Touch();
        }

        public struct ClearingReport { public int Pending, Workers, Idle, Completed, Logs, Stone, Stuck, PendingBuilds; }

        public ClearingReport ClearingStatus() => new ClearingReport
        {
            Pending = ClearOrders.Count, Workers = ClearWorkersToday, Idle = D?.Idle ?? 0, Completed = ClearedTiles, Logs = ClearedLogs, Stone = ClearedStone,
            Stuck = _clearStuck.Count, PendingBuilds = Pending.Count,
        };

        // ---------- clear as part of building ----------
        /// <summary>Can this be placed now, after clearing, or not at all? The tiles that need clearing, and which of them are protected.</summary>
        public PlacementPlan PlanPlacement(string type, int x, int y)
        {
            var plan = new PlacementPlan();
            var now = CanPlace(type, x, y);
            if (now.Ok) { plan.State = PlaceState.ValidNow; return plan; }
            var later = CanPlace(type, x, y, true);
            if (!later.Ok) { plan.State = PlaceState.Invalid; plan.Block = later; return plan; }
            var fp = new Building { T = type, X = x, Y = y, Sz = PlacementSize(type) };
            foreach (var (tx, ty) in fp.Tiles()) if (HasTree(tx, ty)) { plan.ToClear.Add((tx, ty)); if (IsProtected(tx, ty)) plan.ProtectedTiles.Add((tx, ty)); }
            if (plan.ToClear.Count == 0) { plan.State = PlaceState.Invalid; plan.Block = now; return plan; }
            plan.State = PlaceState.ValidAfterClearing;
            return plan;
        }

        bool Planned(int x, int y, int sz)
        {
            foreach (var p in Pending)
            {
                if (!p.IsRoad) { int psz = PlacementSize(p.Type); if (x < p.X + psz && p.X < x + sz && y < p.Y + psz && p.Y < y + sz) return true; }
                else foreach (int k in p.Path) if (k % N >= x && k % N < x + sz && k / N >= y && k / N < y + sz) return true;
            }
            return false;
        }

        /// <summary>Place now, or queue the clearing the site needs and place when it is ready (the cost is paid at placement). Protected woodland needs confirmProtected.</summary>
        public Result PlaceWithClearing(string type, int x, int y, bool confirmProtected = false)
        {
            var plan = PlanPlacement(type, x, y);
            if (plan.State == PlaceState.Invalid) return plan.Block;
            if (plan.State == PlaceState.ValidNow) return Place(type, x, y);
            if (Planned(x, y, PlacementSize(type))) return Result.No("Something is already planned on this ground");
            if (plan.ProtectedTiles.Count > 0 && !confirmProtected) return Result.No("Some of these trees are protected woodland. Confirm to clear them for good");
            foreach (var (tx, ty) in plan.ToClear) { var r = DesignateClearing(tx, ty, confirmProtected); if (!r.Ok) return r; }
            Pending.Add(new PendingBuild { Type = type, X = x, Y = y });
            Touch();
            return Result.Yes(plan.ToClear.Count);
        }

        /// <summary>Lay a road across woodland: place the tiles that are free now and queue the rest behind their clearing.</summary>
        public Result PlaceRoadWithClearing(IList<(int x, int y)> path, bool confirmProtected = false)
        {
            var open = RoadPathCheck(path);
            if (open.Ok) return PlaceRoadPath(path);
            var later = RoadPathCheck(path, true);
            if (!later.Ok) return later;
            var trees = path.Where(t => HasTree(t.x, t.y)).ToList();
            if (trees.Count == 0) return open;
            foreach (var t in path) if (Planned(t.x, t.y, 1)) return Result.No("Something is already planned on this ground");
            if (trees.Any(t => IsProtected(t.x, t.y)) && !confirmProtected) return Result.No("Some of these trees are protected woodland. Confirm to clear them for good");
            foreach (var t in trees) { var r = DesignateClearing(t.x, t.y, confirmProtected); if (!r.Ok) return r; }
            Pending.Add(new PendingBuild { Type = "road", X = path[0].x, Y = path[0].y, Path = path.Select(t => Tk(t.x, t.y)).ToList() });
            Touch();
            return Result.Yes(trees.Count);
        }

        public bool CancelPending(int x, int y)
        {
            var p = Pending.FirstOrDefault(q => q.IsRoad ? q.Path.Contains(Tk(x, y)) : x >= q.X && x < q.X + PlacementSize(q.Type) && y >= q.Y && y < q.Y + PlacementSize(q.Type));
            if (p == null) return false;
            Pending.Remove(p); Touch(); return true;
        }

        void StepPending()
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var p = Pending[i];
                if (p.IsRoad)
                {
                    var tiles = p.Path.Select(k => (x: k % N, y: k / N)).ToList();
                    if (tiles.Any(t => HasTree(t.x, t.y))) continue;     // still being cleared
                    var r = PlaceRoadPath(tiles);
                    if (r.Ok || r.Reason == null) { Pending.RemoveAt(i); continue; }
                    if (r.Reason.StartsWith("Not enough")) continue;      // waits for the wood
                    Pending.RemoveAt(i); Events.Add($"A planned road was dropped: {r.Reason}.");
                }
                else
                {
                    var fp = new Building { T = p.Type, X = p.X, Y = p.Y, Sz = PlacementSize(p.Type) };
                    if (fp.Tiles().Any(t => HasTree(t.x, t.y))) continue;
                    var r = Place(p.Type, p.X, p.Y);
                    if (r.Ok) { Pending.RemoveAt(i); continue; }
                    if (r.Reason != null && r.Reason.StartsWith("Not enough")) continue;
                    Pending.RemoveAt(i); Events.Add($"A planned {Names.GetValueOrDefault(p.Type, p.Type)} at {p.X},{p.Y} was dropped: {r.Reason}.");
                }
            }
        }
    }
}
