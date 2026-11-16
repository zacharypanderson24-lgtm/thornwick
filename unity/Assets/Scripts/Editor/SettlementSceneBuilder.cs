// Menu: Crown & Hearth > Rebuild Settlement Scene. Creates Assets/Scenes/Settlement.unity (camera + GameController),
// the unlit pixel material, and makes it the first scene in the build. Everything else is created at runtime by
// GameController, so the scene stays tiny and merge-friendly for collaborators.
using CrownAndHearth.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrownAndHearth.EditorTools
{
    public static class SettlementSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Settlement.unity";
        const string MaterialPath = "Assets/Art/Materials/PixelUnlit.mat";

        [MenuItem("Crown & Hearth/Rebuild Settlement Scene")]
        public static void Build()
        {
            var mat = EnsureMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraRig));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1E, 0x26, 0x1C, 0xFF);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0, 0, -10);

            var game = new GameObject("Game", typeof(GameController)).GetComponent<GameController>();
            game.SpriteMaterial = mat;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Crown & Hearth] Settlement scene built at " + ScenePath);
        }

        static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new System.InvalidOperationException("URP 2D Sprite-Unlit-Default shader not found; is the URP 2D renderer installed?");
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            mat = new Material(shader) { name = "PixelUnlit" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>One call for automation: import art, then build the scene.</summary>
        public static void ImportAndBuild()
        {
            PrototypeArtImporter.ImportAll();
            Build();
        }
    }
}
