const $ = id => document.getElementById(id);
// ---------- Sprites (drawn in code, crisp pixels) ----------
const NBD = [[1, 0, 1], [-1, 0, 2], [0, 1, 4], [0, -1, 8]];
const SPR = { grass: [], road: [], tree: [], rock: [] };
let ZM_FILL = null;
const ZM = { res: [], farm: [], comm: [], wood: [], mine: [] };
const ZSPR = (k, m) => ZM[k][m === undefined ? 15 : m];
const CUR = { k: 1, W: 32, H: 48 };
const mkc = k => { k = k || 1; CUR.k = k; CUR.W = 32 * k; CUR.H = k === 1 ? 48 : 88; const c = document.createElement('canvas'); c.width = CUR.W; c.height = CUR.H; return [c, c.getContext('2d')]; };
const P = (u, v, z) => [16 * CUR.k + (u - v) * 16 * CUR.k, CUR.H - 16 * CUR.k + (u + v) * 8 * CUR.k - (z || 0)];
function hexRgb(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
function mix(a, b, t) { const A = hexRgb(a), B = hexRgb(b); return '#' + A.map((v, i) => Math.round(v + (B[i] - v) * t).toString(16).padStart(2, '0')).join(''); }
const tones = c => [mix(c, '#ffffff', 0.22), c, mix(c, '#000000', 0.3)];
function poly(g, pts, col) {
  g.fillStyle = col;
  const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
  const x0 = Math.max(0, Math.floor(Math.min(...xs))), x1 = Math.min(CUR.W - 1, Math.ceil(Math.max(...xs)));
  const y0 = Math.max(0, Math.floor(Math.min(...ys))), y1 = Math.min(CUR.H - 1, Math.ceil(Math.max(...ys)));
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
    const cx = x + 0.5, cy = y + 0.5; let pos = 0, neg = 0;
    for (let i = 0; i < pts.length; i++) {
      const a = pts[i], b = pts[(i + 1) % pts.length];
      const cr = (b[0] - a[0]) * (cy - a[1]) - (b[1] - a[1]) * (cx - a[0]);
      if (cr > 1e-9) pos++; else if (cr < -1e-9) neg++;
    }
    if (!(pos && neg)) g.fillRect(x, y, 1, 1);
  }
}
function box(g, u0, v0, u1, v1, z0, z1, cT, cL, cR) {
  poly(g, [P(u0, v1, z0), P(u1, v1, z0), P(u1, v1, z1), P(u0, v1, z1)], cL);
  poly(g, [P(u1, v0, z0), P(u1, v1, z0), P(u1, v1, z1), P(u1, v0, z1)], cR);
  poly(g, [P(u0, v0, z1), P(u1, v0, z1), P(u1, v1, z1), P(u0, v1, z1)], cT);
}
function pyramid(g, u0, v0, u1, v1, zb, za, cL, cR, cB) {
  const a = P((u0 + u1) / 2, (v0 + v1) / 2, za);
  poly(g, [P(u0, v0, zb), P(u1, v0, zb), a], cB);
  poly(g, [P(u0, v0, zb), P(u0, v1, zb), a], cB);
  poly(g, [P(u0, v1, zb), P(u1, v1, zb), a], cL);
  poly(g, [P(u1, v0, zb), P(u1, v1, zb), a], cR);
}
function winL(g, u0, u1, z0, z1, v, col) { poly(g, [P(u0, v, z0), P(u1, v, z0), P(u1, v, z1), P(u0, v, z1)], col); }
function winR(g, v0, v1, z0, z1, u, col) { poly(g, [P(u, v0, z0), P(u, v1, z0), P(u, v1, z1), P(u, v0, z1)], col); }
function groundFill(g, fn) {
  const k = CUR.k, y0 = CUR.H - 16 * k;
  for (let Y = y0; Y < CUR.H; Y++) for (let X = 0; X < CUR.W; X++) {
    const s = (Y - y0 + 0.5) / (8 * k), d = (X - 16 * k + 0.5) / (16 * k), u = (s + d) / 2, v = (s - d) / 2;
    if (u < 0 || u > 1 || v < 0 || v > 1) continue;
    const c = fn(u, v, X, Y);
    if (c) { g.fillStyle = c; g.fillRect(X, Y, 1, 1); }
  }
}
function blob(g, cx, cy, r, col) {
  g.fillStyle = col;
  for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r + 1) g.fillRect(cx + dx, cy + dy, 1, 1);
}
function shadow(g, cx, cy, rx, ry) {
  g.fillStyle = 'rgba(10,30,10,.25)';
  for (let dy = -ry; dy <= ry; dy++) for (let dx = -rx; dx <= rx; dx++) if ((dx / rx) ** 2 + (dy / ry) ** 2 <= 1) g.fillRect(cx + dx, cy + dy, 1, 1);
}
function buildSprites() {
  for (let k = 0; k < 4; k++) {
    const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => {
      const h = hash2(X, Y, 11 + k);
      if (h > 0.985) return '#ecebc0';
      if (h > 0.93) return '#79b65b';
      if (h < 0.14) return '#4f8c3b';
      return ((X >> 2) + (Y >> 1) + k) % 7 === 0 ? '#5a9843' : '#5f9e48';
    });
    SPR.grass.push(c);
  }
  for (let m = 0; m < 16; m++) {
    const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => {
      const a = (u > .27 && u < .73 && v > .27 && v < .73) || ((m & 1) && u >= .73 && v > .27 && v < .73) || ((m & 2) && u <= .27 && v > .27 && v < .73) ||
        ((m & 4) && v >= .73 && u > .27 && u < .73) || ((m & 8) && v <= .27 && u > .27 && u < .73);
      if (!a) return null;
      const h = hash2(X, Y, 21);
      if (h < 0.16) return '#9c7f4c'; if (h > 0.92) return '#d2b886'; return '#b99a60';
    });
    SPR.road.push(c);
  }
  for (let k = 0; k < 3; k++) {
    const [c, g] = mkc();
    shadow(g, 16, 41, 7, 2);
    g.fillStyle = '#5a3b22'; g.fillRect(15, 33, 2, 9);
    if (k < 2) {
      blob(g, 16, 27, 7 - k, '#2c6b33'); blob(g, 12 + k, 29, 5, '#2a6230'); blob(g, 20 - k, 30, 4, '#2a6230');
      blob(g, 14, 24 - k, 4, '#4b9a47'); blob(g, 13, 23 - k, 2, '#6bbd5c');
    } else {
      poly(g, [[16, 14], [22, 28], [10, 28]], '#2a6a3c'); poly(g, [[16, 20], [24, 35], [8, 35]], '#235a33'); poly(g, [[16, 14], [19, 21], [13, 21]], '#4b9a5a');
    }
    SPR.tree.push(c);
  }
  for (let k = 0; k < 2; k++) {
    const [c, g] = mkc();
    shadow(g, 16, 41, 8, 2);
    blob(g, 14 + k * 2, 37, 4, '#8a8d98'); blob(g, 19 - k * 2, 38, 3, '#767985'); blob(g, 13 + k * 2, 35, 2, '#b4b8c4'); blob(g, 20, 36, 2, '#9ea2ae');
    SPR.rock.push(c);
  }
  function house(lvl) {
    const [c, g] = mkc();
    const [wt, wl, wr] = tones(lvl === 1 ? '#cdb88a' : '#d4cfc2');
    const h = lvl === 1 ? 9 : 12, ap = lvl === 1 ? 22 : 27;
    box(g, .18, .22, .82, .78, 0, h, wt, wl, wr);
    winL(g, .38, .52, 0, 6, .78, '#4a3322'); winL(g, .62, .74, 4, 8, .78, '#5b7fa6'); winR(g, .4, .6, 4, 8, .82, '#4d6d92');
    const [rt, rl, rr] = tones(lvl === 1 ? '#c9a24a' : '#b4503c');
    pyramid(g, .12, .16, .88, .84, h, ap, rl, rr, rt);
    if (lvl === 2) box(g, .6, .3, .72, .42, h + 3, h + 11, '#9a9aa4', '#7c7c88', '#5d5d68');
    return c;
  }
  SPR.house1 = house(1); SPR.house2 = house(2);
  { const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => {
      if (u < .05 || u > .95 || v < .05 || v > .95) return '#7a5a2d';
      const h = hash2(X, Y, 31);
      if (h > .9) return '#6fae4a';
      return Math.floor(v * 7) % 2 ? '#d9b94a' : '#b99a35';
    });
    const [bt, bl, br] = tones('#a3412f');
    box(g, .62, .08, .92, .38, 0, 7, bt, bl, br);
    const [rt, rl, rr] = tones('#6b4a2d'); pyramid(g, .58, .04, .96, .42, 7, 13, rl, rr, rt);
    SPR.farm = c; }
  { const [c, g] = mkc();
    const [wt, wl, wr] = tones('#8a5a34');
    box(g, .12, .22, .58, .78, 0, 8, wt, wl, wr);
    winL(g, .26, .4, 0, 5, .78, '#33220f');
    const [rt, rl, rr] = tones('#4f6b3a'); pyramid(g, .08, .18, .62, .82, 8, 15, rl, rr, rt);
    for (let i = 0; i < 3; i++) { const [lt, ll, lr] = tones('#b07a44'); box(g, .66, .2 + i * .22, .92, .36 + i * .22, 0, 4, lt, ll, lr); }
    box(g, .7, .3, .88, .56, 4, 7, '#c08a52', '#9a6b3a', '#7e552e');
    SPR.wood = c; }
  { const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => {
      if (u < .08 || u > .92 || v < .08 || v > .92) return null;
      const du = Math.abs(u - .5), dv = Math.abs(v - .5);
      if (du + dv < .26) return '#4d4f5a';
      return hash2(X, Y, 41) > .85 ? '#8c8f9a' : '#9fa2ad';
    });
    box(g, .12, .14, .32, .34, 0, 5, '#cfd2dc', '#a1a5b3', '#7c8090');
    box(g, .66, .62, .88, .86, 0, 6, '#cfd2dc', '#a1a5b3', '#7c8090');
    box(g, .68, .14, .84, .3, 0, 3, '#b9bcc8', '#8d91a0', '#6a6e7c');
    SPR.quarry = c; }
  { const [c, g] = mkc();
    box(g, .3, .3, .7, .7, 0, 6, '#9aa0ad', '#7a808d', '#5d6270');
    poly(g, [P(.38, .38, 6), P(.62, .38, 6), P(.62, .62, 6), P(.38, .62, 6)], '#3f78b0');
    box(g, .28, .28, .34, .34, 6, 15, '#8a6a40', '#6f5230', '#55401f');
    box(g, .66, .66, .72, .72, 6, 15, '#8a6a40', '#6f5230', '#55401f');
    const [rt, rl, rr] = tones('#7a4a2a'); pyramid(g, .22, .22, .78, .78, 15, 21, rl, rr, rt);
    SPR.well = c; }
  { const [c, g] = mkc();
    const [wt, wl, wr] = tones('#8d93a5');
    box(g, .1, .14, .9, .86, 0, 11, wt, wl, wr);
    for (const [a, b] of [[.1, .14], [.78, .14], [.1, .74], [.78, .74]]) box(g, a, b, a + .12, b + .12, 11, 15, '#aeb4c4', '#8d93a5', '#6c7284');
    box(g, .3, .3, .7, .7, 11, 13, '#6b4a2d', '#563a22', '#41291a');
    winL(g, .4, .6, 0, 8, .86, '#2a2230'); winL(g, .44, .56, 8, 9.5, .86, '#2a2230');
    poly(g, [P(.16, .86, 10), P(.3, .86, 10), P(.3, .86, 3), P(.23, .86, 5), P(.16, .86, 3)], '#b3322b');
    poly(g, [P(.9, .3, 9), P(.9, .5, 9), P(.9, .5, 4), P(.9, .4, 5.5), P(.9, .3, 4)], '#b3322b');
    box(g, .46, .46, .5, .5, 13, 24, '#8a6a40', '#6f5230', '#55401f');
    poly(g, [P(.48, .48, 24), P(.48, .48, 19), [24, 19.5], [24, 23]], '#d94b3d');
    SPR.barracks = c; }
  { const [c, g] = mkc();
    const [wt, wl, wr] = tones('#b49462');
    box(g, .08, .12, .92, .88, 0, 10, wt, wl, wr);
    box(g, .08, .12, .92, .88, 6, 7, '#7a5a38', '#6a4c2d', '#573d24');
    winL(g, .42, .58, 0, 7, .88, '#3a2414'); winL(g, .18, .3, 3, 7, .88, '#5b7fa6'); winL(g, .7, .82, 3, 7, .88, '#5b7fa6'); winR(g, .4, .6, 3, 7, .92, '#5b7fa6');
    const [rt, rl, rr] = tones('#7b4a2a'); pyramid(g, .02, .06, .98, .94, 10, 24, rl, rr, rt);
    box(g, .62, .22, .76, .36, 14, 24, '#9a9aa4', '#7c7c88', '#5d5d68');
    poly(g, [[15, 4], [16, 4], [16, 14], [15, 14]], '#5a3b22'); poly(g, [[16, 4], [23, 6], [16, 9]], '#e0a93a');
    SPR.hall = c; }
  function hl(fill, edge) {
    const [c, g] = mkc();
    groundFill(g, (u, v) => (u < .1 || u > .9 || v < .1 || v > .9) ? edge : fill);
    return c;
  }
  function hlz(fill, edge) {
    const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => (u < .08 || u > .92 || v < .08 || v > .92) ? (((X + Y) & 1) ? edge : fill) : fill);
    return c;
  }
  SPR.zoneRes = hlz('rgba(235,140,60,.30)', 'rgba(250,175,95,.95)');
  SPR.zoneFarm = hlz('rgba(235,215,70,.28)', 'rgba(245,225,90,.95)');
  SPR.zoneComm = hlz('rgba(70,170,240,.30)', 'rgba(120,200,255,.95)');
  SPR.zoneWood = hlz('rgba(30,120,60,.38)', 'rgba(70,200,110,.95)');
  SPR.zoneMine = hlz('rgba(160,110,220,.34)', 'rgba(200,160,250,.95)');
  SPR.lens = {
    wood: hl('rgba(40,210,100,.42)', 'rgba(110,255,150,.95)'), stone: hl('rgba(215,220,240,.42)', 'rgba(250,250,255,.95)'),
    ore: hl('rgba(255,140,40,.46)', 'rgba(255,190,90,.95)'), fert: hl('rgba(205,235,60,.34)', 'rgba(235,245,110,.9)')
  };
  SPR.hlGood = hl('rgba(120,230,120,.28)', 'rgba(150,255,150,.95)');
  SPR.hlBad = hl('rgba(240,90,80,.3)', 'rgba(255,120,110,.95)');
  SPR.hlNone = hl('rgba(255,255,255,0)', 'rgba(255,255,255,.9)');
  { const [c, g] = mkc();
    g.fillStyle = '#d65a4a'; g.fillRect(12, 8, 8, 9); g.fillRect(13, 7, 6, 11);
    g.fillStyle = '#fff'; g.fillRect(15, 9, 2, 5); g.fillRect(15, 15, 2, 2);
    SPR.warn = c; }
  ZM_FILL = { res: ['rgba(235,140,60,.22)', 'rgba(250,175,95,.95)'], farm: ['rgba(235,215,70,.2)', 'rgba(245,225,90,.95)'], comm: ['rgba(70,170,240,.22)', 'rgba(120,200,255,.95)'], wood: ['rgba(30,120,60,.3)', 'rgba(70,200,110,.95)'], mine: ['rgba(160,110,220,.26)', 'rgba(200,160,250,.95)'] };
  for (const kd in ZM_FILL) for (let m = 0; m < 16; m++) {
    const [c, g] = mkc(); const [fill, edge] = ZM_FILL[kd];
    groundFill(g, (u, v) => (((m & 1) && u > .9) || ((m & 2) && u < .1) || ((m & 4) && v > .9) || ((m & 8) && v < .1)) ? edge : fill);
    ZM[kd].push(c);
  }
  function manor(lvl) {
    const [c, g] = mkc(2);
    const [wt, wl, wr] = tones(lvl === 1 ? '#cdb88a' : '#d4cfc2');
    const h = lvl === 1 ? 14 : 18, ap = lvl === 1 ? 36 : 42;
    box(g, .1, .14, .9, .86, 0, h, wt, wl, wr);
    winL(g, .43, .57, 0, 10, .86, '#4a3322');
    winL(g, .17, .31, 6, 12, .86, '#5b7fa6'); winL(g, .69, .83, 6, 12, .86, '#5b7fa6');
    winR(g, .22, .36, 6, 12, .9, '#4d6d92'); winR(g, .64, .78, 6, 12, .9, '#4d6d92');
    const [rt, rl, rr] = tones(lvl === 1 ? '#c9a24a' : '#b4503c');
    pyramid(g, .03, .07, .97, .93, h, ap, rl, rr, rt);
    box(g, .6, .24, .72, .36, h + 5, h + 17, '#9a9aa4', '#7c7c88', '#5d5d68');
    if (lvl === 2) { box(g, .16, .2, .3, .34, h + 3, h + 11, '#9a9aa4', '#7c7c88', '#5d5d68'); }
    return c;
  }
  SPR.manor = [manor(1), manor(2)];
  { const [c, g] = mkc(2);
    groundFill(g, (u, v, X, Y) => {
      if (u < .03 || u > .97 || v < .03 || v > .97) return '#7a5a2d';
      const h = hash2(X, Y, 71);
      if (h > .92) return '#6fae4a';
      return Math.floor(v * 14) % 2 ? '#d9b94a' : '#b99a35';
    });
    const [bt, bl, br] = tones('#a3412f');
    box(g, .6, .06, .94, .34, 0, 16, bt, bl, br);
    const [rt, rl, rr] = tones('#6b4a2d'); pyramid(g, .56, .02, .98, .38, 16, 30, rl, rr, rt);
    box(g, .08, .08, .2, .2, 0, 26, '#c9ccd6', '#9a9eac', '#767a89');
    SPR.farm2 = c; }
  { const [c, g] = mkc(2);
    groundFill(g, (u, v, X, Y) => { const h = hash2(X, Y, 72); return h > .85 ? '#8f8672' : h < .2 ? '#b6ad97' : '#a89f8a'; });
    const stalls = [[.1, .12, '#d94b3d'], [.56, .12, '#3d7fd9'], [.1, .58, '#4fae4a'], [.56, .58, '#e0a93a']];
    for (const [su, sv, col] of stalls) {
      box(g, su, sv, su + .3, sv + .3, 0, 6, '#9a6a3e', '#7a5430', '#5f4325');
      for (const [pu, pv] of [[su, sv], [su + .26, sv], [su, sv + .26], [su + .26, sv + .26]]) box(g, pu, pv, pu + .04, pv + .04, 6, 14, '#7a5530', '#5f4325', '#4a331b');
      pyramid(g, su - .03, sv - .03, su + .33, sv + .33, 14, 20, col, '#f0e6d2', '#e8dfc8');
    }
    box(g, .47, .47, .53, .53, 0, 26, '#8a6a40', '#6f5230', '#55401f');
    poly(g, [P(.5, .5, 26), P(.5, .5, 21), [48, 62], [48, 66]], '#d94b3d');
    SPR.market2 = c; }
  SPR.grassF = [];
  for (let k = 0; k < 2; k++) {
    const [c, g] = mkc();
    groundFill(g, (u, v, X, Y) => {
      const h = hash2(X, Y, 61 + k);
      if (h > 0.95) return '#7a6a3c'; if (h > 0.84) return '#8fb04a'; if (h < 0.12) return '#5f9c3c';
      return ((X >> 1) + Y + k) % 5 === 0 ? '#6aa845' : '#72ad4a';
    });
    SPR.grassF.push(c);
  }
  SPR.ore = [];
  for (let k = 0; k < 2; k++) {
    const [c, g] = mkc();
    shadow(g, 16, 41, 8, 2);
    blob(g, 14 + k * 2, 37, 4, '#4a4d5a'); blob(g, 19 - k * 2, 38, 3, '#3b3e4a'); blob(g, 13 + k * 2, 35, 2, '#5d6170'); blob(g, 20, 36, 2, '#4a4d5a');
    g.fillStyle = '#e0873a'; for (const [x, y] of [[13 + k, 36], [17, 37], [19, 35], [15, 39], [21 - k, 38]]) g.fillRect(x, y, 2, 1);
    g.fillStyle = '#8fb8f0'; for (const [x, y] of [[16, 35], [12 + k * 3, 38], [20, 37]]) g.fillRect(x, y, 1, 1);
    SPR.ore.push(c);
  }
  { const [c, g] = mkc();
    const [bt, bl, br] = tones('#9a6a3e');
    box(g, .14, .26, .86, .74, 0, 5, bt, bl, br);
    poly(g, [P(.2, .74, 5), P(.8, .74, 5), P(.8, .74, 3), P(.2, .74, 3)], '#e8dfc8');
    for (const [u0, col] of [[.22, '#d94b3d'], [.4, '#6fae4a'], [.58, '#e0a93a'], [.7, '#d94b3d']]) poly(g, [P(u0, .5, 5), P(u0 + .1, .5, 5), P(u0 + .1, .55, 7), P(u0, .55, 7)], col);
    for (const [a, b] of [[.12, .22], [.82, .22], [.12, .7], [.82, .7]]) box(g, a, b, a + .05, b + .05, 5, 13, '#7a5530', '#5f4325', '#4a331b');
    pyramid(g, .06, .16, .94, .84, 13, 19, '#d94b3d', '#f0e6d2', '#e8dfc8');
    SPR.market = c; }
  { const [c, g] = mkc();
    const [mt, ml, mr] = tones('#6f7486');
    box(g, .1, .12, .9, .88, 0, 8, mt, ml, mr);
    box(g, .22, .24, .78, .76, 8, 12, '#858a9c', '#666b7c', '#4d5262');
    poly(g, [P(.34, .88, 0), P(.66, .88, 0), P(.66, .88, 6), P(.5, .88, 8), P(.34, .88, 6)], '#16131c');
    poly(g, [P(.3, .88, 0), P(.34, .88, 0), P(.34, .88, 7), P(.3, .88, 7)], '#8a5a34'); poly(g, [P(.66, .88, 0), P(.7, .88, 0), P(.7, .88, 7), P(.66, .88, 7)], '#8a5a34');
    g.fillStyle = '#e0873a'; for (const [x, y] of [[11, 33], [19, 31], [15, 29], [23, 34]]) g.fillRect(x, y, 2, 1);
    box(g, .7, .5, .88, .74, 8, 11, '#a0703c', '#8a5a34', '#6e4526');
    SPR.mine = c; }
}
buildSprites();
function spriteFor(t, b, mask, rot) {
  if (t === 'road') return SPR.road[mask || 0];
  const n = slotName(t, b), r = rot === undefined ? ROT : rot;
  // Choose one of eight stable full-tile huts by world coordinates.
  if (n === 'house_1' && b && Number.isFinite(b.x) && Number.isFinite(b.y)) {
    const variant = Math.min(8, 1 + Math.floor(hash2(b.x, b.y, 173) * 8));
    if (variant > 1) {
      const alternate = ovrSprite('house_1_v' + variant, r);
      if (alternate) return alternate;
    }
  }
  const o = ovrSprite(n, r);
  if (o) return o;
  return defaultSprite(n);
}
// ---------- View rotation ----------
let ROT = 0;
function toView(x, y, r) { r = r === undefined ? ROT : r; const m = N - 1; return r === 0 ? [x, y] : r === 1 ? [m - y, x] : r === 2 ? [m - x, m - y] : [y, m - x]; }
function fromView(vx, vy, r) { r = r === undefined ? ROT : r; const m = N - 1; return r === 0 ? [vx, vy] : r === 1 ? [vy, m - vx] : r === 2 ? [m - vx, m - vy] : [m - vy, vx]; }
function tpos(x, y) { const [vx, vy] = toView(x, y); return [(vx - vy) * 16, (vx + vy) * 8]; }
function nbW(x, y, i) { const [vx, vy] = toView(x, y); const d = NBD[i]; return fromView(vx + d[0], vy + d[1]); }
function slotName(t, b) {
  if (b && b.sz === 2) { if (t === 'house') return 'manor_' + (b.lvl || 1); if (t === 'farm') return 'farm2'; if (t === 'market') return 'market2'; }
  if (t === 'house') return 'house_' + ((b && b.lvl === 2) ? 2 : 1);
  return t;
}
function defaultSprite(name) {
  let m;
  if ((m = /^(grass|grassfert|tree|rock|ore)_(\d)$/.exec(name))) return ({ grass: SPR.grass, grassfert: SPR.grassF, tree: SPR.tree, rock: SPR.rock, ore: SPR.ore })[m[1]][+m[2]];
  if (name === 'house_1') return SPR.house1; if (name === 'house_2') return SPR.house2;
  if (name === 'manor_1') return SPR.manor[0]; if (name === 'manor_2') return SPR.manor[1];
  return SPR[name];
}
function setRot(r) {
  r = (r + 4) % 4; if (r === ROT) return;
  const c = screenToTile(stage.clientWidth / 2, stage.clientHeight / 2) || { x: 20, y: 20 };
  ROT = r; centerOn(c.x, c.y); updateInfo();
}

