// Troop types as data (DESIGN_MILITARY.md sections 6 and 7): add a row to add a troop type. Phase 1 carries the four Era I-II types and the fields
// the rest of the system needs (name, era, company size, whether it counts as an archer in the old Soldiers/Archers totals, equipment requirement text).
// The full attribute list of section 7 (melee/ranged attack, armour, speed, stamina, terrain modifiers...) is added here with combat in later phases.
using System.Linq;

namespace CrownAndHearth.Sim
{
    public sealed class TroopDefinition
    {
        public MilitaryUnitType Type;
        public string Name = "", Short = "", Equipment = "", Role = "";
        public int Era = 1, MaxCompany = 30;
        /// <summary>Counts in World.Archers (the aggregate the raid and camp code read); everything else is infantry.</summary>
        public bool Ranged;
        /// <summary>True for the peasant levy: civilians on the roll, never professionals.</summary>
        public bool Levy;
    }

    public static class TroopDefinitions
    {
        public static readonly TroopDefinition[] All =
        {
            new TroopDefinition { Type = MilitaryUnitType.PeasantLevy, Name = "Peasant levy", Short = "levy", Era = 1, MaxCompany = MilitaryConfig.MilitiaCompanyMax, Levy = true,
                Equipment = "Improvised weapons", Role = "Emergency village defence" },
            new TroopDefinition { Type = MilitaryUnitType.Spearman, Name = "Spearman", Short = "spear", Era = 1, MaxCompany = MilitaryConfig.SpearmanCompanyMax,
                Equipment = "Spear", Role = "Basic organised infantry" },
            new TroopDefinition { Type = MilitaryUnitType.HunterArcher, Name = "Hunter archer", Short = "archer", Era = 1, MaxCompany = MilitaryConfig.ArcherCompanyMax, Ranged = true,
                Equipment = "Hunting bow and arrows", Role = "Early defensive ranged support" },
            new TroopDefinition { Type = MilitaryUnitType.ShieldInfantry, Name = "Shield infantry", Short = "shield", Era = 2, MaxCompany = MilitaryConfig.SpearmanCompanyMax,
                Equipment = "Spear and shield", Role = "Sturdier frontline infantry" },
        };

        public static TroopDefinition Get(MilitaryUnitType t) => All.First(d => d.Type == t);
    }
}
