import {tenantId} from './apiConfig';
import {http, jsonBody} from './http';
import type {Address, AddressInput, Customer, CustomerOrder, LoginResult, OrderHistory, ReorderProposal, OnlinePaymentCheckout} from './customerTypes';
export const customerApi = {
  requestOtp: (email: string) => http<{message: string; retryAfterSeconds: number; expiresInSeconds: number}>('/api/auth/request-otp', {method: 'POST', ...jsonBody({tenantId, email: email.trim()})}),
  verifyOtp: (email: string, codigo: string) => http<LoginResult>('/api/auth/verify-otp', {method: 'POST', ...jsonBody({tenantId, email: email.trim(), codigo})}),
  profile: (token: string) => http<Customer>('/api/cuenta', {token}),
  updateProfile: (token: string, nombre: string, telefono: string) => http<Customer>('/api/cuenta', {token, method: 'PUT', ...jsonBody({nombre, telefono})}),
  saveAddress: (token: string, value: AddressInput, id?: string) => http<Address>('/api/cuenta/direcciones' + (id ? '/' + encodeURIComponent(id) : ''), {token, method: id ? 'PUT' : 'POST', ...jsonBody(value)}),
  deleteAddress: (token: string, id: string) => http<void>('/api/cuenta/direcciones/' + encodeURIComponent(id), {token, method: 'DELETE'}),
  orders: (token: string, page = 1, signal?: AbortSignal) => http<OrderHistory>(`/api/pedidos/mis-pedidos?pagina=${page}&tamanio=10`, {token, signal}),
  order: (token: string, id: string, signal?: AbortSignal) => http<CustomerOrder>('/api/pedidos/mis-pedidos/' + encodeURIComponent(id), {token, signal}),
  iniciarPagoOnline: (token: string, pedidoId: string, signal?: AbortSignal) => http<OnlinePaymentCheckout>('/api/pedidos/' + encodeURIComponent(pedidoId) + '/iniciar-pago', {token, method: 'POST', signal, timeoutMs: 45000}),
  reorder: (token: string, id: string) => http<ReorderProposal>('/api/pedidos/reorder/' + encodeURIComponent(id), {token, method: 'POST'}),
};
