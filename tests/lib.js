'use strict';
// Shared helpers for the node tests and benchmarks: load a sim file (the current src/sim.js or the frozen reference copy) with extra internals exposed,
// and build deterministic settlements.
const fs = require('fs'), path = require('path');
const ROOT = path.join(__dirname, '..');
const INTERNALS = ['lotServed', 'devChunk', 'crewReach', 'applyRoadOps', 'rebuildConn', 'cacheOf', 'auditCaches', 'choose', 'develop', 'stepFolk', 'stepCrews', 'crewTarget', 'servedAt', 'computeDemand', 'tilesOf', 'faceOf', 'hash2', 'zoneServed', 'lotOk', 'plantSapling', 'fellAt', 'roadSearch', 'folkOf', 'crewOf'];
// file: CURRENT (all the parts listed in src/sim_parts.txt, in order) or a single-file sim such as the frozen reference. opts.N overrides the map size constant,
// which is how the tests prove nothing depends on 128x128.
function loadSim(file, extra, opts) {
  let code;
  if (file === CURRENT) { const dir = path.join(ROOT, 'src'); code = fs.readFileSync(path.join(dir, 'sim_parts.txt'), 'utf8').split('\n').map(l => l.trim()).filter(Boolean).map(p => fs.readFileSync(path.join(dir, p), 'utf8')).join('\n'); }
  else code = fs.readFileSync(file, 'utf8');
  if (opts && opts.N) { if (!/const N = 128;/.test(code)) throw new Error('map size constant not found'); code = code.replace('const N = 128;', 'const N = ' + opts.N + ';'); }
  const names = INTERNALS.concat(extra || []);
  const tail = '\nmodule.exports.__i = {' + names.map(n => `${n}: (typeof ${n} !== 'undefined' ? ${n} : undefined)`).join(',') + '};';
  const m = { exports: {} };
  new Function('module', 'exports', code + tail)(m, m.exports);
  return m.exports;
}
const CURRENT = path.join(ROOT, 'src', 'sim.js'), REFERENCE = path.join(ROOT, 'tests', 'reference', 'sim_v0.js');

// A deterministic town layout: a comb of roads around the Town Hall, zones painted beside them. Works through the public API only.
function layout(sim, S, R, opts) {
  opts = opts || {};
  const { N, addBld, setZone, canPlace } = sim, C = sim.START;
  const free = (x, y) => x >= 0 && y >= 0 && x < N && y < N && !S.grid[y][x] && !sim.hasTree(S, x, y) && !sim.isSolid(S, x, y);
  const road = (x, y) => { if (x >= 0 && y >= 0 && x < N && y < N && !S.grid[y][x] && (S.deco[y][x] === 'tree' || S.deco[y][x] === 'stump' || S.deco[y][x] === 'sapling')) sim.remove(S, x, y); if (free(x, y)) addBld(S, 'road', x, y); };
  const gap = opts.gap || 4;
  for (let y = C - R; y <= C + R; y += gap) for (let x = C - R; x <= C + R; x++) road(x, y);
  for (let x = C - R; x <= C + R; x += gap) for (let y = C - R; y <= C + R; y++) road(x, y);
  sim.recalc(S);
  const h = sim.__i.hash2;
  for (let y = C - R; y <= C + R; y++) for (let x = C - R; x <= C + R; x++) {
    if (x < 0 || y < 0 || x >= N || y >= N || S.grid[y][x]) continue;
    const dc = S.deco[y][x], r = h(x, y, 4242);
    if (dc === 'tree') { if (r < 0.7) setZone(S, 'wood', x, y); continue; }
    if (dc === 'rock' || dc === 'ore') { setZone(S, 'mine', x, y); continue; }
    const fp = opts.dense ? 0.12 : 0.35, rp = opts.dense ? 0.9 : 0.5, cp = opts.dense ? 0.96 : 0.58;
    if (S.fert[y][x] && r < fp) { setZone(S, 'farm', x, y); continue; }
    if (r < rp) setZone(S, 'res', x, y); else if (r < cp) setZone(S, 'comm', x, y);
  }
}
// Run days with the stockpile topped up so growth is limited by housing, jobs and the rules, not by starvation. Returns days run.
function grow(sim, S, target, maxDays) {
  let d = 0;
  while (d < maxDays && S.peasants < target) {
    S.res.food = Math.max(S.res.food, 300); S.res.wood = Math.max(S.res.wood, 200); S.res.gold = Math.max(S.res.gold, 100);
    sim.tick(S); d++;
  }
  return d;
}
// Synthetic stress fill: put a building on every zoned lot at once (2x2 where it fits) instead of waiting for the development rules, then fill the homes.
function synthFill(sim, S) {
  const { N, addBld } = sim, I = sim.__i, kinds = { res: 'house', farm: 'farm', comm: 'market' };
  sim.recalc(S);
  for (const kind of ['res', 'farm', 'comm']) for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
    if (S.zone[y][x] !== kind || S.grid[y][x]) continue;
    let sz = 0;
    for (const t of [2, 1]) if (I.lotOk(S, kind, x, y, t) && I.servedAt(S, { x, y, sz: t, t: kinds[kind] }, S.d.conn)) { sz = t; break; }
    if (!sz) continue;
    if (kind === 'comm' && ((x * 7 + y * 3) % 9)) continue;
    addBld(S, kinds[kind], x, y, sz).zoned = true;
  }
  sim.recalc(S);
  S.peasants = Math.min(S.d.civ, 10000); sim.recalc(S);
}
module.exports = { loadSim, layout, grow, synthFill, CURRENT, REFERENCE, ROOT };