// ---------- UI state ----------
const cv = document.getElementById('c'), ctx = cv.getContext('2d'), stage = document.getElementById('stage');
let S = createWorld(7);
let lastTile = null, roadAnchor = null;
const ZONE = { zoneRes: 'res', zoneFarm: 'farm', zoneComm: 'comm', zoneWood: 'wood', zoneMine: 'mine' };
const RECT = t => !!ZONE[t];
let scale = 3, cam = { x: 0, y: 0 }, tool = 'inspect', hover = null, pinned = null, speed = 1, drag = null, spaceDown = false, lastKey = '';
const keys = {};
let dprI = 1, viewW = 0, viewH = 0;
const TOOLS = [
  { id: 'inspect', label: 'Look', key: '1' }, { id: 'road', label: 'Road', key: '2' }, { id: 'zoneRes', label: 'Housing', key: '3' },
  { id: 'zoneFarm', label: 'Farmland', key: '4' }, { id: 'zoneComm', label: 'Commerce', key: '5' }, { id: 'zoneWood', label: 'Lumber', key: '6' },
  { id: 'zoneMine', label: 'Mining', key: '7' }, { id: 'well', label: 'Well', key: '8' }, { id: 'barracks', label: 'Barracks', key: '9' },
  { id: 'bulldoze', label: 'Clear', key: '0' }
];
const toolBtns = {};
function iconFor(id) {
  const c = document.createElement('canvas'); c.width = 32; c.height = 40; const g = c.getContext('2d'); g.imageSmoothingEnabled = false;
  if (id === 'inspect') {
    g.fillStyle = '#e8e4d6'; const pts = ['0000001000', '0000011100', '0000111110', '0001111111', '0000011100', '0000011100', '0000001000'];
    for (let y = 0; y < 7; y++) for (let x = 0; x < 10; x++) if (pts[y][x] === '1') g.fillRect(11 + x - 2, 14 + y * 2 - 4, 2, 2);
    g.strokeStyle = 'transparent';
  } else if (id === 'bulldoze') {
    g.fillStyle = '#d65a4a'; for (let i = 0; i < 14; i++) { g.fillRect(9 + i, 12 + i, 3, 3); g.fillRect(9 + i, 25 - i, 3, 3); }
  } else if (id === 'road') {
    g.drawImage(SPR.grass[0], 0, -8); g.drawImage(SPR.road[15], 0, -8);
  } else if (ZONE[id]) {
    const k = ZONE[id];
    g.drawImage(SPR.grass[0], 0, -8); g.drawImage(ZSPR(k), 0, -8);
    g.drawImage({ res: SPR.house1, farm: SPR.farm, comm: SPR.market, wood: SPR.wood, mine: SPR.mine }[k], 0, -8);
  } else {
    g.drawImage(spriteFor(id, null, 0), 0, -8);
  }
  c.className = 'ico'; return c;
}
function refreshIcons() { for (const t of TOOLS) { const b = toolBtns[t.id]; if (b) b.replaceChild(iconFor(t.id), b.firstChild); } }
function costText(id) {
  const c = COST[id]; if (!c) return '';
  const sym = { wood: ['W', 'cw'], stone: ['S', 'cs'], gold: ['G', 'cg'] };
  return Object.keys(c).map(k => '<i class="' + sym[k][1] + '">' + sym[k][0] + c[k] + '</i>').join('');
}
const toolbar = document.getElementById('toolbar');
for (const t of TOOLS) {
  const b = document.createElement('button'); b.type = 'button'; b.className = 'tool'; b.id = 'tool-' + t.id; b.title = NAMES[t.id] || t.label;
  b.appendChild(iconFor(t.id));
  const l = document.createElement('span'); l.className = 'tl'; l.textContent = t.label; b.appendChild(l);
  const c = document.createElement('span'); c.className = 'tc'; c.innerHTML = costText(t.id) || (ZONE[t.id] ? '<i class="cg">free</i>' : '&nbsp;'); b.appendChild(c);
  const k = document.createElement('span'); k.className = 'tk'; k.textContent = t.key; b.appendChild(k);
  b.addEventListener('click', () => setTool(t.id));
  toolbar.appendChild(b); toolBtns[t.id] = b;
}
function setTool(id) {
  tool = id; roadAnchor = null; for (const k in toolBtns) toolBtns[k].classList.toggle('on', k === id);
  if (id !== 'inspect') pinned = null; updateInfo();
}
setTool('inspect');
let toastAt = 0, toastMsg = '';
function toast(msg) {
  const now = performance.now(); if (msg === toastMsg && now - toastAt < 1200) return;
  toastMsg = msg; toastAt = now;
  const d = document.createElement('div'); d.className = 'toast'; d.textContent = msg;
  const box = document.getElementById('toast'); box.appendChild(d);
  while (box.children.length > 3) box.removeChild(box.firstChild);
  setTimeout(() => d.remove(), 2600);
}

