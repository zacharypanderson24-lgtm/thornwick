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

        /// <summary>The first camps are still exactly where the old generator (14 camps, 45 km apart) put them, so saved camp ids keep their meaning.</summary>
        [Test]
        public void TheOriginalCampsStayPutWhenMoreAreAdded()
        {
            var home = Map.HomeSite; int seed = Map.Seed;
            var old = new List<(int x, int y)>();
            for (int attempt = 0; attempt < 6000 && old.Count < 14; attempt++)
            {
                int x = 8 + (int)(Noise.Hash2(attempt, seed, 701) * (WorldMap.Size - 16)), y = 8 + (int)(Noise.Hash2(attempt, seed, 702) * (WorldMap.Size - 16));
                if (!Map.IsLand(x, y)) continue;
                int dist = Max(System.Math.Abs(x - home.X), System.Math.Abs(y - home.Y));
                if (dist < 50 || (old.Count < 7 && dist > 220)) continue;
                if (old.Any(c => Max(System.Math.Abs(c.x - x), System.Math.Abs(c.y - y)) < 45)) continue;
                old.Add((x, y));
            }
            var camps = RaiderCamps.Generate(Map);
            Assert.AreEqual(old, camps.Take(14).Select(c => (c.X, c.Y)).ToList());
            Assert.Greater(camps.Count, old.Count, "and there are more of them now");
        }

        [Test]
        public void CampsAreSpreadOverLandAwayFromHomeAndSomeAreWithinReach()
        {
            var camps = RaiderCamps.Generate(Map);
            Assert.AreEqual(RaiderCamps.Count, camps.Count);
            Assert.AreEqual(Enumerable.Range(0, camps.Count), camps.Select(c => c.Id), "ids run 0, 1, 2...");
            foreach (var c in camps) { Assert.IsTrue(Map.IsLand(c.X, c.Y), c.ToString()); Assert.That(c.Strength, Is.InRange(1, 5)); }
            for (int i = 0; i < camps.Count; i++) for (int j = i + 1; j < camps.Count; j++)
            {
                int apart = Max(System.Math.Abs(camps[i].X - camps[j].X), System.Math.Abs(camps[i].Y - camps[j].Y));
                Assert.GreaterOrEqual(apart, j < RaiderCamps.FirstCount ? RaiderCamps.MinApart : RaiderCamps.MoreApart, $"camps {i} and {j}");
            }
            var home = Map.HomeSite;
            int near = camps.Count(c => Max(System.Math.Abs(c.X - home.X), System.Math.Abs(c.Y - home.Y)) <= RaiderCamps.NearMax);
            Assert.GreaterOrEqual(near, RaiderCamps.FirstCount / 2 + (RaiderCamps.Count - RaiderCamps.FirstCount) / 2, "half of them are within a few days of marching");
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

            // a looted pile disappears from the camp; one with goods left stays
            Assert.IsFalse(w.ClearLootedPiles(map), "nothing to take away while every pile holds goods");
            var looted = map.Piles[0];
            int full = map.Piles.Count, version = w.Version;
            looted.Amount = 0;
            Assert.IsTrue(w.ClearLootedPiles(map));
            Assert.IsNull(w.Grid[oy + looted.Y, ox + looted.X], "the emptied pile is gone");
            Assert.AreEqual(full - 1, w.Blds.Count(b => b.T == "supplies"), "the others stay");
            Assert.Greater(w.Version, version, "the view redraws");
            Assert.IsFalse(w.ClearLootedPiles(map), "taken away once");
            Assert.IsFalse(new World(7).ClearLootedPiles(map), "a settlement is left alone");
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
            w2.RaidMinPopulation = 0; w2.RaidSlowdown = 1; w2.Day = 150;
            var c = Camp(2, 300, 360); c.Id = 3;
            w2.SetCamps(new[] { c }, 300, 300);
            for (int i = 0; i < 400 && w2.Raid == null; i++) { w2.ResAmt[Res.Food] = 400; w2.Tick(); }
            Assert.IsNotNull(w2.Raid, "a camp within reach raids a town worth raiding");
            Assert.AreSame(c, w2.Raid.Camp);
            Assert.AreEqual("south", w2.Raid.From);
            Assert.AreEqual(3 + 2 * 2, w2.Raid.Band);

            var w3 = Town(0, 0);
            w3.RaidMinPopulation = 0; w3.RaidSlowdown = 1; w3.Day = 150;
            var gone = Camp(2, 300, 360); gone.Id = 5;
            w3.SetCamps(new[] { gone }, 300, 300);
            w3.DestroyedCamps.Add(5);
            for (int i = 0; i < 500; i++) { w3.ResAmt[Res.Food] = 400; w3.Tick(); }
            Assert.IsNull(w3.Raid, "a destroyed camp sends no raiders");

            var w4 = Town(0, 0);
            w4.RaidMinPopulation = 0; w4.RaidSlowdown = 1; w4.Day = 150;
            for (int i = 0; i < 300; i++) { w4.ResAmt[Res.Food] = 400; w4.Tick(); }
            Assert.IsNull(w4.Raid, "no camps set, no random raids");
        }

        /// <summary>Ticks until the camp's warband sets out, then returns the day it was first spotted and the day the raid began.</summary>
        static (int leave, int arrive, int spotted, int raided, List<string> events) MarchOn(World w)
        {
            var events = new List<string>();
            for (int i = 0; i < 500 && w.Warband == null && w.Raid == null; i++) { w.ResAmt[Res.Food] = 400; events.AddRange(w.Events); w.Events.Clear(); w.Tick(); }
            Assert.IsNotNull(w.Warband, "the camp sends a warband before any raid");
            Assert.IsNull(w.Raid, "the raid has not begun while the warband marches");
            // a tick runs on day w.Day and then moves the clock on, so what happened in the last tick happened on w.Day - 1
            int leave = w.Warband.LeaveDay, arrive = w.Warband.ArriveDay, spotted = w.Warband.Spotted ? w.Day - 1 : -1;
            events.AddRange(w.Events); w.Events.Clear();
            for (int i = 0; i < 40 && w.Raid == null; i++)
            {
                w.ResAmt[Res.Food] = 400; w.Tick();
                if (spotted < 0 && w.Warband != null && w.Warband.Spotted) spotted = w.Day - 1;
                events.AddRange(w.Events); w.Events.Clear();
            }
            Assert.IsNotNull(w.Raid, "the raid begins when the warband arrives");
            return (leave, arrive, spotted, w.Day - 1, events);
        }

        [Test]
        public void AWarbandMarchesFromItsCampIsSpottedAndThenRaids()
        {
            var w = Town(0, 0);
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            var c = Camp(2, 300, 390); c.Id = 4;   // 90 km south: 6 days of marching
            w.SetCamps(new[] { c }, 300, 300);
            var (leave, arrive, spotted, raided, events) = MarchOn(w);
            Assert.AreEqual(w.MarchDays(c), arrive - leave, "it marches as long as an army would");
            Assert.AreEqual(6, arrive - leave);
            Assert.AreEqual(arrive - World.ScoutSpotDays, spotted, "scouts see it two days out");
            Assert.AreEqual(arrive, raided, "the raid begins the day it arrives");
            Assert.IsNull(w.Warband);
            Assert.AreSame(c, w.Raid.Camp);
            Assert.AreEqual("south", w.Raid.From);
            Assert.AreEqual(3 + 2 * 2, w.Raid.Band);
            Assert.IsTrue(events.Any(e => e.Contains("spotted a warband of 7 raiders") && e.Contains("from the south")), string.Join(" | ", events));
            Assert.IsTrue(events.Any(e => e.Contains($"their camp has only {System.Math.Max(0, CampSize.Of(2).Raiders - 7) + CampSize.Of(2).Brutes + 1} fighters at home: a chance to strike it")), string.Join(" | ", events));
            Assert.IsTrue(events.Any(e => e.Contains("is here")), string.Join(" | ", events));
        }

        [Test]
        public void AWatchtowerSpotsAWarbandSooner()
        {
            var w = Town(0, 0);
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            w.AddBld("watchtower", Defs.Start + 6, Defs.Start + 6);
            Assert.AreEqual(World.TowerSpotDays, w.SpotDays);
            var c = Camp(2, 300, 390); c.Id = 4;
            w.SetCamps(new[] { c }, 300, 300);
            var (leave, arrive, spotted, _, events) = MarchOn(w);
            Assert.AreEqual(arrive - World.TowerSpotDays, spotted, "lookouts see it five days out");
            Assert.IsTrue(events.Any(e => e.StartsWith("Lookouts on your watchtower")), string.Join(" | ", events));
        }

        [Test]
        public void AWarbandOnTheMarchIsSavedAndStillArrives()
        {
            var w = Town(0, 0);
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            var c = Camp(2, 300, 390); c.Id = 4;
            w.SetCamps(new[] { c }, 300, 300);
            for (int i = 0; i < 500 && w.Warband == null; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.IsNotNull(w.Warband);
            var before = w.Warband;
            var mid = w.WarbandCell(before);
            Assert.That(mid.y, Is.InRange(300.0, 390.0), "it is on the road between its camp and your settlement");

            var loaded = World.LoadJson(w.SaveJson());
            Assert.IsNotNull(loaded.Warband, "the warband is saved");
            Assert.AreEqual(before.CampId, loaded.Warband.CampId);
            Assert.AreEqual(before.Band, loaded.Warband.Band);
            Assert.AreEqual(before.ArriveDay, loaded.Warband.ArriveDay);
            Assert.AreEqual(before.Spotted, loaded.Warband.Spotted);
            loaded.RaidMinPopulation = 0; loaded.RaidSlowdown = 1;
            loaded.SetCamps(new[] { c }, 300, 300);
            Assert.IsNotNull(loaded.Warband, "handing the camps over again does not call it off");
            for (int i = 0; i < 40 && loaded.Raid == null; i++) { loaded.ResAmt[Res.Food] = 400; loaded.Tick(); }
            Assert.IsNotNull(loaded.Raid, "after loading it still arrives and raids");
            Assert.AreSame(c, loaded.Raid.Camp);
        }

        [Test]
        public void ACampIsWeakerWhileItsRaidersAreAwayAndAfterItLosesThem()
        {
            var w = Town(0, 0);
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            var c = Camp(5, 300, 390); c.Id = 4;   // 6 days away; 24 raiders, 4 brutes and the warlord
            w.SetCamps(new[] { c }, 300, 300);
            int full = CampBattle.RaiderCount(5), raiders = CampSize.Of(5).Raiders;
            Assert.AreEqual(full, w.CampDefenders(c), "everyone is home before any raid");

            for (int i = 0; i < 500 && w.Warband == null; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.IsNotNull(w.Warband);
            int band = w.Warband.Band;
            Assert.AreEqual(3 + 2 * 5, band);
            Assert.AreEqual(band, w.CampRaidersAway(c), "the warband on the march is away from camp");
            Assert.AreEqual(raiders - band, w.CampRaidersHome(c));
            Assert.AreEqual(full - band, w.CampDefenders(c), "brutes and the warlord stay home");
            var battle = new CampBattle(c, new List<BattleGroup>(), w.CampRaidersHome(c));
            Assert.AreEqual(full - band, battle.AliveRaiders, "an invasion now faces fewer raiders");
            Assert.AreEqual(full - band, battle.RaidersTotal);
            Assert.AreEqual(1, battle.Units.Count(u => u.Kind == BKind.Chief));
            Assert.AreEqual(CampSize.Of(5).Brutes, battle.Units.Count(u => u.Kind == BKind.Brute));

            for (int i = 0; i < 40 && w.Raid == null; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.IsNotNull(w.Raid);
            Assert.AreEqual(band, w.CampRaidersAway(c), "the band raiding you is still away");

            // end the raid: five cut down, the rest got away
            var r = w.Raid;
            for (int i = 0; i < r.Raiders.Count; i++) { if (i < 5) { r.Raiders[i].St = RaiderState.Dead; r.Killed++; } else { r.Raiders[i].St = RaiderState.Gone; r.Escaped++; } }
            w.ResAmt[Res.Food] = 400; w.Tick();
            Assert.IsTrue(r.Over);
            Assert.AreEqual(5, w.CampRaidersLost(c), "the dead are missing from camp");
            Assert.AreEqual(band - 5, w.CampRaidersAway(c), "the survivors are marching back");
            Assert.AreEqual(raiders - band, w.CampRaidersHome(c));

            var loaded = World.LoadJson(w.SaveJson());
            loaded.SetCamps(new[] { c }, 300, 300);
            Assert.AreEqual(5, loaded.CampRaidersLost(c), "losses are saved");
            Assert.AreEqual(band - 5, loaded.CampRaidersAway(c), "so are raiders marching back");

            for (int i = 0; i < w.MarchDays(c); i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.AreEqual(0, w.CampRaidersAway(c), "the survivors are home again");
            Assert.AreEqual(raiders - 5, w.CampRaidersHome(c), "but the dead are not replaced yet");
            for (int i = 0; i < 5 * World.CampRefillDays; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.AreEqual(full, w.CampDefenders(c), "in time the camp replaces them");
            Assert.IsFalse(w.CampForces.ContainsKey(c.Id));
        }

        [Test]
        public void ACampThatLostRaidersSendsASmallerWarband()
        {
            var w = Town(0, 0);
            w.RaidMinPopulation = 0; w.RaidSlowdown = 1; w.Day = 150;
            var c = Camp(5, 300, 390); c.Id = 4;
            w.SetCamps(new[] { c }, 300, 300);
            w.CampForces[c.Id] = new CampForce { Lost = 20 };
            for (int i = 0; i < 800 && w.Warband == null; i++) { w.ResAmt[Res.Food] = 400; w.Tick(); }
            Assert.IsNotNull(w.Warband);
            Assert.AreEqual(3 + 2 * 5, w.Warband.Band + w.CampRaidersLost(c), "the band is short by the raiders the camp has lost");
        }

        // ---------- orders for a landing ----------

        static BattleGroup Landing(int x, int y, int inf, int arc, BattleOrder order, TargetPriority prio = TargetPriority.Nearest) =>
            new BattleGroup { X = x, Y = y, Infantry = inf, Archers = arc, Order = order, Priority = prio };

        [Test]
        public void SoldiersOrderedToFallBackLeaveTheCampAlive()
        {
            var b = new CampBattle(Camp(2), new[] { Landing(0, 16, 5, 2, BattleOrder.FallBack) });
            for (int i = 0; i < 30 && !b.Over; i++) b.Step();
            Assert.IsTrue(b.Over && b.Retreated && !b.Victory, b.Summary());
            Assert.AreEqual(7, b.LeftField, "all of them walked off the map");
            Assert.AreEqual(5, b.SurvivingInfantry); Assert.AreEqual(2, b.SurvivingArchers);
            Assert.AreEqual(0, b.AliveAttackers, "none still fighting");
        }

        [Test]
        public void SoldiersOrderedToHoldStayWhereTheyAre()
        {
            var b = new CampBattle(Camp(2), new[] { Landing(0, 16, 6, 0, BattleOrder.Hold) });
            var start = b.Units.Where(u => u.Attacker).Select(u => (u.X, u.Y)).ToList();
            for (int i = 0; i < 10; i++) b.Step();
            Assert.AreEqual(start, b.Units.Where(u => u.Attacker).Select(u => (u.X, u.Y)).ToList(), "holding soldiers do not move");
        }

        [Test]
        public void SoldiersSentForTheWarlordMakeForHim()
        {
            var b = new CampBattle(Camp(3), new[] { Landing(0, 16, 12, 0, BattleOrder.Warlord), Landing(31, 16, 12, 0, BattleOrder.Charge) });
            b.Step();
            var chief = b.Units.Single(u => u.Kind == BKind.Chief);
            foreach (var u in b.Units.Where(u => u.Group == 0)) Assert.AreSame(chief, u.Target, "the warlord group heads for the warlord");
            Assert.IsTrue(b.Units.Where(u => u.Group == 1).Any(u => u.Target != chief), "the charging group goes for whoever is nearest");
        }

        [Test]
        public void PlunderersCarryGoodsOffTheMapAndNothingIsLostOrMadeUp()
        {
            var camp = Camp(2);
            int before = camp.Site.Map.Piles.Sum(p => p.Amount);
            var b = new CampBattle(camp, new[] { Landing(0, 16, 20, 0, BattleOrder.Plunder) });
            Run(b);
            Assert.IsTrue(b.Over, b.Summary());
            Assert.Greater(b.LootTotal, 0, "they got away with goods: " + b.Summary());
            Assert.AreEqual(before, camp.Site.Map.Piles.Sum(p => p.Amount) + b.LootTotal + b.Carried, "every good taken is either still in a pile, carried, or taken home");
            Assert.LessOrEqual(b.LootTotal, b.SurvivingInfantry * CampBattle.InfantryCarry + b.LostInfantry * CampBattle.InfantryCarry);
            if (!b.Victory) Assert.Greater(b.LeftField, 0, "a plunder raid ends with soldiers walking off with the goods");
            StringAssert.Contains("They carry home", b.Summary(), "a pulled-out army reports its plunder too");
        }

        [Test]
        public void OrdersCanChangeMidFight()
        {
            var b = new CampBattle(Camp(2), new[] { Landing(0, 16, 8, 0, BattleOrder.Hold) });
            var start = b.Units.Where(u => u.Attacker).Select(u => (u.X, u.Y)).ToList();
            for (int i = 0; i < 3; i++) b.Step();
            Assert.AreEqual(start, b.Units.Where(u => u.Attacker).Select(u => (u.X, u.Y)).ToList());
            b.Groups[0].Order = BattleOrder.Charge;
            b.Step();
            Assert.AreNotEqual(start, b.Units.Where(u => u.Attacker).Select(u => (u.X, u.Y)).ToList(), "charging, they move in");
            b.Groups[0].Order = BattleOrder.FallBack;
            for (int i = 0; i < 40 && !b.Over; i++) b.Step();
            Assert.IsTrue(b.Over && b.Retreated, b.Summary());
            Assert.AreEqual(b.SurvivingInfantry, b.LeftField, "every survivor walked off");
        }

        [Test]
        public void TargetPriorityPicksArchersLeadersOrTheWounded()
        {
            var me = new BUnit { X = 0, Y = 0 };
            var near = new BUnit { Id = 1, X = 2, Y = 0, Kind = BKind.Raider, Hp = 3 };
            var archer = new BUnit { Id = 2, X = 6, Y = 0, Kind = BKind.RaiderArcher, Hp = 2.5 };
            var brute = new BUnit { Id = 3, X = 7, Y = 0, Kind = BKind.Brute, Hp = 5 };
            var hurt = new BUnit { Id = 4, X = 3, Y = 0, Kind = BKind.Raider, Hp = 0.5 };
            var all = new[] { near, archer, brute, hurt };
            BUnit Best(TargetPriority p) => all.OrderBy(e => CampBattle.TargetScore(me, e, p)).ThenBy(e => e.Id).First();
            Assert.AreSame(near, Best(TargetPriority.Nearest));
            Assert.AreSame(archer, Best(TargetPriority.Archers));
            Assert.AreSame(brute, Best(TargetPriority.Leaders));
            Assert.AreSame(hurt, Best(TargetPriority.Weakest));
        }
    }
}
