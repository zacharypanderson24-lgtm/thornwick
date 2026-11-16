// ---------- recalc: services, coverage and the workforce, each redone only when its inputs changed ----------
// Dependency map (what invalidates what):
//   road added / removed      -> conn (sim_roads.js) -> doors faces + "served" of the buildings touching the changed tiles
//   building served flips     -> house level + market cover of houses in range (if it is a market), jobs and workforce, totals
//   well / market built or removed -> only the houses within its radius (house level, market cover)
//   job building built / removed / served flips, peasants change, trade priority change -> workforce allocation
// recalc(S) stays the one entry point: the game calls it after every action and every day. With nothing changed it is almost free.
function markServeAround(S, C, tiles) {
  for (const t of tiles) {
    const x = t % N, y = (t / N) | 0, g = S.grid[y][x];
    if (g && g.t === 'road') g.served = !!C.conn[t];
    touch(S, x, y, 0);   // lots and work sites near a tile whose connection changed may have become (in)eligible
    for (let k = 0; k < 4; k++) { const nx = x + NB[k][0], ny = y + NB[k][1]; if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue; const o = S.grid[ny][nx]; if (o && o.t !== 'road') C.pendServe.add(o); }
  }
}
// One building's face and service state from the current roads. Returns true when "served" flipped.
function serveOne(S, C, b) {
  if (b.t === 'hall') { b.served = true; b.face = faceOf(S, b); return false; }
  b.face = faceOf(S, b);
  const was = b.served; b.served = servedAt(S, b, C.conn);
  if (was === b.served) return false;
  C.topoSeq++; C.totalsDirty = C.workDirty = true;
  if (b.t === 'house') C.pendHouse.add(b); else if (b.t === 'market') markHouses(C, b, marketR(b));
  return true;
}
// House level (well in range) and market cover. Mirrors the original rules: level is set for every house, cover only for served ones.
function houseOne(S, C, b, mk) {
  const wells = listOf(C, 'well');
  b.lvl = (b.served && wells.some(w => cheb(w, b) <= WELL_R)) ? 2 : 1;
  if (b.served) b.covered = mk.some(m => cheb(m, b) <= marketR(m));
}
const servedMarkets = C => listOf(C, 'market').filter(b => b.served);

function rebuildAll(S, C) {
  C.roadOps = []; C.pendServe.clear(); C.pendHouse.clear();
  buildLists(S, C); computeRoots(S, C); rebuildConn(S, C);
  for (const b of S.blds) {
    if (b.t === 'road') b.served = !!C.conn[b.y * N + b.x];
    else serveOne(S, C, b);
  }
  C.pendServe.clear(); C.pendHouse.clear();
  const mk = servedMarkets(C);
  for (const b of listOf(C, 'house')) houseOne(S, C, b, mk);
  C.dev = {}; C.reach = new WeakMap(); C.folk = null;
  C.topoSeq++; C.totalsDirty = C.workDirty = true; C.ready = true;
}

