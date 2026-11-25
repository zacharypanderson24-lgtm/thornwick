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

        /// <summary>Why this tile cannot be built on because of water, or null. Open water of any depth blocks ordinary buildings, zones, roads
        /// and walls (specialised ones, such as bridges and docks, will ask here too); marsh blocks buildings and zones but not roads.</summary>
        public string WaterBlock(int x, int y, bool buildingOrZone)
        {
            if (Waters == null || !Inb(x, y)) return null;
            switch (Waters.Class[y * N + x])
            {
                case WaterClass.None: return null;
                case WaterClass.Wetland: return buildingOrZone ? "Cannot construct here: marsh. The ground is too wet to build on" : null;
                case WaterClass.Stream: case WaterClass.SmallRiver: case WaterClass.MajorRiver: case WaterClass.Estuary: return "Cannot construct here: river water";
                case WaterClass.Pond: case WaterClass.Lake: return "Cannot construct here: lake water";
                default: return "Cannot construct here: sea water";
            }
        }

        public static string WaterClassName(WaterClass c) => c switch
        {
            WaterClass.Stream => "Stream", WaterClass.SmallRiver => "River", WaterClass.MajorRiver => "Major river", WaterClass.Pond => "Pond", WaterClass.Lake => "Lake",
            WaterClass.Ocean => "Ocean", WaterClass.Sea => "Sea", WaterClass.Estuary => "Estuary", WaterClass.Wetland => "Marsh", _ => "Land",
        };

        /// <summary>What the player may read about a water tile (and a bank or marsh tile): only values the simulation really has. Empty on dry
        /// ground away from water. Navigability describes the water's depth only: there are no boats yet.</summary>
        public System.Collections.Generic.List<string> DescribeWater(int x, int y)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (Waters == null || !Inb(x, y)) return lines;
            int i = y * N + x; var cl = Waters.Class[i];
            if (cl == WaterClass.Wetland) { lines.Add("MARSH"); lines.Add("Saturated ground beside fresh water. You can walk through it, but nothing can be built on it."); return lines; }
            if (cl == WaterClass.None)
            {
                if ((Waters.Flags[i] & WaterFlags.Bank) != 0) lines.Add("Bank: open water lies beside this tile.");
                return lines;
            }
            lines.Add(WaterClassName(cl).ToUpper());
            int sal = Waters.Salinity[i];
            lines.Add("Water type: " + (sal >= 200 ? "Salt water" : sal >= 40 ? "Brackish (fresh and salt mixed)" : "Fresh water"));
            lines.Add("Depth: " + Waters.Depth[i]);
            if (SurfaceWater.River(cl) && Waters.Flow[i] != 0)
            {
                int st = Waters.Strength[i];
                lines.Add("Flow direction: " + SurfaceWater.DirName[Waters.Flow[i]]);
                lines.Add("Current: " + (st < 90 ? "Gentle" : st < 170 ? "Moderate" : "Strong") + (Waters.Width[i] > 0 ? $"   Width: about {Waters.Width[i]} tile{(Waters.Width[i] == 1 ? "" : "s")}" : ""));
            }
            else if (SurfaceWater.Standing(cl)) lines.Add("Still water: no current");
            lines.Add("Surface level: " + Waters.Surface[i]);
            var body = Waters.BodyAt(x, y); if (body != null) lines.Add($"This water covers {body.Tiles} tiles" + (body.TouchesEdge ? " and runs off the edge of the map" : ""));
            lines.Add(IsBridge(x, y) ? "Land movement: open: a wooden bridge crosses here" : IceBears(x, y) ? "Land movement: open: the water is frozen over" : StreamDry(x, y) ? "Land movement: open: the stream bed is dry" : "Land movement: blocked. Only a bridge crosses water (Water menu, Bridge)");
            if (SeasonNote(x, y) is string season) lines.Add(season);
            double fish = FishRate(cl);
            lines.Add("Fishing suitability: " + FishLevel(fish) + (fish > 0 ? " (a fishing hut within a few tiles of the shore can fish here)" : ""));
            lines.Add("Navigability: " + (Waters.Depth[i] == WaterDepth.Shallow ? "too shallow for boats" : Waters.Depth[i] == WaterDepth.Medium ? "deep enough for small boats" : "deep enough for large vessels") + " (there are no boats yet)");
            return lines;
        }

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
