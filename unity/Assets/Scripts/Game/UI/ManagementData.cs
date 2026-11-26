// What the management windows show, read straight from the simulation. Pure C# (no UnityEngine): every number here is a World value or a plain
// sum of World values, nothing is invented, and the tests compare the rows against the World directly.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum Tone { Normal, Good, Warn, Bad, Dim }

    public struct Row
    {
        public string Label, Value;
        public float Bar;       // 0..1, or -1 for none
        public Tone Tone;
        public bool Wide;       // a sentence: left-aligned value, narrow label column
        public static Row Of(string label, string value, Tone tone = Tone.Normal, float bar = -1f) => new Row { Label = label, Value = value, Tone = tone, Bar = bar };
    }

    public static class ManagementData
    {
        public const int MaxRows = 16;

        public static string[] Tabs(MenuId id) => id switch
        {
            MenuId.Settlement => new[] { "Overview", "Defence" },
            MenuId.Economy => new[] { "Stockpiles" },
            MenuId.Notifications => new[] { "All", "Warnings" },
            _ => new[] { "Overview" },
        };

        public static string ResName(Res r) => r switch { Res.Food => "Wheat", Res.SmokedMeat => "Smoked meat", Res.Ore => "Iron ore", _ => r.ToString() };

        public static List<Row> Rows(MenuId id, int tab, World w, NotificationManager notes)
        {
            switch (id)
            {
                case MenuId.Settlement: return tab == 0 ? SettlementOverview(w) : SettlementDefence(w);
                case MenuId.Economy: return Stockpiles(w);
                case MenuId.Notifications: return Messages(notes, tab == 1);
                default: return new List<Row>();
            }
        }

        public static string EmptyText(MenuId id, int tab) => id == MenuId.Notifications
            ? (tab == 1 ? "No warnings. Nothing needs your attention." : "Nothing has happened yet.") : "Nothing to show.";

        public static List<Row> SettlementOverview(World w)
        {
            var rows = new List<Row>();
            rows.Add(Row.Of("Date", w.SeasonalFarming ? $"{w.Season}, year {w.Year}, day {w.YearDay}" : $"Day {w.Day}"));
            rows.Add(Row.Of("Settlers", w.Peasants.ToString()));
            rows.Add(Row.Of("Working", $"{w.D.Employed} of {w.D.Jobs} jobs filled", w.D.Jobs > 0 && w.D.Employed < w.D.Jobs ? Tone.Warn : Tone.Normal,
                w.D.Jobs > 0 ? (float)Math.Min(1.0, (double)w.D.Employed / w.D.Jobs) : -1f));
            rows.Add(Row.Of("Without work", w.D.Idle.ToString(), w.D.Idle > 0 ? Tone.Warn : Tone.Normal));
            rows.Add(Row.Of("Homes hold", $"{w.D.Civ} people"));
            double demand = w.DailyFoodDemand, wheat = w.WheatStored;
            rows.Add(Row.Of("Wheat stored", $"{Math.Floor(wheat):0}"));
            rows.Add(Row.Of("Eaten each day", $"{demand:0.0}"));
            if (demand > 0.01)
            {
                double days = wheat / demand;
                rows.Add(Row.Of("Food lasts", $"{days:0.#} days", days < 10 ? (days < 3 ? Tone.Bad : Tone.Warn) : Tone.Good));
            }
            rows.Add(Row.Of("Mood", $"{w.MoodName} ({w.Happiness:0})", w.Restless ? Tone.Bad : Tone.Normal, (float)Math.Clamp(w.Happiness / 100.0, 0, 1)));
            if (w.Starving) rows.Add(Row.Of("Warning", "The town is starving", Tone.Bad));
            return rows;
        }

        public static List<Row> SettlementDefence(World w)
        {
            var rows = new List<Row>();
            rows.Add(Row.Of("Soldiers", w.Soldiers.ToString()));
            rows.Add(Row.Of("Infantry", w.Infantry.ToString()));
            rows.Add(Row.Of("Archers", w.Archers.ToString()));
            rows.Add(Row.Of("Era", w.Era.ToString()));
            return rows;
        }

        public static List<Row> Stockpiles(World w)
        {
            var rows = new List<Row>();
            foreach (Res r in Enum.GetValues(typeof(Res)))
            {
                double amount = r == Res.Food ? w.WheatStored : w.ResAmt.TryGetValue(r, out var a) ? a : 0;
                double rate = w.Rate.TryGetValue(r, out var q) ? q : 0;
                string change = Math.Abs(rate) < 0.05 ? "steady" : $"{rate:+0.0;-0.0} a day";
                rows.Add(Row.Of(ResName(r), $"{Math.Floor(amount):0}   ({change})", rate < -0.05 ? Tone.Bad : Tone.Normal));
            }
            return rows;
        }

        public static List<Row> Messages(NotificationManager notes, bool warningsOnly)
        {
            var rows = new List<Row>();
            for (int i = notes.History.Count - 1; i >= 0 && rows.Count < MaxRows; i--)
            {
                var n = notes.History[i];
                if (warningsOnly && n.Kind != NoteKind.Warning && n.Kind != NoteKind.Danger) continue;
                var tone = n.Kind == NoteKind.Danger ? Tone.Bad : n.Kind == NoteKind.Warning ? Tone.Warn : n.Kind == NoteKind.Success ? Tone.Good : Tone.Normal;
                rows.Add(new Row { Label = $"Day {n.Day}", Value = n.Count > 1 ? $"{n.Text} (x{n.Count})" : n.Text, Tone = tone, Bar = -1f, Wide = true });
            }
            return rows;
        }
    }
}
