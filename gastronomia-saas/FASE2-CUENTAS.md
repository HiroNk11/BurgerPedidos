# Fase 2 · backend .NET 10

Esta entrega implementa cuentas sin contraseña, perfil/direcciones, historial y propuesta de recompra. Los componentes de React Native/Expo, la liberación del carrito y ActiveOrderTracker quedan para la siguiente entrega. No incluye favoritos ni ranking.

## Capas y archivos

- Domain/Usuario.cs: email verificado, nombre, teléfono y colección de direcciones.
- Domain/Direccion.cs: propietario, calle, número, piso/depto y coordenadas opcionales validadas.
- Domain/OtpChallenge.cs: un desafío por comercio/email, hash, vencimiento, intentos, consumo y ventana de reenvíos.
- Application/CustomerContracts.cs: DTOs e interfaces IPasswordlessService, ICustomerAccountService, ICustomerOrdersService, IEmailService, IOtpCodeHasher e IAccessTokenIssuer. No depende de ASP.NET ni EF.
- Infrastructure/PasswordlessService.cs: generación criptográfica, persistencia SQL, consumo atómico y vinculación de invitado. HmacOtpCodeHasher evita almacenar un hash simple de un espacio de solo un millón de códigos.
- Infrastructure/CustomerServices.cs: perfil/direcciones e historial/reorder, con filtros de propietario y comercio.
- Infrastructure/CustomerConfigurations.cs: Fluent API, FKs compuestas e índices.
- Api/CustomerAuthentication.cs: JWT de cliente, claves, configuración de autorización y adaptador de correo.
- Api/CustomerControllers.cs: controladores de autenticación, cuenta y pedidos del cliente.

## OTP y JWT

POST /api/auth/request-otp recibe {tenantId,email}. Usa 6 dígitos de RandomNumberGenerator, expira a los 10 minutos y no devuelve el código en JSON. Responde 202 con un mensaje genérico. El email se normaliza igual que las compras invitadas.

La base almacena HMAC-SHA256 del código ligado a comercio, email y MessageId, con una clave independiente de JWT. Máximo 5 intentos fallidos por código, mínimo 60 segundos entre envíos y máximo 5 envíos por hora/email/comercio. El límite por email está en SQL; los límites adicionales por IP (10 solicitudes/10 minutos y 30 verificaciones/5 minutos) son por instancia de API.

POST /api/auth/verify-otp recibe {tenantId,email,codigo}. Un código incorrecto, agotado, vencido o ya utilizado devuelve 401. La lectura WITH (UPDLOCK,HOLDLOCK) y la transacción serializable evitan que dos procesos consuman el mismo código. Los intentos incorrectos se guardan antes de responder el error.

Al verificar se crea o recupera Usuario, se marca EmailVerificado y se vinculan Orders del mismo TenantId/GuestEmailNormalized con UsuarioId NULL. Todo se confirma en la misma transacción. Se realiza también en logins posteriores para recuperar compras como invitado hechas después del registro; nunca se reasignan pedidos de otro usuario.

La respuesta es:

```json
{
  "token": {"accessToken":"JWT","tokenType":"Bearer","expiresAtUtc":"fecha UTC"},
  "cliente": {"id":"GUID","email":"cliente@example.com","nombre":"","telefono":"","direcciones":[]},
  "nuevoUsuario":true,
  "pedidosVinculados":2
}
```

El JWT dura una hora y contiene sub, tenant_id, email, email_verified, role=customer y jti. Valida firma HS256, algoritmo, issuer, audience y vencimiento, con tolerancia de reloj de 30 segundos. No incorpora rol staff. No se implementan refresh tokens en esta fase: al vencer se solicita otro OTP.

El esquema CustomerJwt y la política Customer están separados del acceso administrativo existente. Un JWT de cliente no permite consultar el buzón staff, aceptar pedidos ni registrar pagos. Los endpoints de cuenta obtienen usuario y comercio exclusivamente del JWT.

## Correo de desarrollo

IEmailService usa CustomerEmailService, que adapta el contrato al ITransactionalEmailSender existente. En Development los códigos aparecen en Buzón local del panel, con asunto Tu código de acceso a Burger Club. No hay endpoint público para leer códigos ni se imprimen en logs. No se envían correos externos mientras Email:Provider=Local.

Para Resend se mantiene la configuración ya existente del proveedor. La emisión OTP confirma primero el desafío y después espera al proveedor: un fallo de envío produce un error; no existe outbox de OTP en esta versión. La outbox de pedidos continúa independiente. El usuario puede solicitar otro código respetando el intervalo.

## Perfil y direcciones

Enviar Authorization: Bearer JWT. En el cliente web usar credentials:omit para separar la sesión del cliente de la cookie administrativa y su protección CSRF.

| Método y ruta | Función |
|---|---|
| GET /api/cuenta | Perfil con direcciones |
| PUT /api/cuenta | Nombre y teléfono |
| GET /api/cuenta/direcciones | Direcciones propias |
| GET /api/cuenta/direcciones/{id} | Una dirección propia |
| POST /api/cuenta/direcciones | Alta; responde 201 |
| PUT /api/cuenta/direcciones/{id} | Modificación |
| DELETE /api/cuenta/direcciones/{id} | Baja; responde 204 |

El perfil inicial puede estar incompleto. Editarlo requiere nombre y teléfono de 8 a 15 dígitos, con + opcional. No se cambia el email mediante PUT: la identidad se acredita por OTP. Máximo 10 direcciones por cuenta/comercio. Latitud y longitud deben estar ambas presentes o ambas ausentes, en sus rangos válidos. IDs ajenos devuelven 404. Cambiar o borrar una dirección no modifica el snapshot de pedidos existentes.

