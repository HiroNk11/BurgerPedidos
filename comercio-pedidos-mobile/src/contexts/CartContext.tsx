import {createContext, useCallback, useContext, useEffect, useReducer, useRef, useState, type PropsWithChildren} from 'react';
import {loadStore, type CatalogProduct, type Store, type Quote} from '../services/checkoutService';
import {cartReducer, initialCart, type CartAction, type CartLine, type CartState} from '../state/cartReducer';
type CartValue = CartState & {couponCode: string; setCouponCode(value: string): void; quotation: {signature: string; value: Quote} | null; storeQuotation(signature: string, value: Quote): void; invalidateQuotation(): void;store: Store | null; loading: boolean; error: string; cantidad: number; subtotal: number; setQuantity(product: CatalogProduct, quantity: number): void; clearCart(): void; setLocked(value: boolean): void; replaceCart(items: CartLine[], expectedRevision?: number): boolean; refreshCatalog(): Promise<Store>};
const CartContext = createContext<CartValue | null>(null);
export function CartProvider({children}: PropsWithChildren) {
  const [state, dispatch] = useReducer(cartReducer, initialCart);
  const [catalog, setCatalog] = useState<{store: Store | null; loading: boolean; error: string}>({store: null, loading: true, error: ''});
  const [couponCode, setCoupon] = useState('');
  const [quotation, setQuotation] = useState<{signature: string; value: Quote} | null>(null);
  const setCouponCode = useCallback((value: string) => {setCoupon(value.toUpperCase()); setQuotation(null);}, []);
  const storeQuotation = useCallback((signature: string, value: Quote) => setQuotation({signature, value}), []);
  const invalidateQuotation = useCallback(() => setQuotation(null), []);
  const latest = useRef(initialCart);
  const send = useCallback((action: CartAction) => {latest.current = cartReducer(latest.current, action); dispatch(action);}, []);
  const refreshCatalog = useCallback(async () => {
    try {const store = await loadStore(); setCatalog({store, loading: false, error: ''}); return store;}
    catch (error) {setCatalog(current => ({...current, loading: false, error: error instanceof Error ? error.message : 'No se pudo cargar el menú.'})); throw error;}
  }, []);
  useEffect(() => {void refreshCatalog().catch(() => undefined);}, [refreshCatalog]);
  const setQuantity = useCallback((product: CatalogProduct, quantity: number) => send({type: 'quantity', product, quantity}), [send]);
  const clearCart = useCallback(() => {send({type: 'clear'}); setCoupon(''); setQuotation(null);}, [send]);
  const setLocked = useCallback((value: boolean) => send({type: 'lock', value}), [send]);
  const replaceCart = useCallback((items: CartLine[], expectedRevision?: number) => {
    if (latest.current.locked || expectedRevision !== undefined && latest.current.revision !== expectedRevision) return false;
    send({type: 'replace', items, expectedRevision}); setCoupon(''); setQuotation(null); return true;
  }, [send]);
  return <CartContext.Provider value={{...state, ...catalog, couponCode, setCouponCode, quotation, storeQuotation, invalidateQuotation, setQuantity, clearCart, setLocked, replaceCart, refreshCatalog, cantidad: state.items.reduce((sum, item) => sum + item.cantidad, 0), subtotal: state.items.reduce((sum, item) => sum + item.precio * item.cantidad, 0)}}>{children}</CartContext.Provider>;
}
export function useCart(): CartValue {const value = useContext(CartContext); if (!value) throw new Error('useCart requiere CartProvider.'); return value;}
