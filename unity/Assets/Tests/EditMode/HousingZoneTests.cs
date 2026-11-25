// Level 1 and Level 2 housing zones (Sim/World.Develop.cs): the zone you paint decides which house grows, and what it costs.
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class HousingZoneTests
    {
        static World Crowded(double wood = 1000, double stone = 0)
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = wood; w.ResAmt[Res.Stone] = stone;
            w.Recalc();
            w.Peasants = w.D.Civ;   // full: homes are wanted
            return w;
        }

        /// <summary>Zone a free tile beside a road that reaches the Town Hall. Returns it.</summary>
        static (int x, int y) ZoneBeside(World w, Zone kind)
        {
            foreach (var road in w.Blds.Where(b => b.T == "road" && b.Served).OrderByDescending(b => b.Y).ThenBy(b => b.X))
                for (int k = 0; k < 4; k++)
                {
                    int x = road.X + Defs.Nb[k, 0], y = road.Y + Defs.Nb[k, 1];
                    if (!World.Inb(x, y) || w.Grid[y, x] != null || w.IsSolid(x, y) || w.HasTree(x, y) || !w.Claimed(x, y) || w.ZoneMap[y, x] != Zone.None) continue;
                    if (!w.ZoneCheck(kind, x, y).Ok) continue;
                    w.ZoneMap[y, x] = kind;
                    return (x, y);
                }
            Assert.Fail("no free tile beside a served road");
            return (0, 0);
        }

        [Test]
        public void ALevelOneZoneGrowsACottageForWoodOnly()
        {
            var w = Crowded(wood: 100, stone: 50);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.Develop();
            var home = w.Grid[y, x];
            Assert.IsNotNull(home, w.Note[Zone.Res]);
            Assert.AreEqual("house", home.T);
            Assert.AreEqual(1, home.Tier);
            Assert.AreEqual(1, home.Lvl);
            Assert.AreEqual(Defs.HouseCap[1], home.Capacity);
            Assert.AreEqual(100 - Defs.Cost["house"][Res.Wood], w.ResAmt[Res.Wood], 1e-9);
            Assert.AreEqual(50, w.ResAmt[Res.Stone], 1e-9, "a cottage costs no stone");
        }

        [Test]
        public void ALevelTwoZoneGrowsAStoneHouseForWoodAndStone()
        {
            var w = Crowded(wood: 100, stone: 50);
            var (x, y) = ZoneBeside(w, Zone.Res2);
            w.Develop();
            var home = w.Grid[y, x];
            Assert.IsNotNull(home, w.Note[Zone.Res2]);
            Assert.AreEqual(2, home.Tier);
            Assert.AreEqual(2, home.Lvl);
            Assert.AreEqual(Defs.HouseCap[2], home.Capacity);
            Assert.AreEqual(100 - Defs.House2Cost[Res.Wood], w.ResAmt[Res.Wood], 1e-9);
            Assert.AreEqual(50 - Defs.House2Cost[Res.Stone], w.ResAmt[Res.Stone], 1e-9);
        }

        [Test]
        public void ALevelTwoZoneWaitsForStoneAndSaysSo()
        {
            var w = Crowded(wood: 100, stone: 0);
            var (x, y) = ZoneBeside(w, Zone.Res2);
            w.Develop();
            Assert.IsNull(w.Grid[y, x], "no stone, no stone house");
            StringAssert.Contains($"{Defs.House2Cost[Res.Stone]} stone", w.Note[Zone.Res2]);
            w.ResAmt[Res.Stone] = 20;
            w.Develop();
            Assert.IsNotNull(w.Grid[y, x], "it grows as soon as the stone is there");
        }

        [Test]
        public void WithBothZonesTheTownBuildsWhatItCanPay()
        {
            var w = Crowded(wood: 100, stone: 0);
            var (x1, y1) = ZoneBeside(w, Zone.Res);
            var (x2, y2) = ZoneBeside(w, Zone.Res2);
            w.Develop();
            Assert.IsNotNull(w.Grid[y1, x1], "the cottage zone grows while the stone house waits");
            Assert.IsNull(w.Grid[y2, x2]);
            Assert.AreEqual(1, w.Grid[y1, x1].Tier);
        }

        [Test]
        public void WaterDoesNotChangeAHousesLevel()
        {
            var w = Crowded();
            var house = w.Blds.First(b => b.T == "house" && b.Served);
            Assert.AreEqual(2, house.Tier, "the starting home is a level 2 house");
            house.ShortDays = WaterTuning.Grace + 3;
            w.Recalc();
            Assert.AreEqual(2, house.Lvl, "a parched home keeps its level");
            Assert.AreEqual(house.Capacity / 2, house.Housing, "but holds half its people: water is needed to survive");
        }

        [Test]
        public void ACottageStaysLevelOneEvenWithWater()
        {
            var w = Crowded(wood: 100);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.Develop(); w.Recalc();
            var home = w.Grid[y, x];
            Assert.IsTrue(home.Served);
            Assert.AreEqual(1, home.Lvl, "being served and watered does not raise it");
        }

        [Test]
        public void TheTierIsSavedAndOlderSavesKeepTheirLevelTwoHouses()
        {
            var w = Crowded(wood: 100, stone: 50);
            var (x, y) = ZoneBeside(w, Zone.Res);
            w.Develop();
            var cottage = w.Grid[y, x];
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, back.Grid[cottage.Y, cottage.X].Tier);
            Assert.AreEqual(2, back.Blds.First(b => b.T == "house" && b != back.Grid[cottage.Y, cottage.X]).Tier);

            var o = JObject.Parse(w.SaveJson());
            foreach (var b in (JArray)o["blds"]) if ((string)b["t"] == "house") ((JObject)b).Remove("tier");
            var old = World.LoadJson(o.ToString());
            Assert.IsTrue(old.Blds.Where(b => b.T == "house").All(b => b.Tier == 2), "an older save's houses were level 2 when served");
        }

        [Test]
        public void ALevelTwoZoneIsSavedAndCannotBePaintedOverABuiltTile()
        {
            var w = Crowded(wood: 100, stone: 50);
            var (x, y) = ZoneBeside(w, Zone.Res2);
            Assert.AreEqual(Zone.Res2, World.LoadJson(w.SaveJson()).ZoneMap[y, x]);
            w.Develop();
            Assert.IsFalse(w.ZoneCheck(Zone.Res, x, y).Ok, "a built tile cannot be re-zoned");
        }

        [Test]
        public void ClassicStyleKeepsTheBrowsersHouseLevelRule()
        {
            var w = new World(7, TerrainStyle.Classic);
            var house = w.Blds.First(b => b.T == "house" && b.Served);
            w.Recalc();
            Assert.AreEqual(2, house.Lvl);
            house.ShortDays = WaterTuning.Grace + 1;
            w.Recalc();
            Assert.AreEqual(1, house.Lvl, "in Classic a parched house is level 1, as in the browser game");
        }
    }
}
