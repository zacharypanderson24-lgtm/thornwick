# Thornwick / Crown & Hearth: Water Generation and Hydrology System

**Status (updated with every water PR):**

| Phase | State |
|---|---|
| 1 Water generation (elevation, sea level, drainage, rivers, lakes, ponds, seas, classes, depth, connected waters, determinism, saving) | **Built in PR 1** (Unity only, new worlds created with a water preset; old worlds and saves stay dry). Not yet drawn in the game. |
| 2 Terrain integration (rendering, shorelines, four camera views, start placement, building and movement restrictions) | **Built in PR 2**: water is drawn with the existing art, banks and marsh are tinted (placeholder), all four camera rotations keep currents correct, nothing can be built on water, nobody can walk through it, and Menu > New world picks a preset and seed. Shoreline transition art, biome-specific banks and wetland slowdown are still to do (see below). |
| 3 Bridges, tile inspection, basic fishing | **Built in PR 3**: wooden bridges, water tile inspection, fishing huts (first step). No boats, docks or fish stocks yet. |
| 4 Soil moisture, flooding, docks | Later, only when asked. A first fertility rule from fresh water is built (see below). |
| 5 Irrigation, seasons, ships, naval warfare, moats | Documented and prepared only |

Everything below the line "The specification" is Zach's text, unchanged. Above it: what existed, and how Phase 1 is built.

---

## What existed before this work (audit, written before any code changed)

**Terrain.** `World.Hv` holds vertex heights, (N+1)^2 integers, levels -8..16, 1 level = 7 px. N = 256 (city map), the Town Hall's tile is `Start` = 128. `World.GenRangeHeights(seed, relief)` makes rolling ground (mostly level 0, hills to 3, basins to -2) plus mountain ranges and massifs; the browser-compatible `Classic` style keeps the old generator for parity tests. `TileLevel`, `TileSpread`, `IsSolid` (mountain, pit, ore, spread >= 3, tunnel) and `WalkCost` (World.Crews.cs, not valid UTF-8: do not edit; it asks `IsSolid`) are the movement rules. Basins below level 0 were always dry. Nothing in the old code is surface water.

**Fertility** is `World.Fert[y,x]` (bool), made in `GenTile` from noise plus a patch round the start. **Biomes** are visual: `Game/BiomeArt.cs` + `GroundRenderer.cs` choose ground art per world; the 600 x 600 km world map (`WorldMap.cs`, 576 regions) has biomes and heat but no rivers or coasts.

**Groundwater** (`World.Water.cs`, `WaterTests.cs`) is the drinking-water system: aquifers, wells, cisterns, road delivery. It is independent of surface water and unchanged.

**Placement and pathing.** `World.Rules.cs` `CanPlace`/`ZoneCheck` ask `IsSolid`/`HasTree`; crews, raiders, walls and the town builder ask `WalkCost`/`IsSolid`. Hooking surface water into `IsSolid` (and so into `WalkCost`) later reaches all of them without touching World.Crews.cs.

**Saving and expansion.** `World.Save.cs` stores `hv` and the tile grids as built, plus seed/style/relief (deposits and aquifers regenerate from the seed). "Map expansion" is land claims (`World.Claims.cs`): the 256 x 256 map exists from day one and you own a 64 x 64 district to start, so claiming never moves terrain.

**Existing water art** (made earlier by Codex, merged in `1be496a`, "approved building, defense, biome and animated water art"): `assets/biomes/water/` and `unity/Assets/Resources/Art/Water/` hold 128 transparent 32 x 16 ground-tile frames, 8 frames each for 16 sets: still shallow / still deep (2 variants each), ocean coastal / ocean deep (2 variants), river flow east / south / west / north (2 variants each, deep-ish), plus `water.json`. `Game/WaterArt.cs` is the playback helper: `WaterArt.Frame(kind, seconds, shallow, worldDirection, cameraRotation, variant)`; river direction order is east/+X, south/+Y, west/-X, north/-Y, camera-relative already. Nothing places it: `docs/APPROVED_ART_HANDOFF.md` says "water placement is not wired to local terrain yet". Open PRs #41 (terrain and geology redraw) and #46 (terrain and battle art) contain no water, river, shore or coast files.

