using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SiloFood = 5000, SiloFlax = 60;   // a silo holds a year of food for 14 villagers (360 each)
        const int TownFood = 100, TownFlax = 30, SeasonalTownFood = 1300;   // the Town Hall's store: 6 settlers for 216 days

        public int CropStorage(Res resource)
        {
            if (resource == Res.Eggs) return EggStorage;
            if (resource != Res.Food && resource != Res.Flax) return Storage + StorehouseCapacity(resource);
            int silos = 0;
            foreach (var b in Blds)
                if (b.T == "silo" && b.Served) silos++;
            if (resource == Res.Flax) return TownFlax + silos * SiloFlax;
            return SeasonalFarming ? SeasonalTownFood + (int)(silos * SiloFood * CharterMult("silo")) : TownFood + (int)(silos * 180 * CharterMult("silo"));
        }

    }
}
