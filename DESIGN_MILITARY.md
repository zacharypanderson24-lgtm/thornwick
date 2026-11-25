# Thornwick / Crown & Hearth — Military, Offensive Warfare and Defensive Engineering design

**Status (kept up to date by whoever builds a phase):** design only, written by Zach on 2026-11-25 for the Unity port. The phase roadmap is in sections 64 to 71. Phase 1 (population-based recruitment, militia, professionals, companies, muster ground, barracks accommodation, mobilisation, Military panel, saves) was handed to a Claude session on 2026-11-25; check `gh pr list` and DEVELOPMENT_PROGRESS.md for what has actually landed before assuming anything below exists. Where this document conflicts with the current Unity code, adapt the existing architecture and keep the intent described here.

Earlier, shorter brief (Phase 1 to 3 only, same intent): this document is a superset of it. The first-year rule is unchanged: no mandatory barracks or professionals in year one, military preparation is optional, no unavoidable early raids.

Related notes: DESIGN_TERRAIN.md (terrain, workforce, mining), CLAUDE.md (conventions), TEXTURE_SPEC.md and BUILDING_TILE_GUIDE.md (art rules). The text below is Zach's specification, kept as written.

---

## 1. Overall vision

Completely redesign Thornwick's military system into a comprehensive, scalable simulation of military organization, warfare, siege engineering, defensive infrastructure and military economics.

Thornwick should not become a conventional RTS where players build barracks, purchase soldiers and send large groups of units to attack enemy buildings.

Instead, military strength should emerge from:

- Population.
- Economic productivity.
- Available workforce.
- Natural resources.
- Industrial capabilities.
- Technology.
- Geography.
- Infrastructure.
- Military organization.
- Training and experience.
- Supply networks.
- Strategic decision-making.

A small settlement should rely on civilian militia and inexpensive fortifications. A prosperous medieval city should be capable of maintaining professional soldiers, constructing sophisticated castles and producing siege equipment. A developed regional civilization should eventually support large armies, multiple fortified settlements, artillery and extensive military campaigns.

The military system must complement the city-building experience rather than replace it.

## 2. Initial repository assessment

Before making changes:

- Read CHATGPT_START_HERE.md and all relevant project documentation.
- Inspect the current Unity simulation architecture.
- Inspect existing barracks, recruitment, soldiers, military buildings, enemy behavior, combat, pathfinding, population, workforce and resource systems.
- Inspect the current technology and era progression systems.
- Identify existing military mechanics that should be retained, replaced or expanded.
- Inspect the current save architecture.
- Inspect the first-year tutorial and its dependencies.
- Review existing military textures and their associated building definitions.

Prioritize the Unity implementation. Do not spend development time optimizing or extending the legacy browser version.

Preserve unrelated gameplay functionality.

Do not introduce parallel implementations of systems that already exist.

Where this specification conflicts with the current implementation, adapt the existing architecture while preserving the intended gameplay described here.

---

# PART I — MILITARY ORGANIZATION

## 3. Population-based military recruitment

Remove any mechanic where barracks automatically generate soldiers independently of the settlement's population.

All soldiers must originate from actual members of the settlement's eligible population.

Military service must have meaningful economic consequences.

Example settlement:

- Total population: 200.
- Eligible workforce: 100.
- Professional soldiers: 8.
- Registered militia: 20.

During peacetime:

- Civilian workforce: 92.
- Active soldiers: 8.
- Reserve militia: 20.

During full mobilization:

- Civilian workforce: 72.
- Active soldiers: 28.

Requirements:

- Professional soldiers leave civilian employment.
- Registered militia remain economically productive until mobilized.
- Mobilized militia temporarily leave civilian employment.
- Demobilized militia return to civilian employment.
- Discharged professional soldiers return to civilian life.
- Military casualties reduce actual population.
- Soldiers cannot belong to multiple companies simultaneously.
- Soldiers must not be double-counted in population, food consumption or housing.
- Recruitment must prioritize unemployed eligible workers when possible.
- Recruitment must respect actual population availability.

If the current population system uses aggregated residents, retain that approach. Do not introduce individual resident simulation solely to support military recruitment.

## 4. Military personnel categories

### Civilian militia

Ordinary residents registered for emergency military service.

During peacetime:

- Continue civilian employment.
- Remain in civilian housing.
- Consume ordinary population food allocations.
- Receive occasional training.
- Require no permanent military wages.

During mobilization:

- Stop civilian production.
- Assemble at assigned muster grounds.
- Receive available equipment.
- Follow military orders.
- Defend designated locations.

Militia should be relatively inexpensive but less disciplined than professional soldiers.

### Professional garrison

Permanent soldiers responsible for settlement security.

Responsibilities include:

- Guarding gates.
- Patrolling streets and roads.
- Staffing towers.
- Defending walls.
- Responding to raids.
- Protecting important infrastructure.
- Training.
- Maintaining military readiness.

Professional soldiers require accommodation, food, equipment and eventually wages.

### Field army

Military companies assigned to operations away from their home settlement.

Responsibilities include:

- Offensive campaigns.
- Raids.
- Sieges.
- Reinforcing settlements.
- Protecting trade routes.
- Escorting caravans.
- Territorial expansion.
- Defending distant resource extraction sites.

Field armies require additional logistical support when operating away from settlements.

Militia, professional soldiers and field soldiers are employment or assignment categories, not separate troop types.

A spearman could serve in any of these categories.

## 5. Military companies

Companies should be the primary military management unit.

Avoid requiring players to individually manage hundreds or thousands of soldiers.

Each company should persist:

- Unique identifier.
- Custom name.
- Home settlement.
- Personnel category.
- Troop type.
- Current personnel.
- Target personnel.
- Equipment allocation.
- Training.
- Experience.
- Morale.
- Readiness.
- Fatigue.
- Supply status.
- Assigned station.
- Current position.
- Current orders.
- Formation.
- Mobilization status.

Initial company size recommendations:

| Company | Personnel |
|---|---:|
| Guard detachment | 2–8 |
| Militia company | 10–30 |
| Infantry company | 10–40 |
| Archer company | 10–30 |
| Cavalry company | 10–30 |
| Engineering detachment | 4–20 |

Companies can operate below their target personnel count.

Support recruitment, reinforcement, discharge, reassignment, mobilization, demobilization and disbanding.

Eventually support merging and splitting compatible companies.

For large armies, use aggregate company-level simulation rather than requiring a separate expensive GameObject for every soldier.

Visual soldier representations should remain consistent with the underlying personnel count.

---

# PART II — TROOP TYPES

## 6. Complete troop roster

Implement troop types through configurable definitions rather than hardcoded behavior.

Each troop type should have a meaningful purpose, strengths, weaknesses and economic requirements.

Later troops should not universally outperform earlier troops.

### Era I — Early settlement

**Peasant levy**

- Equipment: Improvised weapons.
- Strengths: Cheap, rapidly mobilized, widely available.
- Weaknesses: Low morale, poor training, weak against organized soldiers.
- Role: Emergency village defense.

**Spearman**

- Equipment: Spear.
- Strengths: Affordable frontline infantry, effective against cavalry.
- Weaknesses: Vulnerable to ranged attacks and poor positioning.
- Role: Basic organized infantry.

**Hunter archer**

- Equipment: Hunting bow and arrows.
- Strengths: Inexpensive ranged support, mobile, suited to forests.
- Weaknesses: Limited range, poor melee capability.
- Role: Early defensive ranged support.

**Javelin thrower**

