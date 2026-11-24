import {createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren} from 'react';
import {AppState, Platform} from 'react-native';
import {useAuth} from './AuthContext';
import {customerApi} from '../services/customerApi';
import {getStatus, type Journal} from '../services/checkoutService';
import type {CustomerOrder, ReorderProposal} from '../services/customerTypes';
import {HttpError} from '../services/http';
import {loadTrackedOrders, purgeTrackedAccount, storeTrackedOrder} from '../services/trackedOrderStorage';
import {isActiveOrder, mergeActiveOrders} from '../utils/orderState';
import {beginOrdersRefresh, type OrderFeed} from '../state/orderFeed';
type OrdersValue = {active: CustomerOrder[]; history: CustomerOrder[]; totalHistory: number; totalActive: number; page: number; ready: boolean; refreshing: boolean; error: string; refreshedAt: number | null; setPage(page: number): void; refresh(force?: boolean): Promise<void>; registerOrder(journal: Journal): Promise<void>; localJournal(id: string): Journal | undefined; reorder(id: string): Promise<ReorderProposal>};
const OrdersContext = createContext<OrdersValue | null>(null);
export function journalOrder(j: Journal): CustomerOrder {
  const saved = j.result!;
  return {id: saved.id, shortCode: saved.shortCode, fecha: saved.fecha, estadoPedido: saved.estadoPedido as CustomerOrder['estadoPedido'], estadoPago: saved.estadoPago as CustomerOrder['estadoPago'], total: saved.total, moneda: saved.moneda, tipoEntrega: j.body.pedido.tipoEntrega, direccionEntrega: j.body.pedido.direccionEntrega, notas: j.body.pedido.notas, items: saved.items ?? [], metodoPago: j.body.pedido.metodoPago};
}
export function OrdersProvider({children}: PropsWithChildren) {
  const auth = useAuth(); const {expire, onSessionEnd} = auth; const token = auth.session?.accessToken; const userId = auth.user?.id; const email = auth.user?.email;
  const scope = userId ?? (auth.status === 'guest' ? 'guest' : 'restoring');
  const [local, setLocal] = useState<Journal[]>([]), [ready, setReady] = useState(false), [pagination, setPagination] = useState({scope, page: 1});
  const page = pagination.scope === scope ? pagination.page : 1;
  const [data, setData] = useState<OrderFeed>({scope: '', page: 1, result: null, refreshing: false, error: '', at: null});
  const sequence = useRef(0), busyScope = useRef<string | null>(null);
  const visible = useMemo(() => local.filter(j => j.auth?.mode === 'customer' ? j.auth.userId === userId : scope === 'guest' || !!email && j.body.pedido.guestEmail.trim().toLowerCase() === email.toLowerCase()), [local, userId, email, scope]);
  useEffect(() => {let mounted = true;loadTrackedOrders().then(entries => {if (mounted) setLocal(current => [...current, ...entries.filter(e => !current.some(j => j.result?.id === e.result?.id))]);}).catch(error => {if (mounted) setData(current => ({...current, error: error.message}));}).finally(() => {if (mounted) setReady(true);});return () => {mounted = false;};}, []);
  useEffect(() => onSessionEnd(async customer => {
    ++sequence.current; busyScope.current = null;
    const belongs = (j: Journal) => j.auth?.mode === 'customer' ? j.auth.userId === customer.id : j.body.pedido.guestEmail.toLowerCase() === customer.email.toLowerCase();
    setLocal(current => current.filter(j => !belongs(j)));
    await purgeTrackedAccount(customer.id, customer.email);
  }), [onSessionEnd]);
  useEffect(() => {
    if (!userId || !email) return;
    const claims = local.filter(j => j.auth?.mode !== 'customer' && j.body.pedido.guestEmail.trim().toLowerCase() === email.toLowerCase());
    if (!claims.length) return;
    let active = true;
    void Promise.resolve().then(async () => {
      if (!active) return;
      const updated = claims.map(j => ({...j, auth: {mode: 'customer' as const, userId}}));
      const writes = updated.map(journal => storeTrackedOrder(journal));
      setLocal(current => current.map(j => updated.find(item => item.result?.id === j.result?.id) ?? j));
      await Promise.all(writes);
    }).catch(() => undefined);
    return () => {active = false;};
  }, [userId, email, local]);
  const registerOrder = useCallback(async (journal: Journal) => {
    if (!journal.result) throw new Error('El pedido todavía no está guardado.');
    setLocal(current => [journal, ...current.filter(j => j.result?.id !== journal.result!.id)]);
    await storeTrackedOrder(journal);
  }, []);
  const invalidateRequests = useCallback(() => {++sequence.current; busyScope.current = null;}, []);
  const refresh = useCallback(async (force = false) => {
    if (scope === 'restoring') return;
    const key = scope + ':' + page;
    if (busyScope.current === key && !force) return;
    busyScope.current = key; const run = ++sequence.current;
    setData(current => beginOrdersRefresh(current, scope, page));
    try {
      const pending = visible.filter(j => j.result && isActiveOrder(j.result.estadoPedido));
      const [server, statuses] = await Promise.all([
        token && userId ? customerApi.orders(token, page) : Promise.resolve(null),
        Promise.allSettled(pending.map(async j => ({journal: j, status: await getStatus(j)}))),
      ]);
      if (run !== sequence.current) return;
      let localFailure = false;
      for (const result of statuses) {
        if (result.status === 'rejected') {localFailure = true; continue;}
        const {journal, status} = result.value;
        if (journal.result!.estadoPedido !== status.estadoPedido || journal.result!.estadoPago !== status.estadoPago) {
          const updated = {...journal, result: {...journal.result!, ...status}};
          setLocal(current => current.map(j => j.result?.id === updated.result.id ? updated : j));
          void storeTrackedOrder(updated).catch(() => undefined);
        }
      }
      setData({scope, page, result: server, refreshing: false, error: localFailure ? 'No pudimos actualizar todos los pedidos. Se conserva el último estado.' : '', at: Date.now()});
    } catch (error) {
      if (run !== sequence.current) return;
      setData(current => ({...current, scope, page, refreshing: false, error: error instanceof Error ? error.message : 'No se pudo actualizar el historial.'}));
      if (error instanceof HttpError && error.status === 401 && token) await expire(token);
    } finally {if (run === sequence.current) busyScope.current = null;}
  }, [scope, page, visible, token, userId, expire]);
  useEffect(() => {
    void refresh();
    const timer = setInterval(() => {if ((AppState.currentState === 'active' || AppState.currentState == null) && (Platform.OS !== 'web' || document.visibilityState === 'visible')) void refresh();}, 12000);
    const listener = AppState.addEventListener('change', state => {if (state === 'active') void refresh();});
    return () => {clearInterval(timer); listener.remove(); invalidateRequests();};
  }, [refresh, invalidateRequests]);
  const current = data.scope === scope && data.page === page ? data : null;
  const localOrders = visible.filter(j => j.result).map(journalOrder);
  const active = mergeActiveOrders([...(current?.result?.activos ?? []), ...(current?.result?.historicos ?? [])], localOrders);
  const history = userId ? current?.result?.historicos ?? [] : localOrders.filter(o => !isActiveOrder(o.estadoPedido)).sort((a,b) => b.fecha.localeCompare(a.fecha));
  const reorder = useCallback(async (id: string) => {
    if (!token || !userId) throw new Error('Ingresá con tu email para volver a pedir.');
    try {return await customerApi.reorder(token, id);} catch (error) {if (error instanceof HttpError && error.status === 401) await expire(token); throw error;}
  }, [token, userId, expire]);
  return <OrdersContext.Provider value={{active, history, page, totalActive: Math.max(active.length, current?.result?.totalActivos ?? 0), totalHistory: userId ? current?.result?.totalHistoricos ?? 0 : history.length, ready, refreshing: current?.refreshing ?? false, error: current?.error ?? '', refreshedAt: current?.at ?? null, setPage: value => setPagination({scope, page: value}), refresh, registerOrder, localJournal: id => visible.find(j => j.result?.id === id), reorder}}>{children}</OrdersContext.Provider>;
}
export function useOrders(): OrdersValue {const value = useContext(OrdersContext); if (!value) throw new Error('useOrders requiere OrdersProvider.'); return value;}
