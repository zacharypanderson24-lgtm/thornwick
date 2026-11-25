// Surface water: the data model for rivers, lakes, ponds, oceans and their shores (DESIGN_WATER.md).
// This is NOT the underground drinking-water system (aquifers, wells, cisterns: World.Water.cs). That one keeps working unchanged.
//
// One lightweight record per tile, kept in flat arrays (no object per water tile). Everything here is derived once when a world is
// generated (Hydrology.cs) and then saved as it is, so a saved map never changes if the generator is improved later.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CrownAndHearth.Sim
{
    /// <summary>What a tile of the surface-water layer is. None is dry land.</summary>
    public enum WaterClass : byte { None = 0, Stream, SmallRiver, MajorRiver, Pond, Lake, Ocean, Sea, Estuary, Wetland }

    /// <summary>Shallow: shores, banks, fords. Medium: ordinary rivers, lakes, coastal water (small boats and barges). Deep: major channels and open sea.</summary>
    public enum WaterDepth : byte { None = 0, Shallow, Medium, Deep }

    /// <summary>Generation presets (DESIGN_WATER.md section 15). None keeps the world exactly as it was before water existed.</summary>
    public enum WaterPreset : byte { None = 0, BalancedInland, RiverValley, Lakeside, Coastal, Island, Archipelago, Wetlands, AridOasis, MountainWatershed }

    [Flags] public enum WaterFlags : byte
    {
        None = 0,
        Shore = 1,       // a water tile with dry land beside it (4 neighbours)
        Bank = 2,        // a dry tile with open water within one tile (8 neighbours)
        Saturated = 4,   // wet ground: wetland tiles and the fringe of marshy water
        Navigable = 8,   // deep enough for boats (medium or deep open water)
        Standing = 16,   // lake, pond, sea, ocean (one flat surface per connected body)
        Delta = 32,      // a distributary channel of a river delta
    }

    /// <summary>One connected body of standing water, or one river system. Built from the tile arrays.</summary>
    public sealed class WaterBody
    {
        public int Id;
        public WaterClass Kind;          // the main class of its tiles
        public bool Standing;            // lake, pond, sea, ocean (flat surface)
        public bool Salt;
        public int Surface;              // standing water: its single surface level; rivers: the highest level
        public int Tiles;
        public int MaxDepth;             // WaterDepth of its deepest tile
        public bool TouchesEdge;         // reaches the map edge (continues beyond it)
    }

    public sealed class SurfaceWater
    {
        public const byte NoFlow = 0;
        /// <summary>Flow directions 1..8: E, SE, S, SW, W, NW, N, NE. World axes: +X east, +Y south.</summary>
        public static readonly int[] Dx = { 0, 1, 1, 0, -1, -1, -1, 0, 1 };
        public static readonly int[] Dy = { 0, 0, 1, 1, 1, 0, -1, -1, -1 };
        public static readonly string[] DirName = { "none", "East", "Southeast", "South", "Southwest", "West", "Northwest", "North", "Northeast" };
        /// <summary>The four art directions of the river sprites (0 east, 1 south, 2 west, 3 north) nearest to a flow direction (1..8).</summary>
        public static int ArtDirection(int flow) => flow == 0 ? 0 : ((flow - 1) / 2 + ((flow - 1) % 2)) % 4;   // diagonals round to the next clockwise axis

        public readonly int Size;                 // tiles per side
        public readonly int Seed;
        public readonly WaterPreset Preset;
        public readonly WaterClass[] Class;
        public readonly WaterDepth[] Depth;
        public readonly byte[] Flow;              // 0 none, 1..8 (see Dx/Dy), world space, camera independent
        public readonly byte[] Strength;          // current: 0 still .. 255 torrent (from the drainage area)
        public readonly byte[] Salinity;          // 0 fresh .. 255 sea water
        public readonly byte[] Width;             // river width in tiles at this tile (0 for standing water)
        public readonly short[] Surface;          // water surface level (terrain levels, same scale as World.Hv); land: 0
        public readonly WaterFlags[] Flags;
        // derived from the arrays above (RebuildBodies), not saved
        public readonly ushort[] Body;            // 0 none; standing bodies and river systems each have their own id
        public readonly ushort[] Network;         // every connected stretch of open water shares one id
        public readonly ushort[] NavRegion;       // connected navigable water (boats cannot go between two regions)
        public readonly List<WaterBody> Bodies = new List<WaterBody>();
        public int StartAttempt;                  // which generation attempt produced this map (diagnostic)
        public string StartProblems = "";
        public bool StartValid;                   // generation found a valid start (diagnostic)

        public SurfaceWater(int size, int seed, WaterPreset preset)
        {
            Size = size; Seed = seed; Preset = preset;
            int n = size * size;
            Class = new WaterClass[n]; Depth = new WaterDepth[n]; Flow = new byte[n]; Strength = new byte[n]; Salinity = new byte[n];
            Width = new byte[n]; Surface = new short[n]; Flags = new WaterFlags[n];
            Body = new ushort[n]; Network = new ushort[n]; NavRegion = new ushort[n];
        }

        public int Idx(int x, int y) => y * Size + x;
        public bool Inb(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static bool OpenWater(WaterClass c) => c != WaterClass.None && c != WaterClass.Wetland;
        public static bool Standing(WaterClass c) => c == WaterClass.Pond || c == WaterClass.Lake || c == WaterClass.Ocean || c == WaterClass.Sea;
        public static bool Salty(WaterClass c) => c == WaterClass.Ocean || c == WaterClass.Sea;
        public static bool River(WaterClass c) => c == WaterClass.Stream || c == WaterClass.SmallRiver || c == WaterClass.MajorRiver || c == WaterClass.Estuary;

        /// <summary>True for any tile of open water (not land, not wet ground).</summary>
        public bool IsWater(int x, int y) => Inb(x, y) && OpenWater(Class[y * Size + x]);
        public bool IsWaterAt(int i) => OpenWater(Class[i]);
        public bool IsWetland(int x, int y) => Inb(x, y) && Class[y * Size + x] == WaterClass.Wetland;
        public WaterClass ClassAt(int x, int y) => Inb(x, y) ? Class[y * Size + x] : WaterClass.None;
        public WaterDepth DepthAt(int x, int y) => Inb(x, y) ? Depth[y * Size + x] : WaterDepth.None;
        public int WaterTiles { get { int c = 0; for (int i = 0; i < Class.Length; i++) if (OpenWater(Class[i])) c++; return c; } }

        /// <summary>Recompute body, network and navigable-region ids and the shore flags from the tile arrays.</summary>
        public void RebuildBodies()
        {
            int n = Size * Size;
            Array.Clear(Body, 0, n); Array.Clear(Network, 0, n); Array.Clear(NavRegion, 0, n); Bodies.Clear();
            for (int i = 0; i < n; i++) Flags[i] &= WaterFlags.Saturated | WaterFlags.Delta;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (OpenWater(Class[i]))
                    {
                        if (Standing(Class[i])) Flags[i] |= WaterFlags.Standing;
                        if (Depth[i] >= WaterDepth.Medium) Flags[i] |= WaterFlags.Navigable;
                        for (int k = 0; k < 4; k++) { int nx = x + Dx[1 + k * 2], ny = y + Dy[1 + k * 2]; if (Inb(nx, ny) && !OpenWater(Class[ny * Size + nx])) { Flags[i] |= WaterFlags.Shore; break; } }
                    }
                    else
                    {
                        for (int dy = -1; dy <= 1 && (Flags[i] & WaterFlags.Bank) == 0; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && Inb(x + dx, y + dy) && OpenWater(Class[(y + dy) * Size + x + dx])) { Flags[i] |= WaterFlags.Bank; break; }
                    }
                }
            var queue = new Queue<int>();
            ushort nextNet = 1, nextNav = 1;
            for (int s = 0; s < n; s++)
            {
                if (!OpenWater(Class[s])) continue;
                if (Network[s] == 0) Flood(s, queue, i => OpenWater(Class[i]), Network, nextNet++, null, true);
                if (NavRegion[s] == 0 && (Flags[s] & WaterFlags.Navigable) != 0) Flood(s, queue, i => (Flags[i] & WaterFlags.Navigable) != 0, NavRegion, nextNav++);
                if (Body[s] != 0) continue;
                bool st = Standing(Class[s]);
                var b = new WaterBody { Id = Bodies.Count + 1, Standing = st, Kind = Class[s] };
                int bid = b.Id; int minSurf = int.MaxValue, maxSurf = int.MinValue;
                Flood(s, queue, i => OpenWater(Class[i]) && Standing(Class[i]) == st, Body, (ushort)bid, i =>
                {
                    b.Tiles++; if (Salty(Class[i])) b.Salt = true;
                    minSurf = Math.Min(minSurf, Surface[i]); maxSurf = Math.Max(maxSurf, Surface[i]);
                    if ((int)Depth[i] > b.MaxDepth) b.MaxDepth = (int)Depth[i];
                    int x = i % Size, y = i / Size; if (x == 0 || y == 0 || x == Size - 1 || y == Size - 1) b.TouchesEdge = true;
                    if (Class[i] == WaterClass.MajorRiver) b.Kind = WaterClass.MajorRiver;
                }, !st);
                b.Surface = st ? minSurf : maxSurf;
                Bodies.Add(b);
            }
        }

        void Flood(int start, Queue<int> q, Func<int, bool> pass, ushort[] ids, ushort id, Action<int> visit = null, bool diag = false)
        {
            q.Clear(); ids[start] = id; q.Enqueue(start);
            while (q.Count > 0)
            {
                int c = q.Dequeue(); visit?.Invoke(c);
                int x = c % Size, y = c / Size;
                for (int k = 0; k < (diag ? 8 : 4); k++)
                {
                    int nx = diag ? x + Dx[1 + k] : x + Dx[1 + k * 2], ny = diag ? y + Dy[1 + k] : y + Dy[1 + k * 2]; if (!Inb(nx, ny)) continue;
                    int ni = ny * Size + nx; if (ids[ni] != 0 || !pass(ni)) continue;
                    ids[ni] = id; q.Enqueue(ni);
                }
            }
        }

        public WaterBody BodyAt(int x, int y) => Inb(x, y) && Body[y * Size + x] != 0 ? Bodies[Body[y * Size + x] - 1] : null;

        // ---------- Saving ----------
        /// <summary>The tile arrays as one compressed base64 string (about 100 KB for a 256 x 256 map, mostly zeros).</summary>
        public string Encode()
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
            using (var w = new BinaryWriter(gz))
            {
                w.Write(1); w.Write(Size); w.Write(Seed); w.Write((byte)Preset);
                int n = Size * Size;
                for (int i = 0; i < n; i++) w.Write((byte)Class[i]);
                for (int i = 0; i < n; i++) w.Write((byte)Depth[i]);
                w.Write(Flow); w.Write(Strength); w.Write(Salinity); w.Write(Width);
                for (int i = 0; i < n; i++) w.Write(Surface[i]);
                for (int i = 0; i < n; i++) w.Write((byte)(Flags[i] & (WaterFlags.Saturated | WaterFlags.Delta)));
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        public static SurfaceWater Decode(string text)
        {
            using var ms = new MemoryStream(Convert.FromBase64String(text));
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var r = new BinaryReader(gz);
            if (r.ReadInt32() != 1) throw new InvalidDataException("Unsupported water layer in settlement save.");
            int size = r.ReadInt32(), seed = r.ReadInt32(); var preset = (WaterPreset)r.ReadByte();
            if (size <= 0 || size > 4096) throw new InvalidDataException("Invalid water layer size.");
            var sw = new SurfaceWater(size, seed, preset); int n = size * size;
            for (int i = 0; i < n; i++) sw.Class[i] = (WaterClass)r.ReadByte();
            for (int i = 0; i < n; i++) sw.Depth[i] = (WaterDepth)r.ReadByte();
            Array.Copy(r.ReadBytes(n), sw.Flow, n); Array.Copy(r.ReadBytes(n), sw.Strength, n);
            Array.Copy(r.ReadBytes(n), sw.Salinity, n); Array.Copy(r.ReadBytes(n), sw.Width, n);
            for (int i = 0; i < n; i++) sw.Surface[i] = r.ReadInt16();
            for (int i = 0; i < n; i++) sw.Flags[i] = (WaterFlags)r.ReadByte();
            sw.RebuildBodies();
            return sw;
        }
    }
}
