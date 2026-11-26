using System;
using System.Linq;
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    /// <summary>The scenario panel (top right, under the goal chip): what is coming, how many days away, how ready the town is, and a checklist of
    /// what would help. While the scenario is happening it says what is going on (World.Scenarios.cs).</summary>
    public partial class Hud
    {
        RectTransform _scenarioCard;
        Text _scenarioTitle, _scenarioBody;
        Image _scenarioFill, _scenarioTrack;

        const float ScenarioTop = -240, ScenarioH = 190;

        void BuildScenario(RectTransform root)
        {
            _scenarioCard = MakePanel(root, "Scenario", Vector2.one, Vector2.one, new Vector2(-440, ScenarioTop - ScenarioH), new Vector2(-8, ScenarioTop));
            _scenarioTitle = MakeText(_scenarioCard, "Title", 17, TextAnchor.UpperLeft);
            _scenarioTitle.fontStyle = FontStyle.Bold;
            Stretch(_scenarioTitle.rectTransform, new Vector2(10, ScenarioH - 30), new Vector2(-10, -6));
            var track = MakePanel(_scenarioCard, "Track", Vector2.up, Vector2.one, new Vector2(10, -42), new Vector2(-10, -34));
            track.anchorMin = new Vector2(0, 1); track.anchorMax = new Vector2(1, 1);
            _scenarioTrack = track.GetComponent<Image>(); _scenarioTrack.color = new Color(0.12f, 0.09f, 0.07f); _scenarioTrack.raycastTarget = false;
            var fill = MakePanel(track, "Fill", Vector2.zero, Vector2.up, Vector2.zero, Vector2.zero);
            _scenarioFill = fill.GetComponent<Image>(); _scenarioFill.raycastTarget = false;
            _scenarioBody = MakeText(_scenarioCard, "Body", 14, TextAnchor.UpperLeft);
            Stretch(_scenarioBody.rectTransform, new Vector2(10, 6), new Vector2(-10, -48));
            _scenarioBody.supportRichText = true;
            _scenarioCard.gameObject.SetActive(false);
        }

        void UpdateScenario(World world)
        {
            var s = world.CurrentScenario;
            bool show = s != null && !world.Defeated;
            if (_scenarioCard.gameObject.activeSelf != show) _scenarioCard.gameObject.SetActive(show);
            if (!show) return;
            int ready = world.ScenarioReadiness();
            var sb = new StringBuilder();
            if (s.Phase == ScenarioPhase.Warned)
            {
                int left = Math.Max(0, s.ArriveDay - world.Day);
                _scenarioTitle.text = $"{World.ScenarioTitle(s.Id)} in {left} {(left == 1 ? "day" : "days")}  ·  {ready}% ready";
                _scenarioTitle.color = ready >= 80 ? Ink : ready >= 40 ? Pulse : BadInk;
                sb.AppendLine(world.ScenarioWarning(s));
            }
            else
            {
                _scenarioTitle.text = $"{World.ScenarioTitle(s.Id)}  ·  happening now";
                _scenarioTitle.color = BadInk;
                switch (s.Id)
                {
                    case "famine": sb.AppendLine($"Fields give a quarter of their grain for {Math.Max(0, s.EndDay - world.Day)} more days."); break;
                    case "fire": sb.AppendLine($"{world.Burning.Count()} burning. Bulldoze a building in its path to make a firebreak. Lost {s.Burned}, saved {s.Saved}."); break;
                    case "host": sb.AppendLine($"{s.Size} raiders. Everyone has gone to the Town Hall."); break;
                }
            }
            foreach (var c in world.ScenarioChecks())
                sb.AppendLine($"<color=#{(c.Ok ? "A8D98A" : "FF9A80")}>{(c.Ok ? "[x]" : "[ ]")}</color> {c.Text}");
            _scenarioBody.text = sb.ToString();
            _scenarioFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ready / 100f), 1);
            _scenarioFill.color = ready >= 80 ? new Color(0.55f, 0.75f, 0.38f) : ready >= 40 ? Pulse : new Color(0.74f, 0.30f, 0.20f);
        }
    }
}
