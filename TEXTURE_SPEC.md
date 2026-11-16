# Thornwick texture spec

The exact numbers for making textures with ChatGPT (or anything else). Game sizes are tiny; ChatGPT draws big and the game shrinks the picture for you, so the numbers below tell you what to ask for and what the game will turn it into.

> Before making a building, read `BUILDING_TILE_GUIDE.md` and attach a guide from `templates/`.

## 1. The grid

- The map is isometric 2:1. One ground tile is a **diamond 32 pixels wide and 16 pixels tall** (game pixels).
- Everything is drawn from the **bottom corner** of its footprint. The lowest point of the diamond sits at the bottom centre of the image.
- Light comes from the **top-left**. Left walls mid-tone, right walls darker, roofs lightest on the left slope. No cast shadows on the ground.

## 2. Final sizes in the game

| Kind | Final sprite | Footprint diamond | Extra height above diamond |
|---|---|---|---|
| One-tile building, tree, rock, ore | **32 px wide, 48 px tall (buildings can be taller)** | 32 x 16, bottom of image | up to 32 px |
| Two-by-two building | **64 x 88 px** | 64 x 32, bottom of image | up to 56 px |
| Three-by-three building (Town Hall, planned) | **96 x 128 px** | 96 x 48, bottom of image | up to 80 px |
| Ground tile (grass, fertile soil) | **32 x 48 px** (diamond fills the bottom 32 x 16) | the whole tile | none |

Rules of thumb for one-tile buildings: the base should be almost the full 32 px width of the diamond, walls about 10 to 14 px tall, roof another 8 to 14 px. A tree is about 24 px wide and 40 px tall. Rocks and ore stay under 24 px wide.

## 3. What to ask ChatGPT for

ChatGPT cannot make an exact 32 x 48 image, and it does not need to. Ask for large pictures with the right shape and chunky pixels. The game crops to the object, scales it to fit, and snaps the edges.

| Asset | Ask for this image size | Why |
|---|---|---|
| One-tile building, tree, rock, ore | **1024 x 1536 (portrait)** | exactly 2:3, the same shape as 32 x 48 |
| Two-by-two building | **1024 x 1536 (portrait)**, object fills the width | game sprite is 8:11, close enough; the lab fits it |
| Ground tile | **2048 x 1024 (landscape, exactly 2:1)**, diamond corners touch the middle of all four edges | the diamond is 2:1, so the canvas must be 2:1 too. 1536 x 1024 is 3:2 and would give a diamond that is too tall |
| Four-view sheet (optional) | **1536 x 1536 (square), 2 x 2 grid** | each cell 768 x 768 |

Pixel size: aim for each game pixel to be about **8 to 32 real pixels** wide in ChatGPT's picture, with visible square blocks and no soft edges. If ChatGPT gives smooth or blurry art, tell it: "redo with large, clearly visible square pixels, hard edges, no anti-aliasing".

Background: **flat pure magenta #FF00FF**, nothing else. No floor, no shadow, no border, no text.

### Ground tiles (grass, fertile soil)

- Canvas exactly **2048 x 1024**. The diamond's four corners touch the midpoints of the four edges; everything outside the diamond is flat magenta #FF00FF.
- **Pixel size for ground tiles:** the tile is only 32 game pixels wide, so at 2048 px wide one game pixel is **64 real pixels**. Draw every tuft, flower and soil patch as solid square blocks of 64 to 128 real pixels, or they shrink to nothing in the game. The game shrinks ground tiles with hard-edged sampling (no blending) so palette colours stay exact.
- The game crops to the diamond and scales it to 32 px wide, so a 3:2 diamond would come out 21 px tall instead of 16 and overlap its neighbours.
- Ground tiles repeat across the whole map: keep contrast low, keep the outer band along the diamond edges (about 2 game pixels) plain base colour, and draw no outline, rim or shading at the edge.
- `python3 tools/check_textures.py` checks ground tiles too (`grass_N.png`, `grassfert_N.png`).

## 4. Style

- Early-medieval farming village: timber, thatch, packed earth, fieldstone. Nothing gleams.
- At most about **24 colours** per asset; share the same colours across assets.
- One-pixel dark outline at most, or none. No gradients, no glow, no blur.
- Hard pixel edges everywhere.

### Shared palette (use these colours, or very close)

| Use | Colour |
|---|---|
| Grass | #5F9E48 base, #4F8C3B dark tuft, #79B65B light tuft |
| Thatch | #C9A04A light, #A8802F mid, #7C5A22 dark |
| Timber | #8A5A34 mid, #6E4526 dark, #A0703C light |
| Cream plaster | #E8D9B0 left, #CDBB8C right |
| Fieldstone | #858A9C left, #666B7C right, #4D5262 dark |
| Clay roof tile | #B5533C light, #8F3F2E dark |
| Iron ore flecks | #E0873A |
| Water (well) | #4C8FD6 |

These are the colours the code-drawn placeholders use, so ChatGPT art will sit comfortably next to them.

## 5. The four views (buildings only)

Each building needs four images, one per camera rotation. Only the rotation changes; size, footprint and colours stay identical.

1. View 1: main door faces the lower-left wall.
2. View 2: rotate the building 90 degrees clockwise (seen from above). The door is on the upper-left wall, so you see the back.
3. View 3: another 90 degrees clockwise. Door on the upper-right wall.
4. View 4: another 90 degrees clockwise. Door on the lower-right wall.

Trees, rocks, ore and ground tiles need one image only.


### Mandatory entrance/orientation convention

> The view order is the same for every building: `_1` lower-left, `_2` upper-left, `_3` upper-right, `_4` lower-right. See `BUILDING_TILE_GUIDE.md`.

- **View 1 is the canonical source artwork:** the single main entrance is on the visible lower-left-facing wall. Always draw this view first and rotate the same object for views 2, 3 and 4, at successive clockwise quarter-turns.
- **View 2:** main entrance is on the far upper-left-facing side, hidden from the fixed camera.
- **View 3:** main entrance is on the far upper-right-facing side, hidden from the fixed camera.
- **View 4:** main entrance is on the visible lower-right-facing wall.
- Thus a building with just one door shows that door in **exactly two** of its four views: 1 and 4. Do not invent additional doors or change the layout between rotations.
- A building's **world-facing** direction is independent of the camera rotation. On placement, the game should orient the canonical door toward an adjacent road, preserving that world-facing direction across camera rotations, reloads and saves. If multiple roads touch the building, preserve its previous road-facing side when possible; if none touches, use the canonical default.
- Every rotation must preserve the same footprint and fit inside its tile boundary, including the door, stairs, walls, banners and any attachments.

## 6. File names

Buildings: `name_1.png` to `name_4.png`, for example `house_1_1.png` (hut, view 1) to `house_1_4.png`. Everything else: `name.png`, for example `grass_0.png`, `tree_2.png`. The full list of 69 names is in `assets/README.txt`. Save as **PNG**. Larger images are fine.

## 7. Checklist before you accept a picture

- The base is a clean 2:1 diamond, lowest point at the bottom centre.
- Light on the left in all four views.
- Nothing cropped; one object only; flat magenta background.
- Palette matches the other assets.
- Pixels are large and square, with no soft edges.
- For buildings: the four views have the same footprint and height.
