const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const path=require('node:path');
const ts=require('typescript');
const source=fs.readFileSync(path.join(__dirname,'../src/state/favorites.ts'),'utf8');
const exportsObject={};
vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{exports:exportsObject});
const {favoritesReducer:reduce,emptyFavorites:empty}=exportsObject;
const p={id:'burger',nombre:'Burger',precio:100,stock:10,activo:true};
const q={...p,id:'papas',nombre:'Papas'};
const initial=()=>reduce(empty,{type:'reset',scope:'user-a'});
test('favorito optimista aparece antes del HTTP y un error restaura la selección',()=>{
 let state=reduce(initial(),{type:'toggle',scope:'user-a',product:p});
 assert.equal(state.favorites[0].id,p.id);assert.equal(state.pending[0],p.id);
 state=reduce(state,{type:'settle',scope:'user-a',product:p,favorite:false,error:'Error de red'});
 assert.equal(state.favorites.length,0);assert.equal(state.pending.length,0);assert.equal(state.error,'Error de red');
});
test('fallo de un toggle no deshace otro producto confirmado',()=>{
 let state=reduce(initial(),{type:'toggle',scope:'user-a',product:p});
 state=reduce(state,{type:'toggle',scope:'user-a',product:q});
 state=reduce(state,{type:'settle',scope:'user-a',product:q,favorite:true});
 state=reduce(state,{type:'settle',scope:'user-a',product:p,favorite:false,error:'Error'});
 assert.equal(state.favorites.length,1);assert.equal(state.favorites[0].id,q.id);
});
test('respuesta tardía de otra cuenta no revela ni modifica favoritos',()=>{
 let state=reduce(initial(),{type:'toggle',scope:'user-a',product:p});
 state=reduce(state,{type:'reset',scope:'user-b'});
 const late=reduce(state,{type:'settle',scope:'user-a',product:p,favorite:true});
 assert.equal(late,state);assert.equal(late.favorites.length,0);
});
test('refresh y doble toque no pisan una actualización optimista pendiente',()=>{
 const state=reduce(initial(),{type:'toggle',scope:'user-a',product:p});
 assert.equal(reduce(state,{type:'toggle',scope:'user-a',product:p}),state);
 assert.equal(reduce(state,{type:'hydrate',scope:'user-a',favorites:[],ranking:[]}),state);
});
