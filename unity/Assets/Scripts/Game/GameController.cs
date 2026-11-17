// Runs the settlement: owns the World, ticks it (1 day = 1 second at 1x, like the browser), redraws on change,
// and turns mouse/keyboard into tools. Hotkeys follow the browser where it has them (1 Look, 2 Road, 8 Well, X Sawmill,
// F Forester, 9 Barracks, 3 Housing, 4 Farmland, 5 Commerce, 6 Lumber, 7 Mining zones, 0 Clear, Q/E rotate).
// As in the browser, homes, farms, markets, woodcutter lodges, quarries and mines are not placed by hand: they grow on
// painted zones beside roads when there is demand (World.Develop).
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Game
{
    public class GameController : MonoBehaviour
    {
        public class ToolDef
        {
            public string Id, Label, Key; public Key Hotkey;
            public string Build; public Zone Zone; public bool Road, Clear;
        }

        public static readonly ToolDef[] Tools =
        {
            new ToolDef { Id = "look", Label = "Look", Key = "1", Hotkey = Key.Digit1 },
            new ToolDef { Id = "road", Label = "Road", Key = "2", Hotkey = Key.Digit2, Road = true },
            new ToolDef { Id = "zoneRes", Label = "Housing", Key = "3", Hotkey = Key.Digit3, Zone = Zone.Res },
            new ToolDef { Id = "zoneFarm", Label = "Farmland", Key = "4", Hotkey = Key.Digit4, Zone = Zone.Farm },
            new ToolDef { Id = "market", Label = "Market", Key = "5", Hotkey = Key.Digit5, Build = "market" },
            new ToolDef { Id = "zoneWood", Label = "Lumber", Key = "6", Hotkey = Key.Digit6, Zone = Zone.Wood },
            new ToolDef { Id = "zoneMine", Label = "Mining", Key = "7", Hotkey = Key.Digit7, Zone = Zone.Mine },
            new ToolDef { Id = "farmhouse", Label = "Farmhouse", Key = "B", Hotkey = Key.B, Build = "farm" },
            new ToolDef { Id = "lodge", Label = "Lodge", Key = "L", Hotkey = Key.L, Build = "wood" },
            new ToolDef { Id = "quarry", Label = "Quarry", Key = "K", Hotkey = Key.K, Build = "quarry" },
            new ToolDef { Id = "mine", Label = "Iron Mine", Key = "M", Hotkey = Key.M, Build = "mine" },
            new ToolDef { Id = "forester", Label = "Forester", Key = "F", Hotkey = Key.F, Build = "forester" },
            new ToolDef { Id = "sawmill", Label = "Sawmill", Key = "X", Hotkey = Key.X, Build = "sawmill" },
            new ToolDef { Id = "silo", Label = "Silo", Key = "J", Hotkey = Key.J, Build = "silo" },
            new ToolDef { Id = "barracks", Label = "Barracks", Key = "9", Hotkey = Key.Digit9, Build = "barracks" },
            new ToolDef { Id = "clear", Label = "Clear", Key = "0", Hotkey = Key.Digit0, Clear = true },
        };

        [Tooltip("Sprite-Unlit material (URP 2D) so the palette shows exactly as drawn.")]
        public Material SpriteMaterial;
        public int Seed = 7;
        [Tooltip("How mountainous the city's region is (the conquest map will set this later).")]
        public Relief Relief = Relief.Upland;

        public World World { get; private set; }
        int _rot, _drawnVersion = -1, _drawnRot = -1, _speed = 1;
        int _ugLevel;   // underground view: 0 off, else the level shown (U cycles)
        float _dayAcc;
        ToolDef _tool = Tools[0];
        Vector2Int? _hover, _roadAnchor, _lastPaint, _zoneAnchor;   // _zoneAnchor: first corner of a zone rectangle
        ArtLibrary _art;
        GroundRenderer _ground; ObjectRenderer _objects; OverlayRenderer _overlay; AgentRenderer _agents;
        CameraRig _cam; Hud _hud;
        readonly Tutorial _tutorial = new Tutorial();
        const string TutorialSeenKey = "crown-hearth-tutorial-seen";

        void Start()
        {
            World = new World(Seed, TerrainStyle.Ranges, Relief);
            _art = new ArtLibrary();
            if (SpriteMaterial == null) Debug.LogError("GameController needs the Sprite-Unlit material. Run Crown & Hearth > Rebuild Settlement Scene.");
            _ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundRenderer)).GetComponent<GroundRenderer>();
            _ground.transform.SetParent(transform, false); _ground.Init(SpriteMaterial, _art.GroundAtlas);
            _overlay = new GameObject("Overlay", typeof(MeshFilter), typeof(MeshRenderer), typeof(OverlayRenderer)).GetComponent<OverlayRenderer>();
            _overlay.transform.SetParent(transform, false); _overlay.Init(SpriteMaterial, _art.GroundAtlas);
            _objects = new GameObject("Objects", typeof(ObjectRenderer)).GetComponent<ObjectRenderer>();
            _objects.transform.SetParent(transform, false); _objects.Init(_art, SpriteMaterial);
            _agents = new GameObject("Agents", typeof(AgentRenderer)).GetComponent<AgentRenderer>();
            _agents.transform.SetParent(transform, false); _agents.Init(SpriteMaterial);

            _cam = Camera.main.GetComponent<CameraRig>();
            if (_cam == null) _cam = Camera.main.gameObject.AddComponent<CameraRig>();
            _cam.CenterOn(IsoView.TileCenter(World, Defs.Start, Defs.Start + 2, _rot));

            _hud = new GameObject("HudRoot", typeof(Hud)).GetComponent<Hud>();
            _hud.Build(Tools);
            _hud.BannerPicked += SetBanner;
            _hud.ToolPicked += id => SetTool(Tools.First(t => t.Id == id));
            _hud.SpeedPicked += s => { _speed = s; _hud.SetSpeed(s); _tutorial.SpeedChanged = true; };
            _hud.RotatePicked += Rotate;
            _hud.PrioMoved += MovePrio;
            _hud.WorkforceToggled += open => { if (open) _tutorial.WorkersOpened = true; };
            _hud.TutorialOpen += () => { _tutorial.Start(World, _tutorial.Finished ? 0 : _tutorial.Index); _hud.ShowTutorial(_tutorial); };
            _hud.TutorialNext += () => { _tutorial.Next(World); if (_tutorial.Finished) PlayerPrefs.SetInt(TutorialSeenKey, 1); _hud.ShowTutorial(_tutorial); };
            _hud.TutorialBack += () => { _tutorial.Back(World); _hud.ShowTutorial(_tutorial); };
            _hud.TutorialClose += () => { _tutorial.Close(); PlayerPrefs.SetInt(TutorialSeenKey, 1); _hud.ShowTutorial(_tutorial); };
            _hud.SavePicked += SaveSettlement;
            _hud.LoadPicked += LoadSettlement;
            SetTool(_tool); _hud.SetSpeed(_speed);
            // the tutorial opens by itself until the player has finished or closed it once (the Tutorial button brings it back)
            if (PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0) { _tutorial.Start(World); _hud.ShowTutorial(_tutorial); }
            else _hud.Toast("Welcome back to Crown & Hearth. The Tutorial button (bottom right) walks through the game.", 6);
        }

        static string SavePath => Path.Combine(Application.persistentDataPath, "thornwick-save-1.json");

        void SaveSettlement()
        {
            string temporary = SavePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporary, World.SaveJson());
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, null);
                else File.Move(temporary, SavePath);
                _hud.Toast("Settlement saved.");
            }
            catch (System.Exception error)
            {
                _hud.Toast("Could not save: " + error.Message, 6);
            }
        }

        void LoadSettlement()
        {
            if (!File.Exists(SavePath))
            {
                _hud.Toast("No saved settlement found.");
                return;
            }

            World loaded;
            try
            {
                loaded = World.LoadJson(File.ReadAllText(SavePath));
            }
            catch (System.Exception error)
            {
                _hud.Toast("Could not load: " + error.Message, 6);
                return;
            }

            World = loaded;
            SetBanner(ColorUtility.TryParseHtmlString(loaded.BannerColor ?? "", out _) ? loaded.BannerColor : null);
            _dayAcc = 0;
            _speed = 0;
            _drawnVersion = -1;
            _hover = _lastPaint = null;
            SetTool(Tools[0]);
            _hud.SetSpeed(_speed);
            _cam.CenterOn(IsoView.TileCenter(World, loaded.Hall.X + 1, loaded.Hall.Y + 1, _rot));
            _hud.Toast("Settlement loaded and paused.");
        }

        void SetBanner(string hex)
        {
            if (!string.IsNullOrEmpty(hex))
            {
                hex = hex.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (hex.Length != 7 || !ColorUtility.TryParseHtmlString(hex, out _))
                {
                    _hud.Toast("Enter a six-digit hex color, such as #2A56C4.");
                    return;
                }
            }
            World.BannerColor = string.IsNullOrEmpty(hex) ? null : hex;
            _art.SetBanner(World.BannerColor);
            _hud.ShowBannerColor(World.BannerColor);
            _drawnVersion = -1;
        }

        void OnDestroy() => _art?.SetBanner(null);

        void Update()
        {
            // a script reload during Play mode (recompiling while the game runs) drops the world; Unity does not call Start again
            if (World == null)
            {
                Debug.LogWarning("Crown & Hearth: scripts were reloaded while playing, so the game state is gone. Stop and start Play mode again.");
                enabled = false;
                return;
            }
            HandleKeys();
            bool overUi = _hud.PointerOverUi;
            var camBefore = _cam.Center; int zoomBefore = _cam.ZoomIndex;
            _cam.Tick(!overUi, !_hud.EditingBanner);
            if ((_cam.Center - camBefore).sqrMagnitude > 1e-6f) _tutorial.Panned = true;
            if (_cam.ZoomIndex != zoomBefore) _tutorial.Zoomed = true;
            HandleMouse(overUi);

            if (_speed > 0)
            {
                _dayAcc += Time.unscaledDeltaTime * _speed;
                int guard = 0;
                while (_dayAcc >= 1f && guard++ < 8) { _dayAcc -= 1f; World.Tick(); }
                if (_dayAcc > 1f) _dayAcc = 0;
            }

            if (World.Version != _drawnVersion || _rot != _drawnRot)
            {
                _ground.Build(World, _rot); _objects.Build(World, _rot);
                _drawnVersion = World.Version; _drawnRot = _rot;
            }
            _agents.Draw(World, _rot, Mathf.Clamp01(_dayAcc), _cam.Zoom <= 0.5f);
            _objects.RefreshCropStages(World.Day);
            DrawOverlay();
            _hud.SetSettlement(World);
            _hud.SetDemand(DemandLine());
            _hud.SetInfo(_hover.HasValue ? TileInfo(_hover.Value.x, _hover.Value.y) : "");
            _hud.RefreshWorkforce(World);
            _hud.RefreshTownMenus(World);
            if (World.Events.Count > 0) { _hud.Toast(World.Events[World.Events.Count - 1], 8); World.Events.Clear(); }   // cave-ins, new levels, empty surveys
            var done = _tutorial.Check(World);
            if (done != null)
            {
                _hud.Toast($"Well done: {done.Title}." + (_tutorial.Finished ? " That's the whole tutorial." : ""), 4);
                if (_tutorial.Finished) PlayerPrefs.SetInt(TutorialSeenKey, 1);
                _hud.ShowTutorial(_tutorial);
            }
        }

        void HandleKeys()
        {
            var kb = Keyboard.current; if (kb == null || _hud.EditingBanner) return;
            foreach (var t in Tools) if (kb[t.Hotkey].wasPressedThisFrame) SetTool(t);
            if (kb.escapeKey.wasPressedThisFrame) SetTool(Tools[0]);
            if (kb.qKey.wasPressedThisFrame) Rotate(-1);
            if (kb.eKey.wasPressedThisFrame) Rotate(1);
            if (kb.spaceKey.wasPressedThisFrame) { _speed = _speed == 0 ? 1 : 0; _hud.SetSpeed(_speed); _tutorial.SpeedChanged = true; }
            if (kb.uKey.wasPressedThisFrame)
            {
                _tutorial.UndergroundViewed = true;
                _ugLevel = (_ugLevel + 1) % (World.Bands + 1);
                _hud.Toast(_ugLevel == 0 ? "Underground view off." : $"Underground view: ore band {_ugLevel} of {World.Bands}" +
                    (_ugLevel == 1 ? " (just under the surface). Ore lies in hills and mountains. Orange = surveyed ore, brighter = richer; grey = surveyed, nothing here; blue = Mining zone not surveyed yet. U for deeper." : _ugLevel < World.Bands ? ". U for deeper." : " (deepest, richest). U to turn off."), 6);
            }
        }

        void SetTool(ToolDef t)
        {
            _hud.SetToolHint(t.Build != null ? $"{t.Label}  ·  {CostText(t.Build)}  ·  Click a tile to place"
                : t.Road ? "Road  ·  Drag from an existing road or building entrance"
                : t.Zone != Zone.None ? $"{t.Label}  ·  Click two corners to mark an area"
                : t.Clear ? "Clear  ·  Click or drag over tiles to remove them" : "Look  ·  Hover a tile to inspect it");
            _tool = t; _roadAnchor = null; _zoneAnchor = null; _hud.SetTool(t.Id);
            if (t.Build != null) _hud.Toast($"{Defs.Names[t.Build]}: {CostText(t.Build)}");
            if (t.Zone != Zone.None) _hud.Toast(ZoneHelp(t.Zone) + (t.Zone == Zone.Res ? " " + World.Note[Zone.Res] : ""), 6);
        }

        void Rotate(int dir)
        {
            _tutorial.Rotated = true;
            var center = IsoView.PickTile(World, _cam.Center, _rot) ?? new Vector2Int(Defs.Start, Defs.Start);
            _rot = (_rot + dir + 4) % 4;
            _cam.CenterOn(IsoView.TileCenter(World, center.x, center.y, _rot));
        }

        void MovePrio(string trade, int dir)
        {
            var p = World.Prio; int i = System.Array.IndexOf(p, trade), j = i + dir;
            if (i < 0 || j < 0 || j >= p.Length) return;
            (p[i], p[j]) = (p[j], p[i]);
            World.Recalc();
        }

        void HandleMouse(bool overUi)
        {
            var mouse = Mouse.current; if (mouse == null) return;
            var wp = _cam.Cam.ScreenToWorldPoint(mouse.position.ReadValue());
            _hover = overUi ? null : IsoView.PickTile(World, wp, _rot);

            if (_tool.Road)
            {
                if (mouse.leftButton.wasPressedThisFrame && _hover.HasValue) _roadAnchor = _hover;
                if (mouse.leftButton.wasReleasedThisFrame && _roadAnchor.HasValue)
                {
                    var end = _hover ?? _roadAnchor.Value;
                    var r = World.PlaceRoadPath(World.LPath(_roadAnchor.Value.x, _roadAnchor.Value.y, end.x, end.y));
                    if (!r.Ok) _hud.Toast(r.Reason);
                    _roadAnchor = null;
                }
                return;
            }
            if (!mouse.leftButton.isPressed || !_hover.HasValue || overUi) { _lastPaint = null; return; }
            var h = _hover.Value;
            if (_tool.Build != null)
            {
                if (!mouse.leftButton.wasPressedThisFrame) return;
                var r = World.Place(_tool.Build, h.x, h.y);
                _hud.Toast(r.Ok ? $"Built a {Defs.Names[_tool.Build]}." : r.Reason);
            }
            else if (_tool.Zone != Zone.None)
            {
                // zoning: the first click sets a corner, the second zones the whole rectangle
                if (!mouse.leftButton.wasPressedThisFrame) return;
                if (!_zoneAnchor.HasValue) { _zoneAnchor = h; _hud.Toast("Click the opposite corner to zone the rectangle (Esc cancels)."); return; }
                var a = _zoneAnchor.Value; _zoneAnchor = null;
                var (zoned, skipped, why) = World.ZoneRect(_tool.Zone, a.x, a.y, h.x, h.y);
                _hud.Toast(zoned == 0 && skipped > 0 ? why : $"Zoned {zoned} tile{(zoned == 1 ? "" : "s")}." + (skipped > 0 ? $" {skipped} skipped: {why}" : ""), 5);
            }
            else if (_tool.Clear)
            {
                if (_lastPaint == h) return;
                _lastPaint = h;
                var r = _tool.Clear ? World.Remove(h.x, h.y) : World.SetZone(_tool.Zone, h.x, h.y);
                if (!r.Ok && mouse.leftButton.wasPressedThisFrame) _hud.Toast(r.Reason);
            }
        }

        static readonly Color32 Ok = new Color32(120, 255, 120, 110), Bad = new Color32(255, 90, 80, 120), Hover = new Color32(255, 255, 255, 70);

        static readonly Color32 UgNone = new Color32(40, 40, 48, 150), UgUnknown = new Color32(70, 110, 200, 110), UgShaft = new Color32(255, 240, 120, 200);

        /// <summary>Underground view of ore band _ugLevel (1 = just under each tile's surface, 3 = deepest): surveyed ore (orange, brighter =
        /// richer), surveyed nothing (grey), Mining zone not yet surveyed (blue), mines with an adit or shaft (yellow).</summary>
        void DrawUnderground()
        {
            var w = World; int L = _ugLevel; double cap = World.LevelCapMax[L];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var b = w.Grid[y, x];
                    if (b != null && b.T == "mine") { if (b.Adit || b.Depth > 0) _overlay.AddTile(w, x, y, _rot, UgShaft); continue; }
                    if (w.IsSurveyed(x, y))
                    {
                        double o = w.KnownOre(x, y, L);
                        if (o <= 1e-9) { _overlay.AddTile(w, x, y, _rot, UgNone); continue; }
                        float t = Mathf.Clamp01((float)(o / cap));
                        _overlay.AddTile(w, x, y, _rot, new Color32(255, (byte)(150 - 70 * t), (byte)(40 - 30 * t), (byte)(110 + 120 * t)));
                    }
                    else if (w.ZoneMap[y, x] == Zone.Mine) _overlay.AddTile(w, x, y, _rot, UgUnknown);
                }
        }

        void DrawOverlay()
        {
            _overlay.Clear();
            if (_ugLevel > 0) DrawUnderground();
            if (_hover.HasValue)
            {
                var h = _hover.Value;
                if (_tool.Road)
                {
                    var from = _roadAnchor ?? h;
                    var path = World.LPath(from.x, from.y, h.x, h.y);
                    bool ok = World.RoadPathCheck(path).Ok;
                    foreach (var (x, y) in path) _overlay.AddTile(World, x, y, _rot, ok ? Ok : Bad);
                }
                else if (_tool.Build != null)
                {
                    bool ok = World.CanPlace(_tool.Build, h.x, h.y).Ok;
                    int sz = PlacementSize(_tool.Build);
                    for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) _overlay.AddTile(World, h.x + i, h.y + j, _rot, ok ? Ok : Bad);
                }
                else if (_tool.Zone != Zone.None)
                {
                    // the rectangle from the first corner to the cursor (or just the hovered tile before the first click)
                    var a = _zoneAnchor ?? h;
                    int x0 = Mathf.Min(a.x, h.x), x1 = Mathf.Max(a.x, h.x), y0 = Mathf.Min(a.y, h.y), y1 = Mathf.Max(a.y, h.y);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) _overlay.AddTile(World, x, y, _rot, World.ZoneCheck(_tool.Zone, x, y).Ok ? Ok : Bad);
                }
                else _overlay.AddTile(World, h.x, h.y, _rot, Hover);
            }
            _overlay.Apply();
        }

        // ---------- Text ----------
        static string ZoneHelp(Zone z) => z switch
        {
            Zone.Res => "Housing zone: homes grow on it beside a road when houses are nearly full.",
            Zone.Farm => "Farmland: mark fertile tiles within 3 tiles of a farmhouse. Set the crop mix in Farming.",
            Zone.Wood => "Lumber: mark forest for crews from a nearby woodcutter lodge.",
            _ => "Mining: mark deposits for a nearby quarry or iron mine.",
        };

        string DemandLine()
        {
            string D(Zone z, string name) { double v = World.Dem[z]; return $"{name} {(v > DemandThresholdShown ? "+" : v < -0.05 ? "-" : " ")}{System.Math.Abs(v) * 100:0}%"; }
            return "Demand   " + D(Zone.Res, "Homes");
        }
        const double DemandThresholdShown = World.DemandThreshold;

        static string CostText(string type) => string.Join(", ", CostOf(type, PlacementSize(type)).Select(kv => $"{kv.Value} {kv.Key.ToString().ToLower()}"));

        /// <summary>JS crewLine: what the building's crew is doing.</summary>
        static string CrewLine(Building b)
        {
            if (b.Crew.Count == 0) return "No crew yet (needs villagers and a road link)";
            int home = 0, outw = 0, at = 0, back = 0, down = 0;
            foreach (var a in b.Crew) switch (a.St) { case CrewState.Home: home++; break; case CrewState.Out: outw++; break; case CrewState.Work: at++; break; case CrewState.Down: down++; break; default: back++; break; }
            if (b.T == "mine") return $"Crew: {home} resting here, {down} underground, {outw + at + back} out surveying";
            return $"Crew: {home} resting here, {outw} walking out, {at} working, {back} carrying home";
        }

        string UndergroundLine(int x, int y)
        {
            var w = World;
            if (!w.IsSurveyed(x, y))
                return w.ZoneMap[y, x] == Zone.Mine ? "Underground: not surveyed yet (a mine's miners will survey it)" : "Underground: unknown. Paint a Mining zone (7) to have it surveyed";
            var parts = new List<string>();
            for (int l = 1; l <= World.MaxDepth; l++) { double o = w.KnownOre(x, y, l); parts.Add($"band {l} " + (o > 1e-9 ? $"{o:0} ore" : "none")); }
            return "Underground (surveyed): " + string.Join(", ", parts);
        }

        string TileInfo(int x, int y)
        {
            var w = World; var lines = new List<string>();
            var b = w.Grid[y, x];
            if (b != null && b.T != "road")
            {
                lines.Add(b.Name.ToUpper());
                lines.Add(b.Served ? "Connected to the Town Hall" : "Not connected: needs a road to the Town Hall");
                if (b.T == "house") lines.Add($"Homes {b.Capacity} villagers. Level {b.Lvl}");
                if (b.Jobs > 0) lines.Add($"Workers {b.Workers}/{b.Jobs} ({b.Eff * 100:0}%)");
                if (World.CrewKind(b.T) != Deco.None) lines.Add(CrewLine(b));
                if (b.T == "mine") lines.Add(w.MineStatus(b));
                if (b.T == "hall") lines.Add($"Pit-saws 1 log a day at half yield. Houses {HallCap}.");
                if (b.T == "silo")
                    lines.Add(b.Served ? $"+{World.SiloFood} wheat and +{World.SiloFlax} flax storage" : "Connect to the Town Hall to use its crop storage.");
                if (b.T == "market") lines.Add("Trading is paused.");
            }
            else if (b != null) lines.Add(b.Served ? "ROAD (connected)" : "ROAD (cut off from the Town Hall)");
            var d = w.DecoMap[y, x];
            if (d == Deco.Tree) lines.Add("FOREST. Paint a Lumber zone (6) so a woodcutter lodge fells it.");
            if (d == Deco.Rock) lines.Add(w.IsMountain(x, y) ? $"MOUNTAIN. Stone left {w.RockLeft(x, y):0}. Quarry it from the edge." : $"STONE OUTCROP. Stone left {w.RockLeft(x, y):0}.");
            if (d == Deco.Ore) lines.Add(w.IsSurveyed(x, y) ? "IRON SHOWING. Ore shows at the surface here." : "IRON SHOWING. Ore shows at the surface here; more lies inside the hill. Paint a Mining zone (7) over it and the high ground, with a lot at the foot for the mine");
            lines.Add(UndergroundLine(x, y));
            if (d == Deco.Stump) lines.Add("Stump. It rots away into open ground.");
            if (d == Deco.Sapling) lines.Add("Sapling. It grows into a tree in three years.");
            if (w.ZoneMap[y, x] == Zone.Farm)
                lines.Add(w.IsFlaxField(x, y) ? "Flax field. Produces flax." : "Wheat field. Produces food.");
            else if (w.ZoneMap[y, x] != Zone.None) lines.Add(w.ZoneMap[y, x] + " zone");
            lines.Add($"Tile {x},{y}   level {w.TileLevel(x, y):0.##}" + (w.Fert[y, x] ? "   fertile soil" : "") + (w.TileSpread(x, y) >= 3 ? "   too steep" : ""));
            return string.Join("\n", lines);
        }
    }
}
