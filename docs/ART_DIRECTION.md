# Crown & Hearth — Regional Art and Concept Reference Guide

**Purpose:** Convert the [game vision](GAME_VISION.md) and its ten-page illustrated concept presentation into actionable regional art direction. This guide is **not permission to replace the existing pixel-art sprites**. The pre-existing detailed asset requirements take precedence: [CHATGPT_START_HERE.md](../CHATGPT_START_HERE.md), [TEXTURE_SPEC.md](../TEXTURE_SPEC.md), [BUILDING_TILE_GUIDE.md](../BUILDING_TILE_GUIDE.md).

## Overall look

A historically inspired **broader Old World** with distinct material cultures, but a **single cohesive, crisp, low-resolution, isometric pixel-art gameplay style**. At village zoom the player should recognize a building's function and era from silhouette, roof, wall material and a small number of readable props. Use existing earthy palettes and outlines. Avoid photorealistic shading, blurred/anti-aliased pixels, stretched textures, implausibly oversized props or detailed illustrations pasted into the runtime.

The game's larger generated illustrations are **concept paintings, not implemented UI screenshots and not drop-in sprites**. They illustrate composition, geography, architecture and visual variety. The master illustrated source is the November 16, 2026 ten-page **Crown & Hearth Complete Game Vision** PDF. The editable content and its descriptions are in [GAME_VISION.md](GAME_VISION.md), especially sections 3, 6, 7 and 11.

## Architectural families to prototype, not all 16 × 5 combinations

1. **Temperate / northern timber:** dark beams, plaster or wattle-and-daub, thatch, timber shingles, later tiled and masonry details. Forest, grassland and some boreal adaptations.
2. **Mediterranean masonry:** stone/lime plaster, dry-climate rooflines, courtyard details, tile where locally available, restrained palettes.
3. **Arid mudbrick/adobe:** thick earthen walls, flat or low roofs, shade and courtyard arrangements, small openings; wells and storage visibly important.
4. **Humid tropical raised timber:** stilted foundations where flooding is relevant, lightweight timber walls, deep overhangs, ventilation and thatch/wood roof.
5. **Mountain stone-and-timber:** compact silhouettes, strong bases, stone, local timber, roofs adapted to precipitation, terraces and hard terrain.
6. **East Asian/subtropical timber family (future extension):** recognizable post-and-beam/courtyard or humid-climate forms, regionally suitable roof and timber materials; research specific historical references before implementation.

These are **reusable architectural families, not mandatory locked culture classes**. Actual forms depend on materials, era, geography, urban density, trade and local institutions. Avoid caricature or mixing decorative elements from incompatible cultures merely for variety.

## Five-era small home progression

All five eras retain the same house function and logical 1×1 scale where appropriate:

- **Era I (600–800):** simple timber-and-earth or locally available materials, straw/thatch, modest openings and no excessive trim.
- **Era II (800–1100):** better carpentry and base, neater roof, slightly improved window/door details; still humble.
- **Era III (1100–1300):** more specialized half-timber or masonry construction as appropriate, chimney, shutters and improved footing.
- **Era IV (1300–1450):** more durable wall finishes, consistent roofing and better fenestration; still a standard household dwelling.
- **Era V (1450–1600):** improved materials and finish, potentially tile or shaped masonry depending on region; not an oversized palace.

A player should read historical improvement without losing building footprint, scale or family identity. The five evolving cottage concepts generated in conversation demonstrate **style direction**, but they are not validated runtime-size textures or approved four-direction asset sets. Must be checked/reworked against the sprite spec before using in the game.

## Four-direction isometric art coherence

- Logical tile diamond **32×16** (2:1). Typical building canvases **1×1 32×48**, **2×2 64×88**, **3×3 96×128**.
- 1×1 houses are intentionally **narrower than 32 pixels** for grass spacing; preserve margins.
- Correct fixed-wall perspective when Q/E rotates: view 1 front-left/door visible, view 2 back-left, view 3 back-right, view 4 front-right. Windows, chimneys and props must stay on their real wall or roof position across views. Do not mirror doors or randomly move windows.
- Pixel-crisp and transparent alpha; no magenta contamination or fringe. Existing magenta background art remains supported.
- Props may extend past the footprint diamond **inside the actual picture canvas** as detailed in the existing guide.
- **Do not claim assets are game-ready** until they're at proper size, directionally coherent, correctly alpha-masked, and validated in the Unity scene.

## Environment and vegetation families

Design forests, grass density, rocky terrain and soil to reveal biome identity without overwhelming tile readability.

- Temperate: patches of oak, beech, birch, mixed grass; farms and hedges.
- Boreal: tall conifers, moss, sparse undergrowth, season-dependent snow.
- Steppe: low grass and sparse trees; horse pasture and open sightlines.
- Highland/alpine: heather, rock, scrub, treeline transitions, passes, snow peaks.
- Mediterranean: hardy shrubs, orchard plants, drought-dry grasses, hills.
- Arid/desert: exposed soil, rock, scattered hardy shrubs, cultivated oasis strips.
- Savanna: tall seasonal grasses, scattered trees and mixed grazing.
- Subtropical/tropical: richer green density, broad leaves, bamboo and palms where ecologically fitting, wet soils and terraces.
- Wetland: reeds, pools, muddy soil, wet-ground construction and river meanders.
- Tundra: low ground vegetation, exposed rock and seasonal snow.

**Water, rivers, shorelines, floodplains, oases and volcanic detail are overlays** on primary biomes. Do not create stark seams between ecology zones.

## Concept-board compositions (reference only)

The ten-page illustrated PDF uses these broad scenes to communicate intended atmosphere:

1. A **small river village** with crops, mills, workers and a founding settlement.
2. A **large strategic campaign map** with persistent towns, regions, roads and borders.
3. A **climate panorama** from humid tropical valleys through high mountains to arid country.
4. A **wet tropical/monsoon settlement** with raised timber homes and cultivated water-sensitive fields.
5. A **working rural economy** of farms, mills, workshops and roads.
6. **Regional buildings** with timber, adobe, masonry and floodplain styles.
7. **A palisaded town at a river crossing**, defending itself from approaching armies.
8. **A kingdom campaign** of fleets, ports, roads and advancing armies.
9. **Material and architectural variations** in masonry and desert settlements.
10. **Settlement-to-kingdom arc**, from early subsistence to strong cities and strategic wars.

When generating new visual references, describe the desired **scene, purpose, region, era, weather, built materials, and game-scale readability**. Concept art should never accidentally be committed as polished playable sprites.

## Practical next steps

Focus on replacing **existing missing placeholder art** only when a scene needs it; keep texture approval separate from broad design goals. Priorities noted in the project include a farm, woodcutter lodge, sawmill, forester, quarry, mine, market, well, larger homes/fields/markets, ore/rock, stump and sapling. Before a new regional art family, validate **one house in all four directions, on the actual 32×16 tile and in Unity**, then expand.

If art work conflicts with this guide and the detailed asset specification, **the detailed asset specification wins**.
