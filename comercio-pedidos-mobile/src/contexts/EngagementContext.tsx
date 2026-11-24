import {createContext, useCallback, useContext, useEffect, useReducer, useRef, type PropsWithChildren} from 'react';
import {useAuth} from './AuthContext';
import {useOrders} from './OrdersContext';
import {engagementApi, type CustomerProduct, type RankedProduct} from '../services/engagementApi';
import {HttpError} from '../services/http';
import {emptyFavorites, favoritesReducer} from '../state/favorites';
type Value = {favorites: CustomerProduct[]; ranking: RankedProduct[]; pending: string[]; error: string; ready: boolean; refresh(): Promise<void>; toggle(product: CustomerProduct): Promise<void>};
const EngagementContext = createContext<Value | null>(null);
export function EngagementProvider({children}: PropsWithChildren) {
  const auth = useAuth(); const {expire} = auth; const orders = useOrders(); const scope = auth.user?.id ?? ''; const token = auth.session?.accessToken;
  const [state, dispatch] = useReducer(favoritesReducer, emptyFavorites);
  const latest = useRef(state); const liveScope = useRef(scope); const sequence = useRef(0); const authEpoch = useRef(0); const pending = useRef(new Set<string>());
  const send = useCallback((action: Parameters<typeof favoritesReducer>[1]) => {latest.current = favoritesReducer(latest.current, action); dispatch(action);}, []);
  const invalidate = useCallback(() => {++sequence.current;}, []);
  const refresh = useCallback(async () => {
    if (!scope || !token || pending.current.size) return;
    const run = ++sequence.current;
    try {
      const [favorites, ranking] = await Promise.all([engagementApi.favorites(token), engagementApi.ranking(token)]);
      if (run === sequence.current) send({type: 'hydrate', scope, favorites, ranking});
    } catch (error) {
      if (run !== sequence.current) return;
      send({type: 'error', scope, error: error instanceof Error ? error.message : 'No se pudieron cargar tus productos.'});
      if (error instanceof HttpError && error.status === 401) await expire(token);
    }
  }, [scope, token, send, expire]);
  const orderKey = orders.active.map(o => o.id + o.estadoPedido).join(',') + ':' + orders.history.map(o => o.id + o.estadoPedido).join(',');
  useEffect(() => {liveScope.current = scope; ++authEpoch.current; ++sequence.current; pending.current.clear(); send({type: 'reset', scope});}, [scope, token, send]);
  useEffect(() => {void refresh(); return invalidate;}, [refresh, orderKey, invalidate]);
  const toggle = useCallback(async (product: CustomerProduct) => {
    if (!scope || !token) throw new Error('Ingresá para guardar tus favoritas.');
    const epoch = authEpoch.current;
    const key = epoch + ':' + scope + ':' + product.id;
    if (pending.current.has(key)) return;
    const original = latest.current.favorites.find(p => p.id === product.id);
    pending.current.add(key); ++sequence.current; send({type: 'toggle', scope, product});
    try {
      const result = await engagementApi.toggle(token, product.id);
      if (liveScope.current === scope && authEpoch.current === epoch) send({type: 'settle', scope, product, favorite: result.esFavorito});
    } catch (error) {
      if (liveScope.current !== scope || authEpoch.current !== epoch) return;
      send({type: 'settle', scope, product: original ?? product, favorite: !!original, error: 'No se guardó el cambio. Se restauró tu selección. Actualizá si hubo un corte de conexión.'});
      if (error instanceof HttpError && error.status === 401) await expire(token);
    } finally {pending.current.delete(key); if (liveScope.current === scope && authEpoch.current === epoch && !pending.current.size) void refresh();}
  }, [scope, token, send, expire, refresh]);
  const visible = state.scope === scope && scope ? state : emptyFavorites;
  return <EngagementContext.Provider value={{favorites: visible.favorites, ranking: visible.ranking, pending: visible.pending, ready: visible.ready, error: visible.error, refresh, toggle}}>{children}</EngagementContext.Provider>;
}
export function useFavorites() {const value = useContext(EngagementContext); if (!value) throw new Error('useFavorites requiere EngagementProvider.'); return value;}
