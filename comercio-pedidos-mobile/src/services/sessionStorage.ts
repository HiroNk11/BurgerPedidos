import * as SecureStore from 'expo-secure-store';
import {Platform} from 'react-native';
import {tenantId} from './apiConfig';
import type {Session} from './customerTypes';
const key = 'burgerclub-session-' + tenantId;
export async function readSecure(key: string): Promise<string | null> {
  return Platform.OS === 'web' ? typeof window === 'undefined' ? null : window.sessionStorage.getItem(key) : SecureStore.getItemAsync(key);
}
export async function writeSecure(key: string, value: string | null): Promise<void> {
  if (Platform.OS === 'web') {
    if (value === null) window.sessionStorage.removeItem(key); else window.sessionStorage.setItem(key, value);
  } else if (value === null) await SecureStore.deleteItemAsync(key); else await SecureStore.setItemAsync(key, value);
}
let writes: Promise<unknown> = Promise.resolve();
export function saveSession(value: Session | null): Promise<void> {
  const work = writes.then(() => writeSecure(key, value ? JSON.stringify(value) : null));
  writes = work.catch(() => undefined); return work;
}
export async function loadSession(): Promise<Session | null> {
  const raw = await readSecure(key);
  if (!raw) return null;
  const value = JSON.parse(raw) as Session;
  if (!value.accessToken || !Number.isFinite(Date.parse(value.expiresAtUtc)) || Date.parse(value.expiresAtUtc) <= Date.now()) { await saveSession(null); return null; }
  return value;
}
