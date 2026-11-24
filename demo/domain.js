export const products = [
 {id:'classic',name:'La Clásica',description:'Smash, cheddar, lechuga, tomate y nuestra salsa secreta.',price:8500,category:'Burgers',image:'classic.jpg',tag:'La favorita'},
 {id:'bacon',name:'Doble Bacon',description:'Doble smash, doble cheddar, bacon crocante y barbacoa.',price:11500,category:'Burgers',image:'bacon.jpg',tag:'Para ir con todo'},
 {id:'onion',name:'Crispy Onion',description:'Smash, cheddar, cebolla crispy y un toque de alioli.',price:9800,category:'Burgers',image:'onion.jpg'},
 {id:'fries',name:'Papas clásicas',description:'Doradas, crocantes y con el punto justo de sal.',price:3500,category:'Papas',image:'fries-classic.jpg'},
 {id:'cheddar',name:'Papas cheddar',description:'Cheddar fundido y bacon. El acompañamiento perfecto.',price:5200,category:'Papas',image:'fries-cheddar.jpg',tag:'Para compartir'},
 {id:'cola',name:'Coca-Cola · lata',description:'354 ml · bien fría.',price:2500,category:'Bebidas',image:'coca-can.jpg'},
 {id:'zero',name:'Coca-Cola Sin Azúcar',description:'354 ml · todo el sabor, sin azúcar.',price:2500,category:'Bebidas',image:'coca-zero-can.jpg'},
 {id:'sprite',name:'Sprite · lata',description:'354 ml · lima-limón.',price:2500,category:'Bebidas',image:'sprite-can.jpg'},
 {id:'bottle',name:'Coca-Cola · 1,25 L',description:'El tamaño ideal para compartir.',price:4500,category:'Bebidas',image:'coca-bottle.jpg'},
];
export const states=['PendienteConfirmacion','Aceptado','EnPreparacion','EnCamino','Entregado','Cancelado'];
export const labels={PendienteConfirmacion:'Por confirmar',Aceptado:'Aceptado',EnPreparacion:'En preparación',EnCamino:'En camino',Entregado:'Entregado',Cancelado:'Cancelado',Pendiente:'Pago pendiente',ComprobanteEnviado:'Comprobante enviado',Pagado:'Pagado',Rechazado:'Rechazado',Efectivo:'Efectivo',TransferenciaCBU:'Transferencia',PasarelaOnline:'Mercado Pago'};
export const active=o=>!['Entregado','Cancelado'].includes(o.state);
export function quote(cart,coupon='',delivery='Retiro'){
 const lines=Object.entries(cart).map(([id,qty])=>{
  const p=products.find(p=>p.id===id);if(!p||!Number.isInteger(qty)||qty<1||qty>20)throw Error('Revisá las cantidades del carrito (máximo 20 por producto).');
  return {id,name:p.name,price:p.price,qty,total:p.price*qty};
 });
 if(!['Retiro','Delivery'].includes(delivery))throw Error('Elegí retiro o delivery.');
 const normalized=coupon.trim().toUpperCase();
 if(normalized&&normalized!=='DEMO20')throw Error('Ese cupón no está disponible. Probá DEMO20.');
 const subtotal=lines.reduce((s,l)=>s+l.total,0),discount=normalized==='DEMO20'?Math.round(subtotal*.2):0,shipping=delivery==='Delivery'&&subtotal?1500:0;
 return {lines,subtotal,discount,shipping,total:subtotal-discount+shipping,coupon:normalized,delivery};
}
export function seed(now=new Date()){
 const previous={...quote({bacon:1,fries:1,cola:1}),id:'demo-1041',code:'#DEMO-1041',date:new Date(now.getTime()-86400000).toISOString(),state:'Entregado',payment:'Pagado',method:'Efectivo',name:'Alex · cuenta demo',address:'',notes:''};
 return {version:1,sequence:1042,cart:{},coupon:'',delivery:'Retiro',orders:[previous],favorites:['classic','bacon'],mail:[]};
}
export function createOrder(data,details,now=new Date()){
 const q=quote(data.cart,data.coupon,data.delivery);if(!q.lines.length)throw Error('Primero agregá algo rico al carrito.');
 if(!['Efectivo','TransferenciaCBU','PasarelaOnline'].includes(details.method))throw Error('Elegí un medio de pago.');
 const order={...q,id:'demo-'+data.sequence,code:'#DEMO-'+data.sequence,date:now.toISOString(),state:'PendienteConfirmacion',payment:'Pendiente',method:details.method,name:'Alex · cuenta demo',address:q.delivery==='Delivery'?'Av. Demo 123 · Ciudad de ejemplo':'',notes:'Pedido de demostración. No preparar ni enviar.'};
 return {...data,sequence:data.sequence+1,orders:[order,...data.orders],cart:{},coupon:'',mail:[mail(order,'Recibimos tu solicitud de pedido',now),...data.mail].slice(0,60)};
}
function mail(order,subject,now){return {id:order.id+'-'+subject,date:now.toISOString(),code:order.code,to:'alex@example.com',subject:subject+' '+order.code};}
export function transition(data,id,target,now=new Date()){
 const order=data.orders.find(o=>o.id===id);if(!order)throw Error('No encontramos ese pedido de prueba.');
 const allowed={PendienteConfirmacion:['Aceptado','Cancelado'],Aceptado:['EnPreparacion','Cancelado'],EnPreparacion:['EnCamino','Entregado','Cancelado'],EnCamino:['Entregado','Cancelado'],Entregado:[],Cancelado:[]};
 if(!allowed[order.state]?.includes(target))throw Error('Ese cambio de estado no está permitido.');
 if(target==='Cancelado'&&order.payment==='Pagado')throw Error('El pedido ya está pagado. Un reembolso requiere un flujo separado.');
 const updated={...order,state:target};let messages=data.mail;
 if(['Aceptado','EnCamino','Entregado'].includes(target))messages=[mail(updated,'Tu pedido '+labels[target].toLowerCase(),now),...messages].slice(0,60);
 return {...data,orders:data.orders.map(o=>o.id===id?updated:o),mail:messages};
}
export function payment(data,id,event){
 const order=data.orders.find(o=>o.id===id);if(!order||!active(order))throw Error('El pedido ya no admite cambios de pago.');
 if(order.payment==='Pagado')throw Error('Este pago ya está confirmado.');
 if(event==='receipt'&&order.method!=='TransferenciaCBU')throw Error('Ese medio no utiliza comprobantes.');
 if(event==='approved'&&order.method!=='PasarelaOnline')throw Error('Este pedido no usa pasarela.');
 if(event==='manual'&&order.method==='PasarelaOnline')throw Error('La pasarela solo se confirma mediante el webhook simulado.');
 if(!['receipt','approved','manual'].includes(event))throw Error('Acción de pago inválida.');
 return {...data,orders:data.orders.map(o=>o.id===id?{...o,payment:event==='receipt'?'ComprobanteEnviado':'Pagado'}:o)};
}
export function reorder(data,id){const order=data.orders.find(o=>o.id===id);if(!order)throw Error('No encontramos ese pedido.');return {...data,cart:Object.fromEntries(order.lines.map(l=>[l.id,l.qty])),coupon:''};}
export function restore(raw){
 try{const data=JSON.parse(raw);if(data?.version!==1||!Number.isInteger(data.sequence)||data.sequence<1042||!Array.isArray(data.orders)||data.orders.length>100||!Array.isArray(data.favorites)||!Array.isArray(data.mail))throw Error();
 quote(data.cart,data.coupon,data.delivery);
 for(const o of data.orders){if(typeof o.id!=='string'||!/^demo-\d+$/.test(o.id)||!states.includes(o.state)||!['Pendiente','ComprobanteEnviado','Pagado','Rechazado'].includes(o.payment)||!['Efectivo','TransferenciaCBU','PasarelaOnline'].includes(o.method)||!Array.isArray(o.lines)||!Number.isFinite(o.total)||typeof o.date!=='string'||!Number.isFinite(Date.parse(o.date)))throw Error();for(const l of o.lines)if(!products.some(p=>p.id===l.id)||!Number.isInteger(l.qty)||l.qty<1||l.qty>20||!Number.isFinite(l.price))throw Error();}
 return data;
 }catch{return seed();}
}
