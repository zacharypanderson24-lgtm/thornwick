// ---------- Chunks, change stamps and derived-data caches (no DOM) ----------
// Everything in here is RUNTIME ONLY: it lives in a WeakMap keyed by the world object S, is never saved, and is rebuilt from the world after loading.
// The world stays the single source of truth; a cache is only ever a faster way of asking a question about it.
//
// MAP SIZE: nothing here assumes 128x128. All sizes come from N (sim.js), and the map is cut into CHUNK x CHUNK tile chunks (CH x CH of them). Every
// cache is bucketed by chunk, so changing one tile invalidates only the chunks near it, never the whole map. A 256x256 region is CH = 16 chunks a side.
//
// CHANGE STAMPS: touch(S, x, y, flags) records "something changed at this tile". Each chunk keeps the sequence number of its last change (cSeq) and of
// its last walkability change (wSeq); a cache entry built at stamp s is still valid while the stamps it depended on are still s. A change pads outwards by
// TOUCH_PAD tiles, because the things we cache about a tile (a worker site count, whether a 2x2 lot fits) look a few tiles around it.
//
// OWNERSHIP (future territory system): claimed(S, x, y) says whether the player may build on / extract from a tile. It is true everywhere today. Walking
// (walkCost, roads, armies, travellers) deliberately never asks it, so unclaimed land is never impassable. When land changes hands the territory code only
// has to call touchBox() over the parcel and every chunk cache refreshes itself.
const CHUNK_SHIFT = 4, CHUNK = 1 << CHUNK_SHIFT;   // 16x16-tile chunks (use 5 for 32x32)
const CH = Math.ceil(N / CHUNK);                   // chunks per row / column
const TOUCH_PAD = 4;                               // WORK_R + 1: a worker site count looks 3 tiles out, and rock workability looks one tile further
const F_WALK = 1, F_GROUND = 2, F_DECO = 4;        // touch flags: walkability, ground-layer look (zones, built tiles, rock, heights), baked decoration (trees, ore, stumps)
const CACHE = new WeakMap();
let WORLD_IDS = 0;
const claimed = (S, x, y) => true;                 // seam for land ownership; see above

function cacheOf(S) {
  let C = CACHE.get(S);
  if (!C) {
    C = {
      id: ++WORLD_IDS, ready: false, seq: 1, cSeq: new Int32Array(CH * CH), wSeq: new Int32Array(CH * CH),
      gSeq: 1, dSeq: 1,                 // whole-map stamps for the renderer's baked layers (ground, decoration)
      topoSeq: 1, listSeq: 1,           // topology changes (roads, buildings, who is served) and building-list changes
      roadOps: [], pendServe: new Set(), pendHouse: new Set(), totalsDirty: true, workDirty: true,
      L: null, hb: null, root: null, conn: null, dev: {}, reach: new WeakMap(), folk: null, tot: null, lastPeas: -1, lastPrio: null
    };
    CACHE.set(S, C);
  }
  return C;
}
// Throw every cache away; the next recalc rebuilds from the world. For code that edits the world directly instead of through place/remove/setZone.
function invalidateCaches(S) { const C = CACHE.get(S); if (C) { C.ready = false; C.dev = {}; C.reach = new WeakMap(); C.folk = null; C.seq++; C.cSeq.fill(C.seq); C.wSeq.fill(C.seq); C.gSeq++; C.dSeq++; C.topoSeq++; C.listSeq++; } }

function touchBox(S, x0, y0, x1, y1, flags) {
  const C = cacheOf(S), s = ++C.seq, w = (flags & F_WALK) !== 0;
  const cx0 = Math.max(0, x0) >> CHUNK_SHIFT, cx1 = Math.min(N - 1, x1) >> CHUNK_SHIFT, cy0 = Math.max(0, y0) >> CHUNK_SHIFT, cy1 = Math.min(N - 1, y1) >> CHUNK_SHIFT;
  for (let cy = cy0; cy <= cy1; cy++) for (let cx = cx0; cx <= cx1; cx++) { const c = cy * CH + cx; C.cSeq[c] = s; if (w) C.wSeq[c] = s; }
  if (flags & F_GROUND) C.gSeq++;
  if (flags & F_DECO) C.dSeq++;
}
const touch = (S, x, y, flags) => touchBox(S, x - TOUCH_PAD, y - TOUCH_PAD, x + TOUCH_PAD, y + TOUCH_PAD, flags || 0);
// Highest stamp in the chunks overlapping a tile box: a cached value built when this was s is still good while it still equals s.
function stampOver(arr, x0, y0, x1, y1) {
  const cx0 = Math.max(0, x0) >> CHUNK_SHIFT, cx1 = Math.min(N - 1, x1) >> CHUNK_SHIFT, cy0 = Math.max(0, y0) >> CHUNK_SHIFT, cy1 = Math.min(N - 1, y1) >> CHUNK_SHIFT;
  let m = 0; for (let cy = cy0; cy <= cy1; cy++) for (let cx = cx0; cx <= cx1; cx++) { const v = arr[cy * CH + cx]; if (v > m) m = v; }
  return m;
}
// Render stamps: the renderer compares these instead of scanning the whole map every frame.
// The world's id is part of the stamp, so a freshly loaded or new world never matches the old one's stamp.
const groundStamp = S => { const C = cacheOf(S); return C.id + ':' + C.gSeq; }, decoStamp = S => { const C = cacheOf(S); return C.id + ':' + C.dSeq; };

