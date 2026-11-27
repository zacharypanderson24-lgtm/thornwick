// Automatic working areas for extraction buildings (lodge, quarry, mine). DESIGN_RESOURCE_EXTRACTION.md.
// The rule: a building works everything within WorkR of it that nature put there. Painted Lumber / Mining zones are only an OPTIONAL restriction:
// if the building has painted tiles of its own kind inside its reach it works only those (old saves, or a player boxing a building in).
// Everything here answers questions with data (no UI), so the player's tools, the map overlay and a future governor all use the same calls.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        // ---------- The working area ----------
        readonly Dictionary<(int, int, int, Zone), bool> _usesZone = new Dictionary<(int, int, int, Zone), bool>();   // keyed by footprint, so a planned (not placed) building can ask too
        int _usesZoneVersion = -1;

        /// <summary>Is the tile inside the square reach of the building (WorkR tiles around its footprint)?</summary>
        public static bool InReach(Building b, int x, int y) =>
            x >= b.X - WorkR && x <= b.X + b.Sz - 1 + WorkR && y >= b.Y - WorkR && y <= b.Y + b.Sz - 1 + WorkR;

        /// <summary>Has the player painted tiles of this zone inside the building's reach? Then the building works only those tiles
        /// (an optional boundary); with none painted its whole reach is open.</summary>
        public bool UsesZone(Building b, Zone zone)
        {
            if (_usesZoneVersion != Version) { _usesZone.Clear(); _usesZoneVersion = Version; }
            if (b.Organic && ((zone == Zone.Wood && b.T == "wood") || (zone == Zone.Mine && (b.T == "quarry" || b.T == "mine")))) return false;   // a district building follows its district's rules, never the painted-boundary ones
            if (_usesZone.TryGetValue((b.X, b.Y, b.Sz, zone), out bool known)) return known;
            bool any = false;
            for (int j = Math.Max(0, b.Y - WorkR); j <= Math.Min(N - 1, b.Y + b.Sz - 1 + WorkR) && !any; j++)
                for (int i = Math.Max(0, b.X - WorkR); i <= Math.Min(N - 1, b.X + b.Sz - 1 + WorkR); i++)
                    if (ZoneMap[j, i] == zone) { any = true; break; }
            _usesZone[(b.X, b.Y, b.Sz, zone)] = any;
            return any;
        }

        /// <summary>May this building work the tile? In reach, and either painted with the building's zone (when it has an optional boundary)
        /// or, with no boundary, free of any other land-use zone (farms, housing and so on keep their ground).</summary>
        public bool InWorkArea(Building b, Zone zone, int x, int y)
        {
            if (!Inb(x, y) || !InReach(b, x, y)) return false;
            if (b.Organic && zone == Zone.Wood && b.T == "wood") return ZoneMap[y, x] == Zone.Wood;   // its permitted area: the Forestry district tiles in reach
            if (zone == Zone.Mine && b.T == "quarry" && ProtectedWood.Contains(Tk(x, y))) return false;   // protected land is never quarried
            if (b.Organic && zone == Zone.Mine && b.T == "quarry") return ZoneMap[y, x] == Zone.Quarry;   // a district quarry digs only its Quarrying district
            if (UsesZone(b, zone)) return ZoneMap[y, x] == zone;
            return ZoneMap[y, x] == Zone.None && RectDist(b, x, y) <= AutoRadius(b);
        }

        /// <summary>How far the automatic area reaches. A lodge and a mine use their whole reach; a quarry starts with a smaller area (QuarryAutoR) so it does not
        /// slowly dig out every hill in reach: painting a Mining boundary widens it to the full reach.</summary>
        public static int AutoRadius(Building b) => b.T == "quarry" ? QuarryAutoR : WorkR;

        // ---------- Mine site survey ----------
        public enum SiteLevel { Valid, Warning, Invalid }

        /// <summary>What a mine placed at a spot would find: used by the placement preview, the inspector and (later) governors.
        /// Reserves are the real figures under the ground; they are an estimate for the player (nothing is guaranteed: miners may not reach it all).</summary>
        public class MineSite
        {
            public SiteLevel Level = SiteLevel.Valid;
            public double Reserve;                 // ore still in the ground in reach
            public double RichestBand;             // richest band in reach (1..3)
            public int OreTiles;                   // tiles in reach with ore
            public int OverlappingMines;           // other mines whose reach overlaps this one
            public double DailyEstimate;           // ore a day with full staffing (an estimate)
            public double LifetimeYears;           // reserve / estimate, in game years (365 days)
            public readonly List<string> Notes = new List<string>();
            public string Headline => Level == SiteLevel.Invalid ? "Cannot build here" : Level == SiteLevel.Warning ? "Poor site" : "Good site";
        }

        /// <summary>Ore a fully staffed mine brings up per day, on average: a miner's trip carries MineLoad of its band and takes TripBase + band + 2 per tile.</summary>
        public static double MineDailyEstimate(int workers, double band = 2, int steps = 3)
        {
            int b = Math.Max(1, Math.Min(Bands, (int)Math.Round(band)));
            return workers * MineLoad[b] / (double)(TripBase + b + 2 * steps);
        }

        public MineSite SurveyMineSite(int x, int y, int size = 2)
        {
            var s = new MineSite();
            var fp = new Building { T = "mine", X = x, Y = y, Sz = size, Organic = OrganicHere("mine", x, y) };
            double byBand1 = 0, byBand2 = 0, byBand3 = 0;
            for (int j = y - WorkR; j <= y + size - 1 + WorkR; j++)
                for (int i = x - WorkR; i <= x + size - 1 + WorkR; i++)
                {
                    if (!Inb(i, j) || !Claimed(i, j)) continue;
                    bool any = false;
                    for (int l = 1; l <= Bands; l++)
                    {
                        double o = OreAt(i, j, l); if (o <= 1e-9) continue;
                        any = true; s.Reserve += o;
                        if (l == 1) byBand1 += o; else if (l == 2) byBand2 += o; else byBand3 += o;
                    }
                    if (any) s.OreTiles++;
                }
            s.RichestBand = byBand3 > 0 ? 3 : byBand2 > 0 ? 2 : byBand1 > 0 ? 1 : 0;
            foreach (var o in Blds) if (o.T == "mine" && Math.Abs(o.X - x) <= 2 * WorkR && Math.Abs(o.Y - y) <= 2 * WorkR) s.OverlappingMines++;
            s.DailyEstimate = MineDailyEstimate(Defs.Jobs["mine"], s.RichestBand == 0 ? 1 : s.RichestBand);
            s.LifetimeYears = s.DailyEstimate > 0 ? s.Reserve / s.DailyEstimate / 365.0 : 0;
            if (s.Reserve <= 1e-9)
            {
                s.Level = SiteLevel.Invalid;
                s.Notes.Add($"No ore within {WorkR} tiles. Ore lies in hills and mountains: put the mine at the foot of one.");
                return s;
            }
            if (s.OverlappingMines > 0) { s.Level = SiteLevel.Warning; s.Notes.Add($"{s.OverlappingMines} other mine{(s.OverlappingMines == 1 ? "" : "s")} already reach{(s.OverlappingMines == 1 ? "es" : "")} this ground; they draw from the same ore, so it lasts less time for each."); }
            if (s.Reserve < 120 || s.LifetimeYears < 1.5) { s.Level = s.Level == SiteLevel.Invalid ? s.Level : SiteLevel.Warning; s.Notes.Add($"Limited ore: about {s.Reserve:0} in reach, enough for roughly {Math.Max(0.1, s.LifetimeYears):0.#} years at full staffing."); }
            if (MineFaceAt(x, y, size) < 0) { s.Level = SiteLevel.Warning; s.Notes.Add("No hill beside this spot, so its tunnel has little high ground to dig into."); }
            if (!NextToRoad(fp)) { s.Level = SiteLevel.Warning; s.Notes.Add("No road beside it yet: a mine only works with a road from its entrance to the Town Hall."); }
            return s;
        }

        /// <summary>Ore still in the ground that this mine's tunnel could reach (its working area plus its own tunnel).</summary>
        public double MineReserve(Building b)
        {
            double r = 0;
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    if (!Inb(i, j) || !Claimed(i, j)) continue;
                    if (!InMineArea(b, i, j) && !IsTunnel(i, j) && !TunnelBeside(b, i, j)) continue;
                    for (int l = 1; l <= Bands; l++) r += OreAt(i, j, l);
                }
            return r;
        }

        bool TunnelBeside(Building b, int x, int y)
        {
            foreach (int t in b.Tunnel) if (Math.Abs(t % N - x) <= WallR && Math.Abs(t / N - y) <= WallR) return true;
            return false;
        }

        /// <summary>Years the reserve would last at what the mine's crew brings up now (an estimate; null when the mine is idle or has nothing).</summary>
        public double? MineLifetimeYears(Building b)
        {
            double per = MineDailyEstimate(Math.Max(1, b.Workers));
            double res = MineReserve(b);
            return b.Workers > 0 && per > 0 && res > 0 ? res / per / 365.0 : (double?)null;
        }

        /// <summary>Monthly: tell the player when a mine's ore is running low or out, so the stop is never a surprise.</summary>
        void CheckMineReserve(Building b)
        {
            if (b.Workers <= 0 && b.Tunnel.Count == 0) return;
            double res = MineReserve(b);
            double per = MineDailyEstimate(Math.Max(1, b.Jobs));
            double years = per > 0 ? res / per / 365.0 : 0;
            int stage = res <= 1e-9 && TookOreInReach(b) ? 2 : (res < 60 || years < 1) && TookOreInReach(b) ? 1 : 0;
            if (stage == b.ReserveStage) return;
            if (stage > b.ReserveStage)
            {
                if (stage == 2) Events.Add($"The {b.Name} at {b.X},{b.Y} has run out of ore within reach. Build another mine at a new deposit, or demolish this one.");
                else Events.Add($"The {b.Name} at {b.X},{b.Y} is running low: about {res:0} ore left within reach, roughly {Math.Max(0.1, years):0.#} years at full staffing. Look for another deposit.");
            }
            b.ReserveStage = stage;
        }
    }
}
