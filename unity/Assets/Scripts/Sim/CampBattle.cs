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
        /// <summary>A raider camp's map is this many tiles across; a settlement's map is bigger the bigger the settlement (N).</summary>
        public const int Size = 32;
        /// <summary>This map's size in tiles (Size for a raider camp).</summary>
        public readonly int N;
        public readonly CampTile[,] Ground;   // [y, x]: what the terrain has on each tile (trees, rocks and mountains block)
        public readonly bool[,] Buildable;    // open, level ground: where tents and the rest can stand
        /// <summary>[y, x]: tiles a settlement's buildings stand on (soldiers cannot walk through them). Raider camps use the lists below.</summary>
        public readonly bool[,] Blocked;
        public readonly List<(int x, int y)> Tents = new List<(int x, int y)>();
        public readonly List<(int x, int y)> Fires = new List<(int x, int y)>();   // campfires, with tents round them
        public readonly List<(int x, int y)> Racks = new List<(int x, int y)>();   // weapon racks
        public (int x, int y) Chief;                                                // the warlord's tent (a settlement: its hall)
        public readonly List<SupplyPile> Piles = new List<SupplyPile>();
        /// <summary>A settlement's barracks (one tile of each): its soldiers muster beside them.</summary>
        public readonly List<(int x, int y)> Barracks = new List<(int x, int y)>();

        public CampMap(int n = Size) { N = n; Ground = new CampTile[n, n]; Buildable = new bool[n, n]; Blocked = new bool[n, n]; }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
        public bool Edge(int x, int y) => x == 0 || y == 0 || x == N - 1 || y == N - 1;
        /// <summary>On a raider camp's 32 x 32 map (kept for the camp code and tests that know its size).</summary>
        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static bool IsEdge(int x, int y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
        static bool Has(List<(int x, int y)> list, int x, int y) { foreach (var t in list) if (t.x == x && t.y == y) return true; return false; }
        public bool HasTent(int x, int y) => Has(Tents, x, y) || (Chief.x == x && Chief.y == y);
        /// <summary>Anything built here that soldiers cannot walk through: tents, the warlord's tent, fires and racks, or a settlement's buildings.</summary>
        public bool HasStructure(int x, int y) => (In(x, y) && Blocked[y, x]) || HasTent(x, y) || Has(Fires, x, y) || Has(Racks, x, y);
        public SupplyPile PileAt(int x, int y) { foreach (var p in Piles) if (p.X == x && p.Y == y && p.Amount > 0) return p; return null; }
        /// <summary>Soldiers walk on open ground; trees, rocks, mountains and the camp's structures are in the way.</summary>
        public bool Passable(int x, int y) => In(x, y) && Ground[y, x] == CampTile.Open && !HasStructure(x, y);

        static readonly Res[] Pool = { Res.Food, Res.Wood, Res.Logs, Res.Stone, Res.Ore, Res.Gold, Res.Flax };

        /// <summary>Would building on (x, y) still leave every open tile beside it reachable from the map's edge? Tents are pitched close
        /// together now, so this keeps them from walling anything (or anyone) in.</summary>
        bool LeavesPath(int bx, int by)
        {
            var seen = new bool[N, N];
            var q = new Queue<(int x, int y)>();
            for (int i = 0; i < N; i++)
                foreach (var (x, y) in new[] { (i, 0), (i, N - 1), (0, i), (N - 1, i) })
                    if (!seen[y, x] && (x != bx || y != by) && Passable(x, y)) { seen[y, x] = true; q.Enqueue((x, y)); }
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Defs.Nb[k, 0], ny = cy + Defs.Nb[k, 1];
                    if (!In(nx, ny) || seen[ny, nx] || (nx == bx && ny == by) || !Passable(nx, ny)) continue;
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
                    if (In(nx, ny) && (nx != bx || ny != by) && Passable(nx, ny) && seen[ny, nx]) return true;
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
            if (!In(x, y) || Edge(x, y) || !Buildable[y, x]) return false;
            for (int j = -apart; j <= apart; j++) for (int i = -apart; i <= apart; i++) if (HasStructure(x + i, y + j) || PileAt(x + i, y + j) != null) return false;
            return true;
        }

        /// <summary>Lay the camp out on the generated terrain of a camp world (a 32 x 32 square starting at x0, y0).</summary>
        public static CampMap FromTerrain(World w, int x0, int y0, RaiderCamp camp)
        {
            var m = new CampMap();
            int s = camp.Seed;
            for (int y = 0; y < m.N; y++)
                for (int x = 0; x < m.N; x++)
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
    public sealed class BattleGroup
    {
        public int X, Y, Infantry, Archers;
        public int Total => Infantry + Archers;
        /// <summary>What this landing's soldiers do; can be changed during the fight.</summary>
        public BattleOrder Order = BattleOrder.Charge;
        /// <summary>Which raiders they go for first.</summary>
        public TargetPriority Priority = TargetPriority.Nearest;
    }

    /// <summary>A landing group's job. Charge: fight the raiders. Warlord: make for the warlord, fighting only who stands in the way.
    /// Plunder: grab a supply pile and carry it off the map. Hold: stay put and fight whoever comes in reach. Fall back: leave by the nearest edge.</summary>
    public enum BattleOrder : byte { Charge, Warlord, Plunder, Hold, FallBack }

    /// <summary>Which raider a soldier picks: the nearest, archers first, brutes and the warlord first, or the most wounded.</summary>
    public enum TargetPriority : byte { Nearest, Archers, Leaders, Weakest }

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
        /// <summary>Soldiers: the landing group they came with (index into CampBattle.Groups); -1 for raiders.</summary>
        public int Group = -1;
        /// <summary>A soldier who walked off the map (falling back, or carrying plunder away): alive, out of the fight.</summary>
        public bool Left;
        /// <summary>Goods a plundering soldier is carrying.</summary>
        public int Carry; public Res CarryRes; public SupplyPile CarryPile;
        public bool OnField => Alive && !Left;
        /// <summary>A settlement's soldier still inside his barracks: not drawn and not in the fight until the town is roused (CampBattle.Alarm).</summary>
        public bool InBarracks;
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

        /// <summary>A fight at this camp. raidersHome: how many of its ordinary raiders are in camp (the rest are away raiding you or were
        /// killed); -1 means all of them. Brutes and the warlord always stay home.</summary>
        public CampBattle(RaiderCamp camp, IList<BattleGroup> groups, int raidersHome = -1)
        {
            Camp = camp; Map = camp.Site.Map;
            Groups = groups.Where(g => g.Total > 0).Select(g => new BattleGroup { X = g.X, Y = g.Y, Infantry = g.Infantry, Archers = g.Archers, Order = g.Order, Priority = g.Priority }).ToList();
            int id = 0;
            for (int gi = 0; gi < Groups.Count; gi++)
            {
                var g = Groups[gi];
                var spots = FreeSpots(g.X, g.Y, g.Total);
                for (int i = 0; i < g.Total; i++)
                {
                    bool inf = i < g.Infantry;
                    var (x, y) = spots[i % spots.Count];
                    Units.Add(new BUnit { Id = id++, Attacker = true, Kind = inf ? BKind.Infantry : BKind.Archer, X = x, Y = y, Px = x, Py = y, Group = gi,
                        Hp = inf ? InfantryHp : ArcherHp, Hit = inf ? InfantryHit : ArcherHit, Range = inf ? 1 : ArcherRange });
                }
                SentInfantry += g.Infantry; SentArchers += g.Archers;
            }
            var size = CampSize.Of(camp.Strength);
            // a camp never has more than its size allows; a town's garrison is as big as its people make it (World.Conquest.cs)
            int raiders = raidersHome < 0 ? size.Raiders : camp.IsTown ? raidersHome : Math.Min(raidersHome, size.Raiders), brutes = size.Brutes;
            for (int i = 0; i < raiders; i++)
            {
                // a settlement's soldiers wait in their barracks (drawn and fought only once the town is roused); a camp's raiders by their tents
                bool barracked = Map.Barracks.Count > 0;
                var homes = barracked ? Map.Barracks : Map.Tents;
                (int x, int y) tent = homes.Count > 0 ? homes[i % homes.Count] : (Map.N / 2, Map.N / 2);
                int per = (raiders + Math.Max(1, homes.Count) - 1) / Math.Max(1, homes.Count);
                var spots = FreeSpots(tent.x, tent.y, barracked ? Math.Max(3, per) : 3);
                var (x, y) = spots[(i / Math.Max(1, homes.Count)) % spots.Count];
                bool archer = i % 3 == 2;
                Units.Add(new BUnit { Id = id++, Attacker = false, Kind = archer ? BKind.RaiderArcher : BKind.Raider, X = x, Y = y, Px = x, Py = y,
                    Hp = archer ? RaiderArcherHp : RaiderHp, Hit = archer ? RaiderArcherHit : RaiderHit, Range = archer ? RaiderArcherRange : 1, InBarracks = barracked });
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
            RaidersTotal = raiders + brutes + 1;
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

        /// <summary>Your soldiers still fighting on the camp map (not counting those who have left it).</summary>
        public int AliveAttackers => Units.Count(u => u.OnField && u.Attacker);
        /// <summary>Your soldiers who walked off the map alive (fell back or carried plunder away).</summary>
        public int LeftField => Units.Count(u => u.Attacker && u.Alive && u.Left);
        public int AliveRaiders => Units.Count(u => u.Alive && !u.Attacker);
        public int SurvivingInfantry => Units.Count(u => u.Alive && u.Kind == BKind.Infantry);
        public int SurvivingArchers => Units.Count(u => u.Alive && u.Kind == BKind.Archer);
        /// <summary>Goods soldiers are carrying on the field right now (plunder not yet off the map).</summary>
        public int Carried => Units.Where(u => u.OnField).Sum(u => u.Carry);

        static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        int[] FieldTo(BUnit target) => FieldTo(target.X, target.Y);

        int[] _edgeField;
        /// <summary>Steps to the nearest open edge tile, from everywhere (the camp's structures never move, so it is worked out once).</summary>
        int[] EdgeField()
        {
            if (_edgeField != null) return _edgeField;
            var d = new int[Map.N * Map.N];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            var q = new Queue<int>();
            for (int i = 0; i < Map.N; i++)
                foreach (var (x, y) in new[] { (i, 0), (i, Map.N - 1), (0, i), (Map.N - 1, i) })
                    if (d[y * Map.N + x] < 0 && Map.Passable(x, y)) { d[y * Map.N + x] = 0; q.Enqueue(y * Map.N + x); }
            Flood(d, q);
            return _edgeField = d;
        }

        void Flood(int[] d, Queue<int> q)
        {
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % Map.N, cy = c / Map.N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Defs.Nb[k, 0], ny = cy + Defs.Nb[k, 1];
                    if (!Map.In(nx, ny) || d[ny * Map.N + nx] >= 0 || !Map.Passable(nx, ny)) continue;
                    d[ny * Map.N + nx] = d[c] + 1; q.Enqueue(ny * Map.N + nx);
                }
            }
        }

        int[] FieldTo(int tx, int ty)
        {
            var d = new int[Map.N * Map.N];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            var q = new Queue<int>();
            d[ty * Map.N + tx] = 0; q.Enqueue(ty * Map.N + tx);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % Map.N, cy = c / Map.N;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + Defs.Nb[k, 0], ny = cy + Defs.Nb[k, 1];
                    if (!Map.In(nx, ny) || d[ny * Map.N + nx] >= 0 || !Map.Passable(nx, ny)) continue;
                    d[ny * Map.N + nx] = d[c] + 1; q.Enqueue(ny * Map.N + nx);
                }
            }
            return d;
        }

        bool StepDown(BUnit u, int[] d)
        {
            int cur = d[u.Y * Map.N + u.X];
            if (cur <= 0) return false;
            for (int k = 0; k < 4; k++)
            {
                int nx = u.X + Defs.Nb[k, 0], ny = u.Y + Defs.Nb[k, 1];
                if (Map.In(nx, ny) && d[ny * Map.N + nx] == cur - 1) { u.X = nx; u.Y = ny; return true; }
            }
            return false;
        }

        /// <summary>How much a soldier wants this raider as a target (lower is better): distance, adjusted by the group's priority. With
        /// Nearest it is the distance alone (ties go to the lowest id), which is how every unit chose before orders existed.</summary>
        public static int TargetScore(BUnit u, BUnit e, TargetPriority p)
        {
            int s = Cheb(u.X, u.Y, e.X, e.Y) * 10;
            if (p == TargetPriority.Archers && e.Kind == BKind.RaiderArcher) s -= 80;
            if (p == TargetPriority.Leaders && (e.Kind == BKind.Brute || e.Kind == BKind.Chief)) s -= 80;
            if (p == TargetPriority.Weakest) s += (int)Math.Round(e.Hp * 10);
            return s;
        }

        static BUnit PickTarget(BUnit u, IEnumerable<BUnit> enemies, TargetPriority p)
        {
            BUnit t = null; int best = int.MaxValue;
            foreach (var e in enemies) { int s = TargetScore(u, e, p); if (s < best || (s == best && e.Id < t.Id)) { t = e; best = s; } }
            return t;
        }

        /// <summary>The order a soldier actually follows now: its group's, unless that has nothing left to do (the warlord is dead, the
        /// piles are empty) when it charges; a soldier carrying plunder always heads off the map.</summary>
        public BattleOrder OrderOf(BUnit u)
        {
            if (!u.Attacker || u.Group < 0) return BattleOrder.Charge;
            if (u.Carry > 0) return BattleOrder.FallBack;
            var o = Groups[u.Group].Order;
            if (o == BattleOrder.Warlord && !Units.Any(e => e.Alive && e.Kind == BKind.Chief)) return BattleOrder.Charge;
            if (o == BattleOrder.Plunder && !Map.Piles.Any(p => p.Amount > 0)) return BattleOrder.Charge;
            return o;
        }

        void Swing(BUnit u, BUnit t, Dictionary<BUnit, double> hits)
        {
            hits[t] = hits.GetValueOrDefault(t) + u.Hit; u.Attacked = true;
            if (u.Ranged) { u.Shooting = true; u.ShotX = t.X; u.ShotY = t.Y; }
        }

        /// <summary>Close in on t (to striking or shooting distance) and hit it if in reach.</summary>
        void Engage(BUnit u, BUnit t, Dictionary<BUnit, int[]> fields, Dictionary<BUnit, double> hits)
        {
            u.Target = t;
            int want = u.Ranged ? u.Range - 1 : 1;
            if (Cheb(u.X, u.Y, t.X, t.Y) > want)
            {
                if (!fields.TryGetValue(t, out var f)) fields[t] = f = FieldTo(t);
                StepDown(u, f);
            }
            if (Cheb(u.X, u.Y, t.X, t.Y) <= u.Range) Swing(u, t, hits);
        }

        /// <summary>A soldier walks off the map: out of the fight, alive, and whatever it carries is safely taken.</summary>
        void Leave(BUnit u)
        {
            u.Left = true; u.Target = null;
            if (u.Carry > 0) { Loot[u.CarryRes] = Loot.GetValueOrDefault(u.CarryRes) + u.Carry; u.Carry = 0; u.CarryPile = null; }
        }

        void StepSoldier(BUnit u, List<BUnit> raiders, Dictionary<BUnit, int[]> fields, Dictionary<(int, int), int[]> pileFields, Dictionary<BUnit, double> hits)
        {
            var prio = Groups[u.Group].Priority;
            BUnit Adjacent() => PickTarget(u, raiders.Where(e => Cheb(u.X, u.Y, e.X, e.Y) <= 1), prio);
            switch (OrderOf(u))
            {
                case BattleOrder.FallBack:
                {
                    u.Target = null;
                    if (!Map.Edge(u.X, u.Y)) StepDown(u, EdgeField());
                    if (Map.Edge(u.X, u.Y)) Leave(u);
                    return;
                }
                case BattleOrder.Hold:
                {
                    var t = PickTarget(u, raiders.Where(e => Cheb(u.X, u.Y, e.X, e.Y) <= u.Range), prio);
                    u.Target = t;
                    if (t != null) Swing(u, t, hits);
                    return;
                }
                case BattleOrder.Warlord:
                {
                    var chief = raiders.FirstOrDefault(e => e.Kind == BKind.Chief);
                    if (chief == null) { if (raiders.Count > 0) Engage(u, PickTarget(u, raiders, prio), fields, hits); return; }
                    if (Cheb(u.X, u.Y, chief.X, chief.Y) <= u.Range) { u.Target = chief; Swing(u, chief, hits); return; }
                    var blocker = Adjacent();   // someone in the way: cut through
                    if (blocker != null && !u.Ranged) { u.Target = blocker; Swing(u, blocker, hits); return; }
                    Engage(u, chief, fields, hits);
                    return;
                }
                case BattleOrder.Plunder:
                {
                    var pile = Map.Piles.Where(p => p.Amount > 0).OrderBy(p => Cheb(u.X, u.Y, p.X, p.Y)).ThenBy(p => p.Y * Map.N + p.X).FirstOrDefault();
                    if (pile == null) break;
                    if (u.X == pile.X && u.Y == pile.Y)
                    {
                        int take = Math.Min(pile.Amount, u.Kind == BKind.Archer ? ArcherCarry : InfantryCarry);
                        pile.Amount -= take; u.Carry = take; u.CarryRes = pile.Res; u.CarryPile = pile; u.Target = null;
                        return;
                    }
                    var guard = Adjacent();   // defend themselves, but do not go looking for a fight
                    if (guard != null) { u.Target = guard; Swing(u, guard, hits); return; }
                    u.Target = null;
                    if (!pileFields.TryGetValue((pile.X, pile.Y), out var pf)) pileFields[(pile.X, pile.Y)] = pf = FieldTo(pile.X, pile.Y);
                    StepDown(u, pf);
                    return;
                }
            }
            if (raiders.Count > 0) Engage(u, PickTarget(u, raiders, prio), fields, hits);   // Charge
        }

        /// <summary>One tick: each soldier follows its group's order (charge, go for the warlord, plunder, hold or fall back), raiders go for
        /// the nearest soldier, everyone in reach swings or shoots; the fallen are removed; the end of the fight is checked.</summary>
        public void Step()
        {
            if (Over) return;
            Tick++;
            foreach (var u in Units) { if (!u.OnField) continue; u.Px = u.X; u.Py = u.Y; u.Hurt = u.Attacked = u.Shooting = false; }
            var att = Units.Where(u => u.OnField && u.Attacker).ToList();
            var rai = Units.Where(u => u.Alive && !u.Attacker && !u.InBarracks).ToList();
            // a camp wakes when you come near its raiders; a town also when you come near its hall or its barracks
            if (!Alarm && att.Any(a => rai.Any(r => Cheb(a.X, a.Y, r.X, r.Y) <= AlertR)
                || Map.Barracks.Any(b => Cheb(a.X, a.Y, b.x, b.y) <= AlertR) || (Camp.IsTown && Cheb(a.X, a.Y, Map.Chief.x, Map.Chief.y) <= AlertR))) Alarm = true;
            if (Alarm && Units.Any(u => u.InBarracks))   // roused: the soldiers pour out of their barracks
            {
                foreach (var u in Units) if (u.InBarracks) { u.InBarracks = false; u.Px = u.X; u.Py = u.Y; }
                rai = Units.Where(u => u.Alive && !u.Attacker).ToList();
            }
            var fields = new Dictionary<BUnit, int[]>();
            var pileFields = new Dictionary<(int, int), int[]>();
            var hits = new Dictionary<BUnit, double>();
            foreach (var u in att) StepSoldier(u, rai, fields, pileFields, hits);
            var onField = att.Where(a => !a.Left).ToList();
            if (Alarm)   // the camp sleeps until the landing is spotted
                foreach (var u in rai)
                {
                    if (onField.Count == 0) break;
                    Engage(u, PickTarget(u, onField, TargetPriority.Nearest), fields, hits);
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
                    if (u.Carry > 0 && u.CarryPile != null) u.CarryPile.Amount += u.Carry;   // the goods fall back onto their pile
                    u.Carry = 0; u.CarryPile = null;
                }
            if (AliveRaiders == 0) { Over = true; Victory = true; TakeLoot(); }
            else if (AliveAttackers == 0) { Over = true; Retreated = LeftField > 0; }
            else if (Tick >= MaxTicks) Retreat();
        }

        /// <summary>Pull out: everyone still on the map leaves, keeping whatever plunder they carry.</summary>
        public void Retreat()
        {
            if (Over) return;
            foreach (var u in Units) if (u.Attacker && u.OnField) Leave(u);
            Over = true; Retreated = true;
        }

        /// <summary>The survivors still on the map keep what they carry and take as much more as they can from the piles, in proportion to each pile.</summary>
        void TakeLoot()
        {
            var here = Units.Where(u => u.Attacker && u.OnField).ToList();
            int capacity = here.Sum(u => (u.Kind == BKind.Archer ? ArcherCarry : InfantryCarry) - u.Carry);
            foreach (var u in here)
                if (u.Carry > 0) { Loot[u.CarryRes] = Loot.GetValueOrDefault(u.CarryRes) + u.Carry; u.Carry = 0; u.CarryPile = null; }
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
            string outcome = Victory ? (Camp.IsTown ? $"Victory: {Camp.Name} has fallen." : "Victory: the camp is destroyed.") : Retreated ? "The army pulled out." : Over ? "Defeat: the army was wiped out." : "The fight goes on.";
            string loot = Loot.Count == 0 ? "nothing" : string.Join(", ", Loot.Where(k => k.Value > 0).Select(k => $"{k.Value} {k.Key.ToString().ToLower()}"));
            string who = Camp.IsTown ? "defenders" : "raiders";
            return $"{outcome}\n{Camp.Name}: {RaidersKilled} of {RaidersTotal} {who} killed.\n" +
                   $"You sent {SentInfantry} infantry and {SentArchers} archers; {LostInfantry} infantry and {LostArchers} archers fell, {SurvivingInfantry} infantry and {SurvivingArchers} archers survive.\n" +
                   (Loot.Values.Any(v => v > 0) ? $"They carry home: {loot}." : Victory ? "They carry home: nothing." : "");
        }
    }
}
