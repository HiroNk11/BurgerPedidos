const {test}=require('node:test');
const assert=require('node:assert/strict');
const {webcrypto}=require('node:crypto');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const ts=require('typescript');
const source=fs.readFileSync(path.join(__dirname,'../src/services/checkoutIdentity.ts'),'utf8');
const code=ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
function load(getRandomValues){
 const exports={};
 vm.runInNewContext(code,{exports,Uint8Array,require:()=>({getRandomValues,randomUUID:()=>{throw Error('randomUUID unavailable');}})});
 return exports;
}
test('checkout funciona sin randomUUID y respeta version y variante UUID v4',()=>{
 const api=load(bytes=>bytes.fill(0));
 assert.equal(api.newRequestId(),'00000000-0000-4000-8000-000000000000');
});
test('token conserva 256 bits y usa la fuente criptografica',()=>{
 const lengths=[];
 const api=load(bytes=>{lengths.push(bytes.length);return webcrypto.getRandomValues(bytes);});
 const ids=new Set();
 for(let i=0;i<100;i++){
  const id=api.newRequestId();assert.match(id,/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);ids.add(id);
  assert.match(api.newToken(),/^[0-9a-f]{64}$/);
 }
 assert.equal(ids.size,100);assert.equal(lengths.filter(n=>n===32).length,100);
});
test('un fallo criptografico se propaga sin generar un token inseguro',()=>{
 const api=load(()=>{throw Error('Crypto unavailable');});
 assert.throws(()=>api.newToken(),/Crypto unavailable/);
 assert.throws(()=>api.newRequestId(),/Crypto unavailable/);
});
