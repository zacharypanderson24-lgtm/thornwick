// The "Getting started" card under the goal chip: the next useful step for a new town, why it matters, and a button that opens the Build
// catalogue on the right category. It can be hidden, and stays hidden (saved). The steps come from the real state of the town (UI/FirstYearGuide.cs).
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        const string GuideHiddenKey = "thornwick.guide.hidden";
        RectTransform _guideCard;
        Text _guideTitle, _guideBody;
        GuideStep _guideStep;
        bool _guideHidden;

        void BuildGuide(RectTransform root)
        {
            _guideHidden = PlayerPrefs.GetInt(GuideHiddenKey, 0) == 1;
            _guideCard = MakePanel(root, "Guide", Vector2.one, Vector2.one, new Vector2(-440, -236), new Vector2(-8, -158));
            _guideTitle = MakeText(_guideCard, "Title", UiTheme.BodySize, TextAnchor.UpperLeft); _guideTitle.fontStyle = FontStyle.Bold; _guideTitle.color = UiTheme.Gold;
            Stretch(_guideTitle.rectTransform, new Vector2(10, 52), new Vector2(-70, -5));
            _guideBody = MakeText(_guideCard, "Body", UiTheme.SmallSize, TextAnchor.UpperLeft); _guideBody.supportRichText = true;
            Stretch(_guideBody.rectTransform, new Vector2(10, 4), new Vector2(-10, -26));
            MakeButton(_guideCard, "Show me", new Vector2(1, 1), new Vector2(-72, -14), new Vector2(86, 22), () => { if (_guideStep != null) { Catalogue.LastCategory = _guideStep.Show; ShowCatalogue(false); ShowCatalogue(true); } });
            MakeButton(_guideCard, "Hide", new Vector2(1, 1), new Vector2(-20, -14), new Vector2(36, 22), () => { _guideHidden = true; PlayerPrefs.SetInt(GuideHiddenKey, 1); PlayerPrefs.Save(); _guideCard.gameObject.SetActive(false); });
            _guideCard.gameObject.SetActive(false);
        }

        /// <summary>Bring the guide back after it was hidden (a switch in Settings would call this).</summary>
        public void ShowGuideAgain() { _guideHidden = false; PlayerPrefs.SetInt(GuideHiddenKey, 0); PlayerPrefs.Save(); }

        void UpdateGuide(World world)
        {
            _guideStep = world.Defeated || !world.CampaignOn ? null : FirstYearGuide.Next(world);
            bool show = _guideStep != null && !_guideHidden;
            if (_guideCard.gameObject.activeSelf != show) _guideCard.gameObject.SetActive(show);
            if (!show) return;
            _guideTitle.text = "Next: " + _guideStep.Title;
            _guideBody.text = _guideStep.Why + (_guideStep.Progress != null ? "  <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.TextDim) + ">" + _guideStep.Progress + "</color>" : "");
        }
    }
}
