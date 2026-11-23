const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'cottage-level2');fs.mkdirSync(out,{recursive:true});
const P={edge:'#463321',timber:'#69462c',wood:'#986b38',woodlight:'#bd9153',plaster:'#e8d7ac',shade:'#c9b78b',stone:'#948a72',mortar:'#574f40',thatch:'#a65037',straw:'#ce7950',darkstraw:'#743b2f',window:'#302b23',iron:'#5b5951'};
let faces=[];
function poly(v,c){faces.push({v,c,mat:[P.thatch,P.straw,P.darkstraw].includes(c)?'thatch':c===P.stone?'stone':c===P.plaster?'plaster':[P.wood,P.timber,P.woodlight].includes(c)?'wood':''});}
function box(x,y,z,w,d,h,c){let a=[x,y,z],b=[x+w,y,z],c1=[x+w,y+d,z],e=[x,y+d,z],A=[x,y,z+h],B=[x+w,y,z+h],C=[x+w,y+d,z+h],E=[x,y+d,z+h];poly([a,b,B,A],c);poly([b,c1,C,B],c);poly([c1,e,E,C],c);poly([e,a,A,E],c);poly([A,B,C,E],c);}
function beam(a,b,r,c){let n=Math.ceil(Math.hypot(...a.map((v,i)=>b[i]-v))*2);for(let i=0;i<=n;i++){let t=i/n;box(...a.map((v,k)=>v+(b[k]-v)*t-r/2),r,r,r,c);}}

