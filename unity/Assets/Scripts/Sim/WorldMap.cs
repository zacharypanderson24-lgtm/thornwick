// The conquest map: a procedurally generated strategic world (Unity-only; the browser prototype has no world map).
//
// A city is its own detailed World (256 x 256 tiles of 8 m, about 2 km; the player owns a 64 x 64 district of it to start). This is the layer above it: a 600 x 600 km
// campaign on a grid of strategic cells, cut into 24 x 24 = 576 regions of about 25 km. It follows the generation sequence in the game
// vision (docs/GAME_VISION.md): geography first, biome labels last.
//
//   1. a campaign setting (northern temperate, central Asian, Mediterranean or monsoon) chosen from the seed: wetness, relief
//      and the prevailing wind. Temperature is the same heat map on every map: cold at the top, hot at the bottom
//   2. elevation: a continent with a ragged coast, mountain chains, hills
//   3. temperature (latitude and altitude) and rainfall (a moist wind crossing the map: rain falls on windward slopes, rain shadows
//      behind mountains)
//   4. hydrology: depressions are filled (priority-flood), every land cell drains to the sea or a lake, flow accumulates into rivers
//   5. biomes: the 16 primary biomes picked from temperature, rainfall, altitude and slope, so each campaign gets a plausible subset;
//      rivers, floodplains and coasts are overlays, not biomes
//   6. fertility, region summaries, and one candidate city site per region
//   7. the home region: where the player's main settlement stands, chosen to match the city's relief
//
// Everything comes from the seed alone (same seed, same world), so nothing here is saved: a save keeps its seed and the map regenerates.
// Pure C# (no UnityEngine): it runs in EditMode tests and on a background thread while the game keeps drawing.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>The 16 primary biomes of the game vision, plus the two kinds of water. Numbers follow the vision document's table.</summary>
    public enum Biome : byte
    {
        Ocean, Lake,
        TemperateGrassland, TemperateForest, BorealForest, Highlands, AlpineMountains, Wetlands, MediterraneanScrub, Steppe,
        SemiAridScrub, Tundra, Desert, Savanna, SubtropicalForest, TropicalRainforest, TropicalSeasonalForest, TropicalHighlands,
    }

    /// <summary>The broad campaign setting (the vision document's four example campaign types).</summary>
    public enum MapSetting : byte { NorthernTemperate, CentralAsian, Mediterranean, Monsoon }

    /// <summary>Geographic overlays: not exclusive biomes, a floodplain can cut across forest or grassland.</summary>
    [Flags] public enum MapFeature : byte { None = 0, River = 1, MajorRiver = 2, Coast = 4, Floodplain = 8 }

    /// <summary>A candidate city site.</summary>
    public sealed class MapSite { public string Name; public int X, Y; }

    /// <summary>One strategic region (25 x 25 cells) and what it is made of.</summary>
    public sealed class MapRegion
    {
        public int Id, Col, Row;
        public string Name;
        public Biome Dominant;
        public readonly int[] BiomeCells = new int[Enum.GetValues(typeof(Biome)).Length];
        public double Land, Water;            // shares of the region's cells (water: sea and lakes)
        public double Fertile, Forest, Mountain;   // shares of the LAND cells
        public double ElevAvg, ElevMax, Temperature, Rain;   // averages over land cells (metres, degrees C, mm a year)
        public int RiverKm;                   // river cells
        public Relief Relief;                 // how mountainous a city here would be (a city's World takes this)
        public MapSite Site;                  // the best place for a city; null in open sea
        public bool Coastal;
        public bool IsLand => Land >= 0.25;
    }

    public sealed class WorldMap
    {
        public const int Regions = 24, RegionCells = 25, Size = Regions * RegionCells;   // 600 x 600 cells
        /// <summary>One strategic cell is 1 km. The vision document's candidate is 250 m; this first map is coarser to stay cheap.</summary>
        public const int CellMeters = 1000;
        /// <summary>A city tile is 8 m; a city can grow to 256 x 256 tiles, about 2 km, which is 2 x 2 strategic cells.</summary>
        public const int CityTileMeters = 8, CityTilesStart = 64, CityTilesMax = 256;
        public const double CityKm = CityTilesMax * CityTileMeters / 1000.0;

        // tuning, in one place
        // Every campaign is a heat map from top to bottom: latitude sets the temperature, so the cold biomes (tundra, boreal forest) lie in
        // the top bands and the hot ones (savanna, desert, tropical forest) in the bottom bands. The setting changes wetness, wind and relief.
        public const double TNorthSea = -7, TSouthSea = 30;   // mean annual degrees C at sea level on the top and bottom edges
        const double ContinentBias = 0.62, ContinentFall = 0.80, ContinentNoise = 1.05;
        const float RiverMinor = 120f, RiverMajor = 800f, LakeDepth = 35f;
        const int MinLakeCells = 12;

        public readonly int Seed;
        public readonly MapSetting Setting;
        public readonly Relief HomeRelief;
        public readonly float[] Elevation = new float[Size * Size];      // metres above sea level; below zero is sea
        public readonly float[] Temperature = new float[Size * Size];    // mean annual, degrees C, at the cell's own height
        public readonly float[] Rainfall = new float[Size * Size];       // mm a year (land)
        public readonly float[] Flow = new float[Size * Size];           // runoff gathered from upstream (land)
        public readonly float[] Fertility = new float[Size * Size];      // 0..1 (land)
        public readonly Biome[] Biomes = new Biome[Size * Size];
        public readonly MapFeature[] Features = new MapFeature[Size * Size];
        public readonly MapRegion[] RegionList = new MapRegion[Regions * Regions];
        public int HomeRegion { get; private set; } = -1;
        public MapSite HomeSite => HomeRegion >= 0 ? RegionList[HomeRegion].Site : null;
        public MapRegion Home => HomeRegion >= 0 ? RegionList[HomeRegion] : null;

        // ---------- Access ----------
        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static int Cell(int x, int y) => y * Size + x;
        public static int RegionIndex(int x, int y) => (y / RegionCells) * Regions + x / RegionCells;
        public bool IsLand(int x, int y) => Elevation[Cell(x, y)] > 0 && Biomes[Cell(x, y)] != Biome.Lake;
        public MapRegion RegionAt(int x, int y) => RegionList[RegionIndex(x, y)];

        public static string BiomeName(Biome b) => b switch
        {
            Biome.Ocean => "Open sea", Biome.Lake => "Lake", Biome.TemperateGrassland => "Temperate grassland", Biome.TemperateForest => "Temperate forest",
            Biome.BorealForest => "Boreal forest", Biome.Highlands => "Highlands and moorland", Biome.AlpineMountains => "Alpine mountains",
            Biome.Wetlands => "Wetlands and marsh", Biome.MediterraneanScrub => "Mediterranean woodland", Biome.Steppe => "Steppe",
            Biome.SemiAridScrub => "Semi-arid scrubland", Biome.Tundra => "Tundra", Biome.Desert => "Desert", Biome.Savanna => "Savanna",
            Biome.SubtropicalForest => "Subtropical forest", Biome.TropicalRainforest => "Tropical rainforest",
            Biome.TropicalSeasonalForest => "Tropical seasonal forest", Biome.TropicalHighlands => "Tropical highlands", _ => b.ToString(),
        };

        public static string SettingName(MapSetting s) => s switch
        {
            MapSetting.NorthernTemperate => "Northern temperate", MapSetting.CentralAsian => "Central Asian",
            MapSetting.Mediterranean => "Mediterranean", _ => "Monsoon",
        };

        // ---------- Generation ----------
        public static WorldMap Generate(int seed, Relief homeRelief = Relief.Upland, MapSetting? setting = null) => new WorldMap(seed, homeRelief, setting);

        struct ClimateSpec { public double Wet, Mountains; public int Wind; }   // Wind: 0 from the west, 1 east, 2 north, 3 south

        static ClimateSpec SpecOf(MapSetting s) => s switch
        {
            MapSetting.NorthernTemperate => new ClimateSpec { Wet = 1.0, Mountains = 1.0, Wind = 0 },
            MapSetting.CentralAsian => new ClimateSpec { Wet = 0.55, Mountains = 1.25, Wind = 0 },
            MapSetting.Mediterranean => new ClimateSpec { Wet = 0.62, Mountains = 1.0, Wind = 0 },
            _ => new ClimateSpec { Wet = 1.45, Mountains = 0.9, Wind = 3 },
        };

        WorldMap(int seed, Relief homeRelief, MapSetting? setting)
        {
            Seed = seed == 0 ? 7 : seed;
            HomeRelief = homeRelief;
            Setting = setting ?? (MapSetting)(int)(Noise.Hash2(Seed, 5, 977) * 4);
            var spec = SpecOf(Setting);
            GenElevation(spec);
            GenClimate(spec);
            var slope = Slopes();
            GenHydrology();
            GenBiomes(slope);
            GenRegions(slope);
            ChooseHome();
        }

        static double Fbm(double x, double y, int octaves, int seed)
        {
            double amp = 1, sum = 0, norm = 0, f = 1;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise.VNoise(x * f + i * 17.3, y * f - i * 9.1, seed + i * 101);
                norm += amp; amp *= 0.5; f *= 2.03;
            }
            return sum / norm;
        }

        // 2. elevation
        void GenElevation(ClimateSpec spec)
        {
            int S = Size; var cont = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    double nx = (x + 0.5) / S * 2 - 1, ny = (y + 0.5) / S * 2 - 1;
                    double r = Math.Sqrt(nx * nx + ny * ny);
                    double big = Fbm(x / 200.0, y / 200.0, 4, Seed + 11), mid = Fbm(x / 64.0, y / 64.0, 3, Seed + 12);
                    cont[y * S + x] = (float)(ContinentBias - ContinentFall * r + (big - 0.5) * ContinentNoise + (mid - 0.5) * 0.28);
                }

            // mountain chains: random walks that start well inland and fade out at both ends
            var ridge = new float[S * S];
            int chains = 6 + (int)(Noise.Hash2(Seed, 6, 978) * 4);
            int draws = 0; double Rnd() => Noise.Hash2(draws++, 29, Seed + 979);
            for (int c = 0; c < chains; c++)
            {
                double px = 0, py = 0; bool ok = false;
                for (int t = 0; t < 60 && !ok; t++) { px = 20 + Rnd() * (S - 40); py = 20 + Rnd() * (S - 40); ok = cont[(int)py * S + (int)px] > 0.2f; }
                if (!ok) continue;
                double heading = Rnd() * Math.PI * 2, length = 110 + Rnd() * 140, height = (1300 + Rnd() * 1500) * spec.Mountains, width = 6 + Rnd() * 7;
                int steps = (int)(length / 5);
                for (int s = 0; s < steps; s++)
                {
                    double taper = 0.3 + 0.7 * Math.Sin(Math.PI * (s + 0.5) / steps);
                    int reach = (int)(width * 2.6) + 1;
                    for (int y = (int)py - reach; y <= (int)py + reach; y++)
                        for (int x = (int)px - reach; x <= (int)px + reach; x++)
                        {
                            if (!Inb(x, y)) continue;
                            double d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) / width;
                            float v = (float)(height * taper * Math.Exp(-d * d));
                            if (v > ridge[y * S + x]) ridge[y * S + x] = v;
                        }
                    heading += (Rnd() - 0.5) * 0.75;
                    px += Math.Cos(heading) * 5; py += Math.Sin(heading) * 5;
                    if (px < 5 || py < 5 || px > S - 6 || py > S - 6) break;
                }
            }

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x; double c = cont[i];
                    if (c <= 0) { Elevation[i] = (float)Math.Min(-1, c * 2600); continue; }
                    double e = 2 + 520 * Math.Pow(c, 0.8)
                        + (Fbm(x / 55.0, y / 55.0, 4, Seed + 13) - 0.5) * 600 * Math.Min(1, c / 0.25)
                        + (Fbm(x / 16.0, y / 16.0, 2, Seed + 14) - 0.5) * 90;
                    if (ridge[i] > 1) e += ridge[i] * (0.65 + 0.7 * Fbm(x / 22.0, y / 22.0, 3, Seed + 15)) * Math.Min(1, c / 0.10);
                    Elevation[i] = (float)Math.Max(2, e);
                }
        }

        // 3. temperature and rainfall
        void GenClimate(ClimateSpec spec)
        {
            int S = Size;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    double sea = TNorthSea + (TSouthSea - TNorthSea) * ((y + 0.5) / S) + (Fbm(x / 180.0, y / 180.0, 3, Seed + 21) - 0.5) * 3.0;
                    Temperature[i] = (float)(sea - 6.5 * Math.Max(0, Elevation[i]) / 1000.0);
                }

            // a moist wind sweeps across the map one line at a time. Over sea it gathers moisture (more when warm); over land it rains out
            // a little every cell, a lot where the ground climbs, and the lee of a range is left dry.
            var rain = new float[S * S];
            for (int b = 0; b < S; b++)
            {
                double m = 0.5; int prev = -1;
                for (int a = 0; a < S; a++)
                {
                    int x, y;
                    switch (spec.Wind) { case 0: x = a; y = b; break; case 1: x = S - 1 - a; y = b; break; case 2: x = b; y = a; break; default: x = b; y = S - 1 - a; break; }
                    int i = y * S + x;
                    double cap = 0.18 + 0.82 * Math.Clamp((Temperature[i] + 8) / 38.0, 0, 1);
                    if (Elevation[i] <= 0) m += (cap - m) * 0.10;
                    else
                    {
                        double rise = prev >= 0 ? Math.Max(0, Elevation[i] - Math.Max(0, Elevation[prev])) / 1000.0 : 0;
                        double r = m * (0.012 + Math.Min(0.5, rise * 6.0)) + Math.Max(0, m - cap) * 0.5;
                        m = Math.Max(0, m - r) + 0.0035 * cap;
                        rain[i] = (float)r;
                    }
                    prev = i;
                }
            }
            Blur(rain, 3); Blur(rain, 3);
            double sum = 0; int land = 0;
            for (int i = 0; i < S * S; i++) if (Elevation[i] > 0) { sum += rain[i]; land++; }
            double scale = land > 0 && sum > 0 ? spec.Wet * 1250.0 * land / sum : 0;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    if (Elevation[i] <= 0) continue;
                    Rainfall[i] = (float)Math.Min(3600, rain[i] * scale * (0.88 + 0.24 * Noise.VNoise(x / 30.0, y / 30.0, Seed + 22)));
                }
        }

        /// <summary>Box blur (radius r), twice over rows then columns, in place.</summary>
        static void Blur(float[] a, int r)
        {
            int S = Size; var tmp = new float[S * S];
            for (int y = 0; y < S; y++)
            {
                double acc = 0; int n = 0;
                for (int x = -r; x <= r; x++) if (x >= 0 && x < S) { acc += a[y * S + x]; n++; }
                for (int x = 0; x < S; x++)
                {
                    tmp[y * S + x] = (float)(acc / n);
                    int add = x + r + 1, rem = x - r;
                    if (add < S) { acc += a[y * S + add]; n++; }
                    if (rem >= 0) { acc -= a[y * S + rem]; n--; }
                }
            }
            for (int x = 0; x < S; x++)
            {
                double acc = 0; int n = 0;
                for (int y = -r; y <= r; y++) if (y >= 0 && y < S) { acc += tmp[y * S + x]; n++; }
                for (int y = 0; y < S; y++)
                {
                    a[y * S + x] = (float)(acc / n);
                    int add = y + r + 1, rem = y - r;
                    if (add < S) { acc += tmp[add * S + x]; n++; }
                    if (rem >= 0) { acc -= tmp[rem * S + x]; n--; }
                }
            }
        }

        float[] Slopes()
        {
            int S = Size; var slope = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float e(int i, int j) => Elevation[Math.Clamp(j, 0, S - 1) * S + Math.Clamp(i, 0, S - 1)];
                    float dx = e(x + 1, y) - e(x - 1, y), dy = e(x, y + 1) - e(x, y - 1);
                    slope[y * S + x] = (float)Math.Sqrt(dx * dx + dy * dy) * 0.5f;   // metres of rise per km
                }
            return slope;
        }

        // 4. hydrology: priority-flood (Barnes): fill depressions from the sea inwards, remembering where each cell drains
        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        void GenHydrology()
        {
            int S = Size, N = S * S;
            var filled = (float[])Elevation.Clone();
            var parent = new int[N]; Array.Fill(parent, -1);
            var closed = new bool[N];
            var order = new int[N]; int on = 0;
            var heap = new MinHeap(N);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                    if (Elevation[i] <= 0) closed[i] = true;
                    bool seed = false;
                    if (Elevation[i] <= 0)
                        for (int k = 0; k < 8 && !seed; k++) { int nx = x + Dx[k], ny = y + Dy[k]; if (Inb(nx, ny) && Elevation[ny * S + nx] > 0) seed = true; }
                    else if (edge) { closed[i] = true; seed = true; }
                    if (seed) heap.Push(filled[i], i);
                }
            const float Eps = 0.01f;
            while (heap.Count > 0)
            {
                int c = heap.Pop(); int cx = c % S, cy = c / S;
                if (Elevation[c] > 0) order[on++] = c;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + Dx[k], ny = cy + Dy[k]; if (!Inb(nx, ny)) continue;
                    int n = ny * S + nx; if (closed[n]) continue;
                    closed[n] = true; parent[n] = c;
                    filled[n] = Math.Max(Elevation[n], filled[c] + Eps);
                    heap.Push(filled[n], n);
                }
            }

            // runoff: what falls and is not lost to the heat and the sun
            for (int i = 0; i < N; i++)
                if (Elevation[i] > 0) Flow[i] = Math.Clamp((Rainfall[i] - 200f - 12f * Math.Max(Temperature[i], 0f)) / 900f, 0.03f, 3f);
            for (int o = on - 1; o >= 0; o--) { int c = order[o]; int p = parent[c]; if (p >= 0) Flow[p] += Flow[c]; }

            // lakes: filled depressions at least LakeDepth deep, and large enough to read as a lake
            var lake = new bool[N];
            for (int i = 0; i < N; i++) lake[i] = Elevation[i] > 0 && filled[i] - Elevation[i] >= LakeDepth;
            var seen = new bool[N]; var stack = new Stack<int>(); var comp = new List<int>();
            for (int i0 = 0; i0 < N; i0++)
            {
                if (!lake[i0] || seen[i0]) continue;
                comp.Clear(); stack.Push(i0); seen[i0] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); comp.Add(c); int cx = c % S, cy = c / S;
                    for (int k = 0; k < 4; k++) { int nx = cx + Dx[k], ny = cy + Dy[k]; if (Inb(nx, ny) && lake[ny * S + nx] && !seen[ny * S + nx]) { seen[ny * S + nx] = true; stack.Push(ny * S + nx); } }
                }
                if (comp.Count < MinLakeCells) foreach (int c in comp) lake[c] = false;
            }

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    if (Elevation[i] <= 0) { Biomes[i] = Biome.Ocean; continue; }
                    if (lake[i]) { Biomes[i] = Biome.Lake; continue; }
                    var f = MapFeature.None;
                    if (Flow[i] >= RiverMinor) f |= MapFeature.River;
                    if (Flow[i] >= RiverMajor) f |= MapFeature.MajorRiver;
                    for (int k = 0; k < 4 && (f & MapFeature.Coast) == 0; k++) { int nx = x + Dx[k], ny = y + Dy[k]; if (Inb(nx, ny) && Elevation[ny * S + nx] <= 0) f |= MapFeature.Coast; }
                    Features[i] = f;
                }
        }

        // 5. biomes
        void GenBiomes(float[] slope)
        {
            int S = Size;
            // floodplains: flat land within 2 cells of a river
            var nearRiver = new bool[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if ((Features[y * S + x] & MapFeature.River) == 0) continue;
                    for (int j = -2; j <= 2; j++) for (int i = -2; i <= 2; i++) if (Inb(x + i, y + j)) nearRiver[(y + j) * S + x + i] = true;
                }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    if (Elevation[i] <= 0 || Biomes[i] == Biome.Lake) continue;
                    float T = Temperature[i], P = Rainfall[i], E = Elevation[i], sl = slope[i];
                    double n = Noise.VNoise(x / 38.0, y / 38.0, Seed + 31), n2 = Noise.VNoise(x / 17.0, y / 17.0, Seed + 32);
                    double p = P * (0.9 + 0.2 * n);                     // so the biome edges wander instead of following rainfall contours
                    double sea = T + 6.5 * E / 1000.0;                  // temperature at sea level: tropical or not does not depend on height
                    bool tropical = sea >= 20;
                    Biome b;
                    if (E >= 1700 || (E >= 1300 && sl >= 100)) b = tropical && E < 2600 ? Biome.TropicalHighlands : Biome.AlpineMountains;
                    else if (E >= 1100 || (E >= 750 && sl >= 55)) b = tropical ? Biome.TropicalHighlands : (T < 0.5 ? Biome.AlpineMountains : Biome.Highlands);
                    else if (T < 0) b = Biome.Tundra;
                    else if (T < 5.5) b = p >= 450 ? Biome.BorealForest : p >= 220 ? (n2 > 0.5 ? Biome.BorealForest : Biome.Steppe) : Biome.Tundra;
                    else if (T < 14) b = p >= 900 ? Biome.TemperateForest : p >= 520 ? (n2 > 0.42 ? Biome.TemperateForest : Biome.TemperateGrassland)
                        : p >= 280 ? (T < 10 ? Biome.Steppe : Biome.TemperateGrassland) : p >= 160 ? Biome.SemiAridScrub : Biome.Desert;
                    else if (T < 19) b = p >= 1300 ? Biome.SubtropicalForest : p >= 650 ? (n2 > 0.55 ? Biome.SubtropicalForest : Biome.MediterraneanScrub)
                        : p >= 380 ? Biome.MediterraneanScrub : p >= 200 ? Biome.SemiAridScrub : Biome.Desert;
                    else b = p >= 2200 ? Biome.TropicalRainforest : p >= 1200 ? Biome.TropicalSeasonalForest : p >= 650 ? Biome.Savanna
                        : p >= 300 ? Biome.SemiAridScrub : Biome.Desert;
                    // marsh: low, flat, wet ground near a river or the coast, in patches
                    if (b != Biome.AlpineMountains && b != Biome.Highlands && b != Biome.TropicalHighlands && E < 140 && sl < 7 && P > 650 && T > 2 &&
                        (nearRiver[i] || (Features[i] & MapFeature.Coast) != 0) && Noise.VNoise(x / 12.0, y / 12.0, Seed + 33) > 0.58) b = Biome.Wetlands;
                    Biomes[i] = b;
                    if (nearRiver[i] && sl < 25) Features[i] |= MapFeature.Floodplain;
                    Fertility[i] = FertilityOf(b, sl, (Features[i] & MapFeature.Floodplain) != 0);
                }
        }

        static float FertilityOf(Biome b, float slope, bool floodplain)
        {
            float f = b switch
            {
                Biome.TemperateGrassland => 0.80f, Biome.TemperateForest => 0.55f, Biome.BorealForest => 0.30f, Biome.Highlands => 0.20f,
                Biome.AlpineMountains => 0.02f, Biome.Wetlands => 0.35f, Biome.MediterraneanScrub => 0.55f, Biome.Steppe => 0.45f,
                Biome.SemiAridScrub => 0.18f, Biome.Tundra => 0.08f, Biome.Desert => 0.03f, Biome.Savanna => 0.50f,
                Biome.SubtropicalForest => 0.60f, Biome.TropicalRainforest => 0.45f, Biome.TropicalSeasonalForest => 0.65f,
                Biome.TropicalHighlands => 0.45f, _ => 0f,
            };
            f *= 1f - Math.Clamp(slope / 150f, 0f, 0.95f);
            if (floodplain) f += 0.25f;
            return Math.Clamp(f, 0f, 1f);
        }

        // 6. regions and city sites
        void GenRegions(float[] slope)
        {
            int S = Size;
            var names = new HashSet<string>();
            for (int r = 0; r < Regions * Regions; r++)
            {
                var reg = RegionList[r] = new MapRegion { Id = r, Col = r % Regions, Row = r / Regions };
                int x0 = reg.Col * RegionCells, y0 = reg.Row * RegionCells;
                int cells = RegionCells * RegionCells, land = 0, water = 0, fertile = 0, forest = 0, mountain = 0;
                double elev = 0, temp = 0, rain = 0; float emax = 0;
                MapSite best = null; double bestScore = double.MinValue;
                for (int y = y0; y < y0 + RegionCells; y++)
                    for (int x = x0; x < x0 + RegionCells; x++)
                    {
                        int i = y * S + x; var b = Biomes[i];
                        reg.BiomeCells[(int)b]++;
                        if (b == Biome.Ocean || b == Biome.Lake) { water++; continue; }
                        land++; elev += Elevation[i]; temp += Temperature[i]; rain += Rainfall[i]; emax = Math.Max(emax, Elevation[i]);
                        if (Fertility[i] >= 0.5f) fertile++;
                        if (b == Biome.TemperateForest || b == Biome.BorealForest || b == Biome.SubtropicalForest || b == Biome.TropicalRainforest || b == Biome.TropicalSeasonalForest) forest++;
                        if (b == Biome.AlpineMountains || (b == Biome.TropicalHighlands && Elevation[i] >= 1500)) mountain++;
                        if ((Features[i] & MapFeature.River) != 0) reg.RiverKm++;
                        if ((Features[i] & MapFeature.Coast) != 0) reg.Coastal = true;
                        // a city wants fertile, flat ground by water, not at the very edge of its region
                        int mx = x - x0, my = y - y0;
                        if (mx < 3 || my < 3 || mx >= RegionCells - 3 || my >= RegionCells - 3) continue;
                        double score = Fertility[i] + ((Features[i] & MapFeature.Coast) != 0 ? 0.25 : 0) + ((Features[i] & MapFeature.River) != 0 ? 0.3 : (Features[i] & MapFeature.Floodplain) != 0 ? 0.2 : 0)
                            - Elevation[i] / 2500.0 - slope[i] / 200.0 + Noise.Hash2(x, y, Seed + 41) * 0.05;
                        if (score > bestScore) { bestScore = score; best = new MapSite { X = x, Y = y }; }
                    }
                reg.Land = land / (double)cells; reg.Water = water / (double)cells;
                if (land > 0)
                {
                    reg.Fertile = fertile / (double)land; reg.Forest = forest / (double)land; reg.Mountain = mountain / (double)land;
                    reg.ElevAvg = elev / land; reg.ElevMax = emax; reg.Temperature = temp / land; reg.Rain = rain / land;
                }
                int dom = 0;
                for (int k = 0; k < reg.BiomeCells.Length; k++) if (reg.BiomeCells[k] > reg.BiomeCells[dom]) dom = k;
                reg.Dominant = (Biome)dom;
                reg.Relief = reg.Mountain >= 0.30 || reg.ElevMax >= 2800 ? Relief.Alpine
                    : reg.Mountain >= 0.08 || reg.ElevAvg >= 650 || (reg.BiomeCells[(int)Biome.Highlands] + reg.BiomeCells[(int)Biome.AlpineMountains]) >= 0.3 * cells ? Relief.Highland
                    : reg.ElevAvg >= 220 || reg.BiomeCells[(int)Biome.Highlands] >= 0.08 * cells ? Relief.Upland : Relief.Lowland;
                reg.Name = UniqueName(names, r, 0);
                if (reg.IsLand && best != null) { best.Name = UniqueName(names, r, 1); reg.Site = best; }
            }
        }

        // 7. the home region: a good place to begin (fertile, wooded, a river, a mild climate), mostly land, matching the city's relief if any
        // region does, and near the middle of the world
        const int HomeRadius = 6;   // regions from the centre

        void ChooseHome()
        {
            int best = -1; double bestScore = double.MinValue;
            for (int pass = 0; pass < 4 && best < 0; pass++)
                foreach (var reg in RegionList)
                {
                    if (reg.Site == null) continue;
                    double dx = reg.Col - (Regions - 1) / 2.0, dy = reg.Row - (Regions - 1) / 2.0, d = Math.Sqrt(dx * dx + dy * dy);
                    if (pass <= 2 && reg.Land < 0.7) continue;
                    if (pass <= 1 && reg.Relief != HomeRelief) continue;
                    if (pass == 0 && d > HomeRadius) continue;
                    double mild = 1 - Math.Min(1, Math.Abs(reg.Temperature - 11) / 12);
                    double score = reg.Fertile * 1.5 + reg.Forest * 0.4 + Math.Min(1, reg.RiverKm / 30.0) * 0.3 + mild * 0.8 + (reg.Mountain < 0.1 ? 0.2 : 0) - d * 0.05;
                    if (score > bestScore) { bestScore = score; best = reg.Id; }
                }
            HomeRegion = best;
        }

        // ---------- Names ----------
        static readonly string[] Onset =
        {
            "Al", "Bran", "Cal", "Dor", "El", "Fen", "Gal", "Hal", "Isk", "Jor", "Kel", "Lor", "Mar", "Nor", "Ost", "Pel", "Quar", "Rav", "Sel", "Tor", "Ul", "Val",
            "Wen", "Yar", "Zan", "Ash", "Bel", "Cor", "Dun", "Eir", "Far", "Gwen", "Har", "Ith", "Kar", "Lin", "Mor", "Nev", "Ors", "Pen", "Ren", "Sab", "Tam", "Vor", "Wyr",
        };
        static readonly string[] Middle = { "", "", "a", "e", "o", "an", "el", "ar", "in", "or" };
        static readonly string[] Coda =
        {
            "dun", "wick", "mere", "ford", "stead", "holm", "ton", "by", "gard", "burg", "vale", "moor", "wen", "ia", "land", "mark", "combe", "thorpe", "ness", "reach", "ham", "stan",
        };

        string UniqueName(HashSet<string> used, int id, int kind)
        {
            for (int t = 0; t < 40; t++)
            {
                int s = Seed + 9000 + kind * 13 + t * 977;
                string name = Onset[(int)(Noise.Hash2(id, 1, s) * Onset.Length)] + Middle[(int)(Noise.Hash2(id, 2, s) * Middle.Length)] + Coda[(int)(Noise.Hash2(id, 3, s) * Coda.Length)];
                if (used.Add(name)) return name;
            }
            string fallback = "Region " + id; used.Add(fallback); return fallback;
        }

        // ---------- A binary min-heap of cells keyed by height ----------
        sealed class MinHeap
        {
            readonly float[] _key; readonly int[] _cell; int _n;
            public MinHeap(int capacity) { _key = new float[capacity]; _cell = new int[capacity]; }
            public int Count => _n;
            public void Push(float key, int cell)
            {
                int i = _n++;
                while (i > 0) { int p = (i - 1) >> 1; if (_key[p] <= key) break; _key[i] = _key[p]; _cell[i] = _cell[p]; i = p; }
                _key[i] = key; _cell[i] = cell;
            }
            public int Pop()
            {
                int top = _cell[0]; _n--;
                if (_n > 0)
                {
                    float key = _key[_n]; int cell = _cell[_n]; int i = 0;
                    while (true)
                    {
                        int c = 2 * i + 1; if (c >= _n) break;
                        if (c + 1 < _n && _key[c + 1] < _key[c]) c++;
                        if (_key[c] >= key) break;
                        _key[i] = _key[c]; _cell[i] = _cell[c]; i = c;
                    }
                    _key[i] = key; _cell[i] = cell;
                }
                return top;
            }
        }
    }
}
