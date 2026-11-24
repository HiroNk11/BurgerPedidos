import {createContext, useCallback, useContext, useEffect, useRef, useState, type PropsWithChildren} from 'react';
import {useAuth} from './AuthContext';
import {useCart} from './CartContext';
import {useOrders} from './OrdersContext';
import {CheckoutHttpError, clearJournal, createOrder, newToken, readJournal, saveJournal, tenantId, type CheckoutBody, type Journal, type SavedOrder} from '../services/checkoutService';
type CheckoutValue = {ready: boolean; busy: boolean; pending: Journal | null; recoveryError: string; submit(body?: CheckoutBody): Promise<SavedOrder>};
const CheckoutContext = createContext<CheckoutValue | null>(null);
export function CheckoutProvider({children}: PropsWithChildren) {
  const auth = useAuth(); const {clearCart, setLocked} = useCart(); const {registerOrder} = useOrders();
  const [ready, setReady] = useState(false), [pending, setPending] = useState<Journal | null>(null), [busy, setBusy] = useState(false), [recoveryError, setRecoveryError] = useState('');
  const inFlight = useRef(false);
  const archive = useCallback(async (saved: Journal) => {
    clearCart();
    await saveJournal(saved);
    await registerOrder(saved);
    await clearJournal();
    setPending(null); setLocked(false);
  }, [clearCart, registerOrder, setLocked]);
  useEffect(() => {
    let mounted = true;
    readJournal().then(async journal => {
      if (!mounted) return;
      if (journal && journal.body.tenantId !== tenantId) throw new Error('Hay una solicitud pendiente de otro local. No generes otra compra sin revisarla.');
      if (journal?.result) {setPending(journal); await archive(journal);}
      else {setPending(journal); setLocked(!!journal);}
    }).catch(error => {if (mounted) {setRecoveryError(error instanceof Error ? error.message : 'No se pudo recuperar la solicitud.'); setLocked(true);}}).finally(() => {if (mounted) setReady(true);});
    return () => {mounted = false;};
  }, [archive, setLocked]);
  const submit = useCallback(async (body?: CheckoutBody): Promise<SavedOrder> => {
    if (!ready || recoveryError) throw new Error(recoveryError || 'Estamos recuperando la compra anterior.');
    if (inFlight.current) throw new Error('El pedido ya se está guardando.');
    inFlight.current = true; setBusy(true);
    let journal = pending;
    try {
      if (!journal) {
        if (!body) throw new Error('Revisá el total antes de confirmar.');
        if (auth.status === 'loading' || auth.status === 'unavailable') throw new Error('Recuperá tu sesión antes de confirmar la compra.');
        journal = {body: {...body, pedido: {...body.pedido, checkoutToken: newToken()}}, auth: auth.user ? {mode: 'customer', userId: auth.user.id} : {mode: 'guest'}};
      }
      const customer = journal.auth?.mode === 'customer';
      if (customer && (!auth.user || journal.auth?.mode !== 'customer' || auth.user.id !== journal.auth.userId || !auth.session)) throw new Error('Volvé a ingresar a la cuenta de esta solicitud para reintentar.');
      if (journal.result) {await archive(journal); return journal.result;}
      setLocked(true); await saveJournal(journal); setPending(journal);
      const result = await createOrder(journal.body, customer ? auth.session?.accessToken : undefined);
      const saved = {...journal, result}; setPending(saved);
      await archive(saved);
      return result;
    } catch (error) {
      if (!journal?.result && error instanceof CheckoutHttpError && (error.status === 400 || error.status === 409)) {
        await clearJournal(); setPending(null); setLocked(false);
      }
      throw error;
    } finally {inFlight.current = false; setBusy(false);}
  }, [ready, recoveryError, pending, auth.user, auth.session, auth.status, archive, setLocked]);
  return <CheckoutContext.Provider value={{ready, busy, pending, recoveryError, submit}}>{children}</CheckoutContext.Provider>;
}
export function useCheckout(): CheckoutValue {const value = useContext(CheckoutContext); if (!value) throw new Error('useCheckout requiere CheckoutProvider.'); return value;}
