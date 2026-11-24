# Demo pública de BurgerPedidos

**Abrir:** https://burgerpedidos-demo.nicogiron09.chatgpt.site

Demo interactiva, independiente del backend y del build Expo, pensada para presentaciones comerciales. Reproduce los principales recorridos con imágenes del proyecto y datos ficticios. Funciona en web de escritorio y celular sin iniciar sesión.

## Recorrido de presentación (2 minutos)

1. En **Vista cliente**, agregar una burger y unas papas al carrito.
2. Aplicar **DEMO20**, elegir retiro o delivery y continuar.
3. Elegir efectivo, transferencia o Mercado Pago simulado y crear el pedido.
4. Abrir **Panel del local** y aceptar el pedido. Registrar el cobro de muestra si corresponde.
5. Avanzar a preparación, reparto y entrega. Volver a la vista cliente para ver el seguimiento.
6. Revisar **Buzón demo**, **Mis pedidos**, favoritos y **Volver a pedir**.
7. Usar **Reiniciar demo** antes de otra presentación.

La transferencia utiliza un comprobante ficticio; no admite documentos reales. El botón de Mercado Pago representa un webhook aprobado, sin contactar a la pasarela. WhatsApp es una vista previa de texto y los emails solo se muestran en el buzón demo. Aceptar un pedido nunca marca el pago como confirmado.

## Aislamiento y límites

Los cambios se guardan en localStorage con la clave `burgerclub-public-demo-v1`, únicamente en el navegador del visitante. Se sincronizan entre pestañas del mismo navegador y origen, pero no entre dispositivos ni usuarios. No se recopilan datos personales, no se generan pedidos reales y no hay credenciales ni acceso al panel de producción. La cuenta Alex y la dirección son ficticias. Se limita a 100 pedidos por navegador; se puede reiniciar.

No prueba la API .NET, OTP real, concurrencia SQL, carga real de comprobantes ni cobros de Mercado Pago. Es una demostración comercial; esas integraciones permanecen en los módulos originales del repositorio.

## Ejecutar y verificar

No requiere instalar dependencias:

```sh
cd demo
npm test
npm run build
python -m http.server 8094 --directory dist
```

Abrir http://localhost:8094. Fuente modular en `domain.js` y `app.js`; estilos responsive en `style.css`. Las fuentes usan Google Fonts con alternativa local Arial.

Validación: 11 pruebas del dominio aprobadas. Pruebas visuales de compra con cupón y delivery, transición hasta entrega, confirmación simulada de pasarela, transferencia y comprobante, reorder, persistencia y reset. Revisión responsive a 390 × 844. Compra y aceptación también verificadas en el enlace público.

## Publicación

Hospedada con Sites, versión 2, con tema oscuro en tienda, checkout, perfil, seguimiento, buzón y panel. El manifiesto `.openai/hosting.json` conserva la identidad del sitio para futuras actualizaciones. No contiene claves. Los cambios requieren compilar, sincronizar el repositorio de fuente de Sites y publicar una nueva versión; un push a este repositorio de GitHub no modifica automáticamente la demo pública.


## Alojamiento alternativo y copia portable

La demo es estática y no necesita la API, SQL Server ni Mercado Pago para funcionar. Se puede mover sin cambiar su lógica. Una visita y una compra de prueba no generan cobros.

- **Cloudflare Pages Free:** alternativa recomendada para compartir mediante una URL pages.dev, sin publicar el repositorio privado. Admite subir directamente una carpeta o ZIP con los archivos compilados. Su plan gratuito no anuncia una fecha de fin; las condiciones y los límites del proveedor pueden cambiar. No se contrató ni creó una cuenta de Cloudflare en esta tarea.
- **GitHub Pages:** disponible gratis desde repositorios públicos. Para usarlo sin publicar el SaaS, crear un repositorio separado que contenga únicamente la demo. Los repositorios privados requieren un plan compatible. No se cambió la visibilidad de BurgerPedidos.
- **Presentación local:** compilar y ejecutar el servidor estático indicado arriba. Funciona mientras la PC y el servidor estén encendidos; no es una URL pública permanente. Los archivos no tienen temporizador ni fecha de vencimiento. Las fuentes remotas tienen alternativas locales.

El ZIP de distribución contiene `index.html`, JavaScript, CSS e imágenes en la raíz, listo para Direct Upload. No contiene el proyecto .NET, credenciales, datos de clientes, historial Git ni manifiesto de Sites. Descomprimir y ejecutar `python -m http.server 8094 --bind 127.0.0.1` desde esa carpeta para mostrarlo localmente; abrir http://localhost:8094. No abrir index.html por file:// porque usa módulos JavaScript.

Publicación alternativa: ingresar a una cuenta propia de Cloudflare; abrir Workers & Pages, crear una aplicación de Pages con Direct Upload, subir el ZIP y publicar en el plan Free. No hace falta un dominio comprado. Un despliegue Direct Upload se actualiza con otra carga; la integración Git automática requiere otro proyecto. No se migró todavía el sitio de Sites a Cloudflare.

Fuentes oficiales consultadas el 24/11/2026:
- https://www.cloudflare.com/products/pages/
- https://developers.cloudflare.com/pages/get-started/direct-upload/
- https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site

Ningún proveedor permite garantizar gratuidad o disponibilidad para siempre. Conservar una copia de los archivos permite cambiar de alojamiento.
