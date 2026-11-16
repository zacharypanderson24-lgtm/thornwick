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
- Lumber chain: Lumber zone marks trees; woodcutters fell marked trees into logs (S.res.logs); sawmill turns logs into wood; Town Hall pit-saws at half yield; felled tile -> stump -> grass. Mining zone marks rock; quarries wear rockAmt 3 mountain -> 2 hill -> 1 rubble -> flat. Ore still works the old way (underground rework planned).
- Saves: localStorage 'thornwick-save-1', loadWorld migrates old saves (S.v2 flag). Never break loading old saves.

## How to test
- Sim tests: node scripts that load src/sim.js (see git history for tests, or write a short node script with `new Function('module','exports',code)`). Keep food topped up in long runs or the town starves.
- Visual: Playwright with /usr/bin/google-chrome (`--no-sandbox --allow-file-access-from-files`), page `file:///home/claude/thornwick-repo/index.html`, globals: setRot, setScale, centerOn, addBld, recalc, __game (S, zone, tick, place). Screenshot and look before claiming UI works. "Failed to load resource" console errors from asset probing are normal.
- Art review method: compare with the previous commit's sprite side by side at 4-6x with the footprint diamond overlaid, run the checker, render in game.

## Open work (update as it changes)
1. Workforce panel (workers per trade) + per-building worker counts so output scales with workers (DESIGN_TERRAIN sections 7-8).
2. Terrain beyond rock/forest, forest regrowth/foresters, underground ore + prospecting + mine levels.
3. World art mismatch: grass, trees, rocks, farms, market, well, mine, quarry, sawmill, stump, hill/mountain stages are still flat code-drawn sprites; need ChatGPT textures and slots. Grass: assets/grass_0.png pushed by Zach is a CORRUPT PNG (IDAT stream broken, no IEND), so the game rejects it and keeps the built-in grass (validGround in textures.js); needs a fresh export/upload, then grass_1..3. Ground files now load through fitToSlot (keys magenta, crops diamond to 32x16).
4. Hut textures pass the clipping checks but sit at 2 px margin (checker wants 3): relax or redo.
5. Starting huts overlap the Hall porch (move start layout down one tile).
6. Older goals: tech tree, conquest map with governors, villager sprites, threats.
