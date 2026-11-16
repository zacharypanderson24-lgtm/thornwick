// The HUD, built in code with uGUI (no OnGUI): resource bar, speed and rotation, tool bar, tile info, messages and the
// Workforce panel (trade priorities, as the browser's Workers panel). Text uses Unity's built-in font so nothing needs importing.
using System;
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public class Hud : MonoBehaviour
    {
        static readonly Color Panel = new Color(0.11f, 0.09f, 0.07f, 0.88f);
        static readonly Color Btn = new Color(0.29f, 0.22f, 0.15f, 1f);
        static readonly Color BtnOn = new Color(0.66f, 0.50f, 0.18f, 1f);
        static readonly Color Ink = new Color(0.96f, 0.91f, 0.78f, 1f);

        Font _font;
        InputField _bannerInput;
        Text _resText, _infoText, _toastText, _workText, _demandText;
        GameObject _infoPanel, _workPanel;
        float _toastUntil;
        readonly Dictionary<string, Image> _toolButtons = new Dictionary<string, Image>();
        readonly Dictionary<int, Image> _speedButtons = new Dictionary<int, Image>();
        RectTransform _workRows;

        public event Action SavePicked, LoadPicked;
        public event Action<string> ToolPicked;
        public event Action<int> SpeedPicked;
        public event Action<int> RotatePicked;
        public event Action<string> BannerPicked;
        public event Action<string, int> PrioMoved;

        public void Build(IList<GameController.ToolDef> tools)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            BuildBannerPicker(root);

            // top bar
            var top = MakePanel(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), Vector2.zero);
            _resText = MakeText(top, "Resources", 17, TextAnchor.MiddleLeft);
            Stretch(_resText.rectTransform, new Vector2(12, 0), new Vector2(-590, 0));
            float x = -560;
            foreach (var (label, sp) in new[] { ("Pause", 0), ("1x", 1), ("2x", 2), ("4x", 4) })
            {
                int s = sp;
                _speedButtons[s] = MakeButton(top, label, new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(60, 30), () => SpeedPicked?.Invoke(s));
                x += 66;
            }
            MakeButton(top, "Rotate Q", new Vector2(1, 0.5f), new Vector2(x + 6, 0), new Vector2(84, 30), () => RotatePicked?.Invoke(-1));
            MakeButton(top, "E Rotate", new Vector2(1, 0.5f), new Vector2(x + 96, 0), new Vector2(84, 30), () => RotatePicked?.Invoke(1));

            MakeButton(top, "Save", new Vector2(1, 0.5f), new Vector2(-73, 0), new Vector2(48, 30), () => SavePicked?.Invoke());
            MakeButton(top, "Load", new Vector2(1, 0.5f), new Vector2(-23, 0), new Vector2(44, 30), () => LoadPicked?.Invoke());

            // bottom tool bar
            var bottom = MakePanel(root, "ToolBar", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 46));
            float bx = 8;
            foreach (var t in tools)
            {
                var id = t.Id;
                float w = Mathf.Max(70, 9f * (t.Label.Length + t.Key.Length + 3));
                _toolButtons[id] = MakeButton(bottom, $"{t.Label} ({t.Key})", new Vector2(0, 0.5f), new Vector2(bx + w / 2, 0), new Vector2(w, 34), () => ToolPicked?.Invoke(id));
                bx += w + 4;
            }
            MakeButton(bottom, "Workers", new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(100, 34), ToggleWorkforce);

            // demand strip above the tool bar
            var dp = MakePanel(root, "Demand", new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 52), new Vector2(620, 80));
            _demandText = MakeText(dp, "DemandText", 15, TextAnchor.MiddleLeft);
            Stretch(_demandText.rectTransform, new Vector2(10, 0), new Vector2(-10, 0));

            // left info panel
            _infoPanel = MakePanel(root, "Info", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -330), new Vector2(330, -48)).gameObject;
            _infoText = MakeText((RectTransform)_infoPanel.transform, "InfoText", 15, TextAnchor.UpperLeft);
            Stretch(_infoText.rectTransform, new Vector2(10, 8), new Vector2(-10, -8));

            // toast
            _toastText = MakeText(root, "Toast", 18, TextAnchor.MiddleCenter);
            var tr = _toastText.rectTransform; tr.anchorMin = new Vector2(0.5f, 0); tr.anchorMax = new Vector2(0.5f, 0); tr.sizeDelta = new Vector2(1100, 40); tr.anchoredPosition = new Vector2(0, 70);
            _toastText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // workforce panel
            var wp = MakePanel(root, "Workforce", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-440, 56), new Vector2(-8, 380));
            _workPanel = wp.gameObject;
            var title = MakeText(wp, "Title", 17, TextAnchor.UpperLeft); title.text = "Workforce: villagers fill trades top to bottom";
            Stretch(title.rectTransform, new Vector2(10, 290), new Vector2(-10, -8));
            _workRows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            _workRows.SetParent(wp, false); Stretch(_workRows, new Vector2(10, 40), new Vector2(-10, -40));
            _workText = MakeText(wp, "Summary", 14, TextAnchor.LowerLeft);
            Stretch(_workText.rectTransform, new Vector2(10, 8), new Vector2(-10, -290));
            _workPanel.SetActive(false);
        }

        void BuildBannerPicker(RectTransform root)
        {
            var anchor = Vector2.one;
            var panel = MakePanel(root, "BannerColors", anchor, anchor, new Vector2(-292, -372), new Vector2(-8, -222));
            MakeButton(root, "Banner", anchor, new Vector2(-82, -203), new Vector2(148, 30),
                () => panel.gameObject.SetActive(!panel.gameObject.activeSelf));

            var palette = new[]
            {
                ("Crimson", "#B3202A"), ("Blue", "#2A56C4"), ("Green", "#2F8F46"), ("Purple", "#7A3FA8"),
                ("Gold", "#D9A31A"), ("Black", "#2B2B33"), ("White", "#E8E4D6"), ("Teal", "#1F9A9A"),
            };
            for (int i = 0; i < palette.Length; i++)
            {
                var (name, hex) = palette[i];
                var button = MakeButton(panel, name, new Vector2(0, 1),
                    new Vector2(38 + 70 * (i % 4), -23 - 34 * (i / 4)), new Vector2(65, 28),
                    () => BannerPicked?.Invoke(hex));
                ColorUtility.TryParseHtmlString(hex, out var color);
                button.color = Color.Lerp(Btn, color, 0.45f);
            }

            var inputPanel = MakePanel(panel, "HexColor", Vector2.zero, Vector2.zero, new Vector2(6, 12), new Vector2(90, 46));
            _bannerInput = inputPanel.gameObject.AddComponent<InputField>();
            var label = MakeText(inputPanel, "Hex", 15, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            _bannerInput.textComponent = label;
            _bannerInput.targetGraphic = inputPanel.GetComponent<Image>();
            _bannerInput.characterLimit = 7;
            _bannerInput.SetTextWithoutNotify("#B3202A");
            MakeButton(panel, "Apply", Vector2.zero, new Vector2(126, 29), new Vector2(62, 32),
                () => BannerPicked?.Invoke(_bannerInput.text));
            MakeButton(panel, "Original", Vector2.zero, new Vector2(217, 29), new Vector2(90, 32),
                () => BannerPicked?.Invoke(null));
            panel.gameObject.SetActive(false);
        }

        public bool EditingBanner => _bannerInput != null && _bannerInput.isFocused;

        public void ShowBannerColor(string hex) => _bannerInput.SetTextWithoutNotify(hex ?? "#B3202A");

        public bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void SetTool(string id) { foreach (var kv in _toolButtons) kv.Value.color = kv.Key == id ? BtnOn : Btn; }
        public void SetSpeed(int s) { foreach (var kv in _speedButtons) kv.Value.color = kv.Key == s ? BtnOn : Btn; }
        public void SetResources(string text) => _resText.text = text;
        public void SetDemand(string text) => _demandText.text = text;
        public void SetInfo(string text) { _infoPanel.SetActive(!string.IsNullOrEmpty(text)); _infoText.text = text; }
        public void Toast(string text, float secs = 3.5f) { _toastText.text = text; _toastUntil = Time.unscaledTime + secs; }

        void Update()
        {
            if (_toastText != null && Time.unscaledTime > _toastUntil && _toastText.text.Length > 0) _toastText.text = "";
        }

        void ToggleWorkforce() => _workPanel.SetActive(!_workPanel.activeSelf);

        public void RefreshWorkforce(World w)
        {
            if (!_workPanel.activeSelf) return;
            for (int i = _workRows.childCount - 1; i >= 0; i--) Destroy(_workRows.GetChild(i).gameObject);
            for (int i = 0; i < w.Prio.Length; i++)
            {
                string t = w.Prio[i];
                var (workers, jobs, blds, shortCount) = w.D.Work.TryGetValue(t, out var r) ? r : (0, 0, 0, 0);
                float y = -i * 38;
                var row = MakeText(_workRows, "Row", 15, TextAnchor.MiddleLeft);
                row.text = $"{i + 1}. {Defs.TradeNames[t]}   {workers}/{jobs} workers   {blds} bldg" + (shortCount > 0 ? $"   {shortCount} short" : "");
                row.color = shortCount > 0 ? new Color(1f, 0.6f, 0.5f) : Ink;
                var rt = row.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.offsetMin = new Vector2(0, y - 34); rt.offsetMax = new Vector2(-80, y);
                string id = t;
                MakeButton(_workRows, "Up", new Vector2(1, 1), new Vector2(-58, y - 17), new Vector2(36, 28), () => PrioMoved?.Invoke(id, -1));
                MakeButton(_workRows, "Dn", new Vector2(1, 1), new Vector2(-18, y - 17), new Vector2(36, 28), () => PrioMoved?.Invoke(id, 1));
            }
            _workText.text = $"Employed {w.D.Employed} of {w.Peasants} peasants, {w.D.Idle} idle, {w.D.Jobs} jobs in all.";
        }

        // ---------- uGUI helpers ----------
        RectTransform MakePanel(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            go.GetComponent<Image>().color = Panel;
            return rt;
        }

        Text MakeText(RectTransform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.alignment = anchor; t.color = Ink; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Image MakeButton(RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.sizeDelta = size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>(); img.color = Btn;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            var t = MakeText(rt, "Label", 14, TextAnchor.MiddleCenter); t.text = label;
            Stretch(t.rectTransform, Vector2.zero, Vector2.zero);
            return img;
        }

        static void Stretch(RectTransform rt, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
