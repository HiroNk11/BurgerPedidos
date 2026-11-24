using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class OutboxMessage : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    public Guid OrderId { get; private set; }
    [Required, MaxLength(80)] public string Tipo { get; private set; } = "";
    public DateTimeOffset CreadoUtc { get; private set; }
    public DateTimeOffset? ProcesadoUtc { get; private set; }
    public int Attempts {get;private set;}
    public DateTimeOffset? NextAttemptUtc {get;private set;}
    public DateTimeOffset? LockedUntilUtc {get;private set;}
    public Guid? LeaseId {get;private set;}
    [MaxLength(1000)] public string? LastError {get;private set;}
    [MaxLength(160)] public string? ProviderMessageId {get;private set;}
    private OutboxMessage() { }
    public OutboxMessage(Order order, string tipo, DateTimeOffset now) { TenantId = order.TenantId; OrderId = order.Id; Tipo = tipo; CreadoUtc = now; }
    public void MarcarProcesado(DateTimeOffset now) => ProcesadoUtc = now;
}
