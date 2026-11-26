# Thornwick / Crown & Hearth: government, governors and several settlements (Unity)

Status: **Phase 1 (characters) and Phase 2 (appointments, recruitment, salaries, Government menu) are implemented.** Phases 3 to 10 are planned, nothing in them exists yet. This file says what is real at the top of every phase; update it as phases land.

Design rule: simple controls, complex consequences. Everything here lives in `CrownAndHearth.Sim` (no UnityEngine), is deterministic, saved, and drives the same validated operations the player uses.

## 1. Audit: what exists, what blocks several settlements

| Area | What it is today | Reusable? | Blocks multi-settlement? |
|---|---|---|---|
| `World` (World*.cs, ~60 partial files) | ONE player settlement: 256x256 `Grid`, `ResAmt` stockpile, `Peasants`, `Hall`, `Day`, workforce `Prio`/`TradeTarget`, `Era`, `Events`, `Mil` military, save/load (`SaveJson`/`LoadJson`, `"v":1`, optional fields) | Yes: it is already a complete, self-contained, saveable settlement | Only because the game holds exactly one and the world map treats it as "home" (`HomeCellX/Y`) |
| `Settlement` (Settlements.cs) | A light NPC record on the world map: population, attitude, liege, garrison, unrest, `Makes`/`Needs`, conquered state | Yes: the map-level identity of any place | Has no buildings, stockpile or workforce: it cannot be "managed" yet |
| World.Conquest / Realms | tribute, occupation, unrest, rebellion on an NPC `Settlement` | Yes, kept as is; governors will feed modifiers into `Unrest` (Phase 5) | Conquered towns stay light records until Phase 4 |
| World.SettlementTrade / MaritimeTrade | caravans (persisted, `SaveCaravans`) and ships between you and NPC settlements | Yes: caravan/shipment persistence and routing are the base of domestic trade (Phase 8) | Endpoints are "you" and "an NPC settlement" |
| World.Economy `Tick()` | one day of the whole settlement, ~ms | Yes | Cost is paid per settlement per day: the reason for the detail-level decision below |
| World.Save | one JSON document, optional keys for new systems, old saves migrate by absence | Yes: the migration pattern is "key absent -> derive deterministically" | Saves one `World` |
| Portraits (`CrownAndHearth.Portraits`, PRs 114/115) | Data-driven library, deterministic generator `Generate(lib, seed, region, archetype)`, 5 age-stage ids (`young, adult, middle_aged, elderly, venerable`), LRU `PortraitCache`, archetypes `civil, military, merchant, agricultural, diplomatic` | Yes | Portrait asmdef does not reference Sim; character data therefore stores only ids and a seed |
| UI (`Game/UI`, `MenuRegistry`, `ManagementData`) | 12 menus; **Government** (F10) is a disabled slot | Yes: Government menu is where governor/council UI goes | none |

Findings that shape the plan:
- A `World` is the right unit of "a controlled settlement". Do not rewrite it into a multi-tenant object; hold several of them.
- Calendar: `World.Day`, `YearLength = 360`, months of 30 days (`SettlementMonth`). Characters use this calendar (birth days are plain day numbers, may be negative).
- `World` is global to the game (`GameController.World`), and the world map assumes one home cell. Phase 3 introduces a `Civilization` container and Phase 4 the switching.

## 2. Architecture decision: several simulated settlements

Options: (A) every controlled settlement is a full `World`, all ticked daily; (B) aggregate economy model for inactive ones; (C) hybrid.

**Decision: hybrid, full simulation with a scheduler, aggregate only as the last-resort fallback.**
- Every player-controlled settlement is a real `World` (own buildings, stockpiles, people, workforce, treasury, garrison, caravans). There is no global inventory.
- One civilization calendar. The active settlement ticks every day it is on screen. Inactive ones are advanced by the same `World.Tick()` (never a second model), staggered over frames and by a per-frame time budget; a settlement that falls behind is caught up in whole days before it is shown or traded with, so nothing is lost or duplicated when switching and there is never a mid-day hand-over.
- Why not aggregate: a second economic model drifts from the real one, and switching detail level would create or destroy resources. The same `Tick()` cannot.
- Memory: a `World` holds ~1.5 MB of grids; 50 settlements about 75 MB in Sim, no Unity objects (rendering scenes exist only for the active settlement). Phase 3 benchmarks 1/5/10/25/50 and, if the budget fails, lets distant, uneventful settlements tick on a coarser schedule using rate-based catch-up *derived from the real Recalc rates* (still conserving every resource); the decision gate and numbers go into this file then.
- Characters are NOT inside a `World`: they belong to the civilization (`CharacterRegistry`), because a person moves between settlements and outlives one. Phase 1 gives each `World` a registry for its own settlement (the capital); Phase 3 lifts it to the civilization without changing its data.

