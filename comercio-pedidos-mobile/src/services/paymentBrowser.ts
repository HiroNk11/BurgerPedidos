import * as WebBrowser from 'expo-web-browser';
import * as Linking from 'expo-linking';
import {Platform} from 'react-native';
import {safeCheckoutUrl} from '../utils/paymentLinks';
export async function openPaymentBrowser(value: string): Promise<'opened' | 'closed'> {
  const url = safeCheckoutUrl(value);
  try {
    // En web, la misma pestaña evita popups bloqueados después del await HTTP.
    const result = await WebBrowser.openBrowserAsync(url, Platform.OS === 'web' ? {windowName: '_self'} : {toolbarColor: '#101110', controlsColor: '#ff8a3d', dismissButtonStyle: 'close'});
    return result.type === 'cancel' || result.type === 'dismiss' ? 'closed' : 'opened';
  } catch (error) {
    if (typeof error === 'object' && error !== null && 'code' in error && error.code === 'ERR_UNAVAILABLE') {await Linking.openURL(url); return 'opened';}
    throw new Error('No se pudo abrir Mercado Pago. Tu pedido sigue guardado; podés reintentar.');
  }
}
export async function dismissPaymentBrowser(): Promise<void> {
  // SDK 58: dismissBrowser solo existe en iOS. Android vuelve desde Custom Tabs mediante el intent.
  if (Platform.OS === 'ios') await WebBrowser.dismissBrowser().catch(() => undefined);
}
