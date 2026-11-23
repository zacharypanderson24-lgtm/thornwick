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



const root=path.resolve(__dirname,'remaining-biomes');fs.mkdirSync(root,{recursive:true});
const greens={sage:['424b35','566047','6c7757','84906b','9da582','b4b99b','c8cbb0'],elm:['35432b','455735','586d40','70844c','8a9c5a','a3af70','bac186'],acacia:['37442b','495831','5e703b','77884a','939f5c','abb471','c2c78a'],palm:['273e2c','355335','456b3f','5b844a','739b57','91ae6a','adbf82'],lush:['20382b','2b4c32','39643a','4c7c44','65974f','82af63','a2c57c'],laurel:['263d2e','345039','456747','5a8054','749a65','91ae7b','adc492'],gold:['574c2c','706037','8b7544','a58c52','bda263','cfb77b','dfca99']};
const configs=[
 {id:'Steppe',slug:'steppe',density:.045,grounds:[['feather_grass','8b8c50'],['dry_loess','a49369'],['worn_grass','a4a06b']],trees:[['siberian_elm','elm',25,29,'elm'],['river_poplar','poplar',13,36,'sage']],plants:['feather_grass','wormwood'],note:'Mostly treeless grassland; elm is isolated and poplar is restricted to river corridors.'},
 {id:'SemiAridScrub',slug:'semi-arid-scrub',density:.13,grounds:[['dry_scrub','9b915f'],['stony_earth','a58f6c'],['dust','b5a17b']],trees:[['tamarisk','tamarisk',25,27,'sage'],['juniper','juniper',22,28,'laurel']],plants:['thorn_scrub','dry_tussock'],note:'Sparse drought-adapted trees and low thorn scrub; no cacti.'},
 {id:'Tundra',slug:'tundra',density:0,grounds:[['lichen','8b9278'],['peat','746f5b'],['frost','b6beb2']],trees:[],plants:['dwarf_birch','dwarf_willow','lichen_cushion'],note:'Treeless tundra; dwarf birch and willow are low shrubs, not full-size trees.'},
 {id:'Desert',slug:'desert',density:.055,grounds:[['sand','c2a36e'],['gravel','ab936f'],['dune_sand','d4b882']],trees:[['date_palm','date',27,37,'palm'],['desert_acacia','acacia',26,27,'acacia']],plants:['desert_grass','saltbush'],note:'Date palms only at oasis margins; scattered acacia in dry washes. No New World cacti.'},
 {id:'Savanna',slug:'savanna',density:.18,grounds:[['golden_grass','b09d54'],['red_earth','a77a50'],['dry_grass','c1ae6c']],trees:[['flat_top_acacia','acacia',28,31,'acacia'],['baobab','baobab',27,35,'elm'],['fan_palm','fan',26,35,'palm']],plants:['tall_grass','thorn_scrub'],note:'Open grassland with acacia, thick-trunked baobab and locally occurring fan palm.'},
 {id:'SubtropicalForest',slug:'subtropical-forest',density:.63,grounds:[['humid_grass','54734b'],['leaf_litter','796d49'],['damp_loam','5d6244']],trees:[['camphor','broad',27,36,'laurel'],['magnolia','magnolia',24,34,'lush'],['fan_palm','fan',26,36,'palm']],plants:['fern','bamboo'],note:'Humid evergreen woodland, with magnolia flowers, fan palms, ferns and bamboo thickets.'},
 {id:'TropicalRainforest',slug:'tropical-rainforest',density:.77,grounds:[['rainforest_moss','3b603e'],['forest_litter','5c5938'],['wet_soil','49523c']],trees:[['kapok','kapok',28,40,'lush'],['strangler_fig','fig',28,37,'laurel'],['rainforest_palm','coconut',27,38,'palm']],plants:['fern','banana'],note:'Dense buttressed canopy trees, fig aerial roots, palms and broad-leaf understory.'},
 {id:'TropicalSeasonalForest',slug:'tropical-seasonal-forest',density:.51,grounds:[['seasonal_grass','82934f'],['dry_leaves','a08852'],['red_loam','946b47']],trees:[['teak','teak',26,37,'elm'],['sal','broad',25,35,'laurel'],['palmyra','fan',25,38,'palm']],plants:['bamboo','dry_tussock'],note:'Seasonally dry monsoon woodland; leaf litter and teak/sal crowns contrast with rainforest.'},
 {id:'TropicalHighlands',slug:'tropical-highlands',density:.32,grounds:[['montane_grass','6a8353'],['volcanic_soil','646356'],['cloud_moss','7c8c69']],trees:[['tree_fern','treefern',27,32,'lush'],['cloud_forest_laurel','cloud',25,34,'laurel'],['mountain_podocarp','podocarp',20,37,'elm']],plants:['fern','montane_tussock'],note:'Cool tropical uplands with tree ferns, mossy cloud-forest trees and podocarps.'}
];
function line(p,x0,y0,x1,y1,color){let dx=x1-x0,dy=y1-y0,n=Math.max(Math.abs(dx),Math.abs(dy));for(let j=0;j<=n;j++)put(p,32,48,Math.round(x0+dx*j/Math.max(1,n)),Math.round(y0+dy*j/Math.max(1,n)),color);}
function drawTree(p,cx,foot,w,h,t,seed){const form=t[1],pal=greens[t[4]].map(rgb),top=foot-h;
 const stem=(height,width=2)=>trunk(p,cx,foot,Math.round(height),width,false,seed,true);
 if(['date','fan','coconut','treefern'].includes(form)){
  const crown=top+Math.round(h*.25),lean=Math.min(30-w*.49-cx,form==='coconut'?4:form==='date'?2:0);
  for(let y=crown;y<=foot;y++){let x=cx+Math.round(lean*(foot-y)/(foot-crown));put(p,32,48,x,y,rgb(form==='treefern'?'6d573e':'8e7856'));put(p,32,48,x+1,y,rgb(y%3===0?'5f503a':'b09a70'));}
  const x0=cx+lean;
  if(form==='fan'){
   for(let n=0;n<9;n++){let angle=-Math.PI+n*Math.PI/8,ex=x0+Math.cos(angle)*w*.46,ey=crown+Math.sin(angle)*h*.24+2;
    for(let k=1;k<=8;k++){let q=k/8,x=x0+(ex-x0)*q,y=crown+(ey-crown)*q,thick=Math.sin(q*Math.PI)*2.2;for(let d=-thick;d<=thick;d++)put(p,32,48,Math.round(x+d),Math.round(y),shade(pal,2+n%3+(d<0?1:0)));}
    line(p,x0,crown,ex,ey,shade(pal,4+n%2));}
  }else{
   for(let n=0;n<9;n++){let angle=n*Math.PI*2/9,ex=x0+Math.cos(angle)*w*.47,ey=crown+Math.sin(angle)*h*.19+3;
    for(let k=1;k<=9;k++){let q=k/9,x=x0+(ex-x0)*q,y=crown+(ey-crown)*q-3*Math.sin(q*Math.PI);put(p,32,48,Math.round(x),Math.round(y),shade(pal,4));
     let leaf=form==='treefern'?2:3;line(p,x,y,x+Math.cos(angle+1.1)*leaf,y+2,shade(pal,2+n%3));line(p,x,y,x+Math.cos(angle-1.1)*leaf,y+1,shade(pal,3+n%3));}}
  }
  if(form==='date')for(let n=0;n<4;n++)put(p,32,48,x0-2+n,crown+4+n%2,rgb('a77c3d'));
 }else if(form==='baobab'){
  for(let y=top+10;y<=foot;y++){let q=(foot-y)/(h-10),half=w*(.16-.065*Math.sin(q*Math.PI));for(let x=Math.floor(cx-half);x<=cx+half;x++)put(p,32,48,x,y,rgb((x+y*2)%7===0?'7e7764':x<cx?'a29880':x===cx?'8d826c':'716854'));}
  for(let n=-2;n<=2;n++)line(p,cx+n,foot-5,cx+n*3,foot-Math.abs(n)%2,rgb('8b816c'));
  for(let n=0;n<5;n++){let x=cx+(n-2)*w*.15,y=top+7+(n%2)*3;line(p,cx,top+16,x,y,rgb('8c8069'));lobe(p,x,y,w*.18,h*.14,pal,seed+n*17,.3);}
 }else if(form==='acacia'){
  stem(h*.76,2);for(let n=0;n<5;n++){let x=cx+(n-2)*w*.16,y=top+h*(.20+Math.abs(n-2)*.025);line(p,cx,foot-h*.55,x,y+3,rgb('7b6949'));lobe(p,x,y,w*.22,h*.11,pal,seed+n*19,n*.1);}
 }else if(form==='tamarisk'){
  stem(h*.63);for(let n=0;n<8;n++){let x=cx+Math.sin(n*2.3)*w*.3,y=top+6+n%3*3;line(p,cx,foot-6,x,y+4,rgb('8c7358'));lobe(p,x,y,w*.17,h*.17,pal,seed+n*13,.4);}
 }else if(form==='juniper'||form==='podocarp'){
  stem(h*.76);for(let n=5;n>=0;n--){let f=n/5;lobe(p,cx+Math.sin(n*2)*w*.08,top+4+f*h*.63,w*(.13+.27*f),h*.14,pal,seed+n*13,n*.07);}
 }else if(form==='poplar')broadleaf(p,cx,foot,w,h,'column',pal,seed,true);
 else{
  stem(h*.7,form==='kapok'?4:form==='fig'?3:2);
  if(form==='kapok'||form==='fig')for(let n=-2;n<=2;n++)line(p,cx,foot-10,cx+n*3,foot-Math.abs(n),rgb(n<0?'8b8064':'5e6048'));
  broadleaf(p,cx,foot,w,h,form==='teak'?'column':'round',pal,seed,false);
  if(form==='fig')for(let n=-2;n<=2;n++)line(p,cx+n*4,top+14,cx+n*4,foot-2-Math.abs(n)*2,rgb('777a54'));
  if(form==='magnolia')for(let n=0;n<5;n++){let x=cx+Math.round(Math.sin(n*5)*w*.28),y=top+Math.round(h*(.22+n*.045));put(p,32,48,x,y,rgb('e2d9b2'));put(p,32,48,x+1,y,rgb('bcb58c'));}
  if(form==='cloud')for(let n=0;n<8;n++){let x=cx+(n%5)-2,y=foot-5-n*2;put(p,32,48,x,y,rgb('9caa76'));}
 }
}
function plant(p,type,v){const pal=(type.includes('dry')||type.includes('grass')||type.includes('tussock')?greens.gold:greens.sage).map(rgb),seed=1000+v*71;
 if(type==='bamboo'){
  for(let n=0;n<6;n++){let x=7+n*3,y=23+(n*5+v)%10;for(let k=y;k<44;k++){put(p,32,48,x,k,rgb(k%5===0?'9b9e64':'60774c'));if(k%5===0){line(p,x,k,x-4,k-2,rgb('7a9259'));line(p,x,k,x+4,k-3,rgb('899d65'));}}}
 }else if(type==='fern'||type==='banana'){
  for(let n=0;n<7;n++){let x=16+Math.cos(n*Math.PI/6)*11,y=41-Math.sin(n*Math.PI/6)*12;line(p,16,44,x,y,rgb('809857'));
   for(let k=2;k<9;k++){let q=k/9,xx=16+(x-16)*q,yy=44+(y-44)*q;line(p,xx,yy,xx-2,yy-2,shade(greens.lush.map(rgb),3+n%3));line(p,xx,yy,xx+2,yy-2,shade(greens.lush.map(rgb),4));if(type==='banana')for(let d=-2;d<=2;d++)put(p,32,48,Math.round(xx+d),Math.round(yy),shade(greens.lush.map(rgb),d<0?4:3));}}
 }else if(type.includes('grass')||type.includes('tussock')){
  for(let n=0;n<13;n++){let x=6+(n*7+v)%21,tip=type==='tall_grass'?27:34;tip+=(n*3+v)%7;line(p,16+(n%5)-2,44,x,tip,shade(pal,2+n%4));if(type==='feather_grass'){put(p,32,48,x,tip-1,rgb('d6c58e'));put(p,32,48,x+1,tip,rgb('c3b17b'));}}
 }else{
  for(let n=0;n<4;n++)lobe(p,9+n*4,41+n%2,4,2.5,pal,seed+n*13,.2);
  if(type==='thorn_scrub')for(let n=0;n<7;n++)line(p,8+n*3,43,6+n*3,36+n%3,rgb('7d7350'));
  if(type==='lichen_cushion')for(let y=38;y<45;y++)for(let x=4;x<28;x++)if(p[(y*32+x)*4+3]&&hash(x,y,seed)>.5)put(p,32,48,x,y,rgb('b0b195'));
 }
}
function blit(dst,w,h,p,pw,ph,ox,oy){for(let y=0;y<ph;y++)for(let x=0;x<pw;x++){let a=(y*pw+x)*4,xx=x+ox,yy=y+oy;if(p[a+3]&&xx>=0&&yy>=0&&xx<w&&yy<h)p.copy(dst,(yy*w+xx)*4,a,a+4);}}
function scale(p,w,h,s,file){let b=Buffer.alloc(w*h*s*s*4);for(let y=0;y<h*s;y++)for(let x=0;x<w*s;x++){let i=(Math.floor(y/s)*w+Math.floor(x/s))*4;p.copy(b,(y*w*s+x)*4,i,i+4);}save(file,w*s,h*s,b);}
const layouts={1:[[16,44,1]],2:[[11,40,.68],[22,44,.67]],3:[[16,35,.58],[9,43,.6],[23,45,.6]],4:[[12,35,.5],[23,39,.49],[8,44,.52],[20,46,.55]]};
let all=[],gallery=Buffer.alloc(960*624*4);
for(const [ci,cfg] of configs.entries()){
 const out=path.join(root,cfg.slug),assets=path.join(out,'assets'),cells=path.join(out,'ground-cells');fs.mkdirSync(assets,{recursive:true});fs.mkdirSync(cells,{recursive:true});let files=[],grounds=[],trees=[],plants=[];
 for(let kind=0;kind<3;kind++)for(let v=0;v<(kind===0?4:2);v++){
  let p=Buffer.alloc(32*16*4),base=rgb(cfg.grounds[kind][1]),seed=ci*173+kind*43+v*29;
  for(let y=0;y<16;y++)for(let x=0;x<32;x++){let edge=Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8),patch=hash(Math.floor(x/5),Math.floor(y/3),seed),r=hash(x,y,seed),d=edge>.8?0:Math.round((patch-.5)*10+(r>.94?8:r<.06?-7:0));let c=base.map(v=>Math.max(0,Math.min(255,v+d)));
   if(cfg.id==='Desert'&&kind!==1&&edge<.75&&((y+Math.floor(x/7)+v)%5===0))c=base.map(v=>v-9);
   if(cfg.id==='Tundra'&&kind===2&&edge<.75&&r>.75)c=rgb('d0d6cc');
   put(p,32,16,x,y,c);}
  let name=cfg.slug.replaceAll('-','_')+'_'+cfg.grounds[kind][0]+'_'+v;
  save(path.join(cells,name+'.png'),32,16,p);files.push('ground-cells/'+name+'.png');
  let diamond=Buffer.from(p);for(let y=0;y<16;y++)for(let x=0;x<32;x++)if(Math.abs((x+.5-16)/16)+Math.abs((y+.5-8)/8)>1)diamond[(y*32+x)*4+3]=0;
  save(path.join(assets,name+'.png'),32,16,diamond);files.push('assets/'+name+'.png');grounds.push({name,p:diamond});
 }
 for(const [ti,t] of cfg.trees.entries())for(let v=0;v<6;v++){
  let count=v<2?1:v<5?v:1,sapling=v===5,p=Buffer.alloc(32*48*4);
  for(const[cx,foot,f]of layouts[count]){let sc=f*(sapling?.5:1),w=Math.round(t[2]*sc),h=Math.round(t[3]*sc),center=Math.max(Math.ceil(w/2)+1,Math.min(30-Math.ceil(w/2),cx));drawTree(p,center,foot,w,h,t,ci*131+ti*37+v*17+cx);}
  let name=cfg.slug.replaceAll('-','_')+'_'+t[0]+(sapling?'_sapling':count>1?'_grove_'+count:'_'+v);save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');trees.push({name,p});
 }
 for(const type of cfg.plants)for(let v=0;v<3;v++){let p=Buffer.alloc(32*48*4);plant(p,type,v);let name=cfg.slug.replaceAll('-','_')+'_plant_'+type+'_'+v;save(path.join(assets,name+'.png'),32,48,p);files.push('assets/'+name+'.png');plants.push({name,p});}
 let scene=Buffer.alloc(320*208*4),objects=[];
 for(let j=0;j<8;j++)for(let i=0;i<10;i++){
  let x=128+(i-j)*16,y=48+(i+j)*8,h=hash(i,j,227+ci*3),g=grounds[h<.17?6+(i+j)%2:h<.35?4+(i+j)%2:(i+j*3)%4];blit(scene,320,208,g.p,32,16,x,y);
  let allowed=true;if(cfg.id==='Desert')allowed=(i<3&&j<3)||(i===7&&j===5);if(cfg.id==='Steppe')allowed=i===1||j===6;
  if(cfg.trees.length&&((h>1-cfg.density&&allowed)||(cfg.id==='Desert'&&i===1&&j===1)||(cfg.id==='Steppe'&&i===1&&j===3))){let type=(i*7+j*3)%cfg.trees.length;if(cfg.id==='Desert')type=i<3?0:1;if(cfg.id==='Steppe'&&j!==6)type=0;let grove=cfg.density>.45&&h>.91?2:0;objects.push({x,y:y-32,p:trees[type*6+grove].p});}
  else if(h>.24&&h<(cfg.id==='Tundra'?.72:.51))objects.push({x,y:y-32,p:plants[(i+j*3)%plants.length].p});
 }
 for(const o of objects.sort((a,b)=>a.y-b.y))blit(scene,320,208,o.p,32,48,o.x,o.y);
 scale(scene,320,208,3,path.join(out,'landscape.png'));blit(gallery,960,624,scene,320,208,ci%3*320,Math.floor(ci/3)*208);
 let sheet=Buffer.alloc(256*136*4);for(let i=0;i<8;i++)blit(sheet,256,136,grounds[i].p,32,16,i%4*64+16,Math.floor(i/4)*24+4);
 let examples=[...cfg.trees.map((t,i)=>trees[i*6]),...plants.filter((t,i)=>i%3===0)].slice(0,4);for(const[i,t]of examples.entries())blit(sheet,256,136,t.p,32,48,i*64+16,64);scale(sheet,256,136,3,path.join(out,'assets_preview.png'));
 const manifest={biome:cfg.id,ground:grounds.map(t=>t.name),trees:cfg.trees.map(t=>({name:t[0],shape:t[1]})),plants:cfg.plants,placement:cfg.note,files};fs.writeFileSync(path.join(out,'biome.json'),JSON.stringify(manifest,null,2));all.push({biome:cfg.id,folder:cfg.slug,textures:files.length});
 console.log(cfg.id+': '+files.length+' native PNGs');
}
scale(gallery,960,624,2,path.join(root,'nine_biomes_overview.png'));
fs.writeFileSync(path.join(root,'README.md'),'# Remaining biome texture packs\n\nCoded native pixel sprites: ground 32x16, vegetation 32x48, binary alpha. Ground-cells are opaque atlas inputs; assets contains isometric diamond ground and vegetation. Landscape previews are assembled sprite scenes, not game screenshots.\n\n'+configs.map(c=>'## '+c.id+'\n\n'+c.note+'\n').join('\n')+'\nBiome placement is not connected in-game yet. No simulation or gameplay files changed.\n');
fs.writeFileSync(path.join(root,'index.json'),JSON.stringify(all,null,2));
