// Military Command dialogs: a number dialog (slider, typed number, -/+, min and max, the consequence sentence, Confirm/Cancel), a confirmation
// dialog, and the Create Company wizard. One instance of each lives under a dimmed overlay inside the window; Esc closes the dialog before the window.
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
        RectTransform _milOverlay, _milNumBox, _milWizBox;
        // number / confirm dialog
        Text _dlgTitle, _dlgSub, _dlgSentence, _dlgRange, _dlgError;
        InputField _dlgField; Slider _dlgSlider; RectTransform _dlgNumberRow;
        Image _dlgConfirm, _dlgMinus, _dlgPlus;
        Text _dlgConfirmText;
        Func<int, string> _dlgSentenceFn; Action<int> _dlgOnNumber; Action _dlgOnConfirm;
        int _dlgMin, _dlgMax, _dlgValue;

        public bool MilDialogOpen => _milOverlay != null && _milOverlay.gameObject.activeSelf;

        /// <summary>Close an open dialog (true) or report there was none (false), so Esc closes the dialog first and the window second.</summary>
        public bool MilCloseDialog()
        {
            if (_milOverlay == null || !_milOverlay.gameObject.activeSelf) return false;
            _milOverlay.gameObject.SetActive(false);
            return true;
        }

        Slider MilSlider(RectTransform row, Action<int> onChange)
        {
            var track = MilRegion(row, "Track", 0, 0.3f, 1, 0.7f, new Vector2(8, 0), new Vector2(-8, 0), Btn);
            var knob = MakePanel(track, "Handle", Vector2.zero, Vector2.up, new Vector2(-9, -8), new Vector2(9, 8));
            var knobImage = knob.GetComponent<Image>(); knobImage.color = BtnOn;
            var s = track.gameObject.AddComponent<Slider>();
            s.targetGraphic = knobImage; s.handleRect = knob; s.wholeNumbers = true;
            s.onValueChanged.AddListener(v => onChange((int)v));
            return s;
        }

        void BuildMilitaryDialogs(RectTransform win)
        {
            _milOverlay = MilRegion(win, "DialogOverlay", 0, 0, 1, 1, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.62f));
            // ---- number / confirm box ----
            _milNumBox = MilRegion(_milOverlay, "Dialog", 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(-310, -215), new Vector2(310, 215), Panel);
            _dlgTitle = MilText(MilBand(_milNumBox, "T", 10, 36, 12, 12), "Title", 26, TextAnchor.MiddleLeft, "", null, true);
            _dlgSub = MilText(MilBand(_milNumBox, "S", 46, 24, 12, 12), "Sub", 17, TextAnchor.MiddleLeft, "", Dim);
            _dlgNumberRow = MilBand(_milNumBox, "Numbers", 80, 84, 12, 12);
            var ctl = MilBand(_dlgNumberRow, "Controls", 0, 40);
            _dlgMinus = MilBtn(MilSlot(ctl, "M", 0, 0.12f, 2), "-", () => DlgSet(_dlgValue - 1), 22);
            _dlgField = MilField(MilSlot(ctl, "F", 0.14f, 0.34f, 2), 22, true, TextAnchor.MiddleCenter);
            _dlgField.onEndEdit.AddListener(v => { if (int.TryParse(v, out int n)) DlgSet(n); else DlgSet(_dlgValue); });
            _dlgPlus = MilBtn(MilSlot(ctl, "P", 0.36f, 0.48f, 2), "+", () => DlgSet(_dlgValue + 1), 22);
            _dlgRange = MilText(MilSlot(ctl, "R", 0.5f, 1, 2), "Range", 16, TextAnchor.MiddleRight, "", Dim);
            _dlgSlider = MilSlider(MilBand(_dlgNumberRow, "SliderRow", 46, 34), n => DlgSet(n));
            _dlgSentence = MilText(MilRegion(_milNumBox, "Sentence", 0, 0, 1, 1, new Vector2(12, 74), new Vector2(-12, -172)), "Sentence", 18, TextAnchor.UpperLeft);
            _dlgError = MilText(MilRegion(_milNumBox, "Error", 0, 0, 1, 0, new Vector2(12, 52), new Vector2(-12, 76)), "Error", 15, TextAnchor.MiddleLeft, "", MilWarn);
            var nav = MilRegion(_milNumBox, "Nav", 0, 0, 1, 0, new Vector2(12, 8), new Vector2(-12, 50));
            MilBtn(MilSlot(nav, "C", 0, 0.45f, 4), "Cancel", () => MilCloseDialog(), 18);
            _dlgConfirm = MilBtn(MilSlot(nav, "OK", 0.5f, 1, 4), "Confirm", () => DlgConfirm(), 18);
            _dlgConfirmText = _dlgConfirm.GetComponentInChildren<Text>();
            BuildWizard();
            _milOverlay.gameObject.SetActive(false);
        }

        void OpenNumberDialog(string title, string sub, int min, int max, int def, Func<int, string> sentence, string confirmLabel, Action<int> onConfirm)
        {
            _milOverlay.gameObject.SetActive(true); _milOverlay.SetAsLastSibling();
            _milNumBox.gameObject.SetActive(true); _milWizBox.gameObject.SetActive(false);
            _dlgTitle.text = title; _dlgSub.text = sub; _dlgConfirmText.text = confirmLabel;
            _dlgNumberRow.gameObject.SetActive(true);
            _dlgSentenceFn = sentence; _dlgOnNumber = onConfirm; _dlgOnConfirm = null;
            _dlgMin = min; _dlgMax = Math.Max(min, max);
            _dlgSlider.minValue = _dlgMin; _dlgSlider.maxValue = _dlgMax;
            _dlgSlider.interactable = _dlgMax > _dlgMin;
            DlgSet(Math.Clamp(def, _dlgMin, _dlgMax));
        }

        void OpenConfirm(string title, string body, string confirmLabel, bool danger, Action onConfirm)
        {
            _milOverlay.gameObject.SetActive(true); _milOverlay.SetAsLastSibling();
            _milNumBox.gameObject.SetActive(true); _milWizBox.gameObject.SetActive(false);
            _dlgTitle.text = title; _dlgSub.text = ""; _dlgConfirmText.text = confirmLabel;
            _dlgNumberRow.gameObject.SetActive(false);
            _dlgSentenceFn = null; _dlgOnNumber = null; _dlgOnConfirm = onConfirm;
            _dlgSentence.text = body; _dlgError.text = "";
            _dlgConfirm.color = danger ? new Color(0.55f, 0.22f, 0.16f) : BtnOn;
            _dlgConfirm.GetComponent<Button>().interactable = true;
        }

        void DlgSet(int v)
        {
            if (_dlgSentenceFn == null) return;
            _dlgValue = Math.Clamp(v, _dlgMin, _dlgMax);
            _dlgSlider.SetValueWithoutNotify(_dlgValue);
            _dlgField.SetTextWithoutNotify(_dlgValue.ToString());
            _dlgRange.text = _dlgMax > _dlgMin ? $"from {_dlgMin} to {_dlgMax}" : $"only {_dlgMax} possible";
            _dlgSentence.text = _dlgSentenceFn(_dlgValue);
            bool ok = _dlgMax >= 1 && (_dlgMax > 0);
            _dlgError.text = _dlgMax <= 0 ? "Nothing can be done right now." : "";
            _dlgConfirm.color = ok ? BtnOn : new Color(0.20f, 0.17f, 0.13f);
            _dlgConfirm.GetComponent<Button>().interactable = ok;
        }

        void DlgConfirm()
        {
            var number = _dlgOnNumber; var confirm = _dlgOnConfirm; int v = _dlgValue;
            MilCloseDialog();
            if (number != null) number(v); else confirm?.Invoke();
        }

        // ---------- Create Company wizard: type, service, name, strength, review ----------

        const int WizSteps = 5;
        readonly RectTransform[] _wizPage = new RectTransform[WizSteps];
        Text _wizTitle, _wizReason, _wizNote, _wizSentence, _wizReview, _wizRange, _wizServiceNote;
        Image _wizBack, _wizNext;
        readonly Image[] _wizTypeBtn = new Image[4];
        readonly Text[] _wizTypeText = new Text[4];
        Image _wizReserve, _wizPro, _wizExisting;
        InputField _wizName, _wizStrengthField; Slider _wizSlider;
        int _wizStep, _wizStrength, _wizMax = 1;
        MilitaryUnitType _wizUnit = MilitaryUnitType.Spearman;
        bool _wizNameEdited;
        int _wizExistingId;

        void BuildWizard()
        {
            _milWizBox = MilRegion(_milOverlay, "Wizard", 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(-360, -250), new Vector2(360, 250), Panel);
            _wizTitle = MilText(MilBand(_milWizBox, "T", 10, 36, 12, 12), "Title", 26, TextAnchor.MiddleLeft, "", null, true);
            for (int i = 0; i < WizSteps; i++)
                _wizPage[i] = MilRegion(_milWizBox, "Page" + i, 0, 0, 1, 1, new Vector2(12, 96), new Vector2(-12, -54));

            // page 0: troop type cards
            var defs = TroopDefinitions.All;
            for (int i = 0; i < 4; i++)
            {
                int idx = i; var def = defs[i];
                var slot = MilRegion(_wizPage[0], "Type" + i, (i % 2) * 0.5f, 1 - (i / 2 + 1) * 0.36f, (i % 2 + 1) * 0.5f, 1 - (i / 2) * 0.36f, new Vector2(3, 3), new Vector2(-3, -3));
                _wizTypeBtn[i] = MilBtn(slot, "", () => WizPickType(def.Type), 16);
                var t = _wizTypeBtn[i].GetComponentInChildren<Text>(); t.alignment = TextAnchor.UpperLeft; t.fontSize = 15;
                Stretch(t.rectTransform, new Vector2(10, 4), new Vector2(-8, -4));
                _wizTypeText[i] = t;
            }
            _wizExisting = MilBtn(MilRegion(_wizPage[0], "Existing", 0, 0, 1, 0, new Vector2(3, 0), new Vector2(-3, 34)), "", () => { if (_wizExistingId != 0) { _milSel = _wizExistingId; MilCloseDialog(); _milStamp = -1; } }, 15);

            // page 1: service category
            _wizReserve = MilBtn(MilBand(_wizPage[1], "R", 0, 64), "Reserve militia\nThey keep their civilian jobs until mobilized.", () => { }, 17);
            _wizPro = MilBtn(MilBand(_wizPage[1], "P", 76, 64), "Professional soldiers\nFull time: they leave their jobs for good and live in barracks.", () => { }, 17);
            _wizServiceNote = MilText(MilBand(_wizPage[1], "N", 150, 90), "Note", 16, TextAnchor.UpperLeft, "", Dim);

            // page 2: name
            MilText(MilBand(_wizPage[2], "L", 0, 28), "Label", 18, TextAnchor.MiddleLeft, "Company name", Dim);
            _wizName = MilField(MilBand(_wizPage[2], "Name", 34, 44), 22, false, TextAnchor.MiddleLeft);
            _wizName.onValueChanged.AddListener(_ => _wizNameEdited = true);
            MilText(MilBand(_wizPage[2], "H", 90, 50), "Hint", 16, TextAnchor.UpperLeft, "A name is suggested for you. You can rename the company at any time.", Dim);

            // page 3: strength
            MilText(MilBand(_wizPage[3], "L", 0, 28), "Label", 18, TextAnchor.MiddleLeft, "How many men should it have?", Dim);
            var ctl = MilBand(_wizPage[3], "C", 34, 40);
            MilBtn(MilSlot(ctl, "M", 0, 0.12f, 2), "-", () => WizSetStrength(_wizStrength - 1), 22);
            _wizStrengthField = MilField(MilSlot(ctl, "F", 0.14f, 0.34f, 2), 22, true, TextAnchor.MiddleCenter);
            _wizStrengthField.onEndEdit.AddListener(v => { if (int.TryParse(v, out int n)) WizSetStrength(n); else WizSetStrength(_wizStrength); });
            MilBtn(MilSlot(ctl, "P", 0.36f, 0.48f, 2), "+", () => WizSetStrength(_wizStrength + 1), 22);
            _wizRange = MilText(MilSlot(ctl, "R", 0.5f, 1, 2), "Range", 16, TextAnchor.MiddleRight, "", Dim);
            _wizSlider = MilSlider(MilBand(_wizPage[3], "S", 80, 34), n => WizSetStrength(n));
            _wizSentence = MilText(MilBand(_wizPage[3], "Sen", 124, 120), "Sentence", 17, TextAnchor.UpperLeft);

            // page 4: review
            _wizReview = MilText(MilRegion(_wizPage[4], "Review", 0, 0, 1, 1, Vector2.zero, Vector2.zero), "Review", 18, TextAnchor.UpperLeft);

            _wizNote = MilText(MilRegion(_milWizBox, "Note", 0, 0, 1, 0, new Vector2(12, 52), new Vector2(-12, 92)), "Note", 15, TextAnchor.MiddleLeft, "", MilWarn);
            _wizReason = _wizNote;
            var nav = MilRegion(_milWizBox, "Nav", 0, 0, 1, 0, new Vector2(12, 8), new Vector2(-12, 50));
            MilBtn(MilSlot(nav, "C", 0, 0.25f, 4), "Cancel", () => MilCloseDialog(), 18);
            _wizBack = MilBtn(MilSlot(nav, "B", 0.4f, 0.68f, 4), "Back", () => WizGo(_wizStep - 1), 18);
            _wizNext = MilBtn(MilSlot(nav, "N", 0.70f, 1, 4), "Next", () => WizNextClicked(), 18);
        }

        /// <summary>Open the Create Company wizard. startUnit pre-selects a troop type (from the empty state's two buttons); null starts on the first type.</summary>
        void OpenWizard(MilitaryUnitType? startUnit)
        {
            if (_menuWorld == null) return;
            _milOverlay.gameObject.SetActive(true); _milOverlay.SetAsLastSibling();
            _milNumBox.gameObject.SetActive(false); _milWizBox.gameObject.SetActive(true);
            _wizUnit = startUnit ?? (_menuWorld.Mil.Barracks > 0 ? MilitaryUnitType.Spearman : MilitaryUnitType.PeasantLevy);
            _wizNameEdited = false; _wizStrength = 0;
            _wizName.SetTextWithoutNotify(MilitaryUi.DefaultName(_menuWorld, _wizUnit));
            WizGo(0);
        }

        string WizUnavailable(MilitaryUnitType u)
        {
            var w = _menuWorld; var def = TroopDefinitions.Get(u);
            if (def.Era > w.Era) return "Needs a later era";
            if (def.Levy) return w.Mil.MusterCapacity <= 0 ? "Muster Ground required" : w.Mil.Registered >= w.Mil.MusterCapacity ? "Muster ground capacity reached" : w.Mil.AvailableToServe <= 0 ? "No eligible residents available" : null;
            return w.Mil.Barracks == 0 ? "Barracks required" : w.Mil.AvailableToServe <= 0 ? "No eligible residents available" : null;
        }

        int WizMaxStrength()
        {
            var w = _menuWorld; var def = TroopDefinitions.Get(_wizUnit);
            if (def.Levy) return Math.Max(0, Math.Min(def.MaxCompany, Math.Min(w.Mil.AvailableToServe, w.Mil.MusterCapacity - w.Mil.Registered)));
            return Math.Max(0, Math.Min(def.MaxCompany, Math.Min(w.Mil.AvailableToServe, w.Mil.BunksFree - Math.Max(0, w.Mil.SoldierGoal - w.Soldiers))));
        }

        void WizPickType(MilitaryUnitType u)
        {
            if (WizUnavailable(u) != null) { _wizNote.text = WizUnavailable(u); return; }
            _wizUnit = u;
            if (!_wizNameEdited) _wizName.SetTextWithoutNotify(MilitaryUi.DefaultName(_menuWorld, u));
            WizGo(0);
        }

        void WizSetStrength(int n)
        {
            _wizStrength = Math.Clamp(n, Math.Min(1, _wizMax), Math.Max(1, _wizMax));
            _wizSlider.SetValueWithoutNotify(_wizStrength);
            _wizStrengthField.SetTextWithoutNotify(_wizStrength.ToString());
            var w = _menuWorld;
            _wizSentence.text = TroopDefinitions.Get(_wizUnit).Levy ? MilitaryUi.RegisterSentence(w, _wizStrength) : MilitaryUi.RecruitSentence(w, _wizUnit, _wizStrength);
        }

        void WizGo(int step)
        {
            var w = _menuWorld; if (w == null) return;
            _wizStep = Math.Clamp(step, 0, WizSteps - 1);
            for (int i = 0; i < WizSteps; i++) _wizPage[i].gameObject.SetActive(i == _wizStep);
            string[] names = { "Troop type", "Service", "Name", "Strength", "Review" };
            _wizTitle.text = $"Create Company: {names[_wizStep]}  ({_wizStep + 1} of {WizSteps})";
            _wizBack.GetComponent<Button>().interactable = _wizStep > 0;
            _wizBack.color = _wizStep > 0 ? Btn : new Color(0.20f, 0.17f, 0.13f);
            var def = TroopDefinitions.Get(_wizUnit);
            _wizNote.text = "";
            if (_wizStep == 0)
            {
                var defs = TroopDefinitions.All;
                for (int i = 0; i < 4; i++)
                {
                    string why = WizUnavailable(defs[i].Type);
                    _wizTypeText[i].text = $"{defs[i].Name}\n{defs[i].Role}\nEquipment: {defs[i].Equipment}\n" + (why == null ? (defs[i].Levy ? "Reserve militia" : "Professional") : "Unavailable: " + why);
                    _wizTypeBtn[i].color = defs[i].Type == _wizUnit ? BtnOn : why == null ? Btn : new Color(0.20f, 0.17f, 0.13f);
                }
                var room = w.Mil.Companies.FirstOrDefault(c => c.Unit == _wizUnit && c.Max - Math.Max(c.Target, c.Personnel) > 0);
                _wizExistingId = room?.Id ?? 0;
                _wizExisting.gameObject.SetActive(room != null);
                if (room != null) _wizExisting.GetComponentInChildren<Text>().text = $"You already have {room.Name} with room for more. Add men to it instead";
            }
            else if (_wizStep == 1)
            {
                _wizReserve.color = def.Levy ? BtnOn : new Color(0.20f, 0.17f, 0.13f);
                _wizPro.color = !def.Levy ? BtnOn : new Color(0.20f, 0.17f, 0.13f);
                _wizServiceNote.text = def.Levy ? $"{def.Name} serve only as reserve militia." : $"{def.Name} serve only as full-time professional soldiers (reserve companies of this type do not exist yet).";
            }
            else if (_wizStep == 3)
            {
                _wizMax = WizMaxStrength();
                _wizSlider.minValue = Math.Min(1, _wizMax); _wizSlider.maxValue = Math.Max(1, _wizMax); _wizSlider.interactable = _wizMax > 1;
                _wizRange.text = _wizMax >= 1 ? $"from 1 to {_wizMax}" : "none possible";
                WizSetStrength(_wizStrength <= 0 ? Math.Min(def.Levy ? 10 : 8, Math.Max(1, _wizMax)) : _wizStrength);
                if (_wizMax < 1) _wizNote.text = WizUnavailable(_wizUnit) ?? "No room right now.";
            }
            else if (_wizStep == 4)
            {
                var lines = new List<string>
                {
                    $"Name: {_wizName.text}", $"Troop type: {def.Name}", $"Service: {(def.Levy ? "Reserve militia" : "Professional soldiers")}", $"Strength: {_wizStrength} men",
                    $"Equipment: {def.Equipment} (improvised for now)", "",
                    "Requirements:",
                    def.Levy ? $"  Muster ground places: {w.Mil.MusterCapacity - w.Mil.Registered} free" : $"  Barracks beds: {w.Mil.BunksFree} free",
                    $"  Eligible residents available: {w.Mil.AvailableToServe}", "",
                    def.Levy ? MilitaryUi.RegisterSentence(w, _wizStrength) : MilitaryUi.RecruitSentence(w, _wizUnit, _wizStrength),
                };
                _wizReview.text = string.Join("\n", lines);
            }
            bool last = _wizStep == WizSteps - 1;
            _wizNext.GetComponentInChildren<Text>().text = last ? "Create company" : "Next";
            bool canNext = _wizStep == 0 ? WizUnavailable(_wizUnit) == null : _wizStep == 3 ? _wizMax >= 1 : !(last && WizMaxStrength() < 1);
            _wizNext.GetComponent<Button>().interactable = canNext;
            _wizNext.color = canNext ? BtnOn : new Color(0.20f, 0.17f, 0.13f);
        }

        void WizNextClicked()
        {
            if (_wizStep < WizSteps - 1) { WizGo(_wizStep + 1); return; }
            var w = _menuWorld; string name = _wizName.text; var unit = _wizUnit; int n = _wizStrength;
            var r = w.Mil.CreateCompany(unit, name, n);
            if (r.Ok) { _milSel = r.Count; MilCloseDialog(); _milMessage = ""; _milStamp = -1; }
            else _wizNote.text = r.Reason;
        }
    }
}
