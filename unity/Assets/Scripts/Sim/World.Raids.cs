// Raids: a band of raiders walks in from outside your land, picks a building to plunder, takes what it stores and runs for home.
//
// What the raiders do depends on what you built and where:
//   - They go for buildings that hold something worth taking (RaidLoot): the Town Hall and markets hold gold, silos and farms food and flax,
//     sawmills and lodges wood and logs, quarries stone, mines and barracks ore. Houses and wells hold nothing and are left alone.
//   - They like the nearest loot (outlying buildings are hit first) and avoid buildings your soldiers cover (RaidDanger): infantry fight
//     within MeleeR tiles of a barracks, archers shoot within BowR, so a barracks built beside your silo protects it and one built
//     in a corner does not. The Town Hall garrisons the soldiers when there is no barracks.
//   - Each raider takes one bite (RaidBite) from its target's store and flees back the way it came. A raider cut down on the way home drops
//     the loot back into your store; one that gets away keeps it.
// Raiders are runtime state like the work crews: never saved, gone after a load (what they already took stays taken).
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum RaiderState : byte { Waiting, Approach, Loot, Flee, Dead, Gone }

    public sealed class Raider
    {
        public int Id, Delay, X, Y, Px, Py;
        public RaiderState St;
        public double Hp = World.RaiderHp;
        public Building Target;
        public Res LootRes; public double Carry;
        public readonly List<int> Path = new List<int>();   // the way in, walked back when fleeing
    }

    public sealed class RaidReport
    {
        public int Band, Defenders, Killed, Escaped, SoldiersLost, StartDay;
        public string From = "";
        public bool Over, StartAnnounced, EndAnnounced;
        public readonly Dictionary<Res, int> Stolen = new Dictionary<Res, int>();   // what got away with the raiders
        public readonly List<Raider> Raiders = new List<Raider>();
        internal readonly Dictionary<Building, int[]> Fields = new Dictionary<Building, int[]>();

        public int Active => Raiders.Count(r => r.St != RaiderState.Dead && r.St != RaiderState.Gone);

        public string Summary()
        {
            var parts = new List<string>();
            foreach (var kv in Stolen) if (kv.Value > 0) parts.Add($"{kv.Value} {kv.Key.ToString().ToLower()}");
            string loot = parts.Count == 0 ? "nothing" : string.Join(", ", parts);
            string defence = Defenders == 0 ? "You had no soldiers to stop them."
                : $"{Defenders} soldiers stood guard" + (SoldiersLost > 0 ? $" and {SoldiersLost} fell." : ".");
            if (!Over) return $"{Band} raiders are coming from the {From}. {Active} still on the field, {Killed} cut down so far.\nThey have taken: {loot}.";
            return $"{Band} raiders came from the {From}: {Killed} cut down, {Escaped} escaped with loot. {defence}\nThey made off with: {loot}.";
        }
    }

    public partial class World
    {
        public const int RaiderHp = 3, MeleeR = 5, BowR = 9;
        public const double RaidBite = 14;

        /// <summary>The raid in progress, or the last one (null before any).</summary>
        public RaidReport Raid { get; private set; }
        double _wounds;

        // ---------- what raiders want ----------

        /// <summary>The store of a building that raiders can take from, and how much of it is there.</summary>
        public (Res res, double stock) RaidLoot(Building b)
        {
            switch (b.T)
            {
                case "hall": case "market": return (Res.Gold, ResAmt[Res.Gold]);
                case "silo": return ResAmt[Res.Food] >= ResAmt[Res.Flax] ? (Res.Food, ResAmt[Res.Food]) : (Res.Flax, ResAmt[Res.Flax]);
                case "farm": return (Res.Food, ResAmt[Res.Food]);
                case "sawmill": return (Res.Wood, ResAmt[Res.Wood]);
                case "wood": return (Res.Logs, ResAmt[Res.Logs]);
                case "quarry": return (Res.Stone, ResAmt[Res.Stone]);
                case "mine": case "barracks": return (Res.Ore, ResAmt[Res.Ore]);
                default: return (Res.Gold, 0);
            }
        }

        static double LootWeight(Res r) => r switch { Res.Gold => 3.0, Res.Ore => 2.0, Res.Stone => 1.5, Res.Food => 1.5, Res.Wood => 1.0, Res.Flax => 1.0, _ => 0.7 };

        public double RaidValue(Building b) { var (res, stock) = RaidLoot(b); return LootWeight(res) * Math.Min(stock, RaidBite); }

        /// <summary>Soldiers stationed at each barracks (or the Town Hall when there are none): (building, infantry, archers).</summary>
        public List<(Building b, int inf, int arc)> Garrisons()
        {
            var posts = Blds.Where(b => b.T == "barracks").ToList();
            if (posts.Count == 0) posts = Blds.Where(b => b.T == "hall").ToList();
            var list = new List<(Building, int, int)>();
            for (int i = 0; i < posts.Count; i++)
                list.Add((posts[i], Infantry / posts.Count + (i < Infantry % posts.Count ? 1 : 0), Archers / posts.Count + (i < Archers % posts.Count ? 1 : 0)));
            return list;
        }

        static int Reach(Building b, int x, int y) => Math.Max(Math.Max(b.X - x, x - (b.X + b.Sz - 1)), Math.Max(b.Y - y, y - (b.Y + b.Sz - 1)));

        /// <summary>0..1: how strongly soldiers cover this building (6 soldiers in range is as dangerous as it gets).</summary>
        public double RaidDanger(Building b)
        {
            double power = 0;
            foreach (var (g, inf, arc) in Garrisons())
            {
                int d = Math.Max(0, Math.Max(Math.Abs(g.X - b.X), Math.Abs(g.Y - b.Y)) - 1);
                if (d <= MeleeR) power += inf;
                if (d <= BowR) power += arc;
            }
            return Math.Min(1, power / 6.0);
        }

        double RaidScore(Building b, int fromX, int fromY)
        {
            double value = RaidValue(b);
            if (value <= 0) return 0;
            double dist = Math.Max(Math.Abs(b.X - fromX), Math.Abs(b.Y - fromY));
            return value / (10 + dist) * (1 - 0.7 * RaidDanger(b));
        }

        // ---------- starting and running a raid ----------

        static readonly string[] Sides = { "north", "east", "south", "west" };

        /// <summary>A band arrives from outside your land: one side picked by the day and the town, raiders staggered over a few days.</summary>
        public RaidReport BeginRaid(int raiders)
        {
            var r = new RaidReport { Band = raiders, Defenders = Soldiers, StartDay = Day };
            Raid = r; _wounds = 0;
            int side = (int)Math.Floor(Noise.Hash2(Day, Seed, 91) * 4);
            r.From = Sides[side];
            int minX = N, maxX = 0, minY = N, maxY = 0;
            for (int py = 0; py < ParcelsPerSide; py++) for (int px = 0; px < ParcelsPerSide; px++)
                if (ParcelClaimed(px, py)) { minX = Math.Min(minX, px * ParcelSize); maxX = Math.Max(maxX, (px + 1) * ParcelSize - 1); minY = Math.Min(minY, py * ParcelSize); maxY = Math.Max(maxY, (py + 1) * ParcelSize - 1); }
            if (minX > maxX) { minX = minY = Start; maxX = maxY = Start; }
            int mid = Start + (int)Math.Floor((Noise.Hash2(Day, Seed, 92) - 0.5) * 24);   // where along the edge they come
            for (int i = 0; i < raiders; i++)
            {
                int along = Math.Clamp(mid + (i % 5) * 2 - 4, 1, N - 2);
                int x, y;
                switch (side)
                {
                    case 0: x = along; y = Math.Max(1, minY - 3); break;
                    case 1: x = Math.Min(N - 2, maxX + 3); y = along; break;
                    case 2: x = along; y = Math.Min(N - 2, maxY + 3); break;
                    default: x = Math.Max(1, minX - 3); y = along; break;
                }
                var (sx, sy) = NearestOpen(x, y);
                r.Raiders.Add(new Raider { Id = i, Delay = i / 3, X = sx, Y = sy, Px = sx, Py = sy, St = RaiderState.Waiting });
            }
            return r;
        }

        (int x, int y) NearestOpen(int x, int y)
        {
            for (int rad = 0; rad < 12; rad++)
                for (int dy = -rad; dy <= rad; dy++) for (int dx = -rad; dx <= rad; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (Inb(nx, ny) && Grid[ny, nx] == null && !IsSolid(nx, ny)) return (nx, ny);
                }
            return (x, y);
        }

        /// <summary>Steps from every tile to the building (BFS outwards over ground the raiders can cross), -1 where they cannot.</summary>
        int[] RaidField(RaidReport r, Building target)
        {
            if (r.Fields.TryGetValue(target, out var cached)) return cached;
            var dist = new int[N * N];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            for (int j = target.Y; j < target.Y + target.Sz; j++) for (int i = target.X; i < target.X + target.Sz; i++) { dist[j * N + i] = 0; queue.Enqueue(j * N + i); }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(), cx = c % N, cy = c / N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1];
                    if (!Inb(nx, ny) || dist[ny * N + nx] >= 0) continue;
                    var g = Grid[ny, nx]; if (g != null && g.T != "road") continue;
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny)) && !(Grid[cy, cx] == target)) continue;
                    if (IsSolid(nx, ny)) continue;
                    dist[ny * N + nx] = dist[c] + 1; queue.Enqueue(ny * N + nx);
                }
            }
            r.Fields[target] = dist;
            return dist;
        }

        /// <summary>The building a raider at (x, y) would go for now: best loot for its distance and danger, among those it can reach.</summary>
        public Building RaidTarget(int x, int y)
        {
            var r = Raid ?? new RaidReport();
            var candidates = Blds.Where(b => RaidScore(b, x, y) > 0).OrderByDescending(b => RaidScore(b, x, y)).ThenBy(b => b.X).ThenBy(b => b.Y).Take(6);
            foreach (var b in candidates)
                if (RaidField(r, b)[y * N + x] >= 0) return b;
            return null;
        }

        void StepRaid()
        {
            var r = Raid;
            if (r == null || r.Over) return;
            int days = Day - r.StartDay;
            foreach (var a in r.Raiders)
            {
                if (a.St == RaiderState.Dead || a.St == RaiderState.Gone) continue;
                a.Px = a.X; a.Py = a.Y;
                if (a.St == RaiderState.Waiting)
                {
                    if (days < a.Delay) continue;
                    a.Target = RaidTarget(a.X, a.Y);
                    if (a.Target == null) { a.St = RaiderState.Gone; continue; }
                    a.Path.Add(a.Y * N + a.X); a.St = RaiderState.Approach;
                }
                else if (a.St == RaiderState.Approach)
                {
                    if (a.Target == null || !Blds.Contains(a.Target)) { a.Target = RaidTarget(a.X, a.Y); if (a.Target == null) { a.St = RaiderState.Flee; continue; } }
                    var dist = RaidField(r, a.Target);
                    int d = dist[a.Y * N + a.X];
                    if (d <= 1) { a.St = RaiderState.Loot; continue; }
                    int best = -1;
                    for (int k = 0; k < 4 && best < 0; k++)
                    {
                        int nx = a.X + Nb[k, 0], ny = a.Y + Nb[k, 1];
                        if (Inb(nx, ny) && dist[ny * N + nx] == d - 1) best = ny * N + nx;
                    }
                    if (best < 0) { a.St = RaiderState.Flee; continue; }   // cut off
                    a.X = best % N; a.Y = best / N; a.Path.Add(best);
                }
                else if (a.St == RaiderState.Loot)
                {
                    if (a.Target != null && Blds.Contains(a.Target))
                    {
                        var (res, stock) = RaidLoot(a.Target);
                        double take = Math.Min(stock, RaidBite);
                        ResAmt[res] -= take; a.LootRes = res; a.Carry = take;
                    }
                    a.St = RaiderState.Flee;
                }
                else if (a.St == RaiderState.Flee)
                {
                    if (a.Path.Count <= 1)
                    {
                        a.St = RaiderState.Gone; r.Escaped++;
                        if (a.Carry > 0) { r.Stolen[a.LootRes] = r.Stolen.GetValueOrDefault(a.LootRes) + (int)Math.Round(a.Carry); }
                        continue;
                    }
                    a.Path.RemoveAt(a.Path.Count - 1);
                    int c = a.Path[a.Path.Count - 1]; a.X = c % N; a.Y = c / N;
                }
            }

            Fight(r);

            if (r.Raiders.All(a => a.St == RaiderState.Dead || a.St == RaiderState.Gone))
            {
                r.Over = true; r.Fields.Clear();
            }
        }

        /// <summary>Soldiers fire on raiders in range; raiders at the barracks wound the garrison.</summary>
        void Fight(RaidReport r)
        {
            foreach (var (g, inf, arc) in Garrisons())
            {
                var active = r.Raiders.Where(a => a.St == RaiderState.Approach || a.St == RaiderState.Loot || a.St == RaiderState.Flee).ToList();
                var inBow = active.Where(a => Reach(g, a.X, a.Y) <= BowR).ToList();
                var inMelee = active.Where(a => Reach(g, a.X, a.Y) <= MeleeR).ToList();
                if (inBow.Count > 0 && arc > 0) foreach (var a in inBow) a.Hp -= 0.5 * arc / inBow.Count;
                if (inMelee.Count > 0 && inf > 0) foreach (var a in inMelee) a.Hp -= 0.5 * inf / inMelee.Count;
                if (inMelee.Count > 0 && inf + arc > 0) _wounds += 0.1 * inMelee.Count;
            }
            foreach (var a in r.Raiders)
                if (a.Hp <= 0 && a.St != RaiderState.Dead && a.St != RaiderState.Gone)
                {
                    if (a.Carry > 0) { ResAmt[a.LootRes] += a.Carry; a.Carry = 0; }   // the loot drops where it fell and is carried back
                    a.St = RaiderState.Dead; r.Killed++;
                }
            while (_wounds >= 1 && Soldiers > 0)
            {
                _wounds -= 1;
                if (Infantry > 0) Soldiers--; else { Archers--; Soldiers--; }
                r.SoldiersLost++;
            }
            if (Soldiers == 0) _wounds = 0;
        }
    }
}
