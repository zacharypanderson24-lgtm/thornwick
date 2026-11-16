# Thornwick: notes for Claude (read this first in every new chat)

Medieval TheoTown-style pixel isometric city builder. Owner: Zach. Hosted from this repo on GitHub Pages and Vercel (https://thornwick-two.vercel.app/, auto-deploys on push to main). ChatGPT makes the art (rules in CHATGPT_START_HERE.md); Claude does the code and reviews the art.

## Layout and build
- Source: src/sim.js (pure game logic, node-testable), src/textures.js (texture slots, lab, banner tint), src/ui.js (render/input/HUD), src/template.html, src/build.py.
- Build: `cd src && python3 build.py && mv index.html ../index.html`. index.html at repo root is the built single file; always rebuild before committing source changes.
- Assets: assets/name_1..4.png (building views), see TEXTURE_SPEC.md and BUILDING_TILE_GUIDE.md. Art check: `python3 tools/check_textures.py` (buildings, plus ground tiles grass_N / grassfert_N).
- HUD: top bar is compact (resources show icon + number, hover for names); Banner/Textures/Drafting/Save/New live in the Menu dropdown; Goals is a one-line chip (click to expand); the left info panel only shows when hovering/pinning a tile or after pressing the ? button (welcome text shows once for brand-new players).
- Ground tile source art must be a 2048 x 1024 canvas (exactly 2:1) with the diamond corners on the edge midpoints; the loader scales by width, so a 3:2 diamond would be too tall. Edge band ~2 game px stays plain base colour so tiles blend.
- Design plan for terrain/lumber/ore/workforce: DESIGN_TERRAIN.md (status line at the top says what is built).
- Commit messages end with the attribution lines the session gives.

## Conventions that must not break
- Iso 2:1, tile diamond 32x16. Sprites: 1x1 32x48, 2x2 64x88, 3x3 96x128. Height is free. Hut (1x1) at most 28 px wide.
- View order for every building: _1 door lower-left, _2 upper-left, _3 upper-right, _4 lower-right. World face f: 0=+y,1=-x,2=-y,3=+x; sprite view = (ROT + b.face) % 4. 4-way camera rotation (Q/E).
- Buildings grow only on zone tiles touching a Hall-connected road; doors face the road. Town Hall is 3x3, barracks 2x2.
- Zone overlay is hidden under built tiles. Huts have sticky b.var (1..8) that never matches the 8 neighbouring houses.
- Lumber chain: Lumber zone marks trees; woodcutters fell marked trees into logs (S.res.logs); sawmill turns logs into wood; Town Hall pit-saws at half yield; felled tile -> stump -> grass. Mining zone marks rock. Stone and ore are FINITE and never regenerate: every rock tile has a reserve (S.dig = taken so far; mountain MOUNT_CAP 120, hill 50, rubble 40, DIG_U 10 per level), every ore tile holds ORE_CAP 30 (S.oreLeft), and a quarry/mine only takes what is really in the ground (mineRock/mineOre in sim.js; a full stockpile pauses them). Mountain (rockAmt 3) is permanent and solid (no buildings, zones or roads, no bulldozing): mining only HOLLOWS it (rockHollow 0..1 drawn as a growing cave) and when its reserve is gone it stays as a worked-out shell. Hills/rubble dig down level by level: hill -> rubble -> flat (buildable) -> pit 1..3 (rockLevel; pits are solid, cannot be built on, bulldozed or refilled, and joined pits render as one big hole). A mountain tile is only workable where it touches open ground or a worked-out mountain tile (rockOpen), so quarries cut inward. Mined-out ore tiles simply disappear. World gen (sim.js mountainSites/elevAt/genTile) places discrete mountains: sides 2..7 tiles (average ~3x3, never more than 7x7), taller the bigger they are (S.elev = height in sprite px, 14+ mountain, 4..14 hill), each standing on a skirt of hills that climbs toward it; a tile's stone reserve scales with its elevation (rockCap). Map is N=64 (START=32 is the Hall's centre); old 40x40 saves are centred into it by growSave() on load. Zoom ladder 0.5/1/2/3/4/5 (ZOOMS in ui.js); 0.5 shows nearly the whole map. Mountains are drawn as procedural faceted peaks sized from S.elev, snow on the high ones (mountainSprite/hillSprite in ui.js). Ore still works the old way (underground rework planned).
- Saves: localStorage 'thornwick-save-1', loadWorld migrates old saves (S.v2 flag). Never break loading old saves.

## How to test
- Sim tests: node scripts that load src/sim.js (see git history for tests, or write a short node script with `new Function('module','exports',code)`). Keep food topped up in long runs or the town starves.
- Visual: Playwright with /usr/bin/google-chrome (`--no-sandbox --allow-file-access-from-files`), page `file:///home/claude/thornwick-repo/index.html`, globals: setRot, setScale, centerOn, addBld, recalc, __game (S, zone, tick, place). Screenshot and look before claiming UI works. "Failed to load resource" console errors from asset probing are normal.
- Art review method: compare with the previous commit's sprite side by side at 4-6x with the footprint diamond overlaid, run the checker, render in game.

## Open work (update as it changes)
1. Workforce panel (workers per trade) + per-building worker counts so output scales with workers (DESIGN_TERRAIN sections 7-8).
2. Terrain beyond rock/forest, forest regrowth/foresters, underground ore + prospecting + mine levels.
3. World art mismatch: grass, trees, rocks, farms, market, well, mine, quarry, sawmill, stump, hill/mountain are procedural code-drawn peaks (fine for now, ChatGPT art later); the rest need ChatGPT textures and slots. Grass: grass_1..3 and grassfert_0..1 are in and load (calm, near-flat; fertile soil reads muddy olive). grass_0.png is 1536x1024 (3:2), so validGround rejects it and the built-in grass is used for that variant; needs a 2048x1024 redo. Ground files load through fitToSlot with hard-edged sampling (keys magenta, crops the diamond to 32x16, 64 real px = 1 game px).
4. Hut textures pass the clipping checks but sit at 2 px margin (checker wants 3): relax or redo.
5. Starting huts overlap the Hall porch (move start layout down one tile).
6. Older goals: tech tree, conquest map with governors, villager sprites, threats.
