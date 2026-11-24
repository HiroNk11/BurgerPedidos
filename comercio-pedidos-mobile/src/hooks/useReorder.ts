import {useEffect, useRef, useState} from 'react';
import {useRouter} from 'expo-router';
import {useAuth} from '../contexts/AuthContext';
import {useCart} from '../contexts/CartContext';
import {useOrders} from '../contexts/OrdersContext';
import {confirmAction} from '../utils/confirmAction';
import {prepareReorder} from '../utils/reorderCart';
export function useReorder() {
  const auth = useAuth(); const cart = useCart(); const orders = useOrders(); const router = useRouter();
  const [busyId, setBusyId] = useState<string | null>(null); const busy = useRef(false); const currentUser = useRef(auth.user?.id);
  useEffect(() => {currentUser.current = auth.user?.id;}, [auth.user?.id]);
  // Event handlers compare after awaiting the confirmation, never during render.
  async function reorder(id: string) {
    if (!auth.user) {router.navigate({pathname: '/login', params: {returnTo: '/orders'}}); return;}
    if (busy.current) return;
    if (cart.locked) throw new Error('Primero resolvé la solicitud pendiente para evitar duplicar una compra.');
    busy.current = true; setBusyId(id);
    const owner = auth.user.id, revision = cart.revision;
    try {
      const proposal = await orders.reorder(id); const catalog = await cart.refreshCatalog();
      const prepared = prepareReorder(proposal, catalog.products);
      if (!prepared.items.length) {await confirmAction('No hay productos disponibles', [...prepared.messages, 'Tu carrito actual se conserva.'].join('\n')); return;}
      const messages = [...prepared.messages];
      if (cart.items.length) messages.push('Se reemplazará el contenido de tu carrito actual.');
      if (messages.length && !await confirmAction('Revisá tu nueva compra', messages.join('\n'))) return;
      if (currentUser.current !== owner || !cart.replaceCart(prepared.items, revision)) throw new Error('Tu sesión o carrito cambió. Revisá la compra antes de volver a pedir.');
      router.navigate('/checkout');
    } finally {busy.current = false; setBusyId(null);}
  }
  return {busyId, reorder};
}
