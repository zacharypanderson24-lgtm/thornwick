using System;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class HuntingTests
    {
        [Test]
        public void WinterHuntersShareOneFiniteForestReserveAndSaveItsDepletion()
        {
            var world = new World(7);
            int x = Defs.Start + 1, y = Defs.Start + 4;
            int forestX = x + 2;
            Array.Clear(world.Hv, 0, world.Hv.Length);
            for (int row = y - World.HuntingRange - 1; row <= y + World.HuntingRange + 4; row++)
                for (int column = x - World.HuntingRange - 1; column <= x + World.HuntingRange + 2; column++)
                    world.DecoMap[row, column] = Deco.None;

            world.DecoMap[y, forestX] = Deco.Tree;
            world.Day = 271;
            world.ResAmt[Res.Food] = 130;
            Assert.That(world.Place("hunter", x, y).Ok, Is.True);
            Assert.That(world.Place("hunter", x, y + 2).Ok, Is.True);
            Assert.That(world.Grid[y, x].Sz, Is.EqualTo(2));
            Assert.That(world.Grid[y, x].Served && world.Grid[y + 2, x].Served, Is.True);
            Assert.That(world.HuntFoodRemaining(forestX, y), Is.EqualTo(World.ForestFoodReserve));

            double collected = 0;
            int firstDelivery = 0;
            for (int day = 0; day < 24; day++)
            {
                double before = world.ResAmt[Res.Food], meals = world.DailyFoodDemand;
                int date = world.Day;
                world.Tick();
                double delivered = world.ResAmt[Res.Food] - before + meals;
                if (firstDelivery == 0 && delivered > 0.01) firstDelivery = date;
                collected += delivered;
            }

            Assert.That(collected, Is.EqualTo(World.ForestFoodReserve).Within(1e-6));
            Assert.That(world.DecoMap[y, forestX], Is.EqualTo(Deco.Tree));
            Assert.That(world.HuntFoodRemaining(forestX, y), Is.Zero);
            var loaded = World.LoadJson(world.SaveJson());
            Assert.That(loaded.Grid[y, x].T, Is.EqualTo("hunter"));
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.Zero);

            loaded.Day = firstDelivery + World.HuntingRecoveryDays - 1;
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.Zero);
            loaded.Day++;
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.EqualTo(1));

            loaded.Day = firstDelivery + World.HuntingRecoveryDays * World.ForestFoodReserve;
            loaded = World.LoadJson(loaded.SaveJson());
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.EqualTo(World.ForestFoodReserve));
        }
    }
}
