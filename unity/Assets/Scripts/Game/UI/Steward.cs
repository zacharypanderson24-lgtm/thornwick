// The Town Steward: reads the real state of the town (food, water, timber, stone, housing, work) and proposes concrete things to do, ranked by how
// urgent they are. A proposal with a building also carries the best place to put it (found by looking at the real map and the placement forecast),
// what it costs and what it should achieve, so the player can preview it on the map and build it with one click. Nothing here changes the world.
// Pure C# (no UnityEngine), tested directly.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public sealed class Proposal
    {
        public string Id, Title, Why, Effect, CostText, Blocked;
        public int Severity;                   // 3 urgent, 2 important, 1 worth doing, 0 information
        public string Type;                    // the building to place, or null for advice only
        public int X, Y, Size;                 // where
        public bool Affordable;
        public CatalogueCategory? Show;        // advice only: the catalogue category to open
        public bool HasSite => Type != null;
    }

    public static class Steward
    {
        public const int SearchRadius = 28, MaxChecks = 1800, ForecastCandidates = 12;

        public static string CostText(string type)
        {
            var parts = Defs.CostOf(type, Defs.PlacementSize(type)).Select(kv => $"{kv.Value} {kv.Key.ToString().ToLowerInvariant()}").ToList();
            return parts.Count == 0 ? "Free" : string.Join(", ", parts);
        }

        /// <summary>Valid sites for a building beside a road that reaches the Town Hall, nearest to the Hall first (at most `max`).</summary>
        public static List<(int x, int y)> Sites(World w, string type, int max)
        {
            var found = new List<(int x, int y)>();
            if (w.Hall == null) return found;
            int cx = w.Hall.X + 1, cy = w.Hall.Y + 1, checks = 0;
            var order = new List<(int d, int x, int y)>();
            for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
                for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
                    order.Add((dx * dx + dy * dy, cx + dx, cy + dy));
            order.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (_, x, y) in order)
            {
                if (!World.Inb(x, y) || !w.Claimed(x, y)) continue;
                if (++checks > MaxChecks) break;
                if (!w.WouldConnect(type, x, y)) continue;
                var r = w.CanPlace(type, x, y);
                if (!r.Ok && !r.Reason.StartsWith("Not enough")) continue;   // too poor is fine: the proposal says what is missing
                found.Add((x, y));
                if (found.Count >= max) break;
            }
            return found;
        }

        static Proposal Site(World w, string id, string type, int severity, string title, string why, Func<Forecast, double> score, Func<Forecast, string> effect)
        {
            var p = new Proposal { Id = id, Severity = severity, Title = title, Why = why, Type = type, Size = Defs.PlacementSize(type), CostText = CostText(type) };
            var sites = Sites(w, type, ForecastCandidates);
            if (sites.Count == 0)
            {
                p.Type = null; p.Blocked = $"No good spot beside a connected road yet. Extend a road towards open land first.";
                p.Title = title; p.Effect = ""; return p;
            }
            (int x, int y) best = sites[0]; double bestScore = double.NegativeInfinity; Forecast bestForecast = null;
            foreach (var s in sites)
            {
                var f = w.ForecastPlacement(type, s.x, s.y);
                double sc = score(f);
                if (sc > bestScore) { bestScore = sc; best = s; bestForecast = f; }
            }
            if (bestScore <= 0)
            {
                p.Type = null; p.Blocked = "Nothing worth working near the road yet. " + (bestForecast != null && bestForecast.Lines.Count > 0 ? bestForecast.Lines[0] : "");
                return p;
            }
            p.X = best.x; p.Y = best.y;
            p.Effect = effect(bestForecast);
            var can = w.CanPlace(type, best.x, best.y);
            p.Affordable = can.Ok; if (!can.Ok) p.Blocked = can.Reason;
            return p;
        }

        /// <summary>What the town needs most right now, most urgent first (at most `max`).</summary>
        public static List<Proposal> Advise(World w, int max = 3)
        {
            var list = new List<Proposal>();
            if (w == null || w.Defeated) return list;

            // ---- water ----
            var water = w.LastWater;
            if (water != null && (water.ShortHomes > 0 || water.Shortfall > 0.05))
            {
                var p = Site(w, "water", "well", water.ShortHomes > 0 ? 3 : 2, "Dig a well",
                    water.ShortHomes > 0 ? $"{water.ShortHomes} home{(water.ShortHomes == 1 ? " is" : "s are")} short of water. Homes stop growing and people leave if it lasts." : "The wells and the Hall spring no longer cover what the town drinks.",
                    f => 1, f => "Adds a source on a groundwater patch beside a road. The Water lens shows the patches.");
                list.Add(p);
            }

            // ---- food ----
            double demand = w.DailyFoodDemand, stored = w.EdibleFoodStored;
            bool anyFood = w.Blds.Any(b => b.T == "farm" || b.T == "hunter" || b.T == "fisher" || b.T == "dock");
            double days = demand > 0 ? stored / demand : 99;
            if (!anyFood || days < 25)
            {
                int sev = !anyFood || days < 10 ? 3 : 2;
                bool fields = false;
                for (int j = 0; j < Defs.N && !fields; j++) for (int i = 0; i < Defs.N; i++) if (w.ZoneMap[j, i] == Zone.Farm) { fields = true; break; }
                if (!fields && !w.Blds.Any(b => b.T == "hunter" || b.T == "fisher" || b.T == "dock"))
                    // a farmhouse with no fields grows nothing: the first step is the fields
                    list.Add(new Proposal { Id = "fields", Severity = sev, Title = "Zone farmland", Show = CatalogueCategory.Food,
                        Why = !anyFood ? "Nobody grows food yet: the stores only run down." : $"Food would last about {Math.Floor(days)} more days.",
                        Effect = "Paint Farmland (key 3) on fertile ground (Fertile land lens) beside a road, then I will find the spot for the farmhouse." });
                else list.Add(Site(w, "food", "farm", sev, "Build a farm",
                    !anyFood ? "Nobody grows food yet: the stores only run down." : $"Food would last about {Math.Floor(days)} more days. Winter makes it worse.",
                    f => f.FoodPerDay,
                    f => f.FoodPerDay > 0 ? $"About {f.FoodPerDay:0.#} food a day with a full crew, feeding roughly {Math.Round(f.FoodPerDay)} villagers." : "Grows food once its fields are zoned."));
            }

            // ---- timber ----
            if (!w.Blds.Any(b => b.T == "wood"))
                list.Add(Site(w, "timber", "wood", 2, "Open a woodcutter lodge", "Wood pays for almost everything you build and none is coming in.",
                    f => f.Warn ? 0 : 1, f => f.Lines.Count > 0 ? f.Lines[0] : "Fells the trees around it and brings in logs."));

            // ---- housing and work (advice only) ----
            if (w.Peasants >= Math.Max(8, w.D.Civ * 0.85))
                list.Add(new Proposal { Id = "housing", Severity = 1, Title = "Zone more homes", Show = CatalogueCategory.Housing,
                    Why = $"{w.Peasants} people, room for {w.D.Civ}. New people only arrive while there is room.", Effect = "Housing zone beside a road: homes grow on it by themselves." });
            var work = w.WorkHealth();
            if (work.Level != HealthLevel.Good)
                list.Add(new Proposal { Id = "work", Severity = work.Level == HealthLevel.Bad ? 2 : 1, Title = work.Warning, Show = CatalogueCategory.Resources, Why = work.Detail, Effect = "Open the Workers list to steer who does what." });

            // ---- stone ----
            if (w.Peasants >= 12 && !w.Blds.Any(b => b.T == "quarry"))
                list.Add(Site(w, "stone", "quarry", 1, "Open a quarry", "Stone is needed for stone houses, silos and wells.",
                    f => f.Warn ? 0 : 1, f => f.Lines.Count > 0 ? f.Lines[0] : "Digs the rock around it."));

            return list.OrderByDescending(p => p.Severity).ThenByDescending(p => p.HasSite).Take(max).ToList();
        }
    }
}
