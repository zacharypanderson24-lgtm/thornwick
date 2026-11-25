// Plain-language sentences for the Military panel, built from the sim (pure functions, no UI), so what the panel tells the player is testable.
using System;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public static class MilitaryAdvice
    {
        /// <summary>One sentence under the "full-time soldiers" number: what happens to the workers and the bunks.</summary>
        public static string SoldiersLine(World w)
        {
            var m = w.Mil;
            int goal = m.SoldierGoal, have = m.Professionals, incoming = goal - have, workers = m.CivilianWorkforce;
            if (goal == 0)
                return m.Barracks == 0 ? "No full-time soldiers. Build a barracks before you recruit any."
                                       : "No full-time soldiers. Raise the number to recruit some: they leave their jobs for good.";
            if (incoming > 0)
            {
                int joining = Math.Min(incoming, Math.Min(m.AvailableToServe, Math.Max(0, m.BunksFree)));
                var im = m.Impact(joining, 0);
                string s = $"{incoming} more workers will join up, a couple a day: workers {workers} -> {Math.Max(0, workers - incoming)}.";
                if (m.Barracks == 0) s += " Not enough bunks: build a barracks.";
                else if (incoming > m.BunksFree) s += $" Needs {incoming - m.BunksFree} more bunks: build another barracks.";
                if (incoming > m.AvailableToServe) s += $" Only {m.AvailableToServe} civilians are free to serve.";
                if (im.Warning.Length > 0) s += " " + im.Warning;
                return s;
            }
            string on = $"{have} soldiers on duty. They eat 1.5 food each and cost 0.1 gold a day. Workers: {workers}.";
            if (m.Unhoused > 0) on += $" {m.Unhoused} have no bunk: build a barracks.";
            return on;
        }

        /// <summary>One sentence under the militia roll number.</summary>
        public static string RollLine(World w)
        {
            var m = w.Mil;
            if (m.MusterCapacity == 0) return "Build a muster ground to put villagers on the militia roll.";
            string s = $"{m.Registered} of {m.MusterCapacity} places used. People on the roll keep working at their jobs until you call them up.";
            if (m.Registered >= m.MusterCapacity) s += " The roll is full: build another muster ground.";
            else if (m.AvailableToServe == 0) s += " No more civilians are free to register.";
            return s;
        }

        /// <summary>One sentence under the big call-up / send-home button.</summary>
        public static string CallUpLine(World w)
        {
            var m = w.Mil;
            if (m.MobilisedCount > 0)
                return $"{m.MobilisedCount} militia are called up and off work. Sending them home puts {m.MobilisedCount} workers back: {m.CivilianWorkforce} -> {m.CivilianWorkforce + m.MobilisedCount}.";
            int ready = m.Registered;
            if (ready == 0) return "Nobody is on the roll yet, so there is nobody to call up.";
            var im = m.Impact(0, ready);
            return $"Calling up {ready} militia takes them off their jobs: workers {im.Before} -> {im.After}. Work stops where they stood until you send them home. {im.Warning}".TrimEnd();
        }

        /// <summary>What a company is doing, in words.</summary>
        public static string Activity(MilitaryCompany c)
        {
            if (c.IsMilitia) return c.Mobilised ? "Gathered at the muster ground" : "At their normal jobs";
            return c.Order == MilitaryOrderKind.StandDown || c.Order == MilitaryOrderKind.Train ? "Drilling at the barracks" : MilitaryCompanies.OrderName(c.Order);
        }

        /// <summary>One word for how a company is doing: Recruiting, Needs bunks, Low spirits, Training, Gathering, Ready, On the roll.</summary>
        public static string StatusWord(MilitaryCompany c, World w)
        {
            if (c.IsMilitia)
            {
                if (!c.Mobilised) return "On the roll";
                if (c.Morale < 35) return "Low spirits";
                return c.MobilisedDays < MilitaryConfig.MobiliseDays ? "Gathering" : "Ready";
            }
            if (c.Personnel < c.Target) return "Recruiting";
            if (w.Mil.Unhoused > 0) return "Needs bunks";
            if (c.Morale < 35) return "Low spirits";
            if (c.Training < 40) return "Training";
            return "Ready";
        }

        public static string MenText(MilitaryCompany c) => c.Personnel == c.Target || c.IsMilitia ? $"{c.Personnel} men" : $"{c.Personnel} of {c.Target} men";
    }
}
