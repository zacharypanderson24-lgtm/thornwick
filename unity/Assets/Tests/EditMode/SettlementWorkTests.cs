using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class SettlementWorkTests
    {
        [TestCase(Zone.Pigs, 2)]
        [TestCase(Zone.Sheep, 3)]
        [TestCase(Zone.Chickens, 6)]
        public void PenCapacityUsesEnclosedAreaAndSurvivesReload(Zone species, int capacity)
        {
            var world = new World(7);
            int x = Defs.Start + 1, y = Defs.Start + 4;
            PreparePenSite(world, x, y);
            double wood = world.ResAmt[Res.Wood];
            var placed = world.PlacePen(species, x + 1, y + 2, x, y);
            Assert.That(placed.Ok, Is.True, placed.Reason);
            Assert.That(placed.Count, Is.EqualTo(capacity));
            Assert.That(world.ResAmt[Res.Wood], Is.EqualTo(wood - 10));
            Assert.That(world.CanPlace("charcoal", x + 1, y + 1).Ok, Is.False);
            Assert.That(world.PlacePen(species, x, y, x + 1, y + 2).Ok, Is.False);
            Assert.That(world.WalkCost(x - 1, y, x, y), Is.EqualTo(double.PositiveInfinity));

            var restored = World.LoadJson(world.SaveJson());
            var pen = restored.LivestockPens.Single();
            Assert.That((pen.Species, pen.Count, pen.Capacity), Is.EqualTo((species, 1, capacity)));
            Assert.That(restored.PenAt(x + 1, y + 2), Is.SameAs(pen));
            Assert.That(restored.ClearZoneRect(x + 1, y, x + 1, y), Is.EqualTo(6));
            Assert.That(restored.LivestockPens, Is.Empty);
            Assert.That(restored.ZoneMap[y + 2, x], Is.EqualTo(Zone.None));
        }

        [Test]
        public void ConnectedWorkshopsKeepSuppliesInTheirRespectiveStocks()
        {
            var initial = new World(7) { Peasants = 18 };
            int c = Defs.Start;
            initial.AddBld("storehouse", c + 1, c + 4);
            initial.AddBld("road", c, c + 7);
            initial.AddBld("road", c, c + 8);
            initial.AddBld("smokehouse", c + 1, c + 7);
            initial.AddBld("charcoal", c + 1, c + 8);
            initial.ResAmt[Res.Food] = 100;
            initial.ResAmt[Res.Logs] = 40;
            var saved = JObject.Parse(initial.SaveJson());
            saved["freshMeat"] = 12;
            var world = World.LoadJson(saved.ToString());
            Assert.That(world.CropStorage(Res.Wood), Is.EqualTo(Defs.Storage + World.StorehouseSpace));
            world.Tick();
            Assert.That(world.Blds.First(b => b.T == "storehouse").Workers, Is.EqualTo(2));
            Assert.That(world.ResAmt[Res.Charcoal], Is.EqualTo(2));
            Assert.That(world.ResAmt[Res.SmokedMeat], Is.EqualTo(World.SmokehouseRate));
            Assert.That(world.FreshMeatStored, Is.LessThan(12));
            var legacy = JObject.Parse(world.SaveJson());
            foreach (var row in (JArray)legacy["statistics"])
            {
                ((JArray)row).RemoveAt(row.Count() - 1);
                ((JArray)row).RemoveAt(row.Count() - 1);
            }
            Assert.That(World.LoadJson(legacy.ToString()).Statistics.Last().Stockpile(Res.Charcoal), Is.Zero);
        }

        static void PreparePenSite(World world, int x, int y)
        {
            for (int dy = 0; dy <= 3; dy++)
                for (int dx = 0; dx <= 2; dx++)
                    world.Hv[(y + dy) * (Defs.N + 1) + x + dx] = 0;
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    world.DecoMap[y + dy, x + dx] = Deco.None;
                    world.ZoneMap[y + dy, x + dx] = Zone.None;
                }
        }
    }
}
