using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class CampTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static RaiderCamp Camp(int strength = 2, int x = 340, int y = 300) => new RaiderCamp { Id = 0, X = x, Y = y, Strength = strength, Seed = 12345 + strength, Name = "Test camp" };

        static BattleGroup Landing(int x, int y, int inf, int arc) => new BattleGroup { X = x, Y = y, Infantry = inf, Archers = arc };

        static void Run(CampBattle b) { for (int i = 0; i < CampBattle.MaxTicks + 5 && !b.Over; i++) b.Step(); }

        static int Max(int a, int b) => System.Math.Max(a, b);

        [Test]
        public void CampsAreSpreadOverLandAwayFromHomeAndSomeAreWithinReach()
        {
            var camps = RaiderCamps.Generate(Map);
            Assert.AreEqual(RaiderCamps.Count, camps.Count);
            foreach (var c in camps) { Assert.IsTrue(Map.IsLand(c.X, c.Y), c.ToString()); Assert.That(c.Strength, Is.InRange(1, 5)); }
            for (int i = 0; i < camps.Count; i++) for (int j = i + 1; j < camps.Count; j++)
                Assert.GreaterOrEqual(Max(System.Math.Abs(camps[i].X - camps[j].X), System.Math.Abs(camps[i].Y - camps[j].Y)), RaiderCamps.MinApart);
            var home = Map.HomeSite;
            int near = camps.Count(c => Max(System.Math.Abs(c.X - home.X), System.Math.Abs(c.Y - home.Y)) <= RaiderCamps.NearMax);
            Assert.GreaterOrEqual(near, RaiderCamps.Count / 2, "half of them are within a few days of marching");
            Assert.IsTrue(camps.Select(c => c.Name).Distinct().Count() > 3, "they have different names");
            Assert.AreEqual(string.Join(";", camps.Select(c => c.ToString())), string.Join(";", RaiderCamps.Generate(Map).Select(c => c.ToString())), "the same world always has the same camps");
        }

        [Test]
        public void ACampMapHasOnlyTentsAndSupplyPilesAndAFreeEdge()
        {
            foreach (int strength in new[] { 1, 3, 5 })
            {
                var m = CampMap.Generate(Camp(strength));
                Assert.AreEqual(3 + strength, m.Tents.Count);
                Assert.AreEqual(2 + strength, m.Piles.Count);
                Assert.IsTrue(m.Piles.All(p => p.Amount > 0 && m.Ground[p.Y, p.X] == CampTile.Open && !m.HasTent(p.X, p.Y)));
                for (int i = 0; i < CampMap.Size; i++)
                    foreach (var (x, y) in new[] { (i, 0), (i, CampMap.Size - 1), (0, i), (CampMap.Size - 1, i) })
                        Assert.IsTrue(m.Passable(x, y), $"edge tile {x},{y} is open for landing");
            }
            Assert.AreEqual(string.Join(",", CampMap.Generate(Camp(3)).Tents), string.Join(",", CampMap.Generate(Camp(3)).Tents), "deterministic");
        }

        [Test]
        public void ALargeArmyDestroysACampAndCarriesHomeWhatItCanAndAWeakOneLoses()
        {
            var camp = Camp(2);
            var win = new CampBattle(camp, new[] { Landing(0, 16, 14, 6), Landing(16, 0, 6, 4) });
            Assert.AreEqual(30, win.AliveAttackers);
            Assert.AreEqual(3 + 2 * 2, win.AliveRaiders);
            Run(win);
            Assert.IsTrue(win.Over && win.Victory, win.Summary());
            Assert.AreEqual(win.RaidersTotal, win.RaidersKilled);
            int capacity = win.SurvivingInfantry * CampBattle.InfantryCarry + win.SurvivingArchers * CampBattle.ArcherCarry;
            Assert.Greater(win.LootTotal, 0);
            Assert.LessOrEqual(win.LootTotal, capacity, "they carry no more than they can");
            StringAssert.Contains("Victory", win.Summary());

            var lose = new CampBattle(Camp(5), new[] { Landing(0, 16, 2, 0) });
            Run(lose);
            Assert.IsTrue(lose.Over && !lose.Victory);
            Assert.AreEqual(0, lose.LootTotal);
            Assert.AreEqual(0, lose.SurvivingInfantry);
        }

        [Test]
        public void TheCampSleepsUntilSpottedAndAnArmyCanPullOut()
        {
            var b = new CampBattle(Camp(2), new[] { Landing(0, 0, 5, 0) });
            var raiders = b.Units.Where(u => !u.Attacker).Select(u => (u.X, u.Y)).ToList();
            b.Step();
            Assert.IsFalse(b.Alarm, "a corner landing is not yet seen");
            Assert.AreEqual(raiders, b.Units.Where(u => !u.Attacker).Select(u => (u.X, u.Y)).ToList(), "the raiders have not moved");
            b.Retreat();
            Assert.IsTrue(b.Over && b.Retreated && !b.Victory);
            Assert.AreEqual(0, b.LootTotal);
            Assert.AreEqual(5, b.SurvivingInfantry, "all of them go home");
        }

        [Test]
        public void ABattleIsDeterministic()
        {
            string Play() { var b = new CampBattle(Camp(3), new[] { Landing(0, 10, 10, 5), Landing(31, 20, 8, 4) }); Run(b); return b.Summary() + b.Tick; }
            Assert.AreEqual(Play(), Play());
        }

        static World Town(int infantry, int archers)
        {
            var w = new World(7);
            w.Drafting = false;
            w.Soldiers = infantry + archers; w.Archers = archers;
            return w;
        }

        [Test]
        public void AnInvasionTakesTroopsOutAndTheSurvivorsComeHomeWithLoot()
        {
            var w = Town(34, 10);
            var camp = Camp(2);
            w.SetCamps(new[] { camp }, 300, 300);
            Assert.IsFalse(w.CanInvade(camp, new[] { Landing(0, 5, 40, 0) }).Ok, "more infantry than you have");
            Assert.IsFalse(w.CanInvade(camp, new[] { Landing(5, 5, 10, 0) }).Ok, "not on the edge");
            Assert.IsFalse(w.CanInvade(camp, new[] { Landing(0, 5, 0, 0) }).Ok, "nobody chosen");
            var battle = w.BeginInvasion(camp, new[] { Landing(0, 16, 20, 8), Landing(16, 0, 6, 2) });
            Assert.IsNotNull(battle);
            Assert.AreEqual(8, w.Infantry, "34 - 26 infantry stay home");
            Assert.AreEqual(0, w.Archers, "all the archers went");
            Run(battle);
            Assert.IsTrue(battle.Victory, battle.Summary());
            w.FinishInvasion(battle);
            w.FinishInvasion(battle);   // settling twice changes nothing
            Assert.IsTrue(w.DestroyedCamps.Contains(camp.Id));
            Assert.AreEqual(1, w.Expeditions.Count);
            var e = w.Expeditions[0];
            Assert.AreEqual(w.Day + w.MarchDays(camp), e.ReturnDay);
            Assert.AreEqual(battle.LootTotal, e.Loot.Values.Sum());
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual(1, loaded.Expeditions.Count, "the army on the road is saved");
            Assert.IsTrue(loaded.DestroyedCamps.Contains(camp.Id), "and the destroyed camp");
            w.Day = e.ReturnDay;
            w.Tick();
            Assert.AreEqual(0, w.Expeditions.Count);
            Assert.That(w.Soldiers, Is.InRange(7 + battle.SurvivingInfantry + battle.SurvivingArchers, 8 + battle.SurvivingInfantry + battle.SurvivingArchers), "home again (the test town has few bunks, so one may be lost that day)");
            Assert.That(w.Archers, Is.InRange(battle.SurvivingArchers - 1, battle.SurvivingArchers));
            Assert.IsTrue(w.Events.Any(ev => ev.Contains("army is home")));
        }

        [Test]
        public void ACampIsDrawnAsASmallWorldWithTentsAndSuppliesInsideItsSquare()
        {
            var camp = Camp(3);
            var map = CampMap.Generate(camp);
            var w = World.CreateCamp(camp, map);
            Assert.IsTrue(w.IsCamp);
            Assert.IsFalse(w.InPlay(0, 0), "only the 32 x 32 square is in play");
            Assert.IsTrue(w.InPlay(World.CampOrigin, World.CampOrigin));
            Assert.IsTrue(w.InPlay(World.CampOrigin + 31, World.CampOrigin + 31));
            Assert.IsFalse(w.InPlay(World.CampOrigin + 32, World.CampOrigin));
            Assert.AreEqual(map.Tents.Count, w.Blds.Count(b => b.T == "tent"));
            Assert.AreEqual(map.Piles.Count, w.Blds.Count(b => b.T == "supplies"));
            foreach (var p in map.Piles) Assert.AreEqual((int)p.Res, w.Grid[World.CampOrigin + p.Y, World.CampOrigin + p.X].Var, "a pile shows its goods");
            foreach (var t in map.Tents) Assert.AreEqual("tent", w.Grid[World.CampOrigin + t.y, World.CampOrigin + t.x].T);
            int trees = 0; for (int y = 0; y < CampMap.Size; y++) for (int x = 0; x < CampMap.Size; x++) if (map.Ground[y, x] == CampTile.Tree) { trees++; Assert.AreEqual(Deco.Tree, w.DecoMap[World.CampOrigin + y, World.CampOrigin + x]); }
            Assert.Greater(trees, 0);
            Assert.IsFalse(new World(7).IsCamp, "a settlement has no play area limit");
            Assert.IsTrue(new World(7).InPlay(0, 0));
        }

        [Test]
        public void RaidsLaunchFromLivingCampsTowardsThemAndStopWhenTheyAreDestroyed()
        {
            var w = Town(0, 0);
            var east = Camp(3, 380, 300); east.Id = 0;
            w.SetCamps(new[] { east }, 300, 300);
            var r = w.BeginRaid(6);
            Assert.AreSame(east, r.Camp);
            Assert.AreEqual("east", r.From);
            StringAssert.Contains("Test camp", r.Summary() + r.Camp.Name);

            var w2 = Town(0, 0);
            w2.RaidMinPopulation = 0; w2.Day = 150;
            var c = Camp(2, 300, 360); c.Id = 3;
            w2.SetCamps(new[] { c }, 300, 300);
            for (int i = 0; i < 400 && w2.Raid == null; i++) { w2.ResAmt[Res.Food] = 400; w2.Tick(); }
            Assert.IsNotNull(w2.Raid, "a camp within reach raids a town worth raiding");
            Assert.AreSame(c, w2.Raid.Camp);
            Assert.AreEqual("south", w2.Raid.From);
            Assert.AreEqual(3 + 2 * 2, w2.Raid.Band);

            var w3 = Town(0, 0);
            w3.RaidMinPopulation = 0; w3.Day = 150;
            var gone = Camp(2, 300, 360); gone.Id = 5;
            w3.SetCamps(new[] { gone }, 300, 300);
            w3.DestroyedCamps.Add(5);
            for (int i = 0; i < 500; i++) { w3.ResAmt[Res.Food] = 400; w3.Tick(); }
            Assert.IsNull(w3.Raid, "a destroyed camp sends no raiders");

            var w4 = Town(0, 0);
            w4.RaidMinPopulation = 0; w4.Day = 150;
            for (int i = 0; i < 300; i++) { w4.ResAmt[Res.Food] = 400; w4.Tick(); }
            Assert.IsNull(w4.Raid, "no camps set, no random raids");
        }
    }
}
