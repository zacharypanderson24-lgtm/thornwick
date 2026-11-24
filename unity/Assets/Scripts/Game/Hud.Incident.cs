using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    /// <summary>The incident card: a title, what happened, and up to three choices with what each costs (World.Incidents.cs).</summary>
    public partial class Hud
    {
        /// <summary>The player picked choice i on the card.</summary>
        public event Action<int> IncidentChosen;

        RectTransform _incidentCard;
        Text _incidentTitle, _incidentBody;
        readonly Image[] _incidentButtons = new Image[3];
        readonly Text[] _incidentLabels = new Text[3];
        string _incidentShown;

        void BuildIncident(RectTransform root)
        {
            _incidentCard = MakePanel(root, "Incident", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280, -60), new Vector2(280, 200));
            _incidentTitle = MakeText(_incidentCard, "Title", 24, TextAnchor.UpperCenter);
            _incidentTitle.fontStyle = FontStyle.Bold; _incidentTitle.color = Pulse;
            Stretch(_incidentTitle.rectTransform, new Vector2(16, 220), new Vector2(-16, -12));
            _incidentBody = MakeText(_incidentCard, "Body", 17, TextAnchor.UpperCenter);
            Stretch(_incidentBody.rectTransform, new Vector2(20, 130), new Vector2(-20, -50));
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var img = MakeButton(_incidentCard, "Choice" + i, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(520, 30), () => IncidentChosen?.Invoke(k));
                _incidentButtons[i] = img; _incidentLabels[i] = img.GetComponentInChildren<Text>();
                var rt = (RectTransform)img.transform; rt.anchoredPosition = new Vector2(0, 12 + 36 * (2 - i) + 0);
                AddHover(img.gameObject, on =>
                {
                    if (_world == null || _world.CurrentIncident == null) return;
                    var choices = _world.IncidentChoices();
                    if (k < choices.Count) ShowTip(on, rt, choices[k].Label, choices[k].Detail, "");
                });
            }
            _incidentCard.gameObject.SetActive(false);
        }

        void UpdateIncident(World world)
        {
            var inc = world.CurrentIncident;
            bool show = inc != null && !world.Defeated;
            if (_incidentCard.gameObject.activeSelf != show) _incidentCard.gameObject.SetActive(show);
            if (!show) { _incidentShown = null; return; }
            string key = inc.Id + inc.RaisedDay;
            if (_incidentShown != key) { _incidentShown = key; _incidentCard.SetAsLastSibling(); }
            _incidentTitle.text = World.IncidentTitle(inc.Id);
            _incidentBody.text = world.IncidentBody(inc) + $"\n(Decide within {Math.Max(0, World.IncidentWaitDays - (world.Day - inc.RaisedDay))} days, or the council settles it.)";
            var choices = world.IncidentChoices();
            for (int i = 0; i < 3; i++)
            {
                bool has = i < choices.Count;
                if (_incidentButtons[i].gameObject.activeSelf != has) _incidentButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                _incidentLabels[i].text = choices[i].Label;
                _incidentLabels[i].color = choices[i].Enabled ? Ink : new Color(Dim.r, Dim.g, Dim.b, 0.5f);
                _incidentButtons[i].GetComponent<Button>().interactable = choices[i].Enabled;
            }
            // the buttons sit at the bottom, a row each, the first on top; with two choices the third row is empty, so lift them evenly
            int n = choices.Count;
            for (int i = 0; i < n; i++) ((RectTransform)_incidentButtons[i].transform).anchoredPosition = new Vector2(0, 12 + 36 * (n - 1 - i));
        }
    }
}