// ---------- Camera / input ----------
function resize() {
  dprI = Math.max(1, Math.round(window.devicePixelRatio || 1));
  cv.width = Math.max(1, stage.clientWidth * dprI); cv.height = Math.max(1, stage.clientHeight * dprI);
  viewW = stage.clientWidth / scale; viewH = stage.clientHeight / scale;
}
function centerOn(x, y) { const [px, py] = tpos(x, y); cam.x = px - viewW / 2; cam.y = py + 8 - viewH / 2; }
function screenToTile(mx, my) {
  const wx = mx / scale + Math.round(cam.x), wy = my / scale + Math.round(cam.y);
  const p = wx / 16, q = wy / 8;
  const vx = Math.floor((p + q) / 2), vy = Math.floor((q - p) / 2);
  if (!inb(vx, vy)) return null; const [x, y] = fromView(vx, vy); return { x, y };
}
function setScale(ns, mx, my) {
  ns = Math.max(2, Math.min(5, ns)); if (ns === scale) return;
  const wx = mx / scale + cam.x, wy = my / scale + cam.y;
  scale = ns; resize(); cam.x = wx - mx / scale; cam.y = wy - my / scale;
}
function mousePos(e) { const r = cv.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; }
function applyTool(t) {
  if (!t) return;
  if (tool === 'inspect') { pinned = t; updateInfo(); return; }
  if (RECT(tool)) return;
  const r = tool === 'bulldoze' ? remove(S, t.x, t.y) : place(S, tool, t.x, t.y);
  if (!r.ok) toast(r.reason); else updateHUD();
}
cv.addEventListener('contextmenu', e => e.preventDefault());
cv.addEventListener('pointerdown', e => {
  cv.setPointerCapture(e.pointerId);
  const [mx, my] = mousePos(e); hover = screenToTile(mx, my);
  if (e.button === 2 && roadAnchor) { roadAnchor = null; }
  const panning = e.button === 1 || e.button === 2 || tool === 'inspect' || spaceDown;
  drag = { sx: mx, sy: my, cx: cam.x, cy: cam.y, panning, moved: false, btn: e.button };
  lastTile = null;
  if (!panning && e.button === 0) {
    if (tool === 'road') { drag.road = true; drag.rs = hover; }
    else if (RECT(tool)) { if (hover) drag.rect = { a: hover, b: hover }; }
    else { lastKey = hover ? hover.x + ',' + hover.y : ''; paintPath(hover); }
  }
});
cv.addEventListener('pointermove', e => {
  const [mx, my] = mousePos(e); hover = screenToTile(mx, my);
  if (drag) {
    const dx = mx - drag.sx, dy = my - drag.sy;
    if (Math.abs(dx) + Math.abs(dy) > 5) drag.moved = true;
    if (drag.panning && drag.moved) { cam.x = drag.cx - dx / scale; cam.y = drag.cy - dy / scale; }
    else if (drag.rect && hover) {
      drag.rect.b = hover;
      const r = drag.rect, w = Math.abs(r.a.x - r.b.x) + 1, h = Math.abs(r.a.y - r.b.y) + 1, el = $('rectinfo');
      el.hidden = false; el.textContent = w + ' x ' + h + ' tiles'; el.style.left = (mx + 16) + 'px'; el.style.top = (my + 16) + 'px';
    }
    else if (!drag.panning && hover && !drag.road) paintPath(hover);
  }
  updateInfo();
});
function paintPath(t) {
  if (!t) return;
  if (!lastTile) { lastTile = t; applyTool(t); return; }
  let cx = lastTile.x, cy = lastTile.y;
  while (cx !== t.x || cy !== t.y) {
    const dx = t.x - cx, dy = t.y - cy;
    if (Math.abs(dx) >= Math.abs(dy)) cx += Math.sign(dx); else cy += Math.sign(dy);
    applyTool({ x: cx, y: cy });
  }
  lastTile = t;
}
function applyRect(r) {
  const kind = ZONE[tool];
  let ok = 0, why = '';
  for (let y = Math.min(r.a.y, r.b.y); y <= Math.max(r.a.y, r.b.y); y++)
    for (let x = Math.min(r.a.x, r.b.x); x <= Math.max(r.a.x, r.b.x); x++) {
      const res = setZone(S, kind, x, y); if (res.ok) ok++; else if (!why) why = res.reason;
    }
  if (!ok && why) toast(why); updateHUD();
}
cv.addEventListener('pointerup', () => {
  if (drag && drag.rect) { applyRect(drag.rect); $('rectinfo').hidden = true; }
  if (drag && drag.road && drag.btn === 0 && !drag.panning) {
    if (drag.moved && drag.rs && hover) { commitRoad(drag.rs, hover); roadAnchor = null; }
    else if (hover) { if (!roadAnchor) roadAnchor = hover; else { commitRoad(roadAnchor, hover); roadAnchor = null; } }
  }
  if (drag && drag.panning && !drag.moved && drag.btn === 0 && tool === 'inspect') { pinned = hover; updateInfo(); }
  drag = null;
});
cv.addEventListener('pointerleave', () => { if (!drag) { hover = null; updateInfo(); } });
cv.addEventListener('wheel', e => { e.preventDefault(); const [mx, my] = mousePos(e); setScale(scale + (e.deltaY < 0 ? 1 : -1), mx, my); }, { passive: false });
window.addEventListener('keydown', e => {
  if (e.target && /INPUT|TEXTAREA/.test(e.target.tagName)) return;
  const k = e.key.toLowerCase();
  if (k === ' ') { e.preventDefault(); spaceDown = true; return; }
  if (k === 'p') { setSpeed(speed === 0 ? 1 : 0); return; }
  if (k === 'v') { setLens(!lensOn); return; }
  if (k === 'q') { setRot(ROT - 1); return; }
  if (k === 'e') { setRot(ROT + 1); return; }
  if (k === 'escape') { if (roadAnchor) { roadAnchor = null; return; } setTool('inspect'); pinned = null; updateInfo(); return; }
  if (k === '+' || k === '=') { setScale(scale + 1, stage.clientWidth / 2, stage.clientHeight / 2); return; }
  if (k === '-') { setScale(scale - 1, stage.clientWidth / 2, stage.clientHeight / 2); return; }
  const t = TOOLS.find(t => t.key === k); if (t) { setTool(t.id); return; }
  keys[k] = true;
});
window.addEventListener('keyup', e => { const k = e.key.toLowerCase(); if (k === ' ') spaceDown = false; keys[k] = false; });
window.addEventListener('resize', resize);
if (window.ResizeObserver) new ResizeObserver(resize).observe(stage);

