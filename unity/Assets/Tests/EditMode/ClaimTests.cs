// Land ownership (Sim/World.Claims.cs): a 256 x 256 city map of which you own a 64 x 64 district to start, then buy 16 x 16 parcels.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ClaimTests
    {
        static readonly int C = Defs.Start;

        [Test]
        public void TheCityMapIs256AndYouStartWithA64x64DistrictAroundTheTownHall()
        {
            Assert.AreEqual(256, Defs.N);
            var w = new World(7);
            Assert.AreEqual(World.StartParcels, w.ClaimedParcels);
            Assert.AreEqual(16, w.ClaimedParcels, "4 x 4 parcels of 16 x 16 tiles");
            int owned = 0, minX = 999, maxX = -1, minY = 999, maxY = -1;
            for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.Claimed(x, y)) { owned++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            Assert.AreEqual(64 * 64, owned);
            Assert.AreEqual(64, maxX - minX + 1); Assert.AreEqual(64, maxY - minY + 1);
            Assert.AreEqual(C - 32, minX); Assert.AreEqual(C + 31, maxX, "centred on the Town Hall's start tile");
            foreach (var b in w.Blds) foreach (var (x, y) in b.Tiles()) Assert.IsTrue(w.Claimed(x, y), $"{b.T} at {x},{y} is on your land");
            Assert.IsFalse(w.Claimed(-1, 5)); Assert.IsFalse(w.Claimed(5, 5));
        }

        [Test]
        public void YouCanOnlyBuildZoneRoadAndClearOnLandYouOwn()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 500; w.ResAmt[Res.Stone] = 100;
            int outX = C + 40;   // 8 tiles beyond the border
            var spot = Enumerable.Range(C - 10, 20).Select(dy => (x: outX, y: C + dy)).First(t => !w.IsSolid(t.x, t.y) && !w.HasTree(t.x, t.y) && w.Grid[t.y, t.x] == null);
            Assert.IsFalse(w.Claimed(spot.x, spot.y));
            StringAssert.Contains("not yours", w.CanPlace("silo", spot.x, spot.y).Reason);
            StringAssert.Contains("not yours", w.ZoneCheck(Zone.Res, spot.x, spot.y).Reason);
            StringAssert.Contains("not yours", w.SetZone(Zone.Res, spot.x, spot.y).Reason);
            StringAssert.Contains("not yours", w.RoadPathCheck(new List<(int x, int y)> { (spot.x, spot.y) }).Reason);
            StringAssert.Contains("not yours", w.Remove(spot.x, spot.y).Reason);
            Assert.IsFalse(w.Place("silo", spot.x, spot.y).Ok);
            Assert.AreEqual(Zone.None, w.ZoneMap[spot.y, spot.x]);
            // the same things work on your own land
            var own = Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N)).First(t => w.Claimed(t.x, t.y) && w.CanPlace("silo", t.x, t.y).Ok);
            Assert.IsTrue(w.Place("silo", own.x, own.y).Ok);
        }

        [Test]
        public void CrewsAndMinesWorkOnlyOwnedLand()
        {
            var w = new World(7);
            // a marked tree beyond the border is not work for a lodge inside it
            var lodge = w.AddBld("wood", C + 28, C + 28, 2); w.Recalc();
            int tx = C + 33, ty = C + 29;   // x + 33: across the border (the district ends at C + 31)
            w.DecoMap[ty, tx] = Deco.Tree; w.ZoneMap[ty, tx] = Zone.Wood; w.Touch(); w.Recalc();
            Assert.IsFalse(w.Claimed(tx, ty));
            Assert.AreEqual(0, w.ActiveJobs(lodge), "a marked tree on land you do not own is not work");
            w.ClaimAll(); w.Recalc();
            Assert.AreEqual(1, w.ActiveJobs(lodge), "once you own the land it is");
        }

        [Test]
        public void ParcelsAreBoughtNextToYourLandForGoldAndCostMoreEachTime()
        {
            var w = new World(7);
            int lo = (C - 32) / World.ParcelSize;   // the first parcel column of the district
            Assert.AreEqual(25, w.ParcelCost, "the first parcel costs the base price");
            Assert.IsFalse(w.CanClaim(lo - 2, lo).Ok);
            StringAssert.Contains("next to", w.CanClaim(lo - 2, lo).Reason);
            Assert.IsFalse(w.CanClaim(lo, lo).Ok, "already yours");
            w.ResAmt[Res.Gold] = 10;
            StringAssert.Contains("Not enough gold", w.CanClaim(lo - 1, lo).Reason);
            w.ResAmt[Res.Gold] = 100;
            Assert.IsTrue(w.CanClaim(lo - 1, lo).Ok);
            int before = w.Version;
            Assert.IsTrue(w.ClaimParcel(lo - 1, lo).Ok);
            Assert.AreEqual(17, w.ClaimedParcels);
            Assert.AreEqual(75, w.ResAmt[Res.Gold], "25 gold paid");
            Assert.AreEqual(28, w.ParcelCost, "each parcel costs 3 more");
            Assert.That(w.Version, Is.GreaterThan(before), "the world reports the change so it is redrawn");
            Assert.IsTrue(w.Claimed((lo - 1) * 16 + 3, lo * 16 + 3));
            Assert.IsTrue(w.CanClaim(lo - 2, lo).Ok, "the new parcel is now a border");
            // you can now build there
            w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Stone] = 50;
            var site = Enumerable.Range(lo * 16, 16).Select(dy => (x: (lo - 1) * 16 + 8, y: dy)).First(t => w.CanPlace("silo", t.x, t.y).Ok);
            Assert.IsTrue(w.Place("silo", site.x, site.y).Ok);
        }

        [Test]
        public void ClaimingTheWholeMapEndsAtTheBigCity()
        {
            var w = new World(7);
            w.ClaimAll();
            Assert.AreEqual(World.ParcelsPerSide * World.ParcelsPerSide, w.ClaimedParcels);
            Assert.AreEqual(256, World.ParcelsPerSide * World.ParcelSize);
            Assert.IsFalse(w.CanClaim(3, 3).Ok);
            Assert.IsTrue(w.Claimed(0, 0) && w.Claimed(255, 255));
        }

        [Test]
        public void OwnershipSurvivesASaveAndIsPartOfIt()
        {
            var w = new World(7);
            w.ResAmt[Res.Gold] = 500;
            int lo = (C - 32) / World.ParcelSize;
            Assert.IsTrue(w.ClaimParcel(lo - 1, lo).Ok); Assert.IsTrue(w.ClaimParcel(lo - 2, lo).Ok); Assert.IsTrue(w.ClaimParcel(lo + 4, lo + 1).Ok);
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(19, loaded.ClaimedParcels);
            for (int y = 0; y < Defs.N; y += 8) for (int x = 0; x < Defs.N; x += 8) Assert.AreEqual(w.Claimed(x, y), loaded.Claimed(x, y), $"{x},{y}");
            Assert.AreEqual(w.SaveJson(), loaded.SaveJson());
            Assert.AreEqual(w.ParcelCost, loaded.ParcelCost);
        }

        /// <summary>A save as the 128 x 128 game wrote it: the middle 128 x 128 of this world, every index in the old numbering, no land parcels.</summary>
        static string Shrunk(World w, out int offset)
        {
            const int Old = 128; int off = offset = (Defs.N - Old) / 2;
            var save = JObject.Parse(w.SaveJson());
            bool Inside(int x, int y) => x >= off && y >= off && x < off + Old && y < off + Old;
            JArray CropGrid(JArray rows) { var o = new JArray(); for (int y = off; y < off + Old; y++) o.Add(new JArray(((JArray)rows[y]).Skip(off).Take(Old))); return o; }
            foreach (var g in new[] { "deco", "zone", "fert" }) save[g] = CropGrid((JArray)save[g]);
            var hv = (JArray)save["hv"]; int stride = Defs.N + 1; var nh = new JArray();
            for (int y = off; y <= off + Old; y++) for (int x = off; x <= off + Old; x++) nh.Add(hv[y * stride + x]);
            save["hv"] = nh;
            foreach (var key in new[] { "flaxFields", "rockAmt", "elev", "stumpT", "sapT", "chop", "dig", "oreLeft", "sownFields" })
            {
                if (!(save[key] is JObject map)) continue;
                var re = new JObject();
                foreach (var p in map.Properties())
                {
                    int k = int.Parse(p.Name), x = k % Defs.N, y = k / Defs.N;
                    if (Inside(x, y)) re[((y - off) * Old + (x - off)).ToString()] = p.Value;
                }
                save[key] = re;
            }
            var surveyed = new JArray();
            foreach (var t in (JArray)save["surveyed"]) { int k = t.Value<int>(), x = k % Defs.N, y = k / Defs.N; if (Inside(x, y)) surveyed.Add((y - off) * Old + (x - off)); }
            save["surveyed"] = surveyed;
            var taken = new JObject();
            foreach (var p in ((JObject)save["oreTaken"]).Properties())
            {
                int key = int.Parse(p.Name), cells = Defs.N * Defs.N, band = key / cells, k = key % cells, x = k % Defs.N, y = k / Defs.N;
                if (Inside(x, y)) taken[(band * Old * Old + (y - off) * Old + (x - off)).ToString()] = p.Value;
            }
            save["oreTaken"] = taken;
            foreach (var b in (JArray)save["blds"]) { b["x"] = b.Value<int>("x") - off; b["y"] = b.Value<int>("y") - off; }
            save.Remove("claimed"); save.Remove("aquifers");
            return save.ToString();
        }

        [Test]
        public void ASaveFromTheOld128MapLoadsIntoTheMiddleAndYouOwnAllOfIt()
        {
            var w = new World(7);
            for (int day = 0; day < 40; day++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            w.AddBld("mine", C + 3, C - 20); w.Recalc();
            int rich = Enumerable.Range(0, Defs.N * Defs.N).First(k => Math.Abs(k % Defs.N - C) < 40 && Math.Abs(k / Defs.N - C) < 40 && w.OreAt(k % Defs.N, k / Defs.N, 3) > 10);
            w.Surveyed[rich] = true; w.OreTaken[3 * Defs.N * Defs.N + rich] = 4.5;
            int houses = w.Blds.Count(b => b.T == "house"), roads = w.Blds.Count(b => b.T == "road");

            var old = Shrunk(w, out int offset);
            var loaded = World.LoadJson(old);
            Assert.AreEqual(houses, loaded.Blds.Count(b => b.T == "house"));
            Assert.AreEqual(roads, loaded.Blds.Count(b => b.T == "road"));
            Assert.AreEqual(w.Hall.X, loaded.Hall.X, "buildings keep their world position (the old map sits in the middle of the new one)");
            Assert.AreEqual(w.Hall.Y, loaded.Hall.Y);
            // the old city is entirely yours: the parcels that touch the old 128 x 128 square
            Assert.AreEqual(64, loaded.ClaimedParcels, "8 x 8 parcels = the old 128 x 128 map");
            Assert.IsTrue(loaded.Claimed(offset, offset) && loaded.Claimed(offset + 127, offset + 127));
            Assert.IsFalse(loaded.Claimed(offset - 1, offset), "but not beyond it");
            // surveys and mined ore moved with the tile they belong to
            Assert.IsTrue(loaded.Surveyed[rich], "the surveyed tile is still surveyed, in the new numbering");
            Assert.AreEqual(1, loaded.OreTaken.Count);
            Assert.AreEqual(4.5, loaded.OreTaken[3 * Defs.N * Defs.N + rich], 1e-9, "the same band, the same tile");
            for (int day = 0; day < 20; day++) { loaded.ResAmt[Res.Food] = 300; loaded.Tick(); }
            Assert.That(loaded.Peasants, Is.GreaterThan(0));
            var again = World.LoadJson(loaded.SaveJson());
            Assert.AreEqual(64, again.ClaimedParcels);
            Assert.AreEqual(loaded.SaveJson(), again.SaveJson());
        }
    }
}
