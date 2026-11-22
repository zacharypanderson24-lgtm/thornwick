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

    /// <summary>How big a camp of each strength is: a strength 1 camp is a couple of tents round one fire, a strength 5 camp a sprawling
    /// war camp. Reach is how far from the warlord's tent its fires may stand.</summary>
    public readonly struct CampSize
    {
        public readonly int Fires, Tents, Racks, Piles, Raiders, Brutes, Reach;
        CampSize(int fires, int tents, int racks, int piles, int raiders, int brutes, int reach) { Fires = fires; Tents = tents; Racks = racks; Piles = piles; Raiders = raiders; Brutes = brutes; Reach = reach; }
        static readonly CampSize[] Table =
        {
            new CampSize(1, 2, 1, 1, 3, 1, 4),
            new CampSize(2, 5, 1, 3, 6, 1, 5),
            new CampSize(3, 8, 2, 5, 10, 2, 6),
            new CampSize(4, 12, 3, 7, 15, 3, 7),
            new CampSize(6, 20, 5, 10, 24, 4, 9),
        };
        public static CampSize Of(int strength) => Table[Math.Max(1, Math.Min(5, strength)) - 1];
    }

    public sealed class CampMap
    {
        public const int Size = 32;
        public readonly CampTile[,] Ground = new CampTile[Size, Size];   // [y, x]: what the terrain has on each tile (trees, rocks and mountains block)
        public readonly bool[,] Buildable = new bool[Size, Size];        // open, level ground: where tents and the rest can stand
        public readonly List<(int x, int y)> Tents = new List<(int x, int y)>();
        public readonly List<(int x, int y)> Fires = new List<(int x, int y)>();   // campfires, with tents round them
        public readonly List<(int x, int y)> Racks = new List<(int x, int y)>();   // weapon racks
        public (int x, int y) Chief;                                                // the warlord's tent
        public readonly List<SupplyPile> Piles = new List<SupplyPile>();

        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static bool IsEdge(int x, int y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
        static bool Has(List<(int x, int y)> list, int x, int y) { foreach (var t in list) if (t.x == x && t.y == y) return true; return false; }
        public bool HasTent(int x, int y) => Has(Tents, x, y) || (Chief.x == x && Chief.y == y);
        /// <summary>Anything built here that soldiers cannot walk through: tents, the warlord's tent, fires and racks.</summary>
        public bool HasStructure(int x, int y) => HasTent(x, y) || Has(Fires, x, y) || Has(Racks, x, y);
        public SupplyPile PileAt(int x, int y) { foreach (var p in Piles) if (p.X == x && p.Y == y && p.Amount > 0) return p; return null; }
        /// <summary>Soldiers walk on open ground; trees, rocks, mountains and the camp's structures are in the way.</summary>
        public bool Passable(int x, int y) => Inb(x, y) && Ground[y, x] == CampTile.Open && !HasStructure(x, y);

        static readonly Res[] Pool = { Res.Food, Res.Wood, Res.Logs, Res.Stone, Res.Ore, Res.Gold, Res.Flax };

        /// <summary>Would building on (x, y) still leave every open tile beside it reachable from the map's edge? Tents are pitched close
        /// together now, so this keeps them from walling anything (or anyone) in.</summary>
        bool LeavesPath(int bx, int by)
        {
            var seen = new bool[Size, Size];
            var q = new Queue<(int x, int y)>();
            for (int i = 0; i < Size; i++)
                foreach (var (x, y) in new[] { (i, 0), (i, Size - 1), (0, i), (Size - 1, i) })
                    if (!seen[y, x] && (x != bx || y != by) && Passable(x, y)) { seen[y, x] = true; q.Enqueue((x, y)); }
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Defs.Nb[k, 0], ny = cy + Defs.Nb[k, 1];
                    if (!Inb(nx, ny) || seen[ny, nx] || (nx == bx && ny == by) || !Passable(nx, ny)) continue;
                    seen[ny, nx] = true; q.Enqueue((nx, ny));
                }
            }
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = bx + dx, ny = by + dy;
                    if ((dx != 0 || dy != 0) && Passable(nx, ny) && !seen[ny, nx]) return false;
                }
            // the warlord's tent and every tent right beside this spot must keep an open, connected tile next to it
            bool Reachable(int tx, int ty)
            {
                for (int k = 0; k < 4; k++)
                {
                    int nx = tx + Defs.Nb[k, 0], ny = ty + Defs.Nb[k, 1];
                    if (Inb(nx, ny) && (nx != bx || ny != by) && Passable(nx, ny) && seen[ny, nx]) return true;
                }
                return false;
            }
            if (!Reachable(bx, by) || !Reachable(Chief.x, Chief.y)) return false;
            foreach (var t in Tents)
                if (Math.Max(Math.Abs(t.x - bx), Math.Abs(t.y - by)) <= 1 && !Reachable(t.x, t.y)) return false;
            return true;
        }

        /// <summary>Room for a structure at (x, y): level open ground away from the edge, and nothing built within `apart` tiles.</summary>
        bool Free(int x, int y, int apart = 0)
        {
            if (!Inb(x, y) || IsEdge(x, y) || !Buildable[y, x]) return false;
            for (int j = -apart; j <= apart; j++) for (int i = -apart; i <= apart; i++) if (HasStructure(x + i, y + j) || PileAt(x + i, y + j) != null) return false;
            return true;
        }

        /// <summary>Lay the camp out on the generated terrain of a camp world (a 32 x 32 square starting at x0, y0).</summary>
        public static CampMap FromTerrain(World w, int x0, int y0, RaiderCamp camp)
        {
            var m = new CampMap();
            int s = camp.Seed;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int wx = x0 + x, wy = y0 + y;
                    var deco = w.DecoMap[wy, wx];
                    m.Ground[y, x] = w.HasTree(wx, wy) ? CampTile.Tree : (w.IsSolid(wx, wy) || deco == Deco.Rock) ? CampTile.Rock : CampTile.Open;
                    m.Buildable[y, x] = m.Ground[y, x] == CampTile.Open && w.TileSpread(wx, wy) == 0;
                }

            // the warlord's tent: the open ground nearest the middle, with a hoard of gold beside it
            bool found = false;
            for (int rad = 0; rad < 14 && !found; rad++)
                for (int dy = -rad; dy <= rad && !found; dy++) for (int dx = -rad; dx <= rad && !found; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != rad) continue;
                    if (m.Free(16 + dx, 16 + dy, 0)) { m.Chief = (16 + dx, 16 + dy); found = true; }
                }
            if (!found) m.Chief = (16, 16);

            // campfires in a tight ring round it (a bigger camp spreads a little further), each with its tents pitched right beside it
            var size = CampSize.Of(camp.Strength);
            int fires = size.Fires, tentsWanted = size.Tents, perFire = (tentsWanted + fires - 1) / fires, reach = size.Reach;
            var offsets = new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) };
            for (int a = 0; a < 600 && m.Fires.Count < fires; a++)
            {
                double ang = Noise.Hash2(a, 7, s + 21) * Math.PI * 2, rad = 3 + Noise.Hash2(a, 8, s + 21) * (reach - 3);
                int x = m.Chief.x + (int)Math.Round(Math.Cos(ang) * rad), y = m.Chief.y + (int)Math.Round(Math.Sin(ang) * rad);
                if (!m.Free(x, y, 1)) continue;
                bool apart = true;
                foreach (var f in m.Fires) if (Math.Max(Math.Abs(f.x - x), Math.Abs(f.y - y)) < 4) { apart = false; break; }
                if (!apart) continue;
                m.Fires.Add((x, y));
                int placed = 0, turn = (int)(Noise.Hash2(a, 9, s + 22) * 8);
                for (int k = 0; k < offsets.Length && placed < perFire && m.Tents.Count < tentsWanted; k++)
                {
                    var o = offsets[(k + turn) % offsets.Length];
                    if (m.Free(x + o.Item1, y + o.Item2, 0) && m.LeavesPath(x + o.Item1, y + o.Item2)) { m.Tents.Add((x + o.Item1, y + o.Item2)); placed++; }
                }
            }
            // a camp on rough ground may find no room for its ring of fires: give it at least one, wherever there is room
            for (int a = 0; a < 400 && m.Fires.Count == 0; a++)
            {
                int x = m.Chief.x + (int)((Noise.Hash2(a, 15, s + 21) - 0.5) * 2 * reach), y = m.Chief.y + (int)((Noise.Hash2(a, 16, s + 21) - 0.5) * 2 * reach);
                if (m.Free(x, y, 1)) m.Fires.Add((x, y));
            }
            // any tents still owed are pitched near the camp's middle, widening the search only if the ground is cramped
            for (int spread = reach; spread <= 14 && m.Tents.Count < tentsWanted; spread += 3)
                for (int a = 0; a < 500 && m.Tents.Count < tentsWanted; a++)
                {
                    int x = m.Chief.x + (int)Math.Round((Noise.Hash2(a + spread * 1000, 1, s + 2) - 0.5) * 2 * spread), y = m.Chief.y + (int)Math.Round((Noise.Hash2(a + spread * 1000, 2, s + 2) - 0.5) * 2 * spread);
                    if (m.Free(x, y, 0) && m.LeavesPath(x, y)) m.Tents.Add((x, y));
                }
            // weapon racks next to tents
            int racks = size.Racks;
            for (int a = 0; a < 400 && m.Racks.Count < racks && m.Tents.Count > 0; a++)
            {
                var t = m.Tents[(int)(Noise.Hash2(a, 10, s + 23) * m.Tents.Count)];
                int x = t.x + (int)(Noise.Hash2(a, 11, s + 23) * 3) - 1, y = t.y + 1 + (int)(Noise.Hash2(a, 12, s + 23) * 2);
                if (m.Free(x, y, 0) && m.LeavesPath(x, y)) m.Racks.Add((x, y));
            }
            // supplies: the warlord's hoard of gold by his tent, and piles of goods round the camp
            for (int a = 0; a < 200; a++)
            {
                int x = m.Chief.x + (int)(Noise.Hash2(a, 13, s + 24) * 7) - 3, y = m.Chief.y + (int)(Noise.Hash2(a, 14, s + 24) * 7) - 3;
                if (m.Free(x, y, 0)) { m.Piles.Add(new SupplyPile { X = x, Y = y, Res = Res.Gold, Amount = 14 + 5 * camp.Strength }); break; }
            }
            int piles = size.Piles, pileSpread = 2 * (reach + 2);
            for (int a = 0; a < 600 && m.Piles.Count < piles + 1; a++)
            {
                int spread = a < 300 ? pileSpread : 20;   // among the tents, or further out if the ground is cramped
                int x = m.Chief.x + (int)((Noise.Hash2(a, 3, s + 3) - 0.5) * spread), y = m.Chief.y + (int)((Noise.Hash2(a, 4, s + 3) - 0.5) * spread);
                if (!m.Free(x, y, 0)) continue;
                var res = Pool[(int)(Noise.Hash2(a, 5, s + 3) * Pool.Length)];
                int amount = 10 + 6 * camp.Strength + (int)(Noise.Hash2(a, 6, s + 3) * 12);
                m.Piles.Add(new SupplyPile { X = x, Y = y, Res = res, Amount = amount });
            }
            return m;
        }
    }

    /// <summary>One landing: a tile on the edge of the camp map and the troops that come ashore there.</summary>
    public sealed class BattleGroup { public int X, Y, Infantry, Archers; public int Total => Infantry + Archers; }

    public enum BKind : byte { Infantry, Archer, Raider, RaiderArcher, Brute, Chief }

    public sealed class BUnit
    {
        public int Id, X, Y, Px, Py, ShotX, ShotY, DeadTick;
        public BKind Kind;
        public BUnit Target;   // who it is fighting or walking towards (drawn facing it)
        public bool Attacker, Alive = true, Hurt, Attacked, Shooting;
        public double Hp, Hit;
        public int Range = 1;
        public bool Ranged => Range > 1;
    }

    public sealed class CampBattle
    {
        public const double InfantryHp = 5, ArcherHp = 3, RaiderHp = 3, RaiderArcherHp = 2.5, InfantryHit = 1.2, ArcherHit = 0.8, RaiderHit = 0.8, RaiderArcherHit = 0.6;
        /// <summary>Brutes (big, tough, heavy-handed) guard the warlord; the warlord himself is the toughest fighter in the camp.</summary>
        public const double BruteHp = 5, BruteHit = 1.1, ChiefHp = 9, ChiefHit = 1.6;

        /// <summary>Everyone in a camp of this strength: raiders and archers, brutes round the warlord, and the warlord.</summary>
        public static int RaiderCount(int strength) { var c = CampSize.Of(strength); return c.Raiders + c.Brutes + 1; }
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
            Camp = camp; Map = camp.Site.Map;
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
            var size = CampSize.Of(camp.Strength);
            int raiders = size.Raiders, brutes = size.Brutes;
            for (int i = 0; i < raiders; i++)
            {
                (int x, int y) tent = Map.Tents.Count > 0 ? Map.Tents[i % Map.Tents.Count] : (16, 16);
                var spots = FreeSpots(tent.x, tent.y, 3);
                var (x, y) = spots[(i / Math.Max(1, Map.Tents.Count)) % spots.Count];
                bool archer = i % 3 == 2;
                Units.Add(new BUnit { Id = id++, Attacker = false, Kind = archer ? BKind.RaiderArcher : BKind.Raider, X = x, Y = y, Px = x, Py = y,
                    Hp = archer ? RaiderArcherHp : RaiderHp, Hit = archer ? RaiderArcherHit : RaiderHit, Range = archer ? RaiderArcherRange : 1 });
            }
            // the brutes stand guard beside the warlord's tent, and the warlord is at its door
            for (int i = 0; i < brutes; i++)
            {
                var spots = FreeSpots(Map.Chief.x, Map.Chief.y + 1, 4);
                var (x, y) = spots[(i + 1) % spots.Count];
                Units.Add(new BUnit { Id = id++, Attacker = false, Kind = BKind.Brute, X = x, Y = y, Px = x, Py = y, Hp = BruteHp, Hit = BruteHit, Range = 1 });
            }
            {
                var spots = FreeSpots(Map.Chief.x, Map.Chief.y + 1, 1);
                var (x, y) = spots[0];
                Units.Add(new BUnit { Id = id++, Attacker = false, Kind = BKind.Chief, X = x, Y = y, Px = x, Py = y, Hp = ChiefHp, Hit = ChiefHit, Range = 1 });
            }
            RaidersTotal = RaiderCount(camp.Strength);
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
                u.Target = t;
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
