using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class RaidTests
    {
        static World Town(bool barracks, int infantry, int archers)
        {
            var w = new World(7);
            w.ResAmt[Res.Wood] = 400; w.ResAmt[Res.Stone] = 100; w.ResAmt[Res.Gold] = 300; w.ResAmt[Res.Ore] = 100; w.ResAmt[Res.Flax] = 50; w.ResAmt[Res.Food] = 300;
            int c = Defs.Start;
            if (barracks)
            {
                var site = Enumerable.Range(0, 25 * 25).Select(i => (x: c - 12 + i % 25, y: c - 12 + i / 25)).First(t => w.CanPlace("barracks", t.x, t.y).Ok);
                Assert.IsTrue(w.Place("barracks", site.x, site.y).Ok);
            }
            w.Drafting = false;
            w.Soldiers = infantry + archers; w.Archers = archers;
            return w;
        }

        static void RunRaid(World w, int band)
        {
            var r = w.BeginRaid(band);
            for (int day = 0; day < 600 && !r.Over; day++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.IsTrue(r.Over, "the raid ends");
        }

        [Test]
        public void RaidersWalkInFromOutsideYourLandTakeLootAndRunAndSoldiersChangeTheOutcome()
        {
            var open = Town(false, 0, 0);
            var r = open.BeginRaid(12);
            Assert.AreEqual(12, r.Raiders.Count);
            Assert.IsFalse(open.Claimed(r.Raiders[0].X, r.Raiders[0].Y), "they start outside your land");
            Assert.IsTrue(r.Raiders.All(a => a.St == RaiderState.Waiting));
            double gold = open.ResAmt[Res.Gold];
            RunRaid(open, 12);
            Assert.AreEqual(0, open.Raid.Killed, "nobody to fight them");
            Assert.Greater(open.Raid.Escaped, 0);
            Assert.Greater(open.Raid.Stolen.Values.Sum(), 0);
            StringAssert.Contains("raiders came", open.Raid.Summary());

            var guarded = Town(true, 20, 10);
            RunRaid(guarded, 12);
            Assert.Greater(guarded.Raid.Killed, 0, "the garrison cuts raiders down");
            Assert.Less(guarded.Raid.Stolen.Values.Sum(), open.Raid.Stolen.Values.Sum(), "and less is carried off");
        }

        [Test]
        public void RaidersWantLootNotHousesAndAvoidWhatSoldiersCover()
        {
            var w = Town(true, 10, 0);
            var house = w.Blds.FirstOrDefault(b => b.T == "house");
            if (house != null) Assert.AreEqual(0, w.RaidValue(house));
            var hall = w.Blds.Single(b => b.T == "hall");
            Assert.Greater(w.RaidValue(hall), 0, "the Town Hall holds gold");
            var barracks = w.Blds.Single(b => b.T == "barracks");
            Assert.Greater(w.RaidDanger(barracks), w.RaidDanger(hall) - 1e-9);
            var bare = Town(true, 0, 0);
            Assert.AreEqual(0, bare.RaidDanger(bare.Blds.Single(b => b.T == "barracks")), "no soldiers, no danger");
            Assert.AreEqual(1.0, w.RaidDanger(barracks), 1e-9, "10 soldiers beside it");
        }

        [Test]
        public void RaidersAreRuntimeOnlyAndNeverSaved()
        {
            var w = Town(false, 0, 0);
            w.BeginRaid(6);
            for (int i = 0; i < 5; i++) w.Tick();
            var loaded = World.LoadJson(w.SaveJson());
            Assert.IsNull(loaded.Raid);
        }

        [Test]
        public void ARaidIsDeterministic()
        {
            var a = Town(true, 8, 4); var b = Town(true, 8, 4);
            RunRaid(a, 10); RunRaid(b, 10);
            Assert.AreEqual(a.Raid.Summary(), b.Raid.Summary());
        }
    }
}
