// ---------- The simulation clock and scheduler ----------
// One deterministic clock: S.day (saved with the world). A day is one tick() call. Everything that happens in a day is a named task in the TASKS table below,
// run in table order; a task says how often it runs (every N days, from an offset), so slower systems (a yearly harvest, a monthly census) and faster ones
// (movement and combat can take several sub-steps per day) slot in without new timers. Right now every task runs every day, in exactly the order the
// original tick() used, so gameplay and numbers are unchanged.
// Event-driven work (roads, workforce, services, development candidates) is not scheduled: it is done on demand through the dirty flags and chunk stamps in
// sim_cache.js / sim_econ.js, so it costs nothing on days when nothing changed.
//   stage 'sync'       bring derived data (services, workforce) up to date
//   stage 'production' daily production (farms, foresters, sawmills)
//   stage 'movement'   crews and townsfolk step (this is where combat / formations will step, several times a day if wanted)
//   stage 'nature'     stumps rot, saplings grow
//   stage 'economy'    consumption, income, population, military training, summaries
//   stage 'develop'    automatic development grows new buildings
const TASKS = [];
function schedule(name, stage, every, offset, run) { TASKS.push({ name, stage, every, offset, run }); }
function runTasks(S, T) { for (const t of TASKS) if (t.every === 1 || ((S.day - t.offset) % t.every + t.every) % t.every === 0) t.run(S, T); }

schedule('sync', 'sync', 1, 0, (S, T) => { recalc(S); T.d = S.d; T.before = { ...S.res }; T.employed = T.d.employed; T.food = 0; T.wood = 0; T.logs = 0; T.stone = 0; T.ore = 0; });
schedule('farms+foresters', 'production', 1, 0, (S, T) => {
  const C = cacheOf(S);
  for (const b of listOf(C, 'farm')) if (b.served) T.food += foodOf(b) * (b.eff || 0);
  for (const b of listOf(C, 'forester')) if (b.served) { const e = b.eff || 0; if (e > 0) { b.prog += e; if (b.prog >= PLANT_EVERY) { if (plantSapling(S, b.x, b.y, FORESTER_R)) b.prog = 0; else b.prog = PLANT_EVERY; } } }
});
// Woodcutters, quarrymen and miners walk out to their marked tiles and carry loads home; only delivered loads count.
schedule('crews', 'movement', 1, 0, (S, T) => { const got = { logs: 0, stone: 0, ore: 0 }; stepCrews(S, got); T.logs += got.logs; T.stone += got.stone; T.ore += got.ore; });
schedule('townsfolk', 'movement', 1, 0, (S, T) => { stepFolk(S); });
schedule('deliver logs', 'production', 1, 0, (S, T) => { S.res.logs = (S.res.logs || 0) + T.logs; });
// Sawmills first (full yield, limited by workers), then the Town Hall pit-saw for what is left.
schedule('sawing', 'production', 1, 0, (S, T) => {
  for (const b of listOf(cacheOf(S), 'sawmill')) if (b.served && S.res.logs > 0) { const c = Math.min(S.res.logs, SAW_RATE * b.eff); S.res.logs -= c; T.wood += c; }
  if (S.res.logs > 0) { const c = Math.min(S.res.logs, HALL_SAW); S.res.logs -= c; T.wood += c * HALL_YIELD; }
});
schedule('nature', 'nature', 1, 0, (S, T) => {
  // Stumps rot away into open, buildable grass.
  if (S.stumpT) for (const k in S.stumpT) if (--S.stumpT[k] <= 0) { delete S.stumpT[k]; const y = Math.floor(k / N), x = k % N; if (S.deco[y][x] === 'stump') { S.deco[y][x] = null; touch(S, x, y, F_DECO); } }
  // Saplings grow into trees (the forester's tile stays in its Lumber zone, ready for felling again).
  if (S.sapT) for (const k in S.sapT) if (--S.sapT[k] <= 0) { delete S.sapT[k]; const y = Math.floor(k / N), x = k % N; if (S.deco[y][x] === 'sapling') { S.deco[y][x] = 'tree'; touch(S, x, y, F_DECO); } }
});
schedule('economy', 'economy', 1, 0, (S, T) => {
  const d = T.d, before = T.before;
  const trade = d.markets ? 0.04 * S.peasants * d.cov * d.mkEff : 0;
  const eat = (S.peasants + S.soldiers * 1.5) * 0.2;
  S.res.food += T.food - eat; S.res.wood += T.wood; S.res.stone += T.stone; S.res.ore += T.ore;
  S.res.gold += T.employed * 0.12 + trade - S.soldiers * 0.1;
  S.starving = false;
  if (S.res.food <= 0) {
    S.res.food = 0; S.starving = true;
    if (S.peasants > 0) S.peasants--; else if (S.soldiers > 0) S.soldiers--;
  } else {
    const free = Math.max(0, 1 - S.peasants / Math.max(1, d.civ));
    const births = S.peasants * 0.04 * free * (S.res.food > 40 ? 1 : 0.5);
    S.popAcc += births - S.peasants * 0.004 + (S.peasants < 2 ? 0.05 : 0);
    while (S.popAcc >= 1 && S.peasants < d.civ) { S.peasants++; S.popAcc -= 1; }
    if (S.popAcc > 0.99) S.popAcc = 0.99;
    while (S.popAcc <= -1 && S.peasants > 0) { S.peasants--; S.popAcc += 1; }
  }
  if (S.peasants > d.civ) S.peasants--;
  if (S.soldiers > d.bunks) S.soldiers--;
  for (const b of listOf(cacheOf(S), 'barracks')) if (b.served) {
    b.prog = Math.min(4, b.prog + 1);
    if (b.prog >= 4 && S.drafting && S.soldiers < d.bunks && S.peasants >= 3 && S.res.wood >= 2 && S.res.gold >= 1 && S.res.ore >= 1) {
      S.soldiers++; S.peasants--; S.trained++; S.res.wood -= 2; S.res.gold -= 1; S.res.ore -= 1; b.prog = 0;
    }
  }
  for (const k in S.res) S.res[k] = Math.max(0, Math.min(STORAGE, S.res[k]));
  for (const k in S.res) S.rate[k] = (k === 'logs' || k === 'stone' || k === 'ore') ? 0.85 * (S.rate[k] || 0) + 0.15 * (S.res[k] - before[k]) : S.res[k] - before[k];   // loads arrive in lumps, so smooth those rates
  S.day++;
});
schedule('develop', 'develop', 1, 0, (S, T) => { recalc(S); develop(S); });
function tick(S) { runTasks(S, {}); }

