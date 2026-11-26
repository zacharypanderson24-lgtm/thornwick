// The overlay selector: one button in the lower right that opens a short list of the map overlays (Game/UI/OverlayRegistry.cs). The game draws
// the chosen overlay (GameController); this only shows the choice and says which one is on.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        public event Action<OverlayId> OverlayPicked;
        RectTransform _overlayMenu;
        Text _overlayButtonLabel;
        readonly Dictionary<OverlayId, Image> _overlayRows = new Dictionary<OverlayId, Image>();
        readonly Dictionary<OverlayId, Text> _overlayRowLabels = new Dictionary<OverlayId, Text>();
        OverlayId _overlayShown = (OverlayId)(-1);

        public bool OverlayMenuOpen => _overlayMenu != null && _overlayMenu.gameObject.activeSelf;

        void BuildOverlayMenu(RectTransform root, float buttonX)
        {
            var barRt = (RectTransform)_townButtons[2].transform.parent;
            var button = MakeButton(barRt, "Overlay: Off", new Vector2(1, 0.5f), new Vector2(buttonX, 0), new Vector2(150, 34), () => _overlayMenu.gameObject.SetActive(!_overlayMenu.gameObject.activeSelf));
            _overlayButtonLabel = button.GetComponentInChildren<Text>();
            int rows = OverlayRegistry.All.Count + 1;
            float h = 12 + rows * 36;
            // the list opens above the button; offsets are from the bottom right of the screen
            float right = -8 + buttonX + 75 + 8;
            _overlayMenu = MakePanel(root, "OverlayMenu", Vector2.right, Vector2.right, new Vector2(right - 270, BottomH + 56), new Vector2(right, BottomH + 56 + h));
            int i = 0;
            foreach (var id in new[] { OverlayId.None }.Concat(OverlayRegistry.All.Select(o => o.Id)))
            {
                var oid = id;
                var row = MakeButton(_overlayMenu, OverlayRegistry.TitleOf(oid), new Vector2(0, 1), new Vector2(135, -(6 + 18 + i * 36)), new Vector2(258, 32), () => PickOverlay(oid));
                _overlayRows[oid] = row; _overlayRowLabels[oid] = row.GetComponentInChildren<Text>();
                i++;
            }
            _overlayMenu.gameObject.SetActive(false);
        }

        void PickOverlay(OverlayId id)
        {
            if (_world != null)
            {
                var why = OverlayRegistry.WhyLocked(id, _world);
                if (why != null) { Toast(why, 4); return; }
            }
            _overlayMenu.gameObject.SetActive(false);
            OverlayPicked?.Invoke(id);
        }

        /// <summary>Show which overlay is on (called every frame by the game).</summary>
        public void SetOverlayState(OverlayId id, string label)
        {
            if (_overlayButtonLabel == null) return;
            _overlayButtonLabel.text = "Overlay: " + label;
            foreach (var kv in _overlayRows)
            {
                bool locked = _world != null && OverlayRegistry.WhyLocked(kv.Key, _world) != null;
                kv.Value.color = kv.Key == id ? BtnOn : locked ? new Color(Btn.r * 0.6f, Btn.g * 0.6f, Btn.b * 0.6f, 0.9f) : Btn;
                _overlayRowLabels[kv.Key].color = locked ? new Color(Dim.r, Dim.g, Dim.b, 0.5f) : Ink;
            }
        }

        public void ToggleOverlayMenu() { if (_overlayMenu != null) _overlayMenu.gameObject.SetActive(!_overlayMenu.gameObject.activeSelf); }

        public void CloseOverlayMenu() { if (_overlayMenu != null) _overlayMenu.gameObject.SetActive(false); }
    }
}
