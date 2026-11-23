const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'large-house');fs.mkdirSync(out,{recursive:true});
const P={edge:'#463321',timber:'#69462c',wood:'#986b38',woodlight:'#bd9153',plaster:'#e8d7ac',shade:'#c9b78b',stone:'#948a72',mortar:'#574f40',thatch:'#c28b3f',straw:'#dcad59',darkstraw:'#a16f2f',window:'#302b23',iron:'#5b5951'};
let faces=[];
function poly(v,c){faces.push({v,c,mat:[P.thatch,P.straw,P.darkstraw].includes(c)?'thatch':c===P.stone?'stone':c===P.plaster?'plaster':[P.wood,P.timber,P.woodlight].includes(c)?'wood':''});}
function box(x,y,z,w,d,h,c){let a=[x,y,z],b=[x+w,y,z],c1=[x+w,y+d,z],e=[x,y+d,z],A=[x,y,z+h],B=[x+w,y,z+h],C=[x+w,y+d,z+h],E=[x,y+d,z+h];poly([a,b,B,A],c);poly([b,c1,C,B],c);poly([c1,e,E,C],c);poly([e,a,A,E],c);poly([A,B,C,E],c);}
function beam(a,b,r,c){let n=Math.ceil(Math.hypot(...a.map((v,i)=>b[i]-v))*2);for(let i=0;i<=n;i++){let t=i/n;box(...a.map((v,k)=>v+(b[k]-v)*t-r/2),r,r,r,c);}}
// One model: W1 is x-min (entrance), W2 is y-min (wing), W3 x-max, W4 y-max.
box(4,4,0,22,23,2,P.mortar);box(4,4,1,22,23,2,P.stone);
box(4.5,4.5,3,21,22,17,P.plaster);
// Fixed lower wing at W2. Main hall ridge runs along x.
box(12,1.5,0,12,8,2,P.mortar);box(12,1.5,2,12,8,12,P.plaster);
function gable(x0,x1,y0,y1,z,rise){let ym=(y0+y1)/2;poly([[x0,y0,z],[x0,ym,z+rise],[x0,y1,z]],P.plaster);poly([[x1,y1,z],[x1,ym,z+rise],[x1,y0,z]],P.plaster);}
gable(4.5,25.5,4.5,26.5,20,13);
// Timber frames wrap all walls, including unseen back faces.
for(let x of [4.2,11,18,25.2])for(let y of [4.2,26.2])box(x,y,3,.8,.8,17,P.timber);
for(let y of [4.2,11.5,19,26.2])for(let x of [4.2,25.2])box(x,y,3,.8,.8,17,P.timber);
for(let z of [3,11,19]){box(4.2,4.2,z,21.8,.8,.8,P.timber);box(4.2,26.2,z,21.8,.8,.8,P.timber);box(4.2,4.2,z,.8,22.8,.8,P.timber);box(25.2,4.2,z,.8,22.8,.8,P.timber);}
for(let x of [4.1,25.6]){beam([x,5,12],[x,10,18],.7,P.timber);beam([x,20,18],[x,25,12],.7,P.timber);beam([x,5,20],[x,15.5,32],.7,P.timber);beam([x,26,20],[x,15.5,32],.7,P.timber);beam([x,15.5,20],[x,15.5,32],.7,P.timber);}
for(let y of [4.1,26.6]){beam([5,y,4],[10,y,10],.7,P.timber);beam([19,y,10],[24,y,4],.7,P.timber);}
// Four windows fixed to their wall, dark recess, plank shutters. No glass.
function win(axis,plane,u,z=10){let x=axis==='x'?plane:u,y=axis==='x'?u:plane;box(x,y,z,axis==='x'?.45:4.5,axis==='x'?4.5:.45,5,P.timber);box(x-.08,y-.08,z+1,axis==='x'?.6:4.5,axis==='x'?4.5:.6,3,P.window);for(let du of [0,3.5]){let xx=axis==='x'?x-.18:x+du,yy=axis==='x'?y+du:y-.18;box(xx,yy,z+.4,axis==='x'?.8:1.2,axis==='x'?1.2:.8,4.2,P.wood);}}
win('x',3.9,20);win('y',1.1,16,6);win('x',25.7,12);win('y',26.7,14);
// Single door and compact porch on W1.
box(3.8,10,3,.9,5,9,P.edge);for(let y=10.4;y<15;y+=1)box(3.5,y,3.3,.6,.72,8.2,P.wood);for(let z of [5,10])box(3.25,10.5,z,.5,4.1,.45,P.iron);box(3,13.7,7,.7,.55,.55,P.iron);
box(1.8,9.5,0,3.3,6.3,1.2,P.stone);box(1.5,10,1,1.8,5.2,.9,P.stone);
for(let y of [9.7,15.1])box(2,y,2,.7,.7,10,P.timber);
// Roof surfaces subdivided into coherent thatch courses, not random noise.
function roof(x0,x1,y0,y1,z,rise){let mid=(y0+y1)/2;for(let side of [-1,1]){let end=side<0?y0:y1;for(let row=0;row<7;row++){let t=row/7,t2=(row+1)/7;let ya=mid+(end-mid)*t,yb=mid+(end-mid)*t2,za=z+rise*(1-t),zb=z+rise*(1-t2);for(let x=x0;x<x1;x+=2.5){let xe=Math.min(x1,x+2.5),alt=(Math.floor(x*2)+row)%4;poly([[x,ya,za],[xe,ya,za],[xe,yb,zb],[x,yb,zb]],alt===0?P.straw:alt===2?P.darkstraw:P.thatch);}}beam([x0,end,z],[x1,end,z],.7,P.darkstraw);}beam([x0,mid,z+rise+.3],[x1,mid,z+rise+.3],1,P.straw);}
roof(3,27,3,28,20,13);roof(11,25,.5,10,14,5);
// Porch gable shares the W1 entrance attachment, rotates with it.
poly([[1,9,12],[1,12.6,16],[5,12.6,16],[5,9,12]],P.thatch);poly([[1,12.6,16],[1,16.2,12],[5,16.2,12],[5,12.6,16]],P.straw);
// Wing framing.
for(let x of [12,23.5])for(let y of [1.4,9])box(x,y,2,.7,.7,12,P.timber);box(12,1.3,2,12,.7,.8,P.timber);box(12,1.3,13,12,.7,.8,P.timber);
// One chimney at the W3/W4 corner, seated through roof.
box(21,21,25,3,3,13,P.mortar);for(let z=25;z<38;z+=2)for(let k=0;k<2;k++){let offset=((z-25)/2)%2*.65;box(20.9+k*1.5-offset/2,20.9,z,1.4,3.2,1.5,P.stone);}box(20.4,20.4,37,4.2,4.2,1.2,P.stone);box(21.3,21.3,38.25,2.4,2.4,.1,P.window);
// Joinery, projecting sills and hinges: deliberately one-pixel details at game scale.
for(let x of [4.0,25.9])for(let y of [5,11.8,19.3,25.6])for(let z of [4.3,12,18])box(x,y,z,.55,.55,.55,P.woodlight);
for(let y of [4,26.9])for(let x of [5,11.5,18.5,25])for(let z of [4.3,12,18])box(x,y,z,.55,.55,.55,P.woodlight);
function trimWindow(axis,plane,u,z){let b=(along,height,width,depth,h,col)=>axis==='x'?box(plane,along,height,depth,width,h,col):box(along,plane,height,width,depth,h,col);b(u-.4,z-.3,5.3,.75,.7,P.woodlight);b(u-.4,z+4.9,5.3,.55,.65,P.timber);b(u+2,z+1, .45,.65,3,P.woodlight);b(u+.4,z+2.6,3.7,.7,.45,P.wood);for(let q of [u+.2,u+3.7])for(let zz of [z+1,z+3.5])b(q,zz,.7,.8,.4,P.iron);}
trimWindow('x',3.6,20,10);trimWindow('y',.85,16,6);trimWindow('x',26,12,10);trimWindow('y',26.9,14,10);
// Porch edge boards, ridge and honest supporting brackets.
beam([1,9,12],[1,12.6,16],.65,P.darkstraw);beam([1,12.6,16],[1,16.2,12],.65,P.darkstraw);
beam([1,12.6,16],[5,12.6,16],.65,P.straw);
for(let y of [9.9,15.2])beam([2,y,9],[3.7,y,11.4],.55,P.woodlight);
// A small stack of split firewood fixed to the back wall, not copied between views.
for(let layer=0;layer<2;layer++)for(let i=0;i<4-layer;i++){box(8+i*1.3+layer*.5,27.1,1.3+layer*1.1,1.1,1.1,1,P.edge);box(8+i*1.3+layer*.5,27.8,1.3+layer*1.1,1.1,.5,1,P.woodlight);}
function rgb(h){return h.match(/[a-f\d]{2}/gi).map(x=>parseInt(x,16));}
function surface(f,q,normal,shade){let[x,y,z]=q,c=f.c;
 if(f.mat==='thatch'){
   // Golden bundled reeds, horizontal binding courses, and dark undercuts.
   let course=Math.floor(z/2.2),strand=Math.floor(x*1.25),k=((strand*7)%13+13)%13;
   let seam=(z/2.2)%1;
   c=seam<.10?P.darkstraw:k<2?P.straw:k<5?'#cc9848':P.thatch;
 }else if(f.mat==='stone'){
   let along=Math.abs(normal[0])>Math.abs(normal[1])?y:x;
   let row=Math.floor(z/1.6),brick=(along+row%2*1.8)%3.4;
   c=(z%1.6<.27||brick<.28)?P.mortar:((Math.floor(along/3.4)+row)%3===0?'#aaa087':P.stone);
 }else if(f.mat==='wood'){
   let along=Math.abs(normal[0])>Math.abs(normal[1])?y:x;
   if((Math.floor(along*3)+Math.floor(z*2)*7)%19===0)c=P.woodlight;
 }else if(f.mat==='plaster'){
   let k=(Math.floor(x*2)*37+Math.floor(y*2)*17+Math.floor(z*2)*13)%101;
   if(k<5)c='#dac79b';
 }
 return rgb(c).map(v=>Math.round(v*shade));
}
function render(rot){let rgba=Buffer.alloc(64*88*4),depth=new Float64Array(64*88).fill(-1e9);function tr(v){let[x,y,z]=v;for(let i=0;i<rot;i++)[x,y]=[y,30-x];return[x,y,z];}function proj(v){let[x,y,z]=v;return[32+(x-y)*1.1,90-(x+y)*.55-z,z*2-x-y];}
for(let f of faces){let vv=f.v.map(tr),a=vv[0],b=vv[1],c=vv[2],u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);let normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];let shade=Math.abs(normal[2])>Math.abs(normal[0])+Math.abs(normal[1])?.99:Math.abs(normal[0])>Math.abs(normal[1])?1:.81;let col=rgb(f.c).map(v=>Math.round(v*shade));let p=vv.map(proj);for(let j=1;j<p.length-1;j++){let t=[p[0],p[j],p[j+1]],area=(t[1][0]-t[0][0])*(t[2][1]-t[0][1])-(t[1][1]-t[0][1])*(t[2][0]-t[0][0]);if(Math.abs(area)<.001)continue;let minx=Math.max(0,Math.floor(Math.min(...t.map(v=>v[0])))),maxx=Math.min(63,Math.ceil(Math.max(...t.map(v=>v[0])))),miny=Math.max(0,Math.floor(Math.min(...t.map(v=>v[1])))),maxy=Math.min(87,Math.ceil(Math.max(...t.map(v=>v[1]))));for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){let px=x+.5,py=y+.5,w1=((t[1][0]-px)*(t[2][1]-py)-(t[1][1]-py)*(t[2][0]-px))/area,w2=((t[2][0]-px)*(t[0][1]-py)-(t[2][1]-py)*(t[0][0]-px))/area,w3=1-w1-w2;if(Math.min(w1,w2,w3)<-1e-7)continue;let d=w1*t[0][2]+w2*t[1][2]+w3*t[2][2],idx=y*64+x;if(d>=depth[idx]){depth[idx]=d;let world=[0,1,2].map(k=>w1*f.v[0][k]+w2*f.v[j][k]+w3*f.v[j+1][k]);let color=surface(f,world,normal,shade);rgba.set([...color,255],idx*4);}}}}
return rgba;}
let crcTable=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});function crc(b){let c=0xffffffff;for(let v of b)c=crcTable[(c^v)&255]^(c>>>8);return(c^0xffffffff)>>>0;}function chunk(n,b){let name=Buffer.from(n),h=Buffer.alloc(4),tail=Buffer.alloc(4);h.writeUInt32BE(b.length);tail.writeUInt32BE(crc(Buffer.concat([name,b])));return Buffer.concat([h,name,b,tail]);}
function png(file,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)p.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(file,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]));}
let sheet=Buffer.alloc(256*352*4);for(let r=0;r<4;r++){let pixels=render(r);png(path.join(out,`manor_1_${r+1}.png`),64,88,pixels);let count=0,minx=64,maxx=0,miny=88,maxy=0;for(let y=0;y<88;y++)for(let x=0;x<64;x++){let i=(y*64+x)*4;if(pixels[i+3]){count++;minx=Math.min(x,minx);maxx=Math.max(x,maxx);miny=Math.min(y,miny);maxy=Math.max(y,maxy);}for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){let sx=(r%2)*128+x*2+dx,sy=Math.floor(r/2)*176+y*2+dy;pixels.copy(sheet,(sy*256+sx)*4,i,i+4);}}if(minx<2||maxx>61||maxy>85||count<500)throw Error(`Bad fit view ${r+1}`);console.log(`view ${r+1}: 64x88, alpha 0/255, bounds ${minx},${miny}..${maxx},${maxy}`);}png(path.join(out,'large_house_four_views.png'),256,352,sheet);
let zoom=Buffer.alloc(512*704*4);for(let y=0;y<704;y++)for(let x=0;x<512;x++){let i=(Math.floor(y/2)*256+Math.floor(x/2))*4;sheet.copy(zoom,(y*512+x)*4,i,i+4);}png(path.join(out,'large_house_detail.png'),512,704,zoom);
