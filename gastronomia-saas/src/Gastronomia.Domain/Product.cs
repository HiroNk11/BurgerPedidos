using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Product : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, MaxLength(160)] public string Nombre { get; private set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Precio { get; private set; }
    [Range(0, int.MaxValue)] public int Stock { get; private set; }
    [Required,MaxLength(40)] public string Categoria {get;private set;} = "Burgers";
    [MaxLength(500)] public string Descripcion {get;private set;} = "";
    [MaxLength(40)] public string? ImagenKey {get;private set;}
    public void ActualizarPresentacion(string categoria,string descripcion,string? imagenKey) {Categoria=categoria;Descripcion=descripcion;ImagenKey=imagenKey;}
    public bool Activo { get; private set; } = true;
    public ICollection<UsuarioFavorito> Favoritos { get; private set; } = new List<UsuarioFavorito>();
    private Product() { }
    public Product(Guid tenantId, string nombre, decimal precio, int stock) { Money.Validate(precio); if (stock < 0) throw new DomainException("Stock inválido."); TenantId = tenantId; Nombre = nombre; Precio = precio; Stock = stock; }
    public void Reservar(int cantidad) { if (!Activo || cantidad <= 0 || Stock < cantidad) throw new DomainException("Stock insuficiente."); Stock -= cantidad; }
    public void Reponer(int cantidad) { Stock = checked(Stock + cantidad); }
    public void CambiarPrecio(decimal precio) { Money.Validate(precio); Precio = precio; }
}