// ---------- Resource view ----------
let lensOn = false; const lensSet = { wood: true, stone: true, ore: true, fert: true };
function lensShows(k) {
  if (lensOn && lensSet[k]) return true;
  if (tool === 'zoneFarm' && k === 'fert') return true;
  if (tool === 'zoneWood' && k === 'wood') return true;
  if (tool === 'zoneMine' && (k === 'stone' || k === 'ore')) return true;
  return false;
}
function setLens(on) { lensOn = on; if (on) S.flags.lens = true; $('lensbtn').classList.toggle('on', on); $('lens').hidden = !on; updateGoals(); }
$('lensbtn').onclick = () => setLens(!lensOn);
for (const k of ['wood', 'stone', 'ore', 'fert']) $('lc-' + k).onclick = () => { lensSet[k] = !lensSet[k]; $('lc-' + k).classList.toggle('off', !lensSet[k]); };
// ---------- Rendering ----------
function zmask(x, y, z) {
  let m = 0;
  for (let i = 0; i < 4; i++) { const [nx, ny] = nbW(x, y, i); if (!(inb(nx, ny) && S.zone[ny][nx] === z)) m |= NBD[i][2]; }
  return m;
}
function roadPath(a, b, flip) {
  const out = [[a.x, a.y]]; let x = a.x, y = a.y;
  const dx = Math.sign(b.x - a.x), dy = Math.sign(b.y - a.y);
  const xFirst = Math.abs(b.x - a.x) >= Math.abs(b.y - a.y), first = flip ? !xFirst : xFirst;
  const stepX = () => { while (x !== b.x) { x += dx; out.push([x, y]); } }, stepY = () => { while (y !== b.y) { y += dy; out.push([x, y]); } };
  if (first) { stepX(); stepY(); } else { stepY(); stepX(); }
  return out;
}
function commitRoad(a, b) {
  const r = placeRoadPath(S, roadPath(a, b, !!keys['shift']));
  if (!r.ok) toast(r.reason); else { updateHUD(); if (!r.n) toast('That stretch is already road'); }
}
function draw() {
  const k = scale * dprI;
  ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.fillStyle = '#0e1118'; ctx.fillRect(0, 0, cv.width, cv.height);
  ctx.imageSmoothingEnabled = false;
  const lensAny = lensOn || ZONE[tool];
  const cx = Math.round(cam.x), cy = Math.round(cam.y);
  ctx.setTransform(k, 0, 0, k, -cx * k, -cy * k);
  const vw = cv.width / k, vh = cv.height / k;
  for (let s = 0; s <= 2 * N - 2; s++) {
    for (let vx = Math.max(0, s - N + 1); vx <= Math.min(N - 1, s); vx++) {
      const vy = s - vx; const [x, y] = fromView(vx, vy); const sx = (vx - vy) * 16, sy = (vx + vy) * 8;
      if (sx + 16 < cx || sx - 16 > cx + vw || sy + 16 < cy || sy - 32 > cy + vh) continue;
      const ox = sx - 16, oy = sy - 32;
      const hv = hash2(x, y, 5);
      const gi = Math.floor(hv * 4), gf = hv < 0.5 ? 0 : 1;
      ctx.drawImage(S.fert[y][x] ? (ovrSprite('grassfert_' + gf, ROT) || SPR.grassF[gf]) : (ovrSprite('grass_' + gi, ROT) || SPR.grass[gi]), ox, oy);
      if (S.zone[y][x]) ctx.drawImage(ZSPR(S.zone[y][x], zmask(x, y, S.zone[y][x])), ox, oy);
      if (lensAny) {
        const dk = S.deco[y][x];
        const lk = dk === 'tree' ? 'wood' : dk === 'rock' ? 'stone' : dk === 'ore' ? 'ore' : (!S.grid[y][x] && S.fert[y][x] ? 'fert' : null);
        if (lk && lensShows(lk)) ctx.drawImage(SPR.lens[lk], ox, oy);
      }
      const b = S.grid[y][x];
      if (b) {
        let mask = 0;
        if (b.t === 'road') for (let i = 0; i < 4; i++) { const [nx, ny] = nbW(x, y, i); if (inb(nx, ny) && S.grid[ny][nx]) mask |= NBD[i][2]; }
        if (b.sz === 2) {
          let front = true;
          for (const [fx, fy] of [[b.x, b.y], [b.x + 1, b.y], [b.x, b.y + 1], [b.x + 1, b.y + 1]]) { const [a, c] = toView(fx, fy); if (a + c > vx + vy) front = false; }
          if (front) {
            ctx.drawImage(spriteFor(b.t, b, 0), sx - 32, sy + 16 - 88);
            if (!b.served) ctx.drawImage(SPR.warn, sx - 16, sy - 40);
          }
        } else {
          ctx.drawImage(spriteFor(b.t, b, mask), ox, oy);
          if (!b.served && b.t !== 'road') ctx.drawImage(SPR.warn, ox, oy - 8);
        }
      } else if (S.deco[y][x]) {
        const kind = S.deco[y][x], v = Math.floor(hash2(x, y, 8) * (kind === 'tree' ? 3 : 2));
        ctx.drawImage(ovrSprite(kind + '_' + v, ROT) || SPR[kind][v], ox, oy);
      }
    }
  }
  if (drag && drag.rect) {
    const r = drag.rect, kind = ZONE[tool]; ctx.globalAlpha = 0.9;
    const x0 = Math.min(r.a.x, r.b.x), x1 = Math.max(r.a.x, r.b.x), y0 = Math.min(r.a.y, r.b.y), y1 = Math.max(r.a.y, r.b.y);
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let m = 0; for (let i = 0; i < 4; i++) { const [nx, ny] = nbW(x, y, i); if (nx < x0 || nx > x1 || ny < y0 || ny > y1) m |= NBD[i][2]; }
      const [px, py] = tpos(x, y); ctx.drawImage(zoneCheck(S, kind, x, y).ok ? ZSPR(kind, m) : SPR.hlBad, px - 16, py - 32);
    }
    ctx.globalAlpha = 1;
  }
  const mark = (t, spr) => { if (t) { const [px, py] = tpos(t.x, t.y); ctx.drawImage(spr, px - 16, py - 32); } };
  if (pinned) mark(pinned, SPR.hlNone);
  if (hover) {
    if (tool === 'inspect') mark(hover, SPR.hlNone);
    else if (tool === 'road') {
      const a = (drag && drag.road && drag.moved) ? drag.rs : roadAnchor;
      const path = a ? roadPath(a, hover, !!keys['shift']) : [[hover.x, hover.y]];
      const chk = roadPathCheck(S, path);
      ctx.globalAlpha = 0.7;
      for (const [x, y] of path) { const g = S.grid[y][x]; if (!g) { const [px, py] = tpos(x, y); ctx.drawImage(SPR.road[15], px - 16, py - 32); } }
      ctx.globalAlpha = 1;
      for (const [x, y] of path) mark({ x, y }, chk.ok ? SPR.hlGood : SPR.hlBad);
      if (a) mark(a, SPR.hlNone);
    }
    else if (RECT(tool)) mark(hover, zoneCheck(S, ZONE[tool], hover.x, hover.y).ok ? SPR.hlGood : SPR.hlBad);
    else if (tool === 'bulldoze') mark(hover, (S.grid[hover.y][hover.x] && S.grid[hover.y][hover.x].t !== 'hall') || S.deco[hover.y][hover.x] || S.zone[hover.y][hover.x] ? SPR.hlBad : SPR.hlNone);
    else {
      const c = canPlace(S, tool, hover.x, hover.y);
      ctx.globalAlpha = 0.7;
      if (!S.grid[hover.y][hover.x]) { const [px, py] = tpos(hover.x, hover.y); ctx.drawImage(spriteFor(tool, { lvl: 1 }, tool === 'road' ? 15 : 0), px - 16, py - 32); }
      ctx.globalAlpha = 1; mark(hover, c.ok ? SPR.hlGood : SPR.hlBad);
    }
  }
}

