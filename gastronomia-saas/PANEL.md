# Panel del negocio — primera versión operativa

## Qué permite

- Iniciar/cerrar sesión en el entorno local de desarrollo con contraseña aleatoria generada, almacenada como hash PBKDF2 mediante PasswordHasher.
- Ver pedidos desde SQL Server, paginados y ordenados por fecha, con filtros de estado, pago, rango de fechas y búsqueda por código/email/teléfono.
- Consultar el detalle histórico: productos, precios, descuentos, total, contactos e intentos de pago.
- Aceptar, pasar a preparación, marcar en camino, entregar y cancelar cuando las reglas de dominio lo permiten.
- Revisar el enlace al comprobante cuando existe, y registrar efectivo o transferencia solo después de declarar el importe recibido y confirmar la verificación manual.
- Ver indicadores de pedidos por aceptar, cocina, pagos por revisar y cobrado total del historial. Los indicadores no se limitan a la página ni a los filtros de la tabla; sus etiquetas indican su alcance.
- Recibir cambios en vivo mediante SSE, con respaldo cada 15 segundos mientras el panel esté visible y sin una ficha abierta. Cerrar sesión también está disponible en el encabezado para pantallas pequeñas.

## Acceso en este equipo

Se preparó una base **nueva** `BurgerClubPanelDev_<identificador>` con seis pedidos ficticios, productos de ejemplo, un cupón y un operador local. Los datos de acceso están en el archivo `outputs/Panel-acceso-local.md`, fuera del código fuente. El usuario es admin@burgerclub.local; la contraseña no es fija ni está incorporada al frontend.

URL: http://localhost:5180/admin/

El script `outputs/Iniciar-panel.ps1` inicia la API. Si faltan la configuración y los archivos de frontend, prepara la base de demostración y compila el panel. Puede ejecutarse con `-Recompilar` después de modificar React. Mantener abierta esa terminal mientras se usa el panel; Ctrl+C lo detiene.

El acceso y los pedidos son de desarrollo. El comprobante de uno de los pedidos es una imagen marcada como DEMO SIN VALIDEZ; no acredita un ingreso bancario. La prueba en pantalla aceptó y marcó como pagado uno de los pedidos ficticios; esos cambios permanecen guardados en la base local.

## Preparar otro equipo

Desde gastronomia-saas:

```powershell
dotnet restore Gastronomia.slnx
dotnet build Gastronomia.slnx --no-restore
cd admin
npm ci
npm run build
cd ..
dotnet run --project tools/Gastronomia.Dev -- . ./acceso-local.md
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/Gastronomia.Api --no-launch-profile --urls http://localhost:5180
```

La preparación requiere SQL Server LocalDB en Windows y crea una base nueva. Si ya existe appsettings.Development.local.json, se detiene: nunca borra ni reinicializa la base existente. El archivo de acceso contiene una contraseña de prueba y no debe compartirse. Para otro SQL Server, adaptar el inicializador; el resto de la solución sigue usando ConnectionStrings:SqlServer.

## Arquitectura y protección del acceso

`StaffOrdersController` expone consultas de administración protegidas por la política Staff. Las mutaciones siguen usando `OrderService`: no se implementó otro camino para alterar estados o importes. El registro de pago ahora exige `importeRecibido` y el servidor valida que coincida con el intento y el total del pedido, incluso si se manipula la interfaz.

El acceso local solo se registra con Development:LocalStaff:Enabled y ASPNETCORE_ENVIRONMENT=Development. En producción esa configuración causa un error de arranque; se mantiene JWT/OIDC y la verificación de tenant_id. El formulario local no sustituye la integración de inicio de sesión con un proveedor real para el panel de producción.

Cookies HttpOnly, SameSite Strict, vencimiento de ocho horas, tokens antiforgery en login/logout/mutaciones, límite de intentos de login y ausencia de CORS abierto. El panel y la API comparten origen. En desarrollo se permite HTTP en localhost; en producción se mantiene redirección HTTPS. Las claves de protección de cookies se guardan en App_Data/keys y en Windows están protegidas con DPAPI.

Los archivos appsettings.Development.local.json y App_Data están excluidos de Git y del ZIP. No contienen contraseñas en claro; el archivo de acceso externo es el único que muestra la contraseña aleatoria para esta demostración. No hay cuentas hardcodeadas con una contraseña universal.

## Construcción y pruebas

- .NET: build correcto, sin errores ni advertencias.
- React/TypeScript: `npm run build`, correcto.
- 47 verificaciones de dominio y SQL Server, incluyendo el rechazo de un importe recibido distinto.
- 8 verificaciones HTTP: login, datos privados, comercio ajeno, CSRF, resumen, filtro de cobros, logout y validación de DTO inválido (400).
- Prueba manual en navegador: aceptación persistida, comprobante visible como enlace, rechazo de importe erróneo, cobro ficticio correcto y actualización de indicadores sin alterar automáticamente el estado del pedido.

```powershell
dotnet run --project tests/Gastronomia.Checks -- --sql sql
python tests/verify-panel.py http://localhost:5180 RUTA_AL_ARCHIVO_DE_ACCESO.md
```

El test HTTP espera la base de seis pedidos ficticios. No envía mensajes ni accede a bancos. Usa y cierra su propia sesión; no comparte cookies con el navegador.

## Pendiente

Expo ya crea pedidos en esta API, sube comprobantes al almacenamiento privado local y genera correos capturados en el buzón. Quedan para producción la identidad del cliente en la app, storage externo, activación de Resend, pagos/webhooks, expiración de reservas y auditoría completa. La imagen no verifica automáticamente el ingreso bancario. No se realizaron cobros ni transferencias reales. Ver CHECKOUT.md.
