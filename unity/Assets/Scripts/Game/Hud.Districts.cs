// The Districts panel (key Y, or the Districts button under the goal chip): a LARGE management panel (Zach's rule: big menus are large panels with exact numeric entry).
// Left: every district (zone, size, state). Right: the standard district inspector: name (rename), zone, area, buildings, workers, output, development priority
// (Disabled .. Critical), mode (Automatic / Approval required / Paused), a plain-words explanation of why it is or is not developing, its open problems, its queue
// of projects (approve, reject, pause, cancel, move up), and the optional advanced limits with exact number fields. A player can zone and never open this.
// The rules and numbers are in Sim/World.Districts.cs, World.Projects.cs and World.DistrictControls.cs (the same calls a governor uses).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        RectTransform _distPanel, _distBody;
        Image _distButton;
        int _distSelected, _distPage;
        string _distKey = "";
        readonly Dictionary<string, string> _distDraft = new Dictionary<string, string>();   // typed but not yet applied field text, kept across refreshes
        const int DistRows = 15;

        public bool DistrictsOpen => _distPanel != null && _distPanel.gameObject.activeSelf;
        public int DistrictSelected => _distSelected;

        void BuildDistricts(RectTransform root)
        {
            _distButton = MakeButton(_goalCard, "Districts", new Vector2(1, 0.5f), new Vector2(-138, 0), new Vector2(84, 24), () => ToggleDistricts());
            _distPanel = MakePanel(root, "Districts", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-540, -380), new Vector2(540, 380));
            var title = MakeText(_distPanel, "Title", 22, TextAnchor.UpperLeft); title.fontStyle = FontStyle.Bold; title.color = UiTheme.Gold;
            title.text = "Districts: where your settlement grows by itself";
            var tr = title.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.offsetMin = new Vector2(16, -38); tr.offsetMax = new Vector2(-130, -8);
            MakeButton(_distPanel, "Close", Vector2.one, new Vector2(-50, -26), new Vector2(80, 32), () => ToggleDistricts());
            _distBody = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            _distBody.SetParent(_distPanel, false);
            Stretch(_distBody, new Vector2(0, 0), new Vector2(0, -48));
            _distPanel.gameObject.SetActive(false);
        }

        public void ToggleDistricts(int select = 0)
        {
            bool open = !DistrictsOpen || select > 0;
            if (open) { ShowTownMenu(TownMenu.Closed); CloseAllMenus(); _distKey = ""; if (select > 0) _distSelected = select; }
            _distPanel.gameObject.SetActive(open);
            if (open) { _distPanel.SetAsLastSibling(); if (_world != null) UpdateDistricts(_world); }
        }

        // ---- test and governor handles: find a control by name ----
        public RectTransform DistrictControl(string name) => FindDeep(_distBody, name) as RectTransform;
        public InputField DistrictField(string name) => DistrictControl("Field:" + name)?.GetComponent<InputField>();

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform c in root) { if (c.name == name) return c; var d = FindDeep(c, name); if (d != null) return d; }
            return null;
        }

        string DistKey(World w)
        {
            var sb = new StringBuilder();
            sb.Append(_distSelected).Append('|').Append(_distPage).Append('|').Append(w.Reserve.KeepWood).Append(',').Append(w.Reserve.KeepStone).Append(',').Append(w.Reserve.KeepGold).Append(',').Append(w.Reserve.MaxOpenProjects);
            foreach (var d in w.Districts)
                sb.Append(';').Append(d.Id).Append(d.Name).Append(d.Area).Append(',').Append(d.Buildings.Count).Append(',').Append(d.Workers).Append('/').Append(d.Jobs).Append(',').Append((int)d.Priority).Append((int)d.Mode).Append(d.Note)
                  .Append(d.Limits.MaxBuildings).Append(',').Append(d.Limits.MaxEmployment).Append(',').Append(d.Limits.MaxSpend).Append(',').Append(d.Limits.MinWood).Append(',').Append(d.Limits.MinStone).Append(',').Append(d.Limits.ProductionTarget).Append((int)d.Limits.HarvestMode);
            foreach (var p in w.Projects.Where(p => p.Open)) sb.Append('#').Append(p.Id).Append((int)p.Status).Append(p.Progress).Append(p.Paused).Append(p.Rank).Append(p.Missing);
            return sb.ToString();
        }

        void UpdateDistricts(World world)
        {
            if (_distPanel == null || !DistrictsOpen) return;
            string key = DistKey(world);
            if (key == _distKey) return;
            _distKey = key;
            RefreshDistricts(world);
        }

        // ---- small builders (top-left origin, pixel units) ----
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        Text DText(RectTransform p, string text, float x, float y, float w, float h, int size = 15, TextAnchor a = TextAnchor.UpperLeft, Color? color = null, bool bold = false)
        {
            var t = MakeText(p, "T", size, a); t.supportRichText = true; t.text = text; if (color.HasValue) t.color = color.Value; if (bold) t.fontStyle = FontStyle.Bold;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            return t;
        }

        Image DButton(RectTransform p, string name, string label, float x, float y, float w, float h, Action click, bool on = false, bool dim = false)
        {
            var img = MakeButton(p, label, new Vector2(0, 1), new Vector2(x + w / 2, -(y + h / 2)), new Vector2(w, h), click);
            img.gameObject.name = name; if (on) img.color = BtnOn; else if (dim) img.color = new Color(0.3f, 0.26f, 0.24f);
            return img;
        }

        InputField DField(RectTransform p, string name, string text, float x, float y, float w, float h, bool numeric, int limit = 32)
        {
            var panel = MakePanel(p, "Field:" + name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -(y + h)), new Vector2(x + w, -y));
            var f = panel.gameObject.AddComponent<InputField>();
            var t = MakeText(panel, "Text", 15, TextAnchor.MiddleLeft); Stretch(t.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
            f.textComponent = t; f.targetGraphic = panel.GetComponent<Image>();
            if (numeric) f.contentType = InputField.ContentType.IntegerNumber;
            f.characterLimit = numeric ? 6 : limit;
            string shown = _distDraft.TryGetValue(name, out var draft) ? draft : text;
            f.SetTextWithoutNotify(shown);
            f.onValueChanged.AddListener(v => _distDraft[name] = v);
            return f;
        }

        static string TypeBlurb(DistrictType t) => t switch
        {
            DistrictType.Residential => "Housing grows here when homes are in demand.",
            DistrictType.Agricultural => "Farmhouses are built where the fields need workers.",
            DistrictType.Forestry => "Woodcutter lodges harvest this forest; it is replanted.",
            DistrictType.Mining => "Mines open where ore lies under the ground.",
            DistrictType.Quarrying => "Quarries dig the surface stone here.",
            DistrictType.Hunting => "Hunting lodges take the game these woods can spare.",
            _ => "Fishing huts line the shore as the fish allow.",
        };

        static int IntOf(string s, int fallback = 0) => int.TryParse(s, out var v) ? Math.Max(0, v) : fallback;

        void RefreshDistricts(World w)
        {
            for (int i = _distBody.childCount - 1; i >= 0; i--) { var old = _distBody.GetChild(i).gameObject; if (Application.isPlaying) Destroy(old); else DestroyImmediate(old); }   // (edit-mode tests cannot Destroy)
            var list = w.Districts.OrderBy(d => d.Type).ThenBy(d => d.Id).ToList();
            var sel = w.DistrictById(_distSelected) ?? list.FirstOrDefault();
            _distSelected = sel?.Id ?? 0;
            var body = _distBody;

            // ---- settlement-wide construction reserve (optional; the defaults need no setup) ----
            DText(body, "Construction reserve (optional): never spend the store below", 16, 6, 470, 22, 14, color: UiTheme.TextDim);
            DText(body, "wood", 480, 6, 40, 22, 14, color: UiTheme.TextDim); DField(body, "ResWood", w.Reserve.KeepWood.ToString(), 520, 3, 60, 24, true);
            DText(body, "stone", 586, 6, 44, 22, 14, color: UiTheme.TextDim); DField(body, "ResStone", w.Reserve.KeepStone.ToString(), 630, 3, 60, 24, true);
            DText(body, "gold", 696, 6, 36, 22, 14, color: UiTheme.TextDim); DField(body, "ResGold", w.Reserve.KeepGold.ToString(), 734, 3, 60, 24, true);
            DText(body, "open projects", 800, 6, 100, 22, 14, color: UiTheme.TextDim); DField(body, "ResMax", w.Reserve.MaxOpenProjects.ToString(), 900, 3, 40, 24, true);
            DButton(body, "ApplyReserve", "Set", 946, 3, 100, 24, () =>
            {
                w.Reserve = new DevReserve { KeepWood = IntOf(Draft("ResWood", w.Reserve.KeepWood)), KeepStone = IntOf(Draft("ResStone", w.Reserve.KeepStone)), KeepGold = IntOf(Draft("ResGold", w.Reserve.KeepGold)), MaxOpenProjects = Math.Clamp(IntOf(Draft("ResMax", w.Reserve.MaxOpenProjects), 3), 1, 20) };
                foreach (var k in new[] { "ResWood", "ResStone", "ResGold", "ResMax" }) _distDraft.Remove(k);
                _distKey = ""; Toast("Construction reserve set.", 3);
            });

            // ---- compact demand indicators: housing, food, timber, stone, minerals (the reasons are in the Economy menu) ----
            {
                var rep = w.DemandReport(); var sbd = new StringBuilder("<b>Demand</b>   ");
                foreach (var it in rep)
                {
                    string col = it.Level == DemandLevel.Critical ? "ff6b57" : it.Level == DemandLevel.High ? "ffa94d" : it.Level == DemandLevel.Some ? Hex(UiTheme.Gold) : Hex(UiTheme.TextDim);
                    sbd.Append($"<color=#{col}>{it.Label} {(it.Level == DemandLevel.None ? "-" : it.Level == DemandLevel.Some ? "+" : it.Level == DemandLevel.High ? "++" : "+++")}</color>   ");
                }
                var big = rep.FirstOrDefault(x => x.Level >= DemandLevel.High);
                if (big != null) sbd.Append($"<color=#{Hex(UiTheme.TextDim)}>{big.Reason}</color>");
                DText(body, sbd.ToString(), 16, 32, 1040, 22, 14);
            }

            // ---- left: the list ----
            int pages = Math.Max(1, (list.Count + DistRows - 1) / DistRows); _distPage = Math.Clamp(_distPage, 0, pages - 1);
            var left = MakePanel(body, "List", new Vector2(0, 0), new Vector2(0, 1), new Vector2(12, 10), new Vector2(332, -60));
            if (list.Count == 0)
                DText(left, "You have no districts yet.\n\nOpen the Build catalogue (B), pick a zone (Residential, Farmland, Forestry, Mining, Quarrying, Hunting or Fishing) and drag over the land. Zoning is free: the settlement builds the rest when it is needed.", 10, 10, 300, 400, 15, color: UiTheme.TextDim);
            for (int i = 0; i < DistRows && _distPage * DistRows + i < list.Count; i++)
            {
                var d = list[_distPage * DistRows + i]; var dd = d;
                string state = d.Priority == DevPriority.Disabled ? "disabled" : d.Mode == DevMode.Paused ? "paused" : d.Mode == DevMode.ApprovalRequired ? "approval" : "";
                bool open = w.ProjectsOf(d).Any();
                var row = DButton(left, "Row:" + d.Id, "", 6, 6 + i * 36, 308, 32, () => { _distSelected = dd.Id; _distKey = ""; }, on: d.Id == _distSelected);
                var label = row.transform.Find("Label")?.GetComponent<Text>();
                if (label != null) { label.alignment = TextAnchor.MiddleLeft; label.supportRichText = true; label.fontSize = 13; label.text = $" {d.Name}\n <color=#{Hex(UiTheme.TextDim)}>{World.TypeName(d.Type)}, {d.Area} tiles, {d.Buildings.Count} building{(d.Buildings.Count == 1 ? "" : "s")}{(state.Length > 0 ? ", " + state : "")}{(open ? ", project" : "")}</color>"; }
            }
            if (pages > 1)
            {
                DButton(body, "Page:Prev", "Prev", 12, 600, 60, 26, () => { _distPage = Math.Max(0, _distPage - 1); _distKey = ""; }, dim: _distPage == 0);
                DText(body, $"{_distPage + 1} / {pages}", 80, 602, 80, 24, 14, TextAnchor.MiddleCenter, UiTheme.TextDim);
                DButton(body, "Page:Next", "Next", 172, 600, 60, 26, () => { _distPage = Math.Min(pages - 1, _distPage + 1); _distKey = ""; }, dim: _distPage == pages - 1);
            }
            if (sel == null) return;

            // ---- right: the inspector ----
            var right = MakePanel(body, "Inspector", new Vector2(0, 0), new Vector2(1, 1), new Vector2(340, 10), new Vector2(-12, -60));
            DText(right, "Name", 12, 10, 50, 26, 15, TextAnchor.MiddleLeft, UiTheme.TextDim);
            DField(right, "Name", sel.Name, 64, 10, 330, 28, false, 40);
            DButton(right, "Rename", "Rename", 402, 10, 90, 28, () =>
            {
                var r = w.RenameDistrict(sel.Id, Draft("Name", sel.Name)); if (r.Ok) _distDraft.Remove("Name"); else Toast(r.Reason, 4); _distKey = "";
            });
            DButton(right, "Rename:Auto", "Auto name", 498, 10, 100, 28, () => { w.RenameDistrict(sel.Id, ""); _distDraft.Remove("Name"); _distKey = ""; });
            double output = w.DistrictOutputPerDay(sel);
            DText(right, $"<b>{World.TypeName(sel.Type)}</b>  ·  {sel.Area} tiles ({sel.OpenTiles} open)  ·  {sel.Buildings.Count} building{(sel.Buildings.Count == 1 ? "" : "s")}  ·  {sel.Workers}/{sel.Jobs} workers  ·  makes about {output:0.#} {World.DistrictOutputUnit(sel.Type)}\n<color=#{Hex(UiTheme.TextDim)}>{TypeBlurb(sel.Type)}</color>", 12, 46, 600, 44, 14);

            DText(right, "Development priority", 12, 98, 200, 22, 15, TextAnchor.UpperLeft, UiTheme.Gold, true);
            float bx = 12;
            foreach (DevPriority p in Enum.GetValues(typeof(DevPriority))) { var pp = p; DButton(right, "Prio:" + p, p.ToString(), bx, 122, 112, 28, () => { w.SetDistrictPriority(sel.Id, pp); _distKey = ""; }, on: sel.Priority == p); bx += 118; }
            DText(right, "Mode", 12, 158, 100, 22, 15, TextAnchor.UpperLeft, UiTheme.Gold, true);
            bx = 12;
            foreach (DevMode m in Enum.GetValues(typeof(DevMode))) { var mm = m; DButton(right, "Mode:" + m, m == DevMode.ApprovalRequired ? "Approval required" : m.ToString(), bx, 182, 176, 28, () => { w.SetDistrictMode(sel.Id, mm); _distKey = ""; }, on: sel.Mode == m); bx += 182; }
            DText(right, "Pausing or disabling stops only NEW buildings. Everything standing keeps working.", 12, 214, 590, 20, 13, color: UiTheme.TextDim);

            var why = new StringBuilder();
            why.Append("<b>Why it is (or is not) developing</b>\n").Append(string.IsNullOrEmpty(sel.Note) ? "Nothing to report yet. The settlement looks at this district every few days." : sel.Note);
            var problems = w.DistrictProblems(sel);
            if (problems.Count > 0) { why.Append("\n<b>Problems</b>"); foreach (var pr in problems) why.Append("\n- ").Append(pr); }
            DText(right, why.ToString(), 12, 238, 590, 118, 14);

            // ---- the queue for this district ----
            DText(right, "Projects", 12, 358, 200, 22, 15, TextAnchor.UpperLeft, UiTheme.Gold, true);
            var projects = w.ProjectsOf(sel).OrderBy(p => p.Id).Take(3).ToList();
            if (projects.Count == 0) DText(right, "No open projects.", 100, 360, 300, 22, 14, color: UiTheme.TextDim);
            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i]; int id = p.Id; float y = 380 + i * 30;
                DText(right, w.ProjectLine(p), 12, y + 2, 400, 28, 12);
                float x = 420;
                if (p.Status == ProjectStatus.Proposed) { DButton(right, "Approve:" + id, "Approve", x, y, 66, 26, () => { w.ApproveProject(id); _distKey = ""; }); DButton(right, "Reject:" + id, "Reject", x + 70, y, 60, 26, () => { w.RejectProject(id); _distKey = ""; }); }
                else { DButton(right, "Pause:" + id, p.Paused ? "Resume" : "Pause", x, y, 66, 26, () => { w.PauseProject(id, !p.Paused); _distKey = ""; }); DButton(right, "Cancel:" + id, "Cancel", x + 70, y, 60, 26, () => { w.CancelProject(id); _distKey = ""; }); }
                DButton(right, "Up:" + id, "Up", x + 134, y, 36, 26, () => { w.RankProject(id, 1); _distKey = ""; });
                DButton(right, "Down:" + id, "Down", x + 174, y, 48, 26, () => { w.RankProject(id, -1); _distKey = ""; });
            }

            // ---- advanced: exact numbers, all optional (0 = no limit) ----
            DText(right, "Advanced limits (optional, 0 = no limit)", 12, 476, 400, 22, 15, TextAnchor.UpperLeft, UiTheme.Gold, true);
            var lim = sel.Limits;
            void Row(string name, string label, int value, float x, float y) { DText(right, label, x, y + 3, 150, 22, 14, color: UiTheme.TextDim); DField(right, name, value.ToString(), x + 152, y, 70, 26, true); }
            Row("MaxBuildings", "Most buildings", lim.MaxBuildings, 12, 502); Row("MaxEmployment", "Most jobs", lim.MaxEmployment, 12, 532); Row("MaxSpend", "Spend a year (units)", lim.MaxSpend, 12, 562);
            Row("MinWood", "Keep wood", lim.MinWood, 310, 502); Row("MinStone", "Keep stone", lim.MinStone, 310, 532); Row("Target", "Target (" + World.DistrictOutputUnit(sel.Type) + ")", lim.ProductionTarget, 310, 562);
            if (sel.Type == DistrictType.Forestry)
            {
                DText(right, "New lodges harvest", 610 - 12, 504, 150, 22, 13, color: UiTheme.TextDim);
                DButton(right, "Harvest:Sustainable", "Sustainable", 590, 528, 100, 26, () => { var l = sel.Limits.Copy(); l.HarvestMode = ForestMode.Sustainable; w.SetDistrictLimits(sel.Id, l); _distKey = ""; }, on: lim.HarvestMode == ForestMode.Sustainable);
                DButton(right, "Harvest:Intensive", "Intensive", 590, 558, 100, 26, () => { var l = sel.Limits.Copy(); l.HarvestMode = ForestMode.Intensive; w.SetDistrictLimits(sel.Id, l); _distKey = ""; }, on: lim.HarvestMode == ForestMode.Intensive);
            }
            DButton(right, "ApplyLimits", "Apply limits", 12, 594, 130, 28, () =>
            {
                var l = sel.Limits.Copy();
                l.MaxBuildings = IntOf(Draft("MaxBuildings", lim.MaxBuildings)); l.MaxEmployment = IntOf(Draft("MaxEmployment", lim.MaxEmployment)); l.MaxSpend = IntOf(Draft("MaxSpend", lim.MaxSpend));
                l.MinWood = IntOf(Draft("MinWood", lim.MinWood)); l.MinStone = IntOf(Draft("MinStone", lim.MinStone)); l.ProductionTarget = IntOf(Draft("Target", lim.ProductionTarget));
                var r = w.SetDistrictLimits(sel.Id, l);
                if (r.Ok) foreach (var k in new[] { "MaxBuildings", "MaxEmployment", "MaxSpend", "MinWood", "MinStone", "Target" }) _distDraft.Remove(k); else Toast(r.Reason, 4);
                _distKey = "";
            });
            DButton(right, "ClearLimits", "Clear limits", 150, 594, 130, 28, () => { var l = new DistrictLimits { HarvestMode = ForestMode.Sustainable }; w.SetDistrictLimits(sel.Id, l); foreach (var k in new[] { "MaxBuildings", "MaxEmployment", "MaxSpend", "MinWood", "MinStone", "Target" }) _distDraft.Remove(k); _distKey = ""; });
            DButton(right, "RemoveZone", "Remove zone", 462, 594, 130, 28, () => { int n = w.RemoveDistrictZone(sel.Id); Toast($"{n} tiles unzoned. Buildings stay.", 4); _distKey = ""; });
            DText(right, "Spent this year: " + w.SpentThisYear(sel) + " units", 310, 598, 150, 22, 12, color: UiTheme.TextDim);
        }

        string Draft(string name, int current) => _distDraft.TryGetValue(name, out var s) ? s : current.ToString();
        string Draft(string name, string current) => _distDraft.TryGetValue(name, out var s) ? s : current;
    }
}
