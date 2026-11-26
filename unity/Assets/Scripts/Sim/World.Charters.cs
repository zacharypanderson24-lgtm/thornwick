// CHARTERS: the settlement's rank (Hamlet, Village, Town, City, by people) and the charter the player adopts at each rung from Village up. A charter
// is a specialisation: the six ways of playing (farming, trade, conquest, the sea, industry, fortress) each have one at every rung, with a gain and
// a price, so growing is a choice of road and no road is the single right one. This file is the model and the saved choice; the effects are
// applied where each system lives (a later change). Rank is derived from the people, so only the choices are saved (old saves have none).
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public enum TownRank { Hamlet, Village, Town, City }

    public sealed class Charter
    {
        public string Id, Title, Path, Blurb, Gain, Price;
        public Dictionary<string, double> Effects;
        public TownRank Rank;
    }

    public partial class World
    {
        public static readonly int[] RankPeople = { 0, 25, 60, 150 };   // people needed for Hamlet, Village, Town, City

        public static TownRank RankFor(int people)
        {
            var rank = TownRank.Hamlet;
            for (int i = 1; i < RankPeople.Length; i++) if (people >= RankPeople[i]) rank = (TownRank)i;
            return rank;
        }

        public TownRank Rank => RankFor(Peasants + Soldiers);

        static Dictionary<string, double> Fx(params (string key, double mult)[] e) => e.ToDictionary(x => x.key, x => x.mult);

        // Every gain and price is a multiplier that the game really applies (World.CharterMult): silo = grain storage, farm = wheat yield, fish = catches,
        // industry = sawmill and smithery speed, soldierFood = what soldiers eat, tribute = gold and goods from subdued settlements, trade = what a
        // partner will buy or sell in one deal.
        public static readonly Charter[] Charters =
        {
            // the Village rung: the first choice of road
            new Charter { Id = "agrarian1", Title = "Agrarian Charter", Path = "Farmer", Rank = TownRank.Village, Blurb = "A village of the plough.",
                Gain = "Silos hold half again as much grain.", Price = "Sawmills and smitheries work a tenth slower.", Effects = Fx(("silo", 1.5), ("industry", 0.9)) },
            new Charter { Id = "merchant1", Title = "Merchant Charter", Path = "Trader", Rank = TownRank.Village, Blurb = "A village of the market square.",
                Gain = "Each trade deal with a partner can be a quarter bigger.", Price = "Farms yield a tenth less: you buy what you could grow.", Effects = Fx(("trade", 1.25), ("farm", 0.9)) },
            new Charter { Id = "martial1", Title = "Martial Charter", Path = "Warlord", Rank = TownRank.Village, Blurb = "A village that keeps a sword by the door.",
                Gain = "Subdued settlements pay a quarter more tribute.", Price = "Soldiers eat half as much again.", Effects = Fx(("tribute", 1.25), ("soldierFood", 1.5)) },
            new Charter { Id = "maritime1", Title = "Maritime Charter", Path = "Seafarer", Rank = TownRank.Village, Blurb = "A village that looks to the water.",
                Gain = "Fishing huts and docks catch a quarter more.", Price = "Farms yield a tenth less: the town looks to the sea.", Effects = Fx(("fish", 1.25), ("farm", 0.9)) },
            new Charter { Id = "industrial1", Title = "Guild Charter", Path = "Industrialist", Rank = TownRank.Village, Blurb = "A village of forges and sawpits.",
                Gain = "Sawmills and smitheries work a fifth faster.", Price = "Each trade deal with a partner is a tenth smaller.", Effects = Fx(("industry", 1.2), ("trade", 0.9)) },
            new Charter { Id = "warden1", Title = "Warden Charter", Path = "Fortress", Rank = TownRank.Village, Blurb = "A village behind its palisade.",
                Gain = "Soldiers eat a quarter less: the garrison is fed from the keep.", Price = "Each trade deal with a partner is a tenth smaller: the gates are watched.", Effects = Fx(("soldierFood", 0.75), ("trade", 0.9)) },
            // the Town rung: deepen the road, or branch into another
            new Charter { Id = "agrarian2", Title = "Granary Charter", Path = "Farmer", Rank = TownRank.Town, Blurb = "The breadbasket of the district.",
                Gain = "Wheat yields a tenth more.", Price = "Fishing huts and docks catch a tenth less.", Effects = Fx(("farm", 1.1), ("fish", 0.9)) },
            new Charter { Id = "merchant2", Title = "Free Market Charter", Path = "Trader", Rank = TownRank.Town, Blurb = "A town where anything can be bought.",
                Gain = "Each trade deal with a partner can be another quarter bigger.", Price = "Soldiers eat a quarter more: a rich town needs guarding.", Effects = Fx(("trade", 1.25), ("soldierFood", 1.25)) },
            new Charter { Id = "martial2", Title = "Warband Charter", Path = "Warlord", Rank = TownRank.Town, Blurb = "A town that lives by the campaign.",
                Gain = "Subdued settlements pay another quarter more tribute.", Price = "Each trade deal with a partner is a fifth smaller.", Effects = Fx(("tribute", 1.25), ("trade", 0.8)) },
            new Charter { Id = "maritime2", Title = "Harbour Charter", Path = "Seafarer", Rank = TownRank.Town, Blurb = "A town that is a port first.",
                Gain = "Fishing huts and docks catch another fifth.", Price = "Sawmills and smitheries work a tenth slower.", Effects = Fx(("fish", 1.2), ("industry", 0.9)) },
            new Charter { Id = "industrial2", Title = "Foundry Charter", Path = "Industrialist", Rank = TownRank.Town, Blurb = "A town of smoke and hammers.",
                Gain = "Sawmills and smitheries work another tenth faster.", Price = "Farms yield a tenth less.", Effects = Fx(("industry", 1.1), ("farm", 0.9)) },
            new Charter { Id = "warden2", Title = "Bulwark Charter", Path = "Fortress", Rank = TownRank.Town, Blurb = "A town that raiders break against.",
                Gain = "Soldiers eat another quarter less.", Price = "Subdued settlements pay a fifth less tribute.", Effects = Fx(("soldierFood", 0.75), ("tribute", 0.8)) },
        };

        /// <summary>The combined effect of every charter the town holds on one thing (1 when none touches it).</summary>
        public double CharterMult(string key)
        {
            double m = 1;
            foreach (var id in CharterAt.Values)
                if (CharterById(id)?.Effects is Dictionary<string, double> fx && fx.TryGetValue(key, out var v)) m *= v;
            return m;
        }

        public static Charter CharterById(string id) => Charters.FirstOrDefault(c => c.Id == id);

        /// <summary>The charter chosen at each rung (a rung with no entry is still open). Saved.</summary>
        public readonly Dictionary<TownRank, string> CharterAt = new Dictionary<TownRank, string>();

        public IEnumerable<Charter> CharterChoices(TownRank rung) => Charters.Where(c => c.Rank == rung);

        /// <summary>The lowest rung the town has reached that has charters to choose from and none chosen yet (null when nothing is waiting).</summary>
        public TownRank? OpenCharterRung()
        {
            for (var r = TownRank.Village; r <= Rank; r++)
                if (!CharterAt.ContainsKey(r) && CharterChoices(r).Any()) return r;
            return null;
        }

        /// <summary>True when the town holds the charter with this id.</summary>
        public bool HasCharter(string id) => CharterAt.Values.Contains(id);

        /// <summary>How many charters of this path (Farmer, Trader, ...) the town holds: 0, 1 or 2.</summary>
        public int CharterDepth(string path) => CharterAt.Values.Count(id => CharterById(id)?.Path == path);

        public Result AdoptCharter(string id)
        {
            var c = CharterById(id);
            if (c == null) return Result.No("There is no such charter");
            if (Rank < c.Rank) return Result.No($"Your settlement must first grow to a {c.Rank} ({RankPeople[(int)c.Rank]} people)");
            if (CharterAt.ContainsKey(c.Rank)) return Result.No($"You have already chosen your {c.Rank} charter");
            CharterAt[c.Rank] = c.Id;
            Events.Add($"The {c.Title} is signed. {c.Gain}");
            return Result.Yes();
        }

        JObject SaveCharters()
        {
            var o = new JObject();
            foreach (var kv in CharterAt) o[kv.Key.ToString()] = kv.Value;
            return o;
        }

        void LoadCharters(JObject o)
        {
            CharterAt.Clear();
            if (o == null) return;   // saves from before charters: none chosen
            foreach (var p in o.Properties())
                if (Enum.TryParse(p.Name, out TownRank r) && CharterById((string)p.Value) is Charter c && c.Rank == r) CharterAt[r] = c.Id;
        }
    }
}
