// Crown & Hearth > UI Screenshots: builds the real HUD (the same Hud.Build the game runs), opens each management window, and renders it to PNG at
// 1280x720, 1920x1080 and 2560x1440 for looking at. The picture is the HUD over a flat green field, not over the map: it checks layout, not the world.
// Run from the command line (needs a graphics device, so no -nographics): -executeMethod CrownAndHearth.EditorTools.UiShots.Run
using System.IO;
using CrownAndHearth.Game;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.EditorTools
{
    public static class UiShots
    {
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1920, 1080), (2560, 1440) };

        [MenuItem("Crown & Hearth/UI Screenshots")]
        public static void Run()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "ui-shots"); Directory.CreateDirectory(dir);
            var art = new ArtLibrary();
            foreach (var (w, h) in Sizes)
            {
                Shot(dir, art, w, h, "hud", hud => { });
                Shot(dir, art, w, h, "settlement", hud => hud.ToggleMenu(MenuId.Settlement));
                Shot(dir, art, w, h, "economy", hud => hud.ToggleMenu(MenuId.Economy));
                Shot(dir, art, w, h, "messages", hud => hud.ToggleMenu(MenuId.Notifications));
                Shot(dir, art, w, h, "locked", hud => hud.ToggleMenu(MenuId.Technology));
                Shot(dir, art, w, h, "confirm", hud => { hud.ToggleMenu(MenuId.Notifications); hud.Confirm("Clear the message log?", "All saved messages will be removed. This cannot be undone.", () => { }); });
                Shot(dir, art, w, h, "population", hud => hud.ToggleMenu(MenuId.Population));
                Shot(dir, art, w, h, "agriculture", hud => hud.ToggleMenu(MenuId.Agriculture));
                Shot(dir, art, w, h, "transport", hud => hud.ToggleMenu(MenuId.Transportation));
            }
            Debug.Log("UI screenshots written to " + dir);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Shot(string dir, ArtLibrary art, int width, int height, string name, System.Action<Hud> act)
        {
            var world = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside);
            world.StartCampaign(); world.Peasants = 38; world.Day = 120; world.ResAmt[Res.Food] = 900; world.ResAmt[Res.Wood] = 240; world.ResAmt[Res.Stone] = 85; world.ResAmt[Res.Gold] = 60;
            world.Tick();

            var go = new GameObject("UiShotHud"); var hud = go.AddComponent<Hud>();
            hud.Build(GameController.Tools, GameController.Cats, art);
            hud.Notify("Raiders! A band of 12 is coming in from the north.", 118, 0);
            hud.Notify("A cave-in at the mine: two workers hurt.", 119, 0);
            hud.Notify("The harvest begins.", 120, 0);
            hud.SetSettlement(world);
            act(hud);
            hud.SetSettlement(world);

            var canvas = go.GetComponentInChildren<Canvas>();
            var camGo = new GameObject("UiShotCam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.45f, 0.25f); cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 10;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();   // the game's scaler reads the screen size; here the picture size is what matters
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1600f, 2f), Mathf.Log(height / 900f, 2f), 0.5f));
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform); Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, $"{name}_{width}x{height}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(go);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(es.gameObject);
        }
    }
}
