import {commerceUrl} from './apiConfig';
export class HttpError extends Error {
  constructor(message: string, public readonly status: number) { super(message); this.name = 'HttpError'; }
}
export type HttpOptions = RequestInit & {token?: string; timeoutMs?: number};
export async function http<T>(path: string, options: HttpOptions = {}): Promise<T> {
  const {token, timeoutMs = 20000, signal, ...init} = options;
  const controller = new AbortController();
  const cancel = () => controller.abort();
  if (signal?.aborted) controller.abort();
  signal?.addEventListener('abort', cancel, {once: true});
  const timer = setTimeout(cancel, timeoutMs);
  try {
    const headers = new Headers(init.headers);
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const response = await fetch(commerceUrl + path, {...init, headers, credentials: 'omit', signal: controller.signal});
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      const fallback = response.status === 401 ? 'Tu sesión venció. Volvé a ingresar con un código.' : response.status === 429 ? 'Hiciste varios intentos. Esperá unos minutos antes de volver a probar.' : `No se pudo completar la operación (${response.status}).`;
      throw new HttpError(problem?.title ?? fallback, response.status);
    }
    return response.status === 204 ? undefined as T : await response.json() as T;
  } catch (error) {
    if (error instanceof HttpError) throw error;
    if (controller.signal.aborted) throw new Error('La conexión demoró demasiado. Reintentá sin generar otra compra.');
    throw new Error('No pudimos conectar con el local. Revisá tu conexión e intentá de nuevo.');
  } finally {
    clearTimeout(timer); signal?.removeEventListener('abort', cancel);
  }
}
export const jsonBody = (value: unknown): Pick<RequestInit, 'body' | 'headers'> => ({body: JSON.stringify(value), headers: {'Content-Type': 'application/json'}});
