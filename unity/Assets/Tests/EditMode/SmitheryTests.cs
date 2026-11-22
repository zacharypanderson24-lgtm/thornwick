using System;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SmitheryTests
    {
        [Test]
        public void RoadLinkedSmitheryRefinesOreAndRespectsStorage()
        {
            var world = new World(7);
            world.Day = 271; // Winter leaves the starting farm idle.
            int x = Defs.Start + 1, y = Defs.Start + 4;
            Array.Clear(world.Hv, 0, world.Hv.Length);
            for (int row = y; row < y + 2; row++)
                for (int column = x; column < x + 2; column++)
                    world.DecoMap[row, column] = Deco.None;

            world.DecoMap[y + 1, x + 1] = Deco.Ore;
            Assert.That(world.Place("smithery", x, y).Ok, Is.False);
            world.DecoMap[y + 1, x + 1] = Deco.None;
            Assert.That(world.Place("smithery", x, y).Ok, Is.True);

            var smithery = world.Grid[y, x];
            Assert.That(smithery.Sz, Is.EqualTo(2));
            Assert.That(world.Grid[y + 1, x + 1], Is.SameAs(smithery));
            Assert.That(smithery.Workers, Is.EqualTo(3));
            world.ResAmt[Res.Ore] = 10;
            world.Tick();
            Assert.That(world.ResAmt[Res.Ore], Is.EqualTo(6));
            Assert.That(world.ResAmt[Res.Metal], Is.EqualTo(2));

            world.ResAmt[Res.Metal] = Defs.Storage - 0.5;
            world.Tick();
            world.Tick();
            Assert.That(world.ResAmt[Res.Metal], Is.EqualTo(Defs.Storage));
            Assert.That(world.ResAmt[Res.Ore], Is.EqualTo(5));

            var loaded = World.LoadJson(world.SaveJson());
            Assert.That(loaded.ResAmt[Res.Metal], Is.EqualTo(Defs.Storage));
            Assert.That(loaded.Grid[y, x].Sz, Is.EqualTo(2));
            loaded.Remove(Defs.Start, Defs.Start + 2);
            loaded.ResAmt[Res.Metal] = 0;
            loaded.Tick();
            Assert.That(loaded.ResAmt[Res.Metal], Is.Zero);
            Assert.That(loaded.ResAmt[Res.Ore], Is.EqualTo(5));
        }

        [Test]
        public void LegacyStockpilesAndHistoryLoadWithoutMetal()
        {
            var world = new World(7);
            world.Tick();
            var legacy = JObject.Parse(world.SaveJson());
            ((JObject)legacy["res"]).Remove("metal");
            // a save from before Metal has history rows that stop just before it (and before everything added after it)
            foreach (JArray row in (JArray)legacy["statistics"]) while (row.Count > 2 + (int)Res.Metal) row.RemoveAt(row.Count - 1);
            var loaded = World.LoadJson(legacy.ToString());
            Assert.That(loaded.ResAmt[Res.Metal], Is.Zero);
            Assert.That(loaded.Statistics[0].Stockpile(Res.Metal), Is.Zero);
            Assert.That(loaded.Statistics[0].Stockpile(Res.Food), Is.EqualTo(world.Statistics[0].Stockpile(Res.Food)), "older columns keep their values");
        }

        [Test]
        public void HistoryFromEverySaveVersionLoads()
        {
            var world = new World(7);
            world.Tick();
            int full = 2 + Enum.GetValues(typeof(Res)).Length;
            // every row length a save has ever written, from before Metal to today (each new resource added one column at the end)
            for (int length = 2 + (int)Res.Metal; length <= full; length++)
            {
                var save = JObject.Parse(world.SaveJson());
                foreach (JArray row in (JArray)save["statistics"]) while (row.Count > length) row.RemoveAt(row.Count - 1);
                var loaded = World.LoadJson(save.ToString());
                Assert.That(loaded.Statistics.Count, Is.EqualTo(world.Statistics.Count), $"rows of {length} load");
                for (int r = length - 2; r < full - 2; r++) Assert.That(loaded.Statistics[0].Stockpile((Res)r), Is.Zero, $"missing column {(Res)r} reads as zero");
            }
            var bad = JObject.Parse(world.SaveJson());
            ((JArray)((JArray)bad["statistics"])[0]).Add(1.0);
            Assert.Throws<System.IO.InvalidDataException>(() => World.LoadJson(bad.ToString()), "a row longer than any save ever wrote is rejected");
        }

        [Test]
        public void SmitheryLoadsAllFourApprovedSprites()
        {
            for (int view = 0; view < 4; view++)
            {
                var name = "smithery_" + (view + 1);
                var sprites = UnityEngine.Resources.LoadAll<UnityEngine.Sprite>("Art/" + name);
                Assert.That(sprites.Length, Is.EqualTo(1));
                var sprite = sprites[0];
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.name, Is.EqualTo(name));
                Assert.That(sprite.rect.width, Is.EqualTo(64));
                Assert.That(sprite.rect.height, Is.EqualTo(88));
            }
        }
    }
}
