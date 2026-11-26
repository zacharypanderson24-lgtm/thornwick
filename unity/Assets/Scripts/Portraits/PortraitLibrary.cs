// The portrait registry: every layer image, palette, archetype, regional wardrobe pool and compatibility rule lives in this one ScriptableObject
// (Resources/Portraits/PortraitLibrary.asset). Nothing about hairstyles, outfits, hats or colours is written in code: adding or replacing one is a
// data edit (Portrait Asset Editor, or the Inspector). Ids are the stable names from the Python generator ("short", "civic_fur"); they only need to
// be unique inside one layer, and a saved appearance stores ids, never image references, so replacing artwork never breaks a character.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    // The image layers a portrait is made of, in no particular drawing order (the renderer owns the order).
    public enum PortraitLayer { Face, Age, Clothing, Hair, Brows, FacialHair, Eyes, Headwear }

    public enum PaletteKind { Skin, Hair, Material }

    [Serializable]
    public class PortraitAsset
    {
        public string id;                          // stable, unique within the layer; the Python generator's file name
        public string displayName;
        public PortraitLayer layer;
        public Texture2D texture;                  // native 48x48, RGBA, Point, no compression
        // Recolouring (clothing and headwear only): which material palette dyes it and which exact source colours the dye masks cover.
        public string materialId;                  // key of a Material palette set ("textile", "leather", "fur", "metal", "straw", "gem", "linen"), empty = never dyed
        public List<string> maskColors = new List<string>();   // RRGGBB hex, the colours of the original artwork that the dye replaces (empty = no dye)
        // Headwear only: per image column, the lowest row (inclusive) of hair that the hat hides; -1 = hair in that column is untouched. Empty = hides nothing.
        public int[] hairOcclusion = new int[0];
        // Clothing only: the headwear ids that may be worn with this outfit.
        public List<string> compatibleHeadwear = new List<string>();
        public float weight = 1f;                  // relative chance in random generation
        public List<string> tags = new List<string>();         // free labels: climate, culture, era...
    }

    [Serializable] public class PaletteEntry { public string id; public List<string> colors = new List<string>(); }   // RRGGBB hex, same length as the set's sourceColors (one colour for material palettes)

    [Serializable]
    public class PaletteSet
    {
        public string id;
        public PaletteKind kind;
        public List<string> sourceColors = new List<string>();   // skin/hair: the colours of the base artwork that get replaced
        public List<PaletteEntry> entries = new List<PaletteEntry>();
    }

    [Serializable]
    public class PortraitArchetype
    {
        public string id; public string displayName;
        public List<string> outfits = new List<string>();
        public List<string> headwear = new List<string>();      // hats this archetype may wear (besides none)
        public float weight = 1f;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class PortraitRegion        // a regional wardrobe pool ("temperate", "cold", "hot"...)
    {
        public string id; public string displayName;
        public List<string> outfits = new List<string>();
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class AgeStageDef
    {
        public string id; public string displayName;
        public int minYears;                       // the first year of age this stage covers (the last stage is open-ended)
        public string assetId;                     // the Age layer image
        public float randomWeight = 1f;            // chance in random generation
    }

    [Serializable]
    public class PortraitStyle
    {
        public string background = "4c516e";
        public string frameOuter = "30242a";
        public string frameInner = "c39a55";
        public bool drawFrame = true;
        public string baseSkinId = "peach";        // the id inside the skin set whose colours are the artwork's own
        public string baseHairId = "brown";        // likewise for the hair set
        public List<string> browChoices = new List<string> { "straight", "short" };
    }

    [CreateAssetMenu(menuName = "Thornwick/Portrait Library", fileName = "PortraitLibrary")]
    public class PortraitLibrary : ScriptableObject
    {
        public const string ResourcePath = "Portraits/PortraitLibrary";
        public const int Size = 48;

        public int version = 19;
        public PortraitStyle style = new PortraitStyle();
        public List<PortraitAsset> assets = new List<PortraitAsset>();
        public List<PaletteSet> palettes = new List<PaletteSet>();
        public string skinSetId = "skin";
        public string hairSetId = "hair";
        public List<PortraitArchetype> archetypes = new List<PortraitArchetype>();
        public List<PortraitRegion> regions = new List<PortraitRegion>();
        public List<AgeStageDef> ageStages = new List<AgeStageDef>();

        Dictionary<(PortraitLayer, string), PortraitAsset> _assetIndex;
        Dictionary<string, PaletteSet> _paletteIndex;

        static PortraitLibrary _shared;
        public static PortraitLibrary Shared => _shared != null ? _shared : (_shared = Resources.Load<PortraitLibrary>(ResourcePath));

        void OnEnable() { InvalidateIndex(); }
        void OnValidate() { InvalidateIndex(); }
        public void InvalidateIndex() { _assetIndex = null; _paletteIndex = null; }

        void BuildIndex()
        {
            _assetIndex = new Dictionary<(PortraitLayer, string), PortraitAsset>();
            foreach (var a in assets) if (a != null && !string.IsNullOrEmpty(a.id)) _assetIndex[(a.layer, a.id)] = a;
            _paletteIndex = new Dictionary<string, PaletteSet>();
            foreach (var p in palettes) if (p != null && !string.IsNullOrEmpty(p.id)) _paletteIndex[p.id] = p;
        }

        public PortraitAsset Find(PortraitLayer layer, string id)
        {
            if (_assetIndex == null) BuildIndex();
            return id != null && _assetIndex.TryGetValue((layer, id), out var a) ? a : null;
        }

        public PaletteSet FindPalette(string id)
        {
            if (_paletteIndex == null) BuildIndex();
            return id != null && _paletteIndex.TryGetValue(id, out var p) ? p : null;
        }

        public PaletteEntry FindEntry(string setId, string entryId)
        {
            var set = FindPalette(setId);
            if (set == null) return null;
            foreach (var e in set.entries) if (e.id == entryId) return e;
            return null;
        }

        public List<PortraitAsset> AssetsOf(PortraitLayer layer)
        {
            var list = new List<PortraitAsset>();
            foreach (var a in assets) if (a != null && a.layer == layer) list.Add(a);
            return list;
        }

        public PortraitArchetype FindArchetype(string id) { foreach (var a in archetypes) if (a.id == id) return a; return null; }
        public PortraitRegion FindRegion(string id) { foreach (var r in regions) if (r.id == id) return r; return null; }
        public AgeStageDef FindAgeStage(string id) { foreach (var s in ageStages) if (s.id == id) return s; return null; }
    }
}
