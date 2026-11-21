// The Map menu: the procedurally generated world (Sim/WorldMap.cs), opened with the Map button or M. A big centred panel: the map on the
// left (zoom with the wheel, pan by dragging or WASD), and on the right what is under the cursor (region and cell), a layer switch
// (terrain or heat map, region borders on or off) and a legend. The world is generated on a background thread by the game controller;
// this panel shows "Generating" until it arrives (SetWorldMap).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        enum MapLayer { Terrain, Heat }

        RectTransform _mapPanel, _mapLegend;
        MapView _mapView;
        Text _mapTitle, _mapSubtitle, _mapInfo, _mapCell, _mapStatus;
        UnityEngine.UI.Image _mapMenuButton, _bordersButton, _terrainButton, _heatButton;
        RawImage _mapBase, _mapBorders, _mapSites;
        Texture2D _terrainTex, _heatTex, _bordersTex, _sitesTex;
        WorldMap _worldMap;
        MapLayer _mapLayer = MapLayer.Terrain;
        bool _mapOpen, _mapBordersOn = true;

        public bool MapOpen => _mapOpen;
        /// <summary>Raised when the map opens (true) or closes (false); the game asks for the world to be generated on open.</summary>
        public event Action<bool> MapToggled;

        void BuildMap(RectTransform root, RectTransform toolbar)
        {
            _mapMenuButton = MakeButton(toolbar, "Map", new Vector2(1, 0.5f), new Vector2(-55 - 4 * 106, 0), new Vector2(100, 34), ToggleMap);

            _mapPanel = MakePanel(root, "WorldMap", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-570, -380), new Vector2(570, 380));
            _mapPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.09f, 0.075f, 0.06f, 1f);
            _mapTitle = PanelLabel(_mapPanel, "Title", "World map", 22, 8, 40); _mapTitle.fontStyle = FontStyle.Bold;
            _mapSubtitle = PanelLabel(_mapPanel, "Subtitle", "", 14, 40, 62); _mapSubtitle.color = Dim;

            // the map: a clipped square holding three layers that share one view rectangle
            var surface = MakePanel(_mapPanel, "Surface", Vector2.zero, Vector2.zero, new Vector2(14, 14), new Vector2(14 + 680, 14 + 680));
            surface.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.07f, 0.11f, 1);
            surface.gameObject.AddComponent<RectMask2D>();
            _mapBase = MakeMapLayer(surface, "Terrain");
            _mapBorders = MakeMapLayer(surface, "Borders");
            _mapSites = MakeMapLayer(surface, "Sites");
            var highlight = MakePanel(surface, "Highlight", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hi = highlight.GetComponent<UnityEngine.UI.Image>(); hi.color = new Color(1, 1, 1, 0.10f); hi.raycastTarget = false;
            var outline = highlight.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1, 0.95f, 0.75f, 0.95f); outline.effectDistance = new Vector2(2, -2);
            highlight.gameObject.SetActive(false);
            _mapStatus = MakeText(surface, "Status", 20, TextAnchor.MiddleCenter);
            Stretch(_mapStatus.rectTransform, Vector2.zero, Vector2.zero);
            _mapView = _mapPanel.gameObject.AddComponent<MapView>();
            _mapView.Surface = surface; _mapView.Highlight = highlight; _mapView.Layers = new[] { _mapBase, _mapBorders, _mapSites };
            _mapView.Hovered = OnMapHover;

            // the side: what is under the cursor, the layers, the legend, the buttons
            var side = MakePanel(_mapPanel, "Side", Vector2.zero, Vector2.one, new Vector2(708, 14), new Vector2(-14, -66));
            side.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            _mapInfo = MakeText(side, "RegionInfo", 15, TextAnchor.UpperLeft);
            TopStretch(_mapInfo.rectTransform, 4, 250);
            _mapCell = MakeText(side, "CellInfo", 13, TextAnchor.UpperLeft); _mapCell.color = Dim;
            TopStretch(_mapCell.rectTransform, 252, 294);

            _terrainButton = MakeButton(side, "Terrain", Vector2.up, new Vector2(8 + 63, -(300 + 15)), new Vector2(126, 30), () => SetMapLayer(MapLayer.Terrain));
            _heatButton = MakeButton(side, "Heat map", Vector2.up, new Vector2(8 + 63 + 132, -(300 + 15)), new Vector2(126, 30), () => SetMapLayer(MapLayer.Heat));
            _bordersButton = MakeButton(side, "Region borders", Vector2.up, new Vector2(8 + 63 + 264, -(300 + 15)), new Vector2(136, 30), () => { _mapBordersOn = !_mapBordersOn; RefreshMapLayers(); });

            _mapLegend = new GameObject("Legend", typeof(RectTransform)).GetComponent<RectTransform>();
            _mapLegend.SetParent(side, false);
            _mapLegend.anchorMin = Vector2.up; _mapLegend.anchorMax = Vector2.one; _mapLegend.offsetMin = new Vector2(0, -(340 + 250)); _mapLegend.offsetMax = new Vector2(0, -340);

            var hint = MakeText(side, "Hint", 13, TextAnchor.LowerLeft); hint.color = Dim;
            hint.text = "Scroll to zoom. Drag or W A S D to pan. M or Esc closes the map.";
            var hr = hint.rectTransform; hr.anchorMin = Vector2.zero; hr.anchorMax = new Vector2(1, 0); hr.offsetMin = new Vector2(8, 52); hr.offsetMax = new Vector2(-8, 90);
            MakeButton(side, "Find my settlement", Vector2.zero, new Vector2(8 + 85, 14 + 15), new Vector2(170, 30), () =>
            {
                var site = _worldMap?.HomeSite;
                if (site != null) _mapView.FocusCell(site.X, site.Y, 5f);
            });
            MakeButton(side, "Whole world", Vector2.zero, new Vector2(8 + 170 + 6 + 60, 14 + 15), new Vector2(120, 30), () => _mapView.ShowAll());
            MakeButton(side, "Close", Vector2.zero, new Vector2(8 + 170 + 6 + 120 + 6 + 40, 14 + 15), new Vector2(80, 30), CloseMap);

            _mapPanel.gameObject.SetActive(false);
            RefreshMapLayers();
        }

        RawImage MakeMapLayer(RectTransform surface, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform; rt.SetParent(surface, false);
            Stretch(rt, Vector2.zero, Vector2.zero);
            var raw = go.GetComponent<RawImage>(); raw.raycastTarget = false; raw.color = Color.white;
            return raw;
        }

        /// <summary>A label anchored to the top of its parent, `top` to `bottom` pixels down.</summary>
        static void TopStretch(RectTransform rt, int top, int bottom)
        {
            rt.anchorMin = Vector2.up; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(8, -bottom); rt.offsetMax = new Vector2(-8, -top);
        }

        public void ToggleMap() { if (_mapOpen) CloseMap(); else OpenMap(); }

        public void OpenMap()
        {
            if (_mapOpen) return;
            _mapOpen = true;
            ShowTownMenu(TownMenu.Closed);
            _mapPanel.gameObject.SetActive(true);
            _mapPanel.SetAsLastSibling(); _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling();
            _mapMenuButton.color = BtnOn;
            _mapStatus.text = _worldMap == null ? "Generating the world..." : "";
            MapToggled?.Invoke(true);
        }

        public void CloseMap()
        {
            if (!_mapOpen) return;
            _mapOpen = false;
            _mapPanel.gameObject.SetActive(false);
            _mapMenuButton.color = Btn;
            MapToggled?.Invoke(false);
        }

        /// <summary>The world is ready (or a new one, after loading another save): build its pictures and show it.</summary>
        public void SetWorldMap(WorldMap map)
        {
            if (map == _worldMap && _terrainTex != null) { _mapStatus.text = ""; return; }
            _worldMap = map;
            foreach (var tex in new[] { _terrainTex, _heatTex, _bordersTex, _sitesTex }) if (tex != null) Destroy(tex);
            _terrainTex = WorldMapRenderer.BuildTerrain(map); _heatTex = WorldMapRenderer.BuildHeat(map);
            _bordersTex = WorldMapRenderer.BuildBorders(); _sitesTex = WorldMapRenderer.BuildSites(map);
            _mapBorders.texture = _bordersTex; _mapSites.texture = _sitesTex;
            _mapTitle.text = $"World map: {WorldMap.SettingName(map.Setting)} campaign";
            _mapSubtitle.text = $"{WorldMap.Size} x {WorldMap.Size} km  ·  {WorldMap.Regions} x {WorldMap.Regions} regions of {WorldMap.RegionCells} km  ·  seed {map.Seed}";
            _mapStatus.text = "";
            _mapView.SetMap(map);
            RefreshMapLayers();
            OnMapHover(null);
        }

        public void SetMapGenerating() { if (_mapStatus != null && _worldMap == null) _mapStatus.text = "Generating the world..."; }

        void SetMapLayer(MapLayer layer) { _mapLayer = layer; RefreshMapLayers(); }

        void RefreshMapLayers()
        {
            _terrainButton.color = _mapLayer == MapLayer.Terrain ? BtnOn : Btn;
            _heatButton.color = _mapLayer == MapLayer.Heat ? BtnOn : Btn;
            _bordersButton.color = _mapBordersOn ? BtnOn : Btn;
            _mapBase.texture = _mapLayer == MapLayer.Terrain ? _terrainTex : _heatTex;
            _mapBase.color = _mapBase.texture == null ? Color.clear : Color.white;
            _mapBorders.enabled = _mapBordersOn && _bordersTex != null; _mapSites.enabled = _sitesTex != null;
            BuildMapLegend();
        }

        // ---------- Legend ----------
        void BuildMapLegend()
        {
            for (int i = _mapLegend.childCount - 1; i >= 0; i--) Destroy(_mapLegend.GetChild(i).gameObject);
            if (_worldMap == null) return;
            var rows = new List<(Color32 colour, string label, string value)>();
            if (_mapLayer == MapLayer.Terrain)
            {
                int[] counts = new int[Enum.GetValues(typeof(Biome)).Length]; int land = 0;
                foreach (var b in _worldMap.Biomes) { counts[(int)b]++; if (b != Biome.Ocean && b != Biome.Lake) land++; }
                foreach (int k in Enumerable.Range(2, counts.Length - 2).Where(k => counts[k] > 0).OrderByDescending(k => counts[k]))
                    rows.Add((WorldMapRenderer.ColorOf((Biome)k), WorldMap.BiomeName((Biome)k), $"{100.0 * counts[k] / Math.Max(1, land):0}%"));
                rows.Add((WorldMapRenderer.ColorOf(Biome.Ocean), "Sea", ""));
                rows.Add((WorldMapRenderer.ColorOf(Biome.Lake), "Lake", ""));
                rows.Add((WorldMapRenderer.River, "River", ""));
                rows.Add((WorldMapRenderer.SiteWhite, "City site", ""));
                rows.Add((WorldMapRenderer.HomeGold, "Your settlement", ""));
            }
            else
            {
                foreach (var (lo, hi, label) in new[] { (-10f, 0f, "Below 0 °C"), (0f, 5f, "0 to 5 °C"), (5f, 10f, "5 to 10 °C"), (10f, 15f, "10 to 15 °C"),
                                                        (15f, 20f, "15 to 20 °C"), (20f, 25f, "20 to 25 °C"), (25f, 30f, "25 to 30 °C"), (30f, 34f, "Above 30 °C") })
                    rows.Add((WorldMapRenderer.HeatColor((lo + hi) / 2f), label, ""));
                rows.Add((WorldMapRenderer.HomeGold, "Your settlement", ""));
            }
            int perColumn = (rows.Count + 1) / 2;
            for (int i = 0; i < rows.Count; i++)
            {
                int col = i / perColumn, row = i % perColumn; float x = 8 + col * 206, y = -row * 20;
                var swatch = MakePanel(_mapLegend, "Swatch", Vector2.up, Vector2.up, new Vector2(x, y - 17), new Vector2(x + 14, y - 3));
                swatch.GetComponent<UnityEngine.UI.Image>().color = rows[i].colour;
                var label = MakeText(_mapLegend, "Label", 13, TextAnchor.MiddleLeft); label.text = rows[i].label;
                var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = Vector2.up; lr.pivot = new Vector2(0, 0.5f); lr.sizeDelta = new Vector2(150, 18); lr.anchoredPosition = new Vector2(x + 20, y - 10);
                if (rows[i].value.Length > 0)
                {
                    var value = MakeText(_mapLegend, "Share", 12, TextAnchor.MiddleRight); value.text = rows[i].value; value.color = Dim;
                    var vr = value.rectTransform; vr.anchorMin = vr.anchorMax = Vector2.up; vr.pivot = new Vector2(0, 0.5f); vr.sizeDelta = new Vector2(34, 18); vr.anchoredPosition = new Vector2(x + 168, y - 10);
                }
            }
        }

        // ---------- What is under the cursor ----------
        void OnMapHover(Vector2Int? cell)
        {
            if (_worldMap == null) return;
            var region = cell.HasValue ? _worldMap.RegionAt(cell.Value.x, cell.Value.y) : _worldMap.Home;
            _mapInfo.text = region == null ? "" : DescribeRegion(_worldMap, region);
            _mapCell.text = cell.HasValue ? DescribeCell(_worldMap, cell.Value.x, cell.Value.y) : "Move the cursor over the map. Showing your home region.";
        }

        static string DescribeRegion(WorldMap map, MapRegion r)
        {
            var sb = new StringBuilder();
            bool home = r.Id == map.HomeRegion;
            sb.AppendLine($"<b>{(r.IsLand ? r.Name : r.Name + " Sea")}</b>   region {r.Col + 1}, {r.Row + 1}" + (home ? "   <color=#FFC428>YOUR SETTLEMENT</color>" : ""));
            if (!r.IsLand) { sb.AppendLine(r.Land > 0 ? $"Open sea, {r.Land:P0} land." : "Open sea."); return sb.ToString(); }
            int landCells = r.BiomeCells.Where((c, k) => k >= 2).Sum();
            var top = Enumerable.Range(2, r.BiomeCells.Length - 2).Where(k => r.BiomeCells[k] > 0).OrderByDescending(k => r.BiomeCells[k]).Take(3)
                .Select(k => $"{WorldMap.BiomeName((Biome)k)} {100.0 * r.BiomeCells[k] / Math.Max(1, landCells):0}%");
            sb.AppendLine($"{r.Land:P0} land" + (r.Water > 0.005 ? $", {r.Water:P0} water" : "") + (r.Coastal ? ", on the coast" : ", inland"));
            sb.AppendLine(string.Join(", ", top));
            sb.AppendLine($"Climate: {r.Temperature:0.#} °C a year, {r.Rain:0} mm of rain");
            sb.AppendLine($"Height: {r.ElevAvg:0} m on average, {r.ElevMax:0} m at the highest. Relief: {r.Relief}");
            sb.AppendLine($"Fertile land {r.Fertile:P0}, forest {r.Forest:P0}, mountains {r.Mountain:P0}, rivers {r.RiverKm} km");
            if (r.Site != null) sb.AppendLine(home ? $"Your settlement: {r.Site.Name}" : $"City site: {r.Site.Name} (unclaimed)");
            return sb.ToString();
        }

        static string DescribeCell(WorldMap map, int x, int y)
        {
            int i = WorldMap.Cell(x, y); float e = map.Elevation[i]; var b = map.Biomes[i];
            var parts = new List<string> { WorldMap.BiomeName(b) };
            parts.Add(e > 0 ? $"{e:0} m high" : $"{-e:0} m deep");
            parts.Add($"{map.Temperature[i]:0.#} °C");
            if (e > 0 && b != Biome.Lake) parts.Add($"{map.Rainfall[i]:0} mm");
            var f = map.Features[i];
            if ((f & MapFeature.MajorRiver) != 0) parts.Add("major river"); else if ((f & MapFeature.River) != 0) parts.Add("river");
            if ((f & MapFeature.Floodplain) != 0) parts.Add("floodplain");
            if ((f & MapFeature.Coast) != 0) parts.Add("coast");
            return $"Cell {x}, {y}  ({x} km from the west edge, {y} km from the north)\n" + string.Join("  ·  ", parts);
        }
    }
}
