// The world map's water, as far as trade by water needs it: which cells are sea and which are lake, grouped into connected bodies, so two
// settlements are on the same water only when one body of sea or lake really joins them. Derived from the world map (never saved).
//
// What it does NOT know yet: rivers. The world map keeps a runoff value for land cells but no river courses or where they meet the sea, so
// trade by river between settlements cannot be verified and is not offered (README: DESIGN_WATER.md, "Transport modes"). Where the world
// map cannot show a water connection, trade stays overland: a connection is never assumed.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>How a shipment travels.</summary>
    public enum TradeMode : byte { Overland, River, Lake, Coastal }

    public struct WaterAccess
    {
        public TradeMode Mode;      // Coastal (sea) or Lake
        public int Body;            // which connected sea or lake
        public int EntryX, EntryY;  // the nearest water cell
        public int Approach;        // cells from the settlement to that water
    }

    public sealed class WaterAtlas
    {
        public const int AccessReach = 3;

        public readonly int Size;
        readonly int[] _sea, _lake;      // 0 = not this water, else the id of the connected body
        public readonly int Seas, Lakes;

        public WaterAtlas(int size, bool[] sea, bool[] lake)
        {
            Size = size; _sea = Label(size, sea, out Seas); _lake = Label(size, lake, out Lakes);
        }

        /// <summary>Sea is every cell below sea level that is not a lake; lakes are the world map's lake cells.</summary>
        public static WaterAtlas FromWorldMap(WorldMap m)
        {
            int n = WorldMap.Size * WorldMap.Size; var sea = new bool[n]; var lake = new bool[n];
            for (int i = 0; i < n; i++) { bool isLake = m.Biomes[i] == Biome.Lake; lake[i] = isLake; sea[i] = !isLake && m.Elevation[i] < 0; }
            return new WaterAtlas(WorldMap.Size, sea, lake);
        }

        static int[] Label(int size, bool[] water, out int count)
        {
            var id = new int[size * size]; count = 0; var q = new Queue<int>();
            for (int s = 0; s < id.Length; s++)
            {
                if (!water[s] || id[s] != 0) continue;
                count++; id[s] = count; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), x = c % size, y = c / size;
                    if (x > 0 && water[c - 1] && id[c - 1] == 0) { id[c - 1] = count; q.Enqueue(c - 1); }
                    if (x < size - 1 && water[c + 1] && id[c + 1] == 0) { id[c + 1] = count; q.Enqueue(c + 1); }
                    if (y > 0 && water[c - size] && id[c - size] == 0) { id[c - size] = count; q.Enqueue(c - size); }
                    if (y < size - 1 && water[c + size] && id[c + size] == 0) { id[c + size] = count; q.Enqueue(c + size); }
                }
            }
            return id;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public bool IsSea(int x, int y) => InBounds(x, y) && _sea[y * Size + x] != 0;
        public bool IsLake(int x, int y) => InBounds(x, y) && _lake[y * Size + x] != 0;

        /// <summary>The sea or lake a cell touches (within AccessReach cells), the sea first and the nearest first; null for a landlocked cell.</summary>
        public WaterAccess? AccessAt(int x, int y)
        {
            WaterAccess? best = null; int bestD = int.MaxValue;
            for (int d = 0; d <= AccessReach && !best.HasValue; d++)
                for (int dy = -d; dy <= d; dy++)
                    for (int dx = -d; dx <= d; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != d) continue;
                        int nx = x + dx, ny = y + dy; if (!InBounds(nx, ny)) continue;
                        int i = ny * Size + nx;
                        if (_sea[i] != 0 && d < bestD) { best = new WaterAccess { Mode = TradeMode.Coastal, Body = _sea[i], EntryX = nx, EntryY = ny, Approach = d }; bestD = d; }
                        else if (_lake[i] != 0 && d < bestD && !best.HasValue) { best = new WaterAccess { Mode = TradeMode.Lake, Body = _lake[i], EntryX = nx, EntryY = ny, Approach = d }; bestD = d; }
                    }
            return best;
        }

        /// <summary>Length in cells (about km) of the shortest route over the water between two accesses, including the approaches on land; -1 when
        /// they are on different water (or different kinds).</summary>
        public int RouteKm(WaterAccess a, WaterAccess b)
        {
            if (a.Mode != b.Mode || a.Body != b.Body) return -1;
            var body = a.Mode == TradeMode.Coastal ? _sea : _lake;
            int s = a.EntryY * Size + a.EntryX, t = b.EntryY * Size + b.EntryX;
            var dist = new int[Size * Size]; for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var q = new Queue<int>(); dist[s] = 0; q.Enqueue(s);
            while (q.Count > 0)
            {
                int c = q.Dequeue(); if (c == t) break; int x = c % Size, y = c / Size;
                if (x > 0 && dist[c - 1] < 0 && body[c - 1] == a.Body) { dist[c - 1] = dist[c] + 1; q.Enqueue(c - 1); }
                if (x < Size - 1 && dist[c + 1] < 0 && body[c + 1] == a.Body) { dist[c + 1] = dist[c] + 1; q.Enqueue(c + 1); }
                if (y > 0 && dist[c - Size] < 0 && body[c - Size] == a.Body) { dist[c - Size] = dist[c] + 1; q.Enqueue(c - Size); }
                if (y < Size - 1 && dist[c + Size] < 0 && body[c + Size] == a.Body) { dist[c + Size] = dist[c] + 1; q.Enqueue(c + Size); }
            }
            return dist[t] < 0 ? -1 : dist[t] + a.Approach + b.Approach;
        }
    }
}
