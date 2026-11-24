using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Payment : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    public Guid OrderId { get; private set; }
    public MetodoPago Metodo { get; private set; }
    public EstadoPago Estado { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Importe { get; private set; }
    [Required, MaxLength(3)] public string Moneda { get; private set; } = "ARS";
    [MaxLength(2048), Url] public string? ComprobanteUrl { get; private set; }
    [MaxLength(80)] public string? Proveedor { get; private set; }
    [MaxLength(160)] public string? TransaccionIdExterna { get; private set; }
    [MaxLength(160)] public string? PreferenceId { get; private set; }
    [MaxLength(2048), Url] public string? CheckoutUrl { get; private set; }
    public DateTimeOffset CreadoUtc { get; private set; }
    public DateTimeOffset? ConfirmadoUtc { get; private set; }
    public Guid? ConfirmadoPorUsuarioId { get; private set; }
    private Payment() { }
    public Payment(Order order, MetodoPago metodo, DateTimeOffset now) { TenantId = order.TenantId; OrderId = order.Id; Metodo = metodo; Importe = order.Total; Moneda = order.Moneda; CreadoUtc = now; }
    private static bool EsUrlComprobante(string url) => url.StartsWith("/api/tenants/",StringComparison.Ordinal) && url.Contains("/staff/receipts/",StringComparison.Ordinal) || Uri.TryCreate(url,UriKind.Absolute,out var uri) && uri.Scheme=="https";
    public void AdjuntarComprobante(string url)
    {
        if (Metodo != MetodoPago.TransferenciaCBU || Estado == EstadoPago.Pagado || !EsUrlComprobante(url)) throw new DomainException("Comprobante inválido.");
        ComprobanteUrl = url; Estado = EstadoPago.ComprobanteEnviado;
    }
    public void ConfirmarManual(Guid operador, DateTimeOffset now) { if (Metodo == MetodoPago.PasarelaOnline) throw new DomainException("La pasarela requiere validación servidor a servidor."); if (Estado == EstadoPago.Rechazado) throw new DomainException("Intento rechazado."); Estado = EstadoPago.Pagado; ConfirmadoUtc = now; ConfirmadoPorUsuarioId = operador; }

    // Solo desde un adaptador confiable, ANTES de recibir el webhook. No aceptar IDs del cliente.
    public void AsociarTransaccion(string proveedor, string transactionId)
    {
        if (Metodo != MetodoPago.PasarelaOnline || string.IsNullOrWhiteSpace(proveedor) || proveedor.Length > 80 || string.IsNullOrWhiteSpace(transactionId) || transactionId.Length > 160)
            throw new DomainException("Identificador de pasarela inválido.");
        if (TransaccionIdExterna is not null && (TransaccionIdExterna != transactionId || Proveedor != proveedor)) throw new DomainException("El intento ya está asociado a otra transacción.");
        Proveedor = proveedor; TransaccionIdExterna = transactionId;
    }
    public void AsociarPreferencia(string proveedor, string preferenceId, string checkoutUrl)
    {
        if (Metodo != MetodoPago.PasarelaOnline || string.IsNullOrWhiteSpace(proveedor) || proveedor.Length > 80 || string.IsNullOrWhiteSpace(preferenceId) || preferenceId.Length > 160 || checkoutUrl.Length > 2048 || !Uri.TryCreate(checkoutUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new DomainException("Preferencia inválida.");
        if (Proveedor is not null && Proveedor != proveedor || PreferenceId is not null && (PreferenceId != preferenceId || CheckoutUrl != checkoutUrl)) throw new DomainException("El pago ya está asociado a otra preferencia.");
        Proveedor = proveedor; PreferenceId = preferenceId; CheckoutUrl = checkoutUrl;
    }
    // Un checkout puede producir varios intentos rechazados antes del pago aprobado.
    public void AsociarIntentoVerificado(string proveedor, string preferenceId, string transactionId)
    {
        if (Metodo != MetodoPago.PasarelaOnline || Proveedor != proveedor || PreferenceId != preferenceId || string.IsNullOrWhiteSpace(transactionId) || transactionId.Length > 160 || Estado == EstadoPago.Pagado) throw new DomainException("Intento incompatible con la preferencia.");
        TransaccionIdExterna = transactionId;
    }
    public void RechazarPasarela()
    {
        if (Metodo != MetodoPago.PasarelaOnline || TransaccionIdExterna is null || Estado == EstadoPago.Pagado) throw new DomainException("No se puede rechazar este pago.");
        Estado = EstadoPago.Rechazado;
    }
    public void ConfirmarPasarela(string proveedor, string transactionId, decimal importe, string moneda, DateTimeOffset now)
    {
        if (Metodo != MetodoPago.PasarelaOnline || importe != Importe || moneda != Moneda || Proveedor != proveedor || TransaccionIdExterna != transactionId || TransaccionIdExterna is null)
            throw new DomainException("Transacción no válida para este pago.");
        if (Estado == EstadoPago.Pagado) return;
        Estado = EstadoPago.Pagado; ConfirmadoUtc = now;
    }
}
