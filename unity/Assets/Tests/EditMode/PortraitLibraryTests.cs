using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrownAndHearth.Portraits;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    // The imported portrait package must be complete, exact and consistent. These tests read the registry the game loads at runtime.
    public class PortraitLibraryTests
    {
        static PortraitLibrary Lib => PortraitLibrary.Shared;
        static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PortraitSource", "assets"));

        [Test] public void RegistryLoadsFromResources() { Assert.NotNull(Lib, "Resources/Portraits/PortraitLibrary.asset is missing"); }

        [TestCase(PortraitLayer.Face, 1)]
        [TestCase(PortraitLayer.Age, 5)]
        [TestCase(PortraitLayer.Clothing, 44)]
        [TestCase(PortraitLayer.Hair, 17)]
        [TestCase(PortraitLayer.Brows, 3)]
        [TestCase(PortraitLayer.FacialHair, 16)]
        [TestCase(PortraitLayer.Eyes, 4)]
        [TestCase(PortraitLayer.Headwear, 27)]
        public void EveryPackageImageIsRegistered(PortraitLayer layer, int count)
        {
            Assert.AreEqual(count, Lib.AssetsOf(layer).Count);
            foreach (string file in Directory.GetFiles(Path.Combine(SourceDir, PortraitImporterFolder(layer)), "*.png"))
                Assert.NotNull(Lib.Find(layer, Path.GetFileNameWithoutExtension(file)), file);
        }

        static string PortraitImporterFolder(PortraitLayer l) => l switch
        {
            PortraitLayer.Face => "faces", PortraitLayer.Age => "age", PortraitLayer.Clothing => "clothing", PortraitLayer.Hair => "hair",
            PortraitLayer.Brows => "brows", PortraitLayer.FacialHair => "beards", PortraitLayer.Eyes => "eyes", _ => "headwear",
        };

        [Test]
        public void ImagesAreByteForByteTheSuppliedArtwork()
        {
            foreach (var a in Lib.assets)
            {
                string src = Path.Combine(SourceDir, PortraitImporterFolder(a.layer), a.id + ".png");
                string dst = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Portraits", PortraitImporterFolder(a.layer), a.id + ".png"));
                Assert.IsTrue(File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst)), $"{a.layer} {a.id} differs from the package");
            }
        }

        [Test]
        public void ImagesAreNative48x48PointUncompressedNoMips()
        {
            foreach (var a in Lib.assets)
            {
                Assert.NotNull(a.texture, $"{a.layer} {a.id} has no texture");
                Assert.AreEqual(48, a.texture.width, a.id); Assert.AreEqual(48, a.texture.height, a.id);
                Assert.AreEqual(FilterMode.Point, a.texture.filterMode, a.id);
                Assert.AreEqual(1, a.texture.mipmapCount, a.id + " has mipmaps");
                Assert.AreEqual(TextureFormat.RGBA32, a.texture.format, a.id);
                Assert.IsTrue(a.texture.isReadable, a.id + " is not readable");
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(a.texture));
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, a.id);
                Assert.IsFalse(imp.mipmapEnabled, a.id);
            }
        }

        [Test]
        public void IdsAreUniqueWithinALayer()
        {
            foreach (var g in Lib.assets.GroupBy(a => a.layer)) Assert.AreEqual(g.Count(), g.Select(a => a.id).Distinct().Count(), g.Key.ToString());
        }

        [Test]
        public void PalettesAreComplete()
        {
            var skin = Lib.FindPalette(Lib.skinSetId); var hair = Lib.FindPalette(Lib.hairSetId);
            Assert.AreEqual(10, skin.entries.Count); Assert.AreEqual(10, hair.entries.Count);
            Assert.AreEqual(5, skin.sourceColors.Count); Assert.AreEqual(3, hair.sourceColors.Count);
            Assert.NotNull(Lib.FindEntry(skin.id, Lib.style.baseSkinId)); Assert.NotNull(Lib.FindEntry(hair.id, Lib.style.baseHairId));
            foreach (var e in skin.entries) Assert.AreEqual(5, e.colors.Count, e.id);
            foreach (var e in hair.entries) Assert.AreEqual(3, e.colors.Count, e.id);
            Assert.AreEqual(7, Lib.palettes.Count(p => p.kind == PaletteKind.Material));
        }

        [Test]
        public void ArchetypesAndRegionsReferenceRealAssets()
        {
            Assert.AreEqual(8, Lib.archetypes.Count); Assert.AreEqual(6, Lib.regions.Count); Assert.AreEqual(5, Lib.ageStages.Count);
            foreach (var a in Lib.archetypes)
            {
                Assert.AreEqual(3, a.outfits.Count, a.id); Assert.AreEqual(3, a.headwear.Count, a.id);
                foreach (var o in a.outfits) Assert.NotNull(Lib.Find(PortraitLayer.Clothing, o), a.id + "/" + o);
                foreach (var h in a.headwear) Assert.NotNull(Lib.Find(PortraitLayer.Headwear, h), a.id + "/" + h);
            }
            foreach (var r in Lib.regions) foreach (var o in r.outfits) Assert.NotNull(Lib.Find(PortraitLayer.Clothing, o), r.id + "/" + o);
            foreach (var s in Lib.ageStages) Assert.NotNull(Lib.Find(PortraitLayer.Age, s.assetId), s.id);
            Assert.AreEqual(new[] { 18, 30, 45, 60, 75 }, Lib.ageStages.Select(s => s.minYears).ToArray());
        }

        [Test]
        public void EveryOutfitHasCompatibleHatsAndMaskedColoursMatchMaterials()
        {
            foreach (var c in Lib.AssetsOf(PortraitLayer.Clothing))
            {
                Assert.IsNotEmpty(c.compatibleHeadwear, c.id);
                foreach (var h in c.compatibleHeadwear) Assert.NotNull(Lib.Find(PortraitLayer.Headwear, h), c.id + "/" + h);
            }
            foreach (var a in Lib.assets.Where(a => a.maskColors.Count > 0))
            {
                Assert.NotNull(Lib.FindPalette(a.materialId), a.id + " material " + a.materialId);
                foreach (var m in a.maskColors) Assert.AreEqual(6, m.Length, a.id);
            }
        }

        [Test]
        public void HairOcclusionHasOneEntryPerColumn()
        {
            foreach (var h in Lib.AssetsOf(PortraitLayer.Headwear))
            {
                if (h.hairOcclusion.Length == 0) continue;
                Assert.AreEqual(48, h.hairOcclusion.Length, h.id);
                foreach (int y in h.hairOcclusion) Assert.That(y, Is.InRange(-1, 47), h.id);
            }
            Assert.AreEqual(0, Lib.Find(PortraitLayer.Headwear, "none").hairOcclusion.Length);
        }
    }
}
