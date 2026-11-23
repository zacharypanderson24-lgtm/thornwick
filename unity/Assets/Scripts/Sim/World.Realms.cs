// How settlements answer you: unrest in the places you occupy, what becomes of a conquered settlement's own subjects, and the armies
// realms send when you hurt one of their own.
//
// UNREST (occupied settlements). Each month an occupied settlement's unrest (0..100) moves by the sum of its reasons:
//   - how it feels about you (an angry place grows restless, a friendly one calms),
//   - its ways (proud, vengeful and warlike places chafe; cautious ones settle),
//   - a recent conquest (the first six months),
//   - being laid waste by a sack,
//   - the garrison: too small and unrest climbs fast; a garrison above what is needed calms it, but no garrison makes a rising impossible.
// You are warned when it passes 50 and 75, and when the place grows enough to need a bigger garrison. At 75 a rising may break out, at 100 it
// does. A rising is fought out (Skirmish): the garrison either puts it down (losses on both sides, unrest falls) or is driven out (half of
// what is left escapes and marches home). Every soldier is accounted for in the report.
//
// SUBJECTS. When you occupy a settlement or make it pay tribute, the settlements that answered to it break away and answer to nobody: they
// are not yours and pay you nothing. Sacking it or leaving it changes nobody's allegiance.
//
// REALM ARMIES. When you attack a settlement, its liege (or a liege above that) may send soldiers: a vengeful or warlike place always does
// when it can, a proud one marches to free its own people (an occupied subject) or strikes back when it is angry, a cautious one only when
// pushed hard, a mercantile one never. The sender must be near enough, have soldiers to spare (they are missing from its garrison while
// away) and not have marched in the last half year. It mobilizes first (announced), then marches: against your town it comes as a warband
// (spotted, then a raid); against a settlement you hold it fights your garrison there. Survivors go home.
using System;
using System.Collections.Generic;
using System.Linq;
using SettlementRules = CrownAndHearth.Sim.Settlements;

namespace CrownAndHearth.Sim
{
    public enum RealmArmyGoal : byte { Retaliate, Relieve }

    /// <summary>Soldiers a settlement sends against you: mobilizing until MarchDay, then on the road until ArriveDay. Saved.</summary>
    public sealed class RealmArmy
    {
        public int FromId, TargetId, Size, MarchDay, ArriveDay;
        public RealmArmyGoal Goal;
        public bool Marching;
    }

    public partial class World
    {
        // ---------- realms: safe walks up and down ----------

        /// <summary>The settlements above this one, nearest first (missing ids and loops are cut off).</summary>
        public IEnumerable<Settlement> LiegesOf(Settlement s)
        {
            var seen = new HashSet<int> { s.Id };
            var l = SettlementById(s.Liege);
            while (l != null && seen.Add(l.Id)) { yield return l; l = SettlementById(l.Liege); }
        }

        // ---------- unrest ----------

        public const int RecentConquestDays = 180;

        /// <summary>Why an occupied settlement's unrest moves this month, each reason with its change (positive: more restless).</summary>
        public List<(string why, double change)> UnrestReasons(Settlement s)
        {
            var list = new List<(string, double)>();
            if (s.Attitude != 0) list.Add((s.Attitude < 0 ? $"it resents you ({s.Attitude:+0;-0})" : $"it has warmed to you ({s.Attitude:+0})", -s.Attitude / 10.0));
            double ways = s.Personality switch { Personality.Proud => 3, Personality.Vengeful => 2, Personality.Warlike => 2, Personality.Cautious => -1, _ => 0 };
            if (ways != 0) list.Add((ways > 0 ? $"{SettlementRules.PersonalityName(s.Personality).ToLower()} people chafe under you" : "cautious people keep their heads down", ways));
            if (Day - s.ConqueredDay < RecentConquestDays) list.Add(("conquered only recently", 4));
            if (s.Devastated) list.Add(("laid waste by your sack", 3));
            int need = GarrisonNeeded(s);
            double ratio = need > 0 ? s.Garrison / (double)need : 2;
            if (ratio < 1) list.Add(($"the garrison is too small ({s.Garrison} of {need} needed)", (1 - ratio) * 12));
            else list.Add(($"the garrison keeps order ({s.Garrison}, {need} needed)", -2 - 4 * (Math.Min(ratio, 2) - 1)));
            list.Add(("time", -1));
            return list;
        }

        public static string UnrestName(double u) => u >= 75 ? "on the brink of rising" : u >= 50 ? "restless" : u >= 25 ? "uneasy" : "calm";

