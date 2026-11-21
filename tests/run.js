'use strict';
// Test suite: node tests/run.js [filter]. No dependencies. Compares the optimized sim with the frozen original (tests/reference/sim_v0.js) wherever it can.
const fs = require('fs'), path = require('path');
const L = require('./lib.js'), E = require('./equiv.js');
const sim = L.loadSim(L.CURRENT), I = sim.__i, N = sim.N, C0 = sim.START;
const filter = process.argv[2] || '';
let pass = 0, fail = 0; const failed = [];
function test(name, fn) {
  if (filter && !name.includes(filter)) return;
  const t0 = Date.now();
  try { fn(); pass++; console.log('ok   ' + name + '  (' + (Date.now() - t0) + ' ms)'); }
  catch (e) { fail++; failed.push(name); console.log('FAIL ' + name + '\n     ' + String(e.stack || e).split('\n').slice(0, 4).join('\n     ')); }
}
const eq = (a, b, m) => { if (a !== b) throw new Error((m || 'expected equal') + ': ' + a + ' !== ' + b); };
const ok = (c, m) => { if (!c) throw new Error(m || 'assertion failed'); };
const audit = (S, m) => { const bad = sim.auditCaches(S); if (bad.length) throw new Error((m || 'audit') + ': ' + bad.slice(0, 4).join('; ')); };
const food = S => { S.res.food = Math.max(S.res.food, 300); S.res.wood = Math.max(S.res.wood, 200); S.res.gold = Math.max(S.res.gold, 100); };
// A flat, tree-free world to build test layouts in (the terrain around the Hall is flat for 7+ tiles; clear a wider square of trees/rocks).
function clean(seed, R) {
  const S = sim.createWorld(seed || 7), r = R || 12;
  for (let y = C0 - r; y <= C0 + r; y++) for (let x = C0 - r; x <= C0 + r; x++) if (!S.grid[y][x]) { const k = S.deco[y][x]; if (k === 'tree') sim.remove(S, x, y); else if (k && k !== 'rock') S.deco[y][x] = null; }
  sim.remove(S, C0 - 1, C0 + 5);   // the starting farm would soak up workers in the labour tests
  sim.invalidateCaches(S); sim.recalc(S, true); return S;
}
const road = (S, pts) => { S.res.wood = 500; for (const [x, y] of pts) if (!S.grid[y][x]) sim.addBld(S, 'road', x, y); sim.recalc(S); };
const hline = (x0, x1, y) => { const a = []; for (let x = x0; x <= x1; x++) a.push([x, y]); return a; };
const vline = (x, y0, y1) => { const a = []; for (let y = y0; y <= y1; y++) a.push([x, y]); return a; };
const connAt = (S, x, y) => S.d.conn[y * N + x];

