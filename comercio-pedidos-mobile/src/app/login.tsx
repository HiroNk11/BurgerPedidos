import {useLocalSearchParams, useRouter} from 'expo-router';
import {useEffect, useRef, useState} from 'react';
import {Text, View} from 'react-native';
import {useAuth} from '../contexts/AuthContext';
import ScreenShell from '../components/ScreenShell';
import {Button, Field, Notice, ui} from '../components/ui';
export default function LoginScreen() {
  const auth = useAuth(); const router = useRouter(); const params = useLocalSearchParams<{returnTo?: string}>();
  const [email, setEmail] = useState(''), [code, setCode] = useState(''), [step, setStep] = useState<'email' | 'code'>('email'), [busy, setBusy] = useState(false), [error, setError] = useState(''), [cooldown, setCooldown] = useState(0);
  const inFlight = useRef(false);
  useEffect(() => {const timer = setInterval(() => setCooldown(value => Math.max(0, value - 1)), 1000); return () => clearInterval(timer);}, []);
  const destination = params.returnTo === '/orders' ? '/orders' : params.returnTo === '/checkout' ? '/checkout' : '/profile';
  async function request() {
    if (inFlight.current || cooldown > 0) return;
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) {setError('Ingresá un email válido.'); return;}
    inFlight.current = true; setBusy(true); setError('');
    try {const result = await auth.requestOtp(email); setStep('code'); setCode(''); setCooldown(result.retryAfterSeconds || 60);}
    catch (error) {setError(error instanceof Error ? error.message : 'No se pudo solicitar el código.');}
    finally {inFlight.current = false; setBusy(false);}
  }
  async function verify(value: string) {
    if (inFlight.current || !/^[0-9]{6}$/.test(value)) return;
    inFlight.current = true; setBusy(true); setError('');
    try {await auth.verifyOtp(email, value); router.replace(destination);}
    catch (error) {setError(error instanceof Error ? error.message : 'No se pudo verificar el código.');}
    finally {inFlight.current = false; setBusy(false);}
  }
  if (auth.status === 'loading') return <ScreenShell><Text style={ui.text}>Recuperando tu sesión…</Text></ScreenShell>;
  return <ScreenShell><View style={ui.page}><Text style={ui.small}>BIENVENIDO AL CLUB</Text><Text style={ui.title}>{step === 'email' ? 'Tu próxima burger,\na un código.' : 'Revisá tu correo.'}</Text><Text style={ui.muted}>Guardá tus datos, seguí tus pedidos y volvé a pedir lo que te gustó.</Text>{auth.user ? <View style={ui.card}><Text style={ui.text}>Ya ingresaste como {auth.user.email}.</Text><Button label="Ir a mi cuenta" onPress={() => router.replace('/profile')}/></View> : <View style={ui.card}>{step === 'email' ? <><Field label="Email de tu cuenta" autoComplete="email" keyboardType="email-address" autoCapitalize="none" maxLength={254} placeholder="tu@email.com" value={email} onChangeText={setEmail} editable={!busy}/><Button label="Recibir código" busy={busy} onPress={() => {void request();}}/></> : <><Text style={ui.text}>Enviamos un código a {email.trim()}. Vence en 10 minutos.</Text><Field label="Código de 6 dígitos" autoFocus value={code} editable={!busy} onChangeText={value => {const digits = value.replace(/\D/g, '').slice(0, 6); setCode(digits); if (digits.length === 6) void verify(digits);}} keyboardType="number-pad" inputMode="numeric" textContentType="oneTimeCode" autoComplete="one-time-code" maxLength={6} placeholder="000000" style={{fontSize: 30, letterSpacing: 9, textAlign: 'center'}}/><Button label="Ingresar" busy={busy} disabled={code.length !== 6} onPress={() => {void verify(code);}}/><Button label={cooldown ? `Reenviar en ${cooldown} s` : 'Reenviar código'} secondary disabled={busy || cooldown > 0} onPress={() => {void request();}}/><Button label="Cambiar email" secondary disabled={busy} onPress={() => {setStep('email'); setCode(''); setError('');}}/></>}{process.env.EXPO_PUBLIC_EMAIL_MODE === 'local' && <Notice message="Durante esta prueba, consultá el código en el Buzón local del panel del negocio."/>}<Notice message={error || auth.notice} error/></View>}<Button label={auth.user ? 'Volver al menú' : 'Seguir como invitado'} secondary disabled={busy} onPress={() => router.replace('/')}/></View></ScreenShell>;
}