        /// <summary>Monthly, for each occupied settlement: unrest moves, warnings are given, and a rising may break out.</summary>
        void StepUnrest(Settlement s)
        {
            int need = GarrisonNeeded(s);
            if (s.LastNeeded > 0 && need > s.LastNeeded && s.Garrison < need)
                Events.Add($"{s.Name} has grown: it now needs {need} soldiers to hold (you have {s.Garrison} there). Send more before it grows restless.");
            s.LastNeeded = need;
            double before = s.Unrest;
            s.Unrest = Math.Clamp(s.Unrest + UnrestReasons(s).Sum(r => r.change), 0, 100);
            if (before < 50 && s.Unrest >= 50) Events.Add($"{s.Name} is growing restless (unrest {s.Unrest:0}). {UnrestAdvice(s)}");
            if (before < 75 && s.Unrest >= 75) Events.Add($"{s.Name} is on the brink of rising against you (unrest {s.Unrest:0})! {UnrestAdvice(s)}");
            double roll = Noise.Hash2(s.Id, Day, Seed + 977);
            if (s.Unrest >= 100 || (s.Unrest >= 75 && roll < 0.3)) Rising(s);
        }

        string UnrestAdvice(Settlement s) => s.Garrison < GarrisonNeeded(s)
            ? $"It needs {GarrisonNeeded(s)} soldiers; you have {s.Garrison} there."
            : "Its garrison is big enough, but resentment is growing: more soldiers calm it faster.";

        /// <summary>A rising: the rebels (more the angrier it is) fight the garrison.</summary>
        void Rising(Settlement s)
        {
            int rebels = Math.Max(2, (int)Math.Round(Garrison(s) * (0.5 + s.Unrest / 200)));
            var (won, lostInf, lostArc, theirLost) = Skirmish(s.GarrisonInfantry, s.GarrisonArchers, rebels, 0.6, Seed + s.Id * 7 + Day);
            s.GarrisonInfantry -= lostInf; s.GarrisonArchers -= lostArc;
            s.Population = Math.Max(SettlementRules.TierFloor[0], s.Population - theirLost);
            if (won)
            {
                s.Unrest = 40; s.Attitude = Math.Max(-100, s.Attitude - 10);
                Events.Add($"{s.Name} rose against you: {rebels} rebels against your {s.Garrison + lostInf + lostArc} soldiers. The rising was put down; " +
                           $"{theirLost} rebels and {lostInf} infantry and {lostArc} archers of yours fell.");
                return;
            }
            DriveOut(s, $"{s.Name} rose against you: {rebels} rebels against your {s.Garrison + lostInf + lostArc} soldiers, and they won. {lostInf} infantry and {lostArc} archers fell; ");
            s.Attitude = -80;
        }

        /// <summary>The garrison is beaten: the survivors march home and the settlement is its own again.</summary>
        void DriveOut(Settlement s, string news)
        {
            int inf = s.GarrisonInfantry, arc = s.GarrisonArchers;
            if (inf + arc > 0)
                Expeditions.Add(new Expedition { CampId = TownIdBase + s.Id, Infantry = inf, Archers = arc, ReturnDay = Day + SettlementMarchDays(s) });
            Events.Add(news + (inf + arc > 0 ? $"{inf} infantry and {arc} archers escaped and are marching home ({SettlementMarchDays(s)} days)." : "none of the garrison escaped."));
            s.Allegiance = Allegiance.Own; s.GarrisonInfantry = 0; s.GarrisonArchers = 0; s.Unrest = 0;
        }

        /// <summary>A fight not shown on a map: your soldiers (infantry and archers) against a force of `them` of the given quality (your
        /// infantry 1, archers 0.8). The stronger side wins (with a little luck); the loser loses most of its men, the winner a share by how
        /// close it was. Returns who won and the losses.</summary>
        public static (bool won, int lostInf, int lostArc, int theirLost) Skirmish(int inf, int arc, int them, double quality, int seed)
        {
            double ours = inf + 0.8 * arc, theirs = Math.Max(0.1, them * quality);
            double luck = 0.85 + 0.3 * Noise.Hash2(seed, 1, 991);
            bool won = ours * luck >= theirs;
            int total = inf + arc;
            int ourLoss = won ? (int)Math.Round(total * Math.Min(0.6, 0.5 * theirs / Math.Max(0.1, ours))) : (int)Math.Round(total * 0.5);
            int theirLoss = won ? (int)Math.Round(them * 0.6) : (int)Math.Round(them * Math.Min(0.6, 0.5 * ours / theirs));
            ourLoss = Math.Clamp(ourLoss, 0, total);
            int lostInf = total == 0 ? 0 : Math.Min(inf, (int)Math.Round(ourLoss * inf / (double)total));
            int lostArc = Math.Min(arc, ourLoss - lostInf);
            return (won, lostInf, lostArc, Math.Clamp(theirLoss, 0, them));
        }

        // ---------- subjects ----------

