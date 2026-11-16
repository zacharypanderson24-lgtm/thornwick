'use strict';
// Usage: node --expose-gc bench/bench.js [--sim file] [--label name] [--out file.json] [scenario names...]
// Loads each snapshot from bench/.cache (make_snapshots.js builds them with the frozen reference sim) into the sim under test and times the pieces.
const fs = require('fs'), path = require('path'), os = require('os');
const L = require('../tests/lib.js'), SC = require('./scenarios.js');
const args = process.argv.slice(2); const opt = { sim: L.CURRENT, label: 'run', out: null }; const only = [];
for (let i = 0; i < args.length; i++) { if (args[i] === '--sim') opt.sim = path.resolve(args[++i]); else if (args[i] === '--label') opt.label = args[++i]; else if (args[i] === '--out') opt.out = args[++i]; else only.push(args[i]); }
const sim = L.loadSim(opt.sim), I = sim.__i, N = sim.N;
const now = () => Number(process.hrtime.bigint()) / 1e6;
const stats = a => { const s = a.slice().sort((x, y) => x - y), n = s.length; return { avg: s.reduce((p, q) => p + q, 0) / n, p50: s[n >> 1], p95: s[Math.min(n - 1, Math.floor(n * 0.95))], max: s[n - 1] }; };
const median = a => a.slice().sort((x, y) => x - y)[a.length >> 1];
const topUp = S => { S.res.food = Math.max(S.res.food, 300); S.res.wood = Math.max(S.res.wood, 200); S.res.gold = Math.max(S.res.gold, 100); S.res.stone = Math.max(S.res.stone, 60); S.res.ore = Math.max(S.res.ore, 30); };
const REP = 5;   // each micro-measurement is repeated and the median reported
function timeIt(fn, calls) { const r = []; for (let k = 0; k < REP; k++) { const t0 = now(); for (let i = 0; i < calls; i++) fn(i); r.push((now() - t0) / calls); } return median(r); }

function freeSpot(S) {   // a free buildable tile touching a Hall-connected road (for adding a road tile)
  for (const b of S.blds) if (b.t === 'road' && b.served) for (const [dx, dy] of sim.NB || [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    const x = b.x + dx, y = b.y + dy; if (x < 1 || y < 1 || x >= N - 1 || y >= N - 1) continue;
    if (!S.grid[y][x] && !sim.hasTree(S, x, y) && !sim.isSolid(S, x, y) && !S.zone[y][x]) return [x, y];
  }
  return null;
}
const results = { label: opt.label, sim: path.relative(L.ROOT, opt.sim), node: process.version, cpu: os.cpus()[0].model, cores: os.cpus().length, os: os.platform() + ' ' + os.release(), scenarios: {} };
for (const sc of SC) {
  if (only.length && !only.includes(sc.name)) continue;
  const f = path.join(__dirname, '.cache', sc.name + '.json'); if (!fs.existsSync(f)) { console.error('missing snapshot', sc.name, '(run make_snapshots.js)'); continue; }
  const txt = fs.readFileSync(f, 'utf8'), R = { kind: sc.kind };
  global.gc(); const h0 = process.memoryUsage().heapUsed;
  let S = sim.loadWorld(txt); topUp(S);
  const jobs = S.d.jobs;
  R.peasants = S.peasants; R.blds = S.blds.length; R.roads = S.blds.filter(b => b.t === 'road').length; R.houses = S.blds.filter(b => b.t === 'house').length; R.crewBlds = S.blds.filter(b => b.t === 'wood' || b.t === 'quarry' || b.t === 'mine').length; R.saveBytes = txt.length;
  // 1. whole-tick time: 120 consecutive days (growth continues, as in play)
  const tt = []; for (let i = 0; i < 120; i++) { topUp(S); const t0 = now(); sim.tick(S); tt.push(now() - t0); }
  R.tick = stats(tt);
  global.gc(); R.heapMB = (process.memoryUsage().heapUsed - h0) / 1048576;
  // 2. recalc with nothing changed
  R.recalcIdle = timeIt(() => sim.recalc(S), 200);
  // 3. road add / remove (each includes the recalc the game does after it)
  S.res.wood = 400; const spot = freeSpot(S);
  if (spot) { const ta = [], tr = []; for (let i = 0; i < REP * 4; i++) { S.res.wood = 400; let t0 = now(); sim.place(S, 'road', spot[0], spot[1]); ta.push(now() - t0); t0 = now(); sim.remove(S, spot[0], spot[1]); tr.push(now() - t0); } R.roadAdd = median(ta); R.roadRemove = median(tr); }
  // 4. workforce allocation: swap two trades' priority then recalc (what the Workers panel does)
  { const tms = []; for (let i = 0; i < REP * 4; i++) { const p = (S.d.prio).slice(); const a = i % (p.length - 1); [p[a], p[a + 1]] = [p[a + 1], p[a]]; S.prio = p; const t0 = now(); sim.recalc(S); tms.push(now() - t0); } R.prioChange = median(tms); S.prio = undefined; sim.recalc(S); }
  // 5. development candidate selection, per zone kind (what develop() calls each day)
  if (I.choose) { R.choose = {}; for (const k of ['res', 'farm', 'comm', 'wood', 'mine']) { S.res.wood = 400; R.choose[k] = timeIt(() => I.choose(S, k), k === 'res' || k === 'farm' || k === 'comm' ? 20 : 20); } R.chooseAll = Object.values(R.choose).reduce((p, q) => p + q, 0); }
  if (I.develop) { R.develop = timeIt(() => { S.res.wood = 400; I.develop(S); }, 5); }
  // 6. worker pathfinding + resource collection, and ambient villagers
  if (I.stepCrews) { const got = { logs: 0, stone: 0, ore: 0 }; topUp(S); R.stepCrews = timeIt(() => { topUp(S); got.logs = got.stone = got.ore = 0; I.stepCrews(S, got); }, 30); }
  if (I.stepFolk) { R.stepFolk = timeIt(() => I.stepFolk(S), 30); }
  if (I.crewTarget) { const cb = S.blds.filter(b => b.t === 'wood' || b.t === 'quarry' || b.t === 'mine'); if (cb.length) R.crewTarget = timeIt(i => { const b = cb[i % cb.length]; I.crewTarget(S, b, b.t === 'wood' ? 'tree' : b.t === 'quarry' ? 'rock' : 'ore', i, null); }, 100); }
  // 7. save / load
  let str; R.save = timeIt(() => { str = sim.saveWorld(S); }, 5); R.load = timeIt(() => { sim.loadWorld(str); }, 3); R.saveBytesAfter = str.length;
  results.scenarios[sc.name] = R;
  console.log(sc.name.padEnd(7), sc.kind.padEnd(9), 'pop', String(R.peasants).padStart(5), 'blds', String(R.blds).padStart(5), '| tick avg', R.tick.avg.toFixed(3), 'max', R.tick.max.toFixed(2), '| recalcIdle', R.recalcIdle.toFixed(3), 'roadAdd', (R.roadAdd || 0).toFixed(3), 'prio', R.prioChange.toFixed(3), '| chooseAll', (R.chooseAll || 0).toFixed(3), '| crews', (R.stepCrews || 0).toFixed(3), 'folk', (R.stepFolk || 0).toFixed(3), '| save', R.save.toFixed(1), 'load', R.load.toFixed(1), 'heapMB', R.heapMB.toFixed(1));
}
if (opt.out) fs.writeFileSync(opt.out, JSON.stringify(results, null, 1));
