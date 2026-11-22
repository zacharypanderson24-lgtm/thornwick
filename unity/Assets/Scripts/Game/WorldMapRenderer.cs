// Draws the world map (Sim/WorldMap.cs) into textures: terrain (biome colours, relief shading, rivers), a heat map (temperature, cold at the
// top, hot at the bottom), region borders, and city sites. One pixel per strategic cell; the texture's row 0 is the BOTTOM of the picture,
// so cell row y (0 = north) is written to pixel row Size - 1 - y. Point filtering keeps cells crisp when zoomed.
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public static class WorldMapRenderer
    {
        const int S = WorldMap.Size;

        static readonly Color32[] BiomeColors =
        {
            new Color32(34, 78, 130, 255),    // ocean (replaced by the depth gradient)
            new Color32(66, 128, 192, 255),   // lake
            new Color32(143, 176, 92, 255),   // temperate grassland
            new Color32(72, 128, 70, 255),    // temperate forest
            new Color32(52, 96, 84, 255),     // boreal forest
            new Color32(150, 142, 98, 255),   // highlands and moorland
            new Color32(152, 152, 158, 255),  // alpine mountains
            new Color32(96, 138, 120, 255),   // wetlands
            new Color32(170, 160, 92, 255),   // mediterranean woodland
            new Color32(192, 178, 112, 255),  // steppe
            new Color32(198, 172, 122, 255),  // semi-arid scrub
            new Color32(176, 188, 180, 255),  // tundra
            new Color32(224, 198, 132, 255),  // desert
            new Color32(194, 178, 84, 255),   // savanna
            new Color32(60, 140, 84, 255),    // subtropical forest
            new Color32(32, 108, 62, 255),    // tropical rainforest
            new Color32(90, 142, 62, 255),    // tropical seasonal forest
            new Color32(114, 150, 102, 255),  // tropical highlands
        };

        public static readonly Color32 River = new Color32(70, 132, 204, 255), SiteWhite = new Color32(246, 240, 224, 255), HomeGold = new Color32(255, 196, 40, 255);
        public static Color32 ColorOf(Biome b) => BiomeColors[(int)b];

        /// <summary>The heat-map colour for a mean annual temperature in degrees C: blue (cold) through green and yellow to red (hot).</summary>
        public static Color32 HeatColor(float t)
        {
            float[] stops = { -15, -5, 0, 5, 10, 15, 20, 25, 30, 34 };
            Color32[] cols =
            {
                new Color32(44, 64, 150, 255), new Color32(70, 130, 210, 255), new Color32(122, 192, 232, 255), new Color32(150, 214, 172, 255),
                new Color32(192, 226, 124, 255), new Color32(240, 226, 104, 255), new Color32(250, 182, 72, 255), new Color32(240, 122, 52, 255),
                new Color32(206, 52, 42, 255), new Color32(140, 22, 42, 255),
            };
            if (t <= stops[0]) return cols[0];
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i]) return Color32.Lerp(cols[i - 1], cols[i], (t - stops[i - 1]) / (stops[i] - stops[i - 1]));
            return cols[cols.Length - 1];
        }

        static Texture2D NewTexture(Color32[] pixels)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "WorldMap" };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static int Px(int x, int y) => (S - 1 - y) * S + x;

        static float Elev(WorldMap m, int x, int y) => Mathf.Max(0, m.Elevation[WorldMap.Cell(Mathf.Clamp(x, 0, S - 1), Mathf.Clamp(y, 0, S - 1))]);

        /// <summary>Relief shading: light from the north-west, so slopes facing it are brighter.</summary>
        static float Shade(WorldMap m, int x, int y)
        {
            float dx = Elev(m, x + 1, y) - Elev(m, x - 1, y), dy = Elev(m, x, y + 1) - Elev(m, x, y - 1);
            return Mathf.Clamp(1f + 0.0022f * (-dx - dy), 0.72f, 1.28f);
        }

        static bool TouchesLand(WorldMap m, int x, int y)
        {
            return (x > 0 && m.Elevation[WorldMap.Cell(x - 1, y)] > 0) || (x < S - 1 && m.Elevation[WorldMap.Cell(x + 1, y)] > 0)
                || (y > 0 && m.Elevation[WorldMap.Cell(x, y - 1)] > 0) || (y < S - 1 && m.Elevation[WorldMap.Cell(x, y + 1)] > 0);
        }

        static Color32 Scale(Color32 c, float k) => new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);

        /// <summary>Biome colours with relief shading, snow on the highest peaks, shallow-to-deep sea, lakes and rivers.</summary>
        public static Texture2D BuildTerrain(WorldMap m)
        {
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = WorldMap.Cell(x, y); var b = m.Biomes[i]; Color32 c;
                    if (b == Biome.Ocean)
                    {
                        float depth = Mathf.Clamp01(-m.Elevation[i] / 2200f);
                        c = Color32.Lerp(new Color32(50, 110, 164, 255), new Color32(22, 54, 100, 255), depth);
                        if (TouchesLand(m, x, y)) c = Color32.Lerp(c, new Color32(122, 180, 210, 255), 0.55f);
                    }
                    else if (b == Biome.Lake) c = BiomeColors[(int)Biome.Lake];
                    else
                    {
                        c = Scale(BiomeColors[(int)b], Shade(m, x, y));
                        float snow = Mathf.Clamp01((m.Elevation[i] - 2300f) / 700f) * 0.85f;
                        if (snow > 0) c = Color32.Lerp(c, new Color32(240, 242, 246, 255), snow);
                    }
                    px[Px(x, y)] = c;
                }
            // rivers: one cell wide, major rivers two
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var f = m.Features[WorldMap.Cell(x, y)];
                    if ((f & MapFeature.River) == 0) continue;
                    px[Px(x, y)] = River;
                    if ((f & MapFeature.MajorRiver) != 0)
                        foreach (var (nx, ny) in new[] { (x + 1, y), (x, y + 1) })
                            if (nx < S && ny < S && m.Biomes[WorldMap.Cell(nx, ny)] != Biome.Ocean && m.Biomes[WorldMap.Cell(nx, ny)] != Biome.Lake) px[Px(nx, ny)] = River;
                }
            return NewTexture(px);
        }

        /// <summary>Mean annual temperature as colour, over land and sea, with a faint relief and a darker coast so the shapes still read.</summary>
        public static Texture2D BuildHeat(WorldMap m)
        {
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int i = WorldMap.Cell(x, y);
                    var c = HeatColor(m.Temperature[i]);
                    if (m.Elevation[i] > 0)
                    {
                        c = Scale(c, 0.88f + 0.12f * Shade(m, x, y));
                        if (!(x > 0 && x < S - 1 && y > 0 && y < S - 1) || !AllLand(m, x, y)) c = Scale(c, 0.68f);
                    }
                    else c = Scale(c, 0.9f);
                    px[Px(x, y)] = c;
                }
            return NewTexture(px);
        }

        static bool AllLand(WorldMap m, int x, int y) =>
            m.Elevation[WorldMap.Cell(x - 1, y)] > 0 && m.Elevation[WorldMap.Cell(x + 1, y)] > 0 && m.Elevation[WorldMap.Cell(x, y - 1)] > 0 && m.Elevation[WorldMap.Cell(x, y + 1)] > 0;

        /// <summary>Faint lines at the region borders (the first cell row and column of every 25 x 25 region), transparent elsewhere.</summary>
        public static Texture2D BuildBorders()
        {
            var px = new Color32[S * S]; var line = new Color32(8, 12, 24, 78);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (x % WorldMap.RegionCells == 0 || y % WorldMap.RegionCells == 0) px[Px(x, y)] = line;
            return NewTexture(px);
        }

        /// <summary>City sites as small dark-outlined dots (white), and the player's settlement as a larger gold marker.</summary>
        public static readonly Color32 WarbandOrange = new Color32(255, 140, 30, 255);

        /// <summary>City sites, camps and your settlement; a spotted warband (warband = where it is now, home = where it is going) is drawn
        /// as an orange marker with a dashed line along the rest of its road.</summary>
        public static Texture2D BuildSites(WorldMap m, System.Collections.Generic.IReadOnlyList<RaiderCamp> camps = null, System.Collections.Generic.ISet<int> destroyed = null,
            (double x, double y)? warband = null, (int x, int y)? home = null)
        {
            var px = new Color32[S * S]; var outline = new Color32(10, 10, 16, 210);
            void Square(int cx, int cy, int r, Color32 c)
            {
                for (int y = cy - r; y <= cy + r; y++) for (int x = cx - r; x <= cx + r; x++) if (WorldMap.Inb(x, y)) px[Px(x, y)] = c;
            }
            foreach (var reg in m.RegionList)
            {
                if (reg.Site == null || reg.Id == m.HomeRegion) continue;
                Square(reg.Site.X, reg.Site.Y, 2, outline); Square(reg.Site.X, reg.Site.Y, 1, SiteWhite);
            }
            if (camps != null)
                foreach (var c in camps)
                {
                    bool dead = destroyed != null && destroyed.Contains(c.Id);
                    Square(c.X, c.Y, 3, outline);
                    Square(c.X, c.Y, 2, dead ? new Color32(110, 110, 116, 255) : new Color32(226, 56, 46, 255));
                }
            if (warband.HasValue && home.HasValue)
            {
                double wx = warband.Value.x, wy = warband.Value.y, dx = home.Value.x - wx, dy = home.Value.y - wy;
                int steps = System.Math.Max(1, (int)System.Math.Ceiling(System.Math.Sqrt(dx * dx + dy * dy)));
                for (int i = 0; i <= steps; i++)
                    if (i % 6 < 3) Square((int)System.Math.Round(wx + dx * i / steps), (int)System.Math.Round(wy + dy * i / steps), 0, WarbandOrange);
            }
            if (m.HomeSite != null)
            {
                Square(m.HomeSite.X, m.HomeSite.Y, 5, outline); Square(m.HomeSite.X, m.HomeSite.Y, 4, HomeGold);
                Square(m.HomeSite.X, m.HomeSite.Y, 1, outline);
            }
            if (warband.HasValue)
            {
                int x = (int)System.Math.Round(warband.Value.x), y = (int)System.Math.Round(warband.Value.y);
                Square(x, y, 4, outline); Square(x, y, 3, WarbandOrange); Square(x, y, 1, outline);
            }
            return NewTexture(px);
        }
    }
}
