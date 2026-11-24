# Burger Club

Adaptación móvil de ComercioPedidos, clonada de https://github.com/HiroNk11/Proyecto-BackEnd-BaseDeDatos-FrontEnd.

## Iniciar

Desde `comercio-pedidos-mobile`:

```sh
npm ci
npm start
```

Abrir con Expo Go compatible con SDK 58, o usar `npm run web` para la versión navegador.

## Incluido

Menú por categorías, carrito con cantidades (máximo 20 por producto), retiro o delivery, nombre, dirección obligatoria para delivery, notas y total con envío. El botón prepara el pedido en WhatsApp; el usuario debe enviarlo y el local debe confirmarlo. No se cobran pagos automáticamente ni se registra una venta al abrir WhatsApp.

Nombre, menú, precios de ejemplo y envío están en `src/constants/menu.ts`. Copiar `.env.example` a `.env` y completar `EXPO_PUBLIC_WHATSAPP` con el número internacional del local, solo dígitos. Reiniciar Expo después de cambiar variables. Por defecto se usa el WhatsApp de pruebas autorizado por el usuario. Una variable vacía mantiene ese valor de prueba.

## Alcance

La experiencia de compra funciona sin backend y utiliza un menú local de ejemplo. El carrito permanece en memoria durante la sesión. El backend .NET/SQL y las pantallas administrativas originales se conservaron como referencia; aún no están conectados con este nuevo flujo de compra. No exponer las rutas administrativas en una publicación sin implementar autenticación y permisos.

Para operar faltan nombre y carta definitivos, fotos propias, WhatsApp comercial definitivo, dirección de retiro, cobertura/horarios y política de entrega. La confirmación y el pago se acuerdan con el local por WhatsApp. No se generó APK ni se publicó en tiendas.

## Verificación de esta adaptación

- TypeScript: `npx tsc --noEmit`, correcto.
- Lint: `npm run lint`, correcto.
- Exportación web: `npx expo export --platform web`, correcta.
- No se verificó en dispositivo físico ni se envió un pedido real.

El repositorio original fue clonado en el commit `0a6d985785726d0bd683df54dbcd34bbedac5b9e`. Los cambios son locales; no se modificó el repositorio remoto.
- Exportación del bundle Android: correcta; esto no genera un APK instalable.


## Actualización SDK 58

Dependencias alineadas con Expo SDK 58 y React Native 0.88.1 para Expo Go 58. Expo Doctor: 20/20 controles correctos. TypeScript y lint correctos. Para reiniciar tras actualizar: detener Metro con Ctrl+C y ejecutar npm start -- --clear.


## Diseño y pruebas de pedidos

Fotos ilustrativas tomadas del menú de referencia indicado por el usuario: https://pedir.tucan.la/menu/burgersur/Delivery. Se guardan en assets/images/menu para cargar también sin depender del servidor de imágenes. Estas fotos son del comercio de referencia y deben reemplazarse por las fotos definitivas del negocio al preparar su lanzamiento.

WhatsApp de pruebas configurado: +54 9 11 2483-0571. Puede sobrescribirse con EXPO_PUBLIC_WHATSAPP en .env; reiniciar Metro tras modificarlo. El botón abre un mensaje preparado y no lo envía automáticamente.

## Papas, bebidas y localización

- Dos videos locales de papas con reproductor a demanda y controles (sin reproducción en segundo plano).
- Cuatro latas de 354 ml: Coca-Cola, Coca-Cola Sin Azúcar, Sprite y Fanta Naranja; botellas de Coca-Cola y Sprite de 1,25 L. Precios de ejemplo.
- En delivery: permiso de ubicación solo al tocar Usar mi ubicación actual, GPS con tiempo límite, dirección aproximada en Android/iOS si está disponible, revisión manual, precisión aproximada y enlace al mapa. La dirección manual sigue disponible sin permiso. Editar la dirección quita el punto GPS anterior para evitar asociar una ubicación vieja. La dirección y el enlace al mapa se incluyen únicamente en pedidos de delivery preparados para WhatsApp.
- En navegador, la ubicación necesita HTTPS o localhost; el autocompletado de calle por GPS se realiza solo en Android/iOS. La ubicación real debe verificarse en un teléfono.

Fuentes de medios ilustrativos:
- Videos: krzys16, https://pixabay.com/videos/french-fries-potatoes-fried-tasty-28019/ y Engin Akyurt, https://pixabay.com/videos/potato-fries-french-fries-frying-72025/ (Pixabay Content License).
- Papas clásicas: Nils B, https://unsplash.com/photos/a-bowl-of-french-fries-and-sauce-on-a-wooden-table-x-lXO1C1NCA
- Papas cheddar: https://www.chefstore.com/p/glenview-farms-feather-shred-mild-cheddar-cheese_2115336/
- Bebidas: catálogos públicos de https://www.supertop.com.ar/ y https://www.disco.com.ar/; fotos de productos ilustrativas.

Verificación: TypeScript y lint correctos; exportaciones web y Android correctas. En navegador se verificaron filtros, reproducción efectiva del video, carrito, total de delivery y aparición de búsqueda en mapa al escribir una dirección. GPS y geocodificación inversa requieren prueba en teléfono. Clips comprimidos: 2,60 MB + 1,25 MB.

