// Character definitions: the five attributes, the sixteen traits (data table), employment, age stages and the balance numbers.
// Everything a designer might tune is in this file; the generator and the monthly step contain no magic numbers of their own.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    /// <summary>The only five skills. Index into Character.Attr.</summary>
    public enum Attr { Administration, Economics, Military, Stewardship, Diplomacy }

    public enum Trait
    {
        Industrious, Ambitious, Loyal, Greedy, Frugal, Compassionate, Ruthless, Brave,
        Cautious, Scholarly, Traditionalist, Innovative, Honest, Deceitful, Patient, Extravagant,
    }

    public enum Employment { Available, Civilian, Government, Retired, Deceased }

    /// <summary>Where a notable came from; it leans the attributes (never decides them) and picks the portrait archetype.</summary>
    public enum Background { Local, Farmer, Merchant, Craftsman, Veteran, Educated, Foreign }

    /// <summary>Age stages, tied to the calendar. Ids match the portrait library's age stages (CharacterBalance.StageIds).</summary>
    public enum AgeStage { Young, Adult, MiddleAged, Elderly, Venerable }

    /// <summary>A major appointment. Phase 2 fills these in; Phase 1 only reserves the field. One per person.</summary>
    public enum Post { None, Governor, Chancellor, Treasurer, Marshal, Steward, Diplomat }

    public enum PayLevel { Low, Standard, Generous }

    public sealed class TraitDef
    {
        public Trait Id; public string Name, Text;
        public int[] Attr = new int[5];       // modifier to each attribute (Attr order)
        public int Loyalty, Ambition;         // modifier to starting loyalty / ambition
        public int SalaryPct, ExperiencePct;  // +x percent salary, +x percent experience gained
        public Trait? Opposes;                // cannot be given together
    }

    public static class Traits
    {
        static TraitDef T(Trait id, string name, string text, int adm = 0, int eco = 0, int mil = 0, int stw = 0, int dip = 0, int loy = 0, int amb = 0, int sal = 0, int exp = 0, Trait? opp = null)
            => new TraitDef { Id = id, Name = name, Text = text, Attr = new[] { adm, eco, mil, stw, dip }, Loyalty = loy, Ambition = amb, SalaryPct = sal, ExperiencePct = exp, Opposes = opp };

        public static readonly TraitDef[] All =
        {
            T(Trait.Industrious, "Industrious", "Works hard: gains experience 25% faster.", exp: 25),
            T(Trait.Ambitious, "Ambitious", "Wants a higher post: much more ambition, a little less loyalty.", amb: 20, loy: -5),
            T(Trait.Loyal, "Loyal", "Stays true to you: starts with more loyalty.", loy: 15, opp: Trait.Deceitful),
            T(Trait.Greedy, "Greedy", "Expects better pay: salary 15% higher.", sal: 15, opp: Trait.Frugal),
            T(Trait.Frugal, "Frugal", "Content with less: salary 10% lower.", sal: -10, opp: Trait.Extravagant),
            T(Trait.Compassionate, "Compassionate", "Looks after people: better Stewardship and Diplomacy.", stw: 3, dip: 2, opp: Trait.Ruthless),
            T(Trait.Ruthless, "Ruthless", "Hard on others: better Military, worse Diplomacy.", mil: 4, dip: -4, opp: Trait.Compassionate),
            T(Trait.Brave, "Brave", "Bold in a fight: better Military.", mil: 4, opp: Trait.Cautious),
            T(Trait.Cautious, "Cautious", "Careful with risks: better Stewardship, worse Military.", stw: 3, mil: -2, opp: Trait.Brave),
            T(Trait.Scholarly, "Scholarly", "Well read: better Administration, learns 10% faster.", adm: 4, exp: 10),
            T(Trait.Traditionalist, "Traditionalist", "Trusts the old ways: better Stewardship, worse Economics.", stw: 3, eco: -2, opp: Trait.Innovative),
            T(Trait.Innovative, "Innovative", "Tries new methods: better Economics, worse Stewardship.", eco: 4, stw: -2, opp: Trait.Traditionalist),
            T(Trait.Honest, "Honest", "Plain dealer: a little more loyalty and Diplomacy.", loy: 5, dip: 2, opp: Trait.Deceitful),
            T(Trait.Deceitful, "Deceitful", "Smooth liar: better Diplomacy, much less loyalty.", dip: 4, loy: -8),
            T(Trait.Patient, "Patient", "Takes the long view: better Diplomacy and Stewardship.", dip: 2, stw: 2),
            T(Trait.Extravagant, "Extravagant", "Lives well: salary 15% higher, worse Economics.", eco: -3, sal: 15, opp: Trait.Frugal),
        };
        public static TraitDef Get(Trait t) => All[(int)t];
    }

    /// <summary>All the numbers behind generation and aging, in one place.</summary>
    public static class CharacterBalance
    {
        public const int YearLength = 360;          // = World.YearLength
        public const int Month = 30;                // characters step once a month (= World.SettlementMonth)
        public const int AdultAge = 18;
        public static readonly int[] StageStart = { 18, 30, 45, 60, 75 };
        public static readonly string[] StageIds = { "young", "adult", "middle_aged", "elderly", "venerable" };
        public static readonly string[] StageNames = { "Young", "Adult", "Middle-aged", "Elderly", "Venerable" };

        // how many notable citizens a settlement of this size should have (piecewise linear; growth diminishes)
        public static readonly (int pop, double count)[] NotableTargets =
            { (0, 2), (50, 3), (200, 6), (500, 11.5), (1000, 20), (5000, 52), (10000, 82), (50000, 200) };

        public const int FoundersMin = 3, FoundersMax = 5;

        // attribute shape: a bell curve (mean of four dice) around AttrMean with spread AttrSpread; the background lifts two attributes
        public const int AttrMean = 38, AttrSpread = 97, AttrMin = 5, AttrMax = 95;
        public const double ExceptionalChance = 0.04;   // one attribute is exceptional (82 to 95)
        public const int ExceptionalFloor = 82;

        // monthly aging
        public const int RetireFrom = 62;               // first age a person may retire
        public const double RetireChanceBefore70 = 0.03, RetireChanceAfter70 = 0.08;
        public static readonly (int age, double yearly)[] DeathRisk = { (18, 0.003), (45, 0.010), (60, 0.040), (75, 0.12) };
        public const double ExperienceBase = 0.35, ExperienceGovernment = 0.60, ExperienceAvailable = 0.10;   // per month, before the slow-down at high values

        public static double TargetNotables(double population)
        {
            var t = NotableTargets;
            if (population <= t[0].pop) return t[0].count;
            for (int i = 1; i < t.Length; i++)
                if (population <= t[i].pop) return t[i - 1].count + (t[i].count - t[i - 1].count) * (population - t[i - 1].pop) / (t[i].pop - t[i - 1].pop);
            return t[t.Length - 1].count;
        }
        public static AgeStage StageOfAge(int years)
        {
            AgeStage s = AgeStage.Young;
            for (int i = 0; i < StageStart.Length; i++) if (years >= StageStart[i]) s = (AgeStage)i;
            return s;
        }
    }

    /// <summary>Small deterministic generator (splitmix64) so the same inputs give the same people on every machine; never System.Random.</summary>
    public struct CharRng
    {
        ulong _s;
        public CharRng(long a, long b = 0, long c = 0)
        {
            unchecked
            {
                _s = (ulong)a * 0x9E3779B97F4A7C15UL ^ ((ulong)b + 0x632BE59BD9B4E019UL) * 0xBF58476D1CE4E5B9UL ^ ((ulong)c + 0x1234567UL) * 0x94D049BB133111EBUL;
            }
            Next(); Next();
        }
        public ulong Next()
        {
            unchecked
            {
                ulong z = (_s += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
        public int Int(int n) => n <= 1 ? 0 : (int)(Next() % (ulong)n);
        public int Range(int lo, int hi) => lo + Int(hi - lo + 1);   // inclusive
        public double Double() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        public bool Chance(double p) => Double() < p;
        /// <summary>Mean of four dice, 0..1, bell-shaped around 0.5.</summary>
        public double Bell() => (Double() + Double() + Double() + Double()) / 4.0;
        /// <summary>Round a fractional amount to a whole number without bias: 2.3 gives 2 seventy percent of the time and 3 thirty percent.</summary>
        public int Stochastic(double v) { int f = (int)Math.Floor(v); return f + (Chance(v - f) ? 1 : 0); }
        public int Weighted(IList<double> w)
        {
            double total = 0; foreach (var x in w) total += x > 0 ? x : 0;
            if (total <= 0) return Int(w.Count);
            double r = Double() * total;
            for (int i = 0; i < w.Count; i++) { r -= w[i] > 0 ? w[i] : 0; if (r < 0) return i; }
            return w.Count - 1;
        }
    }
}
