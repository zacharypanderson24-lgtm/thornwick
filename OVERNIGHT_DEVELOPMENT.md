# Overnight development journal

Started from Zach's roadmap (12 milestones). **Honest scope note (read first):** the roadmap is many weeks of work. This session finished Milestone 1 part 1 (the organic economic development engine, 10 pull requests) and records below exactly what is and is not done. Nothing here claims more than was run.

## Current milestone
Milestone 1, organic economic development: simulation, controls panel, catalogue and overlays, migration and scenarios are built. Open items inside it: the HUD cleanup (remove the bottom strip, floating Build/Map/Overlay controls) and a live-game playtest.

## Pull requests (all stacked on one another, base `unity-migration`, none merged)
#151 phase 1 framework and design doc, #152 engine core and queue, #153 farmland and forestry, #154 mining and quarrying, #155 hunting and fishing, #156 district controls and the Districts panel (key Y), #157 site clearing and protected land, #158 Zoning catalogue section, overlays, demand indicators, #159 save migration and optimisation (full suite ran: 851 tests, 840 passed, 0 failed, 10 skipped, 1 inconclusive), phase 10 PR: balance and scenarios.

## Decisions
See `DESIGN_ORGANIC_DEVELOPMENT.md` sections 2 to 16. Notable: zones start activity, buildings are "organic" when built in or beside their district, older games load with districts paused (housing excepted), protected land is a hard restriction, construction is a minimal queue with held materials, homes wait for food security.
**Roadmap conflict to decide:** the roadmap asks to "update the tutorial", but Zach earlier removed the first-year tutorial on purpose and said guidance comes from the Town Steward and lenses. Left as removed; teaching zoning through the Steward is still to do.

## Known issues
Crews file `World.Crews.cs` is not valid UTF-8, so hunters are not limited to their district and ignore protected woodland. Growth is conservative after the food-security change. Nothing was seen in the live game.

## Not started (in the roadmap's priority order)
HUD cleanup, multi-settlement architecture, settlement switching and founding, governors, civilization overview, domestic trade, technology and eras, production chains, council, diplomacy, terrain presentation, tutorials, long-term balancing, stretch goals.

## Next steps
1. Merge the organic stack in order (#151 to the phase 10 PR) after Zach looks at the Districts panel in the game.
2. HUD cleanup (small, visual, needs the live game).
3. Multi-settlement architecture (Government Phase 3): a `Civilization` container over several `World`s with one calendar.
