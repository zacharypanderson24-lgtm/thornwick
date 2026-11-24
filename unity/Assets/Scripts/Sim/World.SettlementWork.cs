using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public sealed class LivestockPen
    {
        public Zone Species { get; }
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public int Count { get; internal set; }
        public int LastShearedYear { get; internal set; }
        /// <summary>The villager looking after this pen (0 or 1), from the farming trade (World.Recalc). Without one the animals give
        /// nothing and do not breed. Runtime only.</summary>
        public int Keepers { get; internal set; }
        public int Area => (Right - Left + 1) * (Bottom - Top + 1);
        public int Capacity => Area / World.TilesPerAnimal(Species);
        public int FenceLength => 2 * (Right - Left + Bottom - Top + 2);

        public LivestockPen(Zone species, int ax, int ay, int bx, int by)
        {
            Species = species;
            Left = Math.Min(ax, bx); Right = Math.Max(ax, bx);
            Top = Math.Min(ay, by); Bottom = Math.Max(ay, by);
        }

        public IEnumerable<(int x, int y)> Tiles()
        {
            for (int y = Top; y <= Bottom; y++)
                for (int x = Left; x <= Right; x++)
                    yield return (x, y);
        }

    }

    public partial class World
    {
        public const int StorehouseSpace = 80, HaulPerWorker = 4, StarterLivestockGold = 5;
        /// <summary>A smokehouse worker preserves this much fresh meat a day (15 food: meat for 15 villagers).</summary>
        public const int LogsPerCharcoal = 2, SmokehouseRate = 15;
        /// <summary>Food one log of fuel smokes.</summary>
        public const int FoodPerSmokeLog = 20;
        public const int EggsPerChicken = 1, WoolPerSheep = 1, EggsPerFood = 1, EggStorage = 80;
        /// <summary>A hen lays an egg every this many days (an egg is a food: a third of a food a day).</summary>
        public const int DaysPerEgg = 3;
        /// <summary>Every pen with animals needs one keeper from the farming trade.</summary>
        public const int KeepersPerPen = 1;
        /// <summary>A pen that needs a keeper: it has animals and a road to the Town Hall.</summary>
        bool PenNeedsKeeper(LivestockPen pen) => pen.Count > 0 && PenHasRoad(pen);
        /// <summary>Wheat an animal eats a day (a hen lays a third of a food a day on 0.2, so it earns its keep).</summary>
        public const double WheatPerAnimalPerDay = 0.2;
        readonly Dictionary<int, LivestockPen> _penTiles = new();
        readonly List<LivestockPen> _pens = new();
        double _freshMeat;
        public double FreshMeatStored => Math.Clamp(_freshMeat, 0, Math.Max(0, ResAmt[Res.Food] - WheatStored));
        double FreshMeatShare(double amount) => ResAmt[Res.Food] > 0 ? FreshMeatStored * Math.Clamp(amount / ResAmt[Res.Food], 0, 1) : 0;
        public IReadOnlyList<LivestockPen> LivestockPens => _pens;
        public LivestockPen PenAt(int x, int y) => Inb(x, y) && _penTiles.TryGetValue(Tk(x, y), out var pen) ? pen : null;
        public static bool IsLivestockZone(Zone kind) => kind == Zone.Pigs || kind == Zone.Sheep || kind == Zone.Chickens;
        public static int TilesPerAnimal(Zone kind) => kind switch { Zone.Pigs => 3, Zone.Sheep => 2, _ => 1 };
        public static int MeatPerAnimal(Zone kind) => kind switch { Zone.Pigs => 20, Zone.Sheep => 15, _ => 5 };
        public bool PenHasRoad(LivestockPen pen) => pen.Tiles().Any(t => BesideConnectedRoad(t.x, t.y));

        public Result CanBuyLivestock(LivestockPen pen)
        {
            if (!_pens.Contains(pen)) return Result.No("Select a livestock pen");
            if (pen.Count >= pen.Capacity) return Result.No("The pen is full");
            if (!PenHasRoad(pen)) return Result.No("Connect the pen to the Town Hall");
            return ResAmt[Res.Gold] >= StarterLivestockGold ? Result.Yes() : Result.No($"An animal costs {StarterLivestockGold} gold");
        }

        public Result BuyLivestock(LivestockPen pen)
        {
            var check = CanBuyLivestock(pen);
            if (!check.Ok) return check;
            ResAmt[Res.Gold] -= StarterLivestockGold;
            pen.Count++;
            Touch();
            return check;
        }

        public Result CanSlaughterLivestock(LivestockPen pen)
        {
            if (!_pens.Contains(pen)) return Result.No("Select a livestock pen");
            if (pen.Count == 0) return Result.No("This pen has no livestock");
            if (!PenHasRoad(pen)) return Result.No("Connect the pen to the Town Hall");
            int meat = MeatPerAnimal(pen.Species);
            return ResAmt[Res.Food] + meat <= CropStorage(Res.Food) ? Result.Yes(meat) : Result.No($"Make room for {meat} fresh meat in food storage");
        }

        public Result SlaughterLivestock(LivestockPen pen)
        {
            var check = CanSlaughterLivestock(pen);
            if (!check.Ok) return check;
            _freshMeat = FreshMeatStored + check.Count;
            ResAmt[Res.Food] += check.Count;
            pen.Count--;
            Touch();
            return check;
        }

        public Result CheckPen(Zone kind, int ax, int ay, int bx, int by)
        {
            var pen = new LivestockPen(kind, ax, ay, bx, by);
            if (!IsLivestockZone(kind)) return Result.No("Choose a livestock species");
            if (!Inb(ax, ay) || !Inb(bx, by)) return Result.No("Outside the map");
            if (pen.Capacity == 0) return Result.No($"This species needs {TilesPerAnimal(kind)} tiles per animal");
            int level = Vh(ax, ay);
            foreach (var (x, y) in pen.Tiles())
            {
                if (!Claimed(x, y)) return Result.No(NotYours);
                if (Grid[y, x] != null || ZoneMap[y, x] != Zone.None || DecoMap[y, x] != Deco.None)
                    return Result.No("Pens need clear, unzoned land");
                if (IsSolid(x, y) || TileSpread(x, y) != 0 || Vh(x, y) != level)
                    return Result.No("The entire pen must be level");
            }
            if (!pen.Tiles().Any(t => BesideConnectedRoad(t.x, t.y))) return Result.No("One edge must touch a road to the Town Hall");
            if (ResAmt[Res.Wood] < pen.FenceLength) return Result.No($"The fence needs {pen.FenceLength} wood");
            if (ResAmt[Res.Gold] < StarterLivestockGold) return Result.No($"Starter livestock cost {StarterLivestockGold} gold");
            return Result.Yes(pen.Capacity);
        }

        public Result PlacePen(Zone kind, int ax, int ay, int bx, int by)
        {
            var check = CheckPen(kind, ax, ay, bx, by);
            if (!check.Ok) return check;
            var pen = new LivestockPen(kind, ax, ay, bx, by) { Count = 1 };
            ResAmt[Res.Wood] -= pen.FenceLength;
            ResAmt[Res.Gold] -= StarterLivestockGold;
            AddPen(pen);
            Touch();
            return check;
        }

        void AddPen(LivestockPen pen)
        {
            _pens.Add(pen);
            foreach (var (x, y) in pen.Tiles())
            {
                _penTiles.Add(Tk(x, y), pen);
                ZoneMap[y, x] = pen.Species;
            }
        }

        int RemovePen(LivestockPen pen)
        {
            _pens.Remove(pen);
            foreach (var (x, y) in pen.Tiles())
            {
                _penTiles.Remove(Tk(x, y));
                ZoneMap[y, x] = Zone.None;
            }
            return pen.Area;
        }

        int WorkLivestock()
        {
            int eggs = 0;
            foreach (var pen in _pens)
            {
                if (pen.Count == 0 || !PenHasRoad(pen)) continue;
                if (pen.Keepers <= 0) continue;   // nobody to feed them, gather eggs or shear: nothing comes of the pen today
                double feed = pen.Count * WheatPerAnimalPerDay;
                if (WheatStored < feed || ResAmt[Res.Food] < feed) continue;
                _wheat = WheatStored - feed;
                ResAmt[Res.Food] -= feed;
                if (pen.Species == Zone.Chickens && (Day + Tk(pen.Left, pen.Top)) % DaysPerEgg == 0) eggs += pen.Count * EggsPerChicken;
                if (pen.Species == Zone.Sheep && YearDay <= SeasonLength && pen.LastShearedYear != Year)
                {
                    ResAmt[Res.Wool] += pen.Count * WoolPerSheep;
                    pen.LastShearedYear = Year;
                }
                int cycle = pen.Species == Zone.Chickens ? 4 : pen.Species == Zone.Sheep ? 7 : 6;
                if (pen.Count < pen.Capacity && (Day + Tk(pen.Left, pen.Top)) % cycle == 0)
                {
                    pen.Count++;
                    Touch();
                }
            }
            return eggs;
        }

        void PreserveMeat()
        {
            foreach (var b in Blds.Where(b => b.T == "smokehouse" && b.Served && b.Workers > 0))
            {
                double kept = Math.Min(Math.Min(FreshMeatStored, SmokehouseRate * b.Eff),
                    Math.Min(ResAmt[Res.Logs] * FoodPerSmokeLog, CropStorage(Res.SmokedMeat) - ResAmt[Res.SmokedMeat]));
                if (kept <= 0) continue;
                _freshMeat = FreshMeatStored - kept;
                ResAmt[Res.Food] -= kept;
                ResAmt[Res.Logs] -= kept / FoodPerSmokeLog;
                ResAmt[Res.SmokedMeat] += kept;
            }
        }

        void BurnCharcoal()
        {
            foreach (var b in Blds.Where(b => b.T == "charcoal" && b.Served && b.Workers > 0))
            {
                double made = Math.Min(b.Eff * 2, Math.Min(ResAmt[Res.Logs] / LogsPerCharcoal, CropStorage(Res.Charcoal) - ResAmt[Res.Charcoal]));
                if (made <= 0) continue;
                ResAmt[Res.Logs] -= made * LogsPerCharcoal;
                ResAmt[Res.Charcoal] += made;
            }
        }

        static bool StoredByHaulers(Res resource) =>
            resource == Res.Wood || resource == Res.Logs || resource == Res.Stone ||
            resource == Res.Ore || resource == Res.Metal || resource == Res.Charcoal;

        int StorehouseCapacity(Res resource) => StoredByHaulers(resource)
            ? Blds.Count(b => b.T == "storehouse" && b.Served) * StorehouseSpace : 0;

        void AddLooseMaterial(Res resource, double amount)
        {
            ResAmt[resource] += Math.Min(amount, Math.Max(0, CropStorage(resource) - ResAmt[resource]));
        }
    }
}
