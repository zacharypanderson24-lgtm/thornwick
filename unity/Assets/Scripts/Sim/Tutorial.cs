using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>Guides a new settlement from its first meals to its first garrison.</summary>
    public sealed class Tutorial
    {
        public const int WheatFields = 18, Silos = 4, ReserveDays = World.YearLength + 5;
        public const int MilitiaRaidSize = 2, ReturnRaidSize = 3, GarrisonSize = 3;
        static readonly (int x, int y) HuntingLot = (Defs.Start + 1, Defs.Start + 4);
        static readonly (int x, int y) QuarryLot = (Defs.Start + 2, Defs.Start - 1);
        static readonly (int x, int y) LumberLot = (Defs.Start + 1, Defs.Start + 6);
        static readonly (int x, int y) BarracksLot = (Defs.Start - 3, Defs.Start - 1);
        static readonly (int x, int y)[] Stone = Enumerable.Range(Defs.Start - 2, 3).Select(y => (Defs.Start + 5, y)).ToArray();
        static readonly (int x, int y)[] Timber = Enumerable.Range(Defs.Start + 5, 3).Select(y => (Defs.Start + 4, y)).ToArray();

        public sealed class Step
        {
            public string Id, Title, Body, Tool, Goal, Button;
            public Func<World, bool> Ready;
        }

        public bool Active { get; private set; }
        public int Index { get; private set; }
        public Step Current => Finished ? null : Steps[Index];
        public bool Finished => Index >= Steps.Count;
        public RaidReport FirstRaid { get; private set; }
        public RaidReport ReturnRaid { get; private set; }
        bool _opened;

        public void Open(World world)
        {
            if (!_opened && world.SeasonalFarming && world.Day == 1 && world.Blds.Count == 9)
                PrepareStarterGround(world);
            _opened = true;
            if (Finished) Index = Steps.Count - 1;
            Active = true;
        }

        public void Close() => Active = false;

        public bool CanAdvance(World world)
        {
            if (!Active || Current?.Button == null) return false;
            return Current.Id switch
            {
                "militia" => world.MilitiaFights && (world.Raid == null || world.Raid.Over),
                "return" => GarrisonReady(world) && (world.Raid == null || world.Raid.Over),
                _ => true
            };
        }

        public void Next(World world)
        {
            if (!CanAdvance(world)) return;
            if (Current.Id == "militia") FirstRaid = world.BeginRaid(MilitiaRaidSize);
            else if (Current.Id == "return") ReturnRaid = world.BeginRaid(ReturnRaidSize);
            Index++;
            if (Finished) Close();
        }

        public Step Check(World world)
        {
            if (!Active || Finished) return null;
            var step = Current;
            bool ready = step.Id switch
            {
                "first-raid" => FirstRaid?.Over == true,
                "second-raid" => ReturnRaid?.Over == true && !world.Starving && world.Peasants > 0 && FoodDays(world) >= 30,
                _ => step.Ready?.Invoke(world) == true
            };
            if (!ready) return null;
            Index++;
            return step;
        }

        public string Progress(World world)
        {
            if (!Active) return null;
            return Current.Id switch
            {
                "hunt" => Connected(world, "hunter") ? "Waiting for the hunters" : "Place a road-connected lodge",
                "farmland" => $"{Wheat(world)} / {WheatFields} wheat fields",
                "silos" => $"{CountConnected(world, "silo")} / {Silos} connected silos",
                "harvest" => $"{world.Season} · {OpeningFoodDays(world)} / {ReserveDays} food days",
                "mining" => Connected(world, "quarry") ? "Zone rock and wait for stone" : "Place a road-connected quarry",
                "lumber" => Connected(world, "wood") ? "Zone trees and wait for logs" : "Place a road-connected lodge",
                "militia" => world.MilitiaFights ? "Militia on" : "Enable Militia fights in Troops",
                "first-raid" => RaidProgress(FirstRaid),
                "barracks" => $"{world.Soldiers} / {GarrisonSize} soldiers" +
                    (!Connected(world, "barracks") ? " · connect a Barracks" : world.CanAfford(world.NextRecruit) ? "" : " · check recruit supplies in Troops"),
                "return" => GarrisonReady(world) ? "Garrison ready" : "Reconnect the Barracks and train three soldiers",
                "second-raid" => ReturnRaid?.Over == true ? "Rebuild 30 days of food before finishing" : RaidProgress(ReturnRaid),
                "finish" => $"{FoodDays(world)} food days at today's demand",
                _ => null
            };
        }

        static string RaidProgress(RaidReport raid) => raid == null ? null :
            $"{raid.From} · {raid.Active} remaining · {raid.Killed} repelled · {raid.Stolen.Values.Sum()} goods lost";

        public string BodyText => Current?.Id switch
        {
            "barracks" when FirstRaid != null => Current.Body + "\n\n" + FirstRaid.Summary(),
            "finish" when ReturnRaid != null => Current.Body + "\n\n" + ReturnRaid.Summary(),
            _ => Current?.Body ?? ""
        };

        public IEnumerable<(int x, int y)> SuggestedTiles(World world)
        {
            if (!Active) return Enumerable.Empty<(int, int)>();
            switch (Current.Id)
            {
                case "hunt": return Site(world, "hunter", HuntingLot);
                case "farmland":
                    return (from row in Enumerable.Range(Start + 1, 6)
                            from column in Enumerable.Range(Start - 5, 4)
                            where world.ZoneMap[row, column] == Zone.None && world.ZoneCheck(Zone.Farm, column, row).Ok
                            select (column, row)).Take(Math.Max(0, WheatFields - Wheat(world)));
                case "silos":
                    return (from row in Enumerable.Range(Start - 2, 10)
                            from column in Enumerable.Range(Start - 4, 9)
                            where !Reserved(column, row) && world.ZoneMap[row, column] == Zone.None &&
                                  world.CanPlace("silo", column, row).Ok && world.WouldConnect("silo", column, row)
                            select (column, row)).Take(Math.Max(0, Silos - CountConnected(world, "silo")));
                case "mining":
                    return Connected(world, "quarry")
                        ? Stone.Where(p => world.DecoMap[p.y, p.x] == Deco.Rock && world.ZoneMap[p.y, p.x] != Zone.Mine)
                        : Site(world, "quarry", QuarryLot);
                case "lumber":
                    return Connected(world, "wood")
                        ? Timber.Where(p => world.DecoMap[p.y, p.x] == Deco.Tree && world.ZoneMap[p.y, p.x] != Zone.Wood)
                        : Site(world, "wood", LumberLot);
                case "barracks": return Site(world, "barracks", BarracksLot);
                default: return Enumerable.Empty<(int, int)>();
            }
        }

        static IEnumerable<(int x, int y)> Site(World w, string type, (int x, int y) lot) =>
            !Connected(w, type) && w.CanPlace(type, lot.x, lot.y).Ok && w.WouldConnect(type, lot.x, lot.y)
                ? Footprint(lot) : Enumerable.Empty<(int, int)>();

        static IEnumerable<(int x, int y)> Footprint((int x, int y) lot)
        {
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++) yield return (lot.x + i, lot.y + j);
        }

        static bool Reserved(int x, int y) => Footprint(QuarryLot).Contains((x, y)) ||
            Footprint(LumberLot).Contains((x, y)) || Footprint(BarracksLot).Contains((x, y));

        static void PrepareStarterGround(World world)
        {
            void SetDeco(int x, int y, Deco deco)
            {
                if (world.Grid[y, x] != null || world.ZoneMap[y, x] != Zone.None || !world.TileFlat(x, y)) return;
                world.DecoMap[y, x] = deco;
                int key = World.Tk(x, y);
                world.OreLeftMap.Remove(key);
                world.RockAmtMap.Remove(key);
                if (deco == Deco.Rock) world.RockAmtMap[key] = 1;
            }

            foreach (var (x, y) in Footprint(LumberLot)) SetDeco(x, y, Deco.None);
            for (int y = Start + 2; y <= Start + 4; y++) SetDeco(Start + 5, y, Deco.Tree);
            foreach (var (x, y) in Stone) SetDeco(x, y, Deco.Rock);
            foreach (var (x, y) in Timber) SetDeco(x, y, Deco.Tree);
            world.Touch();
        }

        public static int FoodDays(World world) => DaysOfFood(world, world.DailyFoodDemand);

        static int DaysOfFood(World world, double demand) => demand > 0
            ? (int)Math.Floor(world.ResAmt[Res.Food] / demand + 1e-9) : 0;

        // Allow for births up to the existing housing limit and the extra rations of the tutorial garrison.
        static int OpeningFoodDays(World world) => DaysOfFood(world,
            Math.Max(world.Peasants + world.Soldiers, world.D.Civ) * 0.2 + Math.Max(world.Soldiers, GarrisonSize) * 0.1);

        static int Wheat(World w) => w.FarmFields().Keys.Count(key => !w.IsFlaxField(key % N, key / N));
        static int CountConnected(World w, string type) => w.Blds.Count(b => b.T == type && b.Served);
        static bool Connected(World w, string type) => CountConnected(w, type) > 0;
        static bool GarrisonReady(World w) => Connected(w, "barracks") && w.Soldiers >= GarrisonSize;

        public static readonly IReadOnlyList<Step> Steps = new[]
        {
            new Step { Id = "welcome", Title = "Build a settlement that can hold its own", Button = "Next",
                Body = "The town starts paused. Build while paused and use 4× to watch work happen. Follow the gold tiles or pick your own sites. Keep the starting homes until you've built up your food reserve.",
                Goal = "Start with food" },
            new Step { Id = "hunt", Title = "Hunt before the first harvest", Tool = "hunter",
                Body = "Open Hunting (5) and place the 2×2 lodge on the gold lot by the road. Leave its forest standing. Run at 4× until hunters bring back food. If nobody leaves, move Hunting up in Workers.",
                Goal = "Receive hunting food", Ready = w => w.LastHuntingDeliveryDay > 0 },
            new Step { Id = "farmland", Title = "Plant wheat in spring", Tool = "zoneFarm",
                Body = $"Open Farming (3), pick Farmland, and drag over the gold tiles or other cleared fertile soil. Aim for {WheatFields} wheat fields total. The starting farmhouse works fields anywhere. Keep flax at 0% for this harvest.",
                Goal = "Zone wheat fields", Ready = w => Wheat(w) >= WheatFields },
            new Step { Id = "silos", Title = "Build space for the crop", Tool = "silo",
                Body = $"Build {Silos} silos on the gold lots or beside another connected road. Each adds {World.SiloFood} food storage. Keep the wheat fields open and leave the starting road connected.",
                Goal = "Connect silos", Ready = w => CountConnected(w, "silo") >= Silos },
            new Step { Id = "harvest", Title = "Store food for the year",
                Body = "Run at 4× until the summer wheat comes in. Keep Farming first in Workers. The guide pauses when food can cover a year at full housing, including extra rations for three soldiers.",
                Goal = "Harvest and stock food", Ready = w => w.LastWheatHarvestDay > 0 && !w.Starving &&
                    OpeningFoodDays(w) >= ReserveDays && CountConnected(w, "silo") >= Silos && Wheat(w) >= WheatFields },
            new Step { Id = "mining", Title = "Quarry stone for construction", Tool = "quarry",
                Body = "In Mining (6), build a Quarry on the gold lot. Use Mining Zone on the highlighted rock, then run at 4×. Iron mines survey zoned hills and need a road at the entrance. Press U to see deposits.",
                Goal = "Receive a stone delivery", Ready = w => Connected(w, "quarry") && w.LastStoneDeliveryDay > 0 },
            new Step { Id = "lumber", Title = "Turn forest into building wood", Tool = "lodge",
                Body = "In Wood (4), build a Woodcutter Lodge on the gold lot. Paint Lumber Zone on the highlighted trees and run at 4×. The Town Hall saws logs slowly. A Sawmill speeds it up and a Forester replants marked land.",
                Goal = "Receive a log delivery", Ready = w => Connected(w, "wood") && w.LastLogDeliveryDay > 0 },
            new Step { Id = "militia", Title = "The militia faces the first raiders", Button = "Start raid",
                Body = "A small band is approaching. With Militia fights on in Troops, working villagers defend nearby buildings with bows, axes and other tools. Start the raid, then run at 4× and watch the attackers approach.",
                Goal = "Send the first band" },
            new Step { Id = "first-raid", Title = "First raid",
                Body = "Run at 4×. Raiders go after stores and flee with what they can carry. Follow them from the side shown below. Your militia fights around staffed buildings, and lost workers can slow production.",
                Goal = "Watch the militia defence" },
            new Step { Id = "barracks", Title = "Train a garrison before they return", Tool = "barracks",
                Body = "Build a connected Barracks (Military, 8) near your stores. Run at 4× to train three soldiers. Infantry need wood, gold and ore. Archers need wood, gold and flax. Use Troops to choose the mix.",
                Goal = "Train a garrison", Ready = GarrisonReady },
            new Step { Id = "return", Title = "The raiders return", Button = "Start raid",
                Body = "Your garrison is ready. Nearby infantry fight at close range and archers can reach farther. The militia can still help. Start the next raid, then watch which buildings the raiders target.",
                Goal = "Send the returning band" },
            new Step { Id = "second-raid", Title = "Defend with your garrison",
                Body = "Run at 4× until the returning band is gone. Raiders avoid well-defended targets. Read the final report to see what escaped and what your town kept.",
                Goal = "See the second raid through" },
            new Step { Id = "finish", Title = "Keep the settlement supplied", Button = "Finish",
                Body = "Troops eat more and leave the workforce. Check Farming for the next harvest and Workers for shortages before expanding. Save your settlement from the top bar.",
                Goal = "Finish the tutorial" },
        };
    }
}