        /// <summary>The settlements that answered to one you now hold or take tribute from break away: they answer to nobody (they are not
        /// yours and pay you nothing).</summary>
        void FreeSubjects(Settlement s)
        {
            var subs = SubjectsOf(s).ToList();
            foreach (var o in subs) o.Liege = -1;
            RecomputeRealms();
            if (subs.Count > 0)
                Events.Add($"{(subs.Count == 1 ? subs[0].Name + ", which answered" : subs.Count + " settlements that answered")} to {s.Name}, " +
                           $"{(subs.Count == 1 ? "breaks" : "break")} away and now answer{(subs.Count == 1 ? "s" : "")} to nobody. They are not yours.");
        }

        // ---------- realm armies ----------

        public const int RealmArmyKm = 220, RealmCooldownDays = 180, MaxRealmArmies = 2;
        public readonly List<RealmArmy> RealmArmies = new List<RealmArmy>();

        /// <summary>Whether this settlement would send soldiers over a wrong to `victim` (one of its own, or itself), and for what.</summary>
        static RealmArmyGoal? Willing(Settlement sender, bool held)
        {
            switch (sender.Personality)
            {
                case Personality.Mercantile: return null;
                case Personality.Cautious: return sender.Attitude <= -60 ? RealmArmyGoal.Retaliate : null;
                case Personality.Proud: return held ? RealmArmyGoal.Relieve : sender.Attitude <= -40 ? RealmArmyGoal.Retaliate : null;
                default: return held && sender.Personality == Personality.Warlike ? RealmArmyGoal.Relieve : RealmArmyGoal.Retaliate;
            }
        }

        /// <summary>How many it sends: a share of the defenders it has now (a warlike place sends more), 4 to 40.</summary>
        public static int ArmySize(Settlement sender) => Math.Clamp((int)Math.Round(DefendersOf(sender) * (sender.Personality == Personality.Warlike ? 0.5 : 0.35)), 4, 40);

        /// <summary>You hurt `victim`: the first one up its realm (itself first, if it is still free and not laid waste) that is willing,
        /// near enough, able and not on cooldown mobilizes.</summary>
        void ConsiderResponse(Settlement victim, int liegeId)
        {
            if (RealmArmies.Count >= MaxRealmArmies) return;
            bool held = victim.Allegiance == Allegiance.Occupied;
            var candidates = new List<Settlement>();
            if (victim.Allegiance == Allegiance.Own && !victim.Devastated) candidates.Add(victim);
            if (SettlementById(liegeId) is Settlement liege && liege != victim)
            {
                candidates.Add(liege);
                candidates.AddRange(LiegesOf(liege).Where(l => l != victim));
            }
            candidates.RemoveAll(c => c.Allegiance != Allegiance.Own);
            foreach (var c in candidates)
            {
                if (Day < c.NextMarchDay || RealmArmies.Any(a => a.FromId == c.Id)) continue;
                var goal = Willing(c, held && c != victim);
                if (goal == null || DefendersOf(c) < 8) continue;
                int km = goal == RealmArmyGoal.Relieve ? (int)Math.Round(Math.Sqrt(Math.Pow(c.X - victim.X, 2) + Math.Pow(c.Y - victim.Y, 2))) : SettlementKm(c);
                if (km > RealmArmyKm) continue;
                int size = ArmySize(c), muster = 10 + (int)(Noise.Hash2(c.Id, Day, Seed + 1201) * 10);
                RealmArmies.Add(new RealmArmy { FromId = c.Id, TargetId = goal == RealmArmyGoal.Relieve ? victim.Id : -1, Size = size, Goal = goal.Value, MarchDay = Day + muster });
                c.NextMarchDay = Day + RealmCooldownDays;
                Events.Add(goal == RealmArmyGoal.Relieve
                    ? $"{c.Name} is mobilizing {size} soldiers to free {victim.Name} from you. They will set out in about {muster} days."
                    : $"{c.Name} is mobilizing {size} soldiers to strike back at you for {victim.Name}. They will set out in about {muster} days.");
                return;
            }
        }

