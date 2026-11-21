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
            int x = Defs.Start + 1, y = Defs.Start + 5;
            int forestX = x + 2;
            Array.Clear(world.Hv, 0, world.Hv.Length);
            for (int row = y - World.HuntingRange; row <= y + World.HuntingRange + 1; row++)
                for (int column = x - World.HuntingRange; column <= x + World.HuntingRange; column++)
                    world.DecoMap[row, column] = Deco.None;

            world.DecoMap[y, forestX] = Deco.Tree;
            world.Day = 271;
            world.ResAmt[Res.Food] = 130;
            Assert.That(world.Place("hunter", x, y).Ok, Is.True);
            Assert.That(world.Place("hunter", x, y + 1).Ok, Is.True);
            Assert.That(world.Grid[y, x].Served && world.Grid[y + 1, x].Served, Is.True);
            Assert.That(world.HuntFoodRemaining(forestX, y), Is.EqualTo(World.ForestFoodReserve));

            double collected = 0;
            for (int day = 0; day < 24; day++)
            {
                double before = world.ResAmt[Res.Food], meals = world.DailyFoodDemand;
                world.Tick();
                collected += world.ResAmt[Res.Food] - before + meals;
            }

            Assert.That(collected, Is.EqualTo(World.ForestFoodReserve).Within(1e-6));
            Assert.That(world.DecoMap[y, forestX], Is.EqualTo(Deco.Tree));
            Assert.That(world.HuntFoodRemaining(forestX, y), Is.Zero);
            var loaded = World.LoadJson(world.SaveJson());
            Assert.That(loaded.Grid[y, x].T, Is.EqualTo("hunter"));
            Assert.That(loaded.HuntFoodRemaining(forestX, y), Is.Zero);
        }
    }
}
