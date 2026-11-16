'use strict';
// Builds the benchmark snapshots with the frozen REFERENCE sim (so before/after runs start from byte-identical states) into bench/.cache/.
const fs = require('fs'), path = require('path');
const L = require('../tests/lib.js'), SC = require('./scenarios.js');
const dir = path.join(__dirname, '.cache'); fs.mkdirSync(dir, { recursive: true });
const only = process.argv.slice(2);
const sim = L.loadSim(L.REFERENCE);
for (const sc of SC) {
  if (only.length && !only.includes(sc.name)) continue;
  const f = path.join(dir, sc.name + '.json'); if (fs.existsSync(f) && !only.length) { console.log(sc.name, 'cached'); continue; }
  const S = sim.createWorld(7); L.layout(sim, S, sc.R, { dense: sc.dense });
  const t0 = Date.now();
  if (sc.kind === 'synthetic') { L.synthFill(sim, S); }
  const days = L.grow(sim, S, sc.target, 8000) + (sc.kind === 'synthetic' ? (() => { for (let i = 0; i < 200; i++) { S.res.food = 400; S.res.wood = 300; sim.tick(S); } return 200; })() : 0);
  fs.writeFileSync(f, sim.saveWorld(S));
  console.log(sc.name, sc.kind, 'days', days, 'peasants', S.peasants, 'blds', S.blds.length, 'KB', (fs.statSync(f).size / 1024) | 0, 'sec', (Date.now() - t0) / 1000);
}