## 3. Character data model (Phase 1, implemented)

`Sim/Characters/`. One `Character` per notable person, stable integer `Id` (never reused, never changes).

- Identity: `Id`, `Name`, `BirthDay` (calendar day), `HomeSettlement`, `Settlement` (current), `Civilization` (0 = yours), `Background`, `Occupation` (civilian job text), `Archetype` (portrait archetype id), `Alive`, `Employment` (`Available / Civilian / Government / Retired / Deceased`).
- Skills (0-100): exactly five attributes `Administration, Economics, Military, Stewardship, Diplomacy`. Nothing else is a skill.
- Standing (0-100): `Loyalty, Ambition, Health, Experience` (experience is kept in hundredths so slow growth is not lost).
- `Traits`: 1 to 3 of the 16 listed, with a data table of modest modifiers (`TraitTable`). Effective attribute = base + trait modifier, clamped; salary and experience-gain multipliers come from the same table.
- Appointment: `Post` (`None, Governor, Chancellor, Treasurer, Marshal, Steward, Diplomat`) and `PostSettlement`. One major appointment per person. Written by Phase 2; Phase 1 only reserves and saves it (and clears it on retirement/death).
- `MonthlySalary` and `PayLevel`: reserved (0 / Standard) for Phase 2.
- `Appearance`: a plain definition, never an image: `Seed` (physical identity: face, skin, hair colour, brows, eyes, beard all derive from it), `Climate`, `Archetype`, and *aging tendencies* `GreyAge`, `RecedeAge`, `BaldAge` (the age in years at which greying/receding/baldness starts; 0 = never) and `Outfit` (empty = by archetype). Changing job or clothing changes only `Outfit`/`Archetype`, never `Seed`, so the face never changes. The Game layer maps this to `PortraitGenerator.Generate(lib, Seed, Climate, Archetype)` and the stage id from `AgeStage`; greying/hair-loss rendering follows the tendencies when the portrait renderer supports it (integration after PR 115).
- History: a short per-character log of dated entries ("born", "became Adult", "retired", "died"), capped.
- Age stages from the calendar: Young 18-29, Adult 30-44, Middle-aged 45-59, Elderly 60-74, Venerable 75+; ids map 1:1 to the portrait age stages.

**Notable pools.** `CharacterRegistry` keeps, per settlement, who lives there. Target size follows `CharacterBalance.NotableTargets` (piecewise-linear through the requested points: 50 -> 3, 200 -> 6, 500 -> 11.5, 1,000 -> 20, 5,000 -> 52, 10,000 -> 82, 50,000 -> 200), so growth diminishes. Founders at world start: 3 to 5. Every month a settlement below its target may gain one person (more when far below), picked by origin weights from its real economy (farms -> farmers/landowners, markets and ports -> merchants, mines/quarries/sawmills/smithery -> craftsmen, soldiers -> veterans, Town Hall/era -> educated, trade and ports -> foreign immigrants, plus locals). The pool is persistent: opening a menu never rerolls anything.

**Attributes.** Each skill is a sum of four dice shaped like a bell curve around ~38 (sd ~14), with the background lifting two favoured attributes and a ~4 percent chance of one exceptional attribute. Result: most notables are average, 80+ is rare, background influences but never guarantees. Loyalty, Ambition, Health, Experience follow age and traits.

**Aging and death.** Monthly: age stage changes are logged, health drifts down with age, experience grows for people at work (slower the higher it already is), retirement from 62+, death from age/health risk. All dice come from `CharRng`, a hash of (world seed, settlement, serial) or (id, day), never from `System.Random` and never from the world's own random stream, so existing behaviour is untouched and save/load replays identically.

**Saves.** `characters` key in the world JSON (`{ v:1, next, pools, rows, journal }`, rows are compact arrays). A save without the key (every older save) is migrated by creating founders for the settlement deterministically from its seed and current population; the result is saved from then on.

## 4. Phase plan (separate PRs)

