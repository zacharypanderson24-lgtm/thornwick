// The map overlays the player can switch on (one at a time) from the Overlay selector. Only overlays with real data behind them are listed; an
// overlay that cannot show anything in this world says why. Pure C#, tested directly.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum OverlayId { None, Underground, Water, Fertility, Sailing, Forest, Workers, Food, Trade, Military, Danger }

    public sealed class OverlayDef
    {
        public OverlayId Id; public string Title, Hint;
        public System.Func<World, string> LockedReason;
        public string Reason(World w) => LockedReason?.Invoke(w);
    }

    public static class OverlayRegistry
    {
        public static readonly IReadOnlyList<OverlayDef> All = new List<OverlayDef>
        {
            new OverlayDef { Id = OverlayId.Underground, Title = "Underground", Hint = "Ore under the hills (U also cycles the depth)" },
            new OverlayDef { Id = OverlayId.Water, Title = "Groundwater", Hint = "Groundwater patches, homes short of water and the roads water travels" },
            new OverlayDef { Id = OverlayId.Fertility, Title = "Fertile land", Hint = "Ground good for farming" },
            new OverlayDef { Id = OverlayId.Forest, Title = "Forest", Hint = "Mature trees, saplings, protected woodland (blue) and clearing orders (orange)" },
            new OverlayDef { Id = OverlayId.Sailing, Title = "Sailable water", Hint = "Where boats can go (green) and water too shallow for them (grey)",
                LockedReason = w => w.Waters == null ? "Sailable water: this world has no surface water." : null },
            new OverlayDef { Id = OverlayId.Workers, Title = "Workers", Hint = "Which work buildings are fully staffed (green), short (amber) or empty (red)" },
            new OverlayDef { Id = OverlayId.Food, Title = "Food sources", Hint = "Farms, hunting, fishing and stores, and Farmland waiting for a farmhouse" },
            new OverlayDef { Id = OverlayId.Trade, Title = "Trade", Hint = "Markets, ports and storehouses, and the roads caravans use" },
            new OverlayDef { Id = OverlayId.Military, Title = "Military reach", Hint = "Barracks, muster grounds and how far watchtowers see" },
            new OverlayDef { Id = OverlayId.Danger, Title = "Danger", Hint = "The edges raiders come from, and your walls and towers",
                LockedReason = w => !w.LiveCamps.Any() ? "Danger: no raider camps are known in this world." : null },
        };

        /// <summary>The overlay that helps place a tool: fertility for farms, groundwater for wells, ore for mines, sailable water for harbours. None when the tool has no need of one
        /// (or the overlay cannot show anything in this world, in which case the tool just works without it).</summary>
        public static OverlayId ForTool(string toolId, World w)
        {
            OverlayId id;
            switch (toolId)
            {
                case "farmhouse": case "zoneFarm": case "canal": id = OverlayId.Fertility; break;
                case "well": case "cistern": id = OverlayId.Water; break;
                case "mine": id = OverlayId.Underground; break;
                case "lodge": case "forester": case "clearLand": case "protectWood": case "unprotectWood": id = OverlayId.Forest; break;
                case "dock": case "fisher": case "port": case "ferry": case "ferryRoute": case "boatyard": id = OverlayId.Sailing; break;
                default: return OverlayId.None;
            }
            return w != null && WhyLocked(id, w) != null ? OverlayId.None : id;
        }

        public static OverlayDef Get(OverlayId id) => All.FirstOrDefault(o => o.Id == id);
        public static string WhyLocked(OverlayId id, World w) => id == OverlayId.None ? null : Get(id)?.Reason(w);
        public static string TitleOf(OverlayId id) => id == OverlayId.None ? "Off" : Get(id)?.Title ?? "Off";
    }
}
