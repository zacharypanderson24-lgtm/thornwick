const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

const out = process.argv[2] || path.join(__dirname, 'geology-draft');
fs.mkdirSync(out, { recursive: true });

function png(file, width, height, rgba) {
  function chunk(type, bytes) {
    const content = Buffer.concat([Buffer.from(type), bytes]);
    const size = Buffer.alloc(4), trailer = Buffer.alloc(4);
    size.writeUInt32BE(bytes.length);
    let crc = -1;
    for (const byte of content) {
      crc ^= byte;
      for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
    trailer.writeUInt32BE((crc ^ -1) >>> 0);
    return Buffer.concat([size, content, trailer]);
  }
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 6;
  const rows = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) rgba.copy(rows, (width * 4 + 1) * y + 1, width * y * 4, width * (y + 1) * 4);
  fs.writeFileSync(path.join(out, file + '.png'), Buffer.concat([
    Buffer.from('89504e470d0a1a0a', 'hex'), chunk('IHDR', header),
    chunk('IDAT', zlib.deflateSync(rows)), chunk('IEND', Buffer.alloc(0)),
  ]));
}

const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
const blend = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * t));
const colors = hex => hex.split(' ').map(s => s.match(/../g).map(n => parseInt(n, 16)));
const granite = colors('363e3c 4b5550 606962 737b70 8b9282 a1a697 b9bbaa');
const ore = colors('283239 37454b 465659 596668 6d7878 879091 a2aaaa');
const oxide = colors('543b2e 795033 a1643b b87a47 ce955b e3b576');
const moss = colors('3d4735 505d3a 657444 7c8a51 92975e');
const mountain = colors('414b4c 515b5b 616b69 737c76 858d82 9b9f91 b0b1a1');
const snow = colors('626f74 808e93 9ba9ad b5c1c3 ced9d8 e2e8e4 f1f4ed');
const pit = colors('302e26 3e4035 4f5144 606153 757465 8a8777 9e9985');

function hash(x, y, z) {
  let value = Math.imul(x + 7, 0x45d9f3b) ^ Math.imul(y + 23, 0x119de1f3) ^ Math.imul(z + 5, 0x27d4eb2d);
  value = Math.imul(value ^ (value >>> 16), 0x45d9f3b);
  return ((value ^ (value >>> 16)) >>> 0) / 4294967296;
}
function paint(pixels, index, color) {
  pixels.set(color, index * 4);
  pixels[index * 4 + 3] = 255;
}

function authoredBoulder(faces, model) {
  const { x, y, width, depth, height, turn, seed, iron } = model;
  const ring = (r, lift, phase, lean) => Array.from({ length: 7 }, (_, i) => {
    const a = i * Math.PI * 2 / 7 + turn + phase;
    const variation = .83 + hash(i, seed, 11) * .24;
    return [x + Math.cos(a) * width * r * variation + lean, y + Math.sin(a) * depth * r * variation - lean * .45,
      lift + (lift === 0 ? 0 : (hash(i, seed, 16) - .5) * height * .24)];
  });
  const tier = [ring(1, 0, 0, 0), ring(1.02, height * .44, .13, .1), ring(.69, height * .86, -.16, -.7)];
  const crown = [x - width * .25, y - depth * .19, height];
  for (let j = 0; j < 7; j++) {
    const k = (j + 1) % 7;
    for (let t = 0; t < 2; t++) {
      const [a, b, c, d] = [tier[t][j], tier[t][k], tier[t + 1][k], tier[t + 1][j]];
      faces.push({ points: [a,b,c], seed, iron });
      faces.push({ points: [a,c,d], seed, iron });
    }
    faces.push({ points: [tier[2][j], tier[2][k], crown], seed, iron });
  }
}

function project([x,y,z], origin) {
  return [16 + y - x, origin + (x + y) / 2 - z, x + y + z];
}

function rockShade(x, y, z, n, object) {
  const grit = hash(Math.floor(x * 1.4), Math.floor(y * 1.4), Math.floor(z * 2.2));
  const press = Math.sin((z + .40 * x + .15 * y) * 1.42 + object.seed * .08);
  const fracture = press > .84 && hash(Math.floor(x / 2), Math.floor(y / 2), object.seed) < .76;
  const sunlight = n[0] * .6 - n[1] * .7 + n[2] * .9;
  let level = Math.floor(3.15 + sunlight * 1.85 + (grit - .45) * 1.33);
  if (fracture) level--;
  if (press < -.86 && sunlight > .62) level++;
  if (z < .85) level--;
  const base = (object.iron ? ore : granite)[clamp(level,0,6)];
  if (object.iron) {
    const line = z + x * .69 - y * .28 + Math.sin(y * .91 + object.seed) * .75;
    const position = Math.abs(line - (8.2 + (object.seed % 3 - 1) * .77));
    const seam = position < .75 || (position < 1.75 && grit > .62);
    if (seam) return oxide[clamp(Math.floor(2.5 + sunlight * 1.1 + grit * 1.7), 0, 5)];
    if (fracture && grit > .88 && sunlight > .40) return blend(base, [161,173,169], .46);
  } else if (z < 2.65 && grit > .42 && hash(Math.floor(x / 2.7), Math.floor(y / 2.7), object.seed) > .63) {
    return blend(base, moss[clamp(level - 1, 0, 4)], .70);
  }
  return base;
}

