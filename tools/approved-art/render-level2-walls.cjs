const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'defenses-level2');fs.mkdirSync(out,{recursive:true});
const P={edge:'#463321',timber:'#69462c',wood:'#986b38',woodlight:'#bd9153',plaster:'#e8d7ac',shade:'#c9b78b',stone:'#948a72',mortar:'#574f40',thatch:'#a65037',straw:'#ce7950',darkstraw:'#743b2f',window:'#302b23',iron:'#5b5951'};
let faces=[];
function poly(v,c){faces.push({v,c,mat:[P.thatch,P.straw,P.darkstraw].includes(c)?'thatch':c===P.stone?'stone':c===P.plaster?'plaster':[P.wood,P.timber,P.woodlight].includes(c)?'wood':''});}
function box(x,y,z,w,d,h,c){let a=[x,y,z],b=[x+w,y,z],c1=[x+w,y+d,z],e=[x,y+d,z],A=[x,y,z+h],B=[x+w,y,z+h],C=[x+w,y+d,z+h],E=[x,y+d,z+h];poly([a,b,B,A],c);poly([b,c1,C,B],c);poly([c1,e,E,C],c);poly([e,a,A,E],c);poly([A,B,C,E],c);}
function beam(a,b,r,c){let n=Math.ceil(Math.hypot(...a.map((v,i)=>b[i]-v))*2);for(let i=0;i<=n;i++){let t=i/n;box(...a.map((v,k)=>v+(b[k]-v)*t-r/2),r,r,r,c);}}




const L=29.09,C=L/2,deck=10.5;
function stake(x,y,id,damaged=false){
 let h=damaged?9+(id*7%5):17+(id*7%3),r=1.15;
 box(x-r,y-r,0,r*2,r*2,h-3,'#745031');
 box(x-.5,y-r-.1,2,.45,.2,h-6,'#ae7f49');
 box(x+.4,y-r-.15,4,.3,.22,h-8,'#503b28');
 let p=[x,y,h],a=[x-r,y-r,h-3],b=[x+r,y-r,h-3],c=[x+r,y+r,h-3],d=[x-r,y+r,h-3];
 for(const[v,w,col]of [[a,b,'#c69b5e'],[b,c,'#946b3d'],[c,d,'#8a6037'],[d,a,'#b5874e']])poly([v,w,p],col);
}
function originalWall(mask,damaged=false){
 faces=[];let positions=new Map();positions.set('c',[C,C,0]);
 for(const[bit,dx,dy]of [[1,1,0],[2,-1,0],[4,0,1],[8,0,-1]])if(mask&bit){
  for(let i=1;i<=4;i++){let x=C+dx*C*i/4,y=C+dy*C*i/4;positions.set(x+','+y,[x,y,i+bit]);}
  for(const z of [4,10])for(const side of [-1,1]){if(damaged&&z===10)continue;let ox=dy?side*1.4:0,oy=dx?side*1.4:0,a=[C+ox,C+oy,z],b=[C+dx*C+ox,C+dy*C+oy,z];beam(a,b,.8,'#705032');beam([a[0],a[1],z+.7],[b[0],b[1],z+.7],.4,'#ac804a');}
 }
 if(!mask){positions.set('a',[C-3,C,1]);positions.set('b',[C+3,C,2]);}
 for(const[x,y,id]of positions.values()){if(damaged&&id%4===2)continue;stake(x,y,(x<.01||y<.01||x>L-.01||y>L-.01)?0:id,damaged);for(const z of [4.3,10.3])if(!damaged||z<5)box(x-.4,y-1.4,z,.8,.4,.65,'#493728');}
}

