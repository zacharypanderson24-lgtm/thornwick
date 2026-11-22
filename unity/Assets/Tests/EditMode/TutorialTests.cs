using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TutorialTests
    {
        [Test]
        public void RequiredStepsCannotBeSkippedAndClosingKeepsProgress()
        {
            var w = new World();
            var t = new Tutorial();
            t.Open(w);
            t.Next(w);
            Assert.That(t.Current.Id, Is.EqualTo("hunt"));
            t.Next(w);
            Assert.That(t.Current.Id, Is.EqualTo("hunt"));
            Assert.That(t.Check(w), Is.Null);
            t.Close();
            Assert.That(t.Active, Is.False);
            t.Open(w);
            Assert.That(t.Current.Id, Is.EqualTo("hunt"));
            Assert.That(t.FirstRaid, Is.Null);
        }

        [TestCase(7, Relief.Upland)]
        [TestCase(61, Relief.Lowland)]
        [TestCase(127, Relief.Highland)]
        [TestCase(12, Relief.Alpine)]
        public void TutorialUsesRealDeliveriesAndBothRaidsWithoutStarvation(int seed, Relief relief)
        {
            var w = new World(seed, TerrainStyle.Ranges, relief);
            var t = new Tutorial();
            t.Open(w);
            t.Next(w);
            Build(t, w, "hunter");
            RunTo(t, w, "farmland", 28);
            PaintZone(t, w, Zone.Farm);
            Assert.That(t.Check(w)?.Id, Is.EqualTo("farmland"));
            var siloSites = t.SuggestedTiles(w).ToArray();
            Assert.That(siloSites.Length, Is.EqualTo(Tutorial.Silos));
            foreach (var (x, y) in siloSites) Assert.That(w.Place("silo", x, y).Ok, Is.True);
            Assert.That(t.Check(w)?.Id, Is.EqualTo("silos"));
            Assert.That(t.Check(w), Is.Null, "rations do not count as harvesting wheat");
            RunTo(t, w, "mining", 160);
            Assert.That(Tutorial.FoodDays(w), Is.GreaterThanOrEqualTo(Tutorial.ReserveDays));
            Build(t, w, "quarry");
            PaintZone(t, w, Zone.Mine);
            RunTo(t, w, "lumber", 40);
            Build(t, w, "wood");
            PaintZone(t, w, Zone.Wood);
            RunTo(t, w, "militia", 40);
            Assert.That(w.Raid, Is.Null);

            w.MilitiaFights = false;
            t.Next(w);
            Assert.That(w.Raid, Is.Null, "turn the militia on before the first raid");
            w.MilitiaFights = true;
            t.Next(w);
            var first = t.FirstRaid;
            Assert.That(first, Is.SameAs(w.Raid));
            Assert.That(first.Defenders, Is.Zero);
            t.Close();
            t.Open(w);
            t.Next(w);
            Assert.That(w.Raid, Is.SameAs(first), "reopening cannot repeat the attack");
            RunTo(t, w, "barracks", 180);
            Assert.That(first.Over, Is.True);
            Assert.That(first.Tools, Is.Not.Empty, $"militia never met the {first.From} band");
            Assert.That(t.ReturnRaid, Is.Null);
            t.Next(w);
            Assert.That(t.Current.Id, Is.EqualTo("barracks"), "a garrison is required");
            Build(t, w, "barracks");
            RunTo(t, w, "return", 40);
            Assert.That(w.Soldiers, Is.GreaterThanOrEqualTo(Tutorial.GarrisonSize));
            Assert.That(t.ReturnRaid, Is.Null, "players decide when to start the return raid");
            t.Next(w);
            Assert.That(t.ReturnRaid, Is.Not.SameAs(first));
            Assert.That(t.ReturnRaid.Defenders, Is.GreaterThanOrEqualTo(Tutorial.GarrisonSize));
            RunTo(t, w, "finish", 180);
            Assert.That(t.ReturnRaid.Over, Is.True);
            Assert.That(Tutorial.FoodDays(w), Is.GreaterThan(30));
            t.Next(w);
            Assert.That(t.Finished, Is.True);
            Assert.That(t.Active, Is.False);
            var loaded = World.LoadJson(w.SaveJson());
            Assert.That(loaded.LastHuntingDeliveryDay, Is.Positive);
            Assert.That(loaded.LastWheatHarvestDay, Is.Positive);
            Assert.That(loaded.LastStoneDeliveryDay, Is.Positive);
            Assert.That(loaded.LastLogDeliveryDay, Is.Positive);
        }

        static void Build(Tutorial tutorial, World world, string type)
        {
            var tiles = tutorial.SuggestedTiles(world).ToArray();
            Assert.That(tiles.Length, Is.EqualTo(4), $"highlighted {type} lot on day {world.Day}");
            var (x, y) = tiles[0];
            var result = world.Place(type, x, y);
            Assert.That(result.Ok, Is.True, result.Reason);
            Assert.That(world.Grid[y, x].Served, Is.True, type);
        }

        static void PaintZone(Tutorial tutorial, World world, Zone zone)
        {
            var tiles = tutorial.SuggestedTiles(world).ToArray();
            Assert.That(tiles, Is.Not.Empty, zone.ToString());
            foreach (var (x, y) in tiles) Assert.That(world.SetZone(zone, x, y).Ok, Is.True, zone.ToString());
        }

        static void RunTo(Tutorial t, World w, string step, int budget)
        {
            while (t.Current.Id != step && budget-- > 0)
            {
                w.Tick();
                Assert.That(w.Starving, Is.False, $"day {w.Day} in {t.Current.Id}");
                t.Check(w);
            }
            Assert.That(t.Current.Id, Is.EqualTo(step),
                $"day {w.Day}, food {w.ResAmt[Res.Food]:0}, wood {w.ResAmt[Res.Wood]:0}, stone {w.ResAmt[Res.Stone]:0}, " +
                $"ore {w.ResAmt[Res.Ore]:0}, gold {w.ResAmt[Res.Gold]:0}, peasants {w.Peasants}, soldiers {w.Soldiers}");
        }
    }
}
