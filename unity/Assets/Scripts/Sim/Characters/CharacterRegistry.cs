// Every notable person in the civilization, with stable ids, per-settlement pools, the monthly aging step and the save format.
// Deterministic: all dice come from CharRng (hashes of seed, settlement, serial, id and day), never from System.Random or the world's own streams.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public sealed class CharacterRegistry
    {
        public const int SaveVersion = 1, JournalMax = 200;
        public int NextId = 1;
        readonly Dictionary<int, Character> _byId = new Dictionary<int, Character>();
        readonly List<Character> _all = new List<Character>();                  // in id order (ids only ever grow)
        readonly Dictionary<int, int> _serial = new Dictionary<int, int>();    // settlement id -> people generated so far (the generator's stream position)
        public readonly List<CharEvent> Journal = new List<CharEvent>();       // the newest events, capped

        public IReadOnlyList<Character> All => _all;
        public int Count => _all.Count;
        public Character Get(int id) => _byId.TryGetValue(id, out var c) ? c : null;

        /// <summary>The people of a settlement who are alive and in the world (not dead).</summary>
        public IEnumerable<Character> InSettlement(int settlement) => _all.Where(c => c.Alive && c.Settlement == settlement);
        /// <summary>Notables that count towards a settlement's pool: living and not retired.</summary>
        public int PoolCount(int settlement) => _all.Count(c => c.Alive && c.Settlement == settlement && c.Employment != Employment.Retired);

        public Character Add(Character c)
        {
            _byId[c.Id] = c; _all.Add(c);
            if (c.Id >= NextId) NextId = c.Id + 1;
            return c;
        }

        void Note(int day, Character c, CharEventKind kind, string text)
        {
            Journal.Add(new CharEvent { Day = day, Id = c.Id, Kind = kind, Post = c.Post, Text = text });
            if (Journal.Count > JournalMax) Journal.RemoveRange(0, Journal.Count - JournalMax);
        }

        Character Make(PoolContext ctx, int day, Background? bg = null)
        {
            _serial.TryGetValue(ctx.SettlementId, out int serial);
            _serial[ctx.SettlementId] = serial + 1;
            return Add(CharacterGenerator.Create(NextId, ctx, serial, day, bg));
        }

        /// <summary>The first people of a new settlement: 3 to 5, scaled to its size.</summary>
        public void CreateFounders(PoolContext ctx, int day)
        {
            int n = Math.Clamp((int)Math.Round(CharacterBalance.TargetNotables(ctx.Population)), CharacterBalance.FoundersMin, CharacterBalance.FoundersMax);
            for (int i = 0; i < n; i++) Make(ctx, day);
        }

        /// <summary>Migration of an older save, or a conquered place: bring the pool straight up to its target for its size.</summary>
        public void FillToTarget(PoolContext ctx, int day)
        {
            int target = (int)Math.Round(CharacterBalance.TargetNotables(ctx.Population));
            for (int have = PoolCount(ctx.SettlementId); have < Math.Max(target, CharacterBalance.FoundersMin); have++) Make(ctx, day);
        }

        public bool HasPool(int settlement) => _serial.ContainsKey(settlement);

        // ---------- the monthly step ----------

        /// <summary>Once a month: everyone ages (stage changes, health, experience, retirement, death), then each pool below its target gains people.</summary>
        public void StepMonth(int day, IEnumerable<PoolContext> pools)
        {
            foreach (var c in _all.ToList())   // (a copy: the list does not change here, but Journal callers may)
                if (c.Alive) AgeOne(c, day);
            foreach (var ctx in pools) Grow(ctx, day);
        }

        void AgeOne(Character c, int day)
        {
            var rng = new CharRng(c.Id, day, 17);
            int age = c.AgeYears(day);
            var stage = c.StageAt(day);
            if (stage != c.Stage)
            {
                c.Stage = stage;
                c.Log(day, $"Became {CharacterBalance.StageNames[(int)stage]}");
                Note(day, c, CharEventKind.StageChanged, $"{c.Name} is now {CharacterBalance.StageNames[(int)stage].ToLower()}.");
            }

            // health fades with age (a fraction of a point a month, rounded without bias)
            if (age > 30) c.Health = Math.Max(0, c.Health - rng.Stochastic((age - 30) * 0.2 / 12.0));

            // death: a yearly risk by age, worse in poor health
            double yearly = 0;
            foreach (var (a, r) in CharacterBalance.DeathRisk) if (age >= a) yearly = r;
            if (age >= 75) yearly = Math.Min(0.45, yearly + (age - 75) * 0.02);
            if (c.Health < 50) yearly *= 1 + (50 - c.Health) / 25.0;
            double monthly = 1 - Math.Pow(1 - Math.Min(0.95, yearly), 1.0 / 12.0);
            if (c.Health <= 0 || rng.Chance(monthly)) { Die(c, day); return; }

            // retirement
            if (age >= CharacterBalance.RetireFrom && c.Employment != Employment.Retired &&
                rng.Chance(age < 70 ? CharacterBalance.RetireChanceBefore70 : CharacterBalance.RetireChanceAfter70)) { Retire(c, day); return; }

            // experience: people at work learn, slower the more they already know
            double gain = c.Employment == Employment.Government ? CharacterBalance.ExperienceGovernment
                : c.Employment == Employment.Civilian ? CharacterBalance.ExperienceBase
                : c.Employment == Employment.Available ? CharacterBalance.ExperienceAvailable : 0;
            if (gain > 0)
            {
                double slow = Math.Pow(1 - c.ExperienceHundredths / 10000.0, 1.5);
                c.ExperienceHundredths = Math.Min(10000, c.ExperienceHundredths + rng.Stochastic(gain * 100 * slow * c.ExperienceTraitFactor()));
            }
        }

        public void Retire(Character c, int day)
        {
            if (!c.Alive || c.Employment == Employment.Retired) return;
            var post = c.Post;
            c.Employment = Employment.Retired; c.RetiredDay = day;
            c.Log(day, post == Post.None ? "Retired" : $"Retired from the post of {post}");
            Note(day, c, CharEventKind.Retired, post == Post.None ? $"{c.Name} has retired." : $"{c.Name} has retired: the post of {post} is vacant.");
            c.Post = Post.None; c.PostSettlement = 0;
        }

        public void Die(Character c, int day)
        {
            if (!c.Alive) return;
            var post = c.Post;
            c.Alive = false; c.Employment = Employment.Deceased; c.DeathDay = day;
            c.Log(day, post == Post.None ? "Died" : $"Died in office as {post}");
            Note(day, c, CharEventKind.Died, post == Post.None ? $"{c.Name} has died." : $"{c.Name} has died: the post of {post} is vacant.");
            c.Post = Post.None; c.PostSettlement = 0;
        }

        void Grow(PoolContext ctx, int day)
        {
            int deficit = (int)Math.Round(CharacterBalance.TargetNotables(ctx.Population)) - PoolCount(ctx.SettlementId);
            if (deficit <= 0) return;
            int n = deficit >= 15 ? 3 : deficit >= 6 ? 2 : 1;   // far below the target: a faster trickle, never a flood
            for (int i = 0; i < n; i++)
            {
                var c = Make(ctx, day);
                c.Log(day, "Came to notice");
                Note(day, c, CharEventKind.Arrived, $"{c.Name}, a {c.Occupation.ToLower()}, has come to notice in the settlement.");
            }
        }

        // ---------- saving ----------
        // Row layout (fixed order, append new fields at the end): id, name, birth, home, here, civ, background, occupation, alive, employment,
        // [5 attributes], loyalty, ambition, health, experience, [traits], post, postSettlement, salary, payLevel,
        // [look: seed, climate, archetype, outfit, grey, recede, bald], stage, deathDay, retiredDay, [history: [day, text]...]

        public JObject Save()
        {
            var rows = new JArray();
            foreach (var c in _all)
                rows.Add(new JArray(c.Id, c.Name, c.BirthDay, c.HomeSettlement, c.Settlement, c.Civilization, (int)c.Background, c.Occupation, c.Alive, (int)c.Employment,
                    new JArray(c.Attr), c.Loyalty, c.Ambition, c.Health, c.ExperienceHundredths, new JArray(c.Traits.Select(t => (int)t)),
                    (int)c.Post, c.PostSettlement, c.MonthlySalary, (int)c.Pay,
                    new JArray(c.Look.Seed, c.Look.Climate, c.Look.Archetype, c.Look.Outfit, c.Look.GreyAge, c.Look.RecedeAge, c.Look.BaldAge),
                    (int)c.Stage, c.DeathDay, c.RetiredDay, new JArray(c.History.Select(h => new JArray(h.day, h.text)))));
            return new JObject
            {
                ["v"] = SaveVersion, ["next"] = NextId,
                ["serial"] = new JArray(_serial.OrderBy(k => k.Key).Select(k => new JArray(k.Key, k.Value))),
                ["rows"] = rows,
                ["journal"] = new JArray(Journal.Select(e => new JArray(e.Day, e.Id, (int)e.Kind, (int)e.Post, e.Text))),
            };
        }

        public static CharacterRegistry Load(JObject o)
        {
            var reg = new CharacterRegistry();
            if (o == null) return reg;
            reg.NextId = o.Value<int?>("next") ?? 1;
            foreach (var p in (o["serial"] as JArray) ?? new JArray()) reg._serial[p[0].Value<int>()] = p[1].Value<int>();
            foreach (JArray r in (o["rows"] as JArray) ?? new JArray())
            {
                var c = new Character
                {
                    Id = r[0].Value<int>(), Name = r[1].Value<string>(), BirthDay = r[2].Value<int>(), HomeSettlement = r[3].Value<int>(), Settlement = r[4].Value<int>(),
                    Civilization = r[5].Value<int>(), Background = (Background)r[6].Value<int>(), Occupation = r[7].Value<string>(), Alive = r[8].Value<bool>(),
                    Employment = (Employment)r[9].Value<int>(),
                };
                var attr = (JArray)r[10]; for (int i = 0; i < 5; i++) c.Attr[i] = attr[i].Value<int>();
                c.Loyalty = r[11].Value<int>(); c.Ambition = r[12].Value<int>(); c.Health = r[13].Value<int>(); c.ExperienceHundredths = r[14].Value<int>();
                foreach (var t in (JArray)r[15]) c.Traits.Add((Trait)t.Value<int>());
                c.Post = (Post)r[16].Value<int>(); c.PostSettlement = r[17].Value<int>(); c.MonthlySalary = r[18].Value<int>(); c.Pay = (PayLevel)r[19].Value<int>();
                var look = (JArray)r[20];
                c.Look = new CharAppearance { Seed = look[0].Value<int>(), Climate = look[1].Value<string>(), Archetype = look[2].Value<string>(), Outfit = look[3].Value<string>(),
                    GreyAge = look[4].Value<int>(), RecedeAge = look[5].Value<int>(), BaldAge = look[6].Value<int>() };
                c.Stage = (AgeStage)r[21].Value<int>(); c.DeathDay = r[22].Value<int>(); c.RetiredDay = r[23].Value<int>();
                foreach (JArray h in (JArray)r[24]) c.History.Add((h[0].Value<int>(), h[1].Value<string>()));
                reg.Add(c);
            }
            foreach (JArray e in (o["journal"] as JArray) ?? new JArray())
                reg.Journal.Add(new CharEvent { Day = e[0].Value<int>(), Id = e[1].Value<int>(), Kind = (CharEventKind)e[2].Value<int>(), Post = (Post)e[3].Value<int>(), Text = e[4].Value<string>() });
            return reg;
        }
    }
}
