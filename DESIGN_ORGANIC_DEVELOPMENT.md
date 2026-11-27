# Organic Settlement Development and Economic Zoning Overhaul

**Status (updated each phase PR):** Phases 1 (audit, document, district framework) and 2 (shared engine core: development queue, reservations, construction, approval, housing on it) built. Phases 3 to 10 are **planned, not built**.
Unity port only; the browser prototype is untouched.

**This document supersedes `DESIGN_RESOURCE_EXTRACTION.md` where they disagree.** That earlier design made forestry, quarrying and mining
"automatic working areas with painted zones as an optional boundary" and told the game to stop teaching zones for them. Zach has reversed
the direction: **zones are now the primary way to start ordinary economic activity** (a district builds its own lodges, quarries, mines, farmhouses,
hunting lodges and fishing huts). What stays from the earlier work is everything geological, forestry-, conservation- and clearing-related:
finite stone and ore, pits, tunnels, sustainable/intensive/clearing modes, replanting, protected woodland, universal land clearing, the timber chain
and the site surveys. See section 9 for exactly how the two fit together.

## 0. Zach's spec (kept in his words, complete in intent)

**Design philosophy (permanent): "Zone ordinary economic activity. Manually construct strategic infrastructure."** Instead of placing every
farmhouse, mine, quarry, hunting lodge and woodcutter's lodge, the settlement develops appropriate buildings automatically inside player-designated
zones, according to demand, resources, available workers and priorities (inspired by TheoTown, Cities: Skylines and Manor Lords, but medieval,
physical-economy and geographical). This milestone is completed before Government Phase 3 and further multi-settlement/governor work.

**Core rule (also Zach's): a settlement must be able to succeed in a variety of ways** (trade, agriculture, conquest, fishing and the sea, mining and
industry, fortress), so the development engine supports specialised settlements, not one template.

### Part I: Development model
Zones: Residential, Agricultural, Forestry, Mining, Quarrying, Hunting, Fishing. Extensible (commercial and industrial later) but **no commercial or
private-industrial development now**. Existing manual buildings keep working. Strategic infrastructure stays manual and is never auto-built without
authorisation: town halls, roads, bridges, ferries, canals, walls, gates, watchtowers, barracks, fortifications, major markets, ports, boatyards,
major warehouses, major industry, government buildings, important public/religious/cultural buildings. Manual placement of ordinary economic buildings
stays possible with the **same** costs, placement rules, labour and resource accounting as automatic development; manual buildings count toward a
district's capacity and the engine must not build duplicates; the player can disable automatic development per district.

### Part II: Zoning
Workflow: open the Build catalogue, pick an economic zone, see the contextual overlay, drag over the area, confirm, connect to infrastructure, let it
develop. **Zoning is free; construction consumes resources and labour.** A forestry district eventually produces its own woodcutter lodge. Connected
same-type zone tiles form **districts** with stable identity, zone type, tiles/boundary, buildings, pending development, priority, mode, resource
availability, estimated production potential, employment, production and problems; renameable; sensible default names ("Northern Forestry District",
"Eastern Farmland", "Hill Quarry", "Riverside Residential District", "Western Iron District"); expansion, splitting and merging deterministic and
settings preserved where practical; no heavy administration. Suitability differs per zone and is shown through overlays and placement feedback:
residential (roads, distance to work, demand, water, services, terrain, safety); agricultural (fertility, water, climate, terrain, season, roads);
forestry (density, maturity, renewable potential, access, protected woodland, existing operations); mining (underground deposits, accessible depth,
reserves, entrance suitability, technology, transport); quarrying (surface stone, reserves, excavation suitability, access, infrastructure); hunting
(wildlife, habitat, forest cover, hunting pressure, access); fishing (shoreline, fish stock, depth, access, existing activity, season). Compatibility:
no houses in active quarry pits; distinguish building footprints, exclusive land-use zones, underground resources, renewable catchments and temporary
work areas; underground mining must not reserve every surface tile above a deposit; compatible coexistence without duplicating resources; the default
stays simple and the rules are documented (section 4).

