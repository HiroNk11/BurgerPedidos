import type {CatalogProduct} from '../services/checkoutService';
export type CartLine = CatalogProduct & {cantidad: number};
export type CartState = {items: CartLine[]; locked: boolean; revision: number};
export type CartAction = {type: 'quantity'; product: CatalogProduct; quantity: number} | {type: 'replace'; items: CartLine[]; expectedRevision?: number} | {type: 'clear'} | {type: 'lock'; value: boolean};
export const initialCart: CartState = {items: [], locked: true, revision: 0};
export function cartReducer(state: CartState, action: CartAction): CartState {
  if (action.type === 'lock') return {...state, locked: action.value};
  if (action.type === 'clear') return {...state, items: [], revision: state.revision + 1};
  if (state.locked) return state;
  if (action.type === 'replace') {
    if (action.expectedRevision !== undefined && state.revision !== action.expectedRevision) return state;
    return {...state, items: action.items.filter(item => Number.isInteger(item.cantidad) && item.cantidad > 0), revision: state.revision + 1};
  }
  const quantity = Math.max(0, Math.min(1000, action.product.stock, Math.trunc(action.quantity)));
  const items = state.items.filter(item => item.id !== action.product.id);
  if (quantity) items.push({...action.product, cantidad: quantity});
  return {...state, items, revision: state.revision + 1};
}
