// Surface water drawn as one mesh (DESIGN_WATER.md sections 13 and 14): a ground-shaped diamond per water tile, textured from the approved
// eight-frame water art (Resources/Art/Water, the sets WaterArt plays). No GameObject per tile; the animation swaps the mesh's atlas texture.
//
// Directions are world-space: a river tile's art is chosen from the flow direction plus the camera rotation, and the mesh is rebuilt when the
// camera turns (as the ground is), so a current never appears to reverse or spin when you rotate (WaterArt: (worldDirection + rotation) % 4).
//
// The shore: a land tile beside water is tinted (mud, sand or rock by biome and by sea or fresh water) and marsh is drawn dark green. These
// tints are placeholders until shoreline transition tiles exist (see DESIGN_WATER.md, art needs).
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrownAndHearth.Game
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterRenderer : MonoBehaviour
    {
        // cells of the water atlas: one cell per art set
        public const int StillShallow = 0, StillDeep = 2, OceanCoastal = 4, OceanDeep = 6, RiverEast = 8;   // each has two variants; rivers: 8 + direction * 2 + variant
        public const int CellCount = 16, Ticks = 16;
        public const float TicksPerSecond = 8f;

        Mesh _mesh; Texture2D[] _atlas; Material _mat; int _tick = -1;
        readonly List<Vector3> _v = new List<Vector3>(); readonly List<Vector2> _uv = new List<Vector2>(); readonly List<Color32> _c = new List<Color32>(); readonly List<int> _t = new List<int>();

        public void Init(Material mat)
        {
            _mesh = new Mesh { name = "Water", indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            _mat = new Material(mat) { name = "Water (instance)" }; mr.sharedMaterial = _mat;
            mr.sortingOrder = short.MinValue + 1;   // above the ground, below everything standing on it
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        // ---------- which art a tile uses (pure, tested) ----------
        /// <summary>The atlas cell of a water tile for a camera rotation: still water for lakes and ponds, coastal or deep ocean, river art by direction.</summary>
        public static int CellFor(SurfaceWater w, int x, int y, int rot)
        {
            int i = w.Idx(x, y); var cl = w.Class[i]; var depth = w.Depth[i];
            int variant = SurfaceWater.River(cl) ? 0 : Noise.Hash2(x >> 1, y >> 1, w.Seed + 91) < 0.5 ? 0 : 1;   // rivers keep one pattern so the current flows unbroken
            bool salty = SurfaceWater.Salty(cl) || (cl == WaterClass.Estuary && w.Salinity[i] > 128);
            if (salty) return (depth == WaterDepth.Shallow ? OceanCoastal : OceanDeep) + variant;
            if (SurfaceWater.River(cl) && w.Flow[i] != 0)
            {
                // the current of a wide river is the commonest direction among the river tiles round it, so a diagonal run does not flip between two arts
                var votes = new int[4];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!w.Inb(x + dx, y + dy)) continue;
                        int j = w.Idx(x + dx, y + dy);
                        if (SurfaceWater.River(w.Class[j]) && w.Flow[j] != 0) votes[SurfaceWater.ArtDirection(w.Flow[j])] += dx == 0 && dy == 0 ? 2 : 1;
                    }
                int world = SurfaceWater.ArtDirection(w.Flow[i]);
                for (int k = 0; k < 4; k++) if (votes[k] > votes[world]) world = k;
                int dir = (world + rot) % 4;
                return RiverEast + dir * 2 + variant;
            }
            return (depth == WaterDepth.Shallow ? StillShallow : StillDeep) + variant;
        }

        /// <summary>Placeholder shore tint of a dry tile beside water, multiplied into the ground colour; null when it is not on a shore.</summary>
        public static Color32? ShoreTint(World w, int x, int y)
        {
            var sw = w.Waters; if (sw == null) return null;
            int i = y * Defs.N + x; var cl = sw.Class[i];
            if (cl == WaterClass.Wetland) return new Color32(150, 190, 150, 255);   // reeds and wet ground
            if (cl != WaterClass.None || (sw.Flags[i] & WaterFlags.Bank) == 0) return null;
            bool sea = false;
            for (int dy = -1; dy <= 1 && !sea; dy++) for (int dx = -1; dx <= 1; dx++) if (sw.Inb(x + dx, y + dy) && SurfaceWater.Salty(sw.Class[(y + dy) * Defs.N + x + dx])) { sea = true; break; }
            var biome = BiomeArt.For(w);
            bool dry = biome == Biome.Desert || biome == Biome.SemiAridScrub || biome == Biome.Savanna || biome == Biome.MediterraneanScrub;
            bool rocky = biome == Biome.AlpineMountains || biome == Biome.Highlands || w.TileSpread(x, y) >= 1;
            if (sea || dry) return new Color32(255, 240, 190, 255);       // sand
            if (rocky) return new Color32(205, 210, 222, 255);            // pale rock
            return new Color32(215, 200, 165, 255);                       // mud
        }

        // ---------- the atlases: 16 art sets side by side, one atlas per animation tick ----------
        static Texture2D[] _shared;

        static Texture2D[] BuildAtlases()
        {
            if (_shared != null && _shared[0] != null) return _shared;
            var atlas = new Texture2D[Ticks];
            for (int t = 0; t < Ticks; t++) { atlas[t] = new Texture2D(32 * CellCount, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "WaterAtlas" + t }; Clear(atlas[t]); }
            var rt = RenderTexture.GetTemporary(32, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            for (int cell = 0; cell < CellCount; cell++)
                for (int t = 0; t < Ticks; t++)
                {
                    bool still = cell < OceanCoastal;
                    int frame = still ? (t / 2) % 8 : t % 8;
                    var sprite = Frame(cell, frame); if (sprite == null) continue;
                    Graphics.Blit(sprite.texture, rt);
                    RenderTexture.active = rt; atlas[t].ReadPixels(new Rect(0, 0, 32, 16), cell * 32, 0);
                }
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            foreach (var a in atlas) a.Apply(false);
            return _shared = atlas;
        }

        static void Clear(Texture2D t) { var px = new Color32[t.width * t.height]; t.SetPixels32(px); }

        static readonly string[] Dir = { "east", "south", "west", "north" };
        static Sprite Frame(int cell, int frame)
        {
            int variant = cell & 1, set = cell >> 1; string name;
            if (set == 0) name = "water_still_shallow"; else if (set == 1) name = "water_still_deep"; else if (set == 2) name = "water_ocean_coastal"; else if (set == 3) name = "water_ocean_deep";
            else name = "water_river_flow_" + Dir[set - 4];
            return Resources.Load<Sprite>("Art/Water/" + name + "_v" + variant + "_f" + frame);
        }

        // ---------- building the mesh ----------
        public void Build(World w, int rot, RectInt win)
        {
            _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
            var sw = w.Waters;
            if (sw != null)
            {
                float cw = 1f / CellCount, inU = 0.5f / (CellCount * 32f), inV = 0.5f / 16f;
                for (int vy = win.yMin; vy < win.yMax; vy++)
                    for (int vx = win.xMin; vx < win.xMax; vx++)
                    {
                        var wt = IsoView.FromView(vx, vy, rot); int x = wt.x, y = wt.y;
                        if (!w.InPlay(x, y) || !sw.IsWater(x, y)) continue;
                        int cell = CellFor(sw, x, y, rot);
                        int i0 = _v.Count;
                        _v.Add(IsoView.VertexPos(vx, vy, IsoView.VhView(w, vx, vy, rot)));
                        _v.Add(IsoView.VertexPos(vx + 1, vy, IsoView.VhView(w, vx + 1, vy, rot)));
                        _v.Add(IsoView.VertexPos(vx + 1, vy + 1, IsoView.VhView(w, vx + 1, vy + 1, rot)));
                        _v.Add(IsoView.VertexPos(vx, vy + 1, IsoView.VhView(w, vx, vy + 1, rot)));
                        float u0 = cell * cw + inU, u1 = (cell + 1) * cw - inU, um = (cell + 0.5f) * cw;
                        _uv.Add(new Vector2(um, 1 - inV)); _uv.Add(new Vector2(u1, 0.5f)); _uv.Add(new Vector2(um, inV)); _uv.Add(new Vector2(u0, 0.5f));
                        byte shade = w.Claimed(x, y) ? (byte)255 : (byte)190;   // water in land you do not own yet is dimmer, like the ground
                        var col = new Color32(shade, shade, (byte)Mathf.Min(255, shade + 10), 255);
                        _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                        _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
                    }
            }
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetUVs(0, _uv); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshRenderer>().enabled = _v.Count > 0;
            if (_v.Count > 0 && _atlas == null) { _atlas = BuildAtlases(); _tick = -1; _mat.mainTexture = _atlas[0]; }
        }

        /// <summary>Show animation tick t (0..15) now (the game steps it from the clock in Update).</summary>
        public void SetTick(int t) { if (_atlas != null) { _tick = t % Ticks; _mat.mainTexture = _atlas[_tick]; } }

        void Update()
        {
            if (_atlas == null || _mat == null) return;
            int tick = (int)(Time.time * TicksPerSecond) % Ticks;
            if (tick == _tick) return;
            _tick = tick; _mat.mainTexture = _atlas[tick];
        }
    }
}
