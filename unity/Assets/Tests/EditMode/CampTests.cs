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
                var m = Camp(strength).Site.Map;
                var size = CampSize.Of(strength);
                Assert.AreEqual(size.Tents, m.Tents.Count);
                Assert.AreEqual(size.Piles + 1, m.Piles.Count, "goods piles plus the warlord's hoard");
                Assert.AreEqual(size.Fires, m.Fires.Count);
                Assert.AreEqual(size.Racks, m.Racks.Count);
                // the tents are pitched close: each one stands right beside a fire or another tent, and the camp is compact round the warlord
                foreach (var t in m.Tents)
                    Assert.IsTrue(m.Fires.Any(f => Max(System.Math.Abs(f.x - t.x), System.Math.Abs(f.y - t.y)) <= 1) || m.Tents.Any(o => o != t && Max(System.Math.Abs(o.x - t.x), System.Math.Abs(o.y - t.y)) <= 2),
                        $"strength {strength}: tent {t} stands on its own");
                double meanDist = m.Tents.Average(t => Max(System.Math.Abs(t.x - m.Chief.x), System.Math.Abs(t.y - m.Chief.y)));
                Assert.LessOrEqual(meanDist, size.Reach + 1.5, $"strength {strength}: the tents are close round the warlord");
                // every tent can still be walked up to from the edge (close tents never wall anything in)
                var reach = new bool[CampMap.Size, CampMap.Size]; var q = new Queue<(int x, int y)>();
                for (int i = 0; i < CampMap.Size; i++)
                    foreach (var (x, y) in new[] { (i, 0), (i, CampMap.Size - 1), (0, i), (CampMap.Size - 1, i) })
                        if (!reach[y, x] && m.Passable(x, y)) { reach[y, x] = true; q.Enqueue((x, y)); }
                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (CampMap.Inb(nx, ny) && !reach[ny, nx] && m.Passable(nx, ny)) { reach[ny, nx] = true; q.Enqueue((nx, ny)); }
                    }
                }
                bool Reachable((int x, int y) t) => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => CampMap.Inb(t.x + d.Item1, t.y + d.Item2) && reach[t.y + d.Item2, t.x + d.Item1]);
                Assert.IsTrue(m.Tents.All(Reachable), $"strength {strength}: every tent can be reached");
                Assert.IsTrue(Reachable(m.Chief), $"strength {strength}: the warlord's tent can be reached");
                Assert.IsTrue(m.Piles.Any(p => p.Res == Res.Gold), "the warlord keeps gold");
                var spots = new List<(int, int)>(m.Tents); spots.AddRange(m.Fires); spots.AddRange(m.Racks); spots.Add(m.Chief);
                Assert.AreEqual(spots.Count, spots.Distinct().Count(), "nothing is built on top of anything else");
                Assert.IsTrue(spots.All(t => !m.Passable(t.Item1, t.Item2)), "soldiers cannot walk through the camp's buildings");
                Assert.IsTrue(m.Piles.All(p => p.Amount > 0 && m.Ground[p.Y, p.X] == CampTile.Open && !m.HasTent(p.X, p.Y)));
                int edge = 0, edgeOpen = 0;
                for (int i = 0; i < CampMap.Size; i++)
                    foreach (var (x, y) in new[] { (i, 0), (i, CampMap.Size - 1), (0, i), (CampMap.Size - 1, i) }) { edge++; if (m.Passable(x, y)) edgeOpen++; }
                Assert.GreaterOrEqual(edgeOpen, edge * 6 / 10, "most of the edge is open ground to land on");
            }
            Assert.AreEqual(string.Join(",", Camp(3).Site.Map.Tents), string.Join(",", Camp(3).Site.Map.Tents), "deterministic");
        }

        [Test]
        public void ALargeArmyDestroysACampAndCarriesHomeWhatItCanAndAWeakOneLoses()
        {
            var camp = Camp(2);
            var win = new CampBattle(camp, new[] { Landing(0, 16, 14, 6), Landing(16, 0, 6, 4) });
            Assert.AreEqual(30, win.AliveAttackers);
            Assert.AreEqual(CampBattle.RaiderCount(2), win.AliveRaiders);
            Assert.AreEqual(1, win.Units.Count(u => u.Kind == BKind.Chief), "one warlord");
            Assert.AreEqual(1, win.Units.Count(u => u.Kind == BKind.Brute), "one brute guards the warlord at strength 2");
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
        public void ACampIsDrawnAsASmallWorldMadeByTheSettlementTerrainGenerator()
        {
            var camp = Camp(3); camp.Relief = Relief.Alpine;
            var site = camp.Site;
            var w = site.World; var map = site.Map;
            int ox = w.CampX0, oy = w.CampY0;
            Assert.IsTrue(w.IsCamp);
            Assert.AreSame(site, camp.Site, "the camp is made once and kept");
            Assert.IsFalse(w.InPlay(0, 0), "only the 32 x 32 square is in play");
            Assert.IsTrue(w.InPlay(ox, oy));
            Assert.IsTrue(w.InPlay(ox + 31, oy + 31));
            Assert.IsFalse(w.InPlay(ox + 32, oy));
            Assert.AreEqual(map.Tents.Count, w.Blds.Count(b => b.T == "tent"));
            Assert.AreEqual(map.Piles.Count, w.Blds.Count(b => b.T == "supplies"));
            Assert.AreEqual(map.Fires.Count, w.Blds.Count(b => b.T == "campfire"));
            Assert.AreEqual(map.Racks.Count, w.Blds.Count(b => b.T == "rack"));
            Assert.AreEqual("chieftent", w.Grid[oy + map.Chief.y, ox + map.Chief.x].T);
            foreach (var p in map.Piles) Assert.AreEqual((int)p.Res, w.Grid[oy + p.Y, ox + p.X].Var, "a pile shows its goods");
            foreach (var t in map.Tents) Assert.AreEqual("tent", w.Grid[oy + t.y, ox + t.x].T);
            for (int y = 0; y < CampMap.Size; y++)
                for (int x = 0; x < CampMap.Size; x++)
                {
                    if (map.Ground[y, x] == CampTile.Tree) Assert.AreEqual(Deco.Tree, w.DecoMap[oy + y, ox + x]);
                    if (map.Ground[y, x] == CampTile.Open) Assert.IsFalse(w.IsSolid(ox + x, oy + y), "open ground is not mountain or too steep");
                    if (map.HasStructure(x, y)) Assert.IsTrue(map.Buildable[y, x] || map.Chief == (x, y) , "built on level open ground");
                }

            // the very same generator as a settlement: the same seed and relief give the same heights, trees and rocks outside the camp's own square
            var settlement = new World(camp.Seed, TerrainStyle.Ranges, Relief.Alpine);
            int compared = 0;
            for (int y = 0; y < Defs.N; y += 3)
                for (int x = 0; x < Defs.N; x += 3)
                {
                    if (w.InPlay(x, y) || System.Math.Max(System.Math.Abs(x - Defs.Start), System.Math.Abs(y - Defs.Start)) < 30) continue;   // the settlement lays out its town valley and starting buildings there
                    Assert.AreEqual(settlement.DecoMap[y, x], w.DecoMap[y, x], $"tile {x},{y}");
                    Assert.AreEqual(settlement.Vh(x, y), w.Vh(x, y), $"height {x},{y}");
                    compared++;
                }
            Assert.Greater(compared, 1000);
            int mountains = 0; for (int y = 0; y < Defs.N; y++) for (int x = 0; x < Defs.N; x++) if (w.IsMountain(x, y)) mountains++;
            Assert.Greater(mountains, 0, "an alpine camp has mountains in its world");
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
