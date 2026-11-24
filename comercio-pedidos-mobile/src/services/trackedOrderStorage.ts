import AsyncStorage from '@react-native-async-storage/async-storage';
import {Platform} from 'react-native';
import type {Journal} from './checkoutService';
import {tenantId} from './apiConfig';
import {readSecure, writeSecure} from './sessionStorage';
import {isActiveOrder} from '../utils/orderState';
const key = 'burgerclub-orders-v2-' + tenantId;
const tokenKey = (id: string) => 'burgerclub-order-' + tenantId + '-' + id;
async function readRaw(): Promise<Journal[]> {
  const raw = Platform.OS === 'web' ? typeof window === 'undefined' ? null : sessionStorage.getItem(key) : await AsyncStorage.getItem(key);
  return raw ? JSON.parse(raw) as Journal[] : [];
}
async function writeRaw(journals: Journal[]) {
  const raw = JSON.stringify(journals.map(j => ({...j, body: {...j.body, pedido: {...j.body.pedido, checkoutToken: undefined}}})));
  if (Platform.OS === 'web') sessionStorage.setItem(key, raw); else await AsyncStorage.setItem(key, raw);
}
let writes: Promise<unknown> = Promise.resolve();
function serialize<T>(action: () => Promise<T>): Promise<T> {const task = writes.then(action); writes = task.catch(() => undefined); return task;}
export async function loadTrackedOrders(): Promise<Journal[]> {
  const entries = await readRaw(); const result: Journal[] = [];
  for (const j of entries) {
    if (!j.result) continue;
    const token = await readSecure(tokenKey(j.result.id));
    if (token) result.push({...j, body: {...j.body, pedido: {...j.body.pedido, checkoutToken: token}}});
  }
  return result;
}
export function storeTrackedOrder(journal: Journal): Promise<void> {
  return serialize(async () => {
    if (!journal.result || !journal.body.pedido.checkoutToken) throw new Error('No se pudo conservar el seguimiento del pedido.');
    await writeSecure(tokenKey(journal.result.id), journal.body.pedido.checkoutToken);
    const old = await readRaw(); const merged = [journal, ...old.filter(j => j.result?.id !== journal.result!.id)];
    const active = merged.filter(j => j.result && isActiveOrder(j.result.estadoPedido));
    const past = merged.filter(j => j.result && !isActiveOrder(j.result.estadoPedido)).slice(0, 50);
    const kept = [...active, ...past]; await writeRaw(kept);
    const ids = new Set(kept.map(j => j.result!.id));
    for (const j of old) if (j.result && !ids.has(j.result.id)) await writeSecure(tokenKey(j.result.id), null);
  });
}
export function purgeTrackedAccount(userId: string, email: string): Promise<void> {
  return serialize(async () => {
    const entries = await readRaw();
    const belongs = (j: Journal) => j.auth?.mode === 'customer' ? j.auth.userId === userId : j.body.pedido.guestEmail.trim().toLowerCase() === email.trim().toLowerCase();
    await writeRaw(entries.filter(j => !belongs(j)));
    for (const j of entries.filter(belongs)) if (j.result) await writeSecure(tokenKey(j.result.id), null);
  });
}
