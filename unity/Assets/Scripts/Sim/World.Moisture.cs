// Soil moisture from surface water (DESIGN_WATER.md section 20). Derived from the water layer once, never saved.
//
// No universal bonus around every water tile: a tile's moisture starts from the region's rainfall, gains from FRESH water within a few tiles
// (less the further away, and none from water that lies well below the land, which cannot be drawn up), and loses to salt water within two
// tiles. Marsh is saturated. Land just above a big river and not much higher than it can flood (the risk is stored; floods are not simulated yet).
// Dry (no-water) worlds have no moisture layer at all: every farm field there yields exactly what it always did.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int MoistReach = 8;
        public const double TooDry = 0.15;

        byte[] _moist; bool[] _flood;

        /// <summary>The region's own moisture before any water is near: from the preset's rainfall (arid 0.12, temperate about 0.4).</summary>
        public double BaseMoisture => Waters == null ? 0.4 : Math.Clamp(0.4 * HydroParams.For(Waters.Preset).Rainfall, 0.1, 0.6);

        void BuildMoisture()
        {
            var w = Waters; int nn = N * N;
            _moist = new byte[nn]; _flood = new bool[nn];
            var dist = new int[nn]; var level = new short[nn]; var q = new Queue<int>();
            for (int i = 0; i < nn; i++) dist[i] = -1;
            for (int i = 0; i < nn; i++)
            {
                var cl = w.Class[i];
                bool fresh = cl == WaterClass.Wetland || (SurfaceWater.OpenWater(cl) && !SurfaceWater.Salty(cl) && w.Salinity[i] < 40);
                if (!fresh) continue;
                dist[i] = 0; level[i] = w.Surface[i]; q.Enqueue(i);
            }
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % N, y = c / N;
                if (dist[c] >= MoistReach) continue;
                for (int k = 1; k <= 8; k++)
                {
                    int nx = x + SurfaceWater.Dx[k], ny = y + SurfaceWater.Dy[k]; if (!Inb(nx, ny)) continue;
                    int ni = ny * N + nx; if (dist[ni] >= 0) continue;
                    dist[ni] = dist[c] + 1; level[ni] = level[c]; q.Enqueue(ni);
                }
            }
            double baseM = BaseMoisture;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x; var cl = w.Class[i];
                    if (SurfaceWater.OpenWater(cl) || cl == WaterClass.Wetland) { _moist[i] = 255; continue; }
                    double m = baseM;
                    if (dist[i] > 0)
                    {
                        double near = Math.Max(0, 1 - (dist[i] - 1) / (double)MoistReach);
                        double rise = TileLevel(x, y) - level[i];
                        if (rise > 2) near *= Math.Max(0, 1 - (rise - 2) / 3);   // water far below the land cannot be drawn up
                        m += 0.5 * near;
                    }
                    bool salt = false;
                    for (int dy = -2; dy <= 2 && !salt; dy++) for (int dx = -2; dx <= 2; dx++) { int nx = x + dx, ny = y + dy; if (Inb(nx, ny) && SurfaceWater.Salty(w.Class[ny * N + nx])) { salt = true; break; } }
                    if (salt) m -= 0.3;
                    _moist[i] = (byte)Math.Round(Math.Clamp(m, 0, 1) * 255);
                    // flood plain: within two tiles of a real river and not much above it
                    if (dist[i] > 0 && dist[i] <= 2 && TileLevel(x, y) - level[i] <= 1)
                        for (int dy = -2; dy <= 2 && !_flood[i]; dy++) for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy; if (!Inb(nx, ny)) continue; int j = ny * N + nx;
                            if (SurfaceWater.River(w.Class[j]) && w.Width[j] >= 3 && w.Salinity[j] < 40) { _flood[i] = true; break; }
                        }
                }
        }

        /// <summary>Soil moisture 0 (bone dry) to 1 (saturated). 0.4 on dry-world maps (there is no moisture layer there).</summary>
        public double SoilMoisture(int x, int y)
        {
            if (Waters == null || !Inb(x, y)) return 0.4;
            if (_moist == null) BuildMoisture();
            double m = _moist[y * N + x] / 255.0;
            return Waters.Class[y * N + x] == WaterClass.None ? Math.Min(1.0, m + IrrigationBonus(x, y)) : m;   // canals add to the soil's own moisture
        }

        /// <summary>Low land beside a real river: floods will come here when seasonal floods exist. Stored now, not simulated yet.</summary>
        public bool FloodPlain(int x, int y)
        {
            if (Waters == null || !Inb(x, y)) return false;
            if (_moist == null) BuildMoisture();
            return _flood[y * N + x];
        }

        public static string MoistureWord(double m) => m < TooDry ? "Parched" : m < 0.35 ? "Dry" : m < 0.55 ? "Fair" : m < 0.8 ? "Moist" : "Wet";

        /// <summary>How much a farm field on this tile yields compared with ordinary ground: 1 on a dry-world map, and on a water map from 0.55
        /// (parched) through 1 (fair) to 1.3 (well watered). Marsh is too wet and gives the least (it cannot be farmed anyway).</summary>
        public double MoistureYield(int x, int y)
        {
            if (Waters == null) return 1.0;
            double f = Math.Clamp(0.55 + 0.9 * SoilMoisture(x, y), 0.55, 1.3);
            if (SiltThisYear && FloodPlain(x, y)) f *= SiltYield;   // World.WaterSeasons.cs: a flood year leaves silt
            if (DroughtOn) f *= DroughtYield;
            return f;
        }

        /// <summary>Why this farm tile cannot be farmed for want of water, or null.</summary>
        public string TooDryReason(int x, int y) =>
            Waters != null && SoilMoisture(x, y) < TooDry ? "Too dry to farm: little rain here and no fresh water near. Farmland needs fresh water within a few tiles in dry country" : null;
    }
}