### Part III: Engine
Generalise the residential development system into one economic development engine. Every project: identify a need, eligible districts, existing
capacity, suitable locations, expected benefit, cost, labour, transport/operational needs, player priorities and restrictions, then create a
construction project. Use the same validation as manual construction; automatic buildings never bypass simulation rules. Demand is real, not "zoned
land exists": residential (housing shortage, immigration, growth); agricultural (food demand, falling reserves, seasonal forecasts: prepare ahead);
forestry (timber use, falling stock, trade); mining (mineral demand and downstream industry; never more mines just because a big zone exists);
quarrying (construction demand, falling stone); hunting (immediate food need and sustainable capacity); fishing (food demand and fishing capacity).
**Initial development:** a newly designated suitable district represents intent and establishes its FIRST basic production building when technology,
affordability, labour, suitability and a non-paused state allow (a mining district's first mine is not blocked because no smithery exists; a first
forestry district needs no timber crisis), balanced against reckless spending; later expansion depends more on demand. Scoring is simple, explainable and
deterministic (urgency, priority, expected production, stock, workers, cost, operating cost, roads, suitability, local capacity, land, future demand;
deterministic tie-breaks); the player can see why a district develops or not. **Prevent overdevelopment:** check existing buildings and vacant jobs,
production, existing projects, natural resources, forecast demand, materials, workforce and overlapping working areas; prefer staffing existing
buildings; no second woodcutter lodge while the first has no workers; no new farm because one is idle in winter; no new mine when nearby mines already
exploit the reserve at adequate capacity; cooldowns and planning horizons against oscillation. **Order of priority:** survival, essential housing and
water, keeping existing productive capacity, critical shortages, explicit player directives, ordinary development, optional expansion; hard player
restrictions always win; district priority influences competition for resources but never lets a high-priority industry starve the settlement.

### Part IV: Construction
Auto-built buildings consume real resources at existing costs; track available, reserved, consumed, pending and completed materials; no two projects
reserve the same materials; cancelled projects return only eligible unused materials. Use the existing workforce/crew systems or construction mechanism;
if construction is instant today, add only a minimal scalable construction queue; development must not consume all labour forever and must protect food
and critical services. Funded from the existing settlement economy (no private economy). Optional construction reserve (e.g. keep 100 logs, 50 stone, a
minimum gold, a cap on simultaneous auto projects) with sane defaults needing no setup. Unified development queue showing project, district, cost, reason,
expected benefit, status, missing requirements and progress, with approve, reject, pause, cancel and reprioritise; routine projects need no approval
unless the player picks that mode.

