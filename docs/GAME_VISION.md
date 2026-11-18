# Crown & Hearth — Complete Game Vision and Design Reference

**Status:** Long-term game design vision, not an implementation checklist or a claim of completed features.  
**Source:** *Crown & Hearth — Complete Game Vision (10 Pages)*, November 16, 2026, together with approved design decisions in the project conversation.  
**Owner's direction:** A geographically believable, fictional **broader Old World**, not a single European culture or a mandatory mix of all climates in each campaign.  
**Read alongside:** [DEVELOPMENT_PROGRESS.md](../DEVELOPMENT_PROGRESS.md), [unity/README.md](../unity/README.md), [CLAUDE.md](../CLAUDE.md), [ART_DIRECTION.md](ART_DIRECTION.md), and the existing texture and terrain specifications.

> **Instructions for Claude Code and other contributors**
>
> This document explains what we ultimately want to build and WHY. It does **not** authorize implementing all these systems in the current task. Preserve the playable Unity port, follow the current milestone, prefer small feature branches and verified tests, and request approval before major scope or architectural changes. Do not treat examples, tentative counts, or balancing targets as locked gameplay constants. Update DEVELOPMENT_PROGRESS.md after implementation, distinguishing proposed / built / tested / merged. If design ideas conflict with actual code, report the discrepancy rather than silently rewriting game mechanics.

## 1. Core concept and intended experience

**Crown & Hearth** is a historically inspired **2D isometric settlement-building and regional strategy game** in a fictional Afro-Eurasian / Old World-inspired setting. The long campaign begins around **600–800 CE**: roughly six settlers, a founding hall, small food and material stores, and a wilderness of useful but dangerous terrain. Over centuries, a player clears land, constructs homes and workshops, grows crops, employs people, establishes trade, builds institutions, protects settlements, raises armies, and ultimately governs a kingdom through about **1600 CE**.

The theme is **“from humble settlement to mighty kingdom”**—from a single household’s survival to a realm’s politics and logistics. The *hearth* means houses, farms, work, storage, winter, civic identity and material survival; the *crown* means governance, warfare, diplomacy and conquest. These are not two separate minigames. Every military victory has economic causes and consequences. An army takes villagers out of workplaces, needs weapons and wages, draws food from harvest reserves, travels on actual routes and leaves damage or obligations in its wake.

We want **readable causal systems** rather than invisible bonuses: trees must be felled, food grown and stored, goods moved, roads connected, people assigned work, defensive sites chosen and troops supplied. A citizen or city should have a visible history. A settlement that survived a winter famine or a siege should not instantly reset to a generic “prosperous” state.

### Player decisions at three scales

- **Hamlet / household:** Where is the well? Which trees should be cut? Can we plant enough grain before winter? What supplies are missing from a bakery?
- **Town / region:** Where should roads, fields, markets, mills, granaries and walls go? Do we put workers on the harvest or the quarry? Should we specialize, trade or build an industry?
- **Kingdom:** Which trade routes and passes must be defended? Which general should lead a campaign? Should a hostile settlement be negotiated with, supplied, besieged or occupied? How are distant cities governed?

The player retains detailed control in an active city while being able to **delegate** increasingly large economic and military responsibilities. AI administrators and generals should be helpful but fallible, not perfect black boxes.

## 2. City map, tiles, growth and zoning

### Scale and representation

- A logical city tile is planned to represent approximately **8 × 8 meters**, drawn as a **2:1 isometric diamond** (currently 32 × 16 source pixels).
- **1×1 small homes** accommodate small households; **2×2 homes** and larger civic/industrial facilities consume correspondingly larger footprints. Concrete occupancy numbers must be balanced through playtesting rather than accepted as universal values. The small early house was originally conceived around four occupants, a large longhouse around a dozen.
- The proposed starting owned/access-controlled district is **64×64 tiles**, expandable toward **256×256** through adjacent land parcels. The prototype Unity settlement currently has a **128×128** city map; the ownership expansion system is a future change, not a claim about existing functionality.
- Terrain outside the currently owned district should be geographically visible and traversable where sensible, but building/extraction permissions depend on ownership or permits. **Do not shift existing world coordinates when expanding borders.**
- The working concept uses stable world positions, chunked terrain and adjacent parcels (an illustrative parcel size is 16×16 tiles); the exact land-claim mechanics remain to be designed.

