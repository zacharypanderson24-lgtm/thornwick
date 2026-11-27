// Districts, phase 1 of organic development (Sim/Districts.cs, Sim/World.Districts.cs, DESIGN_ORGANIC_DEVELOPMENT.md).
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class DistrictTests
    {
        /// <summary>A w x h rectangle of free claimed tiles near the Town Hall where every tile accepts this zone. Returns its top-left corner.</summary>
        static (int x, int y) FreeRect(World w, Zone kind, int rw, int rh, int skip = 0)
        {
            int found = 0;
            for (int r = 4; r < 28; r++)
                for (int y = Defs.Start - r; y <= Defs.Start + r; y++)
                    for (int x = Defs.Start - r; x <= Defs.Start + r; x++)
                    {
                        bool ok = true;
                        for (int j = 0; j < rh && ok; j++)
                            for (int i = 0; i < rw && ok; i++)
                            {
                                int tx = x + i, ty = y + j;
                                ok = World.Inb(tx, ty) && w.ZoneMap[ty, tx] == Zone.None && w.Grid[ty, tx] == null && w.Claimed(tx, ty) && w.ZoneCheck(kind, tx, ty).Ok;
                            }
                        // keep one tile of clear ground all round so separate rectangles never touch
                        for (int j = -1; j <= rh && ok; j++)
                            for (int i = -1; i <= rw && ok; i++)
                                ok = !World.Inb(x + i, y + j) || w.ZoneMap[y + j, x + i] == Zone.None;
                        if (ok && found++ >= skip) return (x, y);
                    }
            Assert.Fail("no free rectangle for " + kind);
            return (0, 0);
        }

        static District Make(World w, DistrictType t, int rw, int rh, int skip = 0)
        {
            var (x, y) = FreeRect(w, World.DefaultZone(t), rw, rh, skip);
            var (d, zoned, _, why) = w.ZoneDistrict(t, x, y, x + rw - 1, y + rh - 1);
            Assert.IsNotNull(d, why);
            Assert.AreEqual(rw * rh, zoned);
            return d;
        }

        [Test]
        public void ZonesOfTheSameKindThatTouchFormOneDistrictAndSeparateOnesStayApart()
        {
            var w = new World(7);
            int forestBefore = w.Districts.Count(q => q.Type == DistrictType.Forestry), allBefore = w.Districts.Count;
            var a = Make(w, DistrictType.Forestry, 4, 3);
            var b = Make(w, DistrictType.Forestry, 3, 3, 1);
            Assert.AreNotEqual(a.Id, b.Id);
            Assert.AreEqual(12, a.Area); Assert.AreEqual(9, b.Area);
            Assert.AreEqual(forestBefore + 2, w.Districts.Count(d => d.Type == DistrictType.Forestry));
            Assert.AreSame(a, w.DistrictAt(a.MinX, a.MinY));
            Assert.IsNull(w.DistrictAt(a.MinX - 1, a.MinY - 1));
        }

        [Test]
        public void BothHousingLevelsJoinOneResidentialDistrictButOtherKindsDoNot()
        {
            var w = new World(7);
            int forestBefore = w.Districts.Count(q => q.Type == DistrictType.Forestry), allBefore = w.Districts.Count;
            var d = Make(w, DistrictType.Residential, 4, 2);
            int x = d.MaxX + 1, y = d.MinY;
            Assume.That(w.ZoneCheck(Zone.Res2, x, y).Ok && w.ZoneCheck(Zone.Wood, x, y + 1).Ok);
            Assert.IsTrue(w.SetZone(Zone.Res2, x, y).Ok);
            Assert.IsTrue(w.SetZone(Zone.Wood, x, y + 1).Ok);
            Assert.AreEqual(9, w.DistrictAt(d.MinX, d.MinY).Area, "eight Level 1 tiles and one Level 2 tile are one district");
            Assert.AreEqual(forestBefore + 1, w.Districts.Count(q => q.Type == DistrictType.Forestry), "a lone tile of another kind is its own district");
            Assert.AreEqual(allBefore + 2, w.Districts.Count);
        }

        [Test]
        public void ExpandingKeepsTheIdentityNameAndSettings()
        {
            var w = new World(7);
            var d = Make(w, DistrictType.Forestry, 4, 3);
            int id = d.Id; w.SetDistrictPriority(id, DevPriority.High); w.SetDistrictMode(id, DevMode.ApprovalRequired);
            Assert.IsTrue(w.RenameDistrict(id, "Old Wood").Ok);
            int x = d.MaxX + 1;
            Assume.That(w.ZoneCheck(Zone.Wood, x, d.MinY).Ok);
            w.SetZone(Zone.Wood, x, d.MinY);
            var after = w.DistrictAt(d.MinX, d.MinY);
            Assert.AreEqual(id, after.Id);
            Assert.AreEqual(13, after.Area);
            Assert.AreEqual(DevPriority.High, after.Priority); Assert.AreEqual(DevMode.ApprovalRequired, after.Mode);
            Assert.AreEqual("Old Wood", after.Name, "a player's name survives growth");
        }

        [Test]
        public void MergingKeepsTheBiggerDistrictAndItsSettings()
        {
            var w = new World(7);
            int forestBefore = w.Districts.Count(q => q.Type == DistrictType.Forestry), allBefore = w.Districts.Count;
            var (x, y) = FreeRect(w, Zone.Wood, 7, 2);
            var small = w.ZoneDistrict(DistrictType.Forestry, x, y, x + 1, y + 1).district;     // 2 x 2 at the left
            var big = w.ZoneDistrict(DistrictType.Forestry, x + 4, y, x + 6, y + 1).district;   // 3 x 2 at the right, a gap of two tiles between
            Assert.AreNotEqual(small.Id, big.Id);
            w.SetDistrictPriority(big.Id, DevPriority.Critical); w.SetDistrictPriority(small.Id, DevPriority.Low);
            int bigId = big.Id;
            w.ZoneRect(Zone.Wood, x + 2, y, x + 3, y + 1);   // fill the gap
            var merged = w.DistrictAt(x, y);
            Assert.AreEqual(forestBefore + 1, w.Districts.Count(q => q.Type == DistrictType.Forestry));
            Assert.AreEqual(7 * 2, merged.Area);
            Assert.AreEqual(bigId, merged.Id, "the bigger district's identity wins");
            Assert.AreEqual(DevPriority.Critical, merged.Priority);
            Assert.IsTrue(merged.History.Any(h => h.Contains("merged")));
        }

        [Test]
        public void SplittingKeepsTheBiggerPieceAndGivesTheOtherACopyOfTheSettings()
        {
            var w = new World(7);
            var (x, y) = FreeRect(w, Zone.Wood, 7, 1);
            var d = w.ZoneDistrict(DistrictType.Forestry, x, y, x + 6, y).district;
            w.SetDistrictPriority(d.Id, DevPriority.High);
            w.SetDistrictLimits(d.Id, new DistrictLimits { MaxBuildings = 3 });
            int id = d.Id;
            Assert.AreEqual(1, w.ClearZoneRect(x + 4, y, x + 4, y));   // cut the strip: 4 tiles on the left, 2 on the right
            var left = w.DistrictAt(x, y); var right = w.DistrictAt(x + 5, y);
            Assert.AreEqual(id, left.Id); Assert.AreNotEqual(id, right.Id);
            Assert.AreEqual(4, left.Area); Assert.AreEqual(2, right.Area);
            Assert.AreEqual(DevPriority.High, right.Priority); Assert.AreEqual(3, right.Limits.MaxBuildings);
            right.Limits.MaxBuildings = 9; Assert.AreEqual(3, left.Limits.MaxBuildings, "the copy is independent");
        }

        [Test]
        public void NamesAreSensibleAndUnique()
        {
            var w = new World(7);
            var a = Make(w, DistrictType.Forestry, 3, 3); var b = Make(w, DistrictType.Forestry, 3, 3, 1);
            var q = w.Districts.Select(d => d.Name).ToList();
            Assert.AreEqual(q.Count, q.Distinct(System.StringComparer.OrdinalIgnoreCase).Count(), string.Join("; ", q));
            Assert.IsTrue(a.Name.Contains("Forestry District"), a.Name);
            Assert.IsFalse(w.RenameDistrict(b.Id, a.Name).Ok, "two districts cannot share a name");
            Assert.IsTrue(w.RenameDistrict(b.Id, "The Hollins").Ok);
            Assert.AreEqual("The Hollins", w.DistrictById(b.Id).Name);
            Assert.IsTrue(w.RenameDistrict(b.Id, "").Ok, "an empty name goes back to the automatic one");
            Assert.IsTrue(w.DistrictById(b.Id).Name.Contains("Forestry District"));
            Assert.IsFalse(w.RenameDistrict(b.Id, new string('x', 41)).Ok);
        }

        [Test]
        public void ManualBuildingsInsideCountAndBuildingsOutsideAreLegacy()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 500;
            var d = Make(w, DistrictType.Forestry, 6, 6);
            // a 2 x 2 lodge placed by hand inside the district, and one placed far from any zone
            Building inside = null;
            for (int y = d.MinY; y < d.MaxY && inside == null; y++)
                for (int x = d.MinX; x < d.MaxX && inside == null; x++)
                    if (w.CanPlace("wood", x, y).Ok) inside = w.AddBld("wood", x, y, 2);
            Assert.IsNotNull(inside, "a lodge fits inside the zone");
            w.Touch();
            Assert.AreEqual(1, w.DistrictAt(d.MinX, d.MinY).Buildings.Count);
            Assert.IsFalse(w.IsLegacyBuilding(inside));
            Assert.IsTrue(w.LegacyBuildings().All(b => b != inside));
            var farm = w.Blds.First(b => b.T == "farm");   // the starting farm stands on its own zone: farmland, not forestry
            Assert.IsNull(w.DistrictAt(d.MinX - 5, d.MinY - 5));
            Assert.AreEqual(w.IsLegacyBuilding(farm), w.DistrictAt(farm.X, farm.Y)?.Type != DistrictType.Agricultural);
        }

        [Test]
        public void RemovingADistrictsZoneNeverDemolishes()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 500;
            var d = Make(w, DistrictType.Forestry, 6, 6);
            Building lodge = null;
            for (int y = d.MinY; y < d.MaxY && lodge == null; y++)
                for (int x = d.MinX; x < d.MaxX && lodge == null; x++)
                    if (w.CanPlace("wood", x, y).Ok) lodge = w.AddBld("wood", x, y, 2);
            Assert.IsNotNull(lodge);
            int cleared = w.RemoveDistrictZone(d.Id);
            Assert.AreEqual(36 - 4, cleared, "the four tiles under the lodge keep their zone");
            Assert.IsTrue(w.Blds.Contains(lodge));
            Assert.AreEqual(4, w.DistrictAt(d.MinX, d.MinY).Area);
        }

        [Test]
        public void NewZoneKindsFollowTheirRules()
        {
            var w = new World(7);
            // find a claimed open tile far from water and rock
            (int x, int y) t = (0, 0); bool got = false;
            for (int y = Defs.Start - 12; y < Defs.Start + 12 && !got; y++)
                for (int x = Defs.Start - 12; x < Defs.Start + 12 && !got; x++)
                    if (w.Claimed(x, y) && w.Grid[y, x] == null && w.DecoMap[y, x] == Deco.None && !w.NearWater(x, y, World.FishZoneReach) && !w.IsSolid(x, y)) { t = (x, y); got = true; }
            Assert.IsTrue(got);
            Assert.IsFalse(w.ZoneCheck(Zone.Quarry, t.x, t.y).Ok, "no stone here");
            Assert.IsFalse(w.ZoneCheck(Zone.Fish, t.x, t.y).Ok, "no shore here");
            Assert.IsTrue(w.ZoneCheck(Zone.Hunt, t.x, t.y).Ok);
            Assert.IsTrue(w.ZoneCheck(Zone.Hunt, t.x, t.y).Ok);
            var s = w.Suitability(Zone.Hunt, t.x, t.y);
            Assert.IsTrue(s.Ok); Assert.That(s.Score, Is.InRange(0.0, 1.0));
            Assert.IsFalse(w.Suitability(Zone.Quarry, t.x, t.y).Ok);
            Assert.IsFalse(w.Suitability(Zone.Comm, t.x, t.y).Ok);
        }

        [Test]
        public void SaveAndLoadKeepIdentitiesNamesAndSettingsAndCreateNoDuplicates()
        {
            var w = new World(7);
            var a = Make(w, DistrictType.Forestry, 4, 3); var b = Make(w, DistrictType.Hunting, 3, 3, 1);
            w.SetDistrictPriority(a.Id, DevPriority.Critical); w.SetDistrictMode(b.Id, DevMode.Paused);
            w.SetDistrictLimits(a.Id, new DistrictLimits { MaxBuildings = 2, MinWood = 40, ProductionTarget = 5 });
            w.RenameDistrict(b.Id, "Kings Chase");
            string json = w.SaveJson();
            var loaded = World.LoadJson(json);
            Assert.AreEqual(w.Districts.Count, loaded.Districts.Count, "same districts after a load");
            var la = loaded.DistrictById(a.Id); var lb = loaded.DistrictById(b.Id);
            Assert.AreEqual(12, la.Area); Assert.AreEqual(DevPriority.Critical, la.Priority);
            Assert.AreEqual(2, la.Limits.MaxBuildings); Assert.AreEqual(40, la.Limits.MinWood); Assert.AreEqual(5, la.Limits.ProductionTarget);
            Assert.AreEqual("Kings Chase", lb.Name); Assert.AreEqual(DevMode.Paused, lb.Mode);
            Assert.AreEqual(json, loaded.SaveJson(), "loading and saving again changes nothing");
            // new districts after a load do not reuse an id
            var (x, y) = FreeRect(loaded, Zone.Wood, 2, 2);
            var c = loaded.ZoneDistrict(DistrictType.Forestry, x, y, x + 1, y + 1).district;
            Assert.IsFalse(new[] { a.Id, b.Id }.Contains(c.Id));
        }

        [Test]
        public void ASaveFromBeforeDistrictsGetsDistrictsFromItsZones()
        {
            var w = new World(7);
            Make(w, DistrictType.Forestry, 4, 3); Make(w, DistrictType.Hunting, 3, 3, 1);
            var save = JObject.Parse(w.SaveJson());
            save.Remove("districts");
            var loaded = World.LoadJson(save.ToString());
            Assert.AreEqual(w.Districts.Count, loaded.Districts.Count, "districts are derived from the zone map");
            Assert.AreEqual(Enumerable.Range(1, w.Districts.Count).ToArray(), loaded.Districts.Select(d => d.Id).ToArray());
            Assert.IsTrue(loaded.Districts.All(d => d.Priority == DevPriority.Normal && (d.Type == DistrictType.Residential ? d.Mode == DevMode.Automatic : d.Mode == DevMode.Paused)), "migrated districts: housing as before, everything else paused until the player says so");
            // the starting farm's field is farmland: a default save always has at least that district
            Assert.IsTrue(new World(7).Districts.Any(d => d.Type == DistrictType.Agricultural));
        }

        [Test]
        public void TheSameActionsGiveTheSameDistrictsEveryTime()
        {
            string Run()
            {
                var w = new World(7);
                var a = Make(w, DistrictType.Forestry, 4, 3); Make(w, DistrictType.Hunting, 3, 3, 1);
                w.ZoneRect(Zone.Wood, a.MaxX + 1, a.MinY, a.MaxX + 2, a.MaxY);
                w.ClearZoneRect(a.MinX + 1, a.MinY, a.MinX + 1, a.MaxY);
                return string.Join("|", w.Districts.Select(d => $"{d.Id}:{d.Name}:{d.Area}:{d.CenterX:F2},{d.CenterY:F2}"));
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void ADisabledOrPausedDistrictStopsNewDevelopmentOnly()
        {
            var w = new World(7);
            var d = Make(w, DistrictType.Forestry, 3, 3);
            Assert.IsTrue(d.DevelopmentOn);
            w.SetDistrictPriority(d.Id, DevPriority.Disabled); Assert.IsFalse(d.DevelopmentOn);
            w.SetDistrictPriority(d.Id, DevPriority.High); w.SetDistrictMode(d.Id, DevMode.Paused); Assert.IsFalse(d.DevelopmentOn);
            w.SetDistrictMode(d.Id, DevMode.Automatic); Assert.IsTrue(d.DevelopmentOn);
            Assert.IsFalse(w.SetDistrictPriority(9999, DevPriority.Low).Ok);
            Assert.IsFalse(w.SetDistrictLimits(d.Id, new DistrictLimits { MaxBuildings = -1 }).Ok);
        }
    }
}
