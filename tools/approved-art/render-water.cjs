const fs=require('fs'),path=require('path'),zlib=require('zlib');
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

const out=path.resolve(__dirname,'water'),assets=path.join(out,'assets'),cells=path.join(out,'ground-cells');fs.mkdirSync(assets,{recursive:true});fs.mkdirSync(cells,{recursive:true});
const TAU=Math.PI*2,frames=8;
const palettes={still:['304f4b','365c55','3e685f','487568','558373','699384','83a99a','a7c2af'],ocean:['203b52','26485e','2c566a','35657a','40788a','548e9b','78a8ad','b0c8c0'],river:['2c5356','336264','3d7372','4a8580','5d978d','77a99c','97bdb0','bfcec0']};
let files=[],sets=[],images=new Map();
function tile(kind,depth,variant,dir,frame){const p=Buffer.alloc(32*16*4),pal=palettes[kind].map(rgb),t=frame/frames;
 for(let y=0;y<16;y++)for(let x=0;x<32;x++){
  const u=(x+.5-16)/32+(y+.5-8)/16+.5,v=-(x+.5-16)/32+(y+.5-8)/16+.5;
  // Integer plan frequencies repeat across every isometric edge. Interior variants fade to the shared edge pattern.
  let fade=Math.max(0,Math.sin(Math.PI*Math.max(0,Math.min(1,u)))*Math.sin(Math.PI*Math.max(0,Math.min(1,v))));
  let fine=Math.sin(TAU*(5*u+3*v)),level=2;
  if(kind==='still'){
   let soft=Math.sin(TAU*(u+v)+.6*Math.sin(TAU*(u-2*v)))*.15;
   let ripple=Math.sin(TAU*(2*u+2*v-t)+.8*Math.sin(TAU*(u-v))),breaks=Math.cos(TAU*(2*u-v)+.5*Math.sin(TAU*v));
   level=2+(soft>.12?1:0);
   if(ripple>.96&&breaks>.6)level=4;if(ripple>.995&&breaks>.85)level=5;
  }else if(kind==='ocean'){
   let wave=Math.sin(TAU*(u+v-t)+.65*Math.sin(TAU*(2*u-v))),chop=Math.sin(TAU*(3*u-2*v+t));
   level=wave<-.55?1:wave<.5?2:3;
   if(wave>.88&&chop>.05)level=4;if(wave>.965&&chop>.5)level=5;
   if(depth==='coastal'&&wave>.99&&chop>.8)level=6;
  }else{
   let along=dir==='east'?u:dir==='west'?-u:dir==='south'?v:-v,cross=dir==='east'||dir==='west'?v:u;
   let streak=Math.cos(TAU*(2*cross+.13*Math.sin(TAU*(along-t)))),pulse=Math.sin(TAU*(3*along-t));
   level=streak<-.4?1:streak<.45?2:3;
   if(streak>.86&&pulse>.55)level=4;if(streak>.98&&pulse>.85)level=5;
   if(streak<-.9&&pulse<-.5)level=0;
  }
  if(variant&&fade>.6&&fine>.8&&level<4)level++;
  let c=pal[Math.max(0,Math.min(7,level))];
  if(depth==='shallow'||depth==='coastal')c=c.map((q,k)=>Math.min(255,q+[12,17,10][k]));
  put(p,32,16,x,y,c);
 }
 return p;
}
for(const kind of ['still','ocean','river'])for(const depth of kind==='ocean'?['coastal','deep']:kind==='still'?['shallow','deep']:['flow'])for(const dir of kind==='river'?['east','south','west','north']:['none'])for(let v=0;v<2;v++){
 let names=[];for(let f=0;f<frames;f++){let p=tile(kind,depth,v,dir,f),name='water_'+kind+'_'+depth+(dir==='none'?'':'_'+dir)+'_v'+v+'_f'+f;
  save(path.join(cells,name+'.png'),32,16,p);files.push('ground-cells/'+name+'.png');
  const d=Buffer.from(p);for(let y=0;y<16;y++)for(let x=0;x<32;x++)if(Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8)>1)d[(y*32+x)*4+3]=0;
  save(path.join(assets,name+'.png'),32,16,d);files.push('assets/'+name+'.png');images.set(name,d);names.push(name);
 }
 // Horizontal sprite sheet; frame order is explicit in the manifest.
 let sheet=Buffer.alloc(256*16*4);for(let f=0;f<frames;f++)blit(sheet,256,16,images.get(names[f]),32,16,f*32,0);save(path.join(out,names[0].replace('_f0','_sheet')+'.png'),256,16,sheet);
 sets.push({kind,depth,direction:dir,variant:v,fps:kind==='still'?4:8,frames:names});
}
function blit(dst,w,h,p,pw,ph,ox,oy){for(let y=0;y<ph;y++)for(let x=0;x<pw;x++){let a=(y*pw+x)*4,xx=x+ox,yy=y+oy;if(p[a+3]&&xx>=0&&yy>=0&&xx<w&&yy<h)p.copy(dst,(yy*w+xx)*4,a,a+4);}}
function scaled(p,w,h,s){let b=Buffer.alloc(w*h*s*s*4);for(let y=0;y<h*s;y++)for(let x=0;x<w*s;x++){let i=(Math.floor(y/s)*w+Math.floor(x/s))*4;p.copy(b,(y*w*s+x)*4,i,i+4);}return b;}
let previews=[];for(let frame=0;frame<8;frame++){
 let scene=Buffer.alloc(544*144*4);
 for(let panel=0;panel<3;panel++)for(let j=0;j<5;j++)for(let i=0;i<5;i++){
  let kind=['still','ocean','river'][panel],depth=panel===0?'deep':panel===1?'coastal':'flow',direction=panel===2?'east':'none';
  let set=sets.find(s=>s.kind===kind&&s.depth===depth&&s.direction===direction&&s.variant===(i+j)%2);
  blit(scene,544,144,images.get(set.frames[frame]),32,16,panel*176+72+(i-j)*16,24+(i+j)*8);
 }
 previews.push(scaled(scene,544,144,2));
}
save(path.join(out,'water_preview.png'),1088,288,previews[0]);
// Portable GIF encoder: fixed 9-bit LZW blocks, reset before dictionary growth. No external libraries.
function gif(file,w,h,pictures,delay){let colors=[[0,0,0]],map=new Map();for(const p of pictures)for(let i=0;i<p.length;i+=4){if(!p[i+3])continue;let key=p[i]+','+p[i+1]+','+p[i+2];if(!map.has(key)){map.set(key,colors.length);colors.push([p[i],p[i+1],p[i+2]]);}}
 if(colors.length>256)throw Error('GIF palette too large');let table=Buffer.alloc(768);colors.forEach((c,i)=>c.forEach((v,j)=>table[i*3+j]=v));
 let dim=Buffer.alloc(7);dim.writeUInt16LE(w);dim.writeUInt16LE(h,2);dim[4]=0xf7;let parts=[Buffer.from('GIF89a'),dim,table,Buffer.from([0x21,0xff,11]),Buffer.from('NETSCAPE2.0'),Buffer.from([3,1,0,0,0])];
 for(const p of pictures){parts.push(Buffer.from([0x21,0xf9,4,9,delay,0,0,0]));let desc=Buffer.alloc(10);desc[0]=0x2c;desc.writeUInt16LE(w,5);desc.writeUInt16LE(h,7);parts.push(desc,Buffer.from([8]));let codes=[256],n=0;for(let i=0;i<p.length;i+=4){if(n===200){codes.push(256);n=0;}codes.push(p[i+3]?map.get(p[i]+','+p[i+1]+','+p[i+2]):0);n++;}codes.push(257);let packed=Buffer.alloc(Math.ceil(codes.length*9/8));let bit=0;for(const c of codes){let k=bit>>3,shift=bit&7;packed[k]|=(c<<shift)&255;if(k+1<packed.length)packed[k+1]|=c>>(8-shift);bit+=9;}for(let o=0;o<packed.length;o+=255){let part=packed.subarray(o,o+255);parts.push(Buffer.from([part.length]),part);}parts.push(Buffer.from([0]));}
 parts.push(Buffer.from([0x3b]));fs.writeFileSync(file,Buffer.concat(parts));
}
gif(path.join(out,'water_animation.gif'),1088,288,previews,15);
fs.writeFileSync(path.join(out,'water.json'),JSON.stringify({tileSize:[32,16],alpha:'binary',sets,files,flowDirections:'east=+world X, south=+world Y; rotate direction with the camera',note:'Still water has subtle ripples, ocean has moving wave crests, rivers have directional advecting streaks. Art only; no water simulation changes.'},null,2));
fs.writeFileSync(path.join(out,'README.md'),'# Water textures\n\nLeft to right in preview: still water, ocean, flowing river.\n\n32x16 native tiles, opaque ground atlas cells and transparent diamond sprites. Eight animation frames per set. Two interior variants per set share the same boundary pattern. Still water: shallow and deep. Ocean: coastal and deep. Rivers: four world directions. Horizontal 256x16 sheets contain eight frames. See water.json for names, playback rates and direction mapping.\n\nThese assets are not yet connected to local water terrain or animation in the game.\n');
console.log('Generated '+files.length+' water PNG frames, '+sets.length+' sprite sheets, static and animated previews.');
