using System.ComponentModel.DataAnnotations;
namespace Gastronomia.Domain;

public sealed class Direccion : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid UsuarioId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, MaxLength(180)] public string Calle { get; private set; } = "";
    [Required, MaxLength(20)] public string Numero { get; private set; } = "";
    [MaxLength(60)] public string? PisoDepto { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    private Direccion() { }
    public Direccion(Guid tenantId, Guid usuarioId, string calle, string numero, string? pisoDepto, double? latitud, double? longitud)
    {
        TenantId = tenantId; UsuarioId = usuarioId;
        Actualizar(calle, numero, pisoDepto, latitud, longitud);
    }
    public void Actualizar(string calle, string numero, string? pisoDepto, double? latitud, double? longitud)
    {
        if (string.IsNullOrWhiteSpace(calle) || calle.Trim().Length > 180 || string.IsNullOrWhiteSpace(numero) || numero.Trim().Length > 20 || pisoDepto?.Length > 60)
            throw new DomainException("Dirección inválida.");
        if (latitud.HasValue != longitud.HasValue || latitud.HasValue && (!double.IsFinite(latitud.Value) || !double.IsFinite(longitud!.Value) || latitud is < -90 or > 90 || longitud is < -180 or > 180))
            throw new DomainException("Coordenadas inválidas.");
        Calle = calle.Trim(); Numero = numero.Trim(); PisoDepto = string.IsNullOrWhiteSpace(pisoDepto) ? null : pisoDepto.Trim();
        Latitud = latitud; Longitud = longitud;
    }
}
