// A company: the unit the military is managed and simulated in (never individual soldiers). Pure data, no UI.
using System;

namespace CrownAndHearth.Sim
{
    /// <summary>Which of the three personnel categories a company belongs to.</summary>
    public enum MilitaryCategory
    {
        /// <summary>Registered civilians in emergency service: keep their jobs and homes until mobilised, no wages.</summary>
        Militia,
        /// <summary>Permanent soldiers recruited from the workforce: leave their jobs, live in barracks, eat, are paid.</summary>
        Professional,
        /// <summary>A company assigned away from its home settlement (architecture only in this version, nothing creates one yet).</summary>
        Field,
    }

    public enum MilitaryUnitType { PeasantLevy, Spearman, HunterArcher, ShieldInfantry }   // the four Phase 1 troop types of DESIGN_MILITARY.md; more are rows in TroopDefinitions

    public enum MilitaryOrderKind { StandDown, Train, Assemble, HoldPosition, GuardBuilding, DefendArea, Patrol, ReturnToStation }

    public sealed class MilitaryCompany
    {
        public int Id;
        public string Name = "";
        public MilitaryCategory Category;
        public MilitaryUnitType Unit;
        public int HomeSettlement = -1;           // -1: this settlement
        public int Personnel, Target;             // real men now, and what the player wants (understrength is allowed)
        public double Training, Experience, Morale = MilitaryConfig.RecruitMorale, Readiness;   // 0-100 each
        public bool Mobilised;                    // militia only: called up, off the job, at the muster point
        public int MobilisedDays;                 // days since the call-up (readiness and the long-mobilisation morale effect)
        public MilitaryOrderKind Order = MilitaryOrderKind.StandDown;
        public int OrderX = -1, OrderY = -1;      // target tile of an order that has one
        public int StationX = -1, StationY = -1;  // the building tile the company is based at (muster ground, barracks)
        public int Equipped;                      // men with their kit (equipment system, a later phase); improvised weapons need none
        public string Location = "At station";    // text for the UI; real tile positions arrive with company movement

        public bool IsMilitia => Category == MilitaryCategory.Militia;
        public int Max => TroopDefinitions.Get(Unit).MaxCompany;
        public double Fatigue;                    // 0-100, rises in the field (later phases)
        public string Formation = "";             // formation id (later phases)
        public string Supply = "Home";            // supply status text (later phases)
        public double Fill => Target > 0 ? Math.Min(1, Personnel / (double)Target) : (Personnel > 0 ? 1 : 0);
    }

    public static class MilitaryCompanies
    {

        public static string UnitName(MilitaryUnitType u) => TroopDefinitions.Get(u).Name;

        public static string CategoryName(MilitaryCategory c) => c switch { MilitaryCategory.Militia => "Militia", MilitaryCategory.Professional => "Professional", _ => "Field army" };

        public static string OrderName(MilitaryOrderKind k) => k switch
        {
            MilitaryOrderKind.StandDown => "Stand down", MilitaryOrderKind.Train => "Train", MilitaryOrderKind.Assemble => "Assemble",
            MilitaryOrderKind.HoldPosition => "Hold position", MilitaryOrderKind.GuardBuilding => "Guard a building",
            MilitaryOrderKind.DefendArea => "Defend an area", MilitaryOrderKind.Patrol => "Patrol a route", _ => "Return to station",
        };
    }
}
