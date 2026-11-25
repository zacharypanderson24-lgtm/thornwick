// The scrolling list of company cards in the Military Command window. Kept in one place so a test can check that cards really get
// their own height (a layout group that does not control child height gives every card zero height, so cards vanish or pile up).
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public static class MilitaryListLayout
    {
        public const int CardHeight = 92, Spacing = 6, Padding = 4;

        /// <summary>Set up the list content: cards stack top to bottom, each CardHeight tall and as wide as the list, and the content grows
        /// with the number of cards so the scroll view can scroll to all of them.</summary>
        public static void Configure(RectTransform content)
        {
            var layout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Spacing; layout.padding = new RectOffset(Padding, Padding, Padding, Padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;   // height must be controlled, or the cards collapse to nothing
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Give a card its fixed height inside the list.</summary>
        public static void SizeCard(GameObject card)
        {
            var le = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>();
            le.preferredHeight = CardHeight; le.minHeight = CardHeight;
        }
    }
}
