'use strict';
// ---------- Simulation (no DOM) ----------
const N = 64;                     // map is N x N tiles (old saves were 40 and are centred into this on load)
const OLD_N = 40, START = (N >> 1);   // START: the tile column/row the Town Hall is centred on
const COST = {
  road: { wood: 1 }, house: { wood: 10 }, farm: { wood: 8 }, wood: { wood: 6 }, quarry: { wood: 10 }, mine: { wood: 12 },
  market: { wood: 12 }, sawmill: { wood: 15 }, well: { wood: 5, stone: 5 }, barracks: { wood: 20, stone: 15, gold: 20 }
};
const NAMES = {
  road: 'Dirt Road', house: 'Timber House', farm: 'Farm', wood: 'Woodcutter', quarry: 'Quarry', mine: 'Iron Mine', market: 'Market', sawmill: 'Sawmill',
  well: 'Well', barracks: 'Barracks', hall: 'Town Hall',
  zoneRes: 'Housing Zone', zoneFarm: 'Farm Zone', zoneComm: 'Commerce Zone', zoneWood: 'Lumber Zone', zoneMine: 'Mining Zone'
};
const ZONE_NAMES = { res: 'Housing zone', farm: 'Farm zone', comm: 'Commerce zone', wood: 'Lumber zone', mine: 'Mining zone' };
const ZONE_KINDS = ['res', 'farm', 'comm', 'wood', 'mine'];
const JOBS = { farm: 4, wood: 3, quarry: 3, mine: 3, market: 2, sawmill: 3 };
const HOUSE_CAP = [0, 4, 6];
// Lumber chain: woodcutters fell designated trees into logs; a sawmill turns 1 log into 1 plank (wood). The Town Hall can pit-saw 1 log/tick at half yield.
const SAW_RATE = 4, HALL_SAW = 1, HALL_YIELD = 0.5, STUMP_DAYS = 8;
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

