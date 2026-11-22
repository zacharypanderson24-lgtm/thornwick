const fs = require('node:fs');
const zlib = require('node:zlib');
const path = require('node:path');

const out = process.argv[2] || path.join(__dirname, 'battle-art');
fs.mkdirSync(out, { recursive: true });
const W = 32, H = 28, FOOT = 24;
const unity = process.argv.includes('--unity');
const native = unity ? { width:28, height:24, foot:21 } : { width:W, height:H, foot:FOOT };
const directions = ['se', 'sw', 'ne', 'nw'];
const crc = Array.from({length:256}, (_, i) => {
  for (let j=0;j<8;j++) i = (i >>> 1) ^ ((i & 1) ? 0xedb88320 : 0);
  return i >>> 0;
});
function packet(type, bytes) {
  const body = Buffer.concat([Buffer.from(type), bytes]);
  const result = Buffer.alloc(body.length+8);
  result.writeUInt32BE(bytes.length);
  body.copy(result,4);
  let checksum = 0xffffffff;
  for(const b of body) checksum = (checksum >>> 8) ^ crc[(checksum ^ b) & 255];
  result.writeUInt32BE((checksum ^ 0xffffffff) >>> 0,result.length-4);
  return result;
}
function png(name,w,h,pixels) {
  const header = Buffer.alloc(13), scan = Buffer.alloc((w*4+1)*h);
  header.writeUInt32BE(w); header.writeUInt32BE(h,4); header[8]=8; header[9]=6;
  for(let y=0;y<h;y++) pixels.copy(scan,y*(w*4+1)+1,y*w*4,(y+1)*w*4);
  fs.writeFileSync(path.join(out,name+'.png'),Buffer.concat([
    Buffer.from('89504e470d0a1a0a','hex'),packet('IHDR',header),packet('IDAT',zlib.deflateSync(scan)),packet('IEND',Buffer.alloc(0))
  ]));
}
const color = hex => [...hex.matchAll(/../g)].map(x=>parseInt(x[0],16));
const dust = ['52473b','74614c','a08763','b5a27c'];
const wood = ['35271f','68482f','ad7d4a','c49a60'];
const metal = ['2d3638','66777a','a5b6b1','e1ddbc'];
const leather = ['261f1a','47352a','735139','956c45'];
const skin = ['57382c','946145','c2895e','e1ad7f'];
const ink = '202923';
const paletteCache = new Map();
function pixel(b,x,y,c) {
  x=Math.round(x); y=Math.round(y);
  if(x<0||x>=W||y<0||y>=H) return;
  if(!paletteCache.has(c)) paletteCache.set(c,color(c));
  const i=(y*W+x)*4;
  b.set(paletteCache.get(c),i); b[i+3]=255;
}
function box(b,x,y,w,h,c) {
  for(let j=0;j<h;j++)for(let i=0;i<w;i++)pixel(b,x+i,y+j,c);
}
function polygon(b,p,c) {
  const low = Math.min(...p.map(v=>v[1])), high = Math.max(...p.map(v=>v[1]));
  const left = Math.min(...p.map(v=>v[0])), right = Math.max(...p.map(v=>v[0]));
  for(let y=Math.floor(low);y<high;y++) for(let x=Math.floor(left);x<right;x++) {
    let inside=false;
    for(let i=0,j=p.length-1;i<p.length;j=i++) {
      const [a,u]=p[i],[d,v]=p[j];
      if((u>y+.5)!==(v>y+.5) && x+.5<(d-a)*(y+.5-u)/(v-u)+a)inside=!inside;
    }
    if(inside)pixel(b,x,y,c);
  }
}
function stroke(b,a,d,c,width=1) {
  const count=Math.max(1,Math.ceil(Math.max(Math.abs(d[0]-a[0]),Math.abs(d[1]-a[1]))));
  for(let i=0;i<=count;i++) {
    const x=a[0]+(d[0]-a[0])*i/count,y=a[1]+(d[1]-a[1])*i/count;
    box(b,Math.round(x-width/2+.5),Math.round(y-width/2+.5),width,width,c);
  }
}
const roles = [
  {name:'raider',label:'Raider',coat:['39282a','653332','904943','bc6a51'],head:'hood',weapon:'spear',legs:'4b3430'},
  {name:'infantry',label:'Infantry',coat:['263938','3a5757','557c78','84a59a'],head:'helm',weapon:'sword',shield:true,legs:'3d4c49'},
  {name:'archer',label:'Archer',coat:['283a42','415972','6c89a3','b1c9c9'],head:'cap',weapon:'bow',legs:'39494a'},
  {name:'militia_farmer',label:'Farm militia',coat:['47392b','6b5238','a17a4e','c8ab73'],head:'bare',weapon:'fork',legs:'574535'},
  {name:'militia_hunter',label:'Hunter militia',coat:['2b3c30','3f5940','5c7853','87986e'],head:'hood',weapon:'bow',legs:'454538'},
  {name:'militia_woodcutter',label:'Woodcutter militia',coat:['463127','76513c','a26d48','cf9866'],head:'bare',weapon:'axe',legs:'594238'},
  {name:'militia_forester',label:'Forester militia',coat:['344036','4e6853','6f8666','a3ad81'],head:'cap',weapon:'hook',legs:'454e3c'},
  {name:'militia_sawyer',label:'Sawmill militia',coat:['4a372b','77573a','a18356','c3ab7a'],head:'bare',weapon:'saw',legs:'534537'},
  {name:'militia_quarry',label:'Quarry militia',coat:['363f43','50646a','74878a','a1aaa0'],head:'cap',weapon:'hammer',legs:'4b5352'},
  {name:'militia_miner',label:'Miner militia',coat:['363330','524c43','847a67','d3b477'],head:'cap',weapon:'pick',legs:'443d39'}
];
function motion(state, frame) {
  const walk = state==='walk', run = state==='run'||state==='carry';
  const offsets=[[-2,2],[-1,1],[0,0],[2,-2],[1,-1],[0,0]][frame%6];
  return {
    dx: state==='hurt' ? [-1,-2,-1][frame] : state==='attack' ? [0,-1,0,1,1,0][frame] : state==='loot' ? [0,1,2,1,0][frame] : 0,
    dy: run ? [0,-1,-1,0,-1,-1][frame%6] : walk ? [0,0,-1,0,0,-1][frame%6] :
      state==='loot' ? [0,1,3,2,0][frame] : state==='idle' ? -frame : 0,
    legs: walk||run ? offsets.map(v=>run?v*1.5:v) : [0,0],
    lift: walk||run ? [frame%6===2?1:0,frame%6===5?1:0] : [0,0]
  };
}
function drawWeapon(b,kind,x,y,v) {
  const ax=x+v[0], ay=y+v[1], shaft=[x-v[0]*.22,y-v[1]*.22];
  if(kind==='bow') return;
  stroke(b,shaft,[ax,ay],wood[0],2);
  stroke(b,shaft,[ax,ay],wood[2]);
  const length=Math.max(1,Math.hypot(v[0],v[1])), nx=-v[1]/length, ny=v[0]/length;
  const extension=n=>[ax+v[0]*n/length,ay+v[1]*n/length];
  const across=n=>[ax+nx*n,ay+ny*n];
  switch(kind) {
    case 'sword':
    case 'spear':
      stroke(b,[ax,ay],extension(kind==='sword'?3:2),metal[0],2);
      stroke(b,[ax,ay],extension(kind==='sword'?3:2),metal[2]);
      if(kind==='sword')stroke(b,[x-nx*2,y-ny*2],[x+nx*2,y+ny*2],'b18c54');
      pixel(b,...extension(kind==='sword'?3:2),metal[3]);
      break;
    case 'fork':
      stroke(b,across(-1.6),across(1.6),metal[1]);
      for(let n=-1;n<=1;n++)stroke(b,across(n*1.6),[ax+nx*n*1.6+2.2*v[0]/length,ay+ny*n*1.6+2.2*v[1]/length],metal[2]);
      break;
    case 'axe':
    case 'hook':
    case 'pick':
      stroke(b,across(-2.2),across(2.2),metal[0],2);
      stroke(b,across(-1.4),across(2),metal[2]);
      if(kind==='axe')stroke(b,across(1),[ax+nx*2.6+v[0]/length,ay+ny*2.6+v[1]/length],metal[3],2);
      if(kind==='hook'||kind==='pick')pixel(b,ax+nx*2.3-v[0]/length,ay+ny*2.3-v[1]/length,metal[2]);
      break;
    case 'saw':
      stroke(b,[x,y],[ax,ay],metal[1],2);
      stroke(b,[x+nx,y+ny],[ax+nx,ay+ny],metal[2]);
      for(let i=2;i<length;i+=2)pixel(b,x+nx*2+v[0]*i/length,y+ny*2+v[1]*i/length,metal[3]);
      break;
    case 'hammer':
      stroke(b,across(-2),across(2),metal[0],3);
      stroke(b,across(-1.7),across(1.8),metal[2],2);
      break;
  }
}
function drawBow(b,x,y,drawn,release) {
  const top=[x-2,y-5], mid=[x+1,y], base=[x-2,y+5];
  stroke(b,top,[x,y-3],wood[1]); stroke(b,[x,y-3],mid,wood[3]);
  stroke(b,mid,[x,y+3],wood[2]); stroke(b,[x,y+3],base,wood[1]);
  const pull=[x-2-drawn,y];
  stroke(b,top,pull,'b5ae89');stroke(b,pull,base,'b5ae89');
  if(drawn>0&&!release) {
    stroke(b,[pull[0],y],[x+2,y],'ac8353');
    pixel(b,x+3,y,metal[3]);
  }
}
function pack(b,x,y) {
  polygon(b,[[x-5,y],[x-2,y-3],[x+2,y-2],[x+3,y+2],[x+1,y+6],[x-4,y+5]],'59472d');
  polygon(b,[[x-4,y],[x-2,y-2],[x+1,y-1],[x+1,y+3],[x-3,y+4]],'b6975c');
  pixel(b,x-2,y-2,'d6be87');stroke(b,[x-2,y],[x+1,y+2],'9a7a46');
  box(b,x-3,y-4,2,2,'776046');
}
function drawUnit(role,direction,state,frame) {
  const b=Buffer.alloc(W*H*4);
  const rear=direction[0]==='n', mirror=direction[1]==='w';
  const mo=motion(state,frame), x=15+mo.dx, y=mo.dy;
  const coat=role.coat, crouch=state==='loot'&&frame>=2&&frame<=3;
  const bodyY=y+(crouch?1:0), headY=bodyY+5;
  const footA=[x-2+mo.legs[0],FOOT-mo.lift[0]], footB=[x+2+mo.legs[1],FOOT-mo.lift[1]];
  const actionArm = state==='attack' ? [[1,-1],[-1,-2],[2,-1],[1,0],[1,1],[0,0]][frame] : [0,0];
  const hands=[x+5+actionArm[0],bodyY+15+actionArm[1]];
  // Lay the limbs under the folds of the tunic so heel lift stays readable.
  stroke(b,[x-1,bodyY+17],[footA[0],footA[1]-2],ink,3);
  stroke(b,[x-1,bodyY+17],[footA[0],footA[1]-2],role.legs,2);
  box(b,footA[0]-1,footA[1]-2,3,2,leather[0]);
  stroke(b,[x+1,bodyY+17],[footB[0],footB[1]-2],ink,3);
  stroke(b,[x+1,bodyY+17],[footB[0],footB[1]-2],role.legs,2);
  box(b,footB[0]-1,footB[1]-2,4,2,leather[1]);
  pixel(b,footB[0]+1,footB[1]-2,leather[2]);

  if(role.name==='raider') {
    polygon(b,[[x-3,bodyY+10],[x+3,bodyY+11],[x+3,bodyY+18],[x-2,bodyY+20],[x-6+(Math.abs(mo.legs[0])>0?-1:0),bodyY+17]],coat[0]);
    stroke(b,[x-3,bodyY+11],[x-5,bodyY+17],coat[1],2);
  }
  if(role.weapon==='bow') {
    stroke(b,[x-4,bodyY+11],[x-2,bodyY+18],leather[0],2);
    stroke(b,[x-4,bodyY+10],[x-2,bodyY+17],wood[1]);
    pixel(b,x-4,bodyY+9,metal[3]);pixel(b,x-3,bodyY+9,metal[2]);
  }
  polygon(b,[[x-3,bodyY+10],[x+2,bodyY+10],[x+5,bodyY+12],[x+5,bodyY+17],[x+3,bodyY+19],[x-4,bodyY+18],[x-5,bodyY+15],[x-4,bodyY+12]],ink);
  polygon(b,[[x-3,bodyY+11],[x+2,bodyY+11],[x+4,bodyY+13],[x+4,bodyY+17],[x+2,bodyY+18],[x-3,bodyY+17],[x-4,bodyY+13]],coat[1]);
  polygon(b,[[x-3,bodyY+11],[x+1,bodyY+11],[x+2,bodyY+14],[x,bodyY+17],[x-3,bodyY+16]],coat[2]);
  stroke(b,[x-3,bodyY+12],[x,bodyY+12],coat[3]);
  stroke(b,[x-3,bodyY+16],[x+3,bodyY+17],leather[1]);
  pixel(b,x+2,bodyY+16,rear?leather[3]:'d5b273');
  if(role.head==='helm') {
    for(const [dx,dy] of [[-3,12],[-2,13],[-3,14],[2,13],[3,14],[-2,18],[0,18]])pixel(b,x+dx,bodyY+dy,metal[2]);
    if(!rear)stroke(b,[x-2,bodyY+12],[x+1,bodyY+15],'9bb0a1');
  } else if(!rear && role.name.startsWith('militia')) {
    stroke(b,[x-1,bodyY+11],[x+1,bodyY+16],leather[3]);
  }
  if(state==='carry') { pack(b,x-4,bodyY+11);pixel(b,x+1,bodyY+12,wood[3]); }

  // Headwear, lit on the upper-left and separately drawn from the face.
  polygon(b,[[x-2,headY],[x+2,headY],[x+4,headY+2],[x+4,headY+5],[x+1,headY+7],[x-3,headY+6],[x-4,headY+3]],ink);
  if(role.head==='helm') {
    polygon(b,[[x-2,headY],[x+2,headY],[x+3,headY+2],[x+2,headY+4],[x-3,headY+4],[x-3,headY+2]],metal[1]);
    stroke(b,[x-2,headY+1],[x+1,headY+1],metal[2]);
    box(b,x+1,headY+3,2,3,metal[0]);
    if(!rear){box(b,x,headY+4,2,2,skin[1]);pixel(b,x+2,headY+3,metal[3]);}
  } else if(role.head==='cap') {
    box(b,x-3,headY+1,6,3,coat[1]);
    box(b,x-2,headY+1,4,1,coat[3]);
    box(b,x-3,headY+3,7,1,coat[2]);
    if(!rear){box(b,x,headY+4,3,2,skin[2]);pixel(b,x+2,headY+4,ink);}
    else box(b,x-2,headY+4,4,2,coat[1]);
  } else if(role.head==='hood') {
    polygon(b,[[x-2,headY],[x+1,headY],[x+3,headY+2],[x+3,headY+5],[x,headY+6],[x-3,headY+5],[x-3,headY+2]],coat[1]);
    stroke(b,[x-2,headY+1],[x,headY+1],coat[3]);
    if(!rear){box(b,x+1,headY+3,2,2,skin[2]);pixel(b,x+3,headY+3,skin[1]);pixel(b,x+2,headY+2,ink);}
  } else {
    box(b,x-2,headY+1,5,3,leather[1]);
    box(b,x-1,headY+1,3,1,leather[3]);
    if(!rear){box(b,x,headY+3,3,3,skin[2]);pixel(b,x+2,headY+3,ink); pixel(b,x+2,headY+5,skin[1]);}
    else box(b,x-2,headY+3,4,2,leather[2]);
  }

  if(role.shield) {
    const shieldX=x+(rear?-3:-4);
    polygon(b,[[shieldX-3,bodyY+12],[shieldX+1,bodyY+11],[shieldX+3,bodyY+13],[shieldX+2,bodyY+18],[shieldX,bodyY+20],[shieldX-3,bodyY+17]],'303c39');
    polygon(b,[[shieldX-2,bodyY+13],[shieldX+1,bodyY+12],[shieldX+2,bodyY+13],[shieldX+1,bodyY+18],[shieldX,bodyY+19],[shieldX-2,bodyY+17]],coat[2]);
    stroke(b,[shieldX-1,bodyY+13],[shieldX+1,bodyY+13],metal[2]);
    pixel(b,shieldX,bodyY+15,'d4bd78');pixel(b,shieldX,bodyY+16,'ab9253');
  }
  if(state==='loot') {
    const g = [0,2,1,0,0][frame];
    stroke(b,[x+2,bodyY+12],[x+4,bodyY+16+g],coat[2],2);
    pixel(b,x+4,bodyY+17+g,skin[2]);
    if(frame>=1)pack(b,x+5,bodyY+15-g);
  } else if(role.weapon==='bow') {
    const pull=state==='attack'?[0,2,4,4,1,0][frame]:0;
    const move=state==='attack'&&frame>=1&&frame<=4?2:0;
    stroke(b,[x+2,bodyY+12],[x+5+move,bodyY+14],coat[2],2);
    pixel(b,x+5+move,bodyY+14,skin[2]);
    if(pull)stroke(b,[x+2,bodyY+13],[x+5+move-pull,bodyY+14],skin[1]);
    drawBow(b,x+6+move,bodyY+14,pull,frame>=4);
  } else {
    stroke(b,[x+2,bodyY+12],hands,coat[0],3);
    stroke(b,[x+2,bodyY+12],hands,coat[2],2);
    const length=role.weapon==='spear'||role.weapon==='fork'?7:5;
    const vec=state==='attack'?[[2,-length],[-3,-length],[2,-length],[Math.ceil(length*.64),-1],[Math.ceil(length*.64),2],[2,-length]][frame]:[2,-length];
    if(state==='carry') { vec[0]=1; vec[1]=-length; }
    drawWeapon(b,role.weapon,...hands,vec);
    pixel(b,...hands,skin[2]);
  }
  if(state==='hurt' && frame===0) {
    for(let i=0;i<b.length;i+=4) if(b[i+3]) {
      b[i]=Math.min(255,b[i]+38);b[i+1]=Math.floor(b[i+1]*.68);b[i+2]=Math.floor(b[i+2]*.69);
    }
  }
  const fallen=state==='fall'?tipOver(b,frame):b;
  return mirror?flipped(fallen):fallen;
}
function flipped(src) {
  const result=Buffer.alloc(src.length);
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)src.copy(result,(y*W+(W-1-x))*4,(y*W+x)*4,(y*W+x+1)*4);
  return result;
}
function tipOver(src,frame) {
  const tilt=[.12,.37,.75,1.20,1.52][frame], c=Math.cos(tilt), s=Math.sin(tilt);
  const temp=Buffer.alloc(src.length); let bottom=0;
  for(let y=0;y<H;y++)for(let x=0;x<W;x++) {
    const dx=x-16,dy=y-16,ix=Math.round(16+dx*c+dy*s),iy=Math.round(16-dx*s+dy*c);
    if(ix<0||ix>=W||iy<0||iy>=H)continue;
    const start=(iy*W+ix)*4;
    if(src[start+3]){src.copy(temp,(y*W+x)*4,start,start+4);bottom=Math.max(bottom,y);}
  }
  const result=Buffer.alloc(src.length),dy=FOOT-1-bottom;
  for(let y=0;y<H;y++)if(y+dy>=0&&y+dy<H)temp.copy(result,(y+dy)*W*4,y*W*4,(y+1)*W*4);
  return result;
}

