using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    /// <summary>Occupation unrest, a conquered settlement's subjects and the armies realms send (World.Realms.cs).</summary>
    public class RealmTests
    {
        static WorldMap _map;
        static WorldMap Map => _map ??= WorldMap.Generate(7);

        static World Home(int infantry = 90)
        {
            var w = new World(7);
            var home = Map.HomeSite;
            w.SetCamps(new List<RaiderCamp>(), home.X, home.Y);
            w.SetSettlements(Settlements.Generate(Map));
            w.Soldiers = infantry; w.Archers = 0;
            w.ResAmt[Res.Gold] = 500;
            return w;
        }

        static void Days(World w, int n) { for (int i = 0; i < n; i++) { w.ResAmt[Res.Food] = 300; w.ResAmt[Res.Gold] = 200; w.Tick(); } }

        static BattleGroup Landing(CampMap m, int inf)
        {
            for (int i = 0; i < m.N; i++)
                foreach (var (x, y) in new[] { (i, 0), (0, i), (i, m.N - 1), (m.N - 1, i) })
                    if (m.Passable(x, y)) return new BattleGroup { X = x, Y = y, Infantry = inf };
            Assert.Fail("no open edge"); return null;
        }

        /// <summary>Beat a settlement and leave the army waiting for your decision.</summary>
        static void Take(World w, Settlement s, int inf = 90)
        {
            var camp = w.TownCamp(s);
            w.Soldiers = inf; w.Archers = 0;
            var b = w.BeginInvasion(camp, new[] { Landing(camp.Site.Map, inf) });
            Assert.IsNotNull(b);
            for (int i = 0; i < CampBattle.MaxTicks + 5 && !b.Over; i++) b.Step();
            Assume.That(b.Victory, b.Summary());
            w.FinishInvasion(b);
        }

        /// <summary>A hamlet held by you, with the garrison it needs.</summary>
        static (World w, Settlement s) Held()
        {
            var w = Home();
            var s = w.Settlements.Where(o => o.Tier == SettlementTier.Hamlet).OrderBy(w.SettlementKm).First();
            Take(w, s);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            w.RealmArmies.Clear();   // no realm interferes unless a test wants it to
            return (w, s);
        }

        // ---------- unrest ----------

        [Test]
        public void AnAngryProudPlaceGrowsRestlessEvenWithEnoughSoldiers()
        {
            var (w, s) = Held();
            s.Personality = Personality.Proud; s.Attitude = -100;
            s.GarrisonInfantry = World.GarrisonNeeded(s); s.GarrisonArchers = 0;
            var why = w.UnrestReasons(s);
            Assert.Greater(why.Sum(r => r.change), 0, "a big enough garrison is no guarantee");
            Assert.IsTrue(why.Any(r => r.why.Contains("resents")) && why.Any(r => r.why.Contains("recently")) && why.Any(r => r.why.Contains("keeps order")));
            double before = s.Unrest;
            w.Events.Clear();
            var events = new List<string>();
            for (int i = 0; i < 3 * World.SettlementMonth && s.Allegiance == Allegiance.Occupied; i++) { Days(w, 1); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(s.Unrest > before || s.Allegiance != Allegiance.Occupied || events.Any(e => e.Contains("rose against you")), "unrest climbs");
            Assert.IsTrue(events.Any(e => e.Contains("restless") || e.Contains("brink") || e.Contains("rose against you")), "and you are warned: " + string.Join(" | ", events));
        }

        [Test]
        public void AFriendlyWellGuardedPlaceCalmsDown()
        {
            var (w, s) = Held();
            s.Personality = Personality.Cautious; s.Attitude = 30; s.ConqueredDay = w.Day - World.RecentConquestDays - 1;
            s.GarrisonInfantry = World.GarrisonNeeded(s) * 2;
            s.Unrest = 40;
            Assert.Less(w.UnrestReasons(s).Sum(r => r.change), 0);
            Days(w, 2 * World.SettlementMonth + 1);
            Assert.Less(s.Unrest, 40);
            Assert.AreEqual(Allegiance.Occupied, s.Allegiance);
        }

        [Test]
        public void YouAreWarnedWhenGrowthRaisesTheGarrisonNeeded()
        {
            var (w, s) = Held();
            s.Attitude = 50; s.Unrest = 0;
            int need = World.GarrisonNeeded(s);
            s.GarrisonInfantry = need; s.GarrisonArchers = 0;
            s.Population += 80;   // it grew
            Assume.That(World.GarrisonNeeded(s) > need);
            w.Events.Clear();
            var events = new List<string>();
            for (int i = 0; i < World.SettlementMonth + 1; i++) { Days(w, 1); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(events.Any(e => e.Contains($"now needs {World.GarrisonNeeded(s)} soldiers")), string.Join(" | ", events));
        }

        [Test]
        public void ARisingIsFoughtOutAndEverySoldierIsAccountedFor()
        {
            foreach (int garrison in new[] { 1, 60 })
            {
                var (w, s) = Held();
                s.GarrisonInfantry = garrison; s.GarrisonArchers = 0; s.Unrest = 100; s.Attitude = -100;
                w.Expeditions.Clear(); w.Events.Clear();
                var events = new List<string>();
                while (w.Day % World.SettlementMonth != 0) { Days(w, 1); }
                Days(w, 1);
                events.AddRange(w.Events);
                string news = events.FirstOrDefault(e => e.Contains("rose against you"));
                Assert.IsNotNull(news, string.Join(" | ", events));
                int fell = System.Text.RegularExpressions.Regex.Matches(news, @"(\d+) infantry and (\d+) archers (of yours )?fell").Cast<System.Text.RegularExpressions.Match>()
                               .Sum(m => int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value));
                int home = w.Expeditions.Sum(e => e.Infantry + e.Archers);
                Assert.AreEqual(garrison, s.Garrison + home + fell, $"nobody vanishes: {news}");
                if (garrison == 60) { Assert.AreEqual(Allegiance.Occupied, s.Allegiance, "a strong garrison puts it down"); Assert.Less(s.Unrest, 100); }
                else Assert.AreEqual(Allegiance.Own, s.Allegiance, "a token garrison is driven out");
            }
        }

        [Test]
        public void SkirmishLossesNeverExceedTheMenThere()
        {
            for (int seed = 0; seed < 40; seed++)
                foreach (var (inf, arc, them) in new[] { (0, 0, 5), (3, 2, 40), (40, 10, 5), (10, 0, 10) })
                {
                    var (won, li, la, tl) = World.Skirmish(inf, arc, them, 0.7, seed);
                    Assert.That(li, Is.InRange(0, inf)); Assert.That(la, Is.InRange(0, arc)); Assert.That(tl, Is.InRange(0, them));
                    if (inf + arc == 0) Assert.IsFalse(won);
                }
        }

        // ---------- subjects ----------

        [Test]
        public void TheSubjectsOfAPlaceYouOccupyBreakAwayAndAreNotYours()
        {
            var w = Home(120);
            var s = w.Settlements.Where(o => o.Tier == SettlementTier.Village && w.SubjectsOf(o).Any()).OrderBy(w.SettlementKm).First();
            var subs = w.SubjectsOf(s).ToList();
            Take(w, s, 120);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            foreach (var o in subs)
            {
                Assert.AreEqual(-1, o.Liege, $"{o.Name} answers to nobody now");
                Assert.AreEqual(Allegiance.Own, o.Allegiance, "it is not yours");
                Assert.AreNotEqual(s.Id, o.Realm);
            }
            Assert.IsEmpty(w.SubjectsOf(s));
            Assert.IsTrue(w.Events.Any(e => e.Contains("now answer") || e.Contains("break away") || e.Contains("breaks away")));
        }

        [Test]
        public void BrokenRealmLinksAreSafe()
        {
            var w = Home();
            var a = w.Settlements[0]; var b = w.Settlements[1];
            a.Liege = b.Id; b.Liege = a.Id;   // a loop
            Assert.LessOrEqual(w.LiegesOf(a).Count(), 1);
            Assert.That(w.AllSubjects(a), Is.LessThan(w.Settlements.Count));
            w.HurtRealm(a, 5);   // must not loop forever
            var save = World.LoadJson(w.SaveJson());
            save.SetSettlements(Settlements.Generate(Map));   // the loop was saved; loading it is safe too
            a.Liege = 99999;
            Assert.AreEqual(0, w.LiegesOf(a).Count(), "a missing liege is no liege");
        }

        // ---------- realm armies ----------

        static (World w, Settlement s, Settlement liege) Wronged(Personality ways)
        {
            var w = Home(120);
            var s = w.Settlements.Where(o => o.Tier == SettlementTier.Hamlet && w.SettlementById(o.Liege) is Settlement l && w.SettlementKm(l) <= World.RealmArmyKm
                                             && World.DefendersOf(l) >= 12).OrderBy(w.SettlementKm).First();
            var liege = w.SettlementById(s.Liege);
            s.Personality = Personality.Mercantile;   // the place itself will not march
            liege.Personality = ways; liege.NextMarchDay = 0; liege.Attitude = -80;
            foreach (var o in w.LiegesOf(liege)) o.Personality = Personality.Mercantile;
            return (w, s, liege);
        }

        [Test]
        public void AVengefulLiegeMobilizesMarchesAndRaidsYou()
        {
            var (w, s, liege) = Wronged(Personality.Vengeful);
            Take(w, s, 120);
            w.Events.Clear();
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Leave).Ok);
            var army = w.RealmArmies.Single();
            Assert.AreEqual((liege.Id, RealmArmyGoal.Retaliate), (army.FromId, army.Goal));
            Assert.That(army.Size, Is.InRange(4, 40));
            Assert.Greater(army.MarchDay, w.Day, "it musters first: no instant armies");
            Assert.IsTrue(w.Events.Any(e => e.Contains("mobilizing")));
            double lostBefore = liege.DefendersLost;
            for (int i = 0; i < 60 && w.Warband == null; i++) Days(w, 1);
            Assert.IsNotNull(w.Warband, "it sets out");
            Assert.AreEqual(World.TownIdBase + liege.Id, w.Warband.CampId);
            Assert.AreEqual(lostBefore + army.Size, liege.DefendersLost, 1e-9, "those soldiers are away from home");
            for (int i = 0; i < 80 && (w.Raid == null || w.Raid.Camp?.Town != liege); i++) Days(w, 1);
            Assert.IsNotNull(w.Raid, "it comes as a raid on your town");
            Assert.AreSame(liege, w.Raid.Camp.Town);
            for (int i = 0; i < 600 && !w.Raid.Over; i++) Days(w, 1);
            Days(w, 2);
            Assert.AreEqual(lostBefore + w.Raid.Killed, liege.DefendersLost, 1 + 1e-9 + World.Garrison(liege), "the survivors went home");
            Assert.Less(liege.DefendersLost, lostBefore + army.Size + 1e-9);
            // and not again soon
            Assert.Greater(liege.NextMarchDay, w.Day);
        }

        [Test]
        public void AMercantileRealmSendsNobody()
        {
            var (w, s, liege) = Wronged(Personality.Mercantile);
            Take(w, s, 120);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Sack).Ok);
            Assert.IsEmpty(w.RealmArmies);
        }

        [Test]
        public void AProudLiegeMarchesToFreeItsOwnAndTheArmyIsSaved()
        {
            var (w, s, liege) = Wronged(Personality.Proud);
            Take(w, s, 120);
            Assert.IsTrue(w.ResolveConquest(ConquestChoice.Occupy, World.GarrisonNeeded(s)).Ok);
            var army = w.RealmArmies.Single();
            Assert.AreEqual((RealmArmyGoal.Relieve, s.Id), (army.Goal, army.TargetId));
            var loaded = World.LoadJson(w.SaveJson());
            loaded.SetSettlements(Settlements.Generate(Map));
            var la = loaded.RealmArmies.Single();
            Assert.AreEqual((army.FromId, army.TargetId, army.Size, army.MarchDay, army.Goal), (la.FromId, la.TargetId, la.Size, la.MarchDay, la.Goal));
            Assert.AreEqual(s.ConqueredDay, loaded.SettlementById(s.Id).ConqueredDay);
            Assert.AreEqual(s.Unrest, loaded.SettlementById(s.Id).Unrest, 1e-6);
            Assert.AreEqual(liege.NextMarchDay, loaded.SettlementById(liege.Id).NextMarchDay);
            // it arrives and fights your garrison there
            int garrison = s.Garrison;
            w.Events.Clear(); var events = new List<string>();
            for (int i = 0; i < 120 && w.RealmArmies.Count > 0; i++) { Days(w, 1); events.AddRange(w.Events); w.Events.Clear(); }
            Assert.IsTrue(events.Any(e => e.Contains("marching to free")), string.Join(" | ", events));
            Assert.IsTrue(events.Any(e => e.Contains("beat off") || e.Contains("have freed")), string.Join(" | ", events));
            if (s.Allegiance == Allegiance.Own) Assert.AreEqual(liege.Id, s.Liege, "freed, it rejoins its realm");
        }
    }
}
