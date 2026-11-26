using System.Collections.Generic;
using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Game.UI;
using CrownAndHearth.Sim;
using NUnit.Framework;
using UnityEngine;

namespace CrownAndHearth.Tests
{
    // Clicks go through the real Hud: its canvas's GraphicRaycaster finds what is under the point and the click handler it finds runs (UiHarness).
    public class GovernmentClickTests
    {
        GameObject _go; Hud _hud; Camera _cam; World _w;

        [SetUp]
        public void Up()
        {
            _w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.Lakeside); _w.StartCampaign(); _w.Tick(); _w.Era = 3; _w.ResAmt[Res.Gold] = 500;
            for (int i = 0; i < 8; i++) _w.Characters.Add(CharacterGenerator.Create(_w.Characters.NextId, _w.CharacterPool(), 7000 + i, _w.Day, (Background)(i % 7), 25, 55));
            _go = new GameObject("HudClickTest"); _hud = _go.AddComponent<Hud>();
            _hud.Build(GameController.Tools, GameController.Cats, new ArtLibrary());
            _hud.SetSettlement(_w);
            _hud.RefreshWorkforce(_w); _hud.RefreshTownMenus(_w);   // the game does these every frame
            _cam = UiHarness.Attach(_go, 1920, 1080);
        }

        [TearDown]
        public void Down() { UiHarness.Detach(_go, _cam); Object.DestroyImmediate(_go); }

        int FirstRow(string prefix)
        {
            for (int i = 0; i < _hud.ManagementRowCount; i++)
                if (_hud.ManagementRowShown(i) && _hud.ManagementRowId(i) != null && _hud.ManagementRowId(i).StartsWith(prefix)) return i;
            return -1;
        }

        [Test]
        public void ClickingACandidateRowSelectsThemAndOpensTheirDetails()
        {
            _hud.OpenGovernment(1, Post.Treasurer); _hud.SetSettlement(_w);
            int row = FirstRow("gov:cand:"); Assert.That(row, Is.GreaterThanOrEqualTo(0), "the Recruit tab lists candidates");
            int id = int.Parse(_hud.ManagementRowId(row).Substring("gov:cand:".Length));
            Assert.That(_hud.ManagementRowThumbShown(row), Is.True, "each candidate row carries a portrait thumbnail");
            var res = UiHarness.ClickCentre(_hud.HudCanvas, _cam, _hud.ManagementRow(row));
            Assert.That(res.Delivered, Is.True, "a click on the row reaches a handler: " + res);
            _hud.SetSettlement(_w);
            Assert.That(_hud.GovernmentSelected, Is.EqualTo(id));
            Assert.That(_hud.UI.Tab, Is.EqualTo(2), "it jumps to Details");
            Assert.That(_hud.PortraitShown, Is.True, "with the portrait");
        }

        [Test]
        public void ClickingACouncilPostOpensRecruitOrTheHolder()
        {
            _hud.OpenGovernment(0, Post.Chancellor); _hud.SetSettlement(_w);
            int row = FirstRow("gov:post:Treasurer"); Assert.That(row, Is.GreaterThanOrEqualTo(0));
            Assert.That(UiHarness.ClickCentre(_hud.HudCanvas, _cam, _hud.ManagementRow(row)).Delivered, Is.True);
            _hud.SetSettlement(_w);
            Assert.That(_hud.UI.Tab, Is.EqualTo(1), "a vacant post opens the recruit list");
        }

        [Test]
        public void ConstructionToolRowsAreClickableToo()
        {
            _hud.ToggleMenu(MenuId.Construction); _hud.SetSettlement(_w);
            int row = -1;
            for (int i = 0; i < _hud.ManagementRowCount; i++) if (_hud.ManagementRowShown(i) && !string.IsNullOrEmpty(_hud.ManagementRowId(i))) { row = i; break; }
            Assert.That(row, Is.GreaterThanOrEqualTo(0));
            Assert.That(UiHarness.ClickCentre(_hud.HudCanvas, _cam, _hud.ManagementRow(row)).Delivered, Is.True);
        }
    }
}
