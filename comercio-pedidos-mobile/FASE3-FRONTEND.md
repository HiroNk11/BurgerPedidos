# Fase 3 · Favoritas, ranking y descuentos

Backend: ../gastronomia-saas/FASE3-PROMOCIONES-PAGOS.md. Usa los mismos .env y JWT de la Fase 2; no agrega librerías nativas.

## Separación de responsabilidades

- services/engagementApi.ts: contratos tipados y HTTP de favoritos/ranking.
- state/favorites.ts: reducer puro; optimismo, confirmación/rollback por producto y aislamiento por cuenta.
- contexts/EngagementContext.tsx: useFavorites, consultas, bloqueo de doble toque, descarte de respuestas viejas, sincronización al cambiar pedidos y manejo de 401. HTTP no reintenta automáticamente el toggle, porque un segundo POST invertiría el primer resultado.
- components/FavoriteButton.tsx: corazón accesible; invitado navega a ingreso. Se actualiza inmediatamente y revierte si falla; una nueva consulta resuelve cortes de conexión ambiguos.
- components/PersonalizedProducts.tsx: dos carruseles horizontales, Tus Favoritas y Lo que más pedís. Solo se renderizan con una cuenta y datos. Usan imágenes existentes y catálogo actual; productos no disponibles no se agregan al carrito.
- CartContext: couponCode y cotización compartidos. La firma de cotización incluye usuario, productos/cantidades/precios, envío, cupón y datos del formulario. Un cambio impide confirmar un total anterior. clearCart y reorder eliminan el cupón previo.
- useCheckoutForm: llama a calcular-totales con JWT explícito; mantiene el POST final autoritativo y el RequestId recuperable de la fase anterior.
- checkout.tsx: Aplicar código / Actualizar total, nombre de promoción, descuento aplicado y total antes de confirmar. Cambiar el cupón exige cotizar nuevamente.

## Ejecutar y probar

Iniciar API/panel en 5180, aplicar SQL 005, y ejecutar `npx expo start --web --go --lan --port 8082`. Usar Expo Go SDK 58. El buzón local sigue atendiendo OTP; no se habilitó Resend ni pagos reales.

Validado: TypeScript, lint sin advertencias, npm run test:state (16 pruebas), y bundle Android compilado. Prueba conectada en Expo Web a 390 px: OTP con cuenta ficticia, ranking de compras existentes, agregar/quitar favoritos, recargar y recuperar selección, sumar un producto desde el carrusel y aplicar CLUB10 al carrito. No se crearon pedidos nuevos en esta prueba de UI. El pedido #PED-1162 visible es la prueba pendiente de la Fase 2.

Pendiente: validación de esta versión en un dispositivo físico. La pasarela sigue preparada solo en backend; no se muestra una opción de cobro online que todavía no puede procesarse.
