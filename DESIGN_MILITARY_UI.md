# Thornwick: Complete Military UI Redesign (Zach's spec)

**Status (update as it changes):** PR 1 built: Military Command window (header summary strip, tab bar with only the implemented tab, Companies tab with filterable scrolling company cards, detail with progress bars and tooltips, contextual actions with disabled-with-reason, empty state, number dialogs with slider/typed/-/+, Create Company wizard, global mobilize with confirmation, Emergency orders for gates and civilian defense, Esc closes). NOT built yet: Overview tab cards, Recruitment tab, Defense tab (list, camera jump), building inspector panels, Equipment tab, Fleet tab, orders/assign position (no movement in the sim yet). Code: `Game/Hud.Military*.cs`, pure logic in `Sim/Military/MilitaryUiLogic.cs` (tests: `MilitaryUiTests`). Spec text below is condensed from Zach's message; nothing was dropped on purpose.

## 1. Objective
Replace the developer-style military menu with a polished player-facing interface, intuitive for five militia and scalable to thousands of soldiers, several settlements, siege engines and fleets. Mostly UI architecture, presentation and interaction; do not rewrite the military simulation unnecessarily.

## 2. Design requirements
Match Thornwick's medieval identity (dark brown, parchment, gold); consistent typography, buttons, padding; no wasted space; little explanatory text; icons with readable labels; contextual information; disable or hide unavailable actions; many resolutions; usable with future touch; do not cover the whole map when unnecessary; keep all existing functionality. No generic modern dashboard look.

## 3. Layout
Military Command window: header with overview and navigation; left company list (~27%); center selected company (~43%); right contextual actions and information (~30%); bottom status area with warnings and mobilization controls. Responsive sizing, not fixed pixels. No huge empty table when there are no companies.

## 4. Header
"Military Command", the settlement name, a clear close button. Compact summary: population, eligible military population, civilian workforce, professional soldiers, registered reserve militia, mobilized militia, barracks occupancy (for example `Population 420 | Civilian workers 174 | Active military 36 | Reserve militia 24 | Barracks 36/48`). Important numbers prominent; details by tooltip or the Overview tab. No paragraph of raw statistics.

## 5. Tabs
Overview, Companies (default), Recruitment, Defense, Equipment, Fleet. Only show tabs for implemented gameplay (no Fleet before naval gameplay; no Equipment before it has real content). New tabs must be easy to add. No development-phase messages for players.

## 6. Companies tab
Left: scrollable list of compact company cards (troop icon, name, troop type, current/target personnel, current assignment, service category, readiness indicator), for example `1st Thornwick Infantry / Shield infantry · Professional / 16 / 20 soldiers / Defending northern gate / Readiness 85%`. Click selects. Filters: All, Professional, Reserve militia, Mobilized militia, Infantry, Ranged, Cavalry, Engineers (only relevant ones as troop types appear). Scrolling instead of pagination. Prominent Create Company button.

## 7. Empty state
Useful intro instead of an empty table, for example: "You have no military companies. Register civilian militia to prepare your settlement for emergencies, or recruit professional soldiers once barracks are available." Two primary actions: Create militia company, Create professional company. If infrastructure is missing, state it ("Muster Ground required" / "Barracks required") with a shortcut to the construction menu if supported. No pagination or irrelevant actions.

## 8. Center: selected company
Name (renamable), icon, troop type, service category, current and target personnel, location, assignment. Personnel line such as "16 / 20: four more soldiers are being recruited". Horizontal progress bars with readable labels for Training, Experience, Morale, Readiness, Equipment (later fatigue, supply), using real values only (no fake statistics). Each statistic has a tooltip: what it is, what affects it, why it matters. No permanent paragraph explaining statistics. Current orders prominent, with destination or building where applicable.

## 9. Right: contextual actions
Show controls by company type and state. Reserve militia: register additional, set target strength, mobilize, assign defensive position (if implemented), unregister, disband. Mobilized militia: change orders, assign position (if implemented), reinforce, demobilize, disband. Professional: recruit, set target, assign barracks (if supported), train, change orders, discharge, disband. Unavailable actions are hidden or disabled with a reason ("Recruit soldiers: unavailable, no free barracks accommodation"). Never make the player infer why a click does nothing.

## 10. No shared number box
Recruit, Register, Discharge and Set Target open a compact dialog with slider, typed entry, -/+, min and max, requirements, Confirm and Cancel, with a sensible nonzero default.

## 11. Recruitment tab
Troop-type cards (peasant levy, spearman, hunter archer, shield infantry): icon, name, role, equipment (when implemented), strengths, weaknesses, availability, tech needs. Pick a type, then professional or reserve (valid combinations only). Confirmation shows recruits, eligible residents, current and projected workforce, muster capacity, barracks space, equipment (when implemented), duration and upkeep (if supported), for example "Recruit eight professional spearmen. Civilian workforce 174 → 166. Barracks occupancy 36/48 → 44/48." Explain that reserve militia keep jobs until mobilized. Warn on significant labor shortage.

