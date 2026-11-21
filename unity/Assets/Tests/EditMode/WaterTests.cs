// Drinking water (Sim/World.Water.cs): aquifers shared by their wells, depletion and recovery, delivery along connected roads, the
// reasons a home goes short, the grace period before residents leave, saves, and the previews for wells and (later) closed gates.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterTests
    {
        static readonly int C = Defs.Start;

        /// <summary>Lay road tiles straight onto the map (no costs or checks), clearing whatever grows there.</summary>
        static void Road(World w, IEnumerable<(int x, int y)> tiles)
        {
            foreach (var (x, y) in tiles)
            {
                if (w.Grid[y, x] != null) continue;
                w.DecoMap[y, x] = Deco.None;
                w.AddBld("road", x, y);
            }
            w.Recalc();
        }

        static Building Put(World w, string type, int x, int y)
        {
            w.DecoMap[y, x] = Deco.None;
            var b = w.AddBld(type, x, y);
            w.Recalc();
            return b;
        }

        /// <summary>A long road east from the end of the starting street, far past the Town Hall's reach, with two wells on one
        /// aquifer at its end and a cistern between them. Returns the wells, the cistern and the aquifer.</summary>
        static (Building w1, Building w2, Building cistern, Aquifer aq) Wellfield(World w)
        {
            // the nearest pair of groundwater tiles (x, y-1), (x+1, y-1) well east of the town, out of the Town Hall's reach
            var site = Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N))
                .Where(t => t.x >= C + 14 && t.x < Defs.N - 9 && t.y > C + 10 && t.y < Defs.N - 3 && w.AquiferAt(t.x, t.y - 1) >= 0
                    && w.AquiferAt(t.x + 1, t.y - 1) == w.AquiferAt(t.x, t.y - 1) && w.Grid[t.y - 1, t.x] == null && w.Grid[t.y - 1, t.x + 1] == null)
                .OrderBy(t => t.x - C + t.y - C).FirstOrDefault();
            Assume.That(site.x > 0, "groundwater east of the town on this seed");
            int x = site.x, y = site.y;
            // the end of the starting street is (C, C+6): south to C+9, east to x-1, south or north to y, then along y past the wells
            Road(w, World.LPath(C, C + 7, C, C + 9));
            Road(w, World.LPath(C + 1, C + 9, x - 1, y));
            Road(w, Enumerable.Range(x, 7).Select(i => (i, y)));
            var w1 = Put(w, "well", x, y - 1);
            var w2 = Put(w, "well", x + 1, y - 1);
            var cistern = Put(w, "cistern", x + 2, y + 1);
            return (w1, w2, cistern, w.Aquifers[w.AquiferAt(x, y - 1)]);
        }

        [Test]
        public void AquifersAreSeededPatchesWithOneNearTheTown()
        {
            var w = new World(7);
            Assert.That(w.Aquifers.Count, Is.GreaterThan(2));
            int wet = w.AquiferMap.Count(a => a >= 0);
            Assert.That(wet / (double)(Defs.N * Defs.N), Is.InRange(0.06, 0.2), "groundwater covers some of the map, not all of it");
            foreach (var a in w.Aquifers)
            {
                Assert.AreEqual(a.Tiles, w.AquiferMap.Count(id => id == a.Id));
                Assert.That(a.Tiles, Is.InRange(WaterTuning.MinAquiferTiles, 250), "patches stay separate and small enough to run down");
                Assert.AreEqual(a.Cap, a.Reserve, "a new aquifer is full");
                Assert.AreEqual(a.Tiles * WaterTuning.RechargePerTile, a.Recharge, 1e-9);
            }
            int near = Enumerable.Range(0, Defs.N * Defs.N).Where(k => w.AquiferMap[k] >= 0)
                .Min(k => Math.Max(Math.Abs(k % Defs.N - C), Math.Abs(k / Defs.N - C)));
            Assert.That(near, Is.LessThanOrEqualTo(16), "there is groundwater within reach of the town");
            CollectionAssert.AreEqual(w.AquiferMap, new World(7).AquiferMap, "the same seed gives the same patches");
        }

        [Test]
        public void TheExampleAquiferLastsSevenAndAHalfDays()
        {
            Assert.AreEqual(7.5, World.DaysLeft(300, 20, 60).Value, 1e-9);
            Assert.IsNull(World.DaysLeft(300, 20, 20), "a draw the recharge covers never empties it");
            Assert.AreEqual("Drawing down", World.Trend(300, 300, 20, 60, false));
            Assert.AreEqual("Refilling", World.Trend(100, 300, 20, 5, false));
            Assert.AreEqual("Stable", World.Trend(300, 300, 20, 5, false));
            Assert.AreEqual("Dry", World.Trend(0, 300, 20, 20, true));
        }

        [Test]
        public void TheOpeningTownIsWateredByTheTownHall()
        {
            var w = new World(7);
            for (int day = 0; day < 30; day++) { w.ResAmt[Res.Food] = 200; w.Tick(); }
            var p = w.LastWater;
            Assert.That(p.Demand, Is.GreaterThan(0));
            Assert.AreEqual(0, p.ShortHomes, "the Hall's spring and cistern cover the starting town");
            Assert.That(p.Demand, Is.LessThanOrEqualTo(WaterTuning.HallSpring));
            foreach (var h in w.Blds.Where(b => b.T == "house")) { Assert.AreEqual(2, h.Lvl, "watered homes are level 2"); Assert.AreEqual(0, h.ShortDays); }
            Assert.AreEqual(WaterTuning.HallCistern, w.Hall.Water, 1e-9, "the Hall's cistern stays full while its spring keeps up");
            Assert.AreEqual(0, p.GroundwaterDraw, "no groundwater is touched without wells");
        }

        [Test]
        public void WellsShareTheirAquiferRunItDownAndItRefills()
        {
            var w = new World(7);
            var (w1, w2, cistern, aq) = Wellfield(w);
            aq.Cap = 300; aq.Reserve = 300; aq.Recharge = 20;
            // the cistern is emptied every morning, so the two wells pump flat out (2 x 30 = 60 a day) to refill it
            var reserves = new List<double>(); var pumped = new List<double>();
            for (int day = 0; day < 9; day++)
            {
                cistern.Water = 0;
                var p = w.StepWater();
                pumped.Add(aq.Draw);
                Assert.That(w1.Pumped, Is.LessThanOrEqualTo(WaterTuning.WellPump + 1e-9), "each well keeps its own pumping limit");
                Assert.That(w2.Pumped, Is.LessThanOrEqualTo(WaterTuning.WellPump + 1e-9));
                Assert.AreEqual(w1.Pumped + w2.Pumped, aq.Draw, 1e-9, "both wells draw on the one shared reserve");
                reserves.Add(aq.Reserve);
            }
            // 60 drawn against 20 recharged: 40 a day out of 300, so it is empty during day 8 (300 / 40 = 7.5 days)
            Assert.AreEqual(260, reserves[0], 1e-6); Assert.AreEqual(220, reserves[1], 1e-6); Assert.AreEqual(20, reserves[6], 1e-6);
            Assert.AreEqual(60, pumped[0], 1e-6, "two wells at their limits");
            Assert.AreEqual(0, reserves[7], 1e-6, "empty on day 8");
            Assert.AreEqual(40, pumped[7], 1e-6, "day 8 gets only what was left plus the recharge");
            Assert.AreEqual(20, pumped[8], 1e-6, "once empty, the wells give only what recharges each day");
            Assert.AreEqual("Dry", aq.Trend);
            // nobody needs the water now (the cistern is topped up and stays full): the spare wells leave the groundwater alone and it refills
            cistern.Water = WaterTuning.CisternCap;
            for (int day = 0; day < 5; day++) w.StepWater();
            Assert.AreEqual(0, aq.Draw, 1e-9, "a full cistern and no demand: the wells do not pump");
            Assert.AreEqual(100, aq.Reserve, 1e-6, "it refills by its recharge each day");
            Assert.AreEqual("Refilling", aq.Trend);
            for (int day = 0; day < 30; day++) w.StepWater();
            Assert.AreEqual(300, aq.Reserve, 1e-6, "but never past its capacity");
            Assert.AreEqual("Stable", aq.Trend);
        }

        /// <summary>Homes along a new road south of the starting street, both sides, so the town drinks more than the Hall's spring gives.</summary>
        static List<Building> Crowd(World w, int count)
        {
            int fromY = C + 8;
            Road(w, World.LPath(C, C + 7, C, fromY + count));
            var homes = new List<Building>();
            for (int y = fromY; y < fromY + count && homes.Count < count; y++)
                foreach (int x in new[] { C - 1, C + 1 }) if (homes.Count < count && w.Grid[y, x] == null) homes.Add(Put(w, "house", x, y));
            w.Recalc();
            w.Peasants = w.D.Civ;
            return homes;
        }

        [Test]
        public void DistanceDoesNotMatterOnlyBeingOnTheNetwork()
        {
            var w = new World(7);
            // a home 40 tiles down the road drinks exactly like one beside the Hall, and one with no road at all is the only one cut off
            Road(w, World.LPath(C, C + 7, C, C + 40));
            var near = Put(w, "house", C + 1, C + 8);
            var distant = Put(w, "house", C + 1, C + 40);
            var cutOff = Put(w, "house", C + 6, C - 6);
            w.Peasants = w.D.Civ;
            var p = w.PlanWater();
            Assert.AreEqual(WaterWhy.Ok, p.Why[near]);
            Assert.AreEqual(WaterWhy.Ok, p.Why[distant], "no radius: any home on a connected road is in the supply");
            Assert.AreEqual(WaterWhy.NoRoad, p.Why[cutOff]);
            Assert.AreEqual(p.Need[near], p.Got[near] + p.Stocked[near], 1e-9);
            Assert.AreEqual(p.Need[distant], p.Got[distant] + p.Stocked[distant], 1e-9);
            Assert.IsFalse(p.OnNetwork.Contains(cutOff));
            Assert.IsTrue(p.OnNetwork.Contains(distant));
        }

        [Test]
        public void ShortSupplyIsSharedEvenlyAfterStorageRunsOut()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 1000;
            var homes = Crowd(w, 6);
            w.Hall.Water = 0;   // nothing in store to make up the difference
            var p = w.PlanWater();
            Assert.That(p.Demand, Is.GreaterThan(WaterTuning.HallSpring));
            Assert.AreEqual(WaterTuning.HallSpring, p.Delivered, 1e-9, "the spring gives everything it can, no more");
            Assert.AreEqual(0, p.FromStore, 1e-9);
            double share = p.Delivered / p.Demand;
            foreach (var h in homes.Concat(new[] { w.Hall }))
            {
                Assert.AreEqual(WaterWhy.NotEnough, p.Why[h]);
                Assert.AreEqual(p.Need[h] * share, p.Got[h], 1e-9, "every home gets the same share");
            }
            Assert.AreEqual(homes.Count + 1 + w.Blds.Count(b => b.T == "house" && !homes.Contains(b)), p.ShortHomes);
            // with the cistern full again, stored water makes up the shortfall for everyone
            w.Hall.Water = WaterTuning.HallCistern;
            var q = w.PlanWater();
            Assert.That(q.FromStore, Is.GreaterThan(0));
            Assert.AreEqual(0, q.ShortHomes, "storage covers the town");
            Assert.That(q.StoreDays.Value, Is.EqualTo(q.Stored / q.FromStore).Within(1e-9), "days the store lasts at the rate it is drawn");
        }

        [Test]
        public void ShortHomesStopGrowthThenLosePeopleAfterTheirGrace()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 1000;
            var homes = Crowd(w, 6);
            w.ZoneMap[C + 7, C + 1] = Zone.Res;
            w.Hall.Water = 0;
            var first = homes[0];
            for (int day = 1; day < WaterTuning.Grace; day++) { w.ResAmt[Res.Food] = 300; w.ResAmt[Res.Wood] = 300; w.Hall.Water = 0; w.Tick(); }
            Assert.AreEqual(WaterTuning.Grace - 1, first.ShortDays);
            Assert.IsFalse(first.Parched);
            StringAssert.Contains("short of water", w.Note[Zone.Res], "house growth stops while homes are short");
            Assert.IsNull(w.Grid[C + 7, C + 1], "no home grows meanwhile");
            int civ = w.D.Civ;
            w.ResAmt[Res.Food] = 300; w.Hall.Water = 0; w.Tick();
            Assert.IsTrue(first.Parched, "past its grace");
            Assert.AreEqual(1, first.Lvl, "a parched home drops to level 1");
            Assert.That(w.D.Civ, Is.LessThan(civ), "it now holds fewer people");
            Assert.AreEqual(first.Capacity / 2, first.Housing);
        }

        [Test]
        public void ACisternAnywhereOnTheNetworkBuildsTheTownsReserve()
        {
            var w = new World(7);
            Road(w, World.LPath(C, C + 7, C, C + 45));
            var cistern = Put(w, "cistern", C + 1, C + 45);   // forty tiles from the Town Hall
            Assert.AreEqual(0, cistern.Water);
            for (int day = 0; day < 3; day++) w.StepWater();
            Assert.That(cistern.Water, Is.GreaterThan(0), "the Hall's spare spring water reaches a cistern however far away");
            var before = w.WaterStored();
            for (int day = 0; day < 40; day++) w.StepWater();
            Assert.AreEqual(WaterTuning.CisternCap, cistern.Water, 1e-6, "supply above demand keeps building the reserve up to capacity");
            Assert.That(w.WaterStored().stored, Is.GreaterThan(before.stored));
            // a cistern with no road is left empty: it is not on the network
            var lone = Put(w, "cistern", C + 8, C - 8);
            for (int day = 0; day < 5; day++) w.StepWater();
            Assert.AreEqual(0, lone.Water);
        }

        [Test]
        public void WellsGoOnlyOnGroundwater()
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 100; w.ResAmt[Res.Stone] = 100;
            var dry = Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N))
                .First(t => w.AquiferAt(t.x, t.y) < 0 && w.CanPlace("cistern", t.x, t.y).Ok);
            StringAssert.Contains("groundwater", w.CanPlace("well", dry.x, dry.y).Reason);
            var wet = Enumerable.Range(0, Defs.N * Defs.N).Select(k => (x: k % Defs.N, y: k / Defs.N))
                .First(t => w.AquiferAt(t.x, t.y) >= 0 && w.CanPlace("cistern", t.x, t.y).Ok);
            Assert.IsTrue(w.CanPlace("well", wet.x, wet.y).Ok, "wells can be built again");
        }

        [Test]
        public void TheWellPreviewSeparatesItsLimitFromWhatTheTownGets()
        {
            var w = new World(7);
            var (w1, w2, cistern, aq) = Wellfield(w);
            // nothing to fill and nobody to serve out there: a new well would pump nothing
            cistern.Water = WaterTuning.CisternCap;
            // a free tile on the same aquifer beside the wellfield road (the road runs along y = w1.Y + 1)
            var spots = from dx in Enumerable.Range(-3, 9) from dy in new[] { 0, 2 }
                        let x = w1.X + dx let y = w1.Y + dy
                        where w.AquiferAt(x, y) == aq.Id && w.Grid[y, x] == null && w.Grid[w1.Y + 1, x]?.T == "road"
                        select (x, y);
            Assume.That(spots.Any(), "a free groundwater tile beside the road");
            var spot = spots.First();
            var f = w.ForecastWell(spot.Item1, spot.Item2);
            Assert.AreEqual(WaterTuning.WellPump, f.MaxPump);
            Assert.AreEqual(0, f.ToHomes + f.ToStorage, 1e-9, "demand met and storage full: no extra water");
            Assert.AreEqual(aq.Id, f.Aquifer);
            // an empty cistern: the preview shows the extra water and the draw it puts on the aquifer
            cistern.Water = 0;
            f = w.ForecastWell(spot.Item1, spot.Item2);
            double three = Math.Min(3 * WaterTuning.WellPump, WaterTuning.CisternCap), two = Math.Min(2 * WaterTuning.WellPump, WaterTuning.CisternCap);
            Assert.AreEqual(three - two, f.ToStorage, 1e-6, "only the extra water the third well adds, not its whole limit");
            Assert.AreEqual(three, f.AqDraw, 1e-6, "the three wells share the fill on one aquifer");
            Assert.AreEqual(f.AqDraw > aq.Recharge ? "Drawing down" : "Stable", f.Trend);
            Assert.AreEqual(World.DaysLeft(aq.Reserve, aq.Recharge, f.AqDraw), f.DaysLeft);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "well" && b.X == spot.Item1 && b.Y == spot.Item2), "a preview builds nothing");
        }

        [Test]
        public void ClosingARoadCutsDeliveriesButNotTheAquiferBeneathIt()
        {
            var w = new World(7);
            var (w1, w2, cistern, aq) = Wellfield(w);
            // homes beside the wellfield road, fed by the wells
            var homes = new List<Building>();
            for (int i = 3; i <= 5; i++) if (w.Grid[w1.Y + 2, w1.X + i] == null) homes.Add(Put(w, "house", w1.X + i, w1.Y + 2));
            Assume.That(homes.Count > 0);
            w.Peasants = w.D.Civ;
            cistern.Water = WaterTuning.CisternCap;
            var before = w.PlanWater();
            Assert.That(homes.All(h => before.Why[h] == WaterWhy.Ok));
            // "close a gate" on the road tile between the wells and those homes
            int gx = w1.X + 2, gy = w1.Y + 1;
            var f = w.ForecastClosure((x, y) => x == gx && y == gy);
            Assert.That(f.HomesLosingService.Count, Is.GreaterThan(0), "homes beyond the closed tile lose the wells' water");
            CollectionAssert.AreEquivalent(homes, f.HomesCutOff, "they are cut off from the network altogether");
            Assert.That(f.GroundwaterAfter, Is.LessThanOrEqualTo(f.GroundwaterBefore));
            Assert.AreEqual(w.PlanWater().Delivered, before.Delivered, 1e-9, "a forecast changes nothing");
            // closing the road near the Town Hall cuts the wells off entirely: the town falls back on its own spring and cisterns
            var g = w.ForecastClosure((x, y) => x == C && y == C + 8);
            Assert.That(before.GroundwaterDraw, Is.GreaterThan(0), "the wells were needed before");
            Assert.AreEqual(0, g.GroundwaterAfter, 1e-9, "wells outside the closed tile pump nothing");
            Assert.That(g.HomesCutOff.Count, Is.GreaterThanOrEqualTo(homes.Count));
            Assert.That(aq.Reserve, Is.EqualTo(aq.Cap), "the aquifer itself is untouched by a forecast");
        }

        [Test]
        public void WaterStateSurvivesSavesAndOldSavesGetDefaults()
        {
            var w = new World(7);
            var (w1, w2, cistern, aq) = Wellfield(w);
            aq.Reserve = aq.Cap / 3; cistern.Water = 42;
            var house = w.Blds.First(b => b.T == "house"); house.ShortDays = 5;
            w.Hall.Water = 77;
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(aq.Cap / 3, loaded.Aquifers[aq.Id].Reserve, 1e-3);
            Assert.AreEqual(42, loaded.Blds.First(b => b.T == "cistern").Water, 1e-9);
            Assert.AreEqual(77, loaded.Hall.Water, 1e-9);
            Assert.AreEqual(5, loaded.Blds.First(b => b.T == "house" && b.X == house.X && b.Y == house.Y).ShortDays);
            CollectionAssert.AreEqual(w.AquiferMap, loaded.AquiferMap);
            Assert.AreEqual(w.SaveJson(), loaded.SaveJson());

            // a save from before water: no aquifers, no stored water, no shortage days
            var old = JObject.Parse(w.SaveJson());
            old.Remove("aquifers");
            foreach (var b in (JArray)old["blds"]) { ((JObject)b).Remove("water"); ((JObject)b).Remove("dry"); }
            var fromOld = World.LoadJson(old.ToString());
            Assert.That(fromOld.Aquifers.All(a => a.Reserve == a.Cap), "aquifers start full");
            Assert.AreEqual(WaterTuning.HallCistern, fromOld.Hall.Water, "the Town Hall's cistern starts full");
            Assert.AreEqual(0, fromOld.Blds.First(b => b.T == "cistern").Water);
            Assert.That(fromOld.Blds.All(b => b.ShortDays == 0));
            Assert.AreEqual(2, fromOld.Blds.Count(b => b.T == "well"), "existing wells still stand and work");
            fromOld.Tick();
        }
    }
}
