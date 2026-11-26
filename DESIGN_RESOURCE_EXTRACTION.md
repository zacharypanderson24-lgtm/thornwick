# Resource extraction and land management

Status: living design. The permanent rule is **simple controls, complex consequences**: the player decides where to put a building and how hard to work it;
the simulation works out which trees, rock and ore the building can reach.

**Zoning is for deciding how land will be used. Automatic working areas are for buildings that take what nature already put there.**

| Activity | Working area | Needs painted zone |
|---|---|---|
| Forestry (woodcutter lodge) | automatic (square of `WorkR` around the lodge) | no |
| Underground mining | automatic | no |
| Quarrying | automatic | no |
| Hunting, fishing | automatic (already) | no |
| Farming, housing, livestock | painted | yes |

Painted Lumber and Mining zones still work, but only as an **optional restriction**: if a building has painted tiles of its own kind inside its reach,
it works only those (this is how old saves keep behaving, and how a player boxes a lodge or quarry in). With none painted the whole reach is open.

## 1. Audit: what the code did before this milestone (Phase 1)

Read from the code, not from older notes.

| Area | File | Before |
|---|---|---|
| Trees | `World.Crews.cs` `CrewMatch` | A lodge cut a tree only if `ZoneMap == Zone.Wood`. |
| Replanting | `World.Economy.cs` `PlantSapling`, building `forester` | Only a forester, only onto Lumber-zoned open ground. A felled tile kept its zone only if a forester covered it. |
| Mining | `World.Mining.cs` `Diggable`, `MineWorkTiles` | The tunnel could only run through `Zone.Mine` tiles joined to the mine. No zone, no jobs, no digging. |
| Ore | `GenDeposits` | Ore is in three depth bands under hills and mountains, from value noise, and hidden until a tunnel reveals it (`Surveyed`). Reserves are per tile (`OreTaken`), so two mines already drain the same ground. |
| Quarry | `World.Quarry.cs`, `MineRock` | Rock tiles of the Mining zone within `WorkR`. Hills and outcrops dig down level by level into pits (persist in `RockAmtMap`/`Dig`); mountains are cut inward from an open edge. |
| Wood chain | `World.Economy.cs` | Lodge makes `Res.Logs`; sawmill turns logs into `Res.Wood` (the building material); Town Hall saws at half yield; charcoal clamp (`BurnCharcoal`, `World.SettlementWork.cs`) turns logs into `Res.Charcoal`; smithery uses charcoal as a boost. There is no firewood and no household heating anywhere in the simulation. |
| Clearing | `World.Rules.cs` `ClearTile` | A bulldozer-style instant removal of one tile (a tree gives 2 loose logs). No work orders. |
| Saves | `World.Save.cs` | `zone` map, `tunnel`, `surveyed`, `oreTaken`, `rockAmt`, `dig`, `chop`, `sapT`, `stumpT` are all saved. |
| Tests that depended on the old rule | `MiningTests`, `QuarryTests`, `WorkReachTests`, `TownBuilderTests`, `SimParityTests`, `FirstYearOpeningTests` | They paint zones first. They keep passing (a painted zone is still a valid restriction); the ones that asserted "nothing without a zone" are changed on purpose and listed in each phase's PR. |

### Names used in the brief that do not exist

`World.Quarry.cs` exists (it is feedback only). There is no `World.Develop` extraction code and no `World.Forecast` extraction code beyond reading
`ActiveJobs`. The brief's "firewood" has no consumer today; see decisions.

## 2. Decisions

1. **Auto area = implicit zone, not a new system.** One helper (`World.Extraction.cs`) answers "may this building work this tile?" for trees, rock and
   tunnels. Crews, job counting, the map overlay and the info panel all ask it, so there is one rule.
2. **Restriction, not requirement.** A building with painted tiles of its own kind in reach is restricted to them. Nothing is migrated or erased in old saves.
3. **Reserves stay per tile and global**, so shared deposits cannot be duplicated by building a second mine or quarry. Tests assert it.
4. **Wood resources.** Four ideas map to: raw logs = `Res.Logs`, planks = `Res.Wood` (kept under its old key, shown as "Planks" where it helps), charcoal =
   `Res.Charcoal`. **Firewood is not added**: nothing would burn it. It is listed as the first thing to add when a heating system exists.