// ---------- HUD / info / goals ----------
const fmtRate = r => (r >= 0.05 ? '+' : r <= -0.05 ? '' : '±') + (Math.abs(r) < 0.05 ? '0' : r.toFixed(1));
const DEM_KEYS = ['res', 'farm', 'comm', 'wood', 'mine'];
const DEM_LABELS = { res: 'Homes', farm: 'Farms', comm: 'Trade', wood: 'Lumber', mine: 'Mining' };
{ const box = $('dem'); for (const k of DEM_KEYS) { const w = document.createElement('div'); w.className = 'bar'; w.title = DEM_LABELS[k] + ' demand'; w.innerHTML = '<div class="track"><div class="mid"></div><div class="fill pos" id="db-' + k + '"></div></div><span>' + DEM_LABELS[k] + '</span>'; box.appendChild(w); } }
function updateHint() {
  let t = '';
  if (tool === 'road') t = roadAnchor ? 'Click where the road ends. Shift bends it the other way. Esc cancels.' : 'Click where the road starts, then click where it ends. You can start or end on an existing road.';
  else if (ZONE[tool]) t = 'Drag across an area to zone it. Buildings grow within two tiles of a road.';
  const el = $('hint'); if (el.textContent !== t) { el.textContent = t; el.hidden = !t; }
}
function updateHUD() {
  const d = S.d || recalc(S);
  for (const k of ['food', 'wood', 'stone', 'ore', 'gold']) {
    $('v-' + k).textContent = Math.floor(S.res[k]);
    const r = $('r-' + k); r.textContent = fmtRate(S.rate[k]); r.className = 'rate ' + (S.rate[k] > 0.05 ? 'up' : S.rate[k] < -0.05 ? 'down' : '');
  }
  $('v-day').textContent = S.day;
  $('v-peas').textContent = S.peasants + '/' + d.civ;
  $('v-sold').textContent = S.soldiers + '/' + d.bunks;
  const emp = Math.min(S.peasants, d.jobs);
  $('v-jobs').textContent = emp + '/' + d.jobs;
  for (const k of DEM_KEYS) { const v = S.dem[k] || 0, f = $('db-' + k); f.style.height = Math.round(Math.abs(v) * 22) + 'px'; f.className = 'fill ' + (v >= 0 ? 'pos' : 'neg'); f.style[v >= 0 ? 'bottom' : 'top'] = '22px'; f.style[v >= 0 ? 'top' : 'bottom'] = 'auto'; }
  $('hud-warn').textContent = S.starving ? 'Starving: build farms' : '';
  $('draft').textContent = 'Drafting ' + (S.drafting ? 'on' : 'off'); $('draft').classList.toggle('off', !S.drafting);
  updateGoals();
}
const cntZone = k => S.zone.flat().filter(z => z === k).length;
const GOALS = [
  ['Press V to open the resource view', S => !!S.flags.lens],
  ['Zone 8 tiles for housing beside a road', S => cntZone('res') >= 8],
  ['Zone farmland on fertile soil', S => cntZone('farm') >= 4],
  ['Zone forest and get a woodcutter', S => S.blds.some(b => b.t === 'wood' && b.zoned)],
  ['Zone commerce and get a market', S => S.blds.some(b => b.t === 'market')],
  ['Zone ore and get an iron mine', S => S.blds.some(b => b.t === 'mine')],
  ['Raise a barracks and train 6 soldiers', S => S.trained >= 6]
];
const goalsEl = $('goals-list');
GOALS.forEach((g, i) => { const li = document.createElement('li'); li.id = 'g' + i; li.textContent = g[0]; goalsEl.appendChild(li); });
function updateGoals() { GOALS.forEach((g, i) => { if (g[1](S)) S['goal' + i] = true; $('g' + i).classList.toggle('done', !!S['goal' + i]); }); }
function describe(t) {
  const b = S.grid[t.y][t.x], d = S.d;
  if (!b) {
    const k = S.deco[t.y][t.x];
    if (k === 'tree') return ['Forest', ['A woodcutter within 3 tiles harvests these trees.', 'Building here clears the tree.']];
    if (k === 'rock') return ['Rock outcrop', ['Stone. A mining zone here grows quarries.', 'Rocks block other buildings.']];
    if (k === 'ore') return ['Iron ore', ['A mining zone here grows iron mines. Soldiers need ore to arm.', 'Ore blocks other buildings.']];
    const z = S.zone[t.y][t.x];
    if (z) {
      const nm = ZONE_NAMES[z];
      return [nm, [zoneServed(S, t.x, t.y) ? (S.note[z] || 'Ready to grow.') : 'No road access. Zones need a road touching them that leads to the Town Hall.', 'Use Clear to remove the zone.']];
    }
    return [S.fert[t.y][t.x] ? 'Fertile soil' : 'Open grass', [S.fert[t.y][t.x] ? 'Good farmland. Paint a farm zone here.' : 'Poor soil. Farms need fertile land.', 'Free to zone or build on.']];
  }
  const L = []; const nm = nameOf(b);
  if (b.zoned) L.push('Grew from a zone.');
  if (b.t !== 'road' && b.t !== 'hall') L.push(b.served ? 'Road connected' : 'No road link. Place a road touching this building that leads back to the Town Hall.');
  if (b.t === 'road') L.push(b.served ? 'Connected to the Town Hall' : 'Not connected to the Town Hall');
  if (b.t === 'hall') L.push('Houses 6 people. Storage limit ' + STORAGE + ' per resource.');
  if (b.t === 'house') L.push('Level ' + b.lvl + ' · holds ' + capOf(b) + ' people', b.lvl === 1 ? 'A well within 4 tiles upgrades it to level 2.' : 'Upgraded by a nearby well.');
  if (b.t === 'farm') L.push('Jobs: ' + jobsOf(b) + ' · makes ' + (b.served ? (foodOf(b) * d.ratio).toFixed(1) : '0') + ' food per second', 'Staffed at ' + Math.round(d.ratio * 100) + '%');
  if (b.t === 'wood') { const n = countDeco(S, b.x, b.y, 3, 'tree'); L.push('Jobs: 3 · ' + n + ' trees in range', n ? 'Makes ' + (b.served ? (2.5 * d.ratio).toFixed(1) : '0') + ' wood per second' : 'No trees left. Move it closer to a forest.'); }
  if (b.t === 'quarry') { const n = countDeco(S, b.x, b.y, 3, 'rock'); L.push('Jobs: 3 · ' + n + ' rock tiles in range', n ? 'Makes ' + (b.served ? (2 * d.ratio).toFixed(1) : '0') + ' stone per second' : 'Out of rock. Clear it and zone a new site.'); }
  if (b.t === 'mine') { const n = countDeco(S, b.x, b.y, 3, 'ore'); L.push('Jobs: 3 · ' + n + ' ore tiles in range', n ? 'Makes ' + (b.served ? (1.5 * d.ratio).toFixed(1) : '0') + ' ore per second' : 'Out of ore. Clear it and zone a new site.'); }
  if (b.t === 'market') L.push('Jobs: ' + jobsOf(b) + ' · covers homes within ' + (b.sz === 2 ? 7 : 5) + ' tiles', 'Earns gold from covered villagers (' + Math.round(d.cov * 100) + '% of homes covered).');
  if (b.t === 'barracks') L.push('Sleeps 6 soldiers · army ' + S.soldiers + '/' + d.bunks, 'Each recruit costs 2 wood, 1 gold, 1 ore and one villager.', 'Soldiers eat 1.5x and cost 0.1 gold per second.');
  if (b.t === 'well') L.push('Upgrades houses within 4 tiles.');
  return [nm, L];
}
function updateInfo() {
  const t = pinned || hover; const el = $('info');
  if (!t) {
    el.innerHTML = '<h2>Thornwick</h2><p>Paint zones beside a road that reaches the Town Hall. Buildings grow there by themselves when the demand bars show a need and there is wood. Press V to see forest, stone, ore and farmable soil. Soldiers sleep in barracks, so every recruit needs a bunk.</p><p class="dim">Drag to pan. Scroll to zoom. Pick a tool below. Drag to paint roads, drag a box to zone.</p>';
    return;
  }
  const [name, lines] = describe(t);
  el.innerHTML = '<h2>' + name + '</h2>' + lines.map(l => '<p>' + l + '</p>').join('') + '<p class="dim">Tile ' + t.x + ', ' + t.y + '</p>';
}
function setSpeed(s) { speed = s; for (const id of [0, 1, 3]) $('sp' + id).classList.toggle('on', id === s); }
$('sp0').onclick = () => setSpeed(0); $('sp1').onclick = () => setSpeed(1); $('sp3').onclick = () => setSpeed(3);
$('draft').onclick = () => { S.drafting = !S.drafting; updateHUD(); };
$('rotl').onclick = () => setRot(ROT - 1);
$('rotr').onclick = () => setRot(ROT + 1);
$('zin').onclick = () => setScale(scale + 1, stage.clientWidth / 2, stage.clientHeight / 2);
$('zout').onclick = () => setScale(scale - 1, stage.clientWidth / 2, stage.clientHeight / 2);
setSpeed(1);
// ---------- Save / load ----------
const SAVE_KEY = 'thornwick-save-1';
function doSave(quiet) { try { localStorage.setItem(SAVE_KEY, saveWorld(S)); if (!quiet) toast('Game saved'); } catch (e) { if (!quiet) toast('Saving is blocked in this browser'); } }
function loadSaved() { try { const t = localStorage.getItem(SAVE_KEY); if (t) { const w = loadWorld(t); if (w) { S = w; return true; } } } catch (e) { } return false; }
$('savebtn').onclick = () => doSave(false);
$('newbtn').onclick = () => { if (!confirm('Start a new village? Your saved game will be replaced.')) return; S = createWorld(Math.floor(Math.random() * 9000) + 1); pinned = null; roadAnchor = null; try { localStorage.removeItem(SAVE_KEY); } catch (e) { } centerOn(20, 24); updateHUD(); updateInfo(); toast('A new village'); };
setInterval(() => doSave(true), 30000);
window.addEventListener('beforeunload', () => doSave(true));

// ---------- Main loop ----------
let last = performance.now(), acc = 0;
function frame(now) {
  const dt = Math.min(0.25, (now - last) / 1000); last = now;
  const sp = 340 / scale * dt * 1.0;
  if (keys['w'] || keys['arrowup']) cam.y -= sp; if (keys['s'] || keys['arrowdown']) cam.y += sp;
  if (keys['a'] || keys['arrowleft']) cam.x -= sp; if (keys['d'] || keys['arrowright']) cam.x += sp;
  if (speed > 0) { acc += dt * speed; while (acc >= 1) { acc -= 1; tick(S); updateHUD(); } }
  updateHint(); draw(); requestAnimationFrame(frame);
}
loadSaved(); resize(); centerOn(20, 24); updateHUD(); updateInfo();
requestAnimationFrame(frame);
window.__game = { zone: (k, x, y) => setZone(S, k, x, y), get S() { return S; }, setTool, place: (t, x, y) => place(S, t, x, y), tick: () => { tick(S); updateHUD(); } };
