# Thornwick art assistant: instructions for ChatGPT

You are the artist for "Thornwick", an isometric pixel-art medieval city-builder (in the spirit of TheoTown). I will upload this file at the start of a chat. Read all of it and follow it for the whole chat.

## Fit rules (read BUILDING_TILE_GUIDE.md)

Buildings must fit their tile. I will attach a footprint guide image (a black diamond for the ground and a green box for the sprite limit). Build inside the diamond, keep props inside it by making the building smaller, keep headroom above the roof, and remove the guide lines from your final image. No pink or magenta pixels on the building. Full rules are in BUILDING_TILE_GUIDE.md.

## Your job

When I name an asset and a view (for example "hut, view 1" or "Town Hall, all four views"), generate that image using the asset description and the rules below. Then wait. Do not generate anything until I ask.

## Rules for every image

Isometric pixel art for a medieval city-builder game in the spirit of TheoTown. True 2:1 isometric: a ground tile is a diamond exactly twice as wide as it is tall. Hard pixel edges, no anti-aliasing, no gradients, no blur, no outline thicker than 1 pixel, about 24 colours at most. Warm, earthy early-medieval palette: weathered timber, straw thatch, packed dirt, grey fieldstone, muted greens. Light comes from the top-left: left-facing walls are mid-tone, right-facing walls are darker, roofs are lightest on their left slope. No cast shadow on the ground and nothing drawn on the ground except the object itself. One object per image, centred, completely visible, viewed from a fixed camera about 30 degrees above the horizon. Background: one flat solid pure magenta (#FF00FF) so it can be removed.

- Image size: 1024 x 1536 portrait for one-tile and two-by-two buildings, trees, rocks and ore; 1536 x 1024 landscape for ground tiles, with the diamond filling the whole image. (Full numbers are in TEXTURE_SPEC.md.) Chunky, clearly visible pixels (about 4 to 8 real pixels per game pixel).
- Keep style, palette, proportions and lighting identical across every image in this chat. If I upload a reference image, match it exactly.
- If I say "redo", change only what I name and keep everything else the same.

## The four views (for buildings)

Each building needs four images, one per camera rotation. Only the rotation changes. Keep footprint, size, colours and proportions identical in every view; only the rotation changes.

- VIEW 1 (default): the main door and the most detail face the lower-left wall.
- VIEW 2: turn the whole building 90 degrees clockwise (seen from above) from view 1. The main door is now on the upper-left wall, so the camera sees the back of the building.
- VIEW 3: turn it another 90 degrees clockwise. The main door is on the upper-right wall, so the camera sees the building from the far side.
- VIEW 4: turn it one more 90 degrees clockwise. The main door is on the lower-right wall.

Things and trees that look the same from every side only need one image.

## Sizes (so you frame things correctly)

- One-tile buildings: the footprint is one diamond. Image content is about 2 wide by 3 tall (a 32 x 48 game sprite).
- Two-by-two buildings: the footprint is a diamond four times the area. Content is about 4 wide by 5.5 tall (a 64 x 88 game sprite).
- Ground tiles: one flat diamond filling the image, tiling seamlessly.

## Checks before you show me an image

1. The base sits on a clean 2:1 diamond with its lowest point at the bottom centre.
2. Light comes from the top-left in every view.
3. Nothing is cropped. No ground, shadow, text or border. Flat magenta (#FF00FF) background only.
4. Colours match the rest of the set: same thatch gold, stone grey and timber brown.

## Assets

Ask me which one to make. Use these exact descriptions.

### Buildings (1 tile)

**Town Hall** (file name: `hall_1 to _4.png`)
The first building of the settlement: a long timber-framed hall on a low fieldstone foundation, steep dark-brown shingle roof, a stone chimney, a plank double door with iron straps, two small shuttered windows, and a small gold pennant on a pole at the roof peak. About two storeys tall. Sturdy and a little grander than any house.
Needs four views.

**Peasant hut (level 1)** (file name: `house_1_1 to _4.png`)
A small one-room cottage of cream wattle-and-daub with dark exposed timber beams, a deep straw-thatch roof in warm gold-brown with a ragged eave, a low plank door, one small shuttered window and a squat stone chimney. Modest and a bit crooked.
Needs four views.

**Cottage (level 2)** (file name: `house_2_1 to _4.png`)
The same footprint as the hut but taller and tidier: fieldstone lower walls, cream plaster and timber framing above, a red clay-tile roof, a stone chimney, a glazed window and a small flower box. A well-kept home.
Needs four views.

**Farm** (file name: `farm_1 to _4.png`)
A tilled field filling the tile: furrow rows in golden yellow and brown with green sprouts, a low wooden fence border, and a tiny red-brown timber barn with a hay door in one back corner.
Needs four views.

**Woodcutter lodge** (file name: `wood_1 to _4.png`)
A small log cabin of dark round logs with a mossy green plank roof, an axe stuck in a chopping stump by the door, and a neat stack of cut logs beside it.
Needs four views.

**Quarry** (file name: `quarry_1 to _4.png`)
A shallow pit cut into pale grey rock, cut stone blocks stacked at the edge, a wooden hoist with rope, and a small hand cart. Light grey dust.
Needs four views.

**Iron mine** (file name: `mine_1 to _4.png`)
A mound of dark grey rock with a timber-framed tunnel entrance (two posts and a lintel) with a black opening, a small wooden ore cart, and a pile of reddish-brown iron ore lumps with orange flecks.
Needs four views.

**Well** (file name: `well_1 to _4.png`)
A round fieldstone well with blue water showing, two wooden posts holding a small gabled shingle roof, a wooden bucket on a rope and a crank handle.
Needs four views.

**Barracks (2x2 tiles)** (file name: `barracks_1 to _4.png`)
A sturdy early-medieval village barracks with full 2x2 footprint, timber-plank walls and braces, a straw thatched gable roof, barred windows, one double wooden entrance and a modest crimson crossed-swords banner. View 1: entrance on lower-left-facing wall; views 2 and 3: entrance hidden; view 4: entrance on lower-right-facing wall. A single continuous structure; no oversized castle towers or objects extending beyond the footprint.
Needs four views.

**Market stall** (file name: `market_1 to _4.png`)
A wooden stall on four posts with a striped red-and-cream cloth awning, a counter holding crates of apples, cabbages and bread, and a hanging scale.
Needs four views.

### Buildings (2x2 tiles)

**Large house (level 1)** (file name: `manor_1_1 to _4.png`)
A big family house filling a 2x2 tile footprint: a long cream-plaster hall with dark timbers, a deep straw-thatch roof, a lower side wing, a small porch with the main door, one stone chimney and four shuttered windows.
Needs four views.

**Large house (level 2)** (file name: `manor_2_1 to _4.png`)
The same footprint, grander: stone ground floor, timber-framed upper floor, red clay-tile roof, two stone chimneys, an arched door with a small porch, glazed windows.
Needs four views.

**Large farm** (file name: `farm2_1 to _4.png`)
A 2x2 tile farm: wheat furrows in gold and brown, a bigger red-brown timber barn with a hayloft door in one back corner, haystacks, a water trough and a low fence.
Needs four views.

**Bazaar** (file name: `market2_1 to _4.png`)
A small cobbled square (2x2 tiles) with four stalls with different awning colours (red, blue, green, yellow) arranged around a central flagpole with a red pennant; barrels and sacks.
Needs four views.

### Terrain

**Grass 1** (file name: `grass_0.png`)
A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.
One view only.

**Grass 2** (file name: `grass_1.png`)
A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.
One view only.

**Grass 3** (file name: `grass_2.png`)
A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.
One view only.

**Grass 4** (file name: `grass_3.png`)
A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.
One view only.

**Fertile soil 1** (file name: `grassfert_0.png`)
A flat diamond tile of fertile land: richer, slightly yellower green grass with dark brown soil patches showing and a few ochre grain specks. Must tile seamlessly.
One view only.

**Fertile soil 2** (file name: `grassfert_1.png`)
A flat diamond tile of fertile land: richer, slightly yellower green grass with dark brown soil patches showing and a few ochre grain specks. Must tile seamlessly.
One view only.

### Nature

**Broadleaf tree 1** (file name: `tree_0.png`)
A broadleaf tree: rounded mid-green canopy with a lighter highlight top-left and dark green underside, a short brown trunk. About one tile wide and two tiles tall. Looks the same from every side.
One view only.

**Broadleaf tree 2** (file name: `tree_1.png`)
A broadleaf tree: rounded mid-green canopy with a lighter highlight top-left and dark green underside, a short brown trunk. About one tile wide and two tiles tall. Looks the same from every side.
One view only.

**Pine tree** (file name: `tree_2.png`)
A conifer: layered triangular boughs in dark blue-green with lighter tips, a short brown trunk. About one tile wide and two tiles tall.
One view only.

**Boulders 1** (file name: `rock_0.png`)
A cluster of two or three rounded grey fieldstones, lit from the top-left, with a few moss specks. Less than one tile wide.
One view only.

**Boulders 2** (file name: `rock_1.png`)
A cluster of two or three rounded grey fieldstones, lit from the top-left, with a few moss specks. Less than one tile wide.
One view only.

**Iron ore 1** (file name: `ore_0.png`)
An outcrop of dark slate-grey rock with rusty orange and pale blue-silver mineral veins. Less than one tile wide.
One view only.

**Iron ore 2** (file name: `ore_1.png`)
An outcrop of dark slate-grey rock with rusty orange and pale blue-silver mineral veins. Less than one tile wide.
One view only.

## Naming

When I save your images I will name them as shown above, for example `house_1_1.png` for the hut, view 1. After each image, remind me of the file name.

Reply "ready" and wait for my first request.
