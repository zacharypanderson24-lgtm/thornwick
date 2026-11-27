// Forestry: a woodcutter lodge manages the forest around it by itself (World.Extraction.cs gives it its working area). DESIGN_RESOURCE_EXTRACTION.md.
//   Sustainable (default)  fells mature trees, replants every felled tile once its stump has rotted, and always leaves SustainFloor trees standing in its area.
//   Intensive              fells without a floor and does not replant: the most logs now, a bare area later.
//   Clearing               works only land the player designated for clearing (ClearOrders), never replants, recovers the logs.
// A painted Lumber zone is an optional boundary AND an explicit order to fell those trees: such a lodge keeps the old rules (no floor, no replanting).
// Protected woodland is skipped by every crew and is only cleared on explicit confirmation. Everything here is data in and data out (no UI).
using System;
using System.Collections.Generic;
using System.Text;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum ForestMode : byte { Sustainable, Intensive, Clearing }

    public partial class World
    {
        /// <summary>A sustainable lodge leaves at least this many mature trees standing in its area.</summary>
        public const int SustainFloor = 10;
        /// <summary>Logs one tree gives: ChopsPerTree chops of the lodge's load.</summary>
        public static int LogsPerTree => ChopsPerTree * Load["wood"];
        /// <summary>Planted saplings only grow on growing days (not winter) and slower on dry ground: about this many calendar days to a mature tree on moist ground.</summary>
        public const int ForesterAutoR = 6;

        public readonly HashSet<int> ProtectedWood = new HashSet<int>();   // tiles the player protected (saved)
        public readonly HashSet<int> Replant = new HashSet<int>();         // felled tiles a sustainable lodge will replant when the stump has rotted (saved)
        public readonly HashSet<int> ClearOrders = new HashSet<int>();     // tiles designated for permanent clearing (saved)

        public bool IsProtected(int x, int y) => Inb(x, y) && ProtectedWood.Contains(Tk(x, y));
        public bool IsClearing(int x, int y) => Inb(x, y) && ClearOrders.Contains(Tk(x, y));

        // ---------- operating mode ----------
        public Result SetForestMode(Building b, ForestMode mode)
        {
            if (b == null || b.T != "wood") return Result.No("Only a woodcutter lodge has an operating mode");
            b.Mode = mode; Touch(); Recalc();   // the jobs it needs depend on the mode
            return Result.Yes();
        }

        public static string ModeName(ForestMode m) => m == ForestMode.Sustainable ? "Sustainable" : m == ForestMode.Intensive ? "Intensive" : "Land clearing";
        public static string ModeBlurb(ForestMode m) => m == ForestMode.Sustainable ? "fells mature trees, replants, and keeps at least " + SustainFloor + " standing"
            : m == ForestMode.Intensive ? "fells as fast as it can and does not replant: it will strip its area" : "clears only the land you designate, and does not replant";

        // ---------- protection and clearing ----------
        public Result ProtectWoodland(int x, int y)
        {
            if (!Inb(x, y)) return Result.No("Outside the map");
            if (!Claimed(x, y)) return Result.No(NotYours);
            if (Grid[y, x] != null) return Result.No("That tile is built on");
            if (ProtectedWood.Contains(Tk(x, y))) return Result.Yes();
            var d = DecoMap[y, x];
            if (d != Deco.Tree && d != Deco.Sapling && d != Deco.Stump && (d != Deco.None || IsSolid(x, y))) return Result.No("Only woodland or open ground can be protected");
            ProtectedWood.Add(Tk(x, y)); ClearOrders.Remove(Tk(x, y)); Replant.Remove(Tk(x, y)); Touch();
            return Result.Yes();
        }

        public int ProtectRect(int ax, int ay, int bx, int by, bool protect = true)
        {
            int n = 0;
            for (int y = Math.Min(ay, by); y <= Math.Max(ay, by); y++)
                for (int x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++)
                {
                    if (!Inb(x, y)) continue;
                    if (protect) { if (ProtectWoodland(x, y).Ok && Grid[y, x] == null) n++; }
                    else if (ProtectedWood.Remove(Tk(x, y))) { n++; Touch(); }
                }
            return n;
        }

        /// <summary>Designate a tile of woodland for permanent clearing. Protected woodland needs confirmProtected (which lifts the protection).</summary>
        public Result DesignateClearing(int x, int y, bool confirmProtected = false)
        {
            if (!Inb(x, y)) return Result.No("Outside the map");
            if (!Claimed(x, y)) return Result.No(NotYours);
            if (Grid[y, x] != null) return Result.No("That tile is built on");
            if (WhyNotClearable(x, y) is string why) return Result.No(why);
            if (ProtectedWood.Contains(Tk(x, y)))
            {
                if (!confirmProtected) return Result.No("This woodland is protected. Confirm to lift the protection and clear it for good");
                ProtectedWood.Remove(Tk(x, y));
            }
            Replant.Remove(Tk(x, y)); ClearOrders.Add(Tk(x, y)); Touch();
            return Result.Yes();
        }

        public bool CancelClearing(int x, int y) { if (!Inb(x, y) || !ClearOrders.Remove(Tk(x, y))) return false; Touch(); return true; }

        // ---------- the forest around a lodge ----------
        public struct ForestStats { public int Mature, Saplings, Stumps, ProtectedTrees, Designated, OpenLand; }

        readonly Dictionary<(int, int, int), ForestStats> _forestStats = new Dictionary<(int, int, int), ForestStats>();
        int _forestStatsVersion = -1;

        /// <summary>Trees, saplings, protected trees and clearing orders in the lodge's reach. Cached until the world changes.</summary>
        public ForestStats ForestAround(Building b)
        {
            if (_forestStatsVersion != Version) { _forestStats.Clear(); _forestStatsVersion = Version; }
            var key = (b.X, b.Y, b.Sz);
            if (_forestStats.TryGetValue(key, out var cached)) return cached;
            var s = new ForestStats();
            bool bounded = UsesZone(b, Zone.Wood);
            for (int j = Math.Max(0, b.Y - WorkR); j <= Math.Min(N - 1, b.Y + b.Sz - 1 + WorkR); j++)
                for (int i = Math.Max(0, b.X - WorkR); i <= Math.Min(N - 1, b.X + b.Sz - 1 + WorkR); i++)
                {
                    if (!Claimed(i, j)) continue;
                    var d = DecoMap[j, i]; int k = Tk(i, j);
                    if (d == Deco.Tree)
                    {
                        if (ProtectedWood.Contains(k)) { s.ProtectedTrees++; continue; }
                        if (ClearOrders.Contains(k)) { s.Designated++; if (b.Mode != ForestMode.Clearing) continue; }   // designated trees are clearing work, not harvest
                        if (b.Mode == ForestMode.Clearing ? ClearOrders.Contains(k) : InWorkArea(b, Zone.Wood, i, j)) s.Mature++;
                    }
                    else if (d == Deco.Sapling) s.Saplings++;
                    else if (d == Deco.Stump) s.Stumps++;
                    else if (d == Deco.None && Grid[j, i] == null && !IsSolid(i, j) && ZoneMap[j, i] == Zone.None) s.OpenLand++;
                }
            _forestStats[key] = s;
            return s;
        }

        /// <summary>May the lodge's crews cut right now? A painted Lumber boundary is an explicit order (always); Intensive always; Clearing needs designated trees;
        /// Sustainable needs more mature trees than the standing floor.</summary>
        public bool MayHarvest(Building b)
        {
            if (b.T != "wood") return true;
            if (b.Mode == ForestMode.Clearing) return ForestAround(b).Mature > 0;
            if (b.Mode == ForestMode.Intensive || UsesZone(b, Zone.Wood)) return true;
            return ForestAround(b).Mature > SustainFloor;
        }

        /// <summary>Is this tile's tree reachable for the building's crew (area rules, protection, mode)?</summary>
        bool TreeWorkable(Building b, int x, int y)
        {
            int k = Tk(x, y);
            if (ProtectedWood.Contains(k)) return false;
            if (b.T == "wood" && b.Mode == ForestMode.Clearing) return ClearOrders.Contains(k) && InReach(b, x, y);
            if (ClearOrders.Contains(k) && b.T == "wood") return false;   // a tree designated for clearing is the clearing lodges' work, not a harvest
            return InWorkArea(b, Zone.Wood, x, y);
        }

        // ---------- growth ----------
        /// <summary>How likely a sapling is to grow today: none in winter, and less on dry ground (1 on a dry-world map, where moisture is not modelled).</summary>
        public double SaplingGrowthRate(int x, int y)
        {
            if (YearDay > 3 * SeasonLength) return 0;
            return Math.Clamp(0.5 + 0.5 * MoistureYield(x, y), 0.5, 1.0);
        }

        bool Replantable(int x, int y) =>
            Inb(x, y) && Grid[y, x] == null && DecoMap[y, x] == Deco.None && !IsSolid(x, y) && TileSpread(x, y) < 2 && !ClearOrders.Contains(Tk(x, y)) && !ProtectedWood.Contains(Tk(x, y))
            && (ZoneMap[y, x] == Zone.None || ZoneMap[y, x] == Zone.Wood) && PenAt(x, y) == null && WaterBlock(x, y, false) == null;

        // ---------- site survey (placement preview, inspector, governors) ----------
        public class LodgeSite
        {
            public SiteLevel Level = SiteLevel.Valid;
            public int Mature, Saplings, ProtectedTrees, OverlappingLodges, OpenLand;
            public double ShortTermPerDay, SustainablePerDay, StockLogs;
            public readonly List<string> Notes = new List<string>();
        }

        /// <summary>Logs a lodge's crew brings home a day while it cuts: a trip carries Load["wood"] and takes 2 x steps + WorkT + 1 days.</summary>
        public static double LodgeDailyEstimate(int workers, int steps = 4) => workers * Load["wood"] / (double)(2 * steps + WorkT + 1);

        /// <summary>Logs a day a forest of this many trees (growing and mature) can give for ever: each tree is worth LogsPerTree and takes about
        /// SaplingDays growing days (three quarters of a year) to come back.</summary>
        public static double SustainableLogsPerDay(int trees) => trees * LogsPerTree / (SaplingDays / 0.75 / 0.9);

        public LodgeSite SurveyLodgeSite(int x, int y, int size = 2)
        {
            var fp = new Building { T = "wood", X = x, Y = y, Sz = size, Organic = OrganicHere("wood", x, y) };
            var f = ForestAround(fp);
            var s = new LodgeSite { Mature = f.Mature, Saplings = f.Saplings, ProtectedTrees = f.ProtectedTrees, OpenLand = f.OpenLand };
            foreach (var o in Blds) if (o.T == "wood" && Math.Abs(o.X - x) <= 2 * WorkR && Math.Abs(o.Y - y) <= 2 * WorkR) s.OverlappingLodges++;
            s.StockLogs = f.Mature * (double)LogsPerTree;
            s.ShortTermPerDay = f.Mature > 0 ? LodgeDailyEstimate(Jobs["wood"]) : 0;
            s.SustainablePerDay = SustainableLogsPerDay(f.Mature + f.Saplings);
            if (f.Mature == 0) { s.Level = SiteLevel.Warning; s.Notes.Add(f.ProtectedTrees > 0 ? $"Every tree in reach is protected ({f.ProtectedTrees}). Lodges skip protected woodland." : $"No trees within {WorkR} tiles on your land. A lodge needs forest in reach."); }
            else if (f.Mature <= SustainFloor) { s.Level = SiteLevel.Warning; s.Notes.Add($"Very few mature trees ({f.Mature}). A sustainable lodge leaves {SustainFloor} standing, so it would barely work."); }
            if (f.ProtectedTrees > 0 && f.Mature > 0) s.Notes.Add($"{f.ProtectedTrees} protected tree{(f.ProtectedTrees == 1 ? "" : "s")} in reach will not be cut.");
            if (s.OverlappingLodges > 0) { s.Level = SiteLevel.Warning; s.Notes.Add($"{s.OverlappingLodges} other lodge{(s.OverlappingLodges == 1 ? "" : "s")} already work{(s.OverlappingLodges == 1 ? "s" : "")} this forest: they share its trees."); }
            if (f.Mature > 0 && f.OpenLand < 6 && f.Stumps < 6) s.Notes.Add("Little open ground to replant: felled trees will not all come back.");
            if (!NextToRoad(fp)) { s.Level = SiteLevel.Warning; s.Notes.Add("No road beside it yet: a lodge only works with a road to the Town Hall."); }
            return s;
        }

        // ---------- the lodge's inspector text ----------
        public string LodgeStatus(Building b)
        {
            var f = ForestAround(b);
            var sb = new StringBuilder();
            sb.Append($"Mode: {ModeName(b.Mode)} ({ModeBlurb(b.Mode)})");
            if (UsesZone(b, Zone.Wood)) sb.Append("\nWorking area: your painted Lumber boundary (those trees are ordered felled, with no floor and no replanting)");
            else if (b.Mode != ForestMode.Clearing) sb.Append($"\nWorking area: automatic, {WorkR} tiles around it");
            sb.Append($"\nForest in reach: {f.Mature} mature trees, {f.Saplings} saplings growing" + (f.ProtectedTrees > 0 ? $", {f.ProtectedTrees} protected (skipped)" : ""));
            double sustain = SustainableLogsPerDay(f.Mature + f.Saplings), now = LodgeDailyEstimate(Math.Max(1, b.Workers));
            if (b.Mode == ForestMode.Clearing)
                sb.Append(f.Mature > 0 ? $"\nClearing {f.Mature} designated tree{(f.Mature == 1 ? "" : "s")}; the logs are kept" : "\nNothing designated: mark woodland for clearing (Land clearing tool) and the lodge will fell it");
            else
            {
                sb.Append($"\nSustainable output about {sustain:0.#} logs a day for ever; this crew cuts about {now:0.#} a day while it works");
                if (b.Mode == ForestMode.Sustainable && !UsesZone(b, Zone.Wood))
                {
                    if (f.Mature <= SustainFloor) sb.Append($"\nLimited sustainable production: only {f.Mature} mature trees in reach and the lodge leaves {SustainFloor} standing. Recommended: build another lodge by a bigger forest, plant more with a forester, or use less timber while the saplings grow");
                    else if (now > sustain * 1.15) sb.Append($"\nCutting faster than the forest regrows ({now:0.#} against {sustain:0.#} a day): the stock will fall to the floor in about {(f.Mature - SustainFloor) * (double)LogsPerTree / Math.Max(0.01, now - sustain) / 360:0.#} years. Recommended: build another lodge by a bigger forest or plant more with a forester");
                }
                else if (b.Mode == ForestMode.Intensive)
                    sb.Append($"\nIntensive: at this rate the {f.Mature} mature trees last about {f.Mature * (double)LogsPerTree / Math.Max(0.01, now) / 360:0.#} years and then the area is bare (nothing is replanted)");
            }
            if (f.Mature == 0 && b.Mode != ForestMode.Clearing) sb.Append("\nNo mature tree it may cut: " + (f.ProtectedTrees > 0 ? "all in reach are protected" : f.Saplings > 0 ? "the saplings are still growing" : "no forest in reach"));
            return sb.ToString();
        }
    }
}
