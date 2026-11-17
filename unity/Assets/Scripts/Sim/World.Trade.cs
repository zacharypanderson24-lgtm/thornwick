using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SiloFood = 180, SiloFlax = 60;
        const int TownFood = 100, TownFlax = 30;

        public int CropStorage(Res resource)
        {
            if (resource != Res.Food && resource != Res.Flax) return Storage;
            int silos = 0;
            foreach (var b in Blds)
                if (b.T == "silo" && b.Served) silos++;
            return resource == Res.Food ? TownFood + silos * SiloFood : TownFlax + silos * SiloFlax;
        }

    }
}
