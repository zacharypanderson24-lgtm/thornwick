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

        public static readonly Charter[] Charters =
        {
            // the Village rung: the first choice of road
            new Charter { Id = "agrarian1", Title = "Agrarian Charter", Path = "Farmer", Rank = TownRank.Village, Blurb = "A village of the plough.",
                Gain = "Silos hold half again as much grain.", Price = "Markets trade a quarter less with you." },
            new Charter { Id = "merchant1", Title = "Merchant Charter", Path = "Trader", Rank = TownRank.Village, Blurb = "A village of the market square.",
                Gain = "Each trading partner trades a quarter more with you.", Price = "Your own farms work a tenth slower: you buy what you could make." },
            new Charter { Id = "martial1", Title = "Martial Charter", Path = "Warlord", Rank = TownRank.Village, Blurb = "A village that keeps a sword by the door.",
                Gain = "Tribute and frontier income are a quarter higher.", Price = "Soldiers eat half as much again." },
            new Charter { Id = "maritime1", Title = "Maritime Charter", Path = "Seafarer", Rank = TownRank.Village, Blurb = "A village that looks to the water.",
                Gain = "Fishing huts and docks catch a quarter more.", Price = "Farms work a tenth slower: the town looks to the sea." },
            new Charter { Id = "industrial1", Title = "Guild Charter", Path = "Industrialist", Rank = TownRank.Village, Blurb = "A village of forges and sawpits.",
                Gain = "Mines, quarries and sawmills work a fifth faster.", Price = "Each trading partner trades a fifth less grain with you." },
            new Charter { Id = "warden1", Title = "Warden Charter", Path = "Fortress", Rank = TownRank.Village, Blurb = "A village behind its palisade.",
                Gain = "Watchtowers collect twice the road toll.", Price = "Each trading partner trades a tenth less with you: the gates are watched." },
            // the Town rung: deepen the road, or branch into another
            new Charter { Id = "agrarian2", Title = "Granary Charter", Path = "Farmer", Rank = TownRank.Town, Blurb = "The breadbasket of the district.", Gain = "Wheat yields a tenth more.", Price = "Markets trade a tenth less with you." },
            new Charter { Id = "merchant2", Title = "Free Market Charter", Path = "Trader", Rank = TownRank.Town, Blurb = "A town where anything can be bought.", Gain = "Each trading partner trades another quarter more.", Price = "Soldiers eat a quarter more: a rich town needs guarding." },
            new Charter { Id = "martial2", Title = "Warband Charter", Path = "Warlord", Rank = TownRank.Town, Blurb = "A town that lives by the campaign.", Gain = "Frontier income is half again as much.", Price = "Each trading partner trades a fifth less with you." },
            new Charter { Id = "maritime2", Title = "Harbour Charter", Path = "Seafarer", Rank = TownRank.Town, Blurb = "A town that is a port first.", Gain = "Fishing huts and docks catch another fifth.", Price = "Quarries and mines work a tenth slower." },
            new Charter { Id = "industrial2", Title = "Foundry Charter", Path = "Industrialist", Rank = TownRank.Town, Blurb = "A town of smoke and hammers.", Gain = "Mines, quarries and sawmills work another tenth faster.", Price = "Farms work a tenth slower." },
            new Charter { Id = "warden2", Title = "Bulwark Charter", Path = "Fortress", Rank = TownRank.Town, Blurb = "A town that raiders break against.", Gain = "Watchtowers collect another half toll.", Price = "Frontier income is a fifth lower." },
        };

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
