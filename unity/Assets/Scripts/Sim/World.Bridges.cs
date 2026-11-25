// Wooden bridges (DESIGN_WATER.md section 19). A bridge is a stretch of road laid over water: its tiles are ordinary road buildings
// standing on water tiles, so road connection, walking, workers, raiders and saving all work with no new building kind and no new save
// field. Water stays impassable (IsSolid) everywhere except under a road.
//
// Rules: a straight line (so it has an orientation), from dry land to dry land, across one stretch of fresh or brackish water that is
// no more than BridgeMaxSpan tiles wide and has no deep water in it (a wooden bridge cannot span the open sea or a deep channel). It costs
// BridgeWood wood per water tile, and one wood per new land tile (the approach road).
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int BridgeMaxSpan = 8, BridgeWood = 5;

        /// <summary>True when the tile is a road laid over water.</summary>
        public bool IsBridge(int x, int y) => Waters != null && Inb(x, y) && Grid[y, x]?.T == "road" && Waters.IsWater(x, y);

        /// <summary>The straight line from a to b, along whichever axis the mouse has moved furthest (so a bridge always has an orientation).</summary>
        public static List<(int x, int y)> BridgeLine(int ax, int ay, int bx, int by)
        {
            var path = new List<(int x, int y)>();
            if (Math.Abs(bx - ax) >= Math.Abs(by - ay)) { int s = bx >= ax ? 1 : -1; for (int x = ax; x != bx + s; x += s) path.Add((x, ay)); }
            else { int s = by >= ay ? 1 : -1; for (int y = ay; y != by + s; y += s) path.Add((ax, y)); }
            return path;
        }

        public Result BridgePathCheck(IList<(int x, int y)> path)
        {
            if (Waters == null) return Result.No("There is no water here to bridge");
            if (path.Count < 3) return Result.No("A bridge must reach from one bank to the other");
            bool alongX = path[0].y == path[path.Count - 1].y, alongY = path[0].x == path[path.Count - 1].x;
            if (!alongX && !alongY) return Result.No("A bridge runs in a straight line");
            int first = -1, last = -1, water = 0;
            for (int i = 0; i < path.Count; i++) if (Inb(path[i].x, path[i].y) && Waters.IsWater(path[i].x, path[i].y)) { if (first < 0) first = i; last = i; water++; }
            if (water == 0) return Result.No("A bridge crosses water: drag from one bank across to the other");
            if (first == 0 || last == path.Count - 1) return Result.No("A bridge must start and end on dry land: drag from bank to bank");
            if (last - first + 1 != water) return Result.No("A bridge crosses one stretch of water at a time");
            if (water > BridgeMaxSpan) return Result.No($"Too wide: a wooden bridge spans at most {BridgeMaxSpan} tiles of water (this is {water})");
            int landNew = 0, waterNew = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var (x, y) = path[i];
                if (!Inb(x, y)) return Result.No("Outside the map");
                if (!Claimed(x, y)) return Result.No(NotYours);
                var g = Grid[y, x];
                bool isWater = i >= first && i <= last;
                if (g != null && g.T == "road") continue;
                if (g != null) return Result.No("A building is in the way");
                if (PenAt(x, y) != null) return Result.No("A livestock pen is in the way");
                if (isWater)
                {
                    var c = Waters.Class[y * N + x];
                    if (c == WaterClass.Ocean || c == WaterClass.Sea) return Result.No("A wooden bridge cannot cross the open sea");
                    if (Waters.Depth[y * N + x] == WaterDepth.Deep) return Result.No("Too deep for a wooden bridge: find a narrower, shallower crossing");
                    waterNew++;
                }
                else
                {
                    if (HasTree(x, y)) return Result.No(TreeMsg);
                    if (IsSolid(x, y)) return Result.No("The bank is blocked here (mountain, pit, ore or ground too steep). Pick another crossing");
                    if (WaterBlock(x, y, false) is string wet) return Result.No(wet);
                    landNew++;
                }
            }
            int cost = landNew * Cost["road"][Res.Wood] + waterNew * BridgeWood;
            if (ResAmt[Res.Wood] < cost) return Result.No($"Not enough wood: this bridge needs {cost} wood ({waterNew} bridge tiles at {BridgeWood}, {landNew} road tiles at 1)", landNew + waterNew);
            return Result.Yes(landNew + waterNew);
        }

        /// <summary>Wood a bridge along this path would cost, counting only the tiles not yet built.</summary>
        public int BridgeCost(IList<(int x, int y)> path)
        {
            int cost = 0;
            foreach (var (x, y) in path) if (Inb(x, y) && Grid[y, x] == null) cost += Waters != null && Waters.IsWater(x, y) ? BridgeWood : Cost["road"][Res.Wood];
            return cost;
        }

        public Result PlaceBridgePath(IList<(int x, int y)> path)
        {
            var c = BridgePathCheck(path);
            if (!c.Ok || c.Count == 0) return c;
            ResAmt[Res.Wood] -= BridgeCost(path);
            foreach (var (x, y) in path) if (Grid[y, x] == null) AddBld("road", x, y);
            Touch(); Recalc();
            return c;
        }
    }
}