function recalc(S, force) {
  const C = cacheOf(S);
  if (force || !C.ready) rebuildAll(S, C);
  else {
    if (C.roadOps.length) markServeAround(S, C, applyRoadOps(S, C));
    if (C.pendServe.size) { const ps = Array.from(C.pendServe); C.pendServe.clear(); for (const b of ps) serveOne(S, C, b); }
    if (C.pendHouse.size) { const mk = servedMarkets(C), hs = Array.from(C.pendHouse); C.pendHouse.clear(); for (const b of hs) houseOne(S, C, b, mk); C.totalsDirty = true; }
  }
  const prioSig = Array.isArray(S.prio) ? S.prio.join(',') : '';
  if (S.d && !C.totalsDirty && !C.workDirty && C.lastPeas === S.peasants && C.lastPrio === prioSig) return S.d;
  return refreshD(S, C, prioSig);
}
function refreshD(S, C, prioSig) {
  const L = C.L;
  if (C.totalsDirty || !C.tot) {
    let civ = HALL_CAP, bunks = 0, jobs = 0, markets = 0, houseCap = 0, covCap = 0;
    for (const b of listOf(C, 'house')) if (b.served) { const cap = capOf(b); civ += cap; houseCap += cap; if (b.covered) covCap += cap; }
    for (const b of listOf(C, 'barracks')) if (b.served) bunks += BUNKS;
    for (const b of L.jobs) if (b.served) { jobs += jobsOf(b); if (b.t === 'market') markets++; }
    C.tot = { civ, bunks, jobs, markets, houseCap, covCap };
  }
  // Fill the jobs: villagers go to trades in priority order, and within a trade to the oldest buildings first.
  const prio = (Array.isArray(S.prio) && S.prio.length === TRADES.length && TRADES.every(t => S.prio.includes(t))) ? S.prio : TRADES;
  let avail = S.peasants; const work = {};
  for (const t of prio) work[t] = { workers: 0, jobs: 0, blds: 0, short: 0 };
  for (const b of L.jobs) { b.workers = 0; b.eff = 0; }
  for (const t of prio) for (const b of (L.trade[t] || EMPTY)) if (b.served) {
    const j = jobsOf(b), w = Math.min(j, avail); avail -= w; b.workers = w; b.eff = j ? w / j : 0;
    const r = work[t]; r.workers += w; r.jobs += j; r.blds++; if (w < j) r.short++;
  }
  let mkW = 0, mkJ = 0; for (const b of listOf(C, 'market')) if (b.served) { mkW += b.workers; mkJ += jobsOf(b); }
  const T = C.tot;
  S.d = { civ: T.civ, bunks: T.bunks, jobs: T.jobs, work, prio, idle: avail, employed: S.peasants - avail, mkEff: mkJ ? mkW / mkJ : 0, conn: C.conn, markets: T.markets, houseCap: T.houseCap, covCap: T.covCap, cov: T.houseCap ? T.covCap / T.houseCap : 1, ratio: T.jobs ? Math.min(1, S.peasants / T.jobs) : 0 };
  C.totalsDirty = C.workDirty = false; C.lastPeas = S.peasants; C.lastPrio = prioSig;
  return S.d;
}
// Recompute everything from scratch (the original recalc) WITHOUT touching the world or the caches and report any field where the caches disagree.
// Used by the tests after random play; cheap enough to call every day in a long run.
function auditCaches(S) {
  const bad = [], conn = new Uint8Array(N * N), h = S.hall, q = [];
  const isRoad = (x, y) => inb(x, y) && S.grid[y][x] && S.grid[y][x].t === 'road';
  for (const [hx, hy] of tilesOf(h)) for (const [dx, dy] of NB) { const x = hx + dx, y = hy + dy; if (inb(x, y) && S.grid[y][x] !== h && isRoad(x, y) && !conn[y * N + x]) { conn[y * N + x] = 1; q.push([x, y]); } }
  while (q.length) { const [cx, cy] = q.pop(); for (const [dx, dy] of NB) { const x = cx + dx, y = cy + dy; if (isRoad(x, y) && !conn[y * N + x]) { conn[y * N + x] = 1; q.push([x, y]); } } }
  const C = cacheOf(S); recalc(S);
  for (let i = 0; i < N * N; i++) if ((conn[i] | 0) !== (C.conn[i] | 0)) { bad.push('conn@' + i % N + ',' + ((i / N) | 0) + ' want ' + conn[i]); if (bad.length > 5) return bad; }
  const wells = S.blds.filter(b => b.t === 'well'), mk = S.blds.filter(b => b.t === 'market' && b.served);
  let civ = HALL_CAP, bunks = 0, jobs = 0, markets = 0, houseCap = 0, covCap = 0;
  for (const b of S.blds) {
    if (b.t === 'hall') continue;
    if (b.t === 'road') { if (b.served !== !!conn[b.y * N + b.x]) bad.push('road served ' + b.x + ',' + b.y); continue; }
    const sv = servedAt(S, b, conn); if (b.served !== sv) bad.push(b.t + ' served ' + b.x + ',' + b.y + ' want ' + sv);
    const fc = faceOf(S, b); if (b.face !== fc) bad.push(b.t + ' face ' + b.x + ',' + b.y + ' want ' + fc);
    if (b.t === 'house') {
      const lv = (sv && wells.some(w => cheb(w, b) <= WELL_R)) ? 2 : 1; if (b.lvl !== lv) bad.push('house lvl ' + b.x + ',' + b.y + ' want ' + lv);
      if (sv) { civ += capOf(b); houseCap += capOf(b); const cv = mk.some(m => cheb(m, b) <= marketR(m)); if (b.covered !== cv) bad.push('house covered ' + b.x + ',' + b.y + ' want ' + cv); if (cv) covCap += capOf(b); }
    } else if (b.t === 'barracks') { if (sv) bunks += BUNKS; }
    else if (JOBS[b.t] && sv) { jobs += jobsOf(b); if (b.t === 'market') markets++; }
  }
  const d = S.d, same = (k, v) => { if (d[k] !== v) bad.push('d.' + k + ' ' + d[k] + ' want ' + v); };
  same('civ', civ); same('bunks', bunks); same('jobs', jobs); same('markets', markets); same('houseCap', houseCap); same('covCap', covCap);
  const prio = d.prio; let avail = S.peasants;
  for (const t of prio) for (const b of S.blds) if (TRADE_OF[b.t] === t && b.served) { const j = jobsOf(b), w = Math.min(j, avail); avail -= w; if (b.workers !== w) bad.push(b.t + ' workers ' + b.x + ',' + b.y + ' ' + b.workers + ' want ' + w); }
  same('idle', avail);
  return bad;
}
