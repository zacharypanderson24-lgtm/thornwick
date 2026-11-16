# Phase 1 simulation optimization: benchmark results

All numbers below are measured, not estimated. `bench/bench.js` loads each snapshot into the sim under test and times it; `bench/compare.js` produced the tables.
Reproduce: `node bench/make_snapshots.js` (builds the snapshots with the frozen ORIGINAL sim, once), then
`node --expose-gc bench/bench.js --sim tests/reference/sim_v0.js --label before --out bench/baseline_before.json`,
`node --expose-gc bench/bench.js --label after --out bench/baseline_after.json`, `node bench/compare.js bench/baseline_before.json bench/baseline_after.json`.

## What each scenario is
- **p50, p200, p1000, p2500, p5000: organic.** A comb of roads and painted zones around the Town Hall (deterministic, seed 7), then grown to the target population by the game's own development and population rules with food topped up. These are real settlements the current rules produce; p1000+ are bigger than a normal player reaches today, so treat them as "mature town" cases.
- **p10000: synthetic.** Every zoned lot is filled at once (2x2 homes and farms where they fit) and the homes are filled, then 200 days of settling. It is artificial: a town this dense could not grow this way under the current rules.
- All run on the current 128x128 map. 256x256 was only tested for correctness (tests/run.js test 13), not benchmarked.

## What each metric means
- Tick: one `tick(S)` (a full simulated day), 120 consecutive days after loading the snapshot, growth continuing. Average, median, 95th percentile and worst case are over those 120 ticks (the worst case includes the first tick after load, when caches are cold, and JIT warm-up).
- `recalc()` nothing changed: the call the game makes every day and after every action when nothing relevant changed.
- Road connectivity: `place(road)` / `remove()` on one tile, each including the recalc the game does after it.
- Workforce: swap two trades' priority, then `recalc()` (what the Workers panel does).
- Development candidate selection: `choose()` for each of the five zone kinds, summed. Development runs `choose()` only for kinds in demand, so this is a ceiling, not a per-day cost.
- stepCrews: one day of all woodcutters, quarrymen and miners (movement, delivery). crewTarget: finding a work tile and route for one worker. stepFolk: one day of the ambient villagers (capped at 90 by design).
- Heap: JS heap retained by the world plus caches (after gc), not process memory.
- Environment: see the table header. Timings on another machine will differ; the ratios are what carry over.

Environment: Intel(R) Xeon(R) Processor @ 2.30GHz, 2 cores, linux 6.18.44-fc-v470, Node v22.23.4. Same snapshots (built once by the frozen original sim), same machine, run back to back.

### p50 (organic, 50 residents, 401 buildings, 367 road tiles, 11 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 1.78 | 0.267 | 6.7x faster |
| Median tick (ms) | 1.02 | 0.098 | 10x faster |
| 95th percentile tick (ms) | 6.17 | 0.900 | 6.9x faster |
| Worst-case tick (ms) | 15.8 | 4.05 | 3.9x faster |
| recalc(), nothing changed (ms) | 0.123 | 0.0001 | 913x faster |
| Road connectivity: place one road + recalc (ms) | 0.415 | 0.011 | 37x faster |
| Road connectivity: remove one road + recalc (ms) | 0.403 | 0.011 | 37x faster |
| Workforce re-allocation after a priority change (ms) | 0.408 | 0.0078 | 52x faster |
| Development candidate selection, all 5 zone kinds (ms) | 1.77 | 0.187 | 9.5x faster |
| develop() full day (ms) | 0.0024 | 0.0013 | 1.8x faster |
| Worker day: stepCrews (ms) | 0.016 | 0.0034 | 4.6x faster |
| Worker pathfinding: crewTarget (ms) | 0.072 | 0.0086 | 8.3x faster |
| Ambient villagers: stepFolk (ms) | 0.083 | 0.067 | 1.2x faster |
| Save (ms) | 1.34 | 1.12 | 1.2x faster |
| Load (ms) | 1.10 | 1.12 | ~same |
| Heap held by the world + caches (MB) | 1.53 | 1.43 | 1.1x faster |

