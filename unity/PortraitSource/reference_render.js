// Reference renderer for the Unity parity tests: a literal port of render(), recolor(), dye() and occlude_hair() from generate.py (Python/Pillow was not
// available on the build machine), plus a recipe set that exercises every asset. Run it from anywhere:
//   node reference_render.js <PortraitSource dir> <output dir>
// It writes <output>/recipes.json (the appearance definitions) and <output>/reference_sheet.png (the expected 48x48 portraits, 25 per row, in recipe order).
// Python semantics kept on purpose: recolour matches exact RGB and ignores alpha, round() is round-half-to-even, alpha_composite over an opaque base.
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const [, , SRC, OUT] = process.argv;

// ---------- PNG ----------
function crc32(buf) { let c, crc = 0xffffffff; for (let n = 0; n < buf.length; n++) { c = (crc ^ buf[n]) & 0xff; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crc = (crc >>> 8) ^ c; } return (crc ^ 0xffffffff) >>> 0; }
function decodePng(file) {
  const b = fs.readFileSync(file); let p = 8, w, h, ct, idat = [];
  while (p < b.length) { const len = b.readUInt32BE(p), type = b.toString('ascii', p + 4, p + 8), data = b.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); if (data[8] !== 8 || data[12] !== 0) throw new Error('unsupported png ' + file); ct = data[9]; }
    else if (type === 'IDAT') idat.push(data); p += 12 + len; }
  if (ct !== 6) throw new Error('not RGBA8 ' + file);
  const raw = zlib.inflateSync(Buffer.concat(idat)), bpp = 4, stride = w * bpp, out = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) { const f = raw[y * (stride + 1)];
    for (let x = 0; x < stride; x++) { const v = raw[y * (stride + 1) + 1 + x], a = x >= bpp ? out[y * stride + x - bpp] : 0, u = y ? out[(y - 1) * stride + x] : 0, c = x >= bpp && y ? out[(y - 1) * stride + x - bpp] : 0; let r;
      if (f === 0) r = v; else if (f === 1) r = v + a; else if (f === 2) r = v + u; else if (f === 3) r = v + ((a + u) >> 1);
      else { const pp = a + u - c, pa = Math.abs(pp - a), pb = Math.abs(pp - u), pc = Math.abs(pp - c); r = v + (pa <= pb && pa <= pc ? a : pb <= pc ? u : c); }
      out[y * stride + x] = r & 255; } }
  return { w, h, d: out };
}
function encodePng(w, h, d) {
  const stride = w * 4, raw = Buffer.alloc(h * (stride + 1));
  for (let y = 0; y < h; y++) { raw[y * (stride + 1)] = 0; d.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride); }
  const chunk = (t, data) => { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(t), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc32(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

// ---------- port of generate.py ----------
const defs = JSON.parse(fs.readFileSync(path.join(SRC, 'definitions.json'), 'utf8'));
const PAL = JSON.parse(fs.readFileSync(path.join(SRC, 'palette_library.json'), 'utf8'));
const OCC = JSON.parse(fs.readFileSync(path.join(SRC, 'hair_occlusion.json'), 'utf8'));
const SKINS = defs.skins, HAIRS = defs.hairs;
const cache = {};
function asset(kind, name) { const k = kind + '/' + name; return cache[k] || (cache[k] = decodePng(path.join(SRC, 'assets', kind, name + '.png'))); }
const hex = s => [0, 2, 4].map(i => parseInt(s.replace('#', '').substr(i, 2), 16));
function recolor(im, old, nw) {
  const map = new Map(); old.forEach((a, i) => map.set(hex(a).join(','), hex(nw[i])));   // later duplicates win, like a Python dict
  const d = Buffer.from(im.d);
  for (let i = 0; i < d.length; i += 4) { const m = map.get(d[i] + ',' + d[i + 1] + ',' + d[i + 2]); if (m) { d[i] = m[0]; d[i + 1] = m[1]; d[i + 2] = m[2]; } }
  return { w: im.w, h: im.h, d };
}
function pyRound(x) { const f = Math.floor(x), diff = x - f; if (diff < 0.5) return f; if (diff > 0.5) return f + 1; return f % 2 === 0 ? f : f + 1; }
function rgbToHls(r, g, b) {
  const maxc = Math.max(r, g, b), minc = Math.min(r, g, b), sumc = maxc + minc, rangec = maxc - minc, l = sumc / 2;
  if (minc === maxc) return [0, l, 0];
  const s = l <= 0.5 ? rangec / sumc : rangec / (2 - maxc - minc);
  const rc = (maxc - r) / rangec, gc = (maxc - g) / rangec, bc = (maxc - b) / rangec; let h;
  if (r === maxc) h = bc - gc; else if (g === maxc) h = 2 + rc - bc; else h = 4 + gc - rc;
  h = (h / 6); h = h - Math.floor(h); return [h, l, s];
}
function v(m1, m2, hue) { hue = hue - Math.floor(hue); if (hue < 1 / 6) return m1 + (m2 - m1) * hue * 6; if (hue < 0.5) return m2; if (hue < 2 / 3) return m1 + (m2 - m1) * (2 / 3 - hue) * 6; return m1; }
function hlsToRgb(h, l, s) { if (s === 0) return [l, l, l]; const m2 = l <= 0.5 ? l * (1 + s) : l + s - l * s, m1 = 2 * l - m2; return [v(m1, m2, h + 1 / 3), v(m1, m2, h), v(m1, m2, h - 1 / 3)]; }
function dye(kind, name, variant) {
  const im = asset(kind, name);
  if (variant === 'original') return im;
  const entry = PAL.masks[kind][name];
  if (!entry.colors.length) return im;
  const target = PAL.palettes[entry.material][variant];
  const rgb = s => hex(s).map(x => x / 255);
  const [hue, light, sat] = rgbToHls(...rgb(target));
  const original = entry.colors.map(c => rgbToHls(...rgb(c)));
  const center = (Math.min(...original.map(o => o[1])) + Math.max(...original.map(o => o[1]))) / 2;
  const mapping = original.map(([, l]) => {
    const nl = Math.max(0.12, Math.min(0.91, light + (l - center) * 0.94));
    const [r, g, b] = hlsToRgb(hue, nl, sat);
    return [r, g, b].map(x => pyRound(x * 255).toString(16).padStart(2, '0')).join('');
  });
  return recolor(im, entry.colors, mapping);
}
function occludeHair(hair, hat) {
  if (hat === 'none') return hair; const lines = OCC[hat]; if (!lines) return hair;
  const out = { w: hair.w, h: hair.h, d: Buffer.from(hair.d) };
  lines.forEach((yl, x) => { if (yl === null) return; const ymax = Math.min(out.h, yl + 1); for (let y = 0; y < ymax; y++) { const o = (y * out.w + x) * 4; if (out.d[o + 3]) { out.d[o] = out.d[o + 1] = out.d[o + 2] = out.d[o + 3] = 0; } } });
  return out;
}
function composite(dst, src) {   // alpha_composite onto an opaque base; checkPartial() below proves the artwork only has alpha 0/255
  for (let i = 0; i < dst.length; i += 4) { const a = src.d[i + 3]; if (!a) continue;
    if (a === 255) { dst[i] = src.d[i]; dst[i + 1] = src.d[i + 1]; dst[i + 2] = src.d[i + 2]; dst[i + 3] = 255; }
    else throw new Error('partial alpha in artwork'); }
}
function rect(d, x0, y0, x1, y1, c) { for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++) if (x === x0 || x === x1 || y === y0 || y === y1) { const o = (y * 48 + x) * 4; d[o] = c[0]; d[o + 1] = c[1]; d[o + 2] = c[2]; d[o + 3] = 255; } }
function render(a) {
  const d = Buffer.alloc(48 * 48 * 4); const bg = hex('4c516e'); for (let i = 0; i < d.length; i += 4) { d[i] = bg[0]; d[i + 1] = bg[1]; d[i + 2] = bg[2]; d[i + 3] = 255; }
  composite(d, recolor(asset('faces', a.face), SKINS.peach, SKINS[a.skin]));
  composite(d, recolor(asset('age', a.age || 'young'), SKINS.peach, SKINS[a.skin]));
  composite(d, dye('clothing', a.clothing, a.clothing_color || 'original'));
  composite(d, occludeHair(recolor(asset('hair', a.hair), HAIRS.brown, HAIRS[a.hair_color]), a.headwear));
  composite(d, recolor(asset('brows', a.brows), HAIRS.brown, HAIRS[a.hair_color]));
  composite(d, recolor(asset('beards', a.beard), HAIRS.brown, HAIRS[a.hair_color]));
  composite(d, asset('eyes', a.eyes));
  composite(d, dye('headwear', a.headwear, a.headwear_color || 'original'));
  rect(d, 0, 0, 47, 47, hex('30242a')); rect(d, 1, 1, 46, 46, hex('c39a55'));
  return d;
}

// ---------- recipes ----------
for (const k of fs.readdirSync(path.join(SRC, 'assets'))) for (const f of fs.readdirSync(path.join(SRC, 'assets', k))) { const im = asset(k, path.basename(f, '.png')); for (let i = 3; i < im.d.length; i += 4) if (im.d[i] !== 0 && im.d[i] !== 255) throw new Error('partial alpha in ' + k + '/' + f); }
let seed = 12345; const rnd = n => { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return Math.floor((seed / 4294967296) * n); };
const pick = l => l[rnd(l.length)];
const colorsFor = (kind, name) => { const m = PAL.masks[kind][name]; return m.colors.length ? ['original', ...Object.keys(PAL.palettes[m.material])] : ['original']; };
const clothing = Object.keys(PAL.masks.clothing), headwear = Object.keys(PAL.masks.headwear);
const beards = defs.beard_styles, hairs = defs.hair_styles, skins = Object.keys(SKINS), hcols = Object.keys(HAIRS), ages = defs.ages;
const mk = o => { const c = pick(clothing), h = pick(headwear); const r = { version: 19, seed: 0, climate: 'temperate', archetype: '', face: 'standard', skin: pick(skins), hair: pick(hairs), hair_color: pick(hcols), brows: pick(['straight', 'short', 'thick']), eyes: pick(['standard', 'round', 'narrow', 'heavy_lidded']), beard: pick(beards), clothing: c, headwear: h, age: pick(ages), ...o };
  r.clothing_color = pick(colorsFor('clothing', r.clothing)); r.headwear_color = pick(colorsFor('headwear', r.headwear)); return r; };
const recipes = [];
JSON.parse(fs.readFileSync(path.join(SRC, 'examples.json'), 'utf8')).forEach(e => recipes.push({ ...e, archetype: e.archetype || '' }));
for (const h of headwear) for (const hair of hairs) recipes.push(mk({ headwear: h, hair }));          // every hat over every hairstyle
for (const c of clothing) recipes.push(mk({ clothing: c }));
for (const b of beards) for (const s of skins) recipes.push(mk({ beard: b, skin: s }));
for (const a of ages) for (const s of skins) recipes.push(mk({ age: a, skin: s }));
for (const hc of hcols) for (const hair of hairs) recipes.push(mk({ hair_color: hc, hair }));
for (const c of clothing) for (const col of colorsFor('clothing', c)) recipes.push(mk({ clothing: c, clothing_color: col }));
for (const h of headwear) for (const col of colorsFor('headwear', h)) recipes.push(mk({ headwear: h, headwear_color: col }));
recipes.forEach(r => { if (r.clothing_color === undefined) r.clothing_color = 'original'; });

fs.mkdirSync(OUT, { recursive: true });
const cols = 25, rows = Math.ceil(recipes.length / cols), sheet = Buffer.alloc(cols * 48 * rows * 48 * 4);
recipes.forEach((r, i) => { const d = render(r), ox = (i % cols) * 48, oy = Math.floor(i / cols) * 48;
  for (let y = 0; y < 48; y++) d.copy(sheet, ((oy + y) * cols * 48 + ox) * 4, y * 48 * 4, (y + 1) * 48 * 4); });
fs.writeFileSync(path.join(OUT, 'reference_sheet.png'), encodePng(cols * 48, rows * 48, sheet));
fs.writeFileSync(path.join(OUT, 'recipes.json'), JSON.stringify({ columns: cols, recipes }, null, 0).replace(/\},\{/g, '},\n{'));
console.log('recipes', recipes.length, 'sheet', cols * 48, 'x', rows * 48);
