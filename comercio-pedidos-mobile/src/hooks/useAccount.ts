import {useCallback, useRef, useState} from 'react';
import {useAuth} from '../contexts/AuthContext';
import {customerApi} from '../services/customerApi';
import type {AddressInput} from '../services/customerTypes';
import {HttpError} from '../services/http';
export function useAccount() {
  const {session, user, refreshProfile, expire} = useAuth(); const [busy, setBusy] = useState(false); const inFlight = useRef(false);
  const run = useCallback(async (action: (token: string) => Promise<unknown>) => {
    if (!session || !user) throw new Error('Ingresá a tu cuenta para continuar.');
    if (inFlight.current) throw new Error('Ya estamos guardando los cambios.');
    inFlight.current = true; setBusy(true); const token = session.accessToken;
    try {await action(token); await refreshProfile();}
    catch (error) {if (error instanceof HttpError && error.status === 401) await expire(token); throw error;}
    finally {inFlight.current = false; setBusy(false);}
  }, [session, user, refreshProfile, expire]);
  return {busy, saveProfile: (nombre: string, telefono: string) => run(token => customerApi.updateProfile(token, nombre.trim(), telefono.replace(/[^+0-9]/g, ''))), saveAddress: (input: AddressInput, id?: string) => run(token => customerApi.saveAddress(token, input, id)), deleteAddress: (id: string) => run(token => customerApi.deleteAddress(token, id))};
}
