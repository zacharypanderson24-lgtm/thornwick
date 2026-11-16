'use strict';
// Equivalence of the optimized sim against the frozen original (tests/reference/sim_v0.js): both are driven through the same seeded sequence of player
// actions and days, and the whole world (saved form + derived data + worker and villager runtime state) must match after every step.
const L = require('./lib.js');
function rng(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const canon = v => JSON.stringify(v, (k, x) => (x && typeof x === 'object' && !Array.isArray(x)) ? Object.keys(x).sort().reduce((o, key) => (o[key] = x[key], o), {}) : x);
function snapshot(sim, S) {
  const I = sim.__i, o = JSON.parse(sim.saveWorld(S));
  const d = S.d, idx = new Map(S.blds.map((b, i) => [b, i]));
  const crews = []; for (const b of S.blds) if (sim.CREW_KIND[b.t]) crews.push([idx.get(b), sim.crewOf(b).map(a => [a.st, a.x, a.y, a.px, a.py, a.i, a.t, a.carry, a.tx, a.ty, a.stand, a.path && a.path.length])]);
  const folk = sim.folkOf(S).map(w => [w.id, w.st, w.x, w.y, w.px, w.py, w.i, w.t, w.v, idx.get(w.home), w.path && w.path.length]);
  const dd = { civ: d.civ, bunks: d.bunks, jobs: d.jobs, idle: d.idle, employed: d.employed, mkEff: d.mkEff, markets: d.markets, houseCap: d.houseCap, covCap: d.covCap, cov: d.cov, ratio: d.ratio, work: d.work, prio: d.prio, conn: Array.from(d.conn).reduce((h, v, i) => v ? (Math.imul(h, 31) + i) | 0 : h, 7) };
  return canon({ o, crews, folk, dd, dem: S.dem, note: S.note });
}
function firstDiff(a, b) { let i = 0; while (i < a.length && a[i] === b[i]) i++; return { at: i, a: a.slice(Math.max(0, i - 120), i + 120), b: b.slice(Math.max(0, i - 120), i + 120) }; }

// One random player action; returns a small description (and the API result) so both sims can be compared on what they were told.
function act(sim, S, R, N) {
  const C = sim.START, pick = (n) => Math.floor(R() * n), r = R();
  const near = () => [C + pick(2 * 30 + 1) - 30, C + pick(2 * 30 + 1) - 30];
  const roads = S.blds.filter(b => b.t === 'road');
  if (r < 0.28) {   // lay a straight road, starting beside an existing road or the Hall
    const s = roads.length ? roads[pick(roads.length)] : { x: C + 3, y: C + 3 }, dir = [[1, 0], [-1, 0], [0, 1], [0, -1]][pick(4)], len = 1 + pick(9), path = [];
    for (let i = 1; i <= len; i++) path.push([s.x + dir[0] * i, s.y + dir[1] * i]);
    S.res.wood = Math.max(S.res.wood, 60); const res = sim.placeRoadPath(S, path); return ['road', s.x, s.y, len, res.ok, res.n];
  }
  if (r < 0.40 && roads.length) { const b = roads[pick(roads.length)]; return ['bulldoze-road', b.x, b.y, sim.remove(S, b.x, b.y).ok]; }
  if (r < 0.46 && S.blds.length > 12) { const b = S.blds[pick(S.blds.length)]; return ['bulldoze', b.t, b.x, b.y, sim.remove(S, b.x, b.y).ok]; }
  if (r < 0.62) {   // paint a zone rectangle
    const kind = ['res', 'farm', 'comm', 'wood', 'mine'][pick(5)], [x, y] = near(), w = 1 + pick(5), h = 1 + pick(5); let ok = 0;
    for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) if (sim.setZone(S, kind, x + i, y + j).ok) ok++;
    return ['zone', kind, x, y, w, h, ok];
  }
  if (r < 0.70) { const t = ['well', 'market', 'sawmill', 'forester', 'barracks'][pick(5)], [x, y] = near(); S.res.wood = Math.max(S.res.wood, 80); S.res.stone = Math.max(S.res.stone, 40); S.res.gold = Math.max(S.res.gold, 40); const res = sim.place(S, t, x, y); return ['place', t, x, y, res.ok]; }
  if (r < 0.76) { const p = (S.d.prio).slice(), a = pick(p.length - 1); [p[a], p[a + 1]] = [p[a + 1], p[a]]; S.prio = p; sim.recalc(S); return ['prio', p.join()]; }
  if (r < 0.79) { S.drafting = !S.drafting; return ['draft']; }
  if (r < 0.85) { const [x, y] = near(); const res = sim.remove(S, x, y); return ['clear', x, y, res.ok]; }
  if (r < 0.90) { sim.recalc(S); return ['recalc']; }
  return ['none'];
}
// Runs both sims in lockstep. `every`: compare the full snapshot after every k-th step (always on the last). Returns { steps, diff }.
function lockstep(opts) {
  const { N, seed, days, every = 1, layoutR = 0, grownTo = 0, feed = 0.6 } = opts;
  const A = L.loadSim(L.REFERENCE, [], { N }), B = L.loadSim(L.CURRENT, [], { N });
  const Sa = opts.snap ? A.loadWorld(opts.snap) : A.createWorld(seed), Sb = opts.snap ? B.loadWorld(opts.snap) : B.createWorld(seed), Ra = rng(seed * 31 + 1), Rb = rng(seed * 31 + 1), Rf = rng(seed * 7 + 3);
  if (layoutR && !opts.snap) { L.layout(A, Sa, layoutR); L.layout(B, Sb, layoutR); }
  let step = 0;
  const check = (what) => { const a = snapshot(A, Sa), b = snapshot(B, Sb); if (a !== b) return { step, what, ...firstDiff(a, b) }; return null; };
  let d = check('create'); if (d) return { steps: step, diff: d };
  for (let day = 0; day < days; day++) {
    const n = 1 + (Rf() < 0.5 ? 1 : 0) + (Rf() < 0.2 ? 2 : 0);
    for (let k = 0; k < n; k++) {
      const a = act(A, Sa, Ra, N), b = act(B, Sb, Rb, N); step++;
      if (JSON.stringify(a) !== JSON.stringify(b)) return { steps: step, diff: { step, what: 'action result', a, b } };
      if (every === 1 || step % every === 0) { d = check('after ' + a[0]); if (d) return { steps: step, diff: d }; }
    }
    if (Rf() < feed) { for (const [S] of [[Sa], [Sb]]) { S.res.food = Math.max(S.res.food, 200); S.res.wood = Math.max(S.res.wood, 100); } }
    A.tick(Sa); B.tick(Sb); step++;
    if (every === 1 || step % every === 0) { d = check('after tick (day ' + Sa.day + ')'); if (d) return { steps: step, diff: d }; }
    if (opts.audit) { const bad = B.auditCaches(Sb); if (bad.length) return { steps: step, diff: { step, what: 'audit', bad } }; }
  }
  d = check('end'); return { steps: step, diff: d, pop: Sa.peasants, blds: Sa.blds.length };
}
module.exports = { lockstep, rng, snapshot, canon, act };
if (require.main === module) {
  const a = process.argv.slice(2), [N, seed, days, every, layoutR] = a.slice(0, 5).map(Number);
  const snap = a[5] ? require('fs').readFileSync(require('path').join(__dirname, '..', 'bench', '.cache', a[5] + '.json'), 'utf8') : null;
  const r = lockstep({ N: N || 128, seed: seed || 7, days: days || 200, every: every || 1, layoutR: layoutR || 0, audit: true, snap });
  console.log(JSON.stringify(r).slice(0, 1500));
  process.exit(r.diff ? 1 : 0);
}
