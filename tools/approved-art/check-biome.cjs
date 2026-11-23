const fs=require('fs'),path=require('path'),zlib=require('zlib'),assert=require('assert');
let count=0;
for(const dir of ['assets','ground-cells'])for(const name of fs.readdirSync(path.join(process.argv[2],dir))){
 const b=fs.readFileSync(path.join(process.argv[2],dir,name)),w=b.readUInt32BE(16),h=b.readUInt32BE(20);assert.equal(w,32);assert([16,48].includes(h));assert.equal(b[25],6);
 let data=[];for(let o=8;o<b.length;){let n=b.readUInt32BE(o),t=b.toString('ascii',o+4,o+8);if(t==='IDAT')data.push(b.subarray(o+8,o+8+n));o+=n+12;}
 const p=zlib.inflateSync(Buffer.concat(data));assert.equal(p.length,(w*4+1)*h);for(let y=0;y<h;y++){assert.equal(p[y*(w*4+1)],0);for(let x=0;x<w;x++){let a=p[y*(w*4+1)+1+x*4+3];assert(a===0||a===255,'partial alpha '+name);}}
 count++;
}
console.log('PASS: '+count+' PNGs; native dimensions, RGBA encoding and binary transparency.');
