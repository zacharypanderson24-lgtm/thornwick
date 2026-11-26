// Phase 9 of the extraction overhaul: the economy holds up over years. A sustainable lodge keeps producing, an intensive one strips its forest, stone and ore are finite, a tick stays cheap.
using System;
using System.Diagnostics;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ExtractionEconomyTests
    {
        const int Year = 360;

        /// <summary>Logs a year of the lodge's work added to the town, counted the way the Town Hall's saw leaves them (1 log -> half a plank), then the stores are emptied.</summary>
        static double OneYear(World w)
        {
            w.ResAmt[Res.Logs] = 0; w.ResAmt[Res.Wood] = 0;
            for (int d = 0; d < Year; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Gold] = 100; w.Tick(); }
            return w.ResAmt[Res.Logs] + 2 * w.ResAmt[Res.Wood];
        }

        [Test]
        public void ASustainableLodgeKeepsProducingForYearsAndTheForestHolds()
        {
            var (w, lodge, trees) = ForestryAutoTests.Forest(60);
            var yearly = Enumerable.Range(0, 6).Select(_ => OneYear(w)).ToList();
            var f = w.ForestAround(lodge);
            Assert.Greater(yearly[0], 0, "it produces from the first year");
            Assert.Greater(yearly[5], 0, "and in the sixth: " + string.Join(", ", yearly.Select(y => y.ToString("0"))));
            Assert.GreaterOrEqual(f.Mature + f.Saplings + f.Stumps, 50, "the forest did not shrink away: trees, saplings and stumps still stand");
            Assert.GreaterOrEqual(f.Mature, World.SustainFloor);
        }

        [Test]
        public void AnIntensiveLodgeOutproducesItEarlyAndRunsOutLater()
        {
            double sustainable;
            { var (w, lodge, trees) = ForestryAutoTests.Forest(60); sustainable = OneYear(w); }
            var (w2, lodge2, trees2) = ForestryAutoTests.Forest(60);
            w2.SetForestMode(lodge2, ForestMode.Intensive);
            var yearly = Enumerable.Range(0, 8).Select(_ => OneYear(w2)).ToList();
            Assert.GreaterOrEqual(yearly[0], sustainable, "never fewer logs in the first year (a big forest is crew-limited either way; ForestryAutoTests compares a small one)");
            Assert.AreEqual(0, w2.ForestAround(lodge2).Mature, "the forest in reach is gone: " + string.Join(", ", yearly.Select(y => y.ToString("0"))));
            Assert.AreEqual(0, yearly[7], 1e-9, "and nothing more comes");
        }

        [Test]
        public void SaplingGrowthDependsOnTheSeasonAndOnMoisture()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.RiverValley);
            w.Day = 100;   // summer
            double min = 2, max = -1;
            for (int y = 0; y < Defs.N; y += 3) for (int x = 0; x < Defs.N; x += 3) { double r = w.SaplingGrowthRate(x, y); min = Math.Min(min, r); max = Math.Max(max, r); }
            Assert.Greater(max, min, "wet ground grows trees faster than dry ground");
            Assert.LessOrEqual(max, 1.0); Assert.GreaterOrEqual(min, 0.5);
            w.Day = 300;   // winter
            Assert.AreEqual(0, w.SaplingGrowthRate(10, 10), "nothing grows in winter");
        }

        [Test]
        public void AQuarryNeverGivesMoreStoneThanTheRockInItsArea()
        {
            var (w, q) = QuarryAutoTests.QuarryTown();
            Assume.That(q != null, "no quarry site in the test world");
            var r = w.QuarryReach(q); double reserve = r.StoneLeft + r.LockedStone, dug0 = w.Dig.Values.Sum(), produced = 0;
            for (int yr = 0; yr < 15; yr++)
            {
                w.ResAmt[Res.Stone] = 0;
                for (int d = 0; d < Year; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Gold] = 100; w.Tick(); }
                produced += w.ResAmt[Res.Stone];
            }
            double dug = w.Dig.Values.Sum() - dug0;
            Assert.LessOrEqual(dug, reserve + 1e-6, "no more was dug than lay in the area");
            Assert.LessOrEqual(produced, dug + 1e-6, "no more stone arrived than was dug");
            Assert.Greater(produced, 0);
        }

        [Test]
        public void TwoMinesNeverTakeMoreThanThereWasInTheGround()
        {
            var (w, mine) = MiningTests.MineTown(wantOre: true);
            Assume.That(mine != null, "no mine site in the test world");
            w.CaveInRiskScale = 0;
            double before = w.SurveyMineSite(mine.X, mine.Y).Reserve;
            for (int yr = 0; yr < 10; yr++) for (int d = 0; d < Year; d++) { w.ResAmt[Res.Food] = 500; w.ResAmt[Res.Wood] = 200; w.ResAmt[Res.Gold] = 100; w.ResAmt[Res.Ore] = 0; w.Tick(); }
            Assert.LessOrEqual(w.OreTaken.Values.Sum(), before + 1e-6, "the ore taken from reach cannot exceed what was there");
        }

        [Test]
        public void ATickWithAWorkingTownStaysCheap()
        {
            var w = FirstYearPlan.Run(7, 18, 120, zones: false);
            for (int d = 0; d < 20; d++) { w.ResAmt[Res.Food] = 500; w.Tick(); }   // warm up
            var sw = Stopwatch.StartNew();
            const int days = 200;
            for (int d = 0; d < days; d++) { w.ResAmt[Res.Food] = 500; w.Tick(); }
            double ms = sw.Elapsed.TotalMilliseconds / days;
            TestContext.Out.WriteLine($"PERF tick={ms:0.00} ms with {w.Blds.Count} buildings, {w.Blds.Count(b => b.T == "wood")} lodges, {w.Blds.Count(b => b.T == "quarry")} quarries, {w.Blds.Count(b => b.T == "mine")} mines");
            Assert.Less(ms, 25, "a day of the simulation must not become slow");
        }
    }
}