// Export each cell independently. Pixel-centre sampling keeps the new silhouettes sharp in-game.
function exportSheet(name, width, rows, pixels) {
  if (!unity) { png(name,width,rows*H,pixels); return; }
  const {width:w,height:h}=native;
  const columns=width/W, newWidth=columns*w, result=Buffer.alloc(newWidth*h*rows*4);
  for(let row=0;row<rows;row++)for(let col=0;col<columns;col++) {
    for(let y=0;y<h;y++)for(let x=0;x<w;x++) {
      const sx=col*W+Math.floor((x+.5)*W/w),sy=row*H+Math.floor((y+.5)*H/h);
      const from=(sy*width+sx)*4,to=((row*h+y)*newWidth+col*w+x)*4;
      pixels.copy(result,to,from,from+4);
    }
  }
  png(name,newWidth,h*rows,result);
}

const regular = [
  ['idle',[260,260],true],['walk',[105,105,105,105,105,105],true],
  ['run',[78,78,78,78,78,78],true],['attack',[100,105,70,70,90,140],false],
  ['hurt',[85,105,120],false],['fall',[85,90,95,110,390],false],
];
const manifest = { cell:{width:W,height:H,pivotPixelsFromTop:[16,FOOT],pixelsPerUnit:32,filter:'point'}, directions, actors:{} };
function attach(sheet,width,x,y,pixels) {
  for(let row=0;row<H;row++) pixels.copy(sheet,((y+row)*width+x)*4,row*W*4,(row+1)*W*4);
}
for(const role of roles) {
  const clips = role.name==='raider' ? [...regular,['loot',[95,130,115,100,130],false],['carry',[92,92,92,92,92,92],true]] : regular;
  const width=clips.reduce((n,clip)=>n+clip[1].length,0)*W;
  const sheet=Buffer.alloc(width*H*directions.length*4);
  let column=0;
  const record={label:role.label,weapon:role.weapon,file:role.name+'.png',clips:{}};
  for(const [state,timings,loop] of clips) {
    record.clips[state]={firstColumn:column,frameMs:timings,loop};
    directions.forEach((d,row)=>timings.forEach((_,i)=>attach(sheet,width,(column+i)*W,row*H,drawUnit(role,d,state,i))));
    column+=timings.length;
  }
  exportSheet(role.name,width,directions.length,sheet);
  manifest.actors[role.name]=record;
}