## Historial, seguimiento y volver a pedir

GET /api/pedidos/mis-pedidos?pagina=1&tamanio=20 devuelve activos y una página de históricos, con contadores separados. Activos: PendienteConfirmacion, Aceptado, EnPreparacion, EnCamino. Históricos: Entregado o Cancelado. Se ordenan por fecha/ID descendentes; máximo 100 por página y hasta 100 activos en una respuesta, con totalActivos para informar el límite.

GET /api/pedidos/mis-pedidos/{id} devuelve estados, fecha, items históricos y total del pedido propio. Servirá para el polling del tracker; aceptar un pedido mantiene su condición de activo. El frontend liberará el carrito al guardar y mantendrá el tracker por separado en la próxima etapa.

POST /api/pedidos/reorder/{idPedidoHistorico} solo acepta un pedido histórico propio. Devuelve:

```json
{
  "pedidoOrigenId":"GUID",
  "items":[{"productId":"GUID","nombre":"Burger","cantidad":2,"precioUnitarioActual":9500,"subtotal":19000,"precioUnitarioHistorico":8500}],
  "warnings":[{"code":"PrecioActualizado","productId":"GUID","message":"Burger: el precio cambió respecto al pedido anterior."}],
  "itemsRemovidos":[],
  "subtotalProductos":19000,
  "moneda":"ARS",
  "requiereCotizacion":true
}
```

- Precios actuales, nunca copia importes históricos al carrito nuevo.
- Producto inactivo/ausente o sin stock: se elimina de la propuesta, con warning y entrada en itemsRemovidos.
- Stock menor que la cantidad histórica: reduce cantidad e informa CantidadReducida.
- Los descuentos históricos no se reaplican; informa PromocionNoReaplicada.
- No crea Order, Payment ni reserva stock. La compra nueva debe pasar por /api/pedidos/cotizar y /api/pedidos, que vuelven a validar catálogo/promociones/tarifa.
- El catálogo actual del proyecto contiene Product/OrderItem sin variantes ni adicionales persistidos. Se comparan los productos efectivamente registrados. No se reconstruyen personalizaciones a partir de notas; para comparar variantes/adicionales habrá que incorporarlos antes al catálogo y al snapshot histórico. No se simula ese soporte.

POST /api/pedidos conserva la compra invitada y ahora reconoce el JWT opcional de cliente. Si se envía un bearer inválido no baja silenciosamente a invitado: devuelve 401. El TenantId del pedido debe coincidir con el JWT y GuestEmail con el email verificado. El RequestId y CheckoutToken de la compra se siguen generando y conservando antes del POST. Un reintento pendiente iniciado como invitado debe conservar su modalidad original hasta recibir resultado definitivo, incluso si se inicia sesión en otra pantalla.

## SQL y claves

- Bases nuevas: sql/schema.sql (001_schema.sql y 002_history_guards.sql).
- Bases existentes de la versión previa: sql/004_customer_accounts.sql, actualización aditiva e idempotente. Añade Nombre/Telefono a Users y tablas Direcciones/OtpChallenges con índices y restricciones. No borra pedidos ni recalcula históricos.
- El actualizador local Gastronomia.Dev --upgrade ejecuta 003, 004 y los triggers, exclusivamente sobre BurgerClubPanelDev_. El script Iniciar-panel.ps1 lo llama al iniciar.
- En Development se generan claves JWT/OTP independientes y se guardan protegidas con ASP.NET Data Protection en App_Data/customer-auth.keys. Las claves de protección se conservan en App_Data/keys y usan DPAPI en Windows. No se imprimen ni se incluyen en el ZIP.
- En producción configurar CustomerAuth__JwtKey y CustomerAuth__OtpKey como Base64 de al menos 32 bytes aleatorios independientes, en un almacén de secretos. Todas las réplicas deben compartir los mismos valores. CustomerAuth__Issuer/Audience son configurables. La API falla al iniciar si faltan claves válidas.
- Producción requiere HTTPS y el proveedor real de correo; el buzón local sigue limitado a Development.

## Pruebas

Build: dotnet build Gastronomia.slnx.
SQL aislado: dotnet run --project tests/Gastronomia.Checks -- --sql sql.

77 verificaciones SQL/dominio incluyen la fase anterior y pruebas OTP, uso único concurrente, reenvíos por hora, bloqueo, vencimiento, linkeo, direcciones, historial y recálculo. La base temporal de cada ejecución se elimina al terminar.

35 verificaciones HTTP sobre la API local comprueban captura de OTP en el buzón, JWT, firma alterada, separación customer/staff, autorización de direcciones/pedidos, compra autenticada, historial y reorder. Las compras técnicas se cancelan y restituyen stock. Informe: outputs/Pruebas-fase2-backend.json. El frontend de cuentas ya se implementó y verificó en Expo Web; ver ../comercio-pedidos-mobile/FASE2-FRONTEND.md. No se enviaron correos externos.

## Recuperación de comprobantes desde la cuenta

PedidoCliente incluye MetodoPago para recuperar el formulario de transferencia sin el diario local. El endpoint público de comprobantes admite JWT CustomerJwt, comprueba rol, email verificado, tenant y propietario. Un bearer inválido devuelve 401 y no cae a invitado. Sin bearer se mantiene X-Order-Token. No requiere migración SQL.
