'use strict';
// Writes the JavaScript prototype's terrain for a few seeds so the C# port can be checked against it tile for tile.
// Run from the repo root:  node unity/ParityReference/make_reference.js
// Output: unity/ParityReference/reference.json (read by the EditMode test SimParityTests).
const fs = require('fs'), path = require('path');
const { loadSim } = require('../../tests/lib.js');
const sim = loadSim(path.join(__dirname, '../../src/sim.js'));
const h = sim.__i.hash2;
const N = sim.N;
// Underground mining is Unity-only (unity/Assets/Scripts/Sim/World.Mining.cs): the browser still mines surface ore tiles, so the
// scenarios below remove every ore showing (and its zone) before laying out the town, so both sides start from the same open ground. Wood, stone, development, crews, townsfolk and the
// economy stay compared day by day; SimParityTests.StripOre does the same on the C# side.
function stripOre(S) {
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) if (S.deco[y][x] === "ore") { S.deco[y][x] = null; S.zone[y][x] = null; }
  sim.invalidateCaches(S); sim.recalc(S, true);
}
// Farming is Unity's own too (farmhouses with zoned crop plots), so the scenarios also remove the starting farm and every Farmland
// zone after laying out the town; food is topped up or scripted, so housing, markets, wood, stone, crews and development stay compared.
function stripFarms(S) {
  for (const b of S.blds.filter(b => b.t === "farm")) sim.remove(S, b.x, b.y);
  for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) if (S.zone[y][x] === "farm") S.zone[y][x] = null;
  sim.invalidateCaches(S); sim.recalc(S, true);
}

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
// Zone development: the deterministic test town from tests/lib.js (roads in a comb, zones painted beside them), then
// develop() runs on scripted stockpiles and population so every zone kind sees demand. Crews and townsfolk are left out
// (they are approximated in the port), so only the development rules are compared.
{
  const { layout } = require('../../tests/lib.js');
  const S = sim.createWorld(7);
  stripOre(S); layout(sim, S, 10); stripFarms(S);
  const steps = [];
  for (let i = 0; i < 150; i++) {
    sim.recalc(S);
    S.res.food = 5 + (i * 37) % 260; S.res.wood = 20 + (i * 53) % 170; S.res.logs = (i * 11) % 30;
    S.res.stone = (i * 13) % 80; S.res.ore = (i * 7) % 40; S.res.gold = 100;
    S.peasants = Math.max(1, Math.round(S.d.civ * (0.55 + 0.05 * ((i * 17) % 10))));
    S.rate.food = ((i % 5) - 2) * 0.2; S.starving = i % 23 === 0;
    sim.recalc(S);
    const before = S.blds.length;
    sim.__i.develop(S);
    steps.push({
      day: S.day, wood: S.res.wood,
      dem: ['res', 'farm', 'comm', 'wood', 'mine'].map(k => S.dem[k]),
      added: S.blds.slice(before).map(b => ({ t: b.t, x: b.x, y: b.y, sz: b.sz, hut: b.var || 0, served: !!b.served, face: -1, lvl: b.lvl }))
    });
    S.day++;
  }
  out.develop = { seed: 7, R: 10, steps };
  console.log('develop scenario grew', S.blds.length, 'buildings');
}
// Whole days through tick(): the same test town, with the stockpile topped up as tests/lib.js grow() does, so walking work crews,
// townsfolk, delivered loads, felling, digging and development all run exactly as in the browser. Every day records the resources,
// the population and a digest of every crew worker and villager (state, tile, load).
{
  const { layout } = require('../../tests/lib.js');
  const S = sim.createWorld(7);
  stripOre(S); layout(sim, S, 10); stripFarms(S);
  // Layout towns never grow woodcutters on their own, so put two lodges on the first sites (scan order) that touch a road and have
  // 3+ marked trees in reach, at least 5 tiles apart. SimParityTests.PlaceLodges does the same.
  const C = sim.START, lodges = [];
  for (let y = C - 13; y <= C + 13 && lodges.length < 2; y++) for (let x = C - 13; x <= C + 13 && lodges.length < 2; x++) {
    if (!sim.canPlace(S, 'wood', x, y).ok || sim.countZoned(S, x, y, 3, 'tree', 'wood') < 3) continue;
    if (![[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => { const g = S.grid[y + dy] && S.grid[y + dy][x + dx]; return g && g.t === 'road'; })) continue;
    if (lodges.some(([lx, ly]) => Math.max(Math.abs(lx - x), Math.abs(ly - y)) < 5)) continue;
    sim.addBld(S, 'wood', x, y); lodges.push([x, y]);
  }
  sim.recalc(S);
  const ST ={ home: 'h', out: 'o', work: 'w', back: 'b', walk: 'k', stay: 's' };
  const crewDigest = () => S.blds.filter(b => sim.CREW_KIND[b.t]).map(b => `${b.t}@${b.x},${b.y}:` +
    sim.crewOf(b).map(a => `${ST[a.st]}${a.x},${a.y},${Math.round(a.carry * 1000)}`).join(' ')).join('|');
  const folkDigest = () => sim.folkOf(S).map(w => `${w.id}${ST[w.st]}${w.x},${w.y},${w.v}`).join(' ');
  const days = [];
  for (let i = 0; i < 300; i++) {
    S.res.food = Math.max(S.res.food, 300); S.res.wood = Math.max(S.res.wood, 200); S.res.gold = Math.max(S.res.gold, 100);
    sim.tick(S);
    let trees = 0, stumps = 0, ore = 0;
    for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) { const d = S.deco[y][x]; if (d === 'tree') trees++; else if (d === 'stump') stumps++; else if (d === 'ore') ore++; }
    days.push({
      day: S.day, res: ['food', 'wood', 'logs', 'stone', 'ore', 'gold'].map(k => S.res[k]), peasants: S.peasants, blds: S.blds.length,
      trees, stumps, ore, crews: crewDigest(), folk: folkDigest()
    });
  }
  out.ticks = { seed: 7, R: 10, lodges: lodges.map(([x, y]) => x + ',' + y).join(' '), days };
  console.log('lodges at', out.ticks.lodges);
  const last = days[days.length - 1];
  console.log('tick scenario: day', last.day, 'peasants', last.peasants, 'buildings', last.blds, 'folk', sim.folkOf(S).length,
    'crew buildings', S.blds.filter(b => sim.CREW_KIND[b.t]).map(b => b.t).join(','));
}
fs.writeFileSync(path.join(__dirname, 'reference.json'), JSON.stringify(out));
console.log('wrote reference.json for seeds', out.seeds.map(s => s.seed).join(', '));
