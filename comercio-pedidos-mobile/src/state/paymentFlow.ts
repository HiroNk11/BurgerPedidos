import type {CustomerOrder, OnlinePaymentCheckout} from '../services/customerTypes';
import {HttpError} from '../services/http';
import {canPayOnline, safeCheckoutUrl} from '../utils/paymentLinks';
type Phase = 'idle' | 'requesting' | 'opening' | 'waiting' | 'checking';
export type PaymentFlowState = {phase: Phase; orderId: string | null; error: string; message: string};
export const idlePayment: PaymentFlowState = {phase: 'idle', orderId: null, error: '', message: ''};
export type PaymentFlowDependencies = {
  order(id: string, signal: AbortSignal): Promise<CustomerOrder>;
  start(id: string, signal: AbortSignal): Promise<OnlinePaymentCheckout>;
  open(url: string): Promise<'opened' | 'closed'>;
  dismiss(): Promise<void>;
  refresh(): Promise<void>;
  unauthorized(): Promise<void>;
  storage: {save(id: string): Promise<void>; clear(): Promise<void>; read(): Promise<string | null>};
};
export class PaymentFlow {
  private state = idlePayment;
  private listeners = new Set<() => void>();
  private version = 0;
  private abort: AbortController | null = null;
  private timers: ReturnType<typeof setTimeout>[] = [];
  private finishing: Promise<void> | null = null;
  constructor(private readonly deps: PaymentFlowDependencies) {}
  setRefresh(refresh: () => Promise<void>) {this.deps.refresh = refresh;}
  getSnapshot = () => this.state;
  subscribe = (listener: () => void) => {this.listeners.add(listener); return () => {this.listeners.delete(listener);};};
  private update(value: PaymentFlowState) {this.state = value; this.listeners.forEach(listener => listener());}
  private clearTimers() {this.timers.forEach(clearTimeout); this.timers = [];}
  async restore() {
    const version = this.version;
    try {const id = await this.deps.storage.read(); if (id && version === this.version && this.state.phase === 'idle') await this.returned(id);} catch { /* El historial del backend sigue siendo recuperable. */ }
  }
  async pay(order: CustomerOrder) {
    if (this.state.phase !== 'idle') return;
    if (!canPayOnline(order)) {this.update({...idlePayment, orderId: order.id, error: 'Este pedido no admite iniciar un pago online.'}); return;}
    const version = ++this.version; this.clearTimers(); const abort = new AbortController(); this.abort = abort;
    this.update({phase: 'requesting', orderId: order.id, error: '', message: ''});
    try {
      const current = await this.deps.order(order.id, abort.signal);
      if (version !== this.version) return;
      if (!canPayOnline(current)) {await this.deps.refresh(); throw new Error('El pedido cambió o ya fue pagado. Revisá su estado.');}
      const response = await this.deps.start(order.id, abort.signal);
      if (version !== this.version) return;
      if (response.proveedor !== 'MercadoPago') throw new Error('El local todavía no habilitó Mercado Pago para este pedido.');
      const url = safeCheckoutUrl(response.checkoutUrl);
      this.update({phase: 'opening', orderId: order.id, error: '', message: ''});
      await this.deps.storage.save(order.id);
      if (version !== this.version) return;
      this.update({phase: 'waiting', orderId: order.id, error: '', message: 'Cuando vuelvas de Mercado Pago consultaremos el estado del pedido.'});
      const result = await this.deps.open(url);
      if (version === this.version && result === 'closed' && this.getSnapshot().phase === 'waiting') await this.returned(order.id);
    } catch (error) {
      if (version !== this.version || this.getSnapshot().phase === 'checking' || this.getSnapshot().phase === 'idle') return;
      this.update({...idlePayment, orderId: order.id, error: error instanceof Error ? error.message : 'No se pudo iniciar el pago.'});
      if (error instanceof HttpError && error.status === 401) await this.deps.unauthorized().catch(() => undefined);
    }
  }
  returned(orderHint: string | null = null): Promise<void> {
    if (this.finishing) return this.finishing;
    if (this.state.phase === 'requesting' || this.state.phase === 'opening') return Promise.resolve();
    if (orderHint && this.state.orderId && this.state.phase === 'waiting' && orderHint !== this.state.orderId) return Promise.resolve();
    const ownsBrowser = this.state.phase === 'waiting';
    const version = this.version; const orderId = ownsBrowser ? this.state.orderId : orderHint ?? this.state.orderId;
    this.clearTimers();
    this.update({phase: 'checking', orderId, error: '', message: 'Estamos consultando el estado de tu pago. El retorno de Mercado Pago no confirma el cobro.'});
    const run = async () => {
      try {
        if (ownsBrowser) await this.deps.dismiss();
        if (version !== this.version) return;
        await this.deps.refresh();
        if (version === this.version) await this.deps.storage.clear().catch(() => undefined);
      } catch {if (version === this.version) this.update({...this.state, error: 'No pudimos actualizar el pago. Usá Actualizar estado para consultar al local.'});}
      finally {
        if (version === this.version) {
          this.update({...this.state, phase: 'idle'});
          // Webhooks demorados: el polling general continúa además de estas consultas cortas.
          for (const delay of [4000, 10000]) this.timers.push(setTimeout(() => {if (version === this.version) void this.deps.refresh().catch(() => undefined);}, delay));
        }
      }
    };
    const completion = run().finally(() => {if (this.finishing === completion) this.finishing = null;});
    this.finishing = completion; return completion;
  }
  clearNotice() {if (this.state.phase === 'idle') this.update(idlePayment);}
  cancel() {
    ++this.version; this.abort?.abort(); this.abort = null; this.clearTimers(); this.finishing = null;
    if (this.state.phase === 'waiting') void this.deps.dismiss();
    this.update(idlePayment);
  }
}
