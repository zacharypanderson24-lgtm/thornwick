using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ConquestTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static World Town(int infantry = 80)
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            w.Soldiers = infantry; w.Archers = 0;
            w.ResAmt[Res.Gold] = 500;
            return w;
        }

        /// <summary>A small settlement that answers to somebody, nearest first (its liege is there to take offence).</summary>
        static Settlement Target(World w, SettlementTier tier) =>
            w.Settlements.Where(s => s.Tier == tier && s.Liege >= 0).OrderBy(w.SettlementKm).First();

        static BattleGroup Landing(CampMap m, int inf)
        {
            for (int i = 0; i < CampMap.Size; i++)
                foreach (var (x, y) in new[] { (i, 0), (0, i), (i, CampMap.Size - 1), (CampMap.Size - 1, i) })
                    if (m.Passable(x, y)) return new BattleGroup { X = x, Y = y, Infantry = inf };
            Assert.Fail("no open edge"); return null;
        }

        static CampBattle Fight(World w, Settlement s, int inf, bool toTheEnd = true)
        {
            var camp = w.TownCamp(s);
            var b = w.BeginInvasion(camp, new[] { Landing(camp.Site.Map, inf) });
            Assert.IsNotNull(b, "the invasion starts");
            if (toTheEnd) for (int i = 0; i < CampBattle.MaxTicks + 5 && !b.Over; i++) b.Step();
            return b;
        }

        [Test]
        public void ASettlementIsDefendedByAGarrisonAsBigAsItsPeopleMakeIt()
        {
            var w = Town();
            var hamlet = w.Settlements.First(s => s.Tier == SettlementTier.Hamlet);
            var capital = w.Settlements.First(s => s.Tier == SettlementTier.Capital);
            Assert.Less(World.Garrison(hamlet), World.Garrison(capital));
            Assert.Greater(World.Garrison(capital), CampBattle.RaiderCount(5), "a capital fields more than any raider camp");
            var camp = w.TownCamp(hamlet);
            Assert.IsTrue(camp.IsTown);
            Assert.AreSame(camp, w.TownCamp(hamlet), "made once");
            var preview = new CampBattle(camp, new List<BattleGroup>(), w.DefendersHome(camp));
            Assert.AreEqual(World.Garrison(hamlet) + CampSize.Of(camp.Strength).Brutes + 1, preview.AliveRaiders, "garrison, the lord's guard and the lord");
            Assert.AreEqual(1, preview.Units.Count(u => u.Kind == BKind.Chief), "one lord");
            var site = camp.Site.World;
            Assert.AreEqual(camp.Site.Map.Tents.Count + 1, site.Blds.Count(b => b.T == "house"), "homes for tents, and the lord's house");
            Assert.IsFalse(site.Blds.Any(b => b.T == "tent" || b.T == "chieftent"));
        }

        [Test]
        public void OurSoldierSheetsCanBeRecolouredForASettlementsTroops()
        {
            // the tunic colours BattleArt.ClothColours dyes (keep in step with it): if the art is redrawn in other colours, update both
            var cloth = new Dictionary<string, string[]>
            {
                { "infantry", new[] { "635B4A", "8A806B", "AFA28B" } },
                { "archer", new[] { "4D5748", "687461" } },
            };
            foreach (var kv in cloth)
            {
                var tex = UnityEngine.Resources.Load<UnityEngine.Texture2D>("Art/Battle/" + kv.Key);
                Assert.IsNotNull(tex, kv.Key);
                Assert.IsTrue(tex.isReadable, $"{kv.Key} must be readable so a settlement's troops can wear their realm's colours");
                var tones = new HashSet<int>(kv.Value.Select(h => System.Convert.ToInt32(h, 16)));
                int tunic = tex.GetPixels32().Count(p => p.a >= 200 && tones.Contains(p.r << 16 | p.g << 8 | p.b));
                Assert.Greater(tunic, 500, $"{kv.Key}'s tunic colours are on the sheet, so its tunic can be dyed");
            }
        }

        [Test]
        public void PullingOutAngersTheSettlementAndItsRealm()
        {
            var w = Town();
            var s = Target(w, SettlementTier.Village);
            var liege = w.SettlementById(s.Liege);
            int att = s.Attitude, latt = liege.Attitude;
            var b = Fight(w, s, 3, toTheEnd: false);
            for (int i = 0; i < 3; i++) b.Step();
            b.Retreat();
            w.FinishInvasion(b);
            Assert.IsNull(w.PendingConquest);
            Assert.Less(s.Attitude, att, "it is angrier");
            Assert.Less(liege.Attitude, latt, "and so is the settlement it answers to");
            Assert.AreEqual(Allegiance.Own, s.Allegiance);
            Assert.AreEqual(1, w.Expeditions.Count, "the survivors march home");
        }

        [Test]
        public void ATakenSettlementCanBeSackedForFarMoreLoot()
        {
            var w = Town();
            var s = Target(w, SettlementTier.Village);
            double pop = s.Population;
            var b = Fight(w, s, 70);
            Assert.IsTrue(b.Victory, b.Summary());
            w.FinishInvasion(b);
            Assert.AreSame(b, w.PendingConquest, "you choose what happens to it");
            Assert.IsFalse(w.CanAttack(w.Settlements[0]).Ok && w.PendingConquest != null, "one decision at a time");
            int gold = b.Loot.GetValueOrDefault(Res.Gold);
            var near = w.Settlements.Where(o => o != s && System.Math.Max(System.Math.Abs(o.X - s.X), System.Math.Abs(o.Y - s.Y)) <= World.SackHeardKm).ToList();
            var nearBefore = near.Select(o => o.Attitude).ToList();
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Sack).Ok);
            Assert.IsNull(w.PendingConquest);
            Assert.Greater(b.Loot.GetValueOrDefault(Res.Gold), gold, "sacking takes much more");
            Assert.Less(s.Population, pop * 0.5, "and burns it");
            Assert.AreEqual(-100, s.Attitude);
            Assert.IsTrue(near.Select((o, i) => o.Attitude < nearBefore[i] || o.Attitude == -100).All(x => x), "every settlement nearby hears of it");
            var e = w.Expeditions.Single();
            Assert.AreEqual(b.Loot[Res.Gold], e.Loot[Res.Gold], "the army carries the spoils home");
        }

        [Test]
        public void ATributaryBreaksFromItsRealmAndPaysEveryMonth()
        {
            var w = Town();
            var s = Target(w, SettlementTier.Hamlet);
            int liege = s.Liege;
            var b = Fight(w, s, 40);
            Assume.That(b.Victory, b.Summary());
            w.FinishInvasion(b);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Tribute).Ok);
            Assert.AreEqual(Allegiance.Tributary, s.Allegiance);
            Assert.AreEqual(-1, s.Liege, "it no longer answers to its old liege");
            Assert.IsFalse(w.SubjectsOf(w.SettlementById(liege)).Contains(s));
            Assert.IsFalse(w.CanAttack(s).Ok, "you do not attack your own tributary");
            while (w.Day % World.SettlementMonth != 0) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            // gold is capped by storage and soldiers draw wages: start the month with none, and no soldiers to pay
            w.Expeditions.Clear(); w.Soldiers = 0; w.Archers = 0;
            w.Events.Clear(); w.ResAmt[Res.Food] = 150; w.ResAmt[Res.Gold] = 0;
            w.Tick();
            Assert.IsTrue(w.Events.Any(e => e.Contains(s.Name) && e.Contains("tribute")), string.Join(" | ", w.Events));
            Assert.Greater(w.ResAmt[Res.Gold], 0, "tribute gold comes in at the turn of the month");
        }

        [Test]
        public void AnOccupiedSettlementNeedsAGarrisonOrItRisesUp()
        {
            var w = Town();
            var s = Target(w, SettlementTier.Hamlet);
            var b = Fight(w, s, 40);
            Assume.That(b.Victory, b.Summary());
            w.FinishInvasion(b);
            int need = World.GarrisonNeeded(s), survivors = b.SurvivingInfantry + b.SurvivingArchers;
            Assert.IsFalse(w.ResolveConquest(ConquestChoice.Occupy, need - 1).Ok, "too small a garrison");
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, need).Ok);
            Assert.AreEqual(Allegiance.Occupied, s.Allegiance);
            Assert.AreEqual(need, s.Garrison);
            Assert.AreEqual(survivors - need, w.Expeditions.Single().Infantry + w.Expeditions.Single().Archers, "the garrison stays behind");
            Assert.IsFalse(w.CanAttack(s).Ok, "it is yours");

            // saved and loaded as it is
            var loaded = World.LoadJson(w.SaveJson());
            loaded.SetSettlements(Settlements.Generate(Map));
            var ls = loaded.SettlementById(s.Id);
            Assert.AreEqual((Allegiance.Occupied, need, -1), (ls.Allegiance, ls.Garrison, ls.Liege));

            // reinforce from home, then strip it bare: sooner or later it rises up
            w.Soldiers = 10;
            Assert.IsTrue(w.ReinforceGarrison(s, 5).Ok);
            Assert.AreEqual(need + 5, s.Garrison);
            Assert.AreEqual(5, w.Soldiers);
            s.Garrison = 0;
            for (int i = 0; i < 24 * World.SettlementMonth && s.Allegiance == Allegiance.Occupied; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            Assert.AreEqual(Allegiance.Own, s.Allegiance, "an unguarded settlement throws you out");
            Assert.LessOrEqual(s.Attitude, -60);
        }

        [Test]
        public void WithdrawingTheGarrisonLeavesATributary()
        {
            var w = Town();
            var s = Target(w, SettlementTier.Hamlet);
            var b = Fight(w, s, 40);
            Assume.That(b.Victory, b.Summary());
            w.FinishInvasion(b);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            Assert.IsTrue(w.WithdrawGarrison(s).Ok);
            Assert.AreEqual(Allegiance.Tributary, s.Allegiance);
            Assert.AreEqual(0, s.Garrison);
            Assert.AreEqual(2, w.Expeditions.Count, "the garrison marches home too");
        }
    }
}