### Planned city appearance and growth

Cities should have recognizable **cores, roads, outskirts, workshops, farmland and defensive perimeters**. Fields must take real area; cottages need space; clustered settlements should leave believable grass, gardens and access paths, not become a seamless solid block of roofs.

The established prototype uses a **hybrid of direct building and zoning**. Players lay out housing, farmland, commercial, lumber and mining zones; structures grow where demand, road access, funds/resources, lot shape and rules allow. Buildings serving strategic/civic functions may be placed intentionally. Keep this combination instead of forcing either fully automatic construction or hand-placement of every humble house.

Construction should eventually consume **materials transported to a site plus builder labor and time**, rather than instantly converting a global resource counter into a completed house. Road quality and path distance should affect feasibility and cost.

## 3. World scale and procedural geography

### Two connected simulation layers

The intended **campaign size is 600 × 600 km**, with roughly **24 × 24 = 576 strategic regions**, approximately 25 km across. These are strategic political/geographic units, **not detailed city tiles**. A region can include forests, fields, waterways, roads, resource zones, armies and several settlements.

The candidate **strategic terrain grid** is approximately **250 meters per cell**, separate from each city’s fine 8-meter grid. A city can reach about **256×256** detailed tiles (roughly 2.05 km per side). Simulate or load those city details when the player needs them; distant cities continue producing and changing through compact regional/aggregate models. **Never attempt a single uninterrupted 600 km map at 8-meter city resolution.**

The geography, natural routes, resources and seasons must remain **coherent across strategic and city scales**, and a loaded city should match the location and climate seen on the campaign map.

### Geography is generated before biome labels

Proposed generation sequence:

1. Choose a **regional campaign setting** (or random seed), with latitude/temperature and broad climatic context.
2. Generate **elevation, broad mountain systems, basins and coastlines**.
3. Model **prevailing winds, seasonal temperature, precipitation and mountain rain shadows**.
4. Generate **watersheds, rivers flowing downhill, lakes, floodplains, estuaries and drainage**.
5. Determine **soils, fertility, vegetation, forest density and biome transitions** based on geography.
6. Place **stone and mineral deposits, agricultural resources, routes and viable settlement sites**.
7. Seed **cities, roads, trade corridors, borders, rival powers and their economic incentives**.

Generated climate regions should blend rather than abruptly change through random biome patches. A river must connect its source to its downstream course across strategic map and city maps. Mountains, hills, passes, coastlines and river crossings should influence roads, defense and kingdom boundaries.

### Sixteen supported primary biomes

**Do not force all 16 into one campaign.** A 600 km map can have sharply varied altitude, rain shadows and coastlines, but it normally cannot contain the full Earth-like latitudinal range from lowland tropical rainforest to lowland arctic tundra. Each campaign should contain a **plausible subset** (often several prominent biomes) selected by its coherent climate and relief.

