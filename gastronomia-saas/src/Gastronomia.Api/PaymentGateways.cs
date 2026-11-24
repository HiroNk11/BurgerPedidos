using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gastronomia.Application;
namespace Gastronomia.Api;

public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    public Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest request, CancellationToken ct) => throw new PaymentGatewayUnavailableException("Configurar un adaptador de pasarela antes de recibir pagos reales.");
    public Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest request, CancellationToken ct) => throw new PaymentGatewayUnavailableException("Los pagos online todavía no están habilitados.");
}
// Protocolo EXCLUSIVO de pruebas locales. No representa el protocolo de Mercado Pago ni Stripe.
public sealed class DevelopmentHmacPaymentGateway : IPaymentGateway
{
    private readonly byte[] key;
    private readonly TimeProvider clock;
    public DevelopmentHmacPaymentGateway(IConfiguration config, IWebHostEnvironment env, TimeProvider clock)
    {
        if (!env.IsDevelopment()) throw new InvalidOperationException("El adaptador simulado solo se permite en Development.");
        this.clock = clock;
        try { key = Convert.FromBase64String(config["Payments:DevelopmentSigningKey"] ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Configurar Payments__DevelopmentSigningKey en Base64."); }
        if (key.Length < 32) throw new InvalidOperationException("La clave de firma de pruebas requiere al menos 32 bytes.");
    }
    public Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest r, CancellationToken ct) => Task.FromResult(new PaymentIntentResult("Development", "local_" + r.PaymentId.ToString("N")));
    public Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest request, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("X-Payment-Timestamp", out var timestamp) || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || Math.Abs((decimal)clock.GetUtcNow().ToUnixTimeSeconds() - seconds) > 300 || !request.Headers.TryGetValue("X-Payment-Signature", out var signature)) throw new InvalidPaymentSignatureException();
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); } catch (FormatException) { throw new InvalidPaymentSignatureException(); }
        var prefix = Encoding.UTF8.GetBytes(timestamp + "."); var data = new byte[prefix.Length + request.Body.Length];
        prefix.CopyTo(data, 0); request.Body.Span.CopyTo(data.AsSpan(prefix.Length));
        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, data), supplied)) throw new InvalidPaymentSignatureException();
        DevelopmentEvent? e;
        try { e = JsonSerializer.Deserialize<DevelopmentEvent>(request.Body.Span, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { throw new BadHttpRequestException("Notificación inválida."); }
        if (e is null) throw new BadHttpRequestException("Notificación inválida.");
        return Task.FromResult(new VerifiedPaymentEvent(e.TenantId, "Development", e.EventId, e.TransactionId, e.Status, e.Amount, e.Currency, Convert.ToHexString(SHA256.HashData(request.Body.Span))));
    }
    private sealed record DevelopmentEvent(Guid TenantId, string EventId, string TransactionId, string Status, decimal Amount, string Currency);
}
