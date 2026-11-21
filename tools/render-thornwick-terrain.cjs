const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

const out = process.argv[2] || path.join(__dirname, 'terrain-draft');
const tex = path.join(out, 'assets');
const cells = path.join(out, 'ground-cells');
fs.mkdirSync(tex, { recursive: true });
fs.mkdirSync(cells, { recursive: true });

const crcTable = Array.from({ length: 256 }, (_, n) => {
  for (let i = 0; i < 8; i++) n = n & 1 ? 0xedb88320 ^ (n >>> 1) : n >>> 1;
  return n >>> 0;
});
function chunk(type, bytes) {
  const tag = Buffer.from(type);
  const result = Buffer.alloc(12 + bytes.length);
  result.writeUInt32BE(bytes.length);
  tag.copy(result, 4);
  bytes.copy(result, 8);
  let crc = 0xffffffff;
  for (const c of result.subarray(4, result.length - 4)) crc = crcTable[(crc ^ c) & 255] ^ (crc >>> 8);
  result.writeUInt32BE((crc ^ 0xffffffff) >>> 0, result.length - 4);
  return result;
}
function save(file, width, height, pixels) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 6;
  const rows = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) pixels.copy(rows, (width * 4 + 1) * y + 1, width * y * 4, width * (y + 1) * 4);
  fs.writeFileSync(file, Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', header),
    chunk('IDAT', zlib.deflateSync(rows)),
    chunk('IEND', Buffer.alloc(0)),
  ]));
}

const rgb = hex => hex.match(/../g).map(value => parseInt(value, 16));
const shade = (palette, index) => palette[Math.max(0, Math.min(palette.length - 1, index))];
function hash(x, y, salt) {
  let n = Math.imul(x + salt * 13, 0x45d9f3b) ^ Math.imul(y + salt * 41, 0x119de1f3);
  n = Math.imul(n ^ (n >>> 16), 0x45d9f3b);
  return ((n ^ (n >>> 16)) >>> 0) / 4294967296;
}
function put(pixels, width, height, x, y, color) {
  if (x < 0 || y < 0 || x >= width || y >= height) return;
  const i = (y * width + x) * 4;
  pixels[i] = color[0];
  pixels[i + 1] = color[1];
  pixels[i + 2] = color[2];
  pixels[i + 3] = 255;
}
const meadow = ['52683d','5c7443','657e48','6c874f','748e56','7b975b','879e62'].map(rgb);
const fertile = ['49482f','555735','5b6237','626b3b','687241','717b47','7c854f'].map(rgb);
function ground(name, isFertile, salt) {
  const palette = isFertile ? fertile : meadow;
  const cell = Buffer.alloc(32 * 16 * 4);
  for (let y = 0; y < 16; y++) for (let x = 0; x < 32; x++) {
    const edge = Math.abs(x + .5 - 16) / 16 + Math.abs(y + .5 - 8) / 8;
    const fleck = hash(x, y, salt);
    const patch = hash(Math.floor((x + y + salt) / 5), Math.floor((2 * y - x + salt) / 7), salt + 8);
    const mark = patch < .18 ? -1 : patch > .82 ? 1 : 0;
    let level = 3 + (edge < .89 ? mark : 0);
    if (fleck > .88) level++;
    else if (fleck < .09) level--;
    if (edge < .78 && isFertile && fleck < .044) level = patch < .45 ? 0 : 1;
    if (edge < .76 && !isFertile && fleck > .97 && patch > .63) level = 6;
    put(cell, 32, 16, x, y, shade(palette, level));
  }
  save(path.join(cells, name + '.png'), 32, 16, cell);
  const width = 2048, height = 1024, diamond = Buffer.alloc(width * height * 4);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (Math.abs((x + .5) / 1024 - 1) + Math.abs((y + .5) / 512 - 1) > 1.001) continue;
    const from = ((y >> 6) * 32 + (x >> 6)) * 4;
    const to = (y * width + x) * 4;
    diamond[to] = cell[from];
    diamond[to + 1] = cell[from + 1];
    diamond[to + 2] = cell[from + 2];
    diamond[to + 3] = 255;
  }
  save(path.join(tex, name + '.png'), width, height, diamond);
}

