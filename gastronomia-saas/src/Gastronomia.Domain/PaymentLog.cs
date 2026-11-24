using System.ComponentModel.DataAnnotations;
namespace Gastronomia.Domain;

// Solo eventos autenticados. Nunca persistir firmas, secretos, tarjetas o el payload completo.
public sealed class PaymentLog : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    public Guid PaymentId { get; private set; }
    [Required, MaxLength(80)] public string Proveedor { get; private set; } = "";
    [Required, MaxLength(160)] public string EventoId { get; private set; } = "";
    [Required, MaxLength(160)] public string TransaccionIdExterna { get; private set; } = "";
    [Required, MaxLength(64)] public string PayloadHash { get; private set; } = "";
    [Required, MaxLength(20)] public string EstadoExterno { get; private set; } = "";
    public decimal Importe { get; private set; }
    [Required, MaxLength(3)] public string Moneda { get; private set; } = "";
    [Required, MaxLength(40)] public string Resultado { get; private set; } = "";
    public DateTimeOffset RecibidoUtc { get; private set; }
    private PaymentLog() { }
    public PaymentLog(Payment payment, string eventoId, string hash, string estado, decimal importe, string moneda, string resultado, DateTimeOffset now, string? transactionId = null)
    {
        TenantId = payment.TenantId; PaymentId = payment.Id; Proveedor = payment.Proveedor!;
        TransaccionIdExterna = transactionId ?? payment.TransaccionIdExterna!; EventoId = eventoId; PayloadHash = hash;
        EstadoExterno = estado; Importe = importe; Moneda = moneda; Resultado = resultado; RecibidoUtc = now;
    }
}
