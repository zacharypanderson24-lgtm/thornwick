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
        public const int PlankAlongX = 16, PlankAlongY = 17;   // bridge decks (code-drawn): planks across a bridge that runs along the view's x or y axis
        public const int CellCount = 18, Ticks = 16;
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
            for (int cell = 0; cell < PlankAlongX; cell++)
                for (int t = 0; t < Ticks; t++)
                {
                    bool still = cell < OceanCoastal;
                    int frame = still ? (t / 2) % 8 : t % 8;
                    var sprite = Frame(cell, frame); if (sprite == null) continue;
                    Graphics.Blit(sprite.texture, rt);
                    RenderTexture.active = rt; atlas[t].ReadPixels(new Rect(0, 0, 32, 16), cell * 32, 0);
                }
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            foreach (var a in atlas) { a.SetPixels32(PlankAlongX * 32, 0, 32, 16, Plank(true)); a.SetPixels32(PlankAlongY * 32, 0, 32, 16, Plank(false)); }
            foreach (var a in atlas) a.Apply(false);
            return _shared = atlas;
        }

        /// <summary>A wooden deck on one tile: boards across the bridge, a rail along each side. Drawn in code (no bridge art exists yet).</summary>
        static Color32[] Plank(bool alongX)
        {
            var px = new Color32[32 * 16];
            Color32 light = new Color32(176, 130, 74, 255), dark = new Color32(112, 78, 44, 255), rail = new Color32(78, 52, 32, 255), railTop = new Color32(138, 100, 58, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 32; x++)
                {
                    float sx = x + 0.5f - 16f, sy = (15 - y) + 0.5f - 8f;   // screen offset from the tile centre, y down
                    float u = (sx / 16f + sy / 8f + 1f) / 2f, v = (sy / 8f - sx / 16f + 1f) / 2f;
                    if (Mathf.Abs(sx) / 16f + Mathf.Abs(sy) / 8f > 1f) continue;
                    float along = alongX ? u : v, across = alongX ? v : u;
                    Color32 c = (int)(along * 8f) % 2 == 0 ? light : dark;
                    if (across < 0.12f || across > 0.88f) c = across < 0.06f || across > 0.94f ? railTop : rail;
                    px[y * 32 + x] = c;
                }
            return px;
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
                        bool canal = w.IsCanal(x, y);
                        if (!w.InPlay(x, y) || ((!sw.IsWater(x, y) || w.StreamDry(x, y)) && !canal)) continue;
                        int cell = canal ? StillShallow : CellFor(sw, x, y, rot);
                        int i0 = _v.Count;
                        _v.Add(IsoView.VertexPos(vx, vy, IsoView.VhView(w, vx, vy, rot)));
                        _v.Add(IsoView.VertexPos(vx + 1, vy, IsoView.VhView(w, vx + 1, vy, rot)));
                        _v.Add(IsoView.VertexPos(vx + 1, vy + 1, IsoView.VhView(w, vx + 1, vy + 1, rot)));
                        _v.Add(IsoView.VertexPos(vx, vy + 1, IsoView.VhView(w, vx, vy + 1, rot)));
                        float u0 = cell * cw + inU, u1 = (cell + 1) * cw - inU, um = (cell + 0.5f) * cw;
                        _uv.Add(new Vector2(um, 1 - inV)); _uv.Add(new Vector2(u1, 0.5f)); _uv.Add(new Vector2(um, inV)); _uv.Add(new Vector2(u0, 0.5f));
                        byte shade = w.Claimed(x, y) ? (byte)255 : (byte)190;   // water in land you do not own yet is dimmer, like the ground
                        var col = new Color32(shade, shade, (byte)Mathf.Min(255, shade + 10), 255);
                        if (canal) col = w.CanalIsFed(x, y) ? new Color32((byte)(shade * 0.7f), (byte)(shade * 0.85f), shade, 255) : new Color32((byte)(shade * 0.55f), (byte)(shade * 0.5f), (byte)(shade * 0.42f), 255);   // a dry (unconnected) canal is mud
                        if (w.IceBears(x, y)) col = new Color32((byte)(shade * 0.86f), (byte)(shade * 0.95f), shade, 255);   // frozen: pale blue-white
                        _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                        _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
                        if (w.IsBridge(x, y)) AddDeck(w, x, y, rot, i0, col);
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

        // a bridge tile: the same diamond again with the deck texture, drawn after the water (so over it)
        void AddDeck(World w, int x, int y, int rot, int water, Color32 col)
        {
            bool Road(int a, int b) => World.Inb(a, b) && w.Grid[b, a]?.T == "road";
            bool worldX = Road(x - 1, y) || Road(x + 1, y);
            bool viewX = (rot % 2 == 0) == worldX;   // a world x bridge runs along the view x axis in rotations 0 and 2
            int cell = viewX ? PlankAlongX : PlankAlongY;
            float cw = 1f / CellCount, inU = 0.5f / (CellCount * 32f), inV = 0.5f / 16f;
            int i0 = _v.Count;
            for (int k = 0; k < 4; k++) _v.Add(_v[water + k]);
            float u0 = cell * cw + inU, u1 = (cell + 1) * cw - inU, um = (cell + 0.5f) * cw;
            _uv.Add(new Vector2(um, 1 - inV)); _uv.Add(new Vector2(u1, 0.5f)); _uv.Add(new Vector2(um, inV)); _uv.Add(new Vector2(u0, 0.5f));
            for (int k = 0; k < 4; k++) _c.Add(col);
            _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
        }

        void Update()
        {
            if (_atlas == null || _mat == null) return;
            int tick = (int)(Time.time * TicksPerSecond) % Ticks;
            if (tick == _tick) return;
            _tick = tick; _mat.mainTexture = _atlas[tick];
        }
    }
}
