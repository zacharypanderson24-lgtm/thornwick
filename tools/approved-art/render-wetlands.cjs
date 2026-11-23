const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'wetlands');const assets=path.join(out,'assets'),cells=path.join(out,'ground-cells');fs.mkdirSync(assets,{recursive:true});fs.mkdirSync(cells,{recursive:true});
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


// Species-specific silhouettes: hanging willow curtains and multi-stem alder.
function wetTree(p,cx,foot,w,h,type,pal,seed){
 if(type.form==='willow'){
  trunk(p,cx,foot,Math.round(h*.71),3,type.pale,seed,true);
  const top=foot-h;
  for(let n=0;n<7;n++){let xx=cx+w*(n/6-.5)*.8,yy=top+h*(.23+Math.abs(n-3)*.045);
   lobe(p,xx,yy,w*.22,h*.19,pal,seed+n*13,.25);
   for(let k=0;k<3;k++){let x=Math.round(xx+(k-1)*w*.08),end=Math.round(yy+h*(.29+hash(n,k,seed)*.17));
    for(let y=Math.round(yy);y<=end;y++){let drift=Math.round(Math.sin((y-yy)*.3+n)*.7);put(p,32,48,x+drift,y,shade(pal,2+(x+y+n)%3));if((y+n)%3!==0)put(p,32,48,x+drift+1,y,shade(pal,3+(n%2)));}}
  }
 }else{
  for(let n=0;n<3;n++){let x=cx+(n-1)*Math.max(2,Math.round(w*.13));trunk(p,x,foot,Math.round(h*(.56+n*.06)),1,false,seed+n,false);}
  const top=foot-h;for(let n=0;n<7;n++)lobe(p,cx+w*([-.2,.18,0,-.25,.24,-.1,.12][n]),top+h*([.47,.44,.24,.31,.27,.15,.13][n]),w*.26,h*.2,pal,seed+n*11,n*.12);
 }
}
let files=[],grounds=[],treeImages=[];
const palettes={meadow:['3c5135','445c3b','4d6742','59734a','667e54'].map(rgb),fertile:['3b3c30','444536','4e4f3c','595b45','66684f'].map(rgb),dry:['3d5550','45615a','4e6c62','5c7870','6b867c'].map(rgb)};
function terrain(kind,variant){let p=Buffer.alloc(32*16*4),pal=palettes[kind],salt=217+variant*31+kind.length*17;for(let y=0;y<16;y++)for(let x=0;x<32;x++){
 let edge=Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8),patch=hash(Math.floor((x+y)/5),Math.floor((y*2-x)/7),salt),h=hash(x,y,salt),k=edge>.78?2:patch<.28?1:patch>.78?3:2;
 if(edge<.74&&h>.93)k=Math.min(4,k+1);if(edge<.74&&h<.045)k=Math.max(0,k-1);
 put(p,32,16,x,y,pal[k]);
 // Tiny tufts and occasional cream meadow flowers, never a flower carpet.
 if(kind==='fertile'&&edge<.65&&h>.94)put(p,32,16,x,y,rgb('737159'));
 if(kind==='dry'&&edge<.65&&y%4===variant%4&&h>.55){put(p,32,16,x,y,rgb('75948a'));if(h>.8)put(p,32,16,x+1,y,rgb('75948a'));}
 if(kind==='meadow'&&edge<.65&&h>.985)put(p,32,16,x,y,rgb('73854b'));
 }let name='wetlands_'+({meadow:'marsh_grass',fertile:'peat_mud',dry:'shallow_water'}[kind])+'_'+variant;save(path.join(cells,name+'.png'),32,16,p);files.push('ground-cells/'+name+'.png');
 let diamond=Buffer.from(p);for(let y=0;y<16;y++)for(let x=0;x<32;x++)if(Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8)>1)diamond[(y*32+x)*4+3]=0;
 save(path.join(assets,name+'.png'),32,16,diamond);files.push('assets/'+name+'.png');grounds.push({name,p:diamond});return diamond;
}
for(let i=0;i<4;i++)terrain('meadow',i);for(let i=0;i<2;i++)terrain('fertile',i);for(let i=0;i<2;i++)terrain('dry',i);
const species=[{name:'weeping_willow',form:'willow',w:27,h:36,colors:['293e2d','375235','476640','5b7c4a','729254','8ba664','a6b97b']},{name:'alder',form:'alder',w:22,h:33,colors:['263c2b','345031','456539','587b43','70934f','8ba762','a6b979']},{name:'white_willow',form:'willow',w:24,h:32,pale:true,colors:['3f5140','53654c','687d5b','80936b','99a97e','b0bc94','c4cba7']}];
const layout={1:[[16,44,1]],2:[[11,39,.7],[21,44,.69]],3:[[16,36,.62],[9,43,.62],[22,45,.64]],4:[[12,35,.54],[23,39,.52],[8,44,.56],[20,46,.6]]};
function tree(type,variant=0,count=1,sapling=false){let p=Buffer.alloc(32*48*4),pal=type.colors.map(rgb),scale0=sapling?.52:1;
 for(const[cx,foot,s]of layout[count]){let w=Math.round(type.w*s*scale0),h=Math.round(type.h*s*scale0),seed=137+variant*61+count*11+cx;
 wetTree(p,Math.max(Math.ceil(w/2)+1,Math.min(30-Math.ceil(w/2),cx)),foot,w,h,type,pal,seed);}
 let name='wetlands_'+type.name+(sapling?'_sapling':count>1?'_grove_'+count:'_'+variant);save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');treeImages.push({name,p});return p;
}
for(const t of species){tree(t,0);tree(t,1);for(let n=2;n<=4;n++)tree(t,0,n);tree(t,0,1,true);}
// Marsh vegetation has upright blades and visible brown cattail heads.
for(let i=0;i<6;i++){let p=Buffer.alloc(32*48*4),cattail=i>=3;
 for(let n=0;n<9;n++){let x=7+(n*7+i*3)%20,foot=42+(n%3),height=9+Math.floor(hash(n,i,702)*12);
  for(let y=foot-height;y<=foot;y++){let bend=Math.round((foot-y)/height*((n%3)-1)*3);put(p,32,48,x+bend,y,rgb(n%2?'7c8a4c':'576b3f'));if(y>foot-height/2)put(p,32,48,x+bend+1,y,rgb('899753'));}
  if(cattail&&n%2===0)for(let y=foot-height-3;y<foot-height+2;y++){put(p,32,48,x,y,rgb('634b30'));put(p,32,48,x+1,y,rgb('88704a'));}
 }
 let name='wetlands_'+(cattail?'cattails_':'reeds_')+(i%3);save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');treeImages.push({name,p});}

function blit(dst,w,h,p,pw,ph,ox,oy){for(let y=0;y<ph;y++)for(let x=0;x<pw;x++){let a=(y*pw+x)*4,xx=x+ox,yy=y+oy;if(p[a+3]&&xx>=0&&yy>=0&&xx<w&&yy<h)p.copy(dst,(yy*w+xx)*4,a,a+4);}}
function scale(p,w,h,s,file){let b=Buffer.alloc(w*h*s*s*4);for(let y=0;y<h*s;y++)for(let x=0;x<w*s;x++){let i=(Math.floor(y/s)*w+Math.floor(x/s))*4;p.copy(b,(y*w*s+x)*4,i,i+4);}save(file,w*s,h*s,b);}
let scene=Buffer.alloc(320*208*4);let objects=[];
for(let j=0;j<8;j++)for(let i=0;i<10;i++){let x=128+(i-j)*16,y=48+(i+j)*8,h=hash(i,j,227);let g=h<.24?grounds[6+(i+j)%2]:h<.57?grounds[4+(i+j)%2]:grounds[(i+j*3)%4];blit(scene,320,208,g.p,32,16,x,y);
 if(h>.64&&!(i>3&&i<7&&j>2&&j<5)){let speciesIndex=(i*7+j*3)%3;let grove=h>.85?2:0;objects.push({x,y:y-32,p:treeImages[speciesIndex*6+grove].p});}
 else if(h>.18&&h<.5)objects.push({x,y:y-32,p:treeImages[18+(i+j)%6].p});}

for(const o of objects.sort((a,b)=>a.y-b.y))blit(scene,320,208,o.p,32,48,o.x,o.y);
scale(scene,320,208,3,path.join(out,'wetlands_landscape.png'));
let sheet=Buffer.alloc(256*136*4);for(let i=0;i<8;i++)blit(sheet,256,136,grounds[i].p,32,16,(i%4)*64+16,Math.floor(i/4)*24+4);for(const[i,t]of [treeImages[0],treeImages[6],treeImages[18],treeImages[21]].entries())blit(sheet,256,136,t.p,32,48,i*64+16,64);scale(sheet,256,136,3,path.join(out,'wetlands_assets.png'));
fs.writeFileSync(path.join(out,'biome.json'),JSON.stringify({biome:'Wetlands',ground:grounds.map(g=>g.name),treeSpecies:['weeping_willow','alder','white_willow'],treeDensity:'Willow and alder on damp ground, with reeds and cattails around shallow pools',files},null,2));
console.log('Generated '+files.length+' native texture files: 8 atlas cells, 8 isometric ground tiles, 18 tree/grove/sapling sprites and 3 reed and 3 cattail sprites.');
