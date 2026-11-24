import type {CatalogProduct} from '../services/checkoutService';
import type {ReorderProposal} from '../services/customerTypes';
import type {CartLine} from '../state/cartReducer';
export function prepareReorder(proposal: ReorderProposal, catalog: CatalogProduct[]): {items: CartLine[]; messages: string[]} {
  const messages = new Set(proposal.warnings.map(w => w.message));
  proposal.itemsRemovidos.forEach(item => messages.add(`${item.nombreHistorico}: ${item.motivo}`));
  const items: CartLine[] = [];
  for (const line of proposal.items) {
    const product = catalog.find(p => p.id === line.productId);
    if (!product || product.stock < 1) {messages.add(`${line.nombre}: ya no está disponible.`); continue;}
    const cantidad = Math.min(line.cantidad, product.stock);
    if (cantidad < line.cantidad) messages.add(`${line.nombre}: la disponibilidad cambió; quedan ${cantidad} unidades.`);
    if (product.precio !== line.precioUnitarioActual) messages.add(`${line.nombre}: el precio se actualizó otra vez.`);
    if (cantidad > 0) items.push({...product, cantidad});
  }
  return {items, messages: [...messages]};
}