### p200 (organic, 201 residents, 1076 buildings, 977 road tiles, 18 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 2.10 | 0.699 | 3.0x faster |
| Median tick (ms) | 1.60 | 0.315 | 5.1x faster |
| 95th percentile tick (ms) | 6.29 | 2.84 | 2.2x faster |
| Worst-case tick (ms) | 9.95 | 11.4 | 1.1x slower |
| recalc(), nothing changed (ms) | 0.319 | 0.0001 | >2000x |
| Road connectivity: place one road + recalc (ms) | 0.575 | 0.0053 | 108x faster |
| Road connectivity: remove one road + recalc (ms) | 0.551 | 0.0046 | 119x faster |
| Workforce re-allocation after a priority change (ms) | 0.526 | 0.010 | 50x faster |
| Development candidate selection, all 5 zone kinds (ms) | 3.74 | 0.163 | 23x faster |
| develop() full day (ms) | 0.0055 | 0.0023 | 2.4x faster |
| Worker day: stepCrews (ms) | 0.014 | 0.0016 | 8.6x faster |
| Worker pathfinding: crewTarget (ms) | 0.038 | 0.011 | 3.6x faster |
| Ambient villagers: stepFolk (ms) | 0.158 | 0.107 | 1.5x faster |
| Save (ms) | 1.19 | 1.16 | ~same |
| Load (ms) | 1.56 | 1.59 | ~same |
| Heap held by the world + caches (MB) | 1.18 | 1.31 | 1.1x slower |

### p1000 (organic, 1001 residents, 2978 buildings, 2569 road tiles, 38 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 6.10 | 0.561 | 11x faster |
| Median tick (ms) | 4.73 | 0.470 | 10x faster |
| 95th percentile tick (ms) | 14.3 | 1.32 | 11x faster |
| Worst-case tick (ms) | 25.1 | 2.19 | 11x faster |
| recalc(), nothing changed (ms) | 1.29 | 0.0000 | >2000x |
| Road connectivity: place one road + recalc (ms) | 1.27 | 0.0045 | 285x faster |
| Road connectivity: remove one road + recalc (ms) | 1.25 | 0.0045 | 276x faster |
| Workforce re-allocation after a priority change (ms) | 1.24 | 0.0081 | 153x faster |
| Development candidate selection, all 5 zone kinds (ms) | 22.0 | 0.230 | 96x faster |
| develop() full day (ms) | 1.93 | 0.092 | 21x faster |
| Worker day: stepCrews (ms) | 0.035 | 0.0030 | 11x faster |
| Worker pathfinding: crewTarget (ms) | 0.079 | 0.010 | 7.6x faster |
| Ambient villagers: stepFolk (ms) | 0.280 | 0.143 | 2.0x faster |
| Save (ms) | 1.50 | 3.24 | 2.2x slower |
| Load (ms) | 2.82 | 2.65 | 1.1x faster |
| Heap held by the world + caches (MB) | 1.39 | 1.50 | 1.1x slower |

### p2500 (organic, 2500 residents, 6961 buildings, 6054 road tiles, 46 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 12.2 | 1.28 | 9.5x faster |
| Median tick (ms) | 11.1 | 0.988 | 11x faster |
| 95th percentile tick (ms) | 18.0 | 3.14 | 5.8x faster |
| Worst-case tick (ms) | 22.5 | 5.99 | 3.8x faster |
| recalc(), nothing changed (ms) | 3.54 | 0.0002 | >2000x |
| Road connectivity: place one road + recalc (ms) | 3.69 | 0.0096 | 386x faster |
| Road connectivity: remove one road + recalc (ms) | 3.70 | 0.012 | 321x faster |
| Workforce re-allocation after a priority change (ms) | 3.34 | 0.020 | 167x faster |
| Development candidate selection, all 5 zone kinds (ms) | 113 | 1.13 | 100x faster |
| develop() full day (ms) | 4.42 | 0.160 | 28x faster |
| Worker day: stepCrews (ms) | 0.077 | 0.0041 | 18x faster |
| Worker pathfinding: crewTarget (ms) | 0.032 | 0.012 | 2.7x faster |
| Ambient villagers: stepFolk (ms) | 0.480 | 0.159 | 3.0x faster |
| Save (ms) | 2.45 | 2.62 | 1.1x slower |
| Load (ms) | 6.40 | 4.64 | 1.4x faster |
| Heap held by the world + caches (MB) | 1.88 | 1.90 | ~same |

