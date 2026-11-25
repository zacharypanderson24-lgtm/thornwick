using System;
using System.Linq;
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    /// <summary>The Mood card at the right end of the strip under the build bar: happiness, what is moving it, and the Festival button.</summary>
    public partial class Hud
    {
        /// <summary>The player pressed Festival.</summary>
        public event Action FestivalPicked;

        static readonly Color MoodBar = new Color(0.86f, 0.64f, 0.30f);
        NeedMeter _moodMeter;
        Image _festivalButton;
        Text _festivalLabel;

        void BuildMood(RectTransform strip)
        {
            _moodMeter = BuildNeedMeter(strip, "Mood", 0.8f, 1, MoodBar, MoodTip);
            // leave room on the right for the button
            _moodMeter.Amount.rectTransform.offsetMax = new Vector2(-96, -2);
            _moodMeter.Alert.rectTransform.offsetMax = new Vector2(-150, -2);
            _festivalButton = MakeButton(_moodMeter.Card, "Festival", new Vector2(1, 0.5f), new Vector2(-48, 3), new Vector2(84, 26), () => FestivalPicked?.Invoke());
            _festivalLabel = _festivalButton.GetComponentInChildren<Text>();
            AddHover(_festivalButton.gameObject, on =>
            {
                if (_world == null) return;
                var can = _world.CanHoldFestival();
                ShowTip(on, (RectTransform)_festivalButton.transform, "Hold a festival",
                    $"Costs {World.FestivalGold} gold and {World.FestivalFood} food. Lifts the mood by {World.FestivalShift:0} at once and by {World.FestivalMood:0} more for {World.FestivalDays} days. It can be held once every {World.FestivalCooldown} days.",
                    can.Ok ? "Click to hold one." : can.Reason);
            });
        }

        /// <summary>The tooltip: the mood, where it is heading, and the biggest things moving it.</summary>
        string MoodTip(World w)
        {
            var sb = new StringBuilder();
            double target = w.HappinessTarget();
            sb.Append($"{w.MoodName}. Heading toward {target:0}. ");
            sb.Append(w.Restless ? $"Below {HappinessTuning.Unrest:0} the workers may rise ({w.UprisingRisk() * 100:0.0}% a day now)." : $"Below {HappinessTuning.Unrest:0} the workers may rise.");
            foreach (var e in w.HappinessLedger().Take(7)) sb.Append($"\n{(e.Delta > 0 ? "+" : "")}{e.Delta:0}  {e.Text}");
            foreach (var (day, delta, text) in w.HappinessLog.Reverse().Take(2)) sb.Append($"\n{(delta > 0 ? "+" : "")}{delta:0} just now  {text}");
            return sb.ToString();
        }

        void UpdateMood(World world)
        {
            var warning = world.Happiness < 15 ? "Revolt near" : world.Restless ? "Restless" : "";
            SetNeedMeter(_moodMeter, world.Happiness, 100, MoodBar, warning, $"{world.Happiness:0}");
            var can = world.CanHoldFestival();
            _festivalButton.GetComponent<Button>().interactable = can.Ok;
            _festivalLabel.color = can.Ok ? Ink : new Color(Dim.r, Dim.g, Dim.b, 0.5f);
            _festivalLabel.text = world.FestivalReadyIn > 0 ? $"Festival {world.FestivalReadyIn}d" : "Festival";
        }
    }
}