5. **Forester** stays as an optional improvement (faster replanting inside its reach). A lodge replants by itself in Sustainable mode.
6. **No new resources** for copper, tin, coal, salt, silver or gold: nothing consumes them. The ore definition is data-driven so they can be added later.
7. **Governor-ready**: every decision (site evaluation, forecast, mode change, protection, clearing) is a `World` method returning data, no UI.

## 3. Phases and status

| Phase | Content | Status |
|---|---|---|
| 1 | Audit and this document | done |
| 2 | Mining without zones, site survey, depletion warnings | done |
| 3 | Quarry auto area and optional boundary (also: lodges match trees without a zone) | done |
| 4 | Forestry modes, replanting, protected woodland | done |
| 5 | Timber chain (status, warnings, chain list) | done |
| 6 | Universal land clearing | done |
| 7 | Catalogue, overlays, inspectors, warnings | done |
| 8 | Save migration | done |
| 9 | Balance and performance | done |
| 10 | Final verification | done (with the limits below) |

Each phase's PR adds its row's details below as it lands.

## 4. Phase 2: mining (built)

- **Working area** (`World.Extraction.cs`, `InMineArea` in `World.Mining.cs`): ground within `WorkR` (16) of the mine that is claimed, passes `ZoneCheck(Zone.Mine)`, has no other land-use zone, and is hill or mountain ground (flat ground only right beside the mine, so a tunnel does not sprawl under housing and fields). If the player painted Mining tiles in reach, only those count (optional boundary; this is also how old saves behave).
- **Site survey** (`SurveyMineSite`): ore in reach (the real figure, shown as an estimate), richest band, other mines on the same ground, an estimated daily output and lifetime in years. Levels: Invalid (no ore in reach; `CanPlace` refuses with the reason), Warning (little ore, shared ground, no hill, no road yet; every warning has a sentence), Valid.
- **Shared finite ore**: unchanged by design. Ore is per tile in `OreTaken`, so a second mine cannot create any. A test checks that surveying before and after adding a mine gives the same figure.
- **Depletion warnings** (`CheckMineReserve`, monthly): one message when the ore in reach is low (under 60, or under a year at full staffing) and one when it is gone. The stage is runtime only: a loaded save can warn once more.
- **Inspector**: `MineStatus` adds the ore left in reach and the years it should last (an estimate).
- **Not built, on purpose**: depth limits by era (nothing in the simulation limits depth today; the three ore bands are all reachable), suspending a mine without demolishing it, mine upgrades. Listed as gaps.
- Tests: `MiningAutoTests` (9). `MiningTests.MineTown` now accepts an ore-free lot for its no-ore case.

## 5. Phase 3: quarrying (built)

- **Automatic area**: rock within `Defs.QuarryAutoR` (8) tiles of the quarry that is claimed and has no other land-use zone. This is deliberately smaller than the reach (`WorkR` 16) so a quarry cannot slowly dig out every hill nearby; **painting Mining tiles in reach is the optional boundary** and then widens the reach to 16 and restricts the quarry to those tiles.
- **Terrain excavation is unchanged**: hills and outcrops dig down into pits, a mountain is cut inward from an open edge and never flattened; pits persist in the save (`Dig`). Tested through save and load.
- **Site survey** (`SurveyQuarrySite`): workable stone, stone locked in mountain interior, estimated daily output and lifetime, other quarries on the same rock, rock beyond the area. Invalid when there is no rock in the area (`CanPlace` refuses, with the reason); warnings for little stone, mountain-only rock, shared rock and no road.
- **Shared finite stone**: per-tile `Dig`, so two quarries cannot duplicate stone (test).
- **Overlay**: `QuarryArea(b)` feeds the map overlay (orange = workable, blue-grey = locked).
- **Lodges** now match trees through the same `InWorkArea` (no Lumber zone needed); modes, replanting and protection are phase 4.
- **Tests changed on purpose** (they asserted the old "no zone, no work" rule): `QuarryTests.NoZoneSaysWhatToDo` (now `NoRockInTheAreaSaysWhatToDo`), `WorkReachTests` quarry and lodge job tests, the lodge forecast message ("forest" instead of "Lumber zone").
- Tests: `QuarryAutoTests` (7).

## 6. Phase 4: forestry (built)

