// The HUD, built in code with uGUI (no OnGUI): resource bar, speed and rotation, the build bar, tile info, messages and the
// Workforce panel (trade priorities, as the browser's Workers panel). Text uses Unity's built-in font so nothing needs importing.
//
// The build bar follows the usual city-builder pattern (SimCity, Cities: Skylines, TheoTown, Banished, Anno): a row of
// category buttons along the bottom, grouped by what the player is trying to do (housing, farming, forestry, mining...), each
// opening a tray of cards with a picture, the name and the cost. Hovering a card shows what it does and what it needs. A zone
// sits in the same tray as the building that works it (wheat fields beside the farmhouse), so the pair is found together.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud : MonoBehaviour
    {
        // every colour comes from UiTheme (the palette lives in one place)
        static Color Panel => UiTheme.OverlayFill;
        static Color Btn => UiTheme.ButtonRest;
        static Color BtnHover => UiTheme.ButtonHover;
        static Color BtnOn => UiTheme.ButtonOn;
        static Color Ink => UiTheme.Text;
        static Color Dim => UiTheme.TextDim;
        static Color BadInk => UiTheme.Critical;
        static Color Pulse => UiTheme.Warning;

        /// <summary>The compact resource bar along the top.</summary>
        public const float TopBarH = 46;
        const float BarH = 84, NeedsH = 54, BottomH = BarH + NeedsH, CatW = 76, CardW = 172, CardH = 84;

        Font _font;
        ArtLibrary _art;
        InputField _bannerInput;
        Text _infoText, _toastText, _workText, _warbandText;
        GameObject _warbandPanel;
        GameObject _infoPanel, _workPanel;
        Image _workersButton;
        float _toastUntil;
        Text _day, _population, _gold, _infoTitle;
        readonly Dictionary<int, Image> _speedButtons = new Dictionary<int, Image>();
        RectTransform _workRows, _root;

        // build bar
        IList<GameController.ToolDef> _tools;
        IList<GameController.CatDef> _cats;
        readonly Dictionary<GameController.Cat, Image> _catButtons = new Dictionary<GameController.Cat, Image>();
        readonly Dictionary<GameController.Cat, RectTransform> _trays = new Dictionary<GameController.Cat, RectTransform>();
        readonly Dictionary<string, Image> _cards = new Dictionary<string, Image>();
        readonly Dictionary<string, Text> _cardCost = new Dictionary<string, Text>();
        readonly Dictionary<GameController.Cat, string> _lastInCat = new Dictionary<GameController.Cat, string>();
        GameController.Cat? _openCat;
        string _currentTool, _hoverCard;
        RectTransform _tip; Text _tipTitle, _tipBody, _tipFoot;
        RectTransform _chip; Text _chipText;
        RectTransform _cursorTip; Text _cursorText;
        Image _demandFill; Text _demandText;

        public event Action SavePicked, LoadPicked;
        public event Action<string> ToolPicked;
        public event Action<int> SpeedPicked;
        public event Action<int> RotatePicked;
        public event Action<string> BannerPicked;
        public event Action<string, int> PrioMoved;
        /// <summary>A trade's + (1), - (-1) or Auto (0) button on the Workforce panel.</summary>
        public event Action<string, int> WorkersAdjusted;
        public event Action<bool> WorkforceToggled;
        World _world;        // for card prices

        public void Build(IList<GameController.ToolDef> tools, IList<GameController.CatDef> cats, ArtLibrary art)
        {
            _tools = tools; _cats = cats; _art = art;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); _scaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            _root = root;
            BuildBannerPicker(root);

            var stats = MakePanel(root, "Stockpiles", Vector2.up, Vector2.one, new Vector2(8, -TopBarH), new Vector2(-8, -8));
            var statRow = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            statRow.padding = new RectOffset(4, 4, 4, 4);
            statRow.spacing = 4;
            statRow.childControlWidth = statRow.childControlHeight = true;
            statRow.childForceExpandWidth = statRow.childForceExpandHeight = true;
            _day = MakeStat(stats, "Day");
            _population = MakeStat(stats, "Population");
            BuildStockpileDropdown(stats, "Crops", ("Wheat", Res.Food), ("Flax", Res.Flax), ("Smoked meat", Res.SmokedMeat));
            BuildStockpileDropdown(stats, "Mining", ("Stone", Res.Stone), ("Ore", Res.Ore), ("Metal", Res.Metal));
            BuildStockpileDropdown(stats, "Wood", ("Logs", Res.Logs), ("Wood", Res.Wood), ("Charcoal", Res.Charcoal));
            _gold = MakeStat(stats, "Gold");

            var top = MakePanel(root, "TimeAndSession", Vector2.one, Vector2.one,
                new Vector2(-480, -TopBarH - 8 - 38), new Vector2(-8, -TopBarH - 8));
            float x = -440;
            foreach (var (label, sp) in new[] { ("Pause", 0), ("1x", 1), ("2x", 2), ("4x", 4) })
            {
                int s = sp;
                _speedButtons[s] = MakeButton(top, label, new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(60, 30), () => SpeedPicked?.Invoke(s));
                x += 66;
            }
            MakeButton(top, "Rotate Q", new Vector2(1, 0.5f), new Vector2(x + 6, 0), new Vector2(84, 30), () => RotatePicked?.Invoke(-1));
            MakeButton(top, "E Rotate", new Vector2(1, 0.5f), new Vector2(x + 96, 0), new Vector2(84, 30), () => RotatePicked?.Invoke(1));

            // Save and Load live in the System menu (Esc), not on the HUD

            BuildNeeds(root);
            BuildCampaign(root);
            BuildGuide(root);
            BuildNewWorld(root);
            BuildVessels(root);
            BuildScenario(root);
            BuildIncident(root);
            BuildBar(root);
            var townMenuBar = MakePanel(root, "TownMenuBar", Vector2.right, Vector2.right,
                new Vector2(-651, BottomH + 8), new Vector2(-8, BottomH + 50));
            BuildTownMenus(root, townMenuBar);
            BuildMap(root, townMenuBar);
            BuildCamp(root);
            BuildManagement(root);
            BuildCatalogueWindow(root);

            // left info panel
            _infoPanel = MakePanel(root, "Info", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -TopBarH - 8 - 349), new Vector2(330, -TopBarH - 8 - 42)).gameObject;
            _infoTitle = MakeText((RectTransform)_infoPanel.transform, "InspectionTitle", 19, TextAnchor.UpperLeft);
            // one line at the top: long headings shrink to fit instead of wrapping over the text below
            var itr = _infoTitle.rectTransform; itr.anchorMin = new Vector2(0, 1); itr.anchorMax = Vector2.one;
            itr.offsetMin = new Vector2(12, -40); itr.offsetMax = new Vector2(-12, -8);
            _infoTitle.resizeTextForBestFit = true; _infoTitle.resizeTextMinSize = 11; _infoTitle.resizeTextMaxSize = 19;
            _infoTitle.horizontalOverflow = HorizontalWrapMode.Wrap; _infoTitle.verticalOverflow = VerticalWrapMode.Truncate;
            _infoText = MakeText((RectTransform)_infoPanel.transform, "InfoText", 15, TextAnchor.UpperLeft);
            Stretch(_infoText.rectTransform, new Vector2(12, 8), new Vector2(-12, -43));

            // toast: messages after an action, above the build bar and its tray
            _toastText = MakeText(root, "Toast", 18, TextAnchor.MiddleCenter);
            var tr = _toastText.rectTransform; tr.anchorMin = new Vector2(0.5f, 0); tr.anchorMax = new Vector2(0.5f, 0); tr.sizeDelta = new Vector2(1100, 40); tr.anchoredPosition = new Vector2(0, BottomH + CardH + 44);
            _toastText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // a spotted warband on its way: stays under the top bar until it arrives
            var wbp = MakePanel(root, "Warband", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-340, -TopBarH - 8 - 80), new Vector2(180, -TopBarH - 8 - 42));
            _warbandPanel = wbp.gameObject;
            _warbandText = MakeText(wbp, "WarbandText", 15, TextAnchor.MiddleCenter);
            Stretch(_warbandText.rectTransform, new Vector2(8, 2), new Vector2(-8, -2));
            _warbandText.color = BadInk;
            _warbandPanel.SetActive(false);

            // workforce panel
            float workforceHeight = 80 + Defs.Trades.Length * 38;
            var wp = MakePanel(root, "Workforce", Vector2.right, Vector2.right, new Vector2(-700, BottomH + 58), new Vector2(-8, BottomH + 58 + workforceHeight));
            _workPanel = wp.gameObject;
            var title = MakeText(wp, "Title", 17, TextAnchor.UpperLeft); title.text = "Workforce: villagers fill trades top to bottom";
            Stretch(title.rectTransform, new Vector2(10, workforceHeight - 34), new Vector2(-10, -8));
            _workRows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            _workRows.SetParent(wp, false); Stretch(_workRows, new Vector2(10, 40), new Vector2(-10, -40));
            _workText = MakeText(wp, "Summary", 14, TextAnchor.LowerLeft);
            Stretch(_workText.rectTransform, new Vector2(10, 8), new Vector2(-10, 34 - workforceHeight));
            _workPanel.SetActive(false);

            AttachHostedPanels();
            BuildCursorTip(root);   // last, so it draws over everything
        }

        // ---------- Build bar: categories, trays of cards, tooltips ----------
        void BuildBar(RectTransform root)
        {
            var bar = MakePanel(root, "BuildBar", Vector2.zero, Vector2.right, new Vector2(0, NeedsH), new Vector2(0, BottomH));
            int slot = 0;
            for (int i = 0; i < _cats.Count; i++)
            {
                var cat = _cats[i];
                if (!QuickCategory(cat.Cat)) continue;   // the other categories live in the Build catalogue (their hotkeys still pick tools)
                var c = cat.Cat;
                var tools = ToolsIn(c);
                var btn = MakeIconButton(bar, cat.Label, cat.Key, CatIcon(cat, tools),
                    new Vector2(0, 0), new Vector2(8 + slot * (CatW + 4), 5), new Vector2(CatW, BarH - 10), () => ClickCategory(c));
                AddHover(btn.gameObject, on => { if (_catButtons[c].color != BtnOn) _catButtons[c].color = on ? BtnHover : Btn; ShowCatTip(on ? cat : null, btn.rectTransform); });
                _catButtons[c] = btn;
                if (tools.Count > 1) _trays[c] = BuildTray(root, c, tools, 8 + slot * (CatW + 4));
                slot++;
            }

            // demand meter
            var dem = MakePanel(bar, "Demand", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-470, 8), new Vector2(-250, BarH - 8));
            dem.GetComponent<Image>().color = Btn;
            _demandText = MakeText(dem, "DemandLabel", 13, TextAnchor.UpperLeft);
            Stretch(_demandText.rectTransform, new Vector2(8, 0), new Vector2(-8, -5));
            var track = MakePanel(dem, "Track", Vector2.zero, Vector2.right, new Vector2(8, 8), new Vector2(-8, 22));
            track.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            var fill = MakePanel(track, "Fill", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            _demandFill = fill.GetComponent<Image>();
            AddHover(dem.gameObject, on => ShowTip(on, dem, "Demand for homes",
                "When the bar is green and full, villagers want new homes and build them on Housing zones beside a road. " +
                "It drops when houses have room to spare or food runs short.", ""));

            _workersButton = MakeButton(bar, "Workers", new Vector2(1, 0.5f), new Vector2(-62, 0), new Vector2(104, 40), ToggleWorkforce);

            // what the selected tool does and how to use it: in the bar, between the categories and the Homes meter
            float chipLeft = 8 + slot * (CatW + 4) + 6;
            _chip = MakePanel(bar, "ActiveTool", Vector2.zero, Vector2.right, new Vector2(chipLeft, 8), new Vector2(-480, BarH - 8));
            _chip.GetComponent<Image>().color = new Color(0, 0, 0, 0.25f);
            _chipText = MakeText(_chip, "ToolInstructions", 14, TextAnchor.MiddleLeft);
            Stretch(_chipText.rectTransform, new Vector2(10, 2), new Vector2(-10, -2));

            // tooltip card
            _tip = MakePanel(root, "Tooltip", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _tip.pivot = Vector2.zero; _tip.sizeDelta = new Vector2(340, 10);
            var col = _tip.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(12, 12, 10, 10); col.spacing = 6;
            col.childControlWidth = col.childControlHeight = true; col.childForceExpandHeight = false;
            _tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tipTitle = MakeText(_tip, "Title", 17, TextAnchor.UpperLeft); _tipTitle.fontStyle = FontStyle.Bold;
            _tipBody = MakeText(_tip, "Body", 14, TextAnchor.UpperLeft);
            _tipFoot = MakeText(_tip, "Foot", 14, TextAnchor.UpperLeft); _tipFoot.color = new Color(1f, 0.84f, 0.45f);
            _tip.GetComponent<Image>().raycastTarget = false;
            _tip.gameObject.SetActive(false);
        }

        RectTransform BuildTray(RectTransform root, GameController.Cat c, List<GameController.ToolDef> tools, float left)
        {
            float w = tools.Count * (CardW + 6) + 6;
            float x0 = Mathf.Min(left, 1600 - 16 - w);   // keep the tray on screen (reference width)
            var tray = MakePanel(root, c + "Tray", Vector2.zero, Vector2.zero, new Vector2(x0, BottomH + 4), new Vector2(x0 + w, BottomH + 4 + CardH + 12));
            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                var card = MakePanel(tray, t.Id, Vector2.zero, Vector2.zero, new Vector2(6 + i * (CardW + 6), 6), new Vector2(6 + i * (CardW + 6) + CardW, 6 + CardH));
                var img = card.GetComponent<Image>(); img.color = Btn;
                var button = card.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
                string id = t.Id;
                button.onClick.AddListener(() => ToolPicked?.Invoke(id));
                AddHover(card.gameObject, on => { _hoverCard = on ? id : (_hoverCard == id ? null : _hoverCard); if (id != _currentTool) img.color = on ? BtnHover : Btn; ShowToolTip(on ? t : null, card); });

                var icon = MakeIcon(card, ToolIcon(t));
                var ir = icon.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(58, 64); ir.anchoredPosition = new Vector2(34, 0);
                // three rows of their own (name, kind, cost) that shrink to fit, so long names and prices never run into each other
                var name = MakeText(card, "Name", 15, TextAnchor.MiddleLeft); name.text = t.Label; name.fontStyle = FontStyle.Bold;
                Stretch(name.rectTransform, new Vector2(68, CardH - 36), new Vector2(-6, -4));
                FitText(name, 10, 15);
                var kind = MakeText(card, "Kind", 12, TextAnchor.MiddleLeft); kind.color = Dim;
                kind.text = t.Claim ? "Click a 16x16 parcel" : t.Zone != Zone.None ? "Zone: drag an area" : t.Unzone ? "Drag an area" : t.Clear ? "Click or drag" : t.Road || t.Wall ? "Drag a line" : $"{Defs.PlacementSize(t.Build)}x{Defs.PlacementSize(t.Build)} building";
                Stretch(kind.rectTransform, new Vector2(68, 30), new Vector2(-6, -36));
                FitText(kind, 9, 12);
                var cost = MakeText(card, "Cost", 13, TextAnchor.MiddleLeft);
                cost.text = t.Claim ? "Free" : t.Build != null ? CostText(t.Build) : t.Road ? "1 wood a tile" : t.Wall ? "2 wood a tile" :
                    World.IsLivestockZone(t.Zone) ? $"Fence wood + {World.StarterLivestockGold} gold" : t.Zone != Zone.None ? "Free" : "";
                Stretch(cost.rectTransform, new Vector2(68, 6), new Vector2(-6, -CardH + 30));
                FitText(cost, 9, 13);
                _cards[id] = img; _cardCost[id] = cost;
            }
            tray.gameObject.SetActive(false);
            return tray;
        }

        static void FitText(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>The tools that stay on the bar: Inspect, Roads, Land and Bulldoze. Everything else is picked from the Build catalogue.</summary>
        static bool QuickCategory(GameController.Cat c) => c == GameController.Cat.Inspect || c == GameController.Cat.Roads || c == GameController.Cat.Land || c == GameController.Cat.Bulldoze;

        List<GameController.ToolDef> ToolsIn(GameController.Cat c) => _tools.Where(t => t.Cat == c).ToList();

        void ClickCategory(GameController.Cat c)
        {
            ShowTip(false, null, "", "", "");   // the category's hover text would cover the cards that open
            var tools = ToolsIn(c);
            if (tools.Count == 1) { ToolPicked?.Invoke(tools[0].Id); ShowTray(null); return; }
            if (_openCat == c) { ToolPicked?.Invoke("look"); ShowTray(null); return; }   // second click closes it
            // like the hotkey and the one-tool categories, a click picks a tool straight away: the last one used here, else the first
            if (!tools.Any(t => t.Id == _currentTool)) ToolPicked?.Invoke(_lastInCat.TryGetValue(c, out var last) ? last : tools[0].Id);
            ShowTray(c);
        }

        /// <summary>A category hotkey: picks the category's last-used tool (or its first); pressed again, the next tool in it.</summary>
        public void CategoryKey(GameController.Cat c)
        {
            var tools = ToolsIn(c); if (tools.Count == 0) return;
            int cur = tools.FindIndex(t => t.Id == _currentTool);
            string id = cur >= 0 ? tools[(cur + 1) % tools.Count].Id
                : _lastInCat.TryGetValue(c, out var last) ? last : tools[0].Id;
            ToolPicked?.Invoke(id);
        }

        void ShowTray(GameController.Cat? c)
        {
            _openCat = c;
            foreach (var kv in _trays) kv.Value.gameObject.SetActive(kv.Key == c);
            RestoreColors();
        }

        public void SetTool(GameController.ToolDef t, string hint)
        {
            _currentTool = t.Id;
            if (t.Id != "look") _lastInCat[t.Cat] = t.Id;
            // a tool in a tray opens that tray; any other tool but Look closes it. Look leaves an open tray alone, because opening
            // a tray picks Look first (CloseTray shuts it on Esc, right-click or the Inspect button).
            if (_trays.ContainsKey(t.Cat)) ShowTray(t.Cat);
            else if (t.Id != "look") ShowTray(null);
            _chipText.text = hint;
            RestoreColors();
        }

        public void CloseTray() => ShowTray(null);

        void RestoreColors()
        {
            var cur = _tools.FirstOrDefault(t => t.Id == _currentTool);
            foreach (var kv in _catButtons) kv.Value.color = (cur != null && cur.Cat == kv.Key && cur.Id != "look") || _openCat == kv.Key ? BtnOn : Btn;
            foreach (var kv in _cards) kv.Value.color = kv.Key == _currentTool ? BtnOn : kv.Key == _hoverCard ? BtnHover : Btn;
        }

        void ShowCatTip(GameController.CatDef cat, RectTransform over)
        {
            if (cat == null || _openCat == cat.Cat) { ShowTip(false, over, "", "", ""); return; }
            ShowTip(true, over, $"{cat.Label}  ({cat.Key})", cat.Desc, "");
        }

        void ShowToolTip(GameController.ToolDef t, RectTransform over)
        {
            if (t == null) { ShowTip(false, over, "", "", ""); return; }
            string foot = "";
            if (t.Build != null)
            {
                foot = "Cost: " + CostText(t.Build);
                if (Defs.Jobs.TryGetValue(t.Build, out int jobs)) foot += $"   ·   {jobs} jobs";
                if (_world != null && !_world.CanAfford(t.Build).Ok) foot += "\n" + _world.CanAfford(t.Build).Reason;
            }
            else if (t.Road) foot = "Cost: 1 wood a tile";
            else if (t.Wall) foot = "Cost: 2 wood a tile";
            else if (t.Zone != Zone.None) foot = "Zoning is free. Right-click or Esc to stop.";
            ShowTip(true, over, t.Label, t.Desc, foot);
        }

        void ShowTip(bool on, RectTransform over, string title, string body, string foot, bool below = false)
        {
            if (_tip == null) return;   // hiding the trays while the bar is still being built
            _tip.pivot = Vector2.zero;
            _tip.gameObject.SetActive(on);
            if (!on) return;
            _tipTitle.text = title; _tipBody.text = body;
            _tipFoot.text = foot; _tipFoot.gameObject.SetActive(foot.Length > 0);
            // just above the hovered element, kept on screen
            var corners = new Vector3[4]; over.GetWorldCorners(corners);
            Vector2 topLeft = _root.InverseTransformPoint(corners[1]);   // world to the canvas, whatever its render mode
            var size = _root.rect.size;
            float x = Mathf.Clamp(topLeft.x + size.x / 2, 8, size.x - 348);
            if (below)   // the toolbar is at the top of the screen: the tip hangs under the button
            {
                Vector2 bottomLeft = _root.InverseTransformPoint(corners[0]);
                _tip.pivot = new Vector2(0, 1);
                _tip.anchoredPosition = new Vector2(x, bottomLeft.y + size.y / 2 - 6);
            }
            else _tip.anchoredPosition = new Vector2(x, topLeft.y + size.y / 2 + 6);
            _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling();
        }

        // ---------- Cursor tip: what will happen on click, next to the mouse ----------
        void BuildCursorTip(RectTransform root)
        {
            _cursorTip = MakePanel(root, "CursorTip", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _cursorTip.pivot = new Vector2(0, 1);
            _cursorTip.GetComponent<Image>().raycastTarget = false;
            var fit = _cursorTip.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(8, 8, 4, 4); fit.childControlWidth = fit.childControlHeight = true;
            var csf = _cursorTip.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cursorText = MakeText(_cursorTip, "Text", 14, TextAnchor.MiddleLeft);
            _cursorText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cursorTip.gameObject.SetActive(false);
        }

        /// <summary>Show a short line beside the mouse (null hides it); bad = red, for "this will not work here, because...".</summary>
        public void SetCursorTip(string text, bool bad, Vector2 screenPos, bool warn = false)
        {
            bool on = !string.IsNullOrEmpty(text);
            _cursorTip.gameObject.SetActive(on);
            if (!on) return;
            _cursorText.text = text;
            _cursorText.color = bad ? BadInk : warn ? Pulse : Ink;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPos, null, out var local);
            var size = _root.rect.size;
            float w = _cursorTip.rect.width;
            float x = local.x + size.x / 2 + 20, y = local.y + size.y / 2 - 18;
            if (x + w > size.x - 8) x = local.x + size.x / 2 - 20 - w;   // flip to the left near the right edge
            _cursorTip.anchoredPosition = new Vector2(x, y);
        }

        void BuildBannerPicker(RectTransform root)
        {
            var anchor = Vector2.one;
            var panel = MakePanel(root, "BannerColors", anchor, anchor, new Vector2(-292, -372), new Vector2(-8, -222));
            _bannerPanel = panel;
            // the old floating Banner button is retired: System (Esc) > Banner colour opens this
            MakeButton(root, "Banner", anchor, new Vector2(-82, -203), new Vector2(148, 30),
                () => panel.gameObject.SetActive(!panel.gameObject.activeSelf)).gameObject.SetActive(false);

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

        public bool EditingBanner => (_bannerInput != null && _bannerInput.isFocused) || EditingMilitaryName || EditingCatalogueSearch;

        public void ShowBannerColor(string hex) => _bannerInput.SetTextWithoutNotify(hex ?? "#B3202A");

        public bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void SetSpeed(int s) { _speedNow = s; foreach (var kv in _speedButtons) kv.Value.color = kv.Key == s ? BtnOn : Btn; }

        /// <summary>Demand for homes, -1..1: a centred meter, green when homes are wanted.</summary>
        public void SetDemand(double v)
        {
            float f = Mathf.Clamp01((float)(v + 1) / 2f);
            var rt = _demandFill.rectTransform; rt.anchorMax = new Vector2(f, 1);
            bool wanted = v > World.DemandThreshold;
            _demandFill.color = wanted ? new Color(0.45f, 0.78f, 0.35f) : v < -0.05 ? new Color(0.80f, 0.40f, 0.30f) : new Color(0.65f, 0.60f, 0.45f);
            _demandText.text = wanted ? "Homes wanted" : v < -0.05 ? "Homes: not wanted" : "Homes: enough";
        }

        public void SetInfo(string text)
        {
            _infoPanel.SetActive(!string.IsNullOrEmpty(text));
            var ins = CrownAndHearth.Game.UI.InspectionData.Parse(text);   // heading, facts as rows, problems marked (Game/UI/InspectionData.cs)
            _infoTitle.text = ins.Title;
            _infoText.supportRichText = true;
            _infoText.text = CrownAndHearth.Game.UI.InspectionData.Body(ins);
            // fit the panel to its text, so it does not hide a big piece of the map
            var pr = (RectTransform)_infoPanel.transform;
            // measured at the panel's text width (the panel may have just been switched on, before its layout ran)
            float textH = _infoText.cachedTextGeneratorForLayout.GetPreferredHeight(_infoText.text, _infoText.GetGenerationSettings(new Vector2(298, 0))) / _infoText.pixelsPerUnit;
            pr.offsetMin = new Vector2(pr.offsetMin.x, pr.offsetMax.y - Mathf.Min(430, 84 + textH));
        }

        Text MakeStat(RectTransform row, string label, string caption = null)
        {
            var card = MakePanel(row, label + "Card", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            card.GetComponent<Image>().color = Btn;
            var title = MakeText(card, label, UiTheme.SmallSize, TextAnchor.MiddleLeft);
            title.text = caption ?? label; title.color = Dim;
            Stretch(title.rectTransform, new Vector2(8, 0), Vector2.zero);
            var value = MakeText(card, label + "Value", UiTheme.StatSize, TextAnchor.MiddleRight);
            Stretch(value.rectTransform, new Vector2(0, 0), new Vector2(-8, 0));
            return value;
        }

        public void SetSettlement(World world)
        {
            if (_world != world) _initialWater = world.PlanWater();
            _world = world;
            _day.text = world.SeasonalFarming ? $"{world.Season} {1 + (world.YearDay - 1) % World.SeasonLength} / Y{world.Year}" : world.Day.ToString();
            _population.text = world.Starving ? "Starving" : world.Soldiers > 0 ? $"{world.Peasants} + {world.Soldiers} troops/{world.D.Civ} · {world.D.Idle} idle" : $"{world.Peasants}/{world.D.Civ} · {world.D.Idle} idle";
            UpdateStockpiles(world);
            UpdateNeeds(world);
            UpdateCampaign(world);
            UpdateGuide(world);
            UpdateVessels(world);
            UpdateScenario(world);
            UpdateIncident(world);
            UpdateWarband(world);
            RefreshSettlementPanel();
            RefreshManagement(world);
            RefreshCatalogue();
            // prices turn red on the cards the town cannot afford yet
            foreach (var t in _tools)
                if (t.Build != null && _cardCost.TryGetValue(t.Id, out var cost)) cost.color = world.CanAfford(t.Build).Ok ? Ink : BadInk;
        }

        /// <summary>The banner for a spotted warband: who, how many, how soon and from which side.</summary>
        void UpdateWarband(World world)
        {
            RefreshWarbandOnMap(world);
            var wb = world.Warband; var camp = wb != null && wb.Spotted ? world.CampById(wb.CampId) : null;
            bool show = camp != null && !_campOpen;
            if (_warbandPanel.activeSelf != show) _warbandPanel.SetActive(show);
            if (!show) return;
            int left = wb.ArriveDay - world.Day;
            string when = left <= 0 ? "arriving now" : left == 1 ? "1 day away" : $"{left} days away";
            _warbandText.text = $"<b>Warband</b> from the {camp.Name}: {wb.Band} raiders, {when}, coming from the {world.CampSideName(camp)}";
        }

        /// <summary>A message from the game: it goes into the message log (repeats are folded) and, when new, shows as a toast.</summary>
        public void Notify(string text, int day, float secs = 6f)
        {
            var note = Notes.Add(day, text, null, out bool isNew);
            if (!isNew) return;
            bool critical = note.Kind == CrownAndHearth.Game.UI.NoteKind.Danger;
            Toast(critical ? "Critical: " + text : text, critical ? Mathf.Max(secs, 9f) : secs);   // a critical message stays longer, in the critical colour
            _toastText.color = critical ? UiTheme.Critical : note.Kind == CrownAndHearth.Game.UI.NoteKind.Warning ? UiTheme.Warning : Ink;
        }

        public void Toast(string text, float secs = 3.5f) { _toastText.text = text; _toastText.color = Ink; _toastUntil = Time.unscaledTime + secs; }

        void Update()
        {
            UpdateCamp();
            if (_toastText != null && Time.unscaledTime > _toastUntil && _toastText.text.Length > 0) _toastText.text = "";
        }

        void ToggleWorkforce()
        {
            bool opening = !_workPanel.activeSelf;
            if (opening) ShowTownMenu(TownMenu.Closed);
            _workPanel.SetActive(opening);
            WorkforceToggled?.Invoke(opening);
        }

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
                string wanted = w.TradeTarget.TryGetValue(t, out var hand) ? hand.ToString() : "auto";
                row.text = $"{i + 1}. {Defs.TradeNames[t]}   working {workers}  wanted {wanted}  jobs {jobs}   {blds} bldg" + (shortCount > 0 ? $"   {shortCount} short" : "");
                row.color = shortCount > 0 ? new Color(1f, 0.6f, 0.5f) : Ink;
                var rt = row.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.offsetMin = new Vector2(0, y - 34); rt.offsetMax = new Vector2(-250, y);
                string id = t;
                MakeButton(_workRows, "Up", new Vector2(1, 1), new Vector2(-58, y - 17), new Vector2(36, 28), () => PrioMoved?.Invoke(id, -1));
                MakeButton(_workRows, "Dn", new Vector2(1, 1), new Vector2(-18, y - 17), new Vector2(36, 28), () => PrioMoved?.Invoke(id, 1));
                MakeButton(_workRows, "+", new Vector2(1, 1), new Vector2(-108, y - 17), new Vector2(36, 28), () => WorkersAdjusted?.Invoke(id, 1));
                MakeButton(_workRows, "-", new Vector2(1, 1), new Vector2(-148, y - 17), new Vector2(36, 28), () => WorkersAdjusted?.Invoke(id, -1));
                MakeButton(_workRows, "Auto", new Vector2(1, 1), new Vector2(-208, y - 17), new Vector2(60, 28), () => WorkersAdjusted?.Invoke(id, 0));
            }
            _workText.text = $"Employed {w.D.Employed} of {w.Peasants} peasants, {w.D.Idle} available (idle), {w.D.Jobs} jobs in all. + and - set how many work a trade; Auto fills every job.";
        }

        // ---------- Icons: building art where it exists, small code-drawn pictures for the rest ----------
        public static string CostText(string type) =>
            string.Join(", ", Defs.CostOf(type, Defs.PlacementSize(type)).Select(kv => $"{kv.Value} {kv.Key.ToString().ToLower()}"));

        static string ArtSlot(string type)
        {
            if (Defs.PlacementSize(type) == 2 && (type == "wood" || type == "mine" || type == "quarry" || type == "farm" || type == "market" || type == "forester" || type == "sawmill")) return type + "2";
            return type;
        }

        Sprite ToolIcon(GameController.ToolDef t)
        {
            if (t.Build != null) return _art?.Building(ArtSlot(t.Build), 0) ?? Glyph("box", Dim);
            if (World.IsLivestockZone(t.Zone)) return _art?.SettlementWork.Animal(t.Zone) ?? Glyph("zone", ZoneColor(t.Zone));
            if (t.Zone != Zone.None) return Glyph("zone", ZoneColor(t.Zone));
            if (t.Road) return Glyph("road", new Color(0.55f, 0.42f, 0.28f));
            if (t.Wall) return Glyph("wall", new Color(0.72f, 0.5f, 0.24f));
            if (t.Unzone) return Glyph("unzone", new Color(0.85f, 0.85f, 0.85f));
            if (t.Claim) return Glyph("claim", new Color(0.95f, 0.78f, 0.25f));
            if (t.Clear) return Glyph("cross", new Color(0.85f, 0.30f, 0.25f));
            return Glyph("lens", Ink);
        }

        Sprite CatIcon(GameController.CatDef cat, List<GameController.ToolDef> tools)
        {
            if (cat.Icon != null) { var s = _art?.Building(cat.Icon, 0); if (s != null) return s; }
            return tools.Count > 0 ? ToolIcon(tools[0]) : Glyph("box", Dim);
        }

        public static Color ZoneColor(Zone z, bool flax = false) => flax ? new Color(0.45f, 0.58f, 0.95f) : z switch
        {
            Zone.Res => new Color(0.92f, 0.66f, 0.32f),
            Zone.Res2 => new Color(0.62f, 0.72f, 0.92f),
            Zone.Farm => new Color(0.92f, 0.80f, 0.25f),
            Zone.Wood => new Color(0.36f, 0.72f, 0.32f),
            Zone.Mine => new Color(0.82f, 0.48f, 0.46f),
            _ => Color.white,
        };

        readonly Dictionary<string, Sprite> _glyphs = new Dictionary<string, Sprite>();

        /// <summary>A 32x32 pixel-art icon drawn in code: an iso diamond (zone, road, unzone), a cross, a magnifying lens or a box.</summary>
        Sprite Glyph(string kind, Color c)
        {
            string key = kind + ColorUtility.ToHtmlStringRGB(c);
            if (_glyphs.TryGetValue(key, out var cached)) return cached;
            const int S = 32;
            var px = new Color32[S * S];
            Color32 col = c, dark = Color.Lerp(c, Color.black, 0.45f), clear = new Color32(0, 0, 0, 0), white = new Color32(245, 235, 210, 255);
            void Set(int x, int y, Color32 v) { if (x >= 0 && y >= 0 && x < S && y < S) px[y * S + x] = v; }
            bool InDiamond(int x, int y) => Mathf.Abs(x - 15.5f) / 15f + Mathf.Abs(y - 15.5f) / 8f <= 1f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Color32 v = clear;
                    switch (kind)
                    {
                        case "zone": if (InDiamond(x, y)) v = ((x + y) / 3) % 2 == 0 ? col : dark; break;
                        case "unzone": if (InDiamond(x, y)) v = new Color32(120, 120, 110, 255); break;
                        case "road": if (InDiamond(x, y)) v = Mathf.Abs((x - 15.5f) - 2 * (y - 15.5f)) < 3f ? white : col; break;
                        case "wall":
                            if (x >= 4 && x < 28 && y >= 6 && y <= 26 - Mathf.Abs((x - 4) % 4 - 1))
                                v = y == 9 || y == 16 || (x - 4) % 4 == 0 ? dark : col;
                            break;
                        case "box": if (x > 6 && x < 25 && y > 6 && y < 25) v = col; break;
                        case "lens":
                            float d = Vector2.Distance(new Vector2(x, y), new Vector2(18, 19));
                            if (d < 9 && d > 6) v = col; else if (d <= 6) v = new Color32(120, 160, 200, 160);
                            if (x - y > -2 && x - y < 2 && x < 13 && x > 4) v = col; break;
                        case "claim": if (InDiamond(x, y)) v = Mathf.Abs(x - 15.5f) / 13f + Mathf.Abs(y - 15.5f) / 6.5f > 1f ? col : new Color32(70, 98, 56, 255); break;
                        case "cross": if ((Mathf.Abs(x - y) < 3 || Mathf.Abs(x + y - 31) < 3) && x > 4 && x < 27) v = col; break;
                    }
                    Set(x, y, v);
                }
            if (kind == "unzone")   // a red slash over the grey tile
                for (int i = 4; i < 28; i++) for (int k = -1; k <= 1; k++) Set(i, (i - 4) * 16 / 24 + 8 + k, new Color32(220, 70, 60, 255));
            if (kind == "zone" || kind == "road" || kind == "unzone")   // outline the diamond
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                        if (InDiamond(x, y) && (!InDiamond(x - 1, y) || !InDiamond(x + 1, y) || !InDiamond(x, y - 1) || !InDiamond(x, y + 1))) Set(x, y, new Color32(30, 22, 15, 255));
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            _glyphs[key] = sprite;
            return sprite;
        }

        // ---------- uGUI helpers ----------
        RectTransform MakePanel(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            go.GetComponent<Image>().color = Panel;
            var edge = go.AddComponent<Outline>(); edge.effectColor = UiTheme.Border; edge.effectDistance = new Vector2(1, -1); edge.useGraphicAlpha = false;   // a hairline, not a frame
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

        Image MakeIcon(RectTransform parent, Sprite s)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false;
            if (s == null) img.color = Color.clear;
            return img;
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

        /// <summary>A category button: picture on top, name below, its hotkey in the corner.</summary>
        Image MakeIconButton(RectTransform parent, string label, string key, Sprite icon, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = Vector2.zero; rt.sizeDelta = size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>(); img.color = Btn;
            var b = go.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(() => onClick());
            var ic = MakeIcon(rt, icon);
            var ir = ic.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1); ir.pivot = new Vector2(0.5f, 1);
            ir.sizeDelta = new Vector2(size.x - 8, size.y - 24); ir.anchoredPosition = new Vector2(0, -2);
            var t = MakeText(rt, "Label", 13, TextAnchor.LowerCenter); t.text = label;
            Stretch(t.rectTransform, new Vector2(0, 3), Vector2.zero);
            var k = MakeText(rt, "Key", 12, TextAnchor.UpperLeft); k.text = key; k.color = Dim;
            Stretch(k.rectTransform, new Vector2(5, 0), new Vector2(0, -3));
            return img;
        }

        static void AddHover(GameObject go, Action<bool> onHover) => go.AddComponent<HoverRelay>().Changed += onHover;

        static void Stretch(RectTransform rt, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }

    /// <summary>Tells the HUD when the pointer enters or leaves a button (for hover colours and tooltips).</summary>
    public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<bool> Changed;
        public void OnPointerEnter(PointerEventData e) => Changed?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => Changed?.Invoke(false);
        void OnDisable() => Changed?.Invoke(false);
    }
}
