# Thornwick: terrain, stone, ore and lumber (future design)

Status: first pass built (felling zones, logs, sawmill, stumps, quarry stages: hill -> rubble -> flat). Mountains and hills reworked: smooth mountain ranges ringed by hills; mountains are permanent and solid, hills are buildable and roads cross them; quarries take stone from the exposed face of a mountain without wearing it down (section 2). Also built: workforce panel, per-building worker counts, foresters (replant Lumber-zone land with saplings). Not yet built: terrain types beyond rock/forest, underground ore. Written so ChatGPT (art) and Claude (code) work from the same plan.
Core principle: **resources come out of the land, and taking them changes the land.**

## 1. Terrain grid
Replace the scattered rock/ore/tree decorations with a real terrain layer. Each tile has a type and an amount.

| Type | Holds | Buildable | Changes into |
|---|---|---|---|
| grass | fertility | yes | (stays) |
| forest | tree density | no | stumps -> cleared grass |
| hill | stone | no (roads ok) | rubble -> quarry pit -> flat |
| mountain | permanent stone source, may cover ore | no, blocks roads | (never changes) |
| water / river | | no (bridges) | |

Provinces are generated from a seed with a different mix (river valley, mining mountain, deep forest), which gives governors and conquest different jobs.

## 2. Stone: from the surface
- **As built (finite, no regeneration):** stone and ore never grow back. Mountains are permanent and solid; each tile holds 120 stone, and quarrying only hollows it out (a cave that grows with the amount taken; worked-out tiles stay as hollow shells and open the tiles behind them). Hills hold 50 and rubble 40: they dig down level by level, hill -> rubble -> flat ground (buildable) -> pit depth 1, 2, 3, and a worked-out pit is permanent, cannot be built on or filled, and joined pits read as one large hole. Ore tiles hold 30 each and vanish when empty. Quarries and mines only take what is in the ground and pause while the stockpile is full.
- Mountains and hills are the stone source. Quarries work them.
- Each tile has a stone amount. As it drops the tile steps down: mountain -> hill -> rubble -> quarry pit -> flat, buildable ground.
- Quarrying trades defence (mountains are walls) for building space and stone.
- Spent pits can flood into ponds, be filled in, or stay as ruins.
- Mountains block roads and buildings until a pass is quarried or a tunnel is dug.

## 3. Ore: underground
- Ore is not visible on the surface. It lives in a separate underground grid (type + amount per tile).
- Revealed by prospecting (prospector building or tech). Mountains and hills are more likely to sit above deposits.
- Mines dig down through levels. Deeper = richer ore (iron, then gold, gems) but more cost and danger (timber supports, workers, cave-ins).
- An underground view toggle (like the Resources lens) shows the layer. Shafts and tunnels are placed there.
- Surface consequences: spoil heaps, thinned forest near mines. Worked-out mines leave holes that can become cellars or storage.

## 4. Lumber: designate, fell, process
Wood is a chain, not a counter.
1. **Designate:** the player paints a felling zone on forest tiles (like zoning).
2. **Fell:** lumber camps send workers who cut the designated trees over time, tile by tile. Dense forest = more logs per tile.
3. **Logs:** felled trees produce logs, stored at the camp/stockpile and carried by workers.
4. **Process:** a sawmill / woodworker turns logs into planks (lumber). Buildings use planks, roads and fences use logs, fuel and charcoal use logs.
5. **Land changes:** a felled tile becomes stumps, then cleared grass that can be zoned and built on. Cutting a forest creates buildable area.
6. **Regrowth:** foresters can replant. Unmanaged cleared land regrows only slowly, so the player chooses between expansion and sustainable woodland.
7. **Trade-offs:** forests give hunting/foraging, shelter, and cover from raiders. Clearing them opens ground but makes it easier to attack.

This also removes the early wood soft-lock: logs are always available from the starting forest by designating a zone.

## 5. The world visibly changes
Mountains shrink, quarry pits appear, spoil heaps pile up, forests thin and become fields. Thirty days in, the map should look different from day one.

