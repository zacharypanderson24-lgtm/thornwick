# Thornwick / Crown & Hearth: UI architecture (Unity)

Status: Phase 1 (foundation) done (PR 106); Phase 2 done (PR 107); Phase 3 done (PR 109); Phase 4 done (PR 110); Phase 5 done (PR 111); Phase 6 done (PR 112); Phase 7 (hover tips, interface size) in progress. Military is the one menu still outside the shared window. This file holds the audit, the framework choice, the menu map and the phase plan. Update the status line as phases land.

## 1. Framework assessment

The Unity HUD is uGUI built entirely in code (`Hud` partial class, one Canvas, CanvasScaler 1600x900 reference, match 0.5, Input System UI module, built-in font). Decision: **stay on uGUI built in code**. UI Toolkit would be a second framework and a rewrite of ~15 working panels for no player-visible gain; the weakness today is structure (hard-coded positions, per-panel one-offs, no shared state), not the framework. We add a thin layer on top: theme, menu registry, UI manager, notification manager, reusable components. Old panels migrate onto it one phase at a time; nothing is removed until its replacement works.

Rules kept from the brief: UI reads sim state and sends validated commands only (`CrownAndHearth.Sim` stays free of UnityEngine); no fake numbers; no dead buttons (a menu with nothing real behind it is shown as "not yet available" and its button is disabled, never empty); no browser-version changes.

## 2. Inventory of existing controls and where each one goes

| Control (today) | Where it lives today | New home |
|---|---|---|
| Resource bar (day, people, crops/mining/wood dropdowns, gold) | Hud top row | stays; becomes the permanent resource bar, tooltips via UI tooltip adapter (later phase) |
| Pause / 1x / 2x / 4x, Space | top right | stays (Time panel); Space unchanged |
| Rotate Q / E | top right | stays |
| Save / Load | top right | stays now; moves into the System menu in the phase that adds it |
| Build bar: Inspect, House, Farm, Wood, Mine, Water, Mil... (digits, L, 0) | bottom | stays: it is the placement tool bar, the Construction menu links to it |
| Tool chip, cursor tip, card tooltips | Hud | stay |
| Workers button + Workforce panel | bottom/side | **Population** menu (Workforce tab) |
| Farming, Mining, Livestock town menus | bottom right bar | **Agriculture** (farming, livestock) and **Economy** (mining) |
| Military window (companies, orders) | bottom right bar, T | **Military** menu (T kept) |
| Statistics window | bottom right bar | **Settlement** (history) |
| Map button / world map, settlement, trade, camp panels | bottom right, M | **World** menu (M kept); trade panels also reachable from **Trade** |
| Needs bar, happiness meter | Hud | stay on HUD; detail in **Population** |
| Campaign goals, Incident cards, scenario banner | Hud | stay; goals link from **Settlement**; incidents also go to the notification history |
| New world dialog, Banner picker | Hud | System menu later; New world stays reachable now |
| Vessels window | button under New world | **Transportation** (Vessels tab) |
| Ferry Route tool, Bridge, Canal, Moat, Dock, Port, Boatyard tools | build bar Water category | stay in the build bar; **Transportation** lists routes and boats |
| Overlays: water (hotkey), Underground view (U), claims | hotkeys and tools | one **overlay selector** (a later phase); keys stay |
| Tile inspection (left Info panel) | Hud | stays (inspection templates later) |
| Toasts (`World.Events`) | Hud | go through the **NotificationManager** (history kept, same toast shown) |
| Esc | closes map, panels, placement | **UIManager** order: modal, placement, open menu, then nothing |

Hotkeys kept: 1-9, 0, L (build categories), Q/E, Space, U, T, M, Esc. New: **F1-F12** open the twelve management menus (digits are taken by the build bar).

## 3. The twelve menus

| # | Menu | Key | Phase 1 tabs with real data | Planned | Status |
|---|---|---|---|---|---|
| 1 | Settlement | F1 | Overview (people, housing, food days, happiness, era, day) | Buildings, History | window |
| 2 | Population | F2 | Workforce (opens the existing panel) | Jobs, Needs, Housing | link |
| 3 | Economy | F3 | Stockpiles (each resource: amount, daily change) | Production, Mining | window |
| 4 | Construction | F4 | build bar (opens it) | Queue | link |
| 5 | Agriculture | F5 | Farming, Livestock (existing panels) | Seasons | link |
| 6 | Technology | F6 | none: there is no tech tree | everything | disabled |
| 7 | Military | F7 | existing Military window | | link |
| 8 | Transportation | F8 | Boats and routes (existing Vessels window) | Roads | link |
| 9 | Trade | F9 | opens the world map trade panels | Routes, Prices | link |
| 10 | Government | F10 | none | laws, taxes | disabled |
| 11 | Diplomacy | F11 | none beyond trade partners | relations | disabled |
| 12 | World | F12 | existing world map | | link |

Unlock rules live in the registry (`MenuDef.Available(World)`): a menu is greyed with the reason in its tooltip until it has a real thing to show (Transportation: a water world with vessels or a boatyard; Trade and World: a world map exists; Military: always, as today).

## 4. Architecture (`unity/Assets/Scripts/Game/UI/`)