- Equipment: Javelins and optional shield.
- Strengths: Mobile skirmishing, useful in rough terrain.
- Weaknesses: Limited ammunition and range.
- Role: Light harassment.

### Era II — Established village

**Shield infantry**

- Equipment: Spear, shield, light protection.
- Strengths: Strong defensive formations, resistance to ranged attacks.
- Weaknesses: Limited offensive capability and mobility.
- Role: Frontline defense and gate protection.

**Axeman**

- Equipment: Axe and light protection.
- Strengths: Strong offensive melee capability.
- Weaknesses: Short reach and weaker defensive formations.
- Role: Assault infantry.

**Bowman**

- Equipment: Military bow and arrows.
- Strengths: Sustained ranged support, effective on defensive walls.
- Weaknesses: Training requirements and weak melee capability.
- Role: General ranged infantry.

**Light cavalry**

- Equipment: Horse, weapon and light protection.
- Strengths: Scouting, mobility, pursuit and flanking.
- Weaknesses: High upkeep and poor performance in confined terrain.
- Role: Reconnaissance and rapid response.

### Era III — Medieval town

**Man-at-arms**

- Equipment: Sword or mace, shield, helmet and mail armor.
- Strengths: Durable, versatile and effective in close combat.
- Weaknesses: Expensive equipment and training.
- Role: Professional infantry.

**Pikeman**

- Equipment: Long pike and optional armor.
- Strengths: Excellent anti-cavalry defense.
- Weaknesses: Poor maneuverability and confined-space performance.
- Role: Formation-based frontline infantry.

**Crossbowman**

- Equipment: Crossbow and bolts.
- Strengths: Effective against armored enemies, relatively accessible training.
- Weaknesses: Slow reload and expensive equipment.
- Role: Anti-armor ranged support.

**Longbowman**

- Equipment: Longbow and arrows.
- Strengths: Long range and sustained ranged output.
- Weaknesses: Extensive training and ammunition requirements.
- Role: Specialist ranged infantry.

**Heavy cavalry**

- Equipment: Warhorse, lance, sword and heavy armor.
- Strengths: Powerful charges and high battlefield mobility.
- Weaknesses: Extremely expensive, vulnerable to organized anti-cavalry formations.
- Role: Shock cavalry.

**Horse archer**

- Equipment: Horse, composite bow and arrows.
- Strengths: Exceptional mobility and ranged harassment.
- Weaknesses: Extensive training and terrain limitations.
- Role: Mobile ranged warfare.

### Era IV — Regional power

**Halberdier**

- Equipment: Halberd, helmet and armor.
- Strengths: Effective against cavalry and armored infantry.
- Weaknesses: Requires disciplined training.
- Role: Advanced frontline infantry.

**Heavy assault infantry**

- Equipment: Heavy armor and powerful close-combat weapons.
- Strengths: High protection and assault capability.
- Weaknesses: Slow movement, high costs and fatigue.
- Role: Breaking defensive formations.

**Handgunner**

- Equipment: Early firearm and ammunition.
- Strengths: Effective against expensive armor and enemy morale.
- Weaknesses: Slow firing rate, low accuracy and weather sensitivity.
- Role: Early gunpowder support.

**Siege engineer**

- Equipment: Engineering tools.
- Strengths: Constructs, repairs and operates siege infrastructure.
- Weaknesses: Expensive specialist with weak direct combat capability.
- Role: Military engineering.

### Era V — Early modern civilization

**Arquebusier**

- Equipment: Arquebus and ammunition.
- Strengths: Strong anti-armor performance.
- Weaknesses: Slow reload, vulnerable without supporting infantry.
- Role: Gunpowder infantry.

**Musketeer**

- Equipment: Musket and ammunition.
- Strengths: Powerful formation-based ranged attacks.
- Weaknesses: Expensive ammunition and protection requirements.
- Role: Advanced ranged infantry.

**Dragoon**

- Equipment: Horse, firearm and secondary weapon.
- Strengths: Rapid deployment and flexible mounted or dismounted operations.
- Weaknesses: Expensive and less effective in direct cavalry charges.
- Role: Mobile infantry.

**Artillery crew**

- Equipment: Artillery training and operating tools.
- Strengths: Operates powerful military equipment.
- Weaknesses: Poor direct combat performance and extensive supply requirements.
- Role: Field and siege artillery.

## 7. Troop attributes

Each troop definition should include:

- Melee attack.
- Ranged attack.
- Attack interval.
- Effective range.
- Armor.
- Shield protection.
- Movement speed.
- Stamina.
- Morale characteristics.
- Anti-cavalry effectiveness.
- Structural attack capability.
- Formation effectiveness.
- Equipment requirements.
- Training requirements.
- Food requirements.
- Ammunition requirements.
- Recruitment requirements.
- Technology prerequisites.
- Terrain modifiers.

Keep all statistics centrally configurable.

Do not balance units solely around damage and health.

## 8. Training and experience

Separate troop type from troop quality.

Experience grades:

1. Recruit.
2. Trained.
3. Regular.
4. Veteran.
5. Elite.

Track training, experience, morale, readiness and fatigue independently.

Training increases through military exercises and appropriate infrastructure.

Experience increases through active service and combat.

Recruiting inexperienced replacements should proportionally reduce average company experience.

Veteran soldiers should be valuable and difficult to replace.

An experienced company of inexpensive soldiers should sometimes outperform inexperienced soldiers equipped with more advanced weapons.

## 9. Formations

Eventually support:

- Loose formation.
- Standard infantry line.
- Shield wall.
- Spear formation.
- Pike formation.
- Skirmish formation.
- Cavalry formation.
- Marching column.
- Defensive hold.
- Gunpowder infantry formation.

Formations should influence protection, movement, attack effectiveness, cohesion and vulnerability.

Formations should be configurable company states rather than entirely separate unit types.

---

# PART III — MILITARY INFRASTRUCTURE

## 10. Military buildings

Initial suggested building sizes and capacities:

| Building | Footprint | Function |
|---|---|---|
| Muster ground | 2×2 | Registers and assembles approximately 30 militia |
| Barracks | 2×2 | Houses approximately 16 professional soldiers |
| Armory | 1×1 | Stores approximately 60 basic equipment sets |
| Wooden watchtower | 1×1 | Observation and two guard positions |
| Guardhouse | 1×1 | Four local guard duty positions |
| Training grounds | 3×3 | Specialized training for approximately 40 soldiers |
| Military headquarters | 3×3 | Coordinates military companies |
| Siege workshop | 3×3 | Produces basic siege equipment |
| Engineering yard | 4×4 | Produces advanced siege equipment |
| Military supply depot | 2×2 | Stores military supplies |
| Stables | 2×2 or larger | Houses and supports military horses |
| Cannon foundry | 3×3 | Produces artillery |
| Fortified keep | 3×3 or larger | Defensive command center |

These are configurable starting targets, not permanent balancing decisions.

### Muster ground

Available from Era I.

Provides militia registration, assembly and basic training.

Does not generate soldiers or require permanent military personnel.

### Barracks

Available from Era II.

Reuse existing barracks artwork where possible.

Provides professional military accommodation, training and assembly.

Does not automatically create soldiers.

Barracks capacity limits locally accommodated professional soldiers, not militia registration.

Destroying a barracks must not automatically delete its soldiers.

### Armory

Stores actual military equipment.

Track available, reserved, issued and damaged equipment.

Support issuing and returning equipment.

Prevent equipment duplication.

### Guardhouse

