// The player's interface settings: how big the interface is and whether the game pauses while the System menu is open. Pure C# (no UnityEngine);
// the Hud keeps one and stores its text form in PlayerPrefs.
using System;
using System.Globalization;

namespace CrownAndHearth.Game.UI
{
    public sealed class UiSettings
    {
        /// <summary>The interface sizes offered. The layout is built for a 1600 x 900 canvas, so anything bigger than 100 percent overflows at every screen size (it buried the toolbar);
        /// a bigger interface needs a responsive layout first. Smaller gives more room for the map.</summary>
        public static readonly float[] Scales = { 0.85f, 1f };

        public int ScaleIndex = 1;
        public bool PauseOnMenu = true;

        public float Scale => Scales[Math.Clamp(ScaleIndex, 0, Scales.Length - 1)];
        public int Percent => (int)Math.Round(Scale * 100);

        public void CycleScale() => ScaleIndex = (Math.Clamp(ScaleIndex, 0, Scales.Length - 1) + 1) % Scales.Length;

        public string Serialize() => $"scale={ScaleIndex};pause={(PauseOnMenu ? 1 : 0)}";

        /// <summary>Settings from their text form; anything missing or broken keeps its default.</summary>
        public static UiSettings Parse(string text)
        {
            var s = new UiSettings();
            if (string.IsNullOrWhiteSpace(text)) return s;
            foreach (var part in text.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "scale" && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) s.ScaleIndex = Math.Clamp(i, 0, Scales.Length - 1);
                else if (kv[0] == "pause" && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)) s.PauseOnMenu = p != 0;
            }
            return s;
        }
    }
}
