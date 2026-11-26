// Runs the settlement: owns the World, ticks it (1 day = 1 second at 1x, like the browser), redraws on change,
// and turns mouse/keyboard into tools. Tools sit in build-bar categories (Cats, Tools): 1 Roads, 2 Housing, 3 Farming,
// 4 Wood, 5 Hunting, 6 Mining, 7 Trade, 8 Military, 9 Water, 0 or Delete Bulldoze; pressing a category key again picks its next tool.
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
        public enum Cat { Inspect, Roads, Housing, Farming, Forestry, Hunting, Mining, Trade, Military, Water, Land, Bulldoze }

        public class CatDef
        {
            public Cat Cat; public string Label, Key, Desc, Icon; public Key Hotkey, Alt;
        }

        public class ToolDef
        {
            public string Id, Label, Desc; public Cat Cat;
            public string Build; public Zone Zone; public bool Road, Clear, Unzone, Claim, Wall, Bridge, Canal, Moat, FerryRoute;
            public string LandOp;   // "clear" / "protect" / "unprotect": drag a rectangle (World.Clearing.cs, World.Forestry.cs)
        }

        public static readonly CatDef[] Cats =
        {
            new CatDef { Cat = Cat.Inspect, Label = "Inspect", Key = "Esc", Hotkey = Key.Escape, Desc = "Hover or click a tile to see what it is and what it needs." },
            new CatDef { Cat = Cat.Roads, Label = "Roads", Key = "1", Hotkey = Key.Digit1, Desc = "Roads link everything to the Town Hall." },
            new CatDef { Cat = Cat.Housing, Label = "Housing", Key = "2", Hotkey = Key.Digit2, Icon = "house_1", Desc = "Zone land where villagers build their own homes." },
            new CatDef { Cat = Cat.Farming, Label = "Farming", Key = "3", Hotkey = Key.Digit3, Icon = "farm2", Desc = "Farmhouses, the wheat and flax fields they work, and silos to store the crop." },
            new CatDef { Cat = Cat.Forestry, Label = "Wood", Key = "4", Hotkey = Key.Digit4, Icon = "wood2", Desc = "Fell trees for logs, replant forests, and saw logs into wood." },
            new CatDef { Cat = Cat.Hunting, Label = "Hunting", Key = "5", Hotkey = Key.Digit5, Icon = "hunter", Desc = "Hunting lodges bring back food from the forest all year, even in winter." },
            new CatDef { Cat = Cat.Mining, Label = "Mining", Key = "6", Hotkey = Key.Digit6, Icon = "mine", Desc = "Quarries for stone, mines for iron ore, and smitheries for metal." },
            new CatDef { Cat = Cat.Trade, Label = "Trade", Key = "7", Hotkey = Key.Digit7, Icon = "market", Desc = "Markets send trade caravans to settlements on the world map; storehouses hold more." },
            new CatDef { Cat = Cat.Military, Label = "Military", Key = "8", Hotkey = Key.Digit8, Icon = "barracks", Desc = "Muster grounds for militia, barracks for professional soldiers, palisades, and watchtowers to spot incoming raiders." },
            new CatDef { Cat = Cat.Water, Label = "Water", Key = "9", Hotkey = Key.Digit9, Icon = "well", Desc = "Wells that pump groundwater and cisterns that store it (homes need water every day, carried along the roads), fishing huts on the shore, and bridges over rivers and lakes." },
            new CatDef { Cat = Cat.Land, Label = "Land", Key = "L", Hotkey = Key.L, Desc = "Your district is 64 x 64 tiles. claim the land beyond it, one 16 x 16 parcel at a time, for free." },
            new CatDef { Cat = Cat.Bulldoze, Label = "Bulldoze", Key = "0", Hotkey = Key.Digit0, Alt = Key.Delete, Desc = "Demolish buildings and roads, clear trees and rocks, or remove zones." },
        };

        public static readonly ToolDef[] Tools =
        {
            new ToolDef { Id = "look", Cat = Cat.Inspect, Label = "Inspect", Desc = "Hover or click a tile to see what it is and what it needs." },
            new ToolDef { Id = "road", Cat = Cat.Roads, Label = "Dirt Road", Road = true,
                Desc = "Press on a tile and drag: an L-shaped road follows the mouse and is built when you let go. Homes and work buildings need a road back to the Town Hall. Roads climb hills but not mountains, and cannot go through forest." },
            new ToolDef { Id = "zoneRes", Cat = Cat.Housing, Label = "Housing Zone, Level 1", Zone = Zone.Res,
                Desc = $"Drag over land to mark it for cottages. Villagers build them here by themselves, on tiles touching a connected road, when the town needs room. A cottage costs {Cost["house"][Res.Wood]} wood and holds {HouseCap[1]} people." },
            new ToolDef { Id = "zoneRes2", Cat = Cat.Housing, Label = "Housing Zone, Level 2", Zone = Zone.Res2,
                Desc = $"Drag over land to mark it for stone houses. They grow the same way, but each costs {House2Cost[Res.Wood]} wood and {House2Cost[Res.Stone]} stone, and holds {HouseCap[2]} people. Growth waits when the town is short of stone." },
            new ToolDef { Id = "farmhouse", Cat = Cat.Farming, Label = "Farmhouse", Build = "farm",
                Desc = $"Works the Farmland within {World.FarmReach} tiles (the shaded ring), nearest first. Each farmer sows, tends and reaps {World.FieldsPerFarmer} fields a year, enough to feed about 10 people; its jobs are how many farmers it can have. Needs a road to the Town Hall." },
            new ToolDef { Id = "zoneFarm", Cat = Cat.Farming, Label = "Farmland", Zone = Zone.Farm,
                Desc = $"Drag over fertile land within {World.FarmReach} tiles of a farmhouse (shaded). Farmland out of reach lies fallow. Set the wheat and flax percentages in the Farming menu above the build bar." },
            new ToolDef { Id = "silo", Cat = Cat.Farming, Label = "Silo", Build = "silo",
                Desc = $"Stores {World.SiloFood} more wheat and {World.SiloFlax} more flax. Needs a road to the Town Hall." },
            new ToolDef { Id = "pigPen", Cat = Cat.Farming, Label = "Pig Pen", Zone = Zone.Pigs,
                Desc = "Drag a fenced pen on level ground beside a connected road. Pigs need 3 tiles each and eat wheat." },
            new ToolDef { Id = "sheepPen", Cat = Cat.Farming, Label = "Sheep Pen", Zone = Zone.Sheep,
                Desc = "Drag a fenced pen on level ground beside a connected road. Sheep need 2 tiles each and eat wheat." },
            new ToolDef { Id = "chickenPen", Cat = Cat.Farming, Label = "Chicken Pen", Zone = Zone.Chickens,
                Desc = "Drag a fenced pen on level ground beside a connected road. Chickens need 1 tile each and eat wheat." },
            new ToolDef { Id = "lodge", Cat = Cat.Forestry, Label = "Woodcutter Lodge", Build = "wood",
                Desc = $"Woodcutters fell the mature trees within {WorkR} tiles by themselves: no zone to paint. Click a lodge with Inspect to set its mode: Sustainable (the default) replants and always leaves {World.SustainFloor} trees standing, Intensive fells fast and strips the area, Land clearing works only land you mark. Protected woodland is never cut. Needs a road to the Town Hall." },
            new ToolDef { Id = "zoneWood", Cat = Cat.Forestry, Label = "Lumber Boundary (optional)", Zone = Zone.Wood,
                Desc = "Optional. Paint trees to have exactly those felled by a nearby lodge (no standing floor, no replanting). With none painted, a lodge works all the forest in reach, so most players never need this." },
            new ToolDef { Id = "forester", Cat = Cat.Forestry, Label = "Forester", Build = "forester",
                Desc = $"An optional improvement: plants saplings on open ground within {World.ForesterAutoR} tiles (or on painted Lumber ground within {ForesterR}), and its care makes nearby trees grow a quarter faster. A sustainable lodge already replants what it fells." },
            new ToolDef { Id = "sawmill", Cat = Cat.Forestry, Label = "Sawmill", Build = "sawmill",
                Desc = "Saws 4 logs a day into wood at full yield (the Town Hall saws 1 at half yield). Needs a road." },
            new ToolDef { Id = "charcoal", Cat = Cat.Forestry, Label = "Charcoal Clamp", Build = "charcoal",
                Desc = "Charcoal burners turn logs into fuel. Smiths use charcoal to smelt extra metal. Needs a connected road." },
            new ToolDef { Id = "hunter", Cat = Cat.Hunting, Label = "Hunting Lodge", Build = "hunter",
                Desc = $"This 2×2 lodge sends two hunters to bring back food year-round from reachable mature forest within {World.HuntingRange} tiles. Each forest tile holds up to {World.ForestFoodReserve} food and recovers 1 every {World.HuntingRecoveryDays} days. Needs a road to the Town Hall." },
            new ToolDef { Id = "smokehouse", Cat = Cat.Hunting, Label = "Smokehouse", Build = "smokehouse",
                Desc = "Uses logs to preserve fresh meat from hunting and livestock. The town eats smoked meat when fresh food is short." },
            new ToolDef { Id = "quarry", Cat = Cat.Mining, Label = "Quarry", Build = "quarry",
                Desc = $"Quarrymen dig the rock within {QuarryAutoR} tiles by themselves: no zone to paint. Hills and outcrops are dug down into pits, mountains are cut in from an open edge. A Mining Boundary widens its reach to {WorkR} tiles or confines it. The stone is finite. Needs a road." },
            new ToolDef { Id = "mine", Cat = Cat.Mining, Label = "Iron Mine", Build = "mine",
                Desc = "Miners dig a tunnel into the hill by themselves and take iron from its walls: no zone to paint. The Underground view opens while you place it and shows the ore in reach. Put it at the foot of high ground with a road in front. Timber props make it safer. The ore is finite and shared by every mine that reaches it." },
            new ToolDef { Id = "smithery", Cat = Cat.Mining, Label = "Smithery", Build = "smithery",
                Desc = $"Smelts {OrePerMetal} iron ore into 1 metal. Makes up to {SmeltRate} metal a day with a full crew." },
            new ToolDef { Id = "zoneMine", Cat = Cat.Mining, Label = "Mining Boundary (optional)", Zone = Zone.Mine,
                Desc = $"Optional. Paint rock or hills to widen a quarry's reach to the full {WorkR} tiles, or to confine a quarry or mine to exactly those tiles. With none painted, they work what is in their automatic area." },
            new ToolDef { Id = "clearLand", Cat = Cat.Forestry, Label = "Clear Land", LandOp = "clear",
                Desc = "Drag over trees, saplings, stumps and small rocks to have idle villagers clear them for good; they keep the logs and stone. Buildings, mountains and quarry pits are never touched, and protected woodland asks first. Build on woodland directly and the clearing is arranged for you." },
            new ToolDef { Id = "protectWood", Cat = Cat.Forestry, Label = "Protect Woodland", LandOp = "protect",
                Desc = "Drag over woodland to protect it: lodges and clearing crews skip it, and it can only be cleared after you confirm." },
            new ToolDef { Id = "unprotectWood", Cat = Cat.Forestry, Label = "Open Woodland", LandOp = "unprotect",
                Desc = "Drag over protected woodland to open it to lodges again." },
            new ToolDef { Id = "market", Cat = Cat.Trade, Label = "Market", Build = "market",
                Desc = "Must touch a road. Sends one trade caravan at a time to settlements willing to deal with you: open the world map (M) and click a settlement." },
            new ToolDef { Id = "storehouse", Cat = Cat.Trade, Label = "Storehouse", Build = "storehouse",
                Desc = $"Haulers move construction supplies, ores, and fuel into extra storage via connected roads. Holds {World.StorehouseSpace} extra of each material." },
            new ToolDef { Id = "muster", Cat = Cat.Military, Label = "Muster Ground", Build = "muster",
                Desc = "Where militia register and gather in an emergency (30 on the roll). Registered militia keep their jobs and homes; only mobilised ones leave work. Houses nobody. Open the Military menu to register and mobilise." },
            new ToolDef { Id = "barracks", Cat = Cat.Military, Label = "Barracks", Build = "barracks",
                Desc = "Houses 16 professional soldiers. It makes none by itself: open the Military menu to recruit them from your workforce into companies, which takes workers off their jobs. Must touch a road." },
            new ToolDef { Id = "wall", Cat = Cat.Military, Label = "Palisade", Wall = true,
                Desc = "A wooden palisade, 2 wood a tile: press and drag a line. It is a thin wall through the middle of each tile, and people and raiders must go around it. A road through the line is a gate." },
            new ToolDef { Id = "wall2", Cat = Cat.Military, Label = "Palisade + Walkway", Wall = true,
                Desc = "Palisades with a plank walkway. Drag a line; existing palisades in the line receive the walkway. Guard patrols on the platform are not implemented yet." },
            new ToolDef { Id = "watchtower", Cat = Cat.Military, Label = "Watchtower", Build = "watchtower",
                Desc = $"Spots raiders within {World.WatchtowerRange} tiles and alerts every garrison. Place it along an approach to town. Works without a road." },
            new ToolDef { Id = "well", Cat = Cat.Water, Label = "Well", Build = "well",
                Desc = $"Pumps up to {WaterTuning.WellPump:0} water a day from the groundwater patch it stands on (the blue patches) into the town-wide supply: any home on a connected road can use it, however far away. Wells on one patch share its reserve and recharge: draw more than the recharge and the patch runs down." },
            new ToolDef { Id = "cistern", Cat = Cat.Water, Label = "Cistern", Build = "cistern",
                Desc = $"Stores {WaterTuning.CisternCap:0} water. Wells and the Town Hall's spring fill it with what the town does not drink, and it covers shortfalls for every home on a connected road. Anywhere on a road will do." },
            new ToolDef { Id = "fisher", Cat = Cat.Water, Label = "Fishing Hut", Build = "fisher",
                Desc = $"Fishermen bring fish from rivers, lakes and the sea. The hut must stand within two tiles of the water and on a road to the Town Hall; the fishermen walk to the shore, so water across a river they cannot cross does not count. A pond gives least, a lake or river more, the sea most. Fish counts as fresh meat: a smokehouse preserves it. Costs {Cost["fisher"][Res.Wood]} wood." },
            new ToolDef { Id = "dock", Cat = Cat.Water, Label = "Fishing Dock", Build = "dock",
                Desc = $"A pier on the shore where three fishermen fish water deep enough for boats, and catch {World.DockBonus:0.#} times as much as from a hut: the best use of a lake, a big river or the sea. It must stand on the shore with medium or deep water within three tiles (a pier reaches out over the shallows), on a road to the Town Hall. Costs {Cost["dock"][Res.Wood]} wood and {Cost["dock"][Res.Stone]} stone. Over-fished water thins out: the catch falls until the fish breed back." },
            new ToolDef { Id = "ferry", Cat = Cat.Water, Label = "Ferry Landing", Build = "ferry",
                Desc = $"Build two landings on the shores of the same deep water, up to {World.FerryRange} tiles apart, to make a ferry. It joins the roads on the two shores, so homes, farms and workplaces across a wide river, a lake or a bay are served from the Town Hall where a bridge cannot reach. A ferry boat shuttles between them. People and crews do not walk through it: only roads are joined. Each landing costs {Cost["ferry"][Res.Wood]} wood and {Cost["ferry"][Res.Stone]} stone; it must stand on the shore with medium or deep water within three tiles, beside a road." },
            new ToolDef { Id = "boatyard", Cat = Cat.Water, Label = "Boatyard", Build = "boatyard",
                Desc = $"Craftsmen build and mend the town's boats here: fishing boats, ferry boats and cargo barges, from wood, flax (rope and sails) and metal fittings. Order them in the Vessels window. A ferry route needs a ferry boat, a fishing dock fishes best with boats and a port trades with a cargo barge; a boatyard also repairs the boats that wear out. Stands on the shore with deep water within three tiles, touching a road. Costs {Cost["boatyard"][Res.Wood]} wood and {Cost["boatyard"][Res.Stone]} stone." },
            new ToolDef { Id = "ferryRoute", Cat = Cat.Water, Label = "Ferry Route", FerryRoute = true,
                Desc = $"Click a ferry landing, then the landing at the other end: green landings can be joined, red ones say why not (a route may sail at most {World.FerryMaxRoute} tiles over connected deep water, and costs {World.FerryRouteCost} gold). The route is drawn along the water it sails. A route needs {World.FerryCrew} ferrymen at a landing on a road to the Town Hall. Click a landing that already has a route to suspend or resume it; hold Shift and click to remove the route. Nothing pairs by itself: building another landing never changes a route." },
            new ToolDef { Id = "port", Cat = Cat.Water, Label = "Port", Build = "port",
                Desc = $"A harbour on a big lake, a major river or the sea (at least {World.PortWaterNeeded} tiles of connected boat-deep water). It is not a market (overland caravans need a Market); it gives your trade one ship, and goods go by water: a port gives you a ship: it trades by water only with settlements the world map shows on the same sea or lake, and the travel time follows the water route. Costs {Cost["port"][Res.Wood]} wood, {Cost["port"][Res.Stone]} stone and {Cost["port"][Res.Gold]} gold. Needs a road to the Town Hall." },
            new ToolDef { Id = "watermill", Cat = Cat.Water, Label = "Watermill", Build = "watermill",
                Desc = $"Stands on a riverbank with flowing water beside it and grinds the wheat harvest: staffed mills add {World.MillBonusShare * 100:0}% to the food from the fields (each mill grinds for {World.MillFields} fields), more mills for more fields. Ponds, lakes and the sea have no current; a frozen river stops the wheel. Needs a road to the Town Hall. Costs {Cost["watermill"][Res.Wood]} wood and {Cost["watermill"][Res.Stone]} stone." },
            new ToolDef { Id = "moat", Cat = Cat.Military, Label = "Moat", Moat = true,
                Desc = $"A water-filled ditch dug from fresh water: press on the bank of a river, lake or pond (or the end of a moat) and drag in a straight line; dig the next stretch from its end to bend it round your town. Up to {World.MoatMaxLength} tiles at a time, {World.MoatWood} wood a tile, on gently sloping ground. A moat is a wall to everyone, raiders included, except where a road crosses it: a road laid over a moat is a causeway, the one way across, and the place to defend. Buildings and zones keep off it; Clear removes a tile." },
            new ToolDef { Id = "canal", Cat = Cat.Water, Label = "Irrigation Canal", Canal = true,
                Desc = $"Press on the bank of a river, lake or pond (or the end of a canal that reaches one) and drag in a straight line away from the water. Water does not run uphill, so the canal may not climb. The soil within {World.CanalReach} tiles of a canal joined to fresh water gets moister, so dry land can be farmed and crops yield more. {World.CanalWood} wood a tile, up to {World.CanalMaxLength} tiles at a time. Roads, buildings and zones keep off canals; people step across them. Clear removes one." },
            new ToolDef { Id = "bridge", Cat = Cat.Water, Label = "Wooden Bridge", Bridge = true,
                Desc = $"Press on one bank and drag in a straight line across the water to the far bank. Costs {World.BridgeWood} wood a tile over water (1 for road on land). A wooden bridge spans up to {World.BridgeMaxSpan} tiles of shallow or medium water: not the sea, not deep channels. Walkers, workers and carts can use it once it joins your roads." },
            new ToolDef { Id = "claim", Cat = Cat.Land, Label = "Claim Land", Claim = true,
                Desc = $"Click a 16 x 16 parcel next to land you own to claim it, free of charge. The tip tells you what lies on a parcel before you click. You can build, zone, clear and work only land you own. The whole map is 256 x 256 tiles." },
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
        [Tooltip("Surface water for a new world (rivers, lakes, seas). None keeps the dry map. The New world panel sets this for the next start.")]
        public WaterPreset Water = WaterPreset.None;

        public World World { get; private set; }
        int _rot, _drawnVersion = -1, _drawnRot = -1, _speed = 1, _lastRunSpeed = 1;
        Rect _builtRect;   // the part of the world the renderers last built (in world units)
        WorldMap _worldMap; System.Threading.Tasks.Task<WorldMap> _worldMapTask;   // the conquest map, made on a background thread the first time the Map opens
        World _campWorld;   // while a raider camp is open the game view shows this little world instead of the settlement
        float _townAcc;     // how far through its day an NPC settlement being looked at is
        Vector3 _savedCamCenter; int _savedZoomIndex;
        int _ugLevel;   // underground view: 0 off, else the level shown (U cycles)
        CrownAndHearth.Game.UI.OverlayId _ov;   // the other map overlays, chosen from the Overlay selector (Underground keeps _ugLevel)
        float _dayAcc;
        ToolDef _tool = Tools[0];
        Vector2Int? _hover, _roadAnchor, _lastPaint, _zoneAnchor;   // _zoneAnchor: first corner of a zone (or unzone) rectangle
        bool _zoneDragging;          // the button is still held since the first corner (drag mode); false = click-click mode
        Vector2? _rightDown;         // where the right button went down, to tell a click (cancel) from a camera drag
        ArtLibrary _art;
        Building _ferryFrom;   // the landing picked first with the Ferry Route tool
        GroundRenderer _ground; WaterRenderer _water; ObjectRenderer _objects; OverlayRenderer _overlay; AgentRenderer _agents;
        CameraRig _cam; Hud _hud;
        Incident _incidentSeen;   // the card the game paused for, and the speed to go back to once it is answered
        int _speedBeforeIncident;

        void Start()
        {
            // the New world panel (Menu) leaves its choice here for the next start; it is used once
            if (PlayerPrefs.HasKey(NewWorldWater)) { Water = (WaterPreset)PlayerPrefs.GetInt(NewWorldWater); Seed = PlayerPrefs.GetInt(NewWorldSeed, Seed); if (Water != WaterPreset.None) Relief = HydroParams.For(Water).SuggestedRelief; PlayerPrefs.DeleteKey(NewWorldWater); PlayerPrefs.Save(); }
            World = new World(Seed, TerrainStyle.Ranges, Relief, Water);
            World.StartCampaign();   // goals, and the game is lost with the Town Hall or the last villager (World.Campaign.cs, World.Defeat.cs)
            _art = new ArtLibrary();
            if (SpriteMaterial == null) Debug.LogError("GameController needs the Sprite-Unlit material. Run Crown & Hearth > Rebuild Settlement Scene.");
            _ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundRenderer)).GetComponent<GroundRenderer>();
            _ground.transform.SetParent(transform, false); _ground.Init(SpriteMaterial, _art.GroundAtlas);
            _water = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer), typeof(WaterRenderer)).GetComponent<WaterRenderer>();
            _water.transform.SetParent(transform, false); _water.Init(SpriteMaterial);
            _overlay = new GameObject("Overlay", typeof(MeshFilter), typeof(MeshRenderer), typeof(OverlayRenderer)).GetComponent<OverlayRenderer>();
            _overlay.transform.SetParent(transform, false); _overlay.Init(SpriteMaterial, _art.GroundAtlas);
            _objects = new GameObject("Objects", typeof(ObjectRenderer)).GetComponent<ObjectRenderer>();
            _objects.transform.SetParent(transform, false); _objects.Init(_art, SpriteMaterial);
            _agents = new GameObject("Agents", typeof(AgentRenderer)).GetComponent<AgentRenderer>();
            _agents.transform.SetParent(transform, false); _agents.Init(SpriteMaterial);

            _cam = Camera.main.GetComponent<CameraRig>();
            if (_cam == null) _cam = Camera.main.gameObject.AddComponent<CameraRig>();
            var home = IsoView.TileCenter(World, Defs.Start, Defs.Start + 2, _rot);
            _cam.CenterOn(home);

            _hud = new GameObject("HudRoot", typeof(Hud)).GetComponent<Hud>();
            _hud.Build(Tools, Cats, _art);
            _hud.BannerPicked += SetBanner;
            _hud.ToolPicked += id => SetTool(Tools.First(t => t.Id == id));
            _hud.OverlayPicked += SetOverlay;
            _hud.LivestockFocusRequested += (x, y) => _cam.CenterOn(IsoView.TileCenter(World, x, y, _rot));
            _hud.SpeedPicked += s => { _speed = s; if (s > 0) _lastRunSpeed = s; _hud.SetSpeed(s); };
            _hud.RotatePicked += Rotate;
            _hud.PrioMoved += MovePrio;
            _hud.WorkersAdjusted += (trade, dir) => World.AdjustTradeTarget(trade, dir);
            _hud.SavePicked += SaveSettlement;
            _hud.MapToggled += open => { if (open) EnsureWorldMap(); };
            _hud.InvasionConfirmed += (camp, groups) => World.BeginInvasion(camp, groups);
            _hud.InvasionFinished += battle =>
            {
                World.FinishInvasion(battle);
                _hud.SetCamps(World);
                _hud.Toast(battle.Victory ? $"The {battle.Camp.Name} is destroyed." : $"The attack on the {battle.Camp.Name} is over.", 6);
            };
            _hud.LoadPicked += LoadSettlement;
            _hud.FestivalPicked += () =>
            {
                var held = World.HoldFestival();
                _hud.Toast(held.Ok ? "The town holds a festival. Spirits are high." : held.Reason, held.Ok ? 4f : 5f);
            };
            _hud.IncidentChosen += choice =>
            {
                var answer = World.ResolveIncident(choice);
                if (!answer.Ok) { _hud.Toast(answer.Reason); return; }
                if (_speedBeforeIncident > 0 && _speed == 0) { _speed = _speedBeforeIncident; _hud.SetSpeed(_speed); }
                _speedBeforeIncident = 0;
            };
            _hud.NewWorldPicked += StartNewWorld;
            _hud.RestartPicked += () => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            SetTool(_tool); _hud.SetSpeed(_speed);
            EnsureWorldMap();   // the raider camps live on the world map, so it is made at the start (on a worker thread)
            _hud.Toast("Welcome to Crown & Hearth. Pick a category along the bottom to build.", 6);
        }

        // ---------- the Ferry Route tool ----------
        void FerryRouteClick(Vector2Int h)
        {
            var b = World.Grid[h.y, h.x];
            if (b == null || b.T != "ferry") { _hud.Toast("Click a ferry landing."); return; }
            if (_ferryFrom == null)
            {
                var route = World.RouteOf(b);
                if (route != null)
                {
                    if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed) { World.RemoveRoute(route); _hud.Toast("Ferry route removed."); }
                    else { World.SetRouteSuspended(route, !route.Suspended); _hud.Toast(route.Suspended ? "Ferry suspended. Click the landing again to resume it." : "Ferry resumed."); }
                    return;
                }
                _ferryFrom = b; _hud.Toast("Now click the landing at the other end (green: can be joined). Esc cancels.");
                return;
            }
            var made = World.EstablishRoute(_ferryFrom, b);
            _hud.Toast(made.Ok ? $"Ferry route made: {made.Count} tiles of sailing. It needs {World.FerryCrew} ferrymen at a landing on a road." : made.Reason, made.Ok ? 4 : 6);
            if (made.Ok || b != _ferryFrom) _ferryFrom = null;
        }

        void DrawFerryOverlay(Vector2Int h)
        {
            foreach (var landing in World.Blds.Where(x => x.T == "ferry"))
            {
                bool ok = _ferryFrom != null ? landing != _ferryFrom && World.CanRoute(_ferryFrom, landing).Ok : World.RouteOf(landing) == null;
                if (landing == _ferryFrom) ok = true;
                _overlay.AddTile(World, landing.X, landing.Y, _rot, ok ? Ok : Bad);
            }
            var hb = World.Grid[h.y, h.x];
            List<int> path = null;
            if (hb != null && hb.T == "ferry")
            {
                var other = _ferryFrom != null ? hb : World.FerryPartner(hb);
                var from = _ferryFrom ?? hb;
                if (other != null && other != from) path = World.SailingRoute(from, other);
            }
            if (path != null) foreach (int t in path) _overlay.AddTile(World, t % N, t / N, _rot, new Color32(120, 200, 255, 140));
        }

        (string, bool) FerryTip(Vector2Int h)
        {
            var b = World.Grid[h.y, h.x];
            if (b == null || b.T != "ferry") return (_ferryFrom != null ? "Click the landing at the other end" : "Click a ferry landing", false);
            var route = World.RouteOf(b);
            if (_ferryFrom == null)
            {
                if (route != null) return ($"Ferry route: {World.StateText(World.StateOf(route))}. Click to {(route.Suspended ? "resume" : "suspend")}, Shift-click to remove", false);
                return ("Click to start a ferry route from this landing", false);
            }
            if (b == _ferryFrom) return ("The first landing. Click another one", false);
            var can = World.CanRoute(_ferryFrom, b);
            return can.Ok ? ($"Click to make the route: {can.Count} tiles of sailing, {World.FerryRouteCost} gold", false) : (can.Reason, true);
        }

        // the tiles a drag covers: an L for roads and palisades, a straight line for a bridge
        System.Collections.Generic.List<(int x, int y)> ToolPath(Vector2Int a, Vector2Int b) => _tool.Bridge || _tool.Canal || _tool.Moat ? World.BridgeLine(a.x, a.y, b.x, b.y) : World.LPath(a.x, a.y, b.x, b.y);

        const string NewWorldWater = "thornwick.newWorld.water", NewWorldSeed = "thornwick.newWorld.seed";

        void StartNewWorld(WaterPreset water, int seed)
        {
            PlayerPrefs.SetInt(NewWorldWater, (int)water); PlayerPrefs.SetInt(NewWorldSeed, seed <= 0 ? 7 : seed); PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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
            if (!loaded.CampaignOn) loaded.StartCampaign();   // a save from before the campaign: what it already beats counts as done, with no reward
            SetBanner(ColorUtility.TryParseHtmlString(loaded.BannerColor ?? "", out _) ? loaded.BannerColor : null);
            _dayAcc = 0;
            _speed = 0;
            _drawnVersion = -1;
            _hover = _lastPaint = null;
            SetTool(Tools[0]);
            _hud.SetSpeed(_speed);
            _cam.CenterOn(IsoView.TileCenter(World, loaded.Hall.X + 1, loaded.Hall.Y + 1, _rot));
            _hud.Toast("Settlement loaded and paused.");
            EnsureWorldMap();   // a different save can have a different world (and its own raider camps)
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

        /// <summary>Make the world for this settlement's seed and relief if the map does not have it yet. It takes a moment, so it runs on a worker thread.</summary>
        void EnsureWorldMap()
        {
            int seed = World.Seed; var relief = World.Relief;
            if (_worldMap != null && _worldMap.Seed == seed && _worldMap.HomeRelief == relief) { _hud.SetWorldMap(_worldMap); AttachCamps(); return; }
            if (_worldMapTask != null && !_worldMapTask.IsCompleted) return;
            _hud.SetMapGenerating();
            _worldMapTask = System.Threading.Tasks.Task.Run(() => WorldMap.Generate(seed, relief));
        }

        /// <summary>Give the settlement the raider camps and the NPC settlements of its world, once the map for its seed exists.</summary>
        void AttachCamps()
        {
            if (_worldMap != null && _worldMap.Seed == World.Seed) {
                var site = _worldMap.HomeSite;
                int bx = site?.X ?? World.HomeCellX, by = site?.Y ?? World.HomeCellY;
                BiomeArt.Bind(World, _worldMap.Biomes[WorldMap.Cell(bx,by)]);
                if (_campWorld != null && _hud.CurrentCamp != null) BiomeArt.Bind(_campWorld, _worldMap.Biomes[WorldMap.Cell(_hud.CurrentCamp.X,_hud.CurrentCamp.Y)]);
            }
            if (_worldMap == null || _worldMap.Seed != World.Seed || (World.Camps.Count > 0 && World.Settlements.Count > 0)) return;
            var home = _worldMap.HomeSite;
            if (World.Camps.Count == 0) World.SetCamps(RaiderCamps.Generate(_worldMap), home?.X ?? WorldMap.Size / 2, home?.Y ?? WorldMap.Size / 2);
            if (World.Settlements.Count == 0) World.SetSettlements(Settlements.Generate(_worldMap, World.Camps));
            if (World.Atlas == null) World.SetAtlas(WaterAtlas.FromWorldMap(_worldMap));   // the sea and lakes, so ports only trade where water really joins
            _hud.SetCamps(World);
        }

        void PollWorldMap()
        {
            if (_worldMapTask == null || !_worldMapTask.IsCompleted) return;
            var task = _worldMapTask; _worldMapTask = null;
            if (task.IsFaulted) { Debug.LogError("World map generation failed: " + task.Exception?.GetBaseException()); _hud.Toast("Could not generate the world map.", 6); return; }
            _worldMap = task.Result;
            EnsureWorldMap();   // the save may have changed while it was being made; this also hands the camps to the settlement
        }

        // ---------- the raider camp view ----------
        void SyncCampView()
        {
            bool want = _hud.CampOpen && _hud.CurrentCamp != null;
            if (want && _campWorld == null) EnterCamp();
            else if (!want && _campWorld != null) ExitCamp();
        }

        void EnterCamp()
        {
            _campWorld = _hud.CurrentCamp.Site.World;
            if (_worldMap != null) BiomeArt.Bind(_campWorld, _worldMap.Biomes[WorldMap.Cell(_hud.CurrentCamp.X,_hud.CurrentCamp.Y)]);   // the camp, made by the settlement terrain generator
            _savedCamCenter = _cam.Center; _savedZoomIndex = _cam.ZoomIndex;
            _cam.ZoomIndex = 2;   // 1x: the 32 x 32 camp fills the view
            int ox = _campWorld.CampX0, oy = _campWorld.CampY0;
            int half = _hud.CurrentCamp.Site.Map.N / 2;   // a settlement's map is bigger than a camp's
            _cam.CenterOn(IsoView.TileCenter(_campWorld, ox + half, oy + half, _rot));
            _drawnVersion = -1; _hover = null;
        }

        void ExitCamp()
        {
            _campWorld = null;
            _cam.ZoomIndex = _savedZoomIndex;
            _cam.CenterOn(_savedCamCenter);
            _drawnVersion = -1;
        }

        /// <summary>Hover and click on the camp: tells the camp panel which tile (0..31 within the camp) the cursor is on.</summary>
        void HandleCampMouse(bool overUi)
        {
            var mouse = Mouse.current; if (mouse == null) return;
            var wp = _cam.Cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var picked = overUi ? null : IsoView.PickTile(_campWorld, wp, _rot);
            (int x, int y)? local = null;
            if (picked.HasValue)
            {
                int lx = picked.Value.x - _campWorld.CampX0, ly = picked.Value.y - _campWorld.CampY0;
                if (_hud.CampView.Map.In(lx, ly)) local = (lx, ly);
            }
            _hud.CampTileHover(local);
            if (local.HasValue && mouse.leftButton.wasPressedThisFrame) _hud.CampTileClick(local.Value.x, local.Value.y);
        }

        /// <summary>Planning marks on the camp: where soldiers can land, the landings so far, the selected one and the tile under the cursor.</summary>
        void DrawCampOverlay()
        {
            _overlay.Clear();
            int ox = _campWorld.CampX0, oy = _campWorld.CampY0;
            if (_hud.CampPlanning)
            {
                var map = _hud.CampView.Map;
                for (int i = 0; i < map.N; i++)
                    foreach (var (x, y) in new[] { (i, 0), (i, map.N - 1), (0, i), (map.N - 1, i) })
                        if (map.Passable(x, y)) _overlay.AddTile(_campWorld, ox + x, oy + y, _rot, new Color32(90, 150, 255, 55));
                foreach (var g in _hud.CampGroups) _overlay.AddTile(_campWorld, ox + g.X, oy + g.Y, _rot, g.Total > 0 ? new Color32(70, 130, 255, 190) : new Color32(70, 130, 255, 110));
                if (_hud.CampSelected.HasValue) _overlay.AddTile(_campWorld, ox + _hud.CampSelected.Value.x, oy + _hud.CampSelected.Value.y, _rot, new Color32(255, 230, 90, 170));
                if (_hud.CampHoverTile.HasValue) _overlay.AddTile(_campWorld, ox + _hud.CampHoverTile.Value.x, oy + _hud.CampHoverTile.Value.y, _rot, new Color32(255, 255, 255, 90));
            }
            // during the fight: mark where the selected landing's soldiers stand
            foreach (var (x, y) in _hud.CampSelectedSoldiers) _overlay.AddTile(_campWorld, ox + x, oy + y, _rot, new Color32(255, 230, 90, 110));
            _overlay.Apply();
        }

        void OnDestroy()
        {
            _art?.SetBanner(null);
            _art?.SettlementWork.Dispose();
        }

        void Update()
        {
            // a script reload during Play mode (recompiling while the game runs) drops the world; Unity does not call Start again
            if (World == null)
            {
                Debug.LogWarning("Crown & Hearth: scripts were reloaded while playing, so the game state is gone. Stop and start Play mode again.");
                enabled = false;
                return;
            }
            SyncCampView();
            HandleKeys();
            bool overUi = _hud.PointerOverUi;
            var camBefore = _cam.Center; int zoomBefore = _cam.ZoomIndex;
            bool modal = _hud.MapOpen && _campWorld == null;   // the world map is modal, the camp view is not (you pan and zoom it)
            _cam.Tick(!overUi && !modal, !_hud.EditingBanner && !modal);
            if (_campWorld != null) HandleCampMouse(overUi); else HandleMouse(overUi || _hud.MapOpen);

            if (World.Defeated && _speed != 0) { _speed = 0; _hud.SetSpeed(0); }   // the clock stops when the settlement has fallen
            if (World.CurrentIncident != null && World.CurrentIncident != _incidentSeen)   // a new card: pause so it can be read
            {
                _incidentSeen = World.CurrentIncident;
                if (_speed > 0) { _speedBeforeIncident = _speed; _speed = 0; _hud.SetSpeed(0); }
            }
            if (_speed > 0)
            {
                _dayAcc += Time.unscaledDeltaTime * _speed;
                int guard = 0;
                while (_dayAcc >= 1f && guard++ < 8) { _dayAcc -= 1f; World.Tick(); }
                if (_dayAcc > 1f) _dayAcc = 0;
            }

            // the map is 256 x 256: draw the part round the camera (half a screen more on every side), and redraw it when the camera leaves that
            // part, the zoom changes it, the world changes or the view turns
            var camera = _cam.Cam; float halfH = camera.orthographicSize, halfW = halfH * camera.aspect; Vector2 at = camera.transform.position;
            var visible = new Rect(at.x - halfW, at.y - halfH, halfW * 2, halfH * 2);
            var drawn = _campWorld ?? World;
            if (_campWorld != null) _campWorld.ClearLootedPiles(_hud.CampView?.Map);
            if (_campWorld != null) _campWorld.MirrorBattle(_hud.CampView?.Map);   // shut gates, broken palisades and gates show in the town   // emptied supply piles disappear (bumps Version, so it redraws)
            if (drawn.Version != _drawnVersion || _rot != _drawnRot || !_builtRect.Contains(visible.min) || !_builtRect.Contains(visible.max)
                || _builtRect.width > visible.width * 3.5f)   // zoomed back in after being zoomed right out: do not keep drawing the whole map
            {
                _builtRect = new Rect(visible.x - visible.width * 0.5f, visible.y - visible.height * 0.5f, visible.width * 2f, visible.height * 2f);
                var window = IsoView.TileWindow(_builtRect);
                _ground.Build(drawn, _rot, window); _water.Build(drawn, _rot, window); _objects.Build(drawn, _rot, window);
                _drawnVersion = drawn.Version; _drawnRot = _rot;
            }
            if (_campWorld != null)
            {
                // an NPC settlement's own people go about their day (about a day a second) while you plan and fight there
                if (_hud.CurrentCamp != null && _hud.CurrentCamp.IsTown)
                {
                    _townAcc += Time.deltaTime;
                    while (_townAcc >= 1f) { _townAcc -= 1f; _campWorld.AmbientDay(); }
                }
                _agents.DrawCamp(_campWorld, _hud.CampView, _rot, _hud.CampFrac, Mathf.Clamp01(_townAcc));
            }
            else _agents.Draw(World, _rot, Mathf.Clamp01(_dayAcc), _cam.Zoom <= 0.5f);
            _objects.RefreshCropStages(World.Day);
            _objects.AnimateLivestock(World.Day, Mathf.Clamp01(_dayAcc), _rot);
            if (_campWorld != null) DrawCampOverlay(); else DrawOverlay();
            _hud.SetSettlement(World);
            _hud.SetOverlayState(_ugLevel > 0 ? CrownAndHearth.Game.UI.OverlayId.Underground : _ov, _ugLevel > 0 ? $"Underground {_ugLevel}" : CrownAndHearth.Game.UI.OverlayRegistry.TitleOf(_ov));
            _hud.SetDemand(World.Dem[Zone.Res]);
            var tip = CursorTip(); _hud.SetCursorTip(tip.text, tip.bad, Mouse.current?.position.ReadValue() ?? Vector2.zero, _tipWarn);
            _hud.SetInfo(_hover.HasValue ? TileInfo(_hover.Value.x, _hover.Value.y) : "");
            _hud.RefreshWorkforce(World);
            _hud.RefreshTownMenus(World);
            PollWorldMap();
            if (World.Events.Count > 0) { foreach (var e in World.Events) _hud.Notify(e, World.Day, 8); World.Events.Clear(); }   // cave-ins, new levels, empty surveys
            var raid = World.Raid;
            if (raid != null)
            {
                if (!raid.StartAnnounced) { raid.StartAnnounced = true; _hud.Notify($"Raiders! A band of {raid.Band} is coming in from the {raid.From}.", World.Day, 6); }
                if (raid.Over && !raid.EndAnnounced) { raid.EndAnnounced = true; _hud.Notify($"The raid is over: {raid.Killed} raiders cut down, {raid.Escaped} got away with {raid.Stolen.Values.Sum()} goods.", World.Day, 8); }
            }
        }

        /// <summary>Space: pause, and resume at the speed the game was running at (not always 1x).</summary>
        void TogglePause()
        {
            if (_speed > 0) { _lastRunSpeed = _speed; _speed = 0; }
            else _speed = _lastRunSpeed;
            _hud.SetSpeed(_speed);
        }

        /// <summary>+ and - step through 1x, 2x, 4x; either one also resumes a paused game.</summary>
        void SetSpeedStep(int direction)
        {
            _speed = CrownAndHearth.Game.UI.SpeedSteps.Next(_speed, direction); _lastRunSpeed = _speed; _hud.SetSpeed(_speed);
        }

        void HandleKeys()
        {
            var kb = Keyboard.current; if (kb == null || _hud.EditingBanner) return;
            for (int f = 1; f <= 12; f++)   // F1..F12: the twelve management menus (digits belong to the build bar)
                if (kb[Key.F1 + f - 1].wasPressedThisFrame) { var m = CrownAndHearth.Game.UI.MenuRegistry.ByFunctionKey(f); if (m != null) { _hud.ToggleMenu(m.Id); return; } }
            if (kb.bKey.wasPressedThisFrame) { _hud.ToggleCatalogue(); return; }
            if (kb.nKey.wasPressedThisFrame) { _hud.ToggleMenu(CrownAndHearth.Game.UI.MenuId.Notifications); return; }
            if (kb.escapeKey.wasPressedThisFrame && _hud.EscapeManagement(_tool != Tools[0] || _roadAnchor.HasValue || _zoneAnchor.HasValue || _ferryFrom != null)) return;
            if (kb.mKey.wasPressedThisFrame) { _hud.ToggleMap(); return; }
            if (kb.tKey.wasPressedThisFrame) { _hud.ToggleMilitary(); return; }
            if (_hud.MapOpen)
            {
                // the map is modal: Esc closes it, Space still pauses, nothing else reaches the town
                if (kb.escapeKey.wasPressedThisFrame) _hud.EscapePanels();
                if (kb.spaceKey.wasPressedThisFrame) TogglePause();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame) Cancel();
            foreach (var c in Cats)
                if (c.Cat != Cat.Inspect && (kb[c.Hotkey].wasPressedThisFrame || (c.Alt != Key.None && kb[c.Alt].wasPressedThisFrame))) _hud.CategoryKey(c.Cat);
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) SetSpeedStep(1);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) SetSpeedStep(-1);
            if (kb.qKey.wasPressedThisFrame) Rotate(-1);
            if (kb.eKey.wasPressedThisFrame) Rotate(1);
            if (kb.spaceKey.wasPressedThisFrame) TogglePause();
            if (kb.uKey.wasPressedThisFrame)
            {
                _autoOv = false; _ugLevel = (_ugLevel + 1) % (World.Bands + 1); if (_ugLevel > 0) _ov = CrownAndHearth.Game.UI.OverlayId.None;
                _hud.Toast(_ugLevel == 0 ? "Underground view off." : $"Underground view: ore band {_ugLevel} of {World.Bands}" +
                    (_ugLevel == 1 ? " (just under the surface). Ore lies in hills and mountains. Orange = ore a tunnel has revealed, brighter = richer; grey = revealed, nothing here; pale orange = an estimate of ore not dug near yet; blue = your painted Mining boundary; yellow = tunnel. U for deeper." : _ugLevel < World.Bands ? ". U for deeper." : " (deepest, richest). U to turn off."), 6);
            }
        }

        void SetOverlay(CrownAndHearth.Game.UI.OverlayId id)
        {
            _autoOv = false;   // the player chose: a tool no longer changes or restores it
            _ugLevel = id == CrownAndHearth.Game.UI.OverlayId.Underground ? 1 : 0;
            _ov = id == CrownAndHearth.Game.UI.OverlayId.Underground ? CrownAndHearth.Game.UI.OverlayId.None : id;
            var def = CrownAndHearth.Game.UI.OverlayRegistry.Get(id);
            _hud.Toast(def == null ? "Overlay off." : $"Overlay: {def.Title}. {def.Hint}.", 5);
        }

        /// <summary>The overlay picked in the selector (Underground has its own drawing, see DrawUnderground).</summary>
        void DrawChosenOverlay()
        {
            var w = World;
            switch (_ov)
            {
                case CrownAndHearth.Game.UI.OverlayId.Water: DrawWater(null, null); break;
                case CrownAndHearth.Game.UI.OverlayId.Forest: DrawForest(); break;
                case CrownAndHearth.Game.UI.OverlayId.Fertility:
                    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                        if (w.Fert[y, x] && w.Grid[y, x] == null && !(w.Waters != null && w.Waters.IsWater(x, y))) _overlay.AddTile(w, x, y, _rot, new Color32(150, 230, 90, 90));
                    break;
                case CrownAndHearth.Game.UI.OverlayId.Sailing:
                    if (w.Waters == null) break;
                    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                    {
                        int i = y * N + x; if (!w.Waters.IsWaterAt(i)) continue;
                        _overlay.AddTile(w, x, y, _rot, w.Waters.NavRegion[i] != 0 ? new Color32(90, 230, 150, 110) : new Color32(150, 150, 150, 110));
                    }
                    break;
            }
        }

        bool _autoOv;   // the overlay on screen was switched on by the chosen tool, not by the player

        /// <summary>A tool that needs to see something (fertility, groundwater, ore, water) turns that overlay on while it is the tool, and puts things back after. An overlay the player chose is left alone.</summary>
        void ApplyToolOverlay(ToolDef t)
        {
            var want = CrownAndHearth.Game.UI.OverlayRegistry.ForTool(t.Id, World);
            var none = CrownAndHearth.Game.UI.OverlayId.None;
            if (want == none) { if (_autoOv) { _autoOv = false; _ov = none; _ugLevel = 0; } return; }
            if (!_autoOv && (_ov != none || _ugLevel > 0)) return;   // the player already chose one
            _autoOv = true;
            if (want == CrownAndHearth.Game.UI.OverlayId.Underground) { _ov = none; _ugLevel = 1; } else { _ugLevel = 0; _ov = want; }
        }

        void SetTool(ToolDef t)
        {
            ApplyToolOverlay(t);
            const string stop = "   ·   Right-click or Esc to stop";
            string hint = t.Build == "well" ? $"Well ({Hud.CostText(t.Build)}): click a blue groundwater patch. Light roads show the network its water travels.{stop}"
                : t.Build == "cistern" ? $"Cistern ({Hud.CostText(t.Build)}): click beside a road. Light roads show the network its water travels.{stop}"
                : t.Build != null ? $"{t.Label} ({Hud.CostText(t.Build)}): click a spot to build it." + (ReachOf(t.Build) > 0 ? " The shaded ring is the land it works." : "") + stop
                : t.Claim ? $"Claim Land: click a 16 x 16 parcel next to your land for free (green = you can, red = not next to your land).{stop}"
                : t.Road ? $"Road (1 wood a tile): press and drag from a road to lay one.{stop}"
                : t.Moat ? $"Moat ({World.MoatWood} wood a tile): press on a bank and drag away from the water in a straight line; lay a road across it for a causeway.{stop}"
                : t.Canal ? $"Irrigation canal ({World.CanalWood} wood a tile): press on a bank and drag away from the water in a straight line.{stop}"
                : t.Bridge ? $"Wooden bridge ({World.BridgeWood} wood a tile over water): press on one bank and drag in a straight line to the far bank.{stop}"
                : t.Wall ? $"Palisade (2 wood a tile): press and drag a line. Raiders and people walk around it; a road through the line is a gate.{stop}"
                : t.LandOp == "clear" ? $"Clear Land: drag over trees and small rocks (orange = will be cleared, blue = protected, asks first).{stop}"
                : t.LandOp == "protect" ? $"Protect Woodland: drag over woodland to keep lodges and crews out.{stop}"
                : t.LandOp == "unprotect" ? $"Open Woodland: drag over protected woodland.{stop}"
                : t.Zone != Zone.None ? $"{t.Label}: drag over the land, or click two corners. Green tiles will be zoned, red ones cannot be.{stop}"
                : t.Unzone ? $"Remove Zones: drag over zoned land. Buildings stay.{stop}"
                : t.Clear ? $"Demolish: click or drag over what you want removed.{stop}"
                : "Inspect: hover or click a tile. Press B to open the Build menu.";
            _tool = t; _roadAnchor = null; _zoneAnchor = null; _ferryFrom = null; _zoneDragging = false; _hud.SetTool(t, hint);
        }

        /// <summary>Esc or a right-click: drop the half-drawn road or rectangle first; otherwise put the tool away.</summary>
        void Cancel()
        {
            if (_ferryFrom != null) { _ferryFrom = null; return; }
            if (_roadAnchor.HasValue || _zoneAnchor.HasValue) { _roadAnchor = null; _zoneAnchor = null; _zoneDragging = false; return; }
            SetTool(Tools[0]); _hud.CloseTray();
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

            // right button: a click (barely moved) cancels; a drag pans the camera (CameraRig)
            var mp = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame) _rightDown = overUi ? (Vector2?)null : mp;
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                if (_rightDown.HasValue && (mp - _rightDown.Value).sqrMagnitude < 36) Cancel();
                _rightDown = null;
            }

            if (_tool.Claim)
            {
                if (mouse.leftButton.wasPressedThisFrame && _hover.HasValue && !overUi)
                {
                    var (cpx, cpy) = World.ParcelXY(World.ParcelOf(_hover.Value.x, _hover.Value.y));
                    var cr = World.ClaimParcel(cpx, cpy);
                    _hud.Toast(cr.Ok ? $"Claimed a parcel. You own {World.ClaimedParcels} of {World.ParcelsPerSide * World.ParcelsPerSide} ({World.ClaimedParcels * World.ParcelSize * World.ParcelSize} tiles)." : cr.Reason, 5);
                }
                return;
            }
            if (_tool.Zone != Zone.None || _tool.Unzone || _tool.LandOp != null)
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

            if (_tool.Road || _tool.Wall || _tool.Bridge || _tool.Canal || _tool.Moat)
            {
                if (mouse.leftButton.wasPressedThisFrame && _hover.HasValue) _roadAnchor = _hover;
                if (mouse.leftButton.wasReleasedThisFrame && _roadAnchor.HasValue)
                {
                    var end = _hover ?? _roadAnchor.Value;
                    var line = ToolPath(_roadAnchor.Value, end);
                    var r = _tool.Moat ? World.PlaceMoatPath(line) : _tool.Canal ? World.PlaceCanalPath(line) : _tool.Bridge ? World.PlaceBridgePath(line) : _tool.Wall ? World.PlaceWallPath(line) : World.PlaceRoadWithClearing(line);
                    if (!r.Ok && _tool.Road && r.Reason != null && r.Reason.StartsWith("Some of these trees are protected"))
                    {
                        var roadLine = line;
                        _hud.Confirm("Clear protected woodland?", "The road crosses protected woodland. Clearing it lifts the protection for good.", () => { var r2 = World.PlaceRoadWithClearing(roadLine, true); _hud.Toast(r2.Ok ? "Road planned: the trees are cleared first." : r2.Reason, 6); });
                    }
                    else if (!r.Ok) _hud.Toast(r.Reason);
                    else if (_tool.Road && line.Any(t => World.HasTree(t.x, t.y))) _hud.Toast($"Road planned: {line.Count(t => World.HasTree(t.x, t.y))} tiles of trees will be cleared first.", 6);
                    else if (_tool.Id == "wall2")
                    {
                        foreach (var (x, y) in line) if (World.Grid[y, x]?.T == "wall") World.Grid[y, x].Lvl = 2;
                        World.Touch();
                    }
                    _roadAnchor = null;
                }
                return;
            }
            if (_tool.Id == "look" && mouse.leftButton.wasPressedThisFrame && _hover.HasValue && !overUi && World.Grid[_hover.Value.y, _hover.Value.x]?.T == "wood") { _hud.ShowLodgePanel(World.Grid[_hover.Value.y, _hover.Value.x]); return; }
            if (!mouse.leftButton.isPressed || !_hover.HasValue || overUi) { _lastPaint = null; return; }
            var h = _hover.Value;
            if (_tool.FerryRoute) { if (mouse.leftButton.wasPressedThisFrame) FerryRouteClick(h); return; }
            if (_tool.Build != null)
            {
                if (!mouse.leftButton.wasPressedThisFrame) return;
                var warn = PlaceWarning(_tool.Build, h.x, h.y);
                var plan = World.PlanPlacement(_tool.Build, h.x, h.y);
                if (plan.State == PlaceState.ValidAfterClearing && plan.ProtectedTiles.Count > 0)
                {
                    string type = _tool.Build; int px = h.x, py = h.y;
                    _hud.Confirm("Clear protected woodland?", $"{plan.ProtectedTiles.Count} protected tree tile{(plan.ProtectedTiles.Count == 1 ? "" : "s")} stand where you want to build. Clearing them lifts their protection for good.", () =>
                    { var r2 = World.PlaceWithClearing(type, px, py, true); _hud.Toast(r2.Ok ? $"Planned a {Defs.Names[type]}: {r2.Count} tile{(r2.Count == 1 ? "" : "s")} of trees will be cleared first." : r2.Reason, 6); });
                    return;
                }
                var r = World.PlaceWithClearing(_tool.Build, h.x, h.y);
                _hud.Toast(!r.Ok ? r.Reason : plan.State == PlaceState.ValidAfterClearing ? $"Planned a {Defs.Names[_tool.Build]}: {plan.ToClear.Count} tile{(plan.ToClear.Count == 1 ? "" : "s")} of trees will be cleared first, then it is built and paid for." : $"Built a {Defs.Names[_tool.Build]}." + (warn != null && !World.Grid[h.y, h.x].Served ? " It needs a road to the Town Hall before it can work." : ""), 6);
            }
            else if (_tool.Clear)
            {
                if (_lastPaint == h) return;
                _lastPaint = h;
                var r = World.Remove(h.x, h.y);
                if (!r.Ok && mouse.leftButton.wasPressedThisFrame) _hud.Toast(r.Reason);
            }
        }

        /// <summary>Clear Land, Protect Woodland and Open Woodland over a dragged rectangle.</summary>
        void LandRect(Vector2Int a, Vector2Int b)
        {
            var w = World; int x0 = Mathf.Min(a.x, b.x), y0 = Mathf.Min(a.y, b.y), x1 = Mathf.Max(a.x, b.x), y1 = Mathf.Max(a.y, b.y);
            if (_tool.LandOp == "protect") { int n = w.ProtectRect(x0, y0, x1, y1); _hud.Toast(n == 0 ? "No woodland or open ground there to protect." : $"Protected {n} tile{(n == 1 ? "" : "s")}."); return; }
            if (_tool.LandOp == "unprotect") { int n = w.ProtectRect(x0, y0, x1, y1, false); _hud.Toast(n == 0 ? "Nothing protected there." : $"Opened {n} tile{(n == 1 ? "" : "s")} of woodland."); return; }
            var r = w.ClearRect(x0, y0, x1, y1);
            if (r.protectedSkipped > 0)
            {
                int pn = r.protectedSkipped;
                _hud.Confirm("Clear protected woodland?", $"{pn} protected tree tile{(pn == 1 ? "" : "s")} lie in this area. Clearing them lifts their protection for good.", () =>
                {
                    var r2 = World.ClearRect(x0, y0, x1, y1, true);
                    _hud.Toast($"Clearing {r.ordered + r2.ordered} tile{(r.ordered + r2.ordered == 1 ? "" : "s")}. Idle villagers do the work; they keep the logs and stone.");
                });
                return;
            }
            _hud.Toast(r.ordered == 0 ? (r.skipped > 0 ? r.why : "Nothing to clear there.") : $"Clearing {r.ordered} tile{(r.ordered == 1 ? "" : "s")}. Idle villagers do the work ({w.D.Idle} idle now); they keep the logs and stone." + (r.skipped > 0 ? $" {r.skipped} skipped: {r.why.TrimEnd('.')}." : ""));
        }

        void ApplyRect(Vector2Int a, Vector2Int b)
        {
            _zoneAnchor = null; _zoneDragging = false;
            if (_tool.LandOp != null) { LandRect(a, b); return; }
            if (_tool.Unzone)
            {
                int n = World.ClearZoneRect(a.x, a.y, b.x, b.y);
                _hud.Toast(n == 0 ? "No zoned open land there." : $"Removed the zone from {n} tile{(n == 1 ? "" : "s")}.");
                return;
            }
            if (World.IsLivestockZone(_tool.Zone))
            {
                var pen = World.PlacePen(_tool.Zone, a.x, a.y, b.x, b.y);
                if (pen.Ok) _hud.SelectLivestockPen(World.PenAt(a.x, a.y));
                _hud.Toast(pen.Ok ? $"Fenced {_tool.Label}. Stocked 1 of {pen.Count} animals." : pen.Reason, 6);
                return;
            }
            var (x0, y0, x1, y1) = (Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var fresh = new List<Vector2Int>();   // tiles that are not this zone yet
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (World.Inb(x, y) && !World.AlreadyZoned(_tool.Zone, x, y)) fresh.Add(new Vector2Int(x, y));
            int already = (x1 - x0 + 1) * (y1 - y0 + 1) - fresh.Count;
            var (zoned, skipped, why) = World.ZoneRect(_tool.Zone, a.x, a.y, b.x, b.y);
            string msg;
            if (zoned == 0 && skipped == 0) msg = already > 0 ? $"That land is already {_tool.Label}: nothing changed." : "Nothing to zone there.";
            else if (zoned == 0) msg = why;
            else
            {
                msg = $"Zoned {zoned} tile{(zoned == 1 ? "" : "s")}." + (skipped > 0 ? $" {skipped} skipped: {why.TrimEnd('.')}." : "");
                if (_tool.Zone == Zone.Res || _tool.Zone == Zone.Res2)
                {
                    int offRoad = fresh.Count(p => World.Grid[p.y, p.x] == null && (World.ZoneMap[p.y, p.x] == Zone.Res || World.ZoneMap[p.y, p.x] == Zone.Res2) && !World.BesideConnectedRoad(p.x, p.y));
                    if (offRoad > 0) msg += $" {offRoad} do not touch a connected road, so homes will not grow there yet.";
                }
            }
            _hud.Toast(msg, 6);
        }

        static readonly Color32 Ok = new Color32(120, 255, 120, 110), Bad = new Color32(255, 90, 80, 120), Hover = new Color32(255, 255, 255, 70), Warn = new Color32(255, 200, 70, 120);

        static readonly Color32 UgNone = new Color32(40, 40, 48, 150), UgUnknown = new Color32(70, 110, 200, 110), UgShaft = new Color32(255, 240, 120, 200);
        static readonly Color32 TunnelBare = new Color32(45, 30, 20, 215), TunnelShored = new Color32(125, 85, 45, 215);

        /// <summary>Every mine's tunnel, always visible: dark tiles, brown where the timber supports are in.</summary>
        void DrawTunnels()
        {
            var w = World;
            foreach (var b in w.Blds)
                if (b.T == "mine")
                    foreach (int k in b.Tunnel) _overlay.AddTile(w, k % N, k / N, _rot, b.Shored.Contains(k) ? TunnelShored : TunnelBare);
        }

        /// <summary>Underground view of ore band _ugLevel (1 = just under each tile's surface, 3 = deepest): surveyed ore (orange, brighter =
        /// richer), surveyed nothing (grey), estimated ore not yet dug near (pale orange), painted Mining boundary not yet surveyed (blue), mines with an adit or shaft (yellow).</summary>
        void DrawUnderground()
        {
            var w = World; int L = _ugLevel; double cap = World.LevelCapMax[L];
            bool estimate = _tool.Build == "mine";   // placing a mine: show the ore in reach as an estimate, before anything has been dug
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var b = w.Grid[y, x];
                    if (w.IsTunnel(x, y)) { _overlay.AddTile(w, x, y, _rot, UgShaft); continue; }
                    if (b != null && b.T == "mine") continue;
                    if (w.IsSurveyed(x, y))
                    {
                        double o = w.KnownOre(x, y, L);
                        if (o <= 1e-9) { _overlay.AddTile(w, x, y, _rot, UgNone); continue; }
                        float t = Mathf.Clamp01((float)(o / cap));
                        _overlay.AddTile(w, x, y, _rot, new Color32(255, (byte)(150 - 70 * t), (byte)(40 - 30 * t), (byte)(110 + 120 * t)));
                    }
                    else if (w.ZoneMap[y, x] == Zone.Mine) _overlay.AddTile(w, x, y, _rot, UgUnknown);
                    else if (estimate && w.OreAt(x, y, L) > 1e-9) _overlay.AddTile(w, x, y, _rot, new Color32(255, 190, 80, (byte)(55 + 90 * Mathf.Clamp01((float)(w.OreAt(x, y, L) / cap)))));   // an estimate of what lies under it (nothing is guaranteed)
                }
        }

        static readonly Color32 Reach = new Color32(110, 190, 255, 55), ReachNew = new Color32(255, 230, 120, 70), Unzone = new Color32(255, 150, 60, 110);

        /// <summary>How far a work building reaches, in tiles around its footprint (0 = it has no local reach).</summary>
        static int ReachOf(string type) => type switch
        {
            "wood" => WorkR, "hunter" => World.HuntingRange, "quarry" => WorkR, "mine" => WorkR, "forester" => ForesterR, "farm" => World.FarmReach, _ => 0,
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

        static readonly Color32 QuarryLocked = new Color32(110, 120, 170, 120);

        /// <summary>What a quarry will dig: orange rock tiles (brighter = more stone left) it can work now, blue-grey zoned mountain rock
        /// still locked inside the mountain.</summary>
        void DrawQuarryRock(Building q)
        {
            var w = World;
            foreach (var (x, y, state) in w.QuarryArea(q))   // the quarry's automatic area, or its painted boundary
                if (state == World.QuarryTile.Dig) _overlay.AddTile(w, x, y, _rot, new Color32(255, 160, 50, (byte)(90 + 110 * w.StoneFraction(x, y))));
                else _overlay.AddTile(w, x, y, _rot, QuarryLocked);
        }

        void AddWatchtowerRange(int centerX, int centerY, Color32 color)
        {
            const int r = World.WatchtowerRange;
            // Outline only, so a large radius does not paint over the settlement.
            for (int dy = -r; dy <= r; dy++)
            {
                int edge = Mathf.FloorToInt(Mathf.Sqrt(r * r - dy * dy));
                int nearEdge = Mathf.Abs(dy) == r ? 0 :
                    Mathf.FloorToInt(Mathf.Sqrt(r * r - (Mathf.Abs(dy) + 1) * (Mathf.Abs(dy) + 1))) + 1;
                for (int dx = Mathf.Min(edge, nearEdge); dx <= edge; dx++)
                {
                    int y = centerY + dy;
                    if (World.Inb(centerX + dx, y)) _overlay.AddTile(World, centerX + dx, y, _rot, color);
                    if (dx != 0 && World.Inb(centerX - dx, y)) _overlay.AddTile(World, centerX - dx, y, _rot, color);
                }
            }
        }

        /// <summary>The buildings whose reach matters to this zone tool: lodges and foresters for lumber, quarries and mines for mining.</summary>
        static bool Works(Zone z, string type) => z switch
        {
            Zone.Wood => type == "wood" || type == "forester",
            Zone.Mine => type == "quarry" || type == "mine",
            Zone.Farm => type == "farm",
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
            foreach (var burning in World.Burning)   // a scenario fire: every tile of a burning building glows red
                for (int j = 0; j < burning.Sz; j++) for (int i = 0; i < burning.Sz; i++) _overlay.AddTile(World, burning.X + i, burning.Y + j, _rot, Bad);
            DrawTunnels();
            if (_ugLevel > 0) DrawUnderground();
            else DrawChosenOverlay();
            // zone tools: shade where the buildings that work this zone can reach, so the player knows where to paint
            if (_tool.Zone == Zone.Wood || _tool.Zone == Zone.Mine || (_tool.Zone == Zone.Farm && World.SeasonalFarming))
            {
                var seen = new HashSet<int>();
                foreach (var b in World.Blds) if (Works(_tool.Zone, b.T)) AddReach(b.T, b.X, b.Y, b.Sz, Reach, seen);
                if (_tool.Zone == Zone.Mine) foreach (var b in World.Blds) if (b.T == "quarry") DrawQuarryRock(b);
            }
            // water tools, or inspecting a water building or a home: groundwater patches, short homes, and the road network water travels
            var hb = _hover.HasValue ? World.Grid[_hover.Value.y, _hover.Value.x] : null;
            if (_tool.Build == "well" || _tool.Build == "cistern") DrawWater(_hover, null);
            else if (_tool.Id == "look" && hb != null && (hb.T == "well" || hb.T == "cistern" || hb.T == "hall" || hb.T == "house"))
                DrawWater(_hover, hb);
            if (_hover.HasValue)
            {
                var h = _hover.Value;
                if (_tool.Road || _tool.Wall || _tool.Bridge || _tool.Canal || _tool.Moat)
                {
                    var from = _roadAnchor ?? h;
                    var path = ToolPath(from, h);
                    bool ok = (_tool.Moat ? World.MoatPathCheck(path) : _tool.Canal ? World.CanalPathCheck(path) : _tool.Bridge ? World.BridgePathCheck(path) : _tool.Wall ? World.WallPathCheck(path) : World.RoadPathCheck(path, true)).Ok;   // a road may cross woodland: the trees are cleared first
                    foreach (var (x, y) in path) _overlay.AddTile(World, x, y, _rot, !ok ? Bad : (_tool.Road && World.HasTree(x, y)) ? Warn : Ok);
                }
                else if (_tool.FerryRoute) DrawFerryOverlay(h);
                else if (_tool.Claim)
                {
                    var (cpx, cpy) = World.ParcelXY(World.ParcelOf(h.x, h.y));
                    var cc = World.CanClaim(cpx, cpy);
                    var col = World.ParcelClaimed(cpx, cpy) ? Hover : cc.Ok ? Ok : !World.ParcelTouchesYours(cpx, cpy) ? Bad : Warn;
                    for (int j = 0; j < World.ParcelSize; j++) for (int i = 0; i < World.ParcelSize; i++) _overlay.AddTile(World, cpx * World.ParcelSize + i, cpy * World.ParcelSize + j, _rot, col);
                }
                else if (_tool.Build != null)
                {
                    var plan = World.PlanPlacement(_tool.Build, h.x, h.y);
                    bool ok = plan.State != PlaceState.Invalid, later = plan.State == PlaceState.ValidAfterClearing;
                    int sz = PlacementSize(_tool.Build);
                    if (_tool.Build == "quarry") foreach (var (qx, qy, qs) in World.QuarryArea(new Building { T = "quarry", X = h.x, Y = h.y, Sz = sz })) _overlay.AddTile(World, qx, qy, _rot, qs == World.QuarryTile.Dig ? new Color32(255, 160, 50, 110) : QuarryLocked);   // what it would dig
                    AddReach(_tool.Build, h.x, h.y, sz, ReachNew, null);
                    if (_tool.Build == "watchtower") AddWatchtowerRange(h.x, h.y, ok ? ReachNew : Bad);
                    for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) _overlay.AddTile(World, h.x + i, h.y + j, _rot, !ok ? Bad : later || PlaceWarning(_tool.Build, h.x, h.y) != null ? Warn : Ok);   // amber: valid after clearing, or valid but poor
                }
                else if (_tool.LandOp != null)
                {
                    var (x0, y0, x1, y1) = RectTo(h);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        bool prot = World.IsProtected(x, y);
                        _overlay.AddTile(World, x, y, _rot, _tool.LandOp == "clear" ? (World.ObstacleAt(x, y) == Obstacle.None ? Hover : prot ? Protected : Clearing) : _tool.LandOp == "protect" ? (World.HasTree(x, y) || World.DecoMap[y, x] == Deco.Stump ? Ok : Hover) : (prot ? Ok : Hover));
                    }
                }
                else if (_tool.Zone != Zone.None)
                {
                    // the rectangle from the first corner to the cursor (or just the hovered tile before the first click)
                    var (x0, y0, x1, y1) = RectTo(h);
                    bool pen = World.IsLivestockZone(_tool.Zone);
                    bool penOk = pen && World.CheckPen(_tool.Zone, x0, y0, x1, y1).Ok;
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        _overlay.AddTile(World, x, y, _rot, pen ? (penOk ? Ok : Bad) : World.AlreadyZoned(_tool.Zone, x, y) ? Hover : World.ZoneCheck(_tool.Zone, x, y).Ok ? Ok : Bad);
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
                    if (b?.T == "quarry") DrawQuarryRock(b);
                    if (b?.T == "watchtower") AddWatchtowerRange(b.X, b.Y, Reach);
                    _overlay.AddTile(World, h.x, h.y, _rot, Hover);
                }
            }
            _overlay.Apply();
        }

        static readonly Color32 Protected = new Color32(80, 140, 255, 170), Clearing = new Color32(255, 140, 40, 170);

        /// <summary>The forest overlay: mature trees (green), saplings (light), protected woodland (blue), clearing orders (orange), and the lodges' work.</summary>
        void DrawForest()
        {
            var w = World;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var d = w.DecoMap[y, x]; if (d != Deco.Tree && d != Deco.Sapling && d != Deco.Stump) { if (w.IsProtected(x, y)) _overlay.AddTile(w, x, y, _rot, new Color32(80, 140, 255, 70)); continue; }
                    _overlay.AddTile(w, x, y, _rot, w.IsClearing(x, y) ? Clearing : w.IsProtected(x, y) ? Protected : d == Deco.Tree ? new Color32(60, 170, 70, 105) : d == Deco.Sapling ? new Color32(170, 230, 120, 120) : new Color32(150, 110, 70, 100));
                }
            foreach (var b in w.Blds) if (b.T == "wood") AddReach("wood", b.X, b.Y, b.Sz, Reach, null);
        }

        static readonly Color32 NetRoad = new Color32(210, 245, 255, 110), HomeShort = new Color32(255, 80, 70, 130);

        // the connected road network, worked out once per world change rather than every frame
        HashSet<int> _netTiles; int _netVersion = -1;
        HashSet<int> NetworkTiles()
        {
            if (_netTiles == null || _netVersion != World.Version) { _netTiles = World.WaterNetwork(); _netVersion = World.Version; }
            return _netTiles;
        }

        /// <summary>Groundwater as blue patches (fainter as they run down, brighter under the cursor), homes short of water in red, and
        /// the road network the town's water travels (water has no radius: anything on these roads is in the one supply).</summary>
        void DrawWater(Vector2Int? hover, Building inspected)
        {
            var w = World;
            int under = hover.HasValue ? w.AquiferAt(hover.Value.x, hover.Value.y) : -1;
            if (inspected != null && inspected.T != "well") under = -1;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int a = w.AquiferMap[World.Tk(x, y)];
                    if (a < 0) continue;
                    var aq = w.Aquifers[a];
                    float full = aq.Cap > 0 ? (float)(aq.Reserve / aq.Cap) : 0;
                    _overlay.AddTile(w, x, y, _rot, new Color32(70, 150, 255, (byte)(30 + 60 * full + (a == under ? 50 : 0))));
                }
            if (w.LastWater != null)
                foreach (var kv in w.LastWater.Why)
                    if (kv.Value != WaterWhy.Ok) foreach (var (x, y) in kv.Key.Tiles()) _overlay.AddTile(w, x, y, _rot, HomeShort);
            foreach (int k in NetworkTiles()) _overlay.AddTile(w, k % N, k / N, _rot, NetRoad);
        }

        // the well preview is worked out once per tile and world state, not every frame
        Vector2Int _forecastAt = new Vector2Int(-1, -1); int _forecastVersion = -1, _forecastDay = -1; World.WellForecast _forecast;
        World.WellForecast WellForecastAt(Vector2Int h)
        {
            if (_forecast == null || h != _forecastAt || World.Version != _forecastVersion || World.Day != _forecastDay)
            {
                _forecast = World.ForecastWell(h.x, h.y); _forecastAt = h; _forecastVersion = World.Version; _forecastDay = World.Day;
            }
            return _forecast;
        }

        /// <summary>What a cistern would add: room for the town's spare water. Any spot touching a connected road joins the one supply.</summary>
        string CisternTip(Vector2Int h)
        {
            var (stored, room) = World.WaterStored();
            bool joins = World.WouldConnect("cistern", h.x, h.y);
            return $"Stores {WaterTuning.CisternCap:0}: the town's reserve would go from {stored:0}/{room:0} to {stored:0}/{room + WaterTuning.CisternCap:0}.\n" +
                (joins ? "Spare water from wells and the spring fills it, and it covers any shortfall in the town." : "It must touch a connected road to join the supply.");
        }

        /// <summary>An aquifer in plain words: what it holds, its recharge, what its wells draw, its trend and how long it lasts.</summary>
        string AquiferLine(Aquifer a)
        {
            var days = World.DaysLeft(a.Reserve, a.Recharge, a.Draw);
            return $"Groundwater: {a.Reserve:0} of {a.Cap:0}, recharges {a.Recharge:0.#} a day; its wells drew {a.Draw:0.#} yesterday. {a.Trend}" +
                (days is double d ? $": empty in about {System.Math.Floor(d)} days" : a.Trend == "Dry" ? ": giving only what recharges" : "") + ".";
        }

        /// <summary>Water lines for the info panel: wells, stores and homes.</summary>
        IEnumerable<string> WaterLines(Building b)
        {
            var w = World; var p = w.LastWater;
            if (b.T == "well")
            {
                var s = w.SourceFor(b);
                yield return $"Pumped {b.Pumped:0.#} of {s.Pump:0} yesterday" + (p != null && b.Pumped < s.Pump - 0.05 && p.ShortHomes == 0 ? " (all its homes had enough and storage is full)" : "") + ".";
                if (s.Aquifer >= 0) yield return AquiferLine(w.Aquifers[s.Aquifer]) + $" {w.Blds.Count(o => o.T == "well" && w.AquiferAt(o.X, o.Y) == s.Aquifer)} well(s) share it.";
                else yield return $"An old well with no groundwater patch beneath it: it seeps {WaterTuning.WellSeep:0} a day.";
                if (!b.Served) yield return "Not on a connected road: its water goes nowhere.";
            }
            if (b.T == "cistern" || b.T == "hall")
            {
                yield return $"Stored water {b.Water:0} of {World.StoreCapOf(b):0}" + (b.T == "hall" ? $"; its spring gives {WaterTuning.HallSpring:0} a day (pumped {b.Pumped:0.#} yesterday)." : ".");
                if (p?.StoreDays is double days) yield return $"Stored water is covering {p.FromStore:0.#} a day of shortfall: it lasts about {System.Math.Floor(days)} more days at this rate.";
            }
            if (b.T == "house" && b.Served)
            {
                if (b.WaterWhy == WaterWhy.Ok) yield return b.WaterNeed > 0.05 ? $"Water: {b.WaterNeed:0.#} a day, all delivered" + (b.WaterStocked > 0.05 ? $" ({b.WaterStocked:0.#} from stored water)." : ".") : "Water: nobody to drink yet.";
                else yield return $"Short of water: {World.WhyText(b.WaterWhy)} (got {b.WaterGot + b.WaterStocked:0.#} of {b.WaterNeed:0.#}).";
                if (b.Parched) yield return $"No water for {b.ShortDays} days: residents are leaving.";
                else if (b.ShortDays > 0) yield return $"Short for {b.ShortDays} day{(b.ShortDays == 1 ? "" : "s")}: residents leave after {WaterTuning.Grace}.";
            }
            else if (b.T == "house") yield return $"Short of water: {World.WhyText(WaterWhy.NoRoad)}.";
        }

        /// <summary>The well preview in words: pumping limit vs what the town would really get, and what it does to the aquifer.</summary>
        string WellTip(Vector2Int h)
        {
            var f = WellForecastAt(h);
            string gain = f.ToHomes + f.ToStorage < 0.05 ? "the town would get nothing more today (needs are met and storage is full)"
                : f.ToHomes < 0.05 ? $"the town gets +{f.ToStorage:0.#} into storage (homes already have enough)"
                : $"the town gets +{f.ToHomes:0.#} a day to homes" + (f.ToStorage > 0.05 ? $" and +{f.ToStorage:0.#} into storage" : "");
            string aq = f.Aquifer < 0 ? "" : $"\nGroundwater {f.AqReserve:0}/{f.AqCap:0}, recharge {f.AqRecharge:0.#}, draw would be {f.AqDraw:0.#}: {f.Trend}"
                + (f.DaysLeft is double d ? $", empty in about {System.Math.Floor(d)} days" + (f.ToStorage > 0.05 ? " (the draw drops once storage is full)" : "") : "");
            if (!f.Connected) return $"Pumps up to {f.MaxPump:0} a day, but this spot does not touch a road connected to the Town Hall, so none of it would reach the town.{aq}";
            return $"Pumps up to {f.MaxPump:0} a day; {gain}.{aq}";
        }

        static string ZoneName(Zone z, bool flax = false) => z switch
        {
            Zone.Res => "Housing (level 1)", Zone.Res2 => "Housing (level 2)", Zone.Farm => flax ? "Flax field" : "Wheat field", Zone.Wood => "Lumber", Zone.Mine => "Mining", Zone.Comm => "Commerce",
            Zone.Pigs => "Pig pen", Zone.Sheep => "Sheep pen", Zone.Chickens => "Chicken pen", _ => "No",
        };

        /// <summary>Why a building that can be placed here may still disappoint: no connected road yet, or zoned land it would cover.
        /// Null when there is nothing to warn about. Shown amber; the click still builds.</summary>
        string PlaceWarning(string type, int x, int y)
        {
            var notes = new List<string>();
            if (type == "mine")
            {
                int sz = PlacementSize(type), face = World.MineFaceAt(x, y, sz);
                if (face < 0) notes.Add("No hill beside it: iron mines go at the foot of a hill or mountain, or they never find ore");
                else
                {
                    var (ex, ey) = World.EntranceTile(x + (face == 3 ? sz - 1 : 0), y + (face == 0 ? sz - 1 : 0), face);
                    if (World.Inb(ex, ey) && World.Grid[ey, ex]?.T != "road") notes.Add($"Its entrance faces away from the hill: a road must reach {ex},{ey}");
                }
            }
            if (!World.WouldConnect(type, x, y)) notes.Add("No connected road here: it stays idle until a road reaches it");
            int zoned = World.ZonedUnder(type, x, y);
            if (zoned > 0) notes.Add($"It covers {zoned} zoned tile{(zoned == 1 ? "" : "s")}");
            return notes.Count == 0 ? null : string.Join(". ", notes);
        }

        bool _tipWarn;   // the cursor tip is a warning (amber): set by CursorTip

        // the placement forecast for the tile under the mouse, kept until the mouse, the tool or the world changes (it walks the crews' paths)
        (string type, int x, int y, int version) _forecastKey; Forecast _forecastValue;
        Forecast ForecastAt(string type, int x, int y)
        {
            var key = (type, x, y, World.Version);
            if (_forecastValue == null || _forecastKey != key) { _forecastValue = World.ForecastPlacement(type, x, y); _forecastKey = key; }
            return _forecastValue;
        }

        /// <summary>The line beside the mouse: what a click will do here, or why it cannot (bad = red).</summary>
        (string text, bool bad) CursorTip()
        {
            _tipWarn = false;
            if (!_hover.HasValue || _tool.Id == "look") return (null, false);
            var h = _hover.Value;
            if (_tool.Claim)
            {
                var (cpx, cpy) = World.ParcelXY(World.ParcelOf(h.x, h.y));
                if (World.ParcelClaimed(cpx, cpy)) return ($"Your land. You own {World.ClaimedParcels} of {World.ParcelsPerSide * World.ParcelsPerSide} parcels", false);
                var cc = World.CanClaim(cpx, cpy);
                if (!cc.Ok) { _tipWarn = World.ParcelTouchesYours(cpx, cpy); return (cc.Reason, !_tipWarn); }
                return ($"Click to claim this parcel (free): {World.DescribeParcel(cpx, cpy)} ({World.ClaimedParcels} of {World.ParcelsPerSide * World.ParcelsPerSide} owned)", false);
            }
            if (_tool.FerryRoute) return FerryTip(h);
            if (_tool.Road || _tool.Wall || _tool.Bridge || _tool.Canal || _tool.Moat)
            {
                var path = ToolPath(_roadAnchor ?? h, h);
                var r = _tool.Moat ? World.MoatPathCheck(path) : _tool.Canal ? World.CanalPathCheck(path) : _tool.Bridge ? World.BridgePathCheck(path) : _tool.Wall ? World.WallPathCheck(path) : World.RoadPathCheck(path);
                if (!r.Ok) return (r.Reason, true);
                int n = r.Count, cost = _tool.Moat ? n * World.MoatWood : _tool.Canal ? n * World.CanalWood : _tool.Bridge ? World.BridgeCost(path) : _tool.Wall ? n * Defs.Cost["wall"][Res.Wood] : n;
                string what = _tool.Moat ? "moat" : _tool.Canal ? "canal" : _tool.Bridge ? "bridge" : _tool.Wall ? "palisade" : "road";
                return _roadAnchor.HasValue ? ($"{n} new {what} tile{(n == 1 ? "" : "s")}, {cost} wood. Let go to build", false) : ($"Press and drag to lay a {what}", false);
            }
            if (_tool.Build != null)
            {
                var r = World.CanPlace(_tool.Build, h.x, h.y);
                if (!r.Ok) return (r.Reason, true);
                var warn = PlaceWarning(_tool.Build, h.x, h.y); _tipWarn = warn != null;
                string water = "";
                if (_tool.Build == "well")
                {
                    var f = WellForecastAt(h);
                    water = "\n" + WellTip(h);
                    if (f.Trend == "Drawing down" || f.Trend == "Dry" || f.ToHomes + f.ToStorage < 0.05) _tipWarn = true;
                }
                else if (_tool.Build == "cistern") water = "\n" + CisternTip(h);
                // what it would make right here, in food or goods a day (World.Forecast.cs): moving the mouse shows which spots are good
                var forecast = ForecastAt(_tool.Build, h.x, h.y);
                if (forecast.Warn) _tipWarn = true;
                string made = forecast.Lines.Count > 0 ? "\n" + forecast.Text : "";
                // water tips run long: one fact per line, so the tip stays narrow
                return ($"Click to build: {Hud.CostText(_tool.Build)}" + (warn != null ? (water.Length > 0 || made.Length > 0 ? "\n" : ". ") + warn : "") + water + made, false);
            }
            if (_tool.Zone != Zone.None)
            {
                var (x0, y0, x1, y1) = RectTo(h);
                if (World.IsLivestockZone(_tool.Zone))
                {
                    if (!_zoneAnchor.HasValue) return ($"Drag a rectangle. {World.TilesPerAnimal(_tool.Zone)} tiles per animal.", false);
                    var pen = World.CheckPen(_tool.Zone, x0, y0, x1, y1);
                    return pen.Ok
                        ? ($"Capacity {pen.Count} animals. Fence costs {2 * (x1 - x0 + y1 - y0 + 2)} wood. Starter livestock cost {World.StarterLivestockGold} gold.", false)
                        : (pen.Reason, true);
                }
                int ok = 0, bad = 0, already = 0, offRoad = 0; string why = null;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        if (World.AlreadyZoned(_tool.Zone, x, y)) { already++; continue; }
                        var r = World.ZoneCheck(_tool.Zone, x, y);
                        if (!r.Ok) { bad++; why ??= r.Reason; continue; }
                        if (World.Grid[y, x] != null) continue;   // a building already carrying this zone: nothing to do
                        ok++;
                        if ((_tool.Zone == Zone.Res || _tool.Zone == Zone.Res2) && !World.BesideConnectedRoad(x, y)) offRoad++;
                    }
                string alreadyNote = already > 0 ? $"{already} already {_tool.Label}" : null;
                if (!_zoneAnchor.HasValue)
                    return ok > 0 ? ("Press and drag over the land" + (offRoad > 0 ? " (homes only grow beside a connected road)" : ""), false)
                        : already > 0 ? ($"Already {_tool.Label}", false) : (why, true);
                string size = $"{x1 - x0 + 1} x {y1 - y0 + 1}: ";
                if (ok == 0) return (size + (already > 0 ? $"already {_tool.Label}, nothing to zone" : why), true);
                var parts = new List<string> { $"{ok} tile{(ok == 1 ? "" : "s")} to zone" };
                if (offRoad > 0) parts.Add($"{offRoad} not beside a connected road");
                if (alreadyNote != null) parts.Add(alreadyNote);
                if (bad > 0) parts.Add($"{bad} cannot be ({why})");
                return (size + string.Join(", ", parts), false);
            }
            if (_tool.Unzone)
            {
                var (x0, y0, x1, y1) = RectTo(h);
                int n = 0;
                var pens = new HashSet<LivestockPen>();
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    if (World.PenAt(x, y) is LivestockPen pen) pens.Add(pen);
                    else if (World.Grid[y, x] == null && World.ZoneMap[y, x] != Zone.None) n++;
                }
                if (pens.Count > 0) return ($"Remove {pens.Count} entire pen(s) with livestock" + (n > 0 ? $" and {n} other zone tiles" : ""), false);
                return _zoneAnchor.HasValue ? ($"Remove the zone from {n} tile{(n == 1 ? "" : "s")}", n == 0) : ("Press and drag over zoned land", false);
            }
            if (_tool.Clear)
            {
                if (World.PenAt(h.x, h.y) != null) return ("Remove the entire pen and its livestock", false);
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
            if (b.T == "mine") return $"Crew: {home} resting here, {down} underground in the tunnel";
            return $"Crew: {home} resting here, {outw} walking out, {at} working, {back} carrying home";
        }

        string UndergroundLine(int x, int y)
        {
            var w = World;
            if (!w.IsSurveyed(x, y))
                return w.ZoneMap[y, x] == Zone.Mine ? "Underground: unknown (a mine's tunnel reveals the ore around it as it is dug)" : "Underground: unknown. Zone it for mining (Mining, 6) and let a tunnel reach it";
            var parts = new List<string>();
            for (int l = 1; l <= World.Bands; l++) { double o = w.KnownOre(x, y, l); parts.Add($"band {l} " + (o > 1e-9 ? $"{o:0} ore" : "none")); }
            return "Underground (revealed by digging): " + string.Join(", ", parts);
        }

        string TileInfo(int x, int y)
        {
            var w = World; var lines = new List<string>();
            var b = w.Grid[y, x];
            if (w.Waters != null && w.Waters.IsWater(x, y))
            {
                var wl = w.DescribeWater(x, y);
                if (b != null) wl.Insert(0, b.Served ? "WOODEN BRIDGE (connected)" : "WOODEN BRIDGE (cut off from the Town Hall)");
                wl.Add($"Tile {x},{y}");
                return string.Join("\n", wl);
            }
            if (b != null && b.T != "road")
            {
                lines.Add(b.Name.ToUpper());
                lines.Add(b.T == "watchtower" ? $"On watch within {World.WatchtowerRange} tiles. Alerts the whole town." :
                    b.Served ? "Connected to the Town Hall" : "Not connected: needs a road to the Town Hall");
                if (b.T == "muster") lines.Add($"Militia roll: {w.Mil.Registered} of {w.Mil.MusterCapacity} places in all muster grounds, {w.Mil.MobilisedCount} mobilised. Houses nobody.");
                if (b.T == "barracks") lines.Add($"Professional soldiers: {w.Soldiers} for {w.D.Bunks} bunks in connected barracks. Recruit in the Military menu.");
                if (b.T == "house") lines.Add($"Homes {b.Capacity} villagers. Level {b.Lvl}");
                if (b.Jobs > 0)
                {
                    int need = w.ActiveJobs(b);
                    lines.Add(need < b.Jobs ? $"Workers {b.Workers}/{need} needed ({b.Jobs} jobs; " + (b.T == "farm" ? $"one farmer for every {World.FieldsPerFarmer} fields from sowing to harvest)" : "more are needed as more tiles are marked or zoned)") : $"Workers {b.Workers}/{b.Jobs} ({b.Eff * 100:0}%)");
                }
                if (b.T == "farm" && w.SeasonalFarming)
                {
                    lines.Add($"Works {w.FieldsOf(b)}/{b.Jobs * World.FieldsPerFarmer} fields of Farmland within {World.FarmReach} tiles ({World.FieldsPerFarmer} per farmer).");
                    int fallow = w.FallowFields();
                    if (fallow > 0) lines.Add($"{fallow} Farmland tiles in town lie fallow: out of reach of a farm, or more than the farms can work. Build another farm near them.");
                }
                if (World.CrewKind(b.T) != Deco.None) lines.Add(CrewLine(b));
                if (b.T == "mine") lines.Add(w.MineStatus(b));
                if (b.T == "quarry") lines.Add(w.QuarryStatus(b));
                if (b.T == "wood") lines.Add(w.LodgeStatus(b));
                if (b.T == "sawmill") lines.Add(w.SawmillStatus(b));
                if (b.T == "charcoal") lines.Add(w.CharcoalStatus(b));
                foreach (var warn in w.BuildingWarnings(b)) lines.Add(warn.ToString());   // what is wrong, why, and what to do (World.Warnings.cs)
                if (b.T == "wood" || b.T == "sawmill" || b.T == "hall")
                {
                    bool saw = w.Blds.Any(s => s.T == "sawmill" && s.Served);
                    lines.Add($"Logs waiting to be sawn: {w.ResAmt[Res.Logs]:0}" + (saw ? "" : ". Build a Sawmill (Wood, 4): the Town Hall saws only 1 a day at half yield"));
                }
                if (b.T == "smithery")
                {
                    lines.Add($"{OrePerMetal} iron ore per metal. Staffed capacity: {SmeltRate * b.Eff:0.##} metal/day.");
                    lines.Add($"Stockpile: {w.ResAmt[Res.Ore]:0.##} ore · {w.ResAmt[Res.Metal]:0.##}/{w.CropStorage(Res.Metal)} metal");
                    lines.Add("Charcoal fuels up to 1 extra metal per day with a full crew.");
                }
                if (b.T == "storehouse") lines.Add($"+{World.StorehouseSpace} storage per material. Hauls up to {b.Workers * World.HaulPerWorker} extra goods/day.");
                if (b.T == "smokehouse") lines.Add($"Fresh meat {w.FreshMeatStored:0.#} · Winter reserve {w.ResAmt[Res.SmokedMeat]:0.#}");
                if (b.T == "charcoal") lines.Add($"Fuel stockpile {w.ResAmt[Res.Charcoal]:0.#}. {World.LogsPerCharcoal} logs per charcoal.");
                if (b.T == "fisher" || b.T == "dock")
                {
                    var fg = w.FishingGrounds(b);
                    if (fg.grounds == 0) lines.Add("No water it can fish: nothing " + (b.T == "dock" ? "deep enough" : "reachable on foot") + " within " + World.FishReach + " tiles.");
                    else
                    {
                        double stock = w.StockOf(fg.body);
                        lines.Add($"Fishes {World.WaterClassName(fg.kind).ToLower()} water: {fg.grounds} tiles in reach, {World.FishLevel(fg.perWorker)} catch (about {fg.perWorker:0.##} food per fisherman a day at full stock).");
                        if (b.T == "dock") lines.Add($"Boats: {w.DockBoatsInService(b)} fishing boat{(w.DockBoatsInService(b) == 1 ? "" : "s")} in service (up to {World.DockBoatsMax}, one for every two fishermen). Without boats the fishermen only get a quarter of the catch from the pier. Order boats at a Boatyard (Vessels window).");
                        lines.Add($"Fish stock: {World.StockWord(stock)} ({stock * 100:0}%). Season: {w.Season} (x{w.FishSeason(fg.kind):0.#}). Today's catch: {w.FishCatch(b):0.##} food.");
                    }
                }
                if (b.T == "ferry")
                {
                    var rt = w.RouteOf(b);
                    lines.Add("Ferry route: " + w.FerryStatus(b));
                    if (rt != null)
                    {
                        var (ea, eb) = w.Ends(rt); var other = ea == b ? eb : ea;
                        if (ea != null && eb != null)
                        {
                            int len = (w.SailingRoute(ea, eb)?.Count ?? 1) - 1; var load = w.RouteLoad(rt); double service = w.RouteService(rt, len, load.demand);
                            lines.Add($"Linked to the landing at {other.X},{other.Y}: {len} tiles of sailing, about {World.TripsPerDay(len)} crossings a day each way.");
                            { var vs = w.VesselOfRoute(rt); lines.Add(vs == null ? "Ferry boat: none assigned (order one at a Boatyard)" : $"Ferry boat #{vs.Id}: condition {vs.Condition:0}%" + (w.InService(vs) ? "" : " (out of service)")); }
                            lines.Add($"Carries {World.FerryPassengers} people and {World.FerryCargo} cargo a trip: {World.PassengerCapacityPerDay(len)} people a day. Crew {w.CrewOf(rt)} of {World.FerryCrew}.");
                            lines.Add($"{load.buildings} buildings across the water ({load.people} people and jobs, demand about {load.demand:0} a day) depend on it. " + (service < 1 ? "It is crowded." : "It copes."));
                        }
                    }
                }
                if (b.T == "port") lines.Add("Port: " + w.PortStatus(b));
                if (b.T == "port") lines.Add($"Gives your trade one ship ({w.ShipsOut} of {w.ShipCapacity} ships at sea). It stands on {World.ModeName(w.PortMode(b)).Replace("by ", "")} water ({w.NavigableTilesInRegion(w.RegionOf(b))} boat-deep tiles). Ships sail only to settlements the world map shows on the same sea or lake; travel time follows the water route. Not a market: overland caravans need a Market.");
                if (b.T == "boatyard") lines.Add($"Boatyard: {w.VesselOrders.Count} order{(w.VesselOrders.Count == 1 ? "" : "s")} waiting, {w.Vessels.Count} vessel{(w.Vessels.Count == 1 ? "" : "s")} owned. Order and see boats in the Vessels window (button under New world). It repairs the worst boat each day while staffed and supplied with wood.");
                if (b.T == "watermill") lines.Add(w.RiverCurrentNear(b, 1) ? $"Mills for {World.MillFields} fields: adds {World.MillBonusShare * 100:0}% to the wheat harvest when staffed." : "No flowing river beside it (frozen or dry): the wheel is still.");
                if (b.T == "hunter") lines.Add($"Hunts mature forest within {World.HuntingRange} tiles in all seasons.");
                if (b.T == "hall") lines.Add($"Pit-saws 1 log a day at half yield. Houses {HallCap}.");
                if (b.T == "silo")
                    lines.Add(b.Served ? $"+{World.SiloFood} wheat and +{World.SiloFlax} flax storage" : "Connect to the Town Hall to use its crop storage.");
                if (b.T == "silo" && w.SeasonalFarming && w.DailyFoodDemand > 0)
                    lines.Add($"Town reserve: {w.ResAmt[Res.Food]:0}/{w.CropStorage(Res.Food)} food, about {System.Math.Floor(w.ResAmt[Res.Food] / w.DailyFoodDemand):0} days without a harvest.");
                if (b.T == "market") lines.Add($"Trade caravans on the road: {w.LandCaravansOut} of {w.Markets} (one per market). Trade from the world map (M): click a settlement.");
                lines.AddRange(WaterLines(b));
            }
            else if (b != null) lines.Add(b.Served ? "ROAD (connected)" : "ROAD (cut off from the Town Hall)");
            if (w.PenAt(x, y) is LivestockPen pen)
            {
                lines.Add(ZoneName(pen.Species).ToUpper());
                lines.Add($"{pen.Count}/{pen.Capacity} animals · {pen.Area} tiles · {World.TilesPerAnimal(pen.Species)} tiles per animal");
                if (pen.Count > 0)
                    lines.Add(pen.Keepers > 0 ? "Keeper: 1 villager (farming) looks after it." + (pen.Species == Zone.Chickens ? $" Hens lay every {World.DaysPerEgg} days." : "")
                        : "No keeper: the animals give nothing and do not breed. Free a villager or raise Farming in the Workforce panel.");
                lines.Add("Buy or slaughter animals in the Livestock menu.");
                return string.Join("\n", lines);
            }
            var d = w.DecoMap[y, x];
            // a short heading line of its own (the info panel shows the first line as its title), then the explanation
            if (d == Deco.Tree)
            {
                lines.Add("FOREST");
                lines.Add($"Game remaining: {w.HuntFoodRemaining(x, y)}/{World.ForestFoodReserve} food. Recovers 1 every {World.HuntingRecoveryDays} days.");
                lines.Add("Hunting lodges hunt here without zoning. Lumber zoning lets woodcutters fell the trees.");
            }
            if (d == Deco.Rock) { lines.Add(w.IsMountain(x, y) ? "MOUNTAIN" : "STONE OUTCROP"); lines.Add(w.IsMountain(x, y) ? $"Stone left {w.RockLeft(x, y):0}. Quarry it from the edge." : $"Stone left {w.RockLeft(x, y):0}."); }
            if (d == Deco.Ore) { lines.Add("IRON SHOWING"); lines.Add(w.IsSurveyed(x, y) ? "Ore shows at the surface here." : "Ore shows at the surface here; more lies inside the hill. Zone it for mining (Mining, 5) with the high ground, with a lot at the foot for the mine."); }
            var z = w.ZoneMap[y, x];
            int fieldKey = World.Tk(x, y);
            int sowDay = z == Zone.Farm && w.SeasonalFarming ? w.SownOn(fieldKey) : 0;
            bool planted = sowDay > 0 && sowDay >= w.Day - w.YearDay + 1 && w.YearDay <= 180;
            bool flax = z == Zone.Farm && (planted ? w.PlantedFlax(fieldKey) : w.IsFlaxField(x, y));
            if (planted && b == null)
            {
                int ready = sowDay + World.CropGrowth(flax);
                lines.Insert(0, flax ? "FLAX FIELD" : "WHEAT FIELD");
                lines.Add(ready > w.Day ? $"Sown on day {sowDay}. Ripens on day {ready}." : "Ripe. Waiting for farm workers to harvest.");
                return string.Join("\n", lines);
            }
            // open land has no heading yet: name it after its zone or its ground, so the title line is always short
            bool zoneHeading = lines.Count == 0 && z != Zone.None && d != Deco.Stump && d != Deco.Sapling;
            if (lines.Count == 0)
                lines.Add(zoneHeading ? (z == Zone.Farm ? ZoneName(z, flax) : ZoneName(z) + " zone").ToUpper()
                    : d == Deco.Stump ? "STUMP" : d == Deco.Sapling ? "SAPLING" : w.Fert[y, x] ? "FERTILE GROUND" : "OPEN GROUND");
            // the underground only matters where ore can be: hills, mountains, rock, ore showings, mining land, surveyed tiles
            if (z == Zone.Mine || d == Deco.Rock || d == Deco.Ore || w.IsSurveyed(x, y) || w.TileLevel(x, y) >= 1) lines.Add(UndergroundLine(x, y));
            if (d == Deco.Stump) lines.Add("It rots away into open ground.");
            if (d == Deco.Sapling) lines.Add("It grows into a tree in three years.");
            if (z == Zone.Farm) lines.Add(flax ? "Produces flax." : "Produces food.");
            else if (z != Zone.None && !zoneHeading) lines.Add(ZoneName(z) + " zone");
            if ((z == Zone.Res || z == Zone.Res2) && b == null)
                lines.Add(!w.BesideConnectedRoad(x, y) ? "No home will grow here: it does not touch a road connected to the Town Hall."
                    : w.Dem[Zone.Res] > World.DemandThreshold ? "Homes are wanted: one should grow here soon."
                    : "Waiting: homes grow when the Homes meter (bottom bar) says homes are wanted.");
            if (z == Zone.Res2 && b == null && w.Note.TryGetValue(Zone.Res2, out var stoneNote) && stoneNote.StartsWith("Waiting")) lines.Add(stoneNote);
            if (w.AquiferAt(x, y) is int aq && aq >= 0 && (b == null || b.T != "well")) lines.Add(AquiferLine(w.Aquifers[aq]) + " A well here could tap it.");
            if (w.IsMoat(x, y)) lines.Add(w.Grid[y, x]?.T == "road" ? "MOAT CAUSEWAY: the road is the one way across the moat here" : "MOAT: a wall to walkers and raiders; only a road across it (a causeway) lets anyone over");
            if (w.IsCanal(x, y)) lines.Add(w.CanalIsFed(x, y) ? "IRRIGATION CANAL: joined to fresh water, so the soil within " + World.CanalReach + " tiles is moister" : "IRRIGATION CANAL: not joined to fresh water, so it irrigates nothing yet");
            if (w.IrrigationBonus(x, y) > 0 && !w.IsCanal(x, y)) lines.Add($"Irrigated: a canal adds {w.IrrigationBonus(x, y):0.00} to the soil moisture here");
            if (w.SeasonNote(x, y) is string seasonNote && w.Waters.Class[y * N + x] == WaterClass.None) lines.Add(seasonNote);
            if (w.Waters != null && w.Waters.Class[y * N + x] == WaterClass.None)
            {
                double moist = w.SoilMoisture(x, y);
                lines.Add($"Soil moisture: {World.MoistureWord(moist)}" + (z == Zone.Farm || w.Fert[y, x] ? $" (crops yield x{w.MoistureYield(x, y):0.##})" : "") + (w.FloodPlain(x, y) ? "   Flood plain: low land beside a river (floods are not simulated yet)" : ""));
            }
            lines.Add($"Tile {x},{y}   level {w.TileLevel(x, y):0.##}" + (w.Fert[y, x] ? "   fertile soil" : "") + (w.TileSpread(x, y) >= 3 ? "   too steep" : ""));
            return string.Join("\n", lines);
        }
    }
}
