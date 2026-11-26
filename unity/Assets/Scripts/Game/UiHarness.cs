// Drives the real Hud the way a mouse does, for tests and the screenshot tool: puts the canvas under a camera and clicks at a rectangle's centre
// (finding what is under the point and running the click handler above it). If a row cannot be hit (its graphic ignores raycasts, or something
// sits on top of it), this reports that instead of calling the handler directly.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public sealed class UiClickResult
    {
        public string Hit;          // name of the topmost thing under the point, null if nothing
        public bool Delivered;      // a click handler was found and run
        public override string ToString() => $"hit={Hit ?? "nothing"} delivered={Delivered}";
    }

    public static class UiHarness
    {
        /// <summary>Put the Hud's canvas under a camera rendering into a texture of this size (what the screenshot tool does), so raycasts work without a window.</summary>
        public static Camera Attach(GameObject hudRoot, int width, int height, float uiScale = 1f)
        {
            var canvas = hudRoot.GetComponentInChildren<Canvas>();
            var camGo = new GameObject("UiHarnessCam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 10;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1600f, 2f), Mathf.Log(height / 900f, 2f), 0.5f)) * uiScale;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform); Canvas.ForceUpdateCanvases();
            return cam;
        }

        public static void Detach(GameObject hudRoot, Camera cam)
        {
            if (cam == null) return;
            var rt = cam.targetTexture; cam.targetTexture = null;
            if (rt != null) Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) if (es.name == "UiHarnessEventSystem") Object.DestroyImmediate(es.gameObject);
        }

        /// <summary>Click at the centre of a rectangle the way the canvas's raycaster would: among the active graphics that take raycasts and contain the point, the one drawn last
        /// (hierarchy order is draw order) is hit, and the click goes to the nearest handler at or above it. The real GraphicRaycaster only works once a rendering device has
        /// ordered the canvas (its depth is -1 until then), so this repeats its rule over the same graphics; a row whose graphics ignore raycasts, or that something covers, is
        /// reported as not delivered.</summary>
        public static UiClickResult ClickCentre(Canvas canvas, Camera cam, RectTransform target)
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null) { var go = new GameObject("UiHarnessEventSystem", typeof(EventSystem)); es = go.GetComponent<EventSystem>(); }
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, target.TransformPoint(target.rect.center));
            var ped = new PointerEventData(es) { position = screen, button = PointerEventData.InputButton.Left, clickCount = 1 };
            GameObject top = null;
            foreach (var g in canvas.GetComponentsInChildren<Graphic>(false))   // pre-order: parents first, later siblings after earlier ones = draw order
                if (g.raycastTarget && g.canvasRenderer != null && !g.canvasRenderer.cull && g.color.a >= 0f && RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, screen, cam)) top = g.gameObject;
            var result = new UiClickResult { Hit = top != null ? top.name : null };
            if (top == null) return result;
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top);
            if (handler != null) { ped.pointerPress = handler; ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler); result.Delivered = true; }
            return result;
        }
    }
}
