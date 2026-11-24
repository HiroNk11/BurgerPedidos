using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Usuario : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, EmailAddress, MaxLength(254)] public string Email { get; private set; } = "";
    [Required, MaxLength(254)] public string NormalizedEmail { get; private set; } = "";
    public bool EmailVerificado { get; private set; }
    [MaxLength(100)] public string Nombre { get; private set; } = "";
    [MaxLength(25)] public string Telefono { get; private set; } = "";
    public ICollection<Direccion> Direcciones { get; private set; } = new List<Direccion>();
    public ICollection<UsuarioFavorito> Favoritos { get; private set; } = new List<UsuarioFavorito>();
    private Usuario() { }
    // Invocar únicamente desde el aprovisionamiento de identidad confiable (OIDC/Identity).
    public Usuario(Guid tenantId, Guid id, string email, bool verificado) { TenantId = tenantId; Id = id; Email = email.Trim(); NormalizedEmail = EmailKey.Normalize(email); EmailVerificado = verificado; }
    public void VerificarEmail() => EmailVerificado = true;
    public void ActualizarPerfil(string nombre, string telefono)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(telefono ?? "", @"^\+?[0-9]{8,15}$"))
            throw new DomainException("Nombre o teléfono inválido.");
        Nombre = nombre.Trim(); Telefono = telefono!;
    }
}
