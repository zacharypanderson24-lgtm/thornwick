// Phase 8 of the extraction overhaul: old saves keep working with the new rules. Nothing is erased, nothing refills, painted zones become optional boundaries.
using System;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ExtractionMigrationTests
    {
        const int N = Defs.N;
        static readonly string[] NewWorldKeys = { "protectedWood", "replant", "clearOrders", "clearProgress", "pendingBuilds", "clearedTiles", "clearedLogs", "clearedStone" };

        /// <summary>What a save looked like before this milestone: none of its new fields (the lodge mode, the protection and clearing data).</summary>
        static string Legacy(World w)
        {
            var o = JObject.Parse(w.SaveJson());
            foreach (var k in NewWorldKeys) o.Remove(k);
            foreach (var b in (JArray)o["blds"]) ((JObject)b).Remove("mode");
            return o.ToString();
        }

        [Test]
        public void AnOldSaveWithLumberZonesAndAForesterKeepsItsOldBehaviour()
        {
            var (w, lodge, trees) = ForestryAutoTests.Forest(20);
            foreach (var (x, y) in trees.Take(8)) w.ZoneMap[y, x] = Zone.Wood;   // an old Lumber zone over 8 of the 20 trees
            var spot = Enumerable.Range(0, 40).Select(i => (x: lodge.X + 3 + i % 8, y: lodge.Y + 4 + i / 8)).FirstOrDefault(t => w.CanPlace("forester", t.x, t.y).Ok);
            Assume.That(spot != default, "no room for a forester");
            var forester = w.AddBld("forester", spot.x, spot.y, 2); w.Touch(); w.Recalc();
            var loaded = World.LoadJson(Legacy(w));
            var l = loaded.Grid[lodge.Y, lodge.X];
            Assert.AreEqual(ForestMode.Sustainable, l.Mode, "no mode in an old save: the default");
            Assert.IsTrue(loaded.UsesZone(l, Zone.Wood), "the old Lumber zone is now an optional boundary");
            Assert.AreEqual(0, loaded.ClearOrders.Count + loaded.ProtectedWood.Count + loaded.Replant.Count + loaded.Pending.Count);
            Assert.IsNotNull(loaded.Grid[forester.Y, forester.X], "the forester building is still there");
            for (int d = 0; d < 500; d++) ForestryAutoTests.Day(loaded);
            foreach (var (x, y) in trees.Skip(8)) Assert.AreEqual(Deco.Tree, loaded.DecoMap[y, x], "trees outside the old zone were left alone");
            Assert.Less(trees.Take(8).Count(t => loaded.DecoMap[t.y, t.x] == Deco.Tree), 8, "the zoned trees were still felled");
        }

        [Test]
        public void AnOldMineKeepsItsTunnelItsDepletionAndItsZone()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            for (int d = 0; d < 200; d++) { w.ResAmt[Res.Food] = 300; w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Gold] = 100; w.Tick(); }
            double taken = w.OreTaken.Values.Sum(), reserve = w.MineReserve(mine);
            Assume.That(taken > 0, "the mine took no ore");
            var loaded = World.LoadJson(Legacy(w));
            var m = loaded.Grid[mine.Y, mine.X];
            CollectionAssert.AreEqual(mine.Tunnel, m.Tunnel);
            Assert.AreEqual(taken, loaded.OreTaken.Values.Sum(), 1e-9, "loading does not refill the ground");
            Assert.AreEqual(reserve, loaded.MineReserve(m), 1e-9);
            Assert.IsTrue(loaded.UsesZone(m, Zone.Mine), "the painted Mining zone is still its boundary");
        }

        [Test]
        public void ExistingQuarryPitsSurviveTheNewRules()
        {
            var (w, q) = QuarryAutoTests.QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            for (int d = 0; d < 300; d++) ForestryAutoTests.Day(w);
            double dug = w.Dig.Values.Sum(); Assume.That(dug > 0, "nothing dug");
            var loaded = World.LoadJson(Legacy(w));
            Assert.AreEqual(dug, loaded.Dig.Values.Sum(), 1e-9);
            Assert.AreEqual(w.QuarryReach(q).Pits, loaded.QuarryReach(loaded.Grid[q.Y, q.X]).Pits);
        }

        [Test]
        public void NewSavesRoundTripAndAnOldSaveReSavesInTheNewFormat()
        {
            var (w, lodge, trees) = ForestryAutoTests.Forest(10);
            var old = World.LoadJson(Legacy(w));
            var resaved = old.SaveJson();
            foreach (var k in NewWorldKeys) StringAssert.Contains("\"" + k + "\"", resaved, k);
            Assert.AreEqual(resaved, World.LoadJson(resaved).SaveJson());
        }
    }
}