| Primary biome | Identity and main gameplay implications |
|---|---|
| **1. Temperate grassland** | Fertile, open fields and grazing; easy expansion and transport, little natural cover for defenders. |
| **2. Temperate forest** | Oak/beech/birch and other mixed woodland, timber and charcoal; land clearing and roads take labor. |
| **3. Boreal forest / taiga** | Conifers, hunting and abundant timber; short growing season and severe fuel/storage demand. |
| **4. Highlands and moorlands** | Rocky hills, heath, pasture, sheep/wool and defensible ridges; limited arable land, hard travel. |
| **5. Alpine mountains** | High passes, rock and ore, steep slopes; valley cultivation, difficult movement, chokepoints. |
| **6. Wetlands and marshes** | Fishing, reeds, peat and waterlogged ground; slow construction and movement, useful defensive barriers. |
| **7. Mediterranean woodland / scrub** | Olives, vines, orchards and mixed grazing; dry summers, water management, fires/drought risks. |
| **8. Steppe** | Wide dryish grasslands, horses and grazing; little timber, open mobile warfare and supply challenges. |
| **9. Semi-arid scrubland** | Hardy pasture, goats/sheep, scarce water and limited cultivation; wells, water routes and stone matter. |
| **10. Tundra** | Cold, treeless terrain; hunting, fishing and very limited cultivation, severe seasonal constraints. |
| **11. Desert** | Sparse water and cultivation except oases/river valleys; caravan corridors, salt and irrigation. |
| **12. Savanna** | Seasonal rainfall and grazing, scattered trees; changing forage, crops and access through wet/dry seasons. |
| **13. Subtropical forest** | Humid broadleaf forest, bamboo and suitable regional crops; dense vegetation and rainfall. |
| **14. Tropical rainforest** | Heavy rain, hardwoods and intense growth; difficult clearing, transport and wet infrastructure. |
| **15. Tropical seasonal forest** | Strong monsoon/wet-dry rhythm, seasonal timber/crop cycles and variable access. |
| **16. Tropical highlands** | Elevated climates, terraces and mountain valleys; locally cooler crops and challenging routes. |

**Geographic overlays, not exclusive primary biomes:** rivers, fertile floodplains, coasts and estuaries, lakeshores, oases, volcanic districts, alluvial soils and cliffs. A floodplain can cut across forest, grassland or subtropical terrain; an oasis sits within a desert; a coastal area can also be boreal or tropical.

### Example regional campaign types

- **Northern temperate:** forests, grassland, wetlands, highlands, boreal fringes; winter fuel, grain, wood and passes.
- **Central Asian:** steppe, scrub, desert, high mountains; horses, grazing, caravan routes and scarce water.
- **Mediterranean / North African:** Mediterranean woodland, scrub, mountains, desert and irrigated valleys; orchards, coastal ports, water and commerce.
- **South or East Asian monsoon:** tropical seasonal and subtropical forests, river plains, wetlands and highlands; rice, irrigation, drainage and monsoon transport.

The user should eventually be able to select a **broad region/climate preset** or **Random World**. This is a fictional Old World inspired by Afro-Eurasia, **not a recreation of exact historical political maps**.

### Geography should matter through systems

Biomes influence planting windows, tree species, wild food, grazing, household fuel, construction materials, water access, road and wagon costs, visibility, military mobility and supplies. A biome is **not** a fixed cultural bonus or a sentence to eternal poverty. Trade, irrigation, terracing, improved technology, institutional decisions, imported materials and conquest enable adaptation.

## 4. Seasons, agriculture, households and survival

### Physical agricultural land

A **farmhouse should manage actual designated crop fields**, rather than generating food from its tiny footprint. Fields have crop assignment, soil fertility, water access, slope and planting suitability; households and seasonal workers sow, maintain and harvest them. Work must be scheduled, and the harvest moved to usable stores. Fields may need fallowing, manure, irrigation or rotational farming as the game progresses.

Regional crop families may include **wheat, barley, rye, rice, millet, legumes, vegetables, orchard fruit and dates**; other food comes from **fishing, hunting and livestock**. Livestock also provides traction, manure, wool and hides. Model agricultural categories at a manageable level, rather than individually simulating every edible plant.

### Calendar and climate

The planned calendar has **365 days per year**. In colder temperate areas, players respond to planting, growing, harvest and winter storage. Monsoon regions have strong **wet/dry seasonal rhythms**. Drought, frost, storms, floods and missed labor windows may reduce yield. Strong granaries, diverse crops and preserved food should reduce exposure without eliminating risk.

