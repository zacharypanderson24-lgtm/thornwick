// ---------- Road connectivity: which road tiles are linked to the Town Hall ----------
// conn[tile] = 1 for a road tile that can reach the Town Hall along roads. It is kept up to date incrementally:
//  * a new road tile is added by flooding outwards only through road tiles that are not yet connected (so the cost is the size of what it joins);
//  * a removed road tile is checked by a best-first search from each neighbour towards the Town Hall: usually it finds another way round after a few
//    steps; if it can't, it has walked the whole cut-off network, which is exactly the set that has to be switched off (bridges and chokepoints included).
// A full flood fill (rebuildConn) only runs when the world is loaded or after invalidateCaches.
const rdSeen = new Int32Array(N * N), rdOk = new Int32Array(N * N), rdVis = new Int32Array(N * N), rdStack = new Int32Array(N * N);
const hpK = new Int32Array(N * N), hpV = new Int32Array(N * N);
let rdStamp = 0, rdOkStamp = 0;
const roadAt = (S, x, y) => { if (x < 0 || y < 0 || x >= N || y >= N) return false; const g = S.grid[y][x]; return !!g && g.t === 'road'; };

function computeRoots(S, C) {   // road tiles touching any edge of the multi-tile Town Hall are connected by definition
  const root = new Uint8Array(N * N), h = S.hall;
  for (const [hx, hy] of tilesOf(h)) for (const [dx, dy] of NB) { const x = hx + dx, y = hy + dy; if (x >= 0 && y >= 0 && x < N && y < N && S.grid[y][x] !== h) root[y * N + x] = 1; }
  C.root = root; C.hcx = h.x + 1; C.hcy = h.y + 1;
}
function rebuildConn(S, C) {
  const conn = C.conn || (C.conn = new Uint8Array(N * N)), root = C.root; conn.fill(0);
  let sp = 0;
  for (let i = 0; i < N * N; i++) if (root[i] && roadAt(S, i % N, (i / N) | 0)) { conn[i] = 1; rdStack[sp++] = i; }
  while (sp) {
    const c = rdStack[--sp], cx = c % N, cy = (c - cx) / N;
    for (let k = 0; k < 4; k++) { const nx = cx + NB[k][0], ny = cy + NB[k][1]; if (roadAt(S, nx, ny)) { const ni = ny * N + nx; if (!conn[ni]) { conn[ni] = 1; rdStack[sp++] = ni; } } }
  }
}
// Flood from tile t through road tiles that are not connected yet, switching them on.
function floodOn(S, C, t, changed) {
  const conn = C.conn; let sp = 0; conn[t] = 1; changed.push(t); rdStack[sp++] = t;
  while (sp) {
    const c = rdStack[--sp], cx = c % N, cy = (c - cx) / N;
    for (let k = 0; k < 4; k++) { const nx = cx + NB[k][0], ny = cy + NB[k][1]; if (roadAt(S, nx, ny)) { const ni = ny * N + nx; if (!conn[ni]) { conn[ni] = 1; changed.push(ni); rdStack[sp++] = ni; } } }
  }
}
// Best-first search (nearest the Town Hall first) over the road network from tile s. Success = it reaches a Town Hall edge (or tiles already proven connected in this pass).
// Failure = the whole network around s was explored without finding one: it is cut off, so every tile seen is switched off.
function verifyFrom(S, C, s, changed) {
  const root = C.root, conn = C.conn, stamp = ++rdStamp, hcx = C.hcx, hcy = C.hcy;
  let hn = 0, nv = 0, ok = false;
  const push = (i) => {
    const key = Math.abs((i % N) - hcx) + Math.abs(((i / N) | 0) - hcy); let p = hn++;
    while (p > 0) { const q = (p - 1) >> 1; if (hpK[q] <= key) break; hpK[p] = hpK[q]; hpV[p] = hpV[q]; p = q; }
    hpK[p] = key; hpV[p] = i;
  };
  rdSeen[s] = stamp; push(s);
  while (hn) {
    const c = hpV[0]; const lk = hpK[--hn], lv = hpV[hn];   // pop the smallest, sift the last element down
    if (hn) { let p = 0; for (;;) { let q = 2 * p + 1; if (q >= hn) break; if (q + 1 < hn && hpK[q + 1] < hpK[q]) q++; if (hpK[q] >= lk) break; hpK[p] = hpK[q]; hpV[p] = hpV[q]; p = q; } hpK[p] = lk; hpV[p] = lv; }
    rdVis[nv++] = c;
    if (root[c] || rdOk[c] === rdOkStamp) { ok = true; break; }
    const cx = c % N, cy = (c - cx) / N;
    for (let k = 0; k < 4; k++) { const nx = cx + NB[k][0], ny = cy + NB[k][1]; if (roadAt(S, nx, ny)) { const ni = ny * N + nx; if (rdSeen[ni] !== stamp) { rdSeen[ni] = stamp; push(ni); } } }
  }
  if (ok) { for (let i = 0; i < nv; i++) rdOk[rdVis[i]] = rdOkStamp; }
  else { for (let i = 0; i < nv; i++) { const c = rdVis[i]; if (conn[c]) { conn[c] = 0; changed.push(c); } } }
}
// Bring conn up to date with the road tiles added or removed since the last call (C.roadOps). Returns the tile indexes whose neighbours need re-evaluating.
function applyRoadOps(S, C) {
  const dirty = Array.from(new Set(C.roadOps)); C.roadOps = [];
  const conn = C.conn, changed = [], root = C.root;
  for (const t of dirty) if (!roadAt(S, t % N, (t / N) | 0) && conn[t]) { conn[t] = 0; changed.push(t); }   // a removed tile is no longer connected
  const seeds = [];
  for (const t of dirty) {
    const x = t % N, y = (t / N) | 0; if (roadAt(S, x, y) && conn[t]) seeds.push(t);
    for (let k = 0; k < 4; k++) { const nx = x + NB[k][0], ny = y + NB[k][1]; if (roadAt(S, nx, ny) && conn[ny * N + nx]) seeds.push(ny * N + nx); }
  }
  rdOkStamp++;
  for (const s of seeds) if (conn[s] && rdOk[s] !== rdOkStamp) verifyFrom(S, C, s, changed);   // did losing a neighbour cut it off from the Town Hall?
  for (const t of dirty) {   // a new (or re-laid) tile joins the network if it touches the Town Hall or a connected road
    const x = t % N, y = (t / N) | 0; if (conn[t] || !roadAt(S, x, y)) continue;
    let join = root[t] === 1;
    for (let k = 0; k < 4 && !join; k++) { const nx = x + NB[k][0], ny = y + NB[k][1]; if (roadAt(S, nx, ny) && conn[ny * N + nx]) join = true; }
    if (join) floodOn(S, C, t, changed);
  }
  return dirty.concat(changed);
}
