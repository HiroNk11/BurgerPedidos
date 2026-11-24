# Fase 4 · Mercado Pago y despliegue

## 1. Código C# de Mercado Pago

`src/Gastronomia.Infrastructure/MercadoPagoGateway.cs` implementa IPaymentGateway con HttpClient tipado, sin depender de un SDK. `MercadoPagoOptions.cs` valida credenciales, vendedor, tenant y URLs HTTPS. Program.cs selecciona el adaptador con Payments__Provider=MercadoPago. Si falta AccessToken o WebhookSecret, falla ANTES de iniciar el servidor y no imprime los valores.

Esta implementación de Checkout Pro usa la Preferences API (`POST /checkout/preferences`), no la nueva Orders API. Solo admite ARS y una cuenta vendedora asociada explícitamente a un tenant por deployment. Para varios vendedores del SaaS corresponde reemplazar la configuración única por un resolver por tenant/OAuth; nunca compartir silenciosamente la cuenta de cobro entre comercios.

Crear preferencia envía ítems netos del snapshot, external_reference=OrderId, metadata tenant_id/order_id/payment_id, notification_url con source_news=webhooks y back_urls HTTPS. El envío figura como un ítem separado; los descuentos se distribuyen a centavos para conservar exactamente el total histórico. Los renglones totalmente gratuitos se omiten del cobro. PaymentId es X-Idempotency-Key. El gate SQL de sesión serializa inicios concurrentes entre réplicas y se libera explícitamente antes de devolver la conexión al pool. No se mantiene una transacción SQL durante HTTP.

**PreferenceId no es el ID del pago.** Payment conserva PreferenceId/CheckoutUrl además de TransaccionIdExterna. La creación devuelve proveedor, preferenceId y checkoutUrl; transaccionId permanece null hasta consultar un pago auténtico. `POST /api/pedidos/{id}/iniciar-pago` exige JWT cliente y propiedad del pedido. La app puede abrir checkoutUrl; los back_urls son navegación y jamás confirman el pago.

Ejemplo de respuesta:

```json
{"proveedor":"MercadoPago","transaccionId":null,"preferenceId":"ID-DEL-PROVEEDOR","checkoutUrl":"https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=..."}
```

### Validación y conciliación

1. Leer data.id desde la query, x-signature y x-request-id. Para pagos se requieren ID numérico y request-id válidos. No se aceptan IPN sin firma ni QR sin firma en este endpoint.
2. HMAC-SHA256 del manifiesto `id:{data.id};request-id:{x-request-id};ts:{ts};`, usando el WebhookSecret, no el AccessToken. Comparación en tiempo constante. ts literal permanece en la firma; la ventana de antigüedad admite segundos o milisegundos y tolera cinco minutos por defecto.
3. Consultar `/v1/payments/{id}`, después `/merchant_orders/{id}` y la preferencia original. Verificar collector_id, ambiente test/live, pertenencia del pago a la orden comercial, external_reference y metadata de la preferencia. El estado, TenantId y evento del JSON recibido NO son evidencia confiable.
4. IPaymentReconciler verifica el PreferenceId/OrderId/PaymentId persistidos, importe y moneda. Dentro de una transacción actualiza estado, PaymentLog inmutable y outbox. La deduplicación usa el snapshot leído al proveedor, no body.id ni el ID de entrega; otro estado de la misma transacción no se pierde por un duplicado anterior.

approved confirma, rejected/cancelled rechazan el intento. pending/in_process/authorized se auditan sin cobrar. refunded/charged_back/in_mediation requieren conciliación manual. Un rechazo tardío no revierte Pagado. Un segundo ID aprobado distinto se registra como RequiereReembolsoDuplicado. Aprobación tras cancelación requiere reembolso. Estas incidencias y los importes inconsistentes se guardan en SQL y emiten un log Error sin datos sensibles; conectar alertas del operador a esos logs. No se ejecutan reembolsos automáticamente.

HTTP 401: firma inválida. HTTP 503: error/timeout del proveedor o asociación todavía ausente; permite reintentar. No se registran payloads completos, firmas, tokens ni respuestas de error del proveedor. El timeout total de verificación es 18 segundos; no se devuelve un éxito antes de persistir la conciliación.

### SQL

`sql/006_mercadopago_checkout.sql` es aditivo e idempotente; después de 005 agrega PreferenceId/CheckoutUrl, índice único y restricciones. No recalcula pedidos. Se aplicó y repitió sobre la base local. `schema.sql`/`001_schema.sql` se regeneraron desde EF; ejecutar 002 para triggers en bases nuevas. En el entorno local se mantiene Payments:Provider=Disabled: no se habilitaron cobros reales ni se modificaron credenciales.

## 2. Docker y secretos

Archivos: Dockerfile, .dockerignore, docker-compose.yml y .env.template.

La imagen compila el panel con Node 22, restaura y publica API y migrador con SDK .NET 10, y usa aspnet:10.0 como runtime. La API corre sin root, en 8080. Un volumen persistente App_Data conserva comprobantes y claves Data Protection; SQL usa otro volumen. Los secretos, App_Data y configuración local están excluidos del contexto Docker y también del publish por el csproj.

Compose levanta db (SQL Server 2022 linux/amd64), db-init y api. db-init espera la salud de SQL, crea base/esquema/ledger y un tenant vacío, y provisiona gastronomia_api con permisos de lectura/escritura, sin DDL. No carga catálogo ni pedidos demo. La contraseña SA solo llega a db/db-init; la API usa SQLSERVER_APP_CONNECTION_STRING con gastronomia_api. La API espera el éxito del inicializador. Al desplegar, respaldar y probar las migraciones sobre una copia de staging antes de ejecutarlas contra una base operativa.

