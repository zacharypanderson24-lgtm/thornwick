# Military system (Unity port)

Population-driven military from Zach's "Thornwick Military System Overhaul". The browser prototype is untouched. This file says what the
old model was, what Phase 1 changed, every balance number, the texture slots still missing, and what is NOT built yet.

## What existed before (replaced)
- `World.Soldiers` / `World.Archers` were plain counts. A served Barracks made one soldier every 4 days out of `Peasants` for a fixed resource
  price (`TrainOne`, `TrainCost`, `Drafting`, `ArcherPercent`); capacity was `Bunks` (16 per served barracks) and soldiers above it were killed.
- Soldiers were already part of the population (`Peasants + Soldiers`), left the workforce and ate 1.5x (`SoldierFoodFactor`), cost 0.1 gold/day.
- Raids (`World.Raids.cs`), camp battles (`CampBattle.cs`), expeditions and garrisons (`World.Camps/Conquest/Realms.cs`) read `Soldiers/Archers`.

## What Phase 1 does
| Spec rule | Implementation |
|---|---|
| Soldiers only from recruitment | Barracks no longer train. `MilitarySystem.StepDaily` recruits professionals towards each company's **Target**, `RecruitPerDay` per company, only while bunks are free and eligible civilians exist. No resource price (equipment arrives in Phase 2; today they carry improvised weapons). |
| Capacity from buildings, no instant kill | Recruiting needs a free bunk (`BunksFree`, a bunk is kept for each soldier on his way home). Soldiers whose barracks is destroyed stay in the population ("unhoused"), morale drops, recruitment stops. The old "kill soldiers above the bunks" rule is gone. |
| Professional removes one worker | Recruit = `Peasants--`, `Soldiers++`. Jobs are refilled by `Recalc` from the civilians left, in trade priority order, so the jobless are taken first and the lowest-priority trades lose workers first. |
| Registered militia do not reduce the workforce | Militia are `Peasants` on a roll (`MilitaryCompany`, category Militia). Only **mobilised** militia are removed: `Recalc` fills jobs from `Mil.CivilianWorkforce = Peasants - MobilisedCount`. |
| Demobilise restores jobs | Nothing about jobs is stored: `Recalc` recomputes workers every time, so demobilising gives the same allocation back (tested building by building). |
| Nobody in two companies, pros cannot be militia | `AvailableToServe = Peasants - Registered - MinCivilians`; both recruiting and registering draw on it. |
| No double counting in food/housing | Militia stay inside `Peasants` (eat 1.0). Professionals are `Soldiers` (eat 1.5). Population is `Peasants + Soldiers`, as before. |
| Casualties reduce population | Fallen soldiers decrement `Soldiers`; dead militia/civilians decrement `Peasants`. `Reconcile` (run by `Recalc`) then trims the companies: missing men are taken from the biggest company of that unit. |
| Old saves | No `military` block: `Reconcile` makes garrison companies from `Soldiers/Archers` (training 40, experience 5). Nobody is added or removed. Saves written by this version keep a versioned `military` block (`MilitaryConfig.SaveVersion`). Incompatibility: a save written by this version, loaded by an older build, loses its companies and the militia roll (the soldiers survive). `archerPercent` is no longer written or read. |

## Companies and stats
`MilitaryCompany` (state only): id, editable name, home settlement, category (Militia, Professional, Field), unit type (Militia, Spearman, Archer),
personnel/target, **Training, Experience, Morale, Readiness** (0-100), mobilised flag and days, order + order tile, station, equipment count, location text.
- Training: `rate * (1 - training/100)` per day while drilling. Pros `ProTrainRate` up to `ProTrainCap`; militia `MilitiaTrainRate` up to `MilitiaTrainCap`, only if a muster ground exists.
- Experience: slow service gain plus kills in a raid; newcomers dilute training and experience as a weighted average.
- Morale: drifts (`MoraleMovePerDay`) towards base 70 minus starvation, unpaid wages, no bunk, long mobilisation.
- Readiness: 50 x manning + 20 if fed + 30 x call-up progress (professionals count as fully activated). It never uses training or experience.
- Mobilised militia fight raiders within `MilitiaReach` of the muster point (`World.MobilisedFight`), add to `RaidDanger`, and the wounded are real residents.