// Original palisade model above is copied verbatim from the approved asset generator.
// Only the walkway, its planks and support brackets below are additions.
function wall(mask,sx=null,sy=null){
 originalWall(mask,false);
 const arms=[[1,1,0],[2,-1,0],[4,0,1],[8,0,-1]].filter(([b])=>mask&b);
 if(!arms.length)arms.push([1,1,0],[2,-1,0]);
 sx??=(mask&2)&&!(mask&1)?-1:1;sy??=(mask&8)&&!(mask&4)?-1:1;
 const near=1.8,far=9.2;
 for(const[bit,dx,dy]of arms){
  if(dx){let x0=dx<0?0:C,y0=C+(sy>0?near:-far);box(x0,y0,deck-.7,C,far-near,.7,'#795633');for(let i=0;i<7;i++){let x=C+dx*(i+.5)*C/7;box(x-.85,y0,deck,1.7,far-near,.35,'#ad8350');}}
  else{let y0=dy<0?0:C,x0=C+(sx>0?near:-far);box(x0,y0,deck-.7,far-near,C,.7,'#795633');for(let i=0;i<7;i++){let y=C+dy*(i+.5)*C/7;box(x0,y-.85,deck,far-near,1.7,.35,'#ad8350');}}
  for(const t of [5,C-2]){let x=C+dx*t,y=C+dy*t,ix=x+(dy?sx*8:0),iy=y+(dx?sy*8:0);beam([x,y,4],[ix,iy,deck-.6],.8,'#8e673e');}
  let x=C+dx*(C-3)+(dy?sx*8:0),y=C+dy*(C-3)+(dx?sy*8:0);box(x-.55,y-.55,0,1.1,1.1,deck-.7,'#745233');
 }
 if(arms.some(a=>a[1])&&arms.some(a=>a[2])){let x=C+(sx>0?near:-far),y=C+(sy>0?near:-far);box(x,y,deck-.7,far-near,far-near,.7,'#795633');for(let i=0;i<4;i++)box(x+i*1.85,y,deck,1.65,far-near,.35,'#ad8350');}
}
function stairs(){faces=[];
 // One-tile access module, ascending to exactly the same deck elevation.
 for(let i=0;i<7;i++){let x=3+i*3.1;box(x,10,0,3.15,9,(i+1)*deck/7,'#694b30');box(x,10,(i+1)*deck/7,3.15,9,.5,'#b28a52');}
 for(const y of [9.5,19.5]){beam([3,y,5],[24.5,y,21],.7,'#a27b49');for(const i of [0,3,6]){let x=3+i*3.1;box(x,y,(i+1)*deck/7,.7,.7,4.5,'#745233');}}
}
function rgb(h){return h.match(/[a-f\d]{2}/gi).map(x=>parseInt(x,16));}
function surface(f,q,normal,shade){let[x,y,z]=q,c=f.c;
 if(f.mat==='thatch'){
   // Golden bundled reeds, horizontal binding courses, and dark undercuts.
   let course=Math.floor(z/1.5),strand=Math.floor(x/1.6+(course%2)*.5),k=((strand*7+course*3)%13+13)%13;
   let seam=(z/1.5)%1;
   c=seam<.13?P.darkstraw:k<2?P.straw:k<5?'#ba6341':P.thatch;
 }else if(f.mat==='stone'){
   let along=Math.abs(normal[0])>Math.abs(normal[1])?y:x;
   let row=Math.floor(z/1.6),brick=(along+row%2*1.8)%3.4;
   c=(z%1.6<.27||brick<.28)?P.mortar:((Math.floor(along/3.4)+row)%3===0?'#aaa087':P.stone);
 }else if(f.mat==='wood'){
   let along=Math.abs(normal[0])>Math.abs(normal[1])?y:x;
   if(f.c!==P.timber&&(Math.floor(along*3)+Math.floor(z*2)*7)%19===0)c=P.woodlight;
 }else if(f.mat==='plaster'){
   let k=(Math.floor(x*2)*37+Math.floor(y*2)*17+Math.floor(z*2)*13)%101;
   if(k<5)c='#dac79b';
 }
 return rgb(c).map(v=>Math.round(v*shade));
}
function render(rot){let rgba=Buffer.alloc(32*48*4),depth=new Float64Array(32*48).fill(-1e9);function tr(v){let[x,y,z]=v;for(let i=0;i<rot;i++)[x,y]=[y,29.09-x];return[x,y,z];}function proj(v){let[x,y,z]=v;return[16+(x-y)*.55,40+(x+y-29.09)*.275-z*.9,z*2+x+y];}
for(let f of faces){let vv=f.v.map(tr),a=vv[0],b=vv[1],c=vv[2],u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);let normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];let shade=Math.abs(normal[2])>Math.abs(normal[0])+Math.abs(normal[1])?.99:Math.abs(normal[0])>Math.abs(normal[1])?1:.81;let col=rgb(f.c).map(v=>Math.round(v*shade));let p=vv.map(proj);for(let j=1;j<p.length-1;j++){let t=[p[0],p[j],p[j+1]],area=(t[1][0]-t[0][0])*(t[2][1]-t[0][1])-(t[1][1]-t[0][1])*(t[2][0]-t[0][0]);if(Math.abs(area)<.001)continue;let minx=Math.max(0,Math.floor(Math.min(...t.map(v=>v[0])))),maxx=Math.min(31,Math.ceil(Math.max(...t.map(v=>v[0])))),miny=Math.max(0,Math.floor(Math.min(...t.map(v=>v[1])))),maxy=Math.min(47,Math.ceil(Math.max(...t.map(v=>v[1]))));for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){let px=x+.5,py=y+.5,w1=((t[1][0]-px)*(t[2][1]-py)-(t[1][1]-py)*(t[2][0]-px))/area,w2=((t[2][0]-px)*(t[0][1]-py)-(t[2][1]-py)*(t[0][0]-px))/area,w3=1-w1-w2;if(Math.min(w1,w2,w3)<-1e-7)continue;let d=w1*t[0][2]+w2*t[1][2]+w3*t[2][2],idx=y*32+x;if(d>=depth[idx]){depth[idx]=d;let world=[0,1,2].map(k=>w1*f.v[0][k]+w2*f.v[j][k]+w3*f.v[j+1][k]);let color=surface(f,world,normal,shade);rgba.set([...color,255],idx*4);}}}}
return rgba;}
let crcTable=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});function crc(b){let c=0xffffffff;for(let v of b)c=crcTable[(c^v)&255]^(c>>>8);return(c^0xffffffff)>>>0;}function chunk(n,b){let name=Buffer.from(n),h=Buffer.alloc(4),tail=Buffer.alloc(4);h.writeUInt32BE(b.length);tail.writeUInt32BE(crc(Buffer.concat([name,b])));return Buffer.concat([h,name,b,tail]);}
function png(file,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)p.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(file,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]));}