## 6. Build order
1. Terrain grid + state changes (sim only, node-testable). Replace decoration tiles.
2. Lumber chain (felling zones, logs, sawmill, planks). Fixes the wood soft-lock.
3. Quarry/mountain stages.
4. Underground grid + prospecting + mines + underground view.
5. Province generation from seeds.
Terrain tile art (grass variants, forest density, stumps, hill, mountain stages, rubble, pit, spoil heap, river) is ordered right after step 1 so ChatGPT can start on it; tiles are 32x16 ground diamonds or taller for mountains, same palette as buildings.

## 7. Workforce panel (population by trade)
A panel (button next to Resources / Banner) showing how many citizens work in each industry.
- **Rows:** farming, fishing, timber (felling), woodworking (sawmill, carpenter), quarrying, masonry, mining, smithing, trade/market, soldiers, builders/labour, unemployed, children/elderly (not working).
- **Each row:** workers now / jobs available, plus a bar. Red when jobs are empty (understaffed), grey when workers are idle.
- **Totals:** employed, unemployed, soldiers, housing capacity, total population.
- **Click a row** to highlight the buildings that employ that trade on the map and list which are understaffed.
- **Controls (later):** priority sliders (more hands to timber than quarry), and governors can set quotas for their province.
- **Sim:** each job-giving building has a `trade` tag and a worker count. The panel is just a sum over buildings, so it is cheap and testable in node.

## 8. Workers set the pace
Output speed of every industry comes from how many workers it has, not from the building existing.
- Each building has a `workers` count (0..capacity). Output per day = workers x base rate x tool/tech multipliers. Half staffed = half speed; unstaffed = zero.
- **Felling:** each logger fells one tree-tile's worth every N days; more loggers = faster clearing and faster land gain.
- **Quarrying:** workers strip stone from a mountain tile; more workers = faster, and the mountain drops stage sooner.
- **Mining:** workers dig down; more workers = faster digging and ore output, but cave-in risk rises with depth and crowding unless supports (timber) are provided.
- **Processing:** sawmill and masonry output is capped by workers AND by input supply (logs, stone); idle workers show as grey in the Workforce panel.
- **Construction:** builders set the pace of placing buildings, so a big project needs a labour pool.
- **Soldiers:** more soldiers means a larger army but less labour, which is the central guns-vs-butter choice.
- **Allocation:** citizens fill jobs by priority (default: food first). The player can move priority sliders in the Workforce panel; unfilled jobs show red. Growth in population (housing + food) is the long-term way to raise total output.
- **Upgrades:** tech and better buildings raise per-worker output or building capacity, so a small skilled town can out-produce a big unskilled one.


## Update: landforms and map size
- Mountains are discrete landforms (2..7 tiles a side, average about 3x3). Height and stone reserve scale with size; hill skirts rise toward each mountain so no two start at the same height. Map is 64x64; old 40x40 saves are centred into it (growSave in sim.js). Zoom out to 0.5 to see most of the map.

## Update: height levels
- Terrain is a vertex height map in levels -8..8 (0 = base). Hills up to 3, mountains up to 8 and rare, in ranges when there are several. Roads climb slopes, buildings level their footprint. Mountains and steep slopes block movement (walkCost), so ranges create chokepoints and passes that should shape base layout once combat is in. Negative levels exist in the data and renderer; today only shallow basins use them (quarry pits are still separate tile sprites), so lakes and pits-as-height are future work.

## Update: workers set the pace (built) and mining decisions
- Built: per-building workers (b.workers, b.eff) filled by trade priority (default food first, player can reorder in the Workforce panel, S.prio saved). Output = base rate x b.eff. Sections 7 and 8 above are now real except builders, soldiers-vs-labour rows, children/elderly and quotas.
- Mining decisions (Zach): ore lives in a hidden underground layer (revealed by prospecting, shown by an underground view toggle); mines dig down through depth levels (deeper = richer ore, more cost); cave-ins are a real risk unless timber supports are supplied, and crowding raises the risk. Next build step: the underground grid + prospecting.
