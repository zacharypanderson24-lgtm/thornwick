// The management UI layer (Game/UI): the twelve menus, the manager's Escape order, the message log, and the data the windows show.
using System.Linq;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class ManagementUiTests
    {
        // ---------- menu registry ----------
        [Test]
        public void ThereAreTwelvePrimaryMenusWithDistinctFunctionKeys()
        {
            Assert.AreEqual(12, MenuRegistry.Primary.Count);
            CollectionAssert.AreEqual(Enumerable.Range(1, 12).ToArray(), MenuRegistry.Primary.Select(m => m.FunctionKey).ToArray());
            Assert.AreEqual(12, MenuRegistry.Primary.Select(m => m.Id).Distinct().Count());
            foreach (var m in MenuRegistry.Primary) Assert.AreSame(m, MenuRegistry.ByFunctionKey(m.FunctionKey));
        }

        [Test]
        public void MenusWithNothingBehindThemAreLockedWithAReason()
        {
            var w = new World(7);
            foreach (var id in new[] { MenuId.Technology, MenuId.Government, MenuId.Diplomacy })
                Assert.That(MenuRegistry.WhyLocked(id, w), Is.Not.Null.And.Not.Empty, id.ToString());
            foreach (var id in new[] { MenuId.Settlement, MenuId.Population, MenuId.Economy, MenuId.Construction, MenuId.Agriculture, MenuId.Military, MenuId.Trade, MenuId.World, MenuId.Notifications })
                Assert.That(MenuRegistry.WhyLocked(id, w), Is.Null, id.ToString());
        }

        [Test]
        public void TransportIsLockedWithoutWaterAndOpenWithIt()
        {
            Assert.That(MenuRegistry.WhyLocked(MenuId.Transportation, new World(7)), Is.Not.Null);
            var wet = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside);
            Assert.That(MenuRegistry.WhyLocked(MenuId.Transportation, wet), Is.Null);
        }

        // ---------- manager ----------
        [Test]
        public void ToggleOpensClosesAndRefusesLockedMenus()
        {
            var w = new World(7); var ui = new UIManager();
            Assert.IsTrue(ui.Toggle(MenuId.Economy, w, out _)); Assert.AreEqual(MenuId.Economy, ui.Open);
            Assert.IsTrue(ui.Toggle(MenuId.Economy, w, out _)); Assert.IsNull(ui.Open);
            Assert.IsFalse(ui.Toggle(MenuId.Technology, w, out var why)); Assert.IsNull(ui.Open); Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void SwitchingMenuResetsTheTab()
        {
            var w = new World(7); var ui = new UIManager();
            ui.Toggle(MenuId.Settlement, w, out _); ui.SetTab(1); Assert.AreEqual(1, ui.Tab);
            ui.Toggle(MenuId.Economy, w, out _); Assert.AreEqual(0, ui.Tab);
        }

        [Test]
        public void EscapeClosesADialogThenStopsPlacementThenTheMenu()
        {
            var w = new World(7); var ui = new UIManager();
            ui.Toggle(MenuId.Settlement, w, out _); ui.PushModal("confirm");
            Assert.AreEqual(EscapeResult.ClosedModal, ui.Escape(true)); Assert.AreEqual(MenuId.Settlement, ui.Open);
            Assert.AreEqual(EscapeResult.CancelledPlacement, ui.Escape(true)); Assert.AreEqual(MenuId.Settlement, ui.Open);
            Assert.AreEqual(EscapeResult.ClosedMenu, ui.Escape(false)); Assert.IsNull(ui.Open);
            Assert.AreEqual(EscapeResult.None, ui.Escape(false));
        }

        [Test]
        public void ChangedFiresOnOpenTabAndModal()
        {
            var w = new World(7); var ui = new UIManager(); int n = 0; ui.Changed += () => n++;
            ui.Toggle(MenuId.Settlement, w, out _); ui.SetTab(1); ui.PushModal("a"); ui.PopModal("a"); ui.Close();
            Assert.AreEqual(5, n);
        }

        // ---------- notifications ----------
        [Test]
        public void IdenticalMessagesInsideTheCooldownFoldIntoOne()
        {
            var n = new NotificationManager();
            n.Add(10, "A cave-in!", null, out bool first); n.Add(11, "A cave-in!", null, out bool second);
            Assert.IsTrue(first); Assert.IsFalse(second);
            Assert.AreEqual(1, n.History.Count); Assert.AreEqual(2, n.History[0].Count); Assert.AreEqual(1, n.Unread);
            n.Add(11 + NotificationManager.CooldownDays + 1, "A cave-in!", null, out bool later); Assert.IsTrue(later); Assert.AreEqual(2, n.History.Count);
        }

        [Test]
        public void MessagesAreSortedByWording()
        {
            Assert.AreEqual(NoteKind.Danger, NotificationManager.Classify("Raiders! A band of 12 is coming in from the north."));
            Assert.AreEqual(NoteKind.Warning, NotificationManager.Classify("A cave-in at the mine."));
            Assert.AreEqual(NoteKind.Success, NotificationManager.Classify("Campaign complete."));
            Assert.AreEqual(NoteKind.Info, NotificationManager.Classify("The harvest begins."));
        }

        [Test]
        public void HistoryIsCappedAndUnreadCountsStayTrue()
        {
            var n = new NotificationManager();
            for (int i = 0; i < NotificationManager.MaxHistory + 20; i++) n.Add(i * 10, "message " + i, NoteKind.Info, out _);
            Assert.AreEqual(NotificationManager.MaxHistory, n.History.Count);
            Assert.AreEqual(NotificationManager.MaxHistory, n.Unread);
            n.MarkAllRead(); Assert.AreEqual(0, n.Unread);
            n.Add(9999, "message 0", NoteKind.Info, out _); Assert.AreEqual(1, n.Unread);
            n.Clear(); Assert.AreEqual(0, n.History.Count); Assert.AreEqual(0, n.Unread);
        }

        // ---------- the data the windows show ----------
        static string Value(System.Collections.Generic.List<Row> rows, string label) => rows.First(r => r.Label == label).Value;

        [Test]
        public void SettlementRowsAreTheWorldsOwnNumbers()
        {
            var w = new World(7); w.StartCampaign(); w.Peasants = 24; w.Day = 50; w.Tick();
            var rows = ManagementData.SettlementOverview(w);
            Assert.AreEqual(w.Peasants.ToString(), Value(rows, "Settlers"));
            Assert.AreEqual($"{w.D.Employed} of {w.D.Jobs} jobs filled", Value(rows, "Working"));
            Assert.AreEqual(w.D.Idle.ToString(), Value(rows, "Without work"));
            Assert.That(Value(rows, "Mood"), Does.StartWith(w.MoodName));
            var def = ManagementData.SettlementDefence(w);
            Assert.AreEqual(w.Soldiers.ToString(), Value(def, "Soldiers")); Assert.AreEqual(w.Era.ToString(), Value(def, "Era"));
        }

        [Test]
        public void StockpileRowsListEveryResourceWithItsRealAmount()
        {
            var w = new World(7); w.ResAmt[Res.Stone] = 123.7;
            var rows = ManagementData.Stockpiles(w);
            Assert.AreEqual(System.Enum.GetValues(typeof(Res)).Length, rows.Count);
            Assert.That(Value(rows, "Stone"), Does.StartWith("123"));
            Assert.That(Value(rows, "Gold"), Does.StartWith(System.Math.Floor(w.ResAmt[Res.Gold]).ToString()));
        }

        [Test]
        public void MessageRowsShowNewestFirstAndWarningsCanBeFiltered()
        {
            var n = new NotificationManager();
            n.Add(1, "The harvest begins.", NoteKind.Info, out _); n.Add(2, "A cave-in!", NoteKind.Warning, out _);
            var all = ManagementData.Messages(n, false);
            Assert.AreEqual("Day 2", all[0].Label); Assert.AreEqual(2, all.Count);
            var warn = ManagementData.Messages(n, true);
            Assert.AreEqual(1, warn.Count); Assert.AreEqual("A cave-in!", warn[0].Value);
            Assert.That(ManagementData.EmptyText(MenuId.Notifications, 1), Is.Not.Empty);
        }

        [Test]
        public void EveryWindowedMenuHasTabsAndRowsWithoutThrowing()
        {
            var w = new World(7); var n = new NotificationManager();
            foreach (var id in new[] { MenuId.Settlement, MenuId.Economy, MenuId.Notifications })
                for (int t = 0; t < ManagementData.Tabs(id).Length; t++)
                    Assert.DoesNotThrow(() => ManagementData.Rows(id, t, w, n), id + " tab " + t);
        }
    }
}
