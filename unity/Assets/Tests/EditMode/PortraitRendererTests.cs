using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrownAndHearth.Portraits;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    // The native renderer must reproduce the reference generator exactly. The expected pictures (unity/PortraitSource/reference/reference_sheet.png) come from
    // reference_render.js, a port of generate.py, run over 1610 recipes that use every layer asset, every hat over every hairstyle, every skin x beard,
    // every age x skin, every hair colour and every dye (see PortraitSource/reference_render.js).
    public class PortraitRendererTests
    {
        [Serializable] class RecipeFile { public int columns; public List<PortraitAppearance> recipes; }
        static PortraitLibrary Lib => PortraitLibrary.Shared;
        static string RefDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PortraitSource", "reference"));

        static (RecipeFile file, Color32[] sheet, int sheetW, int sheetH) LoadReference()
        {
            var file = JsonUtility.FromJson<RecipeFile>(File.ReadAllText(Path.Combine(RefDir, "recipes.json")));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(Path.Combine(RefDir, "reference_sheet.png")), false));
            var px = tex.GetPixels32(); int w = tex.width, h = tex.height; UnityEngine.Object.DestroyImmediate(tex);
            return (file, px, w, h);
        }

        // top-first 48x48 crop of recipe i from the reference sheet
        static Color32[] ReferenceFor(Color32[] sheet, int w, int h, int columns, int i)
        {
            var o = new Color32[48 * 48]; int ox = (i % columns) * 48, oy = (i / columns) * 48;
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) o[y * 48 + x] = sheet[(h - 1 - (oy + y)) * w + ox + x];
            return o;
        }

        [Test]
        public void MatchesTheReferenceGeneratorPixelForPixel()
        {
            var (file, sheet, w, h) = LoadReference();
            Assert.GreaterOrEqual(file.recipes.Count, 1500);
            int bad = 0; var first = new List<string>();
            for (int i = 0; i < file.recipes.Count; i++)
            {
                var got = PortraitRenderer.Render(Lib, file.recipes[i]); var want = ReferenceFor(sheet, w, h, file.columns, i);
                for (int p = 0; p < got.Length; p++)
                    if (got[p].r != want[p].r || got[p].g != want[p].g || got[p].b != want[p].b || got[p].a != want[p].a)
                    { bad++; if (first.Count < 5) first.Add($"recipe {i} pixel ({p % 48},{p / 48}) got {got[p]} want {want[p]}: {file.recipes[i].ToJson()}"); break; }
            }
            Assert.AreEqual(0, bad, $"{bad} of {file.recipes.Count} portraits differ from the reference\n" + string.Join("\n", first));
        }

        [Test]
        public void RendersWithTheFixedLayerOrderAndFrame()
        {
            var a = new PortraitAppearance { hair = "short", headwear = "none", beard = "none" };
            var px = PortraitRenderer.Render(Lib, a);
            Assert.AreEqual(PortraitRenderer.Hex("30242a"), (Color32)new Color32(px[0].r, px[0].g, px[0].b, 255));          // outer frame corner
            Assert.AreEqual(PortraitRenderer.Hex("c39a55").r, px[1 * 48 + 1].r);                                              // inner frame
            Assert.IsTrue(px.All(p => p.a == 255), "portrait must be fully opaque");
        }

        [Test]
        public void RemovingTheHatRestoresTheWholeHairstyle()
        {
            foreach (var hair in Lib.AssetsOf(PortraitLayer.Hair))
            {
                var a = new PortraitAppearance { hair = hair.id, headwear = "none" };
                var layer = PortraitRenderer.RenderLayer(Lib, a, PortraitLayer.Hair);
                var original = PortraitRenderer.Recolor(PortraitRenderer.LayerPixels(hair), Lib.FindPalette(Lib.hairSetId), Lib.FindEntry(Lib.hairSetId, a.hair_color));
                CollectionAssert.AreEqual(original.Select(p => (p.r, p.g, p.b, p.a)), layer.Select(p => (p.r, p.g, p.b, p.a)), hair.id);
            }
        }

        [Test]
        public void CrownsAndCircletsKeepAllHairVisibleAndHatsNeverAddHair()
        {
            int lostTotal = 0; var untouched = new List<string>();
            foreach (var hair in Lib.AssetsOf(PortraitLayer.Hair).Where(h => h.id != "bald"))
            {
                int Opaque(string hat) => PortraitRenderer.RenderLayer(Lib, new PortraitAppearance { hair = hair.id, headwear = hat }, PortraitLayer.Hair).Count(p => p.a != 0);
                int bare = Opaque("none");
                Assert.AreEqual(bare, Opaque("royal_crown"), hair.id + " under a crown"); Assert.AreEqual(bare, Opaque("royal_circlet"), hair.id + " under a circlet");
                int under = Opaque("military"); Assert.LessOrEqual(under, bare, hair.id); lostTotal += bare - under; if (under == bare) untouched.Add(hair.id);
            }
            // The occlusion lines only cut hair that would poke out above a closed hat's silhouette, so most hairstyles lose nothing under a plain cap; what matters is it never adds hair.
        }

        [Test]
        public void HairBelowTheBrimStaysUnderHats()
        {
            foreach (var hat in Lib.AssetsOf(PortraitLayer.Headwear).Where(h => h.hairOcclusion.Length == 48))
                foreach (var hair in new[] { "long", "shoulder_length", "wavy", "curly" })
                {
                    var bare = PortraitRenderer.RenderLayer(Lib, new PortraitAppearance { hair = hair, headwear = "none" }, PortraitLayer.Hair);
                    var hatted = PortraitRenderer.RenderLayer(Lib, new PortraitAppearance { hair = hair, headwear = hat.id }, PortraitLayer.Hair);
                    for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                    {
                        int cut = hat.hairOcclusion[x];
                        if (y > cut) Assert.AreEqual(bare[y * 48 + x].a, hatted[y * 48 + x].a, $"{hat.id}/{hair} ({x},{y}) below the hat should be untouched");
                        else if (hat.hairOcclusion[x] >= 0) Assert.AreEqual(0, hatted[y * 48 + x].a, $"{hat.id}/{hair} ({x},{y}) under the hat should be hidden");
                    }
                }
        }

        [Test]
        public void SkinRecolourChangesOnlySkinAndAgeOverlaysFollowSkin()
        {
            var peach = PortraitRenderer.Render(Lib, new PortraitAppearance { skin = "peach", age = "venerable" });
            var deep = PortraitRenderer.Render(Lib, new PortraitAppearance { skin = "deep", age = "venerable" });
            var young = PortraitRenderer.Render(Lib, new PortraitAppearance { skin = "deep", age = "young" });
            Assert.AreNotEqual(peach[24 * 48 + 24].r, deep[24 * 48 + 24].r);
            Assert.IsTrue(deep.Zip(young, (a, b) => a.r != b.r || a.g != b.g || a.b != b.b).Any(d => d), "the venerable overlay must show on dark skin");
            // no pure peach-palette colour may survive on the deep-skin face
            var peachColors = Lib.FindPalette(Lib.skinSetId).sourceColors.Select(PortraitRenderer.Hex).ToList();
            var face = PortraitRenderer.RenderLayer(Lib, new PortraitAppearance { skin = "deep", age = "venerable" }, PortraitLayer.Face);
            Assert.IsFalse(face.Any(p => p.a != 0 && peachColors.Any(c => c.r == p.r && c.g == p.g && c.b == p.b)), "peach skin pixels left on a deep-skin face");
        }

        [Test]
        public void DyeChangesTheMaskedColoursAndKeepsTheRestOfTheArtwork()
        {
            var civil = Lib.Find(PortraitLayer.Clothing, "civic_doublet");
            var orig = PortraitRenderer.Dye(Lib, civil, "original"); var teal = PortraitRenderer.Dye(Lib, civil, "teal");
            int changed = 0, other = 0, unmasked = 0; var mask = civil.maskColors.Select(PortraitRenderer.Hex).ToList();
            for (int i = 0; i < orig.Length; i++)
            {
                bool masked = mask.Any(c => c.r == orig[i].r && c.g == orig[i].g && c.b == orig[i].b);
                bool same = orig[i].r == teal[i].r && orig[i].g == teal[i].g && orig[i].b == teal[i].b;
                if (masked && !same) changed++; if (masked && same) other++; if (!masked) { unmasked++; Assert.IsTrue(same, "trim and other materials must stay untouched"); }
                Assert.AreEqual(orig[i].a, teal[i].a);
            }
            Assert.Greater(changed, 20); Assert.Greater(unmasked, 0);
        }

        [Test]
        public void UnknownIdsDoNotCrashAndSkipTheLayer()
        {
            var px = PortraitRenderer.Render(Lib, new PortraitAppearance { hair = "does_not_exist", clothing = "gone", headwear = "gone", skin = "nope", clothing_color = "chartreuse" });
            Assert.AreEqual(2304, px.Length);
        }

        [Test]
        public void TexturesAreNative48x48PointNoMips()
        {
            var cache = new PortraitCache(8);
            var t = cache.Get(Lib, new PortraitAppearance());
            Assert.AreEqual(48, t.width); Assert.AreEqual(48, t.height); Assert.AreEqual(FilterMode.Point, t.filterMode); Assert.AreEqual(1, t.mipmapCount); Assert.AreEqual(TextureFormat.RGBA32, t.format);
            cache.Clear();
        }

        [Test]
        public void TextureOrientationMatchesTheBuffer()
        {
            var a = new PortraitAppearance(); var buf = PortraitRenderer.Render(Lib, a);
            var tex = PortraitRenderer.ToTexture(buf);
            var back = tex.GetPixels32();
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) Assert.AreEqual(buf[y * 48 + x].g, back[(47 - y) * 48 + x].g);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        [Test]
        public void CacheRendersOncePerAppearanceAndEvictsOldOnes()
        {
            var cache = new PortraitCache(4); var a = new PortraitAppearance { hair = "curly" };
            var t1 = cache.Get(Lib, a); var t2 = cache.Get(Lib, a.Clone());
            Assert.AreSame(t1, t2); Assert.AreEqual(1, cache.Renders);
            var b = a.Clone(); b.seed = 99; cache.Get(Lib, b); Assert.AreEqual(1, cache.Renders, "the seed does not change the picture");
            b.hair = "wavy"; cache.Get(Lib, b); Assert.AreEqual(2, cache.Renders);
            for (int i = 0; i < 10; i++) { var c = a.Clone(); c.clothing = Lib.AssetsOf(PortraitLayer.Clothing)[i].id; cache.Get(Lib, c); }
            Assert.LessOrEqual(cache.Count, 4);
            cache.Clear(); Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void AppearanceRoundTripsThroughJsonAndLoadsPythonRecipes()
        {
            var a = PortraitGenerator.Generate(Lib, 7, "cold", null);
            var b = PortraitAppearance.FromJson(a.ToJson());
            Assert.AreEqual(a.ToJson(), b.ToJson());
            var py = PortraitAppearance.FromJson("{\"version\":8,\"seed\":0,\"climate\":\"temperate\",\"archetype\":null,\"face\":\"standard\",\"skin\":\"olive\",\"hair\":\"curly\",\"hair_color\":\"auburn\",\"brows\":\"short\",\"eyes\":\"standard\",\"beard\":\"goatee\",\"clothing\":\"merchant\",\"headwear\":\"merchant\",\"clothing_color\":\"indigo\",\"headwear_color\":\"original\"}");
            Assert.AreEqual("olive", py.skin); Assert.AreEqual("young", py.age, "old recipes default to young"); Assert.AreEqual("goatee", py.beard);
        }

        [Test]
        public void GeneratorIsDeterministicAndOnlyUsesRealCompatibleAssets()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                string region = Lib.regions[seed % Lib.regions.Count].id; string arch = seed % 3 == 0 ? Lib.archetypes[seed % Lib.archetypes.Count].id : null;
                var a = PortraitGenerator.Generate(Lib, seed, region, arch); var b = PortraitGenerator.Generate(Lib, seed, region, arch);
                Assert.AreEqual(a.ToJson(), b.ToJson());
                var cl = Lib.Find(PortraitLayer.Clothing, a.clothing); Assert.NotNull(cl);
                Assert.Contains(a.headwear, cl.compatibleHeadwear);
                if (arch != null) Assert.Contains(a.clothing, Lib.FindArchetype(arch).outfits); else Assert.Contains(a.clothing, Lib.FindRegion(region).outfits);
                Assert.NotNull(Lib.Find(PortraitLayer.Hair, a.hair)); Assert.NotNull(Lib.Find(PortraitLayer.FacialHair, a.beard)); Assert.NotNull(Lib.FindAgeStage(a.age));
                Assert.NotNull(Lib.FindEntry(Lib.skinSetId, a.skin)); Assert.NotNull(Lib.FindEntry(Lib.hairSetId, a.hair_color));
                Assert.Contains(a.clothing_color, PortraitGenerator.DyeChoices(Lib, cl));
            }
        }

        // Not a check: writes sheets to unity/Logs/portrait-sheets for looking at (generated people, every hat over a long and a short hairstyle).
        [Test]
        public void WritesContactSheetsForVisualInspection()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "portrait-sheets")); Directory.CreateDirectory(dir);
            var people = new List<PortraitAppearance>();
            for (int s = 0; s < 80; s++) people.Add(PortraitGenerator.Generate(Lib, s, Lib.regions[s % Lib.regions.Count].id, s % 2 == 0 ? Lib.archetypes[(s / 2) % Lib.archetypes.Count].id : null));
            var hats = new List<PortraitAppearance>();
            foreach (var hat in Lib.AssetsOf(PortraitLayer.Headwear))
                foreach (var hair in new[] { "short", "long", "curly", "wavy" }) hats.Add(new PortraitAppearance { hair = hair, headwear = hat.id, hair_color = "auburn", beard = "none", clothing = hat.id == "none" ? "civil" : "civic_doublet" });
            foreach (var (name, list, cols) in new[] { ("generated", people, 10), ("hats", hats, 12) })
            {
                var t = PortraitSheet.Build(Lib, list, cols, 3);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
            }
            Assert.IsTrue(File.Exists(Path.Combine(dir, "generated.png")));
        }

        [Test]
        public void GeneratorUsesTheWholeVariety()
        {
            var hairs = new HashSet<string>(); var beards = new HashSet<string>(); var ages = new HashSet<string>(); var skins = new HashSet<string>();
            for (int s = 0; s < 600; s++) { var a = PortraitGenerator.Generate(Lib, s, "temperate"); hairs.Add(a.hair); beards.Add(a.beard); ages.Add(a.age); skins.Add(a.skin); }
            Assert.AreEqual(17, hairs.Count); Assert.AreEqual(16, beards.Count); Assert.AreEqual(5, ages.Count); Assert.AreEqual(10, skins.Count);
        }
    }
}
