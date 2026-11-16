// ---------- Work crews and townsfolk (movement) ----------
// Work crews: woodcutters, quarrymen and miners live at their lodge/mine, walk out to a marked tile, work, and carry the load home.
// A worker is in one of four states: 'home' (resting inside the building, not drawn), 'out' (walking to the site), 'work' (at the site), 'back' (carrying the load).
// Crews are runtime only (never saved): after loading, everybody starts at home. Output comes only from loads delivered at the building, so farther sites make slower trips.
//
// PATHFINDING: the walkable area around each work building (a BFS out to CREW_DEPTH steps) is cached per building and reused by every worker and every day
// until something that affects walking (a building, road, rock, pit, ore or height) changes within reach of it. Which tile a worker picks still depends on the
// day and on who else is out, so that choice is made fresh from the cached area each time. Trips already under way keep their route, as before.
const CREW = new WeakMap();
const crewOf = b => { let c = CREW.get(b); if (!c) { c = []; CREW.set(b, c); } return c; };
const CREW_KIND = { wood: 'tree', quarry: 'rock', mine: 'ore' };
const WORK_T = 2, CHOPS_PER_TREE = 4, LOAD = { wood: 6, quarry: 5, mine: 4 }, CREW_DEPTH = 10;   // 4 chops x 6 logs = 24 logs per tree
const REACH_W = 2 * CREW_DEPTH + 1, rcQ = new Int32Array(REACH_W * REACH_W);
function crewMatch(S, kind, x, y) {
  if (!claimed(S, x, y)) return false;   // ownership: no extraction on land the player does not hold (walking there is still fine)
  const dc = S.deco[y][x];
  if (kind === 'tree') return dc === 'tree' && S.zone[y][x] === 'wood';
  if (kind === 'rock') return dc === 'rock' && S.zone[y][x] === 'mine' && rockWorkable(S, x, y);
  return dc === 'ore';
}
// The tiles a worker from b can walk to within CREW_DEPTH steps: dist (-1 = unreached) and parent, in a (2D+1)^2 window around b. Cached until walking changes nearby.
function crewReach(S, b) {
  const C = cacheOf(S), D = CREW_DEPTH, ox = b.x - D, oy = b.y - D;
  const stamp = stampOver(C.wSeq, ox, oy, b.x + D, b.y + D);
  let R = C.reach.get(b);
  if (R && R.stamp === stamp) return R;
  if (!R) R = { dist: new Int16Array(REACH_W * REACH_W), par: new Int32Array(REACH_W * REACH_W), ox, oy };
  R.stamp = stamp; R.dist.fill(-1);
  const dist = R.dist, par = R.par, s0 = b.y * N + b.x, l0 = (b.y - oy) * REACH_W + (b.x - ox);
  dist[l0] = 0; par[l0] = -1; rcQ[0] = s0; let qn = 1;
  for (let h = 0; h < qn; h++) {
    const c = rcQ[h], cx = c % N, cy = (c - cx) / N, dd = dist[(cy - oy) * REACH_W + (cx - ox)]; if (dd >= D) continue;
    for (let k = 0; k < 4; k++) {
      const nx = cx + NB[k][0], ny = cy + NB[k][1]; if (!inb(nx, ny)) continue;
      const nl = (ny - oy) * REACH_W + (nx - ox); if (dist[nl] >= 0) continue;
      const g = S.grid[ny][nx]; if (g && g !== b && g.t !== 'road') continue;
      if (walkCost(S, cx, cy, nx, ny) === Infinity) continue;
      dist[nl] = dd + 1; par[nl] = c; rcQ[qn++] = ny * N + nx;
    }
  }
  C.reach.set(b, R); return R;
}
// Nearest-by-walking marked tile within WORK_R of the building. Returns { tx, ty, path } where path runs from the building to a tile beside the target, or null.
function crewTarget(S, b, kind, salt, used) {
  const R = crewReach(S, b), dist = R.dist, ox = R.ox, oy = R.oy, s0 = b.y * N + b.x;
  let best = null, bs = 1e9;
  for (let j = b.y - WORK_R; j <= b.y + WORK_R; j++) for (let i = b.x - WORK_R; i <= b.x + WORK_R; i++) {
    if (!inb(i, j) || !crewMatch(S, kind, i, j)) continue;
    for (const [dx, dy] of NB) {
      const nx = i + dx, ny = j + dy; if (!inb(nx, ny)) continue;
      const ni = ny * N + nx, dd = dist[(ny - oy) * REACH_W + (nx - ox)]; if (dd < 0 || ni === s0) continue;   // workers step out of the building, never work on its roof
      const sc = dd + hash2(i, j, 31 + (salt || 0)) * 0.9 + (used && used.has(ni) ? 2.5 : 0);   // tie-break so workers spread over equally near tiles
      if (sc < bs) { bs = sc; best = [i, j, ni]; }
    }
  }
  if (!best) return null;
  const path = []; for (let c = best[2]; c !== -1; c = R.par[(((c / N) | 0) - oy) * REACH_W + (c % N - ox)]) path.push([c % N, Math.floor(c / N)]);
  path.reverse();
  return { tx: best[0], ty: best[1], path };
}
function crewAct(S, b, kind, a) {
  const { tx, ty } = a;
  if (kind === 'tree') {
    if (!crewMatch(S, kind, tx, ty)) return 0;
    S.chop = S.chop || {}; const k = tk(tx, ty); S.chop[k] = (S.chop[k] || 0) + 1;
    if (S.chop[k] >= CHOPS_PER_TREE) fellAt(S, tx, ty);
    return LOAD.wood;
  }
  if (kind === 'rock') return mineRock(S, tx, ty, 0, LOAD.quarry);
  return mineOre(S, tx, ty, 0, LOAD.mine);
}
// Advance every crew one day. `out` collects what was delivered this day (logs, stone, ore).
function stepCrews(S, out) {
  for (const b of cacheOf(S).L.crews.slice()) {
    const kind = CREW_KIND[b.t];
    const crew = crewOf(b), want = b.workers || 0, res = kind === 'tree' ? 'logs' : kind === 'rock' ? 'stone' : 'ore';
    for (let i = crew.length - 1; i >= 0 && crew.length > want; i--) if (crew[i].st === 'home') crew.splice(i, 1);   // surplus workers who are resting leave; those out finish their trip first
    while (crew.length < want) crew.push({ st: 'home', x: b.x, y: b.y, px: b.x, py: b.y, path: null, i: 0, t: 0, carry: 0, tx: 0, ty: 0, stand: -1 });
    for (const a of crew) {
      a.px = a.x; a.py = a.y;
      if (a.st === 'home') {
        if (!b.served) continue;
        if (kind !== 'tree' && S.res[res] + out[res] >= STORAGE) continue;   // full stockpile: the crew stays home and keeps its reserve in the ground
        const used = new Set(); for (const o of crew) if (o !== a && o.st !== 'home' && o.stand >= 0) used.add(o.stand);   // spread the crew over different standing tiles
        const tg = crewTarget(S, b, kind, crew.indexOf(a) * 7 + S.day, used); if (!tg) continue;
        a.path = tg.path; a.stand = tg.path[tg.path.length - 1][1] * N + tg.path[tg.path.length - 1][0]; a.tx = tg.tx; a.ty = tg.ty; a.i = 0; a.carry = 0;
        if (a.path.length > 1) { a.i = 1; a.x = a.path[1][0]; a.y = a.path[1][1]; }
        a.st = a.i === a.path.length - 1 ? 'work' : 'out'; if (a.st === 'work') a.t = WORK_T;
      } else if (a.st === 'out') {
        a.i++; a.x = a.path[a.i][0]; a.y = a.path[a.i][1];
        if (a.i === a.path.length - 1) { a.st = 'work'; a.t = WORK_T; }
      } else if (a.st === 'work') {
        if (--a.t <= 0) { a.carry = crewAct(S, b, kind, a); a.path.reverse(); a.i = 0; a.st = 'back'; }
      } else {   // back
        if (a.i < a.path.length - 1) { a.i++; a.x = a.path[a.i][0]; a.y = a.path[a.i][1]; }
      }
      if (a.st === 'back' && a.i >= a.path.length - 1) { out[res] += a.carry; a.carry = 0; a.st = 'home'; a.path = null; a.stand = -1; a.x = b.x; a.y = b.y; }
    }
  }
}
// ---- Townsfolk: tiny ambient villagers who live in houses and stroll along the roads ----
// Purely cosmetic and runtime only (never saved, rebuilt after loading): they do not change the economy, they make the town look alive.
// The number on screen follows the population (FOLK_PER_PEASANT per villager, at most FOLK_MAX), so the crowd stays small however big the city gets;
// the economy never looks at them. Each one rests in a house (hidden), sets off along the roads to somewhere, lingers a few days, then walks home.
const FOLK = new WeakMap();
const FOLK_MAX = 90, FOLK_PER_PEASANT = 0.6, FOLK_DEPTH = 40, FOLK_VARIANTS = 6;
const folkOf = S => { let f = FOLK.get(S); if (!f) { f = []; FOLK.set(S, f); } return f; };
const roadSeen = new Int32Array(N * N), roadPar = new Int32Array(N * N), roadDist = new Int16Array(N * N); let roadStamp = 0;
const isRoadAt = (S, x, y) => inb(x, y) && !!S.grid[y][x] && S.grid[y][x].t === 'road';
// A road tile touching the building (preferring the one its door faces).
function roadBeside(S, b) {
  const f = faceOf(S, b), T = tilesOf(b);
  for (const [tx, ty] of T) { const x = tx + FACE_DIRS[f][0], y = ty + FACE_DIRS[f][1]; if (isRoadAt(S, x, y)) return y * N + x; }
  for (const [tx, ty] of T) for (const [dx, dy] of NB) if (isRoadAt(S, tx + dx, ty + dy)) return (ty + dy) * N + tx + dx;
  return -1;
}
// Walk the road network from one road tile: returns the reached tiles in order; roadPathTo gives the path to any of them.
function roadSearch(S, from) {
  roadStamp++; roadSeen[from] = roadStamp; roadDist[from] = 0; roadPar[from] = -1;
  const q = [from];
  for (let h = 0; h < q.length; h++) {
    const c = q[h], cx = c % N, cy = (c - cx) / N, d = roadDist[c]; if (d >= FOLK_DEPTH) continue;
    for (const [dx, dy] of NB) { const nx = cx + dx, ny = cy + dy; if (!isRoadAt(S, nx, ny)) continue; const ni = ny * N + nx; if (roadSeen[ni] === roadStamp) continue; roadSeen[ni] = roadStamp; roadDist[ni] = d + 1; roadPar[ni] = c; q.push(ni); }
  }
  return q;
}
function roadPathTo(to) { const p = []; for (let c = to; c !== -1; c = roadPar[c]) p.push([c % N, Math.floor(c / N)]); return p.reverse(); }
// Houses that can send out a villager and the places they walk to; recomputed only when buildings, roads or service change.
function folkPlaces(S) {
  const C = cacheOf(S);
  if (C.folk && C.folk.topo === C.topoSeq) return C.folk;
  const houses = listOf(C, 'house').filter(b => b.served && roadBeside(S, b) >= 0);
  const pois = S.blds.filter(b => b.t === 'market' || b.t === 'well' || b.t === 'hall' || b.t === 'sawmill' || b.t === 'barracks');
  C.folk = { topo: C.topoSeq, houses, set: new Set(houses), pois };
  return C.folk;
}
function stepFolk(S) {
  const folk = folkOf(S), P = folkPlaces(S), houses = P.houses;
  const want = houses.length ? Math.min(FOLK_MAX, Math.round(S.peasants * FOLK_PER_PEASANT)) : 0;
  for (let i = folk.length - 1; i >= 0 && folk.length > want; i--) if (folk[i].st === 'home' || !P.set.has(folk[i].home)) folk.splice(i, 1);
  let nextId = folk.reduce((m, w) => Math.max(m, w.id + 1), 0);
  while (folk.length < want && houses.length) {
    const h = houses[Math.floor(hash2(nextId, S.day, 91) * houses.length)];
    folk.push({ id: nextId++, st: 'home', home: h, x: h.x, y: h.y, px: h.x, py: h.y, path: null, i: 0, t: 1 + Math.floor(hash2(nextId, 5, 7) * 8), v: Math.floor(hash2(nextId, 3, 11) * FOLK_VARIANTS) });
  }
  const pois = P.pois;
  for (const w of folk) {
    w.px = w.x; w.py = w.y;
    if (w.st === 'home') {
      if (--w.t > 0) continue;
      const door = roadBeside(S, w.home); if (door < 0) { w.t = 6; continue; }
      const q = roadSearch(S, door);
      if (q.length < 2) { w.t = 6 + Math.floor(hash2(w.id, S.day, 13) * 6); continue; }   // no road to walk on
      let dest = -1;
      const r = hash2(w.id, S.day, 17);
      if (r < 0.6 && pois.length) { const p = pois[Math.floor(hash2(w.id, S.day, 19) * pois.length)], rb = roadBeside(S, p); if (rb >= 0 && roadSeen[rb] === roadStamp && rb !== door) dest = rb; }
      if (dest < 0) { const k = 1 + Math.floor(hash2(w.id, S.day, 23) * (q.length - 1)); dest = q[k]; }   // otherwise a wander to some reachable road tile
      w.path = roadPathTo(dest); w.i = 0; w.x = w.path[0][0]; w.y = w.path[0][1]; w.st = 'walk';
    } else if (w.st === 'walk') {
      if (w.i < w.path.length - 1) { w.i++; w.x = w.path[w.i][0]; w.y = w.path[w.i][1]; }
      if (w.i >= w.path.length - 1) { w.st = 'stay'; w.t = 2 + Math.floor(hash2(w.id, S.day, 29) * 5); }
    } else if (w.st === 'stay') {
      if (--w.t <= 0) { w.path.reverse(); w.i = 0; w.st = 'back'; }
    } else {   // back
      if (w.i < w.path.length - 1) { w.i++; w.x = w.path[w.i][0]; w.y = w.path[w.i][1]; }
      if (w.i >= w.path.length - 1) { w.st = 'home'; w.path = null; w.x = w.home.x; w.y = w.home.y; w.t = 3 + Math.floor(hash2(w.id, S.day, 31) * 10); }
    }
  }
}
