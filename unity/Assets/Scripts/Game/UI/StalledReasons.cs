// "Why is this stalled?": for a work building, the real reasons it is idle or short of workers, worked out from the state of the town, each with
// several ways to fix it across the different ways of playing (build, buy, trade, free workers). Pure C# (no UnityEngine), tested directly.
using System.Collections.Generic;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public sealed class StalledReason
    {
        public string Why;
        public readonly List<string> Fixes = new List<string>();
    }

    public static class StalledReasons
    {
        public static List<StalledReason> For(World w, Building b)
        {
            var list = new List<StalledReason>();
            if (w == null || b == null || b.Jobs <= 0) return list;

            if (!b.Served)
            {
                var r = new StalledReason { Why = "It has no road to the Town Hall, so nobody can work or deliver here." };
                r.Fixes.Add("Lay a road from its door to a road that reaches the Town Hall (Roads, then drag).");
                r.Fixes.Add("Or let the Steward suggest a better spot next to a road.");
                list.Add(r);
                return list;
            }

            if (b.T == "farm" && w.SeasonalFarming && w.FieldsOf(b) == 0)
            {
                var r = new StalledReason { Why = $"No Farmland within {World.FarmReach} tiles, so there is nothing for its farmers to work." };
                r.Fixes.Add("Zone Farmland on fertile ground near it (Farming, key 3).");
                r.Fixes.Add("Or buy grain from a trading partner (Trade menu) and let this farm wait.");
                r.Fixes.Add("Or feed the town another way: a hunting lodge in the forest, or fishing on a shore.");
                list.Add(r);
            }

            int need = w.ActiveJobs(b);
            if (need > 0 && b.Workers < need)
            {
                bool nobody = b.Workers == 0;
                var r = new StalledReason { Why = nobody ? "Nobody works here yet." : $"Only {b.Workers} of {need} workers are here." };
                if (w.D.Idle <= 0)
                {
                    r.Why += " Every villager already has a job.";
                    r.Fixes.Add("Zone more housing beside a road: more people arrive while there is room.");
                    r.Fixes.Add("Move this trade up in the Workers list (Workers button): villagers fill trades top to bottom.");
                    r.Fixes.Add("Or buy what it would make from a trading partner instead of staffing it (Trade menu).");
                    if (w.Soldiers > 0) r.Fixes.Add("Or recruit fewer soldiers: every soldier is a worker the town lacks (Military menu).");
                }
                else
                {
                    r.Why += " Idle villagers exist, but other trades rank above this one.";
                    r.Fixes.Add("Move this trade up in the Workers list (Workers button).");
                }
                list.Add(r);
            }
            return list;
        }

        /// <summary>The reasons as inspector lines: a "Stalled:" line (marked as a problem) and one "Fix:" line per way out.</summary>
        public static List<string> Lines(World w, Building b)
        {
            var lines = new List<string>();
            foreach (var r in For(w, b))
            {
                lines.Add("Stalled: " + r.Why);
                foreach (var f in r.Fixes) lines.Add("Fix: " + f);
            }
            return lines;
        }
    }
}
