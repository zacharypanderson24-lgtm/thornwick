// The Town Steward panel (right side, under the goal chip): up to three ranked proposals, each with why, what it achieves, what it costs, a
// Show button (moves the camera and draws the building on the map) and a Build it button. It can be collapsed or hidden and remembers that.
// The proposals come from UI/Steward.cs, worked out from the real state of the town once a day.
using System;
using System.Collections.Generic;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        const string StewardHiddenKey = "thornwick.steward.hidden";
        const int StewardRows = 3;
        const float StewardRowH = 104, StewardHeadH = 30;

        public event Action<Proposal> StewardShow, StewardBuild;

        /// <summary>The proposal whose building the map should draw as a ghost (hovered or shown), or null.</summary>
        public Proposal StewardFocus { get; private set; }

        RectTransform _stewardPanel;
        Text _stewardHead;
        readonly Text[] _stewardTitle = new Text[StewardRows], _stewardBody = new Text[StewardRows];
        readonly Image[] _stewardGo = new Image[StewardRows], _stewardLook = new Image[StewardRows];
        readonly Text[] _stewardGoLabel = new Text[StewardRows];
        readonly RectTransform[] _stewardRow = new RectTransform[StewardRows];
        List<Proposal> _stewardList = new List<Proposal>();
        bool _stewardHidden, _stewardCollapsed, _stewardOn;
        const string StewardFocusKey = "thornwick.steward.focus";
        Focus _focus;
        Text _focusLabel;
        World _stewardWorld; int _stewardDay = -1, _stewardBlds = -1, _stewardPinned = -1;
        Proposal _stewardPin;

        /// <summary>True while the Steward panel is on screen: the older "Getting started" card gives way to it.</summary>
        bool StewardOn => _stewardOn;

        void BuildSteward(RectTransform root)
        {
            _stewardHidden = PlayerPrefs.GetInt(StewardHiddenKey, 0) == 1;
            float h = StewardHeadH + StewardRows * StewardRowH + 6;
            _stewardPanel = MakePanel(root, "Steward", Vector2.one, Vector2.one, new Vector2(-440, -236 - h), new Vector2(-8, -236));
            _stewardHead = MakeText(_stewardPanel, "Head", UiTheme.BodySize, TextAnchor.MiddleLeft); _stewardHead.fontStyle = FontStyle.Bold; _stewardHead.color = UiTheme.Gold;
            var hr = _stewardHead.rectTransform; hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one; hr.offsetMin = new Vector2(10, -StewardHeadH); hr.offsetMax = new Vector2(-232, -2);
            _focus = (Focus)Mathf.Clamp(PlayerPrefs.GetInt(StewardFocusKey, 0), 0, (int)Focus.Fortress);
            var focusButton = MakeButton(_stewardPanel, "Path", Vector2.one, new Vector2(-160, -15), new Vector2(130, 24), () => { _focus = (Focus)(((int)_focus + 1) % (int)(Focus.Fortress + 1)); PlayerPrefs.SetInt(StewardFocusKey, (int)_focus); PlayerPrefs.Save(); _stewardDay = -1; StewardFocus = null; _stewardPin = null; });
            _focusLabel = focusButton.GetComponentInChildren<Text>();
            AddHover(focusButton.gameObject, on => ShowTip(on, focusButton.rectTransform, "Your path: " + _focus, FocusBlurb(_focus), "Click to change the path. Survival problems are always shown first.", true));
            MakeButton(_stewardPanel, "Fold", Vector2.one, new Vector2(-62, -15), new Vector2(40, 24), () => { _stewardCollapsed = !_stewardCollapsed; ApplyStewardLayout(); });
            MakeButton(_stewardPanel, "Hide", Vector2.one, new Vector2(-20, -15), new Vector2(40, 24), () => { _stewardHidden = true; PlayerPrefs.SetInt(StewardHiddenKey, 1); PlayerPrefs.Save(); StewardFocus = null; _stewardPin = null; });
            for (int i = 0; i < StewardRows; i++)
            {
                int row = i;
                float top = -StewardHeadH - i * StewardRowH;
                var r = MakePanel(_stewardPanel, "Proposal" + i, Vector2.one, Vector2.one, new Vector2(-432 + 4, top - StewardRowH + 4), new Vector2(-4, top));
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
                r.offsetMin = new Vector2(4, top - StewardRowH + 4); r.offsetMax = new Vector2(-4, top);
                r.GetComponent<Image>().color = new Color(0, 0, 0, 0.22f);
                _stewardRow[i] = r;
                _stewardTitle[i] = MakeText(r, "Title", UiTheme.BodySize, TextAnchor.UpperLeft); _stewardTitle[i].fontStyle = FontStyle.Bold; _stewardTitle[i].supportRichText = true;
                Stretch(_stewardTitle[i].rectTransform, new Vector2(8, 74), new Vector2(-8, -3));
                _stewardBody[i] = MakeText(r, "Body", UiTheme.SmallSize, TextAnchor.UpperLeft); _stewardBody[i].supportRichText = true;
                Stretch(_stewardBody[i].rectTransform, new Vector2(8, 34), new Vector2(-8, -24));
                _stewardLook[i] = MakeButton(r, "Show", new Vector2(0, 0), new Vector2(52, 18), new Vector2(92, 26), () => OnStewardShow(row));
                _stewardGo[i] = MakeButton(r, "Build it", new Vector2(0, 0), new Vector2(158 + 8, 18), new Vector2(116, 26), () => OnStewardGo(row));
                _stewardGoLabel[i] = _stewardGo[i].GetComponentInChildren<Text>();
                AddHover(r.gameObject, on => { if (on) { StewardFocus = row < _stewardList.Count && _stewardList[row].HasSite ? _stewardList[row] : null; } else if (_stewardPin == null || StewardFocus != _stewardPin) StewardFocus = _stewardPin; });
            }
            _stewardPanel.gameObject.SetActive(false);
        }

        public void ShowStewardAgain() { _stewardHidden = false; PlayerPrefs.SetInt(StewardHiddenKey, 0); PlayerPrefs.Save(); _stewardDay = -1; }

        void OnStewardShow(int row)
        {
            if (row >= _stewardList.Count) return;
            var p = _stewardList[row];
            if (!p.HasSite) { OnStewardGo(row); return; }
            _stewardPin = p; StewardFocus = p; StewardShow?.Invoke(p);
        }

        void OnStewardGo(int row)
        {
            if (row >= _stewardList.Count) return;
            var p = _stewardList[row];
            if (!p.HasSite) { if (p.Show.HasValue) { Catalogue.LastCategory = p.Show.Value; ShowCatalogue(false); ShowCatalogue(true); } return; }
            if (!p.Affordable) { Toast(p.Blocked ?? "Cannot build that yet."); return; }
            StewardBuild?.Invoke(p);
            _stewardPin = null; StewardFocus = null; _stewardDay = -1;   // advise again at once
        }

        void ApplyStewardLayout()
        {
            float h = _stewardCollapsed ? StewardHeadH + 2 : StewardHeadH + Mathf.Max(1, _stewardList.Count) * StewardRowH + 6;
            _stewardPanel.offsetMin = new Vector2(-440, -236 - h);
            for (int i = 0; i < StewardRows; i++) _stewardRow[i].gameObject.SetActive(!_stewardCollapsed && i < _stewardList.Count);
        }

        static string FocusBlurb(Focus f) => f switch
        {
            Focus.Farmer => "Fields, silos and a steady harvest. Grain is your wealth.",
            Focus.Trader => "Markets and caravans. Buy what you lack, sell what you make: you need not farm if you can buy food.",
            Focus.Warlord => "Barracks and soldiers. Raid camps and neighbours for supplies and tribute.",
            Focus.Industrialist => "Sawmills, mines and smitheries. Turn raw materials into metal and goods.",
            Focus.Seafarer => "Fishing huts, docks and boats. Live off the water and trade by sea.",
            Focus.Fortress => "Walls, towers and a trained garrison. Let raiders break against you.",
            _ => "No special path: the Steward offers a bit of everything.",
        };

        static string SeverityTag(int s) => s >= 3 ? "<color=#E8745F>URGENT</color>  " : s == 2 ? "<color=#E8B85F>Soon</color>  " : "";

        void UpdateSteward(World world)
        {
            bool open = _ui.Open != null || MapOpen || world.Defeated || _stewardHidden;
            _stewardOn = !_stewardHidden && !world.Defeated;
            if (_stewardPanel.gameObject.activeSelf == open) _stewardPanel.gameObject.SetActive(!open);
            if (open) { if (StewardFocus != null && (_stewardHidden || world.Defeated)) StewardFocus = null; return; }
            if (_stewardWorld != world || _stewardDay != world.Day || _stewardBlds != world.Blds.Count)
            {
                _stewardWorld = world; _stewardDay = world.Day; _stewardBlds = world.Blds.Count;
                _stewardList = Steward.Advise(world, StewardRows, _focus);
                if (_stewardPin != null && !_stewardList.Exists(p => p.Id == _stewardPin.Id && p.X == _stewardPin.X && p.Y == _stewardPin.Y)) { _stewardPin = null; StewardFocus = null; }
                ApplyStewardLayout();
            }
            _focusLabel.text = "Path: " + _focus;
            _stewardHead.text = _stewardList.Count == 0 ? "Steward: the town is in good order" : $"Steward: {_stewardList.Count} thing{(_stewardList.Count == 1 ? "" : "s")} to look at";
            if (_stewardCollapsed) return;
            for (int i = 0; i < StewardRows; i++)
            {
                if (i >= _stewardList.Count) continue;
                var p = _stewardList[i];
                _stewardTitle[i].text = SeverityTag(p.Severity) + p.Title;
                string cost = p.HasSite ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(p.Affordable ? UiTheme.TextDim : UiTheme.Critical)}>Cost: {p.CostText}{(p.Affordable || p.Blocked == null ? "" : ". " + p.Blocked)}</color>" : (p.Blocked != null ? "\n" + p.Blocked : "");
                _stewardBody[i].text = p.Why + (string.IsNullOrEmpty(p.Effect) ? "" : "\n" + p.Effect) + cost;
                _stewardLook[i].gameObject.SetActive(p.HasSite);
                _stewardGoLabel[i].text = p.HasSite ? "Build it here" : p.Show.HasValue ? "Open menu" : "";
                _stewardGo[i].gameObject.SetActive(p.HasSite || p.Show.HasValue);
                _stewardGo[i].color = p.HasSite && !p.Affordable ? new Color(0.35f, 0.3f, 0.28f) : Btn;
            }
        }
    }
}
