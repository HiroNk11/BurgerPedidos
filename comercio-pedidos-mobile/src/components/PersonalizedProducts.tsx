import {ScrollView, StyleSheet, Text, View} from 'react-native';
import {Image} from 'expo-image';
import {useAuth} from '../contexts/AuthContext';
import {useCart} from '../contexts/CartContext';
import {useFavorites} from '../contexts/EngagementContext';
import {useCheckout} from '../contexts/CheckoutContext';
import {menu, moneda} from '../constants/menu';
import type {CustomerProduct} from '../services/engagementApi';
import FavoriteButton from './FavoriteButton';
import {Button, Notice, ui} from './ui';
export default function PersonalizedProducts() {
  const auth = useAuth(); const favorites = useFavorites(); const cart = useCart(); const checkout = useCheckout();
  if (!auth.user) return null;
  function carousel(title: string, products: {product: CustomerProduct; count?: number}[]) {
    if (!products.length) return null;
    return <View style={{gap: 14}}><Text style={ui.subtitle}>{title}</Text><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{gap: 14}}>{products.map(({product: p, count}) => {
      const image = menu.find(item => item.nombre === p.nombre)?.imagen;
      const current = cart.store?.products.find(item => item.id === p.id); const available = !!current && current.stock > 0 && p.activo;
      return <View key={p.id} style={s.card}>{image ? <Image source={image} style={s.image} contentFit={p.categoria === 'Bebidas' ? 'contain' : 'cover'}/> : <View style={[s.image, {alignItems: 'center', justifyContent: 'center'}]}><Text style={{fontSize: 45}}>🍔</Text></View>}<View style={s.content}><View style={ui.row}><Text numberOfLines={2} style={[ui.subtitle, {flex: 1}]}>{p.nombre}</Text><FavoriteButton product={p}/></View>{count !== undefined && <Text style={ui.muted}>{count} {count === 1 ? 'unidad pedida' : 'unidades pedidas'}</Text>}<Text style={ui.text}>{moneda(current?.precio ?? p.precio)}</Text><Button label={available ? `Sumar ${p.nombre}` : 'No disponible'} disabled={!available || cart.locked || !checkout.ready} onPress={() => {if (current) cart.setQuantity(current, (cart.items.find(item => item.id === p.id)?.cantidad ?? 0) + 1);}}/></View></View>;
    })}</ScrollView></View>;
  }
  return <View style={{gap: 24}}>{!!favorites.error && <View style={{gap: 10}}><Notice error message={favorites.error}/><Button label="Actualizar favoritas" secondary onPress={() => {void favorites.refresh();}}/></View>}{carousel('Tus Favoritas', favorites.favorites.map(product => ({product})))}{carousel('Lo que más pedís', favorites.ranking.map(item => ({product: item.producto, count: item.cantidadPedida})))}</View>;
}
const s = StyleSheet.create({card: {width: 252, backgroundColor: '#1d1f1c', borderRadius: 22, borderWidth: 1, borderColor: '#36392f', overflow: 'hidden'}, image: {width: '100%', height: 145, backgroundColor: '#302a22'}, content: {padding: 15, gap: 12}});
