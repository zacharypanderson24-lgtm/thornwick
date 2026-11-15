// ---------- Texture slots, overrides, asset loader, and the texture lab ----------
const STYLE = 'Isometric pixel art for a medieval city-builder game in the spirit of TheoTown. True 2:1 isometric: a ground tile is a diamond exactly twice as wide as it is tall. Hard pixel edges, no anti-aliasing, no gradients, no blur, no outline thicker than 1 pixel, about 24 colours at most. Warm, earthy early-medieval palette: weathered timber, straw thatch, packed dirt, grey fieldstone, muted greens. Light comes from the top-left: left-facing walls are mid-tone, right-facing walls are darker, roofs are lightest on their left slope. No cast shadow on the ground and nothing drawn on the ground except the object itself. One object per image, centred, completely visible, viewed from a fixed camera about 30 degrees above the horizon. Background: one flat solid pure magenta (#FF00FF) so it can be removed.';
const VIEW_TXT = [
  'VIEW 1 (default): the main door and the most detail face the lower-left wall.',
  'VIEW 2: turn the whole building 90 degrees clockwise (seen from above) from view 1. The main door is now on the upper-left wall, so the camera sees the back of the building.',
  'VIEW 3: turn it another 90 degrees clockwise. The main door is on the upper-right wall, so the camera sees the building from the far side.',
  'VIEW 4: turn it one more 90 degrees clockwise. The main door is on the lower-right wall.'
];
const SAME_TXT = 'Keep footprint, size, colours and proportions identical in every view; only the rotation changes.';
const SLOTS = [], SLOT = {};
function addSlot(name, label, group, size, views, type, desc) { const s = { name, label, group, size, views, type, desc }; SLOTS.push(s); SLOT[name] = s; }
addSlot('hall', 'Town Hall', 'Buildings (3x3 tiles)', 3, 4, 'object', 'The village centerpiece covering 3x3 tiles: a long great hall with a grey fieldstone ground floor, cream plaster and exposed brown timber walls, steep golden thatch, dark shingle ridge, a small open timber belfry holding a bell and gold pennant, rear stone chimney, and one broad porch-covered double plank entrance with two iron straps, shuttered windows and a few small ground props strictly inside the 3x3 diamond. Views 1 and 4 show the entrance; views 2 and 3 show the rear. No detail crosses the ground boundary.');
addSlot('house_1', 'Peasant hut (level 1)', 'Buildings (1 tile)', 1, 4, 'object', 'A small one-room cottage of cream wattle-and-daub with dark exposed timber beams, a deep straw-thatch roof in warm gold-brown with a ragged eave, a low plank door, one small shuttered window and a squat stone chimney. Modest and a bit crooked.');
for (let i = 2; i <= 8; i++) addSlot('house_1_v' + i, 'Peasant hut variation ' + i, 'Buildings (1 tile)', 1, 4, 'object', 'A full-footprint early-medieval Level 1 peasant hut with varied thatch and timber details.');
addSlot('house_2', 'Cottage (level 2)', 'Buildings (1 tile)', 1, 4, 'object', 'The same footprint as the hut but taller and tidier: fieldstone lower walls, cream plaster and timber framing above, a red clay-tile roof, a stone chimney, a glazed window and a small flower box. A well-kept home.');
addSlot('farm', 'Farm', 'Buildings (1 tile)', 1, 4, 'object', 'A tilled field filling the tile: furrow rows in golden yellow and brown with green sprouts, a low wooden fence border, and a tiny red-brown timber barn with a hay door in one back corner.');
addSlot('wood', 'Woodcutter lodge', 'Buildings (1 tile)', 1, 4, 'object', 'A small log cabin of dark round logs with a mossy green plank roof, an axe stuck in a chopping stump by the door, and a neat stack of cut logs beside it.');
addSlot('quarry', 'Quarry', 'Buildings (1 tile)', 1, 4, 'object', 'A shallow pit cut into pale grey rock, cut stone blocks stacked at the edge, a wooden hoist with rope, and a small hand cart. Light grey dust.');
addSlot('mine', 'Iron mine', 'Buildings (1 tile)', 1, 4, 'object', 'A mound of dark grey rock with a timber-framed tunnel entrance (two posts and a lintel) with a black opening, a small wooden ore cart, and a pile of reddish-brown iron ore lumps with orange flecks.');
addSlot('well', 'Well', 'Buildings (1 tile)', 1, 4, 'object', 'A round fieldstone well with blue water showing, two wooden posts holding a small gabled shingle roof, a wooden bucket on a rope and a crank handle.');
addSlot('barracks', 'Barracks', 'Buildings (2x2 tiles)', 2, 4, 'object', 'An early-medieval village barracks covering a full 2x2 tile footprint: long heavy timber plank walls with cross-braces, a deep golden thatched gable roof, narrow barred windows, one double plank door and a modest crimson crossed-swords pennant above the entrance. View 1 puts the single door on the left-facing visible wall; views 2 and 3 hide it; view 4 shows it on the right-facing visible wall. No props or walls extend outside the 2x2 footprint.');
addSlot('market', 'Market stall', 'Buildings (1 tile)', 1, 4, 'object', 'A wooden stall on four posts with a striped red-and-cream cloth awning, a counter holding crates of apples, cabbages and bread, and a hanging scale.');
addSlot('manor_1', 'Large house (level 1)', 'Buildings (2x2 tiles)', 2, 4, 'object', 'A big family house filling a 2x2 tile footprint: a long cream-plaster hall with dark timbers, a deep straw-thatch roof, a lower side wing, a small porch with the main door, one stone chimney and four shuttered windows.');
addSlot('manor_2', 'Large house (level 2)', 'Buildings (2x2 tiles)', 2, 4, 'object', 'The same footprint, grander: stone ground floor, timber-framed upper floor, red clay-tile roof, two stone chimneys, an arched door with a small porch, glazed windows.');
addSlot('farm2', 'Large farm', 'Buildings (2x2 tiles)', 2, 4, 'object', 'A 2x2 tile farm: wheat furrows in gold and brown, a bigger red-brown timber barn with a hayloft door in one back corner, haystacks, a water trough and a low fence.');
addSlot('market2', 'Bazaar', 'Buildings (2x2 tiles)', 2, 4, 'object', 'A small cobbled square (2x2 tiles) with four stalls with different awning colours (red, blue, green, yellow) arranged around a central flagpole with a red pennant; barrels and sacks.');
for (let i = 0; i < 4; i++) addSlot('grass_' + i, 'Grass ' + (i + 1), 'Terrain', 1, 1, 'ground', 'A flat diamond tile of lush green meadow with a few darker tufts and tiny pale flowers. It must tile seamlessly with itself. The four variants differ only in where the tufts and flowers sit.');
for (let i = 0; i < 2; i++) addSlot('grassfert_' + i, 'Fertile soil ' + (i + 1), 'Terrain', 1, 1, 'ground', 'A flat diamond tile of fertile land: richer, slightly yellower green grass with dark brown soil patches showing and a few ochre grain specks. Must tile seamlessly.');
for (let i = 0; i < 2; i++) addSlot('tree_' + i, 'Broadleaf tree ' + (i + 1), 'Nature', 1, 1, 'object', 'A broadleaf tree: rounded mid-green canopy with a lighter highlight top-left and dark green underside, a short brown trunk. About one tile wide and two tiles tall. Looks the same from every side.');
addSlot('tree_2', 'Pine tree', 'Nature', 1, 1, 'object', 'A conifer: layered triangular boughs in dark blue-green with lighter tips, a short brown trunk. About one tile wide and two tiles tall.');
for (let i = 0; i < 2; i++) addSlot('rock_' + i, 'Boulders ' + (i + 1), 'Nature', 1, 1, 'object', 'A cluster of two or three rounded grey fieldstones, lit from the top-left, with a few moss specks. Less than one tile wide.');
for (let i = 0; i < 2; i++) addSlot('ore_' + i, 'Iron ore ' + (i + 1), 'Nature', 1, 1, 'object', 'An outcrop of dark slate-grey rock with rusty orange and pale blue-silver mineral veins. Less than one tile wide.');

