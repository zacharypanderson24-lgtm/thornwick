// The twelve permanent management menus (DESIGN_UI.md). Pure C#: no UnityEngine, so it is tested directly. A menu says whether it can be
// opened right now and, if not, why (shown to the player when they click it). Nothing here draws anything.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum MenuId { Settlement, Population, Economy, Construction, Agriculture, Technology, Military, Transportation, Trade, Government, Diplomacy, World, Notifications }

    public sealed class MenuDef
    {
        public MenuId Id;
        public string Title, Blurb;
        public int FunctionKey;                           // 1..12 for F1..F12, 0 = none
        public System.Func<World, string> LockedReason;   // null result = available
        public string Reason(World w) => LockedReason?.Invoke(w);
    }

    public static class MenuRegistry
    {
        /// <summary>The twelve primary menus in toolbar order.</summary>
        public static readonly IReadOnlyList<MenuDef> Primary = new List<MenuDef>
        {
            new MenuDef { Id = MenuId.Settlement, Title = "Settlement", FunctionKey = 1, Blurb = "People, food, mood and the state of the town" },
            new MenuDef { Id = MenuId.Population, Title = "Population", FunctionKey = 2, Blurb = "Who works where: trade priorities, jobs and housing" },
            new MenuDef { Id = MenuId.Economy, Title = "Economy", FunctionKey = 3, Blurb = "Stockpiles, how fast each is changing, and mining" },
            new MenuDef { Id = MenuId.Construction, Title = "Construction", FunctionKey = 4, Blurb = "Pick a building or zone from the list, then place it on the map" },
            new MenuDef { Id = MenuId.Agriculture, Title = "Agriculture", FunctionKey = 5, Blurb = "Fields, crops and livestock" },
            new MenuDef { Id = MenuId.Technology, Title = "Technology", FunctionKey = 6, Blurb = "Research", LockedReason = w => "Technology: there is no tech tree yet." },
            new MenuDef { Id = MenuId.Military, Title = "Military", FunctionKey = 7, Blurb = "Troops, companies and orders" },
            new MenuDef { Id = MenuId.Transportation, Title = "Transport", FunctionKey = 8, Blurb = "Ferry routes, boats and what the boatyard is building",
                LockedReason = w => w.Waters == null || w.WaterKind == WaterPreset.None ? "Transport: this world has no water to sail on." : null },
            new MenuDef { Id = MenuId.Trade, Title = "Trade", FunctionKey = 9, Blurb = "Caravans and ships on the road, and what carries them" },
            new MenuDef { Id = MenuId.Government, Title = "Government", FunctionKey = 10, Blurb = "Laws and taxes", LockedReason = w => "Government: no laws or taxes exist yet." },
            new MenuDef { Id = MenuId.Diplomacy, Title = "Diplomacy", FunctionKey = 11, Blurb = "Relations with other peoples", LockedReason = w => "Diplomacy: no treaties or relations exist yet." },
            new MenuDef { Id = MenuId.World, Title = "World", FunctionKey = 12, Blurb = "The world map" },
        };

        public static MenuDef Get(MenuId id) => id == MenuId.Notifications
            ? new MenuDef { Id = id, Title = "Messages", Blurb = "Everything that has happened lately" }
            : Primary.First(m => m.Id == id);

        public static MenuDef ByFunctionKey(int n) => Primary.FirstOrDefault(m => m.FunctionKey == n);

        /// <summary>Why this menu cannot be opened right now, or null when it can.</summary>
        public static string WhyLocked(MenuId id, World w) => id == MenuId.Notifications ? null : Get(id).Reason(w);
    }
}
