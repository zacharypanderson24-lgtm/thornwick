// The management layer of the HUD (DESIGN_UI.md): the twelve-menu toolbar under the resource bar, one standard window (title, tabs, rows, footer
// buttons) that the data-driven menus share, the message log, and a confirmation dialog. Menus whose screens already exist (Workforce, Farming,
// Military, Vessels, the world map, the build bar) open those screens; the rest are locked with the reason until something real stands behind them.
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
        public const float ToolbarTop = -70, ToolbarH = 38;

        readonly UIManager _ui = new UIManager();
        public NotificationManager Notes { get; } = new NotificationManager();
        public UIManager UI => _ui;

        readonly Dictionary<MenuId, Image> _menuButtons = new Dictionary<MenuId, Image>();
        readonly Dictionary<MenuId, Text> _menuLabels = new Dictionary<MenuId, Text>();
        RectTransform _mgmtWin, _confirmBox;
        Text _mgmtTitle, _mgmtEmpty, _mgmtFootnote, _confirmTitle, _confirmBody;
        readonly List<Image> _mgmtTabs = new List<Image>();
        readonly List<Text> _mgmtTabLabels = new List<Text>();
        readonly List<Image> _mgmtFooter = new List<Image>();
        readonly List<Text> _mgmtFooterLabels = new List<Text>();
        readonly List<(RectTransform row, Text label, Text value, Image barBack, RectTransform barFill)> _mgmtRows = new List<(RectTransform, Text, Text, Image, RectTransform)>();
        readonly List<string> _mgmtRowIds = new List<string>();
        readonly List<Action> _footerActions = new List<Action>();
        RectTransform _bannerPanel;
        CanvasScaler _scaler;
        int _speedNow = 1, _resumeSpeed = 1;
        bool _pausedBySystem, _wasSystem;
        public UiSettings Settings { get; private set; } = new UiSettings();
        const string SettingsKey = "crownandhearth.ui";
        RectTransform _mgmtHost, _hostDummy;
        ScrollRect _hostScroll;
        int _msgPage;
        Action _confirmYes;
        int _mgmtShownStamp = -1;
        string _mgmtShownKey;

        void BuildManagement(RectTransform root)
        {
            // ---- toolbar ----
            const float bw = 68, gap = 2;
            float width = 8 + MenuRegistry.Primary.Count * (bw + gap) + 4 + 64 + 2 + 56 + 6;
            var bar = MakePanel(root, "ManagementBar", Vector2.up, Vector2.up, new Vector2(8, ToolbarTop - ToolbarH), new Vector2(8 + width, ToolbarTop));
            int i = 0;
            foreach (var def in MenuRegistry.Primary)
            {
                var id = def.Id;
                MakeMenuButton(bar, id, def.Title, "F" + def.FunctionKey, 4 + i * (bw + gap), bw);
                i++;
            }
            MakeMenuButton(bar, MenuId.Notifications, "Messages", "N", 4 + i * (bw + gap) + 4, 64);
            MakeMenuButton(bar, MenuId.System, "System", "Esc", 4 + i * (bw + gap) + 4 + 66, 56);
            Settings = UiSettings.Parse(PlayerPrefs.GetString(SettingsKey, ""));
            ApplyScale();

            // ---- the standard window ----
            _mgmtWin = MakePanel(root, "ManagementWindow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -240), new Vector2(360, 320));
            _mgmtTitle = MakeText(_mgmtWin, "Title", 24, TextAnchor.UpperLeft); _mgmtTitle.fontStyle = FontStyle.Bold;
            var tr = _mgmtTitle.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(18, -46); tr.offsetMax = new Vector2(-60, -8);
            MakeButton(_mgmtWin, "X", Vector2.one, new Vector2(-28, -26), new Vector2(40, 32), () => _ui.Close());
            for (int t = 0; t < 12; t++)
            {
                int tab = t;
                var img = MakeButton(_mgmtWin, "Tab", new Vector2(0, 1), new Vector2(76 + t * 132, -68), new Vector2(126, 28), () => _ui.SetTab(tab));
                var tl = img.GetComponentInChildren<Text>(); tl.resizeTextForBestFit = true; tl.resizeTextMinSize = 9; tl.resizeTextMaxSize = 14;
                _mgmtTabs.Add(img); _mgmtTabLabels.Add(tl);
            }
            _mgmtHost = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>(); _mgmtHost.SetParent(_mgmtWin, false);
            Stretch(_mgmtHost, new Vector2(18, 50), new Vector2(-18, -96));
            _mgmtHost.gameObject.AddComponent<RectMask2D>();   // a hosted screen taller than the window (Workforce has a row for every trade) scrolls
            _hostDummy = new GameObject("None", typeof(RectTransform)).GetComponent<RectTransform>(); _hostDummy.SetParent(_mgmtHost, false);
            _hostScroll = _mgmtHost.gameObject.AddComponent<ScrollRect>(); _hostScroll.horizontal = false; _hostScroll.vertical = true;
            _hostScroll.movementType = ScrollRect.MovementType.Clamped; _hostScroll.scrollSensitivity = 30; _hostScroll.viewport = _mgmtHost; _hostScroll.content = _hostDummy;
            for (int r = 0; r < ManagementData.MaxRows; r++)
            {
                var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>(); row.SetParent(_mgmtWin, false);
                float top = -92 - r * 27;
                row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.offsetMin = new Vector2(18, top - 25); row.offsetMax = new Vector2(-18, top);
                var back = new GameObject("Stripe", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); back.transform.SetParent(row, false);
                Stretch(back.rectTransform, Vector2.zero, Vector2.zero); back.color = new Color(1, 1, 1, r % 2 == 0 ? 0.05f : 0.0f); back.raycastTarget = false;
                int rowIdx = r; var rb = back.gameObject.AddComponent<Button>(); rb.transition = UnityEngine.UI.Selectable.Transition.None; rb.onClick.AddListener(() => RowClicked(rowIdx));
                _mgmtRowIds.Add(null);
                var label = MakeText(row, "Label", 16, TextAnchor.MiddleLeft); Stretch(label.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
                var value = MakeText(row, "Value", 16, TextAnchor.MiddleRight); Stretch(value.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
                var barBack = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); barBack.transform.SetParent(row, false);
                var br = barBack.rectTransform; br.anchorMin = br.anchorMax = new Vector2(1, 0.5f); br.pivot = new Vector2(1, 0.5f); br.sizeDelta = new Vector2(170, 10); br.anchoredPosition = new Vector2(-8, 0);
                barBack.color = new Color(0, 0, 0, 0.5f); barBack.raycastTarget = false;
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.transform.SetParent(br, false);
                fill.color = Pulse; fill.raycastTarget = false;
                _mgmtRows.Add((row, label, value, barBack, fill.rectTransform));
            }
            _mgmtEmpty = MakeText(_mgmtWin, "Empty", 18, TextAnchor.MiddleCenter); _mgmtEmpty.color = Dim; Stretch(_mgmtEmpty.rectTransform, new Vector2(20, 60), new Vector2(-20, -100));
            _mgmtFootnote = MakeText(_mgmtWin, "Footnote", 13, TextAnchor.LowerRight); _mgmtFootnote.color = Dim; Stretch(_mgmtFootnote.rectTransform, new Vector2(16, 8), new Vector2(-16, 40));
            for (int f = 0; f < 4; f++)
            {
                int idx = f;
                var img = MakeButton(_mgmtWin, "Footer", Vector2.zero, new Vector2(90 + f * 170, 26), new Vector2(160, 32), () => { if (idx < _footerActions.Count) _footerActions[idx](); });
                _mgmtFooter.Add(img); _mgmtFooterLabels.Add(img.GetComponentInChildren<Text>());
            }

            // ---- the confirmation dialog (one reusable shape) ----
            var dim = MakePanel(root, "Confirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            _confirmBox = MakePanel(dim, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -100), new Vector2(250, 100));
            _confirmTitle = MakeText(_confirmBox, "Title", 22, TextAnchor.UpperCenter); _confirmTitle.fontStyle = FontStyle.Bold;
            var ct = _confirmTitle.rectTransform; ct.anchorMin = new Vector2(0, 1); ct.anchorMax = Vector2.one; ct.offsetMin = new Vector2(16, -44); ct.offsetMax = new Vector2(-16, -10);
            _confirmBody = MakeText(_confirmBox, "Body", 16, TextAnchor.UpperCenter); Stretch(_confirmBody.rectTransform, new Vector2(20, 56), new Vector2(-20, -50));
            MakeButton(_confirmBox, "Yes", Vector2.zero, new Vector2(130, 30), new Vector2(150, 34), () => { var y = _confirmYes; _ui.PopModal("confirm"); y?.Invoke(); });
            MakeButton(_confirmBox, "Cancel", Vector2.zero, new Vector2(370, 30), new Vector2(150, 34), () => _ui.PopModal("confirm"));

            _ui.Changed += SyncManagement;
            SyncManagement();
        }

        void MakeMenuButton(RectTransform bar, MenuId id, string title, string key, float x, float w)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform; rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, ToolbarH - 6); rt.anchoredPosition = new Vector2(x, 0);
            var img = go.GetComponent<Image>(); img.color = Btn;
            var b = go.GetComponent<Button>(); b.transition = UnityEngine.UI.Selectable.Transition.None; b.onClick.AddListener(() => ToggleMenu(id));
            var t = MakeText(rt, "Label", 12, TextAnchor.MiddleCenter); t.text = title;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = 12; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(1, 8); tr.offsetMax = new Vector2(-1, 0);
            var k = MakeText(rt, "Key", 10, TextAnchor.LowerCenter); k.text = key; k.color = Dim;
            Stretch(k.rectTransform, new Vector2(0, 1), Vector2.zero);
            _menuButtons[id] = img; _menuLabels[id] = t;
            AddHover(go, on => ShowMenuTip(id, on));
        }

        /// <summary>The hover card under a toolbar button: its name and key, what it holds, and why it is locked when it is.</summary>
        public void ShowMenuTip(MenuId id, bool on)
        {
            if (!_menuButtons.TryGetValue(id, out var img)) return;
            if (!on) { ShowTip(false, null, "", "", ""); return; }
            var def = MenuRegistry.Get(id);
            string key = id == MenuId.Notifications ? "N" : id == MenuId.System ? "Esc" : "F" + def.FunctionKey;
            string why = _world != null ? MenuRegistry.WhyLocked(id, _world) : null;
            ShowTip(true, img.rectTransform, $"{def.Title}  ({key})", def.Blurb, why ?? "", true);
        }

        // ---------- opening and closing ----------
        bool IsMenuOpen(MenuId id) => id switch
        {
            MenuId.Military => MilitaryOpen,
            MenuId.World => _mapOpen,
            _ => _ui.Open == id,
        };

        /// <summary>Close every menu and window (the world map too, unless <paramref name="keepMap"/>).</summary>
        void CloseAllMenus(bool keepMap = false)
        {
            _ui.Close();
            if (_workPanel != null && _workPanel.activeSelf) ToggleWorkforce();
            if (_townMenu != TownMenu.Closed) ShowTownMenu(TownMenu.Closed);
            if (_vesselPanel != null) _vesselPanel.gameObject.SetActive(false);
            if (!keepMap && _mapOpen) CloseMap();
        }

        /// <summary>Open the menu (or close it if it is open). A locked menu says why instead.</summary>
        public void ToggleMenu(MenuId id)
        {
            if (_world == null) return;
            if (_campOpen && _campPhase == CampPhase.Fight) return;   // no leaving a battle by accident
            if (_ui.HasModal) return;
            var why = MenuRegistry.WhyLocked(id, _world);
            if (why != null) { Toast(why, 4); return; }
            bool wasOpen = IsMenuOpen(id);
            CloseAllMenus(keepMap: id == MenuId.World);
            if (wasOpen) return;
            switch (id)
            {
                case MenuId.Military: ToggleMilitary(); break;
                case MenuId.World: OpenMap(); break;
                default: _ui.Toggle(id, _world, out _); if (id == MenuId.Notifications) { Notes.MarkAllRead(); _msgPage = 0; } break;
            }
        }

        /// <summary>Escape for the management layer: a dialog, then an open window or panel. Placement comes first when one is active (the caller cancels it).</summary>
        public bool EscapeManagement(bool placementActive)
        {
            if (OverlayMenuOpen) { CloseOverlayMenu(); return true; }
            var r = _ui.Escape(placementActive);
            if (r == EscapeResult.ClosedModal || r == EscapeResult.ClosedMenu) return true;
            if (r == EscapeResult.CancelledPlacement) return false;
            if (_workPanel != null && _workPanel.activeSelf) { ToggleWorkforce(); return true; }
            if (_vesselPanel != null && _vesselPanel.gameObject.activeSelf) { _vesselPanel.gameObject.SetActive(false); return true; }
            if (_townMenu != TownMenu.Closed && _townMenu != TownMenu.Troops) { ShowTownMenu(TownMenu.Closed); return true; }
            // nothing to close and nothing being placed: Escape opens the System menu (the last step of the order: dialog, placement, menu, system)
            if (!placementActive && _openCat == null && !MapOpen && !_newWorldPanel.gameObject.activeSelf && !_bannerPanel.gameObject.activeSelf) { ToggleMenu(MenuId.System); return true; }
            return false;
        }

        /// <summary>A yes/no question over everything else. Escape or Cancel closes it without doing anything.</summary>
        public void Confirm(string title, string body, Action onYes)
        {
            _confirmTitle.text = title; _confirmBody.text = body; _confirmYes = onYes;
            _ui.PushModal("confirm");
        }

        void ApplyScale() { if (_scaler != null) _scaler.referenceResolution = new Vector2(1600 / Settings.Scale, 900 / Settings.Scale); }

        void SyncManagement()
        {
            if (_mgmtWin == null) return;
            bool sys = _ui.Open == MenuId.System;   // the System menu pauses the game while it is open (a setting), and gives the speed back when it closes
            if (sys && !_wasSystem && Settings.PauseOnMenu && _speedNow > 0) { _resumeSpeed = _speedNow; _pausedBySystem = true; SpeedPicked?.Invoke(0); }
            if (!sys && _wasSystem && _pausedBySystem) { _pausedBySystem = false; SpeedPicked?.Invoke(_resumeSpeed); }
            _wasSystem = sys;
            bool open = _ui.Open != null;
            _mgmtWin.gameObject.SetActive(open);
            if (open) { _mgmtWin.SetAsLastSibling(); }
            _confirmBox.parent.gameObject.SetActive(_ui.HasModal);
            if (_ui.HasModal) _confirmBox.parent.SetAsLastSibling();
            if (_tip != null && _cursorTip != null) { _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling(); }
            _mgmtShownKey = null;
            ApplyHostedTab();
        }

        void RowClicked(int idx)
        {
            string id = idx < _mgmtRowIds.Count ? _mgmtRowIds[idx] : null;
            if (id == null) return;
            if (id.StartsWith("cmd:")) { RunCommand(id.Substring(4)); return; }
            CloseAllMenus(); ToolPicked?.Invoke(id);
        }

        void RunCommand(string cmd)
        {
            switch (cmd)
            {
                case "resume": _ui.Close(); break;
                case "save": SavePicked?.Invoke(); break;
                case "load": LoadPicked?.Invoke(); break;
                case "newworld": CloseAllMenus(); _newWorldPanel.gameObject.SetActive(true); break;
                case "banner": CloseAllMenus(); _bannerPanel.gameObject.SetActive(true); break;
                case "scale": Settings.CycleScale(); ApplyScale(); StoreSettings(); _mgmtShownKey = null; break;
                case "pause": Settings.PauseOnMenu = !Settings.PauseOnMenu; StoreSettings(); _mgmtShownKey = null; break;
            }
        }

        void StoreSettings() { PlayerPrefs.SetString(SettingsKey, Settings.Serialize()); PlayerPrefs.Save(); }

        /// <summary>The screen the open tab hosts (Workforce, Farming, Mining, Statistics, Livestock), or None.</summary>
        Host OpenHost()
        {
            if (_ui.Open is MenuId id) { var defs = ManagementData.TabDefs(id); return _ui.Tab >= 0 && _ui.Tab < defs.Length ? defs[_ui.Tab].Host : Host.None; }
            return Host.None;
        }

        /// <summary>Show the old screen a tab hosts and hide the others. They live inside the window now, so hiding the window hides them too.</summary>
        void ApplyHostedTab()
        {
            if (_mgmtHost == null || _workPanel == null) return;
            var want = OpenHost();
            _mgmtHost.gameObject.SetActive(want != Host.None);
            bool work = want == Host.Workforce;
            if (_workPanel.activeSelf != work) { _workPanel.SetActive(work); WorkforceToggled?.Invoke(work); }
            var town = want == Host.Farming ? TownMenu.Farming : want == Host.Mining ? TownMenu.Mining : want == Host.Statistics ? TownMenu.Statistics : want == Host.Livestock ? TownMenu.Livestock : TownMenu.Closed;
            bool hosted = _townMenu == TownMenu.Farming || _townMenu == TownMenu.Mining || _townMenu == TownMenu.Statistics || _townMenu == TownMenu.Livestock;
            var shown = want == Host.Workforce ? (RectTransform)_workPanel.transform : town != TownMenu.Closed ? _townPanels[(int)town - 1] : _hostDummy;
            if (_hostScroll.content != shown) { _hostScroll.content = shown; shown.anchoredPosition = Vector2.zero; _hostScroll.velocity = Vector2.zero; }
            if (town != TownMenu.Closed && _townMenu != town) ShowTownMenu(town);
            else if (town == TownMenu.Closed && hosted) ShowTownMenu(TownMenu.Closed);
        }

        /// <summary>Called once the Hud is built: move the Workforce, Farming, Mining, Statistics and Livestock screens into the window, retire the buttons that
        /// opened them, and tidy what is left of the old button bar (Military and Map).</summary>
        void AttachHostedPanels()
        {
            void Host(RectTransform rt)
            {
                var size = rt.sizeDelta;
                rt.SetParent(_mgmtHost, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            }
            Host((RectTransform)_workPanel.transform);
            foreach (int i in new[] { 0, 1, 3, 4 }) { Host(_townPanels[i]); _townButtons[i].gameObject.SetActive(false); }
            ((RectTransform)_townButtons[2].transform).anchoredPosition = new Vector2(-55, 0);
            ((RectTransform)_mapMenuButton.transform).anchoredPosition = new Vector2(-161, 0);
            var barRt = (RectTransform)_townButtons[2].transform.parent; barRt.offsetMin = new Vector2(-396, barRt.offsetMin.y);
            BuildOverlayMenu(_root, -55 - 106 * 2 - 25);
            if (_workersButton != null) _workersButton.gameObject.SetActive(false);
            SyncManagement();
        }

        List<(string label, Action act)> FooterFor(MenuId id, int tab, World w)
        {
            var list = new List<(string, Action)>();
            switch (id)
            {
                case MenuId.Settlement: list.Add(("Population", () => ToggleMenu(MenuId.Population))); list.Add(("Economy", () => ToggleMenu(MenuId.Economy))); break;
                case MenuId.Population: list.Add(("Settlement", () => ToggleMenu(MenuId.Settlement))); break;
                case MenuId.Transportation: list.Add(("Order boats", () => { CloseAllMenus(); _vesselPanel.gameObject.SetActive(true); _vesselShown = -1; })); break;
                case MenuId.Trade: list.Add(("World map", () => { CloseAllMenus(); OpenMap(); })); break;
                case MenuId.Notifications:
                    list.Add(("Mark all read", () => { Notes.MarkAllRead(); _mgmtShownKey = null; }));
                    list.Add(("Clear log", () => Confirm("Clear the message log?", "All saved messages will be removed. This cannot be undone.", () => { Notes.Clear(); _msgPage = 0; _mgmtShownKey = null; })));
                    if (_msgPage > 0) list.Add(("Newer", () => { _msgPage--; _mgmtShownKey = null; }));
                    if ((_msgPage + 1) * ManagementData.MaxRows < ManagementData.MessageCount(Notes, tab == 1)) list.Add(("Older", () => { _msgPage++; _mgmtShownKey = null; }));
                    break;
            }
            return list;
        }

        // ---------- per-frame refresh (called from SetSettlement) ----------
        void RefreshManagement(World w)
        {
            foreach (var kv in _menuButtons)
            {
                bool locked = kv.Key != MenuId.Notifications && MenuRegistry.WhyLocked(kv.Key, w) != null;
                bool on = IsMenuOpen(kv.Key);
                kv.Value.color = on ? BtnOn : locked ? new Color(Btn.r * 0.6f, Btn.g * 0.6f, Btn.b * 0.6f, 0.85f) : Btn;
                _menuLabels[kv.Key].color = locked ? new Color(Dim.r, Dim.g, Dim.b, 0.45f) : Ink;
            }
            if (_menuLabels.TryGetValue(MenuId.Notifications, out var ml)) ml.text = Notes.Unread > 0 ? $"Messages {Notes.Unread}" : "Messages";

            if (_ui.Open is MenuId id)
            {
                // redraw only when the day, the tab or the message list changed (rows are text, so avoid rebuilding every frame)
                string key = $"{id}|{_ui.Tab}|{_msgPage}|{Settings.Serialize()}|{w.Day}|{w.Version}|{Notes.History.Count}|{Notes.Unread}";
                if (key != _mgmtShownKey) { _mgmtShownKey = key; DrawManagementWindow(id, w); }
                if (id == MenuId.Notifications && Notes.Unread > 0 && _ui.Tab >= 0) { /* stays unread until the player marks it, so a new message shows in the toolbar */ }
            }
        }

        void DrawManagementWindow(MenuId id, World w)
        {
            var def = MenuRegistry.Get(id);
            _mgmtTitle.text = def.Title;
            var tabs = ManagementData.Tabs(id);
            int tab = Mathf.Clamp(_ui.Tab, 0, tabs.Length - 1);
            float tw = Mathf.Clamp((720 - 36 - (tabs.Length - 1) * 6f) / tabs.Length, 54, 126);
            for (int t = 0; t < _mgmtTabs.Count; t++)
            {
                bool show = tabs.Length > 1 && t < tabs.Length;
                _mgmtTabs[t].gameObject.SetActive(show);
                if (!show) continue;
                _mgmtTabLabels[t].text = tabs[t]; _mgmtTabs[t].color = t == tab ? BtnOn : Btn;
                var tr = _mgmtTabs[t].rectTransform; tr.sizeDelta = new Vector2(tw, 28); tr.anchoredPosition = new Vector2(18 + t * (tw + 6) + tw / 2, -68);
            }
            bool hosted = ManagementData.TabDefs(id)[tab].Host != Host.None;
            var rows = ManagementData.Rows(id, tab, w, Notes, _msgPage, Settings);
            for (int r = 0; r < _mgmtRows.Count; r++)
            {
                var ui = _mgmtRows[r];
                bool has = r < rows.Count;
                _mgmtRowIds[r] = has ? rows[r].Id : null;
                ui.row.gameObject.SetActive(has);
                if (!has) continue;
                var row = rows[r];
                ui.label.text = row.Label; ui.value.text = row.Value;
                var col = row.Tone == Tone.Bad ? BadInk : row.Tone == Tone.Warn ? Pulse : row.Tone == Tone.Good ? new Color(0.62f, 0.9f, 0.55f) : row.Tone == Tone.Dim ? Dim : Ink;
                ui.value.color = col;
                bool wide = row.Wide;
                ui.value.alignment = wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                bool bar = row.Bar >= 0;
                ui.barBack.gameObject.SetActive(bar);
                var vr = ui.value.rectTransform; vr.offsetMin = new Vector2(wide ? 100 : 8, 0); vr.offsetMax = new Vector2(bar ? -190 : -8, 0);
                if (bar) { ui.barFill.anchorMin = Vector2.zero; ui.barFill.anchorMax = new Vector2(Mathf.Clamp01(row.Bar), 1); ui.barFill.offsetMin = ui.barFill.offsetMax = Vector2.zero; ui.barFill.GetComponent<Image>().color = col; }
            }
            _mgmtEmpty.gameObject.SetActive(rows.Count == 0 && !hosted);
            _mgmtEmpty.text = ManagementData.EmptyText(id, tab);
            var foot = FooterFor(id, tab, w); _footerActions.Clear();
            for (int f = 0; f < _mgmtFooter.Count; f++)
            {
                _mgmtFooter[f].gameObject.SetActive(f < foot.Count);
                if (f < foot.Count) { _mgmtFooterLabels[f].text = foot[f].label; _footerActions.Add(foot[f].act); }
            }
            _mgmtFootnote.text = id == MenuId.Notifications ? $"{Notes.History.Count} saved, page {_msgPage + 1}" : def.Blurb;
        }
    }
}
