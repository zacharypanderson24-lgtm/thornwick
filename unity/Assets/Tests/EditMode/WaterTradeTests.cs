// Trade by water and winter ferries (World.MaritimeTrade.cs, WaterAtlas.cs, World.Ferries.cs). The world is built by hand (a small atlas
// and four settlements) so these tests do not depend on what a random world happens to contain.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterTradeTests
    {
        const int N = Defs.N, C = Defs.Start, S = 200;

        /// <summary>A 200 x 200 map: sea A down the whole west edge (x 0..9), a small separate sea B (x 50..59, y 0..9), lakes L1 and L2.</summary>
        static WaterAtlas FakeAtlas()
        {
            var sea = new bool[S * S]; var lake = new bool[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                if (x <= 9) sea[y * S + x] = true;
                if (x >= 50 && x <= 59 && y <= 9) sea[y * S + x] = true;
                if (x >= 30 && x <= 34 && y >= 60 && y <= 64) lake[y * S + x] = true;
                if (x >= 70 && x <= 74 && y >= 60 && y <= 64) lake[y * S + x] = true;
            }
            return new WaterAtlas(S, sea, lake);
        }

        static Settlement Place(int id, string name, int x, int y)
        {
            var s = new Settlement { Id = id, Name = name, X = x, Y = y, Population = 2000, Attitude = 100, Personality = Personality.Mercantile, Makes = Res.Wood, Needs = Res.Gold };
            s.Produce.Add((Res.Wood, 1.0)); s.Wants.Add((Res.Gold, 1.0)); return s;
        }

        // 0 near on the sea (a short way), 1 far on the same sea, 2 inland, 3 on the other sea, 4 on a lake
        static World Make(bool market, bool port, int homeX = 12, int homeY = 100)
        {
            var w = new World(1, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Coastal); w.ClaimAll();
            w.ResAmt[Res.Gold] = 5000; w.ResAmt[Res.Wood] = 5000; w.ResAmt[Res.Stone] = 500;
            w.SetAtlas(FakeAtlas()); w.HomeCellX = homeX; w.HomeCellY = homeY;
            w.SetSettlements(new[] { Place(0, "Nearby", 11, 120), Place(1, "Faraway", 11, 199), Place(2, "Inland", 100, 100), Place(3, "OtherSea", 52, 12), Place(4, "Lakeside", 31, 66) });
            if (market) { var m = w.AddBld("market", C + 10, C + 10); m.Served = true; }
            if (port) { var p = PlacePort(w); p.Served = true; }
            return w;
        }

        static Building PlacePort(World w)
        {
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++)
                if (Enumerable.Range(0, 4).All(k => w.Waters.Class[(y + k / 2) * N + x + k % 2] == WaterClass.None && w.Grid[y + k / 2, x + k % 2] == null) && w.CanPlace("port", x, y).Ok)
                    return w.AddBld("port", x, y);
            Assert.Fail("no place for a port"); return null;
        }

        [Test]
        public void ThePortFixtureIsAsDescribed()
        {
            var a = FakeAtlas();
            Assert.IsTrue(a.AccessAt(12, 100).HasValue); Assert.AreEqual(TradeMode.Coastal, a.AccessAt(12, 100).Value.Mode);
            Assert.IsFalse(a.AccessAt(100, 100).HasValue, "inland");
            Assert.AreEqual(TradeMode.Lake, a.AccessAt(31, 66).Value.Mode);
            Assert.AreNotEqual(a.AccessAt(11, 120).Value.Body, a.AccessAt(52, 12).Value.Body, "two separate seas");
            Assert.AreNotEqual(a.AccessAt(31, 66).Value.Body, a.AccessAt(71, 66).Value.Body, "two separate lakes");
            Assert.AreEqual(-1, a.RouteKm(a.AccessAt(11, 120).Value, a.AccessAt(52, 12).Value));
            Assert.Greater(a.RouteKm(a.AccessAt(12, 100).Value, a.AccessAt(11, 199).Value), 95);
        }

        [Test]
        public void AShipSailsOnlyToASettlementOnTheSameSea()
        {
            var w = Make(false, true);
            var far = w.PlanTrade(w.SettlementById(1));
            Assert.IsTrue(far.Ok, far.Reason); Assert.AreEqual(TradeMode.Coastal, far.Mode); Assert.GreaterOrEqual(far.RouteKm, 95);
            Assert.AreEqual((far.RouteKm + World.ShipKmPerDay - 1) / World.ShipKmPerDay + World.ShipLoadDays, far.Days, "travel time follows the water route");
            // not on the same water, or not on water at all: no ship, and with no market no trade at all
            foreach (int id in new[] { 2, 3, 4 })
            {
                var plan = w.PlanTrade(w.SettlementById(id));
                Assert.IsFalse(plan.Ok, w.SettlementById(id).Name); StringAssert.Contains("Market", plan.Reason);
            }
            Assert.IsFalse(w.CanTrade(w.SettlementById(2)).Ok, "a port on the sea does not make a landlocked settlement reachable");
        }

        [Test]
        public void APortNeverSpeedsUpTradeWithAnInlandOrUnconnectedSettlement()
        {
            var w = Make(true, true);
            foreach (int id in new[] { 2, 3, 4 })
            {
                var plan = w.PlanTrade(w.SettlementById(id));
                Assert.IsTrue(plan.Ok); Assert.AreEqual(TradeMode.Overland, plan.Mode, w.SettlementById(id).Name + " goes overland");
                Assert.AreEqual(w.SettlementMarchDays(w.SettlementById(id)), plan.Days, "at the ordinary overland time");
            }
            var noPort = Make(true, false);
            Assert.AreEqual(w.SettlementMarchDays(w.SettlementById(2)), noPort.PlanTrade(noPort.SettlementById(2)).Days, "same as with no port at all");
        }

        [Test]
        public void ARegionWithNoSeaOnTheWorldMapGetsNoShips()
        {
            var w = Make(true, true, 100, 100);   // an inland home
            var plan = w.PlanTrade(w.SettlementById(1)); Assert.IsTrue(plan.Ok); Assert.AreEqual(TradeMode.Overland, plan.Mode);
            var why = w.MaritimeRoute(w.SettlementById(1)); Assert.IsFalse(why.ok); StringAssert.Contains("no sea or lake", why.why);
            var noAtlas = Make(true, true); noAtlas.SetAtlas(null);
            Assert.AreEqual(TradeMode.Overland, noAtlas.PlanTrade(noAtlas.SettlementById(1)).Mode, "unverified water is never assumed");
        }

        [Test]
        public void ThePlanPicksTheFasterWayAndAShipIsNotAlwaysFaster()
        {
            var w = Make(true, true);
            var near = w.PlanTrade(w.SettlementById(0)); var far = w.PlanTrade(w.SettlementById(1));
            Assert.AreEqual(TradeMode.Overland, near.Mode, "a short trip is quicker on foot (a ship needs loading time)");
            Assert.AreEqual(TradeMode.Coastal, far.Mode, "a long coastal trip is quicker by ship");
            Assert.Less(far.Days, w.SettlementMarchDays(w.SettlementById(1)));
            Assert.Less(w.SettlementMarchDays(w.SettlementById(0)), (w.MaritimeRoute(w.SettlementById(0)).km + World.ShipKmPerDay - 1) / World.ShipKmPerDay + World.ShipLoadDays + 1);
        }

        [Test]
        public void CaravansAndShipsHaveSeparateCapacity()
        {
            var w = Make(true, true); var far = w.SettlementById(1); w.Day = 10;
            Assert.AreEqual(1, w.CaravanCapacity); Assert.AreEqual(1, w.ShipCapacity);
            Assert.IsTrue(w.BuyFrom(far, 50).Ok);
            Assert.AreEqual(1, w.ShipsOut); Assert.AreEqual(0, w.LandCaravansOut, "the first went by ship");
            var second = w.PlanTrade(far); Assert.IsTrue(second.Ok); Assert.AreEqual(TradeMode.Overland, second.Mode, "the ship is out, so the next goes overland");
            Assert.IsTrue(w.BuyFrom(far, 50).Ok);
            Assert.AreEqual(1, w.ShipsOut); Assert.AreEqual(1, w.LandCaravansOut);
            Assert.IsFalse(w.PlanTrade(far).Ok, "ship and caravan both out");
            // a land caravan does not use up the ship, nor a ship the caravan
            var w2 = Make(true, true); w2.Day = 10; Assert.IsTrue(w2.BuyFrom(w2.SettlementById(2), 50).Ok);
            Assert.AreEqual(1, w2.LandCaravansOut); Assert.AreEqual(0, w2.ShipsOut);
            var after = w2.PlanTrade(w2.SettlementById(1)); Assert.IsTrue(after.Ok); Assert.AreEqual(TradeMode.Coastal, after.Mode, "the ship is still free");
        }

        [Test]
        public void ShipmentsKeepTheirModeRouteAndArrival()
        {
            var w = Make(false, true); var far = w.SettlementById(1); w.Day = 20;
            var plan = w.PlanTrade(far); Assert.IsTrue(w.BuyFrom(far, 50).Ok);
            var c = w.Caravans.Single();
            Assert.AreEqual(TradeMode.Coastal, c.Mode); Assert.AreEqual(plan.RouteKm, c.RouteKm); Assert.AreEqual(20, c.DepartDay); Assert.AreEqual(20 + plan.Days, c.ArriveDay);
            var back = World.LoadJson(w.SaveJson());
            var d = back.Caravans.Single();
            Assert.AreEqual(c.Mode, d.Mode); Assert.AreEqual(c.RouteKm, d.RouteKm); Assert.AreEqual(c.DepartDay, d.DepartDay); Assert.AreEqual(c.ArriveDay, d.ArriveDay); Assert.AreEqual(c.Amount, d.Amount);
            // it arrives on its day, not before
            w.Day = c.ArriveDay - 1; w.StepCaravansNow(); Assert.AreEqual(1, w.Caravans.Count);
            w.Day = c.ArriveDay; w.StepCaravansNow(); Assert.AreEqual(0, w.Caravans.Count);
            Assert.IsTrue(w.Events.Any(e => e.Contains("ship")), "and the news says ship, not caravan");
        }

        [Test]
        public void OldSavesLoadTheirCaravansAsOverland()
        {
            var w = Make(true, false); var near = w.SettlementById(0); w.BuyFrom(near, 10);
            var json = JObject.Parse(w.SaveJson());
            foreach (var c in (JArray)json["caravans"]) { ((JObject)c).Remove("mode"); ((JObject)c).Remove("depart"); ((JObject)c).Remove("km"); }
            var back = World.LoadJson(json.ToString());
            var loaded = back.Caravans.Single(); Assert.AreEqual(TradeMode.Overland, loaded.Mode); Assert.AreEqual(loaded.ArriveDay, loaded.DepartDay);
        }

        [Test]
        public void ARiverPortDoesNotOfferTradeWithSettlementsBecauseRiversAreNotOnTheWorldMap()
        {
            var w = new World(1, TerrainStyle.Ranges, Relief.Upland, WaterPreset.RiverValley); w.ClaimAll(); w.ResAmt[Res.Wood] = 5000;
            Assert.AreEqual(TradeMode.Overland, w.PortMode(new Building { T = "port", X = 5, Y = 5, Sz = 2 }), "a port with no boat-deep water is no ship's port");
        }

        // ---------- ferries in winter ----------
        static World Ferry(WaterPreset p, bool wantBigWater, out Building a, out Building b)
        {
            var w = new World(1, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); a = b = null;
            var spots = new List<(int x, int y)>();
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++) if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("ferry", x, y).Ok) spots.Add((x, y));
            foreach (var s in spots)
            {
                var sb = new Building { T = "ferry", X = s.x, Y = s.y, Sz = 1 };
                foreach (var t in spots)
                {
                    int d = System.Math.Max(System.Math.Abs(s.x - t.x), System.Math.Abs(s.y - t.y)); if (d < 6 || d > World.FerryRange) continue;
                    var tb = new Building { T = "ferry", X = t.x, Y = t.y, Sz = 1 };
                    if (w.RegionOf(sb) != w.RegionOf(tb) || w.RegionOf(sb) == 0) continue;
                    var la = w.LaunchTile(sb).Value; var lb = w.LaunchTile(tb).Value;
                    var cls = w.Waters.Class[la.y * N + la.x]; bool big = !World.Freezes(cls);
                    if (big != wantBigWater) continue;
                    a = w.AddBld("ferry", s.x, s.y); b = w.AddBld("ferry", t.x, t.y); w.ResAmt[Res.Gold] = 500; Assert.IsTrue(w.EstablishRoute(a, b).Ok); w.FerryRoutes[0].GraceUntil = int.MaxValue; return w;   // a crewed route
                }
            }
            Assert.Ignore("no suitable ferry crossing on this map"); return w;
        }

        [Test]
        public void AFerryOnAFreezingLakeIsSuspendedInWinterAndRunsAgainAtTheThaw()
        {
            var w = Ferry(WaterPreset.Lakeside, false, out var a, out var b);
            var pair = w.FerryPairs().Single();
            w.Day = 100; Assert.IsTrue(w.FerryOperating(pair)); Assert.AreEqual("Operational", w.FerryStatus(a));
            w.StepBoats(); Assert.AreEqual(1, w.Boats.Count(x => x.Ferry));
            w.Day = 300; w.Touch(); Assert.IsFalse(w.FerryOperating(pair), "the lake has frozen over");
            StringAssert.Contains("suspended", w.FerryStatus(a)); StringAssert.Contains("frozen", w.FerryStatus(b));
            w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry), "no ferry boat on frozen water");
            w.Day = 365 + 100; w.Touch(); Assert.IsTrue(w.FerryOperating(pair), "the thaw"); w.StepBoats(); Assert.AreEqual(1, w.Boats.Count(x => x.Ferry));
        }

        [Test]
        public void AFrozenFerryJoinsNoRoadsButTheSeaFerryKeepsRunning()
        {
            var w = Ferry(WaterPreset.Lakeside, false, out var a, out var b);
            (int x, int y) Beside(Building l) { foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int x = l.X + dx, y = l.Y + dy; if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null) return (x, y); } Assert.Fail("no land"); return (0, 0); }
            var na = Beside(a); var nb = Beside(b);
            foreach (var (x, y) in World.LPath(C, C + 6, na.x, na.y)) { if (w.Grid[y, x] != null && w.Grid[y, x].T != "road") { w.Blds.Remove(w.Grid[y, x]); w.Grid[y, x] = null; } if (w.Grid[y, x] == null) w.AddBld("road", x, y); }
            w.AddBld("road", nb.x, nb.y);
            w.Day = 100; w.Recalc(); Assert.IsTrue(w.Grid[nb.y, nb.x].Served, "summer: the ferry joins the far road");
            w.Day = 300; w.Touch(); w.Recalc(); Assert.IsFalse(w.Grid[nb.y, nb.x].Served, "winter: the frozen ferry joins nothing");
            w.Day = 365 + 100; w.Touch(); w.Recalc(); Assert.IsTrue(w.Grid[nb.y, nb.x].Served, "spring: it joins again");
            // a ferry across the sea or a big river is not frozen in winter
            var w2 = Ferry(WaterPreset.Coastal, true, out var c, out var d); var pair = w2.FerryPairs().Single();
            w2.Day = 300; w2.Touch(); Assert.IsTrue(w2.FerryOperating(pair), "open sea does not freeze"); w2.StepBoats(); Assert.AreEqual(1, w2.Boats.Count(x => x.Ferry), "its boat keeps sailing in winter");
        }

        [Test]
        public void TheFirstWinterTickWarnsAboutFrozenFerries()
        {
            var w = Ferry(WaterPreset.Lakeside, false, out var a, out var b); w.Events.Clear();
            w.Day = 269; int ticks = 0; while (!w.IceOn && ticks++ < 6) w.Tick(); w.Tick();
            Assert.IsTrue(w.Events.Any(e => e.Contains("suspended")), "the player is told when a ferry is frozen in");
        }

        [Test]
        public void IceCrossingIsSeparateFromFerryService()
        {
            var w = Ferry(WaterPreset.Lakeside, false, out var a, out var b);
            w.Day = 300; var la = w.LaunchTile(a).Value;
            Assert.IsTrue(w.IceBears(la.x, la.y) || !World.Freezes(w.Waters.Class[la.y * N + la.x]), "frozen small water bears people");
            Assert.IsFalse(w.FerryOperating(w.FerryPairs().Single()), "while the ferry itself is not running");
        }
    }
}
