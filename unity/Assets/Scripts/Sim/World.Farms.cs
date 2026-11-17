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
        const double FoodPerField = 0.4;   // two plots a farmer: about 0.8 food a farmer a day, as the old farms gave
        const int StarterFields = 8;
        internal readonly Dictionary<int, bool> FlaxFields = new Dictionary<int, bool>();

        public int? FlaxPercent { get; private set; }
        readonly HashSet<int> _managedFlax = new HashSet<int>();
        int _cropPlanVersion = -1, _plannedFields;

        public void SetFlaxPercent(int percent)
        {
            int next = Math.Clamp(percent, 0, 100);
            if (FlaxPercent == next) return;
            FlaxPercent = next;
            Touch();
        }

        public bool IsFlaxField(int x, int y)
        {
            int key = Tk(x, y);
            if (!FlaxPercent.HasValue) return FlaxFields.ContainsKey(key);
            RefreshCropPlan();
            return _managedFlax.Contains(key);
        }

        public (int flax, int total) PlannedCrops()
        {
            RefreshCropPlan();
            return (_managedFlax.Count, _plannedFields);
        }

        void RefreshCropPlan()
        {
            if (_cropPlanVersion == Version) return;
            _cropPlanVersion = Version;
            _managedFlax.Clear();
            var fields = new List<int>();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    if (IsFarmField(x, y)) fields.Add(Tk(x, y));
            _plannedFields = fields.Count;

            if (FlaxPercent is int percent)
            {
                int flaxCount = (fields.Count * percent + 50) / 100;
                foreach (int key in fields.OrderBy(key => Noise.Hash2(key, 79, Seed)).ThenBy(key => key).Take(flaxCount))
                    _managedFlax.Add(key);
            }
            else
                foreach (int key in fields)
                    if (FlaxFields.ContainsKey(key)) _managedFlax.Add(key);
        }

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

        public double FarmFood() => FarmOutput().food;

        public (double food, double flax) FarmOutput()
        {
            var counts = new Dictionary<Building, (int wheat, int flax)>();
            foreach (var field in FarmFields())
            {
                counts.TryGetValue(field.Value, out var count);
                if (IsFlaxField(field.Key % N, field.Key / N)) count.flax++;
                else count.wheat++;
                counts[field.Value] = count;
            }

            double food = 0, flax = 0;
            foreach (var pair in counts)
            {
                var (wheat, linenCrop) = pair.Value;
                double worked = Math.Min(1.0, pair.Key.Workers * FieldsPerWorker / (double)(wheat + linenCrop));
                food += wheat * worked * FoodPerField;
                flax += linenCrop * worked;
            }
            return (food, flax);
        }

        void AddStarterFarmFields()
        {
            // Keep the starting farm productive even on seeds with sparse fertile ground.
            var farm = Grid[Start + 5, Start - 1];
            if (farm?.T != "farm") return;
            int planted = FarmFields().Values.Count(owner => owner == farm);
            for (int radius = 1; radius <= FarmFieldRange && planted < StarterFields; radius++)
                for (int y = farm.Y - radius; y <= farm.Y + farm.Sz - 1 + radius && planted < StarterFields; y++)
                    for (int x = farm.X - radius; x <= farm.X + farm.Sz - 1 + radius && planted < StarterFields; x++)
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
