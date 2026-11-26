// One notable person. Plain data, saved by CharacterRegistry. See DESIGN_GOVERNMENT.md section 3.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    /// <summary>What a person looks like, as ids and numbers only (never images). The face never changes: Seed fixes the physical features;
    /// a job or clothes change touches only Outfit/Archetype. Tendencies are the age in years at which greying, a receding hairline and
    /// baldness begin (0 = never), so the same person ages the same way on every machine.</summary>
    public sealed class CharAppearance
    {
        public int Seed;
        public string Climate = "temperate", Archetype = "civil", Outfit = "";
        public int GreyAge, RecedeAge, BaldAge;
        /// <summary>The complete portrait recipe (JSON, opaque to Sim) of this person as a young adult: face, skin, hair, hair colour, brows, eyes, beard, original outfit.
        /// Filled by the Game layer the first time the portrait is needed, then saved, so adding or changing portrait assets never changes an existing person. Empty = not made yet.</summary>
        public string Definition = "";
        public CharAppearance Clone() => (CharAppearance)MemberwiseClone();
    }

    public sealed class Character
    {
        public int Id;
        public string Name;
        public int BirthDay;              // calendar day (World.Day); may be negative for people born before the game began
        public int HomeSettlement, Settlement;   // settlement ids (World.CapitalId = your starting settlement)
        public int Civilization;          // 0 = yours
        public Background Background;
        public string Occupation = "";    // civilian job
        public bool Alive = true;
        public Employment Employment;
        public readonly int[] Attr = new int[5];          // Administration, Economics, Military, Stewardship, Diplomacy, 0-100
        public int Loyalty, Ambition, Health;             // 0-100
        public int ExperienceHundredths;                  // 0-10000 (Experience is this / 100)
        public readonly List<Trait> Traits = new List<Trait>();   // 1 to 3
        public Post Post; public int PostSettlement;       // Phase 2
        public int MonthlySalary; public PayLevel Pay = PayLevel.Standard;
        public int MissedPayments, AppointedDay;   // unpaid months in a row; the day of the current appointment
        public CharAppearance Look = new CharAppearance();
        public AgeStage Stage;            // last stage recorded (so a change is noticed once)
        public int DeathDay, RetiredDay;
        public readonly List<(int day, string text)> History = new List<(int, string)>();
        public const int HistoryMax = 24;

        public double Experience => ExperienceHundredths / 100.0;
        public int AgeYears(int day) => Math.Max(0, (day - BirthDay) / CharacterBalance.YearLength);
        public AgeStage StageAt(int day) => CharacterBalance.StageOfAge(AgeYears(day));
        public string StageId => CharacterBalance.StageIds[(int)Stage];
        public bool InWorld => Alive && Employment != Employment.Deceased;
        public bool HasPost => Post != Post.None;

        /// <summary>The attribute with its traits applied (clamped 0-100); this is what suitability and salary use.</summary>
        public int Effective(Attr a)
        {
            int v = Attr[(int)a];
            foreach (var t in Traits) v += Sim.Traits.Get(t).Attr[(int)a];
            return Math.Clamp(v, 0, 100);
        }
        public double SalaryTraitFactor() => Math.Max(0.5, 1 + Traits.Sum(t => Sim.Traits.Get(t).SalaryPct) / 100.0);
        public double ExperienceTraitFactor() => Math.Max(0.25, 1 + Traits.Sum(t => Sim.Traits.Get(t).ExperiencePct) / 100.0);
        public bool Has(Trait t) => Traits.Contains(t);

        public void Log(int day, string text)
        {
            History.Add((day, text));
            if (History.Count > HistoryMax) History.RemoveAt(1);   // keep the first entry (birth) and the newest ones
        }

        public string Describe(int day) =>
            $"{Name}, {AgeYears(day)}, {CharacterBalance.StageNames[(int)StageAt(day)]} {Occupation} ({string.Join(", ", Traits)})";
    }

    /// <summary>A person who has just changed state; the registry keeps the recent ones for the UI to list and for Phase 2 to turn into notices.</summary>
    public enum CharEventKind { Born, StageChanged, Retired, Died, Arrived, Resigned }
    public struct CharEvent { public int Day, Id; public CharEventKind Kind; public Post Post; public string Text; }
}
