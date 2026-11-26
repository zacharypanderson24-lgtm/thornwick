// The wood chain (World.Warnings.cs, World.Economy.cs, World.SettlementWork.cs): logs become planks and charcoal only by consuming real logs, nothing is made from
// nothing, and a stalled building says what is wrong, why and what to do.
using System;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class TimberChainTests
    {
        const int N = Defs.N;

        /// <summary>The test town's roads with no producers, and one building of the given type beside a road that reaches the Town Hall.</summary>
        static (World w, Building b) Town(string type)
        {
            var w = new World(7); w.ClaimAll(); w.Drafting = false;
            SimParityTests.Layout(w, 10);
            foreach (var old in w.Blds.Where(o => o.T != "road" && o.T != "hall").ToList()) w.Remove(old.X, old.Y);
            for (int zy = 0; zy < N; zy++) for (int zx = 0; zx < N; zx++) w.ZoneMap[zy, zx] = Zone.None;   // the layout's housing zones would grow houses and spend the planks
            w.Touch(); w.Recalc();
            w.Prio = new[] { "woodwork", "charcoal", "farming", "timber", "quarrying", "mining", "trade" };
            int sz = Defs.PlacementSize(type);
            for (int y = Defs.Start - 9; y <= Defs.Start + 9; y++)
                for (int x = Defs.Start - 9; x <= Defs.Start + 9; x++)
                {
                    if (!w.CanPlace(type, x, y).Ok && !(w.CanAfford(type).Ok == false && w.Grid[y, x] == null)) continue;
                    var b = w.AddBld(type, x, y, sz); w.Recalc();
                    if (b.Served) { w.Peasants = 60; w.Recalc(); if (b.Workers > 0) return (w, b); }
                    w.Remove(x, y);
                }
            return (w, null);
        }

        static void Stock(World w, double logs)
        {
            w.ResAmt[Res.Food] = 400; w.ResAmt[Res.Gold] = 100; w.ResAmt[Res.Logs] = logs; w.ResAmt[Res.Wood] = 50; w.ResAmt[Res.Charcoal] = 0;
        }

        [Test]
        public void ASawmillTurnsLogsIntoPlanksOneForOneAndTheHallAtHalfYield()
        {
            var (w, saw) = Town("sawmill");
            Assume.That(saw != null, "no staffed sawmill site");
            Stock(w, 100);
            w.Tick();
            double cap = Defs.SawRate * saw.Eff;
            double lost = 100 - w.ResAmt[Res.Logs], gained = w.ResAmt[Res.Wood] - 50;
            Assert.AreEqual(cap + Defs.HallSaw, lost, 1e-6, "the sawmill and the Town Hall consumed exactly what they could saw");
            Assert.AreEqual(cap + Defs.HallSaw * Defs.HallYield, gained, 1e-6, "planks: one for one at the sawmill, half at the Town Hall");
        }

        [Test]
        public void NothingIsSawnWithoutLogs()
        {
            var (w, saw) = Town("sawmill");
            Assume.That(saw != null, "no staffed sawmill site");
            Stock(w, 0);
            for (int d = 0; d < 30; d++) { w.ResAmt[Res.Food] = 400; w.ResAmt[Res.Logs] = 0; w.Tick(); }
            Assert.AreEqual(50, w.ResAmt[Res.Wood], 1e-9, "no logs, no planks");
            var warn = w.BuildingWarnings(saw);
            var stopped = warn.FirstOrDefault(x => x.Title.Contains("Production stopped"));
            Assert.IsNotNull(stopped.Title, "the stalled sawmill says so");
            StringAssert.Contains("No logs", stopped.Why); StringAssert.Contains("forestry", stopped.Fix);
        }

        [Test]
        public void ACharcoalClampConsumesTwoLogsPerCharcoal()
        {
            var (w, clamp) = Town("charcoal");
            Assume.That(clamp != null, "no staffed clamp site");
            Stock(w, 100);
            w.Tick();
            double made = w.ResAmt[Res.Charcoal];
            Assert.Greater(made, 0);
            Assert.LessOrEqual(made, clamp.Eff * 2 + 1e-9);
            double logsUsedByClamp = 100 - w.ResAmt[Res.Logs] - (w.ResAmt[Res.Wood] - 50) / Defs.HallYield * 0 - Defs.HallSaw;   // the Town Hall also saws 1 log
            Assert.AreEqual(made * World.LogsPerCharcoal, logsUsedByClamp, 1e-6, "every charcoal cost its logs");
        }

        [Test]
        public void NoCharcoalWithoutLogsAndTheWarningSaysWhy()
        {
            var (w, clamp) = Town("charcoal");
            Assume.That(clamp != null, "no staffed clamp site");
            for (int d = 0; d < 20; d++) { Stock(w, 0); w.Tick(); }
            Assert.AreEqual(0, w.ResAmt[Res.Charcoal], 1e-9);
            var warn = w.BuildingWarnings(clamp).FirstOrDefault(x => x.Title.Contains("Production stopped"));
            Assert.IsNotNull(warn.Title);
            StringAssert.Contains("logs", warn.Why);
        }

        [Test]
        public void AnUnconnectedProducerIsToldToBuildARoad()
        {
            var w = new World(7); w.ClaimAll();
            var far = Enumerable.Range(0, N * N).Select(k => (x: k % N, y: k / N)).First(t => w.CanPlace("sawmill", t.x, t.y).Ok && Math.Abs(t.x - Defs.Start) > 24);
            var b = w.AddBld("sawmill", far.x, far.y, 2); w.Recalc();
            var warn = w.BuildingWarnings(b);
            Assert.AreEqual(1, warn.Count);
            StringAssert.Contains("road", warn[0].Fix); Assert.IsTrue(warn[0].Critical);
        }

        [Test]
        public void TheChainListsOnlyWhatIsImplementedAndWhatIsBuilt()
        {
            var (w, saw) = Town("sawmill");
            Assume.That(saw != null, "no staffed sawmill site");
            var chain = w.TimberChain();
            Assert.AreEqual(3, chain.Count, "lodge, sawmill and charcoal: there is no firewood step because nothing burns firewood");
            Assert.IsTrue(chain.Single(c => c.Via == "Sawmill").Built);
            Assert.IsFalse(chain.Single(c => c.Via == "Woodcutter Lodge").Built);
            Assert.IsFalse(chain.Single(c => c.Via == "Charcoal Clamp").Built);
        }

        [Test]
        public void EveryWarningHasAWhatAWhyAndAFix()
        {
            var (w, saw) = Town("sawmill");
            Assume.That(saw != null, "no staffed sawmill site");
            Stock(w, 0);
            foreach (var wr in w.BuildingWarnings(saw)) { Assert.IsNotEmpty(wr.What); Assert.IsNotEmpty(wr.Why); Assert.IsNotEmpty(wr.Fix); }
            StringAssert.Contains("Saws up to", w.SawmillStatus(saw));
        }
    }
}