- **No Lumber zone needed.** A lodge works every claimed tree within `WorkR` (16) that is not protected. A painted Lumber boundary still restricts it, and counts as an explicit order to fell those trees (no floor, no replanting: the old behaviour, so old saves and AI towns are unchanged).
- **Modes** (`ForestMode`, saved per lodge, default Sustainable; old saves load as Sustainable):
  - *Sustainable*: fells mature trees, **replants** each felled tile once its stump has rotted (8 days; `Replant` set, saved), and **leaves `SustainFloor` (10) mature trees standing** in its area (checked again at the last chop, so a crew cannot overshoot). Below the floor it idles and the inspector says why and what to do.
  - *Intensive*: no floor, no replanting. The inspector gives the years until the area is bare.
  - *Land clearing*: works only tiles in `ClearOrders` (saved), keeps the logs, does not replant, removes each order when the tree falls. The universal clearing tool, other obstacles and construction-site clearing are phase 6.
- **Growth**: a sapling takes `SaplingDays` growing days. Growth stops in winter (the last quarter of the 360-day year) and is slower on dry ground (`MoistureYield`; 1 on a dry-world map). A forester's care adds a quarter more. So a replanted tree is back in roughly four calendar years, which is what keeps sustainable output modest: `SustainableLogsPerDay(trees)` is the number the inspector and forecast quote.
- **Forester**: no longer needs a Lumber zone. With none painted it plants within 6 tiles of itself on open ground that is not beside a road or building and not in another land-use zone. It is now an optional improvement, not part of the basic chain.
- **Protected woodland** (`ProtectedWood`, saved): never cut by any crew; only cleared by `DesignateClearing(x, y, confirmProtected: true)`, which lifts the protection.
- **One crew per tree**: `_reservedTrees` stops a second lodge targeting a tree a woodcutter is walking to or chopping. Overlapping areas are fine; overlapping reservations are not.
- **Survey and inspector**: `SurveyLodgeSite` (mature trees, saplings, protected, overlapping lodges, open land, short-term and sustainable output, notes) feeds the placement forecast; `LodgeStatus` is the inspector text with the mode, forest condition, bottlenecks and recommended actions. Both use real counts.
- Tests: `ForestryAutoTests` (12): no zone, floor, replanting, intensive, clearing mode, protection and confirmation, winter growth, one crew per tree, save/load, old-rules boundary, site survey.
- Limits: the mode cannot be changed from the UI yet (phase 7); firewood is not a separate resource (see decisions).

## 7. Phase 5: timber chain (built)

The chain already conserved resources; this phase makes it readable and checks it.

- **What the chain is** (`TimberChain()`): forest -> woodcutter lodge -> raw logs (`Res.Logs`); logs -> sawmill -> planks (`Res.Wood`, the building material; the Town Hall saws 1 log a day at half yield); logs -> charcoal clamp -> charcoal (2 logs each; smithies burn it for extra metal). **There is no firewood step**: nothing in the game burns firewood, so adding it would be a resource without a use. It is the first thing to add with a heating system.
- **Status text**: `SawmillStatus`, `CharcoalStatus` (log use, output, staffing, what is missing) next to `LodgeStatus`, shown in the inspector.
- **Warnings** (`BuildingWarnings`, `ProductionWarning`): for lodges, quarries, mines, sawmills and clamps. Each has a title, what is happening, why, and the recommended action, from real state (not connected, no logs, limited sustainable production, out of ore, restricted extraction, store full, and so on). The inspector prints them.
- **Tests** (`TimberChainTests`): a sawmill and the Town Hall consume exactly what they saw and give one plank per log / half a plank per log; no logs, no planks; charcoal costs exactly 2 logs; an unconnected building says to build a road; the chain lists only what exists.
- Not done: a build-catalogue link from a warning (phase 7).

## 8. Phase 6: land clearing (built)

