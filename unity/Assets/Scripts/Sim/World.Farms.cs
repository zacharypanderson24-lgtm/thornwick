using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int FarmFieldRange = 3;
        const int FieldsPerWorker = 2;
        const double FoodPerField = 1.5;

        bool IsFarmField(int x, int y) => Inb(x, y) && Grid[y, x] == null &&
            ZoneMap[y, x] == Zone.Farm && Fert[y, x] && DecoMap[y, x] == Deco.None &&
            !IsSolid(x, y);

        // A field belongs to the nearest road-served farm. Equal distances favor the earlier building.
        public Dictionary<int, Building> FarmFields()
        {
            var fields = new Dictionary<int, Building>();
            foreach (var farm in Blds)
            {
                if (farm.T != "farm" || !farm.Served) continue;
                for (int y = farm.Y - FarmFieldRange; y < farm.Y + farm.Sz + FarmFieldRange; y++)
                    for (int x = farm.X - FarmFieldRange; x < farm.X + farm.Sz + FarmFieldRange; x++)
                    {
                        if (!IsFarmField(x, y)) continue;
                        int key = Tk(x, y);
                        if (!fields.TryGetValue(key, out var previous) ||
                            ChebPt(farm, x, y) < ChebPt(previous, x, y))
                            fields[key] = farm;
                    }
            }
            return fields;
        }

        public double FarmFood()
        {
            var counts = new Dictionary<Building, int>();
            foreach (var farm in FarmFields().Values)
                counts[farm] = counts.TryGetValue(farm, out int count) ? count + 1 : 1;

            double food = 0;
            foreach (var pair in counts)
                food += Math.Min(pair.Value, pair.Key.Workers * FieldsPerWorker) * FoodPerField;
            return food;
        }

        bool HasRoomForFarmField(int left, int top, int size, Dictionary<int, Building> fields)
        {
            for (int y = top - FarmFieldRange; y < top + size + FarmFieldRange; y++)
                for (int x = left - FarmFieldRange; x < left + size + FarmFieldRange; x++)
                {
                    if (x >= left && x < left + size && y >= top && y < top + size) continue;
                    if (IsFarmField(x, y) && !fields.ContainsKey(Tk(x, y))) return true;
                }
            return false;
        }

        void AddStarterFarmFields()
        {
            // Keep the starting farm productive even on seeds with sparse fertile ground.
            var farm = Grid[Start + 5, Start - 1];
            if (farm?.T != "farm") return;
            int planted = FarmFields().Values.Count(owner => owner == farm);
            for (int radius = 1; radius <= FarmFieldRange && planted < 2; radius++)
                for (int y = farm.Y - radius; y <= farm.Y + radius && planted < 2; y++)
                    for (int x = farm.X - radius; x <= farm.X + radius && planted < 2; x++)
                    {
                        if (!Inb(x, y) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None ||
                            DecoMap[y, x] != Deco.None || !TileFlat(x, y) || IsSolid(x, y)) continue;
                        Fert[y, x] = true;
                        ZoneMap[y, x] = Zone.Farm;
                        planted++;
                    }
        }
    }
}
