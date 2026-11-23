const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'boreal-forest');const assets=path.join(out,'assets'),cells=path.join(out,'ground-cells');fs.mkdirSync(assets,{recursive:true});fs.mkdirSync(cells,{recursive:true});
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


let files=[],grounds=[],treeImages=[];
const palettes={meadow:['354b38','3c5440','445e46','4d674c','597253'].map(rgb),fertile:['454331','4c4a36','55523b','605c42','6a6549'].map(rgb),dry:['66725a','717c63','7c876c','899277','969e82'].map(rgb)};
function terrain(kind,variant){let p=Buffer.alloc(32*16*4),pal=palettes[kind],salt=217+variant*31+kind.length*17;for(let y=0;y<16;y++)for(let x=0;x<32;x++){
 let edge=Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8),patch=hash(Math.floor((x+y)/5),Math.floor((y*2-x)/7),salt),h=hash(x,y,salt),k=edge>.78?2:patch<.28?1:patch>.78?3:2;
 if(edge<.74&&h>.93)k=Math.min(4,k+1);if(edge<.74&&h<.045)k=Math.max(0,k-1);
 put(p,32,16,x,y,pal[k]);
 // Tiny tufts and occasional cream meadow flowers, never a flower carpet.
 if(kind==='fertile'&&edge<.65&&h>.93){put(p,32,16,x,y,rgb('847554'));if(h>.975)put(p,32,16,x+1,y,rgb('706446'));}
 if(kind==='dry'&&edge<.65&&h>.88)put(p,32,16,x,y,rgb(h>.96?'abb091':'929c7d'));
 if(kind==='meadow'&&edge<.65&&h>.985)put(p,32,16,x,y,rgb('73854b'));
 }let name='boreal_'+({meadow:'moss',fertile:'pine_needles',dry:'lichen'}[kind])+'_'+variant;save(path.join(cells,name+'.png'),32,16,p);files.push('ground-cells/'+name+'.png');
 let diamond=Buffer.from(p);for(let y=0;y<16;y++)for(let x=0;x<32;x++)if(Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8)>1)diamond[(y*32+x)*4+3]=0;
 save(path.join(assets,name+'.png'),32,16,diamond);files.push('assets/'+name+'.png');grounds.push({name,p:diamond});return diamond;
}
for(let i=0;i<4;i++)terrain('meadow',i);for(let i=0;i<2;i++)terrain('fertile',i);for(let i=0;i<2;i++)terrain('dry',i);
const species=[{name:'spruce',conifer:true,id:2,w:23,h:39,colors:['1e322d','29443a','355647','426b55','557e61','6a916f','86a382']},{name:'pine',conifer:true,id:1,w:21,h:37,colors:['293b32','354e3f','42614c','557657','6c8a64','849e75','9eaf87']},{name:'birch',form:'column',w:14,h:32,pale:true,colors:['3b4831','4d5e3b','607447','778b53','8e9d63','a5af75','bac28d']}];
const layout={1:[[16,44,1]],2:[[11,39,.7],[21,44,.69]],3:[[16,36,.62],[9,43,.62],[22,45,.64]],4:[[12,35,.54],[23,39,.52],[8,44,.56],[20,46,.6]]};
function tree(type,variant=0,count=1,sapling=false){let p=Buffer.alloc(32*48*4),pal=type.colors.map(rgb),scale0=sapling?.52:1;
 for(const[cx,foot,s]of layout[count]){let w=Math.round(type.w*s*scale0),h=Math.round(type.h*s*scale0),seed=137+variant*61+count*11+cx;
 let center=Math.max(Math.ceil(w/2)+1,Math.min(30-Math.ceil(w/2),cx));
 if(type.conifer)conifer(p,center,foot,w,h,pal,seed,type.id);else broadleaf(p,center,foot,w,h,type.form,pal,seed,type.pale);}
 let name='boreal_'+type.name+(sapling?'_sapling':count>1?'_grove_'+count:'_'+variant);save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');treeImages.push({name,p});return p;
}
for(const t of species){tree(t,0);tree(t,1);for(let n=2;n<=4;n++)tree(t,0,n);tree(t,0,1,true);}
for(let i=0;i<3;i++){let p=Buffer.alloc(32*48*4),pal=['303e33','3e5040','50654b','647b58','809068'].map(rgb);lobe(p,15,42,5+i,3.2,pal,400+i*21,.25);lobe(p,12,41,3.5,2.8,pal,470+i*21,.5);let name='boreal_shrub_'+i;save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');treeImages.push({name,p});}
function blit(dst,w,h,p,pw,ph,ox,oy){for(let y=0;y<ph;y++)for(let x=0;x<pw;x++){let a=(y*pw+x)*4,xx=x+ox,yy=y+oy;if(p[a+3]&&xx>=0&&yy>=0&&xx<w&&yy<h)p.copy(dst,(yy*w+xx)*4,a,a+4);}}
function scale(p,w,h,s,file){let b=Buffer.alloc(w*h*s*s*4);for(let y=0;y<h*s;y++)for(let x=0;x<w*s;x++){let i=(Math.floor(y/s)*w+Math.floor(x/s))*4;p.copy(b,(y*w*s+x)*4,i,i+4);}save(file,w*s,h*s,b);}
let scene=Buffer.alloc(320*208*4);let objects=[];
for(let j=0;j<8;j++)for(let i=0;i<10;i++){let x=128+(i-j)*16,y=48+(i+j)*8,h=hash(i,j,227);let g=h<.24?grounds[6+(i+j)%2]:h<.57?grounds[4+(i+j)%2]:grounds[(i+j*3)%4];blit(scene,320,208,g.p,32,16,x,y);
 if(h>.34&&!(i>3&&i<7&&j>2&&j<5)){let speciesIndex=(i*7+j*3)%9<4?0:(i+j)%5===0?2:1;let grove=h>.85?2:0;objects.push({x,y:y-32,p:treeImages[speciesIndex*6+grove].p});}
 else if(h>.15&&h<.26)objects.push({x,y:y-32,p:treeImages[18+(i+j)%3].p});}

for(const o of objects.sort((a,b)=>a.y-b.y))blit(scene,320,208,o.p,32,48,o.x,o.y);
scale(scene,320,208,3,path.join(out,'boreal_landscape.png'));
let sheet=Buffer.alloc(256*136*4);for(let i=0;i<8;i++)blit(sheet,256,136,grounds[i].p,32,16,(i%4)*64+16,Math.floor(i/4)*24+4);for(const[i,t]of [treeImages[0],treeImages[6],treeImages[12],treeImages[2]].entries())blit(sheet,256,136,t.p,32,48,i*64+16,64);scale(sheet,256,136,3,path.join(out,'boreal_assets.png'));
fs.writeFileSync(path.join(out,'biome.json'),JSON.stringify({biome:'BorealForest',ground:grounds.map(g=>g.name),treeSpecies:['spruce','pine','birch'],treeDensity:'Conifer-dominated woodland with scattered birch and lichen clearings',files},null,2));
console.log('Generated '+files.length+' native texture files: 8 atlas cells, 8 isometric ground tiles, 18 tree/grove/sapling sprites and 3 shrubs.');