Provides local duty stations.

Does not create soldiers or increase barracks accommodation.

### Training grounds

Provide specialized training and improved training capacity.

Training should depend on available instructors, equipment and personnel.

### Military headquarters

Coordinates multiple companies, command assignments and larger operations.

Eventually supports officers, military organization and regional command.

### Siege workshop

Produces basic siege equipment using existing resource chains.

### Engineering yard

Produces and maintains advanced siege equipment and engineering components.

### Military supply depot

Stores military rations, ammunition, spare equipment and engineering supplies.

### Cannon foundry

Produces later-era artillery equipment.

---

# PART IV — MILITARY EQUIPMENT AND ECONOMICS

## 11. Equipment production

Military equipment must be produced, traded for or obtained through explicitly defined sources.

Do not allow equipment to appear automatically when soldiers are recruited.

Equipment categories:

- Spears.
- Shields.
- Axes.
- Swords.
- Pikes.
- Halberds.
- Bows.
- Longbows.
- Crossbows.
- Arrows.
- Bolts.
- Helmets.
- Light armor.
- Heavy armor.
- Horses.
- Firearms.
- Artillery ammunition.
- Engineering supplies.

Production should integrate with appropriate existing or planned industries:

- Lumber production.
- Carpentry.
- Mining.
- Metal processing.
- Blacksmithing.
- Leatherworking.
- Textile production.
- Horse breeding.
- Advanced manufacturing.

Equipment should remain assigned to soldiers until lost, damaged, replaced or returned.

Do not consume an entire equipment set every simulation day.

Use actual resource inventories and production time.

## 12. Military upkeep

Military costs should emerge naturally through:

- Lost civilian labor.
- Food consumption.
- Equipment production.
- Building maintenance.
- Horse feed.
- Ammunition.
- Training.
- Wages, when supported.
- Medical support.
- Supply transportation.

Do not arbitrarily penalize players for maintaining a large military.

Instead, allow economic consequences to determine sustainable military size.

Initial balancing reference:

| Population | Professional soldiers | Emergency militia |
|---:|---:|---:|
| 50 | 0–2 | 5–10 |
| 200 | 4–10 | 20–40 |
| 500 | 10–25 | 50–100 |
| 1,000 | 20–50 | 100–200 |
| 5,000 | 100–250 | 500–1,000 |
| 10,000 | 200–500 | 1,000–2,000 |

These are sustainability reference points, not hard recruitment limits.

---

# PART V — OFFENSIVE SIEGE WARFARE

## 13. Siege engine design philosophy

Siege engines are physical military assets.

They must be:

- Manufactured from resources.
- Transported or assembled.
- Staffed by actual soldiers.
- Supplied with ammunition where applicable.
- Maintained.
- Repaired when damaged.
- Assigned military orders.

Siege engines should not be automatically generated by a siege workshop.

They should exist independently of their assigned crew.

An abandoned siege engine may remain on the map and eventually be recovered, captured, damaged or destroyed.

A siege army should require substantially greater economic and logistical preparation than an ordinary infantry army.

## 14. Siege equipment roster

### Era I — Early settlement

**Basic battering ram**

- Suggested crew: 4–6.
- Role: Attacking wooden gates.
- Strengths: Cheap, mobile, simple to produce.
- Weaknesses: Exposed crew, weak against stone fortifications.

**Scaling ladders**

- Suggested crew: Two carriers, with assigned assault infantry.
- Role: Providing access over suitable defensive walls.
- Strengths: Cheap, portable, bypasses certain barriers.
- Weaknesses: Vulnerable to defenders, restricted by wall characteristics.

### Era II — Established village

**Covered battering ram**

- Suggested crew: Eight.
- Role: Attacking reinforced gates.
- Strengths: Strong gate damage and protected crew.
- Weaknesses: Slow, expensive and vulnerable to unsuitable terrain.

**Mobile mantlet**

- Suggested crew: Two.
- Role: Providing portable protection.
- Strengths: Protects advancing soldiers and ranged infantry.
- Weaknesses: Limited mobility and protection against heavy siege weapons.

**Ballista**

- Suggested crew: Three to four.
- Role: Attacking exposed defenders, infantry and lighter military equipment.
- Strengths: Relatively accurate and mobile.
- Weaknesses: Limited damage against thick masonry.

### Era III — Medieval town

**Mangonel**

- Suggested crew: Six to eight.
- Role: Attacking infantry concentrations and lighter fortifications.
- Strengths: Area damage and structural capability.
- Weaknesses: Slow firing rate and limited accuracy.

**Siege tower**

- Suggested crew: Eight operators.
- Suggested infantry capacity: Approximately 20.
- Role: Providing protected access to defensive walls.
- Strengths: Supports larger organized assaults.
- Weaknesses: Slow, expensive and strongly affected by terrain.

**Sapper detachment**

- Suggested personnel: Six to twelve engineers.
- Role: Overcoming defensive obstacles through an abstracted engineering action.
- Strengths: Can threaten otherwise difficult fortifications.
- Weaknesses: Time-consuming, expensive and affected by terrain and geology.

### Era IV — Regional power

**Trebuchet**

- Suggested crew: Twelve to twenty.
- Role: Heavy siege bombardment.
- Strengths: Long range and substantial structural damage.
- Weaknesses: Expensive, slow to deploy and difficult to relocate.

**Bombard**

- Suggested crew: Eight to twelve.
- Role: Heavy attacks against masonry.
- Strengths: Extremely powerful structural damage.
- Weaknesses: High logistical costs and limited mobility.

### Era V — Early modern civilization

**Field cannon**

- Suggested crew: Four to six.
- Role: Mobile battlefield artillery.
- Strengths: Effective against infantry formations and lighter defenses.
- Weaknesses: Ammunition requirements and limited protection.

**Heavy siege cannon**

- Suggested crew: Ten to sixteen.
- Role: Destroying advanced fortifications.
- Strengths: Exceptional structural damage.
- Weaknesses: Enormous transportation and supply requirements.

**Mortar**

- Suggested crew: Four to six.
- Role: Indirect attacks against fortified areas.
- Strengths: Can affect targets behind walls.
- Weaknesses: Limited mobility, accuracy and firing rate.

## 15. Siege equipment balancing

Use the following preliminary relative ratings, where zero means no direct capability and five represents exceptional capability.

| Equipment | Gate damage | Wall damage | Anti-infantry | Mobility | Cost |
|---|---:|---:|---:|---:|---:|
| Basic ram | 3 | 1 | 0 | 4 | 1 |
| Ladder | 0 | 0 | 0 | 5 | 1 |
| Covered ram | 5 | 1 | 0 | 2 | 2 |
| Mantlet | 0 | 0 | 0 | 3 | 1 |
| Ballista | 2 | 1 | 4 | 3 | 2 |
| Mangonel | 3 | 2 | 4 | 2 | 3 |
| Siege tower | 0 | 0 | 0 | 1 | 4 |
| Sappers | 2 | 5 | 0 | 3 | 3 |
| Trebuchet | 4 | 5 | 3 | 1 | 4 |
| Bombard | 5 | 5 | 2 | 1 | 5 |
| Field cannon | 3 | 3 | 5 | 3 | 4 |
| Heavy siege cannon | 5 | 5 | 4 | 1 | 5 |
| Mortar | 2 | 3 | 4 | 2 | 4 |

Utility equipment should not require damage statistics to be useful.

Translate these relative ratings into actual simulation parameters through testing.

## 16. Siege engine attributes

Each siege engine should store:

