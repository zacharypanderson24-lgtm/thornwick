# Development progress: Crown & Hearth (Unity migration)

Living log of the Unity port of Thornwick. Update it with every milestone commit. The browser prototype at the repo root
is unchanged and still deploys from `main`; all Unity work lives in `unity/` (see `unity/README.md`).

_Last updated: 2026-11-16 (Claude Code, work crews)._

## Branches

| Branch | Purpose | Head |
|---|---|---|
| `main` | Browser prototype (JavaScript). Untouched by the migration. | `4d99fca` |
| `unity-migration` | Integration branch for the Unity port. Merge feature branches here through pull requests. Contains the playable port since PR #1. | `22058e9` |
| `claude/port-work-crews` | Walking work crews and townsfolk (`sim_crews.js`), cut from `unity-migration`. Not merged yet. | see milestone log |
| `claude/settlement-port` | Claude Code's first working branch. [PR #1](https://github.com/zacharypanderson24-lgtm/thornwick/pull/1) merged into `unity-migration`. New work goes on one feature branch per task. | same as `unity-migration` |

ChatGPT and Claude Code each work on their own branch and merge into `unity-migration` through pull requests.
`unity-migration` now has everything from PR #1, so new branches (ChatGPT's included) should be cut from `unity-migration`.

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
| _this commit_ | `claude/port-work-crews` | Walking work crews and townsfolk ported (`World.Crews.cs`), drawn in Unity (`AgentRenderer.cs`); 300-day tick parity test (12 tests). |

## Implemented and tested

Tested means covered by `unity/Assets/Tests/EditMode/SimParityTests.cs` (12 tests, all passing on 2026-11-16 on `claude/port-work-crews`) and play-tested in
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

## Not done yet (next candidates)

1. Saves (the browser uses localStorage `thornwick-save-1`; must load old saves).
2. Hills hiding objects behind them (all ground currently draws before all objects).
3. Info overlay (V: fertility and deposits), banner tint, texture lab.
4. Art: ChatGPT art for the placeholder slots `farm`, `wood`, `sawmill`, `forester`, `quarry`, `mine`, `market`, `well`,
   `manor_1`, `manor_2`, `farm2`, `market2`, rocks, ore, stump, sapling (rules: `TEXTURE_SPEC.md`, `CHATGPT_START_HERE.md`).
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