**Not covered by existing art (needed in PR 2 or later):** shoreline/bank transition tiles per biome (grass bank, sand, rock, mud, reeds), river-mouth and junction pieces, wetland ground, bridge sprites, shallow river variants and a ford. PR 2 will list exact file names, sizes and directions for these and use consistent code-drawn placeholders until they exist.

## What Phase 1 builds

New files: `Sim/SurfaceWater.cs` (data model, saving), `Sim/Hydrology.cs` (generator), `Sim/World.Hydrology.cs` (hook into worlds), `Tests/EditMode/HydrologyTests.cs`. Small edits: `World.cs` (constructor takes a `WaterPreset`, default `None`; no water on trees/fertility; deposits follow the dry terrain), `World.Save.cs` (a `"water"` entry, absent in older saves).

**Old worlds stay as they are.** A world has water only if it is created with a preset other than `None`. The default constructor, every existing save and the browser-compatible Classic terrain are dry and byte-identical to before. There is no menu option yet; PR 2 adds the "new world" water choice together with the drawing.

**Data (per tile, flat arrays, no objects):** class (None, Stream, SmallRiver, MajorRiver, Pond, Lake, Ocean, Sea, Estuary, Wetland), depth (Shallow, Medium, Deep), flow direction (8 directions in world space, 0 = none), flow strength 0..255, salinity 0..255, river width in tiles, surface level (same scale as `Hv`), flags (Shore, Bank, Saturated, Navigable, Standing, Delta). Derived and rebuilt on load: body id (each standing body and each river system), network id (all connected water) and navigable-region id (boats cannot travel between regions). Seasonal variation is not stored yet: the arrays are static and seasonal changes will be scheduled sim updates that rewrite them (spec section 23).

