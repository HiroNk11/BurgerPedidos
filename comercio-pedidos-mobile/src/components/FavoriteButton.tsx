import {Pressable, StyleSheet, Text} from 'react-native';
import {useRouter} from 'expo-router';
import {useAuth} from '../contexts/AuthContext';
import {useFavorites} from '../contexts/EngagementContext';
import type {CustomerProduct} from '../services/engagementApi';
export default function FavoriteButton({product}: {product: CustomerProduct}) {
  const auth = useAuth(); const favorites = useFavorites(); const router = useRouter();
  const selected = favorites.favorites.some(p => p.id === product.id); const busy = favorites.pending.includes(product.id) || !!auth.user && !favorites.ready;
  return <Pressable accessibilityRole="button" accessibilityLabel={`${selected ? 'Quitar de' : 'Guardar en'} favoritas ${product.nombre}`} accessibilityState={{selected, disabled: busy}} disabled={busy || auth.status === 'loading'} hitSlop={6} style={[s.button, selected && s.selected]} onPress={() => {if (!auth.user) router.navigate('/login'); else void favorites.toggle(product).catch(() => undefined);}}><Text style={[s.heart, selected && {color: '#ff8a3d'}]}>{selected ? '♥' : '♡'}</Text></Pressable>;
}
const s = StyleSheet.create({button: {width: 46, height: 46, borderRadius: 23, backgroundColor: '#242620', borderWidth: 1, borderColor: '#45483d', alignItems: 'center', justifyContent: 'center'}, selected: {borderColor: '#ff8a3d'}, heart: {fontSize: 29, lineHeight: 34, color: '#f6f2eb'}});
