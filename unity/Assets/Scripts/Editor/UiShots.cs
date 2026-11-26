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
                Shot(dir, art, w, h, "inspect", hud => hud.SetInfo("FARMHOUSE\nNot connected: needs a road to the Town Hall\nWorkers 1/1 needed (1 jobs; one farmer for every 8 fields from sowing to harvest)\nWorks 6/8 fields of Farmland within 12 tiles (8 per farmer).\nFish stock: plentiful (80%). Season: Summer (x1.0).\nPort: running\nLogs waiting to be sawn: 12"));
                Shot(dir, art, w, h, "overlays", hud => { hud.ToggleOverlayMenu(); hud.SetOverlayState(OverlayId.Water, "Groundwater"); });
                Shot(dir, art, w, h, "system", hud => hud.ToggleMenu(MenuId.System));
                Shot(dir, art, w, h, "small", hud => { hud.Settings.ScaleIndex = 0; hud.ToggleMenu(MenuId.Construction); });
                Shot(dir, art, w, h, "tip", hud => { }, hud => hud.ShowMenuTip(MenuId.Technology, true));
                Shot(dir, art, w, h, "settlement", hud => hud.ToggleMenu(MenuId.Settlement));
                Shot(dir, art, w, h, "economy", hud => hud.ToggleMenu(MenuId.Economy));
                Shot(dir, art, w, h, "messages", hud => hud.ToggleMenu(MenuId.Notifications));
                Shot(dir, art, w, h, "locked", hud => hud.ToggleMenu(MenuId.Technology));
                Shot(dir, art, w, h, "confirm", hud => { hud.ToggleMenu(MenuId.Notifications); hud.Confirm("Clear the message log?", "All saved messages will be removed. This cannot be undone.", () => { }); });
                Shot(dir, art, w, h, "population", hud => hud.ToggleMenu(MenuId.Population));
                Shot(dir, art, w, h, "jobs", hud => { hud.ToggleMenu(MenuId.Population); hud.UI.SetTab(1); });
                Shot(dir, art, w, h, "agriculture", hud => hud.ToggleMenu(MenuId.Agriculture));
                Shot(dir, art, w, h, "mining", hud => { hud.ToggleMenu(MenuId.Economy); hud.UI.SetTab(1); });
                Shot(dir, art, w, h, "history", hud => { hud.ToggleMenu(MenuId.Settlement); hud.UI.SetTab(2); });
                Shot(dir, art, w, h, "construction", hud => { hud.ToggleMenu(MenuId.Construction); hud.UI.SetTab(2); });
                Shot(dir, art, w, h, "transport", hud => { hud.ToggleMenu(MenuId.Transportation); hud.UI.SetTab(1); });
                Shot(dir, art, w, h, "trade", hud => hud.ToggleMenu(MenuId.Trade));
                System.Action<World> staff = wd => { wd.Era = 4; for (int i = 0; i < 9; i++) { var ch = CharacterGenerator.Create(wd.Characters.NextId, wd.CharacterPool(), 9000 + i, wd.Day, (Background)(i % 7), 25, 62); wd.Characters.Add(ch); } wd.Appoint(wd.Characters.All[0].Id, Post.Steward); wd.Appoint(wd.Characters.All[1].Id, Post.Chancellor); wd.ResAmt[Res.Gold] = 140; };
                Shot(dir, art, w, h, "gov_council", hud => hud.OpenGovernment(0, Post.Treasurer), null, staff);
                Shot(dir, art, w, h, "gov_recruit", hud => hud.OpenGovernment(1, Post.Treasurer), null, staff);
                Shot(dir, art, w, h, "gov_details", hud => hud.OpenGovernment(2, Post.Treasurer, 4), null, staff);
                Shot(dir, art, w, h, "gov_payroll", hud => hud.OpenGovernment(3, Post.Treasurer), null, staff);
            }
            Debug.Log("UI screenshots written to " + dir);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Shot(string dir, ArtLibrary art, int width, int height, string name, System.Action<Hud> act, System.Action<Hud> afterLayout = null, System.Action<World> prep = null)
        {
            var world = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside);
            world.StartCampaign(); world.Peasants = 38; world.Day = 120; world.ResAmt[Res.Food] = 900; world.ResAmt[Res.Wood] = 240; world.ResAmt[Res.Stone] = 85; world.ResAmt[Res.Gold] = 60;
            world.Tick();
            prep?.Invoke(world);

            var go = new GameObject("UiShotHud"); var hud = go.AddComponent<Hud>();
            hud.Build(GameController.Tools, GameController.Cats, art);
            hud.Notify("Raiders! A band of 12 is coming in from the north.", 118, 0);
            hud.Notify("A cave-in at the mine: two workers hurt.", 119, 0);
            hud.Notify("The harvest begins.", 120, 0);
            hud.SetSettlement(world);
            act(hud);
            hud.SetSettlement(world); hud.RefreshWorkforce(world); hud.RefreshTownMenus(world);   // the game does these every frame

            var canvas = go.GetComponentInChildren<Canvas>();
            var camGo = new GameObject("UiShotCam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.45f, 0.25f); cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 10;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();   // the game's scaler reads the screen size; here the picture size is what matters
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1600f, 2f), Mathf.Log(height / 900f, 2f), 0.5f)) * hud.Settings.Scale;   // the interface-size setting
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform); Canvas.ForceUpdateCanvases();
            if (afterLayout != null) { afterLayout(hud); Canvas.ForceUpdateCanvases(); }   // things placed from where a button is (tooltips) need the final layout
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
