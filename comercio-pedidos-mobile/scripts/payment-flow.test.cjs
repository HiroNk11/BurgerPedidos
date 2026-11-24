const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');const path=require('node:path');const vm=require('node:vm');const ts=require('typescript');
function load(file,mocks={},globals={}) {
 const exports={};const source=fs.readFileSync(path.join(__dirname,'../src',file),'utf8');
 vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{exports,require:name=>{if(!(name in mocks))throw Error(name);return mocks[name];},URL,Error,Date,Set,Promise,AbortController,setTimeout,clearTimeout,...globals});return exports;
}
const links=load('utils/paymentLinks.ts');
class HttpError extends Error {constructor(message,status){super(message);this.status=status;}}
const id='11111111-2222-4333-8444-555555555555';
const order={id,metodoPago:'PasarelaOnline',estadoPago:'Pendiente',estadoPedido:'Aceptado',total:100};
const response={proveedor:'MercadoPago',transaccionId:null,preferenceId:'pref-1',checkoutUrl:'https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=pref-1'};
const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return{promise,resolve};};
function setup(t,override={}) {
 const count={order:0,start:0,open:0,dismiss:0,refresh:0,save:0,clear:0,unauthorized:0};const timers=[];
 const {PaymentFlow}=load('state/paymentFlow.ts',{'../utils/paymentLinks':links,'../services/http':{HttpError}},{setTimeout:fn=>{timers.push(fn);return fn;},clearTimeout:()=>{}});
 const deps={order:async()=>{count.order++;return {...order};},start:async()=>{count.start++;return response;},open:async()=>{count.open++;return 'opened';},dismiss:async()=>{count.dismiss++;},refresh:async()=>{count.refresh++;},unauthorized:async()=>{count.unauthorized++;},storage:{save:async()=>{count.save++;},clear:async()=>{count.clear++;},read:async()=>null},...override};
 const flow=new PaymentFlow(deps);t.after(()=>flow.cancel());return{flow,count,timers,deps};
}
test('checkout solo acepta HTTPS y dominio real, sin credenciales ni hosts parecidos',()=>{
 for(const bad of ['javascript:alert(1)','http://www.mercadopago.com.ar/a','https://mercadopago.com.ar.evil.test','https://evil.test/mercadopago.com.ar','https://jwt@www.mercadopago.com.ar/a','https://www.mercadopago.com.ar:444/a'])assert.throws(()=>links.safeCheckoutUrl(bad));
 assert.match(links.safeCheckoutUrl(response.checkoutUrl),/^https:/);
 assert.match(links.safeCheckoutUrl('https://sandbox.mercadopago.com.ar/checkout/v1/redirect'),/sandbox/);
});
test('retorno soporta esquemas existentes y HTTPS solo del origen configurado',()=>{
 assert.equal(links.parsePaymentReturn('gastronomiaapp:///payment-return?external_reference='+id).orderId,id);
 assert.equal(links.parsePaymentReturn('burgerclub://payment-return?orderId='+id).orderId,id);
 assert.equal(links.parsePaymentReturn('https://shop.example.com/payment-return?result=success','https://shop.example.com').outcome,'success');
 assert.equal(links.parsePaymentReturn('https://evil.test/payment-return?result=success','https://shop.example.com'),null);
 assert.equal(links.parsePaymentReturn('gastronomiaapp://evil/payment-return'),null);
 assert.equal(links.parsePaymentReturn('gastronomiaapp:///payment-return?orderId=bad').orderId,null);
});
test('elegibilidad excluye métodos manuales, pagados, rechazados, entregados y cancelados',()=>{
 assert.equal(links.canPayOnline(order),true);
 for(const changed of [{metodoPago:'Efectivo'},{metodoPago:'TransferenciaCBU'},{estadoPago:'Pagado'},{estadoPago:'Rechazado'},{estadoPedido:'Cancelado'},{estadoPedido:'Entregado'},{total:0}])assert.equal(links.canPayOnline({...order,...changed}),false);
});
test('doble toque comparte bloqueo global y solo genera/abre una preferencia',async t=>{
 const pending=deferred();const {flow,count}=setup(t,{start:async()=>{count.start++;return pending.promise;}});
 const first=flow.pay(order);await Promise.resolve();await flow.pay(order);pending.resolve(response);await first;
 assert.equal(count.start,1);assert.equal(count.open,1);assert.equal(flow.getSnapshot().phase,'waiting');
});
test('se vuelve a consultar el pedido antes de solicitar la preferencia',async t=>{
 const {flow,count}=setup(t,{order:async()=>({...order,estadoPago:'Pagado'})});await flow.pay(order);
 assert.equal(count.start,0);assert.equal(count.open,0);assert.match(flow.getSnapshot().error,/cambió/);
});
test('respuesta con URL hostil no abre el navegador ni persiste URL',async t=>{
 const {flow,count}=setup(t,{start:async()=>({...response,checkoutUrl:'https://evil.test/pay'})});await flow.pay(order);
 assert.equal(count.open,0);assert.equal(count.save,0);assert.match(flow.getSnapshot().error,/seguro/);
});
test('401 conserva error visible y vence la sesión sin abrir checkout',async t=>{
 const {flow,count}=setup(t,{start:async()=>{throw new HttpError('Sesión vencida',401);}});await flow.pay(order);
 assert.equal(count.unauthorized,1);assert.equal(count.open,0);assert.equal(flow.getSnapshot().error,'Sesión vencida');
});
test('cancelación por cambio de cuenta descarta el POST tardío sin abrir navegador',async t=>{
 const pending=deferred();const {flow,count}=setup(t,{start:()=>pending.promise});const first=flow.pay(order);await Promise.resolve();flow.cancel();pending.resolve(response);await first;
 assert.equal(count.open,0);assert.equal(count.save,0);assert.equal(flow.getSnapshot().phase,'idle');
});
test('retorno cierra navegador, refresca inmediatamente y programa polling sin marcar Pagado',async t=>{
 const {flow,count,timers}=setup(t);await flow.pay(order);await flow.returned(id);
 assert.equal(count.dismiss,1);assert.equal(count.refresh,1);assert.equal(count.clear,1);assert.equal(timers.length,2);
 assert.equal(order.estadoPago,'Pendiente');assert.match(flow.getSnapshot().message,/no confirma/);assert.equal(flow.getSnapshot().phase,'idle');
});
test('retornos concurrentes comparten una sola actualización',async t=>{
 const pending=deferred();const {flow,count}=setup(t,{refresh:async()=>{count.refresh++;await pending.promise;}});await flow.pay(order);
 const first=flow.returned(id);const second=flow.returned(id);await Promise.resolve();pending.resolve();await Promise.all([first,second]);assert.equal(count.refresh,1);
});
test('retorno de otro pedido no cierra el checkout que está en curso',async t=>{
 const {flow,count}=setup(t);await flow.pay(order);await flow.returned('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee');assert.equal(count.dismiss,0);assert.equal(flow.getSnapshot().phase,'waiting');
});
test('cierre manual de Safari consulta backend y libera bloqueo',async t=>{
 const {flow,count}=setup(t,{open:async()=>{count.open++;return 'closed';}});await flow.pay(order);assert.equal(count.refresh,1);assert.equal(flow.getSnapshot().phase,'idle');
});
test('arranque en frío recupera correlación y consulta sin volver a abrir checkout',async t=>{
 const {flow,count,deps}=setup(t);deps.storage.read=async()=>id;await flow.restore();assert.equal(count.refresh,1);assert.equal(count.open,0);assert.equal(flow.getSnapshot().orderId,id);
});
test('correlación se aísla por cuenta y no guarda token ni URL del checkout',async()=>{
 const memory=new Map();const {paymentAttemptStorage}=load('services/paymentAttemptStorage.ts',{'@react-native-async-storage/async-storage':{__esModule:true,default:{setItem:async(k,v)=>memory.set(k,v),getItem:async k=>memory.get(k)??null,removeItem:async k=>memory.delete(k)}},'react-native':{Platform:{OS:'android'}},'./apiConfig':{tenantId:'tenant-a'},'../utils/paymentLinks':links});
 const a=paymentAttemptStorage('a'),b=paymentAttemptStorage('b');await a.save(id);assert.equal(await a.read(),id);assert.equal(await b.read(),null);
 const entry=JSON.parse([...memory.values()][0]);assert.deepEqual(Object.keys(entry).sort(),['createdAt','orderId']);
});
test('limpieza de logout espera una escritura pendiente y no resucita el intento',async()=>{
 const gate=deferred();const memory=new Map();const {paymentAttemptStorage}=load('services/paymentAttemptStorage.ts',{'@react-native-async-storage/async-storage':{__esModule:true,default:{setItem:async(k,v)=>{await gate.promise;memory.set(k,v);},getItem:async k=>memory.get(k)??null,removeItem:async k=>memory.delete(k)}},'react-native':{Platform:{OS:'android'}},'./apiConfig':{tenantId:'tenant-a'},'../utils/paymentLinks':links});
 const storage=paymentAttemptStorage('a');const save=storage.save(id);const clear=storage.clear();gate.resolve();await Promise.all([save,clear]);assert.equal(await storage.read(),null);
});

