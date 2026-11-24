using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gastronomia.Application;
using Gastronomia.Domain;
namespace Gastronomia.Infrastructure;

public sealed class MercadoPagoGateway : IPaymentGateway
{
    private const string Provider = "MercadoPago";
    private readonly HttpClient http;
    private readonly MercadoPagoOptions options;
    private readonly TimeProvider clock;
    public MercadoPagoGateway(HttpClient http, MercadoPagoOptions options, TimeProvider clock)
    {
        options.Validate(); this.http = http; this.options = options; this.clock = clock;
    }
    public async Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest request, CancellationToken ct)
    {
        if (request.TenantId != options.TenantId) throw new DomainException("El vendedor configurado no corresponde a este comercio.");
        if (request.PaymentId == Guid.Empty || request.OrderId == Guid.Empty || request.Importe <= 0 || request.Moneda != "ARS" || request.Items is null || request.Items.Count == 0)
            throw new DomainException("El checkout de Mercado Pago requiere un pedido ARS con importe positivo e ítems.");
        Money.Validate(request.Importe);
        foreach (var item in request.Items)
        {
            Money.Validate(item.PrecioUnitario);
            if (item.Cantidad <= 0 || item.PrecioUnitario <= 0 || string.IsNullOrWhiteSpace(item.Nombre) || item.Nombre.Length > 256) throw new DomainException("Ítem de checkout inválido.");
        }
        if (request.Items.Sum(i => i.Cantidad * i.PrecioUnitario) != request.Importe) throw new DomainException("Los ítems no coinciden con el total del pedido.");
        var notification = new UriBuilder(options.NotificationUrl);
        notification.Query = string.IsNullOrEmpty(notification.Query) ? "source_news=webhooks" : notification.Query.TrimStart('?') + "&source_news=webhooks";
        using var message = Message(HttpMethod.Post, "checkout/preferences");
        message.Headers.Add("X-Idempotency-Key", request.PaymentId.ToString("D"));
        message.Content = JsonContent.Create(new
        {
            items = request.Items.Select(i => new { id = i.Id, title = i.Nombre, quantity = i.Cantidad, unit_price = i.PrecioUnitario, currency_id = request.Moneda }),
            external_reference = request.OrderId.ToString("D"),
            metadata = new { tenant_id = request.TenantId.ToString("D"), order_id = request.OrderId.ToString("D"), payment_id = request.PaymentId.ToString("D") },
            notification_url = notification.Uri.AbsoluteUri,
            back_urls = new { success = options.SuccessUrl, failure = options.FailureUrl, pending = options.PendingUrl },
            auto_return = "approved"
        });
        using var document = await SendAsync(message, ct);
        var pref = document.RootElement;
        Require(Text(pref, "collector_id") == options.CollectorId, "La preferencia pertenece a otro vendedor.");
        Require(Guid.TryParse(Text(pref, "external_reference"), out var reference) && reference == request.OrderId && pref.TryGetProperty("metadata", out var meta) && Text(meta, "tenant_id") == request.TenantId.ToString("D") && Text(meta, "payment_id") == request.PaymentId.ToString("D"), "La preferencia devuelta no corresponde al intento solicitado.");
        var id = Identifier(pref, "id");
        var checkout = Text(pref, options.Sandbox ? "sandbox_init_point" : "init_point");
        if (!Uri.TryCreate(checkout, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || !(uri.Host == "mercadopago.com" || uri.Host.EndsWith(".mercadopago.com", StringComparison.OrdinalIgnoreCase) || uri.Host == "mercadopago.com.ar" || uri.Host.EndsWith(".mercadopago.com.ar", StringComparison.OrdinalIgnoreCase)))
            throw new PaymentGatewayUnavailableException("Mercado Pago devolvió una URL de checkout inválida.");
        // La preferencia todavía NO es una transacción aprobada.
        return new(Provider, null, id, checkout);
    }
    public async Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest request, CancellationToken ct)
    {
        var id = VerifySignature(request);
        // La firma no cubre el estado ni todos los campos del cuerpo: no se usan como evidencia.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(18));
        try { return await ReadVerifiedPayment(id, deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PaymentGatewayUnavailableException("Mercado Pago no respondió a tiempo. Reintentar webhook."); }
    }
    private string VerifySignature(GatewayWebhookRequest request)
    {
        static string Header(GatewayWebhookRequest r, string name) => r.Headers.FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value ?? "";
        var signature = Header(request, "x-signature"); var requestId = Header(request, "x-request-id");
        if (!request.Query.TryGetValue("data.id", out var id) || !Regex.IsMatch(id, "^[1-9][0-9]{0,24}$") || !Regex.IsMatch(requestId, "^[A-Za-z0-9_-]{1,160}$")) throw new InvalidPaymentSignatureException();
        var parts = signature.Split(',').Select(p => p.Trim().Split('=', 2)).ToArray();
        if (parts.Any(p => p.Length != 2) || parts.Count(p => p[0] == "ts") != 1 || parts.Count(p => p[0] == "v1") != 1) throw new InvalidPaymentSignatureException();
        var timestamp = parts.Single(p => p[0] == "ts")[1]; var hex = parts.Single(p => p[0] == "v1")[1];
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var stamp) || !Regex.IsMatch(hex, "^[a-fA-F0-9]{64}$")) throw new InvalidPaymentSignatureException();
        // Existen entregas/documentación con segundos y milisegundos; el manifiesto conserva ts literal.
        decimal seconds = stamp >= 100_000_000_000 ? stamp / 1000m : stamp;
        if (Math.Abs(clock.GetUtcNow().ToUnixTimeSeconds() - seconds) > options.SignatureToleranceSeconds) throw new InvalidPaymentSignatureException();
        var manifest = $"id:{id};request-id:{requestId};ts:{timestamp};";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.WebhookSecret), Encoding.UTF8.GetBytes(manifest));
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(hex))) throw new InvalidPaymentSignatureException();
        return id;
    }
    private async Task<VerifiedPaymentEvent> ReadVerifiedPayment(string id, CancellationToken ct)
    {
        using var message = Message(HttpMethod.Get, "v1/payments/" + id);
        using var paymentDoc = await SendAsync(message, ct); var payment = paymentDoc.RootElement;
        Require(Text(payment, "id") == id && Text(payment, "collector_id") == options.CollectorId, "Pago o vendedor incompatible.");
        Require(payment.TryGetProperty("live_mode", out var live) && live.ValueKind is JsonValueKind.True or JsonValueKind.False && live.GetBoolean() != options.Sandbox, "El pago no corresponde al ambiente configurado.");
        Require(Guid.TryParse(Text(payment, "external_reference"), out var orderId), "El pago no tiene una referencia de pedido válida.");
        Require(payment.TryGetProperty("order", out var order) && Text(order, "type") == "mercadopago", "El pago no proviene de una preferencia Checkout Pro.");
        var merchantId = Identifier(order, "id");
        using var merchantRequest = Message(HttpMethod.Get, "merchant_orders/" + merchantId);
        using var merchantDoc = await SendAsync(merchantRequest, ct); var merchant = merchantDoc.RootElement;
        Require(merchant.TryGetProperty("collector", out var collector) && Text(collector, "id") == options.CollectorId && Guid.TryParse(Text(merchant, "external_reference"), out var merchantOrder) && merchantOrder == orderId, "La orden comercial no corresponde al pedido.");
        Require(merchant.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array && payments.EnumerateArray().Any(p => Text(p, "id") == id), "El pago no pertenece a la orden comercial.");
        var preferenceId = Identifier(merchant, "preference_id");
        using var preferenceRequest = Message(HttpMethod.Get, "checkout/preferences/" + preferenceId);
        using var preferenceDoc = await SendAsync(preferenceRequest, ct); var preference = preferenceDoc.RootElement;
        Require(Text(preference, "id") == preferenceId && Text(preference, "collector_id") == options.CollectorId && Guid.TryParse(Text(preference, "external_reference"), out var prefOrder) && prefOrder == orderId, "Preferencia ajena al pedido.");
        Require(preference.TryGetProperty("metadata", out var metadata), "La preferencia no conserva metadata de la plataforma.");
        Require(Guid.TryParse(Text(metadata, "tenant_id"), out var tenantId) && tenantId == options.TenantId && Guid.TryParse(Text(metadata, "order_id"), out var metaOrder) && metaOrder == orderId && Guid.TryParse(Text(metadata, "payment_id"), out _), "Metadata de preferencia incompatible.");
        var paymentId = Guid.Parse(Text(metadata, "payment_id"));
        var status = Text(payment, "status");
        Require(status is "approved" or "rejected" or "pending" or "in_process" or "authorized" or "in_mediation" or "cancelled" or "refunded" or "charged_back", "Estado de Mercado Pago no soportado; requiere revisión.");
        Require(payment.TryGetProperty("transaction_amount", out var amountElement) && amountElement.TryGetDecimal(out _), "Importe de pago ausente.");
        var amount = amountElement.GetDecimal(); Money.Validate(amount);
        var currency = Text(payment, "currency_id"); Require(currency == "ARS", "Moneda inesperada.");
        var modified = Text(payment, "date_last_updated"); Require(DateTimeOffset.TryParse(modified, CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Falta la versión temporal del pago.");
        // Identidad estable del snapshot consultado: nunca usar body.id ni x-request-id para deduplicar estados.
        var canonical = JsonSerializer.Serialize(new { id, preferenceId, tenantId, orderId, paymentId, status, detail = Text(payment, "status_detail"), modified, amount, currency, refunded = Text(payment, "transaction_amount_refunded") });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(tenantId, Provider, "mp:" + id + ":" + hash[..32], id, status, amount, currency, hash, paymentId, orderId, preferenceId);
    }
    private HttpRequestMessage Message(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri("https://api.mercadopago.com/" + path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); return request;
    }
    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new PaymentGatewayUnavailableException($"Mercado Pago rechazó {request.Method}: HTTP {(int)response.StatusCode}. No se modificó el estado del pago.");
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > 1024 * 1024) throw new PaymentGatewayUnavailableException("Respuesta de Mercado Pago demasiado grande.");
            return JsonDocument.Parse(bytes);
        }
        catch (HttpRequestException) { throw new PaymentGatewayUnavailableException("No se pudo contactar a Mercado Pago. Reintentar con el mismo intento."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PaymentGatewayUnavailableException("Mercado Pago no respondió a tiempo. Conservar el mismo intento."); }
        catch (JsonException) { throw new PaymentGatewayUnavailableException("Mercado Pago devolvió una respuesta JSON inválida."); }
    }
    private static string Text(JsonElement node, string name) => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "" : "";
    private static string Identifier(JsonElement node, string name)
    {
        var value = Text(node, name); Require(Regex.IsMatch(value, "^[A-Za-z0-9_-]{1,160}$"), "Identificador de Mercado Pago inválido."); return value;
    }
    private static void Require(bool valid, string message) { if (!valid) throw new PaymentGatewayUnavailableException(message); }
}