const species = [
  { id:0, name:'oak', form:'round', w:24, h:31, colors:['243323','34482c','425834','51683b','647d45','78944d','95a662'] },
  { id:6, name:'old_oak', form:'round', w:26, h:35, colors:['293323','3b482c','4e5933','606a3a','718047','8c954b','a6a65e'] },
  { id:2, name:'pine', form:'needle', w:21, h:33, colors:['243632','30473f','3a594a','456858','537961','6a9070','83a07c'] },
  { id:3, name:'spruce', form:'needle', w:17, h:36, colors:['222f2c','2b423a','365248','3e6052','52796b','6b8c77','839f87'] },
  { id:7, name:'fir', form:'needle', w:22, h:38, colors:['1f3430','2a4840','35584a','426856','527961','6e8c68','8d9f7c'] },
  { id:4, name:'birch', form:'column', w:16, h:32, pale:true, colors:['303b29','465837','546941','687d49','829253','9caa65','b8bd7e'] },
  { id:9, name:'aspen', form:'column', w:18, h:35, pale:true, colors:['363c27','4d5a32','65693b','7c8147','95944b','aaa858','c3b976'] },
  { id:5, name:'willow', form:'drape', w:27, h:30, colors:['293827','3b5034','4b6240','5e7647','718f57','8da164','a8b27b'] },
  { id:8, name:'maple', form:'round', w:24, h:31, colors:['423226','65402a','845332','9d6135','b9793d','c9914b','d3aa62'] },
];
const bark = ['35291e','51402b','705537','906e43','a48354'].map(rgb);
const pale = ['474439','696658','958c70','bbb397','d4caad'].map(rgb);

function trunk(pixels, cx, foot, height, breadth, lightBark, salt, split) {
  const palette = lightBark ? pale : bark;
  for (let y = foot - height; y <= foot; y++) {
    const t = (foot - y) / height;
    const x0 = cx + Math.round(Math.sin(t * 2.8) * (salt % 3 - 1));
    const flare = y >= foot - 2 ? (foot - y < 1 ? 2 : 1) : 0;
    const half = Math.max(0, Math.round((breadth * (1 - .58 * t) - 1) / 2)) + flare;
    for (let x = x0 - half; x <= x0 + half; x++) {
      const r = hash(x, y, salt);
      const grayKnot = lightBark && (y + (x % 2)) % 5 === 0;
      let v = x === x0 - half ? 2 : x >= x0 + half ? 1 : r > .75 ? 4 : 3;
      if (grayKnot) v = 0;
      else if (r < .1) v--;
      put(pixels, 32, 48, x, y, shade(palette, v));
    }
  }
  if (!split) return;
  for (let y = foot - height; y < foot - height * .48; y++) {
    const t = (foot - y) / height;
    const reach = Math.round((t - .37) * breadth * 2.3);
    put(pixels, 32, 48, cx - reach, y, palette[2]);
    put(pixels, 32, 48, cx + reach, y + 1, palette[1]);
  }
}

function lobe(pixels, cx, cy, rx, ry, palette, salt, light) {
  for (let y = Math.floor(cy - ry); y <= cy + ry; y++) for (let x = Math.floor(cx - rx); x <= cx + rx; x++) {
    const u = (x + .5 - cx) / rx, v = (y + .5 - cy) / ry;
    const r = hash(x, y, salt);
    const d = u * u + v * v;
    if (d > .92 + r * .2) continue;
    const texture = hash(Math.floor((x + y) / 2), Math.floor(y / 2), salt + 17);
    const lightValue = 3.55 - u * .88 - v * 1.29 + light + (texture - .5) * 1.32;
    let level = Math.floor(lightValue + (r > .91 ? 1 : 0));
    if (d > .68 && u * .6 + v > .34) level -= 2;
    if (d > .80 && v < -.16 && r > .59) level++;
    put(pixels, 32, 48, x, y, shade(palette, level));
  }
}

