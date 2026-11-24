# Fase 3 · Promociones, favoritos y conciliación de pagos

Implementado sobre .NET 10 / EF Core 10 / SQL Server, conservando los contratos previos de pedidos y cuentas. Frontend: ../comercio-pedidos-mobile/FASE3-FRONTEND.md.

## Modelo y esquema

- `UsuarioFavorito`: entidad de unión entre Usuario y Product, con navegaciones y fecha. Índice único (TenantId, UsuarioId, ProductoId); FKs compuestas impiden cruzar comercios.
- `Promotion.MaxUsesPerUser`: límite opcional por cuenta. Se conservan CouponCode, TipoDescuento, Valor, DesdeUtc/HastaUtc, días/horarios y MaxUses/CurrentUses. Corresponden a Código, ValorDescuento, FechaInicio/Fin y LímiteUsosTotal de la especificación. No se crea una segunda tabla de cupones que duplique el motor existente.
- `PaymentLog`: evento autenticado, identificador externo, importe/moneda, resultado, hash SHA-256 del cuerpo y fecha. Índice único (Proveedor, EventoId). Inmutable tanto en EF como mediante trigger SQL. Nunca almacena firmas, secretos, tarjetas o payload completo.
- Cada entidad tiene filtro de tenant y FKs compuestas. Decimales en decimal(18,2), rowversion y borrados restringidos.
- `sql/schema.sql` y `001_schema.sql`: DDL generado por EF para bases nuevas. Ejecutar después `002_history_guards.sql` para los triggers.
- `sql/005_engagement_payments.sql`: actualización aditiva e idempotente para bases existentes después de 003/004. Conserva datos. La conversión de collation de IDs externos elimina y restablece sus restricciones dentro de una transacción con XACT_ABORT. Probar en staging y respaldar antes de aplicarla en producción.
- `Gastronomia.Dev --upgrade .` incluye 005 y solo acepta la base local BurgerClubPanelDev_. Se aplicó y se repitió correctamente en la base de demostración.

## Endpoints

| Método y ruta | Autenticación / comportamiento |
|---|---|
| POST /api/usuarios/favoritos/{productoId} | JWT cliente; toggle serializable; devuelve productoId/esFavorito |
| GET /api/usuarios/favoritos | JWT cliente; productos favoritos con precios actuales y disponibilidad |
| GET /api/usuarios/mas-pedidos | JWT cliente; top 10 por SUM(Cantidad) como bigint; excluye Cancelado; desempate estable por ProductId |
| POST /api/pedidos/calcular-totales | TenantId, Items, CodigoCupon opcional y TipoEntrega; JWT opcional explícito; bearer inválido no se degrada a invitado |
| POST /api/pedidos/cotizar | Contrato anterior conservado; ahora admite JWT para cupones personales |
| POST /api/pedidos | Recalcula precio, cupón, cuotas y stock; no confía en importes enviados por el cliente |
| POST /api/pedidos/{id}/iniciar-pago | JWT cliente y pedido propio online; asocia transacción desde IPaymentGateway, nunca desde un ID enviado por el cliente |
| POST /api/webhooks/pagos | Sin JWT; requiere autenticación del proveedor mediante IPaymentGateway |

Ejemplo de cotización:

```json
{"tenantId":"GUID-DEL-LOCAL","items":[{"productId":"GUID-DEL-PRODUCTO","cantidad":2}],"codigoCupon":"CLUB10","tipoEntrega":"Retiro"}
```

Respuesta: bruto/subtotal, descuento/descuentoAplicado, envio, total, moneda, promocion y codigoCupon. Cotizar no reserva stock ni usos. El código es case insensitive por normalización. La promoción automática de mayor beneficio se aplica si no hay cupón directo; se usa una única promoción.

## Concurrencia de cupones

MaxUses es la capacidad configurada; los usos disponibles son MaxUses - CurrentUses. Crear un pedido incrementa CurrentUses una vez (equivale a restar una unidad disponible), dentro de la misma transacción que reserva stock y guarda el pedido. No se modifica la capacidad máxima porque impediría conservar la auditoría y liberar reservas correctamente.

