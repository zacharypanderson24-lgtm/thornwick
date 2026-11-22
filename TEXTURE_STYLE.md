# Thornwick texture style

Use this guide when making or reviewing art for the current Unity game. The [building tile guide](BUILDING_TILE_GUIDE.md) defines footprint and rotation, and the [texture spec](TEXTURE_SPEC.md) describes source canvases and the shared palette.

## Buildings and materials

- Use a grounded early-medieval village palette. Match the standard house and Town Hall with chalky off-white plaster, orderly dark-brown timber frames, warm straw thatch, and subdued gray-brown fieldstone. Use these buildings as the style reference for lodges and workshops. [House](assets/house_1_1.png) · [Town Hall](assets/hall_1.png)
- Give the front and visible sides equal care. Use joined timber braces, posts and pegs, recessed windows, stepped thresholds, and doors with visible boards, hinges or a latch. Draw a chimney emerging through the roof where the building has one. Carry coherent medieval framing around the back of the Town Hall.
- Show depth through value and construction. Light falls from the upper left. Give the two faces distinct values, place a darker band under the eaves, and use a few bold thatch courses with broken edges. Put detail where materials meet or where players need to recognize the building at a glance.
- Make occupations recognizable by their silhouette and equipment. A farmhouse can show a barn door and harvest gear. A woodcutter's lodge can show split wood and tools, and a hunting lodge can show a rack, hides or hunting gear. The manually driven sawmill reads through its sawing apparatus and log platform. Use timber framing on the reverse face as well.
- Keep the silo's storage volume obvious, the mine entrance sunk into rock, the barracks visibly military, and the smithery identifiable by its forge. Give the market a clear cluster of stalls within its footprint. Keep the well low and compact so it sits comfortably among houses.
- Keep settlement banners prominent with room for both color and symbol. For a tintable banner, check the mask used by [BannerTint](unity/Assets/Scripts/Game/BannerTint.cs) and verify a color swap in Unity.

## Scale and rotation

- Use the 2:1 isometric grid. A tile is 32×16 game pixels. Ground-level walls and supports sit on their allocated diamond, with the front corner anchored at bottom center. Keep stairs, racks, crates, and other props within the footprint's sprite width. Preserve the actual gameplay placement size when replacing existing art.
- Use the full 2×2 allotment for the larger work buildings. The well and watchtower use one tile, and the Town Hall uses 3×3. The watchtower's timber deck, ladder and low thatch roof make it read at roughly 1.5 times palisade height. Palisades use dark timber, uneven sharpened tips, and visible pegged cross rails. [Watchtower](unity/Assets/Resources/Art/watchtower_1.png) · [Palisade renderer](unity/Assets/Scripts/Game/WallArt.cs)
- Draw the canonical `_1` view first, then rotate the same building through `_2`, `_3`, and `_4`. Keep proportions, footprint, and height consistent. A door, window, chimney, ladder, or prop belongs to a fixed side. Check the side seen from each turn against the wall mapping in the [building tile guide](BUILDING_TILE_GUIDE.md).
- Use real PNG alpha with hard, fully opaque or fully transparent pixels. Keep edges sharp and use coherent clusters of pixels for detail. Export gameplay assets with the correct framing and pivot. Large draft canvases are optional when authoring native-size sprites.

## Terrain, trees, and characters

- Keep grass and fertile soil low-contrast so buildings and people stand out. Keep the edge band quiet and check the tile repeated next to itself and neighboring variants. Trees can have distinct crowns, branch shapes, and trunks while sharing muted forest colors. Read the rock face, ore veins, and mountain planes through larger shaded clusters, keeping resource signs visible at game zoom.
- Give raiders, soldiers, workers, and civilians the same compact body proportions. The current Unity character art uses 28×24 sprite cells at 64 pixels per unit, with a shared feet pivot at (14, 3) from the bottom left. Standing figures read about 17 screen pixels high at the default 2× zoom. Use faction colors and weapon shapes for soldiers, trade tools and loads for workers, and plain tunics for jobless civilians. [Character sheets](unity/Assets/Resources/Art/Battle) · [Generator](tools/render-thornwick-battle.cjs) · [Runtime framing](unity/Assets/Scripts/Game/BattleArt.cs)
- Keep pivots stable across directions and animation frames. Verify walking, working, carrying, and combat in motion. Put the strongest contrast on the action or tool that needs to read at that scale.

## Workflow

1. Open the current Unity settlement and compare a first draft beside the Town Hall, houses, and one similar-sized building. Review at 1× and 2× before zooming in for detail. If the asset is hard to recognize, revise the silhouette and materials first.
2. Check all four rotations and the actual placement footprint. Verify anchors, seams on joined terrain or palisades, overlap with neighboring objects, and legibility in the build menu where applicable.
3. Put shared source art in `assets/` and reimport it with **Crown & Hearth → Import Prototype Art**. Keep Unity-only sprites in `unity/Assets/Resources/Art/` and retain their Unity-generated metadata. Use the existing renderer for procedural assets and the existing sheet catalogue for animated characters. [Unity art workflow](unity/README.md#banner-and-textures)
4. Run the applicable texture checks for changed shared assets in `tools/`. Verify the imported sprite in Unity Play Mode, take a game-scale preview, and note which views and states were checked.
