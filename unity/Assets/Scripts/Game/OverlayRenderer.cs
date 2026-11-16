// Translucent tile highlights draped on the same diagonals as the ground.
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public class OverlayRenderer : MonoBehaviour
    {
        sealed class Diagonal
        {
            public readonly Mesh Mesh;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<int> Triangles = new List<int>();

            public Diagonal(Mesh mesh) => Mesh = mesh;

            public void Clear()
            {
                Vertices.Clear();
                Uv.Clear();
                Colors.Clear();
                Triangles.Clear();
            }
        }

        readonly Diagonal[] _diagonals = new Diagonal[IsoView.DiagonalCount];
        readonly List<Diagonal> _active = new List<Diagonal>();
        readonly List<Diagonal> _previous = new List<Diagonal>();
        Material _material;
        Vector2 _white;

        public void Init(Material mat, Texture2D atlas)
        {
            _material = new Material(mat) { mainTexture = atlas, name = "Overlay (instance)" };
            _white = new Vector2((ArtLibrary.CellWhite + 0.5f) / ArtLibrary.CellCount, 0.5f);
        }

        public void Clear()
        {
            _previous.Clear();
            _previous.AddRange(_active);
            foreach (var diagonal in _active) diagonal.Clear();
            _active.Clear();
        }

        public void AddTile(World w, int x, int y, int rot, Color32 col)
        {
            if (!World.Inb(x, y)) return;
            var v = IsoView.ToView(x, y, rot);
            int index = v.x + v.y;
            var diagonal = _diagonals[index] ??= new Diagonal(
                TerrainMesh.Create(transform, _material, "Overlay", IsoView.OverlayOrder(index)));
            int first = diagonal.Vertices.Count;
            if (first == 0) _active.Add(diagonal);
            diagonal.Vertices.Add(IsoView.VertexPos(v.x, v.y, IsoView.VhView(w, v.x, v.y, rot)));
            diagonal.Vertices.Add(IsoView.VertexPos(v.x + 1, v.y, IsoView.VhView(w, v.x + 1, v.y, rot)));
            diagonal.Vertices.Add(IsoView.VertexPos(v.x + 1, v.y + 1, IsoView.VhView(w, v.x + 1, v.y + 1, rot)));
            diagonal.Vertices.Add(IsoView.VertexPos(v.x, v.y + 1, IsoView.VhView(w, v.x, v.y + 1, rot)));
            for (int i = 0; i < 4; i++)
            {
                diagonal.Uv.Add(_white);
                diagonal.Colors.Add(col);
            }
            diagonal.Triangles.Add(first);
            diagonal.Triangles.Add(first + 1);
            diagonal.Triangles.Add(first + 2);
            diagonal.Triangles.Add(first);
            diagonal.Triangles.Add(first + 2);
            diagonal.Triangles.Add(first + 3);
        }

        public void Apply()
        {
            foreach (var diagonal in _previous)
                if (diagonal.Vertices.Count == 0) diagonal.Mesh.Clear();
            foreach (var diagonal in _active)
                TerrainMesh.Upload(diagonal.Mesh, diagonal.Vertices, diagonal.Uv, diagonal.Colors, diagonal.Triangles);
        }

        void OnDestroy()
        {
            foreach (var diagonal in _diagonals)
                if (diagonal != null) Destroy(diagonal.Mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
