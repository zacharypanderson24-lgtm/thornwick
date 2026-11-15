# Thornwick: terrain, stone, ore and lumber (future design)

Status: idea stage, not built. Written so ChatGPT (art) and Claude (code) work from the same plan.
Core principle: **resources come out of the land, and taking them changes the land.**

## 1. Terrain grid
Replace the scattered rock/ore/tree decorations with a real terrain layer. Each tile has a type and an amount.

| Type | Holds | Buildable | Changes into |
|---|---|---|---|
| grass | fertility | yes | (stays) |
| forest | tree density | no | stumps -> cleared grass |
| hill | stone | no (roads ok) | rubble -> quarry pit -> flat |
| mountain | lots of stone, may cover ore | no, blocks roads | hill -> rubble -> pit -> flat |
| water / river | | no (bridges) | |

Provinces are generated from a seed with a different mix (river valley, mining mountain, deep forest), which gives governors and conquest different jobs.

## 2. Stone: from the surface
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
