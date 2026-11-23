const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'storehouse');fs.mkdirSync(out,{recursive:true});
const P={edge:'#463321',timber:'#69462c',wood:'#986b38',woodlight:'#bd9153',plaster:'#e8d7ac',shade:'#c9b78b',stone:'#948a72',mortar:'#574f40',thatch:'#c28b3f',straw:'#dcad59',darkstraw:'#a16f2f',window:'#302b23',iron:'#5b5951'};
let faces=[];
function poly(v,c){faces.push({v,c,mat:[P.thatch,P.straw,P.darkstraw].includes(c)?'thatch':c===P.stone?'stone':c===P.plaster?'plaster':[P.wood,P.timber,P.woodlight].includes(c)?'wood':''});}
function box(x,y,z,w,d,h,c){let a=[x,y,z],b=[x+w,y,z],c1=[x+w,y+d,z],e=[x,y+d,z],A=[x,y,z+h],B=[x+w,y,z+h],C=[x+w,y+d,z+h],E=[x,y+d,z+h];poly([a,b,B,A],c);poly([b,c1,C,B],c);poly([c1,e,E,C],c);poly([e,a,A,E],c);poly([A,B,C,E],c);}
function beam(a,b,r,c){let n=Math.ceil(Math.hypot(...a.map((v,i)=>b[i]-v))*2);for(let i=0;i<=n;i++){let t=i/n;box(...a.map((v,k)=>v+(b[k]-v)*t-r/2),r,r,r,c);}}

