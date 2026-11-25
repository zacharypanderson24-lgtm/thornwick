// Player-defined ferry routes (World.FerryRoutes.cs): validation, persistence, crew, suspension, capacity, migration.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterFerryRouteTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 9000; w.ResAmt[Res.Stone] = 900; w.ResAmt[Res.Gold] = 900; w.Peasants = 80; return w; }

        static List<(int x, int y)> Spots(World w)
        {
            var list = new List<(int x, int y)>();
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("ferry", x, y).Ok) list.Add((x, y));
            return list;
        }
        static Building Probe((int x, int y) t) => new Building { T = "ferry", X = t.x, Y = t.y, Sz = 1 };

        /// <summary>Two free landing spots on the same water whose sailing route has between minLen and maxLen tiles, at least 3 apart from each other.</summary>
        static ((int x, int y) a, (int x, int y) b, int len)? Pair(World w, int minLen, int maxLen, params (int x, int y)[] avoid)
        {
            var spots = Spots(w);
            foreach (var s in spots)
            {
                if (avoid.Any(v => System.Math.Max(System.Math.Abs(v.x - s.x), System.Math.Abs(v.y - s.y)) < 4)) continue;
                foreach (var t in spots)
                {
                    int d = System.Math.Max(System.Math.Abs(s.x - t.x), System.Math.Abs(s.y - t.y)); if (d < 5 || d > 60) continue;
                    if (avoid.Any(v => System.Math.Max(System.Math.Abs(v.x - t.x), System.Math.Abs(v.y - t.y)) < 4)) continue;
                    var route = w.SailingRoute(Probe(s), Probe(t)); if (route == null) continue;
                    int len = route.Count - 1; if (len >= minLen && len <= maxLen) return (s, t, len);
                }
            }
            return null;
        }

        static (World w, Building a, Building b) Linked(int seed = 1)
        {
            var w = Make(WaterPreset.Lakeside, seed); var p = Pair(w, 6, World.FerryMaxRoute).Value;
            var a = w.AddBld("ferry", p.a.x, p.a.y); var b = w.AddBld("ferry", p.b.x, p.b.y);
            Assert.IsTrue(w.EstablishRoute(a, b).Ok); w.Vessels.Add(new Vessel { Id = 1, Type = VesselType.FerryBoat, Kind = AssignKind.FerryRoute, Key = w.FerryRoutes[0].Id }); return (w, a, b);
        }

        [Test]
        public void ARouteIsMadeOnlyByThePlayerAndCostsGold()
        {
            var w = Make(WaterPreset.Lakeside, 1); var p = Pair(w, 6, World.FerryMaxRoute).Value;
            var a = w.AddBld("ferry", p.a.x, p.a.y); var b = w.AddBld("ferry", p.b.x, p.b.y);
            Assert.AreEqual(0, w.FerryRoutes.Count, "landings do not pair by themselves"); Assert.IsNull(w.RouteOf(a));
            double gold = w.ResAmt[Res.Gold]; var r = w.EstablishRoute(a, b); Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(p.len, r.Count, "the route length is the real sailing length");
            Assert.AreEqual(gold - World.FerryRouteCost, w.ResAmt[Res.Gold], 1e-9); Assert.AreEqual(1, w.FerryRoutes.Count);
            Assert.AreSame(w.RouteOf(a), w.RouteOf(b));
        }

        [Test]
        public void RoutesAreRefusedWithAReasonForTheEveryWrongChoice()
        {
            var (w, a, b) = Linked();
            var again = w.CanRoute(a, b); Assert.IsFalse(again.Ok); StringAssert.Contains("already", again.Reason);
            Assert.IsFalse(w.CanRoute(a, a).Ok);
            var spare = Pair(w, 4, World.FerryMaxRoute, (a.X, a.Y), (b.X, b.Y)); Assert.IsTrue(spare.HasValue);
            var c = w.AddBld("ferry", spare.Value.a.x, spare.Value.a.y);
            var withTaken = w.CanRoute(c, a); Assert.IsFalse(withTaken.Ok); StringAssert.Contains("already", withTaken.Reason);
            Assert.IsFalse(w.CanRoute(null, a).Ok);
            w.ResAmt[Res.Gold] = 1; var d = w.AddBld("ferry", spare.Value.b.x, spare.Value.b.y);
            var poor = w.CanRoute(c, d); Assert.IsFalse(poor.Ok); StringAssert.Contains("gold", poor.Reason);
        }

        [Test]
        public void TheSailingLengthNotTheStraightDistanceDecidesTheRange()
        {
            var w = Make(WaterPreset.Lakeside, 1);
            var longPair = Pair(w, World.FerryMaxRoute + 1, 400);
            Assert.IsTrue(longPair.HasValue, "a lake long enough to test the limit");
            var a = w.AddBld("ferry", longPair.Value.a.x, longPair.Value.a.y); var b = w.AddBld("ferry", longPair.Value.b.x, longPair.Value.b.y);
            var r = w.CanRoute(a, b); Assert.IsFalse(r.Ok); StringAssert.Contains("longer than a ferry can run", r.Reason);
            var shortPair = Pair(w, 6, World.FerryMaxRoute, (a.X, a.Y), (b.X, b.Y)); Assert.IsTrue(shortPair.HasValue);
            var c = w.AddBld("ferry", shortPair.Value.a.x, shortPair.Value.a.y); var d = w.AddBld("ferry", shortPair.Value.b.x, shortPair.Value.b.y);
            Assert.IsTrue(w.CanRoute(c, d).Ok);
        }

        [Test]
        public void LandingsOnDifferentWatersCannotBeJoined()
        {
            var w = Make(WaterPreset.Lakeside, 1); var spots = Spots(w);
            (int x, int y)? s1 = null, s2 = null;
            foreach (var s in spots) { int r = w.RegionOf(Probe(s)); if (s1 == null) { s1 = s; continue; } if (r != 0 && r != w.RegionOf(Probe(s1.Value))) { s2 = s; break; } }
            if (s2 == null) Assert.Ignore("this map has only one body of boat water");
            var a = w.AddBld("ferry", s1.Value.x, s1.Value.y); var b = w.AddBld("ferry", s2.Value.x, s2.Value.y);
            var r2 = w.CanRoute(a, b); Assert.IsFalse(r2.Ok); StringAssert.Contains("Different water bodies", r2.Reason);
        }

        [Test]
        public void AThirdLandingNeverChangesAnExistingRoute()
        {
            var (w, a, b) = Linked(); var before = w.FerryRoutes.Select(r => (r.Id, r.AX, r.AY, r.BX, r.BY)).ToList();
            var spare = Pair(w, 4, World.FerryMaxRoute, (a.X, a.Y), (b.X, b.Y)).Value;
            var c = w.AddBld("ferry", spare.a.x, spare.a.y); var d = w.AddBld("ferry", spare.b.x, spare.b.y);
            CollectionAssert.AreEqual(before, w.FerryRoutes.Select(r => (r.Id, r.AX, r.AY, r.BX, r.BY)).ToList(), "adding landings changes nothing");
            Assert.IsNull(w.RouteOf(c)); Assert.AreSame(b, w.FerryPartner(a));
            Assert.IsTrue(w.EstablishRoute(c, d).Ok); Assert.AreEqual(2, w.FerryRoutes.Count); Assert.AreSame(b, w.FerryPartner(a));
        }

        [Test]
        public void ADestroyedLandingDisablesTheRouteAndRebuildingItBringsTheSameRouteBack()
        {
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0]; int id = route.Id; w.FerryRoutes[0].GraceUntil = int.MaxValue;
            Assert.AreEqual(FerryState.Operational, w.StateOf(route));
            int ax = a.X, ay = a.Y; w.Blds.Remove(a); w.Grid[ay, ax] = null;
            Assert.AreEqual(FerryState.MissingLanding, w.StateOf(route)); Assert.AreEqual(0, w.FerryPairs().Count);
            w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry));
            var rebuilt = w.AddBld("ferry", ax, ay);
            Assert.AreEqual(1, w.FerryRoutes.Count, "no duplicate route appears"); Assert.AreEqual(id, w.RouteOf(rebuilt).Id);
            Assert.AreEqual(FerryState.Operational, w.StateOf(route));
        }

        [Test]
        public void ARouteNeedsACrewAndSuspendsAtWillWithoutOne()
        {
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0];
            Assert.AreEqual(FerryState.WaitingForCrew, w.StateOf(route), "a new route has no ferrymen yet");
            StringAssert.Contains("2 ferrymen", World.StateText(FerryState.WaitingForCrew));
            a.Workers = World.FerryCrew; Assert.AreEqual(FerryState.Operational, w.StateOf(route));
            a.Workers = 1; Assert.AreEqual(FerryState.WaitingForCrew, w.StateOf(route), "one is not enough");
            a.Workers = 2; w.SetRouteSuspended(route, true); a.Workers = 2;
            Assert.AreEqual(FerryState.SuspendedByPlayer, w.StateOf(route)); w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry), "no ferry boat on a suspended route");
            w.SetRouteSuspended(route, false); a.Workers = 2; Assert.AreEqual(FerryState.Operational, w.StateOf(route));
            Assert.AreEqual(2, a.Jobs, "a landing employs 2 ferrymen"); Assert.IsTrue(Defs.Trades.Contains("ferrying"));
        }

        [Test]
        public void ACrewedRouteJoinsTheRoadsAndAnUncrewedOneDoesNot()
        {
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0];
            (int x, int y) Beside(Building l) { foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int x = l.X + dx, y = l.Y + dy; if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null) return (x, y); } Assert.Fail("no land"); return (0, 0); }
            var na = Beside(a); var nb = Beside(b);
            foreach (var (x, y) in World.LPath(C, C + 6, na.x, na.y)) { if (w.Grid[y, x] != null && w.Grid[y, x].T != "road") { w.Blds.Remove(w.Grid[y, x]); w.Grid[y, x] = null; } if (w.Grid[y, x] == null) w.AddBld("road", x, y); }
            w.AddBld("road", nb.x, nb.y);
            w.Recalc(); w.Recalc(); w.Recalc();
            Assert.IsTrue(w.Grid[na.y, na.x].Served, "the near landing is on the Town Hall's road network");
            Assert.AreEqual(World.FerryCrew, a.Workers, "so it is staffed from the town's workforce");
            Assert.AreEqual(FerryState.Operational, w.StateOf(route));
            Assert.IsTrue(w.Grid[nb.y, nb.x].Served, "and the crewed ferry joins the far road");
            // no villagers to crew it: the link goes
            w.Peasants = 0; w.Recalc(); w.Recalc();
            Assert.AreEqual(FerryState.WaitingForCrew, w.StateOf(route)); Assert.IsFalse(w.Grid[nb.y, nb.x].Served, "an uncrewed ferry joins nothing");
        }

        [Test]
        public void CapacityFallsWithTheRouteLengthAndTheLoadIsWhatDependsOnTheFerry()
        {
            Assert.GreaterOrEqual(World.TripsPerDay(1), World.TripsPerDay(10)); Assert.GreaterOrEqual(World.TripsPerDay(10), World.TripsPerDay(20));
            Assert.AreEqual(World.FerryPassengers * World.TripsPerDay(8), World.PassengerCapacityPerDay(8));
            Assert.AreEqual(World.FerryCargo * World.TripsPerDay(8), World.CargoCapacityPerDay(8));
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0]; route.GraceUntil = int.MaxValue;
            Assert.AreEqual(1.0, w.RouteService(route, 8, 0), "nothing depends on it, nothing is crowded");
            Assert.Less(w.RouteService(route, 8, 1000), 1.0);
            var load = w.RouteLoad(route); Assert.AreEqual(0, load.buildings, "no building is served only by this ferry yet");
        }

        [Test]
        public void RoutesAreSavedAndLoadedExactlyAndKeepTheirState()
        {
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0]; route.Suspended = true; route.GraceUntil = 77; route.MilitaryAllowed = false;
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, back.FerryRoutes.Count); var r = back.FerryRoutes[0];
            Assert.AreEqual(route.Id, r.Id); Assert.AreEqual((route.AX, route.AY, route.BX, route.BY), (r.AX, r.AY, r.BX, r.BY));
            Assert.IsTrue(r.Suspended); Assert.AreEqual(77, r.GraceUntil); Assert.IsFalse(r.MilitaryAllowed);
            Assert.AreEqual(0, back.Events.Count(e => e.Contains("Ferries now have routes")), "a save that already has routes is not migrated");
            Assert.AreEqual(w.SaveJson(), back.SaveJson(), "nothing drifts across a round trip");
        }

        [Test]
        public void SavesFromBeforeRoutesKeepTheirFerriesWithAGracePeriod()
        {
            var (w, a, b) = Linked();
            var json = JObject.Parse(w.SaveJson()); json.Remove("ferryRoutes");
            var back = World.LoadJson(json.ToString());
            Assert.AreEqual(1, back.FerryRoutes.Count, "the old pair became a route"); var r = back.FerryRoutes[0];
            Assert.AreEqual((a.X, a.Y), (r.AX, r.AY)); Assert.AreEqual((b.X, b.Y), (r.BX, r.BY));
            Assert.AreEqual(back.Day + World.FerryMigrationGraceDays, r.GraceUntil);
            Assert.AreEqual(FerryState.Operational, back.StateOf(r), "it keeps running while the town finds a crew");
            Assert.IsTrue(back.Events.Any(e => e.Contains("Ferries now have routes")), "and the player is told");
            back.Day = r.GraceUntil + 1; Assert.AreNotEqual(FerryState.Operational, back.StateOf(r), "after the grace period it needs its crew");
            Assert.AreEqual(1, World.LoadJson(back.SaveJson()).FerryRoutes.Count);
        }

        [Test]
        public void TheFerryBoatSailsOnlyWhileItsRouteRuns()
        {
            var (w, a, b) = Linked(); var route = w.FerryRoutes[0];
            w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry), "no crew, no boat");
            route.GraceUntil = int.MaxValue; w.StepBoats();
            Assert.AreEqual(1, w.Boats.Count(x => x.Ferry)); Assert.AreEqual(route.Id, w.Boats.Single(x => x.Ferry).RouteId);
            w.RemoveRoute(route); w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry), "a removed route has no boat");
            Assert.IsNull(w.RouteOf(a));
        }

        [Test]
        public void DryWorldsHaveNoRoutes()
        {
            var w = new World(7);
            Assert.AreEqual(0, w.FerryRoutes.Count); Assert.IsFalse(w.CanRoute(null, null).Ok); Assert.AreEqual(0, w.MigrateAutomaticFerries());
        }
    }
}
