using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TradeTests
    {
        [Test]
        public void MarketIsPlaceableButDoesNotHireOrTrade()
        {
            var world = new World(7);
            int c = Defs.Start;
            world.Peasants = 30;
            Assert.That(world.SetZone(Zone.Comm, c + 1, c + 4).Ok, Is.False);
            Assert.That(world.Place("market", c + 1, c + 4).Ok, Is.True);
            var market = world.Grid[c + 4, c + 1];
            Assert.That(market.Sz, Is.EqualTo(2));
            Assert.That(world.Grid[c + 5, c + 2], Is.SameAs(market));
            Assert.That(world.ResAmt[Res.Wood], Is.EqualTo(88));

            world.ResAmt[Res.Food] = 120;
            world.ResAmt[Res.Flax] = 1;
            world.Tick();
            Assert.That(market.Workers, Is.Zero);
            Assert.That(market.Jobs, Is.Zero);
            Assert.That(world.ResAmt[Res.Flax], Is.EqualTo(1));
        }

        [Test]
        public void RemovingASiloPreservesStockAlreadyInside()
        {
            var world = new World(7);
            int c = Defs.Start;
            Assert.That(world.Place("silo", c + 1, c + 4).Ok, Is.True);
            Assert.That(world.Grid[c + 4, c + 1].Sz, Is.EqualTo(1));
            Assert.That(world.CropStorage(Res.Food), Is.EqualTo(1300 + World.SiloFood), "the town's own store plus one silo");
            Assert.That(world.CropStorage(Res.Flax), Is.EqualTo(90));
            world.ResAmt[Res.Flax] = 65;
            Assert.That(world.Remove(c + 1, c + 4).Ok, Is.True);
            Assert.That(world.CropStorage(Res.Flax), Is.EqualTo(30));
            world.Tick();
            Assert.That(World.LoadJson(world.SaveJson()).ResAmt[Res.Flax], Is.EqualTo(65));
        }
    }
}
