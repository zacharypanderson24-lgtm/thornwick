'use strict';
// node bench/compare.js before.json after.json > RESULTS table (markdown)
const fs = require('fs'), [a, b] = process.argv.slice(2).map(f => JSON.parse(fs.readFileSync(f, 'utf8')));
const f = v => v === undefined ? 'n/a' : v >= 100 ? v.toFixed(0) : v >= 10 ? v.toFixed(1) : v >= 1 ? v.toFixed(2) : v >= 0.01 ? v.toFixed(3) : v.toFixed(4);
const imp = (x, y) => (x === undefined || y === undefined) ? '' : (y < x * 0.0005 ? '>2000x' : (x / Math.max(y, 1e-9) >= 1.05 ? (x / y).toFixed(x / y >= 10 ? 0 : 1) + 'x faster' : (y / x >= 1.05 ? (y / x).toFixed(1) + 'x slower' : '~same')));
const rows = [
  ['Average tick time (ms)', s => s.tick.avg], ['Median tick (ms)', s => s.tick.p50], ['95th percentile tick (ms)', s => s.tick.p95], ['Worst-case tick (ms)', s => s.tick.max],
  ['recalc(), nothing changed (ms)', s => s.recalcIdle], ['Road connectivity: place one road + recalc (ms)', s => s.roadAdd], ['Road connectivity: remove one road + recalc (ms)', s => s.roadRemove],
  ['Workforce re-allocation after a priority change (ms)', s => s.prioChange],
  ['Development candidate selection, all 5 zone kinds (ms)', s => s.chooseAll], ['develop() full day (ms)', s => s.develop],
  ['Worker day: stepCrews (ms)', s => s.stepCrews], ['Worker pathfinding: crewTarget (ms)', s => s.crewTarget], ['Ambient villagers: stepFolk (ms)', s => s.stepFolk],
  ['Save (ms)', s => s.save], ['Load (ms)', s => s.load], ['Heap held by the world + caches (MB)', s => s.heapMB],
];
let out = '';
out += `Environment: ${b.cpu}, ${b.cores} cores, ${b.os}, Node ${b.node}. Same snapshots (built once by the frozen original sim), same machine, run back to back.\n\n`;
for (const name of Object.keys(b.scenarios)) {
  const A = a.scenarios[name], B = b.scenarios[name]; if (!A) continue;
  out += `### ${name} (${B.kind}, ${B.peasants} residents, ${B.blds} buildings, ${B.roads} road tiles, ${B.crewBlds} work buildings)\n\n| Metric | Before | After | Improvement |\n|---|---|---|---|\n`;
  for (const [label, g] of rows) { const x = g(A), y = g(B); out += `| ${label} | ${f(x)} | ${f(y)} | ${imp(x, y)} |\n`; }
  out += '\n';
}
console.log(out);
