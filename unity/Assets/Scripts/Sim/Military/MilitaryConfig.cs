// Every number of the military system in one place (Zach's rule: centralised, easy to adjust). Balance reference points from the spec
// (population -> professionals / registered militia): 50: 0-2 / 5-10, 200: 4-10 / 20-40, 500: 10-25 / 50-100, 1000: 20-50 / 100-200,
// 5000: 100-250 / 500-1000, 10000: 200-500 / 1000-2000. They are guides, not caps: going over them just costs workers, food and gold.
namespace CrownAndHearth.Sim
{
    public static class MilitaryConfig
    {
        /// <summary>Version of the military part of a save. Bump it when the saved fields change and migrate in MilitarySystem.Load.</summary>
        public const int SaveVersion = 1;

        // ---- who can serve ----
        /// <summary>Share of the civilians (peasants) who are of an age to serve. This sim treats every peasant as a worker, so it is 1.</summary>
        public const double EligibleShare = 1.0;
        /// <summary>Civilians that can never be recruited or registered away (a town keeps at least this many).</summary>
        public const int MinCivilians = 2;

        // ---- company sizes (spec: guard 2-8, militia 10-30, infantry 10-40, archers 10-30, cavalry 10-30) ----
        public const int MilitiaCompanyMax = 30, SpearmanCompanyMax = 40, ArcherCompanyMax = 30, CavalryCompanyMax = 30, GuardCompanyMax = 8;

        // ---- buildings ----
        public const int MusterCapacity = 30;      // registered militia one muster ground can hold on its roll
        public const int BarracksCapacity = Defs.Bunks;   // professionals one barracks houses (16)

        // ---- recruitment ----
        /// <summary>Professionals one company takes on per day while it is below its target (recruits arrive over days, never in one go).</summary>
        public const int RecruitPerDay = 2;
        public const double RecruitTraining = 8, RecruitMorale = 65;   // a fresh recruit starts barely trained
        public const double MilitiaStartTraining = 4;
        /// <summary>Soldiers an old save (or an army coming home) hands over without a company are treated as lightly trained veterans.</summary>
        public const double MigratedTraining = 40, MigratedExperience = 5;

        // ---- training (0-100, grows gradually) ----
        /// <summary>Daily growth is rate * (1 - training/100), up to the cap: basic training only goes so far until training grounds exist.</summary>
        public const double ProTrainRate = 0.9, MilitiaTrainRate = 0.12;
        public const double ProTrainCap = 80, MilitiaTrainCap = 45;

        // ---- experience (0-100, service and combat; it persists) ----
        public const double ServiceExperienceRate = 0.01, ExperiencePerKill = 0.6, ExperienceCap = 100;

        // ---- morale (0-100, recovers slowly) ----
        public const double MoraleBase = 70, MoraleStarving = -30, MoraleUnpaid = -15, MoraleLongMobilisation = -12, MoraleOverCapacity = -8;
        public const int MobilisationComfortDays = 30;
        public const double MoraleMovePerDay = 1.5;

        // ---- mobilisation ----
        /// <summary>Days a militia company needs to gather at the muster point and take up arms; readiness climbs to full over this time.</summary>
        public const int MobiliseDays = 3;

        // ---- how a mobilised militiaman fights (soldier equivalents; a trained professional is 1) ----
        public const double MilitiaPowerUntrained = 0.15, MilitiaPowerTrained = 0.45;
        /// <summary>Damage per day a full-power militiaman does to a raider in reach, and the reach in tiles round the muster point.</summary>
        public const double MilitiaDamagePerPower = 0.5;
        public const int MilitiaReach = 6;
        /// <summary>Wounds (lives) per raider in reach per fighting day, per militiaman; they die like any civilian.</summary>
        public const double MilitiaWoundRate = 0.004;

        // ---- eras (extension points; no tech tree yet, so every era up to CurrentEra is open) ----
        public const int CurrentEra = 2;
    }
}
