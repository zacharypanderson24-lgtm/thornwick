using System;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>All stored food villagers can eat, in food units. Eggs use the same conversion as Tick.</summary>
        public double EdibleFoodStored => Math.Max(0, ResAmt[Res.Food]) + Math.Max(0, ResAmt[Res.SmokedMeat]) +
            Math.Max(0, ResAmt[Res.Eggs]) / EggsPerFood;

        /// <summary>Combined capacity in the same units as EdibleFoodStored.</summary>
        public double EdibleFoodCapacity => CropStorage(Res.Food) + CropStorage(Res.SmokedMeat) +
            CropStorage(Res.Eggs) / (double)EggsPerFood;
    }
}
