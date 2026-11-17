using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SiloFood = 180, SiloFlax = 60;
        const int TownFood = 100, TownFlax = 30;
        public const double FlaxPrice = 2, FoodPrice = 0.5;

        public int CropStorage(Res resource)
        {
            if (resource != Res.Food && resource != Res.Flax) return Storage;
            int silos = 0;
            foreach (var b in Blds)
                if (b.T == "silo" && b.Served) silos++;
            return resource == Res.Food ? TownFood + silos * SiloFood : TownFlax + silos * SiloFlax;
        }

        public double FoodReserve => Math.Max(40, (Peasants + Soldiers * 1.5) * 2);
        double SurplusFood => Math.Max(0, ResAmt[Res.Food] - FoodReserve);

        double SellCrops()
        {
            double gold = 0;
            foreach (var market in Blds)
            {
                if (market.T != "market") continue;
                market.FlaxSold = market.FoodSold = 0;
                if (!market.Served || market.Workers == 0) continue;

                market.FlaxSold = Math.Min(market.Workers, ResAmt[Res.Flax]);
                ResAmt[Res.Flax] -= market.FlaxSold;
                market.FoodSold = Math.Min(market.Workers - market.FlaxSold, SurplusFood);
                ResAmt[Res.Food] -= market.FoodSold;
                gold += market.FlaxSold * FlaxPrice + market.FoodSold * FoodPrice;
            }
            return gold;
        }
    }
}
