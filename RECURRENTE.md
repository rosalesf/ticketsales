# Pagos con tarjeta mediante Recurrente

La aplicación usa pagos únicos en GTQ y tarjeta de contado. No almacena números de tarjeta ni CVV. El formulario utiliza `recurrente-checkout@0.0.5`, publicado y enlazado por el repositorio oficial. La guía de Embedded Checkouts indica otro paquete (`@recurrente/checkout`) que devolvió 404 en npm durante esta implementación.

## Activación

Configurar en el entorno del servidor, sin guardar secretos en el repositorio:

| Variable de entorno | Valor |
| --- | --- |
| `Recurrente__SecretKey` | Llave secreta TEST del Sandbox de Recurrente |
| `Recurrente__WebhookSecret` | Secreto `whsec_...` del endpoint de webhooks de ese Sandbox |
| `Recurrente__PublicBaseUrl` | URL HTTPS pública de la aplicación, sin barra final |

Los campos vacíos de `appsettings.json` son intencionales: el botón de pago permanece deshabilitado hasta completar la configuración. No pegar credenciales en mensajes ni en JavaScript. La llave secreta determina TEST o LIVE; no se necesita una llave pública.

1. Publicar la aplicación en HTTPS o usar un túnel HTTPS hacia la instancia de desarrollo. La URL debe llegar a esta misma aplicación y base de datos.
2. En el Sandbox de Recurrente, registrar `https://TU-DOMINIO/webhooks/recurrente` y guardar su signing secret. Suscribirse a `intent.succeeded`, `intent.failed`, `intent.pending` e `intent.canceled`.
3. Configurar las tres variables y reiniciar la aplicación. No activar cobros LIVE durante las pruebas.
4. Configurar también `AzureCommunicationServices__ConnectionString` y `AzureCommunicationServices__SenderAddress` para el correo con QR.
5. Mantener el proceso web activo (por ejemplo, Always On en el hospedaje). El trabajador de recuperación consulta órdenes pendientes y reintenta correos cada 30 segundos. Tras un reinicio retoma el estado persistido en SQL Server.

Al iniciar, `PaymentSchema.EnsureCreated` agrega las columnas e índices de pagos a `Orders` de forma compatible con el mecanismo de actualización existente. El usuario SQL necesita permisos para esos cambios de esquema. El código fue compilado; no se inició la aplicación contra su base de datos habitual como parte de las pruebas.

## Flujo y recuperación

- El servidor valida precios, evento y disponibilidad, crea la orden y descuenta temporalmente las entradas dentro de una transacción. Un token por checkout evita duplicados por doble envío; las actualizaciones condicionales impiden vender la última entrada a dos compradores.
- Persiste el cuerpo exacto del request y usa `OrderNumber` como `Idempotency-Key` al crear la sesión. Un timeout se reintenta con el mismo cuerpo y la misma llave, sin generar otra orden.
- El checkout vence según `Recurrente:ReservationMinutes` (20 minutos por defecto). Las reservas con checkout publicado se liberan **solo cuando la API devuelve `expired`**; un fallo, cierre del navegador o timeout no libera inventario mientras todavía se pueda pagar. Si Recurrente conserva `payment_in_progress`, se conserva la reserva hasta conocer el resultado.
- Los webhooks se verifican sobre el cuerpo original con HMAC-SHA256, comparación constante y tolerancia de cinco minutos. Mantener sincronizado el reloj del servidor.
- Un webhook válido dispara una consulta autenticada del checkout. Solo se confirma si su ID, total en centavos y moneda coinciden. Los callbacks del navegador únicamente actualizan mensajes.
- La transición a `Approved` es irreversible por eventos repetidos o fuera de orden. Convierte la reserva en venta sin descontar nuevamente y permite mostrar el QR.
- Un pago con monto o moneda diferentes, o recibido después de liberar inventario, queda en `PaymentReview`, sin emitir QR. Requiere revisar el cobro y resolverlo con el comprador; no se reembolsa automáticamente.
- El trabajador reintenta correos aprobados pendientes de envío. Un reinicio justo después de que Azure acepte el correo y antes de guardar el marcador puede repetir el correo; no repite el cobro ni la venta.
- Las páginas y consultas de la orden requieren el token privado o la cuenta propietaria. Los enlaces con token son privados y no deben compartirse. Las órdenes antiguas de invitados que no tenían token ya no son accesibles solamente con su ID numérico.

## Verificación

Pruebas automatizadas, sin contactar a Recurrente ni usar la base de datos de la aplicación:

```powershell
dotnet run --project Tests/PaymentChecks.csproj
```

Requieren SQL Server LocalDB (`MSSQLLocalDB`). Crean una base con nombre aleatorio `TicketSalesPaymentChecks_...`, archivos dentro de `Tests/.data`, y la eliminan al finalizar. Verifican firmas, autenticación de API, reintentos, inventario concurrente, duplicados, expiración y conciliación de montos. Usan una pasarela falsa: no sustituyen la validación con un Sandbox real.

Pruebas pendientes en el Sandbox:

1. Comprar con tarjeta TEST `4242 4242 4242 4242`: comprobar aprobación, un único descuento de inventario, QR y correo.
2. Usar `4000 0000 0000 0002`: comprobar rechazo y reintento en la misma sesión.
3. Cerrar el navegador antes del webhook: comprobar confirmación y correo desde el servidor.
4. Reenviar el webhook: verificar que no duplique la venta.
5. Abandonar el checkout: comprobar que Recurrente lo marque `expired` y se libere la reserva.
6. Detener y reiniciar la aplicación con órdenes pendientes: comprobar recuperación.

Para activar producción, completar estas pruebas y reemplazar tanto la llave TEST como el webhook y su secreto por los de LIVE. Usar una cuenta apta para procesar el volumen previsto.

## Túnel local de prueba

Se preparó Cloudflare Quick Tunnel con el ejecutable portable en `.tunnel/cloudflared.exe`. La instancia escucha solo en `127.0.0.1:5117`, utiliza el ambiente `Tunnel` y acepta los encabezados del proxy local para reconocer HTTPS. Los ajustes locales de cifrado y administrador se pasan al proceso sin imprimirlos ni duplicarlos en el archivo del túnel.

```powershell
# Iniciar (si no está iniciado)
dotnet build TicketSales.csproj --no-restore -p:OutputPath=bin/TunnelTest/
pwsh -NoProfile -File Scripts/Start-TestTunnel.ps1

# Detener la instancia de prueba y el túnel
pwsh -NoProfile -File Scripts/Stop-TestTunnel.ps1
```

La dirección vigente y los identificadores de proceso se guardan en `.tunnel/state.json`. `appsettings.Tunnel.json` guarda la URL pública y los hosts permitidos. Los logs están en `.tunnel/`. Esta dirección es temporal: al cerrar el túnel deja de funcionar y, al crear otro, será necesario actualizar el endpoint en Recurrente. Mantener la computadora encendida durante las pruebas.

## Referencias oficiales

- [Checkout embebido](https://docs.recurrente.com/guias-espanol/guias/embedded-checkouts)
- [Repositorio oficial del formulario](https://github.com/recurrente/recurrente-checkout)
- [Crear checkout](https://docs.recurrente.com/referencia-api/api-reference/checkouts/create-checkout)
- [Webhooks y firmas](https://docs.recurrente.com/guias-espanol/comenzar/webhooks)
- [Autenticación](https://docs.recurrente.com/guias-espanol/comenzar/autenticacion)
