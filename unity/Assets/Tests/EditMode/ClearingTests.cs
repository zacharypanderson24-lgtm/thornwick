// Universal land clearing (World.Clearing.cs): designated obstacles are cleared by idle villagers over real days, resources are recovered once,
// buildings and roads through woodland queue their clearing and place themselves, and none of it touches buildings, mountains or protected woodland.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ClearingTests
    {
        const int N = Defs.N;
        static readonly int C = Defs.Start;

        /// <summary>The test town's roads and Town Hall, with no zones and no producers, plenty of idle villagers and a full larder.</summary>
        static World Bare(int peasants = 60)
        {
            var w = new World(7); w.ClaimAll(); w.Drafting = false;
            SimParityTests.Layout(w, 10);
            foreach (var old in w.Blds.Where(o => o.T != "road" && o.T != "hall").ToList()) w.Remove(old.X, old.Y);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) w.ZoneMap[y, x] = Zone.None;
            w.Peasants = peasants; w.ResAmt[Res.Wood] = 100; w.ResAmt[Res.Logs] = 0; w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Gold] = 100;
            w.Touch(); w.Recalc();
            return w;
        }

        static void Step(World w, int days) { for (int d = 0; d < days; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Gold] = 100; w.Tick(); } }

        /// <summary>Free, open tiles that touch a road (so a villager can reach them).</summary>
        static List<(int x, int y)> BesideRoad(World w, int count)
        {
            var list = new List<(int x, int y)>();
            for (int y = C - 12; y <= C + 12 && list.Count < count; y++)
                for (int x = C - 12; x <= C + 12 && list.Count < count; x++)
                {
                    if (w.Grid[y, x] != null || w.IsSolid(x, y) || w.DecoMap[y, x] != Deco.None || w.HasTree(x, y)) continue;
                    bool road = Enumerable.Range(0, 4).Any(k => w.Grid[y + Defs.Nb[k, 1], x + Defs.Nb[k, 0]]?.T == "road");
                    if (road) list.Add((x, y));
                }
            Assume.That(list.Count >= count, "not enough open ground beside the roads");
            return list;
        }

        static void Plant(World w, IEnumerable<(int x, int y)> tiles, Deco d = Deco.Tree) { foreach (var (x, y) in tiles) w.DecoMap[y, x] = d; w.Touch(); }

        [Test]
        public void ADesignatedTreeIsClearedOverDaysAndItsLogsRecoveredExactlyOnce()
        {
            var w = Bare(); var t = BesideRoad(w, 1);
            Plant(w, t);
            double wood0 = w.ResAmt[Res.Wood];
            Assert.IsTrue(w.DesignateClearing(t[0].x, t[0].y).Ok);
            Step(w, 2);
            Assert.AreEqual(Deco.Tree, w.DecoMap[t[0].y, t[0].x], "a tree takes several worker-days: it is not gone after two");
            Assert.Greater(w.ClearProgress.Count, 0, "work has started");
            Step(w, 12);
            Assert.AreEqual(Deco.None, w.DecoMap[t[0].y, t[0].x]);
            Assert.AreEqual(0, w.ClearOrders.Count);
            Assert.AreEqual(World.LogsFromClearing, w.ClearedLogs);
            // every recovered log is either still in the store or went through the Town Hall's saw (1 log -> half a plank), never both
            Assert.AreEqual(World.LogsFromClearing, w.ResAmt[Res.Logs] + 2 * (w.ResAmt[Res.Wood] - wood0), 1e-6, "logs conserved: recovered once");
        }

        [Test]
        public void BuildingsAndMountainsCannotBeCleared()
        {
            var w = Bare();
            Assert.IsFalse(w.DesignateClearing(w.Hall.X, w.Hall.Y).Ok);
            StringAssert.Contains("building", w.WhyNotClearable(w.Hall.X, w.Hall.Y));
            var mtn = Enumerable.Range(0, N * N).Select(k => (x: k % N, y: k / N)).FirstOrDefault(t => w.IsMountain(t.x, t.y) && w.DecoMap[t.y, t.x] == Deco.Rock);
            Assume.That(mtn != default, "no mountain");
            StringAssert.Contains("mountain", w.WhyNotClearable(mtn.x, mtn.y));
            Assert.AreEqual(Obstacle.None, w.ObstacleAt(mtn.x, mtn.y));
            var open = BesideRoad(w, 1)[0];
            StringAssert.Contains("Nothing to clear", w.WhyNotClearable(open.x, open.y));
        }

        [Test]
        public void TheNumberOfIdleVillagersLimitsHowFastLandIsCleared()
        {
            var w = Bare(2);
            var t = BesideRoad(w, 6); Plant(w, t);
            foreach (var (x, y) in t) w.DesignateClearing(x, y);
            w.Recalc();
            Assume.That(w.D.Idle >= 1 && w.D.Idle <= 3, "idle villagers: " + w.D.Idle);
            Step(w, 6);
            Assert.LessOrEqual(w.ClearWorkersToday, w.D.Idle);
            Assert.LessOrEqual(w.ClearedTiles, 2, "two workers cannot clear six trees in six days");
            Step(w, 60);
            Assert.AreEqual(6, w.ClearedTiles, "they get there in the end");
        }

        [Test]
        public void ClearingOrdersCanBeCancelledAndKeepTheTrees()
        {
            var w = Bare(); var t = BesideRoad(w, 5); Plant(w, t);
            foreach (var (x, y) in t) w.DesignateClearing(x, y);
            Step(w, 2);
            Assert.AreEqual(5, w.CancelClearingRect(t.Min(a => a.x), t.Min(a => a.y), t.Max(a => a.x), t.Max(a => a.y)));
            Assert.AreEqual(0, w.ClearOrders.Count); Assert.AreEqual(0, w.ClearProgress.Count);
            Assert.AreEqual(5, t.Count(a => w.DecoMap[a.y, a.x] == Deco.Tree), "cancelled: the trees stand");
        }

        [Test]
        public void AnUnreachableOrderIsDroppedInsteadOfHoldingWorkersForEver()
        {
            var w = Bare();
            var far = (x: C + 45, y: C);   // farther from every road than a worker will walk (World.ClearReachSteps)
            w.DecoMap[far.y, far.x] = Deco.Tree; w.Touch();
            Assume.That(w.Claimed(far.x, far.y));
            Assert.IsTrue(w.DesignateClearing(far.x, far.y).Ok);
            w.Events.Clear();
            bool told = false;
            for (int d = 0; d < World.StuckDays + 2; d++) { Step(w, 1); told |= w.Events.Any(e => e.Contains("workers cannot reach")); }   // the log is trimmed, so look each day
            Assert.AreEqual(0, w.ClearOrders.Count, "dropped");
            Assert.IsTrue(told, "the player was told why");
            Assert.AreEqual(Deco.Tree, w.DecoMap[far.y, far.x]);
            Assert.AreEqual(0, w.ClearedTiles);
        }

        [Test]
        public void ProtectedTreesNeedConfirmationInADraggedRectangle()
        {
            var w = Bare(); var t = BesideRoad(w, 4); Plant(w, t);
            w.ProtectWoodland(t[0].x, t[0].y);
            var r = w.ClearRect(t.Min(a => a.x), t.Min(a => a.y), t.Max(a => a.x), t.Max(a => a.y));
            Assert.AreEqual(1, r.protectedSkipped); Assert.AreEqual(3, r.ordered);
            Assert.IsFalse(w.ClearOrders.Contains(World.Tk(t[0].x, t[0].y)));
            var r2 = w.ClearRect(t[0].x, t[0].y, t[0].x, t[0].y, confirmProtected: true);
            Assert.AreEqual(1, r2.ordered); Assert.IsFalse(w.IsProtected(t[0].x, t[0].y));
        }

        [Test]
        public void AnOutcropRockIsClearedForStone()
        {
            var w = Bare();
            var rock = BesideRoad(w, 1)[0];
            w.DecoMap[rock.y, rock.x] = Deco.Rock; w.RockAmtMap[World.Tk(rock.x, rock.y)] = 1; w.Touch();
            Assume.That(w.ObstacleAt(rock.x, rock.y) == Obstacle.Rock, "not a removable outcrop");
            double left = w.RockLeft(rock.x, rock.y), stone0 = w.ResAmt[Res.Stone];
            Assert.IsTrue(w.DesignateClearing(rock.x, rock.y).Ok);
            Step(w, 20);
            Assert.AreEqual(Deco.None, w.DecoMap[rock.y, rock.x]);
            Assert.AreEqual(Math.Min(World.RockRecovery, left), w.ResAmt[Res.Stone] - stone0, 1e-6);
            Assert.AreEqual(0, w.RockLeft(rock.x, rock.y), "the rock is gone for good: a quarry cannot take it again");
        }

        [Test]
        public void ABuildingOnWoodlandQueuesItsClearingAndPlacesItselfWhenTheGroundIsReady()
        {
            var w = Bare(); var t = BesideRoad(w, 1); Plant(w, t);
            var plan = w.PlanPlacement("house", t[0].x, t[0].y);
            Assert.AreEqual(PlaceState.ValidAfterClearing, plan.State); StringAssert.Contains("after clearing", plan.Summary);
            double wood0 = w.ResAmt[Res.Wood];
            Assert.IsTrue(w.PlaceWithClearing("house", t[0].x, t[0].y).Ok);
            Assert.AreEqual(wood0, w.ResAmt[Res.Wood], "nothing is paid until the house is placed");
            Assert.AreEqual(1, w.Pending.Count);
            Assert.IsFalse(w.PlaceWithClearing("house", t[0].x, t[0].y).Ok, "no conflicting second plan on the same ground");
            Step(w, 14);
            Assert.AreEqual("house", w.Grid[t[0].y, t[0].x]?.T, "placed once the tree was gone");
            Assert.AreEqual(0, w.Pending.Count);
            Assert.GreaterOrEqual(wood0 - w.ResAmt[Res.Wood], Defs.Cost["house"][Res.Wood] - 0.5 * 14 - 1e-6, "the house's wood was spent");
        }

        [Test]
        public void ABuildingWaitsIfTheTownCannotPayYet()
        {
            var w = Bare(); var t = BesideRoad(w, 1); Plant(w, t);
            w.ResAmt[Res.Wood] = Defs.Cost["house"][Res.Wood];   // exactly enough to plan, then spent elsewhere
            Assert.IsTrue(w.PlaceWithClearing("house", t[0].x, t[0].y).Ok);
            w.ResAmt[Res.Wood] = 0;
            for (int d = 0; d < 14; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Wood] = 0; w.Tick(); }
            Assert.IsNull(w.Grid[t[0].y, t[0].x], "no wood, no house");
            Assert.AreEqual(1, w.Pending.Count, "it keeps waiting rather than being dropped");
            w.ResAmt[Res.Wood] = 50; Step(w, 2);
            Assert.AreEqual("house", w.Grid[t[0].y, t[0].x]?.T);
        }

        [Test]
        public void ProtectedWoodlandBlocksAPlanUntilConfirmed()
        {
            var w = Bare(); var t = BesideRoad(w, 1); Plant(w, t);
            w.ProtectWoodland(t[0].x, t[0].y);
            var plan = w.PlanPlacement("house", t[0].x, t[0].y);
            Assert.AreEqual(1, plan.ProtectedTiles.Count);
            var no = w.PlaceWithClearing("house", t[0].x, t[0].y);
            Assert.IsFalse(no.Ok); StringAssert.Contains("protected", no.Reason);
            Assert.AreEqual(0, w.Pending.Count); Assert.AreEqual(0, w.ClearOrders.Count);
            Assert.IsTrue(w.PlaceWithClearing("house", t[0].x, t[0].y, confirmProtected: true).Ok);
        }

        [Test]
        public void ARoadThroughWoodlandIsPlacedAfterTheClearing()
        {
            var w = Bare();
            var spot = Enumerable.Range(0, 400).Select(i => (x: C + 12 + i % 20, y: C - 5 + i / 20)).First(s => Enumerable.Range(0, 5).All(k => w.Grid[s.y, s.x + k] == null && !w.IsSolid(s.x + k, s.y) && w.DecoMap[s.y, s.x + k] == Deco.None));
            var path = Enumerable.Range(0, 3).Select(k => (x: spot.x + k, y: spot.y)).ToList();
            Plant(w, path);
            Assert.IsFalse(w.RoadPathCheck(path).Ok, "trees block a road");
            double wood0 = w.ResAmt[Res.Wood];
            Assert.IsTrue(w.PlaceRoadWithClearing(path).Ok);
            Assert.AreEqual(0, path.Count(p => w.Grid[p.y, p.x] != null), "nothing is laid before the clearing");
            Step(w, 30);
            Assert.AreEqual(3, path.Count(p => w.Grid[p.y, p.x]?.T == "road"), "all three road tiles were laid");
            Assert.AreEqual(0, w.Pending.Count);
        }

        [Test]
        public void OrdersProgressAndPlansSurviveSaveAndLoad()
        {
            var w = Bare(); var t = BesideRoad(w, 3); Plant(w, t);
            w.DesignateClearing(t[0].x, t[0].y); w.DesignateClearing(t[1].x, t[1].y);
            Assert.IsTrue(w.PlaceWithClearing("house", t[2].x, t[2].y).Ok);
            Step(w, 3);
            var loaded = World.LoadJson(w.SaveJson());
            CollectionAssert.AreEquivalent(w.ClearOrders, loaded.ClearOrders);
            CollectionAssert.AreEquivalent(w.ClearProgress, loaded.ClearProgress);
            Assert.AreEqual(w.Pending.Count, loaded.Pending.Count);
            Assert.AreEqual(w.Pending[0].Type, loaded.Pending[0].Type);
            Assert.AreEqual((w.ClearedTiles, w.ClearedLogs, w.ClearedStone), (loaded.ClearedTiles, loaded.ClearedLogs, loaded.ClearedStone));
            Assert.AreEqual(loaded.SaveJson(), World.LoadJson(loaded.SaveJson()).SaveJson(), "stable");
        }
    }
}
