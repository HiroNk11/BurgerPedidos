# Checkout Expo, panel en vivo y buzón local

Implementado sobre .NET 10 y Expo SDK 58. El backend anterior de ComercioPedidos se conserva separado.

## Abrir en este equipo

- Panel: http://localhost:5180/admin/ · acceso en outputs/Panel-acceso-local.md.
- Expo Web: http://localhost:8082/.
- Expo Go: exp://192.168.0.249:8082, con SDK 58 y misma Wi-Fi.
- API LAN: http://192.168.0.249:5180.

Para reiniciar, ejecutar outputs/Iniciar-panel.ps1 y, en otra terminal, outputs/Iniciar-app-expo.ps1. No ejecutarlos si ya están corriendo esos puertos. El primero actualiza solo la base BurgerClubPanelDev_ sin borrar pedidos ni reinicializar stock. La app toma su TenantId de la configuración privada del panel. Si cambia la IP, pasar -ApiUrl http://NUEVA_IP:5180 a Iniciar-app-expo.ps1 y reiniciar Metro. No se cambiaron reglas de firewall; si el celular no alcanza la API, verificar conectividad de la red privada.

La base inicial tenía seis pedidos ficticios. Las pruebas añaden pedidos claramente identificados; permanecen en el historial local. Las credenciales, claves de protección, imágenes privadas y correos capturados se excluyen del ZIP.

## Flujo del cliente

1. GET /api/tienda?tenantId=GUID obtiene IDs, stock y precios del catálogo SQL. Las fotos locales se usan solo como presentación.
2. POST /api/pedidos/cotizar recibe {tenantId,pedido}; calcula productos, una promoción y tarifa de delivery sin reservar stock.
3. POST /api/pedidos usa el mismo formato. Pedido incluye RequestId, contacto, método, items[{productId,cantidad}], nombreCliente, tipoEntrega, dirección, coordenadas opcionales, notas, cupón, CheckoutToken aleatorio de 32 bytes y TotalEsperado.
4. El servidor valida de nuevo el catálogo y guarda stock/promoción/pedido/pago/outbox en una transacción. Devuelve ShortCode, totales y MensajeWhatsApp/WhatsAppUrl. Si el total cambió, responde 409 para volver a cotizar.
5. La app persiste la solicitud antes del POST y reutiliza RequestId y token tras un timeout; no genera otra compra automáticamente. En dispositivos, el token queda en SecureStore y el resto en AsyncStorage; en web se conserva por pestaña en sessionStorage. Nueva compra cierra ese registro local; el pedido permanece en el servidor.
6. WhatsApp se abre mediante una acción explícita después de guardar. El cliente debe enviar el mensaje. Abrir WhatsApp no acepta el pedido ni acredita el pago.

GET /api/pedidos/{id}?tenantId=GUID requiere X-Order-Token; la base guarda únicamente su hash SHA256. Devuelve estados y total, sin exponer los datos del pedido a quien solo conoce el código. La app consulta el estado cada 5 segundos cuando la ficha está abierta. Este token permite seguir la compra invitada en ese dispositivo; no reemplaza la futura identidad del cliente y su historial autenticado.

## Comprobantes

POST /api/pedidos/{id}/comprobante?tenantId=GUID: multipart/form-data con campo archivo y X-Order-Token. Admite PNG/JPEG/WebP hasta 5 MB, valida la firma del formato y rechaza pedidos cancelados, pagados o que no usan transferencia. Guarda de forma atómica bajo App_Data/receipts, fuera de wwwroot. El nombre es un hash y la ruta de consulta requiere staff del mismo comercio.

La imagen cambia EstadoPago a ComprobanteEnviado. Registrar transferencia recibida exige declarar el importe exacto y confirmar la verificación manual. No hay consulta bancaria ni conciliación automática. La prueba usa un icono PNG del proyecto como imagen de carga; no es un comprobante bancario.

## Panel en vivo

GET /api/tenants/{tenantId}/staff/events abre SSE autenticado con avisos sin datos personales. Crear, cambiar estado, registrar un pago o adjuntar comprobante invalida el listado. El panel conserva el refresco de respaldo cada 15 segundos. Las fichas se vuelven a consultar después de sus acciones.

Los endpoints staff conservan autorización por comercio y protección CSRF. La app pública usa fetch credentials:omit; CORS autoriza los orígenes locales explícitos de Expo Web sin credenciales de administración.

## Emails: Mock Email Service activo

Program.cs registra ITransactionalEmailSender con LocalMailboxEmailSender en Development, por defecto Email:Provider=Local. No requiere cuenta, dominio ni clave. Cada mensaje se captura como JSON por EventId en App_Data/mail/{tenantId}. La sección Buzón local del panel lo consulta mediante un endpoint staff protegido.

- PedidoCreado: Recibimos tu solicitud de pedido #PED-XXXX; aclara que falta aceptación.
- PedidoAceptado: Tu pedido fue aceptado #PED-XXXX.
- PedidoEnCamino: Tu pedido va en camino #PED-XXXX.

La outbox se inserta junto al cambio de dominio. El worker reclama eventos con lease y registra intentos, próximo reintento y error; un envío fallido no se marca como procesado. El buzón usa escritura atómica y deduplicación por EventId. Reiniciar el servidor no vuelve a enviar eventos ya procesados. Los eventos anteriores que ya se habían marcado como simulados no se reproducen.

Cuando esté lista la cuenta de Resend, configurar en el servidor Email__Provider=Resend, Email__From con remitente verificado y Email__ResendApiKey o RESEND_API_KEY. Program.cs selecciona la implementación ResendEmailSender del mismo contrato; no se modifican los casos de uso ni se pone la clave en Expo. El adaptador incorpora Idempotency-Key por evento. No se validó envío real con Resend. El buzón local está limitado a Development y no es una bandeja de correo externo.

## Validación realizada

- Build .NET 10 y panel React sin errores; TypeScript y lint de Expo sin errores.
- Exportación Android SDK 58/Hermes correcta; no equivale a ejecutar en un celular.
- 47 verificaciones de dominio/SQL Server en base temporal: concurrencia, promos, estados, snapshots, vinculación verificada, delivery, hash de token, outbox e idempotencia.
- 19 verificaciones HTTP: autorización de estado y comprobante, aislamiento, CORS, reintentos, cancelación con devolución de stock y correos capturados.
- Compra desde Expo Web #PED-1006: efectivo/retiro, rechaza importe 100 frente a total 12000; finalmente Entregado + Pagado.
- Compra desde Expo Web #PED-1007: transferencia/delivery 15500, PNG adjunto visible únicamente para staff; aceptación, preparación, en camino, cobro manual de prueba y entrega.
- Cinco correos para esas dos compras: dos de efectivo y tres de transferencia, sin duplicados.
- Tercera compra por HTTP técnico: cancelación y devolución de stock. El informe local está en outputs/Pruebas-integracion-pedidos.json.
- WhatsApp abrió el resumen de #PED-1007 con el destino de pruebas. No se pulsó enviar.

Pendiente de validación con el usuario: ejecución física en Expo Go, permisos GPS/galería del dispositivo y comunicación por la Wi-Fi real del celular. No se enviaron correos externos ni se movió dinero.
