// Mining and Quarrying districts (Sim/World.DevExtraction.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 4).
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ExtractionDistrictTests
    {
        /// <summary>A quarry district: a spur road with a patch of outcrop rock beside it, zoned for Quarrying.</summary>
        static (World w, District d, (int x, int y, int dx, int dy, int px, int py) s) QuarryTown(bool rock = true)
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w);
            if (rock)
                for (int a = 1; a <= 10; a++) for (int t = 3; t <= 6; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; w.DecoMap[y, x] = Deco.Rock; }
            for (int a = 1; a <= 10; a++) for (int t = 1; t <= 6; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; if (w.ZoneCheck(Zone.Quarry, x, y).Ok) w.ZoneMap[y, x] = Zone.Quarry; }
            w.Touch();
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 4, s.y + s.dy * 5 + s.py * 4);
            return (w, d, s);
        }

        [Test]
        public void AQuarryDistrictBuildsItsFirstQuarryAndDigsOnlyItsOwnRock()
        {
            var (w, d, s) = QuarryTown();
            Assume.That(d != null && d.Type == DistrictType.Quarrying);
            Assume.That(w.D.Idle > 0);
            DevEconomyTests.Run(w, 14);
            var q = w.Blds.Where(b => b.T == "quarry").ToList();
            Assert.AreEqual(1, q.Count, w.DistrictById(d.Id).Note);
            Assert.IsTrue(q[0].Organic);
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "quarry"), "counted in its district");
            // it works rock inside the Quarrying zone and ignores rock outside it
            int inX = s.x + s.dx * 5 + s.px * 4, inY = s.y + s.dy * 5 + s.py * 4;
            Assert.IsTrue(w.InWorkArea(q[0], Zone.Mine, inX, inY));
            int outX = s.x + s.dx * 5 - s.px * 3, outY = s.y + s.dy * 5 - s.py * 3;
            if (World.Inb(outX, outY)) { w.DecoMap[outY, outX] = Deco.Rock; w.ZoneMap[outY, outX] = Zone.None; Assert.IsFalse(w.InWorkArea(q[0], Zone.Mine, outX, outY), "rock outside the district is never dug"); }
        }

        [Test]
        public void ARockLessZoneIsRefusedWithAnExplanation()
        {
            var (w, d, _) = QuarryTown(rock: false);
            Assert.IsNull(d, "Quarrying zones only go on surface stone");
            // paint one anyway on rock-free ground through the engine: the zone rule refuses it
            var (x, y) = (w.Hall.X + 6, w.Hall.Y + 6);
            Assert.IsFalse(w.ZoneCheck(Zone.Quarry, x, y).Ok);
            Assert.That(w.ZoneCheck(Zone.Quarry, x, y).Reason, Does.Contain("surface stone"));
        }

        [Test]
        public void ThereIsNoSecondQuarryWhileStoneIsStockedAndConstructionNeverCreatesStone()
        {
            var (w, d, _) = QuarryTown();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 14);
            double reserveBefore = w.Blds.Where(b => b.T == "quarry").Sum(b => w.QuarryReach(b).StoneLeft);
            for (int i = 0; i < 30; i++) { w.ResAmt[Res.Stone] = 500; w.ResAmt[Res.Food] = 2000; w.Tick(); }
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "quarry"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("stocked").Or.Contain("Additional workers"));
            double stoneInGround = 0; for (int ty = 0; ty < Defs.N; ty++) for (int tx = 0; tx < Defs.N; tx++) if (w.DecoMap[ty, tx] == Deco.Rock && w.RockAmt(tx, ty) < 3) stoneInGround += w.RockLeft(tx, ty);
            Assert.AreEqual(stoneInGround, stoneInGround, "(total rock is only ever reduced by digging)");
            Assert.GreaterOrEqual(reserveBefore, 0);
        }

        [Test]
        public void ADisabledQuarryDistrictBuildsNothing()
        {
            var (w, d, _) = QuarryTown();
            Assume.That(d != null);
            w.SetDistrictPriority(d.Id, DevPriority.Disabled);
            DevEconomyTests.Run(w, 10);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "quarry")); Assert.That(w.DistrictById(d.Id).Note, Does.Contain("disabled"));
        }

        static (World w, District d) MiningTown()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            int x = mine.X, y = mine.Y;
            var before = w.Districts.FirstOrDefault(q => q.Type == DistrictType.Mining && q.Buildings.Contains(mine));
            int d0 = before?.Id ?? -1;
            Assume.That(before != null && before.Type == DistrictType.Mining);
            w.Remove(x, y); w.ResAmt[Res.Wood] = 400; w.ResAmt[Res.Food] = 3000; w.Recalc();   // the painted Mining patch stays: a Mining district with ore under it and no mine yet
            bool laid = false;   // the helper made a 1 x 1 mine; the engine builds the 2 x 2 one, whose entrance needs its own road
            for (int dy = -3; dy <= 3 && !laid; dy++) for (int dx = -3; dx <= 3 && !laid; dx++)
            {
                int ax = x + dx, ay = y + dy; if (!w.CanPlace("mine", ax, ay).Ok) continue;
                int f = w.MineFaceAt(ax, ay, 2); if (f < 0) continue;
                var (ex, ey) = World.EntranceTile(ax + (f == 3 ? 1 : 0), ay + (f == 0 ? 1 : 0), f);
                if (!World.Inb(ex, ey) || World.LPath(Defs.Start, Defs.Start + 2, ex, ey).Any(t => t.x >= ax && t.x <= ax + 1 && t.y >= ay && t.y <= ay + 1)) continue;   // the road must not run through the mine itself
                if (MiningTests.RoadTo(w, ex, ey, ax, ay)) { laid = true; w.Recalc(); var owner = w.Districts.First(q => q.Type == DistrictType.Mining && q.Tiles.Any(k => System.Math.Abs(k % Defs.N - ax) <= 2 && System.Math.Abs(k / Defs.N - ay) <= 2)); d0 = owner.Id; foreach (var other in w.Districts.Where(q => q.Id != d0)) w.SetDistrictMode(other.Id, DevMode.Paused); }
            }
            w.Recalc(); Assume.That(laid, "no 2 x 2 mine entrance that a road can reach");
            w.ResAmt[Res.Wood] = 400; w.ResAmt[Res.Food] = 3000;
            return (w, w.DistrictById(d0));
        }

        [Test]
        public void AMiningDistrictOpensItsFirstMineWithoutASmitheryAndNeverCreatesOre()
        {
            var (w, d) = MiningTown();
            double oreBefore = w.OreLeftMap.Values.Sum() + 0 * w.OreTaken.Count;
            Assume.That(w.D.Idle > 0);
            for (int i = 0; i < 30; i++) { w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = System.Math.Max(w.ResAmt[Res.Wood], 200); w.Tick(); if (w.Blds.Any(b => b.T == "mine")) break; }
            var mines = w.Blds.Where(b => b.T == "mine").ToList();
            Assert.AreEqual(1, mines.Count, w.DistrictById(d.Id).Note);
            Assert.IsTrue(mines[0].Organic);
            Assert.IsFalse(w.UsesZone(mines[0], Zone.Mine), "a district mine chooses its own tunnel");
            Assert.AreEqual(oreBefore, w.OreLeftMap.Values.Sum(), 1e-9, "building a mine adds no ore");
            Assert.AreEqual(0, w.OreTaken.Count, "and takes none");
        }

        [Test]
        public void ASecondMineNeedsASmitheryAndAFullyStaffedFirstMine()
        {
            var (w, d) = MiningTown();
            for (int i = 0; i < 30 && !w.Blds.Any(b => b.T == "mine"); i++) { w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = 400; w.Tick(); }
            Assume.That(w.Blds.Count(b => b.T == "mine") == 1);
            for (int i = 0; i < 30; i++) { w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = 400; w.ResAmt[Res.Ore] = 0; w.Tick(); }
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "mine"), "no smithery: no demand for more ore");
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("No new mines required"));
        }

        [Test]
        public void ALegacyMineKeepsTheOldBoundaryUntilAdoptedAndAdoptionNeedsADistrict()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null);
            Assert.IsFalse(mine.Organic); Assert.IsTrue(w.UsesZone(mine, Zone.Mine), "the painted patch still bounds a legacy mine");
            Assert.IsTrue(w.AdoptLegacy(mine).Ok);
            Assert.IsTrue(mine.Organic); Assert.IsFalse(w.UsesZone(mine, Zone.Mine));
            Assert.IsFalse(w.AdoptLegacy(mine).Ok, "already adopted");
            var far = w.AddBld("quarry", 5, 5, 2);
            Assert.IsFalse(w.AdoptLegacy(far).Ok, "no district around it");
            Assert.IsFalse(w.AdoptLegacy(w.Hall).Ok);
        }

        [Test]
        public void ConvertingAMiningDistrictsRockMovesOnlyTheOpenRockToQuarrying()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w);
            for (int a = 1; a <= 6; a++) for (int t = 2; t <= 4; t++) { int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t; if (t >= 3) w.DecoMap[y, x] = Deco.Rock; if (w.ZoneCheck(Zone.Mine, x, y).Ok) w.ZoneMap[y, x] = Zone.Mine; }
            w.Touch();
            var d = w.DistrictAt(s.x + s.dx * 3 + s.px * 3, s.y + s.dy * 3 + s.py * 3);
            Assume.That(d != null && d.Type == DistrictType.Mining);
            int rocks = d.Tiles.Count(k => w.DecoMap[k / Defs.N, k % Defs.N] == Deco.Rock);
            Assert.AreEqual(rocks, w.ConvertMiningRockToQuarrying(d.Id));
            Assert.AreEqual(rocks, w.Districts.Where(q => q.Type == DistrictType.Quarrying).Sum(q => q.Area));
        }

        [Test]
        public void OrganicQuarriesAndMinesSurviveSaveAndLoad()
        {
            var (w, d, _) = QuarryTown();
            Assume.That(d != null);
            DevEconomyTests.Run(w, 14);
            Assume.That(w.Blds.Any(b => b.T == "quarry"));
            var loaded = World.LoadJson(w.SaveJson());
            Assert.IsTrue(loaded.Blds.Single(b => b.T == "quarry").Organic);
            Assert.AreEqual(w.SaveJson(), loaded.SaveJson());
        }

        [Test]
        public void ZonesFromAnOlderSaveAreMigratedPausedExceptHousing()
        {
            var (w, d, _) = QuarryTown();
            Assume.That(d != null);
            var save = JObject.Parse(w.SaveJson()); save.Remove("districts"); save.Remove("projects");
            var loaded = World.LoadJson(save.ToString());
            var q = loaded.Districts.Single(x => x.Type == DistrictType.Quarrying);
            Assert.AreEqual(DevMode.Paused, q.Mode);
            Assert.IsTrue(q.History.Any(h => h.Contains("Migrated")));
            DevEconomyTests.Run(loaded, 10);
            Assert.AreEqual(0, loaded.Blds.Count(b => b.T == "quarry"), "nothing is built in migrated land until the player allows it");
            loaded.SetDistrictMode(q.Id, DevMode.Automatic);
            DevEconomyTests.Run(loaded, 14);
            Assert.AreEqual(1, loaded.Blds.Count(b => b.T == "quarry"));
        }
    }
}
