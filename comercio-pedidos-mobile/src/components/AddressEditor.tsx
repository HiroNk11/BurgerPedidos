import {useState} from 'react';
import {Modal, ScrollView, Text} from 'react-native';
import {SafeAreaView} from 'react-native-safe-area-context';
import type {Address, AddressInput} from '../services/customerTypes';
import {Button, colors, Field, Notice, ui} from './ui';
export default function AddressEditor({address, busy, onSave, onClose}: {address?: Address; busy: boolean; onSave(value: AddressInput, id?: string): Promise<void>; onClose(): void}) {
  const [calle, setCalle] = useState(address?.calle ?? ''), [numero, setNumero] = useState(address?.numero ?? ''), [piso, setPiso] = useState(address?.pisoDepto ?? ''), [lat, setLat] = useState(address?.latitud?.toString() ?? ''), [lng, setLng] = useState(address?.longitud?.toString() ?? ''), [error, setError] = useState('');
  async function save() {
    setError('');
    try {
      if (!calle.trim() || !numero.trim()) throw new Error('Completá calle y número.');
      if (!!lat.trim() !== !!lng.trim()) throw new Error('Completá ambas coordenadas o dejalas vacías.');
      const latitud = lat.trim() ? Number(lat.replace(',', '.')) : null, longitud = lng.trim() ? Number(lng.replace(',', '.')) : null;
      if (latitud !== null && (!Number.isFinite(latitud) || latitud < -90 || latitud > 90) || longitud !== null && (!Number.isFinite(longitud) || longitud < -180 || longitud > 180)) throw new Error('Revisá las coordenadas de la dirección.');
      await onSave({calle: calle.trim(), numero: numero.trim(), pisoDepto: piso.trim() || null, latitud, longitud}, address?.id); onClose();
    } catch (error) {setError(error instanceof Error ? error.message : 'No se pudo guardar la dirección.');}
  }
  return <Modal visible animationType="slide" onRequestClose={() => {if (!busy) onClose();}}><SafeAreaView style={{flex: 1, backgroundColor: colors.background}}><ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={[ui.page, {padding: 24, maxWidth: 640, width: '100%', alignSelf: 'center'}]}><Text style={ui.title}>{address ? 'Editar dirección' : 'Nueva dirección'}</Text><Field label="Calle" value={calle} onChangeText={setCalle} maxLength={180} editable={!busy}/><Field label="Número" value={numero} onChangeText={setNumero} maxLength={20} editable={!busy}/><Field label="Piso / departamento" value={piso} onChangeText={setPiso} maxLength={60} editable={!busy}/><Text style={ui.muted}>La ubicación es opcional. Podés completar las coordenadas del punto de entrega.</Text><Field label="Latitud (opcional)" value={lat} onChangeText={setLat} keyboardType="numbers-and-punctuation" editable={!busy}/><Field label="Longitud (opcional)" value={lng} onChangeText={setLng} keyboardType="numbers-and-punctuation" editable={!busy}/><Notice message={error} error/><Button label="Guardar dirección" busy={busy} onPress={() => {void save();}}/><Button label="Cancelar" secondary disabled={busy} onPress={onClose}/></ScrollView></SafeAreaView></Modal>;
}
