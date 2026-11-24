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

Hospedada con Sites, versión 1. El manifiesto `.openai/hosting.json` conserva la identidad del sitio para futuras actualizaciones. No contiene claves. Los cambios requieren compilar, sincronizar el repositorio de fuente de Sites y publicar una nueva versión; un push a este repositorio de GitHub no modifica automáticamente la demo pública.
