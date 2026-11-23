// NPC settlements on the world map, generated from the world seed (nothing about their layout is saved; their people and their attitude
// towards you are). They range from hamlets of a few families to capitals of thousands. Most are hamlets and villages; a handful of
// capitals each head a realm: the towns near a capital answer to it, the villages near a town answer to the town, the hamlets near a
// village answer to the village, so a realm is a pyramid. Some settlements answer to nobody. Each has a personality (how it trades, holds
// grudges and fights), an attitude towards you, something it makes and something it needs, and it grows or shrinks with its land.
// Trading, attacking and realms responding to attacks come in later steps; this is the map of who is where and who answers to whom.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public enum SettlementTier : byte { Hamlet, Village, Town, City, Capital }

    /// <summary>Mercantile: keen to trade. Proud: will not bow, rebuilds and fortifies. Vengeful: strikes back. Cautious: pays to stay safe.
    /// Warlike: raids its neighbours.</summary>
    public enum Personality : byte { Mercantile, Proud, Vengeful, Cautious, Warlike }

    /// <summary>Its standing with you: its own master, paying you tribute, or held by your soldiers (World.Conquest.cs).</summary>
    public enum Allegiance : byte { Own, Tributary, Occupied }

    /// <summary>What kind of place it is: what it lives by, which shapes how its town is laid out (World.TownBuilder.cs).</summary>
    public enum TownKind : byte { Farming, Timber, Quarry, Mining, Pastoral, Market, Fortress }

    public sealed class Settlement
    {
        public int Id, X, Y, Seed;
        public string Name;
        /// <summary>People living there now (changes as it grows; saved).</summary>
        public double Population;
        /// <summary>How it feels about you, -100 (hostile) to 100 (trusting). Saved.</summary>
        public int Attitude;
        /// <summary>The settlement it answers to (its id), or -1 when it answers to nobody.</summary>
        public int Liege = -1;
        /// <summary>The id of the realm's seat at the top of its chain, or -1 for a free settlement with nobody answering to it.</summary>
        public int Realm = -1;
        /// <summary>A realm's seat: a capital at generation. Only a seat can be called a capital.</summary>
        public bool IsSeat;
        public Personality Personality;
        /// <summary>Its main good and its main need (the first of Produce and Wants).</summary>
        public Res Makes, Needs;
        /// <summary>Everything it produces and everything it needs, each with its share (adding up to 1). Generated, not saved.</summary>
        public readonly List<(Res res, double share)> Produce = new List<(Res res, double share)>(), Wants = new List<(Res res, double share)>();
        public bool Produces(Res r) => Produce.Any(p => p.res == r);
        public bool Wanting(Res r) => Wants.Any(p => p.res == r);
        public double ShareOf(Res r) => Produce.Concat(Wants).Where(p => p.res == r).Select(p => p.share).DefaultIfEmpty(0).Max();
        /// <summary>What it lives by (generated from its goods, personality and seed; not saved).</summary>
        public TownKind Kind;
        public Relief Relief = Relief.Upland;
        /// <summary>How good its land is, 0..1: sets how fast it grows and how big it can get.</summary>
        public double Potential;
        /// <summary>How many people its land can hold (it grows towards this).</summary>
        public double Capacity;
        /// <summary>Goods it has sold to you and bought from you this month (World.SettlementTrade.cs); reset each month, saved.</summary>
        public int SoldThisMonth, BoughtThisMonth;
        /// <summary>The same, good by good (saved).</summary>
        public readonly Dictionary<Res, int> SoldOf = new Dictionary<Res, int>(), BoughtOf = new Dictionary<Res, int>();
        /// <summary>Whether it pays you or is held by you, and which of your soldiers hold it (infantry and archers kept apart). Saved.</summary>
        public Allegiance Allegiance;
        public int GarrisonInfantry, GarrisonArchers;
        /// <summary>Your soldiers stationed there, all told.</summary>
        public int Garrison => GarrisonInfantry + GarrisonArchers;

        public SettlementTier Tier
        {
            get
            {
                var t = Settlements.TierOf(Population);
                return t == SettlementTier.Capital && !IsSeat ? SettlementTier.City : t;
            }
        }

        public bool WillTrade => Settlements.WillTrade(this);
        public override string ToString() => $"{Name} ({X},{Y}) {Tier} {Population:0}";
    }

    public static class Settlements
    {
        /// <summary>Lower bound of each size: a village from 40 people, a town from 200, a city from 1000, a capital from 4000.</summary>
        public static readonly int[] TierFloor = { 10, 40, 200, 1000, 4000 };
        public static readonly int[] TierCeil = { 40, 200, 1000, 4000, 12000 };
        public const int Capitals = 6, Cities = 22, CapitalApart = 140, CityApart = 55, ExtraPerRegion = 3, ExtraApart = 12, HomeClear = 25, CampClear = 8;
        /// <summary>How far each size reaches for a settlement to answer to it (km).</summary>
        public const int CapitalReach = 180, CityReach = 90, TownReach = 45, VillageReach = 25;

        public static SettlementTier TierOf(double population)
        {
            for (int t = TierFloor.Length - 1; t > 0; t--) if (population >= TierFloor[t]) return (SettlementTier)t;
            return SettlementTier.Hamlet;
        }

        public static string TierName(SettlementTier t) => t switch
        {
            SettlementTier.Hamlet => "Hamlet", SettlementTier.Village => "Village", SettlementTier.Town => "Town", SettlementTier.City => "City", _ => "Capital",
        };

        public static string PersonalityName(Personality p) => p.ToString();

        public static string PersonalityNote(Personality p) => p switch
        {
            Personality.Mercantile => "keen to trade",
            Personality.Proud => "will not bow; rebuilds and fortifies",
            Personality.Vengeful => "strikes back when wronged",
            Personality.Cautious => "pays to stay safe",
            _ => "raids its neighbours",
        };

        public static string AttitudeName(int a) => a <= -50 ? "Hostile" : a <= -15 ? "Wary" : a < 15 ? "Neutral" : a < 50 ? "Friendly" : "Trusting";

        /// <summary>The attitude it needs before it will trade with you: a mercantile town deals with almost anyone, a warlike one only with friends.</summary>
        public static int TradeThreshold(Personality p) => p switch
        {
            Personality.Mercantile => -20, Personality.Cautious => 0, Personality.Proud => 10, Personality.Vengeful => 10, _ => 40,
        };

        /// <summary>Whether it will trade with you now: big enough to have a market (a village or more) and warm enough towards you.</summary>
        public static bool WillTrade(Settlement s) => s.Tier >= SettlementTier.Village && s.Attitude >= TradeThreshold(s.Personality);

        static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        /// <summary>Every NPC settlement of this world. Region sites become the larger places (the best of them capitals and cities), and a few
        /// more spots in each land region become hamlets and villages. None sit on your home site or on a raider camp.</summary>
        public static List<Settlement> Generate(WorldMap map, IReadOnlyList<RaiderCamp> camps = null)
        {
            int seed = map.Seed;
            var home = map.HomeSite;
            int hx = home?.X ?? WorldMap.Size / 2, hy = home?.Y ?? WorldMap.Size / 2;
            bool Clear(int x, int y) =>
                Cheb(x, y, hx, hy) >= HomeClear && (camps == null || camps.All(c => Cheb(c.X, c.Y, x, y) >= CampClear));

            // ---- candidate places: the region sites, then extra spots on good land in each region ----
            var list = new List<Settlement>();
            var names = new HashSet<string>();
            foreach (var r in map.RegionList)
            {
                if (!r.IsLand || r.Site == null || r.Id == map.HomeRegion || !Clear(r.Site.X, r.Site.Y)) continue;
                names.Add(r.Site.Name);
                list.Add(new Settlement { X = r.Site.X, Y = r.Site.Y, Name = r.Site.Name });
            }
            int regionSites = list.Count;
            foreach (var r in map.RegionList)
            {
                if (!r.IsLand) continue;
                int added = 0;
                for (int a = 0; a < 40 && added < ExtraPerRegion; a++)
                {
                    int x = r.Col * WorldMap.RegionCells + (int)(Noise.Hash2(r.Id, a, seed + 811) * WorldMap.RegionCells);
                    int y = r.Row * WorldMap.RegionCells + (int)(Noise.Hash2(r.Id, a, seed + 812) * WorldMap.RegionCells);
                    if (!WorldMap.Inb(x, y) || !map.IsLand(x, y) || map.Fertility[WorldMap.Cell(x, y)] < 0.15f) continue;
                    if (!Clear(x, y) || list.Any(s => Cheb(s.X, s.Y, x, y) < ExtraApart)) continue;
                    // only some of the spots are settled, more on good land
                    if (Noise.Hash2(r.Id, a, seed + 813) > 0.35 + 0.6 * r.Fertile) continue;
                    list.Add(new Settlement { X = x, Y = y, Name = map.PlaceName(names, 10000 + r.Id * 50 + a) });
                    added++;
                }
            }

            // ---- what each place is like ----
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                s.Id = i; s.Seed = seed * 131 + i * 7919 + 17;
                var r = map.RegionAt(s.X, s.Y);
                s.Relief = r.Relief;
                double cellFert = map.Fertility[WorldMap.Cell(s.X, s.Y)];
                bool river = (map.Features[WorldMap.Cell(s.X, s.Y)] & (MapFeature.River | MapFeature.MajorRiver)) != 0;
                s.Potential = Math.Clamp(0.45 * r.Fertile + 0.15 * cellFert + (r.RiverKm > 0 || river ? 0.12 : 0) + (r.Coastal ? 0.08 : 0)
                    + 0.1 * (1 - r.Mountain) + 0.15 * Noise.Hash2(i, 1, seed + 820), 0, 1);
            }

            // ---- sizes: the best region sites, kept apart, are the capitals, then the cities; then towns; the rest villages and hamlets ----
            var tier = new SettlementTier[list.Count];
            var bySite = Enumerable.Range(0, regionSites).OrderByDescending(i => list[i].Potential).ThenBy(i => i).ToList();
            var big = new List<int>();
            void Pick(int count, int apart, SettlementTier t)
            {
                int n = 0;
                foreach (int i in bySite)
                {
                    if (n >= count) break;
                    if (tier[i] != SettlementTier.Hamlet || big.Any(j => Cheb(list[j].X, list[j].Y, list[i].X, list[i].Y) < apart)) continue;
                    tier[i] = t; big.Add(i); n++;
                }
            }
            for (int i = 0; i < list.Count; i++) tier[i] = SettlementTier.Hamlet;
            Pick(Capitals, CapitalApart, SettlementTier.Capital);
            Pick(Cities, CityApart, SettlementTier.City);
            foreach (int i in bySite)
                if (tier[i] == SettlementTier.Hamlet) tier[i] = Noise.Hash2(i, 3, seed + 820) < 0.2 + 0.5 * list[i].Potential ? SettlementTier.Town : SettlementTier.Village;
            for (int i = regionSites; i < list.Count; i++)
                tier[i] = Noise.Hash2(i, 4, seed + 820) < 0.3 ? SettlementTier.Village : SettlementTier.Hamlet;

            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i]; int t = (int)tier[i];
                s.IsSeat = tier[i] == SettlementTier.Capital;
                double f = 0.1 + 0.8 * (0.5 * s.Potential + 0.5 * Noise.Hash2(i, 5, seed + 820));
                s.Population = Math.Round(TierFloor[t] + (TierCeil[t] - TierFloor[t]) * f);
                // good land can hold more people than it has now: room to grow into the next size (seats and cities up to their own ceiling)
                s.Capacity = Math.Round(t >= (int)SettlementTier.City ? TierCeil[t] * (0.7 + 0.3 * s.Potential) : s.Population * (1.6 + 2.4 * s.Potential));
                s.Capacity = Math.Max(s.Capacity, s.Population);
            }

            // ---- what each place produces and needs: its land, and what a place its size consumes and can craft ----
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                bool river = (map.Features[WorldMap.Cell(s.X, s.Y)] & (MapFeature.River | MapFeature.MajorRiver)) != 0;
                AssignGoods(s, map.RegionAt(s.X, s.Y), tier[i], river, seed + i * 31);
            }

            // ---- who answers to whom: each place looks for the nearest bigger one within that one's reach ----
            int Reach(SettlementTier t) => t switch
            {
                SettlementTier.Capital => CapitalReach, SettlementTier.City => CityReach, SettlementTier.Town => TownReach, _ => VillageReach,
            };
            foreach (var want in new[] { SettlementTier.City, SettlementTier.Town, SettlementTier.Village, SettlementTier.Hamlet })
                for (int i = 0; i < list.Count; i++)
                {
                    if (tier[i] != want) continue;
                    if (Noise.Hash2(i, 6, seed + 820) < (want == SettlementTier.City ? 0.2 : 0.15)) continue;   // some answer to nobody
                    int best = -1, bestD = int.MaxValue;
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (tier[j] <= want) continue;
                        int d = Cheb(list[i].X, list[i].Y, list[j].X, list[j].Y);
                        if (d > Reach(tier[j]) || d >= bestD) continue;
                        best = j; bestD = d;
                    }
                    list[i].Liege = best;
                }
            var hasSubjects = new bool[list.Count];
            foreach (var s in list) if (s.Liege >= 0) hasSubjects[s.Liege] = true;
            foreach (var s in list)
            {
                var top = s; while (top.Liege >= 0) top = list[top.Liege];
                s.Realm = top.Liege < 0 && (hasSubjects[top.Id] || top.IsSeat) ? top.Id : -1;
            }

            // ---- personality and attitude: places share their liege's ways half the time, so a realm has a character ----
            foreach (var want in new[] { SettlementTier.Capital, SettlementTier.City, SettlementTier.Town, SettlementTier.Village, SettlementTier.Hamlet })
                for (int i = 0; i < list.Count; i++)
                {
                    if (tier[i] != want) continue;
                    var s = list[i]; var r = map.RegionAt(s.X, s.Y);
                    if (s.Liege >= 0 && Noise.Hash2(i, 7, seed + 820) < 0.5) s.Personality = list[s.Liege].Personality;
                    else s.Personality = PickPersonality(r, Noise.Hash2(i, 8, seed + 820));
                    int baseline = s.Personality switch
                    {
                        Personality.Mercantile => 15, Personality.Cautious => 5, Personality.Proud => -5, Personality.Vengeful => 0, _ => -25,
                    };
                    int near = Cheb(s.X, s.Y, hx, hy) < 60 ? -5 : 0;   // close neighbours are a little warier of a new town
                    int jitter = (int)Math.Round((Noise.Hash2(i, 9, seed + 820) - 0.5) * 20);
                    s.Attitude = Math.Clamp(baseline + near + jitter, -100, 100);
                    s.Kind = KindOf(s, r, tier[i], Noise.Hash2(i, 11, seed + 820));
                }
            return list;
        }

        /// <summary>What a place lives by. A warlike or proud town (or a cautious one in the hills) may be a fortress; a mercantile town on a
        /// river or the coast, or any big mercantile place, a market town; otherwise what it mainly produces sets its kind.</summary>
        public static TownKind KindOf(Settlement s, MapRegion r, SettlementTier tier, double roll)
        {
            if (tier >= SettlementTier.Town)
            {
                double fort = s.Personality switch { Personality.Warlike => 0.55, Personality.Proud => 0.4, Personality.Cautious => r.Mountain > 0.3 ? 0.3 : 0.1, _ => 0.05 };
                if (roll < fort) return TownKind.Fortress;
                double market = s.Personality == Personality.Mercantile ? (r.Coastal || r.RiverKm > 0 || tier >= SettlementTier.City ? 0.8 : 0.45) : tier >= SettlementTier.City ? 0.25 : 0;
                if (roll > 1 - market) return TownKind.Market;
            }
            return s.Makes switch
            {
                Res.Food or Res.Flax => TownKind.Farming,
                Res.Wood or Res.Charcoal => TownKind.Timber,
                Res.Stone => TownKind.Quarry,
                Res.Ore or Res.Metal => TownKind.Mining,
                Res.Wool => TownKind.Pastoral,
                _ => TownKind.Farming,
            };
        }

        /// <summary>"Farming", "Fortress"...: put before the size ("a Farming village", "a Fortress town").</summary>
        public static string KindName(TownKind k) => k switch
        {
            TownKind.Farming => "Farming", TownKind.Timber => "Timber", TownKind.Quarry => "Quarrying", TownKind.Mining => "Mining",
            TownKind.Pastoral => "Herding", TownKind.Market => "Market", _ => "Fortress",
        };

        static Personality PickPersonality(MapRegion r, double roll)
        {
            double merc = 25 + (r.Coastal ? 15 : 0) + (r.RiverKm > 0 ? 10 : 0), proud = 20 + (r.Mountain > 0.3 ? 10 : 0), veng = 15, caut = 25,
                war = 15 + (r.Mountain > 0.3 ? 15 : 0) + (r.Forest > 0.5 ? 10 : 0);
            double total = merc + proud + veng + caut + war, x = roll * total;
            if ((x -= merc) < 0) return Personality.Mercantile;
            if ((x -= proud) < 0) return Personality.Proud;
            if ((x -= veng) < 0) return Personality.Vengeful;
            if ((x -= caut) < 0) return Personality.Cautious;
            return Personality.Warlike;
        }

        /// <summary>What a place produces and needs, each a weighted list (shares add up to 1).
        /// Produces: the two or three raw goods its land gives most of (fertile plains grain and flax, woods timber, hills stone and iron
        /// ore, open dry country wool), and once it is big enough what it crafts from them (a village with woods burns charcoal, a town
        /// with iron, or any city, works metal).
        /// Needs: what a place its size uses that it does not produce: everyone needs food and timber, a village also stone and cloth
        /// (wool or flax), a town also metal (and ore and charcoal if it works metal itself).
        /// The first of each is its main good (Makes, Needs).</summary>
        public static void AssignGoods(Settlement s, MapRegion r, SettlementTier tier, bool river, int seed)
        {
            double J(int k) => (Noise.Hash2(s.Id, k, seed + 1301) - 0.5) * 0.12;   // a little difference between neighbours
            var raw = new List<(Res res, double v)>
            {
                (Res.Food, r.Fertile + (river ? 0.15 : 0) + J(1)),
                (Res.Flax, r.Fertile * 0.55 + Math.Min(0.25, r.Rain / 4000.0) + J(2)),
                (Res.Wood, r.Forest * 1.05 + J(3)),
                (Res.Stone, r.Mountain * 0.9 + 0.08 + J(4)),
                (Res.Ore, r.Mountain * 0.8 + J(5)),
                (Res.Wool, (1 - r.Forest) * (1 - r.Fertile) * 0.7 + J(6)),
            };
            var sorted = raw.Where(p => p.v > 0.05).OrderByDescending(p => p.v).ThenBy(p => (int)p.res).ToList();
            if (sorted.Count == 0) sorted.Add((Res.Food, 0.1));
            int most = tier == SettlementTier.Hamlet ? 2 : 3;
            var produce = sorted.Where((p, k) => k == 0 || (k < most && p.v >= sorted[0].v * 0.45)).Select(p => (res: p.res, v: Math.Max(0.05, p.v))).ToList();
            // a village or bigger always works a second good, the best of the rest its land gives
            if (tier >= SettlementTier.Village && produce.Count < 2 && sorted.Count > 1) produce.Add((sorted[1].res, Math.Max(0.05, sorted[1].v)));
            // crafted goods for bigger places
            bool hasOre = produce.Any(p => p.res == Res.Ore), hasWood = produce.Any(p => p.res == Res.Wood);
            if (tier >= SettlementTier.Village && hasWood && produce.Count < 4) produce.Add((Res.Charcoal, produce.First(p => p.res == Res.Wood).v * 0.4));
            if ((tier >= SettlementTier.Town && hasOre) || tier >= SettlementTier.City) produce.Add((Res.Metal, 0.25 + (hasOre ? 0.15 : 0)));
            double total = produce.Sum(p => p.v);
            s.Produce.Clear();
            foreach (var (res, v) in produce) s.Produce.Add((res, v / total));

            // what it uses that it does not make, with the main needs first
            var basket = new List<(Res res, double v)> { (Res.Food, 1.0), (Res.Wood, 0.7), (Res.Metal, 0.3) };   // tools even in a hamlet
            if (tier >= SettlementTier.Village) { basket.Add((Res.Stone, 0.5)); basket.Add((r.Rain > 700 ? Res.Flax : Res.Wool, 0.45)); }
            if (tier >= SettlementTier.Town) basket[2] = (Res.Metal, 0.6);
            if (s.Produces(Res.Metal)) { basket.Add((Res.Ore, 0.55)); basket.Add((Res.Charcoal, 0.35)); }
            var needs = basket.Where(b => !s.Produces(b.res))
                              .Select(b => (res: b.res, v: b.v + J(10 + (int)b.res))).OrderByDescending(b => b.v).ThenBy(b => (int)b.res).Take(4).ToList();
            if (needs.Count == 0)   // a place that makes all it needs still wants what it cannot make at all
                needs.Add((new[] { Res.Metal, Res.Wool, Res.Stone, Res.Flax }.First(g => !s.Produces(g)), 0.3));
            double nt = needs.Sum(n => n.v);
            s.Wants.Clear();
            foreach (var (res, v) in needs) s.Wants.Add((res, v / nt));
            s.Makes = s.Produce[0].res; s.Needs = s.Wants[0].res;
        }

        /// <summary>"grain, flax and timber".</summary>
        public static string GoodsList(IEnumerable<(Res res, double share)> goods)
        {
            var names = goods.Select(g => World.GoodName(g.res)).ToList();
            return names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>A month passes: each place grows towards what its land can hold (1 to 5% a year, faster on good land), and shrinks if it
        /// is over it. Returns the places that grew into a bigger size this month.</summary>
        public static List<Settlement> Grow(IReadOnlyList<Settlement> list)
        {
            var grew = new List<Settlement>();
            foreach (var s in list)
            {
                var before = s.Tier;
                double rate = (0.01 + 0.04 * s.Potential) / 12;
                s.Population = Math.Max(TierFloor[0], s.Population + s.Population * rate * (1 - s.Population / Math.Max(1, s.Capacity)));
                if (s.Tier > before) grew.Add(s);
            }
            return grew;
        }
    }
}
