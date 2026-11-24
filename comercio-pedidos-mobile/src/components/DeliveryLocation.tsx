import * as Location from 'expo-location';
import { useEffect, useRef, useState } from 'react';
import { Linking, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
export type DeliveryPoint = {latitude: number; longitude: number; accuracy: number | null};
export const mapLink = (point: DeliveryPoint) => `https://www.google.com/maps/search/?api=1&query=${point.latitude},${point.longitude}`;
export const addressMapLink = (address: string) => `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address.trim())}`;
type Props = {address: string; point: DeliveryPoint | null; onLocate: (point: DeliveryPoint) => void; onAddress: (address: string) => void; onClear: () => void};
export default function DeliveryLocation({address, point, onLocate, onAddress, onClear}: Props) {
 const [busy, setBusy] = useState(false);
 const [message, setMessage] = useState('');
 const active = useRef(true);
 useEffect(() => {active.current = true; return () => {active.current = false;};}, []);
 async function locate() {
  if (busy) return;
  setBusy(true); setMessage('');
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
   const permission = await Location.requestForegroundPermissionsAsync();
   if (!active.current) return;
   if (permission.status !== 'granted') {setMessage('No se habilitó la ubicación. Podés escribir la dirección manualmente o habilitar el permiso en los ajustes del teléfono.'); return;}
   const position = await Promise.race([Location.getCurrentPositionAsync({accuracy: Location.Accuracy.High}), new Promise<never>((_, reject) => {timer = setTimeout(() => reject(new Error('timeout')), 18000);})]);
   if (timer) clearTimeout(timer);
   if (!active.current) return;
   const next = {latitude: position.coords.latitude, longitude: position.coords.longitude, accuracy: position.coords.accuracy};
   onLocate(next);
   setMessage('Ubicación agregada. Revisá la dirección y completá número, piso y departamento.');
   if (Platform.OS !== 'web') {
    try {
     const addresses = await Promise.race([Location.reverseGeocodeAsync(next), new Promise<never>((_, reject) => {timer = setTimeout(() => reject(new Error('timeout')), 5000);})]);
     if (!active.current) return;
     const a = addresses[0];
     const street = a && [a.street, a.streetNumber].filter(Boolean).join(' ');
     if (a && street) onAddress([street, a.city ?? a.district, a.region].filter(Boolean).join(', '));
    } catch { /* El punto y la dirección manual siguen disponibles. */ }
   }
  } catch {if (active.current) setMessage('No pudimos obtener tu ubicación. Activá el GPS y reintentá, o escribí la dirección manualmente.');}
  finally {if (timer) clearTimeout(timer); if (active.current) setBusy(false);}
 }
 async function openMap() {try {await Linking.openURL(point ? mapLink(point) : addressMapLink(address));} catch {setMessage('No se pudo abrir el mapa.');}}
 return <View style={s.box}>
  <Pressable disabled={busy} accessibilityRole="button" accessibilityState={{disabled:busy}} onPress={() => {void locate();}} style={[s.button,busy && {opacity:.6}]}><Text style={s.buttonText}>{busy ? 'Buscando ubicación…' : '⌖ Usar mi ubicación actual'}</Text></Pressable>
  <Text style={s.note}>Al usar esta opción, el punto se incluirá en el pedido de WhatsApp. Confirmá que sea el lugar de entrega.</Text>
  {(point || !!address.trim()) && <Pressable accessibilityRole="button" onPress={() => {void openMap();}}><Text style={s.link}>{point ? 'Ver punto en el mapa ↗' : 'Buscar dirección en el mapa ↗'}</Text></Pressable>}
  {point && <><Text style={s.text}>Punto agregado{point.accuracy !== null ? ` · precisión aproximada ${Math.round(point.accuracy)} m` : ''}</Text><Pressable accessibilityRole="button" onPress={() => {onClear(); setMessage('');}}><Text style={s.link}>Quitar ubicación</Text></Pressable></>}
  {!!message && <Text accessibilityLiveRegion="polite" style={s.text}>{message}</Text>}
 </View>;
}
const s = StyleSheet.create({box:{gap:12},button:{backgroundColor:'#303929',borderRadius:14,padding:17,alignItems:'center'},buttonText:{color:'#e9efd8',fontWeight:'700'},note:{color:'#93988b',fontSize:12,lineHeight:18},text:{color:'#e9efd8',fontSize:13,lineHeight:20},link:{color:'#ffac72',fontWeight:'600',paddingVertical:8}});
