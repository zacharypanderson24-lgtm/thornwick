# Development progress: Crown & Hearth (Unity migration)

Living log of the Unity port of Thornwick. Update it with every milestone commit. The browser prototype at the repo root
is unchanged and still deploys from `main`; all Unity work lives in `unity/` (see `unity/README.md`).

_Last updated: 2026-11-16 (Claude Code, work crews)._

## Branches

| Branch | Purpose | Head |
|---|---|---|
| `main` | Browser prototype (JavaScript). Untouched by the migration. | `4d99fca` |
| `unity-migration` | Integration branch for the Unity port, shared by Claude Code and ChatGPT/Codex. Contains the playable port (PR #1), walking work crews, underground mining, mountain ranges, ChatGPT's saves, Texture Lab, banner colours and art, farming, and the tutorial. | merge of `claude/tutorial` |
| `claude/port-work-crews` | Walking work crews and townsfolk (`sim_crews.js`). Merged into `unity-migration` 2026-11-16. | `unity-migration` |
| `claude/underground-mining` | Underground mining (Unity-only new mechanic): hidden deposits, surveying, shaft levels, timber supports, cave-ins, underground view. Cut from `unity-migration` `bb2df72`. Merged into `unity-migration` 2026-11-16. | `unity-migration` |
| `claude/mine-ore-art` | ChatGPT's mine and ore art, mine entrances. Merged into `unity-migration` 2026-11-16 (`bf2968f`). | `unity-migration` |
| `claude/mountain-ranges` | Mountain ranges: long, tall ranges across the map, relief by region. Cut from `unity-migration` `bf2968f`. Merged into `unity-migration` 2026-11-16. | `unity-migration` |
| `claude/farming` | Farming: ChatGPT's farm stack merged in (saves, Texture Lab, banner colours, nature/woodcutter/mine/farm art, crop fields, barns, wheat), made to work with crews, mining and mountains, then 2x2 farmhouses with zoned 1x1 plots and click-click rectangle zoning. Merged into `unity-migration` 2026-11-16. | `unity-migration` |
| `claude/tutorial` | In-game tutorial for the existing mechanics. Merged into `unity-migration` 2026-11-16. | `unity-migration` |
| `claude/settlement-port` | Claude Code's first working branch. [PR #1](https://github.com/zacharypanderson24-lgtm/thornwick/pull/1) merged into `unity-migration`. | `22058e9` |
| `codex/unity-saves` | ChatGPT/Codex: Unity saves and browser save migration. In `unity-migration` (via `claude/farming`). | `0d1552c` |
| `codex/terrain-occlusion` | ChatGPT/Codex: terrain drawn in depth order, resource view. Not merged yet. | `6490fe9` |
| `codex/settlement-art-tools` | ChatGPT/Codex: banner colours, Texture Lab, nature sprites (contains `codex/unity-saves`). In `unity-migration` (via `claude/farming`). | `b6b1505` |
| `codex/distinct-crop-textures` | ChatGPT/Codex: the farm stack (woodcutter, mine and farm art, crop growth, crop fields, detailed barns, wheat). In `unity-migration` (via `claude/farming`). | `a41c20d` |
| `chatgpt/game-vision-old-world` | ChatGPT: game vision and art direction docs. Not merged yet. | `a4ea49d` |

ChatGPT and Claude Code each work on their own branch and merge into `unity-migration`. All branches above were cut from `22058e9`.
Before merging a branch, merge the latest `unity-migration` into it first and rerun the EditMode tests.

### Note for ChatGPT/Codex: merging after the work crews

`unity-migration` now runs the real walking crews (`World.Crews.cs`) instead of the old per-building approximation, so
`Building.CrewAcc` and `Defs.CrewRate` are gone. Crews and townsfolk are runtime only and never saved, exactly as in the browser.
When `codex/unity-saves` (and `codex/settlement-art-tools`, which contains it) take the latest `unity-migration`:

- `Building.cs` conflicts: keep `partial class` from the saves branch and the `Crew` / `Reach` fields from `unity-migration`.
- In `World.Save.cs` drop the `CrewAcc` field and the `crewAcc` save/load entries (the browser save format has no such field), and in
  `SaveTests.cs` drop `original.Hall.CrewAcc = 2.5`.
- `DEVELOPMENT_PROGRESS.md` conflicts: keep both sets of rows.

`codex/terrain-occlusion` and `chatgpt/game-vision-old-world` merge cleanly (checked with a trial merge).

### Note for ChatGPT/Codex: saves, art and underground mining (now in `unity-migration`)

Mining changed the world generator: `World.GenDeposits` removes the browser's lowland ore tiles and places new showings on hill
flanks, and `SimParityTests.TilesMatch` now treats ore tiles as open ground. The `mine_1..4` and `ore_0/1` art on `codex/mine-art` suits
the new mines (an adit mouth in a rock mound); it and `ore_0/1` are now in `unity-migration` (identical files, so `codex/mine-art` and the
branches after it merge them without conflict). Art note: the ore heap beside the mine sits on wall 2 in view 1 but on wall 4 in view 4,
and views 2 and 3 show neither; per the directional-coherence rule it should stay on one wall in every view that shows that wall.

Mining state that saves must carry once both branches are in: `World.Surveyed` (bool per tile), `World.OreTaken`
(band * N * N + y * N + x -> ore taken), `World.MinerDeaths`, `World.CaveIns`, and per mine `Building.Adit`, `AditShored`, `AditProg`,
`Depth`, `Shored`, `DigProg`, `Closed`, `CaveIns`. Deposit sizes and the frozen mining heights are NOT saved: `GenDeposits`
regenerates them from the seed (it runs before any building levels the ground). It also moves the ore showings, so a save must
store the deco map as it is (it already does) and loading must not re-run the showing placement over it.
Browser saves have none of this; importing one should leave everything unsurveyed with shafts at level 0.
The underground view only adds tiles to `OverlayRenderer` from `GameController.DrawUnderground`, so it should sit beside
the resource view (V) from `codex/terrain-occlusion` without conflict beyond `GameController.cs` merge lines.

## Pull requests

| PR | From → into | Status |
|---|---|---|
| [#1 Unity port: playable settlement with zone growth](https://github.com/zacharypanderson24-lgtm/thornwick/pull/1) | `claude/settlement-port` → `unity-migration` | merged 2026-11-16 as `2ae2883` |

## Milestone log

| Commit | Branch | What |
|---|---|---|
| `5dccc00` | `unity-migration` | Empty Unity 6000.6.9f1 project (URP 2D template) in `unity/`, Unity `.gitignore` and `.gitattributes`. |
| `ed4e32f` | `claude/settlement-port` | First playable settlement: C# sim (terrain, placement, roads, zones, workforce, daily economy), isometric renderer with rotation, camera, HUD, art importer, parity tests (10). |
| `a8de069` | `claude/settlement-port` | Zone development ported (`sim_develop.js`): homes, farms, markets, lodges, quarries and mines grow on zones; 2x2 lots; demand HUD; development parity test (11 tests). |
| `13e9157` | `claude/settlement-port` | `unity/README.md` updated for the zone tools and 2x2 placeholders. |
| `c739d7b` | `claude/settlement-port` | Adds this progress file. |
| `c9870e4` | `claude/settlement-port` | Records PR #1 in this file. |
| `2ae2883` | `unity-migration` | Merge of PR #1: the playable port is now on the integration branch. |
| `22058e9` | both | Records the merge in this file. |
| `0a2b378` | `claude/port-work-crews` | Walking work crews and townsfolk ported (`World.Crews.cs`), drawn in Unity (`AgentRenderer.cs`); 300-day tick parity test (12 tests). |
| `aad0ca9` | `claude/port-work-crews` | Branch map and merge notes for ChatGPT/Codex. |
| `bb2df72` | `unity-migration` | Merge of `claude/port-work-crews` (12/12 EditMode tests in batch mode on the merge). |
| `6d3bfd3` | `claude/underground-mining` | Underground mining (`World.Mining.cs`), underground view (U), 10 mining tests; ore removed from the two day-by-day parity scenarios (22 tests). |
| `5d7c3ce` | `claude/underground-mining` | Mining rework: ore in hills and mountains, adits from the foot of high ground, showings on hill flanks, 3x3 surveys; terrain parity ignores ore tiles (24 tests). |
| `bf69c74` | `claude/underground-mining` | Records the mining merge. |
| `96dc7cc` | `unity-migration` | Merge of `claude/underground-mining` (24/24 EditMode tests in the Editor on the merge). |
| `bf2968f` | `unity-migration` | Merge of `claude/mine-ore-art`. |
| `6e3c8b6` | `claude/mountain-ranges` | Mountain ranges by relief, peaks to 16, passes, Classic style for parity (33 tests). |
| _this commit_ | `claude/mountain-ranges` | Records the mountain merge; then merged into `unity-migration`. |
| `575caa4` | `claude/mine-ore-art` | ChatGPT's mine art (`mine_1..4`), cherry-picked from `codex/mine-art` `19bfe75` with its authorship. |
| `90ae791`, `f0bf19a` and next | `claude/mine-ore-art` | ChatGPT's ore showing art (`ore_0/1` from `b6b1505`, exact files) and its `NatureSprite` lookup; mine entrances face away from the hill and need their road (26 tests); merged into `unity-migration`. |

## Implemented and tested

Tested means covered by `unity/Assets/Tests/EditMode/SimParityTests.cs` (12 tests, all passing on 2026-11-16 on `unity-migration` after the crews merge, and on `claude/underground-mining`) and play-tested in
the Editor (no console errors or warnings). Parity tests compare against the real browser code through
`unity/ParityReference/make_reference.js`. On `claude/underground-mining` the two day-by-day scenarios remove every ore
showing before laying out the town, and the terrain test treats ore tiles as open ground (showings are placed by Unity now).

| Feature | Parity-tested against the JS | Play-tested |
|---|---|---|
| Deterministic hash and value noise (`hash2`, `vnoise`) | yes, bit-exact | n/a |
| Terrain heights: rolling hills, basins, mountain ranges and massifs, slope relaxing | yes, every vertex, seeds 7 and 1234 | yes |
| Tiles: forests, tree species and variants, grove sizes, stone outcrops, ore, fertile soil, mountain elevation | yes, every tile, two seeds | yes |
| Starting settlement: Town Hall, road, two huts (hut looks), farm, door faces, service | yes | yes |
| Housing, jobs, idle and employed totals; workforce by trade priority | yes (start state and the 150-day development scenario) | yes, Workforce panel |
| Zone development: demand per zone kind, lot and site rules, daily hash noise, tie-breaks, 2x2 lots, wood waits | yes, 150 days, every building and demand value | yes, a scripted town grew huts, a large house, farms and a mine |
| Daily economy: food, eating, gold, trade, births and deaths, starvation, barracks training, storage cap | yes: every resource, population and building count, 300 whole days through `Tick()` (with crews, development, felling, digging) | yes |
| Placement, removal, road paths, lumber and mining zones, clearing trees and rocks | ported; exercised by the development and woodcutter tests | yes |
| Walking work crews: woodcutters, quarrymen, miners walk out (BFS over walkable ground, depth 10), work 2 days beside the marked tile, carry the load home; output only from delivered loads; 4 chops fell a tree; full stockpile keeps quarry and mine crews home | yes: every worker's state, tile and load, every day for 300 days (two lodges, four mines, a quarry) | yes: workers walk, work and carry loads, drawn between tiles through the day and in depth order; checked in views 1 and 2 (Q/E) |
| Townsfolk: ambient villagers (0.6 per peasant, max 90) stroll the roads from their houses to markets, wells, the Hall and so on; cosmetic only | yes: every villager's id, state, tile and tunic, every day for 300 days | yes: 3x5 px villagers on the roads; hidden at 0.25x/0.5x like the browser |
| Stumps rot, foresters plant saplings | covered by the tick parity test (stump counts) and the woodcutter test | yes |
| Finite stone and ore (quarries dig hills down, mines empty ore tiles) | covered by the tick parity test (stone and ore delivered, ore tiles left); no dedicated dig-down test yet | yes (mines and a quarry in the play-test) |
| Isometric 2:1 rendering: ground mesh draped over heights with slope shading, snow peaks, roads, zone tints, depth-sorted sprites | n/a | yes |
| 4-way camera rotation (Q/E) with per-view building art, zoom ladder 0.25x-5x, pan, height-aware tile picking | n/a | yes |
| HUD (uGUI): resources and rates, speed, rotation, tools, tile info, demand strip, zone notes, Workforce priorities | n/a | yes |
| Art pipeline: `../assets` imported (magenta keyed out, ground tiles shrunk to 32x16), placeholders for missing art | n/a | yes |
| ChatGPT art in use: iron mine `mine_1..4` (4 views) and ore showings `ore_0/1` | n/a | yes: mine checked in views 1 and 2, all 329 showings drawn with the new art |

### Underground mining (Unity-only, merged into `unity-migration` 2026-11-16)

A new mechanic, not a port: the browser still mines surface ore tiles. Design: `DESIGN_TERRAIN.md` section 3 and Zach's mining
decisions, plus choices made on 2026-11-16: miners survey Mining zones (no prospector building), a cave-in closes the mine and can
kill a miner; and in the rework the same day: ore lies in hills and mountains (not lowland), mines at the foot of high ground drive
an adit into it, quarries and mines share a mountain, and surface showings sit on hill flanks over real ore. All numbers are first
guesses for tuning (top of `World.Mining.cs`).

| Feature | Tested (`MiningTests.cs`, 14 tests) | Play-tested |
|---|---|---|
| Hidden ore in 3 bands counted down from each tile's surface, from the seed; none in lowland, hills about 33 and mountains about 149 ore a tile (seed 7); band 3 richest | yes | yes (underground view) |
| Surface showings only on hill tiles over a strong top band (329 on seed 7; the browser's lowland ones are gone) | yes | yes |
| Mining zones on rock, mountains, showings and open ground; not on forest | yes | yes |
| Mines grow on a zone with a showing or known ore, or as a gamble on 3+ unsurveyed hill or mountain tiles | indirectly | yes |
| Miners survey zone tiles (each trip reveals all bands of a 3x3 patch); a barren zone is reported | yes | yes |
| Adit: a band whose tile rises above the mine's floor is reached through the mine's adit (10 worker-days, 6 wood, half the shaft-1 risk); bands below the floor need the shaft (12 / 24 / 40 worker-days, 6 / 10 / 16 wood) | yes | yes: a mine at a mountain's foot drove its adit and brought up 72 ore from band 3 in 90 days with no shaft |
| Mine entrance: faces straight away from the high ground in reach (the adit runs into the hill), and the mine works and grows only with a Hall-connected road on the tile in front of it; with no high ground near, the door faces the road like any building | yes (`AMineOpensAwayFromTheHighGround`, `AMineNeedsItsRoadAtTheEntrance`, grown mines checked) | yes: mouth opens onto the road, hill behind, views 1, 3 and from the back |
| Mining heights are frozen at generation, so quarrying a mountain's face does not move or destroy its ore | yes | n/a |
| Loads by band 4 / 6 / 8; miners work the richest reachable band first | yes | yes |
| Cave-ins: risk x (shaft level, or 0.5 in the adit) x crowding, x0.15 where shored; mine closed 20 days, supports there smashed, 60% (unshored) / 25% (shored) chance a miner dies | yes | yes (before the rework: an unshored cave-in killed a miner while wood was short) |
| Underground view: U cycles bands 1-3 / off (orange = known ore, grey = surveyed empty, blue = zone not surveyed, yellow = mine with an adit or shaft) | n/a | yes |
| Mine info: adit, shaft depth, supports, digging progress, repairs, unsurveyed tiles, worked out / no ore found | n/a | yes |

Seen in play: supports compete with building for wood (one test town sat at 2-9 wood while mines shored), which led to an
unshored cave-in. Intended pressure, but worth tuning (a wood reserve for supports, or a player toggle). Not saved yet (see the
note for ChatGPT/Codex above). Smelting (ore + charcoal) and other ores (gold, gems) are not built.
### Mountain ranges (Unity-only, merged into `unity-migration` 2026-11-16)

Decided 2026-11-16 (Zach): mountains span the future conquest map, so on a city map they are long ranges that run off the map edges,
much taller (up to level 16, was 8), and how mountainous a map is depends on its region. The browser's own generator stays as
`TerrainStyle.Classic` (bit for bit; the parity tests use it), so all the day-by-day economy comparisons still hold.

| Feature | Tested (`MountainTests.cs`, 7 tests) | Play-tested |
|---|---|---|
| `World(seed, style, relief)`: Ranges (the game) or Classic (browser); relief Lowland / Upland (default) / Highland / Alpine, also a `GameController` Inspector field | yes | yes |
| Mountain share by relief, 12 seeds: about 3.7% / 8.4% / 17.6% / 26.1% (Classic was under 2%) | yes (ordering and ranges, 6 seeds) | yes (Highland seed 7: 16.2%) |
| Peaks up to level 16 (`HMax` 16; Classic still clamps at 8); snow above level 12 | yes | yes |
| Ranges cross the map and run off its edges, bent by noise; steep flanks to level 4, gentler foothills below (hill ground for adits) | yes | yes |
| Saddle passes (crest down to level 2.2 over about 18 tiles, one per ~55 tiles of range); 100% of walkable land reachable from the Hall on all 48 probe maps | yes (97%+ and all four edges, 24 maps) | n/a |
| A valley stays open round the town: no mountain within 15 tiles of the Hall, starting settlement intact | yes | yes |
| Big lone massifs on upland and higher relief | indirectly | yes |

Generation takes about 0.1-0.2 s a map. Not done: the conquest map does not exist yet to choose a city's relief (new games use
Upland unless the Inspector field is changed); rivers and valleys shaped by the ranges; mountain art beyond the flat rock and snow surface.

### Note for ChatGPT/Codex: mountain ranges

`Defs.HMax` is now 16 (was 8) and `World` takes `(seed, style, relief)`. The parity tests construct `TerrainStyle.Classic` worlds; any
new test that compares with the browser must too. `GroundRenderer` picks snow with `TileLevel >= Defs.SnowLevel` (was `* LvlPx >= 44`);
`codex/terrain-occlusion` rewrites that file, so keep the snow test when merging. Taller terrain means more ground can hide what is
behind it, which makes the occlusion work more important.
### Farming (merged into `unity-migration` 2026-11-16)

Built on ChatGPT's crop fields (`codex/working-farm-fields`, `World.Farms.cs`), changed to Zach's design of 2026-11-16: farmhouses are
2x2 and grow on Farmland zones; the 1x1 Farmland tiles on fertile soil around a farmhouse are its plots; zoning is click-click.

| Feature | Tested (`FarmTests.cs`, 4 tests) | Play-tested |
|---|---|---|
| Farmhouses always 2x2 (ChatGPT's barn art); a Farmland lot needs room for unclaimed plots nearby | yes | yes |
| Plots: open fertile Farmland tiles within 3 of a served farmhouse, owned by the nearest; each farmer tends 2 plots at 0.4 food a day (about 0.8 a farmer, as the old farms; ChatGPT's 1.5 a plot gave 4x) | yes | yes (starting farm: 8 plots, 3.2 food a day) |
| New games start with a 2x2 farmhouse and 8 plots; the browser-compatible Classic world keeps the 1x1 farm | yes | yes |
| Click-click zoning for every zone: first click sets a corner, the second zones the rectangle (`World.ZoneRect`), with a green/red preview of the rectangle and a toast of what was zoned and why tiles were skipped; Esc or a tool change cancels | rectangle rules yes | preview and toasts not clicked through in the Editor (no mouse automation); please try it |
| Crop art: ChatGPT's wheat growth stages on plots | n/a | yes |

Merged with it from ChatGPT: saves (fixed here: `crewAcc` dropped; terrain style, relief and all mining state saved; deposits regenerated
on load from the seed, never moving saved showings; browser saves load as Classic terrain), Texture Lab, banner colours, nature,
woodcutter, mine and farm art. Parity: the two day-by-day browser scenarios now also remove the starting farm and every Farmland zone
(farming is Unity's own), which put ChatGPT's switched-off zone-development comparison back on.
### Tutorial (merged into `unity-migration` 2026-11-16)

A guided walk through the existing mechanics in a panel at the top centre (16 steps: welcome, camera, time, roads, housing zones and
growth, farmland and farmhouses, workers, lumber, sawmill, wells and markets, stone and iron, the underground view, supports and
cave-ins, soldiers, saving). Each step lights its toolbar button and finishes when the player has actually done it, counted from when
the step began (`Sim/Tutorial.cs`, pure C#); explanations wait for Next; Back, Skip and Close work; it opens by itself on a fresh game
until finished or closed once (PlayerPrefs), and the Tutorial button (bottom right) reopens it where it was left.
Tested by `TutorialTests.cs` (5 tests). The text names today's buttons; the planned UI rework (grouped Build/Zone flyouts, resource
chips, demand bars, quieter tile info, code-drawn icons; agreed 2026-11-16) must update it.

## Not done yet (next candidates)

1. Saves: built by ChatGPT, in `unity-migration` (see Farming above).
2. Hills hiding objects behind them (all ground currently draws before all objects).
3. Info overlay (V: fertility and deposits), banner tint, texture lab.
4. Art: ChatGPT art for the placeholder slots `farm`, `wood`, `sawmill`, `forester`, `quarry`, `market`, `well`,
   `manor_1`, `manor_2`, `farm2`, `market2`, rocks, stump, sapling (several are drawn on ChatGPT's unmerged `codex/*` branches) (rules: `TEXTURE_SPEC.md`, `CHATGPT_START_HERE.md`).
   Crew workers and villagers are code-drawn like in the browser (ChatGPT villager sprites are a browser idea too).
5. Later milestones from the design: tech tree, fortified cities, armies, conquest map.

## How to verify

```
node unity/ParityReference/make_reference.js     # repo root; refresh after changing src/sim*.js
unity test unity --mode EditMode                 # with the project closed in the Editor
unity cmd test run --mode editor                 # or, inside a running Editor (leave Play mode first: EditMode tests do not start during Play)
unity open unity                                 # then open Assets/Scenes/Settlement.unity and press Play
```

## Known issues

- Recompiling scripts while the game is in Play mode reloads the code and leaves `GameController` without its world (Unity does not
  call `Start()` again), so every frame throws a `NullReferenceException` until Play mode stops. Set Preferences > General > Script
  Changes While Playing to "Recompile After Finished Playing", or stop Play mode before recompiling.
