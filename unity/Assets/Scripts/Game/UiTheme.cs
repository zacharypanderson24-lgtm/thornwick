// The one place the interface's colours, text sizes and spacing are defined. Every HUD file takes its look from here, so changing the
// palette (or adding a high-contrast variant) is one edit. Palette is Zach's reference: restrained medieval, warm dark greys and browns,
// one gold accent, three status colours.
using UnityEngine;

namespace CrownAndHearth.Game
{
    public static class UiTheme
    {
        public static Color Hex(string hex, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c;
        }

        // ----- palette -----
        public static readonly Color Background = Hex("#211F1B");      // primary background
        public static readonly Color PanelSecondary = Hex("#332D24");  // secondary panel, buttons at rest
        public static readonly Color Selected = Hex("#494033");        // selected panel, hover
        public static readonly Color Gold = Hex("#B89B60");            // the accent: selected tab, key marks, headings
        public static readonly Color Text = Hex("#E6DCC5");            // primary text
        public static readonly Color TextDim = Hex("#A79C87");         // secondary text
        public static readonly Color Positive = Hex("#829961");
        public static readonly Color Warning = Hex("#D2A45F");
        public static readonly Color Critical = Hex("#B96554");
        /// <summary>Hairline round panels and cards: the primary background lifted a little, never gold.</summary>
        public static readonly Color Border = Hex("#4A4236");

        // derived looks
        public static Color WindowFill => new Color(Background.r, Background.g, Background.b, 0.95f);
        public static Color OverlayFill => new Color(Background.r, Background.g, Background.b, 0.88f);
        public static Color ButtonRest => PanelSecondary;
        public static Color ButtonHover => Selected;
        /// <summary>A chosen button or tab: the gold accent mixed into the background so the light label stays readable on it.</summary>
        public static Color ButtonOn => Color.Lerp(Background, Gold, 0.45f);
        public static Color ButtonDisabled => new Color(PanelSecondary.r, PanelSecondary.g, PanelSecondary.b, 0.6f);
        public static Color TrackFill => new Color(0f, 0f, 0f, 0.45f);

        public static Color Status(Tone t) => t switch { Tone.Good => Positive, Tone.Warn => Warning, Tone.Bad => Critical, _ => TextDim };

        /// <summary>Status meaning carried by words and shapes as well as colour, so it reads for colour-blind players.</summary>
        public enum Tone { Plain, Good, Warn, Bad }

        // ----- text sizes (reference pixels of the 1600 x 900 canvas) -----
        public const int TitleSize = 22;      // window titles
        public const int HeadingSize = 17;    // section headings, tooltip titles
        public const int BodySize = 15;       // standard text
        public const int StatSize = 17;       // numbers that matter
        public const int SmallSize = 13;      // secondary descriptions, hotkeys, footnotes
        public const int TipSize = 14;        // tooltip body

        // ----- spacing (same units) -----
        public const int Pad = 8;
        public const int Gap = 6;
        public const int Row = 30;
        public const int ButtonH = 30;

        // ----- contrast (WCAG relative luminance) -----
        static float Lin(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        public static float Luminance(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }
    }
}
