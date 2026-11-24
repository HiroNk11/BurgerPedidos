using System.ComponentModel.DataAnnotations;
using Gastronomia.Domain;
namespace Gastronomia.Application;

public sealed record CrearItemRequest([Required] Guid ProductId, [Range(1, 1000)] int Cantidad);
public sealed record CrearPedidoRequest(
 Guid RequestId,
 [Required, EmailAddress, MaxLength(254)] string GuestEmail,
 [Required, RegularExpression(@"^\+?[0-9]{8,15}$")] string GuestPhone,
 MetodoPago MetodoPago,
 [Required, MinLength(1), MaxLength(100)] List<CrearItemRequest> Items,
 [MaxLength(40)] string? CouponCode = null,
 [MaxLength(100)] string NombreCliente = "Invitado",
 TipoEntrega TipoEntrega = TipoEntrega.Retiro,
 [MaxLength(400)] string? DireccionEntrega = null,
 double? Latitud = null, double? Longitud = null,
 [MaxLength(500)] string? Notas = null,
 [RegularExpression("^[A-Fa-f0-9]{64}$")] string? CheckoutToken = null,
 decimal? TotalEsperado = null);
public sealed record ItemResult(Guid ProductId, string Nombre, int Cantidad, decimal PrecioUnitarioHistorico, decimal DescuentoAplicado, decimal Subtotal);
public sealed record PedidoResult(Guid Id, string ShortCode, DateTimeOffset Fecha, EstadoPedido EstadoPedido, EstadoPago EstadoPago, decimal Bruto, decimal Descuento, decimal Total, string Moneda, decimal Envio, Guid PaymentId, IReadOnlyList<ItemResult> Items, string MensajeWhatsApp, string WhatsAppUrl);
public sealed record HistorialPedido(Guid Id, string ShortCode, DateTimeOffset Fecha, EstadoPedido EstadoPedido, EstadoPago EstadoPago, decimal Total, string Moneda);
public sealed record CotizacionResult(decimal Bruto,decimal Descuento,decimal Envio,decimal Total,string Moneda, string? Promocion = null, string? CodigoCupon = null)
{ public decimal Subtotal => Bruto; public decimal DescuentoAplicado => Descuento; }
public interface IOrderService
{
    Task<CotizacionResult> CotizarAsync(CrearPedidoRequest request,CancellationToken ct=default, Guid? usuarioId=null);
    Task<PedidoResult> CrearPedidoAsync(CrearPedidoRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<PedidoResult> AceptarPedidoAsync(Guid orderId, bool iniciarPreparacion = false, CancellationToken ct = default);
    Task<PedidoResult> CambiarEstadoAsync(Guid orderId, EstadoPedido estado, CancellationToken ct = default);
    Task<int> VincularPedidosInvitadoAsync(string email, Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<HistorialPedido>> HistorialAsync(Guid usuarioId, int pagina = 1, int tamanio = 20, CancellationToken ct = default);
    Task ConfirmarPagoManualAsync(Guid orderId, Guid paymentId, Guid operadorId, decimal importeRecibido, CancellationToken ct = default);
}
public sealed class TenantContext { public Guid TenantId { get; set; } }
public sealed class ConflictException(string message) : Exception(message);
public sealed class MissingException(string message) : Exception(message);