- `UITheme`: colours, sizes, spacing, one place (the old `Hud` palette statics keep working, they are mirrored from here).
- `MenuRegistry`: `MenuId` enum (12), `MenuDef` (title, key, tooltip, availability, implemented flag, tabs). Pure C#, tested.
- `UIManager`: which menu is open, which tab, a modal stack, the Esc order, "pointer over UI" bookkeeping. Pure C#, tested; the Hud drives it and draws.
- `NotificationManager`: categories (info, warning, danger, success), classify by keyword from `World.Events`, dedupe by key with a cooldown, history (last 100), optional action target (a menu). Pure C#, tested.
- Components built in code: window frame with title bar and close button, tab bar, stat card, progress bar, empty state, confirmation dialog.
- `ManagementToolbar`: second row under the resource bar, built from the registry.

## 5. Phases (one PR each, individually tested)

1. **Foundation**: theme, registry, manager, notifications, window and tab components, dialogs, management toolbar with F-keys, Esc order, Settlement and Economy windows from real data, notification history.
2. Population and Economy migration (Workforce into Population tabs, mining), Construction hub.
3. Military, Agriculture, Transportation, Trade migration to the shared window.
4. Inspection templates (building, tile, settlement, vessel, route...).
5. Overlays with one selector (only those with real data).
6. Government, Diplomacy, World architecture (only what exists), settings and system menu.
7. Responsive layouts, accessibility, polish.

## 6. Verification

Unit tests for registry, manager and notifications; the full EditMode suite before each PR; screenshots from a batch editor capture at 1280x720, 1920x1080 and 2560x1440. What a PR did not verify is stated in its description.

## 7. Phase 2 notes

The old Workforce, Farming, Mining, Statistics and Livestock screens are not rewritten: the window hosts them (moved inside it, in a scroll area), so their behaviour is unchanged. Their old buttons (Workers, Farming, Mining, Statistics, Livestock) are retired; Military and Map stay in the small bar until their phase. Population gained Jobs and Housing tabs, Settlement a History tab, Economy a Mining tab, Agriculture Farming and Livestock tabs, and Construction is a hub listing every tool by category (click a row to pick the tool).

## 8. Phase 3 notes

Transport (F8) is a data window: Routes (every ferry route with length, crossings a day and state), Boats (every vessel with condition and where it works) and Orders (boatyard state and queue); its Order boats button opens the existing Vessels window, so the floating Vessels button is retired. Trade (F9) is a data window too: On the road (caravans and ships, soonest first) and Capacity (markets, caravans and ships out), with a World map button; World (F12) still opens the map itself. **Military is not moved**: its window has nested dialogs and text fields that depend on its own layout, so it keeps its own window (F7 or T) until a dedicated pass. The Banner and New world buttons wait for the System menu phase.

## 9. Phase 4 notes

The tile and building inspector keeps GameController.TileInfo as the one place that gathers facts (one line per fact), and Game/UI/InspectionData turns those lines into the standard layout: heading, short "Label: value" facts as aligned rows, sentences as paragraphs, problems in a warning colour, a good connection in green. This is one template for every kind of tile (building, road, water, forest, field, pen, ground) rather than ten, because the facts already differ per kind in the data. **Not done:** contextual action buttons in the inspector (open Workforce, Order boats...). The inspector only shows while hovering, so a button on it could not be reached; it needs a click-to-select (pinned) inspector first, a separate change to input handling.

## 10. Phase 5 notes

One Overlay button (lower right, next to Map and Military) opens a list: Off, Underground, Groundwater, Fertile land, Sailable water; one overlay at a time, and a locked one says why (Sailable water needs a water world). Only overlays with real data are listed: ore bands under the hills, the groundwater patches with short homes and the roads water travels, the fertile ground flag, and the navigable-water regions. The registry is Game/UI/OverlayRegistry.cs; GameController draws the chosen one (DrawChosenOverlay) and U still cycles the underground depth. **Not included** (no data behind them yet, so no entry): happiness, crime, pollution, traffic, land value, coverage of services. They join the list when the simulation has them. The always-on contextual overlays (reach rings for work buildings, zone and placement previews, claim parcels) stay tied to their tools.

## 11. Phase 6 notes

A System button at the end of the toolbar (and Esc when nothing else is open) opens the System window: Game (Resume, Save, Load, New world, Banner colour), Controls (every shortcut) and Settings (interface size 85 or 100 percent; pause while the menu is open). Settings are kept in PlayerPrefs. The old floating Banner and New world buttons are retired; Save and Load also stay in the top bar. Esc order is now dialog, placement, open window or panel, then the System menu, which pauses the game while it is open (and gives the speed back on close) unless that setting is off. **Not included:** sound, graphics and key-rebinding options, because the game has none of those systems yet; 

## 12. Phase 7 notes

Hovering a toolbar button now shows a card under it: name and key, what the menu holds, and why it is locked when it is. **Interface size:** the 115 percent option from Phase 6 is removed. The layout is built for a fixed 1600 x 900 canvas that scales with the screen, so any size above 100 percent shrinks that canvas and overflows (the toolbar was buried at 1280 x 720 and at 1920 x 1080 alike). Sizes are now 85 percent (more room for the map; checked at 1280 x 720) and 100 percent; a bigger interface needs a responsive layout first (anchors and flexible rows instead of the fixed offsets in the old panels). Seen at all three resolutions: 1280 x 720, 1920 x 1080 and 2560 x 1440. **Not done:** Military in the shared window (its window has nested dialogs and text fields tied to its own layout; plan: move it last, panel by panel, after a responsive pass), keyboard focus and tab order through the windows, and a high-contrast option.
