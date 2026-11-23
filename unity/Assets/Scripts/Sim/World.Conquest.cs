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

    /// <summary>The army that took a settlement, waiting there for your decision: its survivors and what they carry. Saved.</summary>
    public sealed class PendingConquest
    {
        public int SettlementId, CampId, Infantry, Archers;
        public readonly Dictionary<Res, int> Loot = new Dictionary<Res, int>();
    }

    /// <summary>Soldiers marching from home to join an occupied settlement's garrison. Saved.</summary>
    public sealed class Detachment { public int SettlementId, Infantry, Archers, ArriveDay; }

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
        public int DefendersHome(RaiderCamp c) => c.IsTown ? DefendersOf(c.Town) : CampRaidersHome(c);

        /// <summary>Defenders a settlement has now: its garrison less those killed fighting you and not yet replaced (at least one).</summary>
        public static int DefendersOf(Settlement s) => Math.Max(1, Garrison(s) - (int)Math.Ceiling(s.DefendersLost - 1e-9));

        /// <summary>How fast a settlement replaces its fallen defenders, a share of its full garrison each month: a proud place rebuilds and
        /// fortifies fastest, a sacked one slowest.</summary>
        public static double RecoverShare(Settlement s) => s.Devastated ? 0.08 : s.Personality == Personality.Proud ? 0.4 : 0.25;

        /// <summary>A sacked settlement lies waste this long.</summary>
        public const int SackWasteDays = 360;
        /// <summary>While laid waste it trades and pays tribute at this fraction.</summary>
        public const double WasteFactor = 0.4;

        /// <summary>Monthly: settlements replace the defenders they lost, and those laid waste by a sack recover when their time is up.</summary>
        void RecoverSettlements()
        {
            foreach (var s in _settlements)
            {
                bool was = s.Devastated;
                s.Devastated = Day < s.DevastatedUntil;
                if (was && !s.Devastated && SettlementKm(s) <= RaidFarKm) Events.Add($"{s.Name} has recovered from its sacking.");
                if (s.DefendersLost > 0) s.DefendersLost = Math.Max(0, s.DefendersLost - Garrison(s) * RecoverShare(s));
            }
        }

        /// <summary>The army that took a settlement, waiting for you to decide its fate (null otherwise). Saved, so saving and loading
        /// can neither lose the army nor skip the decision.</summary>
        public PendingConquest PendingConquest { get; private set; }
        /// <summary>The battle the pending decision came from, for the camp panel (runtime only: null after loading).</summary>
        public CampBattle PendingBattle { get; private set; }
        /// <summary>What happened when the last decision was made (shown in the report).</summary>
        public string LastConquestNews { get; private set; } = "";

        /// <summary>Reinforcements on the road to an occupied settlement. Saved.</summary>
        public readonly List<Detachment> Detachments = new List<Detachment>();

        /// <summary>The town's own carts carry this many times what the soldiers carry on their backs when a settlement is sacked.</summary>
        public const int SackCartFactor = 2;

        void Anger(Settlement s, int amount) { if (s != null) s.Attitude = Math.Clamp(s.Attitude - amount, -100, 100); }

        /// <summary>Everyone above this settlement in its realm takes an attack on it badly; those that answer to it a little less so.</summary>
        public void HurtRealm(Settlement s, int amount)
        {
            foreach (var l in LiegesOf(s)) Anger(l, amount);
            foreach (var sub in SubjectsOf(s)) Anger(sub, amount / 2);
        }

        /// <summary>Recompute which realm every settlement is in after somebody broke away.</summary>
        void RecomputeRealms()
        {
            var hasSubjects = new HashSet<int>(_settlements.Where(o => o.Liege >= 0).Select(o => o.Liege));
            foreach (var s in _settlements)
            {
                var top = LiegesOf(s).LastOrDefault() ?? s;   // the top of its chain (missing lieges and loops cut off)
                s.Realm = SettlementById(top.Liege) == null && (hasSubjects.Contains(top.Id) || top.IsSeat) ? top.Id : -1;
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
            s.DefendersLost += b.Units.Count(u => !u.Alive && !u.Attacker && (u.Kind == BKind.Raider || u.Kind == BKind.RaiderArcher));   // and are missing until replaced
            if (b.Victory)
            {
                var p = new PendingConquest { SettlementId = s.Id, CampId = b.Camp.Id, Infantry = b.SurvivingInfantry, Archers = b.SurvivingArchers };
                foreach (var kv in b.Loot) if (kv.Value > 0) p.Loot[kv.Key] = kv.Value;
                PendingConquest = p; PendingBattle = b; LastConquestNews = "";
                Events.Add($"{s.Name} has fallen. Choose what to do with it.");
                return;
            }
            Anger(s, 30); HurtRealm(s, 15);
            ConsiderResponse(s, s.Liege);
            var loot = b.Loot.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            SendHome(b.Camp.Id, b.SurvivingInfantry, b.SurvivingArchers, loot, MarchDays(b.Camp), $"The army pulls back from {s.Name} and marches home.");
        }

        /// <summary>Soldiers and loot set off home. Loot goes home even when no soldier does: a supply train (escorted by the garrison
        /// that stays) carries it. Only when nothing at all comes back is there no column on the road.</summary>
        void SendHome(int campId, int inf, int arc, Dictionary<Res, int> loot, int days, string news)
        {
            bool goods = loot.Values.Any(v => v > 0);
            if (inf + arc <= 0 && !goods) { Events.Add(news); return; }
            var e = new Expedition { CampId = campId, Infantry = inf, Archers = arc, ReturnDay = Day + days };
            foreach (var kv in loot) if (kv.Value > 0) e.Loot[kv.Key] = kv.Value;
            Expeditions.Add(e);
            Events.Add(inf + arc > 0 ? news : news + " The spoils go home by cart.");
        }

        /// <summary>Whether this choice can be made for the fallen settlement (occupying needs enough surviving soldiers to leave behind).</summary>
        public Result CanResolve(ConquestChoice choice, int stationed = 0)
        {
            var p = PendingConquest;
            if (p == null) return Result.No("No settlement is waiting for your decision");
            var s = SettlementById(p.SettlementId);
            if (s == null) return Result.No("The world map is still being made: try again in a moment");
            if (choice != ConquestChoice.Occupy) return Result.Yes();
            int need = GarrisonNeeded(s), have = p.Infantry + p.Archers;
            if (have < need) return Result.No($"Holding {s.Name} takes {need} soldiers; only {have} survived");
            if (stationed < need) return Result.No($"Holding {s.Name} takes at least {need} soldiers");
            if (stationed > have) return Result.No($"Only {have} soldiers survived");
            return Result.Yes(stationed);
        }

        /// <summary>Gold and goods a sack can yield (its stores and treasure, by how many people lived there).</summary>
        public static (int gold, int goods) SackYield(Settlement s) => ((int)Math.Round(s.Population / 4), (int)Math.Round(s.Population / 3));

        /// <summary>What the army can carry from a sack: what its survivors carry on their backs, plus the town's own carts (SackCartFactor
        /// times as much), less what they already took in the fight.</summary>
        public static int SackRoom(PendingConquest p) =>
            Math.Max(0, (p.Infantry * CampBattle.InfantryCarry + p.Archers * CampBattle.ArcherCarry) * (1 + SackCartFactor) - p.Loot.Values.Sum());

        /// <summary>A number of goods split across what a settlement produces, by each good's share (largest first, nothing left over).</summary>
        public static List<(Res res, int n)> SplitGoods(Settlement s, int total)
        {
            var list = new List<(Res res, int n)>();
            if (total <= 0) return list;
            var produce = s.Produce.Count > 0 ? s.Produce.OrderByDescending(p => p.share).ToList() : new List<(Res res, double share)> { (s.Makes, 1) };
            double sum = produce.Sum(p => p.share);
            int left = total;
            for (int i = 0; i < produce.Count; i++)
            {
                int n = i == produce.Count - 1 ? left : Math.Min(left, (int)Math.Round(total * produce[i].share / sum));
                left -= n;
                if (n > 0) list.Add((produce[i].res, n));
            }
            return list;
        }

        /// <summary>"20 grain, 8 timber and 3 metal".</summary>
        public static string GoodsAmounts(IEnumerable<(Res res, int n)> goods)
        {
            var parts = goods.Select(g => $"{g.n} {GoodName(g.res)}").ToList();
            return parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
        }

        /// <summary>Decide the fate of the settlement you took. The army (minus any garrison you leave) marches home with the loot.</summary>
        public Result ResolveConquest(ConquestChoice choice, int stationed = 0)
        {
            var ok = CanResolve(choice, stationed); if (!ok.Ok) return ok;
            var p = PendingConquest; var s = SettlementById(p.SettlementId);
            int liege = s.Liege;   // its realm answers for it (World.Realms.cs), whatever becomes of it
            int inf = p.Infantry, arc = p.Archers;
            string news;
            switch (choice)
            {
                case ConquestChoice.Sack:
                {
                    var (gold, goods) = SackYield(s);
                    int room = SackRoom(p);
                    int takeGold = Math.Min(gold, room); room -= takeGold;
                    int takeGoods = Math.Min(goods, room);
                    int burned = gold - takeGold + goods - takeGoods;
                    if (takeGold > 0) p.Loot[Res.Gold] = p.Loot.GetValueOrDefault(Res.Gold) + takeGold;
                    var taken = SplitGoods(s, takeGoods);
                    foreach (var (r, n) in taken) p.Loot[r] = p.Loot.GetValueOrDefault(r) + n;
                    s.Population = Math.Max(SettlementRules.TierFloor[0], Math.Round(s.Population * 0.4));
                    s.Attitude = -100;
                    s.DevastatedUntil = Day + SackWasteDays; s.Devastated = true;
                    s.DefendersLost = Math.Max(s.DefendersLost, Garrison(s));   // nobody is left to man the walls
                    HurtRealm(s, 40);
                    foreach (var o in _settlements)
                        if (o != s && Math.Max(Math.Abs(o.X - s.X), Math.Abs(o.Y - s.Y)) <= SackHeardKm) Anger(o, 10);
                    news = $"{s.Name} is sacked. The carts carry off {takeGold} gold" + (taken.Count > 0 ? " and " + GoodsAmounts(taken) : "") +
                           (burned > 0 ? $"; {burned} more burned with the town for want of carts." : ".");
                    break;
                }
                case ConquestChoice.Tribute:
                    HurtRealm(s, 40);
                    s.Liege = -1; s.Allegiance = Allegiance.Tributary; s.Attitude = -20;
                    RecomputeRealms();
                    FreeSubjects(s);
                    news = $"{s.Name} bows and will pay you tribute every month. Its old realm will not forget it.";
                    break;
                case ConquestChoice.Occupy:
                {
                    int stInf = Math.Min(inf, stationed), stArc = stationed - stInf;   // infantry hold a town first, archers make up the rest
                    inf -= stInf; arc -= stArc;
                    HurtRealm(s, 40);
                    s.Liege = -1; s.Allegiance = Allegiance.Occupied; s.GarrisonInfantry = stInf; s.GarrisonArchers = stArc; s.Attitude = -30;
                    s.ConqueredDay = Day; s.Unrest = 30; s.LastNeeded = GarrisonNeeded(s);
                    RecomputeRealms();
                    FreeSubjects(s);
                    news = $"{s.Name} is yours. {stInf} infantry and {stArc} archers stay behind to hold it.";
                    break;
                }
                default:
                    Anger(s, 40); HurtRealm(s, 25);
                    news = $"The army leaves {s.Name} with what it can carry.";
                    break;
            }
            PendingConquest = null; PendingBattle = null;
            ConsiderResponse(s, liege);
            string home = inf + arc > 0 ? $" {inf} infantry and {arc} archers march home ({SettlementMarchDays(s)} days)." : "";
            LastConquestNews = news + home;
            SendHome(p.CampId, inf, arc, p.Loot, SettlementMarchDays(s), news + home);
            return Result.Yes();
        }

        /// <summary>Send soldiers from home to an occupied settlement's garrison (infantry first, then archers). They march there and join
        /// it when they arrive; if the settlement is no longer yours by then, they turn round and come home.</summary>
        public Result ReinforceGarrison(Settlement s, int n)
        {
            if (s == null || s.Allegiance != Allegiance.Occupied) return Result.No("Only a settlement you hold can be reinforced");
            n = Math.Min(n, Infantry + Archers);
            if (n <= 0) return Result.No("You have no soldiers at home");
            int inf = Math.Min(n, Infantry), arc = n - inf;
            Soldiers -= n; Archers -= arc;
            Detachments.Add(new Detachment { SettlementId = s.Id, Infantry = inf, Archers = arc, ArriveDay = Day + SettlementMarchDays(s) });
            return Result.Yes(n);
        }

        /// <summary>Soldiers marching to a settlement's garrison right now.</summary>
        public int MarchingTo(Settlement s) => Detachments.Where(d => d.SettlementId == s.Id).Sum(d => d.Infantry + d.Archers);

        /// <summary>Bring an occupied settlement's garrison home, infantry and archers alike: it stays cowed enough to pay tribute instead.</summary>
        public Result WithdrawGarrison(Settlement s)
        {
            if (s == null || s.Allegiance != Allegiance.Occupied) return Result.No("Only a settlement you hold has a garrison");
            if (s.Garrison > 0)
                Expeditions.Add(new Expedition { CampId = TownIdBase + s.Id, Infantry = s.GarrisonInfantry, Archers = s.GarrisonArchers, ReturnDay = Day + SettlementMarchDays(s) });
            s.GarrisonInfantry = 0; s.GarrisonArchers = 0; s.Allegiance = Allegiance.Tributary;
            return Result.Yes();
        }

        /// <summary>Daily: reinforcements that reach their settlement join its garrison, or turn for home if it is no longer yours.</summary>
        void StepDetachments()
        {
            for (int i = Detachments.Count - 1; i >= 0; i--)
            {
                var d = Detachments[i];
                if (Day < d.ArriveDay) continue;
                Detachments.RemoveAt(i);
                var s = SettlementById(d.SettlementId);
                if (s != null && s.Allegiance == Allegiance.Occupied)
                {
                    s.GarrisonInfantry += d.Infantry; s.GarrisonArchers += d.Archers;
                    Events.Add($"{d.Infantry + d.Archers} soldiers have joined the garrison of {s.Name} ({s.Garrison} now).");
                }
                else
                {
                    int days = s != null ? SettlementMarchDays(s) : 1;
                    Expeditions.Add(new Expedition { CampId = TownIdBase + d.SettlementId, Infantry = d.Infantry, Archers = d.Archers, ReturnDay = Day + days });
                    Events.Add($"The soldiers sent to {(s?.Name ?? "a settlement")} found it no longer yours and are marching home.");
                }
            }
        }

        // ---------- where every soldier is ----------

        /// <summary>Soldiers in an invasion being fought right now (set when it starts, cleared when it ends).</summary>
        public int TroopsInBattleInfantry { get; private set; }
        public int TroopsInBattleArchers { get; private set; }
        int _inBattleCamp, _inBattleDays;

        void MarchOut(RaiderCamp camp, int inf, int arc)
        {
            TroopsInBattleInfantry = inf; TroopsInBattleArchers = arc; _inBattleCamp = camp.Id; _inBattleDays = MarchDays(camp);
        }

        void BattleOver() { TroopsInBattleInfantry = 0; TroopsInBattleArchers = 0; }

        /// <summary>Soldiers who will come home: armies on the road home, the army in a fight and the army waiting for your decision.</summary>
        public int TroopsReturning => Expeditions.Sum(e => e.Infantry + e.Archers) + TroopsInBattleInfantry + TroopsInBattleArchers
            + (PendingConquest != null ? PendingConquest.Infantry + PendingConquest.Archers : 0);

        /// <summary>Soldiers holding settlements for you, and those marching to join them. They live in the settlement they hold.</summary>
        public int TroopsStationed => (_settlements.Count > 0 ? _settlements.Sum(s => s.Garrison) : SavedGarrisons()) + Detachments.Sum(d => d.Infantry + d.Archers);

        /// <summary>Every soldier you have away from home. All of them draw wages; those coming home also need a bunk and a home here.</summary>
        public int TroopsAway => TroopsReturning + TroopsStationed;

        /// <summary>Monthly: tributaries pay gold and goods (and may stop paying), occupied settlements pay more (and rise up if the garrison
        /// is too small); both slowly get used to you.</summary>
        void StepConquered()
        {
            foreach (var s in _settlements)
            {
                if (s.Allegiance == Allegiance.Own) continue;
                bool held = s.Allegiance == Allegiance.Occupied;
                double waste = s.Devastated ? WasteFactor : 1;
                int gold = Math.Max(1, (int)Math.Round(s.Population / (held ? 15.0 : 25.0) * waste)), goods = (int)(MonthlyTrade(s) / (held ? 1 : 2) * waste);
                ResAmt[Res.Gold] += gold;
                var sent = new List<(Res res, int n)>();
                foreach (var (r, n) in SplitGoods(s, goods))
                {
                    int stored = (int)Math.Min(n, Math.Max(0, CropStorage(r) - ResAmt[r]));   // what does not fit in your stores is left behind
                    ResAmt[r] += stored;
                    if (stored > 0) sent.Add((r, stored));
                }
                Events.Add($"{s.Name} sends {gold} gold" + (sent.Count > 0 ? " and " + GoodsAmounts(sent) : "") + (held ? "." : " in tribute."));
                if (s.Attitude < 0) s.Attitude++;
                double roll = Noise.Hash2(s.Id, Day, Seed + 977);
                if (held) StepUnrest(s);   // World.Realms.cs: unrest, warnings and risings
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
