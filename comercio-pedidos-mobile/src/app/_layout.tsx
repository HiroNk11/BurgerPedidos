import {PaymentFlowProvider} from '../contexts/PaymentFlowContext';
import {EngagementProvider} from '../contexts/EngagementContext';
import {Stack} from 'expo-router';
import {StatusBar} from 'expo-status-bar';
import {SafeAreaProvider} from 'react-native-safe-area-context';
import {AuthProvider} from '../contexts/AuthContext';
import {CartProvider} from '../contexts/CartContext';
import {OrdersProvider} from '../contexts/OrdersContext';
import {CheckoutProvider} from '../contexts/CheckoutContext';
export default function Layout() {
  return <SafeAreaProvider><AuthProvider><CartProvider><OrdersProvider><CheckoutProvider><EngagementProvider><PaymentFlowProvider><StatusBar style="light"/><Stack screenOptions={{headerShown: false}}/></PaymentFlowProvider></EngagementProvider></CheckoutProvider></OrdersProvider></CartProvider></AuthProvider></SafeAreaProvider>;
}
