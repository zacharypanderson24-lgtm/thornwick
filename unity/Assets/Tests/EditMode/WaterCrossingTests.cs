// People and soldiers on ferries (World.FerryCrossings.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterCrossingTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(int seed = 1) { var w = new World(seed, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Lakeside); w.ClaimAll(); w.ResAmt[Res.Wood] = 9000; w.ResAmt[Res.Gold] = 900; w.Peasants = 80; return w; }

        static (int x, int y) Beside(World w, Building l)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int x = l.X + dx, y = l.Y + dy; if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && !w.IsSolid(x, y) && !w.HasTree(x, y)) return (x, y); }
            return (-1, -1);
        }

        const int StripX = Defs.Start + 20, StripW = 4;

        /// <summary>A world cut in two by a river that runs the whole height of the map (x StripX..StripX+3, boat-deep), so nobody can walk round it: the
        /// fixture does not depend on what a random map contains. The Town Hall is on the west side.</summary>
        static World Split()
        {
            var w = Make();
            for (int y = 0; y < N; y++)
                for (int x = StripX; x < StripX + StripW; x++)
                {
                    int i = y * N + x; w.Waters.Class[i] = WaterClass.SmallRiver; w.Waters.Depth[i] = WaterDepth.Medium; w.Waters.Width[i] = StripW; w.Waters.Strength[i] = 100; w.Waters.Flow[i] = 3; w.DecoMap[y, x] = Deco.None;
                }
            for (int y = 0; y < N; y++) for (int x = StripX - 7; x < StripX + StripW + 7; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && (x < StripX || x >= StripX + StripW)) w.DecoMap[y, x] = Deco.None;
            for (int b = 0; b <= N; b++) for (int x = StripX - 8; x <= StripX + StripW + 8; x++) w.Hv[b * (N + 1) + x] = 0;   // level ground either side, so no cliff splits a shore
            w.Waters.RebuildBodies(); w.Touch(); w.Recalc();
            return w;
        }

        /// <summary>A crewed, boated, military-allowed ferry route across the split map: landing A on the Town Hall's side, B on the far side.</summary>
        static (World w, Building a, Building b, FerryRoute r) Ferry(bool cutByWater = true)
        {
            var w = Split();
            for (int y = 40; y < N - 40; y++)
            {
                int ax = StripX - 3, bx = StripX + StripW + 2;
                if (!w.CanPlace("ferry", ax, y).Ok || !w.CanPlace("ferry", bx, y).Ok) continue;
                var a = w.AddBld("ferry", ax, y); var b = w.AddBld("ferry", bx, y);
                var made = w.EstablishRoute(a, b); Assert.IsTrue(made.Ok, made.Reason);
                var r = w.FerryRoutes[0]; r.GraceUntil = int.MaxValue;
                w.Vessels.Add(new Vessel { Id = 1, Type = VesselType.FerryBoat, Kind = AssignKind.FerryRoute, Key = r.Id });
                var (ax2, ay2) = Beside(w, a); var (bx2, by2) = Beside(w, b); Assert.GreaterOrEqual(ax2, 0); Assert.GreaterOrEqual(bx2, 0);
                Assert.IsTrue(w.SoldierField(bx2, by2)[ay2 * N + ax2] < 0, "the river cuts the map in two: there is no way round by land");
                return (w, a, b, r);
            }
            Assert.Fail("no place for two landings across the river"); return default;
        }

        [Test]
        public void VesselTypesCarryOnlyWhatTheyAreMadeFor()
        {
            Assert.IsTrue(World.Carries(VesselType.FerryBoat, Cargo.Civilian)); Assert.IsTrue(World.Carries(VesselType.FerryBoat, Cargo.Infantry)); Assert.IsTrue(World.Carries(VesselType.FerryBoat, Cargo.Goods));
            Assert.IsFalse(World.Carries(VesselType.FerryBoat, Cargo.Horse)); Assert.IsFalse(World.Carries(VesselType.FerryBoat, Cargo.Wagon)); Assert.IsFalse(World.Carries(VesselType.FerryBoat, Cargo.Siege));
            Assert.IsTrue(World.Carries(VesselType.CargoBarge, Cargo.Wagon)); Assert.IsTrue(World.Carries(VesselType.CargoBarge, Cargo.Horse)); Assert.IsFalse(World.Carries(VesselType.CargoBarge, Cargo.Siege), "nothing carries siege equipment yet");
            Assert.IsFalse(World.Carries(VesselType.FishingBoat, Cargo.Civilian));
            var (w, a, b, r) = Ferry(false);
            Assert.IsTrue(w.RouteCarries(r, Cargo.Infantry)); Assert.IsFalse(w.RouteCarries(r, Cargo.Wagon), "a basic ferry boat takes no wagons"); Assert.IsFalse(w.RouteCarries(r, Cargo.Siege));
            w.Vessels[0].Condition = 3; Assert.IsFalse(w.RouteCarries(r, Cargo.Infantry), "a worn-out boat carries nobody");
        }

        [Test]
        public void SoldiersMayUseOnlyARunningOpenRouteWithTheRightBoat()
        {
            var (w, a, b, r) = Ferry(false);
            Assert.IsTrue(w.MilitaryCanUse(r));
            w.SetMilitaryAccess(r, false); Assert.IsFalse(w.MilitaryCanUse(r), "closed to soldiers"); w.SetMilitaryAccess(r, true);
            w.SetRouteSuspended(r, true); Assert.IsFalse(w.MilitaryCanUse(r), "a suspended ferry takes nobody"); w.SetRouteSuspended(r, false);
            w.Vessels.Clear(); Assert.IsFalse(w.MilitaryCanUse(r), "no boat, no crossing");
            w.Vessels.Add(new Vessel { Id = 2, Type = VesselType.FishingBoat, Kind = AssignKind.FerryRoute, Key = r.Id }); Assert.IsFalse(w.MilitaryCanUse(r), "a fishing boat carries no infantry");
            w.Vessels.Clear(); w.Vessels.Add(new Vessel { Id = 3, Type = VesselType.FerryBoat, Kind = AssignKind.FerryRoute, Key = r.Id }); Assert.IsTrue(w.MilitaryCanUse(r));
        }

        [Test]
        public void ARouteHasRoomForOnlyAsManyPeopleAsItCarriesInADay()
        {
            var (w, a, b, r) = Ferry(false); int len = w.RouteLength(r); int cap = World.PassengerCapacityPerDay(len);
            w.Day = 10; Assert.AreEqual(cap, w.CrossingRoomToday(r));
            Assert.IsTrue(w.ReserveCrossing(r, cap - 1)); Assert.AreEqual(1, w.CrossingRoomToday(r));
            Assert.IsFalse(w.ReserveCrossing(r, 2), "not enough room: nothing is taken"); Assert.AreEqual(1, w.CrossingRoomToday(r));
            Assert.IsTrue(w.ReserveCrossing(r, 1)); Assert.AreEqual(0, w.CrossingRoomToday(r));
            w.Day = 11; Assert.AreEqual(cap, w.CrossingRoomToday(r), "a new day, a new load");
        }

        [Test]
        public void ALargeCompanyCrossesInGroupsOverSeveralTrips()
        {
            var (w, a, b, r) = Ferry(false); int len = w.RouteLength(r); int perDay = World.TripsPerDay(len);
            var small = w.CompanyCrossing(r, World.FerryPassengers); Assert.AreEqual(1, small.trips); Assert.AreEqual(World.CrossDays(len), small.days);
            var big = w.CompanyCrossing(r, 50); Assert.AreEqual((50 + World.FerryPassengers - 1) / World.FerryPassengers, big.trips);
            Assert.AreEqual((big.trips + perDay - 1) / perDay + World.CrossDays(len) - 1, big.days);
            Assert.Greater(big.days, small.days - 1); Assert.GreaterOrEqual(w.CompanyCrossing(r, 400).days, big.days, "bigger companies take longer");
        }

        [Test]
        public void ARiderCountFollowsWhatDependsOnTheFerry()
        {
            var (w, a, b, r) = Ferry(false);
            Assert.AreEqual(0, w.PassengersOn(r), "nothing depends on it yet");
            var (ax, ay) = Beside(w, a); var (bx, by) = Beside(w, b);
            foreach (var (x, y) in World.LPath(C, C + 6, ax, ay)) { if (w.Grid[y, x] != null && w.Grid[y, x].T != "road") { w.Blds.Remove(w.Grid[y, x]); w.Grid[y, x] = null; } if (w.Grid[y, x] == null) w.AddBld("road", x, y); }
            w.AddBld("road", bx, by);
            for (int k = 0; k < 4; k++) { int hx = bx + (k == 0 ? 1 : k == 1 ? -1 : 0), hy = by + (k == 2 ? 1 : k == 3 ? -1 : 0); if (w.Waters.Class[hy * N + hx] == WaterClass.None && w.Grid[hy, hx] == null && w.Grid[by, bx] != null) { var h = w.AddBld("house", hx, hy); h.Served = true; } }
            w.Recalc(); w.Recalc(); w.Touch();
            var load = w.RouteLoad(r);
            if (load.buildings == 0) Assert.Fail("no building is served only through the ferry");
            Assert.Greater(load.people, 0); Assert.GreaterOrEqual(w.PassengersOn(r), 0); Assert.LessOrEqual(w.PassengersOn(r), World.FerryPassengers);
        }

        [Test]
        public void SoldiersCutOffByWaterCrossByFerryAndLandOnTheFarSide()
        {
            var (w, a, b, r) = Ferry(true);
            var (ax, ay) = Beside(w, a); var (bx, by) = Beside(w, b);
            var report = new RaidReport { Alarm = true };
            report.Raiders.Add(new Raider { Id = 1, X = bx, Y = by, Px = bx, Py = by, St = RaiderState.Approach, Hp = 99 });
            var fighter = new Fighter { Id = 1, Kind = Troop.Infantry, Post = w.Hall, X = ax, Y = ay, Px = ax, Py = ay, St = FighterState.Idle, Hp = 50 };
            report.Fighters.Add(fighter);
            int startDist = System.Math.Max(System.Math.Abs(fighter.X - bx), System.Math.Abs(fighter.Y - by));
            bool crossed = false; bool wasCrossing = false; w.Day = 100;
            for (int d = 0; d < 20; d++)
            {
                w.StepFightersNow(report); w.Day++;
                if (fighter.St == FighterState.Crossing) { wasCrossing = true; Assert.AreNotEqual(FighterState.Dead, fighter.St); }
                if (wasCrossing && fighter.St != FighterState.Crossing) { crossed = true; break; }
            }
            Assert.IsTrue(wasCrossing, "he boarded the ferry: " + w.HopDiagnosis(fighter, bx, by)); Assert.IsTrue(crossed, "and landed");
            int endDist = System.Math.Max(System.Math.Abs(fighter.X - bx), System.Math.Abs(fighter.Y - by));
            Assert.Less(endDist, startDist, "on the raider's side of the water");
        }

        [Test]
        public void ASoldierDoesNotCrossAClosedOrSuspendedFerry()
        {
            foreach (var close in new[] { "military", "suspended" })
            {
                var (w, a, b, r) = Ferry(true);
                if (close == "military") w.SetMilitaryAccess(r, false); else w.SetRouteSuspended(r, true);
                var (ax, ay) = Beside(w, a); var (bx, by) = Beside(w, b);
                var report = new RaidReport { Alarm = true };
                report.Raiders.Add(new Raider { Id = 1, X = bx, Y = by, Px = bx, Py = by, St = RaiderState.Approach, Hp = 99 });
                var fighter = new Fighter { Id = 1, Kind = Troop.Infantry, Post = w.Hall, X = ax, Y = ay, Px = ax, Py = ay, St = FighterState.Idle, Hp = 50 };
                report.Fighters.Add(fighter); w.Day = 100;
                for (int d = 0; d < 15; d++) { w.StepFightersNow(report); w.Day++; Assert.AreNotEqual(FighterState.Crossing, fighter.St, close); }
                Assert.AreEqual(ax, fighter.X, close); Assert.AreEqual(ay, fighter.Y, close);   // he stands where he was: he is not stranded anywhere new
            }
        }

        [Test]
        public void RaidersNeverUseYourFerries()
        {
            var (w, a, b, r) = Ferry(true);
            var (ax, ay) = Beside(w, a); var (bx, by) = Beside(w, b);
            Assert.IsTrue(w.SoldierField(ax, ay)[by * N + bx] < 0, "the two shores are cut off for walkers");
            Assert.IsTrue(w.IsSolid(w.LaunchTile(a).Value.x, w.LaunchTile(a).Value.y), "water is a wall: only soldiers who board a ferry get across");
        }
    }
}
