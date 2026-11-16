// Translucent tile highlights draped on the ground: hover, building footprints and road previews (green = ok, red = blocked).
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OverlayRenderer : MonoBehaviour
    {
        Mesh _mesh;
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<int> _t = new List<int>();
        Vector2 _white;

        public void Init(Material mat, Texture2D atlas)
        {
            _mesh = new Mesh { name = "Overlay" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(mat) { mainTexture = atlas, name = "Overlay (instance)" };
            mr.sortingOrder = short.MinValue + 1;
            _white = new Vector2((ArtLibrary.CellWhite + 0.5f) / ArtLibrary.CellCount, 0.5f);
        }

        public void Clear() { _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear(); }

        public void AddTile(World w, int x, int y, int rot, Color32 col)
        {
            if (!World.Inb(x, y)) return;
            var v = IsoView.ToView(x, y, rot);
            int i0 = _v.Count;
            _v.Add(IsoView.VertexPos(v.x, v.y, IsoView.VhView(w, v.x, v.y, rot)));
            _v.Add(IsoView.VertexPos(v.x + 1, v.y, IsoView.VhView(w, v.x + 1, v.y, rot)));
            _v.Add(IsoView.VertexPos(v.x + 1, v.y + 1, IsoView.VhView(w, v.x + 1, v.y + 1, rot)));
            _v.Add(IsoView.VertexPos(v.x, v.y + 1, IsoView.VhView(w, v.x, v.y + 1, rot)));
            for (int i = 0; i < 4; i++) { _uv.Add(_white); _c.Add(col); }
            _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
        }

        public void Apply()
        {
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetUVs(0, _uv); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }
    }
}
