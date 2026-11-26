// Quarry feedback and site survey. A quarry works the rock in its automatic working area (Defs.QuarryAutoR tiles around it) with no painted zone; painted
// Mining tiles in its reach are an optional boundary that widens the reach to WorkR and restricts it to those tiles (World.Extraction.cs).
// Hills and outcrops are dug down level by level into pits; a mountain is cut inward from an open edge (RockOpen) and stays standing.
using System;
using System.Collections.Generic;
using System.Text;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>How the quarry sees one tile: Dig = rock it can work now; Locked = mountain rock still inside the mountain (it opens up as the
        /// edge is cut); None = nothing for a quarry.</summary>
        public enum QuarryTile { None, Dig, Locked }

        /// <summary>The older, zone-only view of a tile (rock the player painted as Mining). Kept for the legacy feedback and for tests.</summary>
        public QuarryTile QuarryTileState(int x, int y)
        {
            if (!Inb(x, y) || ZoneMap[y, x] != Zone.Mine || DecoMap[y, x] != Deco.Rock || RockLeft(x, y) <= 0 || IsTunnel(x, y)) return QuarryTile.None;
            return RockOpen(x, y) ? QuarryTile.Dig : QuarryTile.Locked;
        }

        /// <summary>How THIS quarry sees a tile: rock inside its working area (automatic, or its painted boundary).</summary>
        public QuarryTile QuarryTileFor(Building b, int x, int y)
        {
            if (!Inb(x, y) || DecoMap[y, x] != Deco.Rock || RockLeft(x, y) <= 0 || IsTunnel(x, y) || !Claimed(x, y)) return QuarryTile.None;
            if (!InWorkArea(b, Zone.Mine, x, y)) return QuarryTile.None;
            return RockOpen(x, y) ? QuarryTile.Dig : QuarryTile.Locked;
        }

        /// <summary>Fraction of a rock tile's stone still in the ground (1 = untouched, 0 = dug out).</summary>
        public double StoneFraction(int x, int y)
        {
            double cap = RockCap(x, y);
            return cap <= 0 ? 0 : Math.Min(1, RockLeft(x, y) / cap);
        }

        public struct QuarryReport { public int Dig, Locked, Pits, Beyond; public double StoneLeft, LockedStone; }

        /// <summary>Count the tiles in a quarry's area: workable, locked inside a mountain, pits already dug, and (with no painted boundary) rock in reach
        /// that lies outside the automatic area.</summary>
        public QuarryReport QuarryReach(Building b)
        {
            var r = new QuarryReport();
            bool auto = !UsesZone(b, Zone.Mine);
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    if (!Inb(i, j)) continue;
                    if ((!auto || RectDist(b, i, j) <= AutoRadius(b)) && PitDepth(i, j) > 0) r.Pits++;
                    switch (QuarryTileFor(b, i, j))
                    {
                        case QuarryTile.Dig: r.Dig++; r.StoneLeft += RockLeft(i, j); break;
                        case QuarryTile.Locked: r.Locked++; r.LockedStone += RockLeft(i, j); break;
                        default:
                            if (auto && DecoMap[j, i] == Deco.Rock && RockLeft(i, j) > 0 && Claimed(i, j) && ZoneMap[j, i] == Zone.None && RectDist(b, i, j) > AutoRadius(b)) r.Beyond++;
                            break;
                    }
                }
            return r;
        }

        /// <summary>The tiles a quarry would work (for the map overlay): its working area, as Dig or Locked.</summary>
        public IEnumerable<(int x, int y, QuarryTile state)> QuarryArea(Building b)
        {
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    var s = QuarryTileFor(b, i, j);
                    if (s != QuarryTile.None) yield return (i, j, s);
                }
        }

        /// <summary>Stone a crew brings home a day on average: a trip carries Load["quarry"] and takes 2 x steps + WorkT + 2 days.</summary>
        public static double QuarryDailyEstimate(int workers, int steps = 4) => workers * Load["quarry"] / (double)(2 * steps + WorkT + 2);

        // ---------- site survey (placement preview, inspector, governors) ----------
        public class QuarrySite
        {
            public SiteLevel Level = SiteLevel.Valid;
            public double Reserve, LockedReserve;   // stone in the area you can work now / inside mountain rock that opens as the edge is cut
            public int DigTiles, LockedTiles, BeyondTiles, OverlappingQuarries;
            public double DailyEstimate, LifetimeYears;
            public readonly List<string> Notes = new List<string>();
        }

        public QuarrySite SurveyQuarrySite(int x, int y, int size = 2)
        {
            var s = new QuarrySite();
            var fp = new Building { T = "quarry", X = x, Y = y, Sz = size };
            var r = QuarryReach(fp);
            s.Reserve = r.StoneLeft; s.LockedReserve = r.LockedStone; s.DigTiles = r.Dig; s.LockedTiles = r.Locked; s.BeyondTiles = r.Beyond;
            foreach (var o in Blds) if (o.T == "quarry" && Math.Abs(o.X - x) <= 2 * QuarryAutoR && Math.Abs(o.Y - y) <= 2 * QuarryAutoR) s.OverlappingQuarries++;
            s.DailyEstimate = QuarryDailyEstimate(Jobs["quarry"]);
            s.LifetimeYears = s.DailyEstimate > 0 ? (s.Reserve + s.LockedReserve) / s.DailyEstimate / 365.0 : 0;
            if (r.Dig == 0 && r.Locked == 0)
            {
                s.Level = SiteLevel.Invalid;
                s.Notes.Add(r.Beyond > 0 ? $"No rock within {QuarryAutoR} tiles, though some lies farther out. Put the quarry nearer the rock." : $"No rock to quarry within {QuarryAutoR} tiles. Put the quarry beside hills, outcrops or a mountain edge.");
                return s;
            }
            if (r.Dig == 0) { s.Level = SiteLevel.Warning; s.Notes.Add("Only mountain rock inside the range: a quarry cuts in from an open edge, so it starts slowly. Put it where the mountain meets open ground."); }
            if (s.Reserve + s.LockedReserve < 150 || s.LifetimeYears < 1.5) { s.Level = SiteLevel.Warning; s.Notes.Add($"Little stone: about {s.Reserve + s.LockedReserve:0} in range, roughly {Math.Max(0.1, s.LifetimeYears):0.#} years at full staffing."); }
            if (s.OverlappingQuarries > 0) { s.Level = SiteLevel.Warning; s.Notes.Add($"{s.OverlappingQuarries} other quarr{(s.OverlappingQuarries == 1 ? "y" : "ies")} already work{(s.OverlappingQuarries == 1 ? "s" : "")} this rock: they draw from the same stone."); }
            if (!NextToRoad(fp)) { s.Level = SiteLevel.Warning; s.Notes.Add("No road beside it yet: a quarry only works with a road to the Town Hall."); }
            if (r.Beyond > 0) s.Notes.Add($"{r.Beyond} more rock tile{(r.Beyond == 1 ? "" : "s")} lie beyond its {QuarryAutoR}-tile area: paint a Mining boundary to reach them.");
            return s;
        }

        /// <summary>What a quarry is doing, in plain words, for the info panel.</summary>
        public string QuarryStatus(Building b)
        {
            var r = QuarryReach(b);
            var s = new StringBuilder();
            bool bounded = UsesZone(b, Zone.Mine);
            if (r.Dig > 0)
                s.Append($"Digging {r.Dig} rock tile{(r.Dig == 1 ? "" : "s")} in its area: about {r.StoneLeft:0} stone left" + (r.StoneLeft > 0 ? $", roughly {r.StoneLeft / Math.Max(0.01, QuarryDailyEstimate(Math.Max(1, b.Workers))) / 365.0:0.#} years at this staffing (an estimate)" : "") + ". Each tile is dug down a level at a time, so the pit is the progress.");
            else if (r.Locked > 0)
                s.Append("Nothing open to dig yet: the rock in range is inside a mountain. A quarry cuts in from an open edge, so build it where the mountain meets open ground.");
            else if (r.Pits > 0)
                s.Append("Worked out: all the rock in its area is dug. " + (r.Beyond > 0 ? $"{r.Beyond} rock tiles lie beyond it: paint a Mining boundary to reach them, or build another quarry." : "Build another quarry beside fresh rock."));
            else
                s.Append($"Nothing to quarry: no rock within {(bounded ? WorkR : QuarryAutoR)} tiles{(bounded ? " inside the painted boundary" : "")}.");
            if (r.Dig > 0 && r.Locked > 0)
                s.Append($"\n{r.Locked} mountain tile{(r.Locked == 1 ? "" : "s")} in range still inside the rock: they open up as the edge is cut.");
            if (r.Pits > 0) s.Append($"\n{r.Pits} pit tile{(r.Pits == 1 ? "" : "s")} dug so far (pits stay: nothing can be built on them).");
            s.Append(bounded ? $"\nWorking area: your painted Mining boundary (up to {WorkR} tiles)" : $"\nWorking area: automatic, {QuarryAutoR} tiles around it" + (r.Beyond > 0 ? $"; {r.Beyond} rock tiles lie beyond (paint a Mining boundary to widen it)" : ""));
            if (ResAmt[Res.Stone] >= Storage) s.Append("\nThe stone store is full: the crew waits until some is used.");
            return s.ToString();
        }
    }
}