function broadleaf(pixels, cx, foot, width, height, form, palette, salt, lightBark) {
  const top = foot - height + 1;
  trunk(pixels, cx, foot, Math.round(height * .58), Math.max(2, Math.round(width * .18)), lightBark, salt, form === 'round');
  const layers = form === 'column'
    ? [[-.10,.34,.32,.31,-.4],[.16,.43,.35,.22,-.3],[-.18,.53,.32,.18,.2],[.19,.27,.31,.23,.2],[-.15,.24,.34,.22,.55],[.03,.17,.27,.18,.9]]
    : form === 'drape'
    ? [[.22,.46,.26,.26,-.8],[-.26,.44,.24,.25,-.55],[.34,.42,.18,.25,-.2],[-.38,.38,.18,.22,.1],[.00,.25,.47,.22,.25],[-.13,.19,.32,.19,.55],[.14,.23,.31,.16,.45]]
    : [[.24,.48,.28,.22,-.85],[-.23,.51,.28,.19,-.7],[.28,.35,.24,.18,-.24],[-.29,.35,.25,.2,.0],[.01,.42,.27,.23,.05],[.13,.22,.26,.19,.36],[-.16,.22,.27,.18,.72],[-.03,.15,.26,.16,.8],[-.28,.38,.15,.11,.48]];
  for (let index = 0; index < layers.length; index++) {
    const [x,y,rx,ry,light] = layers[index];
    lobe(pixels, cx + width * x, top + height * y, width * rx, height * ry, palette, salt + index * 9, light);
  }
}

function conifer(pixels, cx, foot, width, height, palette, salt, speciesId) {
  const top = foot - height + 1;
  trunk(pixels, cx, foot, Math.round(height * .84), 2, false, salt, false);
  const bands = speciesId === 2 ? [0,.19,.37,.55,.69] : [0,.17,.33,.49,.64,.75];
  for (let i = bands.length - 1; i >= 0; i--) {
    const level = bands[i];
    const y0 = top + Math.round(height * level);
    const y1 = Math.min(foot - 4, y0 + Math.round(height * (i === 0 ? .37 : .28)));
    const half = Math.max(2, width * (.20 + .42 * level));
    const lean = Math.round((hash(i, 0, salt) - .56) * 3);
    for (let y = y0; y <= y1; y++) {
      const v = (y - y0) / Math.max(1, y1 - y0);
      for (let x = Math.floor(cx - half - 1); x <= cx + half + 1; x++) {
        const u = (x + .5 - cx - lean * (1 - v)) / half;
        const r = hash(x, y, salt + 21 * i);
        const widthAt = .085 + v * .98 + r * .14;
        const hem = v > .84 && Math.abs(u) < .72 && r < .43;
        if (Math.abs(u) > widthAt || hem) continue;
        const needle = (x + y * 2 + i) % 5 === 0;
        let color = Math.floor(4.4 - u * 1.5 - v * .75 + (r - .5) * 1.55);
        if (v > .78) color -= 2;
        if (needle && v > .25 && v < .8 && u < .32) color++;
        put(pixels, 32, 48, x, y, shade(palette, color));
      }
    }
  }
}
function seedAt(seed, tier) { return seed + 21 * tier; }

const groveLayout = [
  [],
  [[16,44,1]],
  [[11,39,.67],[21,44,.66]],
  [[16,36,.60],[9,43,.61],[22,45,.63]],
  [[12,35,.53],[23,39,.51],[8,44,.55],[20,46,.59]],
];
function drawTree(type, count) {
  const pixels = Buffer.alloc(32 * 48 * 4);
  const palette = type.colors.map(rgb);
  groveLayout[count].forEach(([x, y, scale], n) => {
    const variation = count === 1 ? 0 : (hash(n, count, type.id) - .49) * .12;
    const width = Math.max(8, Math.min(25, Math.round(type.w * (scale + variation))));
    const height = Math.max(12, Math.round(type.h * (scale - variation)));
    const seed = 161 + type.id * 17 + n * 45 + count * 8;
    const half = Math.ceil(width * .60);
    const center = count === 1 ? x : Math.min(29 - half, Math.max(2 + half, x));
    if (type.form === 'needle') conifer(pixels, center, y, width, height, palette, seed, type.id);
    else broadleaf(pixels, center, y, width, height, type.form, palette, seed, type.pale);
  });
  const name = count === 1 ? 'tree_' + type.id : 'tree_' + type.name + '_' + count;
  save(path.join(tex, name + '.png'), 32, 48, pixels);
}
for (let variant = 0; variant < 4; variant++) ground('grass_' + variant, false, 130 + variant * 37);
for (let variant = 0; variant < 2; variant++) ground('grassfert_' + variant, true, 279 + variant * 17);
for (const type of species) for (let count = 1; count <= 4; count++) drawTree(type, count);
console.log('Rendered 6 ground tiles, 9 single trees and 27 groves.');
