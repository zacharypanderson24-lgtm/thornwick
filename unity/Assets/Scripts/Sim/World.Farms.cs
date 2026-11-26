using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        const int StarterFieldRange = 3;   // only where the starting farm gets its first fields
        const int FieldsPerWorker = 2;
        /// <summary>Seasonal farming: one farmer sows, tends and reaps this many fields a year (9 x 400 = 3,600 food: exactly 10 people, who
        /// eat 1 food a day, 360 a year, each).</summary>
        public const int FieldsPerFarmer = 9;
        /// <summary>A farm only works Farmland within this many tiles of its farmhouse (shown as a shaded ring); farther fields lie fallow.
        /// A full 2x2 farmhouse (10 farmers, 90 fields) fits comfortably inside it.</summary>
        public const int FarmReach = 10;
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

        // Seasonal farming: a farmhouse works the Farmland within FarmReach of it, nearest first, up to FieldsPerFarmer fields for each of
        // its jobs; fields out of reach of every farm, or beyond what the farms can work, lie fallow. (Classic farming, kept for the parity
        // tests against the browser prototype: farms have no reach and share out every field in proportion to their jobs.)
        Dictionary<int, Building> _farmFieldCache; int _farmFieldVersion = -1;
        public Dictionary<int, Building> FarmFields()
        {
            if (_farmFieldCache != null && _farmFieldVersion == Version) return _farmFieldCache;
            var farms = Blds.Where(f => f.T == "farm" && f.Served).ToList();
            var fields = new Dictionary<int, Building>();
            if (farms.Count > 0 && SeasonalFarming)
            {
                // every (field, farm) pair within reach, nearest first; each field goes to the nearest farm that still has room for it
                var pairs = new List<(int dist, int key, int farm)>();
                for (int i = 0; i < farms.Count; i++)
                {
                    var f = farms[i];
                    for (int y = f.Y - FarmReach; y <= f.Y + f.Sz - 1 + FarmReach; y++)
                        for (int x = f.X - FarmReach; x <= f.X + f.Sz - 1 + FarmReach; x++)
                        {
                            if (!IsFarmField(x, y)) continue;
                            int dx = Math.Max(0, Math.Max(f.X - x, x - (f.X + f.Sz - 1))), dy = Math.Max(0, Math.Max(f.Y - y, y - (f.Y + f.Sz - 1)));
                            pairs.Add((Math.Max(dx, dy), Tk(x, y), i));
                        }
                }
                pairs.Sort((a, b) => a.dist != b.dist ? a.dist.CompareTo(b.dist) : a.key != b.key ? a.key.CompareTo(b.key) : a.farm.CompareTo(b.farm));
                var load = new int[farms.Count];
                foreach (var (_, key, i) in pairs)
                {
                    if (fields.ContainsKey(key) || load[i] >= farms[i].Jobs * FieldsPerFarmer) continue;
                    fields[key] = farms[i]; load[i]++;
                }
            }
            else if (farms.Count > 0)
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
        public const int WheatYield = 400, FlaxYield = 30;
        public int YearDay => (Math.Max(1, Day) - 1) % YearLength + 1;
        public int Year => (Math.Max(1, Day) - 1) / YearLength + 1;
        public string Season => YearDay <= 90 ? "Spring" : YearDay <= 180 ? "Summer" : YearDay <= 270 ? "Autumn" : "Winter";
        /// <summary>FOOD UNITS (Zach's rule): one villager eats FoodPerPerson = 1 food a day, so every food number reads as "villagers fed":
        /// 100 food is 100 villager-days. Every food quantity in the Unity game (crop yields, hunting loads, storage, meat, loot) is in these
        /// units. The browser-parity Classic style keeps the old 0.2 a head and its own numbers.</summary>
        public const double FoodPerPerson = 1.0, ClassicFoodPerPerson = 0.2, SoldierFoodFactor = 1.5;
        /// <summary>How many of today's food units one old (0.2 a head) unit became: for thresholds that were written in old units.</summary>
        public double FoodScale => SeasonalFarming ? FoodPerPerson / ClassicFoodPerPerson : 1.0;
        public double DailyFoodDemand => (Peasants + Soldiers * SoldierFoodFactor) * (SeasonalFarming ? FoodPerPerson : ClassicFoodPerPerson);
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

        Dictionary<Building, int> _farmFieldCounts; int _farmFieldCountVersion = -1;
        /// <summary>How many fields this farm works (seasonal farming: at most FieldsPerFarmer for each of its jobs).</summary>
        public int FieldsOf(Building farm)
        {
            if (_farmFieldCounts == null || _farmFieldCountVersion != Version)
            {
                _farmFieldCounts = new Dictionary<Building, int>();
                foreach (var f in FarmFields().Values) _farmFieldCounts[f] = _farmFieldCounts.GetValueOrDefault(f) + 1;
                _farmFieldCountVersion = Version;
            }
            return _farmFieldCounts.GetValueOrDefault(farm);
        }

        /// <summary>Farmland tiles no farm works: out of every farm's reach, or more than the farms can manage.</summary>
        public int FallowFields()
        {
            if (!SeasonalFarming) return 0;
            if (_fallowVersion == Version) return _fallow;
            int all = 0;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (IsFarmField(x, y)) all++;
            _fallowVersion = Version;
            return _fallow = all - FarmFields().Count;
        }
        int _fallow, _fallowVersion = -1;

        /// <summary>How many workers a building really needs: its jobs (what its size allows) capped by the work there is. Farms (seasonal):
        /// from sowing to the end of harvest (the first half of the year) one farmer for every FieldsPerFarmer fields, all season long, as
        /// fields are sown, tended and reaped; after that only while stubble is cleared. Lodges, quarries and mines: the marked or zoned
        /// tiles within their reach. Everything else: all its jobs.</summary>
        public int ActiveJobs(Building b)
        {
            if (SeasonalFarming && b.T == "farm")
            {
                int owned = FieldsOf(b);
                if (YearDay <= 2 * SeasonLength && owned > 0) return Math.Min(b.Jobs, (owned + FieldsPerFarmer - 1) / FieldsPerFarmer);
                return _fieldJobs.TryGetValue(b, out var jobs) ? Math.Min(b.Jobs, jobs.Count) : 0;
            }
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
                if (b.T == "mine") { _workTiles[b] = MineWorkTiles(b, b.Jobs + 1); continue; }   // the zoned ground its tunnel could reach, plus ore at its face
                for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                    for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                    {
                        if (!Inb(i, j)) continue;
                        if (CrewMatch(kind, i, j)) n++;   // what a crew can fell or cut
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
                int today = pair.Key.Workers;
                if (YearDay <= 90)
                {
                    // a short-handed farm sows only what its farmers can tend all season: FieldsPerFarmer each
                    int sown = FarmFields().Count(f => f.Value == pair.Key && SownOn(f.Key) >= YearStart);
                    today = Math.Min(today, Math.Max(0, pair.Key.Workers * FieldsPerFarmer - sown));
                }
                foreach (int key in pair.Value.Take(today))
                {
                    if (YearDay <= 90)
                        _sownFields[key] = IsFlaxField(key % N, key / N) ? -Day : Day;
                    else if (YearDay <= 180)
                    {
                        double soil = MoistureYield(key % N, key / N);   // 1 on a dry-world map; on a water map the field's soil moisture
                        if (PlantedFlax(key)) flax += FlaxYield * soil;
                        else wheat += WheatYield * soil;
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
                double soilFactor = MoistureYield(field.Key % N, field.Key / N);
                if (cropFlax) flaxYield += FlaxYield * soilFactor;
                deliveries.TryGetValue(date, out double food);
                deliveries[date] = food + (cropFlax ? 0 : WheatYield * soilFactor);
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

        /// <summary>Food the farms make on an average day. Seasonal farming: the wheat fields the farmers can tend (FieldsPerFarmer each)
        /// times WheatYield, spread over the year. Classic farming: today's output.</summary>
        public double FarmFood()
        {
            if (!SeasonalFarming) return FarmOutput().food;
            double food = 0;
            foreach (var farm in Blds.Where(b => b.T == "farm" && b.Served))
            {
                var mine = FarmFields().Where(f => f.Value == farm).Select(f => f.Key).ToList();
                int wheat = mine.Count(k => !IsFlaxField(k % N, k / N));
                double tended = Math.Min(1.0, farm.Workers * FieldsPerFarmer / (double)Math.Max(1, mine.Count));
                food += wheat * tended * WheatYield / (double)YearLength;
            }
            return food;
        }

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
