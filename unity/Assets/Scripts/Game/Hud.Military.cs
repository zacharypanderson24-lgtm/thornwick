// Military Command: the window shell (header summary strip, tab bar, body, bottom bar), shared UI parts and the refresh loop.
// The Companies tab is in Hud.Military.Companies.cs, dialogs and the Create Company wizard in Hud.Military.Dialogs.cs.
// Presentation only: every action calls World.Mil (MilitarySystem); every sentence and every "why is this disabled" comes from MilitaryUi /
// MilitaryAdvice in the sim (tested). The window is big and modal like the world map: T opens and closes it, Esc closes a dialog first, then it.
// Tabs: only tabs with real content exist. Add a tab by adding one MilTabDef to BuildMilitary (Overview, Recruitment, Defense, Equipment, Fleet come later).
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
        sealed class MilTabDef { public string Label; public RectTransform Page; public Image Button; }
        readonly List<MilTabDef> _milTabs = new List<MilTabDef>();
        int _milTab;
        RectTransform _milWin, _milBody;
        Text _milTown;
        readonly Text[] _milStatValue = new Text[7];
        readonly Text[] _milStatLabel = new Text[7];
        int _milStamp = -1, _milStampDay = -1;

        static readonly Color MilGood = new Color(0.55f, 0.78f, 0.45f), MilWarn = new Color(0.95f, 0.72f, 0.25f), MilBad = new Color(0.90f, 0.42f, 0.35f);
        static readonly Color MilCard = new Color(0.17f, 0.13f, 0.09f, 1f), MilSunk = new Color(0.06f, 0.05f, 0.04f, 1f), MilGold = new Color(0.82f, 0.66f, 0.25f);

        public bool MilitaryOpen => _townMenu == TownMenu.Troops;
        public bool EditingMilitaryName => (_newWorldSeed != null && _newWorldSeed.isFocused) || _milWin != null && _milWin.GetComponentsInChildren<InputField>(false).Any(f => f.isFocused);
        public void ToggleMilitary() { if (_campOpen && _campPhase == CampPhase.Fight) return; if (_mapOpen) CloseMap(); ShowTownMenu(MilitaryOpen ? TownMenu.Closed : TownMenu.Troops); }

        // ---------- reusable parts ----------

        /// <summary>A region of a parent: anchors as fractions, offsets in pixels. Transparent unless given a colour.</summary>
        RectTransform MilRegion(RectTransform parent, string name, float ax0, float ay0, float ax1, float ay1, Vector2 offMin, Vector2 offMax, Color? color = null)
        {
            var rt = MakePanel(parent, name, new Vector2(ax0, ay0), new Vector2(ax1, ay1), offMin, offMax);
            rt.GetComponent<Image>().color = color ?? Color.clear;
            return rt;
        }

        /// <summary>A band across the top of a parent, `top` px down and `h` tall.</summary>
        RectTransform MilBand(RectTransform parent, string name, float top, float h, float padL = 0, float padR = 0, Color? color = null)
        {
            var rt = MakePanel(parent, name, Vector2.up, Vector2.one, new Vector2(padL, -(top + h)), new Vector2(-padR, -top));
            rt.GetComponent<Image>().color = color ?? Color.clear;
            return rt;
        }

        /// <summary>A slice of a horizontal row: fractions f0..f1 of its width, a small gap either side.</summary>
        RectTransform MilSlot(RectTransform row, string name, float f0, float f1, float gap = 3, Color? color = null) =>
            MilRegion(row, name, f0, 0, f1, 1, new Vector2(gap, 0), new Vector2(-gap, 0), color);

        Text MilText(RectTransform parent, string name, int size, TextAnchor anchor, string text = "", Color? color = null, bool bold = false)
        {
            var t = MakeText(parent, name, size, anchor);
            Stretch(t.rectTransform, new Vector2(4, 0), new Vector2(-4, 0));
            t.text = text; if (color.HasValue) t.color = color.Value; if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }

        /// <summary>A button that fills its parent region.</summary>
        Image MilBtn(RectTransform parent, string label, Action onClick, int font = 16)
        {
            var img = MakeButton(parent, label, Vector2.zero, Vector2.zero, Vector2.zero, onClick);
            Stretch(img.rectTransform, Vector2.zero, Vector2.zero);
            img.GetComponentInChildren<Text>().fontSize = font;
            return img;
        }

        /// <summary>Show a tooltip under an element (windows near the top of the screen have no room above).</summary>
        void MilTipBelow(RectTransform over, string title, string body, string foot = "")
        {
            ShowTip(true, over, title, body, foot);
            var corners = new Vector3[4]; over.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, corners[0], null, out var bottomLeft);
            var size = _root.rect.size;
            _tip.pivot = new Vector2(0, 1);
            _tip.anchoredPosition = new Vector2(Mathf.Clamp(bottomLeft.x + size.x / 2, 8, size.x - 348), bottomLeft.y + size.y / 2 - 6);
        }

        void MilHover(GameObject go, RectTransform over, Func<(string title, string body)> content)
        {
            AddHover(go, on => { if (on) { var c = content(); MilTipBelow(over, c.title, c.body); } else ShowTip(false, over, "", "", ""); });
        }

        sealed class MilBar { public Image Fill; public Text Value; public RectTransform Row; }

        /// <summary>A labelled horizontal progress bar: label | bar | value. Colour follows the value (green good, amber low, red bad).</summary>
        MilBar MilMakeBar(RectTransform parent, string label)
        {
            var row = parent;
            var l = MilText(MilSlot(row, "L", 0, 0.27f, 0), "Label", 16, TextAnchor.MiddleLeft, label);
            var track = MilSlot(row, "Track", 0.28f, 0.84f, 0, MilSunk);
            var track2 = MilRegion(track, "Inner", 0, 0, 1, 1, new Vector2(2, 6), new Vector2(-2, -6));
            var fill = MilRegion(track2, "Fill", 0, 0, 0, 1, Vector2.zero, Vector2.zero, MilGood);
            var v = MilText(MilSlot(row, "V", 0.85f, 1, 0), "Value", 16, TextAnchor.MiddleRight, "", null, true);
            return new MilBar { Fill = fill.GetComponent<Image>(), Value = v, Row = row };
        }

        static void MilSetBar(MilBar b, double value, bool gold = false)
        {
            float f = Mathf.Clamp01((float)value / 100f);
            b.Fill.rectTransform.anchorMax = new Vector2(f, 1);
            b.Fill.color = gold ? MilGold : value >= 60 ? MilGood : value >= 35 ? MilWarn : MilBad;
            b.Value.text = $"{value:0}%";
        }

        // ---------- the window ----------

        void BuildMilitary(RectTransform panel)
        {
            _milWin = panel;
            panel.anchorMin = new Vector2(0.04f, 0.06f); panel.anchorMax = new Vector2(0.96f, 0.94f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.09f, 0.075f, 0.06f, 1f);
            var oldTitle = panel.Find("Title"); if (oldTitle != null) oldTitle.gameObject.SetActive(false);

            // header: title, town, close
            var top = MilBand(panel, "Header", 0, 44, 10, 10);
            var title = MilText(MilSlot(top, "Title", 0, 0.45f, 0), "Title", 28, TextAnchor.MiddleLeft, "MILITARY COMMAND", null, true);
            _milTown = MilText(MilSlot(top, "Town", 0.45f, 0.84f, 0), "Town", 20, TextAnchor.MiddleRight, "", Dim);
            MilBtn(MilSlot(top, "Close", 0.86f, 1, 0, null), "Close  (T)", () => ShowTownMenu(TownMenu.Closed), 18);

            // summary strip: seven compact statistic cards, details in tooltips
            var strip = MilBand(panel, "Summary", 48, 62, 10, 10);
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var card = MilSlot(strip, "Stat" + i, i / 7f, (i + 1) / 7f, 3, MilCard);
                _milStatLabel[i] = MilText(MilBand(card, "L", 4, 20), "Label", 13, TextAnchor.MiddleCenter, "", Dim);
                _milStatValue[i] = MilText(MilBand(card, "V", 24, 34), "Value", 26, TextAnchor.MiddleCenter, "", null, true);
                MilHover(card.gameObject, card, () => MilStatTip(idx));
            }

            // tab bar (only tabs that have content), body, bottom bar
            var tabs = MilBand(panel, "Tabs", 116, 32, 10, 10);
            _milBody = MilRegion(panel, "Body", 0, 0, 1, 1, new Vector2(10, 104), new Vector2(-10, -152));
            _milTabs.Clear();
            var companies = new MilTabDef { Label = "Companies", Page = MilRegion(_milBody, "CompaniesPage", 0, 0, 1, 1, Vector2.zero, Vector2.zero) };
            _milTabs.Add(companies);
            for (int i = 0; i < _milTabs.Count; i++)
            {
                int idx = i;
                float w = 1f / Math.Max(4, _milTabs.Count);
                _milTabs[i].Button = MilBtn(MilSlot(tabs, "Tab" + i, i * w * 0.9f, (i + 1) * w * 0.9f, 2), _milTabs[i].Label, () => { _milTab = idx; _milStamp = -1; }, 17);
            }
            BuildMilitaryCompanies(companies.Page);
            BuildMilitaryBottom(MilRegion(panel, "Bottom", 0, 0, 1, 0, new Vector2(10, 8), new Vector2(-10, 96), MilCard));
            BuildMilitaryDialogs(panel);
        }

        (string title, string body) MilStatTip(int i)
        {
            var w = _menuWorld; if (w == null) return ("", "");
            var m = w.Mil;
            switch (i)
            {
                case 0: return ("Population", $"Everyone in the settlement: {w.Peasants} civilians and {w.Soldiers} professional soldiers. Nobody is counted twice: militia are civilians until called up.");
                case 1: return ("Fit to serve", $"Civilians old enough to fight: {m.EligiblePopulation}. {m.AvailableToServe} are free to enlist (not already on the militia roll, and the town keeps {MilitaryConfig.MinCivilians} back).");
                case 2: return ("Civilian workers", $"People at work or looking for it: {m.CivilianWorkforce}. {w.D.Jobs} jobs need filling and {w.D.Idle} people have none. Mobilized militia and soldiers are not counted.");
                case 3: return ("Professional soldiers", $"Full-time soldiers: {w.Soldiers} ({w.Infantry} infantry, {w.Archers} archers). They left their jobs for good, eat 1.5 food each and cost 0.1 gold a day.");
                case 4: return ("Reserve militia", $"Villagers on the militia roll: {m.Registered - m.MobilisedCount} waiting at their normal jobs. They only stop working if you mobilize them.");
                case 5: return ("Mobilized militia", $"Militia called up: {m.MobilisedCount}. They gathered at the muster ground, their jobs stand empty and they defend the settlement.");
                default: return ("Barracks", $"Beds: {w.Soldiers} used of {w.D.Bunks} in {m.Barracks} barracks connected by road." + (m.Unhoused > 0 ? $" {m.Unhoused} soldiers have no bed." : ""));
            }
        }

        void RefreshMilitary(World world)
        {
            var mil = world.Mil;
            int stamp = world.Version * 31 + mil.Companies.Count;
            if (stamp == _milStamp && world.Day == _milStampDay) return;
            _milStamp = stamp; _milStampDay = world.Day;
            _milTown.text = MilitaryUi.TownName;
            string[] labels = { "Population", "Fit to serve", "Civilian workers", "Soldiers", "Reserve militia", "Mobilized", "Barracks beds" };
            string[] values =
            {
                $"{world.Peasants + world.Soldiers}", $"{mil.AvailableToServe}", $"{mil.CivilianWorkforce}", $"{world.Soldiers}",
                $"{mil.Registered - mil.MobilisedCount}", $"{mil.MobilisedCount}", $"{world.Soldiers}/{world.D.Bunks}",
            };
            for (int i = 0; i < 7; i++) { _milStatLabel[i].text = labels[i]; _milStatValue[i].text = values[i]; }
            _milStatValue[6].color = mil.Unhoused > 0 ? MilBad : Ink;
            _milStatValue[2].color = world.D.Jobs > mil.CivilianWorkforce ? MilWarn : Ink;
            for (int i = 0; i < _milTabs.Count; i++) { _milTabs[i].Page.gameObject.SetActive(i == _milTab); _milTabs[i].Button.color = i == _milTab ? BtnOn : Btn; }
            RefreshMilitaryCompanies(world);
            RefreshMilitaryBottom(world);
        }
    }
}
