import AsyncStorage from '@react-native-async-storage/async-storage';
import {Platform} from 'react-native';
import {tenantId} from './apiConfig';
import {validOrderId} from '../utils/paymentLinks';
let writes: Promise<unknown> = Promise.resolve();
function serialize(action: () => Promise<void>) {const task = writes.then(action); writes = task.catch(() => undefined); return task;}
export function paymentAttemptStorage(userId: string) {
  const key = 'burgerclub-payment-return-' + tenantId + '-' + userId;
  // Solo correlación por cuenta: no guardar JWT, checkoutUrl ni datos bancarios.
  return {
    save(orderId: string) {return serialize(async () => {
      const value = JSON.stringify({orderId, createdAt: Date.now()});
      if (Platform.OS === 'web') sessionStorage.setItem(key, value); else await AsyncStorage.setItem(key, value);
    });},
    clear() {return serialize(async () => {if (Platform.OS === 'web') sessionStorage.removeItem(key); else await AsyncStorage.removeItem(key);});},
    async read(): Promise<string | null> {
      await writes;
      const value = Platform.OS === 'web' ? typeof window === 'undefined' ? null : sessionStorage.getItem(key) : await AsyncStorage.getItem(key);
      if (!value) return null;
      try {const entry = JSON.parse(value); return validOrderId(entry.orderId) && Number.isFinite(entry.createdAt) && entry.createdAt <= Date.now() && Date.now() - entry.createdAt < 24 * 60 * 60 * 1000 ? entry.orderId : null;} catch {return null;}
    },
  };
}
