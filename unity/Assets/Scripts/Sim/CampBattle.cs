// Invading a raider camp: the camp opens as a small 32 x 32 map with 1 x 1 raider tents and 1 x 1 supply piles (no walls), you plan the
// landing (from which tiles on the edge of the map, how many infantry and archers from each), confirm, and the fight plays out tick by tick.
// When the raiders are all dead the survivors carry off as many supplies as they can; the settlement gets them when the army is home
// (World.Camps.cs). Pure C# like the rest of the simulation; the HUD draws it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public enum CampTile : byte { Open, Tree, Rock }

    public sealed class SupplyPile { public int X, Y; public Res Res; public int Amount; }

    public sealed class CampMap
    {
        public const int Size = 32;
        public readonly CampTile[,] Ground = new CampTile[Size, Size];   // [y, x]
        public readonly List<(int x, int y)> Tents = new List<(int x, int y)>();
        public readonly List<SupplyPile> Piles = new List<SupplyPile>();

        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static bool IsEdge(int x, int y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
        public bool HasTent(int x, int y) { foreach (var t in Tents) if (t.x == x && t.y == y) return true; return false; }
        public SupplyPile PileAt(int x, int y) { foreach (var p in Piles) if (p.X == x && p.Y == y && p.Amount > 0) return p; return null; }
        /// <summary>Soldiers walk on open ground; trees, rocks and tents are in the way.</summary>
        public bool Passable(int x, int y) => Inb(x, y) && Ground[y, x] == CampTile.Open && !HasTent(x, y);

        static readonly Res[] Pool = { Res.Food, Res.Wood, Res.Logs, Res.Stone, Res.Ore, Res.Gold, Res.Flax };

        public static CampMap Generate(RaiderCamp camp)
        {
            var m = new CampMap();
            int s = camp.Seed;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    double h = Noise.Hash2(x, y, s + 1);
                    int cheb = Math.Max(Math.Abs(x - 16), Math.Abs(y - 16));
                    bool border = x < 2 || y < 2 || x >= Size - 2 || y >= Size - 2;   // the landing edge is always open ground
                    if (border) continue;
                    if (cheb > 10) m.Ground[y, x] = h < 0.10 ? CampTile.Tree : h < 0.13 ? CampTile.Rock : CampTile.Open;
                    else if (cheb > 6 && h < 0.04) m.Ground[y, x] = CampTile.Tree;
                }
            int tents = 3 + camp.Strength;
            for (int a = 0; a < 400 && m.Tents.Count < tents; a++)
            {
                int x = 16 + (int)((Noise.Hash2(a, 1, s + 2) - 0.5) * 16), y = 16 + (int)((Noise.Hash2(a, 2, s + 2) - 0.5) * 16);
                if (!Inb(x, y) || IsEdge(x, y) || m.Ground[y, x] != CampTile.Open) continue;
                bool crowded = false;
                foreach (var t in m.Tents) if (Math.Max(Math.Abs(t.x - x), Math.Abs(t.y - y)) < 2) { crowded = true; break; }
                if (!crowded) m.Tents.Add((x, y));
            }
            int piles = 2 + camp.Strength;
            for (int a = 0; a < 400 && m.Piles.Count < piles; a++)
            {
                int x = 16 + (int)((Noise.Hash2(a, 3, s + 3) - 0.5) * 14), y = 16 + (int)((Noise.Hash2(a, 4, s + 3) - 0.5) * 14);
                if (!Inb(x, y) || IsEdge(x, y) || m.Ground[y, x] != CampTile.Open || m.HasTent(x, y) || m.PileAt(x, y) != null) continue;
                var res = Pool[(int)(Noise.Hash2(a, 5, s + 3) * Pool.Length)];
                int amount = 10 + 6 * camp.Strength + (int)(Noise.Hash2(a, 6, s + 3) * 12);
                m.Piles.Add(new SupplyPile { X = x, Y = y, Res = res, Amount = amount });
            }
            return m;
        }
    }

    /// <summary>One landing: a tile on the edge of the camp map and the troops that come ashore there.</summary>
    public sealed class BattleGroup { public int X, Y, Infantry, Archers; public int Total => Infantry + Archers; }

    public enum BKind : byte { Infantry, Archer, Raider, RaiderArcher }

    public sealed class BUnit
    {
        public int Id, X, Y, Px, Py, ShotX, ShotY, DeadTick;
        public BKind Kind;
        public bool Attacker, Alive = true, Hurt, Attacked, Shooting;
        public double Hp, Hit;
        public int Range = 1;
        public bool Ranged => Range > 1;
    }

    public sealed class CampBattle
    {
        public const double InfantryHp = 5, ArcherHp = 3, RaiderHp = 3, RaiderArcherHp = 2.5, InfantryHit = 1.2, ArcherHit = 0.8, RaiderHit = 0.8, RaiderArcherHit = 0.6;
        public const int ArcherRange = 7, RaiderArcherRange = 6, AlertR = 6, MaxTicks = 500, InfantryCarry = 8, ArcherCarry = 5, BodyTicks = 12;

        public readonly RaiderCamp Camp;
        public readonly CampMap Map;
        public readonly List<BUnit> Units = new List<BUnit>();
        public readonly List<BattleGroup> Groups;
        public int Tick, SentInfantry, SentArchers, LostInfantry, LostArchers, RaidersTotal, RaidersKilled;
        public bool Over, Victory, Retreated, Alarm, Settled;
        public readonly Dictionary<Res, int> Loot = new Dictionary<Res, int>();

        public CampBattle(RaiderCamp camp, IList<BattleGroup> groups)
        {
            Camp = camp; Map = CampMap.Generate(camp);
            Groups = groups.Where(g => g.Total > 0).Select(g => new BattleGroup { X = g.X, Y = g.Y, Infantry = g.Infantry, Archers = g.Archers }).ToList();
            int id = 0;
            foreach (var g in Groups)
            {
                var spots = FreeSpots(g.X, g.Y, g.Total);
                for (int i = 0; i < g.Total; i++)
                {
                    bool inf = i < g.Infantry;
                    var (x, y) = spots[i % spots.Count];
                    Units.Add(new BUnit { Id = id++, Attacker = true, Kind = inf ? BKind.Infantry : BKind.Archer, X = x, Y = y, Px = x, Py = y,
                        Hp = inf ? InfantryHp : ArcherHp, Hit = inf ? InfantryHit : ArcherHit, Range = inf ? 1 : ArcherRange });
                }
                SentInfantry += g.Infantry; SentArchers += g.Archers;
            }
            int raiders = 3 + 2 * camp.Strength;
            for (int i = 0; i < raiders; i++)
            {
                (int x, int y) tent = Map.Tents.Count > 0 ? Map.Tents[i % Map.Tents.Count] : (16, 16);
                var spots = FreeSpots(tent.x, tent.y, 3);
                var (x, y) = spots[(i / Math.Max(1, Map.Tents.Count)) % spots.Count];
                bool archer = i % 3 == 2;
                Units.Add(new BUnit { Id = id++, Attacker = false, Kind = archer ? BKind.RaiderArcher : BKind.Raider, X = x, Y = y, Px = x, Py = y,
                    Hp = archer ? RaiderArcherHp : RaiderHp, Hit = archer ? RaiderArcherHit : RaiderHit, Range = archer ? RaiderArcherRange : 1 });
            }
            RaidersTotal = raiders;
        }

        /// <summary>Open tiles next to (x, y), nearest first, for placing units (the tile itself first if open).</summary>
        List<(int x, int y)> FreeSpots(int x, int y, int need)
        {
            var list = new List<(int x, int y)>();
            for (int rad = 0; rad < 6 && list.Count < Math.Max(need, 1); rad++)
                for (int dy = -rad; dy <= rad; dy++) for (int dx = -rad; dx <= rad; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != rad) continue;
                    int nx = x + dx, ny = y + dy;
                    if (Map.Passable(nx, ny) && Map.PileAt(nx, ny) == null) list.Add((nx, ny));
                }
            if (list.Count == 0) list.Add((x, y));
            return list;
        }

        public int AliveAttackers => Units.Count(u => u.Alive && u.Attacker);
        public int AliveRaiders => Units.Count(u => u.Alive && !u.Attacker);
        public int SurvivingInfantry => Units.Count(u => u.Alive && u.Kind == BKind.Infantry);
        public int SurvivingArchers => Units.Count(u => u.Alive && u.Kind == BKind.Archer);

        static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        int[] FieldTo(BUnit target)
        {
            var d = new int[CampMap.Size * CampMap.Size];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            var q = new Queue<int>();
            d[target.Y * CampMap.Size + target.X] = 0; q.Enqueue(target.Y * CampMap.Size + target.X);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % CampMap.Size, cy = c / CampMap.Size;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Defs.Nb[k, 0], ny = cy + Defs.Nb[k, 1];
                    if (!CampMap.Inb(nx, ny) || d[ny * CampMap.Size + nx] >= 0 || !Map.Passable(nx, ny)) continue;
                    d[ny * CampMap.Size + nx] = d[c] + 1; q.Enqueue(ny * CampMap.Size + nx);
                }
            }
            return d;
        }

        static bool StepDown(BUnit u, int[] d)
        {
            int cur = d[u.Y * CampMap.Size + u.X];
            if (cur <= 0) return false;
            for (int k = 0; k < 4; k++)
            {
                int nx = u.X + Defs.Nb[k, 0], ny = u.Y + Defs.Nb[k, 1];
                if (CampMap.Inb(nx, ny) && d[ny * CampMap.Size + nx] == cur - 1) { u.X = nx; u.Y = ny; return true; }
            }
            return false;
        }

        /// <summary>One tick: units pick the nearest enemy, close in, and swing or shoot; the fallen are removed; the end of the fight is checked.</summary>
        public void Step()
        {
            if (Over) return;
            Tick++;
            foreach (var u in Units) { if (!u.Alive) continue; u.Px = u.X; u.Py = u.Y; u.Hurt = u.Attacked = u.Shooting = false; }
            var att = Units.Where(u => u.Alive && u.Attacker).ToList();
            var rai = Units.Where(u => u.Alive && !u.Attacker).ToList();
            if (!Alarm && att.Any(a => rai.Any(r => Cheb(a.X, a.Y, r.X, r.Y) <= AlertR))) Alarm = true;
            var fields = new Dictionary<BUnit, int[]>();
            var hits = new Dictionary<BUnit, double>();
            foreach (var u in att.Concat(rai))
            {
                if (!u.Attacker && !Alarm) continue;   // the camp sleeps until the landing is spotted
                var enemies = u.Attacker ? rai : att;
                if (enemies.Count == 0) continue;
                BUnit t = null; int best = int.MaxValue;
                foreach (var e in enemies) { int d = Cheb(u.X, u.Y, e.X, e.Y); if (d < best || (d == best && e.Id < t.Id)) { t = e; best = d; } }
                int want = u.Ranged ? u.Range - 1 : 1;
                if (best > want)
                {
                    if (!fields.TryGetValue(t, out var f)) fields[t] = f = FieldTo(t);
                    StepDown(u, f);
                }
                if (Cheb(u.X, u.Y, t.X, t.Y) <= u.Range)
                {
                    hits[t] = hits.GetValueOrDefault(t) + u.Hit; u.Attacked = true;
                    if (u.Ranged) { u.Shooting = true; u.ShotX = t.X; u.ShotY = t.Y; }
                }
            }
            foreach (var kv in hits)
            {
                kv.Key.Hp -= kv.Value; kv.Key.Hurt = true;
                if (!kv.Key.Attacker) Alarm = true;
            }
            foreach (var u in Units)
                if (u.Alive && u.Hp <= 0)
                {
                    u.Alive = false; u.DeadTick = Tick;
                    if (!u.Attacker) RaidersKilled++;
                    else if (u.Kind == BKind.Infantry) LostInfantry++; else LostArchers++;
                }
            if (AliveRaiders == 0) { Over = true; Victory = true; TakeLoot(); }
            else if (AliveAttackers == 0) Over = true;
            else if (Tick >= MaxTicks) { Over = true; Retreated = true; }
        }

        /// <summary>Pull out. The survivors go home with nothing.</summary>
        public void Retreat() { if (!Over) { Over = true; Retreated = true; } }

        /// <summary>The survivors carry as much as they can of what is in the piles, in proportion to each pile.</summary>
        void TakeLoot()
        {
            int capacity = SurvivingInfantry * InfantryCarry + SurvivingArchers * ArcherCarry;
            int total = Map.Piles.Sum(p => p.Amount);
            if (total <= 0 || capacity <= 0) return;
            double fraction = Math.Min(1.0, capacity / (double)total);
            foreach (var p in Map.Piles)
            {
                int take = (int)Math.Floor(p.Amount * fraction);
                if (take <= 0) continue;
                p.Amount -= take;
                Loot[p.Res] = Loot.GetValueOrDefault(p.Res) + take;
            }
        }

        public int LootTotal => Loot.Values.Sum();

        public string Summary()
        {
            string outcome = Victory ? "Victory: the camp is destroyed." : Retreated ? "The army pulled out." : Over ? "Defeat: the army was wiped out." : "The fight goes on.";
            string loot = Loot.Count == 0 ? "nothing" : string.Join(", ", Loot.Where(k => k.Value > 0).Select(k => $"{k.Value} {k.Key.ToString().ToLower()}"));
            return $"{outcome}\n{Camp.Name}: {RaidersKilled} of {RaidersTotal} raiders killed.\n" +
                   $"You sent {SentInfantry} infantry and {SentArchers} archers; {LostInfantry} infantry and {LostArchers} archers fell, {SurvivingInfantry} infantry and {SurvivingArchers} archers survive.\n" +
                   (Victory ? $"They carry home: {loot}." : "");
        }
    }
}