- Unique ID.
- Equipment type.
- Owner.
- Current location.
- Assigned crew.
- Required crew.
- Structural health.
- Operational condition.
- Ammunition.
- Movement speed.
- Transport status.
- Deployment time.
- Deployment status.
- Effective range.
- Attack interval.
- Structural damage.
- Anti-personnel damage.
- Crew protection.
- Maintenance requirements.
- Assigned target.
- Current orders.

An engine with insufficient crew should operate less effectively or become unusable.

Crew members must originate from the military personnel system.

Destroying a siege engine should resolve crew outcomes separately from equipment destruction.

## 17. Siege engine movement

Siege equipment must interact with physical terrain.

Consider:

- Road quality.
- Elevation.
- Slopes.
- Forest density.
- Bridges.
- River crossings.
- Marshes.
- Ditches.
- Moats.
- Gate dimensions.
- Available space.
- Equipment footprint.

Larger siege engines should have greater movement restrictions than infantry.

Support packing, transporting, deploying and dismantling heavy engines where appropriate.

Do not teleport siege engines between locations.

Reuse existing pathfinding infrastructure.

## 18. Structural damage

Fortifications should have meaningful material-specific characteristics.

Example materials:

- Timber.
- Reinforced timber.
- Earth.
- Stone.
- Reinforced masonry.
- Advanced earth-backed fortifications.

Track:

- Structural health.
- Material resistance.
- Fire vulnerability.
- Impact resistance.
- Breach state.
- Repair requirements.

Different siege equipment should perform differently against different materials.

Battering rams should primarily threaten gates.

Heavy siege weapons should be substantially more effective against masonry.

Damage should persist until repaired.

Do not represent all fortifications as interchangeable health bars.

---

# PART VI — DEFENSIVE ENGINEERING

## 19. Defensive design philosophy

Defending a settlement should be fundamentally different from fighting an open-field battle.

Walls should not simply increase the time it takes for an enemy to reach a city.

Fortifications should provide:

- Elevated defensive positions.
- Protected firing positions.
- Restricted enemy movement.
- Observation.
- Active defensive equipment.
- Multiple defensive layers.
- Control over entrances.
- Time for reinforcements.
- Protection for civilians and infrastructure.

A carefully designed fortified settlement should gain meaningful advantages over a poorly planned settlement of equivalent military strength.

## 20. Modular fortification system

Use modular fortifications rather than requiring players to place a handful of predetermined castle buildings.

Allow players to combine:

- Wall segments.
- Corners.
- Towers.
- Gates.
- Gatehouses.
- Walkways.
- Ditches.
- Earthworks.
- Moats.
- Defensive platforms.
- Portcullises.
- Specialized upgrades.
- Defensive equipment.

Wall connections should update visually and functionally based on neighboring structures.

Support different wall materials, heights and upgrade levels.

Walls should create actual movement barriers.

Gates should determine legal crossings.

Where the existing grid architecture permits, investigate edge-based fortifications so walls can occupy tile boundaries without unnecessarily consuming entire city blocks.

Do not implement edge-based construction if it would destabilize the current placement architecture. Use an extensible solution appropriate to the existing game.

## 21. Basic defensive structures

### Defensive stakes

- Cheap obstacles.
- Restrict movement.
- Particularly effective at discouraging cavalry.
- Require relatively little construction investment.
- Vulnerable to removal and deterioration.

### Defensive ditches

- Slow advancing troops.
- Restrict wheeled equipment.
- Require construction labor.
- Affect friendly movement unless crossings exist.

### Earthen ramparts

- Provide elevated defensive positions.
- Relatively inexpensive material requirements.
- Require significant labor and space.
- Can eventually be reinforced.

### Wooden palisades

- Inexpensive early defensive barrier.
- Suitable for small villages.
- Vulnerable to fire and siege damage.
- Upgradeable or replaceable.

### Reinforced wooden walls

- Greater structural resistance.
- Increased timber and maintenance requirements.
- Suitable for established villages.

### Stone walls

- Strong resistance to ordinary infantry and lighter siege equipment.
- Expensive masonry requirements.
- Support elevated defensive positions.

### Reinforced masonry walls

- Greater resistance to advanced siege attacks.
- Higher construction and maintenance costs.

### Earth-backed walls

- Advanced resistance to artillery.
- Require substantial space and construction labor.

## 22. Defensive towers

Support multiple tower types.

**Wooden watchtower**

- Observation.
- Early warning.
- Small number of defensive positions.
- Inexpensive but vulnerable.

**Reinforced wooden tower**

- Increased protection.
- Greater staffing capacity.
- Supports basic defensive equipment.

**Stone watchtower**

- Strong structural resistance.
- Protected ranged positions.
- Improved observation.

**Stone defensive tower**

- Larger garrison capacity.
- Supports specialized defensive equipment.
- Provides defensive coverage of adjacent wall sections.

**Artillery tower or bastion**

- Supports mounted artillery.
- Requires advanced engineering.
- Expensive and space-intensive.

Towers should not independently generate soldiers.

Their active defensive effectiveness must depend on assigned personnel.

## 23. Gates and gatehouses

Support progressively advanced entrances:

- Basic wooden gate.
- Reinforced wooden gate.
- Double gate.
- Fortified gatehouse.
- Portcullis.
- Advanced fortified entrance.
- Barbican.

Gates should support:

- Open state.
- Closed state.
- Locked state.
- Damaged state.
- Breached state.
- Access permissions.

A gatehouse may contain multiple independent defensive barriers.

Destroying one gate should not necessarily eliminate all defenses at an advanced entrance.

Gatehouses should contain assignable guard positions and optional defensive upgrades.

## 24. Moats and waterways

Support dry ditches and water-filled moats.

Water-filled moats should depend on suitable geography, water access and the game's terrain systems.

Moats should affect movement and siege equipment accessibility.

Support:

- Fixed bridges.
- Controlled crossings.
- Drawbridges where technically feasible.
- Defensive interactions with existing rivers.

Avoid allowing ordinary infantry or heavy equipment to ignore water barriers.

Natural rivers should be legitimate components of settlement defense.

## 25. Advanced fortification architecture

Later eras should support:

**Barbicans**

Additional fortified structures protecting important entrances.

**Concentric defenses**

Multiple independent defensive perimeters.

Outer defenses may fall while inner defenses remain functional.

**Machicolations**

Permanent stone upgrades providing additional downward defensive coverage.

**Wooden hoardings**

Earlier, less expensive projecting defensive platforms.

**Bastions**

Advanced angled defensive structures supporting overlapping coverage and artillery positions.

**Earthworks**

Later defensive structures designed to resist increasingly powerful siege weapons.

Do not require players to construct entirely predetermined castles.

Allow these structures to emerge from modular construction.

---

# PART VII — ACTIVE DEFENSIVE EQUIPMENT

## 26. General requirements

Defensive equipment should be installed on compatible structures and operated by assigned military personnel.

Active defenses require:

- Appropriate infrastructure.
- Available personnel.
- Necessary technology.
- Equipment condition.
- Consumable supplies where applicable.

Unstaffed defensive structures should not automatically attack enemies.

Defensive actions should be represented as abstract game mechanics with configurable effects, cooldowns, resource consumption and coverage.

## 27. Defensive equipment roster

### Falling stones

- Low-cost defensive action.
- Requires stored stone.
- Limited coverage directly beneath compatible structures.
- Effective against exposed nearby enemies.
- Requires assigned operators.

### Heated-water station

