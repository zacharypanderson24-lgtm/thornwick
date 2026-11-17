using System;
using System.Linq;
using System.IO;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SaveTests
    {
        [Test]
        public void SettlementContinuesAfterRoundTrip()
        {
            var original = new World(1234);
            for (int day = 0; day < 20; day++) original.Tick();
            original.Drafting = false;
            Array.Reverse(original.Prio);
            original.Chop[World.Tk(2, 3)] = 2;
            original.Dig[World.Tk(4, 5)] = 1.25;

            var loaded = World.LoadJson(original.SaveJson());
            Assert.That(loaded.Grid[loaded.Hall.Y, loaded.Hall.X], Is.SameAs(loaded.Hall));
            Assert.That(loaded.SaveJson(), Is.EqualTo(original.SaveJson()));
            original.Tick();
            loaded.Tick();
            Assert.That(loaded.SaveJson(), Is.EqualTo(original.SaveJson()));
        }

        [Test]
        public void TerrainStyleReliefAndMiningSurviveARoundTrip()
        {
            var original = new World(5, TerrainStyle.Ranges, Relief.Highland);
            // a mine part-way through its work, some surveyed ground and ore already taken
            var site = System.Linq.Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N))
                .First(t => original.CanPlace("mine", t.x, t.y).Ok && Math.Abs(t.x - Defs.Start) > 8);
            var mine = original.AddBld("mine", site.x, site.y); original.Recalc();
            mine.Adit = true; mine.AditShored = true; mine.Depth = 2; mine.Shored = 1; mine.DigProg = 7; mine.Closed = 4; mine.CaveIns = 1;
            var rich = System.Linq.Enumerable.Range(0, Defs.N * Defs.N).First(k => original.OreAt(k % Defs.N, k / Defs.N, 3) > 10);
            original.Surveyed[rich] = true; original.Surveyed[rich + 1] = true;
            original.OreTaken[3 * Defs.N * Defs.N + rich] = 6.5;
            original.MinerDeaths = 1; original.CaveIns = 1;

            var loaded = World.LoadJson(original.SaveJson());
            Assert.AreEqual(TerrainStyle.Ranges, loaded.Style);
            Assert.AreEqual(Relief.Highland, loaded.Relief);
            Assert.That(loaded.SaveJson(), Is.EqualTo(original.SaveJson()), "save, load, save gives the same file");
            var m = loaded.Grid[site.y, site.x];
            Assert.That((m.Adit, m.AditShored, m.Depth, m.Shored, m.DigProg, m.Closed, m.CaveIns), Is.EqualTo((true, true, 2, 1, 7, 4, 1)));
            Assert.IsTrue(loaded.IsSurveyed(rich % Defs.N, rich / Defs.N));
            for (int k = 0; k < Defs.N * Defs.N; k++)
                for (int band = 1; band <= World.Bands; band++)
                    Assert.AreEqual(original.OreAt(k % Defs.N, k / Defs.N, band), loaded.OreAt(k % Defs.N, k / Defs.N, band), $"deposit at tile {k} band {band}");
            Assert.AreEqual(original.NeedShaft(mine, rich % Defs.N, rich / Defs.N, 3), loaded.NeedShaft(m, rich % Defs.N, rich / Defs.N, 3), "mining heights are the generated ones, not the levelled ground");
        }

        [TestCase(40, false)]
        [TestCase(64, true)]
        public void OldBrowserMapIsCenteredAndUpgraded(int size, bool hasHeights)
        {
            string Tiles(string cell) => "[" + string.Join(",", System.Linq.Enumerable.Repeat(cell, size)) + "]";
            string Grid(string cell) => Tiles(Tiles(cell));
            string heights = hasHeights
                ? ",\"hv\":[" + string.Join(",", System.Linq.Enumerable.Repeat("2", (size + 1) * (size + 1))) + "]"
                : "";
            string json = "{\"day\":37,\"res\":{\"food\":90},\"deco\":" +
                Grid("\"tree\"") + ",\"zone\":" + Grid("null") + ",\"fert\":" + Grid("false") + heights +
                ",\"chop\":{\"0\":2},\"blds\":[{\"t\":\"hall\",\"x\":10,\"y\":11,\"sz\":3}," +
                "{\"t\":\"wood\",\"x\":17,\"y\":17}]}";

            var world = World.LoadJson(json);
            int offset = (Defs.N - size) / 2;
            Assert.That(world.Day, Is.EqualTo(37));
            Assert.That(world.Hall.X, Is.EqualTo(offset + 10));
            Assert.That(world.Hall.Y, Is.EqualTo(offset + 11));
            Assert.That(world.Chop[World.Tk(offset, offset)], Is.EqualTo(2));
            Assert.That(world.Vh(offset + 12, offset + 12), Is.EqualTo(hasHeights ? 2 : 0));
            Assert.That(world.ZoneMap[offset + 17, offset + 18], Is.EqualTo(Zone.Wood));
            Assert.That(world.ResAmt[Res.Logs], Is.Zero);
        }

        [Test]
        public void RejectsAnIncompleteSave()
        {
            Assert.Throws<InvalidDataException>(() => World.LoadJson("{\"v\":1,\"blds\":[],\"deco\":[]}"));
        }
    }
}
