// Fishing boats (World.Boats.cs): they sail only over connected boat-deep water, within their dock's own water, and go home again.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class WaterBoatsTests
    {
        const int N = Defs.N;

        static World Make(WaterPreset p, int seed)
        {
            var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(p).SuggestedRelief, p); w.ClaimAll(); return w;
        }

        static Building Dock(World w, int workers = 3, int fromY = 8)
        {
            for (int y = fromY; y < N - 8; y++)
                for (int x = 8; x < N - 8; x++)
                    if (w.Waters.Class[y * N + x] == WaterClass.None && w.CanPlace("dock", x, y).Ok)
                    {
                        var d = w.AddBld("dock", x, y); d.Served = true; d.Workers = workers; GiveBoats(w, d, 2); return d;
                    }
            Assert.Fail("no place for a dock"); return null;
        }

        static void GiveBoats(World w, Building dock, int n) { for (int i = 0; i < n; i++) w.Vessels.Add(new Vessel { Id = 900 + i + w.Vessels.Count, Type = VesselType.FishingBoat, Kind = AssignKind.Dock, Key = World.Tk(dock.X, dock.Y) }); }

        static void Run(World w, Building dock, int days, System.Action<int> each = null)
        {
            for (int d = 0; d < days; d++) { dock.Served = true; if (dock.Workers == 0) dock.Workers = 3; w.StepBoats(); w.Day++; each?.Invoke(d); }
        }

        [Test]
        public void ADockWithFishermenLaunchesBoatsOneForEveryTwoUpToTwo()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w, 3);
            w.StepBoats(); Assert.AreEqual(2, w.BoatsOf(dock), "one boat for each of the dock's two vessels in service");
            w.Vessels[0].Condition = 5; w.StepBoats(); Assert.AreEqual(1, w.BoatsOf(dock), "a worn-out vessel is hauled out");
            w.Vessels[0].Condition = 100; GiveBoats(w, dock, 2); w.StepBoats(); Assert.AreEqual(World.BoatsPerDock, w.BoatsOf(dock), "never more than the limit");
            Assert.IsTrue(w.Boats.All(b => b.Home == dock));
        }

        [Test]
        public void BoatsSailOnlyOnConnectedBoatWaterAndStayInTheirOwnRegion()
        {
            foreach (var (p, s) in new[] { (WaterPreset.Lakeside, 1), (WaterPreset.RiverValley, 1), (WaterPreset.Coastal, 1) })
            {
                var w = Make(p, s); var dock = Dock(w);
                var seenAt = new HashSet<int>(); var states = new HashSet<BoatState>();
                Run(w, dock, 80, _ =>
                {
                    foreach (var b in w.Boats)
                    {
                        int i = b.Y * N + b.X; seenAt.Add(i); states.Add(b.St);
                        Assert.IsTrue((w.Waters.Flags[i] & WaterFlags.Navigable) != 0, $"{p}: a boat is on boat-deep water");
                        Assert.AreEqual(b.Region, w.Waters.NavRegion[i], $"{p}: a boat never leaves its own connected water");
                        Assert.IsFalse(w.IsBridge(b.X, b.Y));
                        Assert.LessOrEqual(System.Math.Abs(b.X - b.Px) + System.Math.Abs(b.Y - b.Py), 1, "one tile a day");
                    }
                });
                Assert.Greater(seenAt.Count, 3, $"{p}: the boats actually sail");
                CollectionAssert.IsSupersetOf(states, new[] { BoatState.Out, BoatState.Fishing, BoatState.Back });
            }
        }

        [Test]
        public void BoatsComeHomeToTheirLaunchTile()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w); int home = 0;
            Run(w, dock, 120, _ => { foreach (var b in w.Boats) if (b.St == BoatState.Moored && b.X == b.LaunchX && b.Y == b.LaunchY) home++; });
            Assert.Greater(home, 5);
        }

        [Test]
        public void NoDockNoFishermenNoRoadNoWaterMeansNoBoats()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w);
            w.StepBoats(); Assert.Greater(w.Boats.Count, 0);
            dock.Workers = 0; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count, "no fishermen, no boats");
            dock.Workers = 3; w.StepBoats(); Assert.Greater(w.Boats.Count, 0);
            dock.Served = false; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count, "a dock with no road sends nothing out");
            dock.Served = true; w.StepBoats();
            w.Blds.Remove(dock); w.Grid[dock.Y, dock.X] = null; w.StepBoats(); Assert.AreEqual(0, w.Boats.Count, "a pulled-down dock loses its boats");
            Assert.AreEqual(0, new World(7).Boats.Count);
            var dry = new World(7); dry.StepBoats(); Assert.AreEqual(0, dry.Boats.Count);
        }

        [Test]
        public void ADockOnAShallowOnlyStreamGetsNoBoats()
        {
            var w = Make(WaterPreset.BalancedInland, 1);
            // a hut-style building of type dock placed by hand beside a stream (the player cannot do this: CanPlace refuses)
            for (int y = 8; y < N - 8; y++)
                for (int x = 8; x < N - 8; x++)
                {
                    if (w.Waters.Class[y * N + x] != WaterClass.None || w.IsSolid(x, y) || w.Grid[y, x] != null) continue;
                    bool streamNear = false, boatNear = false;
                    for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++) { int i = (y + dy) * N + x + dx; if (w.Waters.Class[i] == WaterClass.Stream) streamNear = true; if ((w.Waters.Flags[i] & WaterFlags.Navigable) != 0) boatNear = true; }
                    if (!streamNear || boatNear) continue;
                    Assert.IsFalse(w.CanPlace("dock", x, y).Ok);
                    var d = w.AddBld("dock", x, y); d.Served = true; d.Workers = 3;
                    w.StepBoats(); Assert.AreEqual(0, w.Boats.Count);
                    return;
                }
            Assert.Pass("no shallow-only stream found on this map");
        }

        [Test]
        public void BoatSpritesExistForEveryFacing()
        {
            for (int f = 0; f < 4; f++) for (int fr = 0; fr < 2; fr++) Assert.IsNotNull(BoatArt.Get(f, fr));
        }

        [Test]
        public void BoatsAreNeverSaved()
        {
            var w = Make(WaterPreset.Lakeside, 1); var dock = Dock(w); w.StepBoats();
            Assert.Greater(w.Boats.Count, 0);
            Assert.IsFalse(w.SaveJson().Contains("\"boats\""));
        }
    }
}
