'use strict';
// Pure simulation days (no player actions) from a big benchmark snapshot, original vs optimized, comparing the whole world every few days.
const fs = require('fs'), path = require('path'), L = require('./lib.js'), E = require('./equiv.js');
const [name, days, every] = [process.argv[2] || 'p1000', +process.argv[3] || 100, +process.argv[4] || 5];
const txt = fs.readFileSync(path.join(__dirname, '..', 'bench', '.cache', name + '.json'), 'utf8');
const A = L.loadSim(L.REFERENCE), B = L.loadSim(L.CURRENT), Sa = A.loadWorld(txt), Sb = B.loadWorld(txt);
let bad = 0;
for (let d = 1; d <= days; d++) {
  for (const S of [Sa, Sb]) { S.res.food = Math.max(S.res.food, 300); S.res.wood = Math.max(S.res.wood, 200); S.res.gold = Math.max(S.res.gold, 100); S.res.stone = Math.max(S.res.stone, 60); S.res.ore = Math.max(S.res.ore, 30); }
  A.tick(Sa); B.tick(Sb);
  if (d % every === 0 || d === days) { const a = E.snapshot(A, Sa), b = E.snapshot(B, Sb); if (a !== b) { let i = 0; while (a[i] === b[i]) i++; console.log(name, 'DIVERGED day', d, 'at', i, '\n', a.slice(i - 100, i + 100), '\n', b.slice(i - 100, i + 100)); bad = 1; break; } }
}
const au = B.auditCaches(Sb); if (au.length) { console.log('audit', au); bad = 1; }
console.log(name, bad ? 'FAIL' : 'ok', days, 'days, pop', Sb.peasants, 'blds', Sb.blds.length);
process.exit(bad);
