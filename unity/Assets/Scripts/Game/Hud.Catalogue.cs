// The Build catalogue: one Build button opens a large window of building cards, grouped into the nine catalogue categories, with search,
// favourites and recently used. Cards come from the tool list (GameController.Tools), so a building added there appears here by itself.
// Picking a card closes the window and starts placement with the existing tool mechanism.
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
        const string CatalogueKey = "thornwick.catalogue";
        public readonly BuildCatalogue Catalogue = new BuildCatalogue();
        RectTransform _catWin, _catContent, _catSideRoot;
        InputField _catSearch; Text _catTitle, _catEmpty;
        Image _buildButton;
        string _catQuery = "";
        CatalogueView _catView = CatalogueView.Category;
        enum CatalogueView { Category, Recent, Favourites }
        readonly Dictionary<string, CatalogueCard> _catCards = new Dictionary<string, CatalogueCard>();
        readonly List<(Image img, Text label, CatalogueView view, CatalogueCategory cat)> _catSide = new List<(Image, Text, CatalogueView, CatalogueCategory)>();

        sealed class CatalogueCard { public RectTransform Root; public Image Back; public Text Cost, Status, Star; public GameController.ToolDef Tool; public CatalogueEntry Entry; }

        public bool CatalogueOpen => _catWin != null && _catWin.gameObject.activeSelf;
        public bool EditingCatalogueSearch => _catSearch != null && _catSearch.isFocused;

        static string ToolKind(GameController.ToolDef t) =>
            t.Claim ? "Click a 16x16 parcel" : t.Zone != Zone.None ? "Zone: drag an area" : t.Unzone ? "Drag an area" : t.Clear ? "Click or drag" :
            t.Road || t.Wall || t.Bridge || t.Canal || t.Moat ? "Drag a line" : t.FerryRoute ? "Click two landings" :
            t.Build != null ? $"{Defs.PlacementSize(t.Build)}x{Defs.PlacementSize(t.Build)} building" : "";

        static string ToolCost(GameController.ToolDef t) =>
            t.Claim ? "Free" : t.Build != null ? CostText(t.Build) : t.Road ? "1 wood a tile" : t.Wall ? "2 wood a tile" :
            t.Bridge ? $"{World.BridgeWood} wood a tile" : t.Canal ? $"{World.CanalWood} wood a tile" : t.Moat ? $"{World.MoatWood} wood a tile" :
            t.FerryRoute ? $"{World.FerryRouteCost} gold" : World.IsLivestockZone(t.Zone) ? $"Fence wood + {World.StarterLivestockGold} gold" : t.Zone != Zone.None ? "Free" : "";

        void BuildCatalogueWindow(RectTransform root)
        {
            foreach (var t in _tools.Where(t => BuildCatalogue.IsConstruction(t.Id)))
                Catalogue.Add(new CatalogueEntry { Id = t.Id, Label = t.Label, Description = t.Desc, Kind = ToolKind(t), CostText = ToolCost(t), Category = BuildCatalogue.CategoryOf(t.Id, t.Cat.ToString()) });
            Catalogue.Restore(PlayerPrefs.GetString(CatalogueKey, ""));

            // the Build button, bottom right: the one permanent way into construction
            var bb = MakePanel(root, "BuildButton", Vector2.right, Vector2.right, new Vector2(-174, BottomH + 58), new Vector2(-8, BottomH + 116));
            _buildButton = bb.GetComponent<Image>(); _buildButton.color = UiTheme.ButtonOn;
            var bbt = bb.gameObject.AddComponent<Button>(); bbt.transition = Selectable.Transition.None; bbt.onClick.AddListener(() => ToggleCatalogue());
            var bl = MakeText(bb, "Label", 22, TextAnchor.MiddleCenter); bl.text = "Build  (B)"; bl.fontStyle = FontStyle.Bold; Stretch(bl.rectTransform, Vector2.zero, Vector2.zero);
            AddHover(bb.gameObject, on => ShowTip(on, bb, "Build", "Open the building catalogue: housing, farms, resources, industry, services, trade, military. Pick a building, then click the map to place it.", ""));

            _catWin = MakePanel(root, "Catalogue", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560, -310), new Vector2(560, 330));
            _catWin.GetComponent<Image>().color = UiTheme.WindowFill;
            _catTitle = MakeText(_catWin, "Title", UiTheme.TitleSize, TextAnchor.UpperLeft); _catTitle.fontStyle = FontStyle.Bold;
            _catTitle.rectTransform.anchorMin = Vector2.up; _catTitle.rectTransform.anchorMax = Vector2.up; _catTitle.rectTransform.pivot = Vector2.up;
            _catTitle.rectTransform.sizeDelta = new Vector2(500, 32); _catTitle.rectTransform.anchoredPosition = new Vector2(240, -12);
            MakeButton(_catWin, "X", Vector2.one, new Vector2(-26, -24), new Vector2(34, 32), () => ShowCatalogue(false));

            // search
            var sp = MakePanel(_catWin, "Search", Vector2.one, Vector2.one, new Vector2(-290, -42), new Vector2(-52, -10));
            sp.GetComponent<Image>().color = UiTheme.Background;
            _catSearch = sp.gameObject.AddComponent<InputField>();
            var st = MakeText(sp, "Text", 15, TextAnchor.MiddleLeft); Stretch(st.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
            var ph = MakeText(sp, "Placeholder", 15, TextAnchor.MiddleLeft); ph.text = "Search buildings"; ph.color = UiTheme.TextDim; Stretch(ph.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
            _catSearch.textComponent = st; _catSearch.placeholder = ph; _catSearch.targetGraphic = sp.GetComponent<Image>(); _catSearch.characterLimit = 32;
            _catSearch.onValueChanged.AddListener(q => { _catQuery = q; RefreshCatalogue(); });

            // categories down the left
            _catSideRoot = MakePanel(_catWin, "Sidebar", Vector2.zero, new Vector2(0, 1), new Vector2(8, 8), new Vector2(222, -52));
            _catSideRoot.GetComponent<Image>().color = Color.clear;
            var side = _catSideRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            side.spacing = 4; side.childControlWidth = side.childControlHeight = true; side.childForceExpandWidth = true; side.childForceExpandHeight = false;
            foreach (var c in Catalogue.VisibleCategories()) AddSideButton(BuildCatalogue.Title(c), CatalogueView.Category, c);
            AddSideButton("Recently used", CatalogueView.Recent, default);
            AddSideButton("Favourites", CatalogueView.Favourites, default);

            // the card grid, scrolling
            var viewport = MakePanel(_catWin, "Viewport", Vector2.zero, Vector2.one, new Vector2(232, 8), new Vector2(-8, -52));
            viewport.GetComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
            _catContent = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _catContent.SetParent(viewport, false); _catContent.anchorMin = Vector2.up; _catContent.anchorMax = Vector2.one; _catContent.pivot = Vector2.up; _catContent.offsetMin = new Vector2(0, 0); _catContent.offsetMax = Vector2.zero;
            var grid = _catContent.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(284, 176); grid.spacing = new Vector2(8, 8); grid.padding = new RectOffset(2, 2, 2, 2);
            _catContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = _catContent; scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            _catEmpty = MakeText(viewport, "Empty", 16, TextAnchor.MiddleCenter); _catEmpty.color = UiTheme.TextDim; Stretch(_catEmpty.rectTransform, Vector2.zero, Vector2.zero);

            foreach (var e in Catalogue.Entries) _catCards[e.Id] = MakeCatalogueCard(_catContent, _tools.First(t => t.Id == e.Id), e);
            _catWin.gameObject.SetActive(false);
        }

        void AddSideButton(string title, CatalogueView view, CatalogueCategory cat)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_catSideRoot, false); go.GetComponent<LayoutElement>().preferredHeight = 38;
            var img = go.GetComponent<Image>(); img.color = Btn; var b = go.GetComponent<Button>(); b.transition = Selectable.Transition.None;
            var t = MakeText((RectTransform)go.transform, "Label", 15, TextAnchor.MiddleLeft); t.text = title; Stretch(t.rectTransform, new Vector2(10, 0), new Vector2(-6, 0)); FitText(t, 11, 15);
            b.onClick.AddListener(() => { _catView = view; if (view == CatalogueView.Category) Catalogue.LastCategory = cat; _catSearch.SetTextWithoutNotify(""); _catQuery = ""; RefreshCatalogue(); });
            _catSide.Add((img, t, view, cat));
        }

        CatalogueCard MakeCatalogueCard(RectTransform parent, GameController.ToolDef tool, CatalogueEntry e)
        {
            var card = MakePanel(parent, tool.Id, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var back = card.GetComponent<Image>(); back.color = UiTheme.PanelSecondary;
            var btn = card.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            string id = tool.Id; btn.onClick.AddListener(() => PickFromCatalogue(id));
            AddHover(card.gameObject, on => { back.color = on ? UiTheme.ButtonHover : UiTheme.PanelSecondary; ShowToolTip(on ? tool : null, card); });
            var icon = MakeIcon(card, ToolIcon(tool)); var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0, 1); ir.pivot = new Vector2(0, 1); ir.sizeDelta = new Vector2(92, 92); ir.anchoredPosition = new Vector2(8, -8);
            var name = MakeText(card, "Name", 17, TextAnchor.UpperLeft); name.text = e.Label; name.fontStyle = FontStyle.Bold;
            Stretch(name.rectTransform, new Vector2(108, 138), new Vector2(-34, -8)); FitText(name, 10, 17);
            var kind = MakeText(card, "Kind", UiTheme.SmallSize, TextAnchor.UpperLeft); kind.text = e.Kind; kind.color = UiTheme.TextDim;
            Stretch(kind.rectTransform, new Vector2(108, 118), new Vector2(-8, -38));
            var desc = MakeText(card, "Desc", UiTheme.SmallSize, TextAnchor.UpperLeft); desc.text = BuildCatalogue.Short(e.Description, 120);
            Stretch(desc.rectTransform, new Vector2(108, 52), new Vector2(-8, -58)); desc.verticalOverflow = VerticalWrapMode.Truncate; desc.color = UiTheme.Text;
            var cost = MakeText(card, "Cost", UiTheme.BodySize - 1, TextAnchor.LowerLeft); cost.supportRichText = true;
            Stretch(cost.rectTransform, new Vector2(8, 4), new Vector2(-8, -152)); FitText(cost, 10, UiTheme.BodySize - 1);
            var status = MakeText(card, "Status", UiTheme.SmallSize, TextAnchor.LowerLeft);
            Stretch(status.rectTransform, new Vector2(8, 26), new Vector2(-8, -128)); FitText(status, 9, UiTheme.SmallSize);
            var sg = new GameObject("Star", typeof(RectTransform), typeof(Image), typeof(Button)); var sr = (RectTransform)sg.transform; sr.SetParent(card, false);
            sr.anchorMin = sr.anchorMax = Vector2.one; sr.pivot = Vector2.one; sr.sizeDelta = new Vector2(26, 26); sr.anchoredPosition = new Vector2(-4, -4);
            sg.GetComponent<Image>().color = Color.clear;
            var star = MakeText(sr, "Glyph", 20, TextAnchor.MiddleCenter); Stretch(star.rectTransform, Vector2.zero, Vector2.zero);
            sg.GetComponent<Button>().onClick.AddListener(() => { Catalogue.ToggleFavourite(id); SaveCatalogue(); RefreshCatalogue(); });
            return new CatalogueCard { Root = card, Back = back, Cost = cost, Status = status, Star = star, Tool = tool, Entry = e };
        }

        void SaveCatalogue() { PlayerPrefs.SetString(CatalogueKey, Catalogue.Serialize()); PlayerPrefs.Save(); }

        /// <summary>Type into the search box (also used by the screenshot tool).</summary>
        public void SearchCatalogue(string q) { _catSearch.SetTextWithoutNotify(q); _catQuery = q; RefreshCatalogue(); }

        public void ToggleCatalogue() => ShowCatalogue(!CatalogueOpen);

        public void ShowCatalogue(bool on)
        {
            if (_catWin == null || _catWin.gameObject.activeSelf == on) return;
            if (on) { CloseAllMenus(); ShowTray(null); _catView = CatalogueView.Category; _catQuery = ""; _catSearch.SetTextWithoutNotify(""); }
            _catWin.gameObject.SetActive(on);
            if (on) { _catWin.SetAsLastSibling(); RefreshCatalogue(); }
            ShowTip(false, null, "", "", "");
            if (_buildButton != null) _buildButton.color = on ? UiTheme.Selected : UiTheme.ButtonOn;
        }

        void PickFromCatalogue(string id)
        {
            Catalogue.Used(id); SaveCatalogue();
            ShowCatalogue(false);
            ToolPicked?.Invoke(id);
        }

        /// <summary>How a card stands right now: buildable, or short of resources (and which).</summary>
        CardStatus StatusOf(GameController.ToolDef t, out string note)
        {
            note = "";
            if (t.Build != null && _world != null) { var r = _world.CanAfford(t.Build); if (!r.Ok) { note = r.Reason; return CardStatus.Unaffordable; } }
            return CardStatus.Available;
        }

        void RefreshCatalogue()
        {
            if (!CatalogueOpen) return;
            List<CatalogueEntry> shown; string title;
            if (_catQuery.Trim().Length > 0) { shown = Catalogue.Search(_catQuery); title = "Search: " + _catQuery.Trim(); }
            else if (_catView == CatalogueView.Recent) { shown = Catalogue.RecentEntries(); title = "Recently used"; }
            else if (_catView == CatalogueView.Favourites) { shown = Catalogue.FavouriteEntries(); title = "Favourites"; }
            else { shown = Catalogue.In(Catalogue.LastCategory); title = BuildCatalogue.Title(Catalogue.LastCategory); }
            _catTitle.text = "Build  -  " + title;
            _catEmpty.text = shown.Count > 0 ? "" : _catQuery.Trim().Length > 0 ? "No building matches that." : _catView == CatalogueView.Recent ? "Nothing placed yet. Buildings you place show up here." :
                _catView == CatalogueView.Favourites ? "Click the star on a card to keep it here." : "";
            var inView = new HashSet<string>(shown.Select(e => e.Id));
            int index = 0;
            foreach (var e in shown) _catCards[e.Id].Root.SetSiblingIndex(index++);
            foreach (var kv in _catCards)
            {
                var c = kv.Value; bool on = inView.Contains(kv.Key); c.Root.gameObject.SetActive(on); if (!on) continue;
                var status = StatusOf(c.Tool, out string note);
                c.Back.color = UiTheme.PanelSecondary;
                c.Star.text = Catalogue.IsFavourite(kv.Key) ? "★" : "☆"; c.Star.color = Catalogue.IsFavourite(kv.Key) ? UiTheme.Gold : UiTheme.TextDim;
                c.Cost.text = CostLine(c.Tool, c.Entry.CostText);
                c.Status.text = status == CardStatus.Available ? "Available" : status == CardStatus.Unaffordable ? note : "Locked";
                c.Status.color = status == CardStatus.Available ? UiTheme.Positive : status == CardStatus.Unaffordable ? UiTheme.Warning : UiTheme.Critical;
            }
            foreach (var (img, label, view, cat) in _catSide)
            {
                bool sel = _catQuery.Trim().Length == 0 && view == _catView && (view != CatalogueView.Category || cat == Catalogue.LastCategory);
                img.color = sel ? UiTheme.ButtonOn : UiTheme.PanelSecondary;
            }
        }

        /// <summary>The price with each resource coloured: red when the town is short of it, so what is missing is visible at a glance.</summary>
        string CostLine(GameController.ToolDef t, string plain)
        {
            if (t.Build == null || _world == null) return "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Text) + ">" + plain + "</color>";
            var parts = new List<string>();
            foreach (var kv in Defs.CostOf(t.Build, Defs.PlacementSize(t.Build)))
            {
                bool lacking = _world.ResAmt[kv.Key] < kv.Value;
                parts.Add("<color=#" + ColorUtility.ToHtmlStringRGB(lacking ? UiTheme.Critical : UiTheme.Text) + ">" + kv.Value + " " + kv.Key.ToString().ToLower() + (lacking ? " (short)" : "") + "</color>");
            }
            return string.Join(", ", parts);
        }
    }
}