test('Smithery occupies four tiles, smelts ore, and respects stockpile and road access', () => {
  const S = clean();
  const x = C0 + 1, y = C0 + 4;
  for (let row = C0 + 2; row <= C0 + 6; row++) sim.remove(S, C0, row);
  eq(sim.canPlace(S, 'smithery', x, y).ok, false, 'requires a road');
  road(S, vline(C0, C0 + 2, C0 + 5));
  S.deco[y + 1][x + 1] = 'ore';
  eq(sim.canPlace(S, 'smithery', x, y).ok, false, 'checks the entire footprint');
  S.deco[y + 1][x + 1] = null;
  ok(sim.place(S, 'smithery', x, y).ok, 'builds at the road');
  const b = S.grid[y][x];
  eq(S.grid[y + 1][x + 1], b);
  eq(b.workers, 3);
  S.res.ore = 10;
  sim.tick(S); eq(S.res.ore, 6); eq(S.res.metal, 2);
  S.res.metal = 399.5;
  sim.tick(S); eq(S.res.ore, 5); eq(S.res.metal, 400);
  sim.tick(S); eq(S.res.ore, 5, 'full output storage preserves ore');
  const loaded = sim.loadWorld(sim.saveWorld(S));
  eq(loaded.res.metal, 400);
  eq(loaded.grid[y][x].sz, 2);
  sim.remove(loaded, C0, C0 + 2);
  loaded.res.metal = 0;
  sim.tick(loaded);
  eq(loaded.res.metal, 0, 'disconnecting the road stops production');
  eq(loaded.res.ore, 5);
  const legacy = JSON.parse(sim.saveWorld(clean()));
  delete legacy.res.metal;
  delete legacy.rate.metal;
  eq(sim.loadWorld(JSON.stringify(legacy)).res.metal, 0);
});
test('1. road connection and disconnection', () => {
  const S = clean(); const h = C0 + 2;
  road(S, hline(C0 + 2, C0 + 8, C0));                  // starts touching the Hall's east edge
  for (let x = C0 + 2; x <= C0 + 8; x++) eq(connAt(S, x, C0), 1, 'connected ' + x);
  road(S, hline(C0 + 12, C0 + 14, C0));                // a separate stub
  eq(connAt(S, C0 + 13, C0), 0, 'stub is not connected');
  road(S, [[C0 + 9, C0], [C0 + 10, C0], [C0 + 11, C0]]);   // joining it brings the stub in
  eq(connAt(S, C0 + 13, C0), 1, 'stub joined'); audit(S);
  sim.remove(S, C0 + 10, C0);                          // cut the middle
  eq(connAt(S, C0 + 9, C0), 1); eq(connAt(S, C0 + 11, C0), 0); eq(connAt(S, C0 + 14, C0), 0, 'far side switched off'); audit(S);
  sim.addBld(S, 'road', C0 + 10, C0); sim.recalc(S);
  eq(connAt(S, C0 + 14, C0), 1, 'reconnected'); audit(S);
});
test('2. chokepoints, loops and bridges', () => {
  const S = clean();
  // a ring with a tail (a bridge) leading to a second ring
  road(S, hline(C0 + 2, C0 + 6, C0)); road(S, hline(C0 + 2, C0 + 6, C0 + 4)); road(S, vline(C0 + 2, C0 + 1, C0 + 3)); road(S, vline(C0 + 6, C0 + 1, C0 + 3));   // ring 1
  road(S, hline(C0 + 7, C0 + 9, C0 + 2));                                              // the bridge, 3 tiles long
  road(S, hline(C0 + 10, C0 + 13, C0)); road(S, hline(C0 + 10, C0 + 13, C0 + 4)); road(S, vline(C0 + 10, C0 + 1, C0 + 3)); road(S, vline(C0 + 13, C0 + 1, C0 + 3));   // ring 2
  road(S, [[C0 + 10, C0 + 2]]);
  eq(connAt(S, C0 + 13, C0 + 4), 1, 'second ring connected through the bridge'); audit(S);
  sim.remove(S, C0 + 4, C0);                            // a ring tile: the ring still connects the other way round
  eq(connAt(S, C0 + 2, C0), 1); eq(connAt(S, C0 + 6, C0), 1, 'loop keeps it connected'); audit(S);
  sim.remove(S, C0 + 8, C0 + 2);                        // the bridge: everything beyond goes dark
  for (const [x, y] of [[C0 + 9, C0 + 2], [C0 + 10, C0 + 2], [C0 + 13, C0], [C0 + 13, C0 + 4]]) eq(connAt(S, x, y), 0, 'beyond the bridge ' + x + ',' + y);
  eq(connAt(S, C0 + 7, C0 + 2), 1, 'near side still on'); audit(S);
  sim.addBld(S, 'road', C0 + 8, C0 + 2); sim.recalc(S); eq(connAt(S, C0 + 13, C0 + 4), 1); audit(S);
});
test('3. road add/remove fuzz against a full flood fill', () => {
  for (const seed of [1, 2, 3]) {
    const S = clean(seed, 20), R = E.rng(seed * 99); S.res.wood = 5000;
    for (let op = 0; op < 1500; op++) {
      const x = C0 - 18 + Math.floor(R() * 37), y = C0 - 18 + Math.floor(R() * 37);
      if (R() < 0.62) { if (!S.grid[y][x] && !sim.hasTree(S, x, y) && !sim.isSolid(S, x, y)) sim.addBld(S, 'road', x, y); } else if (S.grid[y][x] && S.grid[y][x].t === 'road') sim.remove(S, x, y);
      if (R() < 0.5) { sim.recalc(S); audit(S, 'seed ' + seed + ' op ' + op); }
    }
    sim.recalc(S); audit(S);
  }
});
test('4. workforce follows buildings, jobs and priorities', () => {
  const S = clean(); road(S, vline(C0, C0 + 2, C0 + 8)); S.peasants = 5;
  for (let i = 0; i < 3; i++) { sim.addBld(S, 'house', C0 - 1, C0 + 3 + i * 2); }
  sim.addBld(S, 'farm', C0 + 1, C0 + 3); sim.addBld(S, 'wood', C0 + 1, C0 + 5); sim.recalc(S);
  const farm = S.blds.find(b => b.t === 'farm'), wood = S.blds.find(b => b.t === 'wood');
  eq(farm.workers, 4, 'farm is filled first'); eq(wood.workers, 1, 'the rest go to the next trade');
  S.prio = ['timber', 'farming', 'woodwork', 'quarrying', 'mining', 'trade']; sim.recalc(S);
  eq(wood.workers, 3); eq(farm.workers, 2, 'priority change reallocates');
  sim.remove(S, wood.x, wood.y); eq(farm.workers, 4, 'removing a workplace frees workers'); eq(S.d.idle, 1);
  S.peasants = 2; sim.recalc(S); eq(farm.workers, 2, 'population change reallocates'); audit(S);
  sim.addBld(S, 'sawmill', C0 + 1, C0 + 7); sim.recalc(S);   // not road-connected? it touches the road at x=C0
  eq(S.blds.find(b => b.t === 'sawmill').served, true);
});
test('5. well and market coverage are local', () => {
  const S = clean(); road(S, vline(C0, C0 + 2, C0 + 12)); S.peasants = 10;
  const near = sim.addBld(S, 'house', C0 - 1, C0 + 3), far = sim.addBld(S, 'house', C0 - 1, C0 + 11); sim.recalc(S);
  eq(near.lvl, 1); eq(far.lvl, 1);
  sim.addBld(S, 'well', C0 + 1, C0 + 3); sim.recalc(S);
  eq(near.lvl, 2, 'well in range upgrades the house'); eq(far.lvl, 1, 'well out of range does not'); audit(S);
  sim.remove(S, C0 + 1, C0 + 3); eq(near.lvl, 1, 'removing the well downgrades it');
  sim.addBld(S, 'market', C0 + 1, C0 + 4); sim.recalc(S);
  eq(near.covered, true); eq(far.covered, false, 'market covers 5 tiles'); ok(S.d.cov > 0 && S.d.cov < 1);
  sim.remove(S, C0, C0 + 6); sim.recalc(S);   // cut the road below the market: the lower house loses service, the market stays served
  audit(S);
});
test('6. automatic zoning only builds in valid places', () => {
  const S = clean(7, 16); road(S, vline(C0, C0 + 2, C0 + 14)); road(S, hline(C0 + 1, C0 + 10, C0 + 8));
  for (let y = C0 + 3; y <= C0 + 14; y++) for (let x = C0 + 1; x <= C0 + 10; x++) sim.setZone(S, 'res', x, y);
  for (let y = C0 + 3; y <= C0 + 7; y++) for (let x = C0 - 6; x <= C0 - 1; x++) sim.setZone(S, 'farm', x, y);
  let built = 0, bad = [];
  for (let d = 0; d < 250; d++) {
    const before = S.blds.length; food(S); sim.tick(S);
    for (const b of S.blds.slice(before)) {
      built++; const kind = { house: 'res', farm: 'farm', market: 'comm' }[b.t];
      for (const [x, y] of I.tilesOf(b)) { if (kind && S.zone[y][x] !== kind) bad.push(b.t + ' off-zone ' + x + ',' + y); if (sim.isSolid(S, x, y) || sim.hasTree(S, x, y)) bad.push('invalid ground'); }
      if (!I.servedAt(S, b, S.d.conn)) bad.push(b.t + ' not road-connected ' + b.x + ',' + b.y);
      if (b.t === 'farm') for (const [x, y] of I.tilesOf(b)) if (!S.fert[y][x]) bad.push('farm on poor soil');
    }
  }
  ok(built > 5, 'something grew (' + built + ')'); eq(bad.length, 0, bad.slice(0, 3).join('; '));
  // the unconnected zone must stay empty
  const S2 = clean(7, 16); for (let y = C0 + 3; y <= C0 + 9; y++) for (let x = C0 + 3; x <= C0 + 9; x++) sim.setZone(S2, 'res', x, y);
  for (let d = 0; d < 60; d++) { food(S2); sim.tick(S2); } eq(S2.blds.filter(b => b.t === 'house').length, 2, 'no roads, so only the starting homes');
});
test('7. workers walk, work and deliver before output is credited', () => {
  const S = clean(7, 14); road(S, vline(C0, C0 + 2, C0 + 7)); const area = [];
  for (let y = C0 + 4; y <= C0 + 7; y++) for (let x = C0 + 3; x <= C0 + 6; x++) { S.deco[y][x] = 'tree'; sim.setZone(S, 'wood', x, y); area.push([x, y]); }
  const lodge = sim.addBld(S, 'wood', C0 + 1, C0 + 5); S.peasants = 6; sim.addBld(S, 'house', C0 - 1, C0 + 3); sim.recalc(S);
  eq(lodge.workers, 3, 'three workers');
  const crew = sim.crewOf(lodge), out = { logs: 0, stone: 0, ore: 0 }, daily = []; let seen = { out: false, work: false, back: false };
  for (let d = 0; d < 60; d++) {
    const b = out.logs; sim.recalc(S); sim.stepCrews(S, out); S.day++; daily.push(out.logs - b);
    for (const a of crew) seen[a.st] = true;
  }
  ok(seen.out && seen.work && seen.back, 'workers walked out, worked and came back with loads');
  eq(daily.slice(0, 3).reduce((p, q) => p + q, 0), 0, 'nothing is credited before a worker has walked out, worked and carried a load home');
  ok(out.logs > 0 && out.logs % sim.LOAD.wood === 0, 'output arrives only in whole loads: ' + out.logs);
  const chops = out.logs / sim.LOAD.wood, tileChops = Object.values(S.chop || {}).reduce((p, q) => p + q, 0), stumps = area.filter(([x, y]) => S.deco[y][x] === 'stump').length;
  const carrying = crew.filter(a => a.st === 'back' && a.carry > 0).length;
  eq(tileChops + stumps * sim.CHOPS_PER_TREE, chops + carrying, 'every chop is one load, credited when it is delivered (4 chops fell a tree); loads still on the road: ' + carrying);
  for (const a of crew) ok(a.st === 'home' || a.path, 'a worker away from home has a route');
});
test('8. finite stone and ore deplete and stop', () => {
  const S = clean(7, 14); road(S, vline(C0, C0 + 2, C0 + 7));
  const rocks = [[C0 + 3, C0 + 5], [C0 + 4, C0 + 5], [C0 + 3, C0 + 6]]; let cap = 0;
  for (const [x, y] of rocks) { S.deco[y][x] = 'rock'; S.rockAmt[y * N + x] = 2; sim.setZone(S, 'mine', x, y); cap += sim.rockCap(S, x, y); }
  const q = sim.addBld(S, 'quarry', C0 + 1, C0 + 5); S.peasants = 6; sim.addBld(S, 'house', C0 - 1, C0 + 3); sim.recalc(S);
  const s0 = S.res.stone; S.res.stone = 0; let total = 0;
  for (let d = 0; d < 400; d++) { food(S); const b = S.res.stone; S.res.stone = Math.min(S.res.stone, 100); sim.tick(S); }
  const left = rocks.reduce((p, [x, y]) => p + sim.rockLeft(S, x, y), 0);
  ok(left < 1e-6, 'the rock is worked out (left ' + left + ')'); ok(S.res.stone <= cap + 1e-6, 'never more than the ground held: ' + S.res.stone + ' <= ' + cap);
  const dugAfter = rocks.reduce((p, [x, y]) => p + sim.digDone(S, x, y), 0); eq(Math.round(dugAfter), cap, 'every reserve taken, none invented');
  // ore: a tile vanishes after ORE_CAP
  const S3 = clean(7, 14); road(S3, vline(C0, C0 + 2, C0 + 7)); S3.deco[C0 + 5][C0 + 3] = 'ore'; sim.setZone(S3, 'mine', C0 + 3, C0 + 5);
  sim.addBld(S3, 'mine', C0 + 1, C0 + 5); S3.peasants = 6; sim.addBld(S3, 'house', C0 - 1, C0 + 3); sim.recalc(S3); S3.res.ore = 0;
  for (let d = 0; d < 200; d++) { food(S3); sim.tick(S3); }
  eq(S3.deco[C0 + 5][C0 + 3], null, 'the ore tile is gone'); ok(S3.res.ore <= sim.ORE_CAP + 1e-6, 'ore taken <= ' + sim.ORE_CAP + ': ' + S3.res.ore);
});
test('9. terrain and building changes invalidate walking routes', () => {
  const S = clean(7, 14); road(S, vline(C0, C0 + 2, C0 + 7));
  for (const [x, y] of [[C0 + 3, C0 + 5]]) { S.deco[y][x] = 'tree'; sim.setZone(S, 'wood', x, y); }
  const lodge = sim.addBld(S, 'wood', C0 + 1, C0 + 5); sim.recalc(S);
  const t1 = I.crewTarget(S, lodge, 'tree', 0, null); ok(t1 && t1.path.length >= 2, 'a route exists');
  const R1 = I.crewReach(S, lodge), st1 = R1.stamp; eq(I.crewReach(S, lodge).stamp, st1, 'unchanged terrain reuses the cached area');
  // wall the tree in with ore (solid) on every side that a worker could stand: the cached area must refresh and the target disappears
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const x = C0 + 3 + dx, y = C0 + 5 + dy; S.deco[y][x] = 'ore'; sim.touch(S, x, y, sim.CHUNK ? 7 : 0); }
  const t2 = I.crewTarget(S, lodge, 'tree', 0, null); eq(t2, null, 'no reachable side after the walls went up');
  // the reference sim on an identical world agrees
  const A = L.loadSim(L.REFERENCE), Sa = A.loadWorld(sim.saveWorld(S)); const lodgeA = Sa.blds.find(b => b.t === 'wood'); eq(A.__i.crewTarget(Sa, lodgeA, 'tree', 0, null), null, 'original agrees');
  S.deco[C0 + 5][C0 + 4] = null; sim.touch(S, C0 + 4, C0 + 5, 7); const t3 = I.crewTarget(S, lodge, 'tree', 0, null); ok(t3 !== null, 'opening one side restores a route');
  // a dug pit blocks walking
  sim.addBld(S, 'road', C0 + 2, C0 + 5); sim.recalc(S);
  const R2 = I.crewReach(S, lodge); ok(R2.stamp !== st1, 'a new road refreshed the cached area');
});
test('10. save / load round trip and old saves', () => {
  const S = clean(7, 16); road(S, vline(C0, C0 + 2, C0 + 10)); for (let y = C0 + 3; y <= C0 + 10; y++) for (let x = C0 + 1; x <= C0 + 4; x++) sim.setZone(S, 'res', x, y);
  for (let d = 0; d < 200; d++) { food(S); sim.tick(S); }
  const txt = sim.saveWorld(S), S2 = sim.loadWorld(txt); audit(S2, 'after load');
  eq(E.canon(JSON.parse(sim.saveWorld(S2))), E.canon(JSON.parse(txt)), 'save -> load -> save is identical');
  const A = L.loadSim(L.REFERENCE), Sa = A.loadWorld(txt); for (let d = 0; d < 80; d++) { for (const [X, W] of [[A, Sa], [sim, S2]]) { food(W); X.tick(W); } }
  eq(E.snapshot(A, Sa), E.snapshot(sim, S2), 'a loaded save then plays on exactly like the original');
  // saves from smaller maps (40x40 and 64x64, and a legacy one without heights): centred into 128 by growSave, identical in both sims
  for (const [n, strip] of [[64, false], [40, false], [40, true], [64, true]]) {
    const R = L.loadSim(L.REFERENCE, [], { N: n }), W = R.createWorld(3); L.layout(R, W, n === 64 ? 12 : 8); for (let d = 0; d < 120; d++) { W.res.food = 300; W.res.wood = 200; R.tick(W); }
    let o = JSON.parse(R.saveWorld(W)); if (strip) for (const k of ['hv', 'v2', 'sapT', 'dig', 'oreLeft', 'elev', 'stumpT', 'rockAmt', 'chop']) delete o[k];
    const t = JSON.stringify(o), X = A.loadWorld(t), Y = sim.loadWorld(t);
    eq(E.snapshot(A, X), E.snapshot(sim, Y), 'old ' + n + 'x' + n + (strip ? ' legacy' : '') + ' save loads the same'); audit(Y);
    for (let d = 0; d < 40; d++) { for (const [Q, T] of [[A, X], [sim, Y]]) { food(T); Q.tick(T); } }
    eq(E.snapshot(A, X), E.snapshot(sim, Y), 'and plays the same (' + n + ')');
  }
});
test('11. deterministic: same seed and actions, same world', () => {
  const run = () => { const r = E.lockstep({ N: 128, seed: 5, days: 120, every: 40, layoutR: 14 }); return r; };
  const r = run(); eq(r.diff, null, JSON.stringify(r.diff));
  const mk = () => { const S = sim.createWorld(9); L.layout(sim, S, 14); for (let d = 0; d < 150; d++) { food(S); sim.tick(S); } return E.snapshot(sim, S); };
  eq(mk(), mk(), 'two runs agree');
  ok(E.snapshot(sim, sim.createWorld(9)) !== E.snapshot(sim, sim.createWorld(10)), 'different seeds differ');
});
test('12. long run stays stable (3000 days)', () => {
  const S = sim.createWorld(7); L.layout(sim, S, 30); let peak = 0;
  for (let d = 0; d < 3000; d++) {
    food(S); sim.tick(S); peak = Math.max(peak, S.peasants);
    for (const k in S.res) { ok(Number.isFinite(S.res[k]) && S.res[k] >= 0 && S.res[k] <= 400, 'resource ' + k + ' = ' + S.res[k] + ' day ' + d); }
    ok(Number.isFinite(S.peasants) && S.peasants >= 0 && S.peasants <= S.d.civ + 1, 'population ' + S.peasants + ' civ ' + S.d.civ);
    if (d % 150 === 149) audit(S, 'day ' + d);
  }
  ok(peak > 300, 'the town grew (peak ' + peak + ')');
});
test('13. same results at other map sizes (no 128 hard-coding): 64 and 256', () => {
  for (const [n, seed, days, lay] of [[64, 4, 150, 10], [256, 6, 90, 12], [256, 8, 60, 30]]) {
    const r = E.lockstep({ N: n, seed, days, every: 10, layoutR: lay, audit: n === 64 }); eq(r.diff, null, 'N=' + n + ' ' + JSON.stringify(r.diff).slice(0, 300));
  }
});
test('14. render stamps change whenever the drawn layers would', () => {
  const S = sim.createWorld(7); L.layout(sim, S, 16); const R = E.rng(77);
  const gScan = S => { let h = 0; for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) { const z = S.zone[y][x]; h = (Math.imul(h, 31) + (z && !S.grid[y][x] ? z.charCodeAt(0) + 7 : 0) + (S.fert[y][x] ? 3 : 0) + (S.deco[y][x] === 'rock' ? 5 : 0)) | 0; } for (let i = 0; i < S.hv.length; i++) h = (Math.imul(h, 31) + S.hv[i]) | 0; return h; };
  const dScan = S => { let h = 0; for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) { const k = S.deco[y][x]; if (k && (k === 'tree' || k === 'ore' || k === 'stump' || k === 'sapling')) h = (Math.imul(h, 31) + k.charCodeAt(0) * 3 + k.length + ((y * N + x) & 255)) | 0; } return h; };
  let g0 = gScan(S), d0 = dScan(S), gs = sim.groundStamp ? 0 : 0; const gst = () => sim.cacheOf(S).gSeq, dst = () => sim.cacheOf(S).dSeq; let gs0 = gst(), ds0 = dst();
  for (let step = 0; step < 400; step++) {
    E.act(sim, S, R, N); if (step % 3 === 0) { food(S); sim.tick(S); } sim.recalc(S);
    const g1 = gScan(S), d1 = dScan(S);
    if (g1 !== g0) ok(gst() !== gs0, 'ground layer changed without a ground stamp (step ' + step + ')');
    if (d1 !== d0) ok(dst() !== ds0, 'decoration changed without a deco stamp (step ' + step + ')');
    g0 = g1; d0 = d1; gs0 = gst(); ds0 = dst();
  }
});
console.log('\n' + pass + ' passed, ' + fail + ' failed' + (fail ? ': ' + failed.join(', ') : ''));
process.exit(fail ? 1 : 0);
