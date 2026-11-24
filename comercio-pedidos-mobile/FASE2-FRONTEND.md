# Fase 2 · Cuenta y pedidos en Expo SDK 58

Implementación TypeScript conectada a la API .NET 10 existente. Sin favoritos ni ranking.

## Estructura

- `src/services/http.ts`: HTTP tipado, timeout, errores y bearer explícito. No envía cookies del panel ni reintenta automáticamente compras.
- `customerApi.ts` / `customerTypes.ts`: contratos de OTP, perfil, direcciones, historial y reorder.
- `sessionStorage.ts`: JWT en SecureStore en Android/iOS; sessionStorage por pestaña en web. El token vencido se elimina. Nunca incluir claves de backend en EXPO_PUBLIC_*.
- `trackedOrderStorage.ts`: registro de múltiples pedidos; capacidades de invitado separadas del JSON y guardadas con SecureStore en nativo. Logout elimina el seguimiento local de la cuenta; el historial permanece en SQL.
- `contexts/AuthContext.tsx`: requestOtp, verifyOtp, logout, restauración, vencimiento y limpieza de cuenta. `useAuth` es el hook público.
- `contexts/CartContext.tsx` / `state/cartReducer.ts`: clearCart, reemplazo con control de revisión, stock y bloqueo de una solicitud pendiente.
- `contexts/CheckoutContext.tsx`: conserva RequestId y payload antes del POST; vacía el carrito al recibir creación exitosa. Un fallo de red mantiene el mismo pedido para reintentar sin duplicarlo.
- `contexts/OrdersContext.tsx`: `useOrders`, historial paginado y tracker. Polling cada 12 segundos con la app visible, actualización al volver al primer plano y botón manual. Evita respuestas tardías de otra cuenta o página.
- `hooks/useAccount.ts`, `useReorder.ts`, `useCheckoutForm.ts`: lógica de formularios y operaciones separada de la UI.
- `src/app/login.tsx`, `profile.tsx`, `orders.tsx`, `checkout.tsx`: pantallas OTP, cuenta/direcciones, pedidos y compra.
- `components/ActiveOrderTracker.tsx`, `AddressEditor.tsx`, `ReceiptUploader.tsx`: componentes compartidos.

## Comportamiento

El código de seis dígitos se verifica automáticamente al completarlo; también hay botón de ingreso y reenvío con espera. Durante desarrollo los códigos llegan al Buzón local del panel, no a un correo externo. Al verificar el email, el backend vincula los pedidos invitados anteriores.

Las direcciones admiten calle, número, piso/depto y coordenadas opcionales. Checkout precarga los datos y permite elegir una dirección guardada. Los pedidos activos siguen separados del carrito: se puede hacer otra compra mientras se prepara la anterior. El tracker muestra solo estados activos y desaparece en Entregado/Cancelado. Solo muestra ETA si la API la proporciona.

Volver a pedir consulta la propuesta y el catálogo actual, informa cambios, productos removidos y promociones que no se trasladan. Los avisos se confirman antes de reemplazar el carrito; cancelar conserva el contenido anterior. Si el carrito o la sesión cambian durante la operación, se rechaza el reemplazo tardío.

Los comprobantes de transferencia pueden recuperarse desde la cuenta después de cerrar y volver a ingresar, sin depender del token local de invitado. La API valida propietario y tenant del JWT. Adjuntar una imagen deja el pago en ComprobanteEnviado; no lo marca como Pagado.

## Ejecutar

1. Iniciar el panel/API local en 5180.
2. Copiar `.env.example` a `.env`, configurar API con IP LAN y tenant del local. `EXPO_PUBLIC_EMAIL_MODE=local` muestra la indicación del buzón de pruebas.
3. `npm ci`, luego `npx expo start --go --lan --port 8082`. Usar Expo Go SDK 58 y la misma Wi-Fi.
4. Web local: http://localhost:8082. Para celular: URL/QR LAN de Metro, nunca localhost.

## Validación realizada

- `npx tsc --noEmit`: correcto.
- `npm run lint -- --no-cache`: correcto, sin advertencias.
- `npm run test:state`: 12 pruebas aprobadas (UUID sin randomUUID, carrito, stock, reorder, estados terminales, almacenamiento JWT, HTTP y aislamiento del feed).
- Bundle Android compilado y descargado desde Metro: 6.882.292 bytes.
- 11 verificaciones HTTP de comprobantes: propietario, otra cuenta, otro tenant, JWT inválido y capacidad de invitado. Informe `outputs/Pruebas-comprobante-cuenta.json`.
- Flujo real en Expo Web a 390 px: compra invitada #PED-1111, OTP y linkeo, edición de perfil/dirección, restauración tras recarga, aceptación/entrega, desaparición del tracker, cancelar/confirmar reorder y compra autenticada de transferencia #PED-1162. Carrito vacío en ambas creaciones. Logout oculta todo el historial privado; otro OTP recupera ambos pedidos y el formulario de comprobante.
- #PED-1111 es una prueba entregada, y #PED-1162 queda pendiente con la nota PRUEBA LOCAL · no preparar ni enviar, para inspección del panel. Los emails y datos son ficticios. No se enviaron mensajes de WhatsApp ni correos externos.

Pendiente: ejecutar esta versión en un teléfono físico para validar permisos y apertura de la aplicación nativa de WhatsApp. La compilación Android y la prueba web no sustituyen esa comprobación.
