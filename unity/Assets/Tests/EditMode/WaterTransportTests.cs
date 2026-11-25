// Ferries and ports (World.Ferries.cs).
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterTransportTests
    {
        const int N = Defs.N, C = Defs.Start;
        static World Make(WaterPreset p, int seed) { var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); w.ResAmt[Res.Wood] = 9000; w.ResAmt[Res.Stone] = 900; w.ResAmt[Res.Gold] = 900; return w; }

        static List<(int x, int y)> Landings(World w, int near = 0)
        {
            var list = new List<(int x, int y)>();
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++)
            {
                if (near > 0 && System.Math.Max(System.Math.Abs(x - C), System.Math.Abs(y - C)) > near) continue;
                if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null && w.CanPlace("ferry", x, y).Ok) list.Add((x, y));
            }
            return list;
        }

        static int Region(World w, (int x, int y) t) { var b = new Building { T = "ferry", X = t.x, Y = t.y, Sz = 1 }; return w.RegionOf(b); }

        [Test]
        public void FerryLandingsNeedDeepWaterAndPairUpAcrossTheSameWater()
        {
            var w = Make(WaterPreset.Lakeside, 1);
            Assert.IsFalse(w.CanPlace("ferry", C + 12, C + 12).Ok);
            var spots = Landings(w); Assert.Greater(spots.Count, 4);
            var a = spots[0]; var b = spots.First(s => Region(w, s) == Region(w, a) && System.Math.Max(System.Math.Abs(s.x - a.x), System.Math.Abs(s.y - a.y)) is >= 5 and <= World.FerryRange);
            var la = w.AddBld("ferry", a.x, a.y); Assert.IsNull(w.FerryPartner(la), "a lone landing is not a ferry");
            var lb = w.AddBld("ferry", b.x, b.y);
            Assert.AreSame(lb, w.FerryPartner(la)); Assert.AreSame(la, w.FerryPartner(lb));
            Assert.AreEqual(1, w.FerryPairs().Count);
            // too far apart: not paired
            w.Blds.Remove(lb); w.Grid[b.y, b.x] = null;
            var far = spots.FirstOrDefault(s => System.Math.Max(System.Math.Abs(s.x - a.x), System.Math.Abs(s.y - a.y)) > World.FerryRange + 2);
            if (far != default) { var lf = w.AddBld("ferry", far.x, far.y); Assert.IsNull(w.FerryPartner(la), "beyond the ferry range"); Assert.IsNull(w.FerryPartner(lf)); }
        }

        [Test]
        public void AFerryJoinsTheRoadsOnBothShores()
        {
            var w = Make(WaterPreset.Lakeside, 1); var spots = Landings(w, 70); Assert.Greater(spots.Count, 0);
            var all = Landings(w); (int x, int y) a = default, b = default; bool found = false;
            foreach (var cand in spots) { var pb = all.Where(s => Region(w, s) == Region(w, cand) && System.Math.Max(System.Math.Abs(s.x - cand.x), System.Math.Abs(s.y - cand.y)) is >= 6 and <= World.FerryRange).ToList(); if (pb.Count > 0) { a = cand; b = pb[0]; found = true; break; } }
            Assert.IsTrue(found, "two landings near the start on the same water");
            (int x, int y) Beside((int x, int y) t) { foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int x = t.x + dx, y = t.y + dy; if (w.Waters.Class[y * N + x] == WaterClass.None && w.Grid[y, x] == null) return (x, y); } Assert.Fail("no land beside the landing"); return (0, 0); }
            var na = Beside(a); var nb = Beside(b);
            // roads laid straight into the world (the test is about the ferry link, not about road rules): from the Town Hall's road to beside landing A, and one tile beside landing B
            foreach (var (x, y) in World.LPath(C, C + 6, na.x, na.y))
            {
                if (w.Grid[y, x] != null && w.Grid[y, x].T != "road") { w.Blds.Remove(w.Grid[y, x]); w.Grid[y, x] = null; }   // clear anything in the way (this test is about the ferry link)
                if (w.Grid[y, x] == null) w.AddBld("road", x, y);
            }
            w.AddBld("road", nb.x, nb.y);
            w.Recalc();
            Assert.IsTrue(w.Grid[na.y, na.x].Served, "the road to landing A is connected");
            Assert.IsFalse(w.Grid[nb.y, nb.x].Served, "across the water the road is cut off");
            w.AddBld("ferry", a.x, a.y); w.Recalc();
            Assert.IsFalse(w.Grid[nb.y, nb.x].Served, "one landing alone joins nothing");
            w.AddBld("ferry", b.x, b.y); w.Recalc();
            Assert.IsTrue(w.Grid[nb.y, nb.x].Served, "with the second landing the far shore's road is served from the Town Hall");
            // and it works the other way round, and a road further along the far shore joins too
            var far2 = Beside(nb); w.AddBld("road", far2.x, far2.y); w.Recalc(); Assert.IsTrue(w.Grid[far2.y, far2.x].Served);
        }

        [Test]
        public void FerryBoatsShuttleBetweenTheLandingsOverBoatWater()
        {
            var w = Make(WaterPreset.Lakeside, 1); var spots = Landings(w); var a = spots[0];
            var b = spots.First(s => Region(w, s) == Region(w, a) && System.Math.Max(System.Math.Abs(s.x - a.x), System.Math.Abs(s.y - a.y)) is >= 6 and <= World.FerryRange);
            var la = w.AddBld("ferry", a.x, a.y); var lb = w.AddBld("ferry", b.x, b.y);
            var launchA = w.LaunchTile(la).Value; var launchB = w.LaunchTile(lb).Value; bool atA = false, atB = false;
            for (int d = 0; d < 120; d++)
            {
                w.Day = 100 + d % 100; w.StepBoats();
                foreach (var bt in w.Boats.Where(x => x.Ferry))
                {
                    int i = bt.Y * N + bt.X; Assert.IsTrue((w.Waters.Flags[i] & WaterFlags.Navigable) != 0, "on boat water");
                    if (bt.X == launchA.x && bt.Y == launchA.y) atA = true; if (bt.X == launchB.x && bt.Y == launchB.y) atB = true;
                }
            }
            Assert.AreEqual(1, w.Boats.Count(x => x.Ferry), "one ferry boat for the pair"); Assert.IsTrue(atA && atB, "it visits both landings");
            w.Blds.Remove(lb); w.Grid[b.y, b.x] = null; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count(x => x.Ferry), "no partner, no ferry");
        }

        [Test]
        public void APortNeedsABigWaterAndCountsAsAMarket()
        {
            var w = Make(WaterPreset.Coastal, 1); int px = -1, py = -1; string smallWater = null;
            for (int y = 8; y < N - 8 && px < 0; y++) for (int x = 8; x < N - 8; x++)
                if (Enumerable.Range(0, 4).All(k => w.Waters.Class[(y + k / 2) * N + x + k % 2] == WaterClass.None && w.Grid[y + k / 2, x + k % 2] == null) && w.CanPlace("port", x, y).Ok) { px = x; py = y; break; }
            Assert.GreaterOrEqual(px, 0, "a port fits on the coast");
            int before = w.Markets; var port = w.AddBld("port", px, py); port.Served = true; w.Recalc(); port.Served = true;
            Assert.AreEqual(2, port.Sz); Assert.AreEqual(before, w.Markets, "a port is not a market: caravans and ships have separate capacity"); Assert.AreEqual(1, w.Ports); Assert.AreEqual(1, w.ShipCapacity); Assert.AreEqual(TradeMode.Coastal, w.PortMode(port));
            var inland = Make(WaterPreset.BalancedInland, 1); string why = null;
            for (int y = 8; y < N - 8 && why == null; y++) for (int x = 8; x < N - 8; x++)
            {
                var r = inland.CanPlace("port", x, y);
                if (!r.Ok && (r.Reason.Contains("too small") || r.Reason.Contains("deep water"))) { why = r.Reason; break; }
            }
            Assert.IsNotNull(why, "small or shallow water cannot take a port");
        }

        [Test]
        public void FerriesAndPortsSurviveSaveAndLoad()
        {
            var w = Make(WaterPreset.Lakeside, 1); var spots = Landings(w); var a = spots[0];
            var b = spots.First(s => Region(w, s) == Region(w, a) && System.Math.Max(System.Math.Abs(s.x - a.x), System.Math.Abs(s.y - a.y)) is >= 6 and <= World.FerryRange);
            w.AddBld("ferry", a.x, a.y); w.AddBld("ferry", b.x, b.y);
            var coast = new World(1, TerrainStyle.Ranges, Relief.Lowland, WaterPreset.Coastal); coast.ClaimAll();
            int px = -1, py = -1;
            for (int y = 8; y < N - 8 && px < 0; y++) for (int x = 8; x < N - 8; x++) if (Enumerable.Range(0, 4).All(k => coast.Waters.Class[(y + k / 2) * N + x + k % 2] == WaterClass.None && coast.Grid[y + k / 2, x + k % 2] == null) && coast.CanPlace("port", x, y).Ok) { px = x; py = y; break; }
            Assert.GreaterOrEqual(px, 0); coast.AddBld("port", px, py);
            var back = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, back.FerryPairs().Count);
            Assert.AreEqual(2, back.Blds.Count(x => x.T == "ferry"));
            var backCoast = World.LoadJson(coast.SaveJson());
            var port = backCoast.Blds.Single(x => x.T == "port"); Assert.AreEqual(px, port.X); Assert.AreEqual(2, port.Sz);
            Assert.AreEqual(TradeMode.Coastal, backCoast.PortMode(port), "the saved port still stands on the sea");
        }

        [Test]
        public void DryWorldsHaveNoFerriesOrPorts()
        {
            var w = new World(7);
            Assert.AreEqual(0, w.FerryPairs().Count); Assert.AreEqual(0, w.Ports); Assert.AreEqual(w.Markets, w.Blds.Count(b => b.T == "market" && b.Served));
            Assert.IsFalse(w.CanPlace("ferry", C + 3, C + 8).Ok); Assert.IsFalse(w.CanPlace("port", C + 3, C + 8).Ok);
        }
    }
}
