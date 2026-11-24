export type Address = {id: string; calle: string; numero: string; pisoDepto: string | null; latitud: number | null; longitud: number | null};
export type AddressInput = Omit<Address, 'id'>;
export type Customer = {id: string; email: string; nombre: string; telefono: string; direcciones: Address[]};
export type Session = {accessToken: string; expiresAtUtc: string};
export type LoginResult = {token: Session & {tokenType: string}; cliente: Customer; nuevoUsuario: boolean; pedidosVinculados: number};
export type OrderState = 'PendienteConfirmacion' | 'Aceptado' | 'EnPreparacion' | 'EnCamino' | 'Entregado' | 'Cancelado';
export type PaymentState = 'Pendiente' | 'ComprobanteEnviado' | 'Pagado' | 'Rechazado';
export type OrderItem = {productId: string; nombre: string; cantidad: number; precioUnitarioHistorico: number; descuentoAplicado: number; subtotal: number};
export type CustomerOrder = {id: string; shortCode: string; fecha: string; estadoPedido: OrderState; estadoPago: PaymentState; total: number; moneda: string; tipoEntrega: 'Retiro' | 'Delivery'; direccionEntrega: string | null; notas: string | null; items: OrderItem[]; metodoPago?: 'Efectivo' | 'TransferenciaCBU' | 'PasarelaOnline' | null; estimadoEntregaUtc?: string | null};
export type OrderHistory = {activos: CustomerOrder[]; totalActivos: number; historicos: CustomerOrder[]; totalHistoricos: number; pagina: number; tamanio: number};
export type ReorderProposal = {pedidoOrigenId: string; items: {productId: string; nombre: string; cantidad: number; precioUnitarioActual: number; subtotal: number; precioUnitarioHistorico: number}[]; warnings: {code: string; productId: string | null; message: string}[]; itemsRemovidos: {productId: string; nombreHistorico: string; cantidad: number; motivo: string}[]; subtotalProductos: number; moneda: string; requiereCotizacion: boolean};

export type OnlinePaymentCheckout = {proveedor: string; transaccionId: string | null; preferenceId: string | null; checkoutUrl: string | null};
