// ---------- Zoning and automatic development ----------
// The rules are exactly the original ones (same eligibility, same scoring including the per-day hash noise, same tie-breaks). What changed is where the
// candidates come from: instead of scanning every tile of the map each time, each 16x16 chunk keeps its own list of eligible lots per zone kind, rebuilt only
// when something within a few tiles of that chunk changed (see sim_cache.js stamps). Searching then only visits chunks that can beat the best score so far.
function zoneServed(S, x, y) {
  const conn = S.d.conn, h = S.hall;
  return NB.some(([dx, dy]) => { const a = x + dx, b = y + dy; return inb(a, b) && (conn[b * N + a] === 1 || S.grid[b][a] === h); });
}
function computeDemand(S) {
  const d = S.d, r = S.res, C = cacheOf(S);
  const occ = d.civ ? S.peasants / d.civ : 1;
  const eat = Math.max(1, (S.peasants + S.soldiers * 1.5) * 0.2);
  let prod = 0;
  for (const b of listOf(C, 'farm')) if (b.served) prod += foodOf(b) * b.eff;
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
    if (!claimed(S, a, c) || isSolid(S, a, c) || hasTree(S, a, c)) return false;
    if (kind === 'farm' && !S.fert[c][a]) return false;
  }
  return true;
}
// servedAt for a lot that has no building object yet (same rule, no allocation).
function lotServed(S, conn, x, y, sz) {
  const h = S.hall;
  for (let j = 0; j < sz; j++) for (let i = 0; i < sz; i++) for (let k = 0; k < 4; k++) {
    const nx = x + i + NB[k][0], ny = y + j + NB[k][1];
    if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
    if (nx >= x && ny >= y && nx < x + sz && ny < y + sz) continue;   // the lot's own tiles are not neighbours
    if (conn[ny * N + nx] === 1 || S.grid[ny][nx] === h) return true;
  }
  return false;
}
// The chunk's eligible lots for one zone kind, rebuilt if anything near the chunk changed since it was built.
// res/farm/comm: small[] and big[] hold lot anchors (y*N+x) for 1x1 and 2x2 lots. wood/mine: sites[] holds x,y,count,count triples.
function devChunk(S, C, kind, c) {
  const store = C.dev[kind] || (C.dev[kind] = []);
  let e = store[c];
  if (e && e.stamp === C.cSeq[c]) return e;
  const x0 = (c % CH) << CHUNK_SHIFT, y0 = ((c / CH) | 0) << CHUNK_SHIFT, x1 = Math.min(N, x0 + CHUNK), y1 = Math.min(N, y0 + CHUNK), conn = S.d.conn;
  e = { stamp: C.cSeq[c], small: [], big: [], sites: [] };
  const lot = kind === 'res' || kind === 'farm' || kind === 'comm';
  for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
    if (S.zone[y][x] !== kind || S.grid[y][x]) continue;
    if (lot) {
      if (lotOk(S, kind, x, y, 1) && lotServed(S, conn, x, y, 1)) e.small.push(y * N + x);
      if (x + 1 < N && y + 1 < N && lotOk(S, kind, x, y, 2) && lotServed(S, conn, x, y, 2)) e.big.push(y * N + x);
    } else if (zoneServed(S, x, y)) {
      if (kind === 'wood') { const n = countZoned(S, x, y, WORK_R, 'tree', 'wood'); if (n >= 3) e.sites.push(x, y, n, 0); }
      else { const rk = countZoned(S, x, y, WORK_R, 'rock', 'mine'), ok = countDeco(S, x, y, WORK_R, 'ore'); if (rk >= 2 || ok >= 2) e.sites.push(x, y, rk, ok); }
    }
  }
  store[c] = e; return e;
}
// Smallest possible distance-to-Town-Hall score inside a chunk (the score is never below the distance), used to skip chunks that cannot win.
function chunkLB(h, c) {
  const x0 = (c % CH) << CHUNK_SHIFT, y0 = ((c / CH) | 0) << CHUNK_SHIFT, x1 = x0 + CHUNK - 1, y1 = y0 + CHUNK - 1;
  const dx = h.x < x0 ? x0 - h.x : h.x > x1 ? h.x - x1 : 0, dy = h.y < y0 ? y0 - h.y : h.y > y1 ? h.y - y1 : 0;
  return dx + dy - 1;   // -1: a 2x2 lot is scored at its centre, half a tile each way
}
function choose(S, kind) {
  const h = S.hall, r = S.res, C = cacheOf(S);
  const dist = (cx, cy) => Math.abs(cx - h.x) + Math.abs(cy - h.y);
  const ltype = { res: 'house', farm: 'farm', comm: 'market' }[kind];
  if (ltype) {
    let sBest = null, bBest = null;   // best (lowest score; ties go to the lower y*N+x, like the original stable sort) small and big lot
    const consider = (a, sz, score) => {
      const cur = sz === 2 ? bBest : sBest;
      if (!cur || score < cur.score || (score === cur.score && a < cur.a)) { const o = { a, score }; if (sz === 2) bBest = o; else sBest = o; }
    };
    if (kind !== 'comm') {
      const order = [];
      for (let c = 0; c < CH * CH; c++) order.push([chunkLB(h, c), c]);
      order.sort((p, q) => p[0] - q[0]);   // nearest chunks first; stop once no remaining chunk can beat the best small and the best big lot
      for (const [lb, c] of order) {
        const wantS = !sBest || lb <= sBest.score, wantB = !bBest || lb <= bBest.score;
        if (!wantS && !wantB) break;
        const e = devChunk(S, C, kind, c);
        if (wantS) for (const a of e.small) { const x = a % N, y = (a - x) / N; consider(a, 1, dist(x, y) + hash2(x, y, S.day + 1) * 2.5); }
        if (wantB) for (const a of e.big) { const x = a % N, y = (a - x) / N; consider(a, 2, dist(x + 0.5, y + 0.5) + hash2(x, y, S.day + 2) * 2.5); }
      }
    } else {
      const unc = listOf(C, 'house').filter(b => b.served && !b.covered);
      if (unc.length) {
        const bk = []; for (let i = 0; i < CH * CH; i++) bk.push(null);
        for (const b of unc) { const i = (b.y >> CHUNK_SHIFT) * CH + (b.x >> CHUNK_SHIFT); (bk[i] || (bk[i] = [])).push(b); }
        const gainAt = (cx, cy, R) => {
          let gain = 0;
          const x0 = Math.max(0, Math.floor(cx - R - 1)) >> CHUNK_SHIFT, x1 = Math.min(N - 1, Math.ceil(cx + R + 1)) >> CHUNK_SHIFT, y0 = Math.max(0, Math.floor(cy - R - 1)) >> CHUNK_SHIFT, y1 = Math.min(N - 1, Math.ceil(cy + R + 1)) >> CHUNK_SHIFT;
          for (let j = y0; j <= y1; j++) for (let i = x0; i <= x1; i++) { const l = bk[j * CH + i]; if (l) for (const b of l) if (cheb(b, { x: cx, y: cy }) <= R) gain += capOf(b); }
          return gain;
        };
        for (let c = 0; c < CH * CH; c++) {
          const e = devChunk(S, C, kind, c);
          for (const sz of [1, 2]) for (const a of (sz === 2 ? e.big : e.small)) {
            const x = a % N, y = (a - x) / N, cx = x + (sz - 1) / 2, cy = y + (sz - 1) / 2, gain = gainAt(cx, cy, sz === 2 ? 7 : 5);
            if (gain <= 0) continue;
            consider(a, sz, -gain * 2 + dist(cx, cy) * 0.1);
          }
        }
      }
    }
    const mk = (b, sz) => b ? { x: b.a % N, y: (b.a - b.a % N) / N, type: ltype, sz, cost: costOf(ltype, sz).wood, score: b.score } : null;
    const small = mk(sBest, 1), big = mk(bBest, 2);
    if (!small && !big) return { pick: null };
    const bigOk = big && r.wood >= big.cost, smallOk = small && r.wood >= small.cost;
    const preferBig = bigOk && (!smallOk || hash2(S.day, 77, S.seed) < 0.7);
    if (preferBig) return { pick: big };
    if (smallOk) return { pick: small };
    return { pick: null, wait: Math.min(small ? small.cost : 999, big ? big.cost : 999) };
  }
  // wood and mine: one candidate per site tile, score has no daily noise. Ties go to the earlier tile in (y, x) order, like the original stable sort.
  let best = null; const needOre = (30 - r.ore) / 30, needStone = (60 - r.stone) / 60;
  const woods = listOf(C, 'wood'), quarries = listOf(C, 'quarry'), mines = listOf(C, 'mine');
  for (let c = 0; c < CH * CH; c++) {
    const s = devChunk(S, C, kind, c).sites;
    for (let i = 0; i < s.length; i += 4) {
      const x = s[i], y = s[i + 1]; let type, cnt, score;
      if (kind === 'wood') {
        if (woods.some(b => cheb(b, { x, y }) <= 2)) continue;
        type = 'wood'; cnt = s[i + 2];
      } else {
        const rk = s[i + 2], ok = s[i + 3]; type = null; cnt = 0;
        if (ok >= 2 && (rk < 2 || needOre >= needStone)) { type = 'mine'; cnt = ok; } else if (rk >= 2) { type = 'quarry'; cnt = rk; }
        if (!type || (type === 'mine' ? mines : quarries).some(b => cheb(b, { x, y }) <= 2)) continue;
      }
      score = -cnt * 2 + dist(x, y) * 0.1;
      const a = y * N + x;
      if (!best || score < best.score || (score === best.score && a < best.a)) best = { a, x, y, type, cnt, score };
    }
  }
  if (!best) return { pick: null };
  const cand = { x: best.x, y: best.y, type: best.type, sz: 1, cost: COST[best.type].wood, score: best.score };
  return r.wood >= cand.cost ? { pick: cand } : { pick: null, wait: cand.cost };
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
