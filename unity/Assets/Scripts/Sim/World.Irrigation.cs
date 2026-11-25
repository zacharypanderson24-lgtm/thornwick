// Irrigation canals and watermills (DESIGN_WATER.md sections 20 and 22).
//
// A canal is a dug channel, stored as a set of land tiles (not a building: it has no sprite, crews and walkers step across it, and it saves as a
// list of tiles). It carries fresh water out from a river, lake or pond, or from another canal, across land, and the soil within three tiles of
// a connected canal gets moister, so dry country can be farmed. Water does not run uphill: a canal may not climb. A canal that is not joined to
// fresh water irrigates nothing. Roads, buildings and zones keep off canal tiles.
//
// A watermill stands on a river bank, needs a flowing river beside it (not frozen), and mills the wheat harvest: staffed mills add food.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int CanalMaxLength = 24, CanalWood = 2, CanalReach = 3;
        public const double CanalBonus = 0.35, MillBonusShare = 0.12;
        public const int MillFields = 40;   // fields one staffed mill grinds for

        /// <summary>Dug canal tiles (y * N + x). Saved.</summary>
        public readonly HashSet<int> Canals = new HashSet<int>();

        public bool IsCanal(int x, int y) => Canals.Count > 0 && Inb(x, y) && Canals.Contains(Tk(x, y));

        /// <summary>Why a tile cannot take a building, road or zone for a canal, or null.</summary>
        public string CanalBlock(int x, int y) => IsCanal(x, y) ? "An irrigation canal is in the way. Remove it first" : null;

        static bool FreshOpen(SurfaceWater w, int i) => SurfaceWater.OpenWater(w.Class[i]) && !SurfaceWater.Salty(w.Class[i]) && w.Salinity[i] < 40;

        /// <summary>Straight canal line from a to b (same rule as a bridge: along the axis the mouse moved furthest).</summary>
        public static List<(int x, int y)> CanalLine(int ax, int ay, int bx, int by) => BridgeLine(ax, ay, bx, by);

        public Result CanalPathCheck(IList<(int x, int y)> path)
        {
            if (Waters == null) return Result.No("Canals need fresh water to carry: there is no water on this map");
            if (path.Count == 0) return Result.No("Drag a canal out from fresh water");
            if (path.Count > CanalMaxLength) return Result.No($"Too long: a canal runs at most {CanalMaxLength} tiles in one go (dig the next stretch from its end)");
            // the first tile must touch fresh water or a canal already joined to it
            var (fx, fy) = path[0]; bool joined = false;
            for (int dy = -1; dy <= 1 && !joined; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                    int nx = fx + dx, ny = fy + dy; if (!Inb(nx, ny)) continue;
                    if (FreshOpen(Waters, ny * N + nx) || (IsCanal(nx, ny) && CanalIsFed(nx, ny))) joined = true;
                }
            if (!joined) return Result.No("A canal must start at fresh water (a river, lake or pond) or at the end of a canal that does");
            double level = double.NegativeInfinity; int n = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var (x, y) = path[i];
                if (!Inb(x, y)) return Result.No("Outside the map");
                if (!Claimed(x, y)) return Result.No(NotYours);
                if (IsCanal(x, y)) continue;
                if (Waters.IsWater(x, y)) return Result.No("The canal reaches the water: start it on the bank and dig away from the water");
                if (Grid[y, x] != null) return Result.No("A building or road is in the way");
                if (PenAt(x, y) != null) return Result.No("A livestock pen is in the way");
                if (HasTree(x, y)) return Result.No(TreeMsg);
                if (IsSolid(x, y)) return Result.No("Rock, a pit or steep ground is in the way of the canal");
                double lv = TileLevel(x, y);
                if (level > double.NegativeInfinity && lv > level + 0.51) return Result.No("Water does not run uphill: the canal would have to climb here");
                level = Math.Min(lv, level > double.NegativeInfinity ? level + 0.26 : lv);
                n++;
            }
            if (n == 0) return Result.Yes(0);
            if (ResAmt[Res.Wood] < n * CanalWood) return Result.No($"Not enough wood: {n} canal tiles need {n * CanalWood} wood", n);
            return Result.Yes(n);
        }

        public Result PlaceCanalPath(IList<(int x, int y)> path)
        {
            var c = CanalPathCheck(path);
            if (!c.Ok || c.Count == 0) return c;
            ResAmt[Res.Wood] -= c.Count * CanalWood;
            foreach (var (x, y) in path) Canals.Add(Tk(x, y));
            Touch();
            return c;
        }

        /// <summary>Take a dug canal tile out again (half its wood back). True when there was one.</summary>
        public bool RemoveCanal(int x, int y)
        {
            if (!IsCanal(x, y)) return false;
            Canals.Remove(Tk(x, y)); ResAmt[Res.Wood] += CanalWood / 2.0; Touch(); return true;
        }

        // ---------- which canals carry water, and where it soaks in (cached per Version) ----------
        HashSet<int> _fed; Dictionary<int, double> _irrigated; int _canalVersion = -1;

        void RefreshCanals()
        {
            if (_canalVersion == Version && _fed != null) return;
            _canalVersion = Version; _fed = new HashSet<int>(); _irrigated = new Dictionary<int, double>();
            if (Waters == null || Canals.Count == 0) return;
            var q = new Queue<int>();
            foreach (int k in Canals)
            {
                int x = k % N, y = k / N;
                for (int d = 0; d < 4; d++) { int nx = x + Nb[d, 0], ny = y + Nb[d, 1]; if (Inb(nx, ny) && FreshOpen(Waters, ny * N + nx)) { _fed.Add(k); q.Enqueue(k); break; } }
            }
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % N, y = c / N;
                for (int d = 0; d < 4; d++) { int nx = x + Nb[d, 0], ny = y + Nb[d, 1]; if (!Inb(nx, ny)) continue; int ni = ny * N + nx; if (Canals.Contains(ni) && _fed.Add(ni)) q.Enqueue(ni); }
            }
            foreach (int k in _fed)
            {
                int x = k % N, y = k / N;
                for (int dy = -CanalReach; dy <= CanalReach; dy++)
                    for (int dx = -CanalReach; dx <= CanalReach; dx++)
                    {
                        int nx = x + dx, ny = y + dy; if (!Inb(nx, ny) || Waters.Class[ny * N + nx] != WaterClass.None) continue;
                        double near = 1 - (Math.Max(Math.Abs(dx), Math.Abs(dy)) - 0) / (double)(CanalReach + 1);
                        int ni = ny * N + nx; _irrigated[ni] = Math.Max(_irrigated.GetValueOrDefault(ni), CanalBonus * near);
                    }
            }
        }

        /// <summary>True when the canal tile is joined to fresh water, so it carries water.</summary>
        public bool CanalIsFed(int x, int y) { RefreshCanals(); return _fed.Contains(Tk(x, y)); }

        /// <summary>Extra soil moisture a tile gets from the canals (0 when none reaches it).</summary>
        public double IrrigationBonus(int x, int y)
        {
            if (Canals.Count == 0 || Waters == null) return 0;
            RefreshCanals();
            return _irrigated.TryGetValue(Tk(x, y), out var b) ? b : 0;
        }

        // ---------- watermills ----------
        /// <summary>True when a river with a current runs within `reach` tiles of the footprint and is not frozen or dry.</summary>
        public bool RiverCurrentNear(Building fp, int reach)
        {
            if (Waters == null) return false;
            for (int y = fp.Y - reach; y < fp.Y + fp.Sz + reach; y++)
                for (int x = fp.X - reach; x < fp.X + fp.Sz + reach; x++)
                {
                    if (!Inb(x, y)) continue; var cl = Waters.Class[y * N + x];
                    if ((cl == WaterClass.SmallRiver || cl == WaterClass.MajorRiver || cl == WaterClass.Stream) && Waters.Strength[y * N + x] >= 60 && !IceBears(x, y) && !StreamDry(x, y)) return true;
                }
            return false;
        }

        /// <summary>Extra food the staffed watermills add to a wheat harvest of this size: 12% of the grain they can grind (each mill grinds for 40 fields).</summary>
        public double MillBonus(double wheat)
        {
            if (wheat <= 0 || Waters == null) return 0;
            double capacity = 0;
            foreach (var b in Blds) if (b.T == "watermill" && b.Served && b.Workers > 0 && RiverCurrentNear(b, 1)) capacity += MillFields * b.Eff;
            if (capacity <= 0) return 0;
            int fields = Math.Max(1, FarmFields().Count);
            return wheat * MillBonusShare * Math.Min(1.0, capacity / fields);
        }
    }
}
