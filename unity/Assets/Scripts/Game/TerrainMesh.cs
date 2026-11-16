using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrownAndHearth.Game
{
    internal static class TerrainMesh
    {
        public static Mesh Create(Transform parent, Material material, string name, int order)
        {
            var child = new GameObject(name + " diagonal", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            var mesh = new Mesh { name = child.name };
            mesh.MarkDynamic();
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        public static void Upload(Mesh mesh, List<Vector3> vertices, List<Vector2> uv,
            List<Color32> colors, List<int> triangles)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
        }
    }
}