const slotDims = s => s.size === 3 ? [96, 128] : s.size === 2 ? [64, 88] : [32, 48];
const viewKeys = s => s.views === 4 ? [1, 2, 3, 4] : ['all'];
function finalSize(s) { const d = slotDims(s); return d[0] + ' x ' + d[1] + ' px'; }
function footprintText(s) { return s.type === 'ground' ? 'The image is exactly one ground tile: a flat diamond twice as wide as tall, filling the whole image.' : (s.size === 2 ? 'The footprint is a 2x2 block of ground tiles, so the base of the object is a diamond four times the area of a single tile (twice as wide as tall). The object sits with the bottom corner of that diamond at the bottom of the image.' : 'The footprint is a single ground tile (a diamond twice as wide as tall). The object sits with the bottom corner of that diamond at the bottom of the image.'); }
function promptFor(s, v) {
  const head = STYLE + '\n\nSubject: ' + s.label + '. ' + s.desc + '\n\n' + footprintText(s);
  if (s.views === 4) return head + '\n\n' + (v === 'sheet' ? 'Make ONE image containing four views of the same building in a 2x2 grid on the magenta background, each view in its own equal cell and fully visible. Top-left is view 1, top-right is view 2, bottom-left is view 3, bottom-right is view 4.\n' + VIEW_TXT.join('\n') : VIEW_TXT[(v || 1) - 1]) + '\n' + SAME_TXT;
  return head + '\n\nOne view only; it looks the same from every side.';
}

