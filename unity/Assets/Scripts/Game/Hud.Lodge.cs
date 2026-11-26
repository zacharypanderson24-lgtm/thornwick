// The woodcutter lodge's panel: click a lodge with the Inspect tool to read what it is doing (mode, forest around it, output, problems with what to do about them)
// and to change its operating mode. The hover panel follows the mouse and cannot hold buttons, so this one is a small window that stays until closed.
using System;
using System.Linq;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        public event Action<Building, ForestMode> ForestModePicked;
        RectTransform _lodgePanel; Text _lodgeTitle, _lodgeBody, _lodgeModeNote; Building _lodge;
        readonly Image[] _lodgeModes = new Image[3];
        static readonly ForestMode[] LodgeModeOrder = { ForestMode.Sustainable, ForestMode.Intensive, ForestMode.Clearing };

        void BuildLodgePanel(RectTransform root)
        {
            _lodgePanel = MakePanel(root, "LodgePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -250), new Vector2(300, 250));
            _lodgeTitle = MakeText(_lodgePanel, "Title", 22, TextAnchor.UpperLeft); _lodgeTitle.fontStyle = FontStyle.Bold;
            Stretch(_lodgeTitle.rectTransform, new Vector2(18, 440), new Vector2(-18, -12));
            for (int i = 0; i < 3; i++)
            {
                var mode = LodgeModeOrder[i];
                _lodgeModes[i] = MakeButton(_lodgePanel, World.ModeName(mode), new Vector2(0, 1), new Vector2(112 + i * 188, -66), new Vector2(176, 34), () => PickForestMode(mode));
            }
            _lodgeModeNote = MakeText(_lodgePanel, "ModeNote", UiTheme.SmallSize, TextAnchor.UpperLeft); _lodgeModeNote.color = Dim;
            Stretch(_lodgeModeNote.rectTransform, new Vector2(18, 370), new Vector2(-18, -92));
            _lodgeBody = MakeText(_lodgePanel, "Body", 15, TextAnchor.UpperLeft);
            Stretch(_lodgeBody.rectTransform, new Vector2(18, 56), new Vector2(-18, -128));
            MakeButton(_lodgePanel, "Close", new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(150, 34), () => ShowLodgePanel(null));
            _lodgePanel.gameObject.SetActive(false);
        }

        public RectTransform LodgeModeButton(int i) => _lodgeModes[i].rectTransform;   // for tests: the mode buttons
        public string LodgeBodyText => _lodgeBody.text;
        public bool LodgePanelOpen => _lodgePanel != null && _lodgePanel.gameObject.activeSelf;
        public Building InspectedLodge => LodgePanelOpen ? _lodge : null;

        public void ShowLodgePanel(Building b)
        {
            _lodge = b;
            if (_lodgePanel == null) return;
            _lodgePanel.gameObject.SetActive(b != null);
            if (b != null) { _lodgePanel.SetAsLastSibling(); if (_tip != null) _tip.SetAsLastSibling(); }
            RefreshLodgePanel();
        }

        void PickForestMode(ForestMode mode)
        {
            if (_lodge == null) return;
            ForestModePicked?.Invoke(_lodge, mode);
            RefreshLodgePanel();
        }

        void RefreshLodgePanel()
        {
            if (!LodgePanelOpen) return;
            if (_lodge == null || _world == null || !_world.Blds.Contains(_lodge)) { _lodgePanel.gameObject.SetActive(false); return; }
            var w = _world;
            _lodgeTitle.text = $"{_lodge.Name} at {_lodge.X},{_lodge.Y}   ·   workers {_lodge.Workers}/{w.ActiveJobs(_lodge)} needed ({_lodge.Jobs} jobs)";
            for (int i = 0; i < 3; i++) _lodgeModes[i].color = _lodge.Mode == LodgeModeOrder[i] ? BtnOn : Btn;
            _lodgeModeNote.text = World.ModeName(_lodge.Mode) + ": " + World.ModeBlurb(_lodge.Mode) + ".";
            var lines = new System.Collections.Generic.List<string> { w.LodgeStatus(_lodge) };
            lines.AddRange(w.BuildingWarnings(_lodge).Select(x => x.ToString()));
            _lodgeBody.text = string.Join("\n\n", lines);
        }
    }
}
