namespace Gastronomia.Domain;

public enum EstadoPedido { PendienteConfirmacion = 0, Aceptado = 1, EnPreparacion = 2, EnCamino = 3, Entregado = 4, Cancelado = 5 }
public enum EstadoPago { Pendiente = 0, ComprobanteEnviado = 1, Pagado = 2, Rechazado = 3 }
public enum MetodoPago { Efectivo = 0, TransferenciaCBU = 1, PasarelaOnline = 2 }
public enum TipoPromocion { PorFecha = 0, PorDiaSemana = 1, PorHorario = 2, Cupon = 3 }
public enum TipoDescuento { Porcentaje = 0, ImporteFijo = 1 }
public sealed class DomainException(string message) : Exception(message);
public interface ITenantEntity { Guid Id { get; } Guid TenantId { get; } byte[] Version { get; } }
public static class EmailKey
{
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
public static class Money
{
    public const decimal Max = 9999999999999999.99m;
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static void Validate(decimal value) { if (value < 0 || value > Max || Round(value) != value) throw new DomainException("Importe inválido; se requieren hasta dos decimales."); }
}

public enum TipoEntrega { Retiro = 0, Delivery = 1 }
