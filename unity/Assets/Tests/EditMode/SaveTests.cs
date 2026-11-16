using System;
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
            original.Hall.CrewAcc = 2.5;

            var loaded = World.LoadJson(original.SaveJson());
            Assert.That(loaded.Grid[loaded.Hall.Y, loaded.Hall.X], Is.SameAs(loaded.Hall));
            Assert.That(loaded.SaveJson(), Is.EqualTo(original.SaveJson()));
            original.Tick();
            loaded.Tick();
            Assert.That(loaded.SaveJson(), Is.EqualTo(original.SaveJson()));
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
