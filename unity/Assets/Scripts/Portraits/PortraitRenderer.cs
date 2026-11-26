// The native portrait renderer: composites the layers of an appearance into a 48x48 pixel buffer, with palette replacement and material dyes.
// Layer order (fixed, as in the reference generator): background, face + skin, ageing overlay, clothing, hair (cut under the hat), eyebrows,
// facial hair, eyes, headwear, frame. All content (which image, which colours, which hair a hat hides) comes from the PortraitLibrary.
// Pixel buffers are Color32[2304], row 0 = TOP row (the order the reference and the hat occlusion lines use). ToTexture flips to Unity's order.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    public static class PortraitRenderer
    {
        public const int Size = PortraitLibrary.Size;
        static readonly Dictionary<PortraitAsset, Color32[]> _layers = new Dictionary<PortraitAsset, Color32[]>();

        // Forget decoded layer images (call after artwork is edited or replaced).
        public static void ClearLayerCache() { _layers.Clear(); }

        // The pixels of one asset, top row first. Cached; callers must not modify the result.
        public static Color32[] LayerPixels(PortraitAsset a)
        {
            if (a == null || a.texture == null) return null;
            if (_layers.TryGetValue(a, out var cached)) return cached;
            var src = a.texture.GetPixels32();       // Unity: bottom row first
            int w = a.texture.width, h = a.texture.height;
            var px = new Color32[Size * Size];
            for (int y = 0; y < Math.Min(h, Size); y++)
                for (int x = 0; x < Math.Min(w, Size); x++) px[y * Size + x] = src[(h - 1 - y) * w + x];
            _layers[a] = px;
            return px;
        }

        public static Color32[] Render(PortraitLibrary lib, PortraitAppearance a)
        {
            var dst = new Color32[Size * Size];
            Fill(dst, Hex(lib.style.background));
            var skin = lib.FindPalette(lib.skinSetId); var hair = lib.FindPalette(lib.hairSetId);
            var skinTo = Entry(skin, a.skin); var hairTo = Entry(hair, a.hair_color);

            Composite(dst, Recolor(LayerPixels(lib.Find(PortraitLayer.Face, a.face)), skin, skinTo));
            Composite(dst, Recolor(LayerPixels(lib.Find(PortraitLayer.Age, string.IsNullOrEmpty(a.age) ? "young" : a.age)), skin, skinTo));
            Composite(dst, Dye(lib, lib.Find(PortraitLayer.Clothing, a.clothing), a.clothing_color));
            var hat = lib.Find(PortraitLayer.Headwear, a.headwear);
            Composite(dst, OccludeHair(Recolor(LayerPixels(lib.Find(PortraitLayer.Hair, a.hair)), hair, hairTo), hat));
            Composite(dst, Recolor(LayerPixels(lib.Find(PortraitLayer.Brows, a.brows)), hair, hairTo));
            Composite(dst, Recolor(LayerPixels(lib.Find(PortraitLayer.FacialHair, a.beard)), hair, hairTo));
            Composite(dst, LayerPixels(lib.Find(PortraitLayer.Eyes, a.eyes)));
            Composite(dst, Dye(lib, hat, a.headwear_color));
            if (lib.style.drawFrame)
            {
                Rect(dst, 0, Hex(lib.style.frameOuter)); Rect(dst, 1, Hex(lib.style.frameInner));
            }
            return dst;
        }

        // One layer on its own over transparent (for layer previews and the editor): no skin/hair recolour unless the appearance asks for it.
        public static Color32[] RenderLayer(PortraitLibrary lib, PortraitAppearance a, PortraitLayer layer)
        {
            var skin = lib.FindPalette(lib.skinSetId); var hair = lib.FindPalette(lib.hairSetId);
            var skinTo = Entry(skin, a.skin); var hairTo = Entry(hair, a.hair_color);
            switch (layer)
            {
                case PortraitLayer.Face: return Recolor(LayerPixels(lib.Find(layer, a.face)), skin, skinTo);
                case PortraitLayer.Age: return Recolor(LayerPixels(lib.Find(layer, a.age)), skin, skinTo);
                case PortraitLayer.Clothing: return Dye(lib, lib.Find(layer, a.clothing), a.clothing_color);
                case PortraitLayer.Hair: return OccludeHair(Recolor(LayerPixels(lib.Find(layer, a.hair)), hair, hairTo), lib.Find(PortraitLayer.Headwear, a.headwear));
                case PortraitLayer.Brows: return Recolor(LayerPixels(lib.Find(layer, a.brows)), hair, hairTo);
                case PortraitLayer.FacialHair: return Recolor(LayerPixels(lib.Find(layer, a.beard)), hair, hairTo);
                case PortraitLayer.Eyes: return LayerPixels(lib.Find(layer, a.eyes));
                default: return Dye(lib, lib.Find(layer, a.headwear), a.headwear_color);
            }
        }

        static PaletteEntry Entry(PaletteSet set, string id)
        {
            if (set == null) return null;
            foreach (var e in set.entries) if (e.id == id) return e;
            return null;
        }

        // Replace every pixel whose RGB exactly equals a source colour (alpha is ignored for matching and kept), like the reference's recolor().
        // A missing palette or entry leaves the artwork untouched. Later duplicates of a source colour win.
        public static Color32[] Recolor(Color32[] src, PaletteSet set, PaletteEntry to)
        {
            if (src == null || set == null || to == null) return src;
            return Recolor(src, set.sourceColors, to.colors);
        }

        public static Color32[] Recolor(Color32[] src, IList<string> oldHex, IList<string> newHex)
        {
            if (src == null) return null;
            int n = Math.Min(oldHex.Count, newHex.Count);
            var from = new Color32[n]; var to = new Color32[n];
            for (int i = 0; i < n; i++) { from[i] = Hex(oldHex[i]); to[i] = Hex(newHex[i]); }
            var dst = new Color32[src.Length];
            for (int p = 0; p < src.Length; p++)
            {
                var c = src[p];
                for (int i = n - 1; i >= 0; i--)
                    if (from[i].r == c.r && from[i].g == c.g && from[i].b == c.b) { c.r = to[i].r; c.g = to[i].g; c.b = to[i].b; break; }
                dst[p] = c;
            }
            return dst;
        }

        // The material dye: one target colour re-lights the artwork's own mask colours (keeping their light/dark differences).
        public static Color32[] Dye(PortraitLibrary lib, PortraitAsset a, string variant)
        {
            var px = LayerPixels(a);
            if (px == null || string.IsNullOrEmpty(variant) || variant == "original" || a.maskColors.Count == 0) return px;
            var entry = lib.FindEntry(a.materialId, variant);
            if (entry == null || entry.colors.Count == 0) return px;
            var target = Hex(entry.colors[0]);
            Hls(target.r / 255.0, target.g / 255.0, target.b / 255.0, out double hue, out double light, out double sat);
            var l = new double[a.maskColors.Count];
            for (int i = 0; i < l.Length; i++) { var c = Hex(a.maskColors[i]); Hls(c.r / 255.0, c.g / 255.0, c.b / 255.0, out _, out l[i], out _); }
            double lo = double.MaxValue, hi = double.MinValue; foreach (double v in l) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            double center = (lo + hi) / 2;
            var mapped = new List<string>();
            foreach (double v in l)
            {
                double nl = Math.Max(0.12, Math.Min(0.91, light + (v - center) * 0.94));
                FromHls(hue, nl, sat, out double r, out double g, out double b);
                mapped.Add(((int)Math.Round(r * 255)).ToString("x2") + ((int)Math.Round(g * 255)).ToString("x2") + ((int)Math.Round(b * 255)).ToString("x2"));
            }
            return Recolor(px, a.maskColors, mapped);
        }

        // Hats hide hair: per image column, every hair pixel from the top down to the hat's row (inclusive) is removed; lower hair stays.
        public static Color32[] OccludeHair(Color32[] hair, PortraitAsset hat)
        {
            if (hair == null || hat == null || hat.hairOcclusion == null || hat.hairOcclusion.Length == 0) return hair;
            var o = (Color32[])hair.Clone();
            for (int x = 0; x < Math.Min(Size, hat.hairOcclusion.Length); x++)
            {
                int y1 = hat.hairOcclusion[x]; if (y1 < 0) continue;
                for (int y = 0; y <= Math.Min(Size - 1, y1); y++) if (o[y * Size + x].a != 0) o[y * Size + x] = new Color32(0, 0, 0, 0);
            }
            return o;
        }

        // Alpha-over onto an opaque buffer. The supplied artwork only uses alpha 0 and 255; partial alpha is blended with plain rounding.
        public static void Composite(Color32[] dst, Color32[] src)
        {
            if (src == null) return;
            for (int i = 0; i < dst.Length; i++)
            {
                byte a = src[i].a; if (a == 0) continue;
                if (a == 255) { dst[i] = new Color32(src[i].r, src[i].g, src[i].b, 255); continue; }
                int ia = 255 - a;
                dst[i] = new Color32((byte)((src[i].r * a + dst[i].r * ia + 127) / 255), (byte)((src[i].g * a + dst[i].g * ia + 127) / 255), (byte)((src[i].b * a + dst[i].b * ia + 127) / 255), 255);
            }
        }

        static void Fill(Color32[] d, Color32 c) { for (int i = 0; i < d.Length; i++) d[i] = c; }

        static void Rect(Color32[] d, int inset, Color32 c)
        {
            int lo = inset, hi = Size - 1 - inset;
            for (int x = lo; x <= hi; x++) { d[lo * Size + x] = c; d[hi * Size + x] = c; }
            for (int y = lo; y <= hi; y++) { d[y * Size + lo] = c; d[y * Size + hi] = c; }
        }

        public static Color32 Hex(string s)
        {
            s = s.TrimStart('#');
            return new Color32(Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16), 255);
        }

        // RGB <-> HLS exactly as Python's colorsys.
        static void Hls(double r, double g, double b, out double h, out double l, out double s)
        {
            double maxc = Math.Max(r, Math.Max(g, b)), minc = Math.Min(r, Math.Min(g, b)), sumc = maxc + minc, range = maxc - minc;
            l = sumc / 2.0;
            if (minc == maxc) { h = 0; s = 0; return; }
            s = l <= 0.5 ? range / sumc : range / (2.0 - maxc - minc);
            double rc = (maxc - r) / range, gc = (maxc - g) / range, bc = (maxc - b) / range;
            if (r == maxc) h = bc - gc; else if (g == maxc) h = 2.0 + rc - bc; else h = 4.0 + gc - rc;
            h = h / 6.0; h -= Math.Floor(h);
        }

        static void FromHls(double h, double l, double s, out double r, out double g, out double b)
        {
            if (s == 0) { r = g = b = l; return; }
            double m2 = l <= 0.5 ? l * (1.0 + s) : l + s - l * s, m1 = 2.0 * l - m2;
            r = Hv(m1, m2, h + 1.0 / 3.0); g = Hv(m1, m2, h); b = Hv(m1, m2, h - 1.0 / 3.0);
        }

        static double Hv(double m1, double m2, double hue)
        {
            hue -= Math.Floor(hue);
            if (hue < 1.0 / 6.0) return m1 + (m2 - m1) * hue * 6.0;
            if (hue < 0.5) return m2;
            if (hue < 2.0 / 3.0) return m1 + (m2 - m1) * (2.0 / 3.0 - hue) * 6.0;
            return m1;
        }

        // ---- textures ----

        public static Texture2D ToTexture(Color32[] topFirst, Texture2D reuse = null)
        {
            var flipped = new Color32[Size * Size];
            for (int y = 0; y < Size; y++) Array.Copy(topFirst, y * Size, flipped, (Size - 1 - y) * Size, Size);
            var t = reuse != null ? reuse : new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 0;
            t.SetPixels32(flipped); t.Apply(false, false);
            return t;
        }
    }
}
