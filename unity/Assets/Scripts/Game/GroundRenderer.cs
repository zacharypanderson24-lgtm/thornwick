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

        public void Build(World w, int rot)
        {
            _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
            float cw = 1f / ArtLibrary.CellCount, inU = 0.5f / (ArtLibrary.CellCount * 32f), inV = 0.5f / 16f;
            for (int vy = 0; vy < N; vy++)
                for (int vx = 0; vx < N; vx++)
                {
                    var wt = IsoView.FromView(vx, vy, rot); int x = wt.x, y = wt.y;
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
                    var col = new Color32((byte)(tint.r * f), (byte)(tint.g * f), (byte)(tint.b * f), 255);
                    _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                    _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
                }
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetUVs(0, _uv); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }

        static int CellFor(World w, int x, int y)
        {
            var g = w.Grid[y, x];
            if (g != null && g.T == "road") return ArtLibrary.CellRoad;
            if (w.IsMountain(x, y)) return w.TileLevel(x, y) * LvlPx >= 44 ? ArtLibrary.CellSnow : ArtLibrary.CellRock;
            if (w.DecoMap[y, x] == Deco.Rock && w.PitDepth(x, y) > 0) return ArtLibrary.CellRock;
            double h = Noise.Hash2(x, y, 5);
            return w.Fert[y, x] ? ArtLibrary.CellFert + (h < 0.5 ? 0 : 1) : ArtLibrary.CellGrass + (int)(h * 4);
        }
    }
}
