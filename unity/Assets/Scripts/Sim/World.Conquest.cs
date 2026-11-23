// Attacking NPC settlements. A settlement is fought like a raider camp (CampBattle): it opens as a "camp" whose Town is set, laid out on
// its own terrain with homes for tents and the lord's house for the warlord's tent, and defended by a garrison as big as its people make
// it (a capital fields far more than any camp). Brutes guard the lord and the lord is the toughest fighter there.
//
// Pulling out, or losing, leaves the settlement angrier (and the realm above it). Beating every defender, lord included, lets you choose:
//   - Leave: take what the soldiers carry and go. It survives, much angrier.
//   - Sack: take far more (gold and its goods) and burn it: it loses most of its people; every settlement nearby likes you less.
//   - Tribute: it stays under its own lord but breaks from its realm and pays you gold and goods every month. It may stop paying.
//   - Occupy: leave soldiers there as a garrison: it is yours and sends you more each month, but rises up if the garrison is too small.
// Hurting a settlement angers everyone above it in its realm (and those below it a little). How realms strike back is the next step.
using System;
using System.Collections.Generic;
using System.Linq;
using SettlementRules = CrownAndHearth.Sim.Settlements;

namespace CrownAndHearth.Sim
{
    public enum ConquestChoice : byte { Leave, Sack, Tribute, Occupy }

    public partial class World
    {
        /// <summary>Battle ids of settlements, kept clear of the raider camps' ids.</summary>
        public const int TownIdBase = 100000;
        /// <summary>Largest garrison drawn on a settlement's battle map.</summary>
        public const int MaxGarrison = 120;
        /// <summary>Settlements within this many km of a sack hear of it and like you less.</summary>
        public const int SackHeardKm = 150;

        readonly Dictionary<int, RaiderCamp> _townCamps = new Dictionary<int, RaiderCamp>();

        /// <summary>The battle a settlement is fought as (made once, kept while the game runs, like a camp's).</summary>
        public RaiderCamp TownCamp(Settlement s)
        {
            if (!_townCamps.TryGetValue(s.Id, out var c))
                _townCamps[s.Id] = c = new RaiderCamp
                {
                    Id = TownIdBase + s.Id, X = s.X, Y = s.Y, Strength = (int)s.Tier + 1, Seed = s.Seed, Relief = s.Relief, Name = s.Name, Town = s,
                };
            c.Strength = (int)s.Tier + 1;
            return c;
        }

        /// <summary>Defenders a settlement can put up: a militia in a hamlet, a garrison in bigger places (a smaller share of a big city's
        /// people take up arms, but far more of them in all). Never more than MaxGarrison on the map.</summary>
        public static int Garrison(Settlement s)
        {
            double share = s.Tier switch
            {
                SettlementTier.Hamlet => 1 / 5.0, SettlementTier.Village => 1 / 8.0, SettlementTier.Town => 1 / 10.0, SettlementTier.City => 1 / 16.0, _ => 1 / 24.0,
            };
            return Math.Clamp((int)Math.Round(s.Population * share), 2, MaxGarrison);
        }

        /// <summary>Soldiers an occupied settlement needs to keep it from rising up.</summary>
        public static int GarrisonNeeded(Settlement s) => Math.Max(2, (int)Math.Ceiling(s.Population / 40));

        /// <summary>Who would fight an invasion here now: a camp's raiders at home, or a settlement's garrison.</summary>
        public int DefendersHome(RaiderCamp c) => c.IsTown ? Garrison(c.Town) : CampRaidersHome(c);

        /// <summary>A won battle at a settlement waiting for you to choose what to do with it (null otherwise). Runtime only.</summary>
        public CampBattle PendingConquest { get; private set; }

        void Anger(Settlement s, int amount) { if (s != null) s.Attitude = Math.Clamp(s.Attitude - amount, -100, 100); }

        /// <summary>Everyone above this settlement in its realm takes an attack on it badly; those that answer to it a little less so.</summary>
        public void HurtRealm(Settlement s, int amount)
        {
            var l = SettlementById(s.Liege);
            for (int guard = 0; l != null && guard < 10; guard++) { Anger(l, amount); l = SettlementById(l.Liege); }
            foreach (var sub in SubjectsOf(s)) Anger(sub, amount / 2);
        }

