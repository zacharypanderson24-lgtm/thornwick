// Surface water in the world (DESIGN_WATER.md): rivers, lakes, ponds, seas and their shores. This is not the underground
// drinking-water system (World.Water.cs: aquifers, wells, cisterns), which is unchanged.
//
// A world has water only when it is created with a WaterPreset; every older world and every save without a "water" entry stays dry,
// with exactly the terrain it always had. The layer is generated once (Hydrology.Generate), saved as it is, and never recomputed.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        readonly WaterPreset _waterPreset;

        /// <summary>The surface-water layer, or null on a dry map.</summary>
        public SurfaceWater Waters { get; private set; }

        public WaterPreset WaterKind => Waters?.Preset ?? WaterPreset.None;

        /// <summary>True when the tile is open water (river, lake, pond, sea). Wet ground (wetland) is not water.</summary>
        public bool IsWaterTile(int x, int y) => Waters != null && Waters.IsWater(x, y);

        /// <summary>Cut the water into the terrain: returns the new vertex heights.</summary>
        int[] GenSurfaceWater(int[] dry)
        {
            var result = Hydrology.Generate(Seed, _waterPreset, dry, N, Start);
            Waters = result.Water;
            return result.Hv;
        }

        /// <summary>Fertility from fresh water (DESIGN_WATER.md section 20, first step): low land within three tiles of fresh water is fertile
        /// (a river valley); sea water gives nothing, and ground right behind a beach stays poor. Marsh is too wet to farm.</summary>
        void ApplyWaterFertility()
        {
            var w = Waters;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    if (w.Class[i] == WaterClass.Wetland) { Fert[y, x] = false; continue; }
                    if (w.Class[i] != WaterClass.None) continue;
                    bool fresh = false, salt = false;
                    for (int dy = -3; dy <= 3; dy++)
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int nx = x + dx, ny = y + dy; if (!Inb(nx, ny)) continue;
                            int j = ny * N + nx; var cl = w.Class[j]; if (!SurfaceWater.OpenWater(cl)) continue;
                            if (SurfaceWater.Salty(cl) || w.Salinity[j] > 80) { if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 2) salt = true; }
                            else fresh = true;
                        }
                    if (salt) Fert[y, x] = false;
                    else if (fresh && TileLevel(x, y) <= 1.5 && TileSpread(x, y) <= 1) Fert[y, x] = true;
                }
        }
    }
}
