// The tile and building inspector. GameController.TileInfo still assembles the facts as plain lines (one per fact, the first line is the heading);
// this turns those lines into a standard layout: a heading, short "Label: value" facts as aligned rows, sentences as paragraphs, and problems
// (not connected, no keeper, nothing to fell...) marked in a warning colour. Pure C# (no UnityEngine), tested directly.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrownAndHearth.Game.UI
{
    public sealed class Inspection
    {
        public string Title = "";
        public readonly List<Row> Rows = new List<Row>();
    }

    public static class InspectionData
    {
        static readonly string[] Problems =
        {
            "Not connected", "cut off from", "No keeper", "No water it can fish", "Nothing to fell", "No home will grow", "out of service",
            "No cargo barge", "No ferry boat", "none assigned", "is starving", "Without boats", "No flowing river", "not joined to fresh water",
        };
        /// <summary>For a problem the inspector reports: what to do about it. Only problems whose line does not already say how to fix them are listed, and each fix is a plain
        /// fact about how the game works (nothing is predicted).</summary>
        static readonly (string Phrase, string Fix)[] Advice =
        {
            ("Not connected", "Lay a road from its door to a road that reaches the Town Hall (Roads, then drag)."),
            ("cut off from the Town Hall", "A road or bridge tile is missing between here and the Town Hall: build the missing stretch."),
            ("No home will grow", "Housing zones only fill when they touch a road that reaches the Town Hall: lay a road beside this tile."),
            ("No water it can fish", "Build it where water is within reach (a dock needs water deep enough for boats)."),
            ("not joined to fresh water", "Dig the canal on until it touches a river, lake or pond bank."),
            ("No flowing river", "A watermill needs a flowing river beside it. A frozen river thaws again; otherwise build on a river bank."),
        };

        static readonly Regex Fact = new Regex(@"^([A-Z][A-Za-z' ]{1,22}): (.{1,48})$", RegexOptions.Compiled);

        public static Inspection Parse(string text)
        {
            var ins = new Inspection();
            if (string.IsNullOrWhiteSpace(text)) return ins;
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            ins.Title = lines[0];
            var fixes = new List<string>();
            foreach (var line in lines.Skip(1))
            {
                foreach (var a in Advice) if (line.IndexOf(a.Phrase, StringComparison.OrdinalIgnoreCase) >= 0 && !fixes.Contains(a.Fix)) fixes.Add(a.Fix);
                var tone = Problems.Any(p => line.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ? Tone.Bad
                    : line.StartsWith("Connected to the Town Hall", StringComparison.Ordinal) ? Tone.Good : Tone.Normal;
                var m = Fact.Match(line);
                if (m.Success && tone != Tone.Bad && !m.Groups[2].Value.Contains(". "))
                    ins.Rows.Add(new Row { Label = m.Groups[1].Value, Value = m.Groups[2].Value, Tone = tone, Bar = -1f });
                else
                    ins.Rows.Add(new Row { Label = "", Value = line, Tone = tone, Bar = -1f, Wide = true });
            }
            foreach (var f in fixes) ins.Rows.Add(new Row { Label = "To fix", Value = f, Tone = Tone.Normal, Bar = -1f, Wide = false });
            return ins;
        }

        const string DimHex = "#c8b898", BadHex = "#ff8c73", GoodHex = "#9ee68c";

        static string Wrap(string text, Tone tone) => tone == Tone.Bad ? $"<color={BadHex}>{text}</color>" : tone == Tone.Good ? $"<color={GoodHex}>{text}</color>" : text;

        /// <summary>The body of the inspector as rich text: facts as "label  value", sentences as they are, problems coloured.</summary>
        public static string Body(Inspection ins)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in ins.Rows)
            {
                if (sb.Length > 0) sb.Append('\n');
                if (r.Wide) sb.Append(Wrap(r.Value, r.Tone));
                else sb.Append($"<color={DimHex}>{r.Label}</color>   {Wrap(r.Value, r.Tone)}");
            }
            return sb.ToString();
        }
    }
}