**Generation order (as in spec section 5):** (1) elevation from the existing terrain, with a gentle regional tilt and low-frequency noise added to the *drainage* level only (so flat plains still drain and rivers meander) and a slight rise under the town so water goes round it; (2) sea level and coastline shaping (coast side, island radius, archipelago blobs, warped by coastline complexity: bays, capes, peninsulas, straits; the sea floor is cut into the terrain and mountains are cut off at the coast); (3) the sea is every edge-connected tile below sea level; (4-5) priority-flood fills inland depressions and gives every tile a downhill neighbour (rivers cannot end in dry land: every path leads to the sea, a lake's spill point or the map edge); (6) rain (with more over high ground) is accumulated downstream; (7) rivers start where the drainage area reaches a threshold, are drawn along a smoothly warped line as a disc as wide as the river, and merge where they meet; width = (drainage area / threshold)^0.42 x river size, giving 1 tile streams up to about 16 tile major rivers on a whole-map catchment; (8) lakes and ponds are the deepest, biggest filled depressions (count follows lake frequency and map area), standing at one flat level never above their lowest bank; (9) rivers widen into estuaries near the sea (brackish gradient) and wide mouths on low land can fan out into 2 to 3 delta channels; (10) water tiles are made flat, banks are eased to within one level; wet ground is marked along slow low fresh shores; (11) a first fertility rule (fresh water within 3 tiles on low gentle ground is fertile, marsh and salt shores are not); (12) depth: shallow shores and fords, medium ordinary water, deep channels and open sea.

**Start placement (section 16):** a start is valid when the Town Hall ground (12 tiles round it, 14 south) has no water or marsh, at least 3000 walkable tiles are reachable from it and no pocket of the first district is cut off, and fresh water is within 44 tiles (60 on islands). Generation tries up to 8 salted attempts per seed and keeps the first valid one; the choice is deterministic.

**Presets (section 15), parameters in `HydroParams`:** water coverage, sea level, river frequency, river size, lake frequency, lake size, coastline complexity, island frequency, rainfall, wetness, drainage tilt, valley depth, delta chance and a suggested relief (mountain coverage). Balanced inland, River valley, Lakeside, Coastal, Island, Archipelago, Wetlands, Arid oasis, Mountain watershed. All are functions of the world seed only.

**Continuity (sections 3 and 26), honestly:** the generator works in map tiles with the map's own coordinates, and the whole 256 x 256 map is generated at once, so claiming land never changes water and a river crossing a claim boundary is one body. It does **not** yet continue across the map's own edge into a neighbouring map or the regional map: rivers leaving the edge simply leave. Making that true needs the regional elevation model of the world map to feed the drainage (inflow at the edges and the sea level from the region), which does not exist yet; the generator already takes the heights as an input and is deterministic, so this is a matter of supplying a larger height field, not of redoing the algorithm.

**Performance:** about 0.25 s per attempt at 256 x 256 (no per-frame work; everything is generated once and saved as a compressed array). 512 x 512 is covered by a test with synthetic terrain.

**Saves:** a world with water adds one string (`"water"`, base64 of the gzipped arrays, about 100 KB at 256 x 256). Loading uses the saved arrays and never regenerates, so a later change to the generator cannot move rivers in an existing save. No existing save is incompatible.

**Phase 2 (PR 2) in short:** `IsSolid` is true on open water, so every walker, crew, raider and builder treats it as a wall (World.Crews.cs is untouched: it asks `IsSolid`); `WaterBlock` gives the player-facing message ("Cannot construct here: river water / lake water / sea water", marsh blocks buildings and zones but not roads); roads need bridges (part 3). `Game/WaterRenderer.cs` draws all water tiles as one mesh; 16 art sets (still shallow/deep, ocean coastal/deep, river east/south/west/north, two variants) are packed into 16 small atlases and the mesh swaps atlas at 8 ticks a second (no per-tile objects). River art is chosen from the flow direction in world space plus the camera rotation, using the commonest direction among neighbouring river tiles so wide rivers do not flip between two patterns. Banks are tinted by the ground renderer (sand beside the sea or in dry biomes, pale rock in mountain biomes, mud otherwise; marsh is dark green). Screenshots from the real renderers: Editor menu Crown & Hearth > Water Screenshots.

**Art still needed (placeholders used now):** shoreline transition tiles (grass bank, sand, rock, mud, reeds; 32x16 ground diamonds with alpha, one set per biome group), river mouth and junction pieces, a shallow-river variant (rivers currently use the one flowing art at every depth), wetland ground tiles with reeds, bridge sprites (part 3).

**Phase 3 (PR 3) in short:**
- **Bridges** (Water menu, Wooden Bridge): drag in a straight line from one bank across to the far bank. Rules: dry land at both ends, one stretch of water of at most 8 tiles, no deep water, not the sea; 5 wood per water tile plus 1 per new land tile. A bridge is a road laid on water tiles (a road building standing on a water tile), so road connection, walking, crews, raiders and saving work with no new save field; `IsSolid` is true on water except under a road. Bulldozing a bridge tile removes the road and the water is a wall again. The deck is drawn by the water renderer (code-drawn boards across the span; no bridge art exists yet).
- **Inspection:** hover or click a water tile: class, water type (fresh, brackish, salt), depth, flow direction and current, width, surface level, size of the water body, land movement (blocked, or open over a bridge), fishing suitability, navigability by depth (there are no boats yet, and it says so). Marsh and bank tiles say what they are. Fishing huts show what they can fish.
- **Fishing:** Fishing Hut (Water menu, 6 wood, 2 jobs, new trade "Fishing" in the Workers panel). It must stand within 2 tiles of water. It fishes the water within 6 tiles whose bank its fishermen can walk to; catch per fisherman per day: stream 0.25, pond 0.4, river 0.6 (major 0.8), lake 0.8, estuary 0.9, sea 1.0, scaled down when fewer than 10 water tiles are in reach. Fish joins the food stock as fresh meat, so it spoils and a smokehouse preserves it. Not built: seasons, fish stocks that run down, boats and docks, fishermen drawn walking to the shore.
- Art still needed: a proper fishing hut (placeholder is the small shop with a drying rack, code-drawn), bridge sprites and railings (placeholder deck), fishing boats.

**Known gaps in PR 1 (all planned):** nothing draws the water yet; buildings and walkers still ignore it (`IsSolid`/`WalkCost` hooks are PR 2); no new-world menu entry; river deltas are simple distributaries; wetlands are saturated land, not yet slowing movement; groundwater (wells) ignores surface water.

---

## The specification (Zach's words, unchanged)

# Thornwick / Crown & Hearth — Water Generation and Hydrology System

## 1. Objective
Begin incorporating fully functional bodies of water into the Unity version of Thornwick. The initial implementation must support: rivers, streams, lakes, ponds, oceans, seas, coastlines, bays, islands, river mouths, river deltas, estuaries. Water should be a fundamental geographical feature that influences how settlements develop, rather than simply a decorative terrain type. The system must establish the foundation for future agriculture, fishing, irrigation, water transportation, bridges, maritime trade, naval warfare and defensive engineering. Do not implement all future water-related gameplay immediately. Prioritize realistic terrain generation, visual quality and physical water interactions.

## 2. Repository assessment
Before implementation: read CHATGPT_START_HERE.md and relevant docs; inspect the existing Unity terrain generation architecture, elevation, moisture, fertility and biome generation, all existing water textures and texture definitions, tile data structures, building placement restrictions, pathfinding and road construction, camera rotation and terrain rendering, map expansion functionality, save and load systems. Water textures for still water, ocean water and flowing rivers have previously been developed: locate and evaluate these assets before creating replacements. Prioritize the Unity version; do not modify the legacy browser version. Preserve all existing unrelated gameplay.

## 3. World and settlement scale
Design the water architecture to support the planned map hierarchy: overall world 2,400 x 2,400 km; regional map about 48 x 48 km; starting settlement 64 x 64 tiles; expanded settlement up to 512 x 512 tiles; one settlement tile = 8 x 8 meters. The immediate implementation may operate only at settlement scale, but rivers and coastlines must eventually remain geographically consistent across settlement boundaries, regional maps and the world map. Do not generate water features independently inside each settlement in a way that prevents future regional continuity.

## 4. Water classification
Configurable classifications: Stream (narrow flowing freshwater); Small river (limited navigability); Major river (broad, transportation potential); Pond (small standing freshwater); Lake (larger standing freshwater); Ocean (extensive saltwater); Sea (large regional saltwater); Estuary (transitional fresh/salt); Wetland (shallow water and saturated terrain). Each water tile should support: classification, depth, flow direction, flow strength, salinity, surface elevation, shoreline status, navigability, seasonal variation, associated water body ID. Use lightweight tile data; no GameObject per water tile. Not every attribute needs gameplay immediately but the data architecture should support future expansion.

## 5. Terrain elevation and hydrology
Water generation depends on the physical terrain; use or improve the existing elevation map. Order: 1 generate elevation; 2 determine sea level; 3 identify ocean-connected low areas; 4 identify inland depressions; 5 determine drainage directions; 6 calculate water accumulation; 7 generate river networks; 8 generate lakes and ponds; 9 connect waterways where geographically appropriate; 10 refine coastlines and riverbanks; 11 update soil moisture and fertility; 12 assign final terrain and biome classifications; 13 place vegetation and other environmental features. No randomly placed blue tiles ignoring the terrain; water must look geographically coherent.

## 6. River generation
Rivers originate at higher elevations and flow to lower: mountains, hills, high-rainfall regions, lakes, snowmelt regions, upstream waterways outside the settlement boundary. Use elevation-based drainage and flow accumulation. Rivers flow downhill, form natural curves, develop tributaries, merge into larger waterways, widen with accumulation, and end in lakes, oceans, inland basins or downstream map boundaries. No rivers randomly ending in dry terrain (exceptions: plausible evaporation, infiltration, seasonal waterways, closed basins). Avoid overly straight rivers and repetitive staircase patterns; curves natural at close and distant zoom.

## 7. River widths
Variable-width rivers. Settlement-scale targets: small stream 1 tile (8 m); creek 1-2 (8-16 m); small river 2-4 (16-32 m); medium river 4-8 (32-64 m); large river 8-16 (64-128 m); major river 16-40+ (128-320+ m). Proposed gameplay targets. Width comes from drainage accumulation, regional geography and water availability, not random assignment. Large rivers uncommon on small starting maps but strategically significant. Widths stay visually coherent across bends and junctions.

## 8. River junctions and mouths
Rivers merge naturally: combine flow accumulation, widen downstream, coherent rendering, no disconnected water tiles, no artificial square junctions. Where rivers enter oceans: plausible mouths, estuaries, widening near the coast, eventually branching deltas (valuable for agriculture and transport). Not every mouth is a delta.

## 9. Lakes and ponds
Lakes form in plausible depressions: small woodland ponds, inland freshwater lakes, mountain lakes, large regional lakes, connected lake and river systems, closed-basin lakes. Lakes may receive upstream rivers, drain into downstream rivers, stay isolated, or connect to several waterways. Avoid perfect circles or rectangles, repetitive shapes, single isolated water tiles without justification, water that visibly climbs uphill. Standing water has one consistent surface elevation per connected body. Shorelines reflect the surrounding terrain.

## 10. Ocean and sea generation
Oceans and seas are extensive connected saltwater bodies, generated through sea-level and connectivity calculations. Support open coastline, coastal plains, rocky coastlines, peninsulas, islands, archipelagos, bays, natural harbors, straits, inland seas. Coastlines are not rectangular strips along map edges. A coastal settlement has meaningful variation including potential harbors and defensive features. Some maps contain much ocean, others are landlocked; do not force ocean access onto every settlement.

## 11. Shorelines and transitional terrain
Water transitions naturally into land, with shoreline variation per biome: temperate grassland grassy or muddy banks; forest vegetated banks and exposed roots; desert sand and sparse vegetation; Mediterranean rocky or sandy; mountain rocky and steep; wetland reeds and saturated ground; taiga mossy banks and conifers; tropical sand, dense vegetation, wet soil. Create or adapt shoreline transition textures where necessary, respond to neighbouring water and land tiles, avoid harsh transitions, do not use the same shoreline texture everywhere.

## 12. Water depth
At least three depth categories, separate from classification: shallow (shores, banks, crossings; future fishing and fords; specialised construction), medium (ordinary rivers, lakes, coastal waters; small boats and barges), deep (major waterways and offshore ocean; large vessels). Depth generally increases away from shore, though major channels and steep coasts may differ. A lake can contain deep water and an ocean shallow coastal water.

## 13. Water rendering
Use the existing pixel-art style, consistent with terrain, buildings, vegetation. Distinct treatment: still freshwater (lakes, ponds: subtle surface variation, minimal directional movement); flowing freshwater (rivers, streams: visible directional movement consistent with the river direction); ocean (distinct coastal and deep appearance, subtle waves, shoreline variation, shallow-to-deep colour transition). Animations subtle and coherent. Rivers never flow in arbitrary directions. No realistic water shaders that clash with the pixel art. Reuse existing textures.

## 14. Isometric rotation
Four-directional camera rotation must keep water correct: river flow direction consistent in world space, shoreline orientation updates, water tiles connect in every view, bridges stay aligned, flow never appears to reverse because the camera rotates, depth sorting correct. Follow existing isometric projection.

## 15. Terrain generation presets
Configurable parameters: water coverage, sea level, river frequency, average river size, lake frequency, average lake size, coastline complexity, island frequency, mountain coverage, rainfall. Presets: Balanced inland (several freshwater sources, little or no ocean); River valley (major river, tributaries, fertile land); Lakeside (big freshwater lake, connected waterways); Coastal (ocean access, coastal terrain, potential harbors); Island (settlement on an island, maritime surroundings); Archipelago (several islands, future water transport); Wetlands (shallow waterways, saturated land, lakes, ponds, complex networks); Arid oasis (limited water around specific features); Mountain watershed (high elevations, narrow valleys, streams, mountain lakes). Deterministic from a seed; presets must not produce identical terrain every time.

## 16. Starting settlement placement
Default start locations: enough buildable land; access to freshwater or another viable source; suitable agricultural terrain; nearby resources; space to expand. Never place the Town Hall underwater, on isolated inaccessible land, inside narrow river channels, or without enough initial settlement space. Do not make every start ideal; players should eventually be able to pick harder locations. A default beginner map gives practical freshwater access without needing advanced infrastructure.

## 17. Building placement restrictions
Ordinary buildings cannot be built on water. Placement distinguishes dry buildable land, saturated or restricted terrain, shallow, medium and deep water. Future specialised water buildings (fishing docks, bridges, piers, ports, shipyards, watermills, wells, irrigation, coastal fortifications): rules extensible, not hardcoded one by one. Clear feedback, e.g. "Cannot construct here: water tile."

## 18. Movement and pathfinding
Water is a real barrier for land units: civilians, workers, soldiers, animals, caravans, siege equipment. Initially crossings need explicitly supported infrastructure. Future categories: land, shallow-water crossing, river navigation, coastal navigation, ocean navigation. Reuse the existing pathfinding; no separate incompatible navigation system. Inaccessible resources must not count as reachable: a woodcutter must not collect timber across an uncrossable river.

## 19. Bridges and crossings
Implement basic wooden bridges once generation and movement restrictions are stable: connect valid land endpoints, cross compatible water tiles, connect to the road network, restore land movement, cost resources, respect orientation and span limits, never end in inaccessible water, work across different river widths. Future crossings: ford, wooden footbridge, wooden road bridge, stone bridge, drawbridge, ferry. Only a reliable basic bridge is needed now; keep definitions extensible.

## 20. Water and soil fertility
Introduce a freshwater proximity or soil-moisture calculation. Land near freshwater may gain moisture, seasonal flooding, sediment, irrigation potential; too much moisture can hurt some crops and construction. Saltwater gives no ordinary freshwater irrigation benefit. No universal fertility bonus around every water tile: consider salinity, local soil, elevation, distance from freshwater, biome, flood potential. A fertile river valley supports a stronger farm economy than dry uplands.

## 21. Fishing
First major economic use of water, once the water system works. Buildings: fishing hut, riverside fishing station, fishing dock, commercial fishing port. Productivity depends on water type, accessible shoreline, fish availability, workers, equipment, season. Pond < lake < ocean coast. Fish enter the ordinary food inventory and consumption system; preserved fish integrates with preservation mechanics; no separate food reserve villagers cannot reach.

## 22. Future irrigation and freshwater systems
Prepare the architecture for wells, water collection, reservoirs, irrigation canals, aqueducts, watermills, agricultural irrigation, livestock water, settlement freshwater consumption (carrying capacity). Do not build an overcomplicated drinking-water simulation first; prioritise stable terrain generation and meaningful economic interactions.

## 23. Future seasonal water behavior
Support later: seasonal flooding, drought, reduced stream flow, lake level changes, frozen lakes and rivers, snowmelt, seasonal wetlands, coastal storms and surges, emerging from the climate and season systems. For now store the data and avoid expensive continuous water physics; changes happen on scheduled simulation updates.

## 24. Military and defensive implications
Rivers, lakes and oceans are natural defensive barriers; players eventually build around river crossings, peninsulas, islands, mountain lakes, narrow coastal entrances, river bends, strategic waterways. Future: moats, controlled bridges, fortified crossings, harbor chains, coastal watchtowers, sea walls, fortified harbors, coastal artillery. Military movement respects the same water barriers as civilians; siege equipment has extra crossing restrictions. Do not implement naval or siege systems now; make sure the water architecture allows them (see DESIGN_MILITARY.md).

## 25. Future naval gameplay
Water connectivity and depth must support ships: fishing boat (rivers, lakes, coast); river barge (navigable freshwater); raiding longship (rivers and coast); merchant cog (coastal and maritime); war galley; military transport; carrack, galleon, frigate (deep maritime waters). Eventually draft, navigable restrictions, cargo, crew, home ports, maintenance, orders, supplies. Water bodies have connected navigable regions: no travelling between disconnected lakes or across land. Now: establish the connectivity, not the vessels.

## 26. Map expansion and continuity
Water stays continuous as settlement maps expand: a river leaving the east edge of a 64 x 64 start continues when you expand east (no vanishing, no unreasonable direction or width change, no turning into a lake or land). Use deterministic generation with stable world-space coordinates. Where practical generate hydrology from a larger underlying regional elevation model, not by recalculating each newly revealed chunk, especially for rivers whose upstream drainage lies beyond the visible boundary.

## 27. Water-related user interface
Water tile inspection, e.g. "Major River. Water type: Freshwater. Depth: Medium. Flow direction: Southeast. Current: Moderate. Land movement: Blocked. Fishing suitability: High. Navigability: Suitable for small vessels." Only show values the simulation really supports; no fake statistics. Later overlays: depth, freshwater access, soil moisture, flood risk, navigable waterways, fishing suitability. First priority: basic inspection and movement restrictions.

## 28. Art and asset requirements
Follow the texture specifications. Check whether existing art covers: still water, flowing river water, shallow and deep freshwater, shallow and deep ocean water, riverbanks, sandy, rocky and muddy shorelines, wetland transitions, river junctions, river mouths, bridges. Reuse where suitable, do not replace unrelated textures. For extra textures document exact filenames, dimensions, directional variants and usage. Consistent temporary placeholders only where necessary.

## 29. Performance requirements
Support settlement maps up to 512 x 512: lightweight tile data, chunk-based terrain rendering where supported, no GameObject per water tile, no continuous fluid physics, no per-frame hydrology, cache static drainage and connectivity, update dynamic water on scheduled sim ticks, keep camera-based rendering optimisation, animated water without excessive draw calls. Benchmark water-heavy maps (islands, lakes, major rivers); no substantial slowdown versus ordinary terrain.

## 30. Save compatibility
Persist: terrain seed, water classifications, water body ids, relevant elevation, depth, flow direction, player-built bridges, future water infrastructure, future dynamic changes. Existing settlements stay loadable where practical; never silently regenerate existing terrain in a way that destroys buildings or changes established geography. Document any unavoidable save incompatibility.

## 31. Implementation phases
Phase 1 Water generation: elevation-based generation, streams and rivers, lakes and ponds, oceans and coastlines, classifications, depth, connected waterways, determinism, basic water rendering. Phase 2 Terrain integration: shoreline transitions, flowing water visuals, four-direction camera compatibility, biome integration, start placement, building placement restrictions, land movement restrictions. Phase 3 Basic water infrastructure: wooden bridges, road connectivity across bridges, worker and military pathfinding integration, water tile inspection, basic fishing. Phase 4 Economic integration: freshwater proximity, soil moisture, agricultural suitability, expanded fishing, early docks, water transport foundations. Phase 5 Future expansion (document and prepare): irrigation, flooding, seasonal water changes, regional river navigation, maritime trade, ports, shipbuilding, naval warfare, moats, coastal fortifications. Prioritise a polished, functional Phase 1 and 2 before later systems.

## 32. Testing requirements
Automated tests in the existing Unity test setup. Verify: rivers generally flow downhill; valid downstream connections or plausible terminal basins; tributaries merge correctly; widths consistent with the rules; lakes in plausible depressions; connected standing water has one surface elevation; ocean water is connected; determinism; different seeds give different layouts; ordinary buildings cannot go underwater; land units cannot cross unsupported water; bridges restore connectivity; starts spawn on valid land; map expansion preserves existing waterways; camera rotation preserves orientation; save and load preserve geography; no significant performance regression. Test at least 64, 128, 256 and (where supported) 512 square maps, and several seeds for every preset.

## 33. Visual acceptance criteria
Generate and inspect representative maps: inland temperate river valley; woodland settlement beside a lake; coastal settlement with a natural bay; mountain settlement with narrow streams; island settlement; river delta; desert oasis; wetland region. Water must look natural and integrated; inspect close zoom, distant zoom and all four camera orientations. Do not accept obviously artificial waterways merely because the algorithm compiles. Visual quality of the geography is a core part of the task.

## 34. Final deliverables
Compile; run relevant tests; fix errors and failures; open generated maps in the playable Unity scene and inspect water rendering and terrain interactions; capture representative screenshots; document generation parameters, new data structures, missing texture requirements, performance results, remaining water-related gameplay; update project docs. Do not claim anything is implemented unless it works in the playable Unity build.

## 35. Final design principle
Water should fundamentally influence where players settle and how settlements develop. A fertile river valley, a fortified peninsula, a mountain lake, an isolated island and a desert oasis should each offer different opportunities and limits. Rivers become transport routes and natural defensive boundaries; lakes support fishing, agriculture and regional development; oceans enable maritime trade, naval power and distinct civilisations. Build the geographic system now so those mechanics can emerge as Thornwick develops. Begin with an audit of the current terrain system, then implement and verify the foundational water generation and terrain integration phases.
