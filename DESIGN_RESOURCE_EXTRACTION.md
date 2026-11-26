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
| 2 | Mining without zones, site survey, depletion warnings | |
| 3 | Quarry auto area and optional boundary | |
| 4 | Forestry modes, replanting, protected woodland | |
| 5 | Timber chain | |
| 6 | Universal land clearing | |
| 7 | Catalogue, overlays, inspectors, warnings | |
| 8 | Save migration | |
| 9 | Balance and performance | |
| 10 | Final verification | |

Each phase's PR adds its row's details below as it lands.
