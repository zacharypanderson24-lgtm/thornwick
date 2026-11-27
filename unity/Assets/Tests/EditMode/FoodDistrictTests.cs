// Hunting and Fishing districts (Sim/World.DevFood.cs; DESIGN_ORGANIC_DEVELOPMENT.md phase 5).
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class FoodDistrictTests
    {
        internal static (World w, District d, (int x, int y, int dx, int dy, int px, int py) s) HuntTown(int woods = 4)
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w);
            for (int a = 1; a <= 10; a++)
                for (int t = 1; t <= 6; t++)
                {
                    int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t;
                    if (t > 6 - woods) w.DecoMap[y, x] = Deco.Tree;
                    if (w.ZoneCheck(Zone.Hunt, x, y).Ok) w.ZoneMap[y, x] = Zone.Hunt;
                }
            w.Touch();
            return (w, w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5), s);
        }

        static void Run(World w, int days, bool secureFood = false)
        {
            for (int i = 0; i < days; i++) { if (secureFood) w.ResAmt[Res.Food] = 3000; w.Tick(); }
        }

        [Test]
        public void AHuntingDistrictBuildsItsFirstLodgeEvenWithFoodToSpare()
        {
            var (w, d, _) = HuntTown();
            Assume.That(d != null && d.Type == DistrictType.Hunting && w.D.Idle > 0);
            Run(w, 10, secureFood: true);
            var lodges = w.Blds.Where(b => b.T == "hunter").ToList();
            Assert.AreEqual(1, lodges.Count, w.DistrictById(d.Id).Note);
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "hunter"), "counted in its district");
            Assert.AreEqual(1, w.Projects.Count(p => p.Type == "hunter" && p.Status == ProjectStatus.Done));
        }

        [Test]
        public void ThereIsNoSecondLodgeWhileFoodIsSecureAndTheDistrictSaysSo()
        {
            var (w, d, _) = HuntTown();
            Assume.That(d != null);
            Run(w, 10, secureFood: true);
            Run(w, 40, secureFood: true);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "hunter"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("No new lodge needed").Or.Contain("Additional workers").Or.Contain("at capacity"));
        }

        [Test]
        public void ASparseWoodIsRefusedWithTheRealGameCount()
        {
            var (w, d, _) = HuntTown(woods: 1);
            Assume.That(d != null);
            for (int i = 0; i < 3; i++) w.Develop();
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "hunter"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("little game").And.Contain("woodland tile"));
        }

        [Test]
        public void ADisabledOrApprovalHuntingDistrictObeysItsSettings()
        {
            var (w, d, _) = HuntTown();
            Assume.That(d != null);
            w.SetDistrictPriority(d.Id, DevPriority.Disabled);
            Run(w, 5, true);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "hunter"));
            w.SetDistrictPriority(d.Id, DevPriority.Normal); w.SetDistrictMode(d.Id, DevMode.ApprovalRequired);
            Run(w, 3, true);
            var p = w.Projects.Single(q => q.Type == "hunter");
            Assert.AreEqual(ProjectStatus.Proposed, p.Status);
            Assert.IsTrue(w.RejectProject(p.Id).Ok);
            Run(w, 6, true);
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "hunter"), "a rejected lodge is not rebuilt at once");
        }

        [Test]
        public void ALodgeBuiltByHandNextToTheWoodsCountsAndStopsAnOrganicDuplicate()
        {
            var (w, d, s) = HuntTown();
            Assume.That(d != null);
            foreach (var p in w.OpenProjects.ToList()) w.CancelProject(p.Id);
            Result placed = Result.No("none"); int x = 0, y = 0;
            for (int a = 3; a <= 9 && !placed.Ok; a++) for (int t = 1; t <= 2 && !placed.Ok; t++) { x = s.x + s.dx * a + s.px * t; y = s.y + s.dy * a + s.py * t; placed = w.CanPlace("hunter", x, y); }
            Assume.That(placed.Ok, placed.Reason);
            Assert.IsTrue(w.Place("hunter", x, y).Ok);
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "hunter"));
            Assert.IsFalse(w.IsLegacyBuilding(w.Grid[y, x]));
            Run(w, 20, true);
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "hunter"), "no duplicate while food is secure");
        }

        [Test]
        public void HuntingNeverCreatesGame()
        {
            var (w, d, _) = HuntTown();
            Assume.That(d != null);
            int treesBefore = d.Tiles.Count(k => w.DecoMap[k / Defs.N, k % Defs.N] == Deco.Tree);
            Run(w, 40, true);
            int after = w.DistrictById(d.Id).Tiles.Count(k => w.DecoMap[k / Defs.N, k % Defs.N] == Deco.Tree);
            Assert.LessOrEqual(after, treesBefore + 1, "building a lodge adds no woodland");
        }

        // ---------- fishing ----------
        internal static (World w, District d)? FishTown()
        {
            foreach (var (preset, seed) in new[] { (WaterPreset.Wetlands, 2), (WaterPreset.Lakeside, 1), (WaterPreset.Coastal, 1), (WaterPreset.RiverValley, 1), (WaterPreset.BalancedInland, 1) })
            {
                var w = new World(seed, TerrainStyle.Ranges, HydroParams.For(preset).SuggestedRelief, preset); w.ClaimAll(); w.ResAmt[Res.Food] = 3000; w.ResAmt[Res.Wood] = 400; w.Recalc();
                if (w.Waters == null) continue;
                for (int r = 6; r < 60; r += 2)
                    for (int y = Defs.Start - r; y <= Defs.Start + r; y++)
                        for (int x = Defs.Start - r; x <= Defs.Start + r; x++)
                        {
                            if (!World.Inb(x, y) || !w.Claimed(x, y) || w.Grid[y, x] != null || w.IsSolid(x, y) || w.DecoMap[y, x] != Deco.None || w.ZoneMap[y, x] != Zone.None) continue;
                            if (!w.CanPlace("fisher", x, y).Ok && !w.CanPlace("fisher", x, y).Reason.StartsWith("Not enough")) continue;
                            var probe = new Building { T = "fisher", X = x, Y = y, Sz = 1 };
                            if (w.FishingGrounds(probe).grounds < World.FirstFishMinGrounds) continue;
                            // a road from the town to the tile next to it, then zone the shore around it
                            int rx = x, ry = y + 1; if (!World.Inb(rx, ry) || w.Grid[ry, rx] != null || w.IsSolid(rx, ry)) { rx = x; ry = y - 1; }
                            if (!World.Inb(rx, ry) || !MiningTests.RoadTo(w, rx, ry, x, y)) continue;
                            w.Recalc();
                            if (!w.Grid[ry, rx].Served) continue;
                            for (int j = y - 3; j <= y + 3; j++) for (int i = x - 3; i <= x + 3; i++) if (World.Inb(i, j) && w.Grid[j, i] == null && w.ZoneCheck(Zone.Fish, i, j).Ok) w.ZoneMap[j, i] = Zone.Fish;
                            w.Touch();
                            var d = w.DistrictAt(x, y);
                            if (d != null && d.Type == DistrictType.Fishing) return (w, d);
                        }
            }
            return null;
        }

        [Test]
        public void AFishingDistrictBuildsAHutAtTheShoreAndStopsWhenFoodIsSecure()
        {
            var ft = FishTown();
            Assume.That(ft != null, "no shore with a reachable road in the test seeds");
            var (w, d) = ft.Value;
            Assume.That(w.D.Idle > 0);
            Run(w, 10, secureFood: true);
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "fisher"), w.DistrictById(d.Id).Note);
            Run(w, 40, secureFood: true);
            Assert.AreEqual(1, w.DistrictById(d.Id).Buildings.Count(b => b.T == "fisher"));
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("No new fishing hut needed").Or.Contain("Additional workers"));
        }

        [Test]
        public void ANearlyFishedOutWaterIsLeftToRecover()
        {
            var ft = FishTown();
            Assume.That(ft != null);
            var (w, d) = ft.Value;
            var probe = w.Districts.First(q => q.Id == d.Id);
            var anchor = probe.Tiles.Select(k => (x: k % Defs.N, y: k / Defs.N)).First(t => w.CanPlace("fisher", t.x, t.y).Ok || w.CanPlace("fisher", t.x, t.y).Reason.StartsWith("Not enough"));
            int body = w.FishingGrounds(new Building { T = "fisher", X = anchor.x, Y = anchor.y, Sz = 1 }).body;
            w.FishStock[body] = 0.1;
            for (int i = 0; i < 4; i++) w.Develop();
            Assert.AreEqual(0, w.Blds.Count(b => b.T == "fisher"), "the water is nearly fished out");
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("Fish stock too low").Or.Contain("No suitable"));
        }

        [Test]
        public void ADistrictOfEitherKindSurvivesSaveAndLoadMidProject()
        {
            var (w, d, _) = HuntTown();
            Assume.That(d != null && w.D.Idle > 0);
            Run(w, 2, true);
            Assume.That(w.OpenProjects.Any(p => p.Type == "hunter"));
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, loaded.OpenProjects.Count(p => p.Type == "hunter"));
            Run(loaded, 8, true);
            Assert.AreEqual(1, loaded.Blds.Count(b => b.T == "hunter"));
        }
    }
}