// Cottage: one compact body; W1 x-min has the only door, W2 y-min the flower window.
box(5,5,0,20,20,1.5,P.mortar);box(5,5,1,20,20,2.6,P.stone);
box(5.3,5.3,3.6,19.4,19.4,16.4,P.plaster);
// Structural framing is a continuous set of wall-face quads, not subpixel blocks
// buried inside the plaster. The unequal bays have fixed identities in all views.
function frame(side,u0,z0,u1,z1,width=1.35){
 const len=Math.hypot(u1-u0,z1-z0),du=-(z1-z0)/len*width/2,dz=(u1-u0)/len*width/2;
 const point=(u,z)=>side===0?[4.65,u,z]:side===1?[u,4.65,z]:side===2?[25.05,u,z]:[u,25.05,z];
 poly([point(u0+du,z0+dz),point(u1+du,z1+dz),point(u1-du,z1-dz),point(u0-du,z0-dz)],P.timber);
}
for(let side=0;side<4;side++){
 // A small cottage needs only corner posts and two light rails. Extra uprights
 // collide with the window and door frames when sampled at 32 pixels wide.
 for(const u of [5.5,24.5])frame(side,u,4,u,19.6,1.15);
 frame(side,5.3,4.1,24.7,4.1,1.05);
 frame(side,5.3,19.5,24.7,19.5,1.05);
 if(side===0){frame(side,5.5,14,9.3,19.3,.9);frame(side,20,19.3,24.5,14,.9);}
 if(side===1){frame(side,5.5,14.2,8.8,19.3,.9);frame(side,20.5,19.3,24.5,14,.9);}
 if(side===2){frame(side,15,4.4,15,19.3,1.05);frame(side,5.5,14,10.2,19.3,.9);frame(side,19.6,19.3,24.5,14,.9);}
 if(side===3){frame(side,5.5,14.2,8.8,19.3,.9);frame(side,20.5,19.3,24.5,14,.9);}
}
// Gable king posts and rafters meet the wall plate without dangling ends.
poly([[5.3,5.3,20],[5.3,15,32],[5.3,24.7,20]],P.plaster);poly([[24.7,24.7,20],[24.7,15,32],[24.7,5.3,20]],P.plaster);
for(const side of [0,2]){frame(side,5.3,20,15,32,1.05);frame(side,15,32,24.7,20,1.05);frame(side,15,19.5,15,31.5,1.05);}
// One recessed plank door, 9px tall in the native sprite including the frame.
box(4.65,11,1.5,.6,6.2,13,P.edge);for(let y=11.4;y<16.8;y+=1.2)box(4.3,y,2,.6,1,11.8,P.wood);
for(const z of [4,11])box(4.05,11.8,z,.5,4.8,.6,P.iron);box(3.8,15.5,7,.7,.6,.8,P.iron);
box(2.6,10.5,0,3.6,7.4,1,P.stone);box(3,11,1,2.5,6.5,1,P.stone);
// A glazed window and projecting flower box on W2, and a shuttered window on W4.
function windowY(y,x,z,flowers){box(x-.6,y,z-.5,7.2,.8,7.1,P.timber);box(x,y+(y>15?.85:-.15),z,6,.9,5.8,'#657c77');box(x+.5,y+(y>15?1:-.35),z+3,5,.8,.5,'#b0c5b8');box(x+2.8,y+(y>15?1.1:-.5),z,.5,.8,5.8,P.iron);box(x,y+(y>15?1.1:-.5),z+2.7,6,.8,.5,P.iron);box(x-.8,y+(y>15?.65:-.8),z-1,7.5,1.7,.8,P.woodlight);if(flowers){box(x-.5,y+(y>15?1:-1.7),z-3,7,2,2,P.wood);box(x,y+(y>15?1.1:-1.8),z-1.6,6,2,.6,'#4f6639');for(let i=0;i<3;i++){box(x+.6+i*1.8,y+(y>15?1.2:-1.7),z-.9,.7,.7,.7,i%2?'#d3bf73':'#a66b50');}}}
windowY(4.6,12,10,true);windowY(25,12,10,true);
// Back gable's small wooden ventilation opening.
box(24.9,13.8,21,.5,2.5,3,P.window);box(25.1,14.7,21,.3,.5,3,P.timber);
// Fired-clay roof tiles, broad continuous roof slopes, true rotated geometry.
function roof(x0,x1,y0,y1,z,rise){let mid=(y0+y1)/2;poly([[x0,y0,z],[x1,y0,z],[x1,mid,z+rise],[x0,mid,z+rise]],P.thatch);poly([[x0,mid,z+rise],[x1,mid,z+rise],[x1,y1,z],[x0,y1,z]],P.thatch);for(const y of [y0,y1])beam([x0,y,z],[x1,y,z],1,P.darkstraw);beam([x0,mid,z+rise+.3],[x1,mid,z+rise+.3],1.2,P.straw);}
roof(3.6,26.4,3.6,26.4,20,13);
// A restrained little tiled hood over the only entrance.
poly([[2.8,10.3,15],[5.4,10.3,16.5],[5.4,18.5,16.5],[2.8,18.5,15]],P.thatch);beam([2.8,10.3,15],[2.8,18.5,15],.7,P.darkstraw);
for(const y of [10.7,17.7])beam([4.5,y,12.7],[3.1,y,14.8],.7,P.wood);
// One chimney, permanently at the W3/W4 roof corner.
box(20,20,26,3.2,3.2,11,P.stone);box(19.5,19.5,36.8,4.2,4.2,1.2,P.stone);box(20.4,20.4,38.05,2.5,2.5,.15,P.window);
// Low patches of aged lime plaster, not a repeated speckle texture.
poly([[5.22,6,4],[5.22,10,4],[5.22,9.2,6.2],[5.22,6.8,5.6]],'#d7c397');
poly([[18,4.98,4],[23,4.98,4],[22,4.98,6],[19.7,4.98,5.4]],'#d7c397');poly([[18,25.02,4],[23,25.02,4],[22,25.02,6],[19.7,25.02,5.4]],'#d7c397');
// A few replacement tiles interrupt the otherwise orderly clay courses.
for(const [x,y,z] of [[8,9,26.1],[18,7,23.7],[11,21,25.8]]){
 poly([[x,y,z+.08],[x+2,y,z+.08],[x+2,y+1,z+1.22],[x,y+1,z+1.22]],'#b86a48');
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
function render(rot){let rgba=Buffer.alloc(32*48*4),depth=new Float64Array(32*48).fill(-1e9);function tr(v){let[x,y,z]=v;for(let i=0;i<rot;i++)[x,y]=[y,30-x];return[x,y,z];}function proj(v){let[x,y,z]=v;return[16+(x-y)*.55,48-(x+y)*.275-z*.65,z*2-x-y];}
for(let f of faces){let vv=f.v.map(tr),a=vv[0],b=vv[1],c=vv[2],u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);let normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];let shade=Math.abs(normal[2])>Math.abs(normal[0])+Math.abs(normal[1])?.99:Math.abs(normal[0])>Math.abs(normal[1])?1:.81;let col=rgb(f.c).map(v=>Math.round(v*shade));let p=vv.map(proj);for(let j=1;j<p.length-1;j++){let t=[p[0],p[j],p[j+1]],area=(t[1][0]-t[0][0])*(t[2][1]-t[0][1])-(t[1][1]-t[0][1])*(t[2][0]-t[0][0]);if(Math.abs(area)<.001)continue;let minx=Math.max(0,Math.floor(Math.min(...t.map(v=>v[0])))),maxx=Math.min(31,Math.ceil(Math.max(...t.map(v=>v[0])))),miny=Math.max(0,Math.floor(Math.min(...t.map(v=>v[1])))),maxy=Math.min(47,Math.ceil(Math.max(...t.map(v=>v[1]))));for(let y=miny;y<=maxy;y++)for(let x=minx;x<=maxx;x++){let px=x+.5,py=y+.5,w1=((t[1][0]-px)*(t[2][1]-py)-(t[1][1]-py)*(t[2][0]-px))/area,w2=((t[2][0]-px)*(t[0][1]-py)-(t[2][1]-py)*(t[0][0]-px))/area,w3=1-w1-w2;if(Math.min(w1,w2,w3)<-1e-7)continue;let d=w1*t[0][2]+w2*t[1][2]+w3*t[2][2],idx=y*32+x;if(d>=depth[idx]){depth[idx]=d;let world=[0,1,2].map(k=>w1*f.v[0][k]+w2*f.v[j][k]+w3*f.v[j+1][k]);let color=surface(f,world,normal,shade);rgba.set([...color,255],idx*4);}}}}
return rgba;}
let crcTable=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});function crc(b){let c=0xffffffff;for(let v of b)c=crcTable[(c^v)&255]^(c>>>8);return(c^0xffffffff)>>>0;}function chunk(n,b){let name=Buffer.from(n),h=Buffer.alloc(4),tail=Buffer.alloc(4);h.writeUInt32BE(b.length);tail.writeUInt32BE(crc(Buffer.concat([name,b])));return Buffer.concat([h,name,b,tail]);}
function png(file,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)p.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(file,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]));}

