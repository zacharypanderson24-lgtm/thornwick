// The major appointments: which attributes each one needs (no universal rating), when each unlocks, and the plain-language reasons
// behind a suitability score. Suitability is only ever computed for one post at a time.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public sealed class PostDef
    {
        public Post Id; public string Title, Duty;
        public (Attr attr, double weight)[] Needs;    // weights add up to 1; the first is the main skill
        public int MinEra;                            // council posts unlock by era (Governor: needs a second settlement instead)
        public bool Council;
        public double BaseSalary;                     // gold a month before skill, experience, size, traits and pay level
        public Attr Main => Needs[0].attr;
    }

    public static class Positions
    {
        public static readonly PostDef[] All =
        {
            new PostDef { Id = Post.Governor, Title = "Governor", Duty = "Runs a settlement of yours for you, following your directives.", MinEra = 1, BaseSalary = 8,
                Needs = new[] { (Attr.Administration, 0.35), (Attr.Stewardship, 0.30), (Attr.Diplomacy, 0.20), (Attr.Economics, 0.15) } },
            new PostDef { Id = Post.Chancellor, Title = "Chancellor", Duty = "Runs the government and reports on how well the settlements are administered.", MinEra = 2, Council = true, BaseSalary = 10,
                Needs = new[] { (Attr.Administration, 0.60), (Attr.Diplomacy, 0.20), (Attr.Stewardship, 0.20) } },
            new PostDef { Id = Post.Steward, Title = "Steward", Duty = "Looks after food, housing and the wellbeing of the people.", MinEra = 2, Council = true, BaseSalary = 10,
                Needs = new[] { (Attr.Stewardship, 0.60), (Attr.Economics, 0.20), (Attr.Administration, 0.20) } },
            new PostDef { Id = Post.Treasurer, Title = "Treasurer", Duty = "Watches the treasury, wages and trade.", MinEra = 3, Council = true, BaseSalary = 10,
                Needs = new[] { (Attr.Economics, 0.60), (Attr.Administration, 0.25), (Attr.Stewardship, 0.15) } },
            new PostDef { Id = Post.Marshal, Title = "Marshal", Duty = "Commands the soldiers and advises on defence.", MinEra = 3, Council = true, BaseSalary = 10,
                Needs = new[] { (Attr.Military, 0.60), (Attr.Administration, 0.20), (Attr.Stewardship, 0.20) } },
            new PostDef { Id = Post.Diplomat, Title = "Diplomat", Duty = "Handles other peoples: trade partners, tribute and treaties.", MinEra = 4, Council = true, BaseSalary = 10,
                Needs = new[] { (Attr.Diplomacy, 0.60), (Attr.Administration, 0.20), (Attr.Economics, 0.20) } },
        };

        public static PostDef Get(Post p) => All.First(d => d.Id == p);
        public static IEnumerable<PostDef> Council => All.Where(d => d.Council);

        /// <summary>0-100: the post's weighted attributes (traits included), nudged by up to 8 points for experience.</summary>
        public static int Suitability(Character c, Post post)
        {
            var d = Get(post);
            double v = d.Needs.Sum(n => c.Effective(n.attr) * n.weight);
            v += (c.Experience - 30) * 0.12;   // 30 experience is neutral; 100 gives about +8, 0 about -4
            return Math.Clamp((int)Math.Round(v), 0, 100);
        }

        public static string Rating(int suitability) =>
            suitability >= 80 ? "Exceptional" : suitability >= 65 ? "Excellent" : suitability >= 50 ? "Good" : suitability >= 35 ? "Fair" : "Poor";

        /// <summary>Plain-language reasons for the score: what helps, what hurts.</summary>
        public static List<string> Reasons(Character c, Post post, int day)
        {
            var d = Get(post); var r = new List<string>();
            foreach (var (a, w) in d.Needs)
            {
                int v = c.Effective(a);
                bool main = w >= 0.5;
                if (v >= 65) r.Add($"Strong {a} ({v}){(main ? $": the {d.Title}'s main skill" : "")}");
                else if (v <= 35) r.Add($"Weak {a} ({v}){(main ? $": the {d.Title}'s main skill" : "")}");
            }
            foreach (var t in c.Traits)
            {
                var td = Traits.Get(t);
                foreach (var (a, w) in d.Needs)
                {
                    int m = td.Attr[(int)a];
                    if (m != 0) r.Add($"{td.Name}: {(m > 0 ? "+" : "")}{m} {a}");
                }
            }
            if (c.Experience >= 50) r.Add($"Experienced ({c.Experience:0})");
            else if (c.Experience < 15) r.Add($"Little experience ({c.Experience:0})");
            if (c.Loyalty <= 30) r.Add($"Low loyalty ({c.Loyalty})");
            else if (c.Loyalty >= 70) r.Add($"Loyal ({c.Loyalty})");
            var stage = c.StageAt(day);
            if (stage >= AgeStage.Elderly) r.Add($"{CharacterBalance.StageNames[(int)stage]}: may retire or die soon");
            if (c.Health < 40) r.Add($"In poor health ({c.Health})");
            if (r.Count == 0) r.Add("Ordinary skills for this post");
            return r;
        }

        /// <summary>Why the post cannot be filled yet, or null.</summary>
        public static string LockedReason(Post post, int era)
        {
            var d = Get(post);
            return post == Post.Governor ? null : era >= d.MinEra ? null : $"The {d.Title} appears in Era {ToRoman(d.MinEra)}; you are in Era {ToRoman(era)}.";
        }

        public static string ToRoman(int n) => new[] { "0", "I", "II", "III", "IV", "V" }[Math.Clamp(n, 0, 5)];
    }

    /// <summary>Monthly pay: skill, experience, post, settlement size, traits and the chosen level. Paid in gold from the treasury; never creates money.</summary>
    public static class Pay
    {
        public static readonly double[] LevelFactor = { 0.8, 1.0, 1.35 };
        public const int LoyaltyDrift = 1;           // a month on Low pay costs this much loyalty, a month on Generous gains it
        public const int MissedLoyalty = 6;          // loyalty lost for each unpaid month
        public const int ResignAfterMisses = 3;

        public static int Monthly(Character c, Post post, int population, PayLevel level)
        {
            var d = Positions.Get(post);
            double skill = 0.6 + 0.8 * Positions.Suitability(c, post) / 100.0;
            double experience = 1 + c.Experience / 100.0 * 0.4;
            double size = 1 + Math.Log10(Math.Max(50, population) / 50.0) * 0.35;
            double v = d.BaseSalary * skill * experience * size * c.SalaryTraitFactor() * LevelFactor[(int)level];
            return Math.Max(1, (int)Math.Round(v));
        }
    }
}