### Households and welfare

People require **food, shelter, safe water, heating fuel** and, as prosperity grows, **clothing and other comforts**. Consumption occurs through households and local stores; employment can vary by season. Food shortages can cause poor health, stagnation, unrest, emigration or eventually deaths, rather than a sudden arbitrary game-over. Migration is linked to available homes, services, safety and a sustainable economy. Local needs and production should be inspectable.

### Strategic consequences

A poor harvest can make feeding an army costly; a blockade can cut food imports; a commercial crop can generate cash but weaken food security. Rivers can be both water sources and flood hazards. Farming is therefore a permanent strategic system, not a passive “+food per day” building.

## 5. Economy, work crews, production and trade

### Universal needs, regional materials

All settlements seek food, building materials, clothing, water, fuel and useful transport. **How they meet those needs changes geographically**: different cereals, bamboo vs timber, stone vs mudbrick, wool vs cotton vs silk, horses vs camels vs boats. This creates **regional economic specialization without arbitrary faction bonuses**.

The current Unity port deliberately begins with the smaller **six-resource** prototype economy (food, wood/planks, logs, stone, ore, gold). Expansion candidates in the vision include **grain, vegetables, meat, flour, bread, logs, planks, charcoal, iron ore, iron, tools, wool, cloth, stone and money**. Resource names/counts are provisional: avoid uncontrolled resource bloat.

Potential production chains:

- **Grain → mill → flour → bakery → bread.**
- **Trees → lumber crews → logs → sawmill → planks.**
- **Logs → charcoal production → charcoal; ore + charcoal → bloomery/smelter → iron → forge → tools/weapons.**
- **Sheep → wool → textile workshop → cloth.**
- **Livestock → meat/hides; stone quarry → usable construction stone.**

Output uses workers, suitable equipment, access and real inputs. Tools may **wear down**, introducing recurring economic demand.

### Distance and logistics

Materials should not teleport from a mine on one end of the map to a distant builder. Use **local stockpiles, granaries, market catchments, roads, carts, haulers, animal transport and eventually docks/boats**. Transport cost depends on distance, terrain, capacity, route condition and security. Keep economically significant deliveries and bottlenecks real, while aggregating low-value background motions for performance.

The existing work-crew feature branch implements woodcutters/quarrymen/miners walking and carrying deliveries; this must be integrated and verified against the active branch before building broader hauling systems. Ambient villagers may remain cosmetic.

### Labor and construction

Workforce allocation is a choice: more farmers at harvest, builders during expansion, woodcutters before winter, militia during danger. Training and technology improve productivity but require institutions, resources or experience. Military recruitment can reduce civilian production.

Building sites require **materials, available labor, access and time**. Quarry and forest resources can be local and finite; depletion reshapes settlement economics. A good interface should show why goods are missing (no input, no workers, no road, full store, delayed haulers, lack of money).

### Trade and specialization

Cities buy what they cannot produce efficiently: grain valleys feed mining districts; iron districts supply tools; forests export construction timber; desert oases support caravans; ports enable wider exchange. **Trade routes are physical and vulnerable** to war, terrain, season and raids. Markets move goods through communities, not merely exchange surplus for an arbitrary gold increment. External trade, taxes and exchange rates need careful balancing in later milestones.

## 6. Five eras, technology, culture and architecture

The five historical stages are broad *game eras*, not a claim that all Old World societies historically followed one exact sequence. Developments have regional variations and can coexist.

