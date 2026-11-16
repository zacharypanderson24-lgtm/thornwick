# Development progress: Crown & Hearth (Unity migration)

Living log of the Unity port of Thornwick. Update it with every milestone commit. The browser prototype at the repo root
is unchanged and still deploys from `main`; all Unity work lives in `unity/` (see `unity/README.md`).

_Last updated: 2026-11-16 (Claude Code)._

## Branches

| Branch | Purpose | Head |
|---|---|---|
| `main` | Browser prototype (JavaScript). Untouched by the migration. | `4d99fca` |
| `unity-migration` | Integration branch for the Unity port. Merge feature branches here through pull requests. | `5dccc00` |
| `claude/settlement-port` | Claude Code's working branch. Open as [PR #1](https://github.com/zacharypanderson24-lgtm/thornwick/pull/1) into `unity-migration` (not merged yet). | see the milestone log below |

ChatGPT and Claude Code each work on their own branch and merge into `unity-migration` through pull requests.
`claude/settlement-port` is **ahead of `unity-migration`**: until PR #1 is merged, branches cut from `unity-migration` will not have the port.

## Open pull requests

| PR | From → into | Status |
|---|---|---|
| [#1 Unity port: playable settlement with zone growth](https://github.com/zacharypanderson24-lgtm/thornwick/pull/1) | `claude/settlement-port` → `unity-migration` | open, awaiting review and merge |

## Milestone log

| Commit | Branch | What |
|---|---|---|
| `5dccc00` | `unity-migration` | Empty Unity 6000.6.9f1 project (URP 2D template) in `unity/`, Unity `.gitignore` and `.gitattributes`. |
| `ed4e32f` | `claude/settlement-port` | First playable settlement: C# sim (terrain, placement, roads, zones, workforce, daily economy), isometric renderer with rotation, camera, HUD, art importer, parity tests (10). |
| `a8de069` | `claude/settlement-port` | Zone development ported (`sim_develop.js`): homes, farms, markets, lodges, quarries and mines grow on zones; 2x2 lots; demand HUD; development parity test (11 tests). |
| `13e9157` | `claude/settlement-port` | `unity/README.md` updated for the zone tools and 2x2 placeholders. |
| `c739d7b` | `claude/settlement-port` | Adds this progress file. |
| _this commit_ | `claude/settlement-port` | Records PR #1 in this file. |

## Implemented and tested

Tested means covered by `unity/Assets/Tests/EditMode/SimParityTests.cs` (11 tests, all passing on 2026-11-16) and play-tested in
the Editor (no console errors or warnings). Parity tests compare against the real browser code through
`unity/ParityReference/make_reference.js`.

| Feature | Parity-tested against the JS | Play-tested |
|---|---|---|
| Deterministic hash and value noise (`hash2`, `vnoise`) | yes, bit-exact | n/a |
| Terrain heights: rolling hills, basins, mountain ranges and massifs, slope relaxing | yes, every vertex, seeds 7 and 1234 | yes |
| Tiles: forests, tree species and variants, grove sizes, stone outcrops, ore, fertile soil, mountain elevation | yes, every tile, two seeds | yes |
| Starting settlement: Town Hall, road, two huts (hut looks), farm, door faces, service | yes | yes |
| Housing, jobs, idle and employed totals; workforce by trade priority | yes (start state and the 150-day development scenario) | yes, Workforce panel |
| Zone development: demand per zone kind, lot and site rules, daily hash noise, tie-breaks, 2x2 lots, wood waits | yes, 150 days, every building and demand value | yes, a scripted town grew huts, a large house, farms and a mine |
| Daily economy: food, eating, gold, trade, births and deaths, starvation, barracks training, storage cap | ported line by line; covered by the 1000-day stability test | yes |
| Placement, removal, road paths, lumber and mining zones, clearing trees and rocks | ported; exercised by the development and woodcutter tests | yes |
| Woodcutters fell marked trees (4 chops per tree), stumps rot, foresters plant saplings | woodcutter test (approximated crews, see below) | yes |
| Finite stone and ore (quarries dig hills down, mines empty ore tiles) | ported, not separately tested yet | partly |
| Isometric 2:1 rendering: ground mesh draped over heights with slope shading, snow peaks, roads, zone tints, depth-sorted sprites | n/a | yes |
| 4-way camera rotation (Q/E) with per-view building art, zoom ladder 0.25x-5x, pan, height-aware tile picking | n/a | yes |
| HUD (uGUI): resources and rates, speed, rotation, tools, tile info, demand strip, zone notes, Workforce priorities | n/a | yes |
| Art pipeline: `../assets` imported (magenta keyed out, ground tiles shrunk to 32x16), placeholders for missing art | n/a | yes |

## Not done yet (next candidates)

1. Walking work crews and townsfolk (`sim_crews.js`). Crews are approximated today by the same output per worker
   (0.8 logs, 0.7 stone, 0.55 ore a day) from the same marked tiles.
2. Saves (the browser uses localStorage `thornwick-save-1`; must load old saves).
3. Hills hiding objects behind them (all ground currently draws before all objects).
4. Info overlay (V: fertility and deposits), banner tint, texture lab.
5. Art: ChatGPT art for the placeholder slots `farm`, `wood`, `sawmill`, `forester`, `quarry`, `mine`, `market`, `well`,
   `manor_1`, `manor_2`, `farm2`, `market2`, rocks, ore, stump, sapling (rules: `TEXTURE_SPEC.md`, `CHATGPT_START_HERE.md`).
6. Later milestones from the design: tech tree, fortified cities, armies, conquest map.

## How to verify

```
node unity/ParityReference/make_reference.js     # repo root; refresh after changing src/sim*.js
unity test unity --mode EditMode                 # with the project closed in the Editor
unity open unity                                 # then open Assets/Scenes/Settlement.unity and press Play
```
