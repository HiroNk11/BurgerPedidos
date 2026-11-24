import {useLocalSearchParams, useRouter} from 'expo-router';
import {useEffect} from 'react';
import {Platform, Text, View} from 'react-native';
import * as Linking from 'expo-linking';
import ScreenShell from '../components/ScreenShell';
import {Button, ui} from '../components/ui';
import {usePaymentFlow} from '../hooks/usePaymentFlow';
import {useAuth} from '../contexts/AuthContext';
import {paymentAppReturn, validOrderId} from '../utils/paymentLinks';
export default function PaymentReturnScreen() {
  const params = useLocalSearchParams<{orderId?: string; external_reference?: string}>();
  const raw = params.orderId ?? params.external_reference; const orderId = validOrderId(raw) ? raw : null;
  const {returned, state} = usePaymentFlow(); const auth = useAuth(); const router = useRouter();
  useEffect(() => {void returned(orderId);}, [returned, orderId]);
  return <ScreenShell><View style={ui.page}><Text style={ui.small}>VOLVISTE DE MERCADO PAGO</Text><Text style={ui.title}>Consultemos tu pago.</Text><Text style={ui.text}>El local está procesando la información de Mercado Pago. Esta pantalla no confirma un cobro: el estado actualizado aparece en Mis pedidos.</Text>{Platform.OS === 'web' && <Button label="Volver a la app instalada" onPress={() => {void Linking.openURL(paymentAppReturn(orderId)).catch(() => undefined);}}/>}<Button label={auth.user ? 'Ver mis pedidos' : 'Ingresar para consultar mi pedido'} onPress={() => router.replace(auth.user ? '/orders' : {pathname: '/login', params: {returnTo: '/orders'}})}/>{auth.user && <Button label="Consultar estado ahora" secondary busy={state.phase === 'checking'} onPress={() => {void returned(orderId);}}/>}</View></ScreenShell>;
}
