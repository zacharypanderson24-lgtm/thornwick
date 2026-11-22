using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        public event System.Action<int, int> LivestockFocusRequested;
        LivestockPen _livestockPen;
        Text _animalStock, _penSummary, _penOutput, _penStatus;
        UnityEngine.UI.Button _previousPen, _nextPen, _locatePen, _buyAnimal, _slaughterAnimal;

        void BuildLivestock(RectTransform panel)
        {
            _animalStock = PanelLabel(panel, "AnimalStock", "", 15, 38, 66);
            MakeButton(panel, "Pig Pen", Vector2.up, new Vector2(77, -88), new Vector2(126, 32), () => ToolPicked?.Invoke("pigPen"));
            MakeButton(panel, "Sheep Pen", Vector2.up, new Vector2(217, -88), new Vector2(126, 32), () => ToolPicked?.Invoke("sheepPen"));
            MakeButton(panel, "Chicken Pen", Vector2.up, new Vector2(357, -88), new Vector2(126, 32), () => ToolPicked?.Invoke("chickenPen"));
            _penSummary = PanelLabel(panel, "PenSummary", "", 16, 111, 137);
            _penOutput = PanelLabel(panel, "PenOutput", "", 14, 142, 188);
            _penStatus = PanelLabel(panel, "PenStatus", "", 14, 191, 220);
            _previousPen = MakeButton(panel, "Previous", Vector2.up, new Vector2(66, -241), new Vector2(104, 32), () => MoveLivestockSelection(-1)).GetComponent<UnityEngine.UI.Button>();
            _nextPen = MakeButton(panel, "Next", Vector2.up, new Vector2(179, -241), new Vector2(104, 32), () => MoveLivestockSelection(1)).GetComponent<UnityEngine.UI.Button>();
            _locatePen = MakeButton(panel, "Locate Pen", Vector2.up, new Vector2(316, -241), new Vector2(148, 32), () =>
            {
                if (_livestockPen != null)
                    LivestockFocusRequested?.Invoke((_livestockPen.Left + _livestockPen.Right) / 2, (_livestockPen.Top + _livestockPen.Bottom) / 2);
            }).GetComponent<UnityEngine.UI.Button>();
            _buyAnimal = MakeButton(panel, $"Buy 1 · {World.StarterLivestockGold} gold", Vector2.up, new Vector2(114, -281), new Vector2(200, 34),
                () => ManageLivestock(false)).GetComponent<UnityEngine.UI.Button>();
            _slaughterAnimal = MakeButton(panel, "Slaughter 1", Vector2.up, new Vector2(321, -281), new Vector2(200, 34),
                () => ManageLivestock(true)).GetComponent<UnityEngine.UI.Button>();
        }

        public void SelectLivestockPen(LivestockPen pen) => _livestockPen = pen;

        int SelectedPenIndex(World world)
        {
            for (int i = 0; i < world.LivestockPens.Count; i++)
                if (world.LivestockPens[i] == _livestockPen) return i;
            return -1;
        }

        void MoveLivestockSelection(int direction)
        {
            if (_menuWorld == null || _menuWorld.LivestockPens.Count == 0) return;
            int count = _menuWorld.LivestockPens.Count;
            int selected = Mathf.Max(0, SelectedPenIndex(_menuWorld));
            _livestockPen = _menuWorld.LivestockPens[(selected + direction + count) % count];
        }

        void ManageLivestock(bool slaughter)
        {
            if (_menuWorld == null) return;
            var result = slaughter ? _menuWorld.SlaughterLivestock(_livestockPen) : _menuWorld.BuyLivestock(_livestockPen);
            Toast(result.Ok ? (slaughter ? $"Slaughtered one animal for {result.Count} fresh meat." : "Added one animal to the pen.") : result.Reason);
            RefreshLivestock(_menuWorld);
        }

        void RefreshLivestock(World world)
        {
            int count = world.LivestockPens.Count;
            int index = SelectedPenIndex(world);
            if (index < 0) _livestockPen = count == 0 ? null : world.LivestockPens[index = 0];
            _animalStock.text = $"Wool {world.ResAmt[Res.Wool]:0}   ·   Eggs {world.ResAmt[Res.Eggs]:0.#}   ·   Fresh meat {world.FreshMeatStored:0.#}";
            _previousPen.interactable = _nextPen.interactable = count > 1;
            _locatePen.interactable = count > 0;
            var buy = world.CanBuyLivestock(_livestockPen);
            var slaughter = world.CanSlaughterLivestock(_livestockPen);
            _buyAnimal.interactable = buy.Ok;
            _slaughterAnimal.interactable = slaughter.Ok;
            if (_livestockPen == null)
            {
                _penSummary.text = "No livestock pens";
                _penOutput.text = "Choose a species, then drag a fenced area beside a road.";
                _penStatus.text = "";
                return;
            }
            var pen = _livestockPen;
            _penSummary.text = $"{index + 1}/{count}   {pen.Species}   ·   {pen.Count}/{pen.Capacity} animals   ·   {pen.Area} tiles";
            string output = pen.Species switch
            {
                Zone.Sheep => $"{World.WoolPerSheep} wool per sheep each spring" + (pen.LastShearedYear == world.Year ? " (shorn this year)" : ""),
                Zone.Chickens => $"{World.EggsPerChicken} egg per fed chicken each day",
                _ => "Raised for meat",
            };
            _penOutput.text = $"{output}.\nSlaughter yields {World.MeatPerAnimal(pen.Species)} fresh meat per animal.";
            _penStatus.text = !world.PenHasRoad(pen) ? "Connect this pen to the Town Hall."
                : !slaughter.Ok && pen.Count > 0 ? slaughter.Reason
                : pen.Count == 0 ? "Buy an animal to restock this pen."
                : $"Wheat feed: {pen.Count * World.WheatPerAnimalPerDay:0.##} per day";
        }
    }
}