### Part V: District management
Development priority per district: **Disabled, Low, Normal (default), High, Critical** (separate from operational workforce priority; pausing or disabling
development never stops existing buildings working). Three modes: **Automatic (default), Approval required, Paused.** Optional advanced restrictions
(max buildings, max employment, max construction spend, minimum local reserves, production target, preferred mineral, forestry harvest mode, protected
land, construction boundaries); a player can zone and never touch settings. A standard **District inspector** (large panel, existing medieval theme;
**Zach's rule: big management menus are large panels with exact numeric entry**): name, zone, area, buildings, workers, production, resource potential,
priority, mode, pending projects, problems, optional advanced section; it must EXPLAIN why development is or is not happening ("waiting for enough
timber for the first farmhouse", "additional mines unnecessary: existing mines have unused capacity", "limited by insufficient mature trees", "no
suitable mine entrance in the area", "development paused", "additional workers needed", "existing projects have priority").

### Part VI: Individual systems
- **Residential:** keep and improve the organic housing system (demand, resources, roads, water/services, space, tech), evolving with eras, not filling a
  district when demand is low, preserving approved housing sprites.
- **Agriculture:** the zone decides where, the simulation decides how many farmhouses (cultivable land, workers, food demand, fertility, season, storage,
  roads, existing farms); never more farmhouses than the farmland supports or farmhouses eating the farmland; keep crop selection and seasonal farming.
- **Forestry:** the zone develops woodcutter lodges that harvest eligible trees in a permitted area with basic automatic replanting. Modes: *Sustainable*
  (default: mature trees only, replant, respect growth, limit harvest when long-term capacity is short), *Intensive* (more short-term timber, more
  depletion, warn on declining reserves), *Permanent clearing* (only in explicitly designated clearing areas, recover timber, no replanting, separate
  explicit designation, never silent). The Forester stops being mandatory for basic timber (optional specialised improvement). Lodges must not harvest the
  same trees; productivity tied to real growth, climate, density and maturity.
- **Mining:** the zone develops mine entrances where accessible deposits exist; keep geological depth and finite reserves; evaluate mineral types,
  quality, reserves, accessible depth, entrance location, technology, roads, existing mines and industrial demand; once built, a mine chooses its own
  extraction places (no separate underground extraction zone); several mines on one formation share its reserve; constructing a mine never creates ore;
  technology can unlock depth or efficiency; warn as reserves decline; exhausted mines persist through saves; ready for more minerals later without adding
  them all now.
- **Quarrying:** the zone develops quarries near surface stone; quarries extract only from their permitted area; keep physical terrain excavation and
  persistent pits; never destroy existing infrastructure or dig outside authorised boundaries; quarries on one formation share finite reserves.
- **Hunting:** lodges when real hunting potential exists (forest cover, wildlife, season, pressure, access, existing lodges, food demand); no unlimited
  hunting or more lodges than grounds support; must help early-game food before harvests; keep food reserve and preservation systems.
- **Fishing:** small fishing huts develop automatically at suitable shorelines using real fish stock, depth, access, season, worker needs, food demand
  and roads; major docks, ports and boatyards stay manual; keep maritime/vessel systems.

### Part VII: Processing
Four wood resources where supported: raw logs, planks, firewood, charcoal (no duplicate definitions); lodges produce real logs; sawmills turn logs into
construction timber; firewood and charcoal use timber inputs; review construction costs so a founding settlement can bootstrap without excess specialised
buildings; no new heating/fuel simulation. Major processing (sawmills, smitheries, charcoal, major food processing) stays manual for now, but the engine
recognises downstream demand (a smithery raises the value of expanding a suitable mining district); industrial zoning is a possible later extension.

### Part VIII: Land clearing
A universal clearing tool for trees, bushes, removable rocks and other removable obstacles, creating real work orders via existing crews, recovering
usable resources, respecting ownership, protection, terrain, structures and worker access; no specialised building needed to clear a small site.
Construction-site clearing: when a building needs clearing, queue it; auto development identifies footprints, determines clearing needed, reserves the
site, clears permitted obstacles, recovers materials, then builds; never clears protected woodland or strategic terrain without authorisation; prefers
less destructive sites; prevents conflicting construction and clearing orders. Protected land is respected by forestry, surface mining construction,
quarrying, automatic residential and agricultural expansion, hunting restrictions and clearing; the player can remove protection; persisted in saves;
auto development never silently overrides hard restrictions.

### Part IX: Catalogue and UI
Use the existing Build catalogue (**no permanent bottom toolbar**). A prominent **Zoning section** with Residential, Agriculture, Forestry, Mining,
Quarrying, Hunting, Fishing: recognisable icons or existing sprites, each card explaining what develops, required conditions, output, behaviour and
restrictions; manual buildings stay in their categories and zone tools are clearly distinct from placement. Zoning interaction: click-drag, tile previews,
valid/invalid feedback, automatic relevant overlays, zone removal and expansion, district selection; restrained colours; overlays unobtrusive when not
zoning; optional district overlay. Contextual overlays on zone selection (only real data): residential roads/services, agriculture fertility/water,
forestry density and mature trees, mining geology and accessible depth, quarrying surface geology, hunting habitat, fishing shoreline and accessible
fishing water; restore the previous overlay on leaving. Compact demand indicators (housing, food, timber, stone, minerals) from real production,
consumption, reserves and capacity, not seven big permanent bars, details in the Economy/Settlement UI, with reasons for significant changes. Actionable
feedback with real values on every district that is not developing ("Development waiting: insufficient construction timber. The proposed woodcutter
lodge requires 20 logs; available 12", "No new mines required: the existing mine has six unfilled worker positions", "Development blocked: no connected
road").

### Part X: Governors
Do not implement governor automation or multi-settlement management, but expose simulation-level operations governors can use later: create/expand
districts, evaluate suitability, adjust priorities and modes, approve projects, manage budgets, identify shortages, estimate capacity, find bottlenecks,
protect land, manage extraction intensity; strategic infrastructure stays subject to player authorisation and delegation. Specialised settlements
(agricultural village, mining town, forestry settlement, coastal fishing settlement, frontier fortress) arise naturally from geography, resources and
player priorities.

### Part XI: Performance, determinism, saves
Scale to large settlements: no per-day evaluation of every footprint; cached candidates, spatial indexing or chunk searches, dirty flags, scheduled
district evaluation, resource caches, shared reservations, construction queues; simulation independent of Unity rendering. Deterministic: same seed,
actions and state give the same development, reproducible through save/load. Migrate existing saves: residential zones, farmland, lumber zones, mining
zones, quarries, manual production buildings, forestry buildings, geological reserves, forest growth states and inventories convert into the district
framework where appropriate; never auto-demolish manual buildings, never reset depleted deposits, never replenish harvested forests; buildings outside
zones keep operating under a documented legacy behaviour; document every migration decision. Persist district IDs, names, types, boundaries,
priorities, modes, restrictions, pending projects, reservations, forestry modes, protected land, clearing orders and relevant history; loading must not
duplicate projects or reservations.

### Part XII: Balance and tests
The player must be able to build a viable first year by roads, residential zone, hunting/fishing zone, farmland, forestry, quarry or mining zone and the
essential manual infrastructure, with no development delay that causes unavoidable starvation, emergency food first; verify the optimal opening against
the simulation. (The old first-year tutorial was **removed on purpose**; do not restore it. Guidance comes from the Town Steward and lenses, so zoning is
taught through those.) Resource conservation tests: auto construction consumes real resources; no shared reservations; mines, quarries, lodges, hunting
and fishing cannot duplicate reserves, trees or capacity; processing consumes real inputs; clearing recovers resources once; cancel never creates
resources; save/load never replenishes. Development tests: initial development, expansion on demand, no expansion when capacity suffices, restrictions,
priority changes, approval mode, paused, workforce and material shortages, road disconnection, overlapping catchments, district split/merge/removal,
manual buildings inside districts, save/load mid-development. Scenarios A to I: founding village survives the first winter without hand-placing every
workplace; agricultural expansion only when justified; forestry settlement with sustainable harvest and replanting and no needless lodges; mining town
with downstream industry raising demand; quarrying with real terrain excavation and protected land safe; hunting and fishing reflecting real resources;
resource shortage prioritising survival; advanced control (approval mode, approve one reject one, building cap, pause, existing buildings keep working);
a ten-in-game-year run reviewing housing, spending, food security, forest regeneration, depletion, workforce, district growth, conservation, performance
and appearance without unrestricted overbuilding. Visual testing: launch the real game and use real mouse and keyboard where possible; screenshots of the
catalogue, each zoning type, auto-developed buildings, district inspectors, priorities, queue, shortage warnings, protected land and growth over time. If
the live game cannot be driven, say so plainly and claim nothing was seen.

### Part XIII: Phases and scope limits
1 architecture and audit plus shared district framework and design rules; 2 generalised development engine preserving housing; 3 agricultural and
forestry districts; 4 mining and quarrying districts; 5 hunting and fishing districts; 6 district controls (priorities, modes, restrictions, names,
inspector); 7 land clearing and protection; 8 catalogue and UI; 9 save migration and optimisation; 10 balancing and gameplay verification. Separate,
reviewable PRs; each builds and passes its tests before the next. **Out of scope:** government/multi-settlement work, auto-building strategic buildings,
removing control over roads/walls/layout, free construction, a private economy, land markets or household finance, a separate underground map, infinite
minerals or timber, replacing approved art unnecessarily, a permanent bottom toolbar, removing systems without a migration plan, claiming untested
features are done.

## 1. Audit: what exists today (Phase 1, read from the code)

### Zones and the zone map
- `Zone` enum (`Sim/Defs.cs`): None, Res (Level 1 housing), Res2 (Level 2 housing), Farm, Comm (retired, loads as None), Wood (Lumber), Mine (Mining),
  Pigs/Sheep/Chickens (livestock pens, not painted zones). **Phase 1 adds Quarry, Hunt, Fish** at the end (saved by name, so old saves are unaffected).
- `World.ZoneMap[y,x]` holds one zone per tile; a zone stays under a building built on it. Painting goes through `ZoneCheck` / `SetZone` / `ZoneRect`
  (`World.Rules.cs`): claimed land only, no water/canals/moats/pens, farms need fertile soil that is not too dry, Lumber goes on forest or open gentle
  ground, Mining on rock, ore or open non-steep ground. Zoning is already free. `ClearZoneRect` unzones open tiles and never touches buildings.
- Saved as the `zone` grid (`World.Save.cs`). Old saves with Comm or pen zones load them as None.

### How residential development works today (`World.Develop.cs`) and what the engine reuses
1. `ComputeDemand()`: `Dem[Zone.Res]` from occupancy `(Peasants+Soldiers)/D.Civ` (`(occ-0.7)*4`, clamped), forced negative when starving or food is
   under 10 x `FoodScale`, and held at 0 when food is falling in non-seasonal play.
2. `Develop()` runs once a day: demand under `DemandThreshold` (0.05) stops it with a note; homes short of water stop it with a note.
3. `HomeSite(kind)` scans **every tile** for the best empty zone tile beside a Hall-connected road: score = Manhattan distance to the Hall plus a
   deterministic hash (`Noise.Hash2(x, y, Day+1) * 2.5`), lowest wins. Level 2 costs `House2Cost` (12 wood, 6 stone); Level 1 `Cost["house"]` (10 wood).
4. One home per day, from whichever zone the town can pay for (nearer the Hall wins); a zone short of materials writes a `Note` ("Waiting for 12 wood
   to build"). Payment is **instant**: `ResAmt` is debited and `AddBld` places the finished building the same tick (`Zoned = true`, `Tier` set).
5. Construction is therefore **instant today**: there is no queue, no reservation, no builder labour (except `Pending` builds, which only wait for
   *clearing* and then place instantly, `World.Clearing.cs`).

What the engine reuses and generalises: the daily `Develop()` hook and its deterministic hashed tie-break, the "site + cost + wait note" shape (`Pick`,
`HomeWait`), validation through the same `CanPlace`/`ZoneCheck` the player uses, `AddBld(...).Zoned = true` as the marker for an auto-built building,
and the `Note` dictionary as the origin of "why is it not developing". What it replaces: the per-tile whole-map `HomeSite` scan (replaced by per-district
candidate caches in phase 2) and instant placement (replaced by a minimal construction queue in phase 2).

### Other systems the engine will plug into
| Area | Files | What is there |
|---|---|---|
| Farms | `World.Farms.cs` | Seasonal farming (sowing, harvest, storage), crop fields on Farm zone, `Zoned` farm buildings in town layouts (`World.TownBuilder.cs`). Farms are placed by hand or by the AI town builder; **no organic farmhouse growth yet**. |
| Forestry | `World.Forestry.cs`, `World.Extraction.cs` | Lodge (`wood`): Sustainable / Intensive / Clearing modes, replanting, `SustainFloor`, protected woodland, lodge survey, one crew per tree. Works every claimed tree in `WorkR` (16) unless a Lumber boundary is painted (then only those). Forester optional. |
| Mining | `World.Mining.cs` | Tunnel mine: digs tile by tile; ore per tile (`OreTaken`/`OreLeftMap`), hidden until surveyed; site survey and reserve warnings; auto area ignores other land-use zones; painted Mining tiles are an optional boundary (and the tunnel may only run through them when painted). |
| Quarry | `World.Quarry.cs`, `World.Economy.cs` | Rock within `QuarryAutoR` (8) or a painted Mining boundary; hills dig down into pits (`Dig`, `RockAmtMap`), mountains cut inward; finite reserves per tile, shared automatically. |
| Hunting / fishing | `World.Forecast.cs`, `World.Fishing.cs` | Hunting lodge (`hunter`) and fishing hut (`fisher`) placed by hand; game and fish stock are finite/regrowing; a fisher's yield depends on water class, season and boats. |
| Crews | `World.Crews.cs` (not valid UTF-8: **never edit**) | Work crews per building; target search honours `InWorkArea`. |
| Clearing | `World.Clearing.cs` | Universal clearing orders, `Pending` builds placed after clearing, protected woodland needs confirmation. |
| Workforce | `World.Economy.cs` | `Jobs` per building type, `TradeOf`, priority list, hand-set `TradeTarget`, `Workers`/`Eff` per building. |
| UI | `Hud.Catalogue.cs`, `BuildCatalogue.cs`, `OverlayRegistry.cs`, `Hud.Steward.cs`, `Hud.Charters.cs` | Build catalogue by category, `ToolDef` entries (`zoneMine`, `zoneWood`...), overlays with `ForTool` restore, Town Steward ranked jobs and paths, "why is this stalled" cards, charters. |

Names in the brief that differ from the repo: there is no `World.Ranges.cs`-based extraction logic beyond range helpers, no separate construction
module, and the old tutorial no longer exists.

## 2. Architecture (phase 1, built)

- `Sim/Districts.cs`: `DistrictType`, `DevPriority`, `DevMode`, `DistrictLimits`, `District` (identity, settings, derived tiles/buildings/totals, `Note`).
- `Sim/World.Districts.cs`: derives districts from the zone map, naming, operations, suitability, save/load of identities and settings.
- **Derived, not authoritative:** the zone map stays the single source of truth for *where*; districts add identity and settings on top. A rebuild is lazy:
  `Districts`, `DistrictAt`, etc. rebuild when `Version` changed (`Touch()` is called by every zone and building change; code that writes `ZoneMap`
  directly must call `Touch()`).
- **Identity rules (deterministic):** patches are found by 4-connected flood fill of same-type zone tiles in scan order (y, then x). Both housing levels are
  one Residential type. Each old district goes to the patch holding most of it (ties: earlier patch); a patch keeps the identity of the biggest old
  district that chose it (ties: lower id). *Expand* keeps identity. *Merge*: the biggest district's id, name and settings win; the others end and the
  survivor's history records it. *Split*: the biggest piece keeps the id; every other piece is a new district with a **copy** of priority, mode and limits
  and an automatic name. *Remove*: an empty district disappears. A player's custom name travels with the id.
- **Names:** `"{Direction} Forestry District"`, `"{Direction} Farmland"`, `"{Direction} Iron District"`, `"{Direction} Hunting Grounds"`, `"{Direction} Fishing
  Shore"`, `"{Direction} Residential District"`; direction is the district centre relative to the Town Hall (North is -y, East is +x on the tile grid;
  within 10 tiles is "Central"). Riverside if a quarter of tiles are within 2 of water; Quarrying is "Mountain Quarry" / "Hill Quarry" by terrain. Clashes get
  "II", "III". Renaming is up to 40 letters; an empty name returns to automatic.
- **Settings:** priority (Disabled/Low/Normal/High/Critical), mode (Automatic/Approval required/Paused), limits (max buildings, employment, spend, minimum
  wood and stone reserve, production target; 0 = none). `DevelopmentOn` = not Disabled and not Paused. These only gate *new* projects.
- **Governor-ready operations** (all `World` methods returning data): `ZoneDistrict` (create/expand), `Suitability`, `SetDistrictPriority/Mode/Limits`,
  `RenameDistrict`, `RemoveDistrictZone`, `DistrictAt/ById/DistrictsOf`, `LegacyBuildings`. Later phases add project approval, budgets, shortage and
  bottleneck reports and extraction intensity.
- **Save:** `districts: { next, list:[{id,type,name,custom,prio,mode,founded,limits,history,runs}] }`, tiles as `[y, x, length]` runs. Additive; old saves have
  no field and get districts derived from their zones (ids 1..n in scan order).

## 3. Engine design for later phases (planned)

One engine, seven districts. Per district kind a small `DistrictRules` object supplies: `Need(district)` (demand signal with a reason string),
`Candidates(district)` (cached sites, rebuilt per district only when its chunk neighbourhood changed), `Capacity(district)` (existing and pending
buildings, vacant jobs), `Validate(site)` (the same `CanPlace`/survey the player's placement uses), `Cost`, `Expected benefit`. The engine core is shared:
demand and score, a **construction queue** (project: district, building, site, cost, reason, expected benefit, status, missing requirements, progress),
shared material **reservations**, the construction reserve, approval flow, and the explanation text. Score terms are integers with a fixed order and
`Noise.Hash2` tie-breaks, so results are reproducible. Evaluation is scheduled per district (weekly, with a cooldown after each project), never a
whole-map scan per day. Initial development gives a new suitable district its first basic building as soon as it is affordable and staffable, ahead of the
demand-driven expansion rules. Construction becomes a minimal queue: a project reserves its materials, waits for builders (labour from the idle pool, capped
so food and critical services keep their workers), and completes into the existing `AddBld` path.

## 4. Compatibility rules (the default, simple)

1. One zone per tile; a tile keeps its zone under a building. Zones never reserve underground resources: a Mining district claims only the **entrance and
   surface footprint** it builds; the tunnel runs underground through tiles that other zones may still hold on the surface (as the tunnel mine already does
   by ignoring other land-use zones above ground that is not a building).
2. Footprints are exclusive: no two buildings share a tile; quarry **pits are solid** (`PitDepth`, `IsSolid`) and block housing, farms and zones for good.
3. Housing, farmland and the other surface zones are mutually exclusive with each other on a tile (one zone per tile) but never override each other
   silently: painting over another district's open tile changes that tile (and may split the old district), painting over a building is refused.
4. Renewable catchments (forest trees, game, fish) and finite stores (stone, ore) are **global**: districts and buildings never duplicate them. Two
   districts over one forest or one rock share the same per-tile reserves and the same per-tree reservation.
5. Temporary work areas (a crew's reach, a clearing order, a quarry's current face) are not zones; they live in the buildings and orders and never create
   a district.
6. Protected woodland and strategic terrain are hard restrictions: no district builds on, fells or clears them without explicit confirmation.
7. A quarry may only dig inside its permitted area; infrastructure (roads, buildings, walls) is never dug away.

## 5. Legacy behaviour and migration decisions (every decision recorded)

| Existing thing | Decision |
|---|---|
| Zone map (Res, Res2, Farm, Wood, Mine) | Kept as is. Districts are derived from it on load; a pre-district save gets ids 1..n in scan order, all Normal / Automatic. |
| Zone.Mine tiles that mark **rock** (the old quarry boundary) | **Phase 1 changes nothing** (they stay Mining districts and the quarry code keeps reading `Zone.Mine`). **Phase 4** converts Mine-zoned rock tiles that a quarry works into Quarrying zone, with a test; until then a Mining district over rock is documented as the quarry boundary. |
| Lumber (Wood) zones | Become Forestry districts. A painted Lumber zone keeps its old meaning (felling order, no floor, no replanting) until phase 3 decides what the district's mode defaults to; saved lodges keep their `Mode`. Never replant what was cut, never refill. |
| Manual buildings inside districts | Count toward capacity, never demolished, never duplicated. |
| Manual buildings outside every district of their kind | **Legacy behaviour:** they work exactly as before (same areas, same rules); the engine ignores them for capacity and never demolishes them. `LegacyBuildings()` lists them. Zoning the ground around one adopts it. |
| AI settlement layouts (`TownBuilder`) | Unchanged; they use manual placement and `Zoned = true`. |
| Geological reserves, `OreTaken`, `Dig`, pits, `Chop`, `SapT`, `StumpT`, protected woodland, clear orders | Untouched. Never reset or refilled. Phase 9 adds tests that load, save and compare them. |
| `Pending` builds (clearing then placing) | Kept; phase 2's construction queue absorbs them without duplicating. |

## 6. Phase table

| Phase | Content | Status |
|---|---|---|
| 1 | Audit, this document, district framework (types, identity, names, settings, suitability, save) | **built** (PR for this phase) |
| 2 | Generalised engine core: development queue, reservations, minimal construction, approval flow, reserve; residential preserved on it | **built** (see section 8) |
| 3 | Agricultural and forestry districts | planned |
| 4 | Mining and quarrying districts | planned |
| 5 | Hunting and fishing districts | planned |
| 6 | District controls and the inspector | planned |
| 7 | Land clearing and protection | planned |
| 8 | Catalogue, overlays, demand indicators, feedback | planned |
| 9 | Save migration and optimisation | planned |
| 10 | Balance and gameplay verification | planned |

## 7. Phase 1 report

See the pull request body. Tests: `DistrictTests` (framework), plus the regression classes named in the PR. Not seen in the live game: nothing in phase 1 is
player-visible yet except that the three new zone kinds exist in the simulation and have colours; no catalogue entry exposes them until phase 8.

## 8. Phase 2 report: the shared engine core (built)

**What is built** (`Sim/Projects.cs`, `Sim/World.Projects.cs`, `World.Develop.cs`):
- **Development project** (`DevProject`): district, building, site, cost, reason, expected benefit, status (Proposed, Waiting, Building, Done, Rejected, Cancelled), missing requirements, build days and progress, player rank, pause.
- **Reservations by debit:** funding a project removes its materials from the store and holds them inside the project, so two projects can never hold the same materials. Cancel gives back only the unused share (proportional to work left, whole units, rounded down); nothing held means nothing returned; a second cancel returns nothing. Done projects hold nothing.
- **Minimal construction:** a project with build days needs one idle villager (`D.Idle`) per day, so building never takes workers from jobs; with none free it says "Waiting for a free builder." Housing has 0 build days (a home still appears the day it is funded, so the first-year behaviour is unchanged). Later kinds set their own days.
- **Gates (hard settings win):** `DevGate` (disabled, paused, an open project in the district, building limit, settlement cap of 3 open projects) and `ReserveBlock` (district minimum wood/stone and the settlement's construction reserve, default 0 so no setup is needed). Every refusal is written into `District.Note` in plain words.
- **Approval flow:** an Approval-required district proposes (holding nothing), one proposal at a time; approve funds and builds it, reject closes it and blocks that tile for 30 days. Queue controls: approve, reject, pause, cancel, rank (reprioritise); `DevQueue()` orders by rank, district priority, age.
- **Housing on the engine:** demand and costs are the original rules. The site search now walks only residential districts' tiles (no whole-map scan); a higher-priority district wins over a nearer one, with equal priority the nearest to the Hall wins (ties: lower tile). A settlement that never touches district settings behaves as before.
- **Save:** additive `projects` field (queue, held materials, rejections, reserve); old saves get an empty queue; loading never duplicates projects or refills the store.

**Not built yet:** non-housing demand and sites (phases 3 to 5), scoring beyond housing, the inspector and queue UI (phases 6 and 8), construction-site clearing (phase 7), scheduled/cached per-district evaluation beyond the district-tile search (phase 9).
