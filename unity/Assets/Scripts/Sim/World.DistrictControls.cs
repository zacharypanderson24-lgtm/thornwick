// District controls (DESIGN_ORGANIC_DEVELOPMENT.md, phase 6): the numbers behind the inspector and the advanced restrictions that need them.
// Priority, mode, limits and names were built in phases 1 and 2; this adds the yearly spending limit, the production target, what a district makes, and
// the plain-words "problems" the inspector lists. Everything is data (no UI), so a governor can read and set the same things.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SpendWindowDays = 360;

        /// <summary>Construction funded in this district over the last in-game year, in material units.</summary>
        public int SpentThisYear(District d)
        {
            d.SpendLog.RemoveAll(s => Day - s.day > SpendWindowDays);
            return d.SpendLog.Sum(s => s.amount);
        }

        /// <summary>What a district's production target is measured in.</summary>
        public static string DistrictOutputUnit(DistrictType t) => t switch
        {
            DistrictType.Residential => "homes", DistrictType.Agricultural => "food a day", DistrictType.Forestry => "logs a day", DistrictType.Mining => "ore a day",
            DistrictType.Quarrying => "stone a day", DistrictType.Hunting => "food a day", _ => "food a day",
        };

        /// <summary>About what the district's standing, staffed buildings make each day, in the unit above (an estimate from the same formulas the placement previews use).
        /// Residential counts homes instead of a daily output.</summary>
        public double DistrictOutputPerDay(District d)
        {
            double sum = 0;
            foreach (var b in d.Buildings)
            {
                int w = Math.Min(b.Workers, b.Jobs);
                switch (b.T)
                {
                    case "house": sum += 1; break;
                    case "farm": sum += FarmFields().Count(f => f.Value == b) * Math.Min(1.0, w * FieldsPerFarmer / (double)Math.Max(1, FarmFields().Count(f => f.Value == b))) * WheatYield / YearLength; break;
                    case "wood": sum += LodgeDailyEstimate(w); break;
                    case "quarry": sum += QuarryDailyEstimate(w); break;
                    case "mine": sum += MineDailyEstimate(w); break;
                    case "hunter": sum += HuntRate(b, w); break;
                    case "fisher": sum += FishCatch(b); break;
                }
            }
            return sum;
        }

        /// <summary>The district's open problems in plain words (the inspector lists them under the explanation).</summary>
        public List<string> DistrictProblems(District d)
        {
            var list = new List<string>();
            int vacant = d.Buildings.Where(b => b.T != "house").Sum(b => Math.Max(0, b.Jobs - b.Workers));
            if (vacant > 0) list.Add($"{vacant} job{(vacant == 1 ? "" : "s")} unfilled: more workers are needed.");
            int cut = d.Buildings.Count(b => !b.Served);
            if (cut > 0) list.Add($"{cut} building{(cut == 1 ? " is" : "s are")} not connected to the Town Hall by road.");
            if (d.Mode == DevMode.Paused) list.Add("Development is paused.");
            if (d.Priority == DevPriority.Disabled) list.Add("Development is disabled.");
            int waiting = Projects.Count(p => p.DistrictId == d.Id && p.Open && p.Missing.Length > 0 && p.Status != ProjectStatus.Proposed);
            if (waiting > 0) list.Add($"{waiting} project{(waiting == 1 ? " is" : "s are")} waiting for materials or builders.");
            if (d.Type == DistrictType.Forestry)
                foreach (var b in d.Buildings.Where(b => b.T == "wood" && b.Mode == ForestMode.Intensive)) { list.Add("Intensive harvest: the forest is being used up faster than it regrows."); break; }
            if (d.Type == DistrictType.Mining || d.Type == DistrictType.Quarrying)
                foreach (var b in d.Buildings.Where(b => b.T == "mine" || b.T == "quarry")) if (b.ReserveStage > 0) { list.Add("A mine or quarry here is running low on ore or stone."); break; }
            return list;
        }

        public IEnumerable<DevProject> ProjectsOf(District d) => Projects.Where(p => p.DistrictId == d.Id && p.Open);
    }
}
