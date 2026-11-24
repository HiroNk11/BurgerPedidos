import {mkdir,copyFile,cp,rm,readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
const destination=resolve('dist');if(destination!==resolve(process.cwd(),'dist'))throw Error('Invalid build directory');
await rm(destination,{recursive:true,force:true});await mkdir(destination,{recursive:true});
for(const name of ['index.html','app.js','domain.js','style.css'])await copyFile(name,'dist/'+name);
await cp('assets','dist/assets',{recursive:true});
const html=await readFile('index.html','utf8');if(!html.includes('lang="es"'))throw Error('Missing language');
console.log('Demo estática lista en dist; sin API, secretos ni servicios de pago.');
