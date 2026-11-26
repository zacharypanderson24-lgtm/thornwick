// Phase 2 of DESIGN_GOVERNMENT.md: appointments, recruitment (local, other settlements, international) and monthly salaries.
// Every change goes through a validated operation that returns a Result with a plain reason; the UI and (later) governors use the same ones.
// Salaries are paid in gold from the treasury on the monthly payday and never create money.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public enum CandidateSource { Local, OtherSettlement, International }
    public enum CandidateSort { Suitability, Age, Loyalty, Experience, Salary, Administration, Economics, Military, Stewardship, Diplomacy, Name }

    public sealed class Candidate
    {
        public Character C; public CandidateSource Source; public int Suitability, Salary;
        public string Rating => Positions.Rating(Suitability);
    }

    public sealed class CandidateFilter
    {
        public int? Settlement; public CandidateSource? Source;
        public int MinAge, MaxAge = 200, MinLoyalty, MinExperience, MaxSalary = int.MaxValue;
        public string Occupation = "";
        public readonly int[] MinAttr = new int[5];
    }

    /// <summary>A recruiter on the road to another people to find one person for one post.</summary>
    public sealed class ForeignSearch { public Post Post; public int Settlement, ReadyDay, Serial; }

    public partial class World
    {
        public const int ForeignSearchGold = 60, ForeignBaseDays = 30, ForeignSearchCooldown = 180, ForeignCandidatesTried = 3;

        public readonly List<ForeignSearch> Searches = new List<ForeignSearch>();
        readonly Dictionary<Post, int> _searchCooldown = new Dictionary<Post, int>();
        int _searchSerial;
        /// <summary>The last payday: what was owed and what was actually paid.</summary>
        public double LastPayrollDue, LastPayrollPaid;
        public int LastPayday;

        // ---------- who holds what ----------

        public IEnumerable<Character> Officials => Characters.All.Where(c => c.Alive && c.Employment == Employment.Government && c.Post != Post.None);
        public Character PostHolder(Post post, int settlement = CapitalId) => Officials.FirstOrDefault(c => c.Post == post && c.PostSettlement == settlement);
        /// <summary>Settlements a governor could be appointed to. Only your starting settlement exists as a managed one until the multi-settlement phase.</summary>
        public IEnumerable<int> GovernableSettlements() => Enumerable.Empty<int>();
        public int PopulationOf(int settlement) => settlement == CapitalId ? Peasants + Soldiers : (int)(SettlementById(settlement)?.Population ?? 0);
        /// <summary>Gold in the treasury that pays this settlement's officials (one treasury until settlements keep their own).</summary>
        public double TreasuryFor(int settlement) => ResAmt[Res.Gold];
        /// <summary>What the officials cost a month now (salaries as they would be paid today).</summary>
        public int MonthlyPayroll => Officials.Sum(c => Pay.Monthly(c, c.Post, PopulationOf(c.PostSettlement), c.Pay));
        public int SalaryOf(Character c, Post post, PayLevel level = PayLevel.Standard, int settlement = CapitalId) => Pay.Monthly(c, post, PopulationOf(settlement), level);

        // ---------- operations ----------

        Result CheckPost(Post post, int settlement)
        {
            if (post == Post.None) return Result.No("Pick a post.");
            if (Positions.LockedReason(post, Era) is string locked) return Result.No(locked);
            if (post == Post.Governor)
            {
                if (!GovernableSettlements().Contains(settlement)) return Result.No("You have no other settlement of your own to govern yet.");
            }
            else if (settlement != CapitalId) return Result.No($"The {post} serves in the capital.");
            return Result.Yes();
        }

        Result CheckPerson(Character c)
        {
            if (c == null) return Result.No("No such person.");
            if (!c.Alive) return Result.No($"{c.Name} is dead.");
            if (c.Employment == Employment.Retired) return Result.No($"{c.Name} has retired.");
            if (c.Employment == Employment.Government || c.Post != Post.None) return Result.No($"{c.Name} already holds the post of {c.Post}: one major appointment each.");
            return Result.Yes();
        }

        public Result CanAppoint(int charId, Post post, int settlement = CapitalId)
        {
            var c = Characters.Get(charId);
            var r = CheckPerson(c); if (!r.Ok) return r;
            r = CheckPost(post, settlement); if (!r.Ok) return r;
            if (PostHolder(post, settlement) is Character h) return Result.No($"{h.Name} is your {post}: dismiss or replace them first.");
            return Result.Yes();
        }

        void Install(Character c, Post post, int settlement)
        {
            c.Employment = Employment.Government; c.Post = post; c.PostSettlement = settlement; c.AppointedDay = Day;
            c.Settlement = settlement; c.Civilization = 0; c.MissedPayments = 0;
            c.MonthlySalary = SalaryOf(c, post, c.Pay, settlement);
            c.Log(Day, $"Appointed {post}");
            Events.Add($"{c.Name} is now your {post} (pay {c.MonthlySalary} gold a month).");
        }

        public Result Appoint(int charId, Post post, int settlement = CapitalId)
        {
            var r = CanAppoint(charId, post, settlement); if (!r.Ok) return r;
            Install(Characters.Get(charId), post, settlement);
            return Result.Yes();
        }

        public Result Dismiss(int charId)
        {
            var c = Characters.Get(charId);
            if (c == null || c.Post == Post.None || !c.Alive) return Result.No("That person holds no post.");
            var post = c.Post;
            c.Post = Post.None; c.PostSettlement = 0; c.Employment = Employment.Available; c.MissedPayments = 0;
            c.Log(Day, $"Dismissed as {post}");
            Events.Add($"{c.Name} was dismissed as {post}. They stay in the settlement.");
            return Result.Yes();
        }

        /// <summary>Put a new person in a post that is held: the old holder is dismissed. Everything is checked first, so a refused replacement changes nothing.</summary>
        public Result Replace(int newId, Post post, int settlement = CapitalId)
        {
            var c = Characters.Get(newId);
            var r = CheckPerson(c); if (!r.Ok) return r;
            r = CheckPost(post, settlement); if (!r.Ok) return r;
            var old = PostHolder(post, settlement);
            if (old != null) Dismiss(old.Id);
            Install(c, post, settlement);
            return Result.Yes();
        }

        /// <summary>Move an official to a different, vacant post (a promotion or a change of duty). Their old post becomes vacant.</summary>
        public Result Promote(int charId, Post newPost, int settlement = CapitalId)
        {
            var c = Characters.Get(charId);
            if (c == null || c.Post == Post.None || !c.Alive) return Result.No("Only someone who holds a post can be moved to another.");
            if (c.Post == newPost && c.PostSettlement == settlement) return Result.No($"{c.Name} already holds that post.");
            var r = CheckPost(newPost, settlement); if (!r.Ok) return r;
            if (PostHolder(newPost, settlement) is Character h) return Result.No($"{h.Name} is your {newPost}: dismiss or replace them first.");
            var old = c.Post;
            c.Post = newPost; c.PostSettlement = settlement; c.Settlement = settlement; c.AppointedDay = Day; c.MissedPayments = 0;
            c.MonthlySalary = SalaryOf(c, newPost, c.Pay, settlement);
            c.Log(Day, $"Moved from {old} to {newPost}");
            Events.Add($"{c.Name} moves from {old} to {newPost} (pay {c.MonthlySalary} gold a month).");
            return Result.Yes();
        }

        public Result SetPay(int charId, PayLevel level)
        {
            var c = Characters.Get(charId);
            if (c == null || c.Post == Post.None || !c.Alive) return Result.No("That person holds no post.");
            c.Pay = level; c.MonthlySalary = SalaryOf(c, c.Post, level, c.PostSettlement);
            return Result.Yes();
        }

        // ---------- candidates ----------

        public CandidateSource SourceOf(Character c) => c.Civilization != 0 ? CandidateSource.International : c.Settlement == CapitalId ? CandidateSource.Local : CandidateSource.OtherSettlement;

        /// <summary>Everyone who could be appointed, rated for one post, filtered and sorted. Reads only; nothing is generated or rerolled here.</summary>
        public List<Candidate> FindCandidates(Post post, CandidateFilter f = null, CandidateSort sort = CandidateSort.Suitability, bool descending = true)
        {
            f ??= new CandidateFilter();
            var list = new List<Candidate>();
            foreach (var c in Characters.All)
            {
                if (!c.Alive || c.Post != Post.None || (c.Employment != Employment.Available && c.Employment != Employment.Civilian)) continue;
                var src = SourceOf(c);
                if (f.Source.HasValue && f.Source != src) continue;
                if (f.Settlement.HasValue && c.Settlement != f.Settlement) continue;
                int age = c.AgeYears(Day);
                if (age < f.MinAge || age > f.MaxAge) continue;
                if (c.Loyalty < f.MinLoyalty || c.Experience < f.MinExperience) continue;
                if (!string.IsNullOrEmpty(f.Occupation) && c.Occupation.IndexOf(f.Occupation, StringComparison.OrdinalIgnoreCase) < 0 && c.Background.ToString().IndexOf(f.Occupation, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool ok = true;
                for (int i = 0; i < 5; i++) if (c.Effective((Attr)i) < f.MinAttr[i]) ok = false;
                if (!ok) continue;
                int salary = SalaryOf(c, post);
                if (salary > f.MaxSalary) continue;
                list.Add(new Candidate { C = c, Source = src, Suitability = Positions.Suitability(c, post), Salary = salary });
            }
            Func<Candidate, IComparable> key = sort switch
            {
                CandidateSort.Age => x => x.C.AgeYears(Day),
                CandidateSort.Loyalty => x => x.C.Loyalty,
                CandidateSort.Experience => x => x.C.ExperienceHundredths,
                CandidateSort.Salary => x => x.Salary,
                CandidateSort.Name => x => x.C.Name,
                CandidateSort.Administration => x => x.C.Effective(Attr.Administration),
                CandidateSort.Economics => x => x.C.Effective(Attr.Economics),
                CandidateSort.Military => x => x.C.Effective(Attr.Military),
                CandidateSort.Stewardship => x => x.C.Effective(Attr.Stewardship),
                CandidateSort.Diplomacy => x => x.C.Effective(Attr.Diplomacy),
                _ => x => x.Suitability,
            };
            var ordered = descending ? list.OrderByDescending(key).ThenBy(x => x.C.Id) : list.OrderBy(key).ThenBy(x => x.C.Id);
            return ordered.ToList();
        }

        // ---------- international recruitment ----------

        public Result CanSearchAbroad(Post post, int settlementId)
        {
            if (Positions.LockedReason(post, Era) is string locked) return Result.No(locked);
            var s = SettlementById(settlementId);
            if (s == null) return Result.No("Pick a settlement to send the recruiter to.");
            if (!s.WillTrade) return Result.No($"{s.Name} will not receive your recruiter: they are not on speaking terms (attitude {s.Attitude}).");
            if (Searches.Any(x => x.Post == post)) return Result.No($"A recruiter is already looking for a {post}.");
            if (_searchCooldown.TryGetValue(post, out int until) && Day < until) return Result.No($"You searched for a {post} lately: wait {until - Day} more days.");
            if (ResAmt[Res.Gold] < ForeignSearchGold) return Result.No($"A search abroad costs {ForeignSearchGold} gold; you have {ResAmt[Res.Gold]:0}.");
            return Result.Yes();
        }

        public int SearchDays(int settlementId) => ForeignBaseDays + 2 * SettlementMarchDays(SettlementById(settlementId));

        public Result SearchAbroad(Post post, int settlementId)
        {
            var r = CanSearchAbroad(post, settlementId); if (!r.Ok) return r;
            ResAmt[Res.Gold] -= ForeignSearchGold;
            Searches.Add(new ForeignSearch { Post = post, Settlement = settlementId, ReadyDay = Day + SearchDays(settlementId), Serial = _searchSerial++ });
            Events.Add($"A recruiter leaves to look for a {post} in {SettlementById(settlementId).Name} (back in {SearchDays(settlementId)} days, {ForeignSearchGold} gold spent).");
            return Result.Yes();
        }

        /// <summary>The recruiter tries a few people and brings back the best for the post. Deterministic from the world seed and the search's number.</summary>
        void FinishSearch(ForeignSearch s)
        {
            var home = SettlementById(s.Settlement);
            var ctx = new PoolContext { SettlementId = s.Settlement, Population = (int)(home?.Population ?? 100), Seed = Seed, Era = Era };
            double[] w = { 5, 10, 18, 12, 4, 20, 12 };
            Array.Copy(w, ctx.Weights, 7);
            Character best = null; int bestScore = -1;
            for (int i = 0; i < ForeignCandidatesTried; i++)
            {
                var c = CharacterGenerator.Create(0, ctx, 1_000_000L + s.Serial * ForeignCandidatesTried + i, Day);
                int score = Positions.Suitability(c, s.Post);
                if (score > bestScore) { best = c; bestScore = score; }
            }
            best.Id = Characters.NextId;
            best.Civilization = 1 + s.Settlement; best.Employment = Employment.Available;
            best.Occupation = best.Occupation + " from " + (home?.Name ?? "abroad");
            best.Log(Day, $"Found by your recruiter for the post of {s.Post}");
            Characters.Add(best);
            _searchCooldown[s.Post] = Day + ForeignSearchCooldown;
            Events.Add($"Your recruiter is back from {home?.Name ?? "abroad"} with {best.Name}, a possible {s.Post} (suitability {bestScore}).");
        }

        // ---------- the monthly payday and the daily step ----------

        public void PayOfficials()
        {
            double due = 0, paid = 0;
            foreach (var c in Officials.OrderBy(o => o.Id).ToList())
            {
                int pay = SalaryOf(c, c.Post, c.Pay, c.PostSettlement);
                c.MonthlySalary = pay; due += pay;
                if (TreasuryFor(c.PostSettlement) >= pay)
                {
                    ResAmt[Res.Gold] -= pay; paid += pay; c.MissedPayments = 0;
                    if (c.Pay == PayLevel.Low) c.Loyalty = Math.Max(0, c.Loyalty - Pay.LoyaltyDrift);
                    else if (c.Pay == PayLevel.Generous) c.Loyalty = Math.Min(100, c.Loyalty + Pay.LoyaltyDrift);
                }
                else
                {
                    c.MissedPayments++; c.Loyalty = Math.Max(0, c.Loyalty - Pay.MissedLoyalty);
                    Events.Add($"The treasury could not pay {c.Name} ({pay} gold). {c.MissedPayments} month{(c.MissedPayments == 1 ? "" : "s")} unpaid.");
                }
                if (c.MissedPayments >= Pay.ResignAfterMisses) { Characters.Resign(c, Day, "never paid"); Events.Add(Characters.Journal.Last().Text); }
                else if (c.Loyalty <= 0) { Characters.Resign(c, Day, "lost all loyalty"); Events.Add(Characters.Journal.Last().Text); }
            }
            LastPayrollDue = due; LastPayrollPaid = paid; LastPayday = Day;
        }

        void StepGovernment()
        {
            foreach (var s in Searches.Where(x => Day >= x.ReadyDay).OrderBy(x => x.Serial).ToList()) { Searches.Remove(s); FinishSearch(s); }
        }

        JObject SaveGovernment() => new JObject
        {
            ["searches"] = new JArray(Searches.Select(s => new JArray((int)s.Post, s.Settlement, s.ReadyDay, s.Serial))),
            ["serial"] = _searchSerial,
            ["cooldown"] = new JArray(_searchCooldown.OrderBy(k => (int)k.Key).Select(k => new JArray((int)k.Key, k.Value))),
            ["due"] = Math.Round(LastPayrollDue, 2), ["paid"] = Math.Round(LastPayrollPaid, 2), ["payday"] = LastPayday,
        };

        void LoadGovernment(JObject o)
        {
            Searches.Clear(); _searchCooldown.Clear(); _searchSerial = 0; LastPayrollDue = LastPayrollPaid = 0; LastPayday = 0;
            if (o == null) return;
            foreach (JArray s in o["searches"] as JArray ?? new JArray()) Searches.Add(new ForeignSearch { Post = (Post)s[0].Value<int>(), Settlement = s[1].Value<int>(), ReadyDay = s[2].Value<int>(), Serial = s[3].Value<int>() });
            _searchSerial = o.Value<int?>("serial") ?? 0;
            foreach (JArray k in o["cooldown"] as JArray ?? new JArray()) _searchCooldown[(Post)k[0].Value<int>()] = k[1].Value<int>();
            LastPayrollDue = o.Value<double?>("due") ?? 0; LastPayrollPaid = o.Value<double?>("paid") ?? 0; LastPayday = o.Value<int?>("payday") ?? 0;
        }
    }
}
