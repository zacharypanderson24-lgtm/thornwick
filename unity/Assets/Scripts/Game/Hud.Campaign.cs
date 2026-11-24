using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    /// <summary>The campaign goal chip (top right, under the speed buttons), the Town Hall's health while it is hurt, and the defeat screen.</summary>
    public partial class Hud
    {
        public event Action RestartPicked;

        Text _goalText, _hallText, _defeatTitle, _defeatBody;
        RectTransform _goalCard, _hallCard, _defeatPanel;
        Image _goalFill;
        int _goalsSeen = -1;

        void BuildCampaign(RectTransform root)
        {
            // one line: the current goal and how far along it is; hover for what to do
            _goalCard = MakePanel(root, "Goal", Vector2.one, Vector2.one, new Vector2(-440, -152), new Vector2(-8, -116));
            _goalText = MakeText(_goalCard, "GoalText", 15, TextAnchor.MiddleLeft);
            Stretch(_goalText.rectTransform, new Vector2(10, 8), new Vector2(-10, -2));
            _goalText.horizontalOverflow = HorizontalWrapMode.Overflow; _goalText.verticalOverflow = VerticalWrapMode.Truncate;
            var track = MakePanel(_goalCard, "Track", Vector2.zero, Vector2.right, new Vector2(10, 4), new Vector2(-10, 8));
            track.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f); track.GetComponent<Image>().raycastTarget = false;
            var fill = MakePanel(track, "Fill", Vector2.zero, Vector2.up, Vector2.zero, Vector2.zero);
            _goalFill = fill.GetComponent<Image>(); _goalFill.color = Pulse; _goalFill.raycastTarget = false;
            AddHover(_goalCard.gameObject, on =>
            {
                if (_world == null) return;
                var g = _world.CurrentGoal;
                ShowTip(on, _goalCard, g != null ? g.Title : "Campaign won",
                    g != null ? g.Hint + $"\nReward: {g.Reward} gold. Goals done: {_world.MilestonesDone} of {World.Milestones.Length}." : "Every goal is done. Keep building as long as you like.", "");
            });

            // the Town Hall's health, only while it is hurt
            _hallCard = MakePanel(root, "HallHealth", Vector2.one, Vector2.one, new Vector2(-440, -192), new Vector2(-8, -158));
            _hallText = MakeText(_hallCard, "HallText", 15, TextAnchor.MiddleLeft);
            Stretch(_hallText.rectTransform, new Vector2(10, 0), new Vector2(-10, 0));
            _hallText.color = BadInk;
            _hallCard.gameObject.SetActive(false);

            // defeat: covers the game, the clock is stopped
            _defeatPanel = MakePanel(root, "Defeat", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _defeatPanel.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.88f);
            var box = MakePanel(_defeatPanel, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -170), new Vector2(300, 170));
            _defeatTitle = MakeText(box, "Title", 30, TextAnchor.UpperCenter);
            _defeatTitle.fontStyle = FontStyle.Bold; _defeatTitle.color = BadInk;
            Stretch(_defeatTitle.rectTransform, new Vector2(20, 220), new Vector2(-20, -20));
            _defeatBody = MakeText(box, "Body", 18, TextAnchor.UpperCenter);
            Stretch(_defeatBody.rectTransform, new Vector2(24, 80), new Vector2(-24, -80));
            MakeButton(box, "Load saved game", new Vector2(0.5f, 0), new Vector2(-110, 40), new Vector2(200, 40), () => LoadPicked?.Invoke());
            MakeButton(box, "Start over", new Vector2(0.5f, 0), new Vector2(110, 40), new Vector2(200, 40), () => RestartPicked?.Invoke());
            _defeatPanel.gameObject.SetActive(false);
        }

        void UpdateCampaign(World world)
        {
            bool campaign = world.CampaignOn && !world.Defeated;
            if (_goalCard.gameObject.activeSelf != campaign) _goalCard.gameObject.SetActive(campaign);
            if (campaign)
            {
                var g = world.CurrentGoal;
                if (g == null) { _goalText.text = "<b>Campaign won</b>: a regional power"; _goalFill.rectTransform.anchorMax = Vector2.one; }
                else
                {
                    var (have, need) = world.GoalProgress();
                    _goalText.text = $"<b>Goal {world.MilestonesDone + 1}/{World.Milestones.Length}</b>  {g.Title}  {have}/{need}";
                    _goalFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(have / (float)Math.Max(1, need)), 1);
                }
            }

            bool hurt = !world.Defeated && world.HallHp < World.HallMaxHp - 0.5;
            if (_hallCard.gameObject.activeSelf != hurt) _hallCard.gameObject.SetActive(hurt);
            if (hurt)
            {
                int hiding = world.Sheltered;
                _hallText.text = $"<b>Town Hall</b> {Mathf.CeilToInt((float)world.HallHp)}/{World.HallMaxHp:0}" + (hiding > 0 ? $"   {hiding} sheltering inside" : "");
            }

            if (_defeatPanel.gameObject.activeSelf != world.Defeated)
            {
                _defeatPanel.gameObject.SetActive(world.Defeated);
                if (world.Defeated)
                {
                    _defeatTitle.text = "Your settlement has fallen";
                    _defeatBody.text = $"{world.DefeatText}\n\nIt stood for {world.DefeatDay - 1} days.\nGoals reached: {world.MilestonesDone} of {World.Milestones.Length}.";
                    _defeatPanel.SetAsLastSibling();
                }
            }
        }
    }
}
