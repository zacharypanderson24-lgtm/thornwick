// The map overlays the player can switch on (one at a time) from the Overlay selector. Only overlays with real data behind them are listed; an
// overlay that cannot show anything in this world says why. Pure C#, tested directly.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum OverlayId { None, Underground, Water, Fertility, Sailing }

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
            new OverlayDef { Id = OverlayId.Sailing, Title = "Sailable water", Hint = "Where boats can go (green) and water too shallow for them (grey)",
                LockedReason = w => w.Waters == null ? "Sailable water: this world has no surface water." : null },
        };

        public static OverlayDef Get(OverlayId id) => All.FirstOrDefault(o => o.Id == id);
        public static string WhyLocked(OverlayId id, World w) => id == OverlayId.None ? null : Get(id)?.Reason(w);
        public static string TitleOf(OverlayId id) => id == OverlayId.None ? "Off" : Get(id)?.Title ?? "Off";
    }
}
