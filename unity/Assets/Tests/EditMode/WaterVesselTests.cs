// Vessels, the boatyard, wear and repair, and what ferries, docks and ports need (World.Vessels.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterVesselTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 9000; w.ResAmt[Res.Stone] = 900; w.ResAmt[Res.Gold] = 900; w.ResAmt[Res.Flax] = 100; w.ResAmt[Res.Metal] = 50; w.Peasants = 80; return w; }

        static bool Foot(World w, int x, int y) => Enumerable.Range(0, 4).All(k => w.Waters.Class[(y + k / 2) * N + x + k % 2] == WaterClass.None && w.Grid[y + k / 2, x + k % 2] == null && w.DecoMap[y + k / 2, x + k % 2] == Deco.None && !w.IsSolid(x + k % 2, y + k / 2));

        /// <summary>A boatyard on the shore, staffed (as the workforce would), road beside it laid by hand.</summary>
        static Building Yard(World w)
        {
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++)
            {
                if (!Foot(w, x, y)) continue;
                var probe = new Building { T = "boatyard", X = x, Y = y, Sz = 2 };
                if (!w.NearOpenWater(probe, 3, WaterDepth.Medium)) continue;
                var b = w.AddBld("boatyard", x, y); b.Served = true; b.Workers = 2; b.Eff = 1; return b;
            }
            Assert.Fail("no shore for a boatyard"); return null;
        }

        static Building Dock(World w)
        {
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++)
                if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("dock", x, y).Ok) { var d = w.AddBld("dock", x, y); d.Served = true; d.Workers = 4; return d; }
            Assert.Fail("no dock spot"); return null;
        }

        static Vessel Add(World w, VesselType t, double cond = 100) { var v = new Vessel { Id = w.Vessels.Count + 1, Type = t, Condition = cond }; w.Vessels.Add(v); return v; }

        // ---------- the boatyard ----------
        [Test]
        public void ABoatyardNeedsTheShoreDeepWaterAndARoad()
        {
            var w = Make(WaterPreset.Lakeside, 1);
            Assert.IsFalse(w.CanPlace("boatyard", C + 12, C + 12).Ok);
            int px = -1, py = -1;
            for (int y = 8; y < N - 8 && px < 0; y++) for (int x = 8; x < N - 8; x++) if (Foot(w, x, y) && w.NearOpenWater(new Building { T = "boatyard", X = x, Y = y, Sz = 2 }, 3, WaterDepth.Medium)) { px = x; py = y; break; }
            Assert.GreaterOrEqual(px, 0);
            var noRoad = w.CanPlace("boatyard", px, py); Assert.IsFalse(noRoad.Ok); StringAssert.Contains("road", noRoad.Reason);
            foreach (var (dx, dy) in new[] { (-1, 0), (2, 0), (0, -1), (0, 2) }) { int rx = px + dx, ry = py + dy; if (w.Waters.Class[ry * N + rx] == WaterClass.None && w.Grid[ry, rx] == null && !w.IsSolid(rx, ry) && !w.HasTree(rx, ry)) { w.AddBld("road", rx, ry); break; } }
            Assert.IsTrue(w.CanPlace("boatyard", px, py).Ok, w.CanPlace("boatyard", px, py).Reason);
            Assert.AreEqual(2, Defs.PlacementSize("boatyard")); Assert.AreEqual(2, new Building { T = "boatyard" }.Jobs);
        }

        // ---------- building vessels ----------
        [Test]
        public void AnOrderIsBuiltByAStaffedBoatyardFromRealMaterials()
        {
            var w = Make(WaterPreset.Lakeside, 1);
            Assert.IsFalse(w.OrderVessel(VesselType.FerryBoat).Ok, "no boatyard, no orders");
            var yard = Yard(w); var spec = World.Spec(VesselType.FerryBoat);
            Assert.IsTrue(w.OrderVessel(VesselType.FerryBoat, 2).Ok); Assert.AreEqual(2, w.VesselOrders.Count);
            double wood = w.ResAmt[Res.Wood], flax = w.ResAmt[Res.Flax], metal = w.ResAmt[Res.Metal];
            w.StepVessels();
            Assert.AreEqual(wood - spec.Wood, w.ResAmt[Res.Wood], 1e-9); Assert.AreEqual(flax - spec.Flax, w.ResAmt[Res.Flax], 1e-9); Assert.AreEqual(metal - spec.Metal, w.ResAmt[Res.Metal], 1e-9);
            Assert.IsTrue(w.VesselOrders[0].Started); Assert.IsFalse(w.VesselOrders[1].Started, "one at a time");
            for (int d = 1; d < spec.BuildDays; d++) { Assert.AreEqual(0, w.Vessels.Count); w.StepVessels(); }
            Assert.AreEqual(1, w.Vessels.Count, "finished after the build days"); Assert.AreEqual(VesselType.FerryBoat, w.Vessels[0].Type); Assert.AreEqual(100, w.Vessels[0].Condition);
            Assert.AreEqual(1, w.VesselOrders.Count); Assert.IsTrue(w.Events.Any(e => e.Contains("finished at the boatyard")));
        }

        [Test]
        public void WithoutMaterialsOrACrewNothingIsBuiltAndNothingIsSpent()
        {
            var w = Make(WaterPreset.Lakeside, 1); var yard = Yard(w); w.OrderVessel(VesselType.CargoBarge);
            w.ResAmt[Res.Flax] = 0; double wood = w.ResAmt[Res.Wood];
            for (int d = 0; d < 10; d++) w.StepVessels();
            Assert.IsFalse(w.VesselOrders[0].Started); Assert.AreEqual(wood, w.ResAmt[Res.Wood], 1e-9, "nothing is spent while materials are short");
            StringAssert.Contains("materials", w.OrderStatus(w.VesselOrders[0]));
            w.ResAmt[Res.Flax] = 50; yard.Workers = 0; yard.Eff = 0;
            for (int d = 0; d < 40; d++) w.StepVessels();
            Assert.IsFalse(w.VesselOrders[0].Started, "an unstaffed boatyard builds nothing"); Assert.AreEqual(0, w.Vessels.Count);
            Assert.AreEqual(0, w.YardCapacity);
        }

        [Test]
        public void CancellingAStartedOrderGivesTheMaterialsBack()
        {
            var w = Make(WaterPreset.Lakeside, 1); Yard(w); w.OrderVessel(VesselType.FishingBoat, 2);
            double wood = w.ResAmt[Res.Wood]; w.StepVessels(); Assert.Less(w.ResAmt[Res.Wood], wood);
            Assert.IsTrue(w.CancelOrder(1)); Assert.AreEqual(wood - World.Spec(VesselType.FishingBoat).Wood, w.ResAmt[Res.Wood], 1e-9, "an order not started costs nothing to cancel");
            Assert.IsTrue(w.CancelOrder(0)); Assert.AreEqual(wood, w.ResAmt[Res.Wood], 1e-9, "a started one returns its materials"); Assert.AreEqual(0, w.VesselOrders.Count);
            Assert.IsFalse(w.CancelOrder(3)); Assert.IsFalse(w.OrderVessel(VesselType.FishingBoat, 21).Ok, "the list holds 20");
        }

        // ---------- assignment ----------
        [Test]
        public void VesselsGoToWhatNeedsOneOfTheirTypeAndBecomeIdleWhenItIsGone()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w);
            var fish = Add(w, VesselType.FishingBoat); var ferry = Add(w, VesselType.FerryBoat); var barge = Add(w, VesselType.CargoBarge);
            w.StepVessels();
            Assert.AreEqual(AssignKind.Dock, fish.Kind); Assert.AreEqual(World.Tk(dock.X, dock.Y), fish.Key);
            Assert.AreEqual(AssignKind.None, ferry.Kind, "no ferry route yet"); Assert.AreEqual(AssignKind.None, barge.Kind, "no port yet");
            Add(w, VesselType.FishingBoat); Add(w, VesselType.FishingBoat); w.StepVessels();
            Assert.AreEqual(World.DockBoatsMax, w.VesselsOfDock(dock).Count(), "a dock takes two");
            w.Blds.Remove(dock); w.Grid[dock.Y, dock.X] = null; w.StepVessels();
            Assert.IsTrue(w.Vessels.Where(v => v.Type == VesselType.FishingBoat).All(v => v.Kind == AssignKind.None), "the dock is gone: its boats are idle, not destroyed");
            Assert.AreEqual(5, w.Vessels.Count);
        }

        // ---------- what vessels gate ----------
        [Test]
        public void AFerryRouteNeedsAFerryBoatInService()
        {
            var w = Make(WaterPreset.Lakeside, 1); var spots = new List<(int x, int y)>();
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("ferry", x, y).Ok) spots.Add((x, y));
            Building a = null, b = null;
            foreach (var s in spots) { foreach (var t in spots) { int d = System.Math.Max(System.Math.Abs(s.x - t.x), System.Math.Abs(s.y - t.y)); if (d < 6 || d > 20) continue; var pa = new Building { T = "ferry", X = s.x, Y = s.y, Sz = 1 }; var pb = new Building { T = "ferry", X = t.x, Y = t.y, Sz = 1 }; if (w.CanRoute(pa, pb).Ok) { a = w.AddBld("ferry", s.x, s.y); b = w.AddBld("ferry", t.x, t.y); break; } } if (a != null) break; }
            Assert.IsNotNull(a); Assert.IsTrue(w.EstablishRoute(a, b).Ok); var route = w.FerryRoutes[0]; route.GraceUntil = int.MaxValue;
            Assert.AreEqual(FerryState.MissingVessel, w.StateOf(route)); StringAssert.Contains("Boatyard", World.StateText(FerryState.MissingVessel));
            var boat = Add(w, VesselType.FerryBoat, 10); w.StepVessels();
            Assert.AreEqual(AssignKind.FerryRoute, boat.Kind); Assert.AreEqual(FerryState.VesselUnderRepair, w.StateOf(route), "a worn-out boat does not sail");
            boat.Condition = 100; Assert.AreEqual(FerryState.Operational, w.StateOf(route));
            Assert.AreSame(boat, w.VesselOfRoute(route));
        }

        [Test]
        public void ADocksCatchDependsOnItsBoats()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w);
            Assert.AreEqual(0.25, w.DockBoatFactor(dock), 1e-9, "with no boats the fishermen only fish from the pier");
            dock.Workers = 2; Add(w, VesselType.FishingBoat).Kind = AssignKind.Dock; w.Vessels[0].Key = World.Tk(dock.X, dock.Y);
            Assert.AreEqual(1.0, w.DockBoatFactor(dock), 1e-9, "one boat for two fishermen is a full crew");
            dock.Workers = 4; Assert.AreEqual(0.25 + 0.75 * 0.5, w.DockBoatFactor(dock), 1e-9);
            w.Vessels[0].Condition = 5; Assert.AreEqual(0.25, w.DockBoatFactor(dock), 1e-9, "a worn-out boat does not count");
            var hut = new Building { T = "fisher" }; Assert.AreEqual(1.0, w.DockBoatFactor(hut), "huts need no boat");
        }

        [Test]
        public void APortNeedsABargeInServiceAndAHarbourCrew()
        {
            var w = new World(1, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Coastal); w.ClaimAll();
            int px = -1, py = -1;
            for (int y = 8; y < N - 8 && px < 0; y++) for (int x = 8; x < N - 8; x++) if (Foot(w, x, y) && w.CanPlace("port", x, y).Ok) { px = x; py = y; break; }
            var port = w.AddBld("port", px, py); port.Served = true;
            Assert.AreEqual(0, w.ShipCapacity); StringAssert.Contains("No cargo barge", w.PortStatus(port));
            var barge = Add(w, VesselType.CargoBarge); w.StepVessels();
            Assert.AreEqual(AssignKind.Port, barge.Kind); StringAssert.Contains("harbour crew", w.PortStatus(port)); Assert.AreEqual(0, w.ShipCapacity);
            port.Workers = World.PortCrew; Assert.AreEqual(1, w.ShipCapacity); Assert.AreEqual("Operational", w.PortStatus(port));
            barge.Condition = 3; Assert.AreEqual(0, w.ShipCapacity); StringAssert.Contains("worn out", w.PortStatus(port));
            Assert.AreEqual(2, new Building { T = "port", Sz = 2 }.Jobs);
        }

        // ---------- wear and repair ----------
        [Test]
        public void VesselsWearWithUseAndAYardMendsThemForWood()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w);
            var boat = Add(w, VesselType.FishingBoat); w.StepVessels();
            double c0 = boat.Condition; for (int d = 0; d < 100; d++) w.StepVessels();
            Assert.Less(boat.Condition, c0); Assert.AreEqual(100 - 101 * World.Spec(VesselType.FishingBoat).WearPerDay, boat.Condition, 0.5, "it wears at its type's rate while it works");
            var idle = Add(w, VesselType.FerryBoat); for (int d = 0; d < 100; d++) w.StepVessels();
            Assert.AreEqual(100 - 100 * World.IdleWear, idle.Condition, 0.5, "an idle vessel hardly wears");
            for (int d = 0; d < 700; d++) w.StepVessels();
            Assert.IsFalse(w.InService(boat), "with no boatyard it wears out"); Assert.GreaterOrEqual(boat.Condition, 0);
            var yard = Yard(w); double wood = w.ResAmt[Res.Wood]; boat.Condition = 40;
            for (int d = 0; d < 30; d++) w.StepVessels();
            Assert.AreEqual(100, boat.Condition, 6, "a staffed boatyard repairs it");
            Assert.Less(w.ResAmt[Res.Wood], wood, "and the repairs cost wood"); Assert.AreEqual(0, w.VesselOrders.Count);
        }

        // ---------- old saves ----------
        [Test]
        public void OldSavesGetOneVesselForEachThingThatWasAlreadyWorking()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w);
            Building a = null, b = null; var spots = new List<(int x, int y)>();
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("ferry", x, y).Ok) spots.Add((x, y));
            foreach (var s in spots) { foreach (var t in spots) { int d = System.Math.Max(System.Math.Abs(s.x - t.x), System.Math.Abs(s.y - t.y)); if (d < 6 || d > 20) continue; if (w.CanRoute(new Building { T = "ferry", X = s.x, Y = s.y, Sz = 1 }, new Building { T = "ferry", X = t.x, Y = t.y, Sz = 1 }).Ok) { a = w.AddBld("ferry", s.x, s.y); b = w.AddBld("ferry", t.x, t.y); break; } } if (a != null) break; }
            Assert.IsTrue(w.EstablishRoute(a, b).Ok);
            var json = JObject.Parse(w.SaveJson()); json.Remove("vessels"); json.Remove("vesselOrders"); json.Remove("legacyGrace");
            double wood = (double)json["res"]["wood"];
            var back = World.LoadJson(json.ToString());
            Assert.AreEqual(1 + World.DockBoatsMax, back.Vessels.Count, "a ferry boat for the route and two fishing boats for the dock");
            Assert.AreEqual(1, back.Vessels.Count(v => v.Type == VesselType.FerryBoat && v.Kind == AssignKind.FerryRoute));
            Assert.AreEqual(2, back.Vessels.Count(v => v.Type == VesselType.FishingBoat && v.Kind == AssignKind.Dock));
            Assert.IsTrue(back.Vessels.All(v => v.Condition == 100));
            Assert.AreEqual(back.Day + World.FerryMigrationGraceDays, back.LegacyGraceUntil);
            Assert.AreEqual(wood, back.ResAmt[Res.Wood], 1e-6, "no resources were created or spent"); Assert.IsTrue(back.Events.Any(e => e.Contains("vessels the town owns")));
            Assert.AreEqual(back.Vessels.Count, World.LoadJson(back.SaveJson()).Vessels.Count, "and the migration happens once");
        }

        [Test]
        public void VesselsAndOrdersAreSavedExactly()
        {
            var w = Make(WaterPreset.Lakeside, 1); Yard(w); var dock = Dock(w);
            Add(w, VesselType.FishingBoat, 77.5); w.StepVessels(); w.OrderVessel(VesselType.CargoBarge, 2); w.StepVessels(); w.StepVessels();
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(w.Vessels.Count, back.Vessels.Count); var v = back.Vessels[0]; Assert.AreEqual(w.Vessels[0].Condition, v.Condition, 0.01); Assert.AreEqual(AssignKind.Dock, v.Kind);
            Assert.AreEqual(2, back.VesselOrders.Count); Assert.IsTrue(back.VesselOrders[0].Started); Assert.AreEqual(w.VesselOrders[0].Progress, back.VesselOrders[0].Progress, 0.01);
            var a = JObject.Parse(w.SaveJson()); var b = JObject.Parse(back.SaveJson());
            Assert.AreEqual(a["vessels"].ToString(), b["vessels"].ToString(), "vessels do not drift across a round trip"); Assert.AreEqual(a["vesselOrders"].ToString(), b["vesselOrders"].ToString());
        }

        [Test]
        public void DryWorldsHaveNoVessels()
        {
            var w = new World(7); w.StepVessels();
            Assert.AreEqual(0, w.Vessels.Count); Assert.IsFalse(w.OrderVessel(VesselType.FishingBoat).Ok); Assert.AreEqual(0, w.MigrateLegacyVessels());
        }
    }
}
