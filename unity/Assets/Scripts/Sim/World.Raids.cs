// Raids: a band of raiders walks in from outside your land, picks a building to plunder, takes what it stores and runs for home.
//
// What the raiders do depends on what you built and where:
//   - They go for buildings that hold something worth taking (RaidLoot): the Town Hall and markets hold gold, silos and farms food and flax,
//     sawmills and lodges wood and logs, quarries stone, mines and barracks ore. Houses and wells hold nothing and are left alone.
//   - They like the nearest loot (outlying buildings are hit first) and avoid buildings your soldiers cover (RaidDanger): soldiers answer
//     within AlertR tiles of their post, so a barracks built beside your silo protects it and one built in a corner does not. The Town Hall
//     is the post when there is no barracks.
//   - Each raider takes one bite (RaidBite) from its target's store and flees back the way it came. A raider cut down on the way home drops
//     the loot back into your store; one that gets away keeps it.
//   - The fighting is real and visible: every soldier is a Fighter with a position and hit points. When raiders come within AlertR of a post its
//     soldiers march out (two tiles a day); infantry close in and hit in melee, archers stop and shoot from up to ArcherRange tiles. Raiders
//     that find a soldier next to them stop and fight back. Soldiers who fall are gone from your army (Soldiers and Archers drop); when the
//     raiders are dead or gone the survivors march home.
// Raiders and fighters are runtime state like the work crews: never saved, gone after a load (what they already took stays taken).
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
        public Res LootRes; public double Carry, WheatLoot;
        public Fighter Opponent; // visual facing while a soldier blocks the raider
        public bool Hurt, Fighting;   // took a hit today / stopped to fight a soldier today (drawn red / swinging)
        /// <summary>Its target is behind a palisade or a shut gate: it is hacking its way in (World.Walls.cs).</summary>
        public bool Breaching;
        public int DeadDay;           // the body stays on the field for a few days
        public readonly List<int> Path = new List<int>();   // the way in, walked back when fleeing
    }

    public enum FighterState : byte { Idle, Engage, Return, Dead }

    /// <summary>One soldier during a raid: marches out from its post, fights, and marches home.</summary>
    public sealed class Fighter
    {
        public int Id, X, Y, Px, Py, DeadDay, ShotX, ShotY;
        public Troop Kind;
        public Building Post;
        public FighterState St;
        public double Hp;
        public Raider Target;
        public bool Hurt, Attacked, Shooting;   // took a hit / swung / loosed an arrow today
    }

    public sealed class RaidReport
    {
        public int Band, Defenders, Killed, Escaped, SoldiersLost, StartDay, PeasantsLost;
        public readonly HashSet<string> Tools = new HashSet<string>();   // what the residents fought with
        public string From = "";
        public RaiderCamp Camp;   // the camp the band came from (null when no camps are set)
        public bool Over, StartAnnounced, EndAnnounced, Alarm;   // Once raised, all garrisons respond for the rest of the raid.
        public readonly Dictionary<Res, int> Stolen = new Dictionary<Res, int>();   // what got away with the raiders
        public readonly List<Raider> Raiders = new List<Raider>();
        public readonly List<Fighter> Fighters = new List<Fighter>();
        // One animation cue per participating worksite, cleared before each combat tick.
        public readonly List<(string Trade, int X, int Y, Raider Target)> MilitiaActions = new();
        internal readonly Dictionary<Building, int[]> Fields = new Dictionary<Building, int[]>();
        internal readonly Dictionary<Raider, int[]> Chase = new Dictionary<Raider, int[]>();   // steps to each raider, near it, refreshed daily

        public int Active => Raiders.Count(r => r.St != RaiderState.Dead && r.St != RaiderState.Gone);

        public string Summary()
        {
            var parts = new List<string>();
            foreach (var kv in Stolen) if (kv.Value > 0) parts.Add($"{kv.Value} {kv.Key.ToString().ToLower()}");
            string loot = parts.Count == 0 ? "nothing" : string.Join(", ", parts);
            string folk = Tools.Count > 0 ? $"Residents fought with their {string.Join(", ", Tools.OrderBy(t => t))}" + (PeasantsLost > 0 ? $" and {PeasantsLost} fell." : ".") : "";
            string defence = Defenders == 0 ? (Tools.Count > 0 ? "You had no soldiers. " + folk : "You had no soldiers to stop them.")
                : $"{Defenders} soldiers stood guard" + (SoldiersLost > 0 ? $" and {SoldiersLost} fell." : ".") + (folk == "" ? "" : " " + folk);
            if (!Over) return $"{Band} raiders are coming from the {From}. {Active} still on the field, {Killed} cut down so far.\nThey have taken: {loot}.";
            return $"{Band} raiders came from the {From}" + (Camp != null ? $" ({Camp.Name})" : "") + $": {Killed} cut down, {Escaped} escaped with loot. {defence}\nThey made off with: {loot}.";
        }
    }

    public partial class World
    {
        public const int RaiderHp = 3, AlertR = 14, ChaseR = 22, ArcherRange = 7, FighterSpeed = 2, BodyDays = 4;
        public const double RaidBite = 14, InfantryHp = 5, ArcherHp = 3, InfantryHit = 1.2, ArcherHit = 0.8, RaiderHit = 0.8;

        /// <summary>The raid in progress, or the last one (null before any).</summary>
        public RaidReport Raid { get; private set; }
        double _militiaWounds;

        /// <summary>When the militia is on (World.MilitiaFights), the people at a work building fight with their trade's tool: (name, damage per worker per day, reach in tiles); far weaker than a soldier (0.5 a day), and they get hurt more.</summary>
        public static (string tool, double dmg, int reach)? MilitiaTool(string type) => type switch
        {
            "farm" => ("pitchforks", 0.05, 4),
            "hunter" => ("bows", 0.08, 8),
            "wood" => ("axes", 0.06, 4),
            "forester" => ("billhooks", 0.04, 4),
            "sawmill" => ("saws and axes", 0.05, 4),
            "quarry" => ("hammers", 0.07, 4),
            "mine" => ("picks", 0.07, 4),
            _ => null,
        };

        /// <summary>How much the working residents around this building would add to its defence when the militia fights (soldier equivalents).</summary>
        public double MilitiaPower(Building b)
        {
            if (!MilitiaFights) return 0;
            double power = 0;
            foreach (var w in Blds)
            {
                var t = MilitiaTool(w.T); if (t == null || w.Workers <= 0) continue;
                int d = Math.Max(0, Math.Max(Math.Abs(w.X - b.X), Math.Abs(w.Y - b.Y)) - 1);
                if (d <= t.Value.reach) power += w.Workers * t.Value.dmg * 2;
            }
            return power;
        }

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
                if (d <= AlertR) power += inf + arc;
            }
            return Math.Min(1, (power + MilitiaPower(b)) / 6.0);
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
        public RaidReport BeginRaid(int raiders, RaiderCamp camp = null)
        {
            camp ??= PickRaidCamp();
            var r = new RaidReport { Band = raiders, Defenders = Soldiers, StartDay = Day, Camp = camp };
            Raid = r; _militiaWounds = 0;
            int side = camp != null ? CampSide(camp) : (int)Math.Floor(Noise.Hash2(Day, Seed, 91) * 4);
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
            SpawnFighters(r);
            return r;
        }

        /// <summary>Every soldier becomes a Fighter waiting inside its barracks (or the Town Hall).</summary>
        void SpawnFighters(RaidReport r)
        {
            int id = 0;
            foreach (var (post, inf, arc) in Garrisons())
            {
                var (sx, sy) = StandBeside(post);
                for (int i = 0; i < inf + arc; i++)
                {
                    var kind = i < inf ? Troop.Infantry : Troop.Archer;
                    r.Fighters.Add(new Fighter { Id = id++, Kind = kind, Post = post, X = sx, Y = sy, Px = sx, Py = sy, St = FighterState.Idle, Hp = kind == Troop.Archer ? ArcherHp : InfantryHp });
                }
            }
        }

        /// <summary>An open tile next to a building, where its soldiers come out (nearest the map centre first).</summary>
        (int x, int y) StandBeside(Building b)
        {
            for (int rad = 1; rad <= 3; rad++)
                for (int dy = -rad; dy <= b.Sz - 1 + rad; dy++) for (int dx = -rad; dx <= b.Sz - 1 + rad; dx++)
                {
                    int x = b.X + dx, y = b.Y + dy;
                    if (Inb(x, y) && Grid[y, x] == null && !IsSolid(x, y)) return (x, y);
                }
            return (b.X, b.Y);
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
                    if (IsShutGate(nx, ny)) continue;   // World.Walls.cs
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny)) && !(Grid[cy, cx] == target)) continue;
                    if (IsSolid(nx, ny)) continue;
                    dist[ny * N + nx] = dist[c] + 1; queue.Enqueue(ny * N + nx);
                }
            }
            r.Fields[target] = dist;
            return dist;
        }

        /// <summary>The building a raider at (x, y) would go for now: best loot for its distance and danger, among those it can reach.</summary>
        public Building RaidTarget(int x, int y) => RaidTarget(x, y, out _);

        /// <summary>As above; when nothing worth taking can be reached on foot (the town is walled and its gates are shut), the best target that
        /// can be reached by breaking through a palisade or gate, with `breach` set: the raiders do not simply give up.</summary>
        public Building RaidTarget(int x, int y, out bool breach)
        {
            breach = false;
            var r = Raid ?? new RaidReport();
            var candidates = Blds.Where(b => RaidScore(b, x, y) > 0).OrderByDescending(b => RaidScore(b, x, y)).ThenBy(b => b.X).ThenBy(b => b.Y).Take(6).ToList();
            foreach (var b in candidates)
                if (RaidField(r, b)[y * N + x] >= 0) return b;
            foreach (var b in candidates)
                if (BreachField(b)[y * N + x] >= 0) { breach = true; return b; }
            return null;
        }

        void StepRaid()
        {
            var r = Raid;
            if (r == null) return;
            r.MilitiaActions.Clear();
            if (r.Over && r.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead)) return;
            if (!r.Over) StepRaiders(r);
            StepFighters(r);
            if (!r.Over && r.Raiders.All(a => a.St == RaiderState.Dead || a.St == RaiderState.Gone)) r.Over = true;
            if (r.Over && r.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead)) { r.Fields.Clear(); r.Chase.Clear(); }
        }

        static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        void StepRaiders(RaidReport r)
        {
            int days = Day - r.StartDay;
            foreach (var a in r.Raiders)
            {
                if (a.St == RaiderState.Dead || a.St == RaiderState.Gone) continue;
                a.Px = a.X; a.Py = a.Y; a.Hurt = false; a.Fighting = false; a.Opponent = null;
                if (a.St == RaiderState.Waiting)
                {
                    if (days < a.Delay) continue;
                    a.Target = RaidTarget(a.X, a.Y, out a.Breaching);
                    if (a.Target == null) { a.St = RaiderState.Gone; continue; }
                    a.Path.Add(a.Y * N + a.X); a.St = RaiderState.Approach;
                }
                else if (a.St == RaiderState.Approach)
                {
                    if (a.Target == null || !Blds.Contains(a.Target)) { a.Target = RaidTarget(a.X, a.Y, out a.Breaching); if (a.Target == null) { a.St = RaiderState.Flee; continue; } }
                    // on foot if it can be walked to now; otherwise through the palisade or a shut gate (World.Walls.cs)
                    var dist = RaidField(r, a.Target);
                    a.Breaching = dist[a.Y * N + a.X] < 0;
                    if (a.Breaching) dist = BreachField(a.Target);
                    int d = dist[a.Y * N + a.X];
                    if (d < 0) { a.St = RaiderState.Flee; continue; }   // no way in at all
                    if (d <= 1) { a.St = RaiderState.Loot; continue; }
                    // a soldier right beside the raider (no palisade between): stop and fight instead of walking on
                    a.Opponent = r.Fighters.FirstOrDefault(f => f.St != FighterState.Dead && f.St != FighterState.Idle && MeleeReach(f.X, f.Y, a.X, a.Y));
                    if (a.Opponent != null) { a.Fighting = true; continue; }
                    int best = -1, bestD = d;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = a.X + Nb[k, 0], ny = a.Y + Nb[k, 1];
                        if (!Inb(nx, ny)) continue;
                        int nd = dist[ny * N + nx];
                        if (nd >= 0 && nd < bestD) { best = ny * N + nx; bestD = nd; }
                    }
                    if (best < 0) { a.St = RaiderState.Flee; continue; }   // cut off
                    if (IsBarrier(best % N, best / N)) { DamageBarrier(best % N, best / N, 1); a.Fighting = true; continue; }   // hack at it
                    a.X = best % N; a.Y = best / N; a.Path.Add(best);
                }
                else if (a.St == RaiderState.Loot)
                {
                    if (a.Target != null && Blds.Contains(a.Target))
                    {
                        var (res, stock) = RaidLoot(a.Target);
                        double take = Math.Min(stock, RaidBite);
                        if (res == Res.Food)
                        {
                            _freshMeat = FreshMeatStored - FreshMeatShare(take);
                            a.WheatLoot = WheatShare(take);
                            _wheat = WheatStored - a.WheatLoot;
                        }
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
        }

        /// <summary>Civilians are the people in homes, at work or on the road. A raider this close to any of them raises the alarm.</summary>
        public const int AlarmR = 5;
        public const int WatchtowerRange = 24;

        public static bool WithinWatchtowerRange(int dx, int dy) =>
            dx * dx + dy * dy <= WatchtowerRange * WatchtowerRange;

        bool NearCivilian(Raider a)
        {
            foreach (var b in Blds)
            {
                if (b.T != "house" && b.T != "hall" && b.Workers <= 0) continue;
                if (Reach(b, a.X, a.Y) <= AlarmR) return true;
                foreach (var w in b.Crew) if (w.St != CrewState.Home && w.St != CrewState.Down && Cheb(w.X, w.Y, a.X, a.Y) <= AlarmR) return true;
            }
            foreach (var v in Folk) if (v.St != FolkState.Home && Cheb(v.X, v.Y, a.X, a.Y) <= AlarmR) return true;
            return false;
        }

        static bool RaiderLive(Raider a) => a.St == RaiderState.Approach || a.St == RaiderState.Loot || a.St == RaiderState.Flee;

        bool FighterPass(int x, int y) => Inb(x, y) && !IsSolid(x, y) && (Grid[y, x] == null || Grid[y, x].T == "road") && !IsShutGate(x, y);   // World.Walls.cs

        /// <summary>Steps from every tile within reach of a raider to that raider (BFS over ground soldiers can cross), -1 elsewhere.</summary>
        int[] ChaseField(RaidReport r, Raider a, HashSet<int> responders)
        {
            if (!r.Chase.TryGetValue(a, out var dist)) { dist = new int[N * N]; r.Chase[a] = dist; }
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            dist[a.Y * N + a.X] = 0; queue.Enqueue(a.Y * N + a.X);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(), cx = c % N, cy = c / N;
                if (responders != null)
                {
                    responders.Remove(c);
                    if (responders.Count == 0) break;
                }
                else if (dist[c] >= ChaseR + 4) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Nb[k, 0], ny = cy + Nb[k, 1];
                    if (!Inb(nx, ny) || dist[ny * N + nx] >= 0 || !FighterPass(nx, ny)) continue;
                    if (double.IsPositiveInfinity(WalkCost(cx, cy, nx, ny))) continue;
                    dist[ny * N + nx] = dist[c] + 1; queue.Enqueue(ny * N + nx);
                }
            }
            return dist;
        }

        /// <summary>Move one step to the neighbour that is one nearer in the field. False when there is none.</summary>
        static bool StepDown(Fighter f, int[] dist)
        {
            int d = dist[f.Y * N + f.X];
            if (d <= 0) return false;
            for (int k = 0; k < 4; k++)
            {
                int nx = f.X + Nb[k, 0], ny = f.Y + Nb[k, 1];
                if (Inb(nx, ny) && dist[ny * N + nx] == d - 1) { f.X = nx; f.Y = ny; return true; }
            }
            return false;
        }

        /// <summary>The soldiers' day: pick a raider, march (2 tiles), then swing or shoot; raiders next to a soldier hit back; the fallen are removed.</summary>
        void StepFighters(RaidReport r)
        {
            var live = r.Raiders.Where(RaiderLive).ToList();
            if (!r.Alarm && live.Count > 0)
            {
                bool sighted = Blds.Any(b => b.T == "watchtower" &&
                    live.Any(a => WithinWatchtowerRange(a.X - b.X, a.Y - b.Y)));
                if (sighted || live.Any(NearCivilian))
                {
                    r.Alarm = true;
                    Events.Add(sighted ? "A watchtower spotted raiders and sounded the town alarm!" :
                        "Raiders are among your people! The barracks sounds the alarm and the soldiers march out.");
                }
            }
            var hitRaider = new Dictionary<Raider, double>();
            var hitFighter = new Dictionary<Fighter, double>();

            foreach (var f in r.Fighters)
            {
                if (f.St == FighterState.Dead) continue;
                f.Px = f.X; f.Py = f.Y; f.Hurt = f.Attacked = f.Shooting = false;
                // pick a target: the nearest raider (those carrying loot count as nearer) inside the alert area of the post, or the current one while it stays in reach
                if (f.Target != null && (!RaiderLive(f.Target) || (!r.Alarm && Cheb(f.Post.X, f.Post.Y, f.Target.X, f.Target.Y) > ChaseR))) f.Target = null;
                if (f.Target == null && live.Count > 0)
                {
                    int radius = r.Alarm ? int.MaxValue : f.St == FighterState.Idle ? AlertR : ChaseR;
                    Raider best = null; double bestScore = double.MaxValue;
                    foreach (var a in live)
                    {
                        int d = Cheb(f.Post.X, f.Post.Y, a.X, a.Y); if (d > radius) continue;
                        double score = Cheb(f.X, f.Y, a.X, a.Y) - (a.Carry > 0 ? 6 : 0);
                        if (score < bestScore || (score == bestScore && best != null && a.Id < best.Id)) { best = a; bestScore = score; }
                    }
                    f.Target = best;
                }
                if (f.Target == null)
                {
                    if (f.St == FighterState.Engage) f.St = FighterState.Return;
                }
                else if (f.St == FighterState.Idle || f.St == FighterState.Return) f.St = FighterState.Engage;
            }

            var chase = new Dictionary<Raider, int[]>();
            int[] FieldOf(Raider a)
            {
                if (chase.TryGetValue(a, out var field)) return field;
                // At an alarm, search only until the responding soldiers can all follow the route.
                var responders = r.Alarm ? new HashSet<int>(r.Fighters
                    .Where(f => f.St == FighterState.Engage && f.Target == a)
                    .Select(f => Tk(f.X, f.Y))) : null;
                return chase[a] = ChaseField(r, a, responders);
            }

            foreach (var f in r.Fighters)
            {
                if (f.St == FighterState.Engage)
                {
                    var t = f.Target;
                    int want = f.Kind == Troop.Archer ? ArcherRange - 1 : 1;
                    var field = FieldOf(t);
                    for (int s = 0; s < FighterSpeed && Cheb(f.X, f.Y, t.X, t.Y) > want; s++) if (!StepDown(f, field)) break;
                    int range = f.Kind == Troop.Archer ? ArcherRange : 1;
                    // a sword needs to reach (no palisade between, World.Walls.cs); arrows arc over the palisade
                    if (Cheb(f.X, f.Y, t.X, t.Y) <= range && (f.Kind == Troop.Archer || MeleeReach(f.X, f.Y, t.X, t.Y)))
                    {
                        hitRaider[t] = hitRaider.GetValueOrDefault(t) + (f.Kind == Troop.Archer ? ArcherHit : InfantryHit);
                        f.Attacked = true;
                        if (f.Kind == Troop.Archer) { f.Shooting = true; f.ShotX = t.X; f.ShotY = t.Y; }
                    }
                }
                else if (f.St == FighterState.Return)
                {
                    var home = RaidField(r, f.Post);
                    for (int s = 0; s < FighterSpeed; s++) { if (home[f.Y * N + f.X] <= 1) break; if (!StepDown(f, home)) break; }
                    if (home[f.Y * N + f.X] <= 1) { var (sx, sy) = StandBeside(f.Post); f.X = sx; f.Y = sy; f.St = FighterState.Idle; }
                }
            }

            // raiders stopped beside a soldier hit the nearest one back
            foreach (var a in live.Where(a => a.Fighting))
            {
                Fighter near = null; int nd = int.MaxValue;
                foreach (var f in r.Fighters)
                {
                    if (f.St == FighterState.Dead || f.St == FighterState.Idle) continue;
                    int d = Cheb(f.X, f.Y, a.X, a.Y); if (MeleeReach(f.X, f.Y, a.X, a.Y) && (d < nd || (d == nd && f.Id < near.Id))) { near = f; nd = d; }
                }
                if (near != null) hitFighter[near] = hitFighter.GetValueOrDefault(near) + RaiderHit;
            }

            if (MilitiaFights) MilitiaFight(r);
            foreach (var kv in hitRaider) { kv.Key.Hp -= kv.Value; kv.Key.Hurt = true; }
            foreach (var kv in hitFighter) { kv.Key.Hp -= kv.Value; kv.Key.Hurt = true; }

            foreach (var a in r.Raiders)
                if (a.Hp <= 0 && RaiderLive(a))
                {
                    if (a.Carry > 0)
                    {
                        if (a.LootRes == Res.Food) _wheat = WheatStored + a.WheatLoot;
                        ResAmt[a.LootRes] += a.Carry; a.Carry = 0; a.WheatLoot = 0;
                    }
                    a.St = RaiderState.Dead; a.DeadDay = Day; r.Killed++;
                }
            foreach (var f in r.Fighters)
                if (f.Hp <= 0 && f.St != FighterState.Dead)
                {
                    f.St = FighterState.Dead; f.DeadDay = Day; f.Target = null;
                    if (Soldiers > 0) Soldiers--;
                    if (f.Kind == Troop.Archer && Archers > 0) Archers--;
                    r.SoldiersLost++;
                }
            r.Fighters.RemoveAll(f => f.St == FighterState.Dead && Day - f.DeadDay > BodyDays);
        }

        // Put the cosmetic defender on open ground just outside the participating building.
        (int x, int y) MilitiaPosition(Building b, Raider target)
        {
            int x = -1, y = -1, best = int.MaxValue;
            for (int j = b.Y - 1; j <= b.Y + b.Sz; j++)
                for (int i = b.X - 1; i <= b.X + b.Sz; i++)
                {
                    if (i > b.X - 1 && i < b.X + b.Sz && j > b.Y - 1 && j < b.Y + b.Sz) continue;
                    if (!Inb(i, j) || IsSolid(i, j) || (Grid[j, i] != null && Grid[j, i].T != "road")) continue;
                    int distance = Cheb(i, j, target.X, target.Y);
                    if (distance < best) { x = i; y = j; best = distance; }
                }
            return (x, y);
        }

        /// <summary>Workers fight raiders in reach with their tools, and raiders at the door hurt them back.</summary>
        void MilitiaFight(RaidReport r)
        {
            var active = r.Raiders.Where(a => a.St == RaiderState.Approach || a.St == RaiderState.Loot || a.St == RaiderState.Flee).ToList();
            if (active.Count == 0) return;
            foreach (var b in Blds)
            {
                var t = MilitiaTool(b.T); if (t == null || b.Workers <= 0) continue;
                var near = active.Where(a => Reach(b, a.X, a.Y) <= t.Value.reach).ToList();
                if (near.Count == 0) continue;
                double damage = b.Workers * t.Value.dmg / near.Count;
                foreach (var a in near) { a.Hp -= damage; a.Hurt = true; }
                var target = near.OrderBy(a => Reach(b, a.X, a.Y)).ThenBy(a => a.Id).First();
                var (x, y) = MilitiaPosition(b, target);
                if (Inb(x, y)) r.MilitiaActions.Add((b.T, x, y, target));
                r.Tools.Add(t.Value.tool);
                _militiaWounds += 0.08 * near.Count * Math.Min(1, b.Workers / 2.0);
            }
            while (_militiaWounds >= 1 && Peasants > 1) { _militiaWounds -= 1; Peasants--; r.PeasantsLost++; }
            if (Peasants <= 1) _militiaWounds = 0;
        }

    }
}
