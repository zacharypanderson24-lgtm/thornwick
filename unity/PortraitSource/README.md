# Thornwick procedural portraits — v8
Native 48x48 RGBA sprites; Unity Point filtering, no compression or mipmaps.
Install Pillow, then run `python generate.py --seed 42 --archetype royal --output portrait.png`.
Persist full JSON appearance recipes, not just seeds. Older recipes default to original clothing and headwear colors.
44 garments, 26 actual hats plus no hat, 17 hairstyles, 16 facial hair styles, 10 skin and 10 hair palettes. Standard vertical eyes. Eight archetypes with three dedicated outfits and hats each.
Material-aware palette library: one selectable dye channel per garment/hat, with untouched trim and other materials. Connected civic, estate and linen hoods extend onto both shoulders. Updated dye masks include the draped fabric.
Validation: 336 hood/outfit/compatible hairstyle/selected beard combinations checked for enclosed background gaps after the hood revisions; zero remaining. Not exhaustive visual QA.
Explicit archetype currently overrides climate outfit selection. Python prototype only; Unity integration not included.

V9: shortened base mouth and redrawn mustaches above lip; tested all 16 facial-hair layers for unobstructed central lip pixels. Standalone editor refreshed. Prior randomized gap testing predates this revision.

V10: all 16 beard layers clear the nose tip and the skin immediately below it; central lip pixels remain clear. No base face changes in this revision.

V11: removed isolated and perimeter facial-hair highlights; retained interior shading on larger beards. Removed two lower-lip accent pixels. All beard alpha masks unchanged.

V12: redrawn chevron, chin beard, goatee, handlebar, short beard and pointed beard silhouettes. Nose/lip masks verified for these six. Full randomized compatibility QA not rerun.

V13: chevron uses a five-pixel bar and two diagonal tips; goatee has restored interior shading; handlebar has outward swept ends. Three layers checked for nose/lip clearance; full compatibility testing not rerun.

V14: added per-hat hair occlusion so hidden crown hair is erased before headwear is drawn; lower side hair remains visible where the hat does not cover it. Standalone creator refreshed to use the same masking.

V15: removed isolated and perimeter light-highlight pixels from hairstyles while preserving larger highlight clusters and silhouette shapes. Standalone creator refreshed.

V16: mask only hair above closed hat upper silhouette; retain hair below brim and all hair with open crowns/circlets. All hairstyles selectable with headwear. Eight comparison examples visually reviewed; exhaustive visual and browser testing not performed.

V17: repaired nine exposed scalp pixels across buzz cut, short, cropped and widows peak. Existing hair palettes and hat masks unchanged. All 17 hair layers inspected for small exposed scalp islands; four affected styles visually compared. Browser and exhaustive hat combinations not tested.

V18: reviewed all 17 hairstyles on the standard face; refined ten with material shading and strand definition. Hair alpha silhouettes unchanged for all edited styles; V17 scalp repairs retained. Browser and exhaustive visual combination tests not performed.

V19: five age overlays, recolored using the skin palette; manual age choice in standalone editor, independent hair color. Older recipes default to young. All 50 age/skin combinations rendered; nose/lip clearance checked. Age is categorical, not yet a gameplay simulation of birthdays or automatic graying. Browser testing not performed.
