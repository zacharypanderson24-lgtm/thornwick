// The "Getting started" guide: the next useful thing to do in the first year, worked out from the real state of the town (what is built, what is stored),
// never from a script. Each step says what to do, why it matters and which part of the Build catalogue to open. Pure C# (no UnityEngine), tested directly.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public sealed class GuideStep
    {
        public string Id, Title, Why, Progress;
        public CatalogueCategory Show;     // the catalogue category that holds what to build
        public bool Done;
    }

    public static class FirstYearGuide
    {
        static readonly string[] FoodBuildings = { "farm", "hunter", "fisher", "dock" };

        static bool Has(World w, params string[] types) => w.Blds.Any(b => types.Contains(b.T));

        /// <summary>Every step with its current state, in the order a new town needs them.</summary>
        public static List<GuideStep> Steps(World w)
        {
            double demand = w.DailyFoodDemand, stored = w.EdibleFoodStored;
            double winterNeed = demand * World.SeasonLength;   // winter is one season of 90 days
            var list = new List<GuideStep>
            {
                new GuideStep { Id = "food", Title = "Set up a food supply", Show = CatalogueCategory.Food, Done = Has(w, FoodBuildings),
                    Why = "People eat every day. With no farm, hunting lodge or fishing hut the stores only run down." },
                new GuideStep { Id = "timber", Title = "Start timber production", Show = CatalogueCategory.Resources, Done = Has(w, "wood"),
                    Why = "Wood pays for almost everything you build. Place a woodcutter lodge beside a forest and a road: it fells the trees around it by itself and replants." },
                new GuideStep { Id = "stone", Title = "Open a quarry", Show = CatalogueCategory.Resources, Done = Has(w, "quarry"),
                    Why = "Stone is needed for stone houses, silos and wells. Place a quarry beside rock (hills, outcrops, a mountain edge) and a road: it digs the stone around it by itself." },
                new GuideStep { Id = "housing", Title = "Build more homes", Show = CatalogueCategory.Housing, Done = w.Peasants < w.D.Civ * 0.85,
                    Why = "The town is nearly full: new people only arrive while there is room. Zone more housing beside a road.",
                    Progress = $"{w.Peasants} people, room for {w.D.Civ}" },
                new GuideStep { Id = "winter", Title = "Stock food for winter", Show = CatalogueCategory.Food, Done = w.Day < 120 || stored >= winterNeed,
                    Why = "Fields give little in winter, which lasts 90 days. Keep the stores growing: farms, hunting, a smokehouse and silos.",
                    Progress = $"{stored:0} of about {winterNeed:0} food" },
            };
            return list;
        }

        /// <summary>The first step still to do, or null when the town is on track.</summary>
        public static GuideStep Next(World w) => Steps(w).FirstOrDefault(s => !s.Done);
    }
}