- Relatively inexpensive close-range defensive equipment.
- Requires an appropriate defensive platform.
- Uses abstract water and fuel allocations.
- Limited range and cooldown.
- Requires assigned operators.

### Boiling-oil station

- Expensive specialized defensive equipment.
- Uses a dedicated consumable resource.
- Strong localized defensive effect.
- Limited coverage and availability.
- Requires compatible defensive infrastructure and trained operators.

Boiling oil should be an optional luxury defense rather than a universal requirement for medieval settlements.

### Defensive archery positions

- Allow archers to operate from walls, towers and gatehouses.
- Provide protection and elevation advantages.
- Consume actual ammunition.
- Require assigned ranged personnel.

### Arrow slits

- Improve protection for defenders.
- Restrict firing coverage.
- Compatible with appropriate tower and wall types.

### Wooden hoardings

- Increase downward defensive coverage.
- Protect assigned operators.
- Vulnerable to structural damage.

### Machicolations

- Permanent advanced downward defensive infrastructure.
- More durable than wooden alternatives.
- Requires advanced masonry.

### Mounted ballista

- Long-range defensive equipment.
- Requires a compatible platform and operators.
- Consumes ammunition.
- Useful against exposed troops and lighter siege equipment.

### Defensive bombard

- Heavy defensive equipment available in later medieval eras.
- Requires substantial infrastructure and specialized crew.
- High structural and anti-equipment damage.

### Defensive cannon

- Advanced defensive artillery.
- Installed on appropriate platforms, towers or bastions.
- Powerful against military equipment and troop formations.
- Expensive ammunition and maintenance requirements.

### Defensive mortar

- Later-era indirect defensive artillery.
- Requires compatible infrastructure.
- Strong area-based defensive capability.
- Limited mobility and substantial operating costs.

## 28. Defensive equipment attributes

Each installation should track:

- Unique ID.
- Parent structure.
- Equipment type.
- Assigned operators.
- Required operators.
- Operational condition.
- Ammunition or consumables.
- Coverage area.
- Effective range.
- Cooldown.
- Damage or utility effects.
- Technology requirements.
- Maintenance requirements.
- Current assignment.

Defensive equipment must respect available personnel and supply levels.

Do not generate unlimited ammunition or consumables.

## 29. Fortification upgrades

Allow compatible structures to receive upgrades.

| Upgrade | Benefit | Trade-off |
|---|---|---|
| Reinforced gate | Greater gate resistance | Material and maintenance costs |
| Double gate | Secondary defensive barrier | Additional space |
| Battlements | Improved defender protection | Construction costs |
| Arrow slits | Protected ranged positions | Restricted coverage |
| Wooden hoarding | Downward defensive coverage | Structural vulnerability |
| Machicolations | Durable downward defense | Expensive masonry |
| Stone stockpile | Supports falling-stone defense | Limited inventory |
| Heated-water station | Close-range defensive capability | Consumable requirements |
| Oil station | Powerful localized defense | Expensive consumables |
| Ballista mount | Long-range defensive capability | Crew and ammunition |
| Cannon emplacement | Heavy defensive capability | Advanced industry and upkeep |
| Earth reinforcement | Greater artillery resistance | Additional footprint |

Upgrades should be physical and visually identifiable wherever practical.

Prevent incompatible or duplicate upgrades.

## 30. Defensive staffing

Each defensive structure should contain available personnel positions.

Example gatehouse:

- Four gate guards.
- Eight ranged positions.
- Four defensive equipment operators.
- Three ballista crew positions.

The player may assign a company or detachment to fill available positions.

Available personnel must be tracked centrally.

The same soldier cannot simultaneously staff a tower, operate a ballista and defend another location.

Reserve militia may receive predefined defensive assignments while remaining economically productive until mobilization.

On mobilization, they should travel to those positions.

A defense plan should be configurable before an attack begins.

---

# PART VIII — GEOGRAPHY AND TERRAIN

## 31. Geography as a military mechanic

Thornwick's diverse biomes and physical geography must have meaningful military consequences.

Terrain should influence:

- Movement.
- Visibility.
- Fatigue.
- Formation cohesion.
- Supply transportation.
- Equipment deployment.
- Defensive coverage.
- Combat effectiveness.

Support interactions with:

- Plains.
- Forests.
- Hills.
- Mountains.
- Rivers.
- Marshes.
- Deserts.
- Snow.
- Roads.
- Bridges.
- Urban areas.

Avoid simplistic universal combat bonuses based exclusively on biome.

Apply relevant physical terrain characteristics.

## 32. Terrain suitability

Use the following as preliminary design guidance.

| Troop category | Plains | Forest | Mountains | Urban | Marshes |
|---|---|---|---|---|---|
| Light infantry | Good | Excellent | Excellent | Good | Good |
| Shield infantry | Excellent | Good | Good | Excellent | Fair |
| Heavy infantry | Excellent | Fair | Fair | Excellent | Poor |
| Pikemen | Excellent | Poor | Poor | Fair | Poor |
| Archers | Excellent | Fair | Good | Good | Fair |
| Light cavalry | Excellent | Poor | Poor | Poor | Poor |
| Heavy cavalry | Excellent | Poor | Poor | Poor | Poor |
| Horse archers | Excellent | Poor | Poor | Poor | Poor |
| Gunpowder infantry | Excellent | Fair | Fair | Good | Poor |
| Artillery | Excellent | Poor | Poor | Restricted | Poor |

These are configurable starting assumptions.

## 33. Natural defensive advantages

Allow players to exploit geographical features through city planning.

Potential settlement designs:

- River fortresses.
- Mountain strongholds.
- Coastal fortifications.
- Peninsula settlements.
- Fortified bridge crossings.
- Hilltop towns.
- Concentric fortified cities.
- Advanced artillery fortresses.

Natural barriers should reduce the amount of artificial fortification required.

Elevated positions should influence observation and defensive coverage.

Road networks should influence reinforcement speed.

Forests and rough terrain should affect visibility and mobility.

Do not use an arbitrary circular military coverage radius as the primary defense system.

---

# PART IX — MILITARY LOGISTICS

## 34. Field army supplies

Armies operating away from their home settlements should require supplies.

Track:

- Food rations.
- Water where appropriate.
- Ammunition.
- Spare equipment.
- Horse feed.
- Engineering supplies.
- Medical supplies.

Local garrison food consumption must not be double-counted.

When a company leaves the settlement, transition it to an appropriate field supply model.

Supply consumption should depend on personnel, equipment, activity and duration.

## 35. Support equipment

Introduce noncombat military support assets over time.

**Supply wagon**

Transports food, ammunition and equipment.

**Engineering wagon**

Transports construction and repair supplies.

**Pontoon bridge equipment**

Supports temporary crossing infrastructure through an abstracted engineering action.

**Portable barricade**

Allows temporary defensive positions.

**Military tents**

Support temporary military camps.

**Medical wagon**

Provides abstract medical support and improved recovery outcomes.

**Transport ship**

Transports soldiers and supplies across water.

**Warship**

Provides naval protection and eventually maritime warfare.

Support assets should require resources, transportation capability and personnel where applicable.

## 36. Supply networks

Eventually allow:

- Supply depots.
- Resupply routes.
- Military caravans.
- Field camps.
- Forward supply stations.
- Regional logistics.
- Supply disruptions.
- Blockades.

An army should not remain indefinitely effective while isolated from supplies.

However, avoid making military logistics so tedious that players must manually manage every ration or ammunition delivery.