        /// <summary>Recompute which realm every settlement is in after somebody broke away.</summary>
        void RecomputeRealms()
        {
            var hasSubjects = new HashSet<int>(_settlements.Where(o => o.Liege >= 0).Select(o => o.Liege));
            foreach (var s in _settlements)
            {
                var top = s;
                for (int guard = 0; top.Liege >= 0 && guard < 10; guard++) top = _settlements[top.Liege];
                s.Realm = top.Liege < 0 && (hasSubjects.Contains(top.Id) || top.IsSeat) ? top.Id : -1;
            }
        }

        /// <summary>Whether a settlement can be attacked: not one that is already yours or pays you.</summary>
        public Result CanAttack(Settlement s)
        {
            if (s == null) return Result.No("Choose a settlement");
            if (s.Allegiance == Allegiance.Occupied) return Result.No($"{s.Name} is yours: your soldiers hold it");
            if (s.Allegiance == Allegiance.Tributary) return Result.No($"{s.Name} pays you tribute");
            if (PendingConquest != null) return Result.No("Decide what to do with the last settlement you took first");
            return Result.Yes();
        }

        /// <summary>A battle at a settlement is over (World.FinishInvasion sends town battles here).</summary>
        void FinishTownBattle(CampBattle b)
        {
            var s = b.Camp.Town;
            s.Population = Math.Max(SettlementRules.TierFloor[0], s.Population - b.RaidersKilled);   // the defenders who fell were its people
            if (b.Victory)
            {
                PendingConquest = b;
                Events.Add($"{s.Name} has fallen. Choose what to do with it.");
                return;
            }
            Anger(s, 30); HurtRealm(s, 15);
            SendArmyHome(b, b.SurvivingInfantry, b.SurvivingArchers, $"The army pulls back from {s.Name} and marches home.");
        }

        void SendArmyHome(CampBattle b, int inf, int arc, string news)
        {
            if (inf + arc <= 0) { Events.Add($"No soldiers came back from {b.Camp.Name}."); return; }
            var e = new Expedition { CampId = b.Camp.Id, Infantry = inf, Archers = arc, ReturnDay = Day + MarchDays(b.Camp) };
            foreach (var kv in b.Loot) if (kv.Value > 0) e.Loot[kv.Key] = kv.Value;
            Expeditions.Add(e);
            Events.Add(news);
        }

        /// <summary>Whether this choice can be made for the fallen settlement (occupying needs enough surviving soldiers to leave behind).</summary>
        public Result CanResolve(ConquestChoice choice, int stationed = 0)
        {
            var b = PendingConquest;
            if (b == null) return Result.No("No settlement is waiting for your decision");
            if (choice != ConquestChoice.Occupy) return Result.Yes();
            var s = b.Camp.Town; int need = GarrisonNeeded(s), have = b.SurvivingInfantry + b.SurvivingArchers;
            if (have < need) return Result.No($"Holding {s.Name} takes {need} soldiers; only {have} survived");
            if (stationed < need) return Result.No($"Holding {s.Name} takes at least {need} soldiers");
            if (stationed > have) return Result.No($"Only {have} soldiers survived");
            return Result.Yes(stationed);
        }