if (typeof module !== 'undefined') module.exports = { folkOf, FOLK_MAX, FOLK_VARIANTS, crewOf, CREW_KIND, WORK_T, LOAD, CHOPS_PER_TREE, stepCrews, PLANT_EVERY, SAPLING_DAYS, FORESTER_R, plantSapling, foresterCovers, TRADES, TRADE_OF, TRADE_NAMES, jobsOf, recalc, treeGrove, treeSpecies, treeVariant, TREE_SPECIES, hasTree, saveWorld, walkCost, loadWorld, growSave, START, elevOf, vh, tileCorners, tileLevel, tileSpread, tileFlat, genHeights, levelFootprint, deriveHeights, relaxHeights, HMIN, HMAX, MOUNT_LEVEL, LVL_PX, digDone, rockCap, rockLeft, rockLevel, rockHollow, oreLeft, pitDepth, mineRock, mineOre, rockWorkable, DIG_U, MOUNT_CAP, ORE_CAP, isMountain, isSolid, rockOpen, rockAmt, countZoned, capOf, foodOf, nameOf, roadPathCheck, placeRoadPath, N, COST, NAMES, JOBS, createWorld, canPlace, place, remove, tick, countDeco, addBld, setZone, zoneCheck, computeDemand,
  CHUNK, CH, claimed, cacheOf, invalidateCaches, touch, touchBox, groundStamp, decoStamp, auditCaches, schedule, TASKS };