| Era | Approx. date | Signature opportunities and shifts |
|---|---|---|
| **I — Early Medieval** | **600–800 CE** | Timber/thatch, simple fields, founding halls, basic charcoal and ironwork, small markets, militia, early palisades. |
| **II — Agricultural Expansion** | **800–1100 CE** | Better plows and farming, mills, expanded roads and trade, improved carpentry, larger communities and fortifications. |
| **III — High Medieval** | **1100–1300 CE** | Guilds, more specialized crafts, stone architecture, commerce, towns, stronger siege and defensive works. |
| **IV — Late Medieval** | **1300–1450 CE** | Advanced workshops, mining/metallurgy, universities and knowledge networks, long-distance exchange and changing war techniques. |
| **V — Renaissance** | **1450–1600 CE** | Printing, increasingly complex finance, gunpowder, artillery and new fortifications; still dependent on food, labor and infrastructure. |

### Research system

Five proposed research domains: **Agriculture; Industry; Construction; Commerce & Learning; Military**. Development should involve **institutions, practical economic activity, material access, knowledge exchange and time**, **not a magical science currency**. Unlocks provide opportunities, not universal upgrades: irrigated rice, advanced mills or stone construction matter differently in different environments. A first technology milestone should be a small, playable selection, not all five full trees.

### Regional culture and architectural families

A house with the same function might be a timber-and-thatch hut in a northern forest, a lime-plastered masonry cottage in Mediterranean hills, a mudbrick/adobe courtyard dwelling in an arid zone, or a raised timber house in a humid floodplain. Stone mountain buildings, tiled roofs and other materials add variety. Design **approximately 4–6 reusable regional architectural families**, adapted through eras, before multiplying assets across all 16 biomes. **Do not attempt 16×5 unique texture sets for every structure at once.**

Cultural histories, trade and choices may shape troop equipment, institutions and building styles; **do not lock cultures into immutable terrain bonuses or predetermined political outcomes**. Mobility, technology and imports permit hybrid development.

## 7. Fortified settlements, raids and sieges

### Defense is central from the beginning

Settlements should be worth defending *before* formal barracks are constructed. Recruit an early **militia from civilians**, build **wooden palisades, ditches, gates, watch posts and patrols**, and maintain food and shelter. Fortifications use real timber/stone and work time, and ideally align to meaningful **tile edges**, responding to the landscape.

Technology allows **stone curtain walls, towers, gatehouses, castles and later artillery-resistant defenses/bastions**. A wall should change access, line of sight and attack options, not merely add a generic large hit-point pool.

### Terrain as an active defensive system

Hilltops affect sight and ranged engagement. Mountain passes limit movement. Rivers/bridges/fords concentrate approaches; wetlands slow armies; forests conceal movement and disrupt cavalry. A town should be able to use a cliff, ridge or river crossing defensively. Those terrain elements should be consistent with the procedural map and world movement rules.

### Credible raid objectives and civilian impact

Raiders approach along actual traversable routes from outside the settlement, **not as spawned troops encircling a base**. Their goals can be stealing stores, destroying crops, burning farm buildings, breaking access points, kidnapping workers or enforcing tribute. Defenders can respond via patrols, militia, walls, evacuation, reserves and diplomacy. Farms may be damaged, people displaced, stores emptied and repairs needed after battle; recovery is part of the simulation.

### Siege gameplay

Sieges should include **blockade, food shortages, ladders, siege equipment, wall breaches, gate assaults, mining and later artillery**. Supplying an attacker also matters. The city’s actual farms, warehouses, walls, roads and bridges must be relevant when battle occurs on the local map. Occupying territory should not require flattening every building.

## 8. Armies, generals, conquest, diplomacy and governance

### Persistent armies, not instant combat spawns

Military units are assembled from **recruits, training, equipment, mounts, wages and provisions**. Types may include infantry, archers, cavalry, mounted archers, specialized troops and siege engineers, with effectiveness shaped by training, doctrine, terrain and supply. Recruiting hundreds of people should visibly affect farms, stores and labor.

