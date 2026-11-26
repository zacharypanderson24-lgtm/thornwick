// Contact sheets: a grid of portraits (nearest-neighbour enlarged, no smoothing) as one image, for visual checks and PNG export.
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    public static class PortraitSheet
    {
        // Returns a Texture2D (caller destroys it). `scale` enlarges each 48x48 portrait by whole pixels; `gap` is the space between cells.
        public static Texture2D Build(PortraitLibrary lib, IList<PortraitAppearance> list, int columns, int scale = 4, int gap = 2)
        {
            int rows = Mathf.Max(1, (list.Count + columns - 1) / columns), cell = PortraitLibrary.Size * scale;
            int w = columns * cell + (columns + 1) * gap, h = rows * cell + (rows + 1) * gap;
            var px = new Color32[w * h];
            var bg = new Color32(24, 20, 22, 255); for (int i = 0; i < px.Length; i++) px[i] = bg;
            for (int n = 0; n < list.Count; n++)
            {
                var img = PortraitRenderer.Render(lib, list[n]);
                int ox = gap + (n % columns) * (cell + gap), oyTop = gap + (n / columns) * (cell + gap);
                for (int y = 0; y < cell; y++)
                {
                    int row = h - 1 - (oyTop + y);                       // texture rows run bottom-up
                    for (int x = 0; x < cell; x++) px[row * w + ox + x] = img[(y / scale) * PortraitLibrary.Size + x / scale];
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixels32(px); t.Apply(false, false);
            return t;
        }
    }
}
