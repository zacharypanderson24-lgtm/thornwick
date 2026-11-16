# Thornwick art brief (early game)

To start a ChatGPT chat, upload `CHATGPT_START_HERE.md` instead; it contains everything below in a form ChatGPT can follow.

Everything below is what each thing should look like in the first era: a poor, early-medieval farming village. Timber, thatch, packed earth and fieldstone. Nothing gleams yet.

## How to use this

1. Open the game, press Textures, pick a slot, press Copy prompt, paste it into ChatGPT.
2. Ask for a square image, 1024 x 1024 or larger. Use a transparent background (real PNG alpha); a flat magenta background is still accepted as a fallback and the game removes it.
3. Drop the picture on the matching view in the lab. Turn the camera with Q and E to check it.
4. When you are happy, press Save PNG and keep the files. Putting them in the assets folder next to index.html (named like `house_1_1.png`) makes them load for everyone, with no lab needed.

## File names

Buildings with four views: `name_1.png` to `name_4.png`. Everything else: `name.png` (ground and nature use the slot name, for example `grass_0.png`, `tree_2.png`).

## Style bible (goes in every prompt)

Isometric pixel art for a medieval city-builder game in the spirit of TheoTown. True 2:1 isometric: a ground tile is a diamond exactly twice as wide as it is tall. Hard pixel edges, no anti-aliasing, no gradients, no blur, no outline thicker than 1 pixel, about 24 colours at most. Warm, earthy early-medieval palette: weathered timber, straw thatch, packed dirt, grey fieldstone, muted greens. Light comes from the top-left: left-facing walls are mid-tone, right-facing walls are darker, roofs are lightest on their left slope. No cast shadow on the ground and nothing drawn on the ground except the object itself. One object per image, centred, completely visible, viewed from a fixed camera about 30 degrees above the horizon. Background: fully transparent (real PNG alpha). If you truly cannot produce transparency, flat solid pure magenta (#FF00FF) is still accepted as a fallback and the game removes it.

## The four views

View 1 is the default camera. Each next view turns the building 90 degrees clockwise seen from above, which is what the Q and E keys show.

- VIEW 1 (default): the main door and the most detail face the lower-left wall.
- VIEW 2: turn the whole building 90 degrees clockwise (seen from above) from view 1. The main door is now on the upper-left wall, so the camera sees the back of the building.
- VIEW 3: turn it another 90 degrees clockwise. The main door is on the upper-right wall, so the camera sees the building from the far side.
- VIEW 4: turn it one more 90 degrees clockwise. The main door is on the lower-right wall.
- Keep footprint, size, colours and proportions identical in every view; only the rotation changes.

## Sizes

One-tile things end up 32 x 48 px, two-tile things 64 x 88 px, ground tiles 32 x 48 with the diamond in the bottom 16 rows. Larger pictures are scaled down for you. Pixel art survives scaling best if you ask ChatGPT for very large, clean pixels (about 4 to 8 real pixels per game pixel).

## Things to check before you accept a picture

- The base is a clean diamond that sits on the tile, with the lowest point at the bottom centre.
- The light is on the left, in all four views.
- Nothing is cropped, and there is no ground or shadow drawn around it.
- Colours match the other pieces: same thatch gold, same stone grey, same timber brown.

## Buildings (1 tile)

### Town Hall (`hall`)

The first building of the settlement: a long timber-framed hall on a low fieldstone foundation, steep dark-brown shingle roof, a stone chimney, a plank double door with iron straps, two small shuttered windows, and a small gold pennant on a pole at the roof peak. About two storeys tall. Sturdy and a little grander than any house.

Final size 32 x 48 px, four views.

### Peasant hut (level 1) (`house_1`)

A small one-room cottage of cream wattle-and-daub with dark exposed timber beams, a deep straw-thatch roof in warm gold-brown with a ragged eave, a low plank door, one small shuttered window and a squat stone chimney. Modest and a bit crooked.

Final size 32 x 48 px, four views.

### Cottage (level 2) (`house_2`)

The same footprint as the hut but taller and tidier: fieldstone lower walls, cream plaster and timber framing above, a red clay-tile roof, a stone chimney, a glazed window and a small flower box. A well-kept home.

Final size 32 x 48 px, four views.

### Farm (`farm`)

A tilled field filling the tile: furrow rows in golden yellow and brown with green sprouts, a low wooden fence border, and a tiny red-brown timber barn with a hay door in one back corner.

Final size 32 x 48 px, four views.

### Woodcutter lodge (`wood`)

A small log cabin of dark round logs with a mossy green plank roof, an axe stuck in a chopping stump by the door, and a neat stack of cut logs beside it.

Final size 32 x 48 px, four views.

### Quarry (`quarry`)

A shallow pit cut into pale grey rock, cut stone blocks stacked at the edge, a wooden hoist with rope, and a small hand cart. Light grey dust.

Final size 32 x 48 px, four views.

### Iron mine (`mine`)

A mound of dark grey rock with a timber-framed tunnel entrance (two posts and a lintel) with a black opening, a small wooden ore cart, and a pile of reddish-brown iron ore lumps with orange flecks.

Final size 32 x 48 px, four views.

### Well (`well`)

A round fieldstone well with blue water showing, two wooden posts holding a small gabled shingle roof, a wooden bucket on a rope and a crank handle.

Final size 32 x 48 px, four views.

### Barracks (`barracks`)

A squat fortified building of grey fieldstone with a flat crenellated top, four small corner turrets, a heavy arched wooden door, red banners on the walls, a central wooden watch post with a red flag, and a weapon rack beside the door.

Final size 32 x 48 px, four views.

### Market stall (`market`)

A wooden stall on four posts with a striped red-and-cream cloth awning, a counter holding crates of apples, cabbages and bread, and a hanging scale.

Final size 32 x 48 px, four views.

## Buildings (2x2 tiles)

### Large house (level 1) (`manor_1`)

A big family house filling a 2x2 tile footprint: a long cream-plaster hall with dark timbers, a deep straw-thatch roof, a lower side wing, a small porch with the main door, one stone chimney and four shuttered windows.

Final size 64 x 88 px, four views.

### Large house (level 2) (`manor_2`)

The same footprint, grander: stone ground floor, timber-framed upper floor, red clay-tile roof, two stone chimneys, an arched door with a small porch, glazed windows.

Final size 64 x 88 px, four views.

### Large farm (`farm2`)

A 2x2 tile farm: wheat furrows in gold and brown, a bigger red-brown timber barn with a hayloft door in one back corner, haystacks, a water trough and a low fence.

Final size 64 x 88 px, four views.

### Bazaar (`market2`)

A small cobbled square (2x2 tiles) with four stalls with different awning colours (red, blue, green, yellow) arranged around a central flagpole with a red pennant; barrels and sacks.

Final size 64 x 88 px, four views.

## Terrain

### Grass 1 (`grass_0`)

A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.

Final size 32 x 48 px, one view.

### Grass 2 (`grass_1`)

A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.

Final size 32 x 48 px, one view.

### Grass 3 (`grass_2`)

A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.

Final size 32 x 48 px, one view.

### Grass 4 (`grass_3`)

A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.

Final size 32 x 48 px, one view.

### Fertile soil 1 (`grassfert_0`)

A flat diamond tile of fertile land: richer, slightly yellower green grass with dark brown soil patches showing and a few ochre grain specks. Must tile seamlessly.

Final size 32 x 48 px, one view.

### Fertile soil 2 (`grassfert_1`)

A flat diamond tile of fertile land: richer, slightly yellower green grass with dark brown soil patches showing and a few ochre grain specks. Must tile seamlessly.

Final size 32 x 48 px, one view.

## Nature

### Broadleaf tree 1 (`tree_0`)

A broadleaf tree: rounded mid-green canopy with a lighter highlight top-left and dark green underside, a short brown trunk. About one tile wide and two tiles tall. Looks the same from every side.

Final size 32 x 48 px, one view.

### Broadleaf tree 2 (`tree_1`)

A broadleaf tree: rounded mid-green canopy with a lighter highlight top-left and dark green underside, a short brown trunk. About one tile wide and two tiles tall. Looks the same from every side.

Final size 32 x 48 px, one view.

### Pine tree (`tree_2`)

A conifer: layered triangular boughs in dark blue-green with lighter tips, a short brown trunk. About one tile wide and two tiles tall.

Final size 32 x 48 px, one view.

### Boulders 1 (`rock_0`)

A cluster of two or three rounded grey fieldstones, lit from the top-left, with a few moss specks. Less than one tile wide.

Final size 32 x 48 px, one view.

### Boulders 2 (`rock_1`)

A cluster of two or three rounded grey fieldstones, lit from the top-left, with a few moss specks. Less than one tile wide.

Final size 32 x 48 px, one view.

### Iron ore 1 (`ore_0`)

An outcrop of dark slate-grey rock with rusty orange and pale blue-silver mineral veins. Less than one tile wide.

Final size 32 x 48 px, one view.

### Iron ore 2 (`ore_1`)

An outcrop of dark slate-grey rock with rusty orange and pale blue-silver mineral veins. Less than one tile wide.

Final size 32 x 48 px, one view.

## Not in the lab yet

Roads and the zone colour overlays are still drawn by the game. Later eras (stone keeps, tiled roofs, early gunpowder) get their own slots when they exist.
