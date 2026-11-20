using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SiloFood = 360, SiloFlax = 60;
        const int TownFood = 100, TownFlax = 30, SeasonalTownFood = 260;

        public int CropStorage(Res resource)
        {
            if (resource != Res.Food && resource != Res.Flax) return Storage;
            int silos = 0;
            foreach (var b in Blds)
                if (b.T == "silo" && b.Served) silos++;
            if (resource == Res.Flax) return TownFlax + silos * SiloFlax;
            return SeasonalFarming ? SeasonalTownFood + silos * SiloFood : TownFood + silos * 180;
        }

    }
}
