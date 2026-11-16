'use strict';
// Benchmark scenarios. All are deterministic (seed 7, no randomness in the sim). "organic" settlements are grown by the game's own
// development rules from a painted zone layout, so they are what real play looks like at that size. "synthetic" ones are artificially dense layouts
// that push past what a normal player would build on this map.
module.exports = [
  { name: 'p50',    kind: 'organic',   R: 14, target: 50 },
  { name: 'p200',   kind: 'organic',   R: 24, target: 200 },
  { name: 'p1000',  kind: 'organic',   R: 40, target: 1000 },
  { name: 'p2500',  kind: 'organic',   R: 62, target: 2500 },
  { name: 'p5000',  kind: 'organic',   R: 62, target: 5000 },
  { name: 'p10000', kind: 'synthetic', R: 62, target: 10000, dense: true },
];