### Configuración externa

Copiar .env.template a una ruta segura FUERA del repositorio y completar sus valores. No pegar claves en el chat. Las variables vacías causan error explícito en Compose.

- MSSQL_PID: edición autorizada. Developer se limita a desarrollo/staging; elegir la licencia correspondiente en producción. ACCEPT_EULA se deja vacío para que la aceptación sea una decisión del operador.
- DB_NAME y DB_APP_PASSWORD deben coincidir con la base y password de SQLSERVER_APP_CONNECTION_STRING. Escapar correctamente passwords en connection strings; un ejemplo de estructura aparece comentado en la plantilla. El SQL interno cifra el transporte y usa TrustServerCertificate=True por su certificado local; un SQL externo administrado debe usar certificado confiable.
- CUSTOMER_JWT_KEY_BASE64 y CUSTOMER_OTP_KEY_BASE64: claves independientes Base64 de al menos 32 bytes criptográficos. Los issuer/audience deben coincidir con los clientes. STAFF_OIDC_AUTHORITY/AUDIENCE conservan el proveedor del panel existente; no se activa el login de desarrollo en producción. Aprovisionar los usuarios staff del tenant según el IdP.
- RESEND_API_KEY y RESEND_FROM: remitente verificado. Mercado Pago requiere Access Token, Webhook Secret, CollectorId y el TenantId del local. Staging usa Sandbox=true con vendedor/comprador de prueba compatibles; producción requiere las credenciales y modo live correspondientes.
- ALLOWED_HOSTS es el dominio de la API sin esquema; CUSTOMER_WEB_ORIGIN es la URL HTTPS de la web cliente. No configurar comodines.
- TRUSTED_PROXY_IP es la IP que Kestrel ve como origen del proxy HTTPS. No se confía globalmente en X-Forwarded-For/Proto. El proxy debe establecer ambos headers y preservar Host. El puerto publicado se limita a 127.0.0.1; SQL no publica puertos.

Mantener el archivo externo con permisos restrictivos y respaldos cifrados. Los secretos se inyectan en ejecución, no en build args ni capas. Para varios hosts o rotación centralizada, suministrar estas mismas variables desde un gestor de secretos. El volumen de Data Protection requiere permisos del usuario app y backup protegido; por defecto no usa DPAPI en Linux.

### Comandos del operador

```sh
docker compose --env-file /ruta/segura/gastronomia.env config --quiet
docker compose --env-file /ruta/segura/gastronomia.env up -d --build
docker compose --env-file /ruta/segura/gastronomia.env ps
```

Agregar un proxy HTTPS delante de localhost:8080 (no se incluyen certificados ni un dominio ficticio activo). Configurar en Mercado Pago el webhook público `https://TU-DOMINIO/api/webhooks/pagos` y eventos de pagos de la Preferences API. Las tres URLs de retorno deben pertenecer a una web/app accesible. Confirmar que `/health/live` y `/health/ready` responden, y probar una compra sandbox antes de cambiar a modo live. Los health checks internos están excluidos de la redirección HTTPS y no revelan errores SQL.

Los volúmenes persisten al recrear contenedores. No usar `down -v` sobre datos operativos. No ejecutar el antiguo inicializador Dev en producción. La plantilla no trae productos: aprovisionar catálogo, tarifa de envío y staff del negocio. El storefront móvil de la fase anterior no se modificó para mostrar el nuevo botón online en esta entrega backend/infraestructura.

## 3. Evidencia y límites

- Build .NET: cero errores y advertencias.
- Publish Release de API y migrador: correcto; sin appsettings.Development.local.json ni App_Data.
- 135 verificaciones sobre SQL Server real: 104 anteriores y 31 nuevas (22 gateway HTTP simulado + 9 integración de preferencias). Incluyen rechazo/éxito de firmas, importe, vendedor/tenant, ambiente, redondeo, concurrencia, varios intentos y auditoría de doble cobro.
- 5 verificaciones HTTP de la API: fail-fast por AccessToken/WebhookSecret faltantes, readiness SQL y rechazo 401 antes de consultar Mercado Pago. Informe outputs/Pruebas-fase4-http.json. No se contactó a Mercado Pago ni se efectuaron cobros.
- YAML Compose analizado y 31 referencias de variables contrastadas con .env.template. No sustituye docker compose config/build.
- Docker NO está instalado en esta PC. La imagen y el arranque Compose no pudieron ejecutarse aquí. No se proporcionaron credenciales de Mercado Pago ni dominio HTTPS: la integración real debe validarse con la cuenta sandbox y notificación auténtica antes de producción.

Pruebas reproducibles desde la raíz:

```sh
dotnet build Gastronomia.slnx
dotnet run --project tests/Gastronomia.Checks -- --sql sql
dotnet publish src/Gastronomia.Api -c Release
```

Referencias oficiales usadas:
- https://www.mercadopago.com.ar/developers/en/reference/online-payments/checkout-pro-preferences/create-preference/post
- https://www.mercadopago.com.ar/developers/en/docs/checkout-pro-orders/resources/notifications/webhooks
- https://www.mercadopago.com.ar/developers/en/reference/online-payments/checkout-pro-preferences/merchant-orders/get-merchant-order/get
- https://raw.githubusercontent.com/mercadopago/sdk-php/master/examples/Preference/Create.php
- https://learn.microsoft.com/en-us/dotnet/core/docker/build-container
- https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver16
