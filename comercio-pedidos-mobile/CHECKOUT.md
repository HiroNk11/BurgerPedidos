# Compra conectada al panel .NET 10

El menú y los totales se consultan al backend gastronomia-saas. Configurar EXPO_PUBLIC_COMMERCE_URL y EXPO_PUBLIC_TENANT_ID según .env.example, y reiniciar Metro después de cambiar las variables.

Desde outputs/Iniciar-app-expo.ps1 se toma automáticamente el TenantId local y se inicia Expo LAN en el puerto 8082. Panel y API deben estar corriendo antes. Usar Expo Go para SDK 58 y la misma Wi-Fi que la PC.

La app cotiza, guarda mediante POST /api/pedidos y después permite abrir WhatsApp con el ShortCode generado en SQL. Para transferencia permite adjuntar PNG/JPG/WebP; el pago sigue pendiente de verificación. El pedido en curso se conserva para reintentar sin duplicarlo. El carrito se vacía al crear el pedido; el seguimiento continúa separado en Mis pedidos. Ver FASE2-FRONTEND.md para cuentas, historial y reorder.

El panel muestra la solicitud en vivo y el buzón local captura los avisos de creación, aceptación y salida. Consultar ../gastronomia-saas/CHECKOUT.md para endpoints, seguridad, pruebas y cambio futuro a Resend.

Verificado en Expo Web: efectivo/retiro y transferencia/delivery con imagen. Exportación Android correcta. Pendiente de ejecutar el flujo en el celular físico y comprobar sus permisos GPS/galería.

## Comprobantes en SDK 58

En Android/iOS se envía un File de expo-file-system dentro de FormData. El fetch de SDK 58 rechaza el objeto heredado {uri,name,type} antes de llegar a la API. La versión web conserva el File del navegador. Se valida el archivo legible y el límite de 5 MB; la carga admite hasta 60 segundos. La imagen no confirma el pago.

Se reprodujo el fallo original con el codificador multipart real de SDK 58 y se verificó que el archivo compatible conserva el nombre y los bytes; la API respondió 200/ComprobanteEnviado. Las compras técnicas de esta revisión se cancelaron para restituir stock. La comprobación final en el dispositivo requiere recargar la app.

## Identificadores en la web por IP local

checkoutIdentity genera UUID v4 con Crypto.getRandomValues y configura los bits de versión/variante. No depende de crypto.randomUUID, que puede faltar en orígenes HTTP de la LAN. El token usa 32 bytes criptográficos y no tiene fallback a Math.random. RequestId se conserva durante el reintento del mismo pedido.

Regresión: node --test scripts/checkout-identity.test.cjs. Verifica UUID sin randomUUID, token de 256 bits y fallo cerrado cuando la fuente criptográfica no está disponible.
