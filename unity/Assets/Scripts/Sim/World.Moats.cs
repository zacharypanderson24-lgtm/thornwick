// Moats and longship landings (DESIGN_WATER.md section 24, DESIGN_MILITARY.md sections 24 and 25 first steps).
//
// MOAT: a water-filled ditch dug from fresh water (a river, lake or pond, or from another moat). Like deep water it is a wall to everyone,
// raiders included, except where a road crosses it: a road laid across a moat is a causeway (the one way over, and so the place to defend).
// A moat is a set of land tiles (saved as a list, like canals). Buildings and zones keep off it.
//
// LONGSHIPS: a town on the coast, or on a big river that runs off the map, can be raided from the water. Some raids arrive by longship and land
// on a shore near the town instead of walking in from the map's edge.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int MoatMaxLength = 24, MoatWood = 3, LandingMaxFromHall = 100, LandingMinFromHall = 10;
        public const double LongshipChance = 0.4;

        /// <summary>Dug moat tiles (y * N + x). Saved.</summary>
        public readonly HashSet<int> Moats = new HashSet<int>();

        public bool IsMoat(int x, int y) => Moats.Count > 0 && Inb(x, y) && Moats.Contains(Tk(x, y));

        /// <summary>A moat tile with no road across it: a wall to walkers and raiders alike.</summary>
        bool MoatBlocks(int x, int y) => Moats.Count > 0 && Moats.Contains(Tk(x, y)) && Grid[y, x]?.T != "road";

        public string MoatBlock(int x, int y) => IsMoat(x, y) ? "A moat is in the way. Roads cross a moat on a causeway: lay a road over it, or remove the moat first" : null;

        public Result MoatPathCheck(IList<(int x, int y)> path)
        {
            if (Waters == null) return Result.No("A moat needs fresh water to fill it: there is no water on this map");
            if (path.Count == 0) return Result.No("Drag a moat out from fresh water");
            if (path.Count > MoatMaxLength) return Result.No($"Too long: a moat runs at most {MoatMaxLength} tiles in one go (dig the next stretch from its end)");
            var (fx, fy) = path[0]; bool joined = false;
            for (int d = 0; d < 4 && !joined; d++)
            {
                int nx = fx + Nb[d, 0], ny = fy + Nb[d, 1]; if (!Inb(nx, ny)) continue;
                if (FreshOpen(Waters, ny * N + nx) || IsMoat(nx, ny)) joined = true;
            }
            if (!joined) return Result.No("A moat must start at fresh water (a river, lake or pond) or at the end of a moat that does: water fills it from there");
            int n = 0;
            foreach (var (x, y) in path)
            {
                if (!Inb(x, y)) return Result.No("Outside the map");
                if (!Claimed(x, y)) return Result.No(NotYours);
                if (IsMoat(x, y)) continue;
                if (Waters.IsWater(x, y)) return Result.No("The moat reaches the water: start it on the bank and dig away from the water");
                if (Grid[y, x] != null && Grid[y, x].T == "road") continue;   // a causeway: the moat is left open where a road already crosses
                if (Grid[y, x] != null) return Result.No("A building is in the way");
                if (PenAt(x, y) != null) return Result.No("A livestock pen is in the way");
                if (HasTree(x, y)) return Result.No(TreeMsg);
                if (IsSolid(x, y)) return Result.No("Rock, a pit or steep ground is in the way of the moat");
                if (TileSpread(x, y) > 1) return Result.No("The ground is too steep: a moat must be dug on gently sloping land or water would run out of it");
                if (IsCanal(x, y)) return Result.No("An irrigation canal is in the way");
                n++;
            }
            if (n == 0) return Result.Yes(0);
            if (ResAmt[Res.Wood] < n * MoatWood) return Result.No($"Not enough wood: {n} moat tiles need {n * MoatWood} wood", n);
            return Result.Yes(n);
        }

        public Result PlaceMoatPath(IList<(int x, int y)> path)
        {
            var c = MoatPathCheck(path);
            if (!c.Ok || c.Count == 0) return c;
            ResAmt[Res.Wood] -= c.Count * MoatWood;
            foreach (var (x, y) in path) Moats.Add(Tk(x, y));   // a causeway tile is part of the moat too
            Touch(); Recalc();
            return c;
        }

        public bool RemoveMoat(int x, int y)
        {
            if (!IsMoat(x, y)) return false;
            Moats.Remove(Tk(x, y)); ResAmt[Res.Wood] += MoatWood / 2.0; Touch(); Recalc(); return true;
        }

        // ---------- longships ----------
        /// <summary>A shore where a longship can land: dry ground beside boat-deep water that is part of a body running off the map (the sea or a
        /// big river), 10 to 100 tiles from the Town Hall, the nearest to it. Null for a town with no such water.</summary>
        public (int x, int y, int ship_x, int ship_y)? LongshipLanding()
        {
            if (Waters == null || Hall == null) return null;
            (int, int, int, int)? best = null; int bestD = int.MaxValue;
            int hx = Hall.X + 1, hy = Hall.Y + 1;
            for (int y = Math.Max(2, hy - LandingMaxFromHall); y <= Math.Min(N - 3, hy + LandingMaxFromHall); y++)
                for (int x = Math.Max(2, hx - LandingMaxFromHall); x <= Math.Min(N - 3, hx + LandingMaxFromHall); x++)
                {
                    int d = Math.Max(Math.Abs(x - hx), Math.Abs(y - hy)); if (d < LandingMinFromHall || d >= bestD) continue;
                    if (Waters.Class[y * N + x] != WaterClass.None || IsSolid(x, y) || Grid[y, x] != null) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        // the beach: any open water beside the tile (the shallows, a longship is beached); the ship itself lies in the nearest boat-deep water
                        int wx = x + Nb[k, 0], wy = y + Nb[k, 1]; if (!Inb(wx, wy) || !Waters.IsWater(wx, wy)) continue;
                        var body = Waters.BodyAt(wx, wy); if (body == null || !body.TouchesEdge || body.Tiles < PortWaterNeeded) continue;
                        if (!SurfaceWater.Salty(Waters.Class[wy * N + wx]) && body.Kind != WaterClass.MajorRiver) continue;   // the sea or a big river
                        int sx = wx, sy = wy, sd = int.MaxValue;
                        for (int yy = wy - 4; yy <= wy + 4; yy++) for (int xx = wx - 4; xx <= wx + 4; xx++)
                            if (BoatWater(xx, yy) && Math.Max(Math.Abs(xx - wx), Math.Abs(yy - wy)) < sd) { sd = Math.Max(Math.Abs(xx - wx), Math.Abs(yy - wy)); sx = xx; sy = yy; }
                        best = (x, y, sx, sy); bestD = d; break;
                    }
                }
            return best;
        }

        /// <summary>Whether this raid comes by longship: a town with a landing, on about two raids in five (a function of the day and the seed).</summary>
        bool RaidByShip() => LongshipLanding().HasValue && Noise.Hash2(Day, Seed, 7373) < LongshipChance;
    }
}
