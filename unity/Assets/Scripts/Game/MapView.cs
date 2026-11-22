// The map menu's picture: zoom with the wheel (about the cursor), pan by dragging or with WASD / arrows, hover to pick a cell. The map
// textures are shown through RawImage.uvRect, so zooming costs nothing; the hovered region is outlined by a rectangle placed from the same
// view rectangle. Input is polled from the Input System while the panel is open.
using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public sealed class MapView : MonoBehaviour
    {
        public const float MinZoom = 1f, MaxZoom = 10f;

        public RawImage[] Layers = new RawImage[0];   // all share one view rectangle
        public RectTransform Surface;                  // the clipped square the layers fill
        public RectTransform Highlight;                // outline of the hovered region
        /// <summary>The cell under the cursor (x, y with y = 0 in the north), or null when the cursor is off the map.</summary>
        public Action<Vector2Int?> Hovered;
        /// <summary>A left click that was not a drag, on a cell.</summary>
        public Action<Vector2Int> Clicked;
        Vector2 _pressP; bool _moved;

        WorldMap _map;
        float _zoom = 1f;
        Vector2 _centre = new Vector2(0.5f, 0.5f);   // view centre in uv (v = 0 at the bottom, so v is 1 - y / Size)
        Vector2Int? _hover;
        bool _dragging;
        Vector2 _lastP;

        public float Zoom => _zoom;
        public Vector2Int? HoverCell => _hover;

        public void SetMap(WorldMap map) { _map = map; _hover = null; Hovered?.Invoke(null); ShowAll(); }

        public void ShowAll() { _zoom = 1f; _centre = new Vector2(0.5f, 0.5f); Apply(); }

        /// <summary>Centre the view on a cell and zoom in to the given factor.</summary>
        public void FocusCell(int x, int y, float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _centre = new Vector2((x + 0.5f) / WorldMap.Size, 1f - (y + 0.5f) / WorldMap.Size);
            Apply();
        }

        void Apply()
        {
            float d = 1f / _zoom, half = d / 2f;
            _centre.x = Mathf.Clamp(_centre.x, half, 1f - half);
            _centre.y = Mathf.Clamp(_centre.y, half, 1f - half);
            var rect = new Rect(_centre.x - half, _centre.y - half, d, d);
            foreach (var layer in Layers) if (layer != null) layer.uvRect = rect;
            PlaceHighlight();
        }

        /// <summary>The pointer's position over the surface as 0..1 on both axes (may lie outside 0..1 when the pointer is off it).</summary>
        bool Pointer01(Vector2 screen, out Vector2 p)
        {
            p = default;
            if (Surface == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface, screen, null, out var local)) return false;
            var r = Surface.rect;
            p = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return true;
        }

        Vector2 UvAt(Vector2 p) { float d = 1f / _zoom; return new Vector2(_centre.x - d / 2f + p.x * d, _centre.y - d / 2f + p.y * d); }

        void Update()
        {
            if (_map == null || Surface == null) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float d = 1f / _zoom;
            if (mouse != null && Pointer01(mouse.position.ReadValue(), out var p))
            {
                bool inside = p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
                float scroll = mouse.scroll.ReadValue().y;
                if (inside && Mathf.Abs(scroll) > 0.01f)
                {
                    var uv = UvAt(p);
                    _zoom = Mathf.Clamp(_zoom * (scroll > 0 ? 1.25f : 0.8f), MinZoom, MaxZoom);
                    float nd = 1f / _zoom;
                    _centre = new Vector2(uv.x - (p.x - 0.5f) * nd, uv.y - (p.y - 0.5f) * nd);   // the point under the cursor stays put
                    d = nd; Apply();
                }
                bool down = mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                bool pressed = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
                if (pressed && inside) { _dragging = true; _lastP = p; _pressP = p; _moved = false; }
                if (mouse.leftButton.wasReleasedThisFrame && inside && !_moved && (p - _pressP).sqrMagnitude < 0.0004f && _hover.HasValue) Clicked?.Invoke(_hover.Value);
                if (!down) _dragging = false;
                if (_dragging)
                {
                    var delta = p - _lastP; _lastP = p;
                    if (delta.sqrMagnitude > 0) { _centre -= delta * d; Apply(); }
                    if ((p - _pressP).sqrMagnitude >= 0.0004f) _moved = true;
                }
                SetHover(inside ? CellAt(p) : (Vector2Int?)null);
            }
            if (kb != null)
            {
                Vector2 dir = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
                if (dir != Vector2.zero) { _centre += dir.normalized * Time.unscaledDeltaTime * 0.45f * d; Apply(); }
            }
        }

        Vector2Int CellAt(Vector2 p)
        {
            var uv = UvAt(p);
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(uv.x * WorldMap.Size), 0, WorldMap.Size - 1),
                                  Mathf.Clamp(WorldMap.Size - 1 - Mathf.FloorToInt(uv.y * WorldMap.Size), 0, WorldMap.Size - 1));
        }

        void SetHover(Vector2Int? cell)
        {
            if (cell == _hover) return;
            _hover = cell; Hovered?.Invoke(cell); PlaceHighlight();
        }

        void PlaceHighlight()
        {
            if (Highlight == null) return;
            if (!_hover.HasValue) { Highlight.gameObject.SetActive(false); return; }
            var reg = WorldMap.RegionIndex(_hover.Value.x, _hover.Value.y);
            int col = reg % WorldMap.Regions, row = reg / WorldMap.Regions;
            float d = 1f / _zoom, u0 = _centre.x - d / 2f, v0 = _centre.y - d / 2f, rc = WorldMap.RegionCells / (float)WorldMap.Size;
            float uMin = col * rc, uMax = uMin + rc, vMax = 1f - row * rc, vMin = vMax - rc;
            Highlight.gameObject.SetActive(true);
            Highlight.anchorMin = new Vector2((uMin - u0) / d, (vMin - v0) / d);
            Highlight.anchorMax = new Vector2((uMax - u0) / d, (vMax - v0) / d);
            Highlight.offsetMin = Highlight.offsetMax = Vector2.zero;
        }
    }
}