// ---- overrides ----
const OVR = {}, CELL = {}, ASSET_OK = {};
function ovrSprite(name, rot) { const o = OVR[name]; if (!o) return null; return o[rot + 1] || o.all || null; }
function keyOut(id) {
  const w = id.width, h = id.height, d = id.data;
  const px = (x, y) => (y * w + x) * 4;
  const corners = [px(0, 0), px(w - 1, 0), px(0, h - 1), px(w - 1, h - 1)];
  if (corners.some(i => d[i + 3] < 200)) return;
  const key = [0, 1, 2].map(c => Math.round(corners.reduce((a, i) => a + d[i + c], 0) / 4));
  const tol = 60 * 60, near = i => { const dr = d[i] - key[0], dg = d[i + 1] - key[1], db = d[i + 2] - key[2]; return dr * dr + dg * dg + db * db <= tol; };
  if (!corners.every(near)) return;
  const stack = [], seen = new Uint8Array(w * h);
  const push = (x, y) => { if (x < 0 || y < 0 || x >= w || y >= h) return; const k = y * w + x; if (seen[k]) return; seen[k] = 1; if (near(k * 4)) { d[k * 4 + 3] = 0; stack.push(k); } };
  for (let x = 0; x < w; x++) { push(x, 0); push(x, h - 1); } for (let y = 0; y < h; y++) { push(0, y); push(w - 1, y); }
  while (stack.length) { const k = stack.pop(), x = k % w, y = (k / w) | 0; push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1); }
}
function alphaBox(id) {
  const w = id.width, h = id.height, d = id.data; let x0 = w, y0 = h, x1 = -1, y1 = -1;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (d[(y * w + x) * 4 + 3] > 40) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
}
function fitToSlot(src, slot, o) {
  const [W, H] = slotDims(slot);
  const sw = src.naturalWidth || src.width, sh = src.naturalHeight || src.height;
  const wk = document.createElement('canvas'); wk.width = sw; wk.height = sh;
  const wg = wk.getContext('2d', { willReadFrequently: true }); wg.drawImage(src, 0, 0);
  let box = { x: 0, y: 0, w: sw, h: sh };
  try {
    const id = wg.getImageData(0, 0, sw, sh);
    if (o.bg !== false) keyOut(id);
    wg.putImageData(id, 0, 0);
    box = alphaBox(id) || box;
  } catch (e) { }
  const scale = (slot.type === 'ground' ? W / box.w : Math.min(W / box.w, H / box.h)) * (o.scale || 1);
  const tw = Math.max(1, Math.round(box.w * scale)), th = Math.max(1, Math.round(box.h * scale));
  const out = document.createElement('canvas'); out.width = W; out.height = H;
  const g = out.getContext('2d', { willReadFrequently: true });
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  g.drawImage(wk, box.x, box.y, box.w, box.h, Math.round((W - tw) / 2) + (o.dx || 0), H - th + (o.dy || 0), tw, th);
  try { const id = g.getImageData(0, 0, W, H), d = id.data; for (let i = 3; i < d.length; i += 4) d[i] = d[i] < 128 ? 0 : 255; g.putImageData(id, 0, 0); } catch (e) { }
  return out;
}
function fitAsset(img, slot) {
  const [W, H] = slotDims(slot); const out = document.createElement('canvas'); out.width = W; out.height = H;
  const g = out.getContext('2d'); g.imageSmoothingEnabled = img.width !== W; g.imageSmoothingQuality = 'high'; g.drawImage(img, 0, 0, W, H); return out;
}
function texChanged() { if (typeof refreshIcons === 'function') refreshIcons(); if (typeof labRefresh === 'function') labRefresh(); }
const IDB = {
  db: null,
  open() { return new Promise(res => { try { const rq = indexedDB.open('thornwick-tex', 1); rq.onupgradeneeded = () => rq.result.createObjectStore('tex'); rq.onsuccess = () => { IDB.db = rq.result; res(true); }; rq.onerror = () => res(false); } catch (e) { res(false); } }); },
  put(k, v) { try { if (IDB.db) IDB.db.transaction('tex', 'readwrite').objectStore('tex').put(v, k); } catch (e) { } },
  del(k) { try { if (IDB.db) IDB.db.transaction('tex', 'readwrite').objectStore('tex').delete(k); } catch (e) { } },
  all() { return new Promise(res => { try { const out = {}; const rq = IDB.db.transaction('tex').objectStore('tex').openCursor(); rq.onsuccess = () => { const c = rq.result; if (c) { out[c.key] = c.value; c.continue(); } else res(out); }; rq.onerror = () => res({}); } catch (e) { res({}); } }); }
};
async function setCell(name, vk, blob, opts, persist) {
  const bmp = await createImageBitmap(blob);
  const cell = { blob, bmp, opts: Object.assign({ dx: 0, dy: 0, scale: 1, bg: true }, opts || {}) };
  (CELL[name] = CELL[name] || {})[vk] = cell; renderCell(name, vk);
  if (persist !== false) IDB.put(name + '|' + vk, { blob, opts: cell.opts });
}
function renderCell(name, vk) { const c = CELL[name][vk]; (OVR[name] = OVR[name] || {})[vk] = fitToSlot(c.bmp, SLOT[name], c.opts); texChanged(); }
function clearCell(name, vk) { if (CELL[name]) delete CELL[name][vk]; if (OVR[name]) delete OVR[name][vk]; IDB.del(name + '|' + vk); texChanged(); }
function nudge(name, vk, patch) {
  const c = CELL[name] && CELL[name][vk]; if (!c) return;
  const o = c.opts; if (patch.dx) o.dx += patch.dx; if (patch.dy) o.dy += patch.dy; if (patch.scale) o.scale = Math.max(0.2, Math.round((o.scale * patch.scale) * 100) / 100);
  renderCell(name, vk); IDB.put(name + '|' + vk, { blob: c.blob, opts: o });
}
async function loadSheet(name, file, layout) {
  const s = SLOT[name], bmp = await createImageBitmap(file);
  const cols = layout === '1x4' ? 4 : 2, rows = layout === '1x4' ? 1 : 2, cw = Math.floor(bmp.width / cols), ch = Math.floor(bmp.height / rows);
  for (let i = 0; i < 4; i++) {
    const c = document.createElement('canvas'); c.width = cw; c.height = ch;
    c.getContext('2d').drawImage(bmp, (i % cols) * cw, Math.floor(i / cols) * ch, cw, ch, 0, 0, cw, ch);
    const blob = await new Promise(r => c.toBlob(r, 'image/png')); await setCell(name, i + 1, blob, { bg: labOpts.bg });
  }
}
function probeAssets() {
  for (const s of SLOTS) for (const vk of viewKeys(s)) {
    const file = 'assets/' + s.name + (s.views === 4 ? '_' + vk : '') + '.png';
    const im = new Image(); im.onload = () => { if (!(CELL[s.name] && CELL[s.name][vk])) { (OVR[s.name] = OVR[s.name] || {})[vk] = fitAsset(im, s); (ASSET_OK[s.name] = ASSET_OK[s.name] || {})[vk] = true; texChanged(); } }; im.onerror = () => { }; im.src = file;
  }
}