        int MarchDaysBetween(Settlement a, Settlement b) =>
            Math.Max(1, ((int)Math.Round(Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2))) + MarchKmPerDay - 1) / MarchKmPerDay);

        /// <summary>Daily: armies finish mustering and set out (a strike at your town waits for the road to be clear of other warbands), and
        /// a relief army that reaches a settlement you hold fights your garrison there.</summary>
        void StepRealmArmies()
        {
            for (int i = RealmArmies.Count - 1; i >= 0; i--)
            {
                var a = RealmArmies[i];
                var from = SettlementById(a.FromId);
                if (from == null) { RealmArmies.RemoveAt(i); continue; }
                if (!a.Marching)
                {
                    if (Day < a.MarchDay) continue;
                    if (a.Goal == RealmArmyGoal.Retaliate)
                    {
                        if (Warband != null || (Raid != null && !Raid.Over)) continue;   // one warband on the road at a time
                        from.DefendersLost += a.Size;   // they are away from home
                        var camp = TownCamp(from);
                        Warband = new Warband { CampId = camp.Id, Band = a.Size, LeaveDay = Day, ArriveDay = Day + SettlementMarchDays(from) };
                        Events.Add($"{a.Size} soldiers from {from.Name} have set out against you. They will be here in about {SettlementMarchDays(from)} days.");
                        RealmArmies.RemoveAt(i);   // from here on it is a warband (World.Camps.cs)
                        continue;
                    }
                    var target = SettlementById(a.TargetId);
                    if (target == null || target.Allegiance != Allegiance.Occupied) { Events.Add($"{from.Name} stands its army down."); RealmArmies.RemoveAt(i); continue; }
                    from.DefendersLost += a.Size;
                    a.Marching = true; a.ArriveDay = Day + MarchDaysBetween(from, target);
                    Events.Add($"{a.Size} soldiers from {from.Name} are marching to free {target.Name}. They will be there in about {a.ArriveDay - Day} days.");
                    continue;
                }
                if (Day < a.ArriveDay) continue;
                RealmArmies.RemoveAt(i);
                var held = SettlementById(a.TargetId);
                int back = a.Size;
                if (held != null && held.Allegiance == Allegiance.Occupied)
                {
                    var (won, lostInf, lostArc, theirLost) = Skirmish(held.GarrisonInfantry, held.GarrisonArchers, a.Size, 0.75, Seed + held.Id * 13 + Day);
                    held.GarrisonInfantry -= lostInf; held.GarrisonArchers -= lostArc;
                    back = a.Size - theirLost;
                    if (won)
                        Events.Add($"Your garrison at {held.Name} beat off {a.Size} soldiers from {from.Name}: {theirLost} of them and {lostInf} infantry and {lostArc} archers of yours fell.");
                    else
                    {
                        DriveOut(held, $"{a.Size} soldiers from {from.Name} have freed {held.Name}: {lostInf} infantry and {lostArc} archers of yours fell; ");
                        held.Liege = from.Id; RecomputeRealms();   // it goes back to its realm
                    }
                }
                from.DefendersLost = Math.Max(0, from.DefendersLost - back);   // the survivors are home again
            }
        }

        /// <summary>A realm army that came as a warband (a raid on your town) is over: its survivors go home to their settlement.</summary>
        void TownRaidOver(RaiderCamp camp, int survivors)
        {
            var from = camp.Town;
            from.DefendersLost = Math.Max(0, from.DefendersLost - survivors);
        }

        /// <summary>Armies being raised or on the way against you, by the settlement that sends them, for the settlement panel.</summary>
        public string ArmyNews(Settlement s)
        {
            var a = RealmArmies.FirstOrDefault(r => r.FromId == s.Id);
            if (a != null)
            {
                var t = SettlementById(a.TargetId);
                string what = a.Goal == RealmArmyGoal.Relieve ? $"to free {t?.Name}" : "against you";
                return a.Marching ? $"{a.Size} of its soldiers are marching {what} (there in {Math.Max(0, a.ArriveDay - Day)} days)"
                                  : $"Mobilizing {a.Size} soldiers {what} (they set out in {Math.Max(0, a.MarchDay - Day)} days)";
            }
            if (Warband != null && Warband.CampId == TownIdBase + s.Id) return $"{Warband.Band} of its soldiers are marching against you (here in {Math.Max(0, Warband.ArriveDay - Day)} days)";
            return Day < s.NextMarchDay ? "Will not march again for a while" : "";
        }

        // ---------- saving ----------

        Newtonsoft.Json.Linq.JArray SaveRealmArmies() => new Newtonsoft.Json.Linq.JArray(RealmArmies.Select(a => new Newtonsoft.Json.Linq.JObject
        {
            ["from"] = a.FromId, ["target"] = a.TargetId, ["size"] = a.Size, ["goal"] = a.Goal.ToString(), ["march"] = a.MarchDay, ["arrive"] = a.ArriveDay, ["marching"] = a.Marching,
        }));

        void LoadRealmArmies(Newtonsoft.Json.Linq.JArray rows)
        {
            RealmArmies.Clear();
            foreach (var o in rows?.OfType<Newtonsoft.Json.Linq.JObject>() ?? Enumerable.Empty<Newtonsoft.Json.Linq.JObject>())
                RealmArmies.Add(new RealmArmy
                {
                    FromId = (int)o["from"], TargetId = (int)o["target"], Size = (int)o["size"], MarchDay = (int)o["march"], ArriveDay = (int)o["arrive"], Marching = (bool)o["marching"],
                    Goal = Enum.TryParse<RealmArmyGoal>((string)o["goal"], out var g) ? g : RealmArmyGoal.Retaliate,
                });
        }
    }
}
