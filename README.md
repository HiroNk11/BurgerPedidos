# BurgerPedidos

Plataforma gastronómica con tienda web/móvil en React Native y Expo, API en ASP.NET Core .NET 10, Entity Framework Core y SQL Server, y panel del negocio en React.

## Estructura

- `comercio-pedidos-mobile/`: storefront Expo y web, OTP, perfil, direcciones, historial, reorder, favoritos, promociones y seguimiento de pedidos.
- `gastronomia-saas/`: API, dominio, infraestructura, esquema SQL, pruebas, panel y Docker.

## Guías

- [Aplicación cliente](comercio-pedidos-mobile/README.md)
- [Backend](gastronomia-saas/README.md)
- [Panel del negocio](gastronomia-saas/PANEL.md)
- [Mercado Pago y Docker](gastronomia-saas/FASE4-MERCADOPAGO-DOCKER.md)
- [Checkout y retorno en Expo](comercio-pedidos-mobile/PAGOS-ONLINE-EXPO.md)

## Configuración

Instalar .NET SDK 10, Node.js y SQL Server (o Docker). Consultar las guías de cada módulo antes de iniciar. Las plantillas `.env.example` y `.env.template` no contienen secretos; completar la configuración local fuera de Git. No se incluyen bases de datos, comprobantes, buzones de correo ni credenciales.

## Estado de pagos

Integración Mercado Pago implementada. El entorno local usa pagos deshabilitados hasta configurar credenciales y URLs HTTPS. Falta validar el checkout sandbox en un celular con un development build instalado; Expo Go no registra el esquema personalizado.

## Verificación del cliente

```sh
cd comercio-pedidos-mobile
npm ci
npx tsc --noEmit
npm run lint
npm run test:state
```

Este repositorio contiene la adaptación gastronómica actual, sin el historial ni los módulos del proyecto retail de referencia.
