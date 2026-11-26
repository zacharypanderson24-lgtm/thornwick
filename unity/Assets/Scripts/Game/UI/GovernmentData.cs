// The Government menu (DESIGN_GOVERNMENT.md phase 2): the council posts, one recruitment screen, a person's details, and the payroll.
// Pure C# (no UnityEngine): every row is read from the World, and every button runs a validated World operation.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    /// <summary>What the player has selected in the Government menu. A change bumps Stamp so the window redraws.</summary>
    public sealed class GovernmentUiState
    {
        public Post Post = Post.Chancellor;
        public int Selected;                       // a character id, 0 = none
        public CandidateSort Sort = CandidateSort.Suitability;
        public CandidateSource? Source;            // null = everyone
        public int Stamp { get; private set; }
        public void Changed() => Stamp++;

        public static readonly CandidateSort[] SortOrder = { CandidateSort.Suitability, CandidateSort.Experience, CandidateSort.Loyalty, CandidateSort.Salary, CandidateSort.Age };
        public void CycleSort() { Sort = SortOrder[(Array.IndexOf(SortOrder, Sort) + 1) % SortOrder.Length]; Changed(); }
        public void CycleSource()
        {
            Source = Source == null ? CandidateSource.Local : Source == CandidateSource.Local ? CandidateSource.OtherSettlement : Source == CandidateSource.OtherSettlement ? CandidateSource.International : (CandidateSource?)null;
            Changed();
        }
        public string SortName => Sort == CandidateSort.Suitability ? "Suitability" : Sort.ToString();
        public string SourceName => Source == null ? "Everyone" : Source == CandidateSource.Local ? "Local" : Source == CandidateSource.OtherSettlement ? "Other settlements" : "Abroad";
        // (sort order is best first, except salary and age where the smallest comes first)
        public bool Descending => Sort != CandidateSort.Salary && Sort != CandidateSort.Age;
    }

    public static class GovernmentData
    {
        public const string Prefix = "gov:";
        public const int CandidateRows = 11;

        public static string[] Tabs => new[] { "Council", "Recruit", "Details", "Payroll" };

        /// <summary>The Government menu opens once there is something real to do: a council post exists in this era, or someone already holds one.</summary>
        public static string WhyLocked(World w) =>
            w.Era >= 2 || w.Officials.Any() ? null : "Government: the first posts (Chancellor and Steward) appear in Era II.";

        static string Sal(int g) => $"{g} gold/mo";

        public static List<Row> Rows(World w, int tab, GovernmentUiState s) => tab switch
        {
            0 => Council(w, s), 1 => Recruit(w, s), 2 => Details(w, s), _ => Payroll(w),
        };

        public static string EmptyText(int tab) => tab == 3 ? "No one is on the payroll. Appoint someone from the Council tab." : "Nothing to show.";

        // ---------- Council ----------
        public static List<Row> Council(World w, GovernmentUiState s)
        {
            var rows = new List<Row>();
            foreach (var d in Positions.All)
            {
                var row = new Row { Label = d.Title, Id = Prefix + "post:" + d.Id, Bar = -1 };
                var holder = w.PostHolder(d.Id);
                if (holder != null) { row.Value = $"{holder.Name}, {holder.AgeYears(w.Day)}  ({Sal(holder.MonthlySalary)})"; row.Tone = Tone.Normal; row.PortraitId = holder.Id; }
                else if (Positions.LockedReason(d.Id, w.Era) is string why) { row.Value = why; row.Tone = Tone.Dim; }
                else if (d.Id == Post.Governor) { row.Value = "No other settlement of yours yet"; row.Tone = Tone.Dim; }
                else { row.Value = "Vacant: click to recruit"; row.Tone = Tone.Warn; }
                if (s != null && d.Id == s.Post) row.Label = "> " + d.Title;
                rows.Add(row);
            }
            var sel = Positions.Get(s?.Post ?? Post.Chancellor);
            rows.Add(new Row { Label = sel.Title, Value = sel.Duty + " Needs: " + string.Join(", ", sel.Needs.Select(n => $"{n.attr} {n.weight * 100:0}%")), Wide = true, Tone = Tone.Dim, Bar = -1 });
            rows.Add(Row.Of("Payroll", $"{w.MonthlyPayroll} gold a month (treasury {w.ResAmt[Res.Gold]:0})", w.ResAmt[Res.Gold] < w.MonthlyPayroll ? Tone.Bad : Tone.Normal));
            foreach (var e in w.Characters.Journal.Where(j => j.Kind == CharEventKind.Retired || j.Kind == CharEventKind.Died || j.Kind == CharEventKind.Resigned).Reverse().Take(2))
                rows.Add(new Row { Label = "Day " + e.Day, Value = e.Text, Wide = true, Tone = Tone.Dim, Bar = -1 });
            return rows;
        }

        // ---------- Recruit ----------
        public static List<Candidate> CandidatesFor(World w, GovernmentUiState s) =>
            w.FindCandidates(s.Post, new CandidateFilter { Source = s.Source }, s.Sort, s.Descending);

        public static List<Row> Recruit(World w, GovernmentUiState s)
        {
            var rows = new List<Row>();
            var d = Positions.Get(s.Post);
            string locked = Positions.LockedReason(s.Post, w.Era);
            rows.Add(new Row { Label = "Hiring", Value = $"{d.Title} | show: {s.SourceName} | best first by: {s.SortName}", Wide = true, Bar = -1, Tone = Tone.Good });
            if (locked != null) { rows.Add(new Row { Label = "", Value = locked, Wide = true, Tone = Tone.Dim, Bar = -1 }); return rows; }
            var holder = w.PostHolder(s.Post);
            rows.Add(new Row { Label = "Now", Value = holder != null ? $"{holder.Name} holds it (appointing replaces them)" : "Vacant", Wide = true, Bar = -1, Tone = holder != null ? Tone.Normal : Tone.Warn });
            foreach (var search in w.Searches)
                rows.Add(new Row { Label = "Abroad", Value = $"A recruiter is looking for a {search.Post}: back in {Math.Max(0, search.ReadyDay - w.Day)} days", Wide = true, Bar = -1, Tone = Tone.Dim });
            foreach (var c in CandidatesFor(w, s).Take(Math.Max(0, CandidateRows - (rows.Count - 2))))
            {
                string src = c.Source == CandidateSource.Local ? "" : c.Source == CandidateSource.International ? "abroad, " : "other town, ";
                rows.Add(new Row
                {
                    Label = (c.C.Id == s.Selected ? "> " : "") + c.C.Name,
                    Value = $"{src}{c.C.AgeYears(w.Day)}y {c.C.Occupation.ToLower()}  {c.Rating} {c.Suitability}  {Sal(c.Salary)}",
                    Id = Prefix + "cand:" + c.C.Id, Bar = c.Suitability / 100f, PortraitId = c.C.Id,
                    Tone = c.Suitability >= 65 ? Tone.Good : c.Suitability < 35 ? Tone.Dim : Tone.Normal,
                });
            }
            if (rows.Count <= 2 + w.Searches.Count) rows.Add(new Row { Label = "", Value = "No one matches. Try a different source, or search abroad.", Wide = true, Bar = -1, Tone = Tone.Dim });
            return rows;
        }

        // ---------- Details ----------
        public static List<Row> Details(World w, GovernmentUiState s)
        {
            var rows = new List<Row>();
            var c = s.Selected != 0 ? w.Characters.Get(s.Selected) : null;
            if (c == null) return rows;
            const float pad = 120;
            Row P(string l, string v, Tone t = Tone.Normal) => new Row { Label = l, Value = v, Tone = t, Bar = -1, RightPad = pad };
            rows.Add(P(c.Name, $"{c.AgeYears(w.Day)}, {CharacterBalance.StageNames[(int)c.StageAt(w.Day)]}"));
            rows.Add(P(c.Background.ToString(), c.Occupation));
            rows.Add(P("Traits", string.Join(", ", c.Traits.Select(t => Traits.Get(t).Name))));
            rows.Add(P("Status", c.Post != Post.None ? $"{c.Post} ({Sal(c.MonthlySalary)})" : c.Employment == Employment.Available ? "Available" : c.Employment.ToString(), c.Alive ? Tone.Normal : Tone.Bad));
            foreach (Attr a in Enum.GetValues(typeof(Attr)))
            {
                int b = c.Attr[(int)a], e = c.Effective(a);
                rows.Add(new Row { Label = a.ToString(), Value = e == b ? $"{b}" : $"{b} ({(e > b ? "+" : "")}{e - b} traits)", Bar = e / 100f, Tone = e >= 65 ? Tone.Good : e <= 30 ? Tone.Dim : Tone.Normal });
            }
            rows.Add(new Row { Label = "Loyalty", Value = c.Loyalty.ToString(), Bar = c.Loyalty / 100f, Tone = c.Loyalty <= 30 ? Tone.Bad : Tone.Normal });
            rows.Add(new Row { Label = "Experience", Value = $"{c.Experience:0}", Bar = (float)c.Experience / 100f });
            rows.Add(new Row { Label = "Health", Value = c.Health.ToString(), Bar = c.Health / 100f, Tone = c.Health < 40 ? Tone.Warn : Tone.Normal });
            var post = s.Post;
            int suit = Positions.Suitability(c, post);
            rows.Add(new Row { Label = $"As {post}", Value = $"{Positions.Rating(suit)} ({suit}), {Sal(w.SalaryOf(c, post))}", Bar = suit / 100f, Tone = suit >= 65 ? Tone.Good : suit < 35 ? Tone.Bad : Tone.Normal });
            var reasons = Positions.Reasons(c, post, w.Day);
            rows.Add(new Row { Label = "Why", Value = string.Join("; ", reasons.Take(3)), Wide = true, Tone = Tone.Dim, Bar = -1 });
            return rows.Take(ManagementData.MaxRows).ToList();
        }

        // ---------- Payroll ----------
        public static List<Row> Payroll(World w)
        {
            var rows = new List<Row>();
            double treasury = w.ResAmt[Res.Gold]; int payroll = w.MonthlyPayroll;
            rows.Add(Row.Of("Monthly payroll", $"{payroll} gold (treasury {treasury:0})", treasury < payroll ? Tone.Bad : Tone.Normal));
            if (w.LastPayday > 0 && w.LastPayrollDue > 0) rows.Add(Row.Of("Last payday", $"day {w.LastPayday}: paid {w.LastPayrollPaid:0} of {w.LastPayrollDue:0}", w.LastPayrollPaid < w.LastPayrollDue ? Tone.Bad : Tone.Dim));
            foreach (var c in w.Officials.OrderBy(o => o.Post))
                rows.Add(new Row
                {
                    Label = $"{c.Post}: {c.Name}",
                    Value = $"{Sal(c.MonthlySalary)} | {c.Pay} pay | loyalty {c.Loyalty}" + (c.MissedPayments > 0 ? $" | {c.MissedPayments} unpaid" : ""),
                    Id = Prefix + "pay:" + c.Id, Bar = -1, PortraitId = c.Id, Tone = c.MissedPayments > 0 ? Tone.Bad : c.Loyalty <= 30 ? Tone.Warn : Tone.Normal,
                });
            if (rows.Count > 2) rows.Add(new Row { Label = "", Value = "Click a row to change that person's pay: Low (cheaper, loyalty falls), Standard, Generous (dearer, loyalty rises).", Wide = true, Tone = Tone.Dim, Bar = -1 });
            return rows;
        }

        // ---------- commands (the same World operations the rules use) ----------
        /// <summary>Handle a click on a row whose Id starts with "gov:". Returns a message to show the player, or null.</summary>
        public static string Click(World w, GovernmentUiState s, string id, out int goToTab)
        {
            goToTab = -1;
            var parts = id.Substring(Prefix.Length).Split(':');
            switch (parts[0])
            {
                case "post":
                    s.Post = (Post)Enum.Parse(typeof(Post), parts[1]);
                    var holder = w.PostHolder(s.Post);
                    s.Selected = holder?.Id ?? 0; s.Changed();
                    goToTab = holder != null ? 2 : 1;
                    return Positions.LockedReason(s.Post, w.Era);
                case "cand": s.Selected = int.Parse(parts[1]); s.Changed(); goToTab = 2; return null;
                case "pay":
                    var c = w.Characters.Get(int.Parse(parts[1]));
                    if (c == null) return null;
                    var next = (PayLevel)(((int)c.Pay + 1) % 3);
                    w.SetPay(c.Id, next); s.Changed();
                    return $"{c.Name}: {next} pay ({c.MonthlySalary} gold a month).";
            }
            return null;
        }

        /// <summary>Appoint the selected person to the chosen post (replacing the holder if there is one).</summary>
        public static Result AppointSelected(World w, GovernmentUiState s)
        {
            if (s.Selected == 0) return Result.No("Click a candidate first.");
            var r = w.PostHolder(s.Post) != null ? w.Replace(s.Selected, s.Post) : w.Appoint(s.Selected, s.Post);
            if (r.Ok) s.Changed();
            return r;
        }

        public static Result DismissSelected(World w, GovernmentUiState s)
        {
            var c = s.Selected != 0 ? w.Characters.Get(s.Selected) : w.PostHolder(s.Post);
            if (c == null || c.Post == Post.None) return Result.No("Select someone who holds a post.");
            var r = w.Dismiss(c.Id); if (r.Ok) s.Changed();
            return r;
        }

        /// <summary>The settlement a recruiter would visit: the nearest one that will receive them.</summary>
        public static Settlement RecruitingTarget(World w) => w.Settlements.Where(x => x.WillTrade).OrderBy(w.SettlementKm).FirstOrDefault();

        public static Result SearchAbroad(World w, GovernmentUiState s)
        {
            var t = RecruitingTarget(w);
            if (t == null) return Result.No("No settlement will receive your recruiter.");
            var r = w.SearchAbroad(s.Post, t.Id); if (r.Ok) s.Changed();
            return r;
        }
    }
}