// ---- Terrain heights ----
// The ground is a grid of VERTEX heights in whole levels, -8..8, with 0 as base ground (S.hv, (N+1)^2 numbers). A tile's four corners come from it, so
// slopes run smoothly across tiles. Hills reach level 3, mountains up to 8; shallow basins dip to -1/-2. Roads follow slopes; buildings level their
// footprint. A tile whose average level is 4 or more is mountain (solid, quarry only); tiles with a spread of 3+ levels are too steep to build on.
const HMIN = -8, HMAX = 8, MOUNT_LEVEL = 4, LVL_PX = 7;
const vIdx = (a, b) => b * (N + 1) + a;
function vh(S, a, b) { if (!S.hv) return 0; a = a < 0 ? 0 : a > N ? N : a; b = b < 0 ? 0 : b > N ? N : b; return S.hv[b * (N + 1) + a] | 0; }
function tileCorners(S, x, y) { return [vh(S, x, y), vh(S, x + 1, y), vh(S, x, y + 1), vh(S, x + 1, y + 1)]; }
function tileLevel(S, x, y) { const c = tileCorners(S, x, y); return (c[0] + c[1] + c[2] + c[3]) / 4; }
function tileSpread(S, x, y) { const c = tileCorners(S, x, y); return Math.max(...c) - Math.min(...c); }
const tileFlat = (S, x, y) => tileSpread(S, x, y) === 0;
// Mountain layout: rare. Usually one range (a ridge with several summits and saddles) and maybe one lone massif.
function mountainLayout(seed) {
  let n = 0; const rnd = () => hash2(n++, 23, seed + 900);
  const ranges = [], lone = [];
  const far = (x, y, r) => Math.max(Math.abs(x - START), Math.abs(y - START)) > r;
  for (let r = 0, want = 1 + (rnd() < 0.3 ? 1 : 0); r < want; r++) for (let t = 0; t < 400; t++) {
    const len = 14 + rnd() * 9, ang = rnd() * Math.PI * 2, sx = 6 + rnd() * (N - 12), sy = 6 + rnd() * (N - 12);
    const bend = (rnd() - 0.5) * 1.1, pts = [];
    for (let i = 0; i <= 4; i++) { const a = ang + bend * (i / 4 - 0.5) * 2, f = (i / 4 - 0.5) * len; pts.push([sx + Math.cos(a + bend * i * 0.25) * f, sy + Math.sin(a + bend * i * 0.25) * f]); }
    if (!pts.every(p => p[0] > 4 && p[1] > 4 && p[0] < N - 4 && p[1] < N - 4 && far(p[0], p[1], 15))) continue;
    if (ranges.some(o => o.pts.some(q => pts.some(p => Math.hypot(p[0] - q[0], p[1] - q[1]) < 14)))) continue;
    ranges.push({ pts, ph: rnd() * 90 }); break;
  }
  for (let t = 0, want = rnd() < 0.7 ? 1 : 0; t < 400 && lone.length < want; t++) {
    const cx = 6 + rnd() * (N - 12), cy = 6 + rnd() * (N - 12), side = 2 + Math.floor(rnd() * 3), Hp = 5.2 + side * 0.5 + rnd() * 1.2;
    if (!far(cx, cy, 15) || ranges.some(o => o.pts.some(q => Math.hypot(q[0] - cx, q[1] - cy) < 14))) continue;
    lone.push({ cx, cy, side, Hp });
  }
  return { ranges, lone };
}
function mountainField(L, seed, a, b) {
  let v = 0;
  for (const r of L.ranges) {
    // nearest point on the spine, with how far along it we are
    let best = 1e9, bs = 0, acc = 0, total = 0;
    for (let i = 0; i + 1 < r.pts.length; i++) total += Math.hypot(r.pts[i + 1][0] - r.pts[i][0], r.pts[i + 1][1] - r.pts[i][1]);
    for (let i = 0; i + 1 < r.pts.length; i++) {
      const [x0, y0] = r.pts[i], [x1, y1] = r.pts[i + 1], dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
      const t = Math.max(0, Math.min(1, ((a - x0) * dx + (b - y0) * dy) / l2)), d = Math.hypot(a - (x0 + dx * t), b - (y0 + dy * t));
      if (d < best) { best = d; bs = acc + Math.sqrt(l2) * t; } acc += Math.sqrt(l2);
    }
    const along = bs, endFade = Math.min(1, 0.25 + Math.min(along, total - along) / 4);
    const crest = 6.2 + 3.0 * vnoise(along * 0.42 + r.ph, 3.3, seed + 31) - 0.4;           // summits and saddles along the ridge
    const d = best + (vnoise(a * 0.45 + r.ph, b * 0.45, seed + 32) - 0.5) * 1.6;
    v = Math.max(v, (crest * endFade) - Math.max(0, d - 0.4) * 1.9);
  }
  for (const m of L.lone) {
    const d = Math.hypot(a - m.cx, b - m.cy) + (vnoise(a * 0.5, b * 0.5, seed + 33) - 0.5) * 1.2;
    v = Math.max(v, m.Hp - Math.max(0, d - m.side * 0.2) * 1.5);
  }
  return v;
}
function genHeights(seed) {
  const M = N + 1, hv = new Array(M * M).fill(0), L = mountainLayout(seed);
  for (let b = 0; b < M; b++) for (let a = 0; a < M; a++) {
    const f = vnoise(a * 0.05 + 30, b * 0.05 + 30, seed + 400) * 0.7 + vnoise(a * 0.12, b * 0.12, seed + 401) * 0.3;
    let v = Math.max(0, Math.min(3.45, (f - 0.575) * 15));                                   // broad rolling hills, at most level 3
    const g = vnoise(a * 0.045 + 77, b * 0.045 + 77, seed + 500);
    if (v < 0.5 && g < 0.27) v = -Math.min(2.4, (0.27 - g) * 14);                           // a few shallow basins
    v = Math.max(v, mountainField(L, seed, a, b));
    const dist = Math.max(Math.abs(a - START), Math.abs(b - START));
    v *= Math.min(1, Math.max(0, (dist - 7) / 7));                                           // the start area stays flat
    hv[b * M + a] = Math.max(HMIN, Math.min(HMAX, Math.round(v)));
  }
  const S = { hv };
  relaxHeights(S, null, null);
  return hv;
}
const vnMountainTouch = (S, a, b) => { for (const [i, j] of [[a - 1, b - 1], [a, b - 1], [a - 1, b], [a, b]]) if (i >= 0 && j >= 0 && i < N && j < N && S.deco && S.deco[j][i] === 'rock' && rockAmt(S, i, j) >= 3) return true; return false; };
// Keep neighbouring vertices within 1 level (2 on high ground) so slopes stay walkable. Moves the lower vertex up unless it is pinned.
function relaxHeights(S, box, pinned) {
  const M = N + 1, hv = S.hv, a0 = box ? box[0] : 0, b0 = box ? box[1] : 0, a1 = box ? box[2] : N, b1 = box ? box[3] : N;
  const movable = (a, b) => !(pinned && pinned.has(b * M + a)) && !(S.deco && vnMountainTouch(S, a, b));
  for (let pass = 0; pass < 24; pass++) {
    let ch = false;
    for (let b = b0; b <= b1; b++) for (let a = a0; a <= a1; a++) {
      const h = hv[b * M + a];
      for (const [da, db] of [[1, 0], [0, 1], [-1, 0], [0, -1]]) {
        const na = a + da, nb = b + db; if (na < 0 || nb < 0 || na > N || nb > N) continue;
        const o = hv[nb * M + na], lim = Math.max(h, o) <= 3 ? 1 : 2;
        if (h - o > lim) { if (movable(na, nb)) { hv[nb * M + na] = h - lim; ch = true; } else if (movable(a, b)) { hv[b * M + a] = o + lim; ch = true; } }
      }
    }
    if (!ch) break;
  }
}
// Build UP the hill, not into it: the footprint is raised to its highest corner (a terrace, like a foundation), so the slope above is left alone and
// the land below is filled out to meet it. Other buildings keep their ground; the land around is eased back to a gentle slope.
function levelFootprint(S, tiles) {
  if (!S.hv) return;
  const M = N + 1, set = new Set(); let top = HMIN, a0 = N, b0 = N, a1 = 0, b1 = 0;
  for (const [x, y] of tiles) for (const [da, db] of [[0, 0], [1, 0], [0, 1], [1, 1]]) {
    const k = (y + db) * M + x + da; if (set.has(k)) continue; set.add(k); top = Math.max(top, S.hv[k]);
    a0 = Math.min(a0, x + da); b0 = Math.min(b0, y + db); a1 = Math.max(a1, x + da); b1 = Math.max(b1, y + db);
  }
  let moved = false;
  for (const k of set) if (S.hv[k] !== top) { S.hv[k] = top; moved = true; }
  if (!moved) return;
  const box = [Math.max(0, a0 - 5), Math.max(0, b0 - 5), Math.min(N, a1 + 5), Math.min(N, b1 + 5)], pinned = new Set(set);
  for (const bl of S.blds) if (bl.t !== 'road') for (const [i, j] of tilesOf(bl)) for (const [da, db] of [[0, 0], [1, 0], [0, 1], [1, 1]]) pinned.add((j + db) * M + i + da);
  relaxHeights(S, box, pinned);
}
// Movement over the land (for combat and walkers): you cannot climb straight up a mountain. An edge between two neighbouring tiles is blocked when either
// is mountain/solid or their heights differ by 2+ levels; gentle slopes cost more the steeper they are. Saddles between summits are the passes.
function walkCost(S, x, y, nx, ny) {
  if (!inb(x, y) || !inb(nx, ny) || isSolid(S, x, y) || isSolid(S, nx, ny)) return Infinity;
  const d = Math.abs(tileLevel(S, nx, ny) - tileLevel(S, x, y));
  return d >= 1.75 ? Infinity : 1 + d * 1.5;
}
// Saves from before the height map: flat ground, with mountains lifted according to their old height.
function deriveHeights(S) {
  const M = N + 1; S.hv = new Array(M * M).fill(0);
  const lv = (x, y) => (inb(x, y) && S.deco[y][x] === 'rock' && rockAmt(S, x, y) >= 3) ? Math.max(4, Math.min(8, Math.round(((S.elev && S.elev[y * N + x]) || 40) / 9))) : 0;
  for (let b = 0; b < M; b++) for (let a = 0; a < M; a++) {
    const t = [lv(a - 1, b - 1), lv(a, b - 1), lv(a - 1, b), lv(a, b)], m = Math.max(...t), c = t.filter(v => v > 0).length;
    S.hv[b * M + a] = c === 4 ? Math.round(t.reduce((p, q) => p + q, 0) / 4) : c ? Math.max(1, Math.round(m / 2)) : 0;
  }
  relaxHeights(S, null, null);
}
// One tile of terrain on top of the height map: fertility, then mountain / rock outcrop / ore / tree.
function genTile(S, seed, x, y) {
  const dist = Math.max(Math.abs(x - START), Math.abs(y - START));
  S.fert[y][x] = Math.max(Math.abs(x - START), Math.abs(y - (START + 3))) <= 7 || vnoise(x * 0.11 + 10, y * 0.11 + 10, seed + 20) > 0.55;
  if (dist < 6) return;
  const f = vnoise(x * 0.16, y * 0.16, seed) * 0.65 + vnoise(x * 0.4, y * 0.4, seed + 3) * 0.35;
  const o = vnoise(x * 0.3 + 90, y * 0.3 + 90, seed + 13);
  const lvl = tileLevel(S, x, y), k = y * N + x;
  if (lvl >= MOUNT_LEVEL) { S.deco[y][x] = 'rock'; S.rockAmt[k] = 3; S.elev[k] = Math.round(lvl * 8); return; }
  const flat = tileFlat(S, x, y) && lvl >= 0;
  const q = vnoise(x * 0.33 + 210, y * 0.33 + 210, seed + 61);
  if (flat && q > 0.8 && hash2(x, y, seed + 62) > 0.25 && dist > 7) { S.deco[y][x] = 'rock'; S.rockAmt[k] = q > 0.88 ? 2 : 1; return; }   // stone outcrops: the main stone supply besides mountains
  if (o > 0.72 && hash2(x, y, seed + 7) > 0.35) S.deco[y][x] = 'ore';
  else if (f > 0.56 && hash2(x, y, seed + 5) > 0.22) S.deco[y][x] = 'tree';
}
function genTerrain(S, seed) {
  S.hv = genHeights(seed);
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) genTile(S, seed, x, y);
}
function createWorld(seed) {
  seed = seed || 7;
  const S = {
    seed, grid: [], deco: [], zone: [], fert: [], blds: [], hall: null,
    res: { food: 60, wood: 120, logs: 20, stone: 30, ore: 10, gold: 80 }, rate: { food: 0, wood: 0, logs: 0, stone: 0, ore: 0, gold: 0 }, hv: null, rockAmt: {}, elev: {}, stumpT: {}, dig: {}, oreLeft: {}, v2: true,
    dem: { res: 0, farm: 0, comm: 0, wood: 0, mine: 0 }, note: { res: '', farm: '', comm: '', wood: '', mine: '' }, flags: {},
    peasants: 6, popAcc: 0, soldiers: 0, day: 1, drafting: true, starving: false, trained: 0, d: null
  };
  for (let y = 0; y < N; y++) { S.grid.push(Array(N).fill(null)); S.deco.push(Array(N).fill(null)); S.zone.push(Array(N).fill(null)); S.fert.push(Array(N).fill(false)); }
  genTerrain(S, seed);
  // The 3x3 Town Hall is anchored at (19,19), centered on the original (20,20).
  // All nine cells are reserved before roads and zones are placed.
  const C = START;
  addBld(S, 'hall', C - 1, C - 1, 3);
  for (let y = C + 2; y <= C + 6; y++) addBld(S, 'road', C, y);
  addBld(S, 'house', C - 1, C + 3).zoned = true; addBld(S, 'house', C + 1, C + 3).zoned = true; addBld(S, 'farm', C - 1, C + 5).zoned = true;
  S.zone[C + 3][C - 1] = 'res'; S.zone[C + 3][C + 1] = 'res'; S.zone[C + 5][C - 1] = 'farm';
  recalc(S); computeDemand(S);
  return S;
}
function saveWorld(S) { return JSON.stringify(Object.assign({}, S, { grid: undefined, hall: undefined, d: undefined, v: 1 })); }
// Old saves are a 40x40 map. Centre them in the bigger map (everything shifts by the same offset) and generate fresh land around them.
function growSave(o) {
  const oldN = o.deco.length; if (oldN >= N) return o;
  const off = (N - oldN) >> 1, seed = o.seed || 7;
  const S = Object.assign({}, o);
  S.deco = []; S.zone = []; S.fert = []; S.rockAmt = {}; S.elev = {}; S.stumpT = {}; S.dig = {}; S.oreLeft = {};
  for (let y = 0; y < N; y++) { S.deco.push(Array(N).fill(null)); S.zone.push(Array(N).fill(null)); S.fert.push(Array(N).fill(false)); }
  S.hv = genHeights(seed);
  const inOld = (x, y) => x >= off && y >= off && x < off + oldN && y < off + oldN;
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
    if (inOld(x, y)) { const ox = x - off, oy = y - off; S.deco[y][x] = o.deco[oy][ox]; S.zone[y][x] = o.zone[oy][ox]; S.fert[y][x] = o.fert[oy][ox]; }
    else genTile(S, seed, x, y);
  }
  for (const key of ['rockAmt', 'elev', 'stumpT', 'dig', 'oreLeft']) for (const k in (o[key] || {})) {
    const ox = k % oldN, oy = Math.floor(k / oldN); S[key][(oy + off) * N + ox + off] = o[key][k];
  }
  S.blds = o.blds.map(b => Object.assign({}, b, { x: b.x + off, y: b.y + off }));
  // heights inside the old square come from the old tiles (flat, mountains lifted); outside stays as generated
  const gen = S.hv; deriveHeights(S); const old = S.hv; S.hv = gen;
  const pinned = new Set();
  for (let b = off; b <= off + oldN; b++) for (let a = off; a <= off + oldN; a++) { S.hv[b * (N + 1) + a] = old[b * (N + 1) + a]; }
  for (const bl of S.blds) for (const [i, j] of tilesOf(bl)) for (const [da, db] of [[0, 0], [1, 0], [0, 1], [1, 1]]) pinned.add((j + db) * (N + 1) + i + da);
  relaxHeights(S, null, pinned);
  return S;
}
function loadWorld(str) {
  let o = JSON.parse(str); if (!o || !o.blds || !o.deco) return null;
  o = growSave(o);
  const S = Object.assign({}, o); S.grid = []; S.hall = null; S.d = null;
  for (let y = 0; y < N; y++) S.grid.push(Array(N).fill(null));
  // Upgrade legacy one-tile barracks only when their 2x2 footprint is clear.
  // Crowded old saves keep the original 1x1 building instead of overwriting neighbours.
  for (const b of S.blds) if (b.t === 'barracks' && (b.sz || 1) === 1) {
    const cells = [[b.x, b.y], [b.x + 1, b.y], [b.x, b.y + 1], [b.x + 1, b.y + 1]];
    const clear = cells.every(([x, y]) => inb(x, y) &&
      !S.blds.some(other => other !== b && tilesOf(other).some(([i, j]) => i === x && j === y)) &&
      !isSolid(S, x, y));
    if (clear) { b.sz = 2; for (const [x, y] of cells) { S.deco[y][x] = null; if (S.rockAmt) delete S.rockAmt[y * N + x]; } }
  }
  for (const b of S.blds) { for (const [i, j] of tilesOf(b)) S.grid[j][i] = b; if (b.t === 'hall') S.hall = b; }
  if (!S.hall) return null;
  if (!S.v2) {
    // Old saves: workers now only touch designated tiles, so designate what existing camps were working.
    S.res.logs = S.res.logs || 0; S.rate.logs = 0; S.rockAmt = S.rockAmt || {}; S.stumpT = S.stumpT || {};
    for (const b of S.blds) {
      if (b.t !== 'wood' && b.t !== 'quarry') continue;
      for (let j = b.y - WORK_R; j <= b.y + WORK_R; j++) for (let i = b.x - WORK_R; i <= b.x + WORK_R; i++) {
        if (!inb(i, j) || S.zone[j][i]) continue;
        if (b.t === 'wood' && S.deco[j][i] === 'tree') S.zone[j][i] = 'wood';
        if (b.t === 'quarry' && S.deco[j][i] === 'rock') S.zone[j][i] = 'mine';
      }
    }
    S.v2 = true;
  }
  if (!S.hv || S.hv.length !== (N + 1) * (N + 1)) deriveHeights(S);
  for (const b of S.blds) if (b.t === 'house' && !b.var) b.var = pickHutVar(S, b.x, b.y);
  recalc(S); computeDemand(S); return S;
}
// Hut look: 1..8. Sticky per building. Never the same as any of the 8 surrounding houses.
const HUT_VARIANTS = 8;
function pickHutVar(S, x, y) {
  const used = new Set();
  for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
    if (!dx && !dy) continue;
    const nx = x + dx, ny = y + dy;
    if (!inb(nx, ny)) continue;
    const o = S.grid[ny][nx];
    if (o && o.t === 'house' && o.var) used.add(o.var);
  }
  const start = Math.floor(hash2(x, y, 173) * HUT_VARIANTS);
  for (let i = 0; i < HUT_VARIANTS; i++) { const v = 1 + (start + i) % HUT_VARIANTS; if (!used.has(v)) return v; }
  return 1 + start;
}
function addBld(S, t, x, y, sz) {
  const b = { t, x, y, sz: sz || (t === 'hall' ? 3 : t === 'barracks' ? 2 : 1), prog: 0, served: false, lvl: 1 };
  if (t === 'house') b.var = pickHutVar(S, x, y);
  S.blds.push(b);
  for (const [i, j] of tilesOf(b)) { S.grid[j][i] = b; S.deco[j][i] = null; if (S.rockAmt) delete S.rockAmt[j * N + i]; if (S.dig) delete S.dig[j * N + i]; if (S.elev) delete S.elev[j * N + i]; if (S.oreLeft) delete S.oreLeft[j * N + i]; if (t === 'road') S.zone[j][i] = null; }
  if (t === 'hall') S.hall = b;
  if (t !== 'road') levelFootprint(S, tilesOf(b));
  return b;
}
function roadPathCheck(S, path) {
  let n = 0;
  for (const [x, y] of path) {
    if (!inb(x, y)) return { ok: false, n, reason: 'Outside the map' };
    const g = S.grid[y][x];
    if (g && g.t === 'road') continue;
    if (g) return { ok: false, n, reason: 'A building is in the way' };
    if (isSolid(S, x, y)) return { ok: false, n, reason: 'A mountain, a quarry pit or ore is in the way. Route around it, or over the hills' };
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
    if (isSolid(S, tx, ty))
      return { ok: false, reason: isMountain(S, tx, ty) ? 'You cannot build on a mountain. Hills and flat ground are fine' : pitDepth(S, tx, ty) > 0 ? 'A quarry pit is in the way. Pits cannot be filled in' : 'Ore blocks this tile. Zone it for mining to clear it' };
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
    if (k === 'tree') S.res.logs = Math.min(STORAGE, (S.res.logs || 0) + 2);
    else if (k === 'stump') { /* nothing to salvage */ }
    else if (k === 'rock') { if (rockAmt(S, x, y) >= 3) return { ok: false, reason: 'A mountain cannot be cleared by hand. Mark it for quarrying' }; if (pitDepth(S, x, y) > 0) return { ok: false, reason: 'A quarry pit cannot be filled in' }; S.res.stone = Math.min(STORAGE, S.res.stone + 1); if (S.rockAmt) delete S.rockAmt[tk(x, y)]; }
    else S.res.ore = Math.min(STORAGE, S.res.ore + 1);
    S.deco[y][x] = null; if (S.stumpT) delete S.stumpT[tk(x, y)]; return { ok: true };
  }
  return { ok: false, reason: 'Nothing to clear here' };
}
function zoneCheck(S, kind, x, y) {
  if (!inb(x, y)) return { ok: false, reason: 'Outside the map' };
  if (S.grid[y][x]) return S.zone[y][x] === kind ? { ok: true } : { ok: false, reason: 'That tile is already built on' };
  const dc = S.deco[y][x];
  if (kind === 'wood') return dc === 'tree' ? { ok: true } : { ok: false, reason: 'Lumber zones go on forest. Press V to see where the trees are' };
  if (kind === 'mine') {
    if (dc === 'rock') return rockLeft(S, x, y) > 0 ? { ok: true } : { ok: false, reason: 'This rock is worked out. Stone does not grow back' };
    return dc === 'ore' ? { ok: true } : { ok: false, reason: 'Mining zones go on rock or ore. Press V to see the deposits' };
  }
  if (isSolid(S, x, y)) return { ok: false, reason: isMountain(S, x, y) ? 'Mountains cannot be built on. Hills and flat ground can' : pitDepth(S, x, y) > 0 ? 'A quarry pit is in the way. Pits cannot be filled in' : 'Ore blocks this zone. Mine it first' };
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
const tk = (x, y) => y * N + x;
// Trees/rocks that belong to a designated zone (the only ones workers touch).
function countZoned(S, x, y, r, kind, zone) {
  let n = 0;
  for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++) if (inb(i, j) && S.deco[j][i] === kind && S.zone[j][i] === zone && (kind !== 'rock' || rockWorkable(S, i, j))) n++;
  return n;
}
// Rock height: 3 = mountain (permanent), 2 = hill, 1 = rubble. Quarrying wears hills and rubble down one stage at a time; mountains never change.
function rockAmt(S, x, y) { return (S.rockAmt && S.rockAmt[tk(x, y)]) || 1; }
// ---- Rock, stone and ore are finite and never regenerate ----
// Every rock tile has a fixed stone reserve; S.dig[tile] is how much has been taken out so far.
//   mountain (rockAmt 3): permanent. Reserve MOUNT_CAP. It never changes level; the more is mined, the bigger its hollow (rockHollow).
//   hill (2), rubble (1): each DIG_U units mined lowers the tile one level: hill -> rubble -> flat -> pit 1 -> pit 2 -> pit 3 (deepest, then worked out).
// Ore tiles hold ORE_CAP each (S.oreLeft). A mined-out ore tile is gone.
const DIG_U = 10, MOUNT_CAP = 120, ORE_CAP = 30;
function digDone(S, x, y) { return (S.dig && S.dig[tk(x, y)]) || 0; }
// A mountain tile holds more stone the taller it is (peaks hold the most); a plain 40 px tile holds MOUNT_CAP.
function elevOf(S, x, y) { return (S.elev && S.elev[tk(x, y)]) || 40; }
function rockCap(S, x, y) { const a = rockAmt(S, x, y); return a >= 3 ? Math.round(MOUNT_CAP * elevOf(S, x, y) / 40) : (a + 3) * DIG_U; }
function rockLeft(S, x, y) { return S.deco[y][x] === 'rock' ? Math.max(0, rockCap(S, x, y) - digDone(S, x, y)) : 0; }
// 3 = mountain, 2 = hill, 1 = rubble, 0 = flat, -1..-3 = pit depth
function rockLevel(S, x, y) { const a = rockAmt(S, x, y); return a >= 3 ? 3 : Math.max(-3, a - Math.floor(digDone(S, x, y) / DIG_U)); }
function rockHollow(S, x, y) { return Math.min(1, digDone(S, x, y) / rockCap(S, x, y)); }
function oreLeft(S, x, y) { return S.deco[y][x] === 'ore' ? ((S.oreLeft && S.oreLeft[tk(x, y)]) ?? ORE_CAP) : 0; }
function isMountain(S, x, y) { return S.deco[y][x] === 'rock' && rockAmt(S, x, y) >= 3; }
function pitDepth(S, x, y) { return (inb(x, y) && S.deco[y][x] === 'rock' && rockAmt(S, x, y) < 3) ? Math.max(0, -rockLevel(S, x, y)) : 0; }
function isSolid(S, x, y) { return S.deco[y][x] === 'ore' || isMountain(S, x, y) || pitDepth(S, x, y) > 0 || tileSpread(S, x, y) >= 3; }   // 3+ levels across one tile is too steep
// A mountain can only be quarried where it meets open ground or a hollowed-out (worked out) mountain tile, so quarries cut inward.
function rockOpen(S, x, y) {
  if (!isMountain(S, x, y)) return true;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const i = x + dx, j = y + dy; if (inb(i, j) && (!isMountain(S, i, j) || rockLeft(S, i, j) === 0)) return true; }
  return false;
}
const rockWorkable = (S, x, y) => S.deco[y][x] === 'rock' && rockLeft(S, x, y) > 0 && rockOpen(S, x, y);
// Take up to `amount` stone out of the nearest marked workable tiles within r. Returns what was actually taken.
function mineRock(S, x, y, r, amount) {
  let got = 0;
  for (let n = 0; n < 4 && amount > 1e-9; n++) {
    let best = null, bd = 1e9;
    for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++)
      if (inb(i, j) && S.zone[j][i] === 'mine' && rockWorkable(S, i, j)) { const d = (i - x) * (i - x) + (j - y) * (j - y); if (d < bd) { bd = d; best = [i, j]; } }
    if (!best) break;
    const e = Math.min(amount, rockLeft(S, best[0], best[1]));
    S.dig = S.dig || {}; S.dig[tk(best[0], best[1])] = digDone(S, best[0], best[1]) + e; got += e; amount -= e;
    if (rockLeft(S, best[0], best[1]) <= 1e-9) { S.dig[tk(best[0], best[1])] = rockCap(S, best[0], best[1]); S.zone[best[1]][best[0]] = null; }
  }
  return got;
}
// Ore: drain the nearest ore tile in range; an empty tile disappears.
function mineOre(S, x, y, r, amount) {
  let got = 0;
  for (let n = 0; n < 4 && amount > 1e-9; n++) {
    let best = null, bd = 1e9;
    for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++)
      if (inb(i, j) && S.deco[j][i] === 'ore') { const d = (i - x) * (i - x) + (j - y) * (j - y); if (d < bd) { bd = d; best = [i, j]; } }
    if (!best) break;
    const k = tk(best[0], best[1]), e = Math.min(amount, oreLeft(S, best[0], best[1]));
    S.oreLeft = S.oreLeft || {}; S.oreLeft[k] = oreLeft(S, best[0], best[1]) - e; got += e; amount -= e;
    if (S.oreLeft[k] <= 1e-9) { S.deco[best[1]][best[0]] = null; S.zone[best[1]][best[0]] = null; delete S.oreLeft[k]; }
  }
  return got;
}
function nearestZoned(S, x, y, r, kind, zone) {
  let best = null, bd = 1e9;
  for (let j = y - r; j <= y + r; j++) for (let i = x - r; i <= x + r; i++)
    if (inb(i, j) && S.deco[j][i] === kind && S.zone[j][i] === zone && (kind !== 'rock' || rockWorkable(S, i, j))) { const d = (i - x) * (i - x) + (j - y) * (j - y); if (d < bd) { bd = d; best = [i, j]; } }
  return best;
}
function fellTree(S, x, y, r) {
  const t = nearestZoned(S, x, y, r, 'tree', 'wood'); if (!t) return;
  S.deco[t[1]][t[0]] = 'stump'; S.zone[t[1]][t[0]] = null;
  S.stumpT = S.stumpT || {}; S.stumpT[tk(t[0], t[1])] = STUMP_DAYS;
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
  dem.wood = clamp1((120 - r.wood - (r.logs || 0) * 0.7) / 120);
  dem.mine = clamp1(Math.max((60 - r.stone) / 60, (30 - r.ore) / 30));
  if (!staffed) { dem.farm = Math.min(dem.farm, 0); dem.wood = Math.min(dem.wood, 0); dem.mine = Math.min(dem.mine, 0); }
  S.dem = dem;
  return dem;
}
function lotOk(S, kind, x, y, sz) {
  for (let j = 0; j < sz; j++) for (let i = 0; i < sz; i++) {
    const a = x + i, c = y + j;
    if (!inb(a, c) || S.zone[c][a] !== kind || S.grid[c][a]) return false;
    if (isSolid(S, a, c)) return false;
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
      const n = countZoned(S, x, y, WORK_R, 'tree', 'wood');
      if (n < 3 || S.blds.some(b => b.t === 'wood' && cheb(b, { x, y }) <= 2)) continue;
      cands.push({ x, y, type: 'wood', sz: 1, cost: COST.wood.wood, score: -n * 2 + dist(x, y) * 0.1 });
    } else if (kind === 'mine') {
      const rk = countZoned(S, x, y, WORK_R, 'rock', 'mine'), ok = countDeco(S, x, y, WORK_R, 'ore');
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
  let food = 0, wood = 0, logs = 0, stone = 0, ore = 0;
  for (const b of S.blds) {
    if (!b.served) continue;
    if (b.t === 'farm') food += foodOf(b) * ratio;
    else if (b.t === 'wood') {
      if (countZoned(S, b.x, b.y, WORK_R, 'tree', 'wood') > 0) {
        logs += 2.5 * ratio; b.prog += ratio;
        if (b.prog >= 10) { b.prog = 0; fellTree(S, b.x, b.y, WORK_R); }
      }
    } else if (b.t === 'quarry') {
      // Finite stone: the quarry pauses (and keeps its reserve in the ground) while the stockpile is full.
      if (S.res.stone + stone < STORAGE) stone += mineRock(S, b.x, b.y, WORK_R, 2 * ratio);
    } else if (b.t === 'mine') {
      if (S.res.ore + ore < STORAGE) ore += mineOre(S, b.x, b.y, WORK_R, 1.5 * ratio);
    }
  }
  S.res.logs = (S.res.logs || 0) + logs;
  // Sawmills first (full yield, limited by workers), then the Town Hall pit-saw for what is left.
  for (const b of S.blds) if (b.t === 'sawmill' && b.served && S.res.logs > 0) {
    const c = Math.min(S.res.logs, SAW_RATE * ratio); S.res.logs -= c; wood += c;
  }
  if (S.res.logs > 0) { const c = Math.min(S.res.logs, HALL_SAW); S.res.logs -= c; wood += c * HALL_YIELD; }
  // Stumps rot away into open, buildable grass.
  if (S.stumpT) for (const k in S.stumpT) if (--S.stumpT[k] <= 0) { delete S.stumpT[k]; const y = Math.floor(k / N), x = k % N; if (S.deco[y][x] === 'stump') S.deco[y][x] = null; }
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
if (typeof module !== 'undefined') module.exports = { saveWorld, walkCost, loadWorld, growSave, START, elevOf, vh, tileCorners, tileLevel, tileSpread, tileFlat, genHeights, levelFootprint, deriveHeights, relaxHeights, HMIN, HMAX, MOUNT_LEVEL, LVL_PX, digDone, rockCap, rockLeft, rockLevel, rockHollow, oreLeft, pitDepth, mineRock, mineOre, rockWorkable, DIG_U, MOUNT_CAP, ORE_CAP, isMountain, isSolid, rockOpen, rockAmt, countZoned, capOf, jobsOf, foodOf, nameOf, roadPathCheck, placeRoadPath, N, COST, NAMES, JOBS, createWorld, canPlace, place, remove, tick, recalc, countDeco, addBld, setZone, zoneCheck, computeDemand };
