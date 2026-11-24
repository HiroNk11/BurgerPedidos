using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Tenant
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    [Required, MaxLength(120)] public string Nombre { get; private set; } = "";
    [Required, MaxLength(80)] public string TimeZoneId { get; private set; } = "America/Argentina/Buenos_Aires";
    [Required, MaxLength(15)] public string WhatsApp { get; private set; } = "";
    [Required, MaxLength(3)] public string Moneda { get; private set; } = "ARS";
    [Column(TypeName="decimal(18,2)")] public decimal TarifaEnvio {get;private set;} = 1500;
    public bool Activo { get; private set; } = true;
    private Tenant() { }
    public Tenant(string nombre, string whatsapp) { Nombre = nombre; WhatsApp = whatsapp; }
}