El límite personal requiere email verificado mediante JWT. Cuenta pedidos no cancelados del usuario o del mismo email normalizado, incluidos invitados anteriores. Las lecturas de la cuota y la inserción ocurren en Serializable; rowversion protege además stock y contador global. Deadlocks, conflictos y colisiones de clave se reintentan con un DbContext nuevo; se reconocen también los errores 1205 envueltos por EF Core. RequestId mantiene la idempotencia de compra. Cancelar libera stock y usos una sola vez, siguiendo el comportamiento previo del MVP. Entregar no libera cuotas.

## Arquitectura de pasarela

`IPaymentGateway` autentica el cuerpo/headers originales y obtiene los datos confiables del proveedor. En un adaptador real debe consultar su API servidor a servidor y comprobar cuenta del comercio, referencia del intento, importe y moneda. `IOnlinePaymentsService` inicia el intento usando PaymentId como idempotency key del proveedor, realiza la llamada de red fuera de la transacción SQL y después conserva la asociación. Si llega un webhook antes de esa asociación, se devuelve 503 para reintentar.

`IPaymentReconciler` hace la conciliación y el log en una sola transacción. Un evento repetido devuelve su resultado sin duplicar cambios. Mismo EventId con otro hash produce 409. approved confirma pago; rejected rechaza el intento. Un rechazo tardío no revierte un pago confirmado. La máquina de estados del pedido no cambia al cobrar. Una aprobación después de cancelar se audita como RequiereReembolso; otro intento con un pedido ya cobrado queda RequiereConciliacionManual. Ambos requieren atención operativa; no se ejecutan reembolsos automáticos.

Los eventos con importe/moneda inconsistentes se auditan y no cambian el pago. Los eventos sin firma válida se rechazan antes de acceder al conciliador. Una transacción desconocida produce 503 y no se registra como evento procesado para permitir reintentos.

**Estado actual: proveedor real deshabilitado.** `Payments:Provider=Disabled` es el valor por defecto y devuelve 503. No se integraron credenciales ni se realizaron cobros. Registrar la implementación Mercado Pago o Stripe de IPaymentGateway y seleccionarla en Program.cs cuando se decida el proveedor. No conectar directamente su JSON al adaptador simulado.

Solo para pruebas locales existe `DevelopmentHmacPaymentGateway`, prohibido en producción:

```powershell
$env:Payments__Provider='DevelopmentHmac'
$env:Payments__DevelopmentSigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Su cuerpo tiene tenantId/eventId/transactionId/status/amount/currency. X-Payment-Timestamp contiene segundos Unix; X-Payment-Signature es hex HMAC-SHA256(timestamp + '.' + bytes originales). Ventana de cinco minutos; el EventId persistido impide reprocesar entregas aceptadas. Este protocolo NO es el de Mercado Pago ni Stripe. No poner la clave en Expo, .env público, URLs o logs.

Referencias oficiales consultadas: https://learn.microsoft.com/en-us/ef/core/saving/concurrency y https://www.mercadopago.com.ar/developers/en/docs/zero-dollar-auth/additional-content/your-integrations/notifications/webhooks .

## Verificación

- dotnet build Gastronomia.slnx: cero errores/advertencias.
- dotnet run --project tests/Gastronomia.Checks -- --sql sql: 104 verificaciones aprobadas sobre SQL Server real en una base temporal, eliminada al terminar. Incluye las 77 anteriores y 27 de Fase 3.
- 9 verificaciones HTTP con servidor HMAC temporal: firmas faltantes/incorrectas, cuerpo alterado, antigüedad, JSON inválido, transacción desconocida, Disabled y JWT obligatorio para favoritos/ranking. No crean pedidos ni pagos. Informe outputs/Pruebas-fase3-webhooks.json.
- Migración 005 aplicada y repetida sobre la base local existente.
- Expo: 16 pruebas de estado, TypeScript/lint, compilación Android y prueba UI de favoritos persistidos, ranking y cupón.
