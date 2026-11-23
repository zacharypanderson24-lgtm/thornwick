# Approved art handoff for Claude

This commit adds the user's approved house/workshop/defense sprites, all 16 land biome sets, and still/ocean/river animation frames to the Unity project. The rejected charcoal clamp is deliberately excluded.

## Buildings and defenses

- Large level-1 house stays at **14** occupants; level 2 stays at 20.
- Smokehouse is **2x2** for new placement. Old saved 1x1 smokehouses retain their procedural legacy sprite.
- Manor levels 1/2, cottage level 2, storehouse and smokehouse have four native directional views.
- Original palisade, damaged palisade, gate states, level-2 palisade walkways and stairs are included. The new walkway retains the approved original log silhouette. Walkway orientations face toward the settlement hall.
- The current road/wall renderer displays open gates. Closed/broken gate art and stairs are supplied, but gate state and guard pathing still need connecting to the defense simulation. The walkway tool uses existing wall placement costs; upgrade balance needs review.

## Land biomes

- Resources/Art/Biomes contains 16 ground atlases and 338 vegetation/outcrop sprites. Each atlas has the original 11 surface cells followed by 8 biome cells. Roads and overlays keep their original atlas indices.
- BiomeArt binds each rendered World to its strategic map biome. GameController binds the home settlement after map generation and camps/towns when viewed. GroundRenderer and ObjectRenderer select the corresponding resources.
- Biome binding is derived from the campaign map rather than saved as new state. Existing terrain, resource counts and simulation are preserved. Existing tree positions/density therefore stay unchanged, including in deserts; tundra's existing woody resources use low shrubs. Biome-specific terrain/resource generation is a separate follow-up.
- Decorative understory is visual only and skips buildings, farms, pens, zoned and mountain tiles. It does not provide resources or block movement.
- All source ground atlas cells live in assets/biomes; procedural renderers are in tools/approved-art. The renderer scripts write native PNGs and previews to sibling output folders.

## Water

- Resources/Art/Water contains 128 transparent 32x16 water frame sprites (8 frames for each of 16 sets).
- Still water: shallow/deep, two variants. Ocean: coastal/deep, two variants. River: four directions, two variants.
- WaterArt.Frame selects a sprite with appropriate playback speed and camera-relative current direction. The world-axis direction order is east/+X, south/+Y, west/-X, north/-Y.
- **Water placement and animation are not wired to local terrain yet.** The strategic map still uses its existing color-based water. Connect hydrology/depth/flow data and renderer updates rather than painting water across walkable land. Water frames alone do not change navigation, bridges or buildability.

## Validation

- Native PNG dimension, alpha and manifest checks passed for the texture packs.
- Crown & Hearth > Verify Approved Art checks buildings and wall imports.
- Crown & Hearth > Verify Biome Art checks all biome resources, pivots, ground indices and per-world bindings.
- The previous biome compile errors (Defs.Zone / Defs.Deco) were corrected to namespace-level enums.
- Check Unity compilation and run these verifiers and EditMode tests after applying this commit. Full Play Mode verification was interrupted by the user earlier; do not treat this as a completed visual gameplay sign-off.

Keep future art coded at native pixel scale and show new sprites in chat. Match TEXTURE_STYLE.md. Do not replace approved sprites with image-generated drafts.
