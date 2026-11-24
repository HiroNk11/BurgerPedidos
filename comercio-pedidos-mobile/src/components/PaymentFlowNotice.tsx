import {View} from 'react-native';
import {usePaymentFlow} from '../hooks/usePaymentFlow';
import {Button, Notice} from './ui';
export default function PaymentFlowNotice() {
  const payment = usePaymentFlow();
  if (!payment.state.error && !payment.state.message) return null;
  return <View style={{marginHorizontal: 20, marginBottom: 14, gap: 10}}><Notice message={payment.state.message}/><Notice error message={payment.state.error}/>{payment.state.phase === 'waiting' ? <Button label="Ya volví: consultar pago" secondary onPress={() => {void payment.returned();}}/> : payment.state.phase === 'checking' ? <Button label="Consultando al local…" busy onPress={() => undefined}/> : <Button label="Cerrar aviso de pago" secondary onPress={payment.clearNotice}/>}</View>;
}
