// The Military panel: a big management screen like the world map (it covers most of the screen, takes the game's input while open, and closes with
// Esc, T or its Close button). Overview of the population's military side, ALL companies at once with their stats, the selected company in detail,
// and exact numeric entry (type a number or use -10/-1/+1/+10) for registering militia, recruiting professionals, a company's target strength
// and mobilising a set number, with the workforce impact of each action written out before you press it.
// Presentation only: every action calls World.Mil (MilitarySystem); the only state kept here is the selection and the number.
using System;
using System.Linq;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        const int MilRows = 12;
        static readonly float[] MilColX = { 8, 168, 232, 322, 428, 466, 504, 542, 584 };   // name, men, type, order, T, E, M, R, kit
        static readonly string[] MilColName = { "Company", "Men", "Type", "Order", "Tr", "Ex", "Mo", "Rd", "Kit" };
        Text _milOverview, _milDetail, _milImpact, _milLegend, _milPageLabel;
        readonly Image[] _milRow = new Image[MilRows];
        readonly Text[][] _milCell = new Text[MilRows][];
        Image _milMobAll, _milDemobAll, _milMilitiaFights, _milGates;
        InputField _milName, _milQtyField;
        int _milSel, _milPage, _milQty = 5, _milStamp = -1, _milStampDay = -1, _milSelAtStamp, _milQtyAtStamp;
        string _milMessage = "";

        public bool MilitaryOpen => _townMenu == TownMenu.Troops;
        public bool EditingMilitaryName => (_milName != null && _milName.isFocused) || (_milQtyField != null && _milQtyField.isFocused);
        public void ToggleMilitary() { if (_campOpen && _campPhase == CampPhase.Fight) return; if (_mapOpen) CloseMap(); ShowTownMenu(MilitaryOpen ? TownMenu.Closed : TownMenu.Troops); }

        Text MilLabel(RectTransform panel, string name, int size, float x, float y, float w, float h)
        {
            var t = MakeText(panel, name, size, TextAnchor.UpperLeft);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.up; rt.pivot = Vector2.up;
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            return t;
        }

        Image MilButton(RectTransform panel, string label, float x, float y, float w, Action onClick, float h = 28)
            => MakeButton(panel, label, Vector2.up, new Vector2(x + w / 2, -(y + h / 2)), new Vector2(w, h), onClick);

        InputField MilInput(RectTransform panel, string name, float x, float y, float w, bool numeric)
        {
            var p = MakePanel(panel, name, Vector2.up, Vector2.up, new Vector2(x, -(y + 28)), new Vector2(x + w, -y));
            var f = p.gameObject.AddComponent<InputField>();
            var t = MakeText(p, "Text", 15, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(6, 0), new Vector2(-6, 0));
            f.textComponent = t; f.targetGraphic = p.GetComponent<Image>();
            if (numeric) { f.contentType = InputField.ContentType.IntegerNumber; f.characterLimit = 5; }
            else f.characterLimit = 32;
            return f;
        }

        void MilDo(Func<Result> action)
        {
            if (_menuWorld == null) return;
            var r = action();
            _milMessage = r.Ok ? (r.Count > 0 ? $"Done: {r.Count}." : "Done.") : r.Reason;
            _milStamp = -1;
        }

        MilitaryCompany MilSelected => _menuWorld?.Mil.Find(_milSel);

        void MilSetQty(int v) { _milQty = Math.Clamp(v, 0, 9999); _milQtyField.SetTextWithoutNotify(_milQty.ToString()); _milStamp = -1; }

        void BuildMilitary(RectTransform panel)
        {
            // a big centred panel, opaque like the world map
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.offsetMin = new Vector2(-570, -380); panel.offsetMax = new Vector2(570, 380);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.075f, 0.06f, 1f);
            MakeButton(panel, "Close (T / Esc)", Vector2.one, new Vector2(-84, -24), new Vector2(150, 30), () => ShowTownMenu(TownMenu.Closed));

            // ---- left column: overview, exact numbers, actions with their impact ----
            _milOverview = MilLabel(panel, "Overview", 14, 14, 44, 420, 200);
            MilLabel(panel, "QtyLabel", 14, 14, 252, 120, 24).text = "Number of men:";
            _milQtyField = MilInput(panel, "Qty", 134, 248, 80, true);
            _milQtyField.SetTextWithoutNotify(_milQty.ToString());
            _milQtyField.onEndEdit.AddListener(v => { int.TryParse(v, out int n); MilSetQty(n); });
            MilButton(panel, "-10", 222, 248, 46, () => MilSetQty(_milQty - 10));
            MilButton(panel, "-1", 272, 248, 40, () => MilSetQty(_milQty - 1));
            MilButton(panel, "+1", 316, 248, 40, () => MilSetQty(_milQty + 1));
            MilButton(panel, "+10", 360, 248, 46, () => MilSetQty(_milQty + 10));

            MilButton(panel, "Register militia", 14, 284, 200, () => MilDo(() => { var c = MilSelected; return _menuWorld.Mil.RegisterMilitia(_milQty, c != null && c.IsMilitia ? c.Id : 0); }));
            MilButton(panel, "Recruit professionals", 220, 284, 200, () => MilDo(() => { var c = MilSelected; return c == null || c.IsMilitia ? Result.No("Select a professional company first") : _menuWorld.Mil.SetTarget(c.Id, c.Target + _milQty); }));
            MilButton(panel, "Set target to number", 14, 318, 200, () => MilDo(() => { var c = MilSelected; return c == null ? Result.No("Select a company first") : _menuWorld.Mil.SetTarget(c.Id, _milQty); }));
            MilButton(panel, "Discharge / unregister", 220, 318, 200, () => MilDo(() => { var c = MilSelected; return c == null ? Result.No("Select a company first") : c.IsMilitia ? _menuWorld.Mil.Unregister(c.Id, _milQty) : _menuWorld.Mil.Discharge(c.Id, _milQty); }));
            MilButton(panel, "Mobilise number", 14, 352, 200, () => MilDo(() => { var c = MilSelected; return c == null ? Result.No("Select a militia company first") : _menuWorld.Mil.Mobilise(c.Id, _milQty); }));
            MilButton(panel, "Demobilise number", 220, 352, 200, () => MilDo(() => { var c = MilSelected; return c == null ? Result.No("Select a militia company first") : _menuWorld.Mil.Demobilise(c.Id, _milQty); }));
            _milMobAll = MilButton(panel, "Mobilise all militia", 14, 386, 200, () => MilDo(() => { int n = _menuWorld.Mil.MobiliseAll(); return n > 0 ? Result.Yes(n) : Result.No("No militia to mobilise"); }));
            _milDemobAll = MilButton(panel, "Demobilise all", 220, 386, 200, () => MilDo(() => { int n = _menuWorld.Mil.DemobiliseAll(); return n > 0 ? Result.Yes(n) : Result.No("Nobody is mobilised"); }));
            _milImpact = MilLabel(panel, "Impact", 13, 14, 424, 420, 230);

            // ---- right: the full company list ----
            for (int c = 0; c < MilColName.Length; c++)
            {
                var h = MilLabel(panel, "Head" + c, 12, 450 + MilColX[c], 44, 120, 20); h.text = MilColName[c]; h.color = Dim;
            }
            for (int i = 0; i < MilRows; i++)
            {
                int row = i;
                _milRow[i] = MilButton(panel, "", 450, 64 + i * 25, 676, () =>
                {
                    var list = _menuWorld?.Mil.Companies; int index = _milPage * MilRows + row;
                    if (list != null && index < list.Count) { _milSel = list[index].Id; _milStamp = -1; }
                }, 23);
                _milRow[i].GetComponentInChildren<Text>().text = "";
                _milCell[i] = new Text[MilColName.Length];
                for (int c = 0; c < MilColName.Length; c++)
                {
                    var t = MakeText(_milRow[i].rectTransform, "Cell" + c, 12, TextAnchor.MiddleLeft);
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 0.5f); t.rectTransform.pivot = new Vector2(0, 0.5f);
                    t.rectTransform.anchoredPosition = new Vector2(MilColX[c], 0);
                    float w = c + 1 < MilColX.Length ? MilColX[c + 1] - MilColX[c] : 90;
                    t.rectTransform.sizeDelta = new Vector2(w, 22); t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    _milCell[i][c] = t;
                }
            }
            MilButton(panel, "Prev", 450, 368, 60, () => { _milPage = Math.Max(0, _milPage - 1); _milStamp = -1; }, 24);
            MilButton(panel, "Next", 516, 368, 60, () => { _milPage++; _milStamp = -1; }, 24);
            _milPageLabel = MilLabel(panel, "Page", 12, 584, 372, 200, 20); _milPageLabel.color = Dim;
            // create companies: the four Phase 1 troop types of the design (peasant levy, spearman, hunter archer, shield infantry)
            float bx = 450;
            foreach (var u in new[] { MilitaryUnitType.PeasantLevy, MilitaryUnitType.Spearman, MilitaryUnitType.HunterArcher, MilitaryUnitType.ShieldInfantry })
            {
                var unit = u;
                MilButton(panel, "New " + TroopDefinitions.Get(u).Name.ToLower() + " company", bx, 398, 166, () => MilDo(() => _menuWorld.Mil.CreateCompany(unit)), 26);
                bx += 170;
            }

            // ---- selected company ----
            _milName = MilInput(panel, "CompanyName", 450, 436, 300, false);
            _milName.onEndEdit.AddListener(v => { if (_menuWorld != null && _milSel != 0) MilDo(() => _menuWorld.Mil.Rename(_milSel, v)); });
            MilButton(panel, "Disband company", 760, 436, 180, () => { var c = MilSelected; if (c != null) MilDo(() => _menuWorld.Mil.Disband(c.Id)); });
            _milDetail = MilLabel(panel, "Detail", 14, 450, 472, 330, 190);
            _milLegend = MilLabel(panel, "Legend", 12, 790, 472, 336, 190); _milLegend.color = Dim;
            _milLegend.text = "What the numbers mean (0-100):\n" +
                              "Training: drill and practice; grows slowly, professionals much faster than militia.\n" +
                              "Experience: service and fighting; it stays, newcomers dilute it.\n" +
                              "Morale: food, pay, housing, casualties, long call-ups.\n" +
                              "Readiness: how soon the company can act: men, food, call-up. Not skill.\n" +
                              "Kit: men with equipment (improvised weapons until Phase 2).";
            _milGates = MilButton(panel, "", 14, 722, 150, () => { if (_menuWorld != null) _menuWorld.KeepGatesShut = !_menuWorld.KeepGatesShut; }, 26);
            _milMilitiaFights = MilButton(panel, "", 170, 722, 190, () => { if (_menuWorld != null) _menuWorld.MilitiaFights = !_menuWorld.MilitiaFights; }, 26);
        }

        void RefreshMilitary(World world)
        {
            var mil = world.Mil;
            int stamp = world.Version * 31 + mil.Companies.Count;
            if (stamp == _milStamp && world.Day == _milStampDay && _milSel == _milSelAtStamp && _milQty == _milQtyAtStamp) return;
            _milStamp = stamp; _milStampDay = world.Day; _milSelAtStamp = _milSel; _milQtyAtStamp = _milQty;
            if (mil.Find(_milSel) == null) _milSel = mil.Companies.Count > 0 ? mil.Companies[0].Id : 0;
            int pages = Math.Max(1, (mil.Companies.Count + MilRows - 1) / MilRows);
            _milPage = Math.Clamp(_milPage, 0, pages - 1);
            _milPageLabel.text = $"Page {_milPage + 1}/{pages}  ·  {mil.Companies.Count} companies";

            // ---- overview and infrastructure ----
            int pop = world.Peasants + world.Soldiers, bunks = world.D.Bunks;
            string threat = world.Raid != null && !world.Raid.Over ? $"RAID in progress: {world.Raid.Active} raiders" : world.TroopsAway > 0 ? $"{world.TroopsAway} soldiers away from home" : "No raid at the gates";
            _milOverview.text =
                $"Population {pop}  ·  fit to serve {mil.EligiblePopulation}  ·  free to enlist {mil.AvailableToServe}\n" +
                $"Civilian workforce {mil.CivilianWorkforce} ({world.D.Jobs} jobs, {world.D.Idle} without work)\n" +
                $"Professionals {world.Soldiers} (infantry {world.Infantry}, archers {world.Archers})\n" +
                $"Registered militia {mil.Registered} (not mobilised {mil.Registered - mil.MobilisedCount}) · MOBILISED {mil.MobilisedCount}\n" +
                $"Barracks {mil.Barracks}: bunks {world.Soldiers}/{bunks}" + (mil.Unhoused > 0 ? $" ({mil.Unhoused} without a bunk)" : "") + "\n" +
                $"Muster grounds {mil.MusterCapacity / MilitaryConfig.MusterCapacity}: roll {mil.Registered}/{mil.MusterCapacity}\n" +
                $"Armory: none yet (equipment comes in Phase 2) · training grounds: none\n" +
                $"Watchtowers {world.Blds.Count(b => b.T == "watchtower")} (unstaffed: staffing comes in Phase 3)\n" +
                $"Upkeep: {world.Soldiers * 0.1:0.#} gold/day wages; militia unpaid\n" +
                $"Threat: {threat}";

            // ---- the full list ----
            for (int i = 0; i < MilRows; i++)
            {
                int index = _milPage * MilRows + i;
                bool has = index < mil.Companies.Count;
                _milRow[i].gameObject.SetActive(has);
                if (!has) continue;
                var c = mil.Companies[index];
                var cells = _milCell[i];
                cells[0].text = (c.Id == _milSel ? "> " : "") + c.Name;
                cells[1].text = $"{c.Personnel}/{c.Target}";
                cells[2].text = (c.IsMilitia ? (c.Mobilised ? "MOBIL. " : "roll ") : "pro ") + TroopDefinitions.Get(c.Unit).Short;
                cells[3].text = MilitaryCompanies.OrderName(c.Order);
                cells[4].text = $"{c.Training:0}"; cells[5].text = $"{c.Experience:0}"; cells[6].text = $"{c.Morale:0}"; cells[7].text = $"{c.Readiness:0}";
                cells[8].text = c.Equipped > 0 ? $"{c.Equipped}" : "improv.";
                _milRow[i].color = c.Id == _milSel ? BtnOn : Btn;
            }

            _milGates.GetComponentInChildren<Text>().text = world.KeepGatesShut ? "Gates: shut" : "Gates: open";
            _milGates.color = world.KeepGatesShut ? BtnOn : Btn;
            _milMilitiaFights.GetComponentInChildren<Text>().text = world.MilitiaFights ? "Workers fight raiders: on" : "Workers fight raiders: off";
            _milMilitiaFights.color = world.MilitiaFights ? BtnOn : Btn;

            // ---- selected company ----
            var sel = mil.Find(_milSel);
            _milName.gameObject.SetActive(sel != null);
            if (sel == null)
            {
                _milDetail.text = mil.MusterCapacity == 0 && mil.Barracks == 0
                    ? "No companies yet.\n\nBuild a Muster Ground (Military build menu) to register militia: they keep their jobs until you call them up. Professionals need a Barracks and come out of your workforce."
                    : "No companies yet. Make one with the buttons above.";
            }
            else
            {
                if (!_milName.isFocused) _milName.SetTextWithoutNotify(sel.Name);
                string status = sel.IsMilitia ? (sel.Mobilised ? $"MOBILISED (day {sel.MobilisedDays}): off their jobs, at the muster point" : "On the roll: still at their normal jobs") : "Professional: left civilian work, lives in barracks";
                _milDetail.text =
                    $"{MilitaryCompanies.CategoryName(sel.Category)} · {TroopDefinitions.Get(sel.Unit).Name}\n" +
                    $"Men {sel.Personnel} of {sel.Target} wanted (up to {sel.Max})\n" +
                    $"Training {sel.Training:0} · Experience {sel.Experience:0} · Morale {sel.Morale:0} · Readiness {sel.Readiness:0}\n" +
                    $"{status}\nEquipment: {(sel.Equipped > 0 ? sel.Equipped + " kitted" : "improvised weapons")}\n" +
                    $"Order: {MilitaryCompanies.OrderName(sel.Order)} · Location: {sel.Location}";
            }

            // ---- the workforce impact of each action at the number typed, shown before anything is pressed ----
            int n = _milQty;
            var lines = new System.Collections.Generic.List<string> { $"With {n} men typed in:" };
            int reg = Math.Min(n, Math.Min(mil.AvailableToServe, Math.Max(0, mil.MusterCapacity - mil.Registered)));
            lines.Add($"• Registering {reg} militia changes nothing at work (workforce stays {mil.CivilianWorkforce}); they keep their jobs.");
            int rec = Math.Min(n, mil.RecruitableNow());
            var ir = mil.Impact(rec, 0);
            lines.Add($"• Recruiting {rec} professionals (a couple a day, into free bunks {mil.BunksFree}) would reduce your civilian workforce from {ir.Before} to {ir.After}. {ir.Warning}");
            int mobN = sel != null && sel.IsMilitia && !sel.Mobilised ? Math.Min(n, sel.Personnel) : 0;
            var im = mil.Impact(0, mobN);
            lines.Add($"• Mobilising {mobN} militia would reduce your civilian workforce from {im.Before} to {im.After}; their production stops until you demobilise. {im.Warning}");
            if (sel != null && sel.IsMilitia && sel.Mobilised) lines.Add($"• Demobilising {Math.Min(n, sel.Personnel)} returns them to work: workforce {mil.CivilianWorkforce} -> {mil.CivilianWorkforce + Math.Min(n, sel.Personnel)}.");
            if (sel != null && sel.Category == MilitaryCategory.Professional) lines.Add($"• Discharging {Math.Min(n, sel.Personnel)} sends them back to the workforce: {mil.CivilianWorkforce} -> {mil.CivilianWorkforce + Math.Min(n, sel.Personnel)}.");
            if (_milMessage.Length > 0) lines.Add("\n" + _milMessage);
            _milImpact.text = string.Join("\n", lines);
        }
    }
}
