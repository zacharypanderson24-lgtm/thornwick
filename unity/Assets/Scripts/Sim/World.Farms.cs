using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        const int StarterFieldRange = 3;   // only where the starting farm gets its first fields; farms have no work radius
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
            var fields = SeasonalFarming ? FarmFields().Keys.ToList() : new List<int>();
            if (!SeasonalFarming)
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

        // Farms have no work radius. The farmhouses decide how many farmers there can be (their jobs); the Farmland tiles decide the crops
        // and how many farmers are needed. Every field on the map is worked by the road-served farmhouses together: the fields are
        // shared out so each farmhouse's share stays in proportion to its jobs (a farmhouse with twice the jobs gets twice the fields).
        Dictionary<int, Building> _farmFieldCache; int _farmFieldVersion = -1;
        public Dictionary<int, Building> FarmFields()
        {
            if (_farmFieldCache != null && _farmFieldVersion == Version) return _farmFieldCache;
            var farms = Blds.Where(f => f.T == "farm" && f.Served).ToList();
            var fields = new Dictionary<int, Building>();
            if (farms.Count > 0)
            {
                var load = new int[farms.Count];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        if (!IsFarmField(x, y)) continue;
                        int best = 0; double bestScore = double.MaxValue;
                        for (int i = 0; i < farms.Count; i++)
                        {
                            double score = (load[i] + 1.0) / farms[i].Jobs;
                            if (score < bestScore - 1e-9) { bestScore = score; best = i; }
                        }
                        load[best]++;
                        fields[Tk(x, y)] = farms[best];
                    }
            }
            _farmFieldCache = fields; _farmFieldVersion = Version;
            return fields;
        }

        public bool SeasonalFarming => Style != TerrainStyle.Classic;
        public const int SeasonLength = 90, YearLength = 360, WheatGrowth = 100, FlaxGrowth = 125;
        public const int WheatYield = 85, FlaxYield = 30;
        public int YearDay => (Math.Max(1, Day) - 1) % YearLength + 1;
        public int Year => (Math.Max(1, Day) - 1) / YearLength + 1;
        public string Season => YearDay <= 90 ? "Spring" : YearDay <= 180 ? "Summer" : YearDay <= 270 ? "Autumn" : "Winter";
        public double DailyFoodDemand => (Peasants + Soldiers * 1.5) * 0.2;
        int YearStart => Day - YearDay + 1;

        // Signed planting day (negative for flax). Zero marks harvested soil awaiting preparation.
        readonly Dictionary<int, int> _sownFields = new Dictionary<int, int>();
        readonly Dictionary<Building, List<int>> _fieldJobs = new Dictionary<Building, List<int>>();
        int _farmJobDay = -1, _farmJobVersion = -1;

        public int SownOn(int key) => Math.Abs(_sownFields.TryGetValue(key, out int sow) ? sow : 0);
        public bool PlantedFlax(int key) => _sownFields.TryGetValue(key, out int sow) && sow < 0;
        public static int CropGrowth(bool flax) => flax ? FlaxGrowth : WheatGrowth;
        public static int LastSowingDay(bool flax) => 2 * SeasonLength - CropGrowth(flax);

        void RefreshFarmJobs()
        {
            if (!SeasonalFarming || (_farmJobDay == Day && _farmJobVersion == Version)) return;
            _farmJobDay = Day;
            _farmJobVersion = Version;
            _fieldJobs.Clear();
            if (YearDay > 270) return;

            foreach (var field in FarmFields().OrderBy(pair => pair.Key))
            {
                int key = field.Key;
                int sow = SownOn(key);
                bool work;
                if (YearDay <= 90)
                    work = !_sownFields.ContainsKey(key) && YearDay <= LastSowingDay(IsFlaxField(key % N, key / N));
                else if (YearDay <= 180)
                    work = sow >= YearStart && Day >= sow + CropGrowth(PlantedFlax(key));
                else
                    work = _sownFields.ContainsKey(key);
                if (!work) continue;

                if (!_fieldJobs.TryGetValue(field.Value, out var jobs))
                    _fieldJobs[field.Value] = jobs = new List<int>();
                jobs.Add(key);
            }
        }

        /// <summary>How many workers a building really needs: its jobs (what its size allows) capped by the work there is. Farms: the fields
        /// that need sowing or harvesting today. Lodges, quarries and mines: the marked or zoned tiles within their reach. Everything
        /// else: all its jobs.</summary>
        public int ActiveJobs(Building b)
        {
            if (SeasonalFarming && b.T == "farm") return _fieldJobs.TryGetValue(b, out var jobs) ? Math.Min(b.Jobs, jobs.Count) : 0;
            return _workTiles.TryGetValue(b, out int tiles) ? Math.Min(b.Jobs, tiles) : b.Jobs;
        }

        // Tiles a lodge, quarry or mine could work right now, within WorkR of the building (World.Crews.cs, World.Mining.cs).
        readonly Dictionary<Building, int> _workTiles = new Dictionary<Building, int>();
        int _workTilesVersion = -1;

        void RefreshWorkTiles()
        {
            if (_workTilesVersion == Version) return;
            _workTilesVersion = Version; _workTiles.Clear();
            foreach (var b in Blds)
            {
                var kind = CrewKind(b.T); if (kind == Deco.None || b.T == "hunter") continue;
                int n = 0;
                for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                    for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                    {
                        if (!Inb(i, j)) continue;
                        // a mine's work: Mining-zone tiles still to survey or holding known ore; the others: what a crew can fell or cut
                        if (b.T == "mine" ? InMineReach(i, j) && (!Surveyed[Tk(i, j)] || HasKnownOre(i, j)) : CrewMatch(kind, i, j)) n++;
                    }
                _workTiles[b] = n;
            }
        }

        (double food, double flax) WorkSeasonalFields()
        {
            double wheat = 0, flax = 0;
            bool changed = false;
            foreach (var pair in _fieldJobs)
            {
                foreach (int key in pair.Value.Take(pair.Key.Workers))
                {
                    if (YearDay <= 90)
                        _sownFields[key] = IsFlaxField(key % N, key / N) ? -Day : Day;
                    else if (YearDay <= 180)
                    {
                        if (PlantedFlax(key)) flax += FlaxYield;
                        else wheat += WheatYield;
                        _sownFields[key] = 0;
                    }
                    else
                        _sownFields.Remove(key);
                    changed = true;
                }
            }
            if (changed) Touch();
            return (wheat, flax);
        }

        public (int wheat, int flax, int wheatTarget, int flaxTarget, int firstHarvest,
            int emptyDay, double wheatYield, double flaxYield, double overflow) FarmingForecast()
        {
            int wheat = 0, flax = 0, wheatTarget = 0, flaxTarget = 0;
            var deliveries = new SortedDictionary<int, double>();
            double flaxYield = 0;
            var sowCount = new Dictionary<Building, int>();
            var harvestSlots = new Dictionary<(Building farm, int day), int>();
            foreach (var field in FarmFields().OrderBy(pair => pair.Key))
            {
                int key = field.Key;
                bool targetFlax = IsFlaxField(key % N, key / N);
                if (targetFlax) flaxTarget++;
                else wheatTarget++;

                int sow = SownOn(key);
                bool planted = sow >= YearStart;
                bool cropFlax = planted ? PlantedFlax(key) : targetFlax;
                if (planted)
                {
                    if (cropFlax) flax++;
                    else wheat++;
                    if (YearDay > 180) continue;
                }
                else
                {
                    if (_sownFields.ContainsKey(key)) continue;
                    sowCount.TryGetValue(field.Value, out int waiting);
                    sow = Day + waiting / field.Value.Jobs;
                    if (sow - YearStart + 1 > LastSowingDay(cropFlax)) continue;
                    sowCount[field.Value] = waiting + 1;
                }

                int date = Math.Max(Day, sow + CropGrowth(cropFlax));
                while (harvestSlots.TryGetValue((field.Value, date), out int booked) && booked >= field.Value.Jobs)
                    date++;
                if (date >= YearStart + 2 * SeasonLength) continue;
                harvestSlots.TryGetValue((field.Value, date), out int used);
                harvestSlots[(field.Value, date)] = used + 1;
                if (cropFlax) flaxYield += FlaxYield;
                deliveries.TryGetValue(date, out double food);
                deliveries[date] = food + (cropFlax ? 0 : WheatYield);
            }

            double eat = DailyFoodDemand, stock = ResAmt[Res.Food], overflow = 0;
            int firstHarvest = deliveries.Count == 0 ? 0 : deliveries.Keys.First();
            int emptyDay = 0, capacity = CropStorage(Res.Food);
            double wheatYield = 0;
            for (int date = Day; date < YearStart + YearLength + WheatGrowth; date++)
            {
                deliveries.TryGetValue(date, out double arrival);
                wheatYield += arrival;
                double space = Math.Max(0, capacity - stock);
                overflow += Math.Max(0, arrival - space);
                stock = Math.Max(0, stock + Math.Min(space, arrival) - eat);
                if (stock <= 0 && eat > 0 && emptyDay == 0) emptyDay = date;
            }
            return (wheat, flax, wheatTarget, flaxTarget, firstHarvest, emptyDay, wheatYield, flaxYield, overflow);
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
            int planted = FarmFields().Count;
            for (int radius = 1; radius <= StarterFieldRange && planted < StarterFields; radius++)
                for (int y = farm.Y - radius; y <= farm.Y + farm.Sz - 1 + radius && planted < StarterFields; y++)
                    for (int x = farm.X - radius; x <= farm.X + farm.Sz - 1 + radius && planted < StarterFields; x++)
                    {
                        if (!Inb(x, y) || Grid[y, x] != null || ZoneMap[y, x] != Zone.None ||
                            DecoMap[y, x] != Deco.None || !TileFlat(x, y) || IsSolid(x, y)) continue;
                        Fert[y, x] = true;
                        ZoneMap[y, x] = Zone.Farm;
                        planted++;
                    }
            _farmFieldCache = null;
        }
    }
}
