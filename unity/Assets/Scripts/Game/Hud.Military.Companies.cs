// Military Command, Companies tab: left company cards (filter + scrolling list + Create Company), centre detail (progress bars with tooltips),
// right contextual actions (disabled with the reason), an empty state, and the bottom bar (threat, emergency orders, global mobilization).
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        sealed class MilCardView { public RectTransform Root; public Image Bg, Badge, Ready; public Text Name, Sub, Line, Letter, ReadyText; public int Id; }
        sealed class MilActView { public RectTransform Root; public Image Btn; public Text Label, Reason; public string Id = ""; }

        CompanyFilter _milFilter = CompanyFilter.All;
        int _milSel;
        RectTransform _milEmpty, _milMain, _milListContent, _milCenter, _milRight;
        ScrollRect _milScroll;
        Image _milFilterBtn, _milCreateBtn;
        Text _milFilterText, _milEmptyText, _milEmptyReq, _milReq, _milWarn, _milSub, _milPersonnel, _milPersonnelNote, _milEquip, _milAssign, _milAssignNote, _milLocation;
        Image _milEmptyMilitia, _milEmptyPro;
        InputField _milName;
        readonly List<MilCardView> _milCards = new List<MilCardView>();
        readonly List<MilActView> _milActs = new List<MilActView>();
        readonly string[] _milBarNames = { "Training", "Experience", "Morale", "Readiness" };
        readonly MilBar[] _milBars = new MilBar[4];

        InputField MilField(RectTransform parent, int size, bool numeric, TextAnchor anchor)
        {
            var p = MakePanel(parent, "Input", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var f = p.gameObject.AddComponent<InputField>();
            var t = MakeText(p, "Text", size, anchor);
            Stretch(t.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
            f.textComponent = t; f.targetGraphic = p.GetComponent<Image>();
            if (numeric) { f.contentType = InputField.ContentType.IntegerNumber; f.characterLimit = 5; } else f.characterLimit = 32;
            return f;
        }

        static string MilIconLetter(MilitaryUnitType u) => u switch { MilitaryUnitType.PeasantLevy => "M", MilitaryUnitType.Spearman => "S", MilitaryUnitType.HunterArcher => "A", _ => "Sh" };

        void BuildMilitaryCompanies(RectTransform page)
        {
            // ---- empty state ----
            _milEmpty = MilRegion(page, "Empty", 0.12f, 0.08f, 0.88f, 0.96f, Vector2.zero, Vector2.zero, MilCard);
            MilText(MilBand(_milEmpty, "T", 14, 34), "Heading", 24, TextAnchor.MiddleCenter, "You have no military companies", null, true);
            _milEmptyText = MilText(MilBand(_milEmpty, "B", 56, 90), "Body", 18, TextAnchor.UpperCenter,
                "Register civilian militia to prepare your settlement for emergencies, or recruit professional soldiers once barracks are available.");
            var btns = MilBand(_milEmpty, "Buttons", 160, 50, 40, 40);
            _milEmptyMilitia = MilBtn(MilSlot(btns, "A", 0, 0.5f, 6), "Create militia company", () => OpenWizard(MilitaryUnitType.PeasantLevy), 18);
            _milEmptyPro = MilBtn(MilSlot(btns, "B", 0.5f, 1, 6), "Create professional company", () => OpenWizard(MilitaryUnitType.Spearman), 18);
            _milEmptyReq = MilText(MilBand(_milEmpty, "R", 224, 80), "Requirements", 16, TextAnchor.UpperCenter, "", MilWarn);
            MilBtn(MilBand(_milEmpty, "Build", 310, 40, 120, 120), "Open the Military build menu", () => { ShowTownMenu(TownMenu.Closed); CategoryKey(GameController.Cat.Military); }, 16);

            // ---- three columns ----
            _milMain = MilRegion(page, "Main", 0, 0, 1, 1, Vector2.zero, Vector2.zero);
            var left = MilRegion(_milMain, "Left", 0, 0, 0.27f, 1, new Vector2(0, 0), new Vector2(-4, 0), MilCard);
            _milCenter = MilRegion(_milMain, "Center", 0.27f, 0, 0.70f, 1, new Vector2(4, 0), new Vector2(-4, 0), MilCard);
            _milRight = MilRegion(_milMain, "Right", 0.70f, 0, 1, 1, new Vector2(4, 0), new Vector2(0, 0), MilCard);

            // left: create, filter, scrolling cards
            MilText(MilBand(left, "H", 4, 24), "Heading", 18, TextAnchor.MiddleLeft, "COMPANIES", Dim, true);
            _milCreateBtn = MilBtn(MilBand(left, "Create", 30, 40, 6, 6), "+  Create Company", () => OpenWizard(null), 18);
            _milCreateBtn.color = BtnOn;
            var fb = MilBand(left, "Filter", 76, 32, 6, 6);
            _milFilterBtn = MilBtn(fb, "", () => CycleFilter(), 15);
            _milFilterText = _milFilterBtn.GetComponentInChildren<Text>();
            var view = MilRegion(left, "Scroll", 0, 0, 1, 1, new Vector2(4, 4), new Vector2(-4, -114), MilSunk);
            view.gameObject.AddComponent<RectMask2D>();
            _milListContent = MilRegion(view, "Content", 0, 1, 1, 1, Vector2.zero, Vector2.zero);
            _milListContent.pivot = new Vector2(0.5f, 1);
            MilitaryListLayout.Configure(_milListContent);
            _milScroll = view.gameObject.AddComponent<ScrollRect>();
            _milScroll.content = _milListContent; _milScroll.viewport = view; _milScroll.horizontal = false; _milScroll.vertical = true;
            _milScroll.movementType = ScrollRect.MovementType.Clamped; _milScroll.scrollSensitivity = 40;

            // centre: identity, personnel, bars, assignment
            _milName = MilField(MilBand(_milCenter, "Name", 8, 40, 10, 10), 22, false, TextAnchor.MiddleLeft);
            _milName.onEndEdit.AddListener(v => { if (_menuWorld != null && _milSel != 0) MilDo(() => _menuWorld.Mil.Rename(_milSel, v)); });
            _milSub = MilText(MilBand(_milCenter, "Sub", 52, 26, 10, 10), "Sub", 17, TextAnchor.MiddleLeft, "", Dim);
            _milPersonnel = MilText(MilBand(_milCenter, "Personnel", 86, 30, 10, 10), "Personnel", 22, TextAnchor.MiddleLeft, "", null, true);
            _milPersonnelNote = MilText(MilBand(_milCenter, "PNote", 116, 44, 10, 10), "Note", 16, TextAnchor.UpperLeft, "", Dim);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var row = MilBand(_milCenter, "Bar" + i, 166 + i * 38, 32, 10, 10);
                _milBars[i] = MilMakeBar(row, _milBarNames[i]);
                MilHover(row.gameObject, row, () => { var c = MilSelected; if (c == null) return ("", ""); return MilitaryUi.StatTip(_milBarNames[idx], c); });
                row.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);   // so the row receives the pointer
            }
            _milEquip = MilText(MilBand(_milCenter, "Equip", 322, 26, 10, 10), "Equip", 16, TextAnchor.MiddleLeft, "", Dim);
            MilText(MilBand(_milCenter, "AH", 360, 22, 10, 10), "AssignHead", 14, TextAnchor.MiddleLeft, "CURRENT ASSIGNMENT", Dim, true);
            _milAssign = MilText(MilBand(_milCenter, "Assign", 382, 34, 10, 10), "Assign", 22, TextAnchor.MiddleLeft, "", MilGold, true);
            _milAssignNote = MilText(MilBand(_milCenter, "ANote", 416, 40, 10, 10), "ANote", 15, TextAnchor.UpperLeft, "", Dim);
            _milLocation = MilText(MilBand(_milCenter, "Loc", 458, 26, 10, 10), "Location", 16, TextAnchor.MiddleLeft, "", Dim);

            // right: actions, requirements, warnings
            MilText(MilBand(_milRight, "H", 4, 24), "Heading", 18, TextAnchor.MiddleLeft, "MANAGEMENT", Dim, true);
            for (int i = 0; i < 6; i++)
            {
                var root = MilBand(_milRight, "Act" + i, 32 + i * 58, 52, 8, 8);
                var btn = MilBtn(root, "", null, 17);
                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.UpperLeft; Stretch(label.rectTransform, new Vector2(10, 0), new Vector2(-6, -4));
                var reason = MakeText(btn.rectTransform, "Reason", 13, TextAnchor.LowerLeft);
                Stretch(reason.rectTransform, new Vector2(10, 3), new Vector2(-6, 0)); reason.color = MilWarn;
                var view2 = new MilActView { Root = root, Btn = btn, Label = label, Reason = reason };
                var button = btn.GetComponent<Button>(); button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => MilAction(view2.Id));
                _milActs.Add(view2);
            }
            _milReq = MilText(MilRegion(_milRight, "Req", 0, 0, 1, 0, new Vector2(8, 70), new Vector2(-8, 170)), "Req", 15, TextAnchor.UpperLeft, "");
            _milWarn = MilText(MilRegion(_milRight, "Warn", 0, 0, 1, 0, new Vector2(8, 4), new Vector2(-8, 66)), "Warn", 15, TextAnchor.LowerLeft, "", MilWarn);
        }

        void CycleFilter()
        {
            var list = MilitaryUi.AvailableFilters(_menuWorld);
            int i = list.IndexOf(_milFilter);
            _milFilter = list[(i + 1) % list.Count];
            _milStamp = -1;
        }

        MilCardView MakeMilCard()
        {
            var root = MakePanel(_milListContent, "Card", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var img = root.GetComponent<Image>(); img.color = Btn;
            MilitaryListLayout.SizeCard(root.gameObject);
            var btn = root.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            root.gameObject.AddComponent<RectMask2D>();
            var v = new MilCardView { Root = root, Bg = img };
            var badge = MilRegion(root, "Badge", 0, 0, 0, 1, new Vector2(8, 24), new Vector2(52, -24), MilGold);
            v.Badge = badge.GetComponent<Image>();
            v.Letter = MilText(badge, "Letter", 20, TextAnchor.MiddleCenter, "", new Color(0.10f, 0.08f, 0.05f), true);
            var text = MilRegion(root, "Text", 0, 0, 1, 1, new Vector2(60, 4), new Vector2(-6, -4));
            v.Name = MilText(MilBand(text, "N", 0, 24), "Name", 18, TextAnchor.MiddleLeft, "", null, true);
            v.Sub = MilText(MilBand(text, "S", 24, 20), "Sub", 14, TextAnchor.MiddleLeft, "", Dim);
            v.Line = MilText(MilBand(text, "L", 44, 20), "Line", 14, TextAnchor.MiddleLeft, "");
            var rd = MilBand(text, "R", 66, 18);
            var track = MilSlot(rd, "Track", 0, 0.45f, 0, MilSunk);
            v.Ready = MilRegion(MilRegion(track, "In", 0, 0, 1, 1, new Vector2(2, 4), new Vector2(-2, -4)), "Fill", 0, 0, 0, 1, Vector2.zero, Vector2.zero, MilGood).GetComponent<Image>();
            v.ReadyText = MilText(MilSlot(rd, "T", 0.47f, 1, 0), "RText", 13, TextAnchor.MiddleLeft, "");
            btn.onClick.AddListener(() => { _milSel = v.Id; _milStamp = -1; });
            return v;
        }

        void MilAction(string id)
        {
            var w = _menuWorld; var c = MilSelected; if (w == null || c == null) return;
            var m = w.Mil;
            int more = MilitaryUi.MaxMore(w, c);
            switch (id)
            {
                case "register":
                    OpenNumberDialog("Register militia", c.Name, 1, more, Math.Min(5, more), n => MilitaryUi.RegisterSentence(w, n), "Register", n => MilDo(() => m.RegisterMilitia(n, c.Id)));
                    break;
                case "recruit":
                    OpenNumberDialog("Recruit soldiers", c.Name, 1, more, Math.Min(5, more), n => MilitaryUi.RecruitSentence(w, c, n), "Recruit", n => MilDo(() => m.SetTarget(c.Id, Math.Max(c.Target, c.Personnel) + n)));
                    break;
                case "target":
                    {
                        int cur = c.IsMilitia ? c.Personnel : Math.Max(c.Target, c.Personnel);
                        int min = c.IsMilitia ? 0 : c.Personnel, max = Math.Min(c.Max, cur + more);
                        OpenNumberDialog("Set target strength", c.Name + (c.IsMilitia ? "" : ". To send soldiers home use Discharge."), min, max, Math.Clamp(cur + 5, min, max), n => MilitaryUi.TargetSentence(w, c, n), "Set target", n => MilDo(() => m.SetTarget(c.Id, n)));
                        break;
                    }
                case "discharge":
                    OpenNumberDialog("Discharge personnel", c.Name, 1, c.Personnel, Math.Min(5, c.Personnel), n => MilitaryUi.DischargeSentence(w, n), "Discharge", n => MilDo(() => m.Discharge(c.Id, n)));
                    break;
                case "unregister":
                    OpenNumberDialog("Unregister personnel", c.Name, 1, c.Personnel, Math.Min(5, c.Personnel), n => MilitaryUi.UnregisterSentence(w, n), "Unregister", n => MilDo(() => m.Unregister(c.Id, n)));
                    break;
                case "mobilise":
                    OpenConfirm("Mobilize " + c.Name, MilitaryUi.MobiliseSentence(w, c.Personnel), "Mobilize", false, () => MilDo(() => m.Mobilise(c.Id)));
                    break;
                case "demobilise":
                    OpenConfirm("Demobilize " + c.Name, MilitaryUi.DemobiliseSentence(w, c.Personnel), "Demobilize", false, () => MilDo(() => m.Demobilise(c.Id)));
                    break;
                case "disband":
                    OpenConfirm("Disband " + c.Name, MilitaryUi.DisbandSentence(c), "Disband", true, () => MilDo(() => m.Disband(c.Id)));
                    break;
            }
        }

        void MilDo(Func<Result> action)
        {
            if (_menuWorld == null) return;
            var r = action();
            _milMessage = r.Ok ? "" : r.Reason;
            if (r.Ok && _milPendingSelect > 0) { _milSel = _milPendingSelect; _milPendingSelect = 0; }
            _milStamp = -1;
        }
        string _milMessage = "";
        int _milPendingSelect;

        MilitaryCompany MilSelected => _menuWorld?.Mil.Find(_milSel);

        void RefreshMilitaryCompanies(World world)
        {
            var mil = world.Mil;
            bool any = mil.Companies.Count > 0;
            _milEmpty.gameObject.SetActive(!any);
            _milMain.gameObject.SetActive(any);
            if (!any)
            {
                bool muster = mil.MusterCapacity > 0, barracks = mil.Barracks > 0;
                var reqs = new List<string>();
                if (!muster) reqs.Add("Muster Ground required for militia.");
                if (!barracks) reqs.Add("Barracks required for professional soldiers.");
                _milEmptyReq.text = string.Join("\n", reqs);
                _milEmptyMilitia.color = muster ? Btn : Btn * 0.6f; _milEmptyPro.color = barracks ? Btn : Btn * 0.6f;
                return;
            }
            var filters = MilitaryUi.AvailableFilters(world);
            if (!filters.Contains(_milFilter)) _milFilter = CompanyFilter.All;
            var shown = mil.Companies.Where(c => MilitaryUi.Matches(c, _milFilter)).ToList();
            _milFilterText.text = "Show: " + MilitaryUi.FilterName(_milFilter) + (filters.Count > 1 ? "   (click to change)" : "");
            if (mil.Find(_milSel) == null || !shown.Any(c => c.Id == _milSel)) _milSel = shown.Count > 0 ? shown[0].Id : 0;

            while (_milCards.Count < shown.Count) _milCards.Add(MakeMilCard());
            for (int i = 0; i < _milCards.Count; i++)
            {
                bool has = i < shown.Count;
                _milCards[i].Root.gameObject.SetActive(has);
                if (!has) continue;
                var c = shown[i]; var v = _milCards[i];
                v.Id = c.Id;
                v.Bg.color = c.Id == _milSel ? BtnOn : Btn;
                v.Badge.color = c.IsMilitia ? new Color(0.62f, 0.55f, 0.35f) : MilGold;
                v.Letter.text = MilIconLetter(c.Unit);
                v.Name.text = c.Name;
                v.Sub.text = TroopDefinitions.Get(c.Unit).Name + " · " + MilitaryUi.Service(c);
                v.Line.text = $"{c.Personnel} / {Math.Max(c.Target, c.Personnel)} " + (c.IsMilitia ? "militia" : "soldiers") + " · " + MilitaryAdvice.Activity(c);
                v.Ready.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)c.Readiness / 100f), 1);
                v.Ready.color = c.Readiness >= 60 ? MilGood : c.Readiness >= 35 ? MilWarn : MilBad;
                v.ReadyText.text = $"Readiness: {c.Readiness:0}%";
            }

            var sel = mil.Find(_milSel);
            _milCenter.gameObject.SetActive(sel != null); _milRight.gameObject.SetActive(sel != null);
            if (sel == null) return;
            if (!_milName.isFocused) _milName.SetTextWithoutNotify(sel.Name);
            _milSub.text = TroopDefinitions.Get(sel.Unit).Name + " · " + MilitaryUi.Service(sel);
            int goal = Math.Max(sel.Target, sel.Personnel);
            _milPersonnel.text = $"Personnel: {sel.Personnel} / {goal}";
            int missing = goal - sel.Personnel;
            _milPersonnelNote.text = sel.IsMilitia
                ? (sel.Mobilised ? "Mobilized: off their jobs and gathered at the muster ground." : "Reserve: they keep their civilian jobs until mobilized.")
                : missing > 0 ? $"{missing} additional soldier{(missing == 1 ? " is" : "s are")} being recruited, about {MilitaryConfig.RecruitPerDay} a day." : "At full strength.";
            double[] values = { sel.Training, sel.Experience, sel.Morale, sel.Readiness };
            for (int i = 0; i < 4; i++) MilSetBar(_milBars[i], values[i], i < 2);
            _milEquip.text = "Equipment: " + (sel.Equipped > 0 ? $"{sel.Equipped} of {sel.Personnel} fully equipped" : TroopDefinitions.Get(sel.Unit).Levy ? "improvised weapons" : "improvised weapons until equipment is supplied");
            _milAssign.text = MilitaryAdvice.Activity(sel);
            _milAssignNote.text = sel.IsMilitia ? (sel.Mobilised ? "Defending the settlement from the muster point." : "No assignment while on the roll.") : "Orders and positions cannot be changed yet.";
            _milLocation.text = "Location: " + sel.Location;

            // contextual actions
            var actions = MilitaryUi.Actions(world, sel);
            for (int i = 0; i < _milActs.Count; i++)
            {
                var a = _milActs[i]; bool has = i < actions.Count;
                a.Root.gameObject.SetActive(has);
                if (!has) continue;
                var st = actions[i]; a.Id = st.Id; a.Label.text = st.Label;
                a.Reason.text = st.Enabled ? "" : "Unavailable: " + st.Reason;
                a.Btn.GetComponent<Button>().interactable = st.Enabled;
                a.Btn.color = st.Enabled ? (st.Danger ? new Color(0.45f, 0.20f, 0.15f) : Btn) : new Color(0.20f, 0.17f, 0.13f);
                a.Label.color = st.Enabled ? Ink : Dim;
            }
            _milReq.text = sel.IsMilitia
                ? $"REQUIREMENTS\nEligible residents available: {mil.AvailableToServe}\nMuster roll: {mil.Registered}/{mil.MusterCapacity}"
                : $"REQUIREMENTS\nEligible residents available: {mil.AvailableToServe}\nFree barracks beds: {mil.BunksFree}";
            var warn = new List<string>();
            if (mil.Unhoused > 0 && !sel.IsMilitia) warn.Add($"{mil.Unhoused} soldiers have no bed.");
            if (world.Starving) warn.Add("The settlement is starving.");
            if (sel.Morale < 35) warn.Add("Morale is low.");
            if (world.D.Jobs > mil.CivilianWorkforce) warn.Add($"Not enough workers: {world.D.Jobs - mil.CivilianWorkforce} jobs unfilled.");
            if (_milMessage.Length > 0) warn.Add(_milMessage);
            _milWarn.text = warn.Count > 0 ? "WARNINGS\n" + string.Join("\n", warn) : "";
        }

        // ---------- bottom bar: threat, emergency orders, global mobilization ----------

        Text _milThreat, _milReserve, _milGatesLabel;
        Image _milGatesBtn, _milCivDefBtn, _milMobAll, _milDemobAll;

        void BuildMilitaryBottom(RectTransform bar)
        {
            var left = MilRegion(bar, "Status", 0, 0, 0.30f, 1, new Vector2(8, 4), new Vector2(0, -4));
            _milThreat = MilText(MilBand(left, "T", 4, 34), "Threat", 20, TextAnchor.MiddleLeft, "", null, true);
            _milReserve = MilText(MilBand(left, "R", 40, 40), "Reserve", 16, TextAnchor.UpperLeft, "", Dim);
            var mid = MilRegion(bar, "Emergency", 0.30f, 0, 0.70f, 1, new Vector2(4, 4), new Vector2(-4, -4));
            MilText(MilBand(mid, "H", 0, 22), "Head", 13, TextAnchor.MiddleLeft, "EMERGENCY ORDERS", Dim, true);
            var row = MilBand(mid, "Row", 24, 54);
            var g = MilSlot(row, "Gates", 0, 0.5f, 3);
            _milGatesBtn = MilBtn(g, "", () => { if (_menuWorld != null) { _menuWorld.KeepGatesShut = !_menuWorld.KeepGatesShut; _milStamp = -1; } }, 15);
            _milGatesLabel = _milGatesBtn.GetComponentInChildren<Text>();
            MilHover(_milGatesBtn.gameObject, g, () => ("Gates", "Gates shut by themselves while raiders are about. Closing them here keeps every gate in the palisade shut all the time, raid or not: raiders and your own people must go round."));
            var c = MilSlot(row, "CivDef", 0.5f, 1, 3);
            _milCivDefBtn = MilBtn(c, "", () => { if (_menuWorld != null) { _menuWorld.MilitiaFights = !_menuWorld.MilitiaFights; _milStamp = -1; } }, 15);
            MilHover(_milCivDefBtn.gameObject, c, () => ("Emergency civilian defense", "When on, working villagers near a raid fight with their trade tools (pitchforks, axes, picks...). They are far weaker than soldiers and get hurt more. When off, only soldiers and mobilized militia fight."));
            var right = MilRegion(bar, "Mobilize", 0.70f, 0, 1, 1, new Vector2(4, 4), new Vector2(-8, -4));
            MilText(MilBand(right, "H", 0, 22), "Head", 13, TextAnchor.MiddleLeft, "MOBILIZATION", Dim, true);
            var mrow = MilBand(right, "Row", 24, 54);
            var a = MilSlot(mrow, "All", 0, 0.5f, 3);
            _milMobAll = MilBtn(a, "Mobilize all militia", () =>
            {
                var w = _menuWorld; if (w == null) return; int n = w.Mil.Registered - w.Mil.MobilisedCount;
                OpenConfirm("Mobilize all militia", MilitaryUi.MobiliseSentence(w, n), "Mobilize all", false, () => MilDo(() => { int k = w.Mil.MobiliseAll(); return k > 0 ? Result.Yes(k) : Result.No("No reserve militia to mobilize"); }));
            }, 15);
            var d = MilSlot(mrow, "Demob", 0.5f, 1, 3);
            _milDemobAll = MilBtn(d, "Demobilize all", () => MilDo(() => { int k = _menuWorld.Mil.DemobiliseAll(); return k > 0 ? Result.Yes(k) : Result.No("Nobody is mobilized"); }), 15);
        }

        void RefreshMilitaryBottom(World world)
        {
            var mil = world.Mil;
            bool raid = world.Raid != null && !world.Raid.Over;
            _milThreat.text = raid ? "Threat: CRITICAL, raiders attacking" : world.TroopsAway > 0 ? "Threat: soldiers away from home" : "Threat: Low";
            _milThreat.color = raid ? MilBad : world.TroopsAway > 0 ? MilWarn : MilGood;
            int reserve = mil.Registered - mil.MobilisedCount;
            _milReserve.text = $"Reserve available: {reserve}\nMobilized: {mil.MobilisedCount}";
            _milGatesLabel.text = world.KeepGatesShut ? "Gates: Kept shut\nOpen gates" : "Gates: Open\nClose gates";
            _milGatesBtn.color = world.KeepGatesShut ? BtnOn : Btn;
            _milCivDefBtn.GetComponentInChildren<Text>().text = world.MilitiaFights ? "Emergency civilian defense: On\nTurn off" : "Emergency civilian defense: Off\nTurn on";
            _milCivDefBtn.color = world.MilitiaFights ? BtnOn : Btn;
            _milMobAll.GetComponent<Button>().interactable = reserve > 0; _milMobAll.color = reserve > 0 ? Btn : new Color(0.20f, 0.17f, 0.13f);
            _milDemobAll.GetComponent<Button>().interactable = mil.MobilisedCount > 0; _milDemobAll.color = mil.MobilisedCount > 0 ? BtnOn : new Color(0.20f, 0.17f, 0.13f);
        }
    }
}