- **Orders** (`ClearOrders`, saved): designate trees, saplings, stumps and outcrop rocks (rock with `RockAmt` below 3 and no pit). `ObstacleAt` says what a tile holds; `WhyNotClearable` explains every refusal (building, mountain, quarry pit, not yours, nothing there). Clearing **never touches a building**: demolition stays a separate tool.
- **Work**: each day up to `ClearCrewMax` (6) **idle villagers** (the real `D.Idle`) take one order each; a tile takes `ClearDays` worker-days (tree 6, sapling 1, stump 2, rock 8). With no idle villagers nothing is cleared. A lodge in Land clearing mode (phase 4) works the same orders with walking crews; a tree a woodcutter is on is skipped by the labourers (one crew per tree). Progress is saved (`ClearProgress`).
- **Recovery, once**: a tree gives `LogsFromClearing` (12 logs; a lodge crew's full felling gives 24: hacking a tree down by hand wastes half), a sapling 2, a stump 1, an outcrop up to `RockRecovery` (8) stone. Totals are saved (`ClearedTiles/Logs/Stone`). A cleared tile is bare ground: its rock or tree is gone, so no quarry or lodge can take it again. Tests check log conservation through the Town Hall's saw.
- **Unreachable orders** (no walkable way to a road or the Town Hall within `ClearReachSteps` 24) are skipped and dropped after `StuckDays` 30 with a message, so they never hold workers for ever.
- **Building through woodland** (`PlanPlacement`, `PlaceWithClearing`, `PlaceRoadWithClearing`): three states, Valid now, Valid after clearing (amber in the preview), Invalid. The clearing is queued as orders and the building or road (`Pending`, saved) is placed when the ground is clear; the cost is paid at placement, and it waits if the town cannot pay. A second plan on the same ground is refused. Protected trees need explicit confirmation (the game asks).
- **Not modelled**: bushes (the game has no bush objects). Listed as a gap rather than invented.
- Tests: `ClearingTests` (12).

## 9. Phase 7: interface (built)

- **Build catalogue**: lodge, forester, quarry and mine are under Natural Resources; sawmill and charcoal under Industry; a new **Land Management** category holds Clear Land, Protect Woodland, Open Woodland and the two optional boundary tools (Lumber Boundary, Mining Boundary, both labelled optional). No description tells the player to paint a zone first (a test checks it).
- **Overlays**: a Forest overlay (mature trees, saplings, protected woodland blue, clearing orders orange, the lodges' reach) opens automatically for the lodge, forester and the three land tools and restores the previous overlay afterwards (the existing `ForTool` mechanism). Placing a mine opens the Underground view with an **estimate** of the ore in reach (nothing is guaranteed). Placing a quarry draws the area it would dig.
- **Placement preview**: green valid, amber valid after clearing or poor (the existing warning text explains it), red invalid. The forecast tip shows the survey numbers (ore, stone, trees, sustainable output, warnings) from real data.
- **Lodge panel** (`Hud.Lodge.cs`): click a lodge with Inspect to read its mode, forest, output and warnings and to switch Sustainable / Intensive / Land clearing (tested with a real click through the harness). Escape closes it.
- **Inspectors**: mines, quarries, lodges, sawmills and clamps print their status and every warning as "what, why, recommended action".
- **Land tools**: drag a rectangle; protected woodland asks first; build and road tools plan their own clearing and ask before touching protected trees.
- **Not done**: no screenshots of the map overlays (the capture tool renders the HUD over flat ground, not the world); no real-mouse playtest in the editor (see section 12).
- Tests: `LandManagementUiTests`; the overlay-list test in `ManagementUiTests` now includes Forest on purpose.

## 10. Phase 8: save migration (verified)

Behaviour when an older save loads (all covered by `ExtractionMigrationTests`, which strips every new field out of a real save first):

| Old data | What happens |
|---|---|
| Lumber zones | kept; they are now an optional boundary and an explicit felling order for lodges that have them (no floor, no replanting: the old behaviour) |
| Mining zones | kept; a mine or quarry with painted tiles in reach is confined to them. A mine no longer needs them |
| Forester buildings | kept; they plant on painted Lumber ground as before, or near themselves with none |
| Lodges | no saved mode: **Sustainable** |
| Mine tunnels, shoring, cave-ins | unchanged |
| Ore taken, rock dug, quarry pits | unchanged: **loading never refills a deposit** (tested) |
| Trees, saplings, stumps, `SapT` | unchanged; a sapling's timer now runs only on growing days |
| Protected woodland, replant list, clearing orders and progress, planned buildings, counters | absent in old saves: start empty; saved from now on |

Limits: the low-ore warning stage is runtime, so a loaded save can warn once more. Old saves that already had a quarry or lodge sitting where the automatic area is now larger will start working that area (that is the intended change).

## 11. Phase 9: balance and performance (measured)

- **First year with no zones** (the existing opening harness, `FirstYearPlan`, with `zones: false`): seeds 7, 42 and 123 all keep food, deliver ore and stone, and have timber above 120 with a lodge, sawmill, quarry and mine that were never zoned. The old zoned opening still passes.
- **Forestry over years** (`ExtractionEconomyTests`): a sustainable lodge on a 60-tree forest still produces in year 6 and the forest (trees, saplings, stumps) holds; an intensive lodge on the same forest produces at least as much in year 1, has stripped it within 8 years and then produces nothing. A small forest shows the early difference clearly (`ForestryAutoTests`).
- **Why a big forest is not "faster" in year 1**: output is crew-limited (3 woodcutters, about 1.2 logs a day); the floor only matters once the forest is small.
- **Finite reserves**: over 15 years a quarry never dug more than lay in its area and never delivered more than it dug; over 10 years mines never took more than the ore in reach.
- **Growth**: `SaplingGrowthRate` is 0 in winter and between 0.5 and 1 otherwise depending on ground moisture (on water maps).
- **Performance**: the same working town (43 buildings, a lodge, a quarry, a mine, the opening harness at the end of year one, 200 measured days) ticked in **1.40 ms a day before** this milestone (zones painted, the old code at the phase 1 commit) and **1.50, 1.64 and 1.82 ms a day after** it (no zones, three separate runs on the same machine, the last two inside a full test run): somewhere between 7 and 30 percent slower, which at these sizes is a fraction of a millisecond and well inside the run-to-run noise. It is not a cost that grows with the map: work areas and forest counts are cached per world version. Work areas and forest counts are cached per world version, ore reserve scans run monthly per mine, and the clearing queue touches at most six orders a day.
- **Balance concerns** (open): sustainable output on a modest forest is small (a forest of 60 trees supports roughly 1 log a day for ever), so a growing town needs several lodges or foresters; the opening harness shows the first year is fine, the third to fifth years have not been played. Costs were not changed.

## 12. Phase 10: final verification and report

**Automated**: the whole suite (724 tests) passes with 0 failures (9 skipped and 1 inconclusive, both from before this milestone). New in this milestone: `MiningAutoTests` 9, `QuarryAutoTests` 7, `ForestryAutoTests` 13, `TimberChainTests` 7, `ClearingTests` 12, `LandManagementUiTests` 5, `ExtractionMigrationTests` 4, `ExtractionEconomyTests` 6, and three zone-free first-year scenarios.

**Scenarios from the brief**: A (founding settlement without zones), B (sustainable lodge over years), C (intensive lodge strips its forest), D (protected woodland, confirmation to clear), E (shared deposits), F (low and out-of-ore warnings), G (building through woodland) and H (first year with no zones) each have a test that runs the real simulation.

**Visual**: the capture tool (`UiShots`) renders the real HUD at five resolutions for the Natural Resources and Land Management pages of the Build catalogue, the lodge panel in Sustainable and Intensive mode, and the inspector with its warnings. Looking at them found and fixed a wrapped title in the lodge panel and leftover text that still told players to paint zones.

**Not verified, plainly**:
- No real-mouse play in the Unity editor. The drag tools, the amber preview, the overlays that draw on the map and the confirmation dialogs are covered by tests of the rules behind them and by one real-click test of the lodge panel, not by playing.
- The capture tool draws the HUD over a flat field, so there are **no screenshots of the world map** with the Forest overlay, the ore estimate, the quarry area, the clearing preview or a quarry pit. Those need a play session.
- Only the first year has been played as a whole; years 3 to 5 of a growing town have not.

**Known gaps and decisions**: no firewood or new minerals (nothing consumes them); no depth limits by era, mine suspend or upgrade (nothing in the simulation limits depth); no bushes (not modelled); no tree species inventories; the low-ore warning stage is not saved; sustainable output on a small forest is modest, so a growing town needs several lodges or foresters; governor-facing calls exist as plain `World` methods (`SurveyMineSite`, `SurveyQuarrySite`, `SurveyLodgeSite`, `SetForestMode`, `ProtectWoodland`, `DesignateClearing`, `PlanPlacement`, `PlaceWithClearing`, `BuildingWarnings`) but no governor uses them yet.

**Recommended next**: a play session on the finished build (the places listed above), then balance the third to fifth years, then the heating system that would give firewood a use. Government Phase 3 can resume.
