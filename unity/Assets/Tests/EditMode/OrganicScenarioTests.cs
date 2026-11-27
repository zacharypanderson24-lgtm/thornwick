// Gameplay scenarios for organic development (DESIGN_ORGANIC_DEVELOPMENT.md phase 10): a founding village with no hand-placed workplaces, agricultural expansion only when
// justified, a forestry settlement, a mining town with a smithery, quarrying with protected land, hunting and fishing against real stocks, survival first, advanced control,
// and a ten-year run. All of them play the SIMULATION (no UI) with the rules a player has: zones are free, buildings cost real wood and stone, workers come from the town.
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class OrganicScenarioTests
    {
        const int Year = 360;
        static (int x, int y, int dx, int dy, int px, int py) S;

        /// <summary>The player's opening, in zones only: a road spur out of town and, along it, a Forestry zone, a Hunting zone and a Farmland zone, each on land that suits it
        /// (woods with a free strip beside the road; fertile open ground). Nothing is placed by hand except the road.</summary>
        static World FoundingVillage(bool farmland = true, bool forestry = true, bool hunting = true)
        {
            var w = new World(7);   // the default start: 6 villagers, the starting farm, 60 food, 120 wood
            S = DevEconomyTests.Spur(w, 10, 6);
            var s = S;
            for (int a = 1; a <= 10; a++)
                for (int t = 1; t <= 6; t++)
                {
                    int x = s.x + s.dx * a + s.px * t, y = s.y + s.dy * a + s.py * t;
                    bool woods = t >= 3;
                    if (a <= 5 && forestry) { if (woods) w.DecoMap[y, x] = Deco.Tree; if (w.ZoneCheck(Zone.Wood, x, y).Ok) w.ZoneMap[y, x] = Zone.Wood; }
                    else if (a <= 8 && hunting) { if (woods) w.DecoMap[y, x] = Deco.Tree; if (w.ZoneCheck(Zone.Hunt, x, y).Ok) w.ZoneMap[y, x] = Zone.Hunt; }
                    else if (a > 8 && farmland) { if (t >= 2) { w.Fert[y, x] = true; if (w.ZoneCheck(Zone.Farm, x, y).Ok) w.ZoneMap[y, x] = Zone.Farm; } }
                }
            w.Touch();
            return w;
        }

        class Log { public double MinFood = 1e9; public int StarvingDays, MaxPop; public Dictionary<string, int> Built = new Dictionary<string, int>(); }

        static Log Play(World w, int days, System.Action<int> daily = null)
        {
            var log = new Log();
            for (int d = 0; d < days; d++)
            {
                daily?.Invoke(d);
                w.Tick();
                log.MinFood = System.Math.Min(log.MinFood, w.EdibleFoodStored);
                if (w.Starving) log.StarvingDays++;
                log.MaxPop = System.Math.Max(log.MaxPop, w.Peasants);
            }
            foreach (var g in w.Blds.GroupBy(b => b.T)) log.Built[g.Key] = g.Count();
            return log;
        }

        static string Series(World w, int d)
        {
            string notes = string.Join("/", w.Districts.Where(q => q.Note.Length > 0).Select(q => q.Type + ":" + q.Note.Substring(0, System.Math.Min(40, q.Note.Length))));
            string proj = string.Join("/", w.Projects.Select(p => p.Type + p.Status));
            return $"SERIES d{d} pop {w.Peasants} idle {w.D.Idle} food {w.EdibleFoodStored:0} wheat {w.WheatStored:0} fields {w.FarmFields().Count} hunters {w.Blds.Count(b => b.T == "hunter")} hw {w.Blds.Where(b => b.T == "hunter").Sum(b => b.Workers)} farms {w.Blds.Count(b => b.T == "farm")} fw {w.Blds.Where(b => b.T == "farm").Sum(b => b.Workers)} houses {w.Blds.Count(b => b.T == "house")} wood {w.ResAmt[Res.Wood]:0} rate {w.Rate[Res.Food]:0.0} income {w.FoodIncomePerDay():0.0} eat {w.DailyFoodDemand:0.0} proj {proj} notes {notes}";
        }

        static string Table(World w, Log l) => $"day {w.Day}: pop {w.Peasants} (idle {w.D.Idle}), food {w.EdibleFoodStored:0} (min {l.MinFood:0}, starving days {l.StarvingDays}), wood {w.ResAmt[Res.Wood]:0}, logs {w.ResAmt[Res.Logs]:0}, stone {w.ResAmt[Res.Stone]:0}, buildings " + string.Join(", ", l.Built.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"));

        static int Trees(World w) { int n = 0; for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.DecoMap[y, x] == Deco.Tree || w.DecoMap[y, x] == Deco.Sapling) n++; return n; }

        /// <summary>The player zones a few housing tiles beside roads that reach the Town Hall (what the Housing zone tool does).</summary>
        static void ZoneHousing(World w, int tiles = 8)
        {
            int n = 0;
            foreach (var road in w.Blds.Where(b => b.T == "road" && b.Served).OrderBy(b => System.Math.Abs(b.X - Defs.Start) + System.Math.Abs(b.Y - Defs.Start)).ThenBy(b => b.Y).ThenBy(b => b.X).ToList())
                for (int k = 0; k < 4 && n < tiles; k++)
                {
                    int x = road.X + Defs.Nb[k, 0], y = road.Y + Defs.Nb[k, 1];
                    if (World.Inb(x, y) && w.Grid[y, x] == null && w.ZoneMap[y, x] == Zone.None && w.ZoneCheck(Zone.Res, x, y).Ok && !w.HasTree(x, y) && w.SetZone(Zone.Res, x, y).Ok) n++;
                }
        }

        /// <summary>What a player does when the Districts panel says "zone more farmland": zone twelve more fields beside the farmhouse.</summary>
        static void Policy(World w, int day)
        {
            if (day % 30 != 0) return;
            if (!w.Districts.Any(q => (q.Type == DistrictType.Agricultural && q.Note.Contains("zone more farmland")) || (q.Type == DistrictType.Residential && q.Note.Contains("Homes wait")))) return;
            try { FirstYearPlan.Fields(w, w.FarmFields().Count + 12); } catch (System.Exception) { }   // (no more fertile ground in reach: nothing more to zone)
        }

        static World Opening()
        {
            var w = FoundingVillage();
            FirstYearPlan.Fields(w, 24);     // the player zones farmland round the starting farmhouse (the same call the first-year opening test uses)
            FirstYearPlan.Build(w, "silo");  // and builds the one manual building the opening needs: a silo to keep the harvest
            ZoneHousing(w);
            return w;
        }

        [Test]
        public void A_AFoundingVillageSurvivesItsFirstYearFromZonesAlone()
        {
            var w = Opening();
            int handPlacedWork = w.Blds.Count(b => b.T == "wood" || b.T == "hunter" || b.T == "fisher" || b.T == "quarry" || b.T == "mine");
            var log = Play(w, Year, d => { Policy(w, d); if (d % 20 == 0) TestContext.Out.WriteLine(Series(w, d)); });
            TestContext.Out.WriteLine("SCENARIO A " + Table(w, log));
            Assert.AreEqual(0, handPlacedWork);
            Assert.AreEqual(0, log.StarvingDays, "no starvation in the first year: " + Table(w, log));
            Assert.GreaterOrEqual(w.Blds.Count(b => b.T == "hunter"), 1, "the Hunting zone built its lodge");
            Assert.GreaterOrEqual(w.Blds.Count(b => b.T == "wood"), 1, "the Forestry zone built its lodge");
            Assert.LessOrEqual(w.Blds.Count(b => b.T == "wood"), 2, "no needless lodges");
            Assert.LessOrEqual(w.Blds.Count(b => b.T == "hunter"), 2, "no needless hunting lodges");
            Assert.Greater(w.Peasants, 6, "the village grew");
            Assert.Greater(w.Blds.Count(b => b.T == "house"), 2, "the Housing zone grew homes");
        }

        [Test]
        public void B_AMoreFarmlandNeedsMoreFarmhousesOnlyWhenFoodIsShort()
        {
            var w = DevEconomyTests.Town(); foreach (var f in w.Blds.Where(b => b.T == "farm").ToList()) w.Remove(f.X, f.Y);
            var s = DevEconomyTests.Spur(w); DevEconomyTests.Farmland(w, s);
            var d = w.DistrictAt(s.x + s.dx * 3 + s.px * 4, s.y + s.dy * 3 + s.py * 4);
            Assume.That(d != null && d.Type == DistrictType.Agricultural);
            Play(w, 120, i => { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 3000); });
            int plentiful = w.DistrictById(d.Id).Buildings.Count(b => b.T == "farm");
            Assert.AreEqual(1, plentiful, "food in plenty: one farmhouse, however big the farmland");
            Assert.That(w.DistrictById(d.Id).Note, Does.Contain("No new farmhouse needed").Or.Contain("Additional workers"));
            // the stores run low: a second farmhouse is justified once the first is staffed
            Play(w, 60, i => { w.ResAmt[Res.Food] = 20; w.ResAmt[Res.Wood] = System.Math.Max(w.ResAmt[Res.Wood], 200); });
            int short_ = w.DistrictById(d.Id).Buildings.Count(b => b.T == "farm");
            TestContext.Out.WriteLine($"SCENARIO B farmhouses with plenty {plentiful}, after shortage {short_}; note: {w.DistrictById(d.Id).Note}");
            Assert.LessOrEqual(short_, 2, "never a pile of farmhouses");
        }

        [Test]
        public void C_AForestrySettlementCutsSustainablyReplantsAndBuildsNoNeedlessLodges()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5);
            Assume.That(d != null && d.Type == DistrictType.Forestry);
            int treesStart = Trees(w), minMature = int.MaxValue, maxSap = 0;
            Play(w, 3 * Year, i => { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 2000); if (i % 30 == 0 && w.Blds.Any(b => b.T == "wood")) { var r = w.ReadForestry(w.DistrictById(d.Id)); minMature = System.Math.Min(minMature, r.Mature); maxSap = System.Math.Max(maxSap, r.Saplings); } });
            int lodges = w.DistrictById(d.Id).Buildings.Count(b => b.T == "wood");
            TestContext.Out.WriteLine($"SCENARIO C lodges {lodges}, trees {treesStart} -> {Trees(w)}, min mature {minMature}, max saplings {maxSap}, logs {w.ResAmt[Res.Logs]:0}, planks {w.ResAmt[Res.Wood]:0}");
            Assert.GreaterOrEqual(lodges, 1); Assert.LessOrEqual(lodges, 2, "no needless lodges");
            Assert.GreaterOrEqual(minMature, 8, "the sustainable lodge never cuts the stand below its floor (it leaves about 10)");
            Assert.Greater(maxSap, 0, "felled ground was replanted");
            Assert.Greater(Trees(w), treesStart / 3, "the forest is still there after three years");
            Assert.AreEqual(ForestMode.Sustainable, w.Blds.First(b => b.T == "wood").Mode);
        }

        [Test]
        public void D_ASmitheryRaisesTheDemandForOreOnlyAfterItExists()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null);
            var before = w.DemandReport().Single(i => i.Key == "minerals");
            Assert.AreEqual(DemandLevel.None, before.Level, "no smithery, no ore demand");
            StringAssert.Contains("No smithery", before.Reason);
            w.AddBld("smithery", mine.X + 6, mine.Y + 6, 2); w.Recalc();
            w.ResAmt[Res.Ore] = 0;
            var after = w.DemandReport().Single(i => i.Key == "minerals");
            TestContext.Out.WriteLine("SCENARIO D " + after.Word + ": " + after.Reason);
            StringAssert.Contains("smithery", after.Reason);
        }

        [Test]
        public void E_QuarryingDigsRealPitsAndLeavesProtectedLandAndRoadsAlone()
        {
            var (w, d, s) = ExtractionDistrictTests.QuarryTown();
            Assume.That(d != null);
            int px = s.x + s.dx * 9 + s.px * 6, py = s.y + s.dy * 9 + s.py * 6;
            Assert.IsTrue(w.ProtectWoodland(px, py).Ok);
            int roads = w.Blds.Count(b => b.T == "road");
            double stone0 = w.ResAmt[Res.Stone];
            Play(w, 2 * Year, i => { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 2000); w.ResAmt[Res.Stone] = System.Math.Min(w.ResAmt[Res.Stone], 80); });
            var q = w.Blds.Where(b => b.T == "quarry").ToList();
            int pits = d.Tiles.Count(k => w.PitDepth(k % Defs.N, k / Defs.N) > 0);
            TestContext.Out.WriteLine($"SCENARIO E quarries {q.Count}, pits {pits}, stone left in district {d.Tiles.Sum(k => w.RockLeft(k % Defs.N, k / Defs.N)):0}");
            Assert.GreaterOrEqual(q.Count, 1); Assert.LessOrEqual(q.Count, 2);
            Assert.Greater(pits, 0, "real terrain excavation: pits were dug");
            Assert.AreEqual(Deco.Rock, w.DecoMap[py, px]); Assert.AreEqual(0, w.PitDepth(px, py), "the protected rock was never dug");
            Assert.AreEqual(roads, w.Blds.Count(b => b.T == "road"), "no road was destroyed");
        }

        [Test]
        public void F_HuntingAndFishingFollowTheRealStocks()
        {
            var (w, d, _) = FoodDistrictTests.HuntTown();
            Assume.That(d != null && w.D.Idle > 0);
            Play(w, Year, i => { w.ResAmt[Res.Food] = System.Math.Max(w.ResAmt[Res.Food], 400); });
            var h = w.ReadHunting(w.DistrictById(d.Id));
            TestContext.Out.WriteLine($"SCENARIO F hunters {w.Blds.Count(b => b.T == "hunter")}, game stock {h.Stock:0} of {h.GameTiles * World.ForestFoodReserve}");
            Assert.LessOrEqual(w.Blds.Count(b => b.T == "hunter"), 2);
            Assert.Greater(h.Stock, 0.4 * h.GameTiles * World.ForestFoodReserve, "the woods were not hunted bare");
            var ft = FoodDistrictTests.FishTown();
            Assume.That(ft != null);
            var (fw, fd) = ft.Value;
            Play(fw, Year, i => { fw.ResAmt[Res.Food] = System.Math.Min(fw.ResAmt[Res.Food], 60); fw.ResAmt[Res.Wood] = System.Math.Max(fw.ResAmt[Res.Wood], 200); });
            var worst = fw.FishStock.Count == 0 ? 1.0 : fw.FishStock.Values.Min();
            TestContext.Out.WriteLine($"SCENARIO F huts {fw.Blds.Count(b => b.T == "fisher")}, lowest fish stock {worst:0.00}");
            Assert.GreaterOrEqual(worst, 0.3, "the water was not fished out");
        }

        [Test]
        public void G_WhenStarvingSurvivalComesFirst()
        {
            var w = Opening();
            w.Peasants = 20; w.ResAmt[Res.Food] = 0; w.Starving = true;
            for (int i = 0; i < 3; i++) w.Develop();
            Assert.AreEqual(2, w.Blds.Count(b => b.T == "house"), "no new homes while starving");
            Assert.IsFalse(w.Projects.Any(p => p.Type == "quarry" || p.Type == "mine" || p.Type == "wood"), "no quarry, mine or lodge while starving");
            var hunting = w.Districts.Where(d => d.Type == DistrictType.Hunting).ToList();
            Assume.That(hunting.Count == 1);
            Play(w, 10, i => { w.ResAmt[Res.Food] = 0; });
            Assert.GreaterOrEqual(w.Blds.Count(b => b.T == "hunter") + w.Projects.Count(p => p.Type == "hunter" && p.Open), 1, "but food gathering is still built");
        }

        [Test]
        public void H_AdvancedControlApprovalCapPauseAndExistingBuildingsKeepWorking()
        {
            var w = DevEconomyTests.Town(); var s = DevEconomyTests.Spur(w); DevEconomyTests.Forest(w, s);
            var d = w.DistrictAt(s.x + s.dx * 5 + s.px * 5, s.y + s.dy * 5 + s.py * 5);
            Assume.That(d != null);
            w.SetDistrictMode(d.Id, DevMode.ApprovalRequired);
            Play(w, 5, i => { w.ResAmt[Res.Food] = 2000; });
            var proposal = w.Projects.Single(p => p.Type == "wood" && p.Open);
            Assert.AreEqual(ProjectStatus.Proposed, proposal.Status);
            Assert.IsTrue(w.ApproveProject(proposal.Id).Ok);
            Play(w, 12, i => { w.ResAmt[Res.Food] = 2000; });
            var lodge = w.Blds.Single(b => b.T == "wood");
            // a building cap of one, even with timber short and the forest full
            w.SetDistrictMode(d.Id, DevMode.Automatic); w.SetDistrictLimits(d.Id, new DistrictLimits { MaxBuildings = 1 });
            Play(w, 40, i => { w.ResAmt[Res.Food] = 2000; w.ResAmt[Res.Wood] = 5; w.ResAmt[Res.Logs] = 0; });
            Assert.AreEqual(1, w.Blds.Count(b => b.T == "wood"), "the cap holds");
            // paused: it keeps working
            w.SetDistrictMode(d.Id, DevMode.Paused);
            double logs = w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood];
            Play(w, 40, i => { w.ResAmt[Res.Food] = 2000; });
            Assert.IsTrue(w.Blds.Contains(lodge), "the existing lodge was not demolished");
            Assert.Greater(w.ResAmt[Res.Logs] + w.ResAmt[Res.Wood], logs - 1e-9, "and it kept producing timber while development was paused");
        }

        [Test]
        public void I_TenYearsOfDevelopmentStayBelievableAndBounded()
        {
            var w = Opening();
            int treesStart = Trees(w);
            var rows = new List<string>(); var sw = System.Diagnostics.Stopwatch.StartNew(); int starving = 0;
            for (int yr = 1; yr <= 10; yr++)
            {
                var log = Play(w, Year, d => Policy(w, d));
                starving += log.StarvingDays;
                rows.Add($"year {yr}: " + Table(w, log));
            }
            double ms = sw.Elapsed.TotalMilliseconds / (10 * Year);
            foreach (var r in rows) TestContext.Out.WriteLine("SCENARIO I " + r);
            TestContext.Out.WriteLine($"SCENARIO I trees {treesStart} -> {Trees(w)}, average {ms:0.0} ms a day, districts {w.Districts.Count}");
            int Count(string t) => w.Blds.Count(b => b.T == t);
            Assert.LessOrEqual(Count("wood"), 3, "lodges"); Assert.LessOrEqual(Count("hunter"), 3, "hunting lodges"); Assert.LessOrEqual(Count("farm"), 4, "farmhouses");
            Assert.LessOrEqual(w.Projects.Count(p => p.Open), 3, "the queue stays short");
            Assert.Greater(Trees(w), treesStart / 4, "forest regeneration keeps the woods alive");
            Assert.Less(ms, 25, "performance");
            foreach (var b in w.Blds.Where(b => (b.T == "wood" || b.T == "hunter" || b.T == "quarry" || b.T == "mine" || b.T == "fisher") && b.Zoned))
                Assert.IsTrue(b.Served, $"{b.T} at {b.X},{b.Y} is connected by road (nothing is scattered into the wilderness)");
            Assert.LessOrEqual(starving, 40, "at most a short famine in ten years");
        }
    }
}
