using System;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        enum TownMenu { Closed, Farming, Mining, Troops, Statistics, Livestock }
        TownMenu _townMenu;
        readonly RectTransform[] _townPanels = new RectTransform[(int)TownMenu.Livestock];
        readonly UnityEngine.UI.Image[] _townButtons = new UnityEngine.UI.Image[(int)TownMenu.Livestock];
        World _menuWorld;
        Text _cropTarget, _cropCount, _supportsLabel, _historyHeader, _resourceTitle, _dayValues, _populationScale, _resourceScale;
        Text _cropWindows, _cropLabor, _cropStorage;
        UnityEngine.UI.Slider _cropSlider;
        UnityEngine.UI.Image _supportsButton;
        UnityEngine.UI.Button _older, _newer;
        HistoryChart _populationChart, _resourceChart;
        int _historyEnd, _historyDay, _historyVersion = -1, _historyWorldDay;
        Res _historyResource = Res.Food;
        static readonly Res[] ChartResources = (Res[])Enum.GetValues(typeof(Res));
        const int HistoryWindow = 30;
        readonly SettlementStatistics[] _visibleSamples = new SettlementStatistics[HistoryWindow];

        void BuildTownMenus(RectTransform root, RectTransform toolbar)
        {
            string[] titles = { "Farming", "Mining", "Military", "Statistics", "Livestock" };
            int[] widths = { 440, 392, 668, 446, 436 };
            int[] heights = { 356, 208, 480, 396, 309 };
            for (int i = 0; i < titles.Length; i++)
            {
                var menu = (TownMenu)(i + 1);
                _townButtons[i] = MakeButton(toolbar, titles[i], new Vector2(1, 0.5f),
                    new Vector2(-55 - (titles.Length - 1 - i) * 106, 0), new Vector2(100, 34),
                    () => ShowTownMenu(_townMenu == menu ? TownMenu.Closed : menu));
                _townPanels[i] = MakePanel(root, titles[i], Vector2.right, Vector2.right,
                    new Vector2(-8 - widths[i], BottomH + 58), new Vector2(-8, BottomH + 58 + heights[i]));
                PanelLabel(_townPanels[i], "Title", titles[i], 20, 8, 36);
            }
            BuildFarming(_townPanels[0]);
            BuildMining(_townPanels[1]);
            BuildMilitary(_townPanels[2]);
            BuildStatistics(_townPanels[3]);
            BuildLivestock(_townPanels[4]);
            ShowTownMenu(TownMenu.Closed);
        }

        Text PanelLabel(RectTransform panel, string name, string value, int size, int top, int bottom)
        {
            var label = MakeText(panel, name, size, TextAnchor.MiddleLeft);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.up; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, -bottom); rt.offsetMax = new Vector2(-12, -top);
            label.text = value;
            return label;
        }

        void BuildFarming(RectTransform panel)
        {
            _cropTarget = PanelLabel(panel, "CropMix", "", 17, 42, 68);
            var track = MakePanel(panel, "FlaxPercent", Vector2.up, Vector2.one,
                new Vector2(20, -93), new Vector2(-20, -76));
            track.GetComponent<UnityEngine.UI.Image>().color = Btn;
            var knob = MakePanel(track, "Handle", Vector2.zero, Vector2.up,
                new Vector2(-7, -5), new Vector2(7, 5));
            var knobImage = knob.GetComponent<UnityEngine.UI.Image>();
            knobImage.color = BtnOn;
            _cropSlider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            _cropSlider.targetGraphic = knobImage;
            _cropSlider.handleRect = knob;
            _cropSlider.maxValue = 100;
            _cropSlider.wholeNumbers = true;
            _cropSlider.onValueChanged.AddListener(value => _menuWorld?.SetFlaxPercent((int)value));
            _cropCount = PanelLabel(panel, "PlantedFields", "", 15, 105, 129);
            _cropWindows = PanelLabel(panel, "CropWindows", "", 14, 135, 200);
            _cropLabor = PanelLabel(panel, "FarmLabor", "", 14, 202, 242);
            _cropStorage = PanelLabel(panel, "CropStorage", "", 14, 244, 344);
        }

        void BuildMining(RectTransform panel)
        {
            PanelLabel(panel, "TunnelHeading", "Mine tunnels", 16, 40, 64);
            _supportsButton = MakeButton(panel, "", Vector2.up, new Vector2(115, -85), new Vector2(206, 30),
                () => { if (_menuWorld != null) _menuWorld.SetMineSupportsRequired(!_menuWorld.RequireMineSupports); });
            _supportsLabel = _supportsButton.GetComponentInChildren<Text>();
            PanelLabel(panel, "SafetyHelp", "With supports required, miners wait for timber props (2 wood a tunnel tile) before they dig on or take ore from an unshored tunnel. Without them they work faster but risk cave-ins.", 14, 106, 192);
        }

        void BuildStatistics(RectTransform panel)
        {
            _historyHeader = PanelLabel(panel, "DayRange", "", 15, 37, 67);
            _older = MakeButton(panel, "Earlier", Vector2.one, new Vector2(-119, -52), new Vector2(65, 26),
                () => MoveHistory(-1)).GetComponent<UnityEngine.UI.Button>();
            _newer = MakeButton(panel, "Later", Vector2.one, new Vector2(-45, -52), new Vector2(65, 26),
                () => MoveHistory(1)).GetComponent<UnityEngine.UI.Button>();

            PanelLabel(panel, "PopulationTitle", "Population", 17, 75, 101);
            _populationChart = MakeHistoryChart(panel, "PopulationChart", 105, 196, out _populationScale);
            _resourceTitle = PanelLabel(panel, "ResourceTitle", "Stockpile", 17, 211, 239);
            MakeButton(panel, "Previous", Vector2.one, new Vector2(-119, -225), new Vector2(65, 26), () => ChangeResource(-1));
            MakeButton(panel, "Next", Vector2.one, new Vector2(-45, -225), new Vector2(65, 26), () => ChangeResource(1));
            _resourceChart = MakeHistoryChart(panel, "StockpileChart", 243, 334, out _resourceScale);
            _dayValues = PanelLabel(panel, "DayValues", "", 15, 344, 384);
        }

        HistoryChart MakeHistoryChart(RectTransform panel, string name, int top, int bottom, out Text scale)
        {
            var slot = MakePanel(panel, name, Vector2.up, Vector2.one, new Vector2(12, -bottom), new Vector2(-12, -top));
            scale = MakeText(slot, "Maximum", 12, TextAnchor.UpperRight);
            Stretch(scale.rectTransform, new Vector2(0, 0), new Vector2(-385, -4));
            var zero = MakeText(slot, "Zero", 12, TextAnchor.LowerRight);
            zero.text = "0";
            Stretch(zero.rectTransform, new Vector2(0, 4), new Vector2(-385, 0));
            var go = new GameObject("Plot", typeof(RectTransform), typeof(CanvasRenderer), typeof(HistoryChart));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            Stretch(rt, new Vector2(43, 6), new Vector2(-6, -6));
            var chart = go.GetComponent<HistoryChart>();
            chart.color = new Color(0.91f, 0.72f, 0.34f);
            chart.SamplePicked = index =>
            {
                _historyDay = _visibleSamples[index].Day;
                _historyVersion = -1;
                RefreshStatistics(_menuWorld);
            };
            return chart;
        }

        void ShowTownMenu(TownMenu menu)
        {
            _townMenu = menu;
            if (menu != TownMenu.Closed && _workPanel != null && _workPanel.activeSelf)
            {
                _workPanel.SetActive(false);
                WorkforceToggled?.Invoke(false);
            }
            for (int i = 0; i < _townPanels.Length; i++)
            {
                bool selected = (int)menu == i + 1;
                _townPanels[i].gameObject.SetActive(selected);
                _townButtons[i].color = selected ? BtnOn : Btn;
            }
            MilCloseDialog();
            if (menu == TownMenu.Troops) { _townPanels[2].SetAsLastSibling(); _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling(); _milStamp = -1; }   // a big panel like the world map: above the bars
            _historyVersion = -1;
        }

        public void RefreshTownMenus(World world)
        {
            if (_menuWorld != world)
            {
                _menuWorld = world;
                _historyEnd = _historyDay = 0;
                _historyVersion = -1;
                _livestockPen = null;
            }
            switch (_townMenu)
            {
                case TownMenu.Farming:
                    var (flax, total) = world.PlannedCrops();
                    int percent = world.FlaxPercent ?? (total == 0 ? 0 : (100 * flax + total / 2) / total);
                    _cropSlider.SetValueWithoutNotify(percent);
                    _cropTarget.text = $"Wheat {100 - percent}%   |   Flax {percent}%";
                    _cropCount.text = $"Assigned fields: {total - flax} wheat, {flax} flax";
                    if (world.SeasonalFarming)
                    {
                        var forecast = world.FarmingForecast();
                        _cropCount.text = $"Standing / planned plots: Wheat {forecast.wheat}/{forecast.wheatTarget} · Flax {forecast.flax}/{forecast.flaxTarget}";
                        _cropWindows.text = $"Both need fertile soil. Harvest in summer (days 91–180).\n" +
                            $"Wheat (food): sow by spring day {World.LastSowingDay(false)}, grows in {World.WheatGrowth} days, yields {World.WheatYield}/field.\n" +
                            $"Flax (trade): sow by spring day {World.LastSowingDay(true)}, grows in {World.FlaxGrowth} days, yields {World.FlaxYield}/field.";
                        var (workers, jobs, _, _) = world.D.Work["farming"];
                        _cropLabor.text = jobs == 0 ? "No field work today. Workers can take other jobs."
                            : $"{workers}/{jobs} field jobs staffed today" + (workers < jobs ? " · Raise Farming priority to avoid delays." : "");
                        string next = forecast.firstHarvest == 0 ? "No harvest forecast this year." : $"First expected harvest on day {forecast.firstHarvest}.";
                        string shortage = forecast.emptyDay == 0 ? "No shortage projected before next spring's wheat matures."
                            : $"Food may run out on day {forecast.emptyDay}.";
                        _cropStorage.text = $"Food {world.ResAmt[Res.Food]:0}/{world.CropStorage(Res.Food)} · Uses {world.DailyFoodDemand:0.0}/day\n" +
                            $"Flax {world.ResAmt[Res.Flax]:0}/{world.CropStorage(Res.Flax)} · {next}\n" +
                            $"Remaining yield at full staffing: {forecast.wheatYield:0} wheat, {forecast.flaxYield:0} flax.\n" +
                            $"{shortage} Potential wheat overflow: {forecast.overflow:0}.";
                    }
                    break;
                case TownMenu.Mining:
                    _supportsButton.color = world.RequireMineSupports ? BtnOn : Btn;
                    _supportsLabel.text = world.RequireMineSupports ? "Require supports: On" : "Require supports: Off";
                    break;
                case TownMenu.Troops:
                    RefreshMilitary(world);
                    break;
                case TownMenu.Statistics:
                    RefreshStatistics(world);
                    break;
                case TownMenu.Livestock:
                    RefreshLivestock(world);
                    break;
            }
        }

        void ChangeResource(int direction)
        {
            int index = Array.IndexOf(ChartResources, _historyResource);
            _historyResource = ChartResources[(index + direction + ChartResources.Length) % ChartResources.Length];
            _historyVersion = -1;
            RefreshStatistics(_menuWorld);
        }

        void MoveHistory(int direction)
        {
            int latest = _menuWorld.Statistics.Count + 1;
            int end = _historyEnd == 0 ? latest : _historyEnd;
            int next = direction < 0 ? Math.Max(1, end - HistoryWindow) : Math.Min(latest, end + HistoryWindow);
            _historyEnd = next == latest ? 0 : next;
            _historyDay = 0;
            _historyVersion = -1;
            RefreshStatistics(_menuWorld);
        }

        void RefreshStatistics(World world)
        {
            if (world == null || (_historyVersion == world.Version && _historyWorldDay == world.Day)) return;
            _historyVersion = world.Version;
            _historyWorldDay = world.Day;

            int latest = world.Statistics.Count + 1;
            int end = _historyEnd == 0 ? latest : _historyEnd;
            int start = Math.Max(0, end - HistoryWindow);
            int count = end - start;
            int selected = count - 1;
            for (int i = 0; i < count; i++)
            {
                int sample = start + i;
                _visibleSamples[i] = sample == latest - 1 ? world.CurrentStatistics() : world.Statistics[sample];
                if (_visibleSamples[i].Day == _historyDay) selected = i;
            }

            var first = _visibleSamples[0];
            var last = _visibleSamples[count - 1];
            var active = _visibleSamples[selected];
            string resourceLabel = _historyResource == Res.Food ? "Wheat" : _historyResource.ToString();
            double populationMax = 1, resourceMax = 1;
            var population = new double[count];
            var stockpile = new double[count];
            for (int i = 0; i < count; i++)
            {
                population[i] = _visibleSamples[i].Population;
                stockpile[i] = _visibleSamples[i].Stockpile(_historyResource);
                populationMax = Math.Max(populationMax, population[i]);
                resourceMax = Math.Max(resourceMax, stockpile[i]);
            }
            _historyHeader.text = first.Day == last.Day ? $"Day {last.Day}" : $"Days {first.Day}–{last.Day}";
            _older.interactable = start > 0;
            _newer.interactable = end < latest;
            _resourceTitle.text = $"{resourceLabel} stockpile";
            populationMax = Math.Ceiling(populationMax);
            resourceMax = Math.Ceiling(resourceMax);
            _populationScale.text = populationMax.ToString("0");
            _resourceScale.text = resourceMax.ToString("0");
            _populationChart.SetSeries(population, populationMax, selected);
            _resourceChart.SetSeries(stockpile, resourceMax, selected);
            _dayValues.text = $"Day {active.Day}   ·   Population {active.Population}   ·   {resourceLabel} {active.Stockpile(_historyResource):0.####}";
        }
    }
}
