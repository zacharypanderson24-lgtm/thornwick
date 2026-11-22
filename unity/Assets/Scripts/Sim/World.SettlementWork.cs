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

        public bool HasAnimalAt(int x, int y)
        {
            int index = (y - Top) * (Right - Left + 1) + x - Left;
            int spacing = World.TilesPerAnimal(Species);
            return index % spacing == 0 && index / spacing < Count;
        }
    }

    public partial class World
    {
        public const int StorehouseSpace = 80, HaulPerWorker = 4, StarterLivestockGold = 5;
        public const int LogsPerCharcoal = 2, SmokehouseRate = 3;
        readonly Dictionary<int, LivestockPen> _penTiles = new();
        readonly List<LivestockPen> _pens = new();
        double _freshMeat;
        public double FreshMeatStored => Math.Clamp(_freshMeat, 0, Math.Max(0, ResAmt[Res.Food] - WheatStored));
        double FreshMeatShare(double amount) => ResAmt[Res.Food] > 0 ? FreshMeatStored * Math.Clamp(amount / ResAmt[Res.Food], 0, 1) : 0;
        public IReadOnlyList<LivestockPen> LivestockPens => _pens;
        public LivestockPen PenAt(int x, int y) => Inb(x, y) && _penTiles.TryGetValue(Tk(x, y), out var pen) ? pen : null;
        public static bool IsLivestockZone(Zone kind) => kind == Zone.Pigs || kind == Zone.Sheep || kind == Zone.Chickens;
        public static int TilesPerAnimal(Zone kind) => kind switch { Zone.Pigs => 3, Zone.Sheep => 2, _ => 1 };

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

        double WorkLivestock()
        {
            double meat = 0;
            foreach (var pen in _pens)
            {
                if (!pen.Tiles().Any(t => BesideConnectedRoad(t.x, t.y))) continue;
                int cycle = pen.Species == Zone.Chickens ? 4 : pen.Species == Zone.Sheep ? 7 : 6;
                if ((Day + Tk(pen.Left, pen.Top)) % cycle != 0) continue;
                double feed = pen.Count * 0.25;
                if (WheatStored < feed || ResAmt[Res.Food] - feed < DailyFoodDemand) continue;
                _wheat = WheatStored - feed;
                ResAmt[Res.Food] -= feed;
                if (pen.Count < pen.Capacity) { pen.Count++; Touch(); }
                else meat += pen.Count * (pen.Species == Zone.Pigs ? 2.0 : pen.Species == Zone.Sheep ? 1.5 : 1.0);
            }
            return meat;
        }

        void PreserveMeat()
        {
            foreach (var b in Blds.Where(b => b.T == "smokehouse" && b.Served && b.Workers > 0))
            {
                double kept = Math.Min(Math.Min(FreshMeatStored, SmokehouseRate * b.Eff),
                    Math.Min(ResAmt[Res.Logs] * 4, CropStorage(Res.SmokedMeat) - ResAmt[Res.SmokedMeat]));
                if (kept <= 0) continue;
                _freshMeat = FreshMeatStored - kept;
                ResAmt[Res.Food] -= kept;
                ResAmt[Res.Logs] -= kept / 4;
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
