import {useRef, useState} from 'react';
import {useAuth} from '../contexts/AuthContext';
import {useCart} from '../contexts/CartContext';
import {useCheckout} from '../contexts/CheckoutContext';
import {CheckoutHttpError, newRequestId, quoteOrder, tenantId, type CheckoutBody} from '../services/checkoutService';
import type {Address} from '../services/customerTypes';
import type {DeliveryPoint} from '../components/DeliveryLocation';
export const formatAddress = (address: Address): string => `${address.calle} ${address.numero}${address.pisoDepto ? ', ' + address.pisoDepto : ''}`;
export function useCheckoutForm() {
  const auth = useAuth(); const cart = useCart(); const checkout = useCheckout(); const savedAddress = auth.user?.direcciones[0];
  const [nombre, setNombre] = useState(auth.user?.nombre ?? ''), [email, setEmail] = useState(auth.user?.email ?? ''), [phone, setPhone] = useState(auth.user?.telefono ?? ''), [entrega, setEntrega] = useState<'Retiro' | 'Delivery'>('Retiro'), [direccion, setDireccion] = useState(savedAddress ? formatAddress(savedAddress) : ''), [point, setPoint] = useState<DeliveryPoint | null>(savedAddress?.latitud != null && savedAddress.longitud != null ? {latitude: savedAddress.latitud, longitude: savedAddress.longitud, accuracy: null} : null), [method, setMethod] = useState<'Efectivo' | 'TransferenciaCBU' | 'PasarelaOnline'>('Efectivo'), [notas, setNotas] = useState('');
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  const coupon = cart.couponCode, setCoupon = cart.setCouponCode, quotation = cart.quotation;
  const requestId = useRef<string | null>(null), working = useRef(false);
  const signature = JSON.stringify({owner: auth.user?.id ?? 'guest',nombre, email, phone, entrega, direccion, point, method, notas, coupon, items: cart.items.map(i => [i.id, i.cantidad, i.precio])});
  const quote = quotation?.signature === signature ? quotation.value : null;
  function body(): CheckoutBody {
    const number = phone.replace(/\D/g, ''); const normalizedPhone = number.length === 10 ? '549' + number : number;
    if (!nombre.trim() || !/^\S+@\S+\.\S+$/.test(email.trim()) || normalizedPhone.length < 8 || normalizedPhone.length > 15 || !cart.items.length || entrega === 'Delivery' && !direccion.trim()) throw new Error('Completá nombre, email, teléfono y dirección si elegís delivery.');
    if (method === 'PasarelaOnline' && !auth.user) throw new Error('Ingresá a tu cuenta para pagar online.');
    if (!requestId.current) requestId.current = newRequestId();
    return {tenantId, pedido: {requestId: requestId.current, guestEmail: email.trim(), guestPhone: normalizedPhone, nombreCliente: nombre.trim(), metodoPago: method, items: cart.items.map(i => ({productId: i.id, cantidad: i.cantidad})), tipoEntrega: entrega, direccionEntrega: entrega === 'Delivery' ? direccion.trim() : null, latitud: entrega === 'Delivery' ? point?.latitude ?? null : null, longitud: entrega === 'Delivery' ? point?.longitude ?? null : null, notas: notas.trim() || null, couponCode: coupon.trim() || null}};
  }
  async function cotizar() {
    if (working.current) return; working.current = true; setBusy(true); setError('');
    try {const value = await quoteOrder(body(), auth.session?.accessToken); cart.storeQuotation(signature, value);}
    catch (error) {cart.invalidateQuotation(); if (error instanceof CheckoutHttpError && error.status === 401 && auth.session) await auth.expire(auth.session.accessToken); setError(error instanceof Error ? error.message : 'No se pudo cotizar.');}
    finally {working.current = false; setBusy(false);}
  }
  async function confirmar(): Promise<string | null> {
    if (working.current) return null; working.current = true; setBusy(true); setError('');
    try {
      if (checkout.pending) return (await checkout.submit()).id;
      if (!quote) throw new Error('Revisá el total antes de confirmar.');
      const request = body(); request.pedido.totalEsperado = quote.total;
      return (await checkout.submit(request)).id;
    } catch (error) {setError(error instanceof Error ? error.message : 'No se pudo guardar el pedido.'); if (error instanceof CheckoutHttpError && [400, 409].includes(error.status)) {cart.invalidateQuotation(); requestId.current = null;} return null;}
    finally {working.current = false; setBusy(false);}
  }
  function chooseAddress(address: Address) {setEntrega('Delivery'); setDireccion(formatAddress(address)); setPoint(address.latitud !== null && address.longitud !== null ? {latitude: address.latitud, longitude: address.longitud, accuracy: null} : null);}
  return {nombre, setNombre, email, setEmail, phone, setPhone, entrega, setEntrega, direccion, setDireccion, point, setPoint, method, setMethod, notas, setNotas, coupon, setCoupon, busy: busy || checkout.busy, error, quote, cotizar, confirmar, chooseAddress};
}
