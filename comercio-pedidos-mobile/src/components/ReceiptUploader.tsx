import {useRef, useState} from 'react';
import * as ImagePicker from 'expo-image-picker';
import {Image} from 'expo-image';
import {Text, View} from 'react-native';
import {uploadOrderReceipt, type Journal} from '../services/checkoutService';
import {useOrders} from '../contexts/OrdersContext';
import {useAuth} from '../contexts/AuthContext';
import type {CustomerOrder} from '../services/customerTypes';
import {HttpError} from '../services/http';
import {Button, Notice, ui} from './ui';
export default function ReceiptUploader({journal, order}: {journal?: Journal; order: CustomerOrder}) {
  const orders = useOrders(); const auth = useAuth(); const [asset, setAsset] = useState<ImagePicker.ImagePickerAsset | null>(null), [busy, setBusy] = useState(false), [error, setError] = useState(''), [message, setMessage] = useState(''); const inFlight = useRef(false);
  if ((order.metodoPago ?? journal?.body.pedido.metodoPago) !== 'TransferenciaCBU' || order.estadoPago === 'Pagado' || order.estadoPedido === 'Cancelado') return null;
  async function pick() {setError(''); try {const result = await ImagePicker.launchImageLibraryAsync({mediaTypes: ['images'], quality: 0.8, allowsEditing: false}); if (!result.canceled) {const image = result.assets[0]; if (image.fileSize && image.fileSize > 5 * 1024 * 1024) throw new Error('Elegí una imagen de hasta 5 MB.'); setAsset(image);}} catch (error) {setError(error instanceof Error ? error.message : 'No se pudo elegir la imagen.');}}
  async function upload() {
    if (!asset || inFlight.current) return;
    inFlight.current = true; setBusy(true); setError('');
    try {
      const credentials = journal?.body.pedido.checkoutToken ? {checkoutToken: journal.body.pedido.checkoutToken} : {accessToken: auth.session?.accessToken};
      const result = await uploadOrderReceipt(order.id, asset, credentials);
      if (journal?.result) await orders.registerOrder({...journal, result: {...journal.result, estadoPago: result.estadoPago}});
      await orders.refresh(); setMessage('Comprobante recibido. El local verificará el ingreso antes de confirmar el pago.');
    } catch (error) {if (error instanceof HttpError && error.status === 401 && auth.session) await auth.expire(auth.session.accessToken); setError(error instanceof Error ? error.message : 'No se pudo cargar el comprobante.');}
    finally {inFlight.current = false; setBusy(false);}
  }
  return <View style={ui.card}><Text style={ui.subtitle}>Comprobante de transferencia</Text><Text style={ui.muted}>Imagen PNG, JPG o WebP de hasta 5 MB.</Text>{asset && <Image source={{uri: asset.uri}} contentFit="contain" accessibilityLabel="Comprobante seleccionado" style={{width: '100%', height: 180, backgroundColor: '#fff', borderRadius: 12}}/>}<Button label={asset ? 'Cambiar comprobante' : 'Elegir comprobante'} secondary disabled={busy} onPress={() => {void pick();}}/><Button label={busy ? 'Subiendo…' : 'Subir comprobante'} busy={busy} disabled={!asset} onPress={() => {void upload();}}/><Notice message={message}/><Notice message={error} error/></View>;
}
