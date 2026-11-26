// Imports the Python portrait package (assets/<layer>/<id>.png, palette_library.json, hair_occlusion.json, definitions.json) into the game:
// the PNGs are copied byte for byte into Resources/Portraits/<layer>/, and the registry (PortraitLibrary.asset) is created or refreshed with the
// palettes, archetypes, wardrobe pools and rules. The artwork is never altered. Menu: Tools > Thornwick > Portraits > Import Package Folder.
// The repo keeps a copy of the supplied package in unity/PortraitSource, which is the default source.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CrownAndHearth.Portraits.EditorTools
{
    public static class PortraitImporter
    {
        public const string ResourcesDir = "Assets/Resources/Portraits";
        public const string LibraryPath = ResourcesDir + "/PortraitLibrary.asset";
        public static string DefaultSourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PortraitSource"));

        // package folder name -> layer
        static readonly (string folder, PortraitLayer layer)[] Folders =
        {
            ("faces", PortraitLayer.Face), ("age", PortraitLayer.Age), ("clothing", PortraitLayer.Clothing), ("hair", PortraitLayer.Hair),
            ("brows", PortraitLayer.Brows), ("beards", PortraitLayer.FacialHair), ("eyes", PortraitLayer.Eyes), ("headwear", PortraitLayer.Headwear),
        };

        public static string FolderOf(PortraitLayer layer) => Folders.First(f => f.layer == layer).folder;
        public static string TexturePath(PortraitLayer layer, string id) => $"{ResourcesDir}/{FolderOf(layer)}/{id}.png";

        [MenuItem("Tools/Thornwick/Portraits/Import Package Folder")]
        public static void ImportFromMenu()
        {
            string dir = EditorUtility.OpenFolderPanel("Portrait package folder (contains assets/, definitions.json...)", DefaultSourceDir, "");
            if (string.IsNullOrEmpty(dir)) return;
            if (!EditorUtility.DisplayDialog("Import portraits", "This copies the package images over the game's portrait images with the same ids and rebuilds the registry from the package files. Edits made in the Portrait Asset Editor are replaced. Continue?", "Import", "Cancel")) return;
            Import(dir);
        }

        // Command line: -executeMethod CrownAndHearth.Portraits.EditorTools.PortraitImporter.ImportDefault
        public static void ImportDefault() { Import(DefaultSourceDir); if (Application.isBatchMode) EditorApplication.Exit(0); }

        public static PortraitLibrary Import(string sourceDir)
        {
            string assetsDir = Path.Combine(sourceDir, "assets");
            var defs = JObject.Parse(File.ReadAllText(Path.Combine(sourceDir, "definitions.json")));
            var pal = JObject.Parse(File.ReadAllText(Path.Combine(sourceDir, "palette_library.json")));
            var occ = JObject.Parse(File.ReadAllText(Path.Combine(sourceDir, "hair_occlusion.json")));

            // 1. copy the images exactly
            Directory.CreateDirectory(ResourcesDir);
            foreach (var (folder, layer) in Folders)
            {
                string dstDir = $"{ResourcesDir}/{folder}"; Directory.CreateDirectory(dstDir);
                foreach (string src in Directory.GetFiles(Path.Combine(assetsDir, folder), "*.png"))
                {
                    string dst = $"{dstDir}/{Path.GetFileName(src)}";
                    if (!File.Exists(dst) || !File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst))) File.Copy(src, dst, true);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. the registry
            var lib = AssetDatabase.LoadAssetAtPath<PortraitLibrary>(LibraryPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<PortraitLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            lib.version = 19;
            lib.style = new PortraitStyle();
            lib.assets.Clear(); lib.palettes.Clear(); lib.archetypes.Clear(); lib.regions.Clear(); lib.ageStages.Clear();

            var masks = (JObject)pal["masks"];
            var outfitHats = ((JObject)defs["outfit_hats"]).Properties().ToDictionary(p => p.Name, p => p.Value.Select(v => (string)v).ToList());
            var archetypeHats = ((JObject)defs["archetype_hats"]).Properties().ToDictionary(p => p.Name, p => p.Value.Select(v => (string)v).ToList());
            // generate.py replaces the hat list of every archetype outfit by the archetype's hats plus "none" when it loads; store that effective list.
            foreach (var pa in ((JObject)defs["archetypes"]).Properties())
                foreach (var outfit in pa.Value.Select(v => (string)v)) outfitHats[outfit] = archetypeHats[pa.Name].Concat(new[] { "none" }).ToList();

            foreach (var (folder, layer) in Folders)
            {
                foreach (string file in Directory.GetFiles(Path.Combine(assetsDir, folder), "*.png").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    var asset = new PortraitAsset { id = id, displayName = Pretty(id), layer = layer, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(layer, id)) };
                    if (layer == PortraitLayer.Clothing || layer == PortraitLayer.Headwear)
                    {
                        var m = (JObject)masks[layer == PortraitLayer.Clothing ? "clothing" : "headwear"][id];
                        if (m != null) { asset.materialId = (string)m["material"]; asset.maskColors = m["colors"].Select(c => (string)c).ToList(); }
                    }
                    if (layer == PortraitLayer.Headwear && occ[id] is JArray rows) asset.hairOcclusion = rows.Select(r => r.Type == JTokenType.Null ? -1 : (int)r).ToArray();
                    if (layer == PortraitLayer.Clothing && outfitHats.TryGetValue(id, out var hats)) asset.compatibleHeadwear = hats;
                    lib.assets.Add(asset);
                }
            }

            lib.palettes.Add(SetFromColors("skin", PaletteKind.Skin, (JObject)defs["skins"], "peach"));
            lib.palettes.Add(SetFromColors("hair", PaletteKind.Hair, (JObject)defs["hairs"], "brown"));
            foreach (var p in ((JObject)pal["palettes"]).Properties())
                lib.palettes.Add(new PaletteSet { id = p.Name, kind = PaletteKind.Material, entries = ((JObject)p.Value).Properties().Select(e => new PaletteEntry { id = e.Name, colors = new List<string> { ((string)e.Value).TrimStart('#') } }).ToList() });

            foreach (var pa in ((JObject)defs["archetypes"]).Properties())
                lib.archetypes.Add(new PortraitArchetype { id = pa.Name, displayName = Pretty(pa.Name), outfits = pa.Value.Select(v => (string)v).ToList(), headwear = archetypeHats[pa.Name] });
            foreach (var pr in ((JObject)defs["pools"]).Properties())
                lib.regions.Add(new PortraitRegion { id = pr.Name, displayName = Pretty(pr.Name), outfits = pr.Value["clothing"].Select(v => (string)v).ToList() });

            var ages = defs["ages"].Select(a => (string)a).ToList();
            var weights = defs["age_weights"].Select(w => (float)w).ToList();
            int[] minYears = { 18, 30, 45, 60, 75 };        // Young 18-29, Adult 30-44, Middle-aged 45-59, Elderly 60-74, Venerable 75+
            for (int i = 0; i < ages.Count; i++)
                lib.ageStages.Add(new AgeStageDef { id = ages[i], displayName = Pretty(ages[i]), minYears = minYears[i], assetId = ages[i], randomWeight = weights[i] });
            lib.style.browChoices = defs["brow_choices"].Select(v => (string)v).ToList();

            lib.InvalidateIndex();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"Portrait import: {lib.assets.Count} images, {lib.palettes.Count} palette sets, {lib.archetypes.Count} archetypes, {lib.regions.Count} regional pools.");
            return lib;
        }

        static PaletteSet SetFromColors(string id, PaletteKind kind, JObject colors, string baseId)
        {
            var set = new PaletteSet { id = id, kind = kind, sourceColors = colors[baseId].Select(c => (string)c).ToList() };
            foreach (var p in colors.Properties()) set.entries.Add(new PaletteEntry { id = p.Name, colors = p.Value.Select(c => (string)c).ToList() });
            return set;
        }

        static string Pretty(string id)
        {
            var words = id.Split('_');
            for (int i = 0; i < words.Length; i++) if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }
    }

    // Every image under Resources/Portraits is a native 48x48 sprite: Point filtering, no compression, no mipmaps, readable by the generator.
    public class PortraitTextureSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PortraitImporter.ResourcesDir + "/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.textureShape = TextureImporterShape.Texture2D;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;      // keep the exact colour under transparent pixels: no edge bleeding
            t.isReadable = true;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 256;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.spriteImportMode = SpriteImportMode.None;
            var d = t.GetDefaultPlatformTextureSettings();
            d.format = TextureImporterFormat.RGBA32; d.textureCompression = TextureImporterCompression.Uncompressed; d.maxTextureSize = 256;
            t.SetPlatformTextureSettings(d);
        }
    }
}
