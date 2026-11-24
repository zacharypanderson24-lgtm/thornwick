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

            // the hunters take the whole reserve, plus the little that grows back (one food every HuntingRecoveryDays) while they are at it
            Assert.That(collected, Is.InRange(World.ForestFoodReserve, World.ForestFoodReserve + 24 / World.HuntingRecoveryDays + 1));
            Assert.That(world.DecoMap[y, forestX], Is.EqualTo(Deco.Tree));
            int left = world.HuntFoodRemaining(forestX, y);
            Assert.That(left, Is.LessThan(World.HuntingLoad), "less than one load is left");
            var loaded = World.LoadJson(world.SaveJson());
            Assert.That(loaded.Grid[y, x].T, Is.EqualTo("hunter"));
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.EqualTo(left), "the depletion is saved");

            loaded.Day += World.HuntingRecoveryDays * 3;
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.EqualTo(left + 3), "it grows back one food every HuntingRecoveryDays");

            loaded.Day = firstDelivery + World.HuntingRecoveryDays * (World.ForestFoodReserve + 24 / World.HuntingRecoveryDays + 1);   // long enough for all of it to grow back
            loaded = World.LoadJson(loaded.SaveJson());
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.EqualTo(World.ForestFoodReserve));
        }
    }
}
