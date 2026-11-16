# Building tile guide

How to make a building texture that fits its tile the first time. Every rule here comes from a mistake we actually hit. Read this before making a new building, and attach the matching guide image from `templates/`.

Related files: `TEXTURE_SPEC.md` (sizes, palette, views), `CHATGPT_START_HERE.md` (what to upload to ChatGPT), `templates/` (footprint guide images).

## 1. The picture you are making

A building texture is a picture of the object standing on a flat ground diamond.

| Building size | Game sprite | Ground diamond | Guide image to attach |
|---|---|---|---|
| 1 tile (hut, well, farm, mine, market stall, Town Hall) | 32 x 48 px | 32 x 16 px | `templates/footprint_1x1_guide.png` |
| 2 x 2 tiles (barracks, large house, bazaar, large farm) | 64 x 88 px | 64 x 32 px | `templates/footprint_2x2_guide.png` |
| 3 x 3 tiles (Town Hall, planned) | 96 x 128 px | 96 x 48 px | `templates/footprint_3x3_guide.png` |

The guide images are 1024 x 1536 with a magenta background. The black diamond is the ground the building covers. The green box marks the footprint's width: nothing may cross its left, right or bottom edge. The top of the box is only the usual height; a taller building may rise above it as long as it stays inside the picture. Ask ChatGPT to remove all guide lines from the final image.

The bottom corner of the diamond is at the bottom centre of the picture. Everything is measured from there.

## The four views: same order for every building (mandatory)

Every building, every size, always uses the same four views in the same order. The game picks a view by number to point the main door at the road, so one building in the wrong order faces the wrong way.

| File | Main door / front faces | What the camera sees |
|---|---|---|
| `name_1.png` | lower-left wall | the door (view 1, canonical source art) |
| `name_2.png` | upper-left wall | the back; the door is hidden |
| `name_3.png` | upper-right wall | the back; the door is hidden |
| `name_4.png` | lower-right wall | the door (view 4) |

- The order is a clockwise quarter-turn each time, seen from above: 1, then 2, then 3, then 4.
- The same number always means the same door side. `_1` is never "whichever view looks best", and `_4` always has the door on the lower-right wall.
- Draw view 1 first, then rotate the same building for the rest. Same footprint, same size, same colours, same props. Only the turn changes.
- This applies to all four-view buildings: huts, cottages, farms, markets, Town Hall, barracks, wells, mines, quarries and the large 2x2 buildings.
- Buildings without a door (well, quarry, farm) still have a main face: the side with the entrance, the cart, the barn door or the open pit. Put that main face where the door would go in each view.
- Test a new set by placing the building next to a road and turning the camera with Q and E. The main face should always point at the road.

## 2. The fit rules

1. **Walls sit inside the diamond.** The foot of every wall follows the diamond's lower-left and lower-right edges. The lowest point of the building touches the diamond's bottom corner.
2. **Props take room, so shrink the building.** Spears, racks, shields, barrels, crates, steps, stumps, hay, flower boxes and lanterns stand on the ground. They may sit outside the building and may extend past the diamond's edges onto the ground around it, as long as they stay inside the green box (never touching or crossing its left, right or bottom edge) and nothing is cut off. The walls and roof still sit on the diamond (rule 1); props are free to stand in front of or beside them. If something will not fit inside the green box, remove the least important prop. Never shrink the building so far that it looks smaller than its neighbours (see rule 8).
3. **Eaves may overhang a little, never past the left or right edge of the green box.** If a roof touches the left or right edge of the guide, make the whole building a bit smaller.
4. **Keep a small margin above the highest point.** Leave a few percent of empty space above the ridge, chimney or flag so nothing is cut off. The picture can be as tall as you need.
5. **Nothing floats.** Banners, flags and signs are attached to the building or stand on the ground (a standing pole may be outside the diamond but inside the green box).
6. **One door, same footprint in every view.** The view order is fixed for every building (see "The four views" above): 1 lower-left, 2 upper-left, 3 upper-right, 4 lower-right. Do not invent extra doors, and do not let the building grow or shrink between views. Props rotate with the building.
7. **Same height in every view.** Wall height, roof height, stone base height and chimney position (on the same roof slope) match across all four views.
8. **Match the neighbours.** A one-tile building should be about as wide as the diamond (32 px game width). A larger building (Town Hall, barracks) is taller and grander, not just wider. A 2x2 building fills its 64 px diamond.

## Building height and overlap

**There is no height limit.** Like TheoTown, a tall building rises up the screen and covers whatever is behind it. That is normal and wanted: a tower, a keep or a cathedral should block the buildings behind it. Overlap is not a fit error.

### What is fixed and what is free

| | Fixed | Free |
|---|---|---|
| **Footprint** | The ground diamond: 32 px wide (1 tile), 64 (2x2), 96 (3x3). The picture's width is set by it, and the base sits inside the diamond. | |
| **Height** | | Any height. The sprite is anchored at the bottom corner of the diamond and grows upward. Make the picture as tall as the building needs. |

The game draws the picture as tall as it is. A 32 px wide house can be 48 px tall or 120 px tall; a 96 px wide Hall can be 128 or 250. Only the width has to match the footprint, so keep the width at 32, 64 or 96 game pixels (in a larger picture, the building's base should span the diamond's full width).

