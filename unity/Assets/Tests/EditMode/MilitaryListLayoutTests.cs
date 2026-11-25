// The company list must show every card, one under another, each with its own height (Game/MilitaryListLayout.cs).
using CrownAndHearth.Game;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class MilitaryListLayoutTests
    {
        GameObject _root;

        [TearDown] public void Clean() { if (_root != null) Object.DestroyImmediate(_root); }

        RectTransform Build(int cards, out RectTransform[] made)
        {
            _root = new GameObject("TestList", typeof(RectTransform));
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_root.transform, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(300, 400);
            MilitaryListLayout.Configure(content);
            made = new RectTransform[cards];
            for (int i = 0; i < cards; i++)
            {
                var card = new GameObject("Card" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                card.SetParent(content, false);
                card.anchorMin = Vector2.zero; card.anchorMax = Vector2.zero; card.offsetMin = Vector2.zero; card.offsetMax = Vector2.zero;   // as the Hud makes them
                MilitaryListLayout.SizeCard(card.gameObject);
                made[i] = card;
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            return content;
        }

        [Test]
        public void EveryCardGetsItsOwnHeightAndSitsBelowTheLast()
        {
            var content = Build(3, out var cards);
            for (int i = 0; i < cards.Length; i++)
                Assert.AreEqual(MilitaryListLayout.CardHeight, cards[i].rect.height, 0.01f, $"card {i} has its full height");
            Assert.Less(cards[1].anchoredPosition.y, cards[0].anchoredPosition.y, "the second card is below the first");
            Assert.Less(cards[2].anchoredPosition.y, cards[1].anchoredPosition.y, "the third card is below the second");
            float expected = 3 * MilitaryListLayout.CardHeight + 2 * MilitaryListLayout.Spacing + 2 * MilitaryListLayout.Padding;
            Assert.AreEqual(expected, content.rect.height, 0.5f, "the list grows to hold every card so the scroll view can reach them");
        }

        [Test]
        public void ACardIsAsWideAsTheListNotZero()
        {
            Build(2, out var cards);
            Assert.Greater(cards[0].rect.width, 100f);
            Assert.AreEqual(cards[0].rect.width, cards[1].rect.width, 0.01f);
        }
    }
}
