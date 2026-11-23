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
            Assert.AreSame(b, w.PendingBattle, "you choose what happens to it");
            var p = w.PendingConquest;
            Assert.AreEqual((s.Id, b.SurvivingInfantry, b.SurvivingArchers), (p.SettlementId, p.Infantry, p.Archers));
            Assert.IsFalse(w.CanAttack(w.Settlements[0]).Ok && w.PendingConquest != null, "one decision at a time");
            int gold = p.Loot.GetValueOrDefault(Res.Gold), carried = p.Loot.Values.Sum();
            int capacity = (p.Infantry * CampBattle.InfantryCarry + p.Archers * CampBattle.ArcherCarry) * (1 + World.SackCartFactor);
            var near = w.Settlements.Where(o => o != s && System.Math.Max(System.Math.Abs(o.X - s.X), System.Math.Abs(o.Y - s.Y)) <= World.SackHeardKm).ToList();
            var nearBefore = near.Select(o => o.Attitude).ToList();
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Sack).Ok);
            Assert.IsNull(w.PendingConquest);
            Assert.IsFalse(w.ResolveConquest(ConquestChoice.Sack).Ok, "deciding twice does nothing");
            var e = w.Expeditions.Single();
            Assert.Greater(e.Loot.GetValueOrDefault(Res.Gold), gold, "sacking takes much more");
            Assert.LessOrEqual(e.Loot.Values.Sum(), System.Math.Max(capacity, carried), "but no more than the army and the town's carts can haul");
            StringAssert.Contains("carts carry off", w.LastConquestNews);
            Assert.Less(s.Population, pop * 0.5, "and burns it");
            Assert.AreEqual(-100, s.Attitude);
            Assert.IsTrue(near.Select((o, i) => o.Attitude < nearBefore[i] || o.Attitude == -100).All(x => x), "every settlement nearby hears of it");
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

            // reinforce from home: they march there and join on arrival
            w.Soldiers = 10; w.Archers = 0;
            Assert.IsTrue(w.ReinforceGarrison(s, 5).Ok);
            Assert.AreEqual(need, s.Garrison, "not there yet");
            Assert.AreEqual(5, w.MarchingTo(s));
            Assert.AreEqual(5, w.Soldiers);
            for (int i = 0; i < w.SettlementMarchDays(s) + 1 && w.Detachments.Count > 0; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            Assert.AreEqual(need + 5, s.Garrison, "they joined on arrival");

            // strip it bare: sooner or later it rises up, and the garrison is driven out rather than vanishing
            s.GarrisonInfantry = 0; s.GarrisonArchers = 0;
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
            s.GarrisonArchers = 2;   // a mixed garrison
            int inf = s.GarrisonInfantry, arc = s.GarrisonArchers;
            Assert.IsTrue(w.WithdrawGarrison(s).Ok);
            Assert.AreEqual(Allegiance.Tributary, s.Allegiance);
            Assert.AreEqual(0, s.Garrison);
            Assert.AreEqual(2, w.Expeditions.Count, "the garrison marches home too");
            var home = w.Expeditions.Last();
            Assert.AreEqual((inf, arc), (home.Infantry, home.Archers), "infantry come home as infantry, archers as archers");
        }

        // ---------- stage 1 regressions: every soldier accounted for, loot always delivered, decisions survive saves ----------

        /// <summary>Every soldier you have, wherever he is.</summary>
        static int AllSoldiers(World w) => w.Soldiers + w.TroopsAway;

        /// <summary>Barracks for `bunks` soldiers (a soldier with no bunk at home is let go each day, which these tests must not mistake
        /// for a soldier lost on the road), and peasants enough that a housing shortfall comes out of them rather than the soldiers.</summary>
        static void Billet(World w, int bunks)
        {
            int c = Defs.Start;
            w.ClaimAll();
            // a long street south from the hall's road, with barracks along it
            foreach (var (x, y) in World.LPath(c, c + 7, c, c + 28).Concat(World.LPath(c - 14, c + 28, c + 14, c + 28)))
            {
                if (w.Grid[y, x] != null) continue;
                w.DecoMap[y, x] = Deco.None;
                w.AddBld("road", x, y);
            }
            w.Touch();
            for (int i = 0; i < 61 * 61 && w.Recalc().Bunks < bunks; i++)
            {
                int x = c - 30 + i % 61, y = c - 30 + i / 61;
                foreach (var r in System.Enum.GetValues(typeof(Res)).Cast<Res>()) w.ResAmt[r] = 300;
                if (!w.CanPlace("barracks", x, y).Ok || !w.Place("barracks", x, y).Ok) continue;
                w.Recalc();
                if (!w.Grid[y, x].Served) w.Remove(x, y);   // only a barracks with a road to the hall has bunks
            }
            Assume.That(w.Recalc().Bunks, Is.GreaterThanOrEqualTo(bunks), $"room for enough road-connected barracks near the hall (got {w.Recalc().Bunks})");
            w.Peasants = 120; w.Drafting = false;
        }

        static (World w, Settlement s, CampBattle b) Taken(int army = 40, SettlementTier tier = SettlementTier.Hamlet, int archers = 0)
        {
            var w = Town(army);
            Billet(w, army + 20);
            w.Archers = archers;
            var s = Target(w, tier);
            var camp = w.TownCamp(s);
            var land = Landing(camp.Site.Map, army - archers); land.Archers = archers;
            var b = w.BeginInvasion(camp, new[] { land });
            for (int i = 0; i < CampBattle.MaxTicks + 5 && !b.Over; i++) b.Step();
            Assume.That(b.Victory, b.Summary());
            w.FinishInvasion(b);
            return (w, s, b);
        }

        [Test]
        public void SoldiersAreConservedThroughOccupationReinforcementAndWithdrawal()
        {
            var (w, s, b) = Taken(40, archers: 10);
            int survivors = b.SurvivingInfantry + b.SurvivingArchers, home = w.Soldiers;
            Assert.AreEqual(home + survivors, AllSoldiers(w), "the army waiting for your decision still counts");
            int need = World.GarrisonNeeded(s);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, need + 2).Ok);
            Assert.AreEqual(home + survivors, AllSoldiers(w), "garrison plus marchers home");
            w.Soldiers += 6; w.Archers += 3;   // some recruits at home
            int total = AllSoldiers(w);
            Assert.IsTrue(w.ReinforceGarrison(s, 6).Ok);
            Assert.AreEqual(total, AllSoldiers(w), "reinforcements on the road still count");
            var d = w.Detachments.Single();
            Assert.AreEqual(6, d.Infantry + d.Archers);
            for (int i = 0; i < 60 && (w.Detachments.Count > 0 || w.Expeditions.Count > 0); i++) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            Assert.AreEqual(total, AllSoldiers(w), "nobody lost or duplicated on the road");
            int gInf = s.GarrisonInfantry, gArc = s.GarrisonArchers, hInf = w.Infantry, hArc = w.Archers;
            Assert.IsTrue(w.WithdrawGarrison(s).Ok);
            for (int i = 0; i < 60 && w.Expeditions.Count > 0; i++) { w.ResAmt[Res.Food] = 150; w.Tick(); }
            Assert.AreEqual((hInf + gInf, hArc + gArc), (w.Infantry, w.Archers), "the garrison comes home as the same infantry and archers");
        }

        [Test]
        public void SoldiersAwayStillDrawWagesAndKeepTheirBunks()
        {
            var w = Town(10);
            w.ResAmt[Res.Gold] = 100; w.Peasants = 0; w.Drafting = false;
            double before = w.ResAmt[Res.Gold];
            w.ResAmt[Res.Food] = 150; w.Tick();
            double payHome = before - w.ResAmt[Res.Gold];
            var (t, s, b) = Taken(40);
            Assert.IsTrue(t.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            Assert.Greater(t.TroopsStationed, 0);
            t.Peasants = 0; t.ResAmt[Res.Gold] = 100; t.Drafting = false;
            int away = t.TroopsAway, athome = t.Soldiers;
            t.ResAmt[Res.Food] = 150; t.Tick();
            Assert.AreEqual(100 - (athome + away) * 0.1, t.ResAmt[Res.Gold], 0.2, "soldiers away are paid like those at home");
            Assert.Greater(payHome, 0);
        }

        [Test]
        public void LootGoesHomeEvenWhenEverySurvivorStaysAsTheGarrison()
        {
            var (w, s, b) = Taken(40);
            var p = w.PendingConquest;
            p.Loot[Res.Gold] = 25;   // make sure there is something to carry
            int all = p.Infantry + p.Archers;
            Assume.That(all >= World.GarrisonNeeded(s));
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, all).Ok);
            Assert.AreEqual(all, s.Garrison);
            var cart = w.Expeditions.Single();
            Assert.AreEqual(0, cart.Infantry + cart.Archers, "no soldier goes home");
            Assert.AreEqual(25, cart.Loot[Res.Gold], "but the spoils do, by cart");
            double gold = 0;
            for (int i = 0; i < 60 && w.Expeditions.Count > 0; i++) { w.ResAmt[Res.Food] = 150; w.ResAmt[Res.Gold] = 0; w.Tick(); gold = System.Math.Max(gold, w.ResAmt[Res.Gold]); }
            Assert.Greater(gold, 0, "and arrive in the treasury");
        }

        [Test]
        public void AFallenSettlementStillAwaitsItsFateAfterSavingAndLoading()
        {
            var (w, s, b) = Taken(40);
            var p = w.PendingConquest;
            p.Loot[Res.Gold] = 17;
            var loaded = World.LoadJson(w.SaveJson());
            Assert.IsNotNull(loaded.PendingConquest, "the decision is saved");
            Assert.IsNull(loaded.PendingBattle, "the battle itself is not");
            var lp = loaded.PendingConquest;
            Assert.AreEqual((p.SettlementId, p.Infantry, p.Archers, 17), (lp.SettlementId, lp.Infantry, lp.Archers, lp.Loot[Res.Gold]));
            Assert.AreEqual(w.TroopsReturning, loaded.TroopsReturning, "the waiting army still counts after loading");
            Assert.IsFalse(loaded.ResolveConquest(ConquestChoice.Leave).Ok, "before the settlements arrive it cannot be decided yet");
            Assert.IsNotNull(loaded.PendingConquest, "and is not lost by trying");
            loaded.SetSettlements(Settlements.Generate(Map));
            Assert.IsFalse(loaded.CanAttack(loaded.Settlements.First(o => o.Id != s.Id)).Ok, "still one decision at a time");
            Assert.IsTrue(loaded.ResolveConquest(ConquestChoice.Tribute).Ok, "it can be decided after loading");
            Assert.AreEqual(Allegiance.Tributary, loaded.SettlementById(s.Id).Allegiance);
            Assert.AreEqual(17, loaded.Expeditions.Single().Loot[Res.Gold], "and the army goes home with its loot");
        }

        [Test]
        public void AnArmySavedMidFightMarchesHomeAfterLoading()
        {
            var w = Town(30);
            w.Archers = 5;
            var s = Target(w, SettlementTier.Village);
            var camp = w.TownCamp(s);
            var land = Landing(camp.Site.Map, 20); land.Archers = 5;
            var b = w.BeginInvasion(camp, new[] { land });
            Assert.IsNotNull(b);
            Assert.AreEqual(25, w.TroopsReturning, "the army in the fight counts");
            var loaded = World.LoadJson(w.SaveJson());
            var e = loaded.Expeditions.Single();
            Assert.AreEqual((20, 5), (e.Infantry, e.Archers), "saved during the fight, it breaks off and marches home whole");
        }

        [Test]
        public void ReinforcementsTravelAndAreSaved()
        {
            var (w, s, b) = Taken(40);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            w.Soldiers = 8; w.Archers = 3;
            Assert.IsTrue(w.ReinforceGarrison(s, 8).Ok);
            var d = w.Detachments.Single();
            Assert.AreEqual((5, 3), (d.Infantry, d.Archers), "infantry first, then archers");
            var loaded = World.LoadJson(w.SaveJson());
            var ld = loaded.Detachments.Single();
            Assert.AreEqual((d.SettlementId, d.Infantry, d.Archers, d.ArriveDay), (ld.SettlementId, ld.Infantry, ld.Archers, ld.ArriveDay));
            loaded.SetSettlements(Settlements.Generate(Map));
            var ls = loaded.SettlementById(s.Id);
            Assert.AreEqual((s.GarrisonInfantry, s.GarrisonArchers), (ls.GarrisonInfantry, ls.GarrisonArchers), "garrison kinds are saved");
            // the settlement is lost before they arrive: they turn round
            ls.Allegiance = Allegiance.Own; ls.GarrisonInfantry = 0; ls.GarrisonArchers = 0;
            for (int i = 0; i < 60 && loaded.Detachments.Count > 0; i++) { loaded.ResAmt[Res.Food] = 150; loaded.Tick(); }
            var back = loaded.Expeditions.Last();
            Assert.AreEqual((5, 3), (back.Infantry, back.Archers), "they march home instead");
        }

        [Test]
        public void OldGarrisonRowsLoadAsInfantry()
        {
            var w = Town();
            var s = w.Settlements[3];
            s.Allegiance = Allegiance.Occupied; s.GarrisonInfantry = 4; s.GarrisonArchers = 0;
            var save = Newtonsoft.Json.Linq.JObject.Parse(w.SaveJson());
            foreach (Newtonsoft.Json.Linq.JArray row in (Newtonsoft.Json.Linq.JArray)save["settlements"]) row.RemoveAt(8);   // the older 8-number rows
            var loaded = World.LoadJson(save.ToString());
            loaded.SetSettlements(Settlements.Generate(Map));
            Assert.AreEqual((4, 0), (loaded.Settlements[3].GarrisonInfantry, loaded.Settlements[3].GarrisonArchers));
        }

        [Test]
        public void TheBarracksDoNotTrainSoldiersIntoTheBunksOfAnArmyAway()
        {
            var (w, s, b) = Taken(40);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Leave).Ok);
            int coming = w.TroopsReturning;
            Assert.Greater(coming, 0);
            int bunks = w.Recalc().Bunks;
            // the barracks are ready to train and the town can pay; the only free bunks belong to the army on its way home
            w.Soldiers = System.Math.Max(0, bunks - coming); w.Archers = 0; w.Drafting = true; w.Peasants = 100;
            foreach (var bk in w.Blds.Where(x => x.T == "barracks")) bk.Prog = World.TrainDays;
            int before = w.Soldiers;
            foreach (var r in System.Enum.GetValues(typeof(Res)).Cast<Res>()) w.ResAmt[r] = 300;
            w.Tick();
            Assert.AreEqual(before, w.Soldiers, "no recruit takes a bunk kept for a soldier coming home");
        }
    }
}