## 12. Create Company wizard
One Create Company button: 1 troop type, 2 service category, 3 name (or generated: "1st Thornwick Spearmen", "2nd Thornwick Infantry", "Woodland Archers", "Village Militia"), 4 desired strength, 5 review requirements, 6 confirm. Offer adding to an existing company instead of forcing a new one.

## 13. Overview tab
Compact cards, not paragraphs. Personnel (professionals, registered, mobilized, available recruits, workforce impact); infrastructure (barracks, muster, watchtowers, training, armories when implemented); economy (wages, food, equipment shortages, maintenance); security (threat level, detected raids, gate status, active defenders, readiness).

## 14. Defense tab
Only implemented functionality (eventually gates, watchtowers, walls, gatehouses, guard positions, patrols, equipment, supplies). Each structure: name, type, condition, assigned personnel, open positions, assignment, equipment status ("Northern Watchtower: personnel 2/2, condition 100%, operational"). Click may move the camera. Status as text labels, never colour alone: fully staffed, understaffed, unstaffed, damaged, disabled.

## 15. Gates and emergency defense
Move Gates and Workers Fight Raiders into a Settlement Defense / Emergency Orders section: gate state with the available action ("Gates: Open. Available action: Close gates"), several gates individually and together when feasible. Rename to "Emergency civilian defense" with a tooltip explaining the existing behaviour. Do not change the underlying behaviour without checking how it works.

## 16. Mobilization controls
Global: Mobilize all, Demobilize all, reserve available, currently mobilized. Before mass mobilization show consequences ("Mobilizing 24 militia members will reduce the civilian workforce from 174 to 150. This may cause labor shortages in agriculture and other industries.") and require confirmation for large-scale mobilization. Single companies can be mobilized from their card or the action panel.

## 17. Equipment tab
Design the architecture only (infantry equipment, ammunition, siege equipment, defensive supplies; available / assigned / reserved). No fake inventory or dead controls; do not show the tab until it has real content.

## 18. Building inspectors
Selecting a military building opens a small contextual panel: barracks (beds used and total, free accommodation, assigned companies, condition, training, recruitment progress), muster ground (registered, capacity, assigned companies, mobilization, assembly), watchtower (staffing, condition, observation, defenders, equipment). Do not duplicate the whole window; give a shortcut to the right management screen.

## 19. Visual standards
Dark brown or charcoal background, slightly lighter panels, parchment primary text, muted secondary text, gold for selection, muted green favourable, amber warnings, muted red critical. Clear type hierarchy; avoid tiny text, paragraphs, abbreviations, low contrast, over-wide buttons, repeated labels, excess borders, big unused areas. Use existing sprites for troop and building icons; consistent temporary icons otherwise.

## 20. Interaction standards
Hover, selected, disabled and pressed states, confirmations, error messages, tooltips. Confirm destructive actions (especially disbanding experienced companies). Actionable messages: No eligible residents available, Muster ground capacity reached, No free barracks beds, Company already mobilized, Insufficient personnel.

## 21. Responsive layout
Large desktop: list, details and actions together. Smaller: fewer simultaneous panels or tabs, readable text. Eventual mobile: single-column list, tap opens detail, large touch controls, no horizontal scrolling tables. Scroll inside panels rather than shrinking text.

## 22. Implementation
Inspect the existing Unity UI (built in code in the Hud) and do not add a second framework. Reusable components: window header, tabs, statistic card, company card, company detail, progress bar, contextual action panel, recruitment dialog, quantity selector, empty-state panel, confirmation dialog, tooltip, defensive structure card. Keep UI state separate from simulation data; the UI issues commands through the military system and never edits population or military state. It must update as the sim advances. Keep keyboard shortcuts, including Esc to close.

## 23. Structure reference
```
MILITARY COMMAND                         THORNWICK   [X]
Population    Civilian    Active     Reserve    Barracks
   420           174         36          24        36/48
 Overview | Companies | Recruitment | Defense | Equipment
 COMPANIES           SELECTED COMPANY        MANAGEMENT
 [+ Create Company]  1st Thornwick Infantry  Recruit
 [Filter: All]       Shield Infantry         Set Strength
 ...                 Training 78%  ...       Requirements / Warnings
 Threat: Low     Reserve available: 24     [Mobilize All]
```

## 24. Testing to do
New village with no military buildings; muster ground but no companies; reserve company; mobilized company; professional company; full barracks; too few eligible residents; several troop types and companies; a long scrolling list; recruit and discharge; create and disband; global mobilize and demobilize; several resolutions; accelerated speeds; save and load. Do not call the redesign finished until it has been inspected in the playable build.

## 25. Deliverables
Compile, fix errors, run the military tests, open the UI in the game, capture screenshots (empty state, company view, recruitment view), check readability and layout, document what stays unavailable, list modified files and UI architecture decisions.
