// The raider camp screen, opened by clicking a red dot on the world map. The camp itself is shown in the game view, drawn like any settlement
// (a small camp World with tents and supply piles, see World.CampWorld.cs); this is the panel beside it where you plan the landing (click a
// tile on the edge of the camp in the view, then choose how many infantry and archers come ashore there; add as many landings as you like),
// confirm, watch the fight play out in the view, and read the report. The troops leave the settlement when you confirm and come home with
// their loot (World.Camps.cs). GameController draws the camp and turns clicks in the view into CampTileClick.
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
        enum CampPhase { Plan, Fight, Report }

        RectTransform _campPanel, _campPlanBox, _campFightBox, _campReportBox;
        Text _campTitle, _campInfo, _campPlanText, _campAvail, _campLanding, _campMessage, _campFightText, _campReportText, _campHoverText;
        UnityEngine.UI.Image _campConfirm, _campPauseButton;
        readonly UnityEngine.UI.Image[] _campSpeedButtons = new UnityEngine.UI.Image[3];

        RaiderCamp _camp;
        CampBattle _campPreview, _campBattle;
        CampPhase _campPhase;
        readonly List<BattleGroup> _campGroups = new List<BattleGroup>();
        (int x, int y)? _campSelected, _campHover;
        bool _campOpen, _campPaused;
        int _campSpeedIndex;
        float _campAcc;
        static readonly float[] CampSpeeds = { 1f, 2f, 4f };

        public bool CampOpen => _campOpen;
        /// <summary>The camp being planned or fought (null when the screen is closed).</summary>
        public RaiderCamp CurrentCamp => _campOpen ? _camp : null;
        /// <summary>What the game view draws on the camp: the battle once it has started, otherwise a preview with only the raiders.</summary>
        public CampBattle CampView => _campOpen ? (_campBattle ?? _campPreview) : null;
        /// <summary>How far through the current tick the fight is (0..1), for smooth movement.</summary>
        public float CampFrac => _campPhase == CampPhase.Fight && !_campPaused && _campBattle != null && !_campBattle.Over ? Mathf.Clamp01(_campAcc) : 0f;
        public bool CampPlanning => _campOpen && _campPhase == CampPhase.Plan;
        public IReadOnlyList<BattleGroup> CampGroups => _campGroups;
        public (int x, int y)? CampSelected => _campSelected;
        public (int x, int y)? CampHoverTile => _campPhase == CampPhase.Plan ? _campHover : null;

        /// <summary>Raised when the player confirms an invasion; the game takes the troops out of the settlement and returns the battle (null if it cannot).</summary>
        public event Func<RaiderCamp, IList<BattleGroup>, CampBattle> InvasionConfirmed;
        /// <summary>Raised once when the fight is over so the game can settle it (destroy the camp, send the survivors home).</summary>
        public event Action<CampBattle> InvasionFinished;

        static string EdgeName(int x, int y) => y == 0 ? "north edge" : y == CampMap.Size - 1 ? "south edge" : x == 0 ? "west edge" : "east edge";

        void BuildCamp(RectTransform root)
        {
            // a panel down the right side, under the time controls, so the camp stays visible in the game view
            _campPanel = MakePanel(root, "RaiderCamp", Vector2.one, Vector2.one, new Vector2(-396, -690), new Vector2(-8, -122));
            _campPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.07f, 0.06f, 0.96f);
            _campTitle = PanelLabel(_campPanel, "Title", "", 20, 6, 36); _campTitle.fontStyle = FontStyle.Bold;
            _campInfo = MakeText(_campPanel, "CampInfo", 14, TextAnchor.UpperLeft); TopStretch(_campInfo.rectTransform, 40, 120);
            _campHoverText = MakeText(_campPanel, "Hover", 13, TextAnchor.UpperLeft); _campHoverText.color = Dim; TopStretch(_campHoverText.rectTransform, 122, 146);

            // planning
            _campPlanBox = MakePanel(_campPanel, "Plan", Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -150));
            _campPlanBox.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            _campPlanText = MakeText(_campPlanBox, "PlanText", 14, TextAnchor.UpperLeft); TopStretch(_campPlanText.rectTransform, 0, 92);
            _campAvail = MakeText(_campPlanBox, "Available", 14, TextAnchor.UpperLeft); TopStretch(_campAvail.rectTransform, 96, 140);
            _campLanding = MakeText(_campPlanBox, "Landing", 14, TextAnchor.UpperLeft); TopStretch(_campLanding.rectTransform, 142, 186);
            float y = 214;
            foreach (bool archers in new[] { false, true })
            {
                bool a = archers;
                var label = MakeText(_campPlanBox, archers ? "ArchersLabel" : "InfantryLabel", 14, TextAnchor.MiddleLeft); label.text = archers ? "Archers" : "Infantry";
                var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = Vector2.up; lr.pivot = new Vector2(0, 0.5f); lr.sizeDelta = new Vector2(80, 28); lr.anchoredPosition = new Vector2(8, -y);
                int k = 0;
                foreach (var (text, delta) in new[] { ("-5", -5), ("-1", -1), ("+1", 1), ("+5", 5) })
                {
                    int d = delta;
                    MakeButton(_campPlanBox, text, Vector2.up, new Vector2(110 + 22 + k * 56, -y), new Vector2(50, 28), () => ChangeCampTroops(a, d));
                    k++;
                }
                y += 38;
            }
            MakeButton(_campPlanBox, "Send everyone here", Vector2.up, new Vector2(8 + 90, -y), new Vector2(180, 28), SendAllHere);
            MakeButton(_campPlanBox, "Remove landing", Vector2.up, new Vector2(8 + 180 + 8 + 70, -y), new Vector2(140, 28), RemoveCampLanding);
            y += 36;
            _campMessage = MakeText(_campPlanBox, "Message", 13, TextAnchor.UpperLeft); _campMessage.color = new Color(1f, 0.7f, 0.5f);
            TopStretch(_campMessage.rectTransform, (int)y, (int)y + 50);
            _campConfirm = MakeButton(_campPlanBox, "Confirm invasion", Vector2.zero, new Vector2(8 + 100, 14 + 18), new Vector2(200, 36), ConfirmCampInvasion);
            MakeButton(_campPlanBox, "Back to the map", Vector2.zero, new Vector2(8 + 200 + 8 + 70, 14 + 18), new Vector2(140, 36), CloseCamp);

            // fighting
            _campFightBox = MakePanel(_campPanel, "Fight", Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -150));
            _campFightBox.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            _campFightText = MakeText(_campFightBox, "FightText", 15, TextAnchor.UpperLeft); TopStretch(_campFightText.rectTransform, 0, 110);
            _campPauseButton = MakeButton(_campFightBox, "Pause", Vector2.up, new Vector2(8 + 40, -140), new Vector2(80, 30), () => { _campPaused = !_campPaused; RefreshCampButtons(); });
            string[] names = { "1x", "2x", "4x" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _campSpeedButtons[i] = MakeButton(_campFightBox, names[i], Vector2.up, new Vector2(8 + 90 + 23 + i * 52, -140), new Vector2(46, 30), () => { _campSpeedIndex = index; _campPaused = false; RefreshCampButtons(); });
            }
            MakeButton(_campFightBox, "Retreat", Vector2.zero, new Vector2(8 + 60, 14 + 18), new Vector2(120, 36), () => { _campBattle?.Retreat(); });

            // report
            _campReportBox = MakePanel(_campPanel, "Report", Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -150));
            _campReportBox.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            _campReportText = MakeText(_campReportBox, "ReportText", 15, TextAnchor.UpperLeft); TopStretch(_campReportText.rectTransform, 0, 330);
            MakeButton(_campReportBox, "Back to the map", Vector2.zero, new Vector2(8 + 80, 14 + 18), new Vector2(160, 36), CloseCamp);

            _campPanel.gameObject.SetActive(false);
        }

        // ---------- opening and closing ----------
        public void OpenCamp(RaiderCamp camp)
        {
            if (camp == null || _world == null) return;
            _camp = camp; _campOpen = true; _campPhase = CampPhase.Plan;
            _campGroups.Clear(); _campSelected = null; _campHover = null; _campBattle = null; _campPaused = false; _campSpeedIndex = 0; _campAcc = 0;
            _campPreview = new CampBattle(camp, new List<BattleGroup>());
            _campTitle.text = camp.Name;
            _mapPanel.gameObject.SetActive(false);   // the camp is shown in the game view; the map comes back when this closes
            _campPanel.gameObject.SetActive(true);
            _campPanel.SetAsLastSibling(); _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling();
            if (_mapView != null) _mapView.enabled = false;
            RefreshCamp();
        }

        public void CloseCamp()
        {
            if (!_campOpen || _campPhase == CampPhase.Fight) return;
            _campOpen = false;
            _campPanel.gameObject.SetActive(false);
            if (_mapOpen) _mapPanel.gameObject.SetActive(true);
            if (_mapView != null) _mapView.enabled = true;
        }

        /// <summary>Esc: close the camp screen first (not during a fight), otherwise the map.</summary>
        public void EscapePanels() { if (_campOpen) CloseCamp(); else CloseMap(); }

        // ---------- planning ----------
        BattleGroup SelectedGroup => _campSelected.HasValue ? _campGroups.FirstOrDefault(g => g.X == _campSelected.Value.x && g.Y == _campSelected.Value.y) : null;
        int AvailableInfantry => _world == null ? 0 : _world.Infantry - _campGroups.Sum(g => g.Infantry);
        int AvailableArchers => _world == null ? 0 : _world.Archers - _campGroups.Sum(g => g.Archers);

        /// <summary>The cursor in the game view is over this camp tile (x, y are 0..31 within the camp), or off the camp (null).</summary>
        public void CampTileHover((int x, int y)? tile)
        {
            _campHover = tile;
            if (tile.HasValue && _campPhase == CampPhase.Plan)
                _campHoverText.text = CampMap.IsEdge(tile.Value.x, tile.Value.y) && _campPreview.Map.Passable(tile.Value.x, tile.Value.y)
                    ? $"Tile {tile.Value.x},{tile.Value.y} ({EdgeName(tile.Value.x, tile.Value.y)}): click to land here" : "";
            else _campHoverText.text = "";
        }

        /// <summary>A click on a camp tile in the game view while planning.</summary>
        public void CampTileClick(int x, int y)
        {
            if (!_campOpen || _campPhase != CampPhase.Plan) return;
            if (!CampMap.IsEdge(x, y) || !_campPreview.Map.Passable(x, y)) { _campMessage.text = "Soldiers can only land on the edge of the camp."; return; }
            _campMessage.text = "";
            _campSelected = (x, y);
            if (SelectedGroup == null) _campGroups.Add(new BattleGroup { X = x, Y = y });
            RefreshCamp();
        }

        void ChangeCampTroops(bool archers, int delta)
        {
            var g = SelectedGroup; if (g == null) { _campMessage.text = "Click a tile on the edge of the camp first."; return; }
            _campMessage.text = "";
            if (archers) g.Archers = Mathf.Clamp(g.Archers + delta, 0, g.Archers + AvailableArchers);
            else g.Infantry = Mathf.Clamp(g.Infantry + delta, 0, g.Infantry + AvailableInfantry);
            RefreshCamp();
        }

        void SendAllHere()
        {
            var g = SelectedGroup; if (g == null) { _campMessage.text = "Click a tile on the edge of the camp first."; return; }
            g.Infantry += AvailableInfantry; g.Archers += AvailableArchers;
            RefreshCamp();
        }

        void RemoveCampLanding()
        {
            var g = SelectedGroup; if (g == null) return;
            _campGroups.Remove(g); _campSelected = null;
            RefreshCamp();
        }

        void ConfirmCampInvasion()
        {
            var groups = _campGroups.Where(g => g.Total > 0).ToList();
            var ok = _world.CanInvade(_camp, groups);
            if (!ok.Ok) { _campMessage.text = ok.Reason; return; }
            var battle = InvasionConfirmed?.Invoke(_camp, groups);
            if (battle == null) { _campMessage.text = "The invasion could not start."; return; }
            _campBattle = battle; _campPhase = CampPhase.Fight; _campPaused = false; _campSpeedIndex = 0; _campAcc = 0; _campSelected = null; _campHover = null;
            _campHoverText.text = "";
            RefreshCamp();
        }

        // ---------- the panel ----------
        void RefreshCamp()
        {
            if (!_campOpen) return;
            var view = _campBattle ?? _campPreview;
            int tents = view.Map.Tents.Count, supplies = view.Map.Piles.Sum(p => Math.Max(0, p.Amount));
            _campInfo.text = $"<b>Strength {_camp.Strength} of 5</b>   ·   {_world.CampKm(_camp)} km away, {_world.MarchDays(_camp)} days of marching home\n" +
                             $"{tents} tents round {view.Map.Fires.Count} campfires, the warlord's tent and {view.Map.Racks.Count} weapon racks\n" +
                             $"{view.AliveRaiders} raiders: {view.Units.Count(u => u.Alive && u.Kind == BKind.RaiderArcher)} archers, {view.Units.Count(u => u.Alive && u.Kind == BKind.Brute)} brutes" + (view.Units.Any(u => u.Alive && u.Kind == BKind.Chief) ? " and the warlord\n" : "\n") +
                             $"{view.Map.Piles.Count(p => p.Amount > 0)} supply piles holding about {supplies} goods";
            _campPlanBox.gameObject.SetActive(_campPhase == CampPhase.Plan);
            _campFightBox.gameObject.SetActive(_campPhase == CampPhase.Fight);
            _campReportBox.gameObject.SetActive(_campPhase == CampPhase.Report);
            if (_campPhase == CampPhase.Plan)
            {
                _campPlanText.text = "Plan the landing. Click a tile on the edge of the camp in the view, then choose how many infantry and archers come ashore there. Add as many landings as you like, then confirm.";
                _campAvail.text = $"At home: {AvailableInfantry} infantry, {AvailableArchers} archers still unassigned";
                var g = SelectedGroup;
                _campLanding.text = g == null ? $"Landings planned: {_campGroups.Count(x => x.Total > 0)}" : $"Landing at {g.X},{g.Y} ({EdgeName(g.X, g.Y)}): {g.Infantry} infantry, {g.Archers} archers";
                _campConfirm.color = _campGroups.Sum(x => x.Total) > 0 ? BtnOn : Btn;
            }
            RefreshCampButtons();
        }

        void RefreshCampButtons()
        {
            for (int i = 0; i < 3; i++) _campSpeedButtons[i].color = i == _campSpeedIndex && !_campPaused ? BtnOn : Btn;
            _campPauseButton.color = _campPaused ? BtnOn : Btn;
        }

        void UpdateCamp()
        {
            if (!_campOpen || _campPhase != CampPhase.Fight || _campBattle == null) return;
            if (!_campPaused && !_campBattle.Over)
            {
                _campAcc += Time.unscaledDeltaTime * CampSpeeds[_campSpeedIndex] * 2f;   // two ticks a second at normal speed
                while (_campAcc >= 1f && !_campBattle.Over) { _campAcc -= 1f; _campBattle.Step(); }
            }
            _campFightText.text = $"<b>The fight is on</b>   tick {_campBattle.Tick}\nYour soldiers: {_campBattle.AliveAttackers} left (lost {_campBattle.LostInfantry + _campBattle.LostArchers})\n" +
                                  $"Raiders: {_campBattle.AliveRaiders} left ({_campBattle.RaidersKilled} killed)" + (_campBattle.Alarm ? "\nThe camp is awake." : "\nThe camp has not seen you yet.");
            if (_campBattle.Over)
            {
                _campPhase = CampPhase.Report;
                InvasionFinished?.Invoke(_campBattle);
                var home = _world.Expeditions.LastOrDefault(e => e.CampId == _camp.Id);
                _campReportText.text = $"<b>Fight report</b>\n\n{_campBattle.Summary()}" +
                    (home != null ? $"\n\nThe army marches home and arrives on day {home.ReturnDay} ({_world.MarchDays(_camp)} days)." : "");
                _campTitle.text = _camp.Name + (_campBattle.Victory ? ": destroyed" : "");
                RefreshCamp();
            }
        }
    }
}
