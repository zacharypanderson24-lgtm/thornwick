// The development queue (DESIGN_ORGANIC_DEVELOPMENT.md, phase 2): project creation, funding, construction, approval, cancel and save.
// Housing (World.Develop.cs) is the first user; later phases add farms, lodges, quarries, mines, hunting lodges and fishing huts through the same calls.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int RejectDays = 30, ProjectKeepDays = 20;
        public readonly List<DevProject> Projects = new List<DevProject>();
        public readonly Dictionary<int, int> RejectedUntil = new Dictionary<int, int>();   // tile key -> day a rejected proposal may come back
        public DevReserve Reserve = new DevReserve();
        int _nextProjectId = 1, _buildersUsed;

        public IEnumerable<DevProject> OpenProjects => Projects.Where(p => p.Open);

        /// <summary>The unified queue as the player sees it: open projects, the player's ranking first, then district priority, then age.</summary>
        public List<DevProject> DevQueue() => OpenProjects
            .OrderByDescending(p => p.Rank).ThenByDescending(p => (int)(DistrictById(p.DistrictId)?.Priority ?? DevPriority.Normal)).ThenBy(p => p.Id).ToList();

        public DevProject ProjectById(int id) => Projects.FirstOrDefault(p => p.Id == id);

        /// <summary>Why this district may not start another project right now (null when it may). Hard player settings always win.</summary>
        public string DevGate(District d)
        {
            if (d.Priority == DevPriority.Disabled) return "Development is disabled for this district.";
            if (d.Mode == DevMode.Paused) return "Development is paused. Existing buildings keep working.";
            if (OpenProjects.Any(p => p.DistrictId == d.Id)) return "Existing projects have priority: one is still open here.";
            if (d.Limits.MaxBuildings > 0 && d.Buildings.Count >= d.Limits.MaxBuildings) return $"Building limit reached ({d.Limits.MaxBuildings}).";
            if (d.Limits.ProductionTarget > 0 && DistrictOutputPerDay(d) >= d.Limits.ProductionTarget) return $"Production target reached ({DistrictOutputPerDay(d):0.#} of {d.Limits.ProductionTarget} {DistrictOutputUnit(d.Type)}).";
            if (OpenProjects.Count(p => p.Status != ProjectStatus.Proposed) >= Math.Max(1, Reserve.MaxOpenProjects)) return "Other projects are using the settlement's construction capacity.";
            return null;
        }

        /// <summary>The wood, stone or gold the store must keep back (the district's own reserve or the settlement's, whichever is larger); null when the cost is fine.</summary>
        public string ReserveBlock(District d, Dictionary<Res, int> cost)
        {
            if (d != null && d.Limits.MaxSpend > 0 && SpentThisYear(d) + cost.Values.Sum() > d.Limits.MaxSpend) return $"Spending limit reached: {SpentThisYear(d)} of {d.Limits.MaxSpend} units used this year, and this project costs {cost.Values.Sum()}.";
            int keepWood = Math.Max(Reserve.KeepWood, d?.Limits.MinWood ?? 0), keepStone = Math.Max(Reserve.KeepStone, d?.Limits.MinStone ?? 0);
            if (cost.TryGetValue(Res.Wood, out var w) && w > 0 && ResAmt[Res.Wood] - w < keepWood) return $"Keeping {keepWood} wood in reserve.";
            if (cost.TryGetValue(Res.Stone, out var s) && s > 0 && ResAmt[Res.Stone] - s < keepStone) return $"Keeping {keepStone} stone in reserve.";
            if (cost.TryGetValue(Res.Gold, out var g) && g > 0 && ResAmt[Res.Gold] - g < Reserve.KeepGold) return $"Keeping {Reserve.KeepGold} gold in reserve.";
            return null;
        }

        bool SiteFree(int x, int y, int sz)
        {
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sz; i++)
                {
                    int tx = x + i, ty = y + j;
                    if (!Inb(tx, ty) || Grid[ty, tx] != null || IsSolid(tx, ty) || HasTree(tx, ty) || !Claimed(tx, ty)) return false;
                    foreach (var p in Projects) if (p.Open && tx >= p.X && tx < p.X + p.Sz && ty >= p.Y && ty < p.Y + p.Sz) return false;
                }
            return true;
        }

        static string Needs(Dictionary<Res, int> cost, IReadOnlyDictionary<Res, double> have)
        {
            var parts = cost.Where(c => c.Value > 0 && have[c.Key] + 1e-9 < c.Value)
                .Select(c => $"{(int)Math.Ceiling(c.Value - have[c.Key])} {c.Key.ToString().ToLowerInvariant()}").ToList();
            return parts.Count == 0 ? "" : "Waiting for " + string.Join(" and ", parts) + ".";
        }

        /// <summary>Create a project. Funded at once unless the district asks for approval (then it is a proposal that holds nothing). Returns null with a reason
        /// when the site is not free; the engine's own gates (DevGate, ReserveBlock) are checked by the caller.</summary>
        public (DevProject project, string why) StartProject(District d, string type, int x, int y, int sz, int tier, Dictionary<Res, int> cost, int buildDays, string reason, string benefit)
        {
            if (d == null || !Names.ContainsKey(type)) return (null, "No such district or building.");
            if (RejectedUntil.TryGetValue(Tk(x, y), out var until) && until > Day) return (null, "You rejected a project here recently.");
            if (!SiteFree(x, y, sz)) return (null, "The site is not free.");
            var p = new DevProject
            {
                Id = _nextProjectId++, DistrictId = d.Id, Type = type, X = x, Y = y, Sz = sz, Tier = tier, Cost = new Dictionary<Res, int>(cost), BuildDays = buildDays,
                Created = Day, Reason = reason ?? "", Benefit = benefit ?? "", Status = d.Mode == DevMode.ApprovalRequired ? ProjectStatus.Proposed : ProjectStatus.Waiting,
            };
            Projects.Add(p);
            if (p.Status == ProjectStatus.Waiting) { if (TryFund(p) && p.BuildDays == 0) StepOne(p); }   // work starts with the next day; a 0-day project is finished now
            else p.Missing = "Waiting for your approval.";
            return (p, "");
        }

        bool TryFund(DevProject p)
        {
            if (p.Status != ProjectStatus.Waiting) return p.Status == ProjectStatus.Building;
            if (p.Cost.Any(c => ResAmt[c.Key] + 1e-9 < c.Value)) { p.Missing = Needs(p.Cost, ResAmt); return false; }
            foreach (var c in p.Cost) { ResAmt[c.Key] -= c.Value; p.Held[c.Key] = c.Value; }
            DistrictById(p.DistrictId)?.SpendLog.Add((Day, p.Cost.Values.Sum()));
            p.Status = ProjectStatus.Building; p.Missing = "";
            return true;
        }

        void StepOne(DevProject p)
        {
            if (p.Status != ProjectStatus.Building || p.Paused) return;
            if (p.Progress < p.BuildDays)
            {
                if (D.Idle - _buildersUsed < 1) { p.Missing = "Waiting for a free builder."; return; }
                p.Progress++; _buildersUsed++; p.Missing = "";
                if (p.Progress < p.BuildDays) return;
            }
            if (!SiteFreeForCompletion(p)) { CancelProject(p.Id, "The site was taken before building finished."); return; }
            var b = AddBld(p.Type, p.X, p.Y, p.Sz);
            b.Zoned = true; b.Tier = p.Tier;
            if (p.Type == "wood" || p.Type == "quarry" || p.Type == "mine") b.Organic = true;
            if (p.Type == "wood") b.Mode = DistrictById(p.DistrictId)?.Limits.HarvestMode ?? ForestMode.Sustainable;   // the district's harvest setting
            p.Held.Clear(); p.Status = ProjectStatus.Done; p.Closed = Day; p.Missing = "";
            Recalc();
        }

        bool SiteFreeForCompletion(DevProject p)
        {
            for (int j = 0; j < p.Sz; j++)
                for (int i = 0; i < p.Sz; i++)
                {
                    int tx = p.X + i, ty = p.Y + j;
                    if (!Inb(tx, ty) || Grid[ty, tx] != null || IsSolid(tx, ty) || HasTree(tx, ty)) return false;
                }
            return true;
        }

        /// <summary>One day of the queue: fund waiting projects (player rank, then district priority), advance building ones, forget old finished ones.</summary>
        public void StepProjects()
        {
            _buildersUsed = 0;
            foreach (var p in DevQueue())
            {
                if (!p.Open) continue;
                if (p.Status == ProjectStatus.Waiting && !p.Paused) TryFund(p);
                StepOne(p);
            }
            Projects.RemoveAll(p => !p.Open && Day - p.Closed > ProjectKeepDays);
            foreach (int k in RejectedUntil.Where(r => r.Value <= Day).Select(r => r.Key).ToList()) RejectedUntil.Remove(k);
        }

        // ---------- Player and governor controls ----------
        public Result ApproveProject(int id)
        {
            var p = ProjectById(id);
            if (p == null || p.Status != ProjectStatus.Proposed) return Result.No("That project is not waiting for approval");
            p.Status = ProjectStatus.Waiting; p.Missing = "";
            if (TryFund(p) && p.BuildDays == 0) StepOne(p);
            return Result.Yes();
        }

        public Result RejectProject(int id)
        {
            var p = ProjectById(id);
            if (p == null || p.Status != ProjectStatus.Proposed) return Result.No("That project is not waiting for approval");
            p.Status = ProjectStatus.Rejected; p.Closed = Day; RejectedUntil[Tk(p.X, p.Y)] = Day + RejectDays;
            return Result.Yes();
        }

        /// <summary>Cancel an open project. Nothing held yet: nothing to return. Building: the materials not yet used (in proportion to the work left, whole units, rounded
        /// down) go back to the store; the rest is gone, so a cancel can never create resources.</summary>
        public Result CancelProject(int id, string why = null)
        {
            var p = ProjectById(id);
            if (p == null || !p.Open) return Result.No("That project is not open");
            if (p.Held.Count > 0)
            {
                double left = p.BuildDays <= 0 ? 1 : 1 - p.Progress / (double)p.BuildDays;
                foreach (var h in p.Held) ResAmt[h.Key] += Math.Floor(h.Value * left + 1e-9);
                p.Held.Clear();
            }
            p.Status = ProjectStatus.Cancelled; p.Closed = Day; p.Missing = why ?? "";
            return Result.Yes();
        }

        public Result PauseProject(int id, bool paused)
        {
            var p = ProjectById(id);
            if (p == null || !p.Open) return Result.No("That project is not open");
            p.Paused = paused; return Result.Yes();
        }

        public Result RankProject(int id, int delta)
        {
            var p = ProjectById(id);
            if (p == null || !p.Open) return Result.No("That project is not open");
            p.Rank = Math.Clamp(p.Rank + delta, -5, 5); return Result.Yes();
        }

        /// <summary>One line the queue shows for a project.</summary>
        public string ProjectLine(DevProject p)
        {
            string name = Names.TryGetValue(p.Type, out var n) ? n : p.Type;
            string where = DistrictById(p.DistrictId)?.Name ?? "a district";
            string state = p.Paused ? "paused" : p.Status == ProjectStatus.Building && p.BuildDays > 0 ? $"building {p.Progress}/{p.BuildDays} days" : p.Status.ToString().ToLowerInvariant();
            string cost = string.Join(", ", p.Cost.Where(c => c.Value > 0).Select(c => $"{c.Value} {c.Key.ToString().ToLowerInvariant()}"));
            return $"{name} in {where} ({cost}): {state}. {p.Reason}{(p.Missing.Length > 0 ? " " + p.Missing : "")}".Trim();
        }

        // ---------- Save ----------
        static JObject ResToJson(Dictionary<Res, int> m)
        {
            var o = new JObject();
            foreach (var kv in m.OrderBy(k => (int)k.Key)) o[kv.Key.ToString().ToLowerInvariant()] = kv.Value;
            return o;
        }

        static Dictionary<Res, int> ResFromJson(JToken t)
        {
            var m = new Dictionary<Res, int>();
            if (t is JObject ro)
                foreach (var kv in ro.Properties())
                    if (Enum.TryParse(kv.Name, true, out Res k) && kv.Value.Type == JTokenType.Integer) m[k] = Math.Max(0, kv.Value.Value<int>());
            return m;
        }

        JObject SaveProjects() => new JObject
        {
            ["next"] = _nextProjectId,
            ["reserve"] = new JObject { ["wood"] = Reserve.KeepWood, ["stone"] = Reserve.KeepStone, ["gold"] = Reserve.KeepGold, ["max"] = Reserve.MaxOpenProjects },
            ["rejected"] = new JArray(RejectedUntil.OrderBy(r => r.Key).Select(r => new JArray(r.Key, r.Value))),
            ["list"] = new JArray(Projects.Select(p => new JObject
            {
                ["id"] = p.Id, ["district"] = p.DistrictId, ["type"] = p.Type, ["x"] = p.X, ["y"] = p.Y, ["sz"] = p.Sz, ["tier"] = p.Tier, ["status"] = p.Status.ToString().ToLowerInvariant(),
                ["paused"] = p.Paused, ["rank"] = p.Rank, ["days"] = p.BuildDays, ["progress"] = p.Progress, ["created"] = p.Created, ["closed"] = p.Closed,
                ["cost"] = ResToJson(p.Cost), ["held"] = ResToJson(p.Held), ["reason"] = p.Reason, ["benefit"] = p.Benefit, ["missing"] = p.Missing,
            })),
        };

        void LoadProjects(JObject o, int size, int offset)
        {
            Projects.Clear(); RejectedUntil.Clear(); Reserve = new DevReserve(); _nextProjectId = 1;
            if (o == null) return;   // saves from before projects: an empty queue
            if (o["reserve"] is JObject r)
                Reserve = new DevReserve { KeepWood = Math.Max(0, r.Value<int?>("wood") ?? 0), KeepStone = Math.Max(0, r.Value<int?>("stone") ?? 0), KeepGold = Math.Max(0, r.Value<int?>("gold") ?? 0), MaxOpenProjects = Math.Clamp(r.Value<int?>("max") ?? 3, 1, 20) };
            var ids = new HashSet<int>(); int maxId = 0;
            foreach (var j in (o["list"] as JArray) ?? new JArray())
            {
                int id = j.Value<int?>("id") ?? 0; string type = j.Value<string>("type");
                int x = j.Value<int?>("x") ?? -1, y = j.Value<int?>("y") ?? -1, sz = Math.Clamp(j.Value<int?>("sz") ?? 1, 1, 3);
                if (id <= 0 || !ids.Add(id) || type == null || !Names.ContainsKey(type) || x < 0 || y < 0 || x >= size || y >= size) continue;
                if (!Enum.TryParse(j.Value<string>("status"), true, out ProjectStatus st)) continue;
                var p = new DevProject
                {
                    Id = id, DistrictId = j.Value<int?>("district") ?? 0, Type = type, X = x + offset, Y = y + offset, Sz = sz, Tier = j.Value<int?>("tier") ?? 1, Status = st,
                    Paused = j.Value<bool?>("paused") ?? false, Rank = Math.Clamp(j.Value<int?>("rank") ?? 0, -5, 5), BuildDays = Math.Max(0, j.Value<int?>("days") ?? 0),
                    Created = j.Value<int?>("created") ?? 0, Closed = j.Value<int?>("closed") ?? 0, Cost = ResFromJson(j["cost"]), Held = ResFromJson(j["held"]),
                    Reason = j.Value<string>("reason") ?? "", Benefit = j.Value<string>("benefit") ?? "", Missing = j.Value<string>("missing") ?? "",
                };
                p.Progress = Math.Clamp(j.Value<int?>("progress") ?? 0, 0, p.BuildDays);
                if (!p.Open) p.Held.Clear();
                Projects.Add(p); maxId = Math.Max(maxId, id);
            }
            foreach (var rj in (o["rejected"] as JArray) ?? new JArray())
                if (rj is JArray pair && pair.Count == 2) { int k = (int)pair[0]; if (k >= 0 && k < size * size) RejectedUntil[Tk(k % size + offset, k / size + offset)] = (int)pair[1]; }
            _nextProjectId = Math.Max(o.Value<int?>("next") ?? 1, maxId + 1);
        }
    }
}
