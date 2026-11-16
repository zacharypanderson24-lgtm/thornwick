// Orthographic camera with the browser's zoom ladder (0.25 .. 5 screen pixels per game pixel), pixel snapping,
// keyboard / drag panning and zoom towards the cursor.
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrownAndHearth.Game
{
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static readonly float[] Zooms = { 0.25f, 0.5f, 1, 2, 3, 4, 5 };
        public int ZoomIndex = 3;
        public float PanSpeed = 900f;     // screen pixels per second

        Camera _cam;
        Vector3 _target;
        Vector2? _dragFrom;

        public Camera Cam => _cam ? _cam : (_cam = GetComponent<Camera>());
        public float Zoom => Zooms[ZoomIndex];
        /// <summary>Unity units per screen pixel.</summary>
        float UnitsPerPixel => 1f / (IsoView.PPU * Zoom);

        void Awake() { Cam.orthographic = true; _target = transform.position; Apply(); }

        public void CenterOn(Vector3 p) { _target = new Vector3(p.x, p.y, -10); Apply(); }
        public Vector3 Center => new Vector3(_target.x, _target.y, 0);

        public void ZoomStep(int dir, Vector2 screenPos)
        {
            int ni = Mathf.Clamp(ZoomIndex + dir, 0, Zooms.Length - 1);
            if (ni == ZoomIndex) return;
            var before = Cam.ScreenToWorldPoint(screenPos);
            ZoomIndex = ni; Apply();
            var after = Cam.ScreenToWorldPoint(screenPos);
            _target += before - after; Apply();
        }

        /// <summary>Called every frame by the game controller; pointerFree is false while the cursor is over the HUD.</summary>
        public void Tick(bool pointerFree, bool keyboardFree = true)
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            Vector2 dir = Vector2.zero;
            if (keyboardFree && kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
            }
            if (dir != Vector2.zero) _target += (Vector3)(dir.normalized * PanSpeed * Time.unscaledDeltaTime * UnitsPerPixel);
            if (mouse != null)
            {
                bool dragBtn = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                var pos = mouse.position.ReadValue();
                if (dragBtn && (_dragFrom.HasValue || pointerFree))
                {
                    if (_dragFrom.HasValue) _target -= (Vector3)((pos - _dragFrom.Value) * UnitsPerPixel);
                    _dragFrom = pos;
                }
                else _dragFrom = null;
                float scroll = mouse.scroll.ReadValue().y;
                if (pointerFree && Mathf.Abs(scroll) > 0.01f) ZoomStep(scroll > 0 ? 1 : -1, pos);
            }
            Apply();
        }

        void Apply()
        {
            Cam.orthographicSize = Screen.height / (2f * IsoView.PPU * Zoom);
            // snap to whole screen pixels so pixel art stays crisp while panning
            float u = UnitsPerPixel;
            transform.position = new Vector3(Mathf.Round(_target.x / u) * u, Mathf.Round(_target.y / u) * u, -10);
        }
    }
}