function put(sheet,w,pixels,ox,oy,scale){for(let y=0;y<48;y++)for(let x=0;x<32;x++)for(let dy=0;dy<scale;dy++)for(let dx=0;dx<scale;dx++){let src=(y*32+x)*4,dst=((oy+y*scale+dy)*w+ox+x*scale+dx)*4;pixels.copy(sheet,dst,src,src+4);}}
function check(p,name){let opaque=0;for(let i=0;i<p.length;i+=4){if(p[i+3]!==0&&p[i+3]!==255)throw Error(name+' soft alpha');opaque+=p[i+3]>0;}if(opaque<30)throw Error(name+' empty');}

let atlas=Buffer.alloc(512*768*4);for(let mask=0;mask<16;mask++){wall(mask);let p=render(0);check(p,'wall2_'+mask);png(path.join(out,'palisade2_'+mask+'.png'),32,48,p);put(atlas,512,p,mask%4*128,Math.floor(mask/4)*192,4);}png(path.join(out,'level2_wall_connections.png'),512,768,atlas);
let access=Buffer.alloc(512*192*4);for(let r=0;r<4;r++){stairs();let p=render(r);check(p,'stairs');png(path.join(out,'wall_stairs_'+(r+1)+'.png'),32,48,p);put(access,512,p,r*128,0,4);}png(path.join(out,'wall_stairs_views.png'),512,192,access);
// Representative straight, corner, T and cross, shown larger for judging materials.
let preview=Buffer.alloc(512*192*4);for(const[mask,i]of [[3,0],[5,1],[7,2],[15,3]]){wall(mask);put(preview,512,render(0),i*128,0,4);}png(path.join(out,'level2_wall_preview.png'),512,192,preview);
console.log('PASS: 16 level-2 wall connections and 4 access-stair rotations; 32x48 transparent sprites; shared walkway elevation 10.5');
// The renderer chooses the inward side relative to the settlement, independently
// of camera rotation; straight runs need both possible walkway sides.
for(let mask=0;mask<16;mask++)for(const sx of [-1,1])for(const sy of [-1,1]){
 wall(mask,sx,sy);png(path.join(out,'palisade2_'+mask+'_'+sx+'_'+sy+'.png'),32,48,render(0));
}
// Assemble actual neighboring tiles to inspect the joins as a continuous enclosure.
const cells=[];for(let y=0;y<4;y++)for(let x=0;x<6;x++)if(x===0||x===5||y===0||y===3)cells.push([x,y]);
const has=(x,y)=>cells.some(c=>c[0]===x&&c[1]===y);
const scene=Buffer.alloc(192*128*4);
for(const[x,y]of cells.sort((a,b)=>a[0]+a[1]-b[0]-b[1])){
 let mask=(has(x+1,y)?1:0)|(has(x-1,y)?2:0)|(has(x,y+1)?4:0)|(has(x,y-1)?8:0);wall(mask,x===5?-1:1,y===3?-1:1);let p=render(0),ox=88+(x-y)*16-16,oy=(x+y)*8;
 for(let py=0;py<48;py++)for(let px=0;px<32;px++){let i=(py*32+px)*4,xx=ox+px,yy=oy+py;if(p[i+3]&&xx>=0&&xx<192&&yy>=0&&yy<128)p.copy(scene,(yy*192+xx)*4,i,i+4);}
}
const big=Buffer.alloc(576*384*4);for(let y=0;y<384;y++)for(let x=0;x<576;x++){let i=(Math.floor(y/3)*192+Math.floor(x/3))*4;scene.copy(big,(y*576+x)*4,i,i+4);}png(path.join(out,'connected_wall_enclosure.png'),576,384,big);
