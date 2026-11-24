import OnlinePaymentButton from './OnlinePaymentButton';
import {useRouter} from 'expo-router';
import {View, Text} from 'react-native';
import {useOrders} from '../contexts/OrdersContext';
import {orderLabels, paymentLabels} from '../utils/orderState';
import {Button, Notice, ui} from './ui';
export default function ActiveOrderTracker() {
  const orders = useOrders(); const router = useRouter(); const latest = orders.active[0];
  if (!latest) return null;
  return <View style={[ui.card, {marginHorizontal: 20, marginBottom: 14, padding: 16}]}><View style={ui.row}><View style={{gap: 5}}><Text style={ui.small}>TU PEDIDO EN MARCHA</Text><Text style={[ui.subtitle, {fontSize: 15}]}>{latest.shortCode} · {orderLabels[latest.estadoPedido]}</Text></View><Text style={ui.pill}>{orders.totalActive} en curso</Text></View>{latest.estimadoEntregaUtc && <Text style={ui.muted}>Entrega estimada: {new Date(latest.estimadoEntregaUtc).toLocaleTimeString('es-AR', {hour: '2-digit', minute: '2-digit'})}</Text>}<View style={ui.row}><Button style={{paddingVertical: 10, paddingHorizontal: 12, minHeight: 44}} label="Ver seguimiento" onPress={() => router.navigate({pathname: '/orders', params: {tab: 'active', orderId: latest.id}})}/><Button style={{paddingVertical: 10, paddingHorizontal: 12, minHeight: 44}} label={orders.refreshing ? 'Actualizando…' : 'Actualizar estado'} secondary busy={orders.refreshing} onPress={() => {void orders.refresh();}}/></View><Text style={ui.muted}>{paymentLabels[latest.estadoPago]}</Text><OnlinePaymentButton order={latest}/><Notice message={orders.error} error/></View>;
}
