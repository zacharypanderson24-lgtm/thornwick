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
        public void TheMilitiaFightsWithTradeToolsFarWeakerThanSoldiersUnlessSwitchedOff()
        {
            var w = Town(false, 0, 0);
            w.Tick();
            var farm = w.Blds.First(b => b.T == "farm");
            farm.Workers = 6;
            Assert.AreEqual("pitchforks", World.MilitiaTool("farm").Value.tool);
            Assert.AreEqual("bows", World.MilitiaTool("hunter").Value.tool);
            Assert.IsNull(World.MilitiaTool("house"));
            double militia = w.MilitiaPower(farm);
            Assert.Greater(militia, 0);
            Assert.Less(militia, 1.0, "six farmers count for less than one soldier");
            var backed = Town(true, 3, 0); backed.Tick();
            var farm2 = backed.Blds.First(b => b.T == "farm"); farm2.Workers = 6;
            Assert.Greater(backed.MilitiaPower(farm2), 0, "the militia still fights beside soldiers");
            backed.MilitiaFights = false;
            Assert.AreEqual(0, backed.MilitiaPower(farm2), "unless you switch it off");
            var off = World.LoadJson(backed.SaveJson());
            Assert.IsFalse(off.MilitiaFights, "the setting is saved");
            Assert.IsTrue(World.LoadJson(w.SaveJson()).MilitiaFights);
            Assert.Less(World.MilitiaTool("farm").Value.dmg, 0.5 / 5, "a pitchfork is far weaker than a soldier");
            var r = w.BeginRaid(12);
            for (int day = 0; day < 600 && !r.Over; day++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.IsTrue(r.Over);
            Assert.AreEqual(0, r.SoldiersLost);
        }

        [Test]
        public void SoldiersMarchOutAndFightRaidersWhereYouCanSeeIt()
        {
            var w = Town(true, 10, 6);
            var r = w.BeginRaid(12);
            Assert.AreEqual(16, r.Fighters.Count, "every soldier is a fighter waiting at its post");
            Assert.IsTrue(r.Fighters.All(f => f.St == FighterState.Idle));
            bool marched = false, swung = false, shot = false, raiderHurt = false;
            int maxOut = 0;
            for (int day = 0; day < 400 && !(r.Over && r.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead)); day++)
            {
                w.ResAmt[Res.Food] = 300; w.Tick();
                maxOut = System.Math.Max(maxOut, r.Fighters.Count(f => f.St == FighterState.Engage));
                marched |= r.Fighters.Any(f => f.St == FighterState.Engage && (f.X != f.Px || f.Y != f.Py));
                swung |= r.Fighters.Any(f => f.Attacked && f.Kind == Troop.Infantry);
                shot |= r.Fighters.Any(f => f.Shooting && f.Kind == Troop.Archer);
                raiderHurt |= r.Raiders.Any(a => a.Hurt);
            }
            Assert.IsTrue(marched, "soldiers walk out to meet the raiders");
            Assert.IsTrue(swung, "infantry swing at them");
            Assert.IsTrue(shot, "archers loose arrows");
            Assert.IsTrue(raiderHurt, "raiders are hit");
            Assert.Greater(maxOut, 3, "a real force is out on the field");
            Assert.IsTrue(r.Fighters.All(f => f.St == FighterState.Idle || f.St == FighterState.Dead), "the survivors are back at their posts");
            Assert.Greater(r.Killed, 0);
        }

        [Test]
        public void SoldiersWhoFallAreGoneFromYourArmy()
        {
            var w = Town(true, 2, 0);   // two infantry against a big band
            RunRaid(w, 18);
            Assert.Greater(w.Raid.SoldiersLost, 0, "some of the two fell");
            Assert.AreEqual(2 - w.Raid.SoldiersLost, w.Soldiers);
            Assert.AreEqual(w.Soldiers, w.Infantry + w.Archers);
            Assert.GreaterOrEqual(w.Archers, 0);

            var bow = Town(true, 0, 2);
            RunRaid(bow, 18);
            Assert.AreEqual(bow.Soldiers, bow.Archers, "archers lost come off the archer count too");
            Assert.AreEqual(2 - bow.Raid.SoldiersLost, bow.Archers);
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
