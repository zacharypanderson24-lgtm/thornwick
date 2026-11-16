// The ground as one mesh per view diagonal: one diamond per tile, its four corners lifted to the vertex heights, textured from the ground atlas
// and shaded by slope (the browser's drape/groundLayer). Rebuilt only when the world or the camera rotation changes.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    public class GroundRenderer : MonoBehaviour
    {
        readonly Mesh[] _diagonals = new Mesh[IsoView.DiagonalCount];
        Material _material;
        readonly List<Vector3> _v = new List<Vector3>(N * 4);
        readonly List<Vector2> _uv = new List<Vector2>(N * 4);
        readonly List<Color32> _c = new List<Color32>(N * 4);
        readonly List<int> _t = new List<int>(N * 6);

        public void Init(Material mat, Texture2D atlas)
        {
            _material = new Material(mat) { mainTexture = atlas, name = "Ground (instance)" };
            for (int diagonal = 0; diagonal < _diagonals.Length; diagonal++)
                _diagonals[diagonal] = TerrainMesh.Create(transform, _material, "Ground", IsoView.GroundOrder(diagonal));
        }

        void OnDestroy()
        {
            foreach (var mesh in _diagonals)
                if (mesh != null) Destroy(mesh);
            if (_material != null) Destroy(_material);
        }

        static readonly Dictionary<Zone, Color32> ZoneTint = new Dictionary<Zone, Color32>
        {
            { Zone.Res, new Color32(255, 225, 150, 255) }, { Zone.Farm, new Color32(255, 240, 120, 255) }, { Zone.Comm, new Color32(190, 200, 255, 255) },
            { Zone.Wood, new Color32(150, 230, 150, 255) }, { Zone.Mine, new Color32(230, 185, 185, 255) },
        };

        public void Build(World w, int rot, bool showResources, Zone zoningTool)
        {
            float cw = 1f / ArtLibrary.CellCount, inU = 0.5f / (ArtLibrary.CellCount * 32f), inV = 0.5f / 16f;
            for (int diagonal = 0; diagonal < _diagonals.Length; diagonal++)
            {
                _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
                int lastX = Mathf.Min(N - 1, diagonal);
                for (int vx = Mathf.Max(0, diagonal - (N - 1)); vx <= lastX; vx++)
                {
                    int vy = diagonal - vx;
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
                    AddTriangles(i0);
                    if (ResourceTint(w, x, y, showResources, zoningTool) is Color32 resourceTint)
                        AddResourceFill(i0, resourceTint);
                }
                TerrainMesh.Upload(_diagonals[diagonal], _v, _uv, _c, _t);
            }
        }

        void AddTriangles(int first)
        {
            _t.Add(first); _t.Add(first + 1); _t.Add(first + 2);
            _t.Add(first); _t.Add(first + 2); _t.Add(first + 3);
        }

        void AddResourceFill(int tile, Color32 color)
        {
            int first = _v.Count;
            var white = new Vector2((ArtLibrary.CellWhite + 0.5f) / ArtLibrary.CellCount, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                // Keep the overlay on the rendered terrain, including non-planar tiles.
                _v.Add(_v[tile + i]);
                _uv.Add(white);
                _c.Add(color);
            }
            AddTriangles(first);
        }

        static Color32? ResourceTint(World w, int x, int y, bool showAll, Zone zoningTool)
        {
            switch (w.DecoMap[y, x])
            {
                case Deco.Tree:
                    return showAll || zoningTool == Zone.Wood ? new Color32(110, 255, 150, 112) : (Color32?)null;
                case Deco.Rock:
                    return showAll || zoningTool == Zone.Mine ? new Color32(228, 232, 252, 112) : (Color32?)null;
                case Deco.Ore:
                    return showAll || zoningTool == Zone.Mine ? new Color32(255, 190, 90, 128) : (Color32?)null;
                default:
                    return (showAll || zoningTool == Zone.Farm) && w.Fert[y, x] && w.Grid[y, x] == null
                        ? new Color32(235, 245, 110, 100) : (Color32?)null;
            }
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
