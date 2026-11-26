// The town's message log. Every World event goes in here (nothing is lost when the toast fades), is sorted into a category, and a repeat of the
// same message inside its cooldown is folded into one entry instead of flooding the list. Pure C#, tested directly.
using System;
using System.Collections.Generic;

namespace CrownAndHearth.Game.UI
{
    public enum NoteKind { Info, Success, Warning, Danger }

    public sealed class Note
    {
        public int Day; public string Text; public NoteKind Kind; public int Count = 1; public bool Read;
    }

    public sealed class NotificationManager
    {
        public const int MaxHistory = 100, CooldownDays = 3;
        readonly List<Note> _notes = new List<Note>();   // oldest first
        public IReadOnlyList<Note> History => _notes;
        public int Unread { get; private set; }

        /// <summary>The kind from the wording of a World event (events are plain sentences, there is no event type yet).</summary>
        public static NoteKind Classify(string text)
        {
            string t = text.ToLowerInvariant();
            if (t.Contains("raider") || t.Contains("starv") || t.Contains("collapse") || t.Contains("was lost") || t.Contains("burn") || t.Contains("attack") || t.Contains("here! watch")) return NoteKind.Danger;
            if (t.Contains("shortage") || t.Contains("cave") || t.Contains("run out") || t.Contains("empty") || t.Contains("nothing") || t.Contains("cannot") || t.Contains("unrest") || t.Contains("sunk") || t.Contains("worn")) return NoteKind.Warning;
            if (t.Contains("complete") || t.Contains("destroyed") || t.Contains("victory") || t.Contains("reached") || t.Contains("new level") || t.Contains("finished") || t.Contains("built")) return NoteKind.Success;
            return NoteKind.Info;
        }

        /// <summary>The three levels the player sees: Critical (Danger), Warning, Info (everything else).</summary>
        public static string SeverityLabel(NoteKind k) => k == NoteKind.Danger ? "Critical" : k == NoteKind.Warning ? "Warning" : "Info";

        /// <summary>The management menu a message is about, or null when it is not clearly about one. Words only: events are plain sentences.</summary>
        public static MenuId? LinkFor(string text)
        {
            string t = text.ToLowerInvariant();
            if (t.Contains("raider") || t.Contains("warband") || t.Contains("garrison") || t.Contains("soldier") || t.Contains("attack")) return MenuId.Military;
            if (t.Contains("starv") || t.Contains("famine") || t.Contains("food") || t.Contains("water") || t.Contains("thirst") || t.Contains("dehydrat")) return MenuId.Settlement;
            if (t.Contains("boat") || t.Contains("ferry") || t.Contains("vessel")) return MenuId.Transportation;
            if (t.Contains("caravan") || t.Contains("trade") || t.Contains("ship")) return MenuId.Trade;
            if (t.Contains("salary") || t.Contains("council") || t.Contains("appoint")) return MenuId.Government;
            return null;
        }

        /// <summary>Add a message. Returns the entry, and whether it is new (false: folded into an earlier identical one inside the cooldown).</summary>
        public Note Add(int day, string text, NoteKind? kind, out bool isNew)
        {
            for (int i = _notes.Count - 1; i >= 0; i--)
            {
                var n = _notes[i];
                if (day - n.Day > CooldownDays) break;
                if (n.Text == text) { n.Count++; n.Day = day; if (n.Read) { n.Read = false; Unread++; } isNew = false; return n; }
            }
            var note = new Note { Day = day, Text = text, Kind = kind ?? Classify(text) };
            _notes.Add(note); Unread++;
            if (_notes.Count > MaxHistory) { if (!_notes[0].Read) Unread--; _notes.RemoveAt(0); }
            isNew = true; return note;
        }

        public void MarkAllRead() { foreach (var n in _notes) n.Read = true; Unread = 0; }
        public void Clear() { _notes.Clear(); Unread = 0; }
    }
}
