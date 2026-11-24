import {createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useSyncExternalStore, type PropsWithChildren} from 'react';
import {AppState, Platform} from 'react-native';
import * as Linking from 'expo-linking';
import {useAuth} from './AuthContext';
import {useOrders} from './OrdersContext';
import {customerApi} from '../services/customerApi';
import type {CustomerOrder} from '../services/customerTypes';
import {paymentAttemptStorage} from '../services/paymentAttemptStorage';
import {dismissPaymentBrowser, openPaymentBrowser} from '../services/paymentBrowser';
import {idlePayment, PaymentFlow, type PaymentFlowState} from '../state/paymentFlow';
import {parsePaymentReturn} from '../utils/paymentLinks';

type PaymentFlowValue = {state: PaymentFlowState; busy: boolean; pay(order: CustomerOrder): Promise<void>; returned(orderId?: string | null): Promise<void>; clearNotice(): void};
const PaymentFlowContext = createContext<PaymentFlowValue | null>(null);
const subscribeEmpty = () => () => undefined;
const emptySnapshot = () => idlePayment;
export function PaymentFlowProvider({children}: PropsWithChildren) {
  const {user, session, expire, onSessionEnd} = useAuth(); const {refresh: refreshOrders} = useOrders();
  const queuedReturn = useRef<{id: string | null} | null>(null);
  const lastReturn = useRef<{key: string; time: number} | null>(null);
  const userId = user?.id, token = session?.accessToken;
  const flow = useMemo(() => {
    if (!userId || !token) return null;
    return new PaymentFlow({
      order: (id, signal) => customerApi.order(token, id, signal),
      start: (id, signal) => customerApi.iniciarPagoOnline(token, id, signal),
      open: openPaymentBrowser, dismiss: dismissPaymentBrowser,
      refresh: async () => undefined, unauthorized: () => expire(token), storage: paymentAttemptStorage(userId),
    });
  }, [userId, token, expire]);
  useLayoutEffect(() => {flow?.setRefresh(() => refreshOrders(true));}, [flow, refreshOrders]);
  const state = useSyncExternalStore(flow?.subscribe ?? subscribeEmpty, flow?.getSnapshot ?? emptySnapshot, emptySnapshot);
  const returned = useCallback(async (orderId: string | null = null) => {
    if (!flow) {queuedReturn.current = {id: orderId}; return;}
    const key = userId + ':' + (orderId ?? 'active');
    if (lastReturn.current?.key === key && Date.now() - lastReturn.current.time < 1500) return;
    lastReturn.current = {key, time: Date.now()}; await flow.returned(orderId);
  }, [flow, userId]);
  useEffect(() => {
    if (!flow) return;
    const queued = queuedReturn.current; queuedReturn.current = null;
    if (queued) void flow.returned(queued.id); else void flow.restore();
    return () => flow.cancel();
  }, [flow]);
  useEffect(() => onSessionEnd(async customer => {
    if (customer.id !== userId) return;
    flow?.cancel(); queuedReturn.current = null; lastReturn.current = null;
    await paymentAttemptStorage(customer.id).clear().catch(() => undefined);
  }), [flow, onSessionEnd, userId]);
  useEffect(() => {
    let active = true;
    function receive(url: string) {
      if (!active) return;
      const origin = process.env.EXPO_PUBLIC_PAYMENT_RETURN_ORIGIN || (Platform.OS === 'web' && typeof window !== 'undefined' ? window.location.origin : undefined);
      const parsed = parsePaymentReturn(url, origin);
      if (parsed) void returned(parsed.orderId);
    }
    const listener = Linking.addEventListener('url', event => receive(event.url));
    void Linking.getInitialURL().then(url => {if (url) receive(url);}).catch(() => undefined);
    return () => {active = false; listener.remove();};
  }, [returned]);
  useEffect(() => {
    if (!flow) return;
    let away = false;
    const app = AppState.addEventListener('change', next => {
      if (next !== 'active') away = true;
      else if (away) {away = false; if (flow.getSnapshot().phase === 'waiting') void flow.returned();}
    });
    return () => app.remove();
  }, [flow]);
  const pay = useCallback(async (order: CustomerOrder) => {if (flow) await flow.pay(order);}, [flow]);
  return <PaymentFlowContext.Provider value={{state, busy: state.phase !== 'idle', pay, returned, clearNotice: () => flow?.clearNotice()}}>{children}</PaymentFlowContext.Provider>;
}
export function usePaymentFlow(): PaymentFlowValue {const value = useContext(PaymentFlowContext); if (!value) throw new Error('usePaymentFlow requiere PaymentFlowProvider.'); return value;}
