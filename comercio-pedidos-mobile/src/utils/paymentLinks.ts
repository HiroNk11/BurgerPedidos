import type {CustomerOrder} from '../services/customerTypes';

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const validOrderId = (value: unknown): value is string => typeof value === 'string' && uuid.test(value);
export function canPayOnline(order: CustomerOrder): boolean {
  return order.metodoPago === 'PasarelaOnline' && order.estadoPago === 'Pendiente' && order.total > 0 && ['PendienteConfirmacion', 'Aceptado', 'EnPreparacion', 'EnCamino'].includes(order.estadoPedido);
}
export function safeCheckoutUrl(value: unknown): string {
  if (typeof value !== 'string' || value.length > 2048) throw new Error('El local no devolvió un enlace de pago válido.');
  try {
    const url = new URL(value);
    const host = url.hostname.toLowerCase();
    if (url.protocol !== 'https:' || url.username || url.password || url.port && url.port !== '443' || !['mercadopago.com', 'mercadopago.com.ar'].some(domain => host === domain || host.endsWith('.' + domain))) throw new Error();
    return url.href;
  } catch {throw new Error('El enlace recibido no pertenece a un checkout seguro de Mercado Pago.');}
}
export type PaymentReturn = {orderId: string | null; outcome: 'success' | 'pending' | 'failure' | 'unknown'};
// Un retorno es una señal para consultar la API, nunca una prueba de pago.
export function parsePaymentReturn(value: string, webOrigin?: string): PaymentReturn | null {
  try {
    const url = new URL(value);
    if (url.username || url.password) return null;
    const native = url.protocol === 'gastronomiaapp:' || url.protocol === 'burgerclub:';
    const path = native ? '/' + [url.hostname, url.pathname].filter(Boolean).join('/').replace(/^\/+|\/+$/g, '') : url.pathname.replace(/\/$/, '');
    if (path !== '/payment-return') return null;
    if (!native && (!webOrigin || url.origin !== new URL(webOrigin).origin || !['https:', 'http:'].includes(url.protocol))) return null;
    const rawId = url.searchParams.get('orderId') ?? url.searchParams.get('external_reference');
    const rawOutcome = url.searchParams.get('result');
    return {orderId: validOrderId(rawId) ? rawId.toLowerCase() : null, outcome: rawOutcome === 'success' || rawOutcome === 'pending' || rawOutcome === 'failure' ? rawOutcome : 'unknown'};
  } catch {return null;}
}
export function paymentAppReturn(orderId: string | null): string {
  return 'gastronomiaapp:///payment-return' + (validOrderId(orderId) ? '?orderId=' + encodeURIComponent(orderId) : '');
}