Use automatic logistics wherever possible, with strategic player control over priorities and destinations.

---

# PART X — COMBAT AND SIEGE GAMEPLAY

## 37. Military orders

Initial orders:

- Stand down.
- Train.
- Assemble.
- Move.
- Hold position.
- Guard building.
- Defend area.
- Staff defensive structure.
- Patrol route.
- Return to station.
- Repair equipment.

Later orders:

- Escort caravan.
- Reinforce settlement.
- Attack enemy company.
- Defend settlement.
- Raid territory.
- Establish camp.
- Begin siege.
- Operate siege equipment.
- Capture strategic location.
- Retreat.
- Resupply.
- Withdraw.

Orders should use an extensible architecture.

Companies should execute orders physically where supported.

## 38. Enemy raids

Early raids should be relatively small and comprehensible.

They may threaten:

- Stored resources.
- Livestock.
- Trade routes.
- Isolated buildings.
- Peripheral settlements.

The player should receive meaningful warnings when threats are detected.

Do not introduce unavoidable first-year attacks that undermine the survival tutorial.

Raid difficulty should reflect world conditions, settlement development and game difficulty settings.

## 39. Full siege state

A settlement should enter a siege state when relevant hostile conditions exist.

A siege should affect:

- Military readiness.
- Trade access.
- Food reserves.
- Civilian activity.
- Supply availability.
- Population morale.
- Fortification condition.
- Local economic output.

Do not automatically conclude a siege when one wall segment is damaged.

Attackers and defenders should continue operating according to actual accessible locations, remaining forces and settlement conditions.

## 40. Fortification breaches

When a wall or gate is sufficiently damaged:

- Update its physical state.
- Update pathfinding.
- Update visual appearance.
- Remove or restrict associated defensive positions.
- Reevaluate accessibility.
- Allow appropriate military movement through the breach.

Breaches should remain until repaired.

Damaged structures must not continue providing their full original defensive benefits.

## 41. Multiple defensive layers

Support settlements with multiple independent defensive perimeters.

Example:

- Outer agricultural and residential area.
- Outer defensive wall.
- Inner commercial and administrative district.
- Inner defensive wall.
- Fortified keep.

The loss of an outer perimeter should not automatically mean the loss of the entire settlement.

Defenders should be capable of withdrawing to remaining defensible areas.

## 42. Civilian behavior during warfare

Eventually support:

- Civilian sheltering.
- Evacuation.
- Reduced productivity during threats.
- Displacement.
- Damage to housing.
- Interrupted resource production.
- Food shortages.
- Medical recovery.
- Reconstruction.

Population casualties should have lasting economic and demographic consequences.

Avoid unnecessarily graphic representations.

## 43. Siege outcomes

Support outcomes beyond total destruction.

Potential outcomes:

- Successful defense.
- Attacker withdrawal.
- Negotiated surrender.
- Temporary occupation.
- Resource tribute.
- Settlement capture.
- Destruction of specific infrastructure.
- Abandonment.
- Political consequences.

Do not require every siege to end with the elimination of all opposing military personnel.

## 44. War exhaustion

Prolonged warfare should gradually affect:

- Population satisfaction.
- Military morale.
- Economic productivity.
- Recruitment willingness.
- Trade.
- Food security.
- Financial stability where applicable.

War exhaustion should emerge from actual conditions rather than an unexplained timer.

---

# PART XI — TECHNOLOGY AND ERA PROGRESSION

## 45. Five-era military development

### Era I — Early settlement

Military:

- Peasant militia.
- Spearmen.
- Hunter archers.
- Javelin throwers.

Offensive equipment:

- Basic battering rams.
- Scaling ladders.

Defensive infrastructure:

- Defensive stakes.
- Ditches.
- Earthen ramparts.
- Wooden palisades.
- Wooden watchtowers.

Military buildings:

- Muster grounds.

### Era II — Established village

Military:

- Professional garrison.
- Shield infantry.
- Axemen.
- Bowmen.
- Light cavalry.

Offensive equipment:

- Covered battering rams.
- Mantlets.
- Ballistae.

Defensive infrastructure:

- Reinforced wooden walls.
- Reinforced gates.
- Wooden hoardings.
- Falling-stone positions.
- Mounted ballistae.

Military buildings:

- Barracks.
- Armories.
- Guardhouses.
- Siege workshops.
- Stables.

### Era III — Medieval town

Military:

- Men-at-arms.
- Pikemen.
- Crossbowmen.
- Longbowmen.
- Heavy cavalry.
- Horse archers.

Offensive equipment:

- Mangonels.
- Siege towers.
- Sapper detachments.

Defensive infrastructure:

- Stone walls.
- Stone towers.
- Fortified gatehouses.
- Portcullises.
- Moats.
- Machicolations.
- Heated-water stations.
- Boiling-oil stations.

Military buildings:

- Training grounds.
- Military headquarters.
- Engineering yards.
- Military supply depots.
- Fortified keeps.

### Era IV — Regional power

Military:

- Halberdiers.
- Heavy assault infantry.
- Handgunners.
- Specialized siege engineers.

Offensive equipment:

- Trebuchets.
- Bombards.

Defensive infrastructure:

- Barbicans.
- Concentric fortifications.
- Advanced stone defenses.
- Defensive bombards.

Military development:

- Regional armies.
- Larger command structures.
- Advanced logistics.
- Offensive campaigns.

### Era V — Early modern civilization

Military:

- Arquebusiers.
- Musketeers.
- Dragoons.
- Artillery crews.

Offensive equipment:

- Field cannons.
- Heavy siege cannons.
- Mortars.

Defensive infrastructure:

- Bastions.
- Earth-backed fortifications.
- Defensive cannon positions.
- Defensive mortars.

Military buildings:

- Cannon foundries.
- Advanced supply infrastructure.

These era assignments are proposed gameplay progression rather than rigid universal historical chronology.

## 46. Military research branches

Allow civilizations to specialize in military development.

Branches should include:

- Infantry organization.
- Archery.
- Cavalry.
- Military engineering.
- Fortification engineering.
- Logistics.
- Gunpowder technology.
- Military administration.

Unlocking a new era should not automatically grant every associated military capability.

Advanced equipment should require appropriate technologies and industries.

Geography and available resources should naturally influence specialization.

Do not permanently region-lock troop types.

---

# PART XII — USER INTERFACE

## 47. Dedicated Military panel

Create a Military management interface consistent with Thornwick's existing UI.

Include sections for:

- Overview.
- Recruitment.
- Companies.
- Defensive assignments.
- Fortifications.
- Siege equipment.
- Military supplies.
- Military research.
- Active threats.
- Campaigns when implemented.

## 48. Military overview

Display:

- Total population.
- Eligible military population.
- Civilian workforce.
- Professional soldiers.
- Registered militia.
- Mobilized militia.
- Field army personnel.
- Available barracks accommodation.
- Training capacity.
- Equipment availability.
- Military upkeep.
- Current threats.

Clearly distinguish reserve personnel from active soldiers.

## 49. Recruitment interface

Allow the player to:

- Register militia.
- Recruit professional soldiers.
- Select troop type.
- Assign personnel to companies.
- Establish company target strength.
- Issue equipment.
- Discharge soldiers.

Display projected workforce consequences before recruitment.

Example:

Recruiting eight professional soldiers will reduce civilian workers from 92 to 84.

Warn when recruitment threatens critical industries, particularly food production.

## 50. Company interface

Display:

