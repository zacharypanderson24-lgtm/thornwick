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
        public void LivestockProducesWoolAndEggsAutomaticallyButRequiresSlaughterForMeat()
        {
            int x = Defs.Start + 1, y = Defs.Start + 4;
            foreach (var species in new[] { Zone.Pigs, Zone.Sheep, Zone.Chickens })
            {
                var original = new World(7);
                PreparePenSite(original, x, y);
                Assert.That(original.PlacePen(species, x, y, x + 1, y + 2).Ok, Is.True);
                var seed = JObject.Parse(original.SaveJson());
                seed["res"]["food"] = 100;
                seed["wheatStored"] = 100;
                var world = World.LoadJson(seed.ToString());
                world.Tick();
                Assert.That(world.LivestockPens.Single().Keepers, Is.EqualTo(1), "a villager keeps the pen");
                Assert.That(world.ResAmt[Res.Wool], Is.EqualTo(species == Zone.Sheep ? World.WoolPerSheep : 0));
                for (int i = 1; i < World.DaysPerEgg; i++) world.Tick();   // hens lay every few days
                if (species == Zone.Chickens) Assert.That(world.ResAmt[Res.Eggs], Is.GreaterThanOrEqualTo(World.EggsPerChicken));
                else Assert.That(world.ResAmt[Res.Eggs], Is.Zero);
                Assert.That(world.FreshMeatStored, Is.Zero);
                double eggs = world.ResAmt[Res.Eggs];
                world = World.LoadJson(world.SaveJson());
                for (int i = 0; i < World.DaysPerEgg; i++) world.Tick();
                if (species == Zone.Sheep) Assert.That(world.ResAmt[Res.Wool], Is.EqualTo(World.WoolPerSheep));
                if (species == Zone.Chickens) Assert.That(world.ResAmt[Res.Eggs], Is.GreaterThan(eggs));
                var pen = world.LivestockPens.Single();
                int count = pen.Count;
                var slaughter = world.SlaughterLivestock(pen);
                Assert.That(slaughter.Ok, Is.True, slaughter.Reason);
                Assert.That(slaughter.Count, Is.EqualTo(World.MeatPerAnimal(species)));
                Assert.That(pen.Count, Is.EqualTo(count - 1));
                Assert.That(world.FreshMeatStored, Is.EqualTo(World.MeatPerAnimal(species)));
                while (pen.Count > 0) Assert.That(world.SlaughterLivestock(pen).Ok, Is.True);
                world = World.LoadJson(world.SaveJson());
                pen = world.LivestockPens.Single();
                Assert.That(pen.Count, Is.Zero);
                Assert.That(world.BuyLivestock(pen).Ok, Is.True);
                Assert.That(pen.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void HensLayEveryFewDaysAndOnlyWithAKeeper()
        {
            int x = Defs.Start + 1, y = Defs.Start + 4;
            var original = new World(7);
            PreparePenSite(original, x, y);
            Assert.That(original.PlacePen(Zone.Chickens, x, y, x + 1, y + 2).Ok, Is.True);
            var seed = JObject.Parse(original.SaveJson());
            seed["res"]["food"] = 200;
            seed["wheatStored"] = 200;

            // with a keeper: a hen lays once every DaysPerEgg days
            var kept = World.LoadJson(seed.ToString());
            int days = 4 * World.DaysPerEgg;
            for (int i = 0; i < days; i++) { kept.ResAmt[Res.Food] = 200; kept.Tick(); }
            var pen = kept.LivestockPens.Single();
            Assert.That(pen.Keepers, Is.EqualTo(1));
            Assert.That(kept.ResAmt[Res.Eggs], Is.GreaterThan(0));
            Assert.That(kept.ResAmt[Res.Eggs], Is.LessThanOrEqualTo(pen.Count * (days / World.DaysPerEgg + 1)), "no more than an egg a hen every few days");

            // nobody free to keep it: no eggs and no new hens
            var unkept = World.LoadJson(seed.ToString());
            int hens = unkept.LivestockPens.Single().Count;
            for (int i = 0; i < days; i++) { unkept.Peasants = 0; unkept.ResAmt[Res.Food] = 200; unkept.Tick(); }
            Assert.That(unkept.LivestockPens.Single().Keepers, Is.Zero);
            Assert.That(unkept.ResAmt[Res.Eggs], Is.Zero, "an unkept pen gives nothing");
            Assert.That(unkept.LivestockPens.Single().Count, Is.EqualTo(hens), "and does not breed");
            unkept.Peasants = 0;
            Assert.That(unkept.Recalc().Work["farming"].jobs, Is.GreaterThanOrEqualTo(1), "the keeper shows as a farming job");
        }

        [Test]
        public void ConnectedWorkshopsKeepSuppliesInTheirRespectiveStocks()
        {
            var initial = new World(7) { Peasants = 18 };
            int c = Defs.Start;
            initial.AddBld("storehouse", c + 1, c + 4);
            initial.AddBld("road", c, c + 7);
            initial.AddBld("road", c, c + 8);
            initial.AddBld("road", c, c + 9);
            initial.AddBld("smokehouse", c + 1, c + 7);   // 2 x 2: it covers c+1..c+2, c+7..c+8
            initial.AddBld("charcoal", c + 1, c + 9);     // beside it, not on it
            initial.ResAmt[Res.Food] = 600;               // 18 villagers eat 18 a day
            initial.ResAmt[Res.Logs] = 40;
            var saved = JObject.Parse(initial.SaveJson());
            saved["freshMeat"] = 60;
            var world = World.LoadJson(saved.ToString());
            Assert.That(world.CropStorage(Res.Wood), Is.EqualTo(Defs.Storage + World.StorehouseSpace));
            world.Tick();
            Assert.That(world.Blds.First(b => b.T == "storehouse").Workers, Is.EqualTo(2));
            Assert.That(world.ResAmt[Res.Charcoal], Is.EqualTo(2));
            Assert.That(world.ResAmt[Res.SmokedMeat], Is.EqualTo(World.SmokehouseRate));
            Assert.That(world.FreshMeatStored, Is.LessThan(60));
            var legacy = JObject.Parse(world.SaveJson());
            foreach (var row in ((JArray)legacy["statistics"]).Cast<JArray>())
                while (row.Count > 2 + (int)Res.Charcoal) row.RemoveAt(row.Count - 1);
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
