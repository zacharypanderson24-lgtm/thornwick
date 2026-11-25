// Pure UI logic of the Military Command window (no Unity types): which filters exist, which actions a company offers and why one is unavailable,
// default names, how many men an action can take, and the plain consequence sentences shown before the player confirms. Tested in MilitaryUiTests.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public enum CompanyFilter { All, Professional, Reserve, Mobilised, Infantry, Ranged }

    public struct ActionState
    {
        public string Id, Label, Reason;   // Reason is empty when Enabled
        public bool Enabled, Danger;
    }

    public static class MilitaryUi
    {
        public const string TownName = "Thornwick";

        public static string FilterName(CompanyFilter f) => f switch
        {
            CompanyFilter.Professional => "Professional soldiers", CompanyFilter.Reserve => "Reserve militia", CompanyFilter.Mobilised => "Mobilized militia",
            CompanyFilter.Infantry => "Infantry", CompanyFilter.Ranged => "Ranged troops", _ => "All companies",
        };

        public static bool Matches(MilitaryCompany c, CompanyFilter f) => f switch
        {
            CompanyFilter.Professional => c.Category == MilitaryCategory.Professional,
            CompanyFilter.Reserve => c.IsMilitia && !c.Mobilised,
            CompanyFilter.Mobilised => c.IsMilitia && c.Mobilised,
            CompanyFilter.Infantry => !TroopDefinitions.Get(c.Unit).Ranged,
            CompanyFilter.Ranged => TroopDefinitions.Get(c.Unit).Ranged,
            _ => true,
        };

        /// <summary>The filters worth showing: All, plus each kind that at least one company belongs to (more appear as troop types arrive).</summary>
        public static List<CompanyFilter> AvailableFilters(World w)
        {
            var list = new List<CompanyFilter> { CompanyFilter.All };
            foreach (var f in new[] { CompanyFilter.Professional, CompanyFilter.Reserve, CompanyFilter.Mobilised, CompanyFilter.Infantry, CompanyFilter.Ranged })
                if (w.Mil.Companies.Any(c => Matches(c, f))) list.Add(f);
            return list;
        }

        public static string Service(MilitaryCompany c) => c.IsMilitia ? (c.Mobilised ? "Mobilized militia" : "Reserve militia") : c.Category == MilitaryCategory.Professional ? "Professional" : "Field army";

        public static string Ordinal(int n) => n % 100 >= 11 && n % 100 <= 13 ? n + "th" : (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };

        /// <summary>A sensible unused name: "Village Militia", "2nd Thornwick Spearmen", "Woodland Archers", "Thornwick Shield Infantry"...</summary>
        public static string DefaultName(World w, MilitaryUnitType unit)
        {
            for (int n = 1; n < 1000; n++)
            {
                string pre = n == 1 ? "" : Ordinal(n) + " ";
                string name = unit switch
                {
                    MilitaryUnitType.PeasantLevy => pre + "Village Militia",
                    MilitaryUnitType.HunterArcher => pre + "Woodland Archers",
                    MilitaryUnitType.ShieldInfantry => pre + TownName + " Shield Infantry",
                    _ => (n == 1 ? "1st " : pre) + TownName + " Spearmen",
                };
                if (!w.Mil.Companies.Any(c => c.Name == name)) return name;
            }
            return "New company";
        }

        // ---------- who can do what ----------

        /// <summary>How many more men this company can take now (registering or recruiting): company room, free civilians, muster places or bunks.</summary>
        public static int MaxMore(World w, MilitaryCompany c)
        {
            var m = w.Mil;
            int room = Math.Max(0, c.Max - Math.Max(c.Target, c.Personnel));
            if (c.IsMilitia) return Math.Max(0, Math.Min(room, Math.Min(m.AvailableToServe, m.MusterCapacity - m.Registered)));
            return Math.Max(0, Math.Min(room, Math.Min(m.AvailableToServe, m.BunksFree - Math.Max(0, m.SoldierGoal - w.Soldiers))));
        }

        /// <summary>The actions a company offers, each enabled or disabled with the reason, so the player never has to guess why a click does nothing.</summary>
        public static List<ActionState> Actions(World w, MilitaryCompany c)
        {
            var m = w.Mil; var list = new List<ActionState>();
            ActionState A(string id, string label, string reason, bool danger = false) => new ActionState { Id = id, Label = label, Reason = reason ?? "", Enabled = string.IsNullOrEmpty(reason), Danger = danger };
            if (c.IsMilitia && !c.Mobilised)
            {
                string reg = m.MusterCapacity == 0 ? "Muster Ground required" : c.Personnel >= c.Max ? "This company is full" : m.Registered >= m.MusterCapacity ? "Muster ground capacity reached"
                    : m.AvailableToServe == 0 ? "No eligible residents available" : null;
                list.Add(A("register", "Register more militia", reg));
                list.Add(A("target", "Set target strength", c.Max <= 0 ? "No room" : null));
                list.Add(A("mobilise", "Mobilize company", c.Personnel <= 0 ? "Insufficient personnel: nobody is on the roll" : null));
                list.Add(A("unregister", "Unregister personnel", c.Personnel <= 0 ? "Insufficient personnel" : null));
            }
            else if (c.IsMilitia)
            {
                list.Add(A("demobilise", "Demobilize company", null));
            }
            else
            {
                string rec = m.Barracks == 0 ? "Barracks required" : c.Max - Math.Max(c.Target, c.Personnel) <= 0 ? "This company is at full strength"
                    : m.AvailableToServe == 0 ? "No eligible residents available" : MaxMore(w, c) <= 0 ? "No free barracks accommodation" : null;
                list.Add(A("recruit", "Recruit additional soldiers", rec));
                list.Add(A("target", "Set target strength", c.Personnel >= c.Max ? "This company is at full strength" : m.Barracks == 0 ? "Barracks required" : null));
                list.Add(A("discharge", "Discharge personnel", c.Personnel <= 0 ? "Insufficient personnel" : null));
            }
            list.Add(A("disband", "Disband company", null, true));
            return list;
        }

        // ---------- consequence sentences ----------

        static string Warn(WorkforceImpact im) => im.Warning.Length > 0 ? " " + im.Warning : "";

        public static string RecruitSentence(World w, MilitaryCompany c, int n) => RecruitSentence(w, c.Unit, n);

        public static string RecruitSentence(World w, MilitaryUnitType unit, int n)
        {
            var m = w.Mil; var im = m.Impact(n, 0);
            return $"Recruit {n} professional {TroopDefinitions.Get(unit).Name.ToLower()}{(n == 1 ? "" : "s")}, about {MilitaryConfig.RecruitPerDay} a day. Civilian workforce: {im.Before} → {im.After}. Barracks occupancy: {w.Soldiers}/{m.BunkCapacity} → {w.Soldiers + n}/{m.BunkCapacity}.{Warn(im)}";
        }

        public static string RegisterSentence(World w, int n)
        {
            var m = w.Mil;
            return $"Register {n} militia. Reserve militia keep their civilian jobs until mobilized: the workforce stays {m.CivilianWorkforce}. Muster roll: {m.Registered}/{m.MusterCapacity} → {m.Registered + n}/{m.MusterCapacity}.";
        }

        public static string UnregisterSentence(World w, int n)
        {
            var m = w.Mil;
            return $"Take {n} off the militia roll. They stay civilians and keep working. Muster roll: {m.Registered}/{m.MusterCapacity} → {m.Registered - n}/{m.MusterCapacity}.";
        }

        public static string DischargeSentence(World w, int n)
        {
            var m = w.Mil;
            return $"Discharge {n}: they go back to civilian work. Civilian workforce: {m.CivilianWorkforce} → {m.CivilianWorkforce + n}. Barracks occupancy: {w.Soldiers}/{m.BunkCapacity} → {w.Soldiers - n}/{m.BunkCapacity}.";
        }

        public static string TargetSentence(World w, MilitaryCompany c, int target)
        {
            var m = w.Mil;
            if (c.IsMilitia)
            {
                int d = target - c.Personnel;
                return d > 0 ? RegisterSentence(w, d) : d < 0 ? UnregisterSentence(w, -d) : "No change.";
            }
            int more = target - c.Personnel;
            if (more <= 0) return "No change. To send soldiers home use Discharge personnel.";
            return RecruitSentence(w, c, more).Replace("Recruit ", "Target " + target + ": recruit ");
        }

        public static string MobiliseSentence(World w, int n)
        {
            var im = w.Mil.Impact(0, n);
            return $"Mobilizing {n} militia member{(n == 1 ? "" : "s")} will reduce the civilian workforce from {im.Before} to {im.After}." +
                   (im.Warning.Length > 0 ? " This may cause labor shortages. " + im.Warning : " Their production stops until you demobilize them.");
        }

        public static string DemobiliseSentence(World w, int n) => $"Demobilizing {n} returns them to their jobs: civilian workforce {w.Mil.CivilianWorkforce} → {w.Mil.CivilianWorkforce + n}.";

        public static string DisbandSentence(MilitaryCompany c) => c.Personnel == 0 ? $"Disband {c.Name}?" :
            $"Disband {c.Name}? Its {c.Personnel} {(c.IsMilitia ? "members leave the militia roll" : "soldiers are discharged back to civilian work")}. Its training ({c.Training:0}%) and experience ({c.Experience:0}%) are lost.";

        // ---------- statistic tooltips ----------

        public static (string title, string body) StatTip(string stat, MilitaryCompany c)
        {
            switch (stat)
            {
                case "Training": return ($"Training: {c.Training:0}%", "Proficiency gained through military drills. " + (c.IsMilitia ? "Reserve militia drill only occasionally at a muster ground, so they improve slowly and stop early." : "Professional soldiers train every day, much faster than militia."));
                case "Experience": return ($"Experience: {c.Experience:0}%", "What service and real fighting have taught them. It is never lost, but new recruits with no experience dilute the average.");
                case "Morale": return ($"Morale: {c.Morale:0}%", "Their mood. Food, wages, a bunk to sleep in and short call-ups keep it high; starvation, unpaid wages, no bunk and long mobilization pull it down. It recovers slowly.");
                default: return ($"Readiness: {c.Readiness:0}%", "How soon the company can act: enough men, fed, and called up. It says nothing about how skilled they are (that is training).");
            }
        }
    }
}
