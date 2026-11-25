// Quarry feedback (no rule changes): what a quarry will dig, what it cannot reach yet, and what it has already dug.
// A quarry works rock tiles of the Mining zone within WorkR. Hills and outcrops are dug down level by level into pits; a mountain is
// cut inward from an open edge (RockOpen) and stays standing. These helpers only describe that, for the map overlay and the info panel.
using System;
using System.Text;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>How the quarry sees one tile: Dig = zoned rock it can work now; Locked = zoned mountain rock still inside the
        /// mountain (it opens up as the edge is cut); None = nothing for a quarry.</summary>
        public enum QuarryTile { None, Dig, Locked }

        public QuarryTile QuarryTileState(int x, int y)
        {
            if (!Inb(x, y) || ZoneMap[y, x] != Zone.Mine || DecoMap[y, x] != Deco.Rock || RockLeft(x, y) <= 0) return QuarryTile.None;
            return RockOpen(x, y) ? QuarryTile.Dig : QuarryTile.Locked;
        }

        /// <summary>Fraction of a rock tile's stone still in the ground (1 = untouched, 0 = dug out).</summary>
        public double StoneFraction(int x, int y)
        {
            double cap = RockCap(x, y);
            return cap <= 0 ? 0 : Math.Min(1, RockLeft(x, y) / cap);
        }

        public struct QuarryReport { public int Dig, Locked, Pits; public double StoneLeft; }

        /// <summary>Count the tiles in a quarry's reach: workable, locked inside a mountain, and pits already dug.</summary>
        public QuarryReport QuarryReach(Building b)
        {
            var r = new QuarryReport();
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    if (!Inb(i, j)) continue;
                    if (PitDepth(i, j) > 0) r.Pits++;
                    switch (QuarryTileState(i, j))
                    {
                        case QuarryTile.Dig: r.Dig++; r.StoneLeft += RockLeft(i, j); break;
                        case QuarryTile.Locked: r.Locked++; break;
                    }
                }
            return r;
        }

        /// <summary>What a quarry is doing, in plain words, for the info panel.</summary>
        public string QuarryStatus(Building b)
        {
            var r = QuarryReach(b);
            var s = new StringBuilder();
            if (r.Dig > 0)
                s.Append($"Digging {r.Dig} rock tile{(r.Dig == 1 ? "" : "s")} in reach: about {r.StoneLeft:0} stone left. Each tile is dug down a level at a time, so the pit is the progress.");
            else if (r.Locked > 0)
                s.Append("Nothing open to dig yet: the zoned rock is inside a mountain. A quarry cuts in from an open edge, so zone the mountain's edge too.");
            else if (r.Pits > 0)
                s.Append($"Worked out: every zoned rock in reach is dug. Zone more rock within {WorkR} tiles, or build another quarry beside fresh rock.");
            else
                s.Append($"Nothing to quarry: zone rock within {WorkR} tiles (Mining zone). Orange tiles on the map show what it will dig.");
            if (r.Dig > 0 && r.Locked > 0)
                s.Append($"\n{r.Locked} zoned mountain tile{(r.Locked == 1 ? "" : "s")} still inside the rock: they open up as the edge is cut.");
            if (r.Pits > 0) s.Append($"\n{r.Pits} pit tile{(r.Pits == 1 ? "" : "s")} dug so far (pits stay: nothing can be built on them).");
            if (ResAmt[Res.Stone] >= Storage) s.Append("\nThe stone store is full: the crew waits until some is used.");
            return s.ToString();
        }
    }
}
