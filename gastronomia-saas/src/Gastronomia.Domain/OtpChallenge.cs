using System.ComponentModel.DataAnnotations;
namespace Gastronomia.Domain;

public sealed class OtpChallenge : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, MaxLength(254)] public string NormalizedEmail { get; private set; } = "";
    [Required, MaxLength(254)] public string Email { get; private set; } = "";
    [Required, MaxLength(32)] public byte[] CodeHash { get; private set; } = [];
    public Guid MessageId { get; private set; }
    public DateTimeOffset CreadoUtc { get; private set; }
    public DateTimeOffset ExpiraUtc { get; private set; }
    public DateTimeOffset? ConsumidoUtc { get; private set; }
    public DateTimeOffset VentanaUtc { get; private set; }
    public int EnviosEnVentana { get; private set; }
    public int IntentosFallidos { get; private set; }
    private OtpChallenge() { }
    public OtpChallenge(Guid tenantId, string email) { TenantId = tenantId; Email = email.Trim(); NormalizedEmail = EmailKey.Normalize(email); }
    public bool PuedeEnviar(DateTimeOffset now) => now >= CreadoUtc.AddSeconds(60) && (now >= VentanaUtc.AddHours(1) || EnviosEnVentana < 5);
    public bool PuedeVerificar(DateTimeOffset now) => ConsumidoUtc is null && now < ExpiraUtc && IntentosFallidos < 5;
    public void Emitir(byte[] codeHash, Guid messageId, DateTimeOffset now)
    {
        if (codeHash.Length != 32 || !PuedeEnviar(now)) throw new DomainException("No se puede emitir otro código todavía.");
        if (now >= VentanaUtc.AddHours(1)) { VentanaUtc = now; EnviosEnVentana = 0; }
        CodeHash = codeHash.ToArray(); MessageId = messageId; CreadoUtc = now; ExpiraUtc = now.AddMinutes(10);
        ConsumidoUtc = null; IntentosFallidos = 0; EnviosEnVentana++;
    }
    public void RegistrarFallo() => IntentosFallidos = Math.Min(5, IntentosFallidos + 1);
    public void Consumir(DateTimeOffset now)
    {
        if (!PuedeVerificar(now)) throw new DomainException("Código inválido o vencido.");
        ConsumidoUtc = now;
    }
}