function drawCluster(name, boulders, iron) {
  const pixels = Buffer.alloc(32 * 48 * 4);
  const depth = new Float64Array(32 * 48).fill(-Infinity);
  const faces = [];
  boulders.forEach(([x,y,w,d,h,turn], i) =>
    authoredBoulder(faces, {x, y, width:w, depth:d, height:h, turn, iron, seed:i * 11 + (name.endsWith('1') ? 22 : 13)}));
  const bottom = Math.max(...faces.flatMap(face => face.points.map(([x,y,z]) => (x+y)/2-z)));
  const origin = 46 - bottom;
  for (const face of faces) {
    const [a,b,c] = face.points;
    const u = b.map((value, i) => value - a[i]), v = c.map((value, i) => value - a[i]);
    const raw = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]];
    const length = Math.hypot(...raw);
    if (length < 1e-8) continue;
    const normal = raw.map(value => value / length);
    const [p,q,r] = face.points.map(point => project(point, origin));
    const area = (q[1]-r[1])*(p[0]-r[0]) + (r[0]-q[0])*(p[1]-r[1]);
    if (Math.abs(area) < 1e-8) continue;
    for (let y = Math.max(0, Math.floor(Math.min(p[1], q[1], r[1]))); y <= Math.min(47, Math.ceil(Math.max(p[1],q[1],r[1]))); y++) {
      for (let x = Math.max(0, Math.floor(Math.min(p[0],q[0],r[0]))); x <= Math.min(31, Math.ceil(Math.max(p[0],q[0],r[0]))); x++) {
        const sx = x + .5 - r[0], sy = y + .5 - r[1];
        const a1 = ((q[1]-r[1])*sx + (r[0]-q[0])*sy) / area;
        const b1 = ((r[1]-p[1])*sx + (p[0]-r[0])*sy) / area;
        const c1 = 1 - a1 - b1;
        if (Math.min(a1,b1,c1) < -1e-7) continue;
        const index = y*32+x, z = p[2]*a1+q[2]*b1+r[2]*c1;
        if (z < depth[index]) continue;
        depth[index] = z;
        const sample = a.map((value, i) => value * a1 + b[i] * b1 + c[i] * c1);
        paint(pixels, index, rockShade(...sample, normal, face));
      }
    }
  }
  png(name, 32, 48, pixels);
}

function surface(name, snowCap, worked) {
  const pixels = Buffer.alloc(32 * 16 * 4);
  for (let y = 0; y < 16; y++) for (let x = 0; x < 32; x++) {
    const warp = hash(Math.floor(x / 6), Math.floor(y / 4), 77);
    const angle = (y + x * .23 + warp * 1.55);
    const strata = angle - Math.floor(angle / 4.3) * 4.3;
    const joint = hash(Math.floor((x - y*.72) / 4.5), Math.floor(angle / 4.3), 62);
    const grit = hash(x, y, 57);
    let level = 3 + (joint > .75 ? 1 : joint < .22 ? -1 : 0);
    if (strata < .65 && joint > .20) level -= 2;
    else if (strata < 1.48) level++;
    if (grit > .94) level++;
    if (grit < .08) level--;
    let color = (worked ? pit : mountain)[clamp(level,0,6)];
    if (snowCap) {
      const dust = strata > .65 || joint > .54;
      color = dust ? snow[clamp(level + 1,0,6)] : blend(color, snow[2], .27);
    } else if (!worked && grit > .82 && joint < .21) {
      color = blend(color, moss[2], .26);
    }
    paint(pixels, y * 32 + x, color);
  }
  png(name, 32, 16, pixels);
}

drawCluster('rock_0', [[12,12,4.2,3.4,5.1,.13],[11.3,16.1,2.8,3,3.3,-.48],[16.1,13.1,2.4,2.25,2.6,.52],[15.1,16.5,1.55,1.9,1.6,-.31]], false);
drawCluster('rock_1', [[11.7,12.5,5.6,5.1,9.5,.42],[15.3,17.4,4.2,3.6,5.5,-.17],[7.9,16.9,2.9,3.4,4.1,.94],[17.4,11.2,3.6,2.7,4.2,-.80]], false);
drawCluster('ore_0', [[13,12.8,6.0,4.0,7.4,.59],[10.3,17.5,3.7,2.7,4.9,-.4],[16.2,17.4,2.7,2.3,3.0,1.1],[18.2,13.5,1.9,1.7,3.0,.7]], true);
drawCluster('ore_1', [[10.5,13.5,4.2,5.4,8.8,.18],[16.3,14.1,4.0,4.4,6.3,-.36],[13.6,18.2,2.7,2.6,3.4,.8],[18.9,10.1,2.5,2.8,3.3,1.1]], true);
surface('mountain_rock', false, false);
surface('mountain_snow', true, false);
surface('worked_stone', false, true);
console.log('Rendered four object sprites and three 32×16 surface cells.');