// ---- Building lists (all in S.blds order, roads excluded) and a chunk index of houses ----
function buildLists(S, C) {
  const L = { by: {}, jobs: [], trade: {}, crews: [] };
  C.hb = []; for (let i = 0; i < CH * CH; i++) C.hb.push([]);
  for (const b of S.blds) if (b.t !== 'road') listAdd(C, L, b);
  C.L = L;
}
function listAdd(C, L, b) {
  (L.by[b.t] || (L.by[b.t] = [])).push(b);
  if (JOBS[b.t]) { L.jobs.push(b); (L.trade[TRADE_OF[b.t]] || (L.trade[TRADE_OF[b.t]] = [])).push(b); }
  if (CREW_KIND[b.t]) L.crews.push(b);
  if (b.t === 'house') C.hb[(b.y >> CHUNK_SHIFT) * CH + (b.x >> CHUNK_SHIFT)].push(b);
  C.listSeq++;
}
function listDel(C, L, b) {
  const del = arr => { const i = arr ? arr.indexOf(b) : -1; if (i >= 0) arr.splice(i, 1); };
  del(L.by[b.t]); if (JOBS[b.t]) { del(L.jobs); del(L.trade[TRADE_OF[b.t]]); } if (CREW_KIND[b.t]) del(L.crews);
  if (b.t === 'house') del(C.hb[(b.y >> CHUNK_SHIFT) * CH + (b.x >> CHUNK_SHIFT)]);
  C.listSeq++;
}
const listOf = (C, t) => C.L.by[t] || EMPTY;
const EMPTY = [];
// Houses whose centre is within r (Chebyshev) of the point (cx, cy): bucket lookup, then the exact test.
function housesNear(C, cx, cy, r, fn) {
  const x0 = Math.max(0, Math.floor(cx - r - 1)) >> CHUNK_SHIFT, x1 = Math.min(N - 1, Math.ceil(cx + r + 1)) >> CHUNK_SHIFT;
  const y0 = Math.max(0, Math.floor(cy - r - 1)) >> CHUNK_SHIFT, y1 = Math.min(N - 1, Math.ceil(cy + r + 1)) >> CHUNK_SHIFT;
  for (let j = y0; j <= y1; j++) for (let i = x0; i <= x1; i++) for (const h of C.hb[j * CH + i]) { const c = ctr(h); if (Math.abs(h.x + c - cx) <= r && Math.abs(h.y + c - cy) <= r) fn(h); }
}

// ---- Events: sim.js calls these when the world changes, so recalc only redoes what is affected ----
function onAdd(S, b) {
  const C = cacheOf(S), T = tilesOf(b);
  if (b.t === 'road') { for (const [x, y] of T) touch(S, x, y, F_WALK | F_GROUND); }
  else { const x0 = b.x, y0 = b.y, e = b.sz || 1; touchBox(S, x0 - 6, y0 - 6, x0 + e + 6, y0 + e + 6, F_WALK | F_GROUND | F_DECO); }   // the footprint is levelled and the slope around it eased (up to 5 tiles)
  if (!C.ready) return;
  C.topoSeq++;
  if (b.t === 'road') { C.roadOps.push(b.y * N + b.x); return; }
  if (b.t === 'hall') { C.ready = false; return; }   // the Town Hall is placed once, at creation
  listAdd(C, C.L, b); C.pendServe.add(b); C.totalsDirty = C.workDirty = true;
  if (b.t === 'house') C.pendHouse.add(b);
  else if (b.t === 'well') markHouses(C, b, WELL_R);
  else if (b.t === 'market') markHouses(C, b, marketR(b));
}
function onRemove(S, b) {
  const C = cacheOf(S);
  for (const [x, y] of tilesOf(b)) touch(S, x, y, F_WALK | F_GROUND);
  if (!C.ready) return;
  C.topoSeq++;
  if (b.t === 'road') { C.roadOps.push(b.y * N + b.x); return; }
  C.pendServe.delete(b); C.pendHouse.delete(b); C.totalsDirty = C.workDirty = true;
  if (b.t === 'well') markHouses(C, b, WELL_R); else if (b.t === 'market') markHouses(C, b, marketR(b));
  listDel(C, C.L, b);
  C.reach.delete(b);
}
function markHouses(C, b, r) { housesNear(C, b.x + ctr(b), b.y + ctr(b), r + 1, h => C.pendHouse.add(h)); }
