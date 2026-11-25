// Crown & Hearth > Water Screenshots: renders water maps with the real renderers (ground, water, objects) to PNG files, four camera
// rotations each, for looking at. Run it from the command line: -executeMethod CrownAndHearth.EditorTools.WaterShots.Run (needs a graphics device).
using System.IO;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using UnityEditor;
using UnityEngine;

namespace CrownAndHearth.EditorTools
{
    public static class WaterShots
    {
        [MenuItem("Crown & Hearth/Water Screenshots")]
        public static void Run()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "water-shots"); Directory.CreateDirectory(dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/PixelUnlit.mat");
            var art = new ArtLibrary();
            Shot(dir, "valley", WaterPreset.RiverValley, 1, c => SurfaceWater.River(c) && true, 4, mat, art, 2f);
            Shot(dir, "coast", WaterPreset.Coastal, 1, c => c == WaterClass.Ocean, 1, mat, art, 2f);
            Shot(dir, "lake", WaterPreset.Lakeside, 1, c => c == WaterClass.Lake, 1, mat, art, 2f);
            Shot(dir, "island", WaterPreset.Island, 2, c => c == WaterClass.Ocean, 1, mat, art, 1f);
            Debug.Log("Water screenshots written to " + dir);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Shot(string dir, string name, WaterPreset preset, int seed, System.Func<WaterClass, bool> focus, int rotations, Material mat, ArtLibrary art, float zoom)
        {
            var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(preset).SuggestedRelief, preset); w.ClaimAll();
            // a spot on the water with land beside it, nearest to the town
            int bx = -1, by = -1, best = int.MaxValue;
            for (int y = 3; y < Defs.N - 3; y++)
                for (int x = 3; x < Defs.N - 3; x++)
                {
                    var c = w.Waters.Class[y * Defs.N + x]; if (!focus(c)) continue;
                    if ((w.Waters.Flags[y * Defs.N + x] & WaterFlags.Shore) == 0) continue;
                    if (name == "valley" && w.Waters.Width[y * Defs.N + x] < 4) continue;
                    int d = System.Math.Abs(x - Defs.Start) + System.Math.Abs(y - Defs.Start);
                    if (d > 20 && d < best) { best = d; bx = x; by = y; }
                }
            var root = new GameObject("shot");
            var cam = new GameObject("cam", typeof(Camera)).GetComponent<Camera>(); cam.transform.SetParent(root.transform);
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f); cam.nearClipPlane = -50; cam.farClipPlane = 50;
            var g = new GameObject("g", typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundRenderer)).GetComponent<GroundRenderer>(); g.transform.SetParent(root.transform); g.Init(mat, art.GroundAtlas);
            var wr = new GameObject("w", typeof(MeshFilter), typeof(MeshRenderer), typeof(WaterRenderer)).GetComponent<WaterRenderer>(); wr.transform.SetParent(root.transform); wr.Init(mat);
            var o = new GameObject("o", typeof(ObjectRenderer)).GetComponent<ObjectRenderer>(); o.transform.SetParent(root.transform); o.Init(art, mat);
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            cam.orthographicSize = H / 2f / (32f * zoom);
            for (int rot = 0; rot < rotations; rot++)
                for (int tick = 0; tick < (rot == 0 && name == "valley" ? 2 : 1); tick++)
                {
                    var center = IsoView.TileCenter(w, bx, by, rot); cam.transform.position = new Vector3(center.x, center.y, -10);
                    var half = new Vector2(H / 2f * 16f / 9f / (32f * zoom), H / 2f / (32f * zoom));
                    var win = IsoView.TileWindow(new Rect(center.x - half.x * 1.5f, center.y - half.y * 1.5f, half.x * 3, half.y * 3));
                    g.Build(w, rot, win); wr.Build(w, rot, win); o.Build(w, rot, win); wr.SetTick(tick * 3);
                    cam.Render();
                    RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"{name}_rot{rot}{(tick > 0 ? "_t" + tick : "")}.png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                }
            Object.DestroyImmediate(root); rt.Release();
        }
    }
}
