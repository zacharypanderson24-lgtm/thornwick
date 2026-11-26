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
| 5 | Timber chain | |
| 6 | Universal land clearing | |
| 7 | Catalogue, overlays, inspectors, warnings | |
| 8 | Save migration | |
| 9 | Balance and performance | |
| 10 | Final verification | |

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
