namespace Gastronomia.Application;

public sealed record GatewayWebhookRequest(ReadOnlyMemory<byte> Body, IReadOnlyDictionary<string,string> Headers, IReadOnlyDictionary<string,string> Query);
// El adaptador autentica la notificación y consulta al proveedor para obtener estos valores.
public sealed record VerifiedPaymentEvent(Guid TenantId, string Proveedor, string EventoId, string TransaccionId, string Estado, decimal Importe, string Moneda, string PayloadHash, Guid? PaymentId = null, Guid? OrderId = null, string? PreferenceId = null);
public sealed record PaymentReconciliationResult(bool Duplicate, string Resultado);
public sealed record PaymentCheckoutItem(string Id, string Nombre, int Cantidad, decimal PrecioUnitario);
public sealed record PaymentIntentRequest(Guid TenantId, Guid PaymentId, Guid OrderId, decimal Importe, string Moneda, IReadOnlyList<PaymentCheckoutItem>? Items = null);
public sealed record PaymentIntentResult(string Proveedor, string? TransaccionId, string? PreferenceId = null, string? CheckoutUrl = null);
public interface IPaymentGateway
{
    Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest request, CancellationToken ct);
    // PaymentId es la clave de idempotencia del proveedor; nunca crear dos cobros al reintentar.
    Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest request, CancellationToken ct);
}
public interface IPaymentReconciler
{
    Task<PaymentReconciliationResult> ReconcileAsync(VerifiedPaymentEvent notification, CancellationToken ct = default);
}
public interface IOnlinePaymentsService
{
    Task<PaymentIntentResult> StartAsync(Guid usuarioId, Guid orderId, CancellationToken ct = default);
}
public sealed class PaymentGatewayUnavailableException(string message) : Exception(message);
public sealed class InvalidPaymentSignatureException() : Exception("Notificación no autenticada.");

public sealed class IgnoredPaymentNotificationException(string message) : Exception(message);
