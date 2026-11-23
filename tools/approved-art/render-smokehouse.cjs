const fs=require('fs'),path=require('path'),zlib=require('zlib');
const out=path.resolve(__dirname,'smokehouse');fs.mkdirSync(out,{recursive:true});
const P={edge:'#463321',timber:'#69462c',wood:'#986b38',woodlight:'#bd9153',plaster:'#e8d7ac',shade:'#c9b78b',stone:'#948a72',mortar:'#574f40',thatch:'#596052',straw:'#7d8068',darkstraw:'#383f37',window:'#302b23',iron:'#5b5951'};
let faces=[];
function poly(v,c){faces.push({v,c,mat:[P.thatch,P.straw,P.darkstraw].includes(c)?'thatch':c===P.stone?'stone':c===P.plaster?'plaster':[P.wood,P.timber,P.woodlight].includes(c)?'wood':''});}
function box(x,y,z,w,d,h,c){let a=[x,y,z],b=[x+w,y,z],c1=[x+w,y+d,z],e=[x,y+d,z],A=[x,y,z+h],B=[x+w,y,z+h],C=[x+w,y+d,z+h],E=[x,y+d,z+h];poly([a,b,B,A],c);poly([b,c1,C,B],c);poly([c1,e,E,C],c);poly([e,a,A,E],c);poly([A,B,C,E],c);}
function beam(a,b,r,c){let n=Math.ceil(Math.hypot(...a.map((v,i)=>b[i]-v))*2);for(let i=0;i<=n;i++){let t=i/n;box(...a.map((v,k)=>v+(b[k]-v)*t-r/2),r,r,r,c);}}