- Company name.
- Personnel count.
- Target personnel.
- Troop type.
- Assigned station.
- Current orders.
- Training.
- Experience.
- Morale.
- Readiness.
- Fatigue.
- Equipment.
- Supplies.
- Location.

Allow map-based orders and assignments.

## 51. Defense planning interface

Create a dedicated defensive planning mode.

Allow players to:

- View wall connections.
- View damaged fortifications.
- Identify entrances.
- View tower coverage.
- View staffed and unstaffed positions.
- Assign companies to defensive structures.
- Configure reserve militia assignments.
- Inspect defensive equipment.
- Inspect ammunition and consumables.
- Prioritize repairs.
- Review defensive perimeters.

Provide optional overlays for:

- Passability.
- Visibility.
- Defensive equipment coverage.
- Personnel assignments.
- Fortification condition.
- Reinforcement travel.

Do not clutter the ordinary city-building view with permanent military information.

## 52. Defensive building interface

Example: Northern Gatehouse.

Display:

- Structural condition.
- Gate condition.
- Additional barrier condition.
- Installed upgrades.
- Assigned guards.
- Assigned ranged infantry.
- Assigned defensive operators.
- Assigned artillery crew.
- Available positions.
- Stored ammunition.
- Stored defensive consumables.
- Current threats.

Allow reassignment, upgrade, repair and supply priority controls.

## 53. Siege equipment interface

Display:

- Equipment type.
- Condition.
- Assigned crew.
- Required crew.
- Ammunition.
- Movement status.
- Deployment status.
- Current target.
- Supply status.
- Repair requirements.

Allow appropriate move, deploy, pack, repair, resupply and assignment orders.

---

# PART XIII — TECHNICAL ARCHITECTURE

## 54. System architecture

Adapt the following proposed responsibilities to the existing Unity architecture.

- MilitarySystem.
- MilitaryCompany.
- MilitaryRecruitmentSystem.
- MilitaryEquipmentSystem.
- MilitaryTrainingSystem.
- MilitaryOrderSystem.
- MilitarySupplySystem.
- MilitaryMovementSystem.
- CombatSystem.
- SiegeSystem.
- SiegeEquipmentDefinition.
- SiegeEquipmentState.
- FortificationSystem.
- FortificationDefinition.
- FortificationUpgradeDefinition.
- DefensiveEquipmentDefinition.
- DefensiveAssignmentSystem.
- StructuralDamageSystem.
- MilitaryThreatSystem.
- MilitaryUI.

Do not create separate classes unnecessarily if existing systems already perform equivalent responsibilities.

Requirements:

- Separate simulation from presentation.
- Integrate with existing simulation ticks.
- Preserve deterministic behavior where practical.
- Centralize balancing configuration.
- Avoid expensive frame-by-frame economic calculations.
- Reuse pathfinding infrastructure.
- Support accelerated simulation speeds.
- Support future multi-settlement gameplay.
- Avoid per-soldier performance bottlenecks.
- Support persistent equipment and fortification state.

## 55. Pathfinding integration

Military pathfinding must account for:

- Tile passability.
- Fortification barriers.
- Gate states.
- Breaches.
- Water crossings.
- Terrain.
- Unit movement restrictions.
- Siege equipment footprints.
- Roads.
- Friendly and hostile access permissions.

Avoid recalculating the entire navigation network whenever a single structure changes if localized updates are practical.

Do not allow ordinary movement to bypass intact defensive barriers.

## 56. Save system

Persist:

- Military companies.
- Military personnel allocations.
- Militia registration.
- Mobilization state.
- Equipment inventories.
- Assigned equipment.
- Training and experience.
- Morale and readiness.
- Orders and positions.
- Siege engines.
- Siege engine crews.
- Ammunition.
- Deployment state.
- Fortification upgrades.
- Structural damage.
- Gate states.
- Breaches.
- Defensive assignments.
- Active siege state.

Version military save data.

Safely migrate existing settlements and barracks.

Preserve population and prevent duplication of military personnel during migration.

Document unavoidable save incompatibilities.

## 57. Performance requirements

The architecture must eventually support:

- Settlements containing thousands of residents.
- Hundreds of active military companies.
- Large defensive networks.
- Multiple settlements.
- Regional armies.
- Large-scale siege operations.

Avoid simulating every arrow, consumable and individual soldier as a persistent expensive Unity object.

Use aggregated simulation with appropriate visual effects.

Where individual movement is necessary for presentation, ensure it does not become the authoritative source of economic or population data.

---

# PART XIV — ART AND ASSETS

## 58. Visual consistency

Follow all existing Thornwick asset guidelines:

- Isometric projection.
- Consistent pixel-art scale.
- Correct tile footprints.
- Four-directional coherence.
- Transparent backgrounds where required.
- Existing palette and visual style.
- Correct depth sorting.

Read TEXTURE_SPEC.md and BUILDING_TILE_GUIDE.md before creating or modifying assets.

Reuse existing barracks artwork where appropriate.

Do not modify unrelated assets.

## 59. Required building assets

Identify required texture slots for:

- Muster grounds.
- Armories.
- Guardhouses.
- Training grounds.
- Military headquarters.
- Siege workshops.
- Engineering yards.
- Military supply depots.
- Stables.
- Cannon foundries.
- Fortified keeps.

## 60. Required troop assets

Provide visually distinguishable silhouettes for each troop category.

Support appropriate directional variations and eventually animations for:

- Idle.
- Walking.
- Running.
- Combat.
- Guarding.
- Operating equipment.

Do not require all 22 troop types to receive finished artwork before the foundational simulation is implemented.

## 61. Required siege equipment assets

Eventually provide:

- Basic battering ram.
- Covered battering ram.
- Scaling ladders.
- Mantlet.
- Ballista.
- Mangonel.
- Siege tower.
- Trebuchet.
- Bombard.
- Field cannon.
- Heavy siege cannon.
- Mortar.
- Supply wagon.
- Engineering wagon.
- Medical wagon.
- Military tents.

Support packed and deployed visual states where appropriate.

## 62. Required fortification assets

Support connected visual variants for:

- Palisades.
- Reinforced wooden walls.
- Stone walls.
- Reinforced masonry.
- Earth-backed walls.
- Corners.
- Gates.
- Gatehouses.
- Towers.
- Ditches.
- Moats.
- Ramparts.
- Hoardings.
- Machicolations.
- Portcullises.
- Barbicans.
- Bastions.
- Defensive equipment installations.
- Damaged and breached structures.

Prioritize functional construction and connectivity before exhaustive artistic variation.

Use clearly identified temporary placeholders when necessary.

Document all missing assets for future production.

---

# PART XV — FIRST-YEAR GAMEPLAY

## 63. Preserve early settlement survival

The existing first-year survival experience must remain functional.

The player should prioritize:

- Shelter.
- Sustainable food production.
- Hunting.
- Farming.
- Lumber production.
- Mining.
- Food storage.
- Winter preparation.

Professional soldiers should not be a mandatory first-year requirement.

An early settlement should be able to survive without constructing barracks.

Introduce military preparation through:

1. Establishing a sustainable settlement.
2. Constructing a muster ground.
3. Registering a small militia.
4. Understanding civilian workforce consequences.
5. Constructing inexpensive defensive infrastructure.
6. Establishing an observation position when appropriate.

Make early military preparation optional or dependent on the selected difficulty.

Do not create unavoidable early raids that make the existing survival tutorial unreliable.

---

# PART XVI — IMPLEMENTATION ROADMAP

## 64. Phase 1 — Military foundation

