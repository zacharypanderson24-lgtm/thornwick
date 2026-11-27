// Districts: the shared framework for organic settlement development (DESIGN_ORGANIC_DEVELOPMENT.md, phase 1).
// A district is a connected patch of one kind of economic zone with a stable identity, a name and the player's development settings.
// Pure data; the rules that build, merge, name and save them are in World.Districts.cs.
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    /// <summary>The seven kinds of ordinary economic district. Commercial and industrial are deliberately not here yet (the design leaves room).</summary>
    public enum DistrictType : byte { Residential, Agricultural, Forestry, Mining, Quarrying, Hunting, Fishing }

    /// <summary>How much the settlement wants this district to grow. Separate from the operational workforce priority: Disabled stops new
    /// buildings only, never the work of buildings that stand.</summary>
    public enum DevPriority : byte { Disabled, Low, Normal, High, Critical }

    /// <summary>Automatic: the district builds on its own. ApprovalRequired: it proposes and you approve or reject. Paused: no new projects.</summary>
    public enum DevMode : byte { Automatic, ApprovalRequired, Paused }

    /// <summary>Optional advanced restrictions. Zero means "no limit". A player can zone and never open these.</summary>
    public class DistrictLimits
    {
        public int MaxBuildings;          // most buildings the district may hold (manual ones count)
        public int MaxEmployment;         // most jobs the district may offer
        public int MaxSpend;              // most construction spend per in-game year (wood + stone + gold units); phase 6 enforces it
        public int MinWood, MinStone;     // construction reserve this district must leave in the store
        public int ProductionTarget;      // stop expanding once the district makes this much per day (units depend on the zone: see World.DistrictOutputUnit)
        public ForestMode HarvestMode;    // forestry: the mode new lodges start in (Sustainable by default; Intensive on request; Permanent clearing needs its own explicit designation)
        public bool Any => HarvestMode != ForestMode.Sustainable || MaxBuildings > 0 || MaxEmployment > 0 || MaxSpend > 0 || MinWood > 0 || MinStone > 0 || ProductionTarget > 0;
        public DistrictLimits Copy() => (DistrictLimits)MemberwiseClone();
    }

    public class District
    {
        // ---- identity and settings (saved) ----
        public int Id;
        public DistrictType Type;
        public string Name;
        public bool CustomName;           // the player renamed it: it keeps its name through growth
        public DevPriority Priority = DevPriority.Normal;
        public DevMode Mode = DevMode.Automatic;
        public DistrictLimits Limits = new DistrictLimits();
        public int Founded;               // day the district first appeared
        public readonly List<string> History = new List<string>();   // last few events: created, merged, split, renamed (kept short)

        // ---- derived each rebuild (never saved) ----
        public readonly List<int> Tiles = new List<int>();            // tile keys (y * N + x), ascending
        public int MinX, MinY, MaxX, MaxY;
        public double CenterX, CenterY;
        public readonly List<Building> Buildings = new List<Building>();   // buildings of this district's kind standing on its tiles (manual ones included)
        public int OpenTiles;             // tiles with nothing built on them
        public int Jobs => Buildings.Sum(b => b.Jobs);          // always current: read from the buildings (workers change every day without a rebuild)
        public int Workers => Buildings.Sum(b => b.Workers);
        public string Note = "";          // why development is or is not happening (written by the engine from phase 2 on)
        public readonly List<(int day, int amount)> SpendLog = new List<(int, int)>();   // construction funded here (saved), for the yearly spending limit

        public int Area => Tiles.Count;
        /// <summary>Development may start new buildings here: not Disabled, not Paused.</summary>
        public bool DevelopmentOn => Priority != DevPriority.Disabled && Mode != DevMode.Paused;
    }
}