// Arrow, non-graphic hit chips and dust, plus a carried sack icon for UI or a loot cue.
const efx = {arrow:8,hit:4,dust:5,sack:1};
const ew=Object.values(efx).reduce((a,b)=>a+b,0)*W, effects=Buffer.alloc(ew*H*4);
manifest.effects={file:'battle_effects.png',clips:{}};
let cell=0;
for(const [name,frames] of Object.entries(efx)) {
  manifest.effects.clips[name]={firstColumn:cell,frames,pivotPixelsFromTop:[16,14]};
  for(let i=0;i<frames;i++) {
    const f=Buffer.alloc(W*H*4);
    if(name==='arrow') {
      const angle=i*Math.PI/4, ux=Math.cos(angle),uy=Math.sin(angle);
      const point=(d)=>[16+ux*d,14+uy*d];
      stroke(f,point(-6),point(5),wood[0],2);stroke(f,point(-6),point(4),wood[2]);
      stroke(f,point(3),point(6),metal[3]);
      pixel(f,16-ux*4+uy,14-uy*4-ux,'d5c8a2');
      pixel(f,16-ux*4-uy,14-uy*4+ux,'b49d6c');
    } else if(name==='hit') {
      for(const [dx,dy] of [[-2,-1],[1,-2],[2,1],[-2,2],[1,3]]) {
        const size=1+i*.72;
        pixel(f,16+dx*size,14+dy*size,['fff1c4','dfb777','b08c58','746752'][i]);
      }
      if(i===0)box(f,15,13,2,2,'ffedc2');
    } else if(name==='dust') {
      for(const [dx,dy] of [[-2,-1],[1,-2],[2,0],[-1,1],[1,2]])pixel(f,16+dx*(1+i*.48),14+dy*(1+i*.29),dust[Math.max(0,3-i)]);
    } else {
      pack(f,17,12);
    }
    attach(effects,ew,(cell+i)*W,0,f);
  }
  cell+=frames;
}
exportSheet('battle_effects',ew,1,effects);
if (unity) {
  const catalogue = {
    width:native.width,height:native.height,foot:native.foot,
    actors:Object.entries(manifest.actors).map(([name,actor])=>({
      name,clips:Object.entries(actor.clips).map(([name,c])=>({name,...c}))
    })),
    effects:Object.entries(manifest.effects.clips).map(([name,c])=>({name,...c}))
  };
  fs.writeFileSync(path.join(out,'battle_catalogue.json'),JSON.stringify(catalogue,null,2)+'\n');
}
if (!unity) fs.writeFileSync(path.join(out,'battle-animation.json'),JSON.stringify(manifest,null,2)+'\n');
console.log('Rendered raiders, infantry, archers, seven militia roles, and four effect groups.');
