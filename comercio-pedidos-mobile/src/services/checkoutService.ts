import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import {Platform} from 'react-native';
import type {ImagePickerAsset} from 'expo-image-picker';
import {tenantId} from './apiConfig';
import {http, HttpError} from './http';
export {newRequestId,newToken} from './checkoutIdentity';
export {commerceUrl, tenantId} from './apiConfig';
export type CatalogProduct={id:string;nombre:string;descripcion:string;precio:number;stock:number;categoria:string;imagenKey:string|null};
export type Store={id:string;nombre:string;moneda:string;envio:number;products:CatalogProduct[]};
export type Quote={bruto:number;descuento:number;envio:number;total:number;moneda:string;promocion?:string|null;codigoCupon?:string|null};
export type CheckoutBody={tenantId:string;pedido:{requestId:string;guestEmail:string;guestPhone:string;metodoPago:'Efectivo'|'TransferenciaCBU'|'PasarelaOnline';items:{productId:string;cantidad:number}[];nombreCliente:string;tipoEntrega:'Retiro'|'Delivery';direccionEntrega:string|null;latitud:number|null;longitud:number|null;notas:string|null;couponCode:string|null;checkoutToken?:string;totalEsperado?:number}};
export type SavedOrder=Quote&{id:string;shortCode:string;fecha:string;estadoPedido:string;estadoPago:string;paymentId:string;mensajeWhatsApp:string;whatsAppUrl:string;items:import('./customerTypes').OrderItem[]};
export type Journal={body:CheckoutBody;result?:SavedOrder;auth?:{mode:'guest'}|{mode:'customer';userId:string}};
const journalKey='burgerclub-checkout-v1',tokenKey='burgerclub-order-token';
export {HttpError as CheckoutHttpError};
export const request = <T>(path: string, init: RequestInit = {}, timeoutMs = 20000): Promise<T> => http<T>(path, {...init, timeoutMs});
export async function loadStore():Promise<Store>{if(!tenantId)throw new Error('Falta configurar el comercio de la app.');return request('/api/tienda?tenantId='+encodeURIComponent(tenantId));}
export const quoteOrder=(body:CheckoutBody,token?:string)=>http<Quote>('/api/pedidos/calcular-totales',{token,method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({tenantId:body.tenantId,items:body.pedido.items,codigoCupon:body.pedido.couponCode,tipoEntrega:body.pedido.tipoEntrega})});
export const createOrder=(body:CheckoutBody,token?:string)=>http<SavedOrder>('/api/pedidos',{token,method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
async function storeValue(key:string,value:string|null,secure=false){if(Platform.OS==='web'){if(value===null)sessionStorage.removeItem(key);else sessionStorage.setItem(key,value);}else if(secure){if(value===null)await SecureStore.deleteItemAsync(key);else await SecureStore.setItemAsync(key,value);}else{if(value===null)await AsyncStorage.removeItem(key);else await AsyncStorage.setItem(key,value);}}
async function readValue(key:string,secure=false){return Platform.OS==='web'?sessionStorage.getItem(key):secure?SecureStore.getItemAsync(key):AsyncStorage.getItem(key);}
export async function saveJournal(journal:Journal){const token=journal.body.pedido.checkoutToken;if(!token)throw new Error('Falta el token del pedido.');await storeValue(tokenKey,token,true);const safe={...journal,body:{...journal.body,pedido:{...journal.body.pedido,checkoutToken:undefined}}};await storeValue(journalKey,JSON.stringify(safe));}
export async function readJournal():Promise<Journal|null>{const data=await readValue(journalKey);if(!data)return null;const token=await readValue(tokenKey,true);if(!token)throw new Error('No se pudo recuperar la clave del pedido en curso. No generes otra compra sin revisar el panel.');const journal=JSON.parse(data) as Journal;journal.body.pedido.checkoutToken=token;return journal;}
export async function clearJournal(){await storeValue(journalKey,null);await storeValue(tokenKey,null,true);}
export const getStatus=(journal:Journal)=>request<{estadoPedido:string;estadoPago:string}>('/api/pedidos/'+journal.result!.id+'?tenantId='+tenantId,{headers:{'X-Order-Token':journal.body.pedido.checkoutToken!}});
export async function uploadReceipt(journal:Journal,asset:ImagePickerAsset){
 if(!journal.result||!journal.body.pedido.checkoutToken)throw new Error('No se pudo recuperar el pedido. Volvé a abrir su ficha antes de subir el comprobante.');
 return uploadOrderReceipt(journal.result.id,asset,{checkoutToken:journal.body.pedido.checkoutToken});
}
export async function uploadOrderReceipt(orderId:string,asset:ImagePickerAsset,credentials:{checkoutToken?:string;accessToken?:string}){
 if(!credentials.checkoutToken&&!credentials.accessToken)throw new Error('Ingresá a tu cuenta para adjuntar el comprobante.');
 const form=new FormData();const name=asset.fileName??'comprobante.jpg';
 if(Platform.OS==='web'){
  const file=asset.file??await (await fetch(asset.uri)).blob();
  if(!file.size||file.size>5*1024*1024)throw new Error('Elegí una imagen de hasta 5 MB.');
  form.append('archivo',file,name);
 }else{
  // SDK 58 uses expo/fetch globally. Its multipart encoder requires a Blob/File;
  // the old React Native {uri,name,type} object throws before reaching the API.
  const {File}=await import('expo-file-system');
  const file=new File(asset.uri);
  if(!file.exists)throw new Error('No se puede leer la imagen seleccionada. Volvé a elegir el comprobante.');
  if(!file.size||file.size>5*1024*1024)throw new Error('Elegí una imagen de hasta 5 MB.');
  form.append('archivo',file);
 }
 return http<{estadoPago:string}>('/api/pedidos/'+encodeURIComponent(orderId)+'/comprobante?tenantId='+tenantId,{method:'POST',token:credentials.accessToken,headers:credentials.checkoutToken?{'X-Order-Token':credentials.checkoutToken}:undefined,body:form,timeoutMs:60000});
}
