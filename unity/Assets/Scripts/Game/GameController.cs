// Runs the settlement: owns the World, ticks it (1 day = 1 second at 1x, like the browser), redraws on change,
// and turns mouse/keyboard into tools. Tools sit in build-bar categories (Cats, Tools): 1 Roads, 2 Housing, 3 Farming,
// 4 Forestry, 5 Mining, 6 Trade, 7 Military, 0 or Delete Bulldoze; pressing a category key again picks its next tool.
// Esc or a right-click (without dragging) cancels the half-drawn road or rectangle, then puts the tool away. Q/E rotate.
// Homes are not placed by hand: they grow on Housing zones beside roads when there is demand (World.Develop).
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
        /// <summary>Build bar categories, grouped by what the player is doing: each production building sits beside the zone it works.</summary>
        public enum Cat { Inspect, Roads, Housing, Farming, Forestry, Mining, Trade, Military, Bulldoze }

        public class CatDef
        {
            public Cat Cat; public string Label, Key, Desc, Icon; public Key Hotkey, Alt;
        }

        public class ToolDef
        {
            public string Id, Label, Desc; public Cat Cat;
            public string Build; public Zone Zone; public bool Road, Clear, Unzone;
        }

        public static readonly CatDef[] Cats =
        {
            new CatDef { Cat = Cat.Inspect, Label = "Inspect", Key = "Esc", Hotkey = Key.Escape, Desc = "Hover or click a tile to see what it is and what it needs." },
            new CatDef { Cat = Cat.Roads, Label = "Roads", Key = "1", Hotkey = Key.Digit1, Desc = "Roads link everything to the Town Hall." },
            new CatDef { Cat = Cat.Housing, Label = "Housing", Key = "2", Hotkey = Key.Digit2, Icon = "house_1", Desc = "Zone land where villagers build their own homes." },
            new CatDef { Cat = Cat.Farming, Label = "Farming", Key = "3", Hotkey = Key.Digit3, Icon = "farm2", Desc = "Farmhouses, the wheat and flax fields they work, and silos to store the crop." },
            new CatDef { Cat = Cat.Forestry, Label = "Forestry", Key = "4", Hotkey = Key.Digit4, Icon = "wood2", Desc = "Woodcutter lodges and the forest they fell, foresters to replant, sawmills to turn logs into wood." },
            new CatDef { Cat = Cat.Mining, Label = "Mining", Key = "5", Hotkey = Key.Digit5, Icon = "mine", Desc = "Quarries for stone and mines for iron, and the Mining zones they work." },
            new CatDef { Cat = Cat.Trade, Label = "Trade", Key = "6", Hotkey = Key.Digit6, Icon = "market", Desc = "Markets." },
            new CatDef { Cat = Cat.Military, Label = "Military", Key = "7", Hotkey = Key.Digit7, Icon = "barracks", Desc = "Barracks that train soldiers." },
            new CatDef { Cat = Cat.Bulldoze, Label = "Bulldoze", Key = "0", Hotkey = Key.Digit0, Alt = Key.Delete, Desc = "Demolish buildings and roads, clear trees and rocks, or remove zones." },
        };

        public static readonly ToolDef[] Tools =
        {
            new ToolDef { Id = "look", Cat = Cat.Inspect, Label = "Inspect", Desc = "Hover or click a tile to see what it is and what it needs." },
            new ToolDef { Id = "road", Cat = Cat.Roads, Label = "Dirt Road", Road = true,
                Desc = "Press on a tile and drag: an L-shaped road follows the mouse and is built when you let go. Homes and work buildings need a road back to the Town Hall. Roads climb hills but not mountains, and cannot go through forest." },
            new ToolDef { Id = "zoneRes", Cat = Cat.Housing, Label = "Housing Zone", Zone = Zone.Res,
                Desc = "Drag over land to mark it for homes. Villagers build houses here by themselves, on tiles touching a connected road, when the town needs room." },
            new ToolDef { Id = "farmhouse", Cat = Cat.Farming, Label = "Farmhouse", Build = "farm",
                Desc = "Its farmers work the wheat and flax fields within 3 tiles. Each farmer tends 2 fields. Needs a road to the Town Hall." },
            new ToolDef { Id = "zoneFarm", Cat = Cat.Farming, Label = "Farmland", Zone = Zone.Farm,
                Desc = "Drag over fertile land near a farmhouse. Set the wheat and flax percentages in the Farming menu above the build bar." },
            new ToolDef { Id = "silo", Cat = Cat.Farming, Label = "Silo", Build = "silo",
                Desc = "Stores 180 more wheat and 60 more flax. Needs a road to the Town Hall." },
            new ToolDef { Id = "lodge", Cat = Cat.Forestry, Label = "Woodcutter Lodge", Build = "wood",
                Desc = "Woodcutters walk out to trees in Lumber zones within 3 tiles, fell them and carry the logs home. Needs a road to the Town Hall." },
            new ToolDef { Id = "zoneWood", Cat = Cat.Forestry, Label = "Lumber Zone", Zone = Zone.Wood,
                Desc = "Drag over forest to have it felled by a nearby lodge, or over open ground for a forester to replant. The shaded area shows where lodges and foresters reach." },
            new ToolDef { Id = "forester", Cat = Cat.Forestry, Label = "Forester", Build = "forester",
                Desc = "Plants saplings on Lumber-zoned open ground within 4 tiles, so the forest grows back." },
            new ToolDef { Id = "sawmill", Cat = Cat.Forestry, Label = "Sawmill", Build = "sawmill",
                Desc = "Saws 4 logs a day into wood at full yield (the Town Hall saws 1 at half yield). Needs a road." },
            new ToolDef { Id = "quarry", Cat = Cat.Mining, Label = "Quarry", Build = "quarry",
                Desc = "Quarrymen dig stone from Mining zones within 3 tiles: rock outcrops, hills, and mountains from their edge inward. Needs a road." },
            new ToolDef { Id = "mine", Cat = Cat.Mining, Label = "Iron Mine", Build = "mine",
                Desc = "Miners survey Mining zones within 3 tiles for iron, then tunnel into the hill. Place it at the foot of high ground; its entrance faces away from the hill and needs a road there." },
            new ToolDef { Id = "zoneMine", Cat = Cat.Mining, Label = "Mining Zone", Zone = Zone.Mine,
                Desc = "Drag over rock, hills and mountains for a nearby quarry or mine to work. The shaded area shows where they reach." },
            new ToolDef { Id = "market", Cat = Cat.Trade, Label = "Market", Build = "market",
                Desc = "Must touch a road. Trading is paused for now." },
            new ToolDef { Id = "barracks", Cat = Cat.Military, Label = "Barracks", Build = "barracks",
                Desc = "Trains soldiers from your peasants. Soldiers eat more and do no other work. Must touch a road." },
            new ToolDef { Id = "clear", Cat = Cat.Bulldoze, Label = "Demolish", Clear = true,
                Desc = "Click or drag over buildings, roads, zones, trees and rocks to remove them. Buildings give back half their cost. The Town Hall stays." },
            new ToolDef { Id = "unzone", Cat = Cat.Bulldoze, Label = "Remove Zones", Unzone = true,
                Desc = "Drag a rectangle to take the zone off open land. Buildings are left standing." },
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
        Vector2Int? _hover, _roadAnchor, _lastPaint, _zoneAnchor;   // _zoneAnchor: first corner of a zone (or unzone) rectangle
        bool _zoneDragging;          // the button is still held since the first corner (drag mode); false = click-click mode
        Vector2? _rightDown;         // where the right button went down, to tell a click (cancel) from a camera drag
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
            _hud.Build(Tools, Cats, _art);
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
            else _hud.Toast("Welcome back to Crown & Hearth. Pick a category along the bottom to build; the Tutorial button walks through the game.", 6);
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
            _hud.SetDemand(World.Dem[Zone.Res]);
            var tip = CursorTip(); _hud.SetCursorTip(tip.text, tip.bad, Mouse.current?.position.ReadValue() ?? Vector2.zero);
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
            if (kb.escapeKey.wasPressedThisFrame) Cancel();
            foreach (var c in Cats)
                if (c.Cat != Cat.Inspect && (kb[c.Hotkey].wasPressedThisFrame || (c.Alt != Key.None && kb[c.Alt].wasPressedThisFrame))) _hud.CategoryKey(c.Cat);
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
            const string stop = "   ·   Right-click or Esc to stop";
            string hint = t.Build != null ? $"{t.Label} ({Hud.CostText(t.Build)}): click a spot to build it." + (ReachOf(t.Build) > 0 ? " The shaded ring is the land it works." : "") + stop
                : t.Road ? $"Road (1 wood a tile): press and drag from a road to lay one.{stop}"
                : t.Zone != Zone.None ? $"{t.Label}: drag over the land, or click two corners. Green tiles will be zoned, red ones cannot be.{stop}"
                : t.Unzone ? $"Remove Zones: drag over zoned land. Buildings stay.{stop}"
                : t.Clear ? $"Demolish: click or drag over what you want removed.{stop}"
                : "Inspect: hover or click a tile. Pick a category along the bottom to build.";
            _tool = t; _roadAnchor = null; _zoneAnchor = null; _zoneDragging = false; _hud.SetTool(t, hint);
        }

        /// <summary>Esc or a right-click: drop the half-drawn road or rectangle first; otherwise put the tool away.</summary>
        void Cancel()
        {
            if (_roadAnchor.HasValue || _zoneAnchor.HasValue) { _roadAnchor = null; _zoneAnchor = null; _zoneDragging = false; return; }
            SetTool(Tools[0]); _hud.CloseTray();
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

            // right button: a click (barely moved) cancels; a drag pans the camera (CameraRig)
            var mp = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame) _rightDown = overUi ? (Vector2?)null : mp;
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                if (_rightDown.HasValue && (mp - _rightDown.Value).sqrMagnitude < 36) Cancel();
                _rightDown = null;
            }

            if (_tool.Zone != Zone.None || _tool.Unzone)
            {
                // zoning: press, drag and let go to zone the rectangle; or click one corner, then click the opposite corner
                if (mouse.leftButton.wasPressedThisFrame && _hover.HasValue && !overUi)
                {
                    if (_zoneAnchor.HasValue && !_zoneDragging) { ApplyRect(_zoneAnchor.Value, _hover.Value); return; }
                    _zoneAnchor = _hover; _zoneDragging = true;
                }
                if (mouse.leftButton.wasReleasedThisFrame && _zoneDragging)
                {
                    _zoneDragging = false;
                    if (_zoneAnchor.HasValue && _hover.HasValue && _hover.Value != _zoneAnchor.Value) ApplyRect(_zoneAnchor.Value, _hover.Value);
                    // let go on the same tile: it stays as the first corner, and the next click is the opposite corner
                }
                return;
            }

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
            else if (_tool.Clear)
            {
                if (_lastPaint == h) return;
                _lastPaint = h;
                var r = World.Remove(h.x, h.y);
                if (!r.Ok && mouse.leftButton.wasPressedThisFrame) _hud.Toast(r.Reason);
            }
        }

        void ApplyRect(Vector2Int a, Vector2Int b)
        {
            _zoneAnchor = null; _zoneDragging = false;
            if (_tool.Unzone)
            {
                int n = World.ClearZoneRect(a.x, a.y, b.x, b.y);
                _hud.Toast(n == 0 ? "No zoned open land there." : $"Removed the zone from {n} tile{(n == 1 ? "" : "s")}.");
                return;
            }
            var (zoned, skipped, why) = World.ZoneRect(_tool.Zone, a.x, a.y, b.x, b.y);
            _hud.Toast(zoned == 0 && skipped > 0 ? why : $"Zoned {zoned} tile{(zoned == 1 ? "" : "s")}." + (skipped > 0 ? $" {skipped} skipped: {why}" : ""), 5);
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

        static readonly Color32 Reach = new Color32(110, 190, 255, 55), ReachNew = new Color32(255, 230, 120, 70), Unzone = new Color32(255, 150, 60, 110);

        /// <summary>How far a work building reaches, in tiles around its footprint (0 = it has no reach). Foresters count from their own
        /// tile (World.ForesterCovers); everything else from the footprint's edge (crews, mines, farm fields).</summary>
        static int ReachOf(string type) => type switch
        {
            "farm" => World.FarmFieldRange, "wood" => WorkR, "quarry" => WorkR, "mine" => WorkR, "forester" => ForesterR, _ => 0,
        };

        void AddReach(string type, int bx, int by, int sz, Color32 col, HashSet<int> seen)
        {
            int r = ReachOf(type); if (r == 0) return;
            for (int y = by - r; y <= by + sz - 1 + r; y++)
                for (int x = bx - r; x <= bx + sz - 1 + r; x++)
                {
                    if (!World.Inb(x, y) || (x >= bx && x < bx + sz && y >= by && y < by + sz)) continue;
                    if (seen != null && !seen.Add(y * N + x)) continue;
                    _overlay.AddTile(World, x, y, _rot, col);
                }
        }

        /// <summary>The buildings whose reach matters to this zone tool: farmhouses for fields, lodges and foresters for lumber, quarries and mines for mining.</summary>
        static bool Works(Zone z, string type) => z switch
        {
            Zone.Farm => type == "farm",
            Zone.Wood => type == "wood" || type == "forester",
            Zone.Mine => type == "quarry" || type == "mine",
            _ => false,
        };

        (int x0, int y0, int x1, int y1) RectTo(Vector2Int h)
        {
            var a = _zoneAnchor ?? h;
            return (Mathf.Min(a.x, h.x), Mathf.Min(a.y, h.y), Mathf.Max(a.x, h.x), Mathf.Max(a.y, h.y));
        }

        void DrawOverlay()
        {
            _overlay.Clear();
            if (_ugLevel > 0) DrawUnderground();
            // zone tools: shade where the buildings that work this zone can reach, so the player knows where to paint
            if (_tool.Zone == Zone.Farm || _tool.Zone == Zone.Wood || _tool.Zone == Zone.Mine)
            {
                var seen = new HashSet<int>();
                foreach (var b in World.Blds) if (Works(_tool.Zone, b.T)) AddReach(b.T, b.X, b.Y, b.Sz, Reach, seen);
            }
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
                    AddReach(_tool.Build, h.x, h.y, sz, ReachNew, null);
                    for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) _overlay.AddTile(World, h.x + i, h.y + j, _rot, ok ? Ok : Bad);
                }
                else if (_tool.Zone != Zone.None)
                {
                    // the rectangle from the first corner to the cursor (or just the hovered tile before the first click)
                    var (x0, y0, x1, y1) = RectTo(h);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) _overlay.AddTile(World, x, y, _rot, World.ZoneCheck(_tool.Zone, x, y).Ok ? Ok : Bad);
                }
                else if (_tool.Unzone)
                {
                    var (x0, y0, x1, y1) = RectTo(h);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                            _overlay.AddTile(World, x, y, _rot, World.Grid[y, x] == null && World.ZoneMap[y, x] != Zone.None ? Unzone : Hover);
                }
                else if (_tool.Clear) _overlay.AddTile(World, h.x, h.y, _rot, Bad);
                else
                {
                    // inspecting a work building shows its reach too
                    var b = World.Grid[h.y, h.x];
                    if (b != null && ReachOf(b.T) > 0) AddReach(b.T, b.X, b.Y, b.Sz, Reach, null);
                    _overlay.AddTile(World, h.x, h.y, _rot, Hover);
                }
            }
            _overlay.Apply();
        }

        static string ZoneName(Zone z, bool flax = false) => z switch
        {
            Zone.Res => "Housing", Zone.Farm => flax ? "Flax field" : "Wheat field", Zone.Wood => "Lumber", Zone.Mine => "Mining", Zone.Comm => "Commerce", _ => "No",
        };

        /// <summary>The line beside the mouse: what a click will do here, or why it cannot (bad = red).</summary>
        (string text, bool bad) CursorTip()
        {
            if (!_hover.HasValue || _tool.Id == "look") return (null, false);
            var h = _hover.Value;
            if (_tool.Road)
            {
                var path = World.LPath((_roadAnchor ?? h).x, (_roadAnchor ?? h).y, h.x, h.y);
                var r = World.RoadPathCheck(path);
                if (!r.Ok) return (r.Reason, true);
                int n = r.Count;
                return _roadAnchor.HasValue ? ($"{n} new tile{(n == 1 ? "" : "s")}, {n} wood. Let go to build", false) : ("Press and drag to lay a road", false);
            }
            if (_tool.Build != null)
            {
                var r = World.CanPlace(_tool.Build, h.x, h.y);
                return r.Ok ? ($"Click to build: {Hud.CostText(_tool.Build)}", false) : (r.Reason, true);
            }
            if (_tool.Zone != Zone.None)
            {
                var (x0, y0, x1, y1) = RectTo(h);
                int ok = 0, bad = 0; string why = null;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        var r = World.ZoneCheck(_tool.Zone, x, y);
                        if (r.Ok) ok++; else { bad++; why ??= r.Reason; }
                    }
                if (!_zoneAnchor.HasValue) return ok > 0 ? ("Press and drag over the land", false) : (why, true);
                string size = $"{x1 - x0 + 1} x {y1 - y0 + 1}: ";
                return ok == 0 ? (size + why, true) : (size + $"{ok} tile{(ok == 1 ? "" : "s")} to zone" + (bad > 0 ? $", {bad} cannot be ({why})" : ""), false);
            }
            if (_tool.Unzone)
            {
                var (x0, y0, x1, y1) = RectTo(h);
                int n = 0;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (World.Grid[y, x] == null && World.ZoneMap[y, x] != Zone.None) n++;
                return _zoneAnchor.HasValue ? ($"Remove the zone from {n} tile{(n == 1 ? "" : "s")}", n == 0) : ("Press and drag over zoned land", false);
            }
            if (_tool.Clear)
            {
                var b = World.Grid[h.y, h.x];
                if (b != null)
                {
                    if (b.T == "hall") return ("The Town Hall cannot be removed", true);
                    var refund = string.Join(", ", CostOf(b.T, b.Sz).Where(kv => kv.Value >= 2).Select(kv => $"{kv.Value / 2} {kv.Key.ToString().ToLower()}"));
                    return ($"Demolish the {b.Name}" + (refund.Length > 0 ? $" (get back {refund})" : ""), false);
                }
                if (World.ZoneMap[h.y, h.x] != Zone.None) return ($"Remove the {ZoneName(World.ZoneMap[h.y, h.x], World.IsFlaxField(h.x, h.y))} zone", false);
                return World.DecoMap[h.y, h.x] switch
                {
                    Deco.Tree => ("Cut down the tree (2 logs)", false),
                    Deco.Rock => World.IsMountain(h.x, h.y) ? ("A mountain cannot be cleared by hand", true) : World.PitDepth(h.x, h.y) > 0 ? ("A quarry pit cannot be filled in", true) : ("Clear the rock (1 stone)", false),
                    Deco.Ore => ("Clear the ore showing (1 ore)", false),
                    Deco.Stump => ("Clear the stump", false),
                    Deco.Sapling => ("Clear the sapling", false),
                    _ => ("Nothing to clear here", true),
                };
            }
            return (null, false);
        }

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
                return w.ZoneMap[y, x] == Zone.Mine ? "Underground: not surveyed yet (a mine's miners will survey it)" : "Underground: unknown. Zone it for mining (Mining, 5) to have it surveyed";
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
            // a short heading line of its own (the info panel shows the first line as its title), then the explanation
            if (d == Deco.Tree) { lines.Add("FOREST"); lines.Add("Zone it for lumber (Forestry, 4) so a woodcutter lodge fells it."); }
            if (d == Deco.Rock) { lines.Add(w.IsMountain(x, y) ? "MOUNTAIN" : "STONE OUTCROP"); lines.Add(w.IsMountain(x, y) ? $"Stone left {w.RockLeft(x, y):0}. Quarry it from the edge." : $"Stone left {w.RockLeft(x, y):0}."); }
            if (d == Deco.Ore) { lines.Add("IRON SHOWING"); lines.Add(w.IsSurveyed(x, y) ? "Ore shows at the surface here." : "Ore shows at the surface here; more lies inside the hill. Zone it for mining (Mining, 5) with the high ground, with a lot at the foot for the mine."); }
            lines.Add(UndergroundLine(x, y));
            if (d == Deco.Stump) lines.Add("Stump. It rots away into open ground.");
            if (d == Deco.Sapling) lines.Add("Sapling. It grows into a tree in three years.");
            if (w.ZoneMap[y, x] == Zone.Farm)
                lines.Add(w.IsFlaxField(x, y) ? "Flax field. Produces flax." : "Wheat field. Produces food.");
            else if (w.ZoneMap[y, x] != Zone.None) lines.Add(ZoneName(w.ZoneMap[y, x]) + " zone");
            lines.Add($"Tile {x},{y}   level {w.TileLevel(x, y):0.##}" + (w.Fert[y, x] ? "   fertile soil" : "") + (w.TileSpread(x, y) >= 3 ? "   too steep" : ""));
            return string.Join("\n", lines);
        }
    }
}
