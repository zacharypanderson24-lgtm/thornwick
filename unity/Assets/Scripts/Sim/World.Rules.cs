// Placement, removal, zoning and the services/workforce recalculation, ported from src/sim.js and src/sim_econ.js.
// recalc here is the straightforward full version (the prototype's audit path); the chunked, incremental caches of the
// JS Phase 1 optimisation are not needed at this map size yet.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public struct Result
    {
        public bool Ok; public string Reason; public int Count;
        public static Result Yes(int n = 0) => new Result { Ok = true, Count = n };
        public static Result No(string why, int n = 0) => new Result { Ok = false, Reason = why, Count = n };
    }

    /// <summary>Derived totals (JS: S.d).</summary>
    public class Derived
    {
        public int Civ, Bunks, Jobs, Idle, Employed;
        public double Ratio;
        public Dictionary<string, (int workers, int jobs, int blds, int shortCount)> Work = new Dictionary<string, (int, int, int, int)>();
        public bool[] Conn = new bool[N * N];
    }

    public partial class World
    {
        public Derived D { get; private set; }

        // ---------- Buildings ----------
        int PickHutVar(int x, int y)
        {
            var used = new HashSet<int>();
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (!Inb(nx, ny)) continue;
                var o = Grid[ny, nx];
                if (o != null && o.T == "house" && o.Var != 0) used.Add(o.Var);
            }
            int start = (int)Math.Floor(Noise.Hash2(x, y, 173) * HutVariants);
            for (int i = 0; i < HutVariants; i++) { int v = 1 + (start + i) % HutVariants; if (!used.Contains(v)) return v; }
            return 1 + start;
        }

        public Building AddBld(string t, int x, int y, int sz = 0)
        {
            var b = new Building { T = t, X = x, Y = y, Sz = sz != 0 ? sz : DefaultSize(t) };
            if (t == "house") b.Var = PickHutVar(x, y);
            Blds.Add(b);
            foreach (var (i, j) in b.Tiles())
            {
                int k = Tk(i, j);
                Grid[j, i] = b; DecoMap[j, i] = Deco.None; FlaxFields.Remove(k); _sownFields.Remove(k);
                RockAmtMap.Remove(k); Dig.Remove(k); Elev.Remove(k); OreLeftMap.Remove(k); SapT.Remove(k);
                if (t == "road") ZoneMap[j, i] = Zone.None;
            }
            if (t == "hall") { Hall = b; b.Water = WaterTuning.HallCistern; }   // the Town Hall starts with a full cistern
            if (t != "road" && t != "wall") LevelFootprint(b);   // roads and palisades follow the slope
            Touch();
            return b;
        }

        public Result CanAfford(string type)
        {
            foreach (var kv in CostOf(type, PlacementSize(type))) if (ResAmt[kv.Key] < kv.Value) return Result.No($"Not enough {kv.Key.ToString().ToLower()} (need {kv.Value})");
            return Result.Yes();
        }

        public Result CanPlace(string type, int x, int y)
        {
            if (type == "well" && AquiferAt(x, y) < 0) return Result.No("No groundwater here. Wells go on the blue patches");
            var fp = new Building { T = type, X = x, Y = y, Sz = PlacementSize(type) };
            foreach (var (tx, ty) in fp.Tiles())
            {
                if (!Inb(tx, ty)) return Result.No("Outside the map");
                if (!Claimed(tx, ty)) return Result.No(NotYours);
                if (Grid[ty, tx] != null) return Result.No("That tile is already built on");
                if (PenAt(tx, ty) != null) return Result.No("Remove the livestock pen first");
                if (HasTree(tx, ty)) return Result.No(TreeMsg);
                if (IsSolid(tx, ty))
                    return Result.No(IsMountain(tx, ty) ? "You cannot build on a mountain. Hills and flat ground are fine"
                        : PitDepth(tx, ty) > 0 ? "A quarry pit is in the way. Pits cannot be filled in"
                        : DecoMap[ty, tx] == Deco.Ore ? "Ore blocks this tile. Zone it for mining to clear it"
                        : "Too steep to build on");
            }
            if (type == "market" && !NextToRoad(fp)) return Result.No("Markets must touch a road");
            if (type == "smithery" && !NextToRoad(fp)) return Result.No("Smithery must touch a road");
            if (type == "barracks" && !NextToRoad(fp)) return Result.No("Barracks must touch a road");
            if ((type == "storehouse" || type == "smokehouse" || type == "charcoal") && !NextToRoad(fp))
                return Result.No($"{Names[type]} must touch a road");
            return CanAfford(type);
        }

        public Result Place(string type, int x, int y)
        {
            var c = CanPlace(type, x, y);
            if (!c.Ok) return c;
            foreach (var kv in CostOf(type, PlacementSize(type))) ResAmt[kv.Key] -= kv.Value;
            AddBld(type, x, y, PlacementSize(type));
            Recalc();
            return Result.Yes();
        }

        /// <summary>Bulldoze a building, clear a zone or clear a tree/rock/ore by hand. JS: remove.</summary>
        public Result Remove(int x, int y)
        {
            if (!Inb(x, y)) return Result.No("Outside the map");
            if (!Claimed(x, y)) return Result.No(NotYours);
            if (PenAt(x, y) is LivestockPen pen)
            {
                RemovePen(pen);
                Touch();
                return Result.Yes();
            }
            var b = Grid[y, x];
            int k = Tk(x, y);
            if (b != null)
            {
                if (b.T == "hall") return Result.No("The Town Hall cannot be removed");
                foreach (var (i, j) in b.Tiles()) { Grid[j, i] = null; ZoneMap[j, i] = Zone.None; }
                Blds.Remove(b);
                foreach (var kv in CostOf(b.T, b.Sz)) AddLooseMaterial(kv.Key, Math.Floor(kv.Value / 2.0));
                Touch(); Recalc();
                return Result.Yes();
            }
            if (ZoneMap[y, x] != Zone.None) { ZoneMap[y, x] = Zone.None; FlaxFields.Remove(k); _sownFields.Remove(k); Touch(); return Result.Yes(); }
            var d = DecoMap[y, x];
            if (d == Deco.None) return Result.No("Nothing to clear here");
            if (d == Deco.Tree) AddLooseMaterial(Res.Logs, 2);
            else if (d == Deco.Rock)
            {
                if (RockAmt(x, y) >= 3) return Result.No("A mountain cannot be cleared by hand. Mark it for quarrying");
                if (PitDepth(x, y) > 0) return Result.No("A quarry pit cannot be filled in");
                AddLooseMaterial(Res.Stone, 1); RockAmtMap.Remove(k);
            }
            else if (d == Deco.Ore) AddLooseMaterial(Res.Ore, 1);
            DecoMap[y, x] = Deco.None; StumpT.Remove(k); SapT.Remove(k);
            Touch();
            return Result.Yes();
        }

        // ---------- Roads ----------
        public Result RoadPathCheck(IList<(int x, int y)> path)
        {
            int n = 0;
            foreach (var (x, y) in path)
            {
                if (!Inb(x, y)) return Result.No("Outside the map", n);
                if (!Claimed(x, y)) return Result.No(NotYours, n);
                var g = Grid[y, x];
                if (g != null && g.T == "road") continue;
                if (g != null) return Result.No("A building is in the way", n);
                if (PenAt(x, y) != null) return Result.No("A livestock pen is in the way", n);
                if (HasTree(x, y)) return Result.No(TreeMsg, n);
                if (IsSolid(x, y)) return Result.No("A mountain, a quarry pit, ore or ground too steep to build on is in the way. Route around it", n);
                n++;
            }
            if (ResAmt[Res.Wood] < n * Cost["road"][Res.Wood]) return Result.No($"Not enough wood: {n} new road tiles need {n} wood", n);
            return Result.Yes(n);
        }

        public Result PlaceRoadPath(IList<(int x, int y)> path)
        {
            var c = RoadPathCheck(path);
            if (!c.Ok || c.Count == 0) return c;
            ResAmt[Res.Wood] -= c.Count * Cost["road"][Res.Wood];
            foreach (var (x, y) in path) if (Grid[y, x] == null) AddBld("road", x, y);
            Recalc();
            return c;
        }

        // ---------- Palisades ----------
        // A wooden palisade is a thin wall that runs through the middle of its tile and joins its neighbours. Walls are buildings, so people and
        // raiders walk around them; a road tile in the line is a gate (it stays a road and anyone can pass).
        public Result WallPathCheck(IList<(int x, int y)> path)
        {
            int n = 0;
            foreach (var (x, y) in path)
            {
                if (!Inb(x, y)) return Result.No("Outside the map", n);
                if (!Claimed(x, y)) return Result.No(NotYours, n);
                var g = Grid[y, x];
                if (PenAt(x, y) != null) return Result.No("A livestock pen is in the way", n);
                if (g != null && (g.T == "wall" || g.T == "road")) continue;   // already a wall, or a road acting as a gate
                if (g != null) return Result.No("A building is in the way", n);
                if (HasTree(x, y)) return Result.No(TreeMsg, n);
                if (IsSolid(x, y)) return Result.No("A mountain, a quarry pit, ore or ground too steep to build on is in the way", n);
                n++;
            }
            if (ResAmt[Res.Wood] < n * Cost["wall"][Res.Wood]) return Result.No($"Not enough wood: {n} palisade tiles need {n * Cost["wall"][Res.Wood]} wood", n);
            return Result.Yes(n);
        }

        public Result PlaceWallPath(IList<(int x, int y)> path)
        {
            var c = WallPathCheck(path);
            if (!c.Ok || c.Count == 0) return c;
            ResAmt[Res.Wood] -= c.Count * Cost["wall"][Res.Wood];
            foreach (var (x, y) in path) if (Grid[y, x] == null) AddBld("wall", x, y);
            Recalc();
            return c;
        }

        /// <summary>An L-shaped road path from a to b: along x first, then along y.</summary>
        public static List<(int x, int y)> LPath(int ax, int ay, int bx, int by)
        {
            var p = new List<(int, int)>();
            int sx = Math.Sign(bx - ax), sy = Math.Sign(by - ay);
            for (int x = ax; ; x += sx) { p.Add((x, ay)); if (x == bx) break; }
            if (ay != by) for (int y = ay + sy; ; y += sy) { p.Add((bx, y)); if (y == by) break; }
            return p;
        }

        // ---------- Zones ----------
        public Result ZoneCheck(Zone kind, int x, int y)
        {
            if (kind == Zone.Comm) return Result.No("Place a market with the build tool.");
            if (!Inb(x, y)) return Result.No("Outside the map");
            if (!Claimed(x, y)) return Result.No(NotYours);
            if (IsLivestockZone(kind)) return Result.No("Place livestock as a fenced rectangle");
            if (PenAt(x, y) != null) return Result.No("Remove the livestock pen first");
            if (Grid[y, x] != null) return ZoneMap[y, x] == kind ? Result.Yes() : Result.No("That tile is already built on");
            var dc = DecoMap[y, x];
            if (kind == Zone.Wood)
            {
                if (dc == Deco.Tree || dc == Deco.Sapling) return Result.Yes();
                if (dc == Deco.None || dc == Deco.Stump)
                    return IsSolid(x, y) || TileSpread(x, y) >= 2 ? Result.No("Too steep or rocky to plant. Foresters need gentle open ground") : Result.Yes();
                return Result.No("Lumber zones go on forest or open ground for a forester to replant");
            }
            if (kind == Zone.Mine)
            {
                if (dc == Deco.Rock) return RockLeft(x, y) > 0 ? Result.Yes() : Result.No("This rock is worked out. Stone does not grow back");
                if (dc == Deco.Ore) return Result.Yes();
                // open ground: miners survey it for underground ore (World.Mining.cs)
                if (dc == Deco.Tree || dc == Deco.Sapling) return Result.No("Clear the forest first. Mining zones go on rock, ore showings or open ground");
                return IsSolid(x, y) ? Result.No("Too steep or broken to mine here") : Result.Yes();
            }
            if (HasTree(x, y)) return Result.No(TreeMsg);
            if (IsSolid(x, y)) return Result.No(IsMountain(x, y) ? "Mountains cannot be built on. Hills and flat ground can" : PitDepth(x, y) > 0 ? "A quarry pit is in the way" : "Ore blocks this zone. Mine it first");
            if (kind == Zone.Farm && !Fert[y, x]) return Result.No("Farms need fertile soil");
            return Result.Yes();
        }

        /// <summary>Zone every tile of the rectangle between two corners (either order). Returns how many tiles were zoned, how many
        /// could not be, and the most common reason they could not.</summary>
        public (int zoned, int skipped, string why) ZoneRect(Zone kind, int ax, int ay, int bx, int by, bool flax = false)
        {
            if (IsLivestockZone(kind))
            {
                var pen = PlacePen(kind, ax, ay, bx, by);
                return pen.Ok ? (new LivestockPen(kind, ax, ay, bx, by).Area, 0, "") : (0, 1, pen.Reason);
            }
            int zoned = 0, skipped = 0; var reasons = new Dictionary<string, int>();
            for (int y = Math.Min(ay, by); y <= Math.Max(ay, by); y++)
                for (int x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++)
                {
                    if (AlreadyZoned(kind, x, y, flax)) continue;
                    var r = SetZone(kind, x, y, flax);
                    if (r.Ok) { if (Grid[y, x] == null) zoned++; continue; }
                    skipped++; reasons[r.Reason] = (reasons.TryGetValue(r.Reason, out var n) ? n : 0) + 1;
                }
            string why = reasons.Count == 0 ? "" : reasons.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            return (zoned, skipped, why);
        }

        /// <summary>Remove the zone from every open tile of the rectangle between two corners (either order). Buildings, and the zone
        /// under them, are left alone: this is the safe "unzone" tool, not the bulldozer. Returns how many tiles lost their zone.</summary>
        public int ClearZoneRect(int ax, int ay, int bx, int by)
        {
            int cleared = 0;
            for (int y = Math.Max(0, Math.Min(ay, by)); y <= Math.Min(N - 1, Math.Max(ay, by)); y++)
                for (int x = Math.Max(0, Math.Min(ax, bx)); x <= Math.Min(N - 1, Math.Max(ax, bx)); x++)
                {
                    if (Grid[y, x] != null || ZoneMap[y, x] == Zone.None) continue;
                    if (PenAt(x, y) is LivestockPen pen) { cleared += RemovePen(pen); continue; }
                    ZoneMap[y, x] = Zone.None; FlaxFields.Remove(Tk(x, y)); _sownFields.Remove(Tk(x, y)); cleared++;
                }
            if (cleared > 0) Touch();
            return cleared;
        }

        public Result SetZone(Zone kind, int x, int y, bool flax = false)
        {
            var c = ZoneCheck(kind, x, y);
            if (!c.Ok || Grid[y, x] != null) return c;
            if (kind != Zone.Farm) _sownFields.Remove(Tk(x, y));
            flax = kind == Zone.Farm && flax;
            int key = Tk(x, y);
            if (ZoneMap[y, x] == kind && (FlaxPercent.HasValue || FlaxFields.ContainsKey(key) == flax)) return c;

            ZoneMap[y, x] = kind;
            if (flax) FlaxFields[key] = true;
            else FlaxFields.Remove(key);
            Touch();
            return c;
        }

        // ---------- Services, faces, workforce ----------
        bool RoadOrHall(int x, int y, Building self)
        {
            if (!Inb(x, y)) return false;
            var g = Grid[y, x];
            return g != null && g != self && (g.T == "road" || g == Hall);
        }
        /// <summary>True when an open tile already carries this zone, so zoning it again changes nothing (ZoneRect skips it).</summary>
        public bool AlreadyZoned(Zone kind, int x, int y, bool flax = false) =>
            Inb(x, y) && Grid[y, x] == null && ZoneMap[y, x] == kind && (FlaxPercent.HasValue || IsFlaxField(x, y) == (kind == Zone.Farm && flax));

        /// <summary>True when the tile touches a road connected to the Town Hall (or the Hall itself): homes only grow on such tiles.</summary>
        public bool BesideConnectedRoad(int x, int y)
        {
            for (int k = 0; k < 4; k++)
            {
                int i = x + Nb[k, 0], j = y + Nb[k, 1];
                if (!Inb(i, j) || Grid[j, i] == null) continue;
                if ((Grid[j, i].T == "road" && Grid[j, i].Served) || Grid[j, i].T == "hall") return true;
            }
            return false;
        }

        /// <summary>True when a building of this type placed at x,y would touch a connected road, so it would be served at once.</summary>
        public bool WouldConnect(string type, int x, int y)
        {
            var fp = new Building { T = type, X = x, Y = y, Sz = PlacementSize(type) };
            foreach (var (tx, ty) in fp.Tiles()) if (BesideConnectedRoad(tx, ty)) return true;
            return false;
        }

        /// <summary>How many zoned open tiles a building of this type placed at x,y would cover (the zone is lost under it).</summary>
        public int ZonedUnder(string type, int x, int y)
        {
            int n = 0;
            var fp = new Building { T = type, X = x, Y = y, Sz = PlacementSize(type) };
            foreach (var (tx, ty) in fp.Tiles()) if (Inb(tx, ty) && Grid[ty, tx] == null && ZoneMap[ty, tx] != Zone.None) n++;
            return n;
        }

        bool NextToRoad(Building b)
        {
            foreach (var (x, y) in b.Tiles()) for (int k = 0; k < 4; k++) if (RoadOrHall(x + Nb[k, 0], y + Nb[k, 1], b)) return true;
            return false;
        }
        int FaceOf(Building b)
        {
            bool Has(int f) { foreach (var (x, y) in b.Tiles()) if (RoadOrHall(x + FaceDirs[f, 0], y + FaceDirs[f, 1], b)) return true; return false; }
            if (b.Face >= 0 && Has(b.Face)) return b.Face;
            for (int f = 0; f < 4; f++) if (Has(f)) return f;
            return b.Face >= 0 ? b.Face : 0;
        }
        bool ServedAt(Building b, bool[] conn)
        {
            foreach (var (tx, ty) in b.Tiles())
                for (int k = 0; k < 4; k++)
                {
                    int x = tx + Nb[k, 0], y = ty + Nb[k, 1];
                    if (!Inb(x, y) || Grid[y, x] == b) continue;
                    if (conn[Tk(x, y)] || Grid[y, x] == Hall) return true;
                }
            return false;
        }

        /// <summary>Road connectivity, service, house levels and the workforce, recomputed from scratch. JS: recalc / auditCaches.</summary>
        public Derived Recalc()
        {
            var d = new Derived();
            var conn = d.Conn;
            bool IsRoad(int x, int y) => Inb(x, y) && Grid[y, x] != null && Grid[y, x].T == "road";
            var stack = new Stack<(int, int)>();
            foreach (var (hx, hy) in Hall.Tiles())
                for (int k = 0; k < 4; k++)
                {
                    int x = hx + Nb[k, 0], y = hy + Nb[k, 1];
                    if (Inb(x, y) && Grid[y, x] != Hall && IsRoad(x, y) && !conn[Tk(x, y)]) { conn[Tk(x, y)] = true; stack.Push((x, y)); }
                }
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                for (int k = 0; k < 4; k++) { int x = cx + Nb[k, 0], y = cy + Nb[k, 1]; if (IsRoad(x, y) && !conn[Tk(x, y)]) { conn[Tk(x, y)] = true; stack.Push((x, y)); } }
            }

            foreach (var b in Blds)
            {
                if (b.T == "road") { b.Served = conn[Tk(b.X, b.Y)]; continue; }
                // a mine's entrance faces away from the high ground it tunnels into, and needs its road right there (World.Mining.cs)
                int mf = b.T == "mine" ? MineFaceAt(b.X, b.Y, b.Sz) : -1;
                if (mf >= 0) { b.Face = mf; b.Served = EntranceServed(b, mf, conn); continue; }
                b.Face = FaceOf(b);
                b.Served = b.T == "hall" || ServedAt(b, conn);
            }
            _farmFieldCache = null;   // which farmhouse works which field depends on which farmhouses are served
            RefreshFarmJobs();
            RefreshWorkTiles();
            int civ = HallCap, bunks = 0, jobs = 0;
            foreach (var b in Blds)
            {
                if (b.T == "house")
                {
                    // the Unity game: a house is the level it was built as (Tier), whatever the water (water is only needed to survive: a parched
                    // home holds half its people, World.Water.cs). Classic keeps the browser's rule: level 2 while served and not parched.
                    b.Lvl = SeasonalFarming ? b.Tier : b.Served && b.ShortDays < WaterTuning.Grace ? 2 : 1;
                    if (b.Served)
                    {
                        civ += b.Housing;
                    }
                }
                else if (b.T == "barracks") { if (b.Served) bunks += Bunks; }
                else if (Defs.Jobs.ContainsKey(b.T) && b.Served) jobs += ActiveJobs(b);
            }
            // livestock pens with animals need a keeper each (farming trade, after the farms)
            var keptPens = _pens.Where(PenNeedsKeeper).ToList();
            jobs += keptPens.Count * KeepersPerPen;

            // Fill the jobs: villagers go to trades in priority order, and within a trade to the oldest buildings first.
            int avail = Peasants;
            foreach (var b in Blds) { b.Workers = 0; b.Eff = 0; }
            foreach (var p in _pens) p.Keepers = 0;
            foreach (var t in Prio)
            {
                int w = 0, j = 0, n = 0, sh = 0;
                int room = TradeTarget.TryGetValue(t, out var hand) ? hand : int.MaxValue;   // the player's limit for this trade, if set
                foreach (var b in Blds)
                {
                    if (!b.Served || !TradeOf.TryGetValue(b.T, out var tr) || tr != t) continue;
                    int bj = ActiveJobs(b), bw = Math.Min(bj, Math.Min(avail, room)); avail -= bw; room -= bw;
                    b.Workers = bw; b.Eff = bj > 0 ? (double)bw / bj : 0;
                    w += bw; j += bj; n++; if (bw < bj) sh++;
                }
                if (t == "farming")
                    foreach (var p in keptPens)
                    {
                        int pw = Math.Min(KeepersPerPen, Math.Min(avail, room)); avail -= pw; room -= pw;
                        p.Keepers = pw;
                        w += pw; j += KeepersPerPen; if (pw < KeepersPerPen) sh++;
                    }
                d.Work[t] = (w, j, n, sh);
            }
            d.Civ = civ; d.Bunks = bunks; d.Jobs = jobs; d.Idle = avail; d.Employed = Peasants - avail;
            d.Ratio = jobs > 0 ? Math.Min(1, (double)Peasants / jobs) : 0;
            D = d;
            return d;
        }
    }
}
