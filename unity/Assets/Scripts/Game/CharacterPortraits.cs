// The bridge between a Sim character (ids, seed, age tendencies) and the portrait system. Sim knows nothing of images; this maps a Character to a
// PortraitAppearance for a given calendar day and hands out cached textures.
//  - The permanent definition (face, skin, hair, original hair colour, original outfit) is made once from the seed, stored in Look.Definition and saved:
//    later changes to the portrait library never alter an existing person.
//  - Occupation / outfit changes (Look.Archetype, Look.Outfit) change only clothing and headwear, never the face.
//  - Age comes from the calendar (Character.AgeYears) and is applied by PortraitAging: overlay stage, graying, receding hairline, baldness.
using CrownAndHearth.Portraits;
using CrownAndHearth.Sim;
using UnityEngine;

namespace CrownAndHearth.Game
{
    public static class CharacterPortraits
    {
        // The saved definition; created and stored on first use. Always the person's young self (age "young", original hair).
        public static PortraitAppearance Definition(Character c, PortraitLibrary lib)
        {
            if (!string.IsNullOrEmpty(c.Look.Definition))
            {
                var saved = PortraitAppearance.FromJson(c.Look.Definition);
                if (saved != null) return saved;
            }
            var def = PortraitGenerator.Generate(lib, c.Look.Seed, c.Look.Climate, c.Look.Archetype);
            def.age = "young";
            if (!string.IsNullOrEmpty(c.Look.Outfit)) Dress(lib, def, c.Look.Outfit, c.Look.Seed);
            c.Look.Definition = def.ToJson();
            return def;
        }

        // What this person looks like on `day`.
        public static PortraitAppearance Current(Character c, int day, PortraitLibrary lib)
        {
            var def = Definition(c, lib);
            var worn = def.Clone();
            if (!string.IsNullOrEmpty(c.Look.Outfit)) { if (c.Look.Outfit != def.clothing) Dress(lib, worn, c.Look.Outfit, c.Look.Seed); }
            else if (!string.IsNullOrEmpty(c.Look.Archetype) && c.Look.Archetype != def.archetype) DressForArchetype(lib, worn, c.Look.Archetype, c.Look.Seed);
            return PortraitAging.Apply(lib, worn, c.AgeYears(day), c.Look.GreyAge, c.Look.RecedeAge, c.Look.BaldAge);
        }

        public static Texture2D Texture(Character c, int day, PortraitLibrary lib = null, PortraitCache cache = null)
        {
            lib = lib != null ? lib : PortraitLibrary.Shared;
            if (lib == null) return null;   // no portrait library loaded: the screen shows no face rather than failing
            return (cache ?? PortraitCache.Shared).Get(lib, Current(c, day, lib));
        }

        // The texture for the screen. The portrait itself is 48 x 48; shown at a size that is not a whole multiple (the interface scale is free, and a
        // list thumbnail is smaller than 48) point sampling drops or doubles pixels unevenly and the face looks broken. So the screen gets a 4x
        // nearest-neighbour copy (every source pixel stays a hard square) sampled smoothly with mipmaps: crisp at any size, no new detail invented.
        const int Up = 4;
        static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> _display = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();
        public static Texture2D Display(Character c, int day)
        {
            var src = Texture(c, day); if (src == null) return null;
            if (_display.TryGetValue(src, out var have) && have != null) return have;
            if (_display.Count > 300) { foreach (var t in _display.Values) if (t != null) Object.Destroy(t); _display.Clear(); }   // sources the LRU cache dropped
            var d = Upscale(src); _display[src] = d; return d;
        }

        public static Texture2D Upscale(Texture2D src)
        {
            int w = src.width, h = src.height; var px = src.GetPixels32(); var outPx = new Color32[w * Up * h * Up];
            for (int y = 0; y < h * Up; y++) for (int x = 0; x < w * Up; x++) outPx[y * w * Up + x] = px[(y / Up) * w + x / Up];
            var t = new Texture2D(w * Up, h * Up, TextureFormat.RGBA32, true, false) { name = "portrait-display", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(outPx); t.Apply(true); return t;
        }

        // Put on an outfit; keep the current hat if it goes with it, otherwise pick one that does (deterministic from the seed).
        static void Dress(PortraitLibrary lib, PortraitAppearance a, string outfit, int seed)
        {
            var cl = lib.Find(PortraitLayer.Clothing, outfit); if (cl == null) return;
            a.clothing = outfit; a.clothing_color = "original";
            if (!cl.compatibleHeadwear.Contains(a.headwear) && cl.compatibleHeadwear.Count > 0)
            {
                var rng = new PortraitRng(seed * 31L + 7); a.headwear = rng.Pick(cl.compatibleHeadwear); a.headwear_color = "original";
            }
        }

        static void DressForArchetype(PortraitLibrary lib, PortraitAppearance a, string archetype, int seed)
        {
            var arch = lib.FindArchetype(archetype); if (arch == null || arch.outfits.Count == 0) return;
            var rng = new PortraitRng(seed * 17L + 3);
            Dress(lib, a, rng.Pick(arch.outfits), seed);
        }
    }
}
