namespace Gastronomia.Infrastructure;

public sealed class MercadoPagoOptions
{
    public string AccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    // Una credencial de vendedor se limita explícitamente al comercio configurado.
    // Marketplace/OAuth requiere un resolver de credenciales por tenant.
    public Guid TenantId { get; set; }
    public string CollectorId { get; set; } = "";
    public bool Sandbox { get; set; } = true;
    public string NotificationUrl { get; set; } = "";
    public string SuccessUrl { get; set; } = "";
    public string FailureUrl { get; set; } = "";
    public string PendingUrl { get; set; } = "";
    public int SignatureToleranceSeconds { get; set; } = 300;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AccessToken)) throw new InvalidOperationException("Configurar MercadoPago__AccessToken en el servidor.");
        if (string.IsNullOrWhiteSpace(WebhookSecret)) throw new InvalidOperationException("Configurar MercadoPago__WebhookSecret en el servidor.");
        if (TenantId == Guid.Empty) throw new InvalidOperationException("Configurar MercadoPago__TenantId para el vendedor.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(CollectorId, "^[1-9][0-9]{0,24}$")) throw new InvalidOperationException("Configurar MercadoPago__CollectorId del vendedor.");
        foreach (var (name, value) in new[] { (nameof(NotificationUrl), NotificationUrl), (nameof(SuccessUrl), SuccessUrl), (nameof(FailureUrl), FailureUrl), (nameof(PendingUrl), PendingUrl) })
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IsLoopback || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
                throw new InvalidOperationException($"MercadoPago__{name} requiere una URL HTTPS pública.");
        if (SignatureToleranceSeconds is < 60 or > 900) throw new InvalidOperationException("MercadoPago__SignatureToleranceSeconds debe estar entre 60 y 900.");
    }
}
