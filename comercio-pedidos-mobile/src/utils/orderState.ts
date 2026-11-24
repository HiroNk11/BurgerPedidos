import type {OrderState, CustomerOrder} from '../services/customerTypes';
export const isActiveOrder = (state: string): boolean => ['PendienteConfirmacion', 'Aceptado', 'EnPreparacion', 'EnCamino'].includes(state);
export const orderLabels: Record<OrderState, string> = {PendienteConfirmacion: 'Por confirmar', Aceptado: 'Aceptado', EnPreparacion: 'En preparación', EnCamino: 'En camino', Entregado: 'Entregado', Cancelado: 'Cancelado'};
export const paymentLabels: Record<string, string> = {Pendiente: 'Pago pendiente', ComprobanteEnviado: 'Comprobante recibido · en revisión', Pagado: 'Pago confirmado', Rechazado: 'Pago rechazado'};
export const orderDate = (value: string) => new Intl.DateTimeFormat('es-AR', {day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'}).format(new Date(value));
export function mergeActiveOrders(server: CustomerOrder[], local: CustomerOrder[]): CustomerOrder[] {
  const map = new Map(local.map(order => [order.id, order]));
  server.forEach(order => map.set(order.id, order));
  return [...map.values()].filter(order => isActiveOrder(order.estadoPedido)).sort((a, b) => b.fecha.localeCompare(a.fecha));
}
