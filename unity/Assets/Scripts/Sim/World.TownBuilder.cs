// An NPC settlement under attack is a real town, built on its own terrain by the same rules as yours: a Town Hall in the middle, a grid of
// streets joined to it, homes enough for its people, barracks for its soldiers (16 to a barracks), wells, a market, a silo and a
// storehouse, workshops for what its land gives, farms with their fields outside, and for a town or bigger a palisade with watchtowers
// round the streets (gaps where the streets pass through: real gates come with the walls work). Its people are the game's own: its
// villagers walk the streets and its crews go out to work (World.AmbientDay), drawn by the usual renderers. The battle is fought on it:
// the buildings block the way, its soldiers muster at their barracks, the lord is at the hall and its stores can be plundered.
//
// The map grows with the settlement (TownMapSize). A big city or capital has more people than its map can house; the town is built as
// far as it fits (the rest are taken to live in the countryside around).
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>The side of a settlement's battle map, in tiles: a hamlet's is a raider camp's 32, a capital's 88.</summary>
        public static int TownMapSize(SettlementTier t) => t switch
        {
            SettlementTier.Hamlet => 32, SettlementTier.Village => 40, SettlementTier.Town => 56, SettlementTier.City => 72, _ => 88,
        };

        /// <summary>How far the streets may reach from the hall (the rest of the map is fields, woods and the approach).</summary>
        static int MaxStreetReach(int n, SettlementTier t) => t switch
        {
            SettlementTier.Hamlet => 6, SettlementTier.Village => 9, _ => n / 2 - (t == SettlementTier.Town ? 12 : 16),
        };

        public const int StreetSpacing = 5;   // a street every 5 tiles: two rows of lots between streets

        /// <summary>What was built, for the battle map and the tests.</summary>
        public sealed class TownPlan
        {
            public int People, Soldiers, Homes, Farms, Fields, Barracks, Towers, WallTiles;
            public readonly List<string> Built = new List<string>();
        }

        /// <summary>The plan of the town this world holds (null for a raider camp or your settlement).</summary>
        public TownPlan TownLayout { get; private set; }

        public static CampSite CreateTown(RaiderCamp camp)
        {
            var s = camp.Town; int n = TownMapSize(s.Tier);
            var w = MakeBattleGround(camp.Seed, camp.Relief, n, n / 3, out int x0, out int y0);
            w.TownLayout = w.BuildTown(s, x0, y0, n);
            var map = w.TownMap(s, x0, y0, n);
            return new CampSite { World = w, Map = map, X0 = x0, Y0 = y0 };
        }

        // ---------- building it ----------

        bool InSquare(int x, int y, int x0, int y0, int n) => x >= x0 && y >= y0 && x < x0 + n && y < y0 + n;

        /// <summary>A footprint that is free to build on: open, flat enough, inside the square away from its landing edge.</summary>
        bool LotFree(int x, int y, int sz, int x0, int y0, int n)
        {
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sz; i++)
                {
                    int tx = x + i, ty = y + j;
                    if (!InSquare(tx, ty, x0 + 3, y0 + 3, n - 6) || Grid[ty, tx] != null || ZoneMap[ty, tx] != Zone.None || IsSolid(tx, ty) || TileSpread(tx, ty) > 1) return false;
                }
            return true;
        }

        /// <summary>A footprint with a road connected to the hall along one of its sides.</summary>
        bool BesideTownRoad(int x, int y, int sz)
        {
            for (int k = 0; k < sz; k++)
                foreach (var (tx, ty) in new[] { (x + k, y - 1), (x + k, y + sz), (x - 1, y + k), (x + sz, y + k) })
                    if (Inb(tx, ty) && Grid[ty, tx] is Building r && r.T == "road" && r.Served) return true;
            return false;
        }

        /// <summary>The free lot beside a connected road nearest (or, with far, farthest) to (cx, cy), within `reach` of it; null if none.</summary>
        (int x, int y)? FindLot(int sz, int cx, int cy, int reach, int x0, int y0, int n, bool far = false, Func<int, int, bool> also = null)
        {
            (int x, int y)? best = null; int bestD = far ? -1 : int.MaxValue;
            for (int y = cy - reach; y <= cy + reach; y++)
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!LotFree(x, y, sz, x0, y0, n) || !BesideTownRoad(x, y, sz) || (also != null && !also(x, y))) continue;
                    int d = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
                    if (far ? d > bestD : d < bestD) { best = (x, y); bestD = d; }
                }
            return best;
        }

        Building Raise(TownPlan plan, string type, (int x, int y)? at, int sz = 0)
        {
            if (!at.HasValue) return null;
            var b = AddBld(type, at.Value.x, at.Value.y, sz != 0 ? sz : PlacementSize(type));
            plan.Built.Add(type);
            return b;
        }

        void LayRoad(int x, int y, int x0, int y0, int n)
        {
            if (!InSquare(x, y, x0 + 2, y0 + 2, n - 4) || Grid[y, x] != null || IsSolid(x, y) || IsMountain(x, y)) return;
            DecoMap[y, x] = Deco.None;
            AddBld("road", x, y);
        }

        TownPlan BuildTown(Settlement s, int x0, int y0, int n)
        {
            var plan = new TownPlan();
            int cx = x0 + n / 2, cy = y0 + n / 2;
            var tier = s.Tier;

            // the hall in the middle (on the nearest ground that takes a 3 x 3), its road leaving south like yours
            (int x, int y) hall = (cx - 1, cy - 1);
            for (int r = 0; r < 8; r++)
            {
                bool ok = false;
                for (int dy = -r; dy <= r && !ok; dy++) for (int dx = -r; dx <= r && !ok; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && LotFree(cx - 1 + dx, cy - 1 + dy, 3, x0, y0, n)) { hall = (cx - 1 + dx, cy - 1 + dy); ok = true; }
                if (ok) break;
            }
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++) DecoMap[hall.y + j, hall.x + i] = Deco.None;
            AddBld("hall", hall.x, hall.y, 3);
            cx = hall.x + 1; cy = hall.y + 1;

            // how many: soldiers in barracks of 16, everyone else in homes; the rest of the land is farmed
            int soldiers = Garrison(s);
            int people = (int)Math.Round(s.Population);
            int civilians = Math.Max(2, people - soldiers);
            bool bigHomes = tier >= SettlementTier.Town;
            int homeCap = bigHomes ? HouseCap2[2] : HouseCap[2];
            int homesWanted = (civilians + homeCap - 1) / homeCap;
            int barracksWanted = (soldiers + Bunks - 1) / Bunks;
            int others = 2 + (tier >= SettlementTier.Village ? 4 : 0) + (tier >= SettlementTier.Town ? 3 : 0);
            int area = homesWanted * (bigHomes ? 4 : 1) + barracksWanted * 4 + others * 4;
            int reach = Math.Clamp((int)Math.Ceiling(Math.Sqrt(area * 2.6) / 2) + 3, 4, MaxStreetReach(n, tier));

            // streets: a grid joined to the hall, every StreetSpacing tiles, out to `reach`
            int hy = cy + 2;   // the street just south of the hall
            for (int k = -reach / StreetSpacing - 1; k <= reach / StreetSpacing + 1; k++)
            {
                int y = hy + k * StreetSpacing, x = cx + k * StreetSpacing;
                for (int t = -reach; t <= reach; t++)
                {
                    if (Math.Abs(y - cy) <= reach) LayRoad(cx + t, y, x0, y0, n);   // never on the palisade line (reach + 2)
                    if (Math.Abs(x - cx) <= reach) LayRoad(x, cy + t, x0, y0, n);
                }
            }
            // four roads out of town towards the edges, for the farms and the approach
            for (int t = reach; t < n / 2 - 3; t++) { LayRoad(cx, cy + t, x0, y0, n); LayRoad(cx, cy - t, x0, y0, n); LayRoad(cx + t, hy, x0, y0, n); LayRoad(cx - t, hy, x0, y0, n); }
            // the town has cleared its own streets: no woods or loose boulders between the houses (mountains stay)
            for (int y = cy - reach - 1; y <= cy + reach + 3; y++)
                for (int x = cx - reach - 1; x <= cx + reach + 1; x++)
                    if (Inb(x, y) && Grid[y, x] == null && !IsMountain(x, y) && DecoMap[y, x] is Deco.Tree or Deco.Sapling or Deco.Stump or Deco.Rock)
                    { DecoMap[y, x] = Deco.None; RockAmtMap.Remove(Tk(x, y)); }
            Recalc();

            // the palisade (a town or bigger): a ring just outside the streets, with gaps where the roads pass and towers at the corners
            int ring = reach + 2;
            if (tier >= SettlementTier.Town)
                for (int t = -ring; t <= ring; t++)
                    foreach (var (x, y) in new[] { (cx + t, cy - ring), (cx + t, cy + ring), (cx - ring, cy + t), (cx + ring, cy + t) })
                    {
                        if (!InSquare(x, y, x0 + 3, y0 + 3, n - 6) || Grid[y, x] != null || IsSolid(x, y)) continue;
                        bool corner = Math.Abs(x - cx) == ring && Math.Abs(y - cy) == ring;
                        bool mid = tier >= SettlementTier.City && (x == cx || y == cy);
                        DecoMap[y, x] = Deco.None;
                        if (corner || (mid && !BesideRoadTile(x, y))) { AddBld("watchtower", x, y); plan.Towers++; }
                        else { AddBld("wall", x, y); plan.WallTiles++; }
                    }

            bool InsideWalls(int x, int y) => Math.Abs(x - cx) <= ring && Math.Abs(y - cy) <= ring;   // the walls and everything within them

            // barracks by the hall, then the market, the stores and the workshops, then homes for everyone
            for (int i = 0; i < barracksWanted; i++) if (Raise(plan, "barracks", FindLot(2, cx, cy, reach + 1, x0, y0, n)) != null) plan.Barracks++;
            if (tier >= SettlementTier.Village) Raise(plan, "market", FindLot(2, cx, cy, reach + 1, x0, y0, n));
            int wells = Math.Max(1, people / 80);
            for (int i = 0; i < Math.Min(wells, 12); i++)
            {
                double a = i * 2.4;   // spread the wells round the town
                Raise(plan, "well", FindLot(1, cx + (int)(Math.Cos(a) * reach * 0.6), cy + (int)(Math.Sin(a) * reach * 0.6), reach, x0, y0, n));
            }
            if (tier >= SettlementTier.Village) Raise(plan, "silo", FindLot(1, cx, cy, reach + 1, x0, y0, n));
            if (tier >= SettlementTier.Town) { Raise(plan, "storehouse", FindLot(2, cx, cy, reach + 1, x0, y0, n)); Raise(plan, "smithery", FindLot(2, cx, cy, reach + 1, x0, y0, n, far: true)); }
            if (tier >= SettlementTier.Village) Raise(plan, "sawmill", FindLot(2, cx, cy, reach + 1, x0, y0, n, far: true));
            for (int i = 0; i < homesWanted; i++)
            {
                var b = Raise(plan, "house", FindLot(bigHomes ? 2 : 1, cx, cy, reach + 1, x0, y0, n), bigHomes ? 2 : 1);
                if (b == null) break;
                b.Lvl = 2; plan.Homes++;
            }

            // a woodcutter where there are woods within reach, marking trees for felling round it
            if (tier >= SettlementTier.Village)
            {
                bool NearTrees(int x, int y)
                {
                    for (int j = -6; j <= 7; j++) for (int i = -6; i <= 7; i++) if (Inb(x + i, y + j) && HasTree(x + i, y + j) && InSquare(x + i, y + j, x0, y0, n)) return true;
                    return false;
                }
                var lodge = Raise(plan, "wood", FindLot(2, cx, cy, n / 2, x0, y0, n, far: true, also: NearTrees));
                if (lodge != null)
                    for (int j = -WorkR; j <= WorkR; j++)
                        for (int i = -WorkR; i <= WorkR; i++)
                        {
                            int x = lodge.X + i, y = lodge.Y + j;
                            if (Inb(x, y) && InSquare(x, y, x0 + 2, y0 + 2, n - 4) && DecoMap[y, x] == Deco.Tree) ZoneMap[y, x] = Zone.Wood;
                        }
            }

            // farms outside the walls along the roads out, each with its fields round it (one farmer feeds about ten)
            int farmers = Math.Max(1, (int)Math.Ceiling(people / 10.0));
            int farmsWanted = Math.Max(1, (farmers + 9) / 10);
            for (int i = 0; i < farmsWanted; i++)
            {
                var spot = FindLot(2, cx, cy, n / 2, x0, y0, n, far: true, also: (x, y) => !InsideWalls(x, y) && Fert[y, x])
                        ?? FindLot(2, cx, cy, n / 2, x0, y0, n, far: true, also: (x, y) => !InsideWalls(x, y));   // poor soil: farm it anyway
                var farm = Raise(plan, "farm", spot, 2);
                if (farm == null) break;
                plan.Farms++;
                int want = Math.Min(farm.Jobs, farmers - i * 10) * FieldsPerFarmer;
                // its fields: the nearest fertile open tiles within its reach
                var spots = new List<(int d, int x, int y)>();
                for (int j = -FarmReach; j <= FarmReach + 1; j++)
                    for (int k = -FarmReach; k <= FarmReach + 1; k++)
                    {
                        int x = farm.X + k, y = farm.Y + j;
                        if (!Inb(x, y) || !InSquare(x, y, x0 + 3, y0 + 3, n - 6) || InsideWalls(x, y) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None) continue;
                        if (!Fert[y, x] || IsSolid(x, y) || DecoMap[y, x] == Deco.Rock) continue;
                        spots.Add((Math.Max(Math.Abs(k), Math.Abs(j)), x, y));
                    }
                foreach (var (_, x, y) in spots.OrderBy(p => p.d).ThenBy(p => p.y * N + p.x).Take(Math.Max(0, want)))
                {
                    DecoMap[y, x] = Deco.None; ZoneMap[y, x] = Zone.Farm; plan.Fields++;
                }
            }

            // its people: soldiers in their barracks, the rest at home; enough food in store that the shown town is not starving
            Soldiers = Math.Min(soldiers, plan.Barracks * Bunks);
            Peasants = Math.Max(2, Blds.Where(b => b.T == "house").Sum(b => b.Capacity));
            plan.Soldiers = Soldiers; plan.Homes = Blds.Count(b => b.T == "house"); plan.People = Peasants + Soldiers;
            ResAmt[Res.Food] = CropStorage(Res.Food);
            Touch();
            Recalc();
            return plan;
        }

        bool BesideRoadTile(int x, int y)
        {
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                if (Inb(nx, ny) && Grid[ny, nx]?.T == "road") return true;
            }
            return false;
        }

        /// <summary>The battle map of a built town: every building blocks the way (roads, fields and gaps in the palisade are open), the lord
        /// waits at the hall, soldiers muster at the barracks, and the stores hold what can be plundered.</summary>
        CampMap TownMap(Settlement s, int x0, int y0, int n)
        {
            var m = new CampMap(n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int wx = x0 + x, wy = y0 + y;
                    var b = Grid[wy, wx];
                    m.Ground[y, x] = HasTree(wx, wy) ? CampTile.Tree : (IsSolid(wx, wy) || DecoMap[wy, wx] == Deco.Rock) ? CampTile.Rock : CampTile.Open;
                    m.Blocked[y, x] = b != null && b.T != "road";
                    m.Buildable[y, x] = m.Ground[y, x] == CampTile.Open && b == null && TileSpread(wx, wy) == 0;
                }
            var hall = Hall;
            m.Chief = (hall.X + 1 - x0, hall.Y + 1 - y0);
            foreach (var b in Blds)
            {
                if (b.T == "barracks") m.Barracks.Add((b.X - x0, b.Y - y0));
                if (b.T == "house") m.Tents.Add((b.X - x0, b.Y - y0));
            }
            // what can be carried off, in the open beside the buildings that hold it
            double pop = s.Population;
            void Store(Building b, Res res, int amount)
            {
                if (b == null || amount <= 0) return;
                for (int k = 0; k < b.Sz * 4 + 4; k++)
                {
                    int side = k % 4, along = k / 4 % Math.Max(1, b.Sz);
                    var (tx, ty) = side switch
                    {
                        0 => (b.X + along, b.Y + b.Sz), 1 => (b.X - 1, b.Y + along), 2 => (b.X + along, b.Y - 1), _ => (b.X + b.Sz, b.Y + along),
                    };
                    int lx = tx - x0, ly = ty - y0;
                    if (m.Passable(lx, ly) && m.PileAt(lx, ly) == null) { m.Piles.Add(new SupplyPile { X = lx, Y = ly, Res = res, Amount = amount }); return; }
                }
            }
            Store(hall, Res.Gold, (int)Math.Round(pop / 12) + 10);
            Store(Blds.FirstOrDefault(b => b.T == "market"), Res.Gold, (int)Math.Round(pop / 20));
            foreach (var silo in Blds.Where(b => b.T == "silo")) Store(silo, Res.Food, (int)Math.Round(pop / 4) + 10);
            Store(Blds.FirstOrDefault(b => b.T == "storehouse") ?? Blds.FirstOrDefault(b => b.T == "market"), s.Makes, (int)Math.Round(pop / 6) + 10);
            if (m.Piles.Count == 0) Store(hall, Res.Food, (int)Math.Round(pop / 4) + 10);
            return m;
        }

        /// <summary>A day in an NPC settlement you are looking at: its crews go out and its villagers walk about. Nothing else of its economy
        /// runs (it is a picture of the town, not a second simulation).</summary>
        public void AmbientDay()
        {
            var got = new Dictionary<Res, double> { { Res.Logs, 0 }, { Res.Stone, 0 }, { Res.Ore, 0 }, { Res.Food, 0 } };
            Recalc();
            StepCrews(got);
            StepFolk();
            Day++;
        }
    }
}