// One 2x2 warehouse. W1=x-min loading doors; W2=y-min stacked cargo.
box(5,5,0,20,20,2.2,P.mortar);box(5,5,1,20,20,2.4,P.stone);
box(5.2,5.2,3.4,19.6,19.6,17.6,P.wood);
// Vertical planks have fixed widths and grain, wrapped around the building.
for(const side of [0,1,2,3])for(let u=5.5;u<25;u+=1.7){let c=Math.round(u*10)%3?P.wood:P.woodlight;if(side===0)box(4.9,u,3.4,.45,1.5,17.6,c);if(side===1)box(u,4.9,3.4,1.5,.45,17.6,c);if(side===2)box(24.8,u,3.4,.45,1.5,17.6,c);if(side===3)box(u,24.8,3.4,1.5,.45,17.6,c);}
for(const x of [4.6,24.4])for(const y of [4.6,24.4])box(x,y,3.2,1.1,1.1,18,P.timber);
for(const z of [3.7,20.5]){box(4.7,4.7,z,20.7,.9,.9,P.timber);box(4.7,24.6,z,20.7,.9,.9,P.timber);box(4.7,4.7,z,.9,20.7,.9,P.timber);box(24.6,4.7,z,.9,20.7,.9,P.timber);}
for(const y of [4.6,25.1]){box(14.7,y,3.5,1,1,17.5,P.timber);beam([6,y,4.5],[14.7,y,12],1,P.timber);beam([15,y,12],[24.2,y,4.5],1,P.timber);}
for(const x of [4.6,25.1]){beam([x,5.6,4.5],[x,9,10],.9,P.timber);beam([x,21,10],[x,24.2,4.5],.9,P.timber);}
// Gable infill and joined rafters.
poly([[5.1,5.1,21],[5.1,15,33],[5.1,24.9,21]],P.wood);poly([[24.9,24.9,21],[24.9,15,33],[24.9,5.1,21]],P.wood);
for(const x of [4.8,25.2]){beam([x,5.1,21],[x,15,33],1,P.timber);beam([x,15,33],[x,24.9,21],1,P.timber);beam([x,15,21],[x,15,32],1,P.timber);}
// Deep single loading opening with two plank leaves, shared central seam.
box(4.15,9.5,3.2,.65,11,13.8,P.edge);
for(let u=9.9;u<20.2;u+=1.4)box(3.9,u,3.6,.5,1.15,12.7,P.woodlight);
for(const u of [9.2,20.3])box(3.7,u,3,1,1.1,14.5,P.timber);
box(3.7,9.2,16.6,1,12.2,1,P.timber);box(3.5,14.75,3.6,.6,.5,12.7,P.edge);
for(const z of [5.3,14]){box(3.35,10.2,z,.6,4.3,.65,P.iron);box(3.35,15.5,z,.6,4.1,.65,P.iron);}
beam([3.3,10.2,6],[3.3,14.3,13],.65,P.wood);beam([3.3,15.6,13],[3.3,19.6,6],.65,P.wood);
for(const u of [14.1,15.8])box(3.1,u,9,.5,.6,1.1,P.iron);
box(1.7,9.3,0,4.2,12,1,P.stone);box(2.4,9.8,1,3.5,11,1,P.stone);
// Loft vent rather than a domestic glazed window.
box(4.7,12.8,23,.5,4.4,4.5,P.window);for(const u of [13.4,15,16.5])box(4.4,u,23,.5,.5,4.5,P.timber);
box(25,12.8,23,.5,4.4,4.5,P.window);for(const u of [13.4,15,16.5])box(25.5,u,23,.3,.5,4.5,P.timber);
// A simple dark board sign fixed above the loading door.
box(3.6,11.4,18,.6,7.3,2.4,P.timber);box(3.3,12.8,18.7,.5,1.2,1.1,P.woodlight);box(3.3,15,18.7,.5,1.2,1.1,P.woodlight);
function roof(x0,x1,y0,y1,z,rise){let mid=(y0+y1)/2;
 // Thick overlapping bundles, laid from the eave toward the ridge.
 for(const end of [y0,y1])for(let row=0;row<5;row++){
  let t=row/5,tt=Math.min(1,(row+1)/5+.035),a=mid+(end-mid)*t,b=mid+(end-mid)*tt;
  for(let x=x0;x<x1;x+=1.6){let xx=Math.min(x1,x+1.6),s=Math.floor((x-x0)/1.6),rag=row===4?((s*7)%5)*.16:0;
   let bz=z+rise*(1-tt)-rag,az=z+rise*(1-t)+.28;
   poly([[x,a,az],[xx,a,az],[xx,b,bz],[x,b,bz]],P.thatch);
   poly([[x,b,bz],[xx,b,bz],[xx,b,bz-.5],[x,b,bz-.5]],P.darkstraw);
  }
 }
 // A bundled ridge cap and uneven straw tips, not a straight wooden roof border.
 beam([x0,mid,z+rise+.5],[x1,mid,z+rise+.5],1.5,P.straw);
}
roof(3.3,26.7,3.3,26.7,21.3,12);
// Crates under the W2 eaves, only on this one side of the same model.
function crate(x,y,z,s){box(x,y,z,s,s,s,P.timber);box(x+.25,y-.1,z+.45,s-.5,.3,s-.9,P.woodlight);box(x-.1,y+.25,z+.45,.3,s-.5,s-.9,P.wood);for(const zz of [z+.5,z+s-1]){box(x-.2,y,zz,.4,s,.6,P.timber);box(x,y-.2,zz,s,.4,.6,P.timber);}beam([x-.22,y+.5,z+1],[x-.22,y+s-.5,z+s-1],.5,P.timber);}
crate(10,.9,0,3.7);crate(14.1,.9,0,3.7);crate(12.2,1.1,3.7,3.3);
// Proper coopered barrels: bulging staves, inset plank lids and fitted iron hoops.
function barrel(cx,cy){
 const sides=12,levels=[[.2,2.05],[1,2.22],[3.8,2.5],[6.7,2.22],[7.5,2.05]];
 const ring=(z,r)=>Array.from({length:sides},(_,i)=>[cx+Math.cos(i*Math.PI*2/sides)*r,cy+Math.sin(i*Math.PI*2/sides)*r,z]);
 const oak=['#aa8050','#987044','#b58b56','#9f7647'];
 for(let j=0;j<levels.length-1;j++){let a=ring(...levels[j]),b=ring(...levels[j+1]);for(let i=0;i<sides;i++){
  poly([a[i],a[(i+1)%sides],b[(i+1)%sides],b[i]],oak[i%4]);
  const t=.09,edge=(p,q)=>p.map((v,k)=>v+(q[k]-v)*t);
  poly([a[i],edge(a[i],a[(i+1)%sides]),edge(b[i],b[(i+1)%sides]),b[i]],'#775537');
 }}
 function radius(z){for(let i=0;i<levels.length-1;i++)if(z<=levels[i+1][0]){let t=(z-levels[i][0])/(levels[i+1][0]-levels[i][0]);return levels[i][1]*(1-t)+levels[i+1][1]*t;}return 2.05;}
 for(const z of [1.2,5.9]){let a=ring(z,radius(z)+.08),b=ring(z+.65,radius(z+.65)+.08);for(let i=0;i<sides;i++)poly([a[i],a[(i+1)%sides],b[(i+1)%sides],b[i]],'#3f423c');}
 // Top rim encloses the lighter inset lid: neither a square crate nor an open tub.
 poly(ring(7.51,2.06),'#674a30');poly(ring(7.54,1.7),'#bc975f');
 for(const dx of [-.65,.65])poly([[cx+dx-.06,cy-1.48,7.56],[cx+dx+.06,cy-1.48,7.56],[cx+dx+.06,cy+1.48,7.56],[cx+dx-.06,cy+1.48,7.56]],'#866039');
}
barrel(19.5,2.9);barrel(24.7,2.9);
// All geometry, including roof and cargo, is inside one 2x2 plan square.
const limit=32/1.1;
for(const f of faces)for(const [x,y]of f.v)if(x<0||y<0||x>limit||y>limit)throw Error('Geometry outside 2x2 footprint: '+[x,y]);
console.log('PASS: every roof, wall and prop vertex is inside the 2x2 footprint');
function rgb(h){return h.match(/[a-f\d]{2}/gi).map(x=>parseInt(x,16));}
function surface(f,q,normal,shade){let[x,y,z]=q,c=f.c;
 if(f.mat==='thatch'){
   // Golden bundled reeds, horizontal binding courses, and dark undercuts.
   let strand=Math.floor(x*2.7),course=Math.floor((z-21.3)/2.4),k=((strand*11+course*3)%17+17)%17;
   let seam=((z-21.3)/2.4%1+1)%1;
   c=f.c===P.darkstraw?P.darkstraw:seam<.09?'#98662d':k<3?P.straw:k<7?'#d2a054':k===10?'#ad7832':P.thatch;
 }else if(f.mat==='stone'){
   let along=Math.abs(normal[0])>Math.abs(normal[1])?y:x;
   let row=Math.floor(z/1.6),brick=(along+row%2*1.8)%3.4;
   c=(z%1.6<.27||brick<.28)?P.mortar:((Math.floor(along/3.4)+row)%3===0?'#aaa087':P.stone);
 }else if(f.mat==='wood'){
   const dx=Math.max(...f.v.map(v=>v[0]))-Math.min(...f.v.map(v=>v[0]));
   let along=dx<.7?y:x;
   if(f.c!==P.timber){
    let board=Math.floor((along-5.5)/1.7),phase=((along-5.5)%1.7+1.7)%1.7;
    let grain=phase+.13*Math.sin(z*.65+board*2);
    c=phase<.17?'#61432c':board%3===0?'#a47a4c':board%3===1?'#946b43':'#9f7448';
    if(grain>.48&&grain<.66&&Math.floor(z/3+board)%3!==0)c='#79532f';
    if(Math.abs(z-(8+(board*7%9)))<.6&&phase>.65&&phase<1.15)c='#715034';
   }
 }else if(f.mat==='plaster'){
   let k=(Math.floor(x*2)*37+Math.floor(y*2)*17+Math.floor(z*2)*13)%101;
   if(k<5)c='#dac79b';
 }
 return rgb(c).map(v=>Math.round(v*shade));
}
function render(rot){let rgba=Buffer.alloc(64*88*4),depth=new Float64Array(64*88).fill(-1e9);function tr(v){let[x,y,z]=v;for(let i=0;i<rot;i++)[x,y]=[y,limit-x];return[x,y,z];}function proj(v){let[x,y,z]=v;return[32+(x-y)*1.1,88-(x+y)*.55-z,z*2-x-y];}
for(let f of faces){let vv=f.v.map(tr),a=vv[0],b=vv[1],c=vv[2],u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);let normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];let shade=Math.abs(normal[2])>Math.abs(normal[0])+Math.abs(normal[1])?.99:Math.abs(normal[0])>Math.abs(normal[1])?1:.81;let col=rgb(f.c).map(v=>Math.round(v*shade));let p=vv.map(proj);for(let j=1;j<p.length-1;j++){let t=[p[0],p[j],p[j+1]],area=(t[1][0]-t[0][0])*(t[2][1]-t[0][1])-(t[1][1]-t[0][1])*(t[2][0]-t[0][0]);if(Math.abs(area)<.001)continue;let minx=Math.max(0,Math.floor(Math.min(...t.map(v=>v[0])))),maxx=Math.min(63,Math.ceil(Math.max(...t.map(v=>v[0])))),miny=Math.max(0,Math.floor(Math.min(...t.map(v=>v[1])))),maxy=Math.min(87,Math.ceil(Math.max(...t.map(v=>v[1]))));for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){let px=x+.5,py=y+.5,w1=((t[1][0]-px)*(t[2][1]-py)-(t[1][1]-py)*(t[2][0]-px))/area,w2=((t[2][0]-px)*(t[0][1]-py)-(t[2][1]-py)*(t[0][0]-px))/area,w3=1-w1-w2;if(Math.min(w1,w2,w3)<-1e-7)continue;let d=w1*t[0][2]+w2*t[1][2]+w3*t[2][2],idx=y*64+x;if(d>=depth[idx]){depth[idx]=d;let world=[0,1,2].map(k=>w1*f.v[0][k]+w2*f.v[j][k]+w3*f.v[j+1][k]);let color=surface(f,world,normal,shade);rgba.set([...color,255],idx*4);}}}}
return rgba;}
let crcTable=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});function crc(b){let c=0xffffffff;for(let v of b)c=crcTable[(c^v)&255]^(c>>>8);return(c^0xffffffff)>>>0;}function chunk(n,b){let name=Buffer.from(n),h=Buffer.alloc(4),tail=Buffer.alloc(4);h.writeUInt32BE(b.length);tail.writeUInt32BE(crc(Buffer.concat([name,b])));return Buffer.concat([h,name,b,tail]);}
function png(file,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)p.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(file,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]));}
let sheet=Buffer.alloc(256*352*4);for(let r=0;r<4;r++){let pixels=render(r);png(path.join(out,`storehouse_${r+1}.png`),64,88,pixels);let count=0,minx=64,maxx=0,miny=88,maxy=0;for(let y=0;y<88;y++)for(let x=0;x<64;x++){let i=(y*64+x)*4;if(pixels[i+3]){count++;minx=Math.min(x,minx);maxx=Math.max(x,maxx);miny=Math.min(y,miny);maxy=Math.max(y,maxy);}for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){let sx=(r%2)*128+x*2+dx,sy=Math.floor(r/2)*176+y*2+dy;pixels.copy(sheet,(sy*256+sx)*4,i,i+4);}}if(minx<2||maxx>61||maxy>85||count<500)throw Error(`Bad fit view ${r+1}`);console.log(`view ${r+1}: 64x88, alpha 0/255, bounds ${minx},${miny}..${maxx},${maxy}`);}png(path.join(out,'storehouse_four_views.png'),256,352,sheet);
let zoom=Buffer.alloc(512*704*4);for(let y=0;y<704;y++)for(let x=0;x<512;x++){let i=(Math.floor(y/2)*256+Math.floor(x/2))*4;sheet.copy(zoom,(y*512+x)*4,i,i+4);}png(path.join(out,'storehouse_detail.png'),512,704,zoom);
