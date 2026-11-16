// Runs the settlement: owns the World, ticks it (1 day = 1 second at 1x, like the browser), redraws on change,
// and turns mouse/keyboard into tools. Hotkeys follow the browser where it has them (1 Look, 2 Road, 8 Well, X Sawmill,
// F Forester, 9 Barracks, 3 Housing, 4 Farmland, 5 Commerce, 6 Lumber, 7 Mining zones, 0 Clear, Q/E rotate).
// As in the browser, homes, farms, markets, woodcutter lodges, quarries and mines are not placed by hand: they grow on
// painted zones beside roads when there is demand (World.Develop).
using System.Collections.Generic;
using System.Linq;
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
            new ToolDef { Id = "zoneComm", Label = "Commerce", Key = "5", Hotkey = Key.Digit5, Zone = Zone.Comm },
            new ToolDef { Id = "zoneWood", Label = "Lumber", Key = "6", Hotkey = Key.Digit6, Zone = Zone.Wood },
            new ToolDef { Id = "zoneMine", Label = "Mining", Key = "7", Hotkey = Key.Digit7, Zone = Zone.Mine },
            new ToolDef { Id = "forester", Label = "Forester", Key = "F", Hotkey = Key.F, Build = "forester" },
            new ToolDef { Id = "sawmill", Label = "Sawmill", Key = "X", Hotkey = Key.X, Build = "sawmill" },
            new ToolDef { Id = "well", Label = "Well", Key = "8", Hotkey = Key.Digit8, Build = "well" },
            new ToolDef { Id = "barracks", Label = "Barracks", Key = "9", Hotkey = Key.Digit9, Build = "barracks" },
            new ToolDef { Id = "clear", Label = "Clear", Key = "0", Hotkey = Key.Digit0, Clear = true },
        };

        [Tooltip("Sprite-Unlit material (URP 2D) so the palette shows exactly as drawn.")]
        public Material SpriteMaterial;
        public int Seed = 7;

        public World World { get; private set; }
        int _rot, _drawnVersion = -1, _drawnRot = -1, _speed = 1;
        float _dayAcc;
        ToolDef _tool = Tools[0];
        Vector2Int? _hover, _roadAnchor, _lastPaint;
        ArtLibrary _art;
        GroundRenderer _ground; ObjectRenderer _objects; OverlayRenderer _overlay; AgentRenderer _agents;
        CameraRig _cam; Hud _hud;

        void Start()
        {
            World = new World(Seed);
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
            _hud.ToolPicked += id => SetTool(Tools.First(t => t.Id == id));
            _hud.SpeedPicked += s => { _speed = s; _hud.SetSpeed(s); };
            _hud.RotatePicked += Rotate;
            _hud.PrioMoved += MovePrio;
            _hud.SetTool(_tool.Id); _hud.SetSpeed(_speed);
            _hud.Toast("Welcome to Crown & Hearth. Right-drag or WASD to pan, scroll to zoom, Q/E to rotate.", 6);
        }

        void Update()
        {
            HandleKeys();
            bool overUi = _hud.PointerOverUi;
            _cam.Tick(!overUi);
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
            DrawOverlay();
            _hud.SetResources(ResourceLine());
            _hud.SetDemand(DemandLine());
            _hud.SetInfo(_hover.HasValue ? TileInfo(_hover.Value.x, _hover.Value.y) : "");
            _hud.RefreshWorkforce(World);
        }

        void HandleKeys()
        {
            var kb = Keyboard.current; if (kb == null) return;
            foreach (var t in Tools) if (kb[t.Hotkey].wasPressedThisFrame) SetTool(t);
            if (kb.escapeKey.wasPressedThisFrame) SetTool(Tools[0]);
            if (kb.qKey.wasPressedThisFrame) Rotate(-1);
            if (kb.eKey.wasPressedThisFrame) Rotate(1);
            if (kb.spaceKey.wasPressedThisFrame) { _speed = _speed == 0 ? 1 : 0; _hud.SetSpeed(_speed); }
        }

        void SetTool(ToolDef t)
        {
            _tool = t; _roadAnchor = null; _hud.SetTool(t.Id);
            if (t.Build != null) _hud.Toast($"{Defs.Names[t.Build]}: {CostText(t.Build)}");
            if (t.Zone != Zone.None) _hud.Toast(ZoneHelp(t.Zone) + " " + World.Note[t.Zone], 6);
        }

        void Rotate(int dir)
        {
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
            else if (_tool.Zone != Zone.None || _tool.Clear)
            {
                if (_lastPaint == h) return;
                _lastPaint = h;
                var r = _tool.Clear ? World.Remove(h.x, h.y) : World.SetZone(_tool.Zone, h.x, h.y);
                if (!r.Ok && mouse.leftButton.wasPressedThisFrame) _hud.Toast(r.Reason);
            }
        }

        static readonly Color32 Ok = new Color32(120, 255, 120, 110), Bad = new Color32(255, 90, 80, 120), Hover = new Color32(255, 255, 255, 70);

        void DrawOverlay()
        {
            _overlay.Clear();
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
                    int sz = DefaultSize(_tool.Build);
                    for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) _overlay.AddTile(World, h.x + i, h.y + j, _rot, ok ? Ok : Bad);
                }
                else if (_tool.Zone != Zone.None) _overlay.AddTile(World, h.x, h.y, _rot, World.ZoneCheck(_tool.Zone, h.x, h.y).Ok ? Ok : Bad);
                else _overlay.AddTile(World, h.x, h.y, _rot, Hover);
            }
            _overlay.Apply();
        }

        // ---------- Text ----------
        string ResourceLine()
        {
            var w = World; var d = w.D;
            string R(Res r, string name)
            {
                double rate = w.Rate[r];
                return $"{name} {Mathf.FloorToInt((float)w.ResAmt[r])}" + (System.Math.Abs(rate) >= 0.05 ? $" ({(rate > 0 ? "+" : "")}{rate:0.0})" : "");
            }
            return $"Day {w.Day}   " + R(Res.Food, "Food") + "   " + R(Res.Wood, "Wood") + "   " + R(Res.Logs, "Logs") + "   " + R(Res.Stone, "Stone") + "   " +
                   R(Res.Ore, "Ore") + "   " + R(Res.Gold, "Gold") + $"     Peasants {w.Peasants}/{d.Civ}   Idle {d.Idle}   Jobs {d.Employed}/{d.Jobs}" +
                   (w.Soldiers > 0 ? $"   Soldiers {w.Soldiers}" : "") + (w.Starving ? "   STARVING" : "");
        }

        static string ZoneHelp(Zone z) => z switch
        {
            Zone.Res => "Housing zone: homes grow on it beside a road when houses are nearly full.",
            Zone.Farm => "Farmland zone (fertile soil only): farms grow when food runs low.",
            Zone.Comm => "Commerce zone: markets grow near homes that have no market.",
            Zone.Wood => "Lumber zone: paint forest; a woodcutter lodge grows beside a road and fells it.",
            _ => "Mining zone: paint rock or ore; quarries and mines grow beside a road.",
        };

        string DemandLine()
        {
            string D(Zone z, string name) { double v = World.Dem[z]; return $"{name} {(v > DemandThresholdShown ? "+" : v < -0.05 ? "-" : " ")}{System.Math.Abs(v) * 100:0}%"; }
            return "Demand   " + D(Zone.Res, "Homes") + "   " + D(Zone.Farm, "Farms") + "   " + D(Zone.Comm, "Markets") + "   " + D(Zone.Wood, "Lumber") + "   " + D(Zone.Mine, "Mining");
        }
        const double DemandThresholdShown = World.DemandThreshold;

        static string CostText(string type) => string.Join(", ", CostOf(type, 1).Select(kv => $"{kv.Value} {kv.Key.ToString().ToLower()}"));

        /// <summary>JS crewLine: what the building's crew is doing.</summary>
        static string CrewLine(Building b)
        {
            if (b.Crew.Count == 0) return "No crew yet (needs villagers and a road link)";
            int home = 0, outw = 0, at = 0, back = 0;
            foreach (var a in b.Crew) switch (a.St) { case CrewState.Home: home++; break; case CrewState.Out: outw++; break; case CrewState.Work: at++; break; default: back++; break; }
            return $"Crew: {home} resting here, {outw} walking out, {at} working, {back} carrying home";
        }

        string TileInfo(int x, int y)
        {
            var w = World; var lines = new List<string>();
            var b = w.Grid[y, x];
            if (b != null && b.T != "road")
            {
                lines.Add(b.Name.ToUpper());
                lines.Add(b.Served ? "Connected to the Town Hall" : "Not connected: needs a road to the Town Hall");
                if (b.T == "house") lines.Add($"Homes {b.Capacity} villagers. Level {b.Lvl}" + (b.Lvl < 2 ? " (a well within 4 tiles raises it)" : "") + (b.Covered ? ". Market nearby" : ""));
                if (b.Jobs > 0) lines.Add($"Workers {b.Workers}/{b.Jobs} ({b.Eff * 100:0}%)");
                if (World.CrewKind(b.T) != Deco.None) lines.Add(CrewLine(b));
                if (b.T == "hall") lines.Add($"Pit-saws 1 log a day at half yield. Houses {HallCap}.");
            }
            else if (b != null) lines.Add(b.Served ? "ROAD (connected)" : "ROAD (cut off from the Town Hall)");
            var d = w.DecoMap[y, x];
            if (d == Deco.Tree) lines.Add("FOREST. Paint a Lumber zone (6) so a woodcutter lodge fells it.");
            if (d == Deco.Rock) lines.Add(w.IsMountain(x, y) ? $"MOUNTAIN. Stone left {w.RockLeft(x, y):0}. Quarry it from the edge." : $"STONE OUTCROP. Stone left {w.RockLeft(x, y):0}.");
            if (d == Deco.Ore) lines.Add($"IRON ORE. Ore left {w.OreLeft(x, y):0}. Zone for mining (7) beside an iron mine.");
            if (d == Deco.Stump) lines.Add("Stump. It rots away into open ground.");
            if (d == Deco.Sapling) lines.Add("Sapling. It grows into a tree in three years.");
            if (w.ZoneMap[y, x] != Zone.None) lines.Add(w.ZoneMap[y, x] + " zone");
            lines.Add($"Tile {x},{y}   level {w.TileLevel(x, y):0.##}" + (w.Fert[y, x] ? "   fertile soil" : "") + (w.TileSpread(x, y) >= 3 ? "   too steep" : ""));
            return string.Join("\n", lines);
        }
    }
}
