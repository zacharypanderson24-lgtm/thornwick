'use strict';
// ---------- Simulation (no DOM) ----------
const N = 40;
const COST = {
  road: { wood: 1 }, house: { wood: 10 }, farm: { wood: 8 }, wood: { wood: 6 }, quarry: { wood: 10 }, mine: { wood: 12 },
  market: { wood: 12 }, well: { wood: 5, stone: 5 }, barracks: { wood: 20, stone: 15, gold: 20 }
};
const NAMES = {
  road: 'Dirt Road', house: 'Timber House', farm: 'Farm', wood: 'Woodcutter', quarry: 'Quarry', mine: 'Iron Mine', market: 'Market',
  well: 'Well', barracks: 'Barracks', hall: 'Town Hall',
  zoneRes: 'Housing Zone', zoneFarm: 'Farm Zone', zoneComm: 'Commerce Zone', zoneWood: 'Lumber Zone', zoneMine: 'Mining Zone'
};
const ZONE_NAMES = { res: 'Housing zone', farm: 'Farm zone', comm: 'Commerce zone', wood: 'Lumber zone', mine: 'Mining zone' };
const ZONE_KINDS = ['res', 'farm', 'comm', 'wood', 'mine'];
const JOBS = { farm: 4, wood: 3, quarry: 3, mine: 3, market: 2 };
const HOUSE_CAP = [0, 4, 6];
const BUNKS = 6, HALL_CAP = 6, WELL_R = 4, WORK_R = 3, MARKET_R = 5, STORAGE = 400;
const NB = [[1, 0], [-1, 0], [0, 1], [0, -1]];
const COST2 = { house: { wood: 30 }, farm: { wood: 24 }, market: { wood: 32 } };
const costOf = (type, sz) => (sz === 2 && COST2[type]) ? COST2[type] : (COST[type] || {});
const capOf = b => b.sz === 2 ? [0, 14, 20][b.lvl] : HOUSE_CAP[b.lvl];
const jobsOf = b => Math.round((JOBS[b.t] || 0) * (b.sz === 2 ? 2.5 : 1));
const foodOf = b => 3 * (b.sz === 2 ? 2.6 : 1);
const marketR = b => b.sz === 2 ? 7 : 5;
const nameOf = b => b.sz === 2 ? ({ house: 'Large House', farm: 'Large Farm', market: 'Bazaar' }[b.t] || NAMES[b.t]) : NAMES[b.t];
const tilesOf = b => { const a = []; for (let j = 0; j < (b.sz || 1); j++) for (let i = 0; i < (b.sz || 1); i++) a.push([b.x + i, b.y + j]); return a; };