let sheet=Buffer.alloc(256*384*4);let widths=[];
for(let r=0;r<4;r++){let pixels=render(r);png(path.join(out,`house_2_${r+1}.png`),32,48,pixels);let xs=[],ys=[];for(let y=0;y<48;y++)for(let x=0;x<32;x++){let i=(y*32+x)*4;if(pixels[i+3]){xs.push(x);ys.push(y);if(pixels[i]>150&&pixels[i+2]>100&&pixels[i+2]>pixels[i+1]+20)throw Error('Pink fringe');}for(let dy=0;dy<4;dy++)for(let dx=0;dx<4;dx++){let sx=r%2*128+x*4+dx,sy=Math.floor(r/2)*192+y*4+dy;pixels.copy(sheet,(sy*256+sx)*4,i,i+4);}}let l=Math.min(...xs),rr=Math.max(...xs),top=Math.min(...ys),bottom=Math.max(...ys);if(l<3||rr>28||top<1||bottom>46)throw Error('Bad fit view '+r+': '+[l,rr,top,bottom]);widths.push(rr-l+1);console.log('view '+(r+1)+':32x48 alpha 0/255 bounds '+[l,top,rr,bottom]);}if(Math.max(...widths)-Math.min(...widths)>2)throw Error('Inconsistent widths');png(path.join(out,'cottage_level2_four_views.png'),256,384,sheet);
