'use strict';
// Writes the JavaScript prototype's terrain for a few seeds so the C# port can be checked against it tile for tile.
// Run from the repo root:  node unity/ParityReference/make_reference.js
// Output: unity/ParityReference/reference.json (read by the EditMode test SimParityTests).
const fs = require('fs'), path = require('path');
const { loadSim } = require('../../tests/lib.js');
const sim = loadSim(path.join(__dirname, '../../src/sim.js'));
const h = sim.__i.hash2;
const N = sim.N;

// Shape kept flat (arrays of plain objects) so Unity's JsonUtility can read it.
const out = { N, hash: [], seeds: [] };
for (const [x, y, s] of [[0, 0, 0], [1, 2, 3], [-5, 17, 7], [127, 64, 812], [64, 64, 401], [100000, -3, 2147483647]]) out.hash.push({ x, y, s, v: h(x, y, s) });

const DECO = { null: '.', undefined: '.', tree: 'T', rock: 'R', ore: 'O', stump: 'S', sapling: 's' };
for (const seed of [7, 1234]) {
  const S = sim.createWorld(seed);
  let deco = '', fert = '', amt = '', groves = '', variants = '';
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
    const d = S.deco[y][x]; deco += DECO[d];
    fert += S.fert[y][x] ? '1' : '0';
    amt += d === 'rock' ? String(sim.rockAmt(S, x, y)) : '0';
    if (d === 'tree') { variants += String(sim.treeVariant(S, x, y)); groves += String(sim.treeGrove(S, x, y).n); } else { variants += '-'; groves += '-'; }
  }
  const elev = []; for (const k in S.elev) elev.push({ k: +k, v: S.elev[k] });
  out.seeds.push({
    seed, hv: S.hv.map(v => v | 0), deco, fert, amt, variants, groves, elev,
    blds: S.blds.map(b => ({ t: b.t, x: b.x, y: b.y, sz: b.sz, hut: b.var || 0, served: !!b.served, face: b.face === undefined ? -1 : b.face, lvl: b.lvl })),
    civ: S.d.civ, jobs: S.d.jobs, idle: S.d.idle, employed: S.d.employed
  });
}
fs.writeFileSync(path.join(__dirname, 'reference.json'), JSON.stringify(out));
console.log('wrote reference.json for seeds', out.seeds.map(s => s.seed).join(', '));