### How overlap works

- The map is drawn back to front. A building is drawn once, at its front tile, so a building in front always covers one behind it.
- One step straight behind a building moves a tile 16 px up the screen, so a building 64 px tall hides about four tiles directly behind it.
- The picture must include everything on the building: roof, tower, flag, chimney, steps, props. Anything outside the footprint's width or above the diamond is part of the picture, not a separate object.

### What to keep right even though height is free

- **Footprint still holds.** Walls stay on the diamond; steps and ground props may spill past it but stay inside the green box. Only height is free; the wall base is not.
- **Same height in all four views** of one building. Wall height, roof height and tower position must match when you rotate.
- **Tall things need the right base.** A tall tower still stands on its own tiles, not on the ones behind it. If a keep is four times taller than its footprint, it should look like a tall building on that footprint, not a poster.
- **Scale ratios.** Keep walls, doors and windows the same size in game pixels on every building, so a taller building has more storeys, not bigger doors. A door is about 8 to 10 px tall at game size.
- **Hierarchy is optional advice, not a rule.** The Town Hall and barracks read best when they are taller than huts, but a church spire or a keep may be much taller than both.

### Optional tips (not limits)

- If you want a door or a road to stay visible behind a tall building, keep the front of the building low (porch, steps) and put the tall part at the back. This is a design choice, not a requirement.
- Check how a tall building looks next to a hut and the Hall in the game before committing.

### Current reference heights

For reference only: huts 32 to 36 px, barracks about 54, Town Hall about 84 to 91.

### Test before you commit

1. Put the building next to a copy of itself on a street: side by side, diagonal, and one behind another.
2. Put it next to a hut and the Town Hall.
3. Turn the camera with Q and E and check all four views.

## 3. Pixel clean-up rules

- **No pink, magenta or purple pixels on the building.** They are leftover background colour on the edge. Recolour them to the nearest wall, thatch or stone colour.
- **Hard edges only.** Every pixel is fully the building or fully background. No soft, blurred or semi-transparent edge.
- **Simple door sides.** The door, lantern and weapon rack should read as separate shapes at game size. Use bigger, simpler shapes and fewer tiny details; the picture is shrunk by 8 to 32 times.
- **No ground, no shadow, no text, no border.** The ground is drawn by the game.

## 4. Mistakes we have already made

| Mistake | What it looked like | Fix |
|---|---|---|
| Pink fringe | Purple-pink pixels along the thatch and stone base | Rule 3 pixel clean-up. If it keeps happening, switch the background to flat green #00FF00 and tell us, and we'll remove that colour in the game |
| Door side too busy | Door, rack and lantern blurred together | Fewer, bigger shapes (section 3) |
| Reads like a barn | Barracks had only a small banner | Add the military cues: big banner, stone base, weapon rack, lantern |
| Town Hall smaller than the huts | The Hall sprite is still a small placeholder | The Hall must be taller and grander than a hut: see rule 8 |
| Roof eaves at the image edge | Huts touch the left and right edges of their 32 px frame | Do not scale huts up. If you add anything, shrink the building |
| Props outside the green box | Items cut off or past the tile width | Rule 2 |

## 5. Making a new building

1. Upload `CHATGPT_START_HERE.md` to a new chat.
2. Attach the right guide image from `templates/` and one existing finished building as a style reference.
3. Ask for view 1 of the building, and say: "Fit it inside the black diamond and the green box, remove the guides, flat magenta background."
4. Check view 1 against the checklist below. Then make views 2, 3 and 4 as quarter-turns of the same building.
5. Save as `name_1.png` to `name_4.png` and add them to `assets/`.

## 6. Checklist before you accept a picture

- [ ] The lowest point touches the diamond's bottom corner at the bottom centre.
- [ ] Every wall foot follows the diamond edges; nothing stands outside the diamond.
- [ ] Props and steps are inside the green box (they may be outside the diamond).
- [ ] Footprint width is right (32, 64 or 96 game px) and the height is whatever the building needs. All four views have the same height.
- [ ] Roof eaves do not touch the left or right edge of the green box; nothing is cut off at the top of the picture.
- [ ] No pink, magenta or purple pixels on the building.
- [ ] Hard edges, no soft pixels.
- [ ] One door only; on the lower-left wall in `_1`, upper-left in `_2`, upper-right in `_3`, lower-right in `_4` (visible in 1 and 4, hidden in 2 and 3).
- [ ] Files are numbered in this exact order, so the same number means the same door side as every other building.
- [ ] All four views have the same footprint, wall height and roof height.
- [ ] Colours match the other buildings.
- [ ] Light comes from the top-left.

## 7. Check it before you commit

Ask Claude to run the overlay check on new files. It draws the diamond over each view and measures the width, height and bottom corner, so problems show up before they are in the game.


## Automatic check

Before pushing new textures, run `python3 tools/check_textures.py` from the repo root. It fails on: wrong width, anything touching the left/right/bottom edge (cut off), a base more than 2 px off the footprint diamond, pink fringe; and warns on white speckle in the stone base and views of different width. Fix every FAIL before the art goes in.