function hash2(x, y, s) {
  let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul((s || 0) | 0, 2147483647)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
function vnoise(x, y, s) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const sm = t => t * t * (3 - 2 * t);
  const a = hash2(xi, yi, s), b = hash2(xi + 1, yi, s), c = hash2(xi, yi + 1, s), d = hash2(xi + 1, yi + 1, s);
  const u = sm(xf), v = sm(yf);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
const inb = (x, y) => x >= 0 && y >= 0 && x < N && y < N;
const ctr = o => (o.sz || 1) > 1 ? ((o.sz || 1) - 1) / 2 : 0;
const cheb = (a, b) => Math.max(Math.abs(a.x + ctr(a) - b.x - ctr(b)), Math.abs(a.y + ctr(a) - b.y - ctr(b)));
const clamp1 = v => Math.max(-1, Math.min(1, v));

function createWorld(seed) {
  seed = seed || 7;
  const S = {
    seed, grid: [], deco: [], zone: [], fert: [], blds: [], hall: null,
    res: { food: 60, wood: 120, stone: 30, ore: 10, gold: 80 }, rate: { food: 0, wood: 0, stone: 0, ore: 0, gold: 0 },
    dem: { res: 0, farm: 0, comm: 0, wood: 0, mine: 0 }, note: { res: '', farm: '', comm: '', wood: '', mine: '' }, flags: {},
    peasants: 6, popAcc: 0, soldiers: 0, day: 1, drafting: true, starving: false, trained: 0, d: null
  };
  for (let y = 0; y < N; y++) { S.grid.push(Array(N).fill(null)); S.deco.push(Array(N).fill(null)); S.zone.push(Array(N).fill(null)); S.fert.push(Array(N).fill(false)); }
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
    const dist = Math.max(Math.abs(x - 20), Math.abs(y - 20));
    S.fert[y][x] = Math.max(Math.abs(x - 20), Math.abs(y - 23)) <= 7 || vnoise(x * 0.11 + 10, y * 0.11 + 10, seed + 20) > 0.55;
    if (dist < 6) continue;
    const f = vnoise(x * 0.16, y * 0.16, seed) * 0.65 + vnoise(x * 0.4, y * 0.4, seed + 3) * 0.35;
    const r = vnoise(x * 0.22 + 40, y * 0.22 + 40, seed + 9);
    const o = vnoise(x * 0.3 + 90, y * 0.3 + 90, seed + 13);
    if (o > 0.72 && hash2(x, y, seed + 7) > 0.35) S.deco[y][x] = 'ore';
    else if (f > 0.56 && hash2(x, y, seed + 5) > 0.22) S.deco[y][x] = 'tree';
    else if (r > 0.7 && hash2(x, y, seed + 6) > 0.35) S.deco[y][x] = 'rock';
  }
  // The 3x3 Town Hall is anchored at (19,19), centered on the original (20,20).
  // All nine cells are reserved before roads and zones are placed.
  addBld(S, 'hall', 19, 19, 3);
  for (let y = 22; y <= 26; y++) addBld(S, 'road', 20, y);
  addBld(S, 'house', 19, 23).zoned = true; addBld(S, 'house', 21, 23).zoned = true; addBld(S, 'farm', 19, 25).zoned = true;
  S.zone[23][19] = 'res'; S.zone[23][21] = 'res'; S.zone[25][19] = 'farm';
  recalc(S); computeDemand(S);
  return S;
}
function saveWorld(S) { return JSON.stringify(Object.assign({}, S, { grid: undefined, hall: undefined, d: undefined, v: 1 })); }
function loadWorld(str) {
  const o = JSON.parse(str); if (!o || !o.blds || !o.deco) return null;
  const S = Object.assign({}, o); S.grid = []; S.hall = null; S.d = null;
  for (let y = 0; y < N; y++) S.grid.push(Array(N).fill(null));
  // Upgrade legacy one-tile barracks only when their 2x2 footprint is clear.
  // Crowded old saves keep the original 1x1 building instead of overwriting neighbours.
  for (const b of S.blds) if (b.t === 'barracks' && (b.sz || 1) === 1) {
    const cells = [[b.x, b.y], [b.x + 1, b.y], [b.x, b.y + 1], [b.x + 1, b.y + 1]];
    const clear = cells.every(([x, y]) => inb(x, y) &&
      !S.blds.some(other => other !== b && tilesOf(other).some(([i, j]) => i === x && j === y)) &&
      S.deco[y][x] !== 'rock' && S.deco[y][x] !== 'ore');
    if (clear) { b.sz = 2; for (const [x, y] of cells) S.deco[y][x] = null; }
  }
  for (const b of S.blds) { for (const [i, j] of tilesOf(b)) S.grid[j][i] = b; if (b.t === 'hall') S.hall = b; }
  if (!S.hall) return null;
  recalc(S); computeDemand(S); return S;
}
function addBld(S, t, x, y, sz) {
  const b = { t, x, y, sz: sz || (t === 'hall' ? 3 : t === 'barracks' ? 2 : 1), prog: 0, served: false, lvl: 1 };
  S.blds.push(b);
  for (const [i, j] of tilesOf(b)) { S.grid[j][i] = b; S.deco[j][i] = null; if (t === 'road') S.zone[j][i] = null; }
  if (t === 'hall') S.hall = b;
  return b;
}
function roadPathCheck(S, path) {
  let n = 0;
  for (const [x, y] of path) {
    if (!inb(x, y)) return { ok: false, n, reason: 'Outside the map' };
    const g = S.grid[y][x];
    if (g && g.t === 'road') continue;
    if (g) return { ok: false, n, reason: 'A building is in the way' };
    const dc = S.deco[y][x];
    if (dc === 'rock' || dc === 'ore') return { ok: false, n, reason: 'Rock or ore is in the way. Route around it or mine it first' };
    n++;
  }
  if (S.res.wood < n * COST.road.wood) return { ok: false, n, reason: 'Not enough wood: ' + n + ' new road tiles need ' + n + ' wood' };
  return { ok: true, n };
}
function placeRoadPath(S, path) {
  const c = roadPathCheck(S, path);
  if (!c.ok || !c.n) return c;
  S.res.wood -= c.n * COST.road.wood;
  for (const [x, y] of path) { const g = S.grid[y][x]; if (!g) addBld(S, 'road', x, y); }
  recalc(S);
  return c;
}
function canAfford(S, type) {
  const c = COST[type] || {};
  for (const k in c) if (S.res[k] < c[k]) return { ok: false, reason: 'Not enough ' + k + ' (need ' + c[k] + ')' };
  return { ok: true };
}
function canPlace(S, type, x, y) {
  const footprint = { x, y, sz: type === 'hall' ? 3 : type === 'barracks' ? 2 : 1, t: type };
  for (const [tx, ty] of tilesOf(footprint)) {
    if (!inb(tx, ty)) return { ok: false, reason: 'Outside the map' };
    if (S.grid[ty][tx]) return { ok: false, reason: 'That tile is already built on' };
    if (S.deco[ty][tx] === 'rock' || S.deco[ty][tx] === 'ore')
      return { ok: false, reason: 'Rock and ore block this tile. Zone it for mining to clear it' };
  }
  if (type === 'barracks' && !nextToRoad(S, footprint)) return { ok: false, reason: 'Barracks must touch a road' };
  return canAfford(S, type);
}
const FACE_DIRS = [[0, 1], [-1, 0], [0, -1], [1, 0]];
function roadOrHall(S, x, y, self) { if (!inb(x, y)) return false; const g = S.grid[y][x]; return !!g && g !== self && (g.t === 'road' || g === S.hall); }
function nextToRoad(S, b) { return tilesOf(b).some(([x, y]) => NB.some(([dx, dy]) => roadOrHall(S, x + dx, y + dy, b))); }
// Which way the door faces: 0 = +y, 1 = -x, 2 = -y, 3 = +x. Keeps its side while a road is still there.
function faceOf(S, b) {
  const T = tilesOf(b);
  const has = f => T.some(([x, y]) => roadOrHall(S, x + FACE_DIRS[f][0], y + FACE_DIRS[f][1], b));
  if (b.face !== undefined && has(b.face)) return b.face;
  for (let f = 0; f < 4; f++) if (has(f)) return f;
  return b.face || 0;
}
function place(S, type, x, y) {
  const c = canPlace(S, type, x, y);
  if (!c.ok) return c;
  const cost = COST[type] || {};
  for (const k in cost) S.res[k] -= cost[k];
  addBld(S, type, x, y);
  recalc(S);
  return { ok: true };
}
function remove(S, x, y) {
  if (!inb(x, y)) return { ok: false, reason: 'Outside the map' };
  const b = S.grid[y][x];
  if (b) {
    if (b.t === 'hall') return { ok: false, reason: 'The Town Hall cannot be removed' };
    const c = costOf(b.t, b.sz);
    for (const k in c) S.res[k] = Math.min(STORAGE, S.res[k] + Math.floor(c[k] / 2));
    for (const [i, j] of tilesOf(b)) { S.grid[j][i] = null; S.zone[j][i] = null; }
    S.blds.splice(S.blds.indexOf(b), 1);
    recalc(S); return { ok: true };
  }
  if (S.zone[y][x]) { S.zone[y][x] = null; return { ok: true }; }
  const k = S.deco[y][x];
  if (k) {
    if (k === 'tree') S.res.wood = Math.min(STORAGE, S.res.wood + 2);
    else if (k === 'rock') S.res.stone = Math.min(STORAGE, S.res.stone + 1);
    else S.res.ore = Math.min(STORAGE, S.res.ore + 1);
    S.deco[y][x] = null; return { ok: true };
  }
  return { ok: false, reason: 'Nothing to clear here' };
}
function zoneCheck(S, kind, x, y) {
  if (!inb(x, y)) return { ok: false, reason: 'Outside the map' };
  if (S.grid[y][x]) return S.zone[y][x] === kind ? { ok: true } : { ok: false, reason: 'That tile is already built on' };
  const dc = S.deco[y][x];
  if (kind === 'wood') return dc === 'tree' ? { ok: true } : { ok: false, reason: 'Lumber zones go on forest. Press V to see where the trees are' };
  if (kind === 'mine') return (dc === 'rock' || dc === 'ore') ? { ok: true } : { ok: false, reason: 'Mining zones go on rock or ore. Press V to see the deposits' };
  if (dc === 'rock' || dc === 'ore') return { ok: false, reason: 'Rock and ore block this zone. Mine it first' };
  if (kind === 'farm' && !S.fert[y][x]) return { ok: false, reason: 'Farms need fertile soil. Press V to see farmable land' };
  return { ok: true };
}
function setZone(S, kind, x, y) {
  const c = zoneCheck(S, kind, x, y);
  if (c.ok && !S.grid[y][x]) S.zone[y][x] = kind;
  return c;
}
function countDeco(S, x, y, r, kind) {
  let n = 0;
  for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++) if (inb(i, j) && S.deco[j][i] === kind) n++;
  return n;
}
function fellNearest(S, x, y, r, kind) {
  let best = null, bd = 1e9;
  for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++)
    if (inb(i, j) && S.deco[j][i] === kind) { const d = (i - x) * (i - x) + (j - y) * (j - y); if (d < bd) { bd = d; best = [i, j]; } }
  if (best) S.deco[best[1]][best[0]] = null;
}
function servedAt(S, b, conn) {
  for (const [tx, ty] of tilesOf(b)) for (const [dx, dy] of NB) {
    const x = tx + dx, y = ty + dy;
    if (!inb(x, y) || S.grid[y][x] === b) continue;
    if (conn[y * N + x] === 1 || S.grid[y][x] === S.hall) return true;
  }
  return false;
}
function recalc(S) {
  const conn = new Uint8Array(N * N), q = [], h = S.hall;
  const isRoad = (x, y) => inb(x, y) && S.grid[y][x] && S.grid[y][x].t === 'road';
  // A road can connect to any external edge of the multi-tile Town Hall.
  for (const [hx, hy] of tilesOf(h)) for (const [dx, dy] of NB) {
    const x = hx + dx, y = hy + dy;
    if (inb(x, y) && S.grid[y][x] !== h && isRoad(x, y) && !conn[y * N + x]) {
      conn[y * N + x] = 1; q.push([x, y]);
    }
  }
  while (q.length) {
    const [cx, cy] = q.pop();
    for (const [dx, dy] of NB) { const x = cx + dx, y = cy + dy; if (isRoad(x, y) && !conn[y * N + x]) { conn[y * N + x] = 1; q.push([x, y]); } }
  }
  const wells = S.blds.filter(b => b.t === 'well');
  let civ = HALL_CAP, bunks = 0, jobs = 0, markets = 0;
  for (const b of S.blds) {
    if (b.t === 'hall') { b.served = true; b.face = faceOf(S, b); continue; }
    if (b.t === 'road') { b.served = !!conn[b.y * N + b.x]; continue; }
    b.face = faceOf(S, b);
    b.served = servedAt(S, b, conn);
    if (b.t === 'house') {
      b.lvl = (b.served && wells.some(w => cheb(w, b) <= WELL_R)) ? 2 : 1;
      if (b.served) civ += capOf(b);
    } else if (b.t === 'barracks') { if (b.served) bunks += BUNKS; }
    else if (JOBS[b.t] && b.served) { jobs += jobsOf(b); if (b.t === 'market') markets++; }
  }
  const mk = S.blds.filter(b => b.t === 'market' && b.served);
  let houseCap = 0, covCap = 0;
  for (const b of S.blds) if (b.t === 'house' && b.served) {
    const cap = capOf(b); houseCap += cap;
    b.covered = mk.some(m => cheb(m, b) <= marketR(m)); if (b.covered) covCap += cap;
  }
  S.d = { civ, bunks, jobs, conn, markets, houseCap, covCap, cov: houseCap ? covCap / houseCap : 1, ratio: jobs ? Math.min(1, S.peasants / jobs) : 0 };
  return S.d;
}
function zoneServed(S, x, y) {
  const conn = S.d.conn, h = S.hall;
  return NB.some(([dx, dy]) => { const a = x + dx, b = y + dy; return inb(a, b) && (conn[b * N + a] === 1 || S.grid[b][a] === h); });
}
function computeDemand(S) {
  const d = S.d, r = S.res;
  const occ = d.civ ? S.peasants / d.civ : 1;
  const eat = Math.max(1, (S.peasants + S.soldiers * 1.5) * 0.2);
  let prod = 0;
  for (const b of S.blds) if (b.t === 'farm' && b.served) prod += foodOf(b) * d.ratio;
  const staffed = d.jobs <= S.peasants + 4;
  const dem = {};
  dem.res = (S.starving || r.food < 10) ? -0.3 : clamp1((occ - 0.7) * 4);
  if (S.rate.food < -0.3 && r.food < 120) dem.res = Math.min(dem.res, 0);
  dem.farm = clamp1((1.25 - prod / eat) * 3 + (r.food < 40 ? 0.3 : 0) - (r.food > 250 ? 0.5 : 0));
  if (S.starving) dem.farm = 1;
  dem.comm = d.houseCap < 8 ? -0.2 : clamp1((1 - d.cov) * 1.6 - 0.15);
  dem.wood = clamp1((120 - r.wood) / 120);
  dem.mine = clamp1(Math.max((60 - r.stone) / 60, (30 - r.ore) / 30));
  if (!staffed) { dem.farm = Math.min(dem.farm, 0); dem.wood = Math.min(dem.wood, 0); dem.mine = Math.min(dem.mine, 0); }
  S.dem = dem;
  return dem;
}
function lotOk(S, kind, x, y, sz) {
  for (let j = 0; j < sz; j++) for (let i = 0; i < sz; i++) {
    const a = x + i, c = y + j;
    if (!inb(a, c) || S.zone[c][a] !== kind || S.grid[c][a]) return false;
    const dc = S.deco[c][a]; if (dc === 'rock' || dc === 'ore') return false;
    if (kind === 'farm' && !S.fert[c][a]) return false;
  }
  return true;
}
function choose(S, kind) {
  const h = S.hall, r = S.res;
  const dist = (cx, cy) => Math.abs(cx - h.x) + Math.abs(cy - h.y);
  const ltype = { res: 'house', farm: 'farm', comm: 'market' }[kind];
  if (ltype) {
    const small = [], big = [];
    const unc = kind === 'comm' ? S.blds.filter(b => b.t === 'house' && b.served && !b.covered) : null;
    for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) for (const sz of [1, 2]) {
      if (!lotOk(S, kind, x, y, sz)) continue;
      if (!servedAt(S, { x, y, sz, t: ltype }, S.d.conn)) continue;
      const cx = x + (sz - 1) / 2, cy = y + (sz - 1) / 2;
      let score = dist(cx, cy) + hash2(x, y, S.day + sz) * 2.5;
      if (kind === 'comm') {
        const R = sz === 2 ? 7 : 5; let gain = 0;
        for (const b of unc) if (cheb(b, { x: cx, y: cy }) <= R) gain += capOf(b);
        if (gain <= 0) continue; score = -gain * 2 + dist(cx, cy) * 0.1;
      }
      (sz === 2 ? big : small).push({ x, y, type: ltype, sz, cost: costOf(ltype, sz).wood, score });
    }
    small.sort((a, b) => a.score - b.score); big.sort((a, b) => a.score - b.score);
    if (!small.length && !big.length) return { pick: null };
    const bigOk = big.length && r.wood >= big[0].cost, smallOk = small.length && r.wood >= small[0].cost;
    const preferBig = bigOk && (!smallOk || hash2(S.day, 77, S.seed) < 0.7);
    if (preferBig) return { pick: big[0] };
    if (smallOk) return { pick: small[0] };
    return { pick: null, wait: Math.min(small.length ? small[0].cost : 999, big.length ? big[0].cost : 999) };
  }
  const cands = [];
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
    if (S.zone[y][x] !== kind || S.grid[y][x] || !zoneServed(S, x, y)) continue;
    if (kind === 'wood') {
      const n = countDeco(S, x, y, WORK_R, 'tree');
      if (n < 3 || S.blds.some(b => b.t === 'wood' && cheb(b, { x, y }) <= 2)) continue;
      cands.push({ x, y, type: 'wood', sz: 1, cost: COST.wood.wood, score: -n * 2 + dist(x, y) * 0.1 });
    } else if (kind === 'mine') {
      const rk = countDeco(S, x, y, WORK_R, 'rock'), ok = countDeco(S, x, y, WORK_R, 'ore');
      const needOre = (30 - r.ore) / 30, needStone = (60 - r.stone) / 60;
      let type = null, cnt = 0;
      if (ok >= 2 && (rk < 2 || needOre >= needStone)) { type = 'mine'; cnt = ok; } else if (rk >= 2) { type = 'quarry'; cnt = rk; }
      if (!type || S.blds.some(b => b.t === type && cheb(b, { x, y }) <= 2)) continue;
      cands.push({ x, y, type, sz: 1, cost: COST[type].wood, score: -cnt * 2 + dist(x, y) * 0.1 });
    }
  }
  if (!cands.length) return { pick: null };
  cands.sort((a, b) => a.score - b.score);
  return r.wood >= cands[0].cost ? { pick: cands[0] } : { pick: null, wait: cands[0].cost };
}
function develop(S) {
  computeDemand(S);
  const T = 0.12;
  const idle = {
    res: 'No demand yet. Homes grow once houses are nearly full and food is stocked.',
    farm: 'No demand yet. Farms grow when food runs low.',
    comm: 'No demand yet. Markets grow when homes have no market within 5 tiles.',
    wood: 'No demand yet. Woodcutters grow when wood runs low.',
    mine: 'No demand yet. Mines and quarries grow when stone or ore run low.'
  };
  const nosite = {
    res: 'Needs empty zone tiles right beside a road that leads to the Town Hall.',
    farm: 'Needs empty fertile zone tiles right beside a road that leads to the Town Hall.',
    comm: 'Needs a road-linked commerce lot near homes that lack a market.',
    wood: 'Needs a road-linked lumber tile with 3+ trees in range, and space from other woodcutters.',
    mine: 'Needs a road-linked mining tile with 2+ rock or ore tiles in range.'
  };
  for (const kind of ZONE_KINDS) {
    S.note[kind] = '';
    if (S.dem[kind] <= T) { S.note[kind] = idle[kind]; continue; }
    const res = choose(S, kind);
    if (!res.pick) { S.note[kind] = res.wait ? 'Waiting for ' + res.wait + ' wood to build.' : nosite[kind]; continue; }
    const p = res.pick;
    S.res.wood -= p.cost;
    addBld(S, p.type, p.x, p.y, p.sz).zoned = true;
    recalc(S);
  }
}
function tick(S) {
  recalc(S);
  const d = S.d, before = { ...S.res };
  const ratio = d.ratio;
  const employed = Math.min(S.peasants, d.jobs);
  let food = 0, wood = 0, stone = 0, ore = 0;
  for (const b of S.blds) {
    if (!b.served) continue;
    if (b.t === 'farm') food += foodOf(b) * ratio;
    else if (b.t === 'wood') {
      if (countDeco(S, b.x, b.y, WORK_R, 'tree') > 0) {
        wood += 2.5 * ratio; b.prog += ratio;
        if (b.prog >= 10) { b.prog = 0; fellNearest(S, b.x, b.y, WORK_R, 'tree'); }
      }
    } else if (b.t === 'quarry') {
      if (countDeco(S, b.x, b.y, WORK_R, 'rock') > 0) {
        stone += 2 * ratio; b.prog += ratio;
        if (b.prog >= 18) { b.prog = 0; fellNearest(S, b.x, b.y, WORK_R, 'rock'); }
      }
    } else if (b.t === 'mine') {
      if (countDeco(S, b.x, b.y, WORK_R, 'ore') > 0) {
        ore += 1.5 * ratio; b.prog += ratio;
        if (b.prog >= 14) { b.prog = 0; fellNearest(S, b.x, b.y, WORK_R, 'ore'); }
      }
    }
  }
  const trade = d.markets ? 0.04 * S.peasants * d.cov * ratio : 0;
  const eat = (S.peasants + S.soldiers * 1.5) * 0.2;
  S.res.food += food - eat; S.res.wood += wood; S.res.stone += stone; S.res.ore += ore;
  S.res.gold += employed * 0.12 + trade - S.soldiers * 0.1;
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
  for (const b of S.blds) if (b.t === 'barracks' && b.served) {
    b.prog = Math.min(4, b.prog + 1);
    if (b.prog >= 4 && S.drafting && S.soldiers < d.bunks && S.peasants >= 3 && S.res.wood >= 2 && S.res.gold >= 1 && S.res.ore >= 1) {
      S.soldiers++; S.peasants--; S.trained++; S.res.wood -= 2; S.res.gold -= 1; S.res.ore -= 1; b.prog = 0;
    }
  }
  for (const k in S.res) S.res[k] = Math.max(0, Math.min(STORAGE, S.res[k]));
  for (const k in S.res) S.rate[k] = S.res[k] - before[k];
  S.day++;
  recalc(S);
  develop(S);
}
if (typeof module !== 'undefined') module.exports = { capOf, jobsOf, foodOf, nameOf, roadPathCheck, placeRoadPath, N, COST, NAMES, JOBS, createWorld, canPlace, place, remove, tick, recalc, countDeco, addBld, setZone, zoneCheck, computeDemand };
