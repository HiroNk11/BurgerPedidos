import type {CatalogProduct} from './checkoutService';
import {http} from './http';
export type CustomerProduct = CatalogProduct & {activo: boolean};
export type RankedProduct = {producto: CustomerProduct; cantidadPedida: number};
export const engagementApi = {
  favorites: (token: string) => http<CustomerProduct[]>('/api/usuarios/favoritos', {token}),
  ranking: (token: string) => http<RankedProduct[]>('/api/usuarios/mas-pedidos', {token}),
  toggle: (token: string, id: string) => http<{productoId: string; esFavorito: boolean}>('/api/usuarios/favoritos/' + encodeURIComponent(id), {token, method: 'POST'}),
};
