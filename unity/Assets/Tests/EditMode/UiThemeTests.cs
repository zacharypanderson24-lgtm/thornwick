using CrownAndHearth.Game;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    public class UiThemeTests
    {
        [Test] public void PaletteMatchesTheReferenceHexValues()
        {
            Assert.AreEqual(UiTheme.Hex("#211F1B"), UiTheme.Background);
            Assert.AreEqual(UiTheme.Hex("#B89B60"), UiTheme.Gold);
            Assert.AreEqual(UiTheme.Hex("#E6DCC5"), UiTheme.Text);
            Assert.AreEqual(UiTheme.Hex("#829961"), UiTheme.Positive);
            Assert.AreEqual(UiTheme.Hex("#D2A45F"), UiTheme.Warning);
            Assert.AreEqual(UiTheme.Hex("#B96554"), UiTheme.Critical);
        }

        [Test] public void TextIsReadableOnEveryFill()
        {
            foreach (var fill in new[] { UiTheme.Background, UiTheme.PanelSecondary, UiTheme.Selected, UiTheme.ButtonOn })
                Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.Text, fill), 4.5f, "primary text on " + ColorUtility.ToHtmlStringRGB(fill));
            foreach (var fill in new[] { UiTheme.Background, UiTheme.PanelSecondary })
                Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.TextDim, fill), 4.5f, "secondary text on " + ColorUtility.ToHtmlStringRGB(fill));
        }

        [Test] public void StatusColoursAreReadableOnPanels()
        {
            foreach (var c in new[] { UiTheme.Positive, UiTheme.Warning, UiTheme.Critical })
                Assert.GreaterOrEqual(UiTheme.Contrast(c, UiTheme.Background), 3f);
        }

        [Test] public void TextSizesStepDownInOrder()
        {
            Assert.Greater(UiTheme.TitleSize, UiTheme.HeadingSize);
            Assert.Greater(UiTheme.HeadingSize, UiTheme.BodySize - 1);
            Assert.Greater(UiTheme.BodySize, UiTheme.SmallSize);
        }

        [Test] public void UltrawideScreensScaleByHeightOthersByTheMean()
        {
            Assert.AreEqual(0.5f, Hud.MatchFor(1920, 1080));
            Assert.AreEqual(0.5f, Hud.MatchFor(1366, 768));
            Assert.AreEqual(0.5f, Hud.MatchFor(1280, 1024));
            Assert.AreEqual(1f, Hud.MatchFor(3440, 1440));
            Assert.AreEqual(1f, Hud.MatchFor(2560, 1080));
        }
    }
}