A campaign is a **physical journey through strategic geography**. Armies travel roads, passes, ferries and bridges; can use ports and ships when available; and must plan for winter, rain, disease, deserts, forage, water and supply lines. The player may intercept an army, cut a route, contest a crossing, or march around defenses at economic cost.

### Player tactics and general autonomy

On the tactical map, the player can command **units in formations**, positioning and maneuvers. At strategic scale, persistent **generals** can execute orders or run campaigns with autonomy. Traits suggested in the design: **tactics, strategic judgment, logistics, leadership, siegecraft**. An effective general may save management time but still make mistakes; the player can intervene.

This should feel more like controlling an ongoing medieval campaign (Manor Lords/Total War/Stronghold inspirations) than a Clash of Clans-style attack where units are spawned on a city perimeter.

### Conquest and governing settlements

Victory can result from a field defeat, surrendered city, captured fortress, blockade, negotiated settlement or occupation. **Conquered settlements persist**: houses, roads, food, inhabitants, culture and damage remain. A ruler then manages **taxation, trade policy, local officials, security, military obligations, unrest and rebuilding**.

Diplomacy is strategically meaningful: treaties, trade arrangements, alliances, tribute and negotiated access can sometimes beat costly conquest.

### AI kingdoms should respond to geography

Rival states seek fertile valleys, water, ports, mineral sources, routes and defensible borders. A steppe kingdom may find mobility economical; a mountainous polity may fortify its passes; a port power may depend on commerce and diplomacy. These patterns emerge through material circumstances and historical choices, not universal faction stereotypes. AI should manage trade, military priorities and changing borders while distant economies remain in aggregate simulation.

## 9. Art direction, Unity engineering and persistence

### Visual identity

Target **crisp pixel-art 2D isometric graphics**, four coherent camera directions, readable landscapes, appropriately scaled buildings and visible terrain height. Use existing repo art documentation for **precise sprite dimensions, transparency, footprint placement and direction coherence**:

- [CHATGPT_START_HERE.md](../CHATGPT_START_HERE.md)
- [TEXTURE_SPEC.md](../TEXTURE_SPEC.md)
- [BUILDING_TILE_GUIDE.md](../BUILDING_TILE_GUIDE.md)

Standard tile diamond: **32×16 px (2:1)**. Current asset guidelines: **1×1 32×48 px; 2×2 64×88; 3×3 96×128**. Small houses are intentionally narrower than their full canvas to preserve grass gaps. View order: first door visible on the lower-left/front wall, then alternate back-left, back-right, front-right orientations; architectural details remain tied to consistent physical walls. Generated high-resolution concept illustrations are **moodboards**, **not production-ready sprite textures**; do not copy them into the asset loader without deliberate pixel-art processing and art review.

### Technical foundation

Use **Unity 6 and C#**, shipping **Windows PC first**, with future Mac/Linux and mobile possible. Maintain a **plain-C# simulation layer** distinct from rendering, input and UI. Prefer seeded/deterministic terrain and replayable tests. Optimize with chunking, spatial indexing, dirty flags, cached paths/accessibility, system-specific tick frequencies and limited visible villagers. **Do not simulate every resident as an always-active Unity GameObject** and do not load the full 600 km world as detailed 8 m tiles.

### Saves and diagnostics

Save enough state to reconstruct **terrain changes, owned parcels, roads and buildings, resources, population, work assignments, seasons/eras, armies, relationships and political events**. Use **versioned schemas** and migration for changed data. Where practical, regenerate unchanged geography from seeds and store modified data. Maintain player-facing overlays for **fertility, deposits, shortages, workforce, services, routes and supply bottlenecks**; developer-facing diagnostics and deterministic replays should support testing.

## 10. Roadmap, priorities and definition of done

### Milestone 1 — Playable settlement port: already merged

As recorded by the Unity progress log: procedural terrain and settlement, isometric display/rotation, roads and zoning, demand-driven development, six-resource economy, workforce and HUD, plus reported JavaScript/Unity parity tests. **This is the existing foundation, not future scope.**

