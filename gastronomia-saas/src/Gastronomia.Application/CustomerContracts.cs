using System.ComponentModel.DataAnnotations;
using Gastronomia.Domain;
namespace Gastronomia.Application;

public sealed record RequestOtpRequest(Guid TenantId, [Required, EmailAddress, MaxLength(254)] string Email);
public sealed record VerifyOtpRequest(Guid TenantId, [Required, EmailAddress, MaxLength(254)] string Email, [Required, RegularExpression("^[0-9]{6}$")] string Codigo);
public sealed record RequestOtpResult(string Message, int ExpiresInSeconds = 600, int RetryAfterSeconds = 60);
public sealed record AccessTokenResult(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
public sealed record CustomerLoginResult(AccessTokenResult Token, PerfilCliente Cliente, bool NuevoUsuario, int PedidosVinculados);
public sealed record OtpEmail(Guid TenantId, Guid MessageId, string Email, string Codigo, DateTimeOffset ExpiraUtc);
public interface IEmailService { Task EnviarOtpAsync(OtpEmail email, CancellationToken ct); }
public interface IOtpCodeHasher { byte[] Hash(Guid tenantId, string normalizedEmail, Guid messageId, string code); }
public interface IAccessTokenIssuer { AccessTokenResult Issue(Usuario user); }
public interface IPasswordlessService
{
    Task<RequestOtpResult> RequestOtpAsync(string email, CancellationToken ct = default);
    Task<CustomerLoginResult> VerifyOtpAsync(string email, string codigo, CancellationToken ct = default);
}
public sealed class InvalidOtpException() : Exception("Código inválido o vencido. Solicitá otro código si agotaste los intentos.");

public sealed record PerfilRequest([Required, MaxLength(100)] string Nombre, [Required, RegularExpression(@"^\+?[0-9]{8,15}$")] string Telefono);
public sealed record DireccionRequest([Required, MaxLength(180)] string Calle, [Required, MaxLength(20)] string Numero, [MaxLength(60)] string? PisoDepto = null, double? Latitud = null, double? Longitud = null);
public sealed record DireccionResult(Guid Id, string Calle, string Numero, string? PisoDepto, double? Latitud, double? Longitud);
public sealed record PerfilCliente(Guid Id, string Email, string Nombre, string Telefono, IReadOnlyList<DireccionResult> Direcciones);
public interface ICustomerAccountService
{
    Task<PerfilCliente> GetAsync(Guid userId, CancellationToken ct = default);
    Task<PerfilCliente> UpdateAsync(Guid userId, PerfilRequest request, CancellationToken ct = default);
    Task<DireccionResult> SaveAddressAsync(Guid userId, Guid? addressId, DireccionRequest request, CancellationToken ct = default);
    Task DeleteAddressAsync(Guid userId, Guid addressId, CancellationToken ct = default);
}

public sealed record PedidoCliente(Guid Id, string ShortCode, DateTimeOffset Fecha, EstadoPedido EstadoPedido, EstadoPago EstadoPago, decimal Total, string Moneda, TipoEntrega TipoEntrega, string? DireccionEntrega, string? Notas, IReadOnlyList<ItemResult> Items, MetodoPago? MetodoPago = null);
public sealed record MisPedidosResult(IReadOnlyList<PedidoCliente> Activos, int TotalActivos, IReadOnlyList<PedidoCliente> Historicos, int TotalHistoricos, int Pagina, int Tamanio);
public sealed record ReorderItem(Guid ProductId, string Nombre, int Cantidad, decimal PrecioUnitarioActual, decimal Subtotal, decimal PrecioUnitarioHistorico);
public sealed record ReorderWarning(string Code, Guid? ProductId, string Message);
public sealed record ReorderRemovedItem(Guid ProductId, string NombreHistorico, int Cantidad, string Motivo);
public sealed record CarritoPropuesto(Guid PedidoOrigenId, IReadOnlyList<ReorderItem> Items, IReadOnlyList<ReorderWarning> Warnings, IReadOnlyList<ReorderRemovedItem> ItemsRemovidos, decimal SubtotalProductos, string Moneda, bool RequiereCotizacion = true);
public interface ICustomerOrdersService
{
    Task<MisPedidosResult> HistoryAsync(Guid userId, int pagina = 1, int tamanio = 20, CancellationToken ct = default);
    Task<PedidoCliente> DetailAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<CarritoPropuesto> ReorderAsync(Guid userId, Guid orderId, CancellationToken ct = default);
}
