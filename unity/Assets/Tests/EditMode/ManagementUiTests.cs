// The management UI layer (Game/UI): the twelve menus, the manager's Escape order, the message log, and the data the windows show.
using System.Linq;
using CrownAndHearth.Game;
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

        // ---------- phase 2: hosted screens, jobs, housing, the construction hub, paging ----------
        [Test]
        public void HostedTabsHaveNoRowsOfTheirOwn()
        {
            var w = new World(7); var n = new NotificationManager();
            Assert.AreEqual(Host.Workforce, ManagementData.TabDefs(MenuId.Population)[0].Host);
            Assert.IsEmpty(ManagementData.Rows(MenuId.Population, 0, w, n));
            Assert.AreEqual(Host.Mining, ManagementData.TabDefs(MenuId.Economy)[1].Host);
            Assert.AreEqual(Host.Statistics, ManagementData.TabDefs(MenuId.Settlement)[2].Host);
            Assert.AreEqual(2, ManagementData.TabDefs(MenuId.Agriculture).Count(t => t.Host != Host.None));
        }

        [Test]
        public void JobsAndHousingRowsAreTheWorldsNumbers()
        {
            var w = new World(7); w.StartCampaign(); w.Peasants = 20; w.Day = 40; w.Tick();
            var jobs = ManagementData.Jobs(w);
            Assert.AreEqual(w.Prio.Length, jobs.Count);
            foreach (var t in w.Prio) { var (workers, total, _, _) = w.D.Work[t]; StringAssert.StartsWith($"{workers} of {total} jobs", jobs.First(r => r.Label == Defs.TradeNames[t]).Value); }
            var housing = ManagementData.Housing(w);
            Assert.AreEqual(w.Peasants.ToString(), Value(housing, "Settlers"));
            Assert.AreEqual(w.D.Bunks.ToString(), Value(housing, "Barracks bunks"));
        }

        [Test]
        public void ConstructionHubListsEveryToolOfACategoryAndMarksWhatTheTownCannotPay()
        {
            var w = new World(7); w.ResAmt[Res.Wood] = 0; w.ResAmt[Res.Stone] = 0; w.ResAmt[Res.Gold] = 0;
            var cats = GameController.Cats.Where(c => c.Cat != GameController.Cat.Inspect).ToArray();
            Assert.AreEqual(cats.Length, ManagementData.TabDefs(MenuId.Construction).Length);
            for (int i = 0; i < cats.Length; i++)
            {
                var rows = ManagementData.ConstructionTools(w, i);
                var expected = GameController.Tools.Where(t => t.Cat == cats[i].Cat).Select(t => t.Id).Take(ManagementData.MaxRows).ToArray();
                CollectionAssert.AreEqual(expected, rows.Select(r => r.Id).ToArray(), cats[i].Label);
            }
            var farm = ManagementData.ConstructionTools(w, System.Array.FindIndex(cats, c => c.Cat == GameController.Cat.Farming));
            Assert.AreEqual(Tone.Bad, farm.First(r => r.Id == "farmhouse").Tone);
        }

        [Test]
        public void MessagePagesRunNewestToOldest()
        {
            var n = new NotificationManager();
            for (int i = 0; i < ManagementData.MaxRows + 4; i++) n.Add(i * 10, "message " + i, NoteKind.Info, out _);
            var p0 = ManagementData.Messages(n, false, 0); var p1 = ManagementData.Messages(n, false, 1);
            Assert.AreEqual(ManagementData.MaxRows, p0.Count); Assert.AreEqual(4, p1.Count);
            Assert.AreEqual("message 19", p0[0].Value); Assert.AreEqual("message 3", p1[0].Value);
            Assert.AreEqual(ManagementData.MaxRows + 4, ManagementData.MessageCount(n, false));
        }

        [Test]
        public void TheHudBuildsAndEveryMenuAndTabOpens()
        {
            var go = new UnityEngine.GameObject("HudTest"); var hud = go.AddComponent<CrownAndHearth.Game.Hud>();
            try
            {
                var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside); w.StartCampaign(); w.Tick();
                hud.Build(GameController.Tools, GameController.Cats, new ArtLibrary());
                hud.SetSettlement(w);
                foreach (var id in MenuRegistry.Primary.Select(m => m.Id).Concat(new[] { MenuId.Notifications, MenuId.System }))
                {
                    if (MenuRegistry.WhyLocked(id, w) != null) continue;
                    hud.ToggleMenu(id); hud.SetSettlement(w);
                    if (hud.UI.Open == id)
                        for (int t = 0; t < ManagementData.Tabs(id).Length; t++) { hud.UI.SetTab(t); hud.SetSettlement(w); }
                    hud.ToggleMenu(id);
                }
                Assert.IsNull(hud.UI.Open);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var es in UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(es.gameObject);
            }
        }

        // ---------- phase 3: transport and trade windows ----------
        [Test]
        public void TransportAndTradeRowsAreTheWorldsOwnNumbers()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside); w.StartCampaign(); w.Tick();
            Assert.AreEqual(3, ManagementData.Tabs(MenuId.Transportation).Length);
            Assert.AreEqual(w.FerryRoutes.Count, ManagementData.Routes(w).Count);
            Assert.AreEqual(w.Vessels.Count, ManagementData.Boats(w).Count);
            var orders = ManagementData.Orders(w);
            Assert.AreEqual(1 + w.VesselOrders.Count, orders.Count); Assert.AreEqual("Boatyards", orders[0].Label);
            var cap = ManagementData.TradeCapacity(w);
            Assert.AreEqual(w.Markets.ToString(), Value(cap, "Markets on the road network"));
            Assert.AreEqual($"{w.ShipsOut} of {w.ShipCapacity}", Value(cap, "Ships out"));
            Assert.AreEqual(w.Caravans.Count, ManagementData.Caravans(w).Count);
            Assert.That(ManagementData.EmptyText(MenuId.Transportation, 0), Is.Not.Empty);
        }

        [Test]
        public void AFerryRouteAndABoatShowTheirStateAndCondition()
        {
            var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside); w.StartCampaign();
            w.Vessels.Add(new Vessel { Id = 77, Type = VesselType.FerryBoat, Condition = 10 });
            var boats = ManagementData.Boats(w);
            Assert.AreEqual(1, boats.Count); StringAssert.Contains("out of service", boats[0].Value); Assert.AreEqual(Tone.Bad, boats[0].Tone);
            Assert.AreEqual(0.1f, boats[0].Bar, 0.001f);
            Assert.AreEqual("running", ManagementData.FerryStateText(FerryState.Operational));
        }

        // ---------- phase 5: overlays ----------
        [Test]
        public void OverlaysListOnlyThingsWithDataAndSailingNeedsWater()
        {
            CollectionAssert.AreEqual(new[] { OverlayId.Underground, OverlayId.Water, OverlayId.Fertility, OverlayId.Sailing }, OverlayRegistry.All.Select(o => o.Id).ToArray());
            var dry = new World(7); var wet = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside);
            Assert.That(OverlayRegistry.WhyLocked(OverlayId.Sailing, dry), Is.Not.Null.And.Not.Empty);
            Assert.That(OverlayRegistry.WhyLocked(OverlayId.Sailing, wet), Is.Null);
            foreach (var id in new[] { OverlayId.None, OverlayId.Underground, OverlayId.Water, OverlayId.Fertility }) Assert.That(OverlayRegistry.WhyLocked(id, dry), Is.Null, id.ToString());
            Assert.AreEqual("Off", OverlayRegistry.TitleOf(OverlayId.None));
            foreach (var o in OverlayRegistry.All) { Assert.That(o.Title, Is.Not.Empty); Assert.That(o.Hint, Is.Not.Empty); }
        }

        // ---------- phase 6: the System menu and settings ----------
        [Test]
        public void SettingsRoundTripAndBrokenTextKeepsDefaults()
        {
            var s = new UiSettings { ScaleIndex = 2, PauseOnMenu = false };
            var back = UiSettings.Parse(s.Serialize());
            Assert.AreEqual(2, back.ScaleIndex); Assert.IsFalse(back.PauseOnMenu); Assert.AreEqual(115, back.Percent);
            var bad = UiSettings.Parse("scale=99;pause=x;junk");
            Assert.AreEqual(UiSettings.Scales.Length - 1, bad.ScaleIndex); Assert.IsTrue(bad.PauseOnMenu);
            Assert.AreEqual(1, UiSettings.Parse(null).ScaleIndex); Assert.AreEqual(1f, UiSettings.Parse("").Scale);
            var c = new UiSettings(); for (int k = 0; k < UiSettings.Scales.Length; k++) c.CycleScale(); Assert.AreEqual(1, c.ScaleIndex, "cycling comes back round");
        }

        [Test]
        public void TheSystemMenuHasGameControlsAndSettingsAndIsAlwaysOpenable()
        {
            var w = new World(7);
            Assert.AreEqual(3, ManagementData.Tabs(MenuId.System).Length);
            Assert.IsNull(MenuRegistry.WhyLocked(MenuId.System, w));
            Assert.IsFalse(MenuRegistry.Primary.Any(m => m.Id == MenuId.System), "it is not one of the twelve");
            var game = ManagementData.SystemGame();
            CollectionAssert.AreEqual(new[] { "cmd:resume", "cmd:save", "cmd:load", "cmd:newworld", "cmd:banner" }, game.Select(r => r.Id).ToArray());
            Assert.IsTrue(ManagementData.SystemControls().All(r => r.Id == null && r.Value.Length > 0));
            Assert.IsTrue(ManagementData.SystemControls().Any(r => r.Label.StartsWith("F1")) && ManagementData.SystemControls().Any(r => r.Label == "Esc"));
            var set = ManagementData.SystemSettings(new UiSettings { ScaleIndex = 0, PauseOnMenu = false });
            StringAssert.StartsWith("85%", Value(set, "Interface size")); StringAssert.StartsWith("off", Value(set, "Pause when this menu opens"));
            Assert.AreEqual(set.Count, ManagementData.Rows(MenuId.System, 2, w, new NotificationManager(), 0, new UiSettings { ScaleIndex = 0, PauseOnMenu = false }).Count);
        }
    }
}