1. **Character foundation** (this PR): data model, generator, traits, notable pools, aging, retirement, death, saves + migration, tests. Portrait mapping fields only; rendering hookup after PR 115 merges.
2. **Recruitment, appointments, salaries:** Post suitability from the five attributes with plain reasons, candidate browser sources (local, other settlements, transfer, international with time and cost), appoint/transfer/promote/dismiss/replace, salary levels and monthly payroll from the right treasury, missed payments and resignations, payroll in the finance UI.
3. **Multi-settlement architecture:** `Civilization` container, shared calendar, per-settlement `World`, scheduler and catch-up, characters lifted to the civilization, benchmarks (1/5/10/25/50).
4. **Settlement access:** switching from the world map, founding (suitability scoring, costs, settlers, transport, five village types), conquered settlements become manageable (keep layout/people/notables, appointment options and their unrest effects).
5. **Governor directives:** nine priorities x five levels, presets, standing orders (hard vs preference), delegation modes, governor effects on unrest.
6. **Governor automation:** scheduled decision loop (restrictions, emergencies, upkeep, orders, bottlenecks, priorities, expansion), cooldowns and forecasts, every action explained, same validation as the player.
7. **Civilization management UI:** governor screen and national overview (Government menu), sortable/filterable, honest about delayed information.
8. **Domestic trade:** routes, shipments in transit (persisted), four standing orders, loop prevention, pricing hooks left open.
9. **Ruling council:** five posts unlocked by era, reports from real data, limited automation.
10. **Testing, balancing, performance, gameplay testing:** the scenarios A to G, benchmarks, balance pass.

Every phase: tests, docs updated to what is true, screenshots for UI, honest report.

## 5. Out of scope
Dynasties, marriage, inheritance, estates, private wealth, factions, espionage, ministerial politics, extra currencies, banking, department budgets, individual citizen simulation, playable underground mines.

## 6. Phase 2 as built (appointments, recruitment, salaries)

Code: `Sim/Characters/Positions.cs` (post definitions, suitability, reasons, `Pay`), `Sim/World.Government.cs` (operations, candidates, recruiting abroad, payday, saves), `Game/UI/GovernmentData.cs` + `Game/CharacterPortraits.cs` + Hud wiring (Government menu, F10). Screenshots: `docs/government/`.

- **Posts and era:** Chancellor and Steward from Era II, Treasurer and Marshal Era III, Diplomat Era IV (the game starts in Era 2). Governor is implemented but refused until a second managed settlement exists (phase 3: `GovernableSettlements()` returns none today). One holder per post, one post per person; Appoint, Dismiss, Replace (checked first, all or nothing), Promote (move to a vacant post), SetPay. Dismissed people stay Available in the world.
- **Suitability:** weighted from the five effective attributes (traits included) plus up to +8 for experience, per post, with plain reasons (strong/weak skills, traits, experience, loyalty, age, health). Ratings: Poor, Fair, Good, Excellent, Exceptional.
- **Candidates:** `FindCandidates(post, filter, sort)` reads the persistent pool and never generates anything (no rerolls). Sources: Local (capital), Other settlements, International. Filters and sorts: source, settlement, age, loyalty, experience, salary, attributes, occupation; sort by suitability, age, loyalty, experience, salary or any attribute.
- **International:** `SearchAbroad(post, settlement)`: needs a settlement that will trade with you, 60 gold, 30 days plus twice the march days; brings back the best of three generated people for that post (deterministic from seed and search number); one search per post at a time and a 180 day cooldown per post; saved mid-journey. The returned person is Available, listed as International, and becomes yours on appointment.
- **Salaries:** `Pay.Monthly` = post base x skill x experience x settlement size x traits x pay level (Low 0.8, Standard 1, Generous 1.35). Paid in gold on day % 30 before aging, from the one treasury (`TreasuryFor`; per-settlement treasuries come in phase 3). Unpaid: no money is created, the official loses 6 loyalty and a month is counted; three unpaid months in a row, or loyalty 0, means resignation. Low pay costs 1 loyalty a month, Generous gives 1. Retirement and death of an official tell the player the post is vacant.
- **UI:** one Government menu: Council (posts, who holds them, duties), Recruit (candidates with portrait-ready details, sort, source, appoint/replace, search abroad with a cost confirmation), Details (portrait, attributes with trait effects, standing, suitability and reasons, appoint/dismiss), Payroll (monthly total against the treasury, last payday, click to change pay level). The menu opens once the era has a post or someone holds one.
- **Portraits:** the face always comes from `Look.Seed` alone; archetype/outfit and age stage come on top; greying, receding hair and baldness follow the saved tendencies (`grey` hair colour, `receding` and `bald` hair styles).
- **Not done:** governors and settlement transfer need phase 3; council reports and automation are phase 9; no per-settlement treasury; the pay formula is untuned (about 7 to 15 gold a month per official at the starting size).
