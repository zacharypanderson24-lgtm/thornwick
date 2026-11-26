// Deterministic random appearances from the library's data: archetypes, regional wardrobe pools, outfit/hat rules, palettes and weights.
// The same (library, seed, region, archetype) always gives the same recipe on every machine (own small generator, not System.Random).
using System.Collections.Generic;

namespace CrownAndHearth.Portraits
{
    public struct PortraitRng
    {
        ulong _s;
        public PortraitRng(long seed) { _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; }
        public ulong Next()   // splitmix64
        {
            ulong z = (_s += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        public int Int(int n) => n <= 1 ? 0 : (int)(Next() % (ulong)n);
        public double Double() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        public T Pick<T>(IList<T> list) => list[Int(list.Count)];
        public int Weighted(IList<float> weights)
        {
            double total = 0; foreach (float w in weights) total += w > 0 ? w : 0;
            if (total <= 0) return Int(weights.Count);
            double r = Double() * total;
            for (int i = 0; i < weights.Count; i++) { r -= weights[i] > 0 ? weights[i] : 0; if (r < 0) return i; }
            return weights.Count - 1;
        }
    }

    public static class PortraitGenerator
    {
        // Colour variants an outfit or hat can be dyed to: "original" plus every entry of its material palette (only "original" when it has no dye mask).
        public static List<string> DyeChoices(PortraitLibrary lib, PortraitAsset a)
        {
            var list = new List<string> { "original" };
            if (a == null || a.maskColors.Count == 0) return list;
            var set = lib.FindPalette(a.materialId);
            if (set != null) foreach (var e in set.entries) list.Add(e.id);
            return list;
        }

        // archetypeId empty = take the outfit from the regional pool (regionId), like the reference generator.
        public static PortraitAppearance Generate(PortraitLibrary lib, long seed, string regionId = "temperate", string archetypeId = null)
        {
            var rng = new PortraitRng(seed);
            var a = new PortraitAppearance { seed = (int)seed, climate = regionId, archetype = archetypeId ?? "", face = "standard", eyes = "standard" };

            string outfit;
            var arch = string.IsNullOrEmpty(archetypeId) ? null : lib.FindArchetype(archetypeId);
            if (arch != null && arch.outfits.Count > 0) outfit = rng.Pick(arch.outfits);
            else
            {
                var region = lib.FindRegion(regionId) ?? (lib.regions.Count > 0 ? lib.regions[0] : null);
                outfit = region != null && region.outfits.Count > 0 ? rng.Pick(region.outfits) : rng.Pick(lib.AssetsOf(PortraitLayer.Clothing)).id;
            }
            var clothing = lib.Find(PortraitLayer.Clothing, outfit);
            string hat = clothing != null && clothing.compatibleHeadwear.Count > 0 ? rng.Pick(clothing.compatibleHeadwear) : "none";
            a.clothing = outfit; a.headwear = hat;
            a.hair = PickWeighted(lib, PortraitLayer.Hair, ref rng, a.hair);
            a.beard = PickWeighted(lib, PortraitLayer.FacialHair, ref rng, a.beard);
            var skin = lib.FindPalette(lib.skinSetId); var hair = lib.FindPalette(lib.hairSetId);
            if (skin != null && skin.entries.Count > 0) a.skin = rng.Pick(skin.entries).id;
            if (hair != null && hair.entries.Count > 0) a.hair_color = rng.Pick(hair.entries).id;
            if (lib.style.browChoices.Count > 0) a.brows = rng.Pick(lib.style.browChoices);
            a.clothing_color = rng.Pick(DyeChoices(lib, clothing));
            a.headwear_color = rng.Pick(DyeChoices(lib, lib.Find(PortraitLayer.Headwear, hat)));
            if (lib.ageStages.Count > 0)
            {
                var w = new List<float>(); foreach (var s in lib.ageStages) w.Add(s.randomWeight);
                a.age = lib.ageStages[rng.Weighted(w)].id;
            }
            return a;
        }

        static string PickWeighted(PortraitLibrary lib, PortraitLayer layer, ref PortraitRng rng, string fallback)
        {
            var list = lib.AssetsOf(layer);
            if (list.Count == 0) return fallback;
            var w = new List<float>(); foreach (var x in list) w.Add(x.weight);
            return list[rng.Weighted(w)].id;
        }
    }
}
