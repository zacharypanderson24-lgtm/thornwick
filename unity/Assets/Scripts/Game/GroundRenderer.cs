// The ground as one mesh: one diamond per tile, its four corners lifted to the vertex heights, textured from the ground atlas
// and shaded by slope (the browser's drape/groundLayer). Rebuilt only when the world or the camera rotation changes.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundRenderer : MonoBehaviour
    {
        static bool Unowned(World w, int x, int y) => World.Inb(x, y) && !w.Claimed(x, y);

        Mesh _mesh;
        readonly List<Vector3> _v = new List<Vector3>(N * N * 4);
        readonly List<Vector2> _uv = new List<Vector2>(N * N * 4);
        readonly List<Color32> _c = new List<Color32>(N * N * 4);
        readonly List<int> _t = new List<int>(N * N * 6);

        public void Init(Material mat, Texture2D atlas)
        {
            _mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(mat) { mainTexture = atlas, name = "Ground (instance)" };
            mr.sortingOrder = short.MinValue;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        static readonly Dictionary<Zone, Color32> ZoneTint = new Dictionary<Zone, Color32>
        {
            { Zone.Res, new Color32(255, 225, 150, 255) }, { Zone.Farm, new Color32(255, 240, 120, 255) }, { Zone.Comm, new Color32(190, 200, 255, 255) },
            { Zone.Wood, new Color32(150, 230, 150, 255) }, { Zone.Mine, new Color32(230, 185, 185, 255) },
        };

        /// <summary>Build the ground for the block of view tiles `win` only (the map is 256 x 256: the part near the camera is enough).</summary>
        public void Build(World w, int rot, RectInt win)
        {
            _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
            float cw = 1f / ArtLibrary.CellCount, inU = 0.5f / (ArtLibrary.CellCount * 32f), inV = 0.5f / 16f;
            for (int vy = win.yMin; vy < win.yMax; vy++)
                for (int vx = win.xMin; vx < win.xMax; vx++)
                {
                    var wt = IsoView.FromView(vx, vy, rot); int x = wt.x, y = wt.y;
                    if (!w.InPlay(x, y)) continue;
                    int cT = IsoView.VhView(w, vx, vy, rot), cR = IsoView.VhView(w, vx + 1, vy, rot), cL = IsoView.VhView(w, vx, vy + 1, rot), cB = IsoView.VhView(w, vx + 1, vy + 1, rot);
                    int cell = CellFor(w, x, y);

                    int i0 = _v.Count;
                    _v.Add(IsoView.VertexPos(vx, vy, cT));           // top
                    _v.Add(IsoView.VertexPos(vx + 1, vy, cR));       // right
                    _v.Add(IsoView.VertexPos(vx + 1, vy + 1, cB));   // bottom
                    _v.Add(IsoView.VertexPos(vx, vy + 1, cL));       // left
                    float u0 = cell * cw + inU, u1 = (cell + 1) * cw - inU, um = (cell + 0.5f) * cw;
                    _uv.Add(new Vector2(um, 1 - inV)); _uv.Add(new Vector2(u1, 0.5f)); _uv.Add(new Vector2(um, inV)); _uv.Add(new Vector2(u0, 0.5f));

                    // slope shading, as in the browser's drape(): lit from the top-left
                    int mn = Mathf.Min(Mathf.Min(cT, cR), Mathf.Min(cL, cB));
                    float rT = cT - mn, rR = cR - mn, rL = cL - mn, rB = cB - mn, k = rT - rR - rL + rB;
                    float zu = (rR - rT) + 0.5f * k, zv = (rL - rT) + 0.5f * k;
                    float dzdx = (zu - zv) * LvlPx / 32f, dzdy = (zu + zv) * LvlPx / 16f;
                    float f = Mathf.Clamp(1 + 0.75f * dzdx - 0.28f * dzdy, 0.62f, 1f);
                    if (w.PitDepth(x, y) > 0) f *= 0.55f;
                    Color32 tint = new Color32(255, 255, 255, 255);
                    var z = w.ZoneMap[y, x];
                    if (z != Zone.None && w.Grid[y, x] == null) tint = ZoneTint[z];
                    if (!w.Claimed(x, y)) { tint = new Color32((byte)(tint.r * 0.74f), (byte)(tint.g * 0.76f), (byte)(tint.b * 0.82f), 255); }   // land you do not own yet is dimmer
                    else if (Unowned(w, x - 1, y) || Unowned(w, x + 1, y) || Unowned(w, x, y - 1) || Unowned(w, x, y + 1))
                        tint = new Color32((byte)Mathf.Min(255, tint.r * 1.0f), (byte)Mathf.Min(255, tint.g * 0.93f), (byte)(tint.b * 0.62f), 255);   // a golden line along the edge of your land
                    var col = new Color32((byte)(tint.r * f), (byte)(tint.g * f), (byte)(tint.b * f), 255);
                    _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                    AddQuad(i0);
                    if (w.Grid[y, x]?.T == "road") AddRoad(w, x, y, vx, vy, rot, i0, f);
                }
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetUVs(0, _uv); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }

        void AddQuad(int index)
        {
            _t.Add(index); _t.Add(index + 1); _t.Add(index + 2);
            _t.Add(index); _t.Add(index + 2); _t.Add(index + 3);
        }

        void AddRoad(World w, int x, int y, int vx, int vy, int rot, int ground, float shade)
        {
            bool HasExit(int nx, int ny)
            {
                if (!World.Inb(nx, ny)) return false;
                var neighbor = w.Grid[ny, nx];
                if (neighbor == null) return false;
                if (neighbor.T == "road") return true;
                int face = neighbor.Face;
                return face >= 0 && face < 4 && nx + FaceDirs[face, 0] == x && ny + FaceDirs[face, 1] == y;
            }

            bool ExitInView(int a, int b)
            {
                if (!World.Inb(a, b)) return false;
                var neighbor = IsoView.FromView(a, b, rot);
                return HasExit(neighbor.x, neighbor.y);
            }

            bool left = ExitInView(vx - 1, vy), right = ExitInView(vx + 1, vy);
            bool up = ExitInView(vx, vy - 1), down = ExitInView(vx, vy + 1);
            var top = _v[ground]; var east = _v[ground + 1]; var bottom = _v[ground + 2]; var west = _v[ground + 3];

            void Vertex(float u, float v, Color32 tint)
            {
                // Match the ground mesh's two triangles so the road follows sloped tile edges.
                var point = u >= v ? top + (east - top) * u + (bottom - east) * v :
                    top + (west - top) * v + (bottom - west) * u;
                _v.Add(point);
                const float uvInset = 0.969f;
                float atlasU = (ArtLibrary.CellRoad + 0.5f + (u - v) * 0.5f * uvInset) / ArtLibrary.CellCount;
                _uv.Add(new Vector2(atlasU, 0.5f + (1 - u - v) * 0.46875f));
                _c.Add(tint);
            }

            void Rect(float u0, float v0, float u1, float v1, Color32 color)
            {
                int index = _v.Count;
                Vertex(u0, v0, color); Vertex(u1, v0, color); Vertex(u1, v1, color); Vertex(u0, v1, color);
                AddQuad(index);
            }

            void Strip(float inner, float outer, byte light)
            {
                byte intensity = (byte)(light * shade);
                var color = new Color32(intensity, intensity, intensity, 255);
                Rect(inner, inner, outer, outer, color);
                if (left) Rect(0, inner, inner, outer, color);
                if (right) Rect(outer, inner, 1, outer, color);
                if (up) Rect(inner, 0, outer, inner, color);
                if (down) Rect(inner, outer, outer, 1, color);
            }

            Strip(0.25f, 0.75f, 200);
            Strip(0.30f, 0.70f, 255);
        }
        static int CellFor(World w, int x, int y)
        {
            if (w.IsMountain(x, y)) return w.TileLevel(x, y) >= SnowLevel ? ArtLibrary.CellSnow : ArtLibrary.CellRock;
            if (w.DecoMap[y, x] == Deco.Rock && w.PitDepth(x, y) > 0) return ArtLibrary.CellRock;
            double h = Noise.Hash2(x, y, 5);
            return w.Fert[y, x] ? ArtLibrary.CellFert + (h < 0.5 ? 0 : 1) : ArtLibrary.CellGrass + (int)(h * 4);
        }
    }
}
