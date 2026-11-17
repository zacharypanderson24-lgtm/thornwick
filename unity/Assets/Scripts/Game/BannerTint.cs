using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Game
{
    internal static class BannerTint
    {
        // Match the browser's connected red-pixel mask. Isolated red details are kept.
        public static void Apply(Color32[] pixels, int width, Color32 target)
        {
            int height = pixels.Length / width;
            var candidates = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 200) continue;
                Color.RGBToHSV(pixels[i], out float hue, out float saturation, out float value);
                candidates[i] = (hue * 360f <= 14f || hue * 360f >= 345f) && saturation >= 0.55f && value >= 0.3f;
            }

            var component = new List<int>();
            for (int first = 0; first < pixels.Length; first++)
            {
                if (!candidates[first]) continue;
                component.Clear();
                component.Add(first);
                candidates[first] = false;
                for (int i = 0; i < component.Count; i++)
                {
                    int x = component[i] % width, y = component[i] / width;
                    for (int ny = Mathf.Max(0, y - 1); ny <= Mathf.Min(height - 1, y + 1); ny++)
                        for (int nx = Mathf.Max(0, x - 1); nx <= Mathf.Min(width - 1, x + 1); nx++)
                        {
                            int next = ny * width + nx;
                            if (!candidates[next]) continue;
                            candidates[next] = false;
                            component.Add(next);
                        }
                }
                if (component.Count < 6) continue;

                foreach (int index in component)
                {
                    var source = pixels[index];
                    float brightness = Mathf.Max(source.r, Mathf.Max(source.g, source.b)) / (255f * 0.85f);
                    pixels[index] = new Color32(Channel(target.r, brightness), Channel(target.g, brightness),
                        Channel(target.b, brightness), source.a);
                }
            }
        }

        static byte Channel(byte color, float brightness)
        {
            float value = brightness <= 1
                ? color * brightness
                : color + (255 - color) * (brightness - 1) * 0.8f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }
    }
}
