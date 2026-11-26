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

    /// <summary>The path the player is aiming for. It never hides a survival problem; it changes the ambitious advice and the fixes offered.</summary>
    public enum Focus { Balanced, Farmer, Trader, Warlord, Industrialist, Seafarer, Fortress }

    public static class Steward
    {
        static readonly Dictionary<Focus, (string type, string title, string why, string effect)[]> FocusGoals = new Dictionary<Focus, (string, string, string, string)[]>
        {
            { Focus.Farmer, new[] { ("silo", "Build a silo", "A farming town lives on its harvest: a silo keeps grain safe for the winter.", "Stores much more grain beside a road.") } },
            { Focus.Trader, new[] { ("market", "Build a market", "Markets send caravans to neighbouring settlements: buy what you lack, sell what you make.", "Opens trade caravans. Set them up in the Trade menu.") } },
            { Focus.Warlord, new[] { ("barracks", "Build barracks", "Soldiers are trained at a barracks. They also eat, so keep food ahead of them.", "Trains soldiers for raids and defence.") } },
            { Focus.Industrialist, new[] { ("sawmill", "Build a sawmill", "Sawmills turn logs into wood, the base of every workshop chain.", "Turns logs into wood."), ("smithery", "Build a smithery", "Smitheries turn ore into metal for tools and weapons.", "Turns ore into metal.") } },
            { Focus.Seafarer, new[] { ("fisher", "Build a fishing hut", "Fish feed a coastal town all year, even in winter, with no fields.", "Brings in fish from the shore."), ("dock", "Build a fishing dock", "Docks reach deep water for bigger catches and are the first step to ships.", "Bigger catches from deep water.") } },
            { Focus.Fortress, new[] { ("watchtower", "Build a watchtower", "Towers spot raiders early. A fortress town sees danger first.", "Warns of raiders and covers the road in."), ("muster", "Build a muster ground", "A muster ground trains militia from your own people.", "Trains militia.") } },
        };

        /// <summary>The other ways, besides building, to fix a food shortage, in the order the chosen path would reach for them.</summary>
        public static string FoodAlternatives(Focus f, World w)
        {
            string buy = w.Markets > 0 ? "buy grain from a neighbour (Trade menu)" : "buy grain once you have a market (Trade)";
            switch (f)
            {
                case Focus.Trader: return $"Other ways: {buy}; or hunt in the forest.";
                case Focus.Seafarer: return "Other ways: fishing huts on the shore; " + buy + ".";
                case Focus.Warlord: return "Other ways: hunt; " + buy + "; or take supplies from a raider camp.";
                default: return "Other ways: a hunting lodge in the forest, fishing on a shore, or " + buy + ".";
            }
        }

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
        public static List<Proposal> Advise(World w, int max = 3, Focus focus = Focus.Balanced)
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
            bool tradeFeeds = (focus == Focus.Trader || focus == Focus.Seafarer) && w.Markets > 0 && w.ResAmt[Res.Gold] >= World.BasePrice(Res.Food) * demand * 20 * 1.3;
            if ((!anyFood || days < 25) && tradeFeeds && days >= 10)
                list.Add(new Proposal { Id = "foodtrade", Severity = 1, Title = "Food is covered by trade", Why = $"Stores last about {Math.Floor(days)} days and you have the gold to buy about 20 days of grain.", Effect = "Buy grain in the Trade menu when it runs low. No farms needed on this path." });
            else if (!anyFood || days < 25)
            {
                int sev = !anyFood || days < 10 ? 3 : 2;
                string alt = FoodAlternatives(focus, w);
                bool fields = false;
                for (int j = 0; j < Defs.N && !fields; j++) for (int i = 0; i < Defs.N; i++) if (w.ZoneMap[j, i] == Zone.Farm) { fields = true; break; }
                string why = (!anyFood ? "Nobody grows food yet: the stores only run down." : $"Food would last about {Math.Floor(days)} more days.") + " " + alt;
                if (focus == Focus.Seafarer && !w.Blds.Any(b => b.T == "fisher" || b.T == "dock"))
                    list.Add(Site(w, "food", "fisher", sev, "Build a fishing hut", why, f => f.Warn ? 0 : 1, f => f.Lines.Count > 0 ? f.Lines[0] : "Fish for the town from the shore."));
                else if (!fields && !w.Blds.Any(b => b.T == "hunter" || b.T == "fisher" || b.T == "dock"))
                    // a farmhouse with no fields grows nothing: the first step is the fields
                    list.Add(new Proposal { Id = "fields", Severity = sev, Title = "Zone farmland", Show = CatalogueCategory.Food, Why = why,
                        Effect = "Paint Farmland (key 3) on fertile ground (Fertile land lens) beside a road, then I will find the spot for the farmhouse." });
                else list.Add(Site(w, "food", "farm", sev, "Build a farm", why,
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
            // stone: only for the paths that build with it, or once the town is big
            if ((w.Peasants >= 25 || focus == Focus.Industrialist || focus == Focus.Fortress || focus == Focus.Balanced) && w.Peasants >= 12 && !w.Blds.Any(b => b.T == "quarry"))
                list.Add(Site(w, "stone", "quarry", 1, "Open a quarry", "Stone is needed for stone houses, silos and wells.",
                    f => f.Warn ? 0 : 1, f => f.Lines.Count > 0 ? f.Lines[0] : "Digs the rock around it."));

            // the chosen path: the next building it needs (never urgent, one at a time)
            if (w.Peasants >= 10 && FocusGoals.TryGetValue(focus, out var goals))
                foreach (var g in goals)
                {
                    if (g.type == "silo" && !w.Blds.Any(b => b.T == "farm")) continue;
                    if (w.Blds.Any(b => b.T == g.type)) continue;
                    list.Add(Site(w, "focus-" + g.type, g.type, 1, g.title, g.why, f => 1, f => g.effect));
                    break;
                }

            return list.OrderByDescending(p => p.Severity).ThenByDescending(p => p.HasSite).Take(max).ToList();
        }
    }
}
