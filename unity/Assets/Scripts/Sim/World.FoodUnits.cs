// Saves made before food was rescaled (one villager eats 1 food a day, World.Farms.cs) hold every food amount in the old units, 5 times
// smaller. Loading one as it stands would leave the town with a fifth of its food, so the food stocks are multiplied by 5 once, on load.
// New saves carry "foodUnits": 5 and are left alone. The Classic style (browser parity) never changed units.
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>How many of today's food units one unit in a save without "foodUnits" was worth.</summary>
        public const int OldFoodUnits = 5;

        void MigrateFoodUnits(JObject save)
        {
            if (!SeasonalFarming || save["foodUnits"] != null) return;
            const double k = OldFoodUnits;
            ResAmt[Res.Food] *= k;
            ResAmt[Res.SmokedMeat] *= k;
            _wheat *= k; _freshMeat *= k;
            if (RecentFoodProduction is double d) RecentFoodProduction = d * k;
            foreach (var c in Caravans.Where(c => c.Res == Res.Food && c.Kind != CaravanKind.Gift)) c.Amount *= OldFoodUnits;   // grain on the road
        }
    }
}
