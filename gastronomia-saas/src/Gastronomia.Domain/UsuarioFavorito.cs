using System.ComponentModel.DataAnnotations;
namespace Gastronomia.Domain;

public sealed class UsuarioFavorito : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    public Guid UsuarioId { get; private set; }
    public Guid ProductoId { get; private set; }
    public DateTimeOffset CreadoUtc { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Product Producto { get; private set; } = null!;
    private UsuarioFavorito() { }
    public UsuarioFavorito(Guid tenantId, Guid usuarioId, Guid productoId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || usuarioId == Guid.Empty || productoId == Guid.Empty) throw new DomainException("Favorito inválido.");
        TenantId = tenantId; UsuarioId = usuarioId; ProductoId = productoId; CreadoUtc = now;
    }
}
