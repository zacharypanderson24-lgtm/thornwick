using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TownMenuTests
    {
        [Test]
        public void CropPlanRoundsToTilesAndUsesTheSameFieldsAfterLoad()
        {
            var world = new World(7);
            world.SetFlaxPercent(25);
            Assert.AreEqual((2, 8), world.PlannedCrops());
            Assert.Greater(world.FarmOutput().flax, 0);
            var savedFields = world.FarmFields().Keys.Where(k => world.IsFlaxField(k % Defs.N, k / Defs.N)).ToArray();
            var loaded = World.LoadJson(world.SaveJson());
            Assert.AreEqual(25, loaded.FlaxPercent);
            CollectionAssert.AreEquivalent(savedFields, loaded.FarmFields().Keys.Where(k => loaded.IsFlaxField(k % Defs.N, k / Defs.N)));
            int removed = savedFields[0];
            Assert.IsTrue(world.Remove(removed % Defs.N, removed / Defs.N).Ok);
            Assert.AreEqual((2, 7), world.PlannedCrops());
            Assert.IsFalse(world.IsFlaxField(removed % Defs.N, removed / Defs.N));
            world.SetFlaxPercent(100);
            Assert.AreEqual(0, world.FarmOutput().food);
        }

        [Test]
        public void DailyStatisticsSurviveSaveLoadAndContinueRecording()
        {
            var world = new World(7);
            world.Tick();
            Assert.AreEqual(1, world.Statistics.Count);
            Assert.AreEqual(1, world.Statistics[0].Day);
            Assert.AreEqual(world.Peasants + world.Soldiers, world.Statistics[0].Population);
            double wood = world.Statistics[0].Stockpile(Res.Wood);
            world.ResAmt[Res.Wood] = 25.125;
            var loaded = World.LoadJson(world.SaveJson());
            Assert.AreEqual(wood, loaded.Statistics[0].Stockpile(Res.Wood));
            Assert.AreEqual(25.125, loaded.CurrentStatistics().Stockpile(Res.Wood));
            loaded.Tick();
            Assert.AreEqual(2, loaded.Statistics[1].Day);
        }

        [Test]
        public void MineSettingsClampAndPersist()
        {
            var world = new World(7);
            Assert.AreEqual(World.MaxDepth, world.ShaftDepthLimit);
            world.SetShaftDepthLimit(99);
            Assert.AreEqual(World.MaxDepth, world.ShaftDepthLimit);
            world.SetShaftDepthLimit(-1);
            world.SetMineSupportsRequired(true);
            var loaded = World.LoadJson(world.SaveJson());
            Assert.AreEqual(0, loaded.ShaftDepthLimit);
            Assert.IsTrue(loaded.RequireMineSupports);
        }
    }
}
