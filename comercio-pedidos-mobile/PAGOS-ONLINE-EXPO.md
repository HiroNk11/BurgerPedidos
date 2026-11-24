# Pago online en Expo · Mercado Pago

Implementación sobre Expo SDK 58 y el backend .NET 10 de Fase 4. No agrega dependencias: expo-web-browser y expo-linking ya estaban instalados.

## API, estado y componentes

- services/customerTypes.ts: OnlinePaymentCheckout con proveedor, transaccionId, preferenceId y checkoutUrl (nullable según contrato backend).
- services/customerApi.ts: iniciarPagoOnline(token, pedidoId, signal). JWT explícito, timeout 45 segundos y sin reintentos automáticos. El hook obtiene el JWT de AuthContext.
- hooks/usePaymentFlow.ts / contexts/PaymentFlowContext.tsx: flujo compartido para todos los botones. Evita abrir dos preferencias desde banner y detalle a la vez, registra Linking antes de abrir checkout y maneja auth/foreground/arranque en frío.
- state/paymentFlow.ts: máquina de estados comprobable sin UI: idle, requesting, opening, waiting, checking. Descarta respuestas después de salir/cambiar de cuenta, consulta el pedido antes de iniciar y bloquea durante la generación/apertura del checkout.
- services/paymentBrowser.ts: openBrowserAsync con Safari modal en iOS y Custom Tabs en Android. En web usa la misma pestaña para evitar popups bloqueados tras esperar HTTP y conservar la sesión. Fallback a Linking.openURL solo si el navegador integrado no está disponible.
- services/paymentAttemptStorage.ts: correlación por tenant/cuenta con expiración de 24 horas. Guarda solamente orderId y fecha, sin JWT ni checkoutUrl; serializa escrituras y limpieza al salir.
- utils/paymentLinks.ts: solo permite HTTPS de Mercado Pago, sin usuario/password, puertos alternativos ni hosts parecidos. El retorno solo acepta esquemas registrados o el origen web configurado.
- components/OnlinePaymentButton.tsx: botón en tracker y detalle. Visible únicamente para cliente autenticado, PasarelaOnline, importe positivo, pago Pendiente y pedido PendienteConfirmacion/Aceptado/EnPreparacion/EnCamino. No modifica pedidos en efectivo/transferencia, rechazados, entregados o cancelados.
- components/PaymentFlowNotice.tsx: aviso persistente de retorno/errores. Incluye consulta manual por si el navegador no notifica su cierre.
- app/payment-return.tsx: puente HTTPS entre los back_urls de Mercado Pago y la aplicación instalada. Sus parámetros NO acreditan un pago.

Checkout permite elegir Mercado Pago con la cuenta autenticada. Primero guarda el pedido y vacía el carrito mediante el flujo existente; desde el seguimiento se solicita la preferencia. No se crea otro pedido al reintentar el pago.

## Deep links y URLs del backend

app.json conserva burgerclub y agrega gastronomiaapp. Ambos son válidos:

```text
gastronomiaapp:///payment-return?orderId=GUID-DEL-PEDIDO
burgerclub://payment-return?orderId=GUID-DEL-PEDIDO
```

El backend actual requiere back_urls HTTPS. Configurar en el entorno externo del backend:

```text
MERCADOPAGO_SUCCESS_URL=https://TIENDA/payment-return?result=success
MERCADOPAGO_PENDING_URL=https://TIENDA/payment-return?result=pending
MERCADOPAGO_FAILURE_URL=https://TIENDA/payment-return?result=failure
```

En Expo configurar EXPO_PUBLIC_PAYMENT_RETURN_ORIGIN=https://TIENDA (sin ruta) y reiniciar Metro. Es configuración pública, no una credencial. La web debe servir /payment-return, y los back_urls deben apuntar a ella. Mercado Pago suele incluir external_reference; se usa solo como ID para consultar el pedido propio. El puente ofrece un botón para abrir la app instalada y transmite únicamente ese GUID, sin propagar parámetros bancarios o estados del proveedor. En web también puede consultar Mis pedidos sin abrir la app.

No poner gastr...:// directamente en los back_urls del backend: la validación HTTPS de Fase 4 se mantiene. Para apertura automática mediante Universal Links/App Links se necesita asociar un dominio; esta entrega registra esquemas personalizados y usa un botón de retorno explícito en el puente HTTPS.

### Development build y Expo Go

Los esquemas del app.json requieren un binario nuevo (development build o producción) instalado. Expo Go no registra gastronomiaapp:// ni burgerclub:// del proyecto; usa su esquema exp:// y rutas /--/. Se puede revisar la pantalla payment-return desde Expo Go o web, pero el retorno real al esquema personalizado debe probarse en el build instalado. No se generó un binario nativo en esta PC.

## Retorno y fuente de verdad

Se escuchan Linking.addEventListener('url'), getInitialURL y los cambios de AppState. El cierre manual de Safari también consulta el backend. En iOS se invoca dismissBrowser; SDK 58 no expone ese cierre programático en Android: el intent devuelve el foco a la app y la consulta manual permite recuperarse si no se recibe AppState. No se llama una API inexistente en Android.

Al volver se cierra el navegador propio cuando corresponde, se muestra un aviso, se fuerza refresh(true) en OrdersContext y se programan consultas a los 4 y 10 segundos. El polling habitual de 12 segundos sigue activo. El refresh forzado descarta una consulta vieja que estuviera en vuelo.

Ni result=success, status=approved ni ninguna URL cambia EstadoPago. Únicamente la respuesta autenticada de la API puede mostrar Pagado, después de la conciliación por webhook. Si el usuario cierra el checkout sin pagar, también se consulta el estado: no se declara éxito. Un error de red o proveedor conserva el pedido y permite reintentar el mismo endpoint.

## Validación y límites

- TypeScript y lint: correctos, sin advertencias.
- npm run test:state: 31 pruebas aprobadas, 15 nuevas para pagos. Cubren URL segura, elegibilidad, doble toque, pedido ya pagado, JWT vencido, respuesta tardía, retorno, cierre manual, restauración y limpieza concurrente del intento.
- Bundle Android compilado desde Metro.
- Prueba real de UI en Expo Web a 390 px: OTP con cuenta ficticia, botones en banner/detalle de #PED-1163, manejo del 503 del proveedor local deshabilitado y retorno result=success manteniendo Pago pendiente. El pedido técnico se canceló después para restituir stock. No se enviaron datos a Mercado Pago ni se hicieron cobros.

Queda pendiente abrir el checkout y verificar el retorno en Android/iOS físicos con una cuenta sandbox y HTTPS público. El backend local sigue con pagos deshabilitados hasta configurar las credenciales: ese 503 se muestra claramente en la app. No se incluyeron secretos en el ZIP.

Referencias consultadas: https://docs.expo.dev/versions/v58.0.0/sdk/webbrowser/ , https://docs.expo.dev/versions/v58.0.0/sdk/linking/ y https://docs.expo.dev/linking/into-your-app/ .
