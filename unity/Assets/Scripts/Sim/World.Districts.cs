// Districts (DESIGN_ORGANIC_DEVELOPMENT.md, phase 1): connected patches of one kind of economic zone, with stable identity, names and settings.
//
// A district is DERIVED from the zone map: every rebuild flood-fills the zone tiles (4-connected, one district type per patch) in scan order
// (y, then x) and hands each patch an identity from the districts it overlaps, so expanding, splitting and merging are deterministic and
// the player's settings survive. Only identity and settings are saved; tiles, buildings and totals are recomputed (and a save from before
// districts simply gets districts from its zone map: that is the whole migration).
//
// Governor-ready: everything here is a World method that returns data; there is no UI in this file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>How suitable one tile is for a zone kind: Ok (the zone rules allow it), a 0..1 score and a plain-words reason.</summary>
    public struct SiteScore { public bool Ok; public double Score; public string Reason; }

    public partial class World
    {
        public const int FishZoneReach = 3, DistrictHistoryKeep = 8, RoadSearchR = 8;

        readonly List<District> _districts = new List<District>();
        readonly Dictionary<int, District> _districtById = new Dictionary<int, District>();
        int[] _districtMap = new int[N * N];   // tile -> district id (0 = none)
        int _nextDistrictId = 1, _districtVersion = -1;

        // ---------- Kinds ----------
        /// <summary>The district type a zone belongs to (both housing levels are Residential); null for zones that are not districts (commerce, livestock pens).</summary>
        public static DistrictType? DistrictTypeOf(Zone z) => z switch
        {
            Zone.Res or Zone.Res2 => DistrictType.Residential, Zone.Farm => DistrictType.Agricultural, Zone.Wood => DistrictType.Forestry,
            Zone.Mine => DistrictType.Mining, Zone.Quarry => DistrictType.Quarrying, Zone.Hunt => DistrictType.Hunting, Zone.Fish => DistrictType.Fishing,
            _ => (DistrictType?)null,
        };

        /// <summary>The ordinary economic building types a district of this type holds (manual ones included). Strategic buildings are never here.</summary>
        public static DistrictType? DistrictTypeOfBuilding(string type) => type switch
        {
            "house" => DistrictType.Residential, "farm" => DistrictType.Agricultural, "wood" or "forester" => DistrictType.Forestry,
            "mine" => DistrictType.Mining, "quarry" => DistrictType.Quarrying, "hunter" => DistrictType.Hunting, "fisher" => DistrictType.Fishing,
            _ => (DistrictType?)null,
        };

        /// <summary>The zone you paint to make a district of this type (Residential: Level 1 housing; Level 2 housing joins the same district).</summary>
        public static Zone DefaultZone(DistrictType t) => t switch
        {
            DistrictType.Residential => Zone.Res, DistrictType.Agricultural => Zone.Farm, DistrictType.Forestry => Zone.Wood, DistrictType.Mining => Zone.Mine,
            DistrictType.Quarrying => Zone.Quarry, DistrictType.Hunting => Zone.Hunt, _ => Zone.Fish,
        };

        public static string TypeName(DistrictType t) => t switch
        {
            DistrictType.Residential => "Residential", DistrictType.Agricultural => "Agricultural", DistrictType.Forestry => "Forestry",
            DistrictType.Mining => "Mining", DistrictType.Quarrying => "Quarrying", DistrictType.Hunting => "Hunting", _ => "Fishing",
        };

        public bool NearWater(int x, int y, int r)
        {
            for (int j = y - r; j <= y + r; j++)
                for (int i = x - r; i <= x + r; i++)
                    if (Inb(i, j) && IsWaterTile(i, j)) return true;
            return false;
        }

        // ---------- Queries ----------
        void EnsureDistricts() { if (_districtVersion != Version) RebuildDistricts(); }

        /// <summary>Every district, by id. Rebuilt on demand when zones or buildings changed.</summary>
        public IReadOnlyList<District> Districts { get { EnsureDistricts(); return _districts; } }

        public District DistrictAt(int x, int y)
        {
            if (!Inb(x, y)) return null;
            EnsureDistricts();
            int id = _districtMap[Tk(x, y)];
            return id != 0 && _districtById.TryGetValue(id, out var d) ? d : null;
        }

        public District DistrictById(int id) { EnsureDistricts(); return _districtById.TryGetValue(id, out var d) ? d : null; }
        public IEnumerable<District> DistrictsOf(DistrictType t) => Districts.Where(d => d.Type == t);

        /// <summary>An ordinary economic building that stands outside every district of its kind. It keeps working exactly as before ("legacy" behaviour,
        /// see the design document): the engine never counts it toward a district's capacity and never demolishes it.</summary>
        public bool IsLegacyBuilding(Building b)
        {
            if (b == null || DistrictTypeOfBuilding(b.T) == null) return false;
            EnsureDistricts();
            return OwnerDistrict(b, _districtMap) == null;
        }

        /// <summary>The district an ordinary building belongs to: the one of its kind it stands in, else the lowest-numbered one of its kind it touches (a farmhouse
        /// beside its fields, a lodge at the edge of its forest). Null for a legacy building (stands in or beside none).</summary>
        District OwnerDistrict(Building b, int[] map)
        {
            if (DistrictTypeOfBuilding(b.T) is not DistrictType t) return null;
            int touching = int.MaxValue;
            foreach (var (x, y) in b.Tiles())
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        if (!Inb(x + i, y + j) || !_districtById.TryGetValue(map[Tk(x + i, y + j)], out var d) || d.Type != t) continue;
                        if (i == 0 && j == 0) return d;
                        touching = Math.Min(touching, d.Id);
                    }
            return touching == int.MaxValue ? null : _districtById[touching];
        }

        public IEnumerable<Building> LegacyBuildings() => Blds.Where(IsLegacyBuilding);

        // ---------- Rebuild ----------
        /// <summary>Recompute every district from the zone map, keeping identities and settings (see the file header for the rules).</summary>
        public void RebuildDistricts()
        {
            var oldMap = _districtMap; var oldById = new Dictionary<int, District>(_districtById);
            var map = new int[N * N]; var seen = new bool[N * N];
            var comps = new List<(DistrictType type, List<int> tiles)>();
            var stack = new Stack<int>();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k0 = Tk(x, y);
                    if (seen[k0] || DistrictTypeOf(ZoneMap[y, x]) is not DistrictType t) continue;
                    var tiles = new List<int>(); stack.Push(k0); seen[k0] = true;
                    while (stack.Count > 0)
                    {
                        int k = stack.Pop(); tiles.Add(k); int kx = k % N, ky = k / N;
                        for (int n = 0; n < 4; n++)
                        {
                            int nx = kx + Nb[n, 0], ny = ky + Nb[n, 1];
                            if (!Inb(nx, ny) || seen[Tk(nx, ny)] || DistrictTypeOf(ZoneMap[ny, nx]) != t) continue;
                            seen[Tk(nx, ny)] = true; stack.Push(Tk(nx, ny));
                        }
                    }
                    tiles.Sort();
                    comps.Add((t, tiles));
                }

            // how much of each old district lies in each patch
            var overlap = new Dictionary<int, int>[comps.Count];
            for (int c = 0; c < comps.Count; c++)
            {
                overlap[c] = new Dictionary<int, int>();
                foreach (int k in comps[c].tiles)
                    if (oldMap[k] != 0 && oldById.TryGetValue(oldMap[k], out var od) && od.Type == comps[c].type)
                        overlap[c][oldMap[k]] = (overlap[c].TryGetValue(oldMap[k], out var n) ? n : 0) + 1;
            }
            // each old district goes to the patch holding most of it (ties: the earlier patch); each patch keeps the identity of the biggest
            // old district that chose it (ties: the lower id). The others are merged into it (their ids end) or, when no old district chose a
            // patch, it is a split-off or new district.
            var chose = new Dictionary<int, int>();
            foreach (int id in oldById.Keys.OrderBy(i => i))
            {
                int best = -1, bestN = 0;
                for (int c = 0; c < comps.Count; c++) if (overlap[c].TryGetValue(id, out var n) && n > bestN) { best = c; bestN = n; }
                if (best >= 0) chose[id] = best;
            }
            var next = new List<District>();
            for (int c = 0; c < comps.Count; c++)
            {
                var cands = chose.Where(p => p.Value == c).Select(p => p.Key).OrderByDescending(id => overlap[c][id]).ThenBy(id => id).ToList();
                District d;
                if (cands.Count > 0)
                {
                    d = oldById[cands[0]];
                    for (int i = 1; i < cands.Count; i++) AddHistory(d, $"Day {Day}: merged with {oldById[cands[i]].Name}");
                }
                else
                {
                    d = new District { Id = _nextDistrictId++, Type = comps[c].type, Founded = Day };
                    var parent = overlap[c].OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => oldById[p.Key]).FirstOrDefault();
                    if (parent != null)
                    {
                        d.Priority = parent.Priority; d.Mode = parent.Mode; d.Limits = parent.Limits.Copy();
                        AddHistory(d, $"Day {Day}: split from {parent.Name}");
                    }
                    else AddHistory(d, $"Day {Day}: zoned");
                }
                d.Tiles.Clear(); d.Tiles.AddRange(comps[c].tiles);
                foreach (int k in d.Tiles) map[k] = d.Id;
                next.Add(d);
            }
            _districts.Clear(); _districts.AddRange(next.OrderBy(d => d.Id));
            _districtById.Clear(); foreach (var d in _districts) _districtById[d.Id] = d;
            _districtMap = map;
            foreach (var d in _districts) { MeasureDistrict(d); d.Buildings.Clear(); d.Jobs = d.Workers = 0; }
            foreach (var b in Blds)
            {
                var owner = OwnerDistrict(b, map);
                if (owner != null) { owner.Buildings.Add(b); owner.Jobs += b.Jobs; owner.Workers += b.Workers; }
            }
            foreach (var d in _districts) { d.OpenTiles = d.Tiles.Count(k => Grid[k / N, k % N] == null); }
            NameDistricts();
            _districtVersion = Version;
        }

        static void AddHistory(District d, string text)
        {
            d.History.Add(text);
            while (d.History.Count > DistrictHistoryKeep) d.History.RemoveAt(0);
        }

        static void MeasureDistrict(District d)
        {
            d.MinX = d.MinY = int.MaxValue; d.MaxX = d.MaxY = int.MinValue; double sx = 0, sy = 0;
            foreach (int k in d.Tiles)
            {
                int x = k % N, y = k / N;
                d.MinX = Math.Min(d.MinX, x); d.MaxX = Math.Max(d.MaxX, x); d.MinY = Math.Min(d.MinY, y); d.MaxY = Math.Max(d.MaxY, y);
                sx += x; sy += y;
            }
            d.CenterX = sx / d.Tiles.Count; d.CenterY = sy / d.Tiles.Count;
        }

        // ---------- Names ----------
        /// <summary>North is -y, east is +x on the tile grid (the map's own axes; the camera may rotate, the names do not).</summary>
        static string Compass(double dx, double dy)
        {
            if (Math.Abs(dx) <= 10 && Math.Abs(dy) <= 10) return "Central";
            double ax = Math.Abs(dx), ay = Math.Abs(dy);
            string ns = dy < 0 ? "North" : "South", ew = dx < 0 ? "West" : "East";
            if (ay > 2 * ax) return ns + "ern";
            if (ax > 2 * ay) return ew + "ern";
            return ns.ToLowerInvariant() == "north" ? (dx < 0 ? "Northwestern" : "Northeastern") : (dx < 0 ? "Southwestern" : "Southeastern");
        }

        string AutoName(District d)
        {
            double hx = Hall != null ? Hall.X + Hall.Ctr : Start, hy = Hall != null ? Hall.Y + Hall.Ctr : Start;
            string dir = Compass(d.CenterX - hx, d.CenterY - hy);
            bool riverside = d.Tiles.Count(k => NearWater(k % N, k / N, 2)) * 4 >= d.Tiles.Count;
            switch (d.Type)
            {
                case DistrictType.Residential: return riverside ? "Riverside Residential District" : $"{dir} Residential District";
                case DistrictType.Agricultural: return riverside ? "Riverside Farmland" : $"{dir} Farmland";
                case DistrictType.Forestry: return $"{dir} Forestry District";
                case DistrictType.Mining: return $"{dir} Iron District";   // iron is the only ore today; the name follows the preferred mineral once there are more
                case DistrictType.Quarrying:
                    int mountain = d.Tiles.Count(k => IsMountain(k % N, k / N)), high = d.Tiles.Count(k => TileLevel(k % N, k / N) >= 1);
                    return mountain * 2 > d.Tiles.Count ? "Mountain Quarry" : high * 2 > d.Tiles.Count ? "Hill Quarry" : $"{dir} Quarry";
                case DistrictType.Hunting: return $"{dir} Hunting Grounds";
                default: return $"{dir} Fishing Shore";
            }
        }

        static readonly string[] Numerals = { "", "", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        void NameDistricts()
        {
            var used = new HashSet<string>(_districts.Where(d => d.CustomName).Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var d in _districts.Where(d => !d.CustomName))
            {
                string name = AutoName(d), baseName = name; int n = 2;
                while (used.Contains(name)) { name = $"{baseName} {(n < Numerals.Length ? Numerals[n] : n.ToString())}"; n++; }
                used.Add(name); d.Name = name;
            }
        }

        // ---------- Operations (what a governor can call) ----------
        /// <summary>Paint a zone rectangle for a district type and return the district that now holds the first zoned tile (null when nothing could be zoned).</summary>
        public (District district, int zoned, int skipped, string why) ZoneDistrict(DistrictType type, int ax, int ay, int bx, int by)
        {
            var (zoned, skipped, why) = ZoneRect(DefaultZone(type), ax, ay, bx, by);
            District d = null;
            for (int y = Math.Min(ay, by); y <= Math.Max(ay, by) && d == null; y++)
                for (int x = Math.Min(ax, bx); x <= Math.Max(ax, bx) && d == null; x++)
                    if (DistrictTypeOf(ZoneMap[Math.Clamp(y, 0, N - 1), Math.Clamp(x, 0, N - 1)]) == type) d = DistrictAt(x, y);
            return (d, zoned, skipped, why);
        }

        public Result SetDistrictPriority(int id, DevPriority p)
        {
            var d = DistrictById(id); if (d == null) return Result.No("There is no such district");
            d.Priority = p; return Result.Yes();
        }

        public Result SetDistrictMode(int id, DevMode m)
        {
            var d = DistrictById(id); if (d == null) return Result.No("There is no such district");
            d.Mode = m; return Result.Yes();
        }

        /// <summary>Rename a district; an empty name goes back to the automatic one.</summary>
        public Result RenameDistrict(int id, string name)
        {
            var d = DistrictById(id); if (d == null) return Result.No("There is no such district");
            name = (name ?? "").Trim();
            if (name.Length > 40) return Result.No("District names can be up to 40 letters");
            if (name.Length == 0) { d.CustomName = false; _districtVersion = -1; return Result.Yes(); }
            if (_districts.Any(o => o.Id != id && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))) return Result.No("Another district already has that name");
            d.Name = name; d.CustomName = true; AddHistory(d, $"Day {Day}: renamed {name}"); return Result.Yes();
        }

        public Result SetDistrictLimits(int id, DistrictLimits limits)
        {
            var d = DistrictById(id); if (d == null) return Result.No("There is no such district");
            if (limits.MaxBuildings < 0 || limits.MaxEmployment < 0 || limits.MaxSpend < 0 || limits.MinWood < 0 || limits.MinStone < 0 || limits.ProductionTarget < 0)
                return Result.No("Limits cannot be negative");
            d.Limits = limits.Copy(); return Result.Yes();
        }

        /// <summary>Remove a district's zone from every open tile. Buildings (and the zone under them) stay: this never demolishes. Returns tiles unzoned.</summary>
        public int RemoveDistrictZone(int id)
        {
            var d = DistrictById(id); if (d == null) return 0;
            int cleared = 0;
            foreach (int k in d.Tiles.ToList())
            {
                int x = k % N, y = k / N;
                if (Grid[y, x] != null) continue;
                ZoneMap[y, x] = Zone.None; FlaxFields.Remove(k); _sownFields.Remove(k); cleared++;
            }
            if (cleared > 0) Touch();
            return cleared;
        }

        // ---------- Suitability (real data only; shown through overlays in a later phase) ----------
        int RoadDistance(int x, int y)
        {
            int best = int.MaxValue;
            for (int j = y - RoadSearchR; j <= y + RoadSearchR; j++)
                for (int i = x - RoadSearchR; i <= x + RoadSearchR; i++)
                    if (Inb(i, j) && (D.Conn[Tk(i, j)] || Grid[j, i] == Hall)) best = Math.Min(best, Math.Max(Math.Abs(i - x), Math.Abs(j - y)));
            return best;
        }

        double Density(int x, int y, int r, Func<int, int, bool> pred)
        {
            int n = 0, all = 0;
            for (int j = y - r; j <= y + r; j++)
                for (int i = x - r; i <= x + r; i++) if (Inb(i, j)) { all++; if (pred(i, j)) n++; }
            return all == 0 ? 0 : n / (double)all;
        }

        /// <summary>How good this tile is for a zone kind. Ok follows the same ZoneCheck the zone tools use; the score weighs road access and the
        /// resource that kind lives on (fertile soil, trees, rock, ore showings, shoreline). A score is a guide, not a promise.</summary>
        public SiteScore Suitability(Zone kind, int x, int y)
        {
            if (DistrictTypeOf(kind) is not DistrictType t) return new SiteScore { Reason = "That is not an economic zone" };
            var c = ZoneCheck(kind, x, y);
            if (!c.Ok) return new SiteScore { Reason = c.Reason };
            int rd = RoadDistance(x, y);
            double access = rd == int.MaxValue ? 0.1 : Math.Max(0, 1 - (rd - 1) / (double)RoadSearchR);
            double flat = 1 - Math.Min(2, TileSpread(x, y)) / 2.0, score; string why;
            switch (t)
            {
                case DistrictType.Residential: score = 0.5 * access + 0.5 * flat; why = "roads and level ground"; break;
                case DistrictType.Agricultural: score = 0.5 + 0.3 * access + 0.2 * flat; why = "fertile soil, roads, level ground"; break;
                case DistrictType.Forestry: score = 0.6 * Math.Min(1, 2 * Density(x, y, 3, HasTree)) + 0.4 * access; why = "tree cover and roads"; break;
                case DistrictType.Mining: score = 0.6 * Math.Min(1, Density(x, y, 4, (i, j) => DecoMap[j, i] == Deco.Ore) * 6) + 0.2 * (TileLevel(x, y) >= 1 ? 1 : 0) + 0.2 * access; why = "ore showings, high ground and roads (underground ore stays hidden until a tunnel finds it)"; break;
                case DistrictType.Quarrying: score = 0.7 * Math.Min(1, RockLeft(x, y) / Math.Max(1, RockCap(x, y))) + 0.3 * access; why = "stone left in the rock and roads"; break;
                case DistrictType.Hunting: score = 0.7 * Math.Min(1, 2 * Density(x, y, 4, HasTree)) + 0.3 * access; why = "forest cover and roads"; break;
                default:
                    int wd = 1; while (wd < FishZoneReach && !NearWater(x, y, wd)) wd++;
                    score = 0.7 * (1 - (wd - 1) / (double)FishZoneReach) + 0.3 * access; why = "closeness to open water and roads"; break;
            }
            return new SiteScore { Ok = true, Score = Math.Round(Math.Clamp(score, 0, 1), 3), Reason = why };
        }

        /// <summary>One line for lists and tooltips.</summary>
        public string DistrictSummary(District d) =>
            $"{d.Name}: {d.Area} tiles, {d.Buildings.Count} building{(d.Buildings.Count == 1 ? "" : "s")}, {d.Workers}/{d.Jobs} workers; development {d.Priority}, {d.Mode}";

        // ---------- Save ----------
        JObject SaveDistricts()
        {
            EnsureDistricts();
            var list = new JArray();
            foreach (var d in _districts)
            {
                var runs = new JArray(); int i = 0;
                while (i < d.Tiles.Count)   // tiles are ascending: runs of consecutive keys in one row become [y, x, length]
                {
                    int k = d.Tiles[i], len = 1;
                    while (i + len < d.Tiles.Count && d.Tiles[i + len] == k + len && (k % N) + len < N) len++;
                    runs.Add(k / N); runs.Add(k % N); runs.Add(len); i += len;
                }
                list.Add(new JObject
                {
                    ["id"] = d.Id, ["type"] = d.Type.ToString().ToLowerInvariant(), ["name"] = d.Name, ["custom"] = d.CustomName,
                    ["prio"] = d.Priority.ToString().ToLowerInvariant(), ["mode"] = d.Mode.ToString().ToLowerInvariant(), ["founded"] = d.Founded,
                    ["limits"] = new JObject { ["bld"] = d.Limits.MaxBuildings, ["jobs"] = d.Limits.MaxEmployment, ["spend"] = d.Limits.MaxSpend, ["wood"] = d.Limits.MinWood, ["stone"] = d.Limits.MinStone, ["target"] = d.Limits.ProductionTarget },
                    ["history"] = new JArray(d.History), ["runs"] = runs,
                });
            }
            return new JObject { ["next"] = _nextDistrictId, ["list"] = list };
        }

        /// <summary>Restore district identities. Called after the zone map is loaded; the next access rebuilds tiles and totals from the zones, so a
        /// tile whose zone no longer matches is simply dropped. A save without districts leaves them to be derived from the zones (the migration).</summary>
        void LoadDistricts(JObject o, int size, int offset)
        {
            _districts.Clear(); _districtById.Clear(); _districtMap = new int[N * N]; _nextDistrictId = 1; _districtVersion = -1;
            if (o == null) return;
            int maxId = 0;
            foreach (var j in (o["list"] as JArray) ?? new JArray())
            {
                int id = j.Value<int?>("id") ?? 0;
                if (id <= 0 || _districtById.ContainsKey(id) || !Enum.TryParse(j.Value<string>("type"), true, out DistrictType type)) continue;
                var d = new District
                {
                    Id = id, Type = type, Name = j.Value<string>("name") ?? "", CustomName = j.Value<bool?>("custom") ?? false, Founded = j.Value<int?>("founded") ?? 0,
                    Priority = Enum.TryParse(j.Value<string>("prio"), true, out DevPriority pr) ? pr : DevPriority.Normal,
                    Mode = Enum.TryParse(j.Value<string>("mode"), true, out DevMode md) ? md : DevMode.Automatic,
                };
                if (j["limits"] is JObject lm)
                    d.Limits = new DistrictLimits
                    {
                        MaxBuildings = Math.Max(0, lm.Value<int?>("bld") ?? 0), MaxEmployment = Math.Max(0, lm.Value<int?>("jobs") ?? 0), MaxSpend = Math.Max(0, lm.Value<int?>("spend") ?? 0),
                        MinWood = Math.Max(0, lm.Value<int?>("wood") ?? 0), MinStone = Math.Max(0, lm.Value<int?>("stone") ?? 0), ProductionTarget = Math.Max(0, lm.Value<int?>("target") ?? 0),
                    };
                foreach (var h in (j["history"] as JArray) ?? new JArray()) AddHistory(d, (string)h);
                if (j["runs"] is JArray runs)
                    for (int i = 0; i + 2 < runs.Count; i += 3)
                    {
                        int ry = (int)runs[i], rx = (int)runs[i + 1], len = (int)runs[i + 2];
                        if (ry < 0 || ry >= size || rx < 0 || len < 1 || rx + len > size) continue;   // outside the saved map (a hand-edited save): that identity is simply not restored
                        for (int n = 0; n < len; n++)
                        {
                            int x = rx + n + offset, y = ry + offset;
                            if (DistrictTypeOf(ZoneMap[y, x]) == type) _districtMap[Tk(x, y)] = id;   // only where the zone still matches
                        }
                    }
                _districts.Add(d); _districtById[id] = d; maxId = Math.Max(maxId, id);
            }
            _nextDistrictId = Math.Max(o.Value<int?>("next") ?? 1, maxId + 1);
        }
    }
}