// 2x2 smokehouse. W1 x-min entrance; W2 y-min covered meat rack.
box(6.5,6.5,0,19,19,1.3,P.mortar);box(6.5,6.5,1,19,19,3.5,P.stone);
box(7,7,4.5,18,18,17,P.wood);
for(let u=7.2;u<25;u+=1.7){box(6.7,u,4.5,.5,1.4,17,P.wood);box(u,6.7,4.5,1.4,.5,17,P.wood);box(24.9,u,4.5,.5,1.4,17,P.wood);box(u,24.9,4.5,1.4,.5,17,P.wood);}
for(const x of [6.5,24.4])for(const y of [6.5,24.4])box(x,y,4.2,1.1,1.1,17.5,P.timber);
for(const z of [4.3,21]){box(6.5,6.5,z,19,.9,.9,P.timber);box(6.5,24.6,z,19,.9,.9,P.timber);box(6.5,6.5,z,.9,19,.9,P.timber);box(24.6,6.5,z,.9,19,.9,P.timber);}
for(const y of [6.4,25.3]){beam([8,y,5],[13,y,11],.9,P.timber);beam([20,y,11],[24,y,5],.9,P.timber);}
poly([[6.8,6.8,21.5],[6.8,16,32.5],[6.8,25.2,21.5]],P.wood);poly([[25.2,25.2,21.5],[25.2,16,32.5],[25.2,6.8,21.5]],P.wood);
for(const x of [6.5,25.5]){beam([x,6.8,21.5],[x,16,32.5],.9,P.timber);beam([x,16,32.5],[x,25.2,21.5],.9,P.timber);}
// A heavy, smoke-tight plank door with a dark seal and two iron straps.
box(6.1,12.5,3,.8,7.5,14,P.edge);for(let y=13;y<19.8;y+=1.4)box(5.7,y,3.6,.6,1.1,12.6,P.woodlight);
for(const z of [6,13])box(5.3,13,z,.5,6.5,.7,P.iron);box(5.1,18.3,9,.6,.6,.9,P.iron);
box(3.9,12.3,0,3.6,8.1,1.2,P.stone);box(4.6,12.8,1.2,2.4,7,1,P.stone);
// Small smoke vent over the door. No domestic windows.
box(6.2,14,24,.5,4,2,P.window);for(const y of [14.5,16,17.5])box(5.9,y,24,.4,.4,2,P.timber);
// Dark shingles and roof ridge, with restrained edge boards.
function roof(x0,x1,y0,y1,z,rise){const mid=(y0+y1)/2;poly([[x0,y0,z],[x1,y0,z],[x1,mid,z+rise],[x0,mid,z+rise]],P.thatch);poly([[x0,mid,z+rise],[x1,mid,z+rise],[x1,y1,z],[x0,y1,z]],P.thatch);for(const y of [y0,y1])beam([x0,y,z],[x1,y,z],.7,P.darkstraw);beam([x0,mid,z+rise+.2],[x1,mid,z+rise+.2],.9,P.straw);}
roof(5.5,26.5,5.5,26.5,21.7,11.5);
// A visible masonry firebox and flue borrow the smeltery's working silhouette.
// The small amber bed is a smouldering smoker fire, not a forge full of flames.
box(2.2,6.6,0,6.3,6.5,1.4,P.mortar);box(2.7,7,1.3,5.8,5.7,8,P.stone);
box(2.3,7.7,2.3,.35,4.3,4.9,'#292a24');
box(2.05,8.15,2.6,.3,3.4,1.5,'#9a512b');box(1.9,8.65,2.7,.25,1.2,.7,'#d59845');
for(const y of [8.15,9.35,10.55,11.55])box(1.75,y,2.2,.3,.32,4.5,'#41443c');
box(1.6,7.6,6.6,.6,4.8,.7,P.iron);
// The flue rises directly out of the firebox into the smoking chamber.
box(6.1,7.5,8,4.5,4.5,26,P.stone);box(6.4,7.8,33.8,3.9,3.9,8,'#6c6858');
box(5.8,7.2,41,5.1,5.1,1.2,P.mortar);box(6.5,7.9,42.25,3.7,3.7,.15,P.window);
// Sparse opaque smoke clusters; all stay above the chimney inside the tile.
for(const[x,y,z,w,d,h,c]of [[7.1,8.6,44,2.5,2,1.2,'#777b6d'],[7.8,8.6,47,3.3,2.5,1.3,'#8d9080'],[9,8.8,50,3.8,2.8,1.2,'#a0a18e']])box(x,y,z,w,d,h,c);
// Sooty smoke-room vents above the hanging rack, not cottage windows.
for(const x of [12.2,15.1,18])box(x,6.3,18.1,1.7,.4,1.7,P.window);
// Covered W2 rack: the posts, hooks and meats belong to this side in every view.
for(const x of [10,23])box(x,2.2,0,.9,.9,14,P.timber);
box(9.7,2.1,13,14.1,1.1,1,P.timber);
beam([10.3,2.5,10.3],[12.5,2.5,13.4],.65,P.woodlight);beam([21,2.5,13.4],[23.3,2.5,10.3],.65,P.woodlight);
poly([[9,1.1,15],[24.8,1.1,15],[24.8,7,18],[9,7,18]],P.thatch);beam([9,1.1,15],[24.8,1.1,15],.7,P.darkstraw);
function ham(x,y,z){
 box(x,y,z, .35,.35,2,'#54594f'); // iron hook
 const body=[[x-.3,y,z-.2],[x+1.3,y,z-.7],[x+1.6,y,z-2.7],[x+.7,y,z-4.2],[x-.8,y,z-4.1],[x-1.3,y,z-2],[x-1,y,z-.8]];
 poly(body,'#a15d40');poly(body.map(([a,b,c])=>[a,b+.8,c]),'#b67b51');
 for(let i=0;i<body.length;i++)poly([body[i],body[(i+1)%body.length],body[(i+1)%body.length].map((v,k)=>k===1?v+.8:v),body[i].map((v,k)=>k===1?v+.8:v)],'#70432e');
 box(x-.1,y-.15,z-4.8,.65,1.1,.9,'#d4c49a');
}
ham(13,2,12.4);ham(17,2,12.1);ham(21,2,12.6);
// Split firewood on the far wall, kept inside the footprint.
for(let row=0;row<2;row++)for(let i=0;i<4-row;i++){box(10+i*1.2+row*.4,25.5,.3+row,1,1.5,.9,P.edge);box(10+i*1.2+row*.4,26.8,.3+row,1,.3,.9,P.woodlight);}
const limit=32/1.1;for(const f of faces)for(const[x,y]of f.v)if(x<0||y<0||x>limit||y>limit)throw Error('Outside 2x2 footprint '+[x,y]);console.log('PASS: all smokehouse geometry fits the 2x2 footprint');
function rgb(h){return h.match(/[a-f\d]{2}/gi).map(x=>parseInt(x,16));}
function surface(f,q,normal,shade){let[x,y,z]=q,c=f.c;
 if(f.mat==='thatch'){
   // Staggered wooden shingles: broad courses with quiet moss-grey highlights.
   let course=Math.floor(z/1.8),tile=Math.floor(x/2.1+(course%2)*.5),seam=(z/1.8)%1;
   c=seam<.13?P.darkstraw:tile%5===0?P.straw:tile%5===2?'#666e5a':P.thatch;
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
let sheet=Buffer.alloc(256*352*4);for(let r=0;r<4;r++){let pixels=render(r);png(path.join(out,`smokehouse_${r+1}.png`),64,88,pixels);let count=0,minx=64,maxx=0,miny=88,maxy=0;for(let y=0;y<88;y++)for(let x=0;x<64;x++){let i=(y*64+x)*4;if(pixels[i+3]){count++;minx=Math.min(x,minx);maxx=Math.max(x,maxx);miny=Math.min(y,miny);maxy=Math.max(y,maxy);}for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){let sx=(r%2)*128+x*2+dx,sy=Math.floor(r/2)*176+y*2+dy;pixels.copy(sheet,(sy*256+sx)*4,i,i+4);}}if(minx<2||maxx>61||maxy>85||count<500)throw Error(`Bad fit view ${r+1}`);console.log(`view ${r+1}: 64x88, alpha 0/255, bounds ${minx},${miny}..${maxx},${maxy}`);}png(path.join(out,'smokehouse_four_views.png'),256,352,sheet);
let zoom=Buffer.alloc(512*704*4);for(let y=0;y<704;y++)for(let x=0;x<512;x++){let i=(Math.floor(y/2)*256+Math.floor(x/2))*4;sheet.copy(zoom,(y*512+x)*4,i,i+4);}png(path.join(out,'smokehouse_detail.png'),512,704,zoom);
