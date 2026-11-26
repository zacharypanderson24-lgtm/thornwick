// The placement forecast (Sim/World.Forecast.cs): what a building would make right where the cursor is, in food or goods a day.
using System;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ForecastTests
    {
        static readonly int C = Defs.Start;

        /// <summary>Flat open ground round the lodge site, no trees anywhere near it.</summary>
        static World Clearing()
        {
            var w = new World(7);
            Array.Clear(w.Hv, 0, w.Hv.Length);
            for (int y = C - 24; y <= C + 32; y++)   // wider than any lodge's reach (16 tiles) from any site the tests use
                for (int x = C - 34; x <= C + 32; x++)
                    if (w.Grid[y, x] == null) { w.DecoMap[y, x] = Deco.None; w.ZoneMap[y, x] = Zone.None; }
            w.Touch();
            return w;
        }

        [Test]
        public void AHuntingLodgeSaysHowMuchFoodAndHowManyVillagersItFeeds()
        {
            var w = Clearing();
            int x = C + 1, y = C + 4;
            for (int dy = -1; dy <= 4; dy++) w.DecoMap[y + dy, x + 4] = Deco.Tree;   // a stand of trees four tiles east
            var f = w.ForecastPlacement("hunter", x, y);
            Assert.Greater(f.FoodPerDay, 0.5, f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("food a day") && l.Contains("feeds")), f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("steps away")), f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("regrowing")), "it says how fast the game grows back: " + f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("Town food: eats")), "and what it does to the town's food: " + f.Text);
        }

        [Test]
        public void MovingTheCursorChangesTheAnswerNearerGameMakesMoreFood()
        {
            var w = Clearing();
            for (int dy = -1; dy <= 4; dy++) w.DecoMap[C + 4 + dy, C + 6] = Deco.Tree;
            var near = w.ForecastPlacement("hunter", C + 1, C + 4);     // game 4 tiles away
            var far = w.ForecastPlacement("hunter", C - 9, C + 4);      // game about 14 tiles away
            Assert.Greater(near.FoodPerDay, far.FoodPerDay, near.Text + "\n---\n" + far.Text);
        }

        [Test]
        public void NoGameInReachIsAWarningWithNoFood()
        {
            var w = Clearing();
            var f = w.ForecastPlacement("hunter", C + 1, C + 4);
            Assert.AreEqual(0, f.FoodPerDay);
            Assert.IsTrue(f.Warn);
            StringAssert.Contains("no game in reach", f.Text);
        }

        [Test]
        public void ThePerHunterFoodMatchesTheTripTime()
        {
            var w = Clearing();
            int x = C + 1, y = C + 4;
            w.DecoMap[y, x + 5] = Deco.Tree;   // one tile of game
            var f = w.ForecastPlacement("hunter", x, y);
            // a hunter's trip is 2 x steps + WorkT + 1 days for HuntingLoad food; two hunters (the lodge's jobs) share the one tile
            var steps = f.Lines.First(l => l.Contains("steps away") || l.Contains("step away"));
            Assert.Greater(f.FoodPerDay, 0);
            Assert.LessOrEqual(f.FoodPerDay, 2.0 * World.HuntingLoad / (2 + World.WorkT + 1), steps);
        }

        [Test]
        public void AFarmhouseSaysHowManyFieldsItWorksAndHowManyVillagersTheyFeed()
        {
            var w = new World(7);
            // somewhere away from the starting farm with plenty of fertile ground in reach
            (int x, int y)? site = null;
            for (int y = 8; y < Defs.N - 12 && site == null; y += 3)
                for (int x = 8; x < Defs.N - 12 && site == null; x += 3)
                {
                    if (Math.Abs(x - C) < 30 && Math.Abs(y - C) < 30) continue;
                    if (!w.Claimed(x, y)) continue;
                    int fertile = 0;
                    for (int j = y - 5; j <= y + 6; j++) for (int i = x - 5; i <= x + 6; i++)
                        if (w.Fert[j, i] && w.Grid[j, i] == null && w.DecoMap[j, i] == Deco.None && !w.IsSolid(j == y ? i : i, j)) fertile++;
                    if (fertile >= 40) site = (x, y);
                }
            if (site == null)   // the default map may keep fertile ground inside the starting district: use it, away from the starting farm
                for (int y = C - 28; y < C + 28 && site == null; y += 3)
                    for (int x = C - 28; x < C + 28 && site == null; x += 3)
                    {
                        if (Math.Abs(x - C) < 14 && Math.Abs(y - C) < 14) continue;
                        if (!w.Claimed(x, y)) continue;
                        int fertile = 0;
                        for (int j = y - 5; j <= y + 6; j++) for (int i = x - 5; i <= x + 6; i++)
                            if (w.Fert[j, i] && w.Grid[j, i] == null && w.DecoMap[j, i] == Deco.None && !w.IsSolid(i, j)) fertile++;
                        if (fertile >= 40) site = (x, y);
                    }
            Assume.That(site.HasValue, "fertile ground away from the starting farm on this seed");
            var (sx, sy) = site.Value;
            var empty = w.ForecastPlacement("farm", sx, sy);
            Assert.AreEqual(0, empty.FoodPerDay, "no Farmland zoned yet");
            Assert.IsTrue(empty.Warn);
            StringAssert.Contains("Zone Farmland", empty.Text);

            int zoned = 0;
            for (int j = sy - 5; j <= sy + 6 && zoned < 18; j++) for (int i = sx - 5; i <= sx + 6 && zoned < 18; i++)
                if (w.Fert[j, i] && w.Grid[j, i] == null && w.DecoMap[j, i] == Deco.None && !w.IsSolid(i, j) && w.ZoneMap[j, i] == Zone.None) { w.ZoneMap[j, i] = Zone.Farm; zoned++; }
            w.SetFlaxPercent(0);
            w.Touch();
            var f = w.ForecastPlacement("farm", sx, sy);
            Assert.AreEqual(zoned * World.WheatYield / (double)World.YearLength, f.FoodPerDay, 0.01, f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("feeds")), f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("farmers")), f.Text);
        }

        [Test]
        public void OneFarmersFieldsFeedExactlyTenVillagers()
        {
            Assert.AreEqual(10.0, World.FieldsPerFarmer * World.WheatYield / (double)World.YearLength, 1e-9);
        }

        [Test]
        public void ASiloSaysWhatItStoresAndForHowManyDays()
        {
            var w = new World(7);
            var f = w.ForecastPlacement("silo", C + 2, C + 5);
            Assert.IsTrue(f.Lines.Any(l => l.Contains(World.SiloFood.ToString())), f.Text);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("days of food")), f.Text);
        }

        [Test]
        public void AWoodcutterLodgeSaysHowManyLogsAMarkedForestGives()
        {
            var w = Clearing();
            int x = C + 1, y = C + 4;
            for (int dy = -1; dy <= 4; dy++) for (int dx = 4; dx <= 5; dx++) { w.DecoMap[y + dy, x + dx] = Deco.Tree; w.ZoneMap[y + dy, x + dx] = Zone.Wood; }
            var f = w.ForecastPlacement("wood", x, y);
            Assert.IsTrue(f.Lines.Any(l => l.Contains("logs a day")), f.Text);
            var none = Clearing().ForecastPlacement("wood", x, y);
            Assert.IsTrue(none.Warn);
            StringAssert.Contains("forest", none.Text);
        }

        [Test]
        public void ForecastingChangesNothing()
        {
            var w = Clearing();
            for (int dy = -1; dy <= 4; dy++) w.DecoMap[C + 4 + dy, C + 6] = Deco.Tree;
            string before = w.SaveJson(); int version = w.Version, blds = w.Blds.Count;
            foreach (var type in new[] { "hunter", "farm", "wood", "quarry", "silo", "well", "house" }) w.ForecastPlacement(type, C + 1, C + 4);
            Assert.AreEqual(version, w.Version);
            Assert.AreEqual(blds, w.Blds.Count);
            Assert.AreEqual(before, w.SaveJson());
        }

        [Test]
        public void TypesWithNothingToForecastReturnNoLines()
        {
            var w = new World(7);
            Assert.AreEqual(0, w.ForecastPlacement("well", C + 2, C + 5).Lines.Count);
            Assert.AreEqual(0, w.ForecastPlacement("barracks", C + 2, C + 5).Lines.Count);
        }
    }
}
