// Save migration and cost of organic development (DESIGN_ORGANIC_DEVELOPMENT.md phase 9): an older game loads untouched and builds nothing on its own until the player allows
// it, deposits and forests never refill, legacy buildings keep working, and a day of the district engine stays cheap on a large settlement.
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class OrganicMigrationTests
    {
        static readonly string[] NonResidential = { "farm", "wood", "quarry", "mine", "hunter", "fisher" };

        /// <summary>A real first-year game (hand-placed farm, hunter, lodge, quarry, mine, painted zones, a year of cutting and digging), saved as an OLDER version would have: no districts, no projects.</summary>
        static (World played, string oldSave) PlayedAndSavedBeforeDistricts()
        {
            var w = FirstYearPlan.Run(7, 18, 120);
            w.ProtectRect(Defs.Start + 8, Defs.Start - 8, Defs.Start + 9, Defs.Start - 7);
            var save = JObject.Parse(w.SaveJson());
            save.Remove("districts"); save.Remove("projects");
            foreach (var b in (JArray)save["blds"]) ((JObject)b).Remove("organic");
            return (w, save.ToString());
        }

        [Test]
        public void AnOlderGameLoadsWithEverythingItHadAndNothingRefilled()
        {
            var (played, oldSave) = PlayedAndSavedBeforeDistricts();
            var loaded = World.LoadJson(oldSave);
            var a = JObject.Parse(played.SaveJson()); var b = JObject.Parse(loaded.SaveJson());
            foreach (var key in new[] { "districts", "projects" }) { a.Remove(key); b.Remove(key); }
            foreach (var bl in (JArray)a["blds"]) ((JObject)bl).Remove("organic");   // an older save has no such flag: those buildings load as legacy
            var differing = a.Properties().Where(p => !JToken.DeepEquals(p.Value, b[p.Name])).Select(p => p.Name).Concat(b.Properties().Where(p => a[p.Name] == null).Select(p => p.Name)).ToList();
            Assert.IsEmpty(differing, "every saved field (buildings, resources, deposits, forests, protected land, clearing orders) is exactly as it was; differing: " + string.Join(", ", differing));
            Assert.AreEqual(played.Blds.Count, loaded.Blds.Count, "nothing demolished");
            Assert.AreEqual(played.OreTaken.Values.Sum(), loaded.OreTaken.Values.Sum(), 1e-9, "depleted ore stays depleted");
            Assert.AreEqual(played.Dig.Values.Sum(), loaded.Dig.Values.Sum(), 1e-9, "dug rock stays dug");
            Assert.AreEqual(played.ProtectedWood.Count, loaded.ProtectedWood.Count);
            int trees(World w) { int n = 0; for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.DecoMap[y, x] == Deco.Tree) n++; return n; }
            Assert.AreEqual(trees(played), trees(loaded), "felled forests are not replenished");
        }

        [Test]
        public void OlderDistrictsStartPausedExceptHousingAndNothingIsBuiltUntilAllowed()
        {
            var (_, oldSave) = PlayedAndSavedBeforeDistricts();
            var w = World.LoadJson(oldSave);
            foreach (var d in w.Districts)
            {
                if (d.Type == DistrictType.Residential) Assert.AreEqual(DevMode.Automatic, d.Mode, d.Name);
                else { Assert.AreEqual(DevMode.Paused, d.Mode, d.Name); Assert.IsTrue(d.History.Any(h => h.Contains("Migrated")), d.Name); }
            }
            var before = NonResidential.ToDictionary(t => t, t => w.Blds.Count(b => b.T == t));
            for (int i = 0; i < 90; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 500); w.ResAmt[Res.Wood] = System.Math.Max(w.ResAmt[Res.Wood], 300); w.ResAmt[Res.Stone] = System.Math.Max(w.ResAmt[Res.Stone], 100); w.Tick(); }
            foreach (var t in NonResidential) Assert.AreEqual(before[t], w.Blds.Count(b => b.T == t), $"no new {t} was built in migrated land");
            Assert.AreEqual(0, w.Projects.Count(p => p.Type != "house"), "no project of any extraction or farming kind was started");
        }

        [Test]
        public void LegacyBuildingsKeepTheirOldRulesAndAreNeverDemolished()
        {
            var (played, oldSave) = PlayedAndSavedBeforeDistricts();
            var w = World.LoadJson(oldSave);
            foreach (var b in w.Blds.Where(b => b.T == "wood" || b.T == "quarry" || b.T == "mine")) Assert.IsFalse(b.Organic, $"{b.T} at {b.X},{b.Y} is legacy until adopted");
            var lodge = w.Blds.FirstOrDefault(b => b.T == "wood"); var oldLodge = played.Blds.FirstOrDefault(b => b.T == "wood");
            if (lodge != null) Assert.AreEqual(played.UsesZone(oldLodge, Zone.Wood), w.UsesZone(lodge, Zone.Wood), "a painted Lumber boundary still binds the old lodge");
            int count = w.Blds.Count;
            for (int i = 0; i < 60; i++) { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 500); w.Tick(); }
            Assert.GreaterOrEqual(w.Blds.Count, count, "nothing is ever demolished by development");
            foreach (var b in played.Blds.Where(b => NonResidential.Contains(b.T))) Assert.IsTrue(w.Blds.Any(o => o.T == b.T && o.X == b.X && o.Y == b.Y), $"{b.T} at {b.X},{b.Y} still stands");
        }

        [Test]
        public void TheMigratedGameSavesAndLoadsAgainIdentically()
        {
            var (_, oldSave) = PlayedAndSavedBeforeDistricts();
            var w = World.LoadJson(oldSave);
            string once = w.SaveJson();
            var again = World.LoadJson(once);
            Assert.AreEqual(once, again.SaveJson());
            Assert.AreEqual(w.Districts.Count, again.Districts.Count);
            Assert.AreEqual(w.Districts.Select(d => d.Id).ToArray(), again.Districts.Select(d => d.Id).ToArray(), "district ids are stable across save and load");
            Assert.AreEqual(DevMode.Paused, again.Districts.First(d => d.Type != DistrictType.Residential).Mode, "and stay paused until you change them");
        }

        [Test]
        public void WorkersAndJobsAreAlwaysCurrentNotSnapshotted()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s);
            DevEconomyTests.Run(w, 12);
            var lodge = w.Blds.FirstOrDefault(b => b.T == "wood");
            Assume.That(lodge != null);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5);
            lodge.Workers = 1;
            Assert.AreEqual(1, d.Workers); lodge.Workers = 3;
            Assert.AreEqual(3, d.Workers, "no rebuild needed to see today's staffing");
        }

        // ---------------- cost ----------------
        [Test]
        public void ADayOfTheDistrictEngineStaysCheapOnALargeSettlement()
        {
            var w = FirstYearPlan.Run(7, 18, 120);
            // plenty of land in every kind of district on top of the played town
            int zoned = 0;
            foreach (var (zone, x0, y0) in new[] { (Zone.Hunt, 40, 40), (Zone.Wood, 70, 40), (Zone.Farm, 40, 80), (Zone.Quarry, 80, 80), (Zone.Res, 110, 40) })
                zoned += w.ZoneRect(zone, Defs.Start - x0, Defs.Start - y0, Defs.Start - x0 + 25, Defs.Start - y0 + 25).zoned;
            for (int d = 0; d < 10; d++) { w.ResAmt[Res.Food] = 500; w.Tick(); }   // warm up
            var sw = Stopwatch.StartNew(); const int days = 120;
            for (int d = 0; d < days; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Wood] = System.Math.Max(w.ResAmt[Res.Wood], 100); w.Tick(); }
            double tickMs = sw.Elapsed.TotalMilliseconds / days;
            sw.Restart();
            for (int d = 0; d < 50; d++) { w.Touch(); w.RebuildDistricts(); }
            double rebuildMs = sw.Elapsed.TotalMilliseconds / 50;
            sw.Restart();
            for (int d = 0; d < 50; d++) { w.Touch(); w.Develop(); w.Day++; }
            double developMs = sw.Elapsed.TotalMilliseconds / 50;
            TestContext.Out.WriteLine($"PERF organic: tick={tickMs:0.00} ms, district rebuild={rebuildMs:0.00} ms, develop={developMs:0.00} ms, {w.Districts.Count} districts, {zoned} zoned tiles, {w.Blds.Count} buildings");
            Assert.Less(tickMs, 40, "a day of the simulation with districts stays cheap");
            Assert.Less(rebuildMs, 15, "rebuilding every district from the zone map is a few milliseconds");
            Assert.Less(developMs, 40, "a day of development, even with the districts rebuilt first, stays cheap");
        }
    }
}