## Where the code is
`Assets/Scripts/Sim/Military/`: `MilitaryConfig` (all numbers), `MilitaryCompany` (state, enums), `MilitaryBuildingDefinition` (buildings as data),
`MilitarySystem` (companies, recruitment, mobilisation, reconcile, daily step, raid hooks), `MilitarySystem.Save`.
Hooks: `World.Rules.cs` (Recalc workforce, era check in `CanPlace`), `World.Economy.cs` (daily step), `World.Raids.cs`, `World.Save.cs`, `World.Troops.cs`, `Defs.cs`.
UI: `Game/Hud.Military.cs` (the Military menu, replaces the Troops menu). Tests: `MilitaryTests.cs`, `TroopTests.cs`.
Spec class names map as: MilitarySystem = `MilitarySystem`; MilitaryCompany = `MilitaryCompany`; MilitaryRecruitmentSystem = the recruitment/mobilisation members of `MilitarySystem`
(split into its own class when equipment lands); MilitaryBuildingDefinition = same; MilitaryUI = `Hud.Military.cs`; Equipment/Order/Training/Supply systems are Phase 2/3.

## Balance parameters (all in `MilitaryConfig`)
EligibleShare 1.0 · MinCivilians 2 · company max sizes: militia 30, spearmen 40, archers 30, cavalry 30, guard 8 · MusterCapacity 30 · BarracksCapacity 16 (`Defs.Bunks`) ·
RecruitPerDay 2 · RecruitTraining 8 · RecruitMorale 65 · MilitiaStartTraining 4 · MigratedTraining 40 / MigratedExperience 5 · ProTrainRate 0.9 / Cap 80 ·
MilitiaTrainRate 0.12 / Cap 45 · ServiceExperienceRate 0.01 · ExperiencePerKill 0.6 · MoraleBase 70, Starving -30, Unpaid -15, LongMobilisation -12 (after 30 days), OverCapacity -8, MovePerDay 1.5 ·
MobiliseDays 3 · MilitiaPowerUntrained 0.15 / Trained 0.45 · MilitiaDamagePerPower 0.5 · MilitiaReach 6 · MilitiaWoundRate 0.004 · CurrentEra 2.
Wages stay 0.1 gold per professional per day (`World.Economy.cs`); militia are unpaid. Reference sizes (not caps): pop 50: 0-2 pros / 5-10 militia; 200: 4-10 / 20-40; 500: 10-25 / 50-100;
1000: 20-50 / 100-200; 5000: 100-250 / 500-1000; 10000: 200-500 / 1000-2000.

## Buildings (`MilitaryBuildings.All`)
Implemented: Muster Ground 2x2 (14 wood, era I, 30 on the roll, houses nobody, no road needed), Barracks 2x2 (era II, 16 bunks), Watchtower (era I). Listed with numbers but not placeable:
Armory (1x1, 60 sets), Guardhouse (1x1, 4 posts), Training Grounds (3x3, 40), Military HQ (3x3), Fortified Keep (3x3). Eras: there is no tech tree, so `World.Era` starts at 2 (saved);
each definition carries its era and `CanPlace` refuses buildings from a later one. Raise/lower `MilitaryConfig.CurrentEra` or set `World.Era` when a tech tree exists.

## Texture requirements
Missing slots (placeholders named `ph_*` where noted, Resources/Art, 2x2 = 64x88, 1x1 = 32x48, 3x3 = 96x128, four views `_1.._4`):
`muster_1..4` (2x2; **`ph_muster.png` is a copy of the barracks view for now**), `armory_1..4` (1x1), `guardhouse_1..4` (1x1), `watchtower_wood_1..4` (1x1, wooden version),
`traininggrounds_1..4` (3x3), `militaryhq_1..4` (3x3), `keep_1..4` (3x3). The existing barracks art is reused.

## Not built yet (be honest)
- **Phase 2**: equipment production/storage/allocation, armory, training facilities, wages as a currency system, maintenance of barracks. Equipment fields exist on companies (`Equipped`) but nothing fills them.
- **Phase 3**: companies do not walk yet; orders (hold, guard, defend area, patrol, return) are stored and shown but have no effect on movement; no watchtower staffing/detection by garrison; raids still spawn fighters from the aggregate `Soldiers` count at the barracks or Hall (`Garrisons()`), not from company positions; no map destination/route UI.
- **Phase 4**: Field armies (category exists; expeditions still send aggregate counts and `Reconcile` takes the men out of their companies and puts them back as a new company on return, so company identity is not kept across a campaign), supply network, regional war, multi-settlement organisation, late eras.
- Merge of companies is not implemented (disband only). Cavalry, shield/heavy infantry need equipment.
- Phase 1 UI has not been checked in the live game by Claude (see the PR text).