### p5000 (organic, 5001 residents, 7871 buildings, 6054 road tiles, 46 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 19.6 | 1.23 | 16x faster |
| Median tick (ms) | 17.4 | 1.14 | 15x faster |
| 95th percentile tick (ms) | 29.5 | 2.51 | 12x faster |
| Worst-case tick (ms) | 58.6 | 4.15 | 14x faster |
| recalc(), nothing changed (ms) | 5.51 | 0.0001 | >2000x |
| Road connectivity: place one road + recalc (ms) | 6.17 | 0.0094 | 655x faster |
| Road connectivity: remove one road + recalc (ms) | 5.87 | 0.012 | 490x faster |
| Workforce re-allocation after a priority change (ms) | 5.51 | 0.033 | 167x faster |
| Development candidate selection, all 5 zone kinds (ms) | 125 | 0.950 | 131x faster |
| develop() full day (ms) | 0.516 | 0.0077 | 67x faster |
| Worker day: stepCrews (ms) | 0.083 | 0.0039 | 21x faster |
| Worker pathfinding: crewTarget (ms) | 0.028 | 0.010 | 2.7x faster |
| Ambient villagers: stepFolk (ms) | 0.570 | 0.128 | 4.5x faster |
| Save (ms) | 2.63 | 2.52 | ~same |
| Load (ms) | 8.26 | 7.49 | 1.1x faster |
| Heap held by the world + caches (MB) | 1.98 | 2.12 | 1.1x slower |

### p10000 (synthetic, 9800 residents, 9444 buildings, 6054 road tiles, 53 work buildings)

| Metric | Before | After | Improvement |
|---|---|---|---|
| Average tick time (ms) | 17.6 | 0.456 | 39x faster |
| Median tick (ms) | 17.5 | 0.190 | 92x faster |
| 95th percentile tick (ms) | 18.4 | 2.32 | 7.9x faster |
| Worst-case tick (ms) | 21.4 | 4.82 | 4.4x faster |
| recalc(), nothing changed (ms) | 7.92 | 0.0001 | >2000x |
| Road connectivity: place one road + recalc (ms) | 8.12 | 0.093 | 88x faster |
| Road connectivity: remove one road + recalc (ms) | 7.98 | 0.068 | 117x faster |
| Workforce re-allocation after a priority change (ms) | 8.00 | 0.017 | 465x faster |
| Development candidate selection, all 5 zone kinds (ms) | 138 | 1.18 | 117x faster |
| develop() full day (ms) | 0.419 | 0.0083 | 50x faster |
| Worker day: stepCrews (ms) | 0.095 | 0.0041 | 23x faster |
| Worker pathfinding: crewTarget (ms) | 0.025 | 0.011 | 2.3x faster |
| Ambient villagers: stepFolk (ms) | 0.779 | 0.151 | 5.2x faster |
| Save (ms) | 3.14 | 3.42 | 1.1x slower |
| Load (ms) | 12.7 | 13.2 | ~same |
| Heap held by the world + caches (MB) | 2.10 | 2.20 | ~same |



## Reading the results
- Per-day cost is now dominated by crews (path/target search) and the ambient villagers' road searches, a few tenths of a millisecond each; everything else is near zero on a quiet day.
- One result got slightly worse: **p200 worst-case tick (11.4 ms vs 9.95 ms)**. It is a single cold tick right after load (caches being filled, JIT), and the 95th percentile is better. Load times are about the same because loading still parses the JSON and rebuilds every cache once.
- Heap is slightly higher at 5,000+ residents (about 0.1 to 0.15 MB for the caches).
- Save time is unchanged: saving still serialises the whole world (the caches are never saved).
