import {createContext, useCallback, useContext, useEffect, useRef, useState, type PropsWithChildren} from 'react';
import {customerApi} from '../services/customerApi';
import type {Customer, Session} from '../services/customerTypes';
import {HttpError} from '../services/http';
import {loadSession, saveSession} from '../services/sessionStorage';
type AuthState = {status: 'loading' | 'guest' | 'authenticated' | 'unavailable'; session: Session | null; user: Customer | null; notice: string};
type AuthValue = AuthState & {requestOtp: typeof customerApi.requestOtp; verifyOtp(email: string, code: string): Promise<void>; logout(): Promise<void>; refreshProfile(): Promise<void>; expire(token: string): Promise<void>; onSessionEnd(handler: (user: Customer) => Promise<void>): () => void};
const AuthContext = createContext<AuthValue | null>(null);
const guest: AuthState = {status: 'guest', session: null, user: null, notice: ''};
export function AuthProvider({children}: PropsWithChildren) {
  const [state, setState] = useState<AuthState>({...guest, status: 'loading'});
  const operation = useRef(0), currentSession = useRef<Session | null>(null), currentUser = useRef<Customer | null>(null);
  const cleanupHandlers = useRef(new Set<(user: Customer) => Promise<void>>());
  const onSessionEnd = useCallback((handler: (user: Customer) => Promise<void>) => {cleanupHandlers.current.add(handler); return () => {cleanupHandlers.current.delete(handler);};}, []);
  const endSession = useCallback(async (expectedToken?: string, notice = '') => {
    if (expectedToken && currentSession.current?.accessToken !== expectedToken) return;
    const generation = ++operation.current;
    if (currentUser.current) await Promise.all([...cleanupHandlers.current].map(handler => handler(currentUser.current!)));
    if (generation !== operation.current) return;
    await saveSession(null);
    if (generation !== operation.current) return;
    currentSession.current = null; currentUser.current = null;
    setState({...guest, notice});
  }, []);
  useEffect(() => {
    const generation = ++operation.current; let mounted = true;
    async function restore() {
      let session: Session | null = null;
      try {
        session = await loadSession();
        const user = session ? await customerApi.profile(session.accessToken) : null;
        if (mounted && generation === operation.current) {currentSession.current = session; currentUser.current = user; setState(session && user ? {status: 'authenticated', session, user, notice: ''} : guest);}
      } catch (error) {
        if (!mounted || generation !== operation.current) return;
        if (error instanceof HttpError && error.status === 401) { await saveSession(null); setState({...guest, notice: 'Tu sesión venció. Ingresá con un código nuevo.'}); }
        else {currentSession.current = session; setState({status: session ? 'unavailable' : 'guest', session, user: null, notice: error instanceof Error ? error.message : 'No se pudo recuperar la sesión.'});}
      }
    }
    void restore(); return () => {mounted = false;};
  }, []);
  const verifyOtp = useCallback(async (email: string, code: string) => {
    const generation = ++operation.current;
    const result = await customerApi.verifyOtp(email, code);
    if (generation !== operation.current) return;
    const session = {accessToken: result.token.accessToken, expiresAtUtc: result.token.expiresAtUtc};
    await saveSession(session);
    if (generation === operation.current) {currentSession.current = session; currentUser.current = result.cliente; setState({status: 'authenticated', session, user: result.cliente, notice: ''});}
  }, []);
  const logout = useCallback(() => endSession(), [endSession]);
  const expire = useCallback((token: string) => endSession(token, 'Tu sesión venció. Volvé a ingresar para ver tu cuenta.'), [endSession]);
  const refreshProfile = useCallback(async () => {
    if (!state.session) throw new Error('Ingresá a tu cuenta para continuar.');
    const token = state.session.accessToken; const generation = operation.current;
    try {
      const user = await customerApi.profile(token);
      if (generation === operation.current) {currentUser.current = user; setState(current => current.session?.accessToken === token ? {...current, user, status: 'authenticated', notice: ''} : current);}
    } catch (error) { if (error instanceof HttpError && error.status === 401) await expire(token); throw error; }
  }, [state.session, expire]);
  useEffect(() => {
    if (!state.session) return;
    const token = state.session.accessToken;
    const timer = setTimeout(() => {void expire(token).catch(() => undefined);}, Math.max(0, Date.parse(state.session.expiresAtUtc) - Date.now()));
    return () => clearTimeout(timer);
  }, [state.session, expire]);
  return <AuthContext.Provider value={{...state, requestOtp: customerApi.requestOtp, verifyOtp, logout, refreshProfile, expire, onSessionEnd}}>{children}</AuthContext.Provider>;
}
export function useAuth(): AuthValue {
  const value = useContext(AuthContext); if (!value) throw new Error('useAuth requiere AuthProvider.'); return value;
}
