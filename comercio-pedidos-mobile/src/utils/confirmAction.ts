import {Alert, Platform} from 'react-native';
export async function confirmAction(title: string, message: string, destructive = false): Promise<boolean> {
  if (Platform.OS === 'web') return window.confirm(title + '\n\n' + message);
  return new Promise(resolve => Alert.alert(title, message, [{text: 'Cancelar', style: 'cancel', onPress: () => resolve(false)}, {text: destructive ? 'Eliminar' : 'Continuar', style: destructive ? 'destructive' : 'default', onPress: () => resolve(true)}], {cancelable: true, onDismiss: () => resolve(false)}));
}
