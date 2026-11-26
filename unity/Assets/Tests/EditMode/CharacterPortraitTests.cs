using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Portraits;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    // Characters (Sim) mapped onto the portrait system: a permanent saved definition, calendar ageing, and wardrobe changes that never touch the face.
    public class CharacterPortraitTests
    {
        static PortraitLibrary Lib => PortraitLibrary.Shared;

        static List<Character> People(int count, int seed = 11)
        {
            var reg = new CharacterRegistry();
            var ctx = new PoolContext { SettlementId = 3, Population = 50000, Seed = seed };
            double[] w = { 30, 15, 10, 10, 6, 8, 3 }; Array.Copy(w, ctx.Weights, 7);
            reg.FillToTarget(ctx, 0);
            var list = reg.All.Take(count).ToList();
            Assert.GreaterOrEqual(list.Count, Math.Min(count, 100));
            return list;
        }

        static string Face(PortraitAppearance a) => string.Join("|", a.face, a.skin, a.brows, a.eyes, a.beard);

        [Test]
        public void AgeStagesMatchTheCalendarStages()
        {
            Assert.AreEqual(CharacterBalance.StageStart, Lib.ageStages.Select(s => s.minYears).ToArray());
            Assert.AreEqual(CharacterBalance.StageIds, Lib.ageStages.Select(s => s.id).ToArray());
            foreach (int years in new[] { 18, 29, 30, 44, 45, 59, 60, 74, 75, 99 })
                Assert.AreEqual(CharacterBalance.StageIds[(int)CharacterBalance.StageOfAge(years)], PortraitAging.StageFor(Lib, years).id, "age " + years);
        }

        [Test]
        public void DefinitionIsMadeOnceSavedAndSurvivesLibraryChanges()
        {
            var c = People(1)[0];
            Assert.IsEmpty(c.Look.Definition);
            var def = CharacterPortraits.Definition(c, Lib);
            string json = c.Look.Definition; Assert.IsNotEmpty(json);
            Assert.AreEqual("young", def.age);

            // round-trip through the real save
            var reg = new CharacterRegistry(); reg.Add(c);
            var loaded = CharacterRegistry.Load(JObject.Parse(reg.Save().ToString())).Get(c.Id);
            Assert.AreEqual(json, loaded.Look.Definition);

            // change the library the generator uses; the saved person must not change
            var hair = Lib.AssetsOf(PortraitLayer.Hair); float old = hair[0].weight;
            try
            {
                foreach (var h in hair) h.weight = 0; hair[hair.Count - 1].weight = 1000; Lib.InvalidateIndex();
                Assert.AreEqual(json, CharacterPortraits.Definition(loaded, Lib).ToJson(), "an existing person changed when generation weights changed");
                var fresh = People(1)[0]; fresh.Look.Definition = "";
                Assert.AreEqual(hair[hair.Count - 1].id, CharacterPortraits.Definition(fresh, Lib).hair);
            }
            finally { foreach (var h in hair) h.weight = 1; hair[0].weight = old; Lib.InvalidateIndex(); }
        }

        [Test]
        public void SavesFromBeforeThePortraitDefinitionStillLoad()
        {
            var c = People(1)[0]; var reg = new CharacterRegistry(); reg.Add(c);
            var saved = reg.Save();
            foreach (JArray row in (JArray)saved["rows"]) ((JArray)row[20]).RemoveAt(7);   // the 7-entry look of an old save
            var back = CharacterRegistry.Load(saved).Get(c.Id);
            Assert.IsEmpty(back.Look.Definition);
            Assert.AreEqual(c.Look.Seed, back.Look.Seed);
            string a = CharacterPortraits.Definition(back, Lib).ToJson(); back.Look.Definition = "";
            Assert.AreEqual(a, CharacterPortraits.Definition(back, Lib).ToJson(), "the same seed must give the same definition");
        }

        [Test]
        public void ChangingOccupationChangesClothesNotTheFace()
        {
            foreach (var c in People(40))
            {
                var before = CharacterPortraits.Current(c, 360 * 40, Lib);
                c.Look.Archetype = c.Look.Archetype == "military" ? "merchant" : "military";
                var after = CharacterPortraits.Current(c, 360 * 40, Lib);
                Assert.AreEqual(Face(before), Face(after)); Assert.AreEqual(before.hair, after.hair); Assert.AreEqual(before.hair_color, after.hair_color);
                Assert.AreEqual(Lib.FindArchetype(c.Look.Archetype).outfits.Contains(after.clothing), true, c.Look.Archetype + " " + after.clothing);
                Assert.Contains(after.headwear, Lib.Find(PortraitLayer.Clothing, after.clothing).compatibleHeadwear);
                c.Look.Outfit = "royal_ceremonial";
                Assert.AreEqual("royal_ceremonial", CharacterPortraits.Current(c, 360 * 40, Lib).clothing);
                Assert.AreEqual(Face(before), Face(CharacterPortraits.Current(c, 360 * 40, Lib)));
            }
        }

        [Test]
        public void AgeingKeepsPeopleRecognisableAndFollowsTheirOwnTendencies()
        {
            int bald70 = 0, white70 = 0, grey70 = 0, receded70 = 0, total = 0;
            foreach (var c in People(120))
            {
                c.BirthDay = 0;
                var def = CharacterPortraits.Definition(c, Lib); string face = Face(def);
                string prevStage = null;
                for (int years = 18; years <= 95; years++)
                {
                    var a = CharacterPortraits.Current(c, years * 360, Lib);
                    Assert.AreEqual(face, Face(a), $"{c.Name} at {years}");
                    Assert.AreEqual(CharacterBalance.StageIds[(int)CharacterBalance.StageOfAge(years)], a.age);
                    Assert.AreEqual(def.clothing, a.clothing); Assert.AreEqual(def.headwear, a.headwear);
                    if (c.Look.GreyAge > 0 && years < c.Look.GreyAge) Assert.AreEqual(def.hair_color, a.hair_color, "greyed before the person's onset age");
                    if (c.Look.RecedeAge == 0 || years < c.Look.RecedeAge) Assert.AreEqual(def.hair, a.hair, "hairline changed before the onset age");
                    if (c.Look.BaldAge == 0 && c.Look.RecedeAge == 0) Assert.AreEqual(def.hair, a.hair);
                    prevStage = a.age;
                }
                var at70 = CharacterPortraits.Current(c, 70 * 360, Lib); total++;
                if (at70.hair == "bald") bald70++; if (at70.hair_color == "white") white70++; if (at70.hair_color != def.hair_color) grey70++; if (at70.hair != def.hair) receded70++;
            }
            Assert.Less(bald70, total * 0.5, "most people must not be bald at 70"); Assert.Less(white70, total * 0.4, "most people must not be white-haired at 70");
            Assert.Greater(grey70, 0); Assert.Greater(receded70, 0);
            Assert.Less(grey70, total, "some people keep their hair colour at 70");
        }

        [Test]
        public void AgeOverlaysShowOnEverySkinTone()
        {
            var skin = Lib.FindPalette(Lib.skinSetId);
            var peachColors = skin.sourceColors.Select(PortraitRenderer.Hex).ToList();
            foreach (var tone in skin.entries.Where(e => e.id != Lib.style.baseSkinId))
            {
                Color32[] prev = null;
                foreach (var stage in Lib.ageStages)
                {
                    var face = PortraitRenderer.RenderLayer(Lib, new PortraitAppearance { skin = tone.id, age = stage.id }, PortraitLayer.Age);
                    Assert.IsFalse(face.Any(p => p.a != 0 && peachColors.Any(c => c.r == p.r && c.g == p.g && c.b == p.b)), $"{tone.id}/{stage.id}: unrecoloured peach pixels in the overlay");
                    var full = PortraitRenderer.Render(Lib, new PortraitAppearance { skin = tone.id, age = stage.id });
                    if (prev != null) Assert.IsTrue(full.Where((p, i) => p.r != prev[i].r || p.g != prev[i].g || p.b != prev[i].b).Count() >= 3, $"{tone.id}: {stage.id} looks the same as the stage before");
                    prev = full;
                }
            }
        }

        [Test]
        public void PortraitsAreOnlyRedrawnWhenTheLookChanges()
        {
            var c = People(1)[0]; c.BirthDay = 0; c.Look.GreyAge = 0; c.Look.RecedeAge = 0; c.Look.BaldAge = 0;
            var cache = new PortraitCache(16);
            for (int day = 20 * 360; day < 28 * 360; day += 30) CharacterPortraits.Texture(c, day, Lib, cache);
            Assert.AreEqual(1, cache.Renders, "age 20 to 28 is one stage: one drawing");
            CharacterPortraits.Texture(c, 31 * 360, Lib, cache); Assert.AreEqual(2, cache.Renders, "a new stage is a new drawing");
            cache.Clear();
        }

        // Not a check: a sheet of people at ages 25 / 40 / 55 / 68 / 80 / 92 written to unity/Logs/portrait-sheets for looking at.
        [Test]
        public void WritesAgingSheet()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "portrait-sheets")); Directory.CreateDirectory(dir);
            var list = new List<PortraitAppearance>();
            foreach (var c in People(14)) { c.BirthDay = 0; foreach (int y in new[] { 25, 40, 55, 68, 80, 92 }) list.Add(CharacterPortraits.Current(c, y * 360, Lib)); }
            var t = PortraitSheet.Build(Lib, list, 6, 3);
            File.WriteAllBytes(Path.Combine(dir, "aging.png"), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        }
    }
}