### Milestone 2 — Finish existing prototype migration: active / partial

- Walking work crews and ambient townsfolk were committed on a separate feature branch `claude/port-work-crews` as of November 16, 2026; verify branch and merge status afresh before reporting completion.
- Implement **versioned save/load** and ideally a compatible importer for JavaScript saves.
- Fix **elevation-based object occlusion and sprite sorting** in all orientations.
- Restore **fertility/mineral overlays** and other useful info tools.
- Replace missing art placeholders in a deliberate, quality-checked asset pipeline.

Milestone 2 completion requires a stable integrated experience, not just individual files or Claude’s reported local test result.

### Milestone 3 — Deeper city economy

Real fields and crops; working seasons; water and household needs; grain stores, stockpiles, transport and construction labor; durable goods and regional production chains. Avoid expanding into hundreds of resources without strong player decisions.

### Milestone 4 — Geography, climate, architecture and historical progression

Build the **layered 16-biome library**, climate presets, rain shadows, rivers and floodplains across scales, appropriate agriculture and regional building families. Introduce a practical first portion of the technology progression. Test multiple contrasting regional settings; **every campaign should use only the plausible biome subset**.

### Milestone 5 — Defense and warfare

Begin with **militia, palisades, gates and credible raids**. Then progress to formations, battles, sieges, persistent armies, supplies and generals. Confirm that the city the player constructed actually affects tactical battles and that warfare has civilian/economic consequences.

### Milestone 6 — Strategic kingdoms

Introduce the **600×600 km regional world**, ~576 strategic regions, AI polities, routes, diplomacy, trade, generals, campaigns, occupation and persistent multi-city administration. Remote settlements should continue to change without their full scenes loaded.

### Working standards and scope control

1. **One bounded deliverable at a time.** Prefer a working vertical slice over many disconnected subsystems.
2. Inspect the existing code and design decisions first. If changed specifications break old parity, change tests deliberately and document why.
3. Use separate branches based on the latest **`unity-migration`** and open pull requests for review; do not merge unilaterally into `unity-migration` or `main`.
4. Preserve the browser prototype at repo root and all usable art. Keep changes scoped.
5. Test what changed; full suites at appropriate integration gates. Record exact tests, results, errors and Unity version. Avoid polling a stuck Editor indefinitely.
6. Maintain `DEVELOPMENT_PROGRESS.md`, separating **proposed**, **implemented**, **play-tested**, **parity-tested**, **merged** and **not built**.
7. **Definition of done:** the feature is actually experienceable, edge cases covered, the relevant tests pass, and the changes are ready for human review.
8. Never claim a speculative design is already coded, or treat illustrative numbers as mandatory balance constants without explicit user approval.

## 11. Illustration references and provenance

The November 16, 2026 ten-page illustrated PDF provides original **AI-generated conceptual visual references** for these themes:

1. Early riverside settlement and founding village.
2. A diverse strategic regional map connecting cities, roads, terrain, armies and borders.
3. Climate gradient: tropical forests, snow mountains, highlands and arid terrain.
4. Monsoon agriculture and raised homes in a wet landscape.
5. Farms, mills, workshops and a transport-connected village economy.
6. Regional architectural language (timber, masonry, adobe and raised timber).
7. Palisade settlement, fortified crossing and an approaching army.
8. Kingdom campaign map, routes and armies.
9. Material palettes of regional town forms.
10. Long-term roadmap from village survival to kingdom command.

These references are **not screenshots of implemented gameplay or a technical art deliverable**. The text of this Markdown design reference is deliberately editable and indexed for Claude Code; the underlying PDF remains an optional presentation/reference artifact.

---

**North-star design test:** Does each new feature make the world feel like a specific place shaped by its terrain, inhabitants, material economy, institutions and history? If it merely adds a detached numeric bonus, reconsider how it connects to the core game.
