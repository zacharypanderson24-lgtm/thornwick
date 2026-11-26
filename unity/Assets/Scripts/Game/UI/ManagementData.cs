// What the management windows show, read straight from the simulation. Pure C# (no UnityEngine): every number here is a World value or a plain
// sum of World values, nothing is invented, and the tests compare the rows against the World directly.
using System;
using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    public enum Tone { Normal, Good, Warn, Bad, Dim }

    /// <summary>An existing screen that a tab hosts (the Hud moves it inside the window), or None for a tab made of rows.</summary>
    public enum Host { None, Workforce, Farming, Mining, Statistics, Livestock }

    public struct TabDef { public string Title; public Host Host; }

    public struct Row
    {
        public string Label, Value;
        public float Bar;       // 0..1, or -1 for none
        public Tone Tone;
        public bool Wide;       // a sentence: left-aligned value, narrow label column
        public string Id;       // a tool id: clicking the row picks that tool (Construction)
        public float RightPad;  // keep the value this far from the right edge (room for a portrait)
        public int PortraitId;  // a character id: show that person's portrait as a thumbnail at the left of the row
        public static Row Of(string label, string value, Tone tone = Tone.Normal, float bar = -1f) => new Row { Label = label, Value = value, Tone = tone, Bar = bar };
    }

    public static class ManagementData
    {
        public const int MaxRows = 16;

        static TabDef T(string title, Host host = Host.None) => new TabDef { Title = title, Host = host };

        public static TabDef[] TabDefs(MenuId id) => id switch
        {
            MenuId.Settlement => new[] { T("Overview"), T("Defence"), T("History", Host.Statistics) },
            MenuId.Population => new[] { T("Workforce", Host.Workforce), T("Jobs"), T("Housing") },
            MenuId.Economy => new[] { T("Stockpiles"), T("Mining", Host.Mining) },
            MenuId.Agriculture => new[] { T("Farming", Host.Farming), T("Livestock", Host.Livestock) },
            MenuId.Transportation => new[] { T("Routes"), T("Boats"), T("Orders") },
            MenuId.Trade => new[] { T("On the road"), T("Capacity") },
            MenuId.Construction => GameController.Cats.Where(c => c.Cat != GameController.Cat.Inspect).Select(c => T(c.Label)).ToArray(),
            MenuId.Government => GovernmentData.Tabs.Select(t => T(t)).ToArray(),
            MenuId.Notifications => new[] { T("All"), T("Warnings") },
            MenuId.System => new[] { T("Game"), T("Controls"), T("Settings") },
            _ => new[] { T("Overview") },
        };

        public static string[] Tabs(MenuId id) => TabDefs(id).Select(t => t.Title).ToArray();

        public static string ResName(Res r) => r switch { Res.Food => "Wheat", Res.SmokedMeat => "Smoked meat", Res.Ore => "Iron ore", _ => r.ToString() };

        public static List<Row> Rows(MenuId id, int tab, World w, NotificationManager notes, int page = 0, UiSettings settings = null, GovernmentUiState gov = null)
        {
            var defs = TabDefs(id);
            if (tab < 0 || tab >= defs.Length || defs[tab].Host != Host.None) return new List<Row>();
            switch (id)
            {
                case MenuId.Settlement: return tab == 0 ? SettlementOverview(w) : SettlementDefence(w);
                case MenuId.Population: return tab == 1 ? Jobs(w) : Housing(w);
                case MenuId.Economy: return Stockpiles(w);
                case MenuId.Construction: return ConstructionTools(w, tab);
                case MenuId.Transportation: return tab == 0 ? Routes(w) : tab == 1 ? Boats(w) : Orders(w);
                case MenuId.Trade: return tab == 0 ? Caravans(w) : TradeCapacity(w);
                case MenuId.Government: return GovernmentData.Rows(w, tab, gov ?? new GovernmentUiState());
                case MenuId.Notifications: return Messages(notes, tab == 1, page);
                case MenuId.System: return tab == 0 ? SystemGame() : tab == 1 ? SystemControls() : SystemSettings(settings ?? new UiSettings());
                default: return new List<Row>();
            }
        }

        public static string EmptyText(MenuId id, int tab) => id switch
        {
            MenuId.Government => tab == 2 ? "Nobody selected. Click a post on the Council tab or a candidate on the Recruit tab." : GovernmentData.EmptyText(tab),
            MenuId.Notifications => tab == 1 ? "No warnings. Nothing needs your attention." : "Nothing has happened yet.",
            MenuId.Transportation => tab == 0 ? "No ferry routes. Build two landings and link them with the Ferry Route tool." : tab == 1 ? "No boats yet. Order some at a Boatyard." : "No orders waiting.",
            MenuId.Trade => "No caravans or ships on the road. Markets send caravans to the settlements on the world map.",
            _ => "Nothing to show.",
        };

        public static string FerryStateText(FerryState s) => s switch
        {
            FerryState.Operational => "running",
            FerryState.WaitingForCrew => "waiting for a crew",
            FerryState.SuspendedByPlayer => "suspended by you",
            FerryState.FrozenRoute => "frozen",
            FerryState.MissingLanding => "a landing is missing",
            FerryState.NoRoute => "no sailing route",
            FerryState.MissingVessel => "no ferry boat",
            _ => "boat under repair",
        };

        /// <summary>Every ferry route: how long, how often it sails, whether it runs, and who may use it.</summary>
        public static List<Row> Routes(World w)
        {
            var rows = new List<Row>();
            foreach (var r in w.FerryRoutes.Take(MaxRows))
            {
                var state = w.StateOf(r); var (ea, eb) = w.Ends(r); var sail = ea != null && eb != null ? w.SailingRoute(ea, eb) : null; int len = sail == null ? 0 : sail.Count - 1;
                string text = $"{len} tiles, {World.TripsPerDay(Math.Max(1, len))} crossings a day, {FerryStateText(state)}";
                rows.Add(Row.Of($"Landing {r.AX},{r.AY} to {r.BX},{r.BY}", text, state == FerryState.Operational ? Tone.Good : Tone.Warn));
            }
            return rows;
        }

        /// <summary>Every vessel the town owns, with its condition and where it works.</summary>
        public static List<Row> Boats(World w)
        {
            var rows = new List<Row>();
            foreach (var v in w.Vessels.Take(MaxRows))
            {
                bool ok = w.InService(v);
                rows.Add(Row.Of($"#{v.Id} {World.Spec(v.Type).Name}", $"{v.Condition:0}%{(ok ? "" : " (out of service)")}, {w.AssignmentText(v)}", ok ? Tone.Normal : Tone.Bad, (float)Math.Clamp(v.Condition / 100.0, 0, 1)));
            }
            return rows;
        }

        /// <summary>The boatyard's state and the orders waiting, in build order.</summary>
        public static List<Row> Orders(World w)
        {
            var rows = new List<Row>();
            rows.Add(Row.Of("Boatyards", $"{w.Boatyards} on the road network, {w.YardCapacity:0.#} crews at work", w.Boatyards == 0 ? Tone.Warn : Tone.Normal));
            for (int i = 0; i < w.VesselOrders.Count && rows.Count < MaxRows; i++)
                rows.Add(Row.Of($"{i + 1}. {World.Spec(w.VesselOrders[i].Type).Name}", w.OrderStatus(w.VesselOrders[i])));
            return rows;
        }

        /// <summary>Caravans and ships on the road, soonest arrival first.</summary>
        public static List<Row> Caravans(World w)
        {
            var rows = new List<Row>();
            foreach (var c in w.Caravans.OrderBy(c => c.ArriveDay).Take(MaxRows))
            {
                var to = w.SettlementById(c.SettlementId); string where = to?.Name ?? "?"; int days = Math.Max(0, c.ArriveDay - w.Day);
                string text = c.Kind switch
                {
                    CaravanKind.Buy => $"{c.Amount} {World.GoodName(c.Res)} coming from {where} {World.ModeName(c.Mode)}",
                    CaravanKind.Sell => $"{c.Amount} {World.GoodName(c.Res)} going to {where} {World.ModeName(c.Mode)} for {c.Gold} gold",
                    _ => $"A gift of {c.Gold} gold going to {where}",
                };
                rows.Add(new Row { Label = $"{days} day{(days == 1 ? "" : "s")}", Value = text, Bar = -1f, Wide = true });
            }
            return rows;
        }

        /// <summary>The game commands of the System menu. A row with an Id starting "cmd:" is a button; the Hud runs it.</summary>
        public static List<Row> SystemGame() => new List<Row>
        {
            new Row { Label = "Resume", Value = "close this menu", Bar = -1f, Id = "cmd:resume" },
            new Row { Label = "Save game", Value = "save to this computer", Bar = -1f, Id = "cmd:save" },
            new Row { Label = "Load game", Value = "load the saved game", Bar = -1f, Id = "cmd:load" },
            new Row { Label = "New world", Value = "start again on a new map", Bar = -1f, Id = "cmd:newworld" },
            new Row { Label = "Banner colour", Value = "choose your banner", Bar = -1f, Id = "cmd:banner" },
        };

        /// <summary>Every shortcut the game answers to.</summary>
        public static List<Row> SystemControls() => new List<Row>
        {
            Row.Of("F1 to F12", "open the twelve management menus"),
            Row.Of("1 to 9, L, 0", "build categories (press again to cycle the tools)"),
            Row.Of("Q and E", "turn the view"),
            Row.Of("Space", "pause or resume"),
            Row.Of("U", "underground view, press again for deeper"),
            Row.Of("M", "the world map"),
            Row.Of("T", "the military window"),
            Row.Of("N", "messages"),
            Row.Of("Right-click", "stop placing"),
            Row.Of("Esc", "close a dialog, stop placing, close a menu; with nothing open it opens this menu"),
        };

        public static List<Row> SystemSettings(UiSettings s) => new List<Row>
        {
            new Row { Label = "Interface size", Value = $"{s.Percent}%   (click to change)", Bar = -1f, Id = "cmd:scale" },
            new Row { Label = "Pause when this menu opens", Value = (s.PauseOnMenu ? "on" : "off") + "   (click to change)", Bar = -1f, Id = "cmd:pause" },
        };

        public static List<Row> TradeCapacity(World w)
        {
            var rows = new List<Row>();
            rows.Add(Row.Of("Markets on the road network", w.Markets.ToString(), w.Markets == 0 ? Tone.Warn : Tone.Normal));
            rows.Add(Row.Of("Land caravans out", $"{w.LandCaravansOut} of {w.Markets}"));
            rows.Add(Row.Of("Ships out", $"{w.ShipsOut} of {w.ShipCapacity}"));
            return rows;
        }

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

        /// <summary>One row per trade, in priority order: how many work it against the jobs it offers.</summary>
        public static List<Row> Jobs(World w)
        {
            var rows = new List<Row>();
            foreach (var t in w.Prio)
            {
                var (workers, jobs, blds, shortCount) = w.D.Work.TryGetValue(t, out var r) ? r : (0, 0, 0, 0);
                string name = Defs.TradeNames.TryGetValue(t, out var n) ? n : t;
                string text = $"{workers} of {jobs} jobs" + (blds > 0 ? $", {blds} building{(blds == 1 ? "" : "s")}" : "") + (shortCount > 0 ? $", {shortCount} short" : "");
                rows.Add(Row.Of(name, text, shortCount > 0 ? Tone.Warn : Tone.Normal, jobs > 0 ? (float)Math.Min(1.0, (double)workers / jobs) : -1f));
            }
            return rows;
        }

        public static List<Row> Housing(World w)
        {
            var rows = new List<Row>();
            rows.Add(Row.Of("Settlers", w.Peasants.ToString()));
            rows.Add(Row.Of("Homes hold", $"{w.D.Civ} people"));
            int free = w.D.Civ - w.Peasants;
            rows.Add(Row.Of(free >= 0 ? "Room to spare" : "Over capacity", Math.Abs(free).ToString(), free < 0 ? Tone.Bad : Tone.Normal));
            rows.Add(Row.Of("Barracks bunks", w.D.Bunks.ToString()));
            rows.Add(Row.Of("Soldiers", w.Soldiers.ToString()));
            return rows;
        }

        public static string CostOf(string type) =>
            string.Join(", ", Defs.CostOf(type, Defs.PlacementSize(type)).Select(kv => $"{kv.Value} {kv.Key.ToString().ToLower()}"));

        /// <summary>The tools of one build category (the tab index counts the categories without Inspect). A building shows its cost, red when the town cannot pay it.</summary>
        public static List<Row> ConstructionTools(World w, int tab)
        {
            var rows = new List<Row>();
            var cats = GameController.Cats.Where(c => c.Cat != GameController.Cat.Inspect).ToArray();
            if (tab < 0 || tab >= cats.Length) return rows;
            foreach (var t in GameController.Tools.Where(t => t.Cat == cats[tab].Cat))
            {
                if (rows.Count >= MaxRows) break;
                bool pays = t.Build == null || w.CanAfford(t.Build).Ok;
                rows.Add(new Row { Label = t.Label, Value = t.Build != null ? CostOf(t.Build) : "drag over the land", Tone = pays ? Tone.Normal : Tone.Bad, Bar = -1f, Id = t.Id });
            }
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

        static bool Matches(Note n, bool warningsOnly) => !warningsOnly || n.Kind == NoteKind.Warning || n.Kind == NoteKind.Danger;

        public static int MessageCount(NotificationManager notes, bool warningsOnly) => notes.History.Count(n => Matches(n, warningsOnly));

        /// <summary>Messages newest first; page 0 is the latest MaxRows, page 1 the ones before them, and so on.</summary>
        public static List<Row> Messages(NotificationManager notes, bool warningsOnly, int page = 0)
        {
            var rows = new List<Row>();
            int skip = Math.Max(0, page) * MaxRows;
            for (int i = notes.History.Count - 1; i >= 0 && rows.Count < MaxRows; i--)
            {
                var n = notes.History[i];
                if (!Matches(n, warningsOnly)) continue;
                if (skip > 0) { skip--; continue; }
                var tone = n.Kind == NoteKind.Danger ? Tone.Bad : n.Kind == NoteKind.Warning ? Tone.Warn : n.Kind == NoteKind.Success ? Tone.Good : Tone.Normal;
                rows.Add(new Row { Label = $"Day {n.Day}", Value = n.Count > 1 ? $"{n.Text} (x{n.Count})" : n.Text, Tone = tone, Bar = -1f, Wide = true });
            }
            return rows;
        }
    }
}
