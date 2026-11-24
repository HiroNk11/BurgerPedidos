const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const ts=require('typescript');
function load(relative, mocks={}, globals={}) {
 const source=fs.readFileSync(path.join(__dirname,'../src',relative),'utf8');
 const code=ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
 const exports={};
 vm.runInNewContext(code,{exports,require:id=>{if(!(id in mocks))throw Error('Unexpected dependency '+id);return mocks[id];},Date,Set,Map,Promise,JSON,Error,Headers,AbortController,setTimeout,clearTimeout,...globals});
 return exports;
}
const product={id:'burger',nombre:'Burger',precio:100,stock:3,categoria:'Burgers',descripcion:'',imagenKey:null};
test('clearCart vacía productos después del POST sin liberar una solicitud sin resolver',()=>{
 const {cartReducer,initialCart}=load('state/cartReducer.ts');
 let state=cartReducer(initialCart,{type:'lock',value:false});
 state=cartReducer(state,{type:'quantity',product,quantity:2});
 state=cartReducer(state,{type:'lock',value:true});
 assert.equal(cartReducer(state,{type:'quantity',product,quantity:1}).items[0].cantidad,2);
 state=cartReducer(state,{type:'clear'});
 assert.equal(state.items.length,0);assert.equal(state.locked,true);
 state=cartReducer(state,{type:'lock',value:false});assert.equal(state.locked,false);
});
test('reemplazo de carrito rechaza cambios concurrentes y respeta stock',()=>{
 const {cartReducer,initialCart}=load('state/cartReducer.ts');
 let state=cartReducer(initialCart,{type:'lock',value:false});
 state=cartReducer(state,{type:'quantity',product,quantity:20});assert.equal(state.items[0].cantidad,3);
 const revision=state.revision;
 state=cartReducer(state,{type:'quantity',product,quantity:1});
 const unchanged=cartReducer(state,{type:'replace',items:[{...product,cantidad:2}],expectedRevision:revision});
 assert.equal(unchanged,state);
 const replaced=cartReducer(state,{type:'replace',items:[{...product,cantidad:2}],expectedRevision:state.revision});assert.equal(replaced.items[0].cantidad,2);
});
test('reorder conserva avisos y detecta cambios entre propuesta y catálogo',()=>{
 const {prepareReorder}=load('utils/reorderCart.ts');
 const proposal={items:[{productId:'burger',nombre:'Burger',cantidad:3,precioUnitarioActual:90},{productId:'gone',nombre:'Agotada',cantidad:1,precioUnitarioActual:10}],warnings:[{message:'El descuento anterior no se copia.'}],itemsRemovidos:[{nombreHistorico:'Vieja',motivo:'Fuera de carta'}]};
 const result=prepareReorder(proposal,[{...product,stock:2}]);
 assert.equal(result.items.length,1);assert.equal(result.items[0].cantidad,2);assert.equal(result.items[0].precio,100);
 assert.equal(result.messages.length,5);
 assert.equal(prepareReorder(proposal,[]).items.length,0);
});
test('tracker elimina terminales y deduplica pedidos locales y de la cuenta',()=>{
 const {mergeActiveOrders}=load('utils/orderState.ts');
 const local=[{id:'a',fecha:'2026-11-04',estadoPedido:'PendienteConfirmacion'},{id:'b',fecha:'2026-11-03',estadoPedido:'EnPreparacion'}];
 const merged=mergeActiveOrders([{...local[0],estadoPedido:'Entregado'},{...local[1],estadoPedido:'EnCamino'}],local);
 assert.equal(merged.length,1);assert.equal(merged[0].id,'b');assert.equal(merged[0].estadoPedido,'EnCamino');
});
function storage(platform){
 const values=new Map();let calls=0;
 const secure={getItemAsync:async key=>{calls++;return values.get(key)??null;},setItemAsync:async(key,value)=>{calls++;values.set(key,value);},deleteItemAsync:async key=>{calls++;values.delete(key);}};
 const sessionStorage={getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,value),removeItem:key=>values.delete(key)};
 const api=load('services/sessionStorage.ts',{'expo-secure-store':secure,'react-native':{Platform:{OS:platform}},'./apiConfig':{tenantId:'test'}},{window:{sessionStorage}});
 return {api,values,calls:()=>calls};
}
test('JWT nativo usa SecureStore, se restaura y se elimina al salir',async()=>{
 const {api,values,calls}=storage('android');const session={accessToken:'test-token',expiresAtUtc:new Date(Date.now()+60000).toISOString()};
 await api.saveSession(session);assert.ok(calls()>0);assert.equal((await api.loadSession()).accessToken,'test-token');
 await api.saveSession(null);assert.equal(await api.loadSession(),null);assert.equal(values.size,0);
});
test('sesión vencida no se recupera y se quita del almacenamiento web',async()=>{
 const {api,values,calls}=storage('web');
 await api.saveSession({accessToken:'expired',expiresAtUtc:new Date(Date.now()-1000).toISOString()});
 assert.equal(await api.loadSession(),null);assert.equal(values.size,0);assert.equal(calls(),0);
});
test('HTTP nunca adjunta cookies staff y solo usa JWT explícito',async()=>{
 const requests=[];
 const {http}=load('services/http.ts',{'./apiConfig':{commerceUrl:'http://api.local'}},{fetch:async(url,init)=>{requests.push({url,init});return {ok:true,status:204};}});
 assert.equal(await http('/api/cuenta',{token:'test-token',method:'DELETE'}),undefined);
 await http('/api/tienda');
 assert.equal(requests[0].init.credentials,'omit');assert.equal(requests[0].init.headers.get('Authorization'),'Bearer test-token');
 assert.equal(requests[1].init.headers.get('Authorization'),null);
});
test('HTTP conserva 401 como error y no reintenta compras automáticamente',async()=>{
 let count=0;
 const {http}=load('services/http.ts',{'./apiConfig':{commerceUrl:'http://api.local'}},{fetch:async()=>{count++;return {ok:false,status:401,json:async()=>({title:'Código inválido o vencido.'})};}});
 await assert.rejects(http('/api/auth/verify-otp',{method:'POST'}),error=>error.status===401&&error.message==='Código inválido o vencido.');assert.equal(count,1);
});

test('cambio de cuenta o página elimina el feed anterior antes de la respuesta HTTP',()=>{
 const {beginOrdersRefresh}=load('state/orderFeed.ts');
 const old={scope:'cuenta-a',page:1,result:{activos:[{id:'privado-a'}]},refreshing:false,error:'',at:123};
 assert.equal(beginOrdersRefresh(old,'cuenta-b',1).result,null);
 assert.equal(beginOrdersRefresh(old,'guest',1).result,null);
 assert.equal(beginOrdersRefresh(old,'cuenta-a',2).result,null);
 assert.equal(beginOrdersRefresh(old,'cuenta-a',1).result,old.result);
});
