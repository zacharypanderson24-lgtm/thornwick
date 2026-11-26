// The New world screen: a large panel to pick the surface water of the next world (dry as before, or one of the nine water presets) and
// to type the seed exactly. Creating a world restarts the game on it; the current settlement is kept only if it was saved.
using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        public event Action<WaterPreset, int> NewWorldPicked;
        RectTransform _newWorldPanel; InputField _newWorldSeed; Text _newWorldNote; WaterPreset _newWorldWater = WaterPreset.None;
        readonly Image[] _newWorldButtons = new Image[10];

        static readonly (WaterPreset preset, string label, string note)[] WaterChoices =
        {
            (WaterPreset.None, "Dry land", "The land as it always was: no rivers, lakes or sea."),
            (WaterPreset.BalancedInland, "Balanced inland", "Several streams, ponds and a lake or two; no sea."),
            (WaterPreset.RiverValley, "River valley", "A major river with tributaries and fertile land beside it."),
            (WaterPreset.Lakeside, "Lakeside", "A big freshwater lake with rivers running in and out."),
            (WaterPreset.Coastal, "Coastal", "The sea along one side, with bays, capes and river mouths."),
            (WaterPreset.Island, "Island", "Your town on an island, surrounded by sea."),
            (WaterPreset.Archipelago, "Archipelago", "A cluster of islands in open sea."),
            (WaterPreset.Wetlands, "Wetlands", "Many shallow streams, ponds and marsh; flat and wet."),
            (WaterPreset.AridOasis, "Arid oasis", "Dry land with little water: a pond and a thin stream."),
            (WaterPreset.MountainWatershed, "Mountain watershed", "High ground, narrow valleys, streams and mountain lakes."),
        };

        void BuildNewWorld(RectTransform root)
        {
            // the old floating New world button is retired: System (Esc) > New world opens this
            MakeButton(root, "New world", Vector2.one, new Vector2(-82, -239), new Vector2(148, 30), () => { _newWorldPanel.gameObject.SetActive(true); }).gameObject.SetActive(false);
            _newWorldPanel = MakePanel(root, "NewWorld", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _newWorldPanel.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            var box = MakePanel(_newWorldPanel, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -270), new Vector2(420, 270));
            var title = MakeText(box, "Title", 28, TextAnchor.UpperCenter); title.fontStyle = FontStyle.Bold; title.text = "New world";
            Stretch(title.rectTransform, new Vector2(20, 480), new Vector2(-20, -14));
            var help = MakeText(box, "Help", 16, TextAnchor.UpperCenter); help.color = Dim;
            help.text = "Pick the water, type a seed (the same seed and choice always make the same land). Save first: this starts a new game.";
            Stretch(help.rectTransform, new Vector2(30, 440), new Vector2(-30, -56));
            for (int i = 0; i < WaterChoices.Length; i++)
            {
                int k = i; int col = i % 2, row = i / 2;
                _newWorldButtons[i] = MakeButton(box, WaterChoices[i].label, new Vector2(0.5f, 1), new Vector2(-200 + col * 400, -130 - row * 50), new Vector2(380, 40),
                    () => { _newWorldWater = WaterChoices[k].preset; RefreshNewWorld(); });
            }
            _newWorldNote = MakeText(box, "Note", 18, TextAnchor.MiddleCenter);
            Stretch(_newWorldNote.rectTransform, new Vector2(30, 70), new Vector2(-30, -400));
            var label = MakeText(box, "SeedLabel", 18, TextAnchor.MiddleRight); label.text = "Seed";
            var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0); lr.anchoredPosition = new Vector2(-260, 36); lr.sizeDelta = new Vector2(80, 36);
            _newWorldSeed = MilField(box, 20, true, TextAnchor.MiddleCenter);
            var sr = (RectTransform)_newWorldSeed.transform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0); sr.anchoredPosition = new Vector2(-150, 36); sr.sizeDelta = new Vector2(120, 36);
            _newWorldSeed.text = "7";
            MakeButton(box, "Cancel", new Vector2(0.5f, 0), new Vector2(110, 36), new Vector2(150, 40), () => _newWorldPanel.gameObject.SetActive(false));
            MakeButton(box, "Create", new Vector2(0.5f, 0), new Vector2(280, 36), new Vector2(150, 40), () =>
            {
                int.TryParse(_newWorldSeed.text, out int seed);
                _newWorldPanel.gameObject.SetActive(false);
                NewWorldPicked?.Invoke(_newWorldWater, seed);
            });
            RefreshNewWorld();
            _newWorldPanel.gameObject.SetActive(false);
        }

        void RefreshNewWorld()
        {
            for (int i = 0; i < WaterChoices.Length; i++) _newWorldButtons[i].color = WaterChoices[i].preset == _newWorldWater ? BtnOn : Btn;
            foreach (var c in WaterChoices) if (c.preset == _newWorldWater) _newWorldNote.text = c.note;
        }
    }
}