        /// <summary>Decide the fate of the settlement you took. The army (minus any garrison you leave) marches home with the loot.</summary>
        public Result ResolveConquest(ConquestChoice choice, int stationed = 0)
        {
            var ok = CanResolve(choice, stationed); if (!ok.Ok) return ok;
            var b = PendingConquest; var s = b.Camp.Town;
            int inf = b.SurvivingInfantry, arc = b.SurvivingArchers;
            string news;
            switch (choice)
            {
                case ConquestChoice.Sack:
                {
                    int gold = (int)Math.Round(s.Population / 4), goods = (int)Math.Round(s.Population / 3);
                    b.Loot[Res.Gold] = b.Loot.GetValueOrDefault(Res.Gold) + gold;
                    b.Loot[s.Makes] = b.Loot.GetValueOrDefault(s.Makes) + goods;
                    s.Population = Math.Max(SettlementRules.TierFloor[0], Math.Round(s.Population * 0.4));
                    s.Attitude = -100;
                    HurtRealm(s, 40);
                    foreach (var o in _settlements)
                        if (o != s && Math.Max(Math.Abs(o.X - s.X), Math.Abs(o.Y - s.Y)) <= SackHeardKm) Anger(o, 10);
                    news = $"{s.Name} is sacked and burned. The army marches home with {gold} gold and {goods} {GoodName(s.Makes)} besides what it carried.";
                    break;
                }
                case ConquestChoice.Tribute:
                    HurtRealm(s, 40);
                    s.Liege = -1; s.Allegiance = Allegiance.Tributary; s.Attitude = -20;
                    RecomputeRealms();
                    news = $"{s.Name} bows and will pay you tribute every month. Its old realm will not forget it.";
                    break;
                case ConquestChoice.Occupy:
                {
                    int stInf = Math.Min(inf, stationed), stArc = stationed - stInf;
                    inf -= stInf; arc -= stArc;
                    HurtRealm(s, 40);
                    s.Liege = -1; s.Allegiance = Allegiance.Occupied; s.Garrison = stationed; s.Attitude = -30;
                    RecomputeRealms();
                    news = $"{s.Name} is yours. {stationed} soldiers stay behind to hold it.";
                    break;
                }
                default:
                    Anger(s, 40); HurtRealm(s, 25);
                    news = $"The army leaves {s.Name} with what it can carry and marches home.";
                    break;
            }
            PendingConquest = null;
            SendArmyHome(b, inf, arc, news);
            return Result.Yes();
        }

        /// <summary>Send more soldiers from home to an occupied settlement's garrison.</summary>
        public Result ReinforceGarrison(Settlement s, int n)
        {
            if (s == null || s.Allegiance != Allegiance.Occupied) return Result.No("Only a settlement you hold can be reinforced");
            n = Math.Min(n, Infantry + Archers);
            if (n <= 0) return Result.No("You have no soldiers at home");
            int arc = Math.Max(0, n - Infantry);
            Soldiers -= n; Archers -= arc;
            s.Garrison += n;
            return Result.Yes(n);
        }

        /// <summary>Bring an occupied settlement's garrison home: it stays loyal enough to pay tribute instead.</summary>
        public Result WithdrawGarrison(Settlement s)
        {
            if (s == null || s.Allegiance != Allegiance.Occupied) return Result.No("Only a settlement you hold has a garrison");
            Expeditions.Add(new Expedition { CampId = TownIdBase + s.Id, Infantry = s.Garrison, Archers = 0, ReturnDay = Day + SettlementMarchDays(s) });
            s.Garrison = 0; s.Allegiance = Allegiance.Tributary;
            return Result.Yes();
        }

        /// <summary>Monthly: tributaries pay gold and goods (and may stop paying), occupied settlements pay more (and rise up if the garrison
        /// is too small); both slowly get used to you.</summary>
        void StepConquered()
        {
            foreach (var s in _settlements)
            {
                if (s.Allegiance == Allegiance.Own) continue;
                bool held = s.Allegiance == Allegiance.Occupied;
                int gold = Math.Max(1, (int)Math.Round(s.Population / (held ? 15.0 : 25.0))), goods = MonthlyTrade(s) / (held ? 1 : 2);
                double stored = Math.Min(goods, Math.Max(0, CropStorage(s.Makes) - ResAmt[s.Makes]));
                ResAmt[Res.Gold] += gold; ResAmt[s.Makes] += stored;
                Events.Add($"{s.Name} sends {gold} gold and {stored:0} {GoodName(s.Makes)}" + (held ? "." : " in tribute."));
                if (s.Attitude < 0) s.Attitude++;
                double roll = Noise.Hash2(s.Id, Day, Seed + 977);
                if (held)
                {
                    if (s.Garrison < GarrisonNeeded(s) && roll < 0.25)
                    {
                        Events.Add($"{s.Name} has risen up! Its people overwhelmed your {s.Garrison} soldiers there (it needed {GarrisonNeeded(s)}).");
                        s.Allegiance = Allegiance.Own; s.Garrison = 0; s.Attitude = -80;
                    }
                }
                else
                {
                    bool defiant = s.Personality is Personality.Proud or Personality.Vengeful or Personality.Warlike;
                    double chance = (defiant ? 0.06 : 0.02) + (s.Attitude < -50 ? 0.04 : 0);
                    if (roll < chance)
                    {
                        Events.Add($"{s.Name} has stopped paying you tribute.");
                        s.Allegiance = Allegiance.Own; s.Attitude = Math.Min(s.Attitude, -60);
                    }
                }
            }
        }
    }
}