Implement and fully verify:

- Population-based recruitment.
- Militia registration.
- Professional soldiers.
- Workforce integration.
- Military company data.
- Muster grounds.
- Barracks accommodation.
- Mobilization.
- Demobilization.
- Military management interface.
- Save compatibility.

Initial troop types:

- Peasant levy.
- Spearman.
- Hunter archer.
- Shield infantry.

Do not move on while fundamental recruitment or population accounting remains broken.

## 65. Phase 2 — Local defensive construction

Implement:

- Wooden palisades.
- Basic gates.
- Wooden watchtowers.
- Defensive ditches.
- Connected wall placement.
- Physical movement barriers.
- Gate states.
- Guard assignments.
- Defensive staffing.
- Basic wall and gate damage.
- Repair functionality.

Ensure players can construct a functional fortified village.

## 66. Phase 3 — Military equipment

Implement:

- Basic equipment production.
- Equipment storage.
- Equipment assignment.
- Armories.
- Training.
- Experience.
- Morale.
- Readiness.
- Ammunition.
- Military upkeep.

Introduce additional infantry and ranged troop types.

## 67. Phase 4 — Initial offensive siege warfare

Implement:

- Basic battering rams.
- Scaling ladders.
- Covered battering rams.
- Mantlets.
- Ballistae.
- Siege workshops.
- Siege equipment crews.
- Equipment transportation.
- Deployment states.
- Structural attacks.
- Fortification breaches.
- Basic enemy raid integration.

Verify that gates, walls and siege equipment interact meaningfully.

## 68. Phase 5 — Active defensive engineering

Implement:

- Falling-stone positions.
- Heated-water stations.
- Boiling-oil stations.
- Defensive archery positions.
- Mounted ballistae.
- Defensive equipment staffing.
- Ammunition and consumable storage.
- Modular fortification upgrades.
- Defensive planning interface.

Ensure active defenses consume supplies and require real personnel.

## 69. Phase 6 — Advanced medieval warfare

Implement:

- Stone walls.
- Advanced gatehouses.
- Portcullises.
- Moats.
- Hoardings.
- Machicolations.
- Mangonels.
- Siege towers.
- Trebuchets.
- Engineering detachments.
- Advanced military companies.
- Persistent siege state.
- Multiple defensive perimeters.

## 70. Phase 7 — Regional warfare and logistics

Implement as the wider world system becomes mature:

- Field armies.
- Supply wagons.
- Military camps.
- Supply depots.
- Regional movement.
- Reinforcements.
- Trade route protection.
- Territorial control.
- Blockades.
- Regional campaigns.
- Settlement surrender and capture.

## 71. Phase 8 — Advanced eras

Eventually implement:

- Gunpowder infantry.
- Bombards.
- Field cannons.
- Heavy siege cannons.
- Mortars.
- Cannon foundries.
- Bastions.
- Advanced earthworks.
- Advanced military administration.
- Naval warfare.

Do not prematurely implement later-era mechanics at the expense of foundational stability.

---

# PART XVII — TESTING AND ACCEPTANCE CRITERIA

## 72. Population and military tests

Verify:

- Registering militia does not reduce civilian workforce.
- Mobilization correctly reduces civilian workforce.
- Demobilization restores workforce eligibility.
- Professional recruitment removes civilian workers.
- Discharge restores workforce eligibility.
- Recruitment respects eligible population.
- Barracks capacity is enforced.
- Soldiers are not counted twice.
- Food consumption is not duplicated.
- Casualties affect population correctly.
- Destroyed barracks do not delete soldiers.
- Companies cannot contain negative personnel.
- Personnel cannot occupy multiple assignments.

## 73. Equipment tests

Verify:

- Equipment requires actual resource production or acquisition.
- Equipment is correctly reserved and issued.
- Returned equipment is restored to inventory.
- Equipment cannot be duplicated.
- Ammunition consumption is persistent.
- Unstaffed equipment cannot operate.
- Damaged equipment requires repair.
- Insufficient crew affects operation.

## 74. Fortification tests

Verify:

- Wall segments connect correctly.
- Corners render appropriately.
- Intact walls block incompatible movement.
- Closed gates restrict access.
- Open gates permit appropriate access.
- Breaches update pathfinding.
- Damage persists.
- Repairs restore functionality.
- Defensive positions are limited by structure capacity.
- Destroyed structures invalidate associated assignments.
- Multiple defensive perimeters remain independently functional.

## 75. Siege equipment tests

Verify:

- Siege equipment requires production.
- Siege equipment requires assigned crew.
- Movement respects terrain restrictions.
- Deployment is required where applicable.
- Equipment uses ammunition where applicable.
- Different equipment affects different fortification materials appropriately.
- Abandoned equipment remains recoverable.
- Damaged equipment loses functionality.
- Destroyed equipment resolves crew outcomes correctly.
- Siege state persists through saving and loading.

## 76. Active defense tests

Verify:

- Unstaffed defensive installations remain inactive.
- Defensive installations consume appropriate supplies.
- Consumables cannot become negative.
- Defensive coverage respects compatible structures.
- Operators cannot be assigned simultaneously elsewhere.
- Destroyed walls disable associated equipment.
- Repairing infrastructure restores eligible defensive functionality.
- Automated defensive behavior respects current orders and ownership.

## 77. Economic tests

Run long-duration simulations to determine:

- Whether small settlements survive without professional soldiers.
- Whether a 200-person settlement can sustain a modest garrison.
- Whether military recruitment meaningfully affects agricultural production.
- Whether large armies create realistic resource pressures.
- Whether prolonged mobilization affects economic output.
- Whether demobilization restores civilian productivity.
- Whether military production integrates with existing industries.
- Whether prolonged sieges meaningfully affect food reserves and trade.

Do not balance the military in isolation from the settlement economy.

## 78. Performance tests

Test:

- Large company counts.
- Large fortified settlements.
- Extensive connected wall networks.
- Multiple active siege engines.
- Accelerated simulation speeds.
- Pathfinding changes during breaches.
- Saving and loading large military states.

Avoid substantial performance regression in ordinary settlement gameplay.

---

# PART XVIII — FINAL DELIVERABLES

## 79. Development requirements

After each implementation phase:

1. Compile the Unity project.
2. Run relevant automated tests.
3. Resolve compilation errors.
4. Resolve military-related test failures.
5. Verify functionality in the playable Unity scene.
6. Document new systems.
7. Document balancing parameters.
8. Document missing assets.
9. Document remaining future-phase requirements.
10. Identify architectural decisions and implementation limitations.

Do not claim features are implemented merely because a class, UI panel or placeholder exists.

A feature is implemented only when connected to the playable simulation and verified.

## 80. Final design principle

The final system should make military power an extension of civilization development.

A player who builds an economically prosperous but poorly defended city should face different risks from a player who builds a smaller, carefully fortified mountain stronghold.

A player maintaining a professional army should sacrifice civilian productivity in exchange for security and military capability.

A player attempting to capture a sophisticated fortress should require appropriate manpower, engineering capability, supplies and economic preparation.

The design of cities, roads, walls, gates, towers, defensive equipment, storage infrastructure and regional trade networks should have direct consequences during warfare.

Military victory should emerge from preparation, organization, economic strength, geography, technology and strategic decisions.

Thornwick should remain a deep city-building and civilization simulation with meaningful warfare, not become a conventional unit-spamming RTS.

Begin by auditing the existing implementation, then complete the foundational phases in order. Preserve working gameplay and report accurately what has been completed.
