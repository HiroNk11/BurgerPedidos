import type {OrderHistory} from '../services/customerTypes';
export type OrderFeed = {scope: string; page: number; result: OrderHistory | null; refreshing: boolean; error: string; at: number | null};
export function beginOrdersRefresh(current: OrderFeed, scope: string, page: number): OrderFeed {
  return current.scope === scope && current.page === page
    ? {...current, refreshing: true, error: ''}
    : {scope, page, result: null, refreshing: true, error: '', at: null};
}
