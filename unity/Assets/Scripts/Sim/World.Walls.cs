// Palisades and gates. A palisade tile is a wall: nobody walks through it. A road tile in a palisade line (walls on both sides of it) is a
// gate: it carries the road (the town's road network is not cut by it) and can be open or shut.
//   - Open: anyone may pass, friend or foe.
//   - Shut: nobody passes, your own soldiers, crews and townsfolk included.
// Gates shut while a raid is under way (or always, if you keep them shut from the Troops menu) and open again after it.
//
// Walls and gates can be broken. A raider that cannot reach anything worth taking does not give up: it picks the barrier on the cheapest way
// in and hacks at it (raiders hit a barrier once a day). A broken palisade tile is gone; a broken gate hangs open until the town mends it
// (1 point a day while no raid is on). Damage is saved.
//
// Fighting across a palisade: a melee blow needs the two to stand side by side with no wall between them (diagonally, at least one of the
// two tiles between them must be open). Arrows arc over a palisade: archers on either side may shoot across it.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const double WallHp = 12, GateHp = 18;

        /// <summary>Damage done to palisade and gate tiles, by tile key. Saved.</summary>
        public readonly Dictionary<int, double> BarrierDamage = new Dictionary<int, double>();
        /// <summary>Keep every gate shut, raid or not (Troops menu). Saved. A battle map uses it for "the town is roused".</summary>
        public bool KeepGatesShut;
        bool _gatesWereShut;

        bool IsWall(int x, int y) => Inb(x, y) && Grid[y, x]?.T == "wall";

        /// <summary>A road tile in a palisade line: walls on two opposite sides.</summary>
        public bool IsGateway(int x, int y)
        {
            if (!Inb(x, y) || Grid[y, x]?.T != "road") return false;
            return (IsWall(x - 1, y) && IsWall(x + 1, y)) || (IsWall(x, y - 1) && IsWall(x, y + 1));
        }

        /// <summary>Whether a gate's posts run along x (its wall line is east-west in the map).</summary>
        public bool GateAlongX(int x, int y) => IsWall(x - 1, y) && IsWall(x + 1, y);

        public bool GateBroken(int x, int y) => BarrierDamage.TryGetValue(Tk(x, y), out double d) && d >= GateHp;

        /// <summary>Gates are shut while a raid is on, or always when you keep them shut.</summary>
        public bool GatesShut => KeepGatesShut || (Raid != null && !Raid.Over);

        /// <summary>A shut gate that still holds.</summary>
        public bool IsShutGate(int x, int y) => GatesShut && IsGateway(x, y) && !GateBroken(x, y);

        /// <summary>Something nobody can walk through that can be broken: a palisade tile or a shut gate.</summary>
        public bool IsBarrier(int x, int y) => IsWall(x, y) || IsShutGate(x, y);

        /// <summary>Strength left in a palisade or gate tile.</summary>
        public double BarrierHpLeft(int x, int y) => (IsWall(x, y) ? WallHp : GateHp) - BarrierDamage.GetValueOrDefault(Tk(x, y));

        /// <summary>A melee blow from (ax, ay) can reach (bx, by): side by side, and no palisade or shut gate between them.</summary>
        public bool MeleeReach(int ax, int ay, int bx, int by)
        {
            int dx = Math.Abs(ax - bx), dy = Math.Abs(ay - by);
            if (Math.Max(dx, dy) > 1) return false;
            if (dx == 0 || dy == 0) return true;
            return !IsBarrier(ax, by) || !IsBarrier(bx, ay);
        }

        /// <summary>Hit a barrier. True when it breaks: a palisade tile is pulled down, a gate is left hanging open.</summary>
        public bool DamageBarrier(int x, int y, double hit)
        {
            if (!IsBarrier(x, y)) return false;
            int k = Tk(x, y);
            bool wall = IsWall(x, y);
            double d = BarrierDamage.GetValueOrDefault(k) + hit;
            BarrierDamage[k] = d;
            if (d < (wall ? WallHp : GateHp)) return false;
            if (wall) { var b = Grid[y, x]; Blds.Remove(b); Grid[y, x] = null; BarrierDamage.Remove(k); }
            Touch();
            if (Raid != null) { Raid.Fields.Clear(); Raid.Chase.Clear(); _breachFields.Clear(); }
            Events.Add(wall ? "Raiders have broken through the palisade!" : "Raiders have broken a gate!");
            return true;
        }

        /// <summary>Daily: shutting or opening the gates changes who can go where (paths are worked out again); outside a raid the town mends
        /// its gates and walls a little each day.</summary>
        void StepGates()
        {
            bool shut = GatesShut;
            if (shut != _gatesWereShut)
            {
                _gatesWereShut = shut;
                Touch();
                if (Raid != null) { Raid.Fields.Clear(); Raid.Chase.Clear(); }
                _breachFields.Clear();
                if (shut && Raid != null && !Raid.Over && Blds.Any(b => b.T == "road" && IsGateway(b.X, b.Y))) Events.Add("The gates are shut against the raiders.");
            }
            if (Raid != null && !Raid.Over) return;
            foreach (var k in BarrierDamage.Keys.ToList())
            {
                BarrierDamage[k] -= 1;
                if (BarrierDamage[k] <= 0) BarrierDamage.Remove(k);
            }
        }

        // ---------- breaching ----------

        public const int BreachCost = 8;   // a barrier counts as this many steps on the way in
        readonly Dictionary<Building, int[]> _breachFields = new Dictionary<Building, int[]>();

        /// <summary>Steps to the building when barriers may be hacked through (each costs BreachCost); other buildings still block.</summary>
        int[] BreachField(Building target)
        {
            if (_breachFields.TryGetValue(target, out var cached)) return cached;
            var dist = new int[N * N];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var open = new SortedSet<(int d, int c)>();
            for (int j = target.Y; j < target.Y + target.Sz; j++) for (int i = target.X; i < target.X + target.Sz; i++) { dist[j * N + i] = 0; open.Add((0, j * N + i)); }
            while (open.Count > 0)
            {
                var (d, c) = open.Min; open.Remove(open.Min);
                if (d > dist[c]) continue;
                int cx = c % N, cy = c / N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1];
                    if (!Inb(nx, ny) || IsSolid(nx, ny)) continue;
                    var g = Grid[ny, nx];
                    bool barrier = IsBarrier(nx, ny);
                    if (g != null && g.T != "road" && !barrier) continue;
                    if (!barrier && double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny)) && Grid[cy, cx] != target) continue;
                    int nd = d + (barrier ? BreachCost : 1);
                    if (nd >= dist[ny * N + nx]) continue;
                    dist[ny * N + nx] = nd; open.Add((nd, ny * N + nx));
                }
            }
            for (int i = 0; i < dist.Length; i++) if (dist[i] == int.MaxValue) dist[i] = -1;
            _breachFields[target] = dist;
            return dist;
        }
    }
}
