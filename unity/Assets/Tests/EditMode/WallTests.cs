using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>Palisades and gates in a fight (World.Walls.cs at home, CampMap and CampBattle in a settlement), and settlements that stay
    /// weak after losing to you (World.Conquest.cs).</summary>
    public class WallTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static World Home(int infantry = 80)
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            w.Soldiers = infantry; w.Archers = 0;
            w.ResAmt[Res.Gold] = 500;
            return w;
        }

        // ---------- the battle map's rules ----------

        /// <summary>A 12 x 12 town map with a palisade across it at y = 6 and a gate at x = 6.</summary>
        static CampMap Walled()
        {
            var m = new CampMap(12) { IsTownMap = true };
            for (int x = 0; x < 12; x++)
            {
                if (x == 6) { m.Gates.Add(6 * 12 + x); m.BarrierHp[6, x] = CampBattle.BattleGateHp; continue; }
                m.Blocked[6, x] = true; m.BarrierHp[6, x] = CampBattle.BattleWallHp;
            }
            return m;
        }

        [Test]
        public void AGateIsOpenUntilTheTownIsRousedThenItHoldsUntilBroken()
        {
            var m = Walled();
            Assert.IsTrue(m.Passable(6, 6), "an open gate lets anyone through");
            Assert.IsFalse(m.Passable(5, 6), "a palisade does not");
            int v = m.Version;
            m.ShutGates();
            Assert.IsFalse(m.Passable(6, 6), "a shut gate holds");
            Assert.IsTrue(m.IsBarrier(6, 6) && m.IsBarrier(5, 6));
            Assert.Greater(m.Version, v, "paths are worked out again");
            Assert.IsFalse(m.HitBarrier(6, 6, CampBattle.BattleGateHp - 1), "it takes many blows");
            Assert.IsTrue(m.HitBarrier(6, 6, 1), "and gives way at last");
            Assert.IsTrue(m.Passable(6, 6), "a broken gate hangs open");
            Assert.IsTrue(m.HitBarrier(3, 6, CampBattle.BattleWallHp));
            Assert.IsTrue(m.Passable(3, 6), "a broken palisade tile is a gap");
            CollectionAssert.AreEqual(new[] { (6, 6), (3, 6) }, m.Broken);
        }

        [Test]
        public void NoSwordReachesThroughAPalisadeButArrowsFlyOverIt()
        {
            var m = Walled();
            m.ShutGates();
            Assert.IsFalse(m.MeleeReach(2, 5, 3, 7), "two rows apart: not side by side");
            // diagonally past the end of a wall: one of the two tiles between them is open, so the blow lands
            var corner = new CampMap(6) { IsTownMap = true };
            corner.Blocked[2, 3] = true; corner.BarrierHp[2, 3] = CampBattle.BattleWallHp;
            Assert.IsTrue(corner.MeleeReach(2, 2, 3, 3), "round the end of a wall");
            corner.Blocked[3, 2] = true; corner.BarrierHp[3, 2] = CampBattle.BattleWallHp;
            Assert.IsFalse(corner.MeleeReach(2, 2, 3, 3), "not through a corner of palisade");
        }

        [Test]
        public void ARousedTownShutsItsGatesAndTheArmyMustBreakIn()
        {
            var w = Home(90);
            var s = w.Settlements.Where(o => o.Tier == SettlementTier.Town).OrderBy(w.SettlementKm).First();
            var camp = w.TownCamp(s);
            var m = camp.Site.Map;
            Assert.Greater(m.Gates.Count, 1, "a walled town has gates where its roads leave");
            Assert.Greater(m.Towers.Count, 3);
            Assert.IsFalse(m.GatesShut, "open while all is quiet");
            // land as far from the town as the map allows
            (int x, int y) land = (0, 0); int far = -1;
            for (int i = 0; i < m.N; i++)
                foreach (var (x, y) in new[] { (i, 0), (0, i), (i, m.N - 1), (m.N - 1, i) })
                {
                    int d = System.Math.Max(System.Math.Abs(x - m.Chief.x), System.Math.Abs(y - m.Chief.y));
                    if (m.Passable(x, y) && d > far) { far = d; land = (x, y); }
                }
            var b = w.BeginInvasion(camp, new[] { new BattleGroup { X = land.x, Y = land.y, Infantry = 90 } });
            Assert.IsNotNull(b);
            for (int i = 0; i < CampBattle.MaxTicks && !b.Alarm; i++) b.Step();
            Assert.IsTrue(b.Alarm, "the watch sees them coming");
            Assert.IsTrue(m.GatesShut, "and the gates are barred");
            var (wx0, wy0, wx1, wy1) = camp.Site.World.TownLayout.Walls.Value;
            int x0 = wx0 - camp.Site.X0, y0 = wy0 - camp.Site.Y0, x1 = wx1 - camp.Site.X0, y1 = wy1 - camp.Site.Y0;
            bool Inside(BUnit u) => u.X > x0 && u.X < x1 && u.Y > y0 && u.Y < y1;
            bool outsideAtAlarm = b.Units.Where(u => u.Attacker && u.OnField).All(u => !Inside(u));
            for (int i = 0; i < CampBattle.MaxTicks && !b.Over; i++)
            {
                b.Step();
                if (outsideAtAlarm && m.Broken.Count == 0)
                    Assert.IsFalse(b.Units.Any(u => u.Attacker && u.OnField && Inside(u)), "nobody gets in before a gate or the palisade gives way");
            }
            if (outsideAtAlarm) Assert.Greater(m.Broken.Count, 0, "they hacked their way in");
            Assert.IsTrue(b.Over);
            // the town's picture follows the fight
            var tw = camp.Site.World;
            Assert.IsTrue(tw.MirrorBattle(m));
            Assert.IsTrue(tw.KeepGatesShut);
            foreach (var (bx, by) in m.Broken)
            {
                int x = camp.Site.X0 + bx, y = camp.Site.Y0 + by;
                Assert.IsTrue(tw.Grid[y, x] == null || tw.GateAt(x, y) == World.GateState.Broken, "a broken wall is gone, a broken gate hangs broken");
            }
        }

        // ---------- at home: raiders and your palisade ----------

        [Test]
        public void RaidersCannotWalkThroughYourPalisadeAndHackAtItInstead()
        {
            var w = new World(7);
            w.ResAmt[Res.Food] = 300; w.Drafting = false; w.Soldiers = 0; w.Archers = 0;
            int c = Defs.Start, r = 9;
            // a ring of palisade round the middle of the town (the roads through it are gates)
            for (int t = -r; t <= r; t++)
                foreach (var (x, y) in new[] { (c + t, c - r), (c + t, c + r), (c - r, c + t), (c + r, c + t) })
                    if (w.Grid[y, x] == null && !w.IsSolid(x, y)) { w.DecoMap[y, x] = Deco.None; w.AddBld("wall", x, y); }
            w.Recalc();
            int walls = w.Blds.Count(b => b.T == "wall");
            bool Inside(int x, int y) => System.Math.Abs(x - c) < r && System.Math.Abs(y - c) < r;
            var raid = w.BeginRaid(10);
            bool everShut = false;
            for (int day = 0; day < 600 && !raid.Over; day++)
            {
                w.ResAmt[Res.Food] = 300; w.Tick();
                everShut |= w.GatesShut;
                bool breached = w.Blds.Count(b => b.T == "wall") < walls || w.BarrierDamage.Keys.Any(k => w.GateBroken(k % Defs.N, k / Defs.N));
                if (!breached)
                    Assert.IsFalse(raid.Raiders.Any(a => Inside(a.X, a.Y) && !Inside(a.Px, a.Py)), "no raider walks through a standing palisade");
            }
            Assert.IsTrue(raid.Over);
            Assert.IsTrue(everShut, "the gates were shut while the raid was on");
            Assert.IsFalse(w.GatesShut, "and open again after it");
            for (int day = 0; day < 60; day++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.IsEmpty(w.BarrierDamage, "the town mends its palisade and gates in quiet times");
        }

        [Test]
        public void KeepingTheGatesShutIsSaved()
        {
            var w = new World(7) { KeepGatesShut = true };
            Assert.IsTrue(w.GatesShut);
            var loaded = World.LoadJson(w.SaveJson());
            Assert.IsTrue(loaded.KeepGatesShut);
        }

        // ---------- a settlement that lost to you ----------

        [Test]
        public void DefendersLostAreMissingUntilReplacedMonthByMonth()
        {
            var w = Home();
            var s = w.Settlements.Where(o => o.Tier == SettlementTier.Town).OrderBy(w.SettlementKm).First();
            int full = World.Garrison(s);
            s.DefendersLost = full * 0.6;
            Assert.Less(World.DefendersOf(s), full, "fewer defenders after losing men to you");
            Assert.AreEqual(World.DefendersOf(s), w.DefendersHome(w.TownCamp(s)), "and that is who fights the next attack");
            double before = s.DefendersLost;
            for (int d = 0; d < World.SettlementMonth + 1; d++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.Less(s.DefendersLost, before, "it replaces them a month at a time");
            for (int d = 0; d < World.SettlementMonth * 6; d++) { w.ResAmt[Res.Food] = 300; w.Tick(); }
            Assert.AreEqual(0, s.DefendersLost, 1e-9, "until it is at full strength again");
            Assert.AreEqual(World.Garrison(s), World.DefendersOf(s));
        }

        [Test]
        public void ASackedSettlementLiesWasteAndRecoversSlowly()
        {
            var w = Home();
            var s = w.Settlements.Where(o => o.Tier >= SettlementTier.Village).OrderBy(w.SettlementKm).First();
            int tradeBefore = World.MonthlyTrade(s);
            s.DevastatedUntil = w.Day + World.SackWasteDays; s.Devastated = true; s.DefendersLost = World.Garrison(s);
            Assert.Less(World.MonthlyTrade(s), tradeBefore, "it trades far less");
            Assert.AreEqual(1, World.DefendersOf(s), "and has hardly anyone to defend it");
            Assert.Less(World.RecoverShare(s), 0.25, "it replaces its defenders slowly");
            double pop = s.Population;
            var save = World.LoadJson(w.SaveJson());
            save.SetSettlements(Settlements.Generate(Map));
            var ls = save.SettlementById(s.Id);
            Assert.AreEqual((s.DevastatedUntil, true), (ls.DevastatedUntil, ls.Devastated), "saved");
            Assert.AreEqual(s.DefendersLost, ls.DefendersLost, 1e-6);
            for (int d = 0; d < World.SackWasteDays + World.SettlementMonth + 1; d++) { w.ResAmt[Res.Food] = 300; w.ResAmt[Res.Gold] = 100; w.Tick(); }
            Assert.IsFalse(s.Devastated, "after a year it has recovered");
            Assert.LessOrEqual(s.Population, pop + 1e-9 + pop * 0.01, "and did not grow while it lay waste");
        }
    }
}
