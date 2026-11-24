import {View, Text} from 'react-native';
import {useAuth} from '../contexts/AuthContext';
import {usePaymentFlow} from '../hooks/usePaymentFlow';
import type {CustomerOrder} from '../services/customerTypes';
import {canPayOnline} from '../utils/paymentLinks';
import {Button, ui} from './ui';
export default function OnlinePaymentButton({order}: {order: CustomerOrder}) {
  const auth = useAuth(); const payment = usePaymentFlow();
  if (!auth.user || !canPayOnline(order)) return null;
  const loading = payment.state.orderId === order.id && ['requesting', 'opening'].includes(payment.state.phase);
  return <View style={{gap: 8}}><Button label={loading ? 'Preparando pago…' : 'Pagar con Mercado Pago'} busy={loading} disabled={payment.busy || auth.status !== 'authenticated'} onPress={() => {void payment.pay(order);}}/><Text style={ui.muted}>El pago se confirma cuando lo informa Mercado Pago al local.</Text></View>;
}
