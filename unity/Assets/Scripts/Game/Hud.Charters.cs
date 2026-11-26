// The Charters panel (key C, or the Charter button under the goal chip): the settlement's rank ladder (Hamlet, Village, Town, City) and, for each
// rung, six charters to choose from, one per way of playing, each showing its gain and its price in plain words. A rung that is waiting for a
// choice makes the button pulse and the game says so once. The data and rules are in Sim/World.Charters.cs.
using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        public event Action<string> CharterPicked;

        RectTransform _charterPanel, _charterBody;
        Image _charterButton;
        string _charterKey = "", _charterNotified = "";
        const string CharterGood = "#9ee68c", CharterBad = "#ff8c73";

        public bool CharterOpen => _charterPanel != null && _charterPanel.gameObject.activeSelf;

        void BuildCharters(RectTransform root)
        {
            _charterButton = MakeButton(_goalCard, "Charters", new Vector2(1, 0.5f), new Vector2(-48, 0), new Vector2(84, 24), ToggleCharters);
            _charterPanel = MakePanel(root, "Charters", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460, -290), new Vector2(460, 290));
            var title = MakeText(_charterPanel, "Title", 22, TextAnchor.UpperLeft); title.fontStyle = FontStyle.Bold; title.color = UiTheme.Gold;
            title.text = "Charters: choose how your settlement grows";
            Stretch(title.rectTransform, new Vector2(16, 0), new Vector2(-130, -10));
            title.rectTransform.offsetMin = new Vector2(16, 520);
            MakeButton(_charterPanel, "Close", Vector2.one, new Vector2(-50, -26), new Vector2(80, 32), () => ToggleCharters());
            _charterBody = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            _charterBody.SetParent(_charterPanel, false);
            Stretch(_charterBody, new Vector2(0, 0), new Vector2(0, -56));
            _charterPanel.gameObject.SetActive(false);
        }

        public void ToggleCharters()
        {
            bool open = !CharterOpen;
            if (open) { ShowTownMenu(TownMenu.Closed); CloseAllMenus(); _charterKey = ""; }
            _charterPanel.gameObject.SetActive(open);
            if (open) _charterPanel.SetAsLastSibling();
        }

        static string CharterState(World w) => w.Rank + "|" + string.Join(",", System.Linq.Enumerable.Select(w.CharterAt, kv => kv.Key + "=" + kv.Value));

        void UpdateCharters(World world)
        {
            if (_charterPanel == null) return;
            var open = world.OpenCharterRung();
            _charterButton.color = open.HasValue ? Color.Lerp(Btn, Pulse, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) : Btn;
            string note = open.HasValue ? open.Value.ToString() : "";
            if (note.Length > 0 && note != _charterNotified)
            {
                _charterNotified = note;
                Notify($"Your settlement has grown to a {world.Rank}. Choose a {open.Value} charter (key C).", world.Day, 8);
            }
            if (!CharterOpen) return;
            string key = CharterState(world);
            if (key == _charterKey) return;
            _charterKey = key;
            RefreshCharters(world);
        }

        void RefreshCharters(World w)
        {
            for (int i = _charterBody.childCount - 1; i >= 0; i--) Destroy(_charterBody.GetChild(i).gameObject);
            var strip = MakeText(_charterBody, "Rank", 17, TextAnchor.UpperLeft); strip.supportRichText = true;
            string Rung(TownRank r) => (w.Rank == r ? "<b><color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Gold) + ">" : "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.TextDim) + ">") + r + (r == TownRank.Hamlet ? "" : $" ({World.RankPeople[(int)r]})") + (w.Rank == r ? "</color></b>" : "</color>");
            strip.text = $"{w.Peasants + w.Soldiers} people:   {Rung(TownRank.Hamlet)}   >   {Rung(TownRank.Village)}   >   {Rung(TownRank.Town)}   >   {Rung(TownRank.City)}";
            var sr = strip.rectTransform; sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1); sr.offsetMin = new Vector2(16, -30); sr.offsetMax = new Vector2(-16, -4);

            float y = -40;
            foreach (var rung in new[] { TownRank.Village, TownRank.Town })
            {
                bool reached = w.Rank >= rung, chosen = w.CharterAt.ContainsKey(rung);
                var head = MakeText(_charterBody, "Head" + rung, 17, TextAnchor.UpperLeft); head.fontStyle = FontStyle.Bold; head.supportRichText = true;
                head.text = chosen ? $"{rung} charter: signed"
                    : reached ? $"{rung} charter: choose one (you can mix paths from rung to rung)"
                    : $"{rung} charter: locked until the settlement has {World.RankPeople[(int)rung]} people ({w.Peasants + w.Soldiers} now)";
                head.color = chosen ? new Color(0.62f, 0.9f, 0.55f) : reached ? UiTheme.Gold : UiTheme.TextDim;
                var hr = head.rectTransform; hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.offsetMin = new Vector2(16, y - 24); hr.offsetMax = new Vector2(-16, y);
                int i = 0;
                foreach (var c in w.CharterChoices(rung))
                {
                    var charter = c;
                    bool mine = chosen && w.CharterAt[rung] == charter.Id;
                    var card = MakePanel(_charterBody, charter.Id, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16 + i * 146, y - 28 - 220), new Vector2(16 + i * 146 + 140, y - 28));
                    card.GetComponent<Image>().color = mine ? new Color(0.25f, 0.34f, 0.2f, 1) : Panel;
                    var t = MakeText(card, "Text", 13, TextAnchor.UpperLeft); t.supportRichText = true;
                    t.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(UiTheme.Gold)}>{charter.Title}</color></b>\n<i>{charter.Path}</i>\n\n<color={CharterGood}>+ {charter.Gain}</color>\n\n<color={CharterBad}>- {charter.Price}</color>";
                    Stretch(t.rectTransform, new Vector2(8, 40), new Vector2(-8, -6));
                    string label = mine ? "Signed" : chosen ? "-" : reached ? "Sign it" : "Locked";
                    var b = MakeButton(card, label, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(110, 28), () => { if (reached && !chosen) CharterPicked?.Invoke(charter.Id); });
                    if (!reached || (chosen && !mine)) b.color = new Color(0.3f, 0.26f, 0.24f);
                    if (mine) b.color = BtnOn;
                    i++;
                }
                y -= 28 + 226;
            }
            var foot = MakeText(_charterBody, "Foot", 13, TextAnchor.UpperLeft); foot.color = UiTheme.TextDim;
            foot.text = "No charter is best. Each gives something and costs something; mixing roads gives a mixed town. City charters come in a later update.";
            var fr = foot.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 0); fr.offsetMin = new Vector2(16, 6); fr.offsetMax = new Vector2(-16, 30);
        }
    }
}
