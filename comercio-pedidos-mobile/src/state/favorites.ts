import type {CustomerProduct, RankedProduct} from '../services/engagementApi';
export type FavoritesState = {scope: string; favorites: CustomerProduct[]; ranking: RankedProduct[]; pending: string[]; error: string; ready: boolean};
export const emptyFavorites: FavoritesState = {scope: '', favorites: [], ranking: [], pending: [], error: '', ready: false};
export type FavoritesAction =
  | {type: 'reset'; scope: string}
  | {type: 'hydrate'; scope: string; favorites: CustomerProduct[]; ranking: RankedProduct[]}
  | {type: 'toggle'; scope: string; product: CustomerProduct}
  | {type: 'settle'; scope: string; product: CustomerProduct; favorite: boolean; error?: string}
  | {type: 'error'; scope: string; error: string};
function membership(items: CustomerProduct[], product: CustomerProduct, favorite: boolean): CustomerProduct[] {
  const rest = items.filter(p => p.id !== product.id);
  return favorite ? [product, ...rest] : rest;
}
export function favoritesReducer(state: FavoritesState, action: FavoritesAction): FavoritesState {
  if (action.type === 'reset') return {...emptyFavorites, scope: action.scope};
  if (action.scope !== state.scope) return state;
  if (action.type === 'hydrate') {
    if (state.pending.length) return state;
    return {...state, favorites: action.favorites, ranking: action.ranking, error: '', ready: true};
  }
  if (action.type === 'error') return {...state, error: action.error, ready: true};
  if (action.type === 'toggle') {
    if (state.pending.includes(action.product.id)) return state;
    return {...state, favorites: membership(state.favorites, action.product, !state.favorites.some(p => p.id === action.product.id)), pending: [...state.pending, action.product.id], error: ''};
  }
  return {...state, favorites: membership(state.favorites, action.product, action.favorite), pending: state.pending.filter(id => id !== action.product.id), error: action.error ?? ''};
}