// ---- the lab ----
const labOpts = { bg: true, same: false };
let labSel = 'house_1';
const lab = document.getElementById('lab');
const VIEW_LABEL = ['View 1: door faces lower-left', 'View 2: door faces upper-left', 'View 3: door faces upper-right', 'View 4: door faces lower-right'];
function el(tag, cls, text) { const e = document.createElement(tag); if (cls) e.className = cls; if (text !== undefined) e.textContent = text; return e; }
function copyText(t, btn) {
  const done = () => { const o = btn.textContent; btn.textContent = 'Copied'; setTimeout(() => btn.textContent = o, 1200); };
  try { navigator.clipboard.writeText(t).then(done, () => fallbackCopy(t, done)); } catch (e) { fallbackCopy(t, done); }
}
function fallbackCopy(t, done) { const a = document.createElement('textarea'); a.value = t; document.body.appendChild(a); a.select(); try { document.execCommand('copy'); done(); } catch (e) { } a.remove(); }
function labStatus(s) { let n = 0; const keys = viewKeys(s); for (const k of keys) if (OVR[s.name] && OVR[s.name][k]) n++; return n + '/' + keys.length; }
function labRefresh() {
  if (!lab || lab.hidden) return;
  const keepScroll = lab.querySelector('.lab-main') ? lab.querySelector('.lab-main').scrollTop : 0;
  lab.innerHTML = '';
  const head = el('div', 'lab-head'); head.appendChild(el('h2', '', 'Texture lab'));
  const x = el('button', 'btn', 'Close'); x.onclick = () => { lab.hidden = true; document.getElementById('texbtn').classList.remove('on'); }; head.appendChild(x); lab.appendChild(head);
  lab.appendChild(el('p', 'lab-help', 'Pick a slot, copy its prompt into ChatGPT, then drop the picture on a view. The game uses it straight away. Turn the camera with Q and E to check each view. Textures you load stay in this browser.'));
  const body = el('div', 'lab-body');
  const list = el('div', 'lab-list'); let grp = '';
  for (const s of SLOTS) {
    if (s.group !== grp) { grp = s.group; list.appendChild(el('h3', '', grp)); }
    const b = el('button', 'lab-item' + (s.name === labSel ? ' on' : ''), s.label); b.appendChild(el('i', '', labStatus(s)));
    b.onclick = () => { labSel = s.name; labRefresh(); }; list.appendChild(b);
  }
  body.appendChild(list);
  const s = SLOT[labSel], main = el('div', 'lab-main');
  main.appendChild(el('h3', 'lab-title', s.label)); main.appendChild(el('p', 'lab-meta', 'Final size ' + finalSize(s) + (s.views === 4 ? ' per view, four views' : ' (one view)') + '. Bigger images are fine: they are scaled down for you.'));
  main.appendChild(el('p', 'lab-desc', s.desc));
  const pr = el('div', 'lab-row');
  if (s.views === 4) { for (let v = 1; v <= 4; v++) { const b = el('button', 'btn', 'Copy prompt: view ' + v); b.onclick = () => copyText(promptFor(s, v), b); pr.appendChild(b); } const b = el('button', 'btn', 'Copy 4-view sheet prompt'); b.onclick = () => copyText(promptFor(s, 'sheet'), b); pr.appendChild(b); }
  else { const b = el('button', 'btn', 'Copy prompt'); b.onclick = () => copyText(promptFor(s), b); pr.appendChild(b); }
  main.appendChild(pr);
  const opt = el('div', 'lab-row');
  const l1 = el('label', 'chk'); const c1 = el('input'); c1.type = 'checkbox'; c1.checked = labOpts.bg; c1.onchange = () => labOpts.bg = c1.checked; l1.append(c1, ' Remove plain background'); opt.appendChild(l1);
  if (s.views === 4) { const l2 = el('label', 'chk'); const c2 = el('input'); c2.type = 'checkbox'; c2.checked = labOpts.same; c2.onchange = () => labOpts.same = c2.checked; l2.append(c2, ' One image for all four views'); opt.appendChild(l2); }
  main.appendChild(opt);
  if (s.views === 4) {
    const sh = el('div', 'lab-row'); sh.appendChild(el('span', '', '4-view sheet:'));
    const sel = el('select'); sel.id = 'sheet-layout'; for (const [v, t] of [['2x2', '2 x 2 grid'], ['1x4', '1 x 4 strip']]) { const o = el('option', '', t); o.value = v; sel.appendChild(o); }
    const fi = el('input'); fi.type = 'file'; fi.accept = 'image/*'; fi.onchange = async () => { if (fi.files[0]) { await loadSheet(s.name, fi.files[0], sel.value); } };
    sh.append(sel, fi); main.appendChild(sh);
  }
  const cells = el('div', 'lab-cells');
  for (const vk of viewKeys(s)) {
    const cell = el('div', 'lab-cell'); cell.appendChild(el('div', 'lab-cl', s.views === 4 ? VIEW_LABEL[vk - 1] : 'Texture'));
    const [W, H] = slotDims(s), sc = s.size === 3 ? 2 : s.size === 2 ? 3 : 5, cvs = el('canvas', 'lab-prev'); cvs.width = W * sc; cvs.height = H * sc;
    const g = cvs.getContext('2d'); g.imageSmoothingEnabled = false;
    const o = OVR[s.name] && OVR[s.name][vk];
    if (o) g.drawImage(o, 0, 0, W * sc, H * sc);
    else { const d = s.type === 'ground' ? (defaultSprite(s.name)) : defaultSprite(s.name); if (d) { g.globalAlpha = 0.55; g.drawImage(d, 0, 0, W * sc, H * sc); g.globalAlpha = 1; } }
    g.strokeStyle = 'rgba(255,255,255,.45)'; g.lineWidth = 1; g.beginPath(); const k = s.size === 2 ? 2 : 1, bx = W * sc / 2, by = H * sc;
    g.moveTo(bx, by - 16 * k * sc); g.lineTo(bx + 16 * k * sc, by - 8 * k * sc); g.lineTo(bx, by); g.lineTo(bx - 16 * k * sc, by - 8 * k * sc); g.closePath(); g.stroke();
    cell.appendChild(cvs);
    cell.appendChild(el('div', 'lab-st', o ? (CELL[s.name] && CELL[s.name][vk] ? 'Your texture' : 'From assets folder') : 'Code-drawn placeholder'));
    const fi = el('input'); fi.type = 'file'; fi.accept = 'image/*'; fi.hidden = true;
    const load = async f => { if (!f) return; if (s.views === 4 && labOpts.same) { for (let i = 1; i <= 4; i++) await setCell(s.name, i, f, { bg: labOpts.bg }); } else await setCell(s.name, vk, f, { bg: labOpts.bg }); };
    fi.onchange = () => load(fi.files[0]);
    const lb = el('button', 'btn', 'Load image'); lb.onclick = () => fi.click();
    cell.append(fi, lb);
    cell.ondragover = e => { e.preventDefault(); cell.classList.add('drop'); }; cell.ondragleave = () => cell.classList.remove('drop');
    cell.ondrop = e => { e.preventDefault(); cell.classList.remove('drop'); load(e.dataTransfer.files[0]); };
    const has = CELL[s.name] && CELL[s.name][vk];
    if (has) {
      const nv = el('div', 'lab-row small');
      for (const [t, p] of [['Left', { dx: -1 }], ['Right', { dx: 1 }], ['Up', { dy: -1 }], ['Down', { dy: 1 }], ['Smaller', { scale: 0.95 }], ['Bigger', { scale: 1.05 }]]) { const b = el('button', 'btn sm', t); b.onclick = () => nudge(s.name, vk, p); nv.appendChild(b); }
      cell.appendChild(nv);
      const rr = el('div', 'lab-row small');
      const rs = el('button', 'btn sm', 'Remove'); rs.onclick = () => clearCell(s.name, vk); rr.appendChild(rs);
      const sv = el('button', 'btn sm', 'Save PNG'); sv.onclick = () => { const a = document.createElement('a'); a.download = s.name + (s.views === 4 ? '_' + vk : '') + '.png'; a.href = OVR[s.name][vk].toDataURL('image/png'); a.click(); }; rr.appendChild(sv);
      cell.appendChild(rr);
    }
    cells.appendChild(cell);
  }
  main.appendChild(cells); body.appendChild(main); lab.appendChild(body);
  main.scrollTop = keepScroll;
}
function openLab() { lab.hidden = false; document.getElementById('texbtn').classList.add('on'); labRefresh(); }
document.getElementById('texbtn').onclick = () => { if (lab.hidden) openLab(); else { lab.hidden = true; document.getElementById('texbtn').classList.remove('on'); } };

(async () => { probeAssets(); if (await IDB.open()) { const all = await IDB.all(); for (const k in all) { const [n, v] = k.split('|'); if (SLOT[n]) await setCell(n, v === 'all' ? 'all' : +v, all[k].blob, all[k].opts, false); } } })();
