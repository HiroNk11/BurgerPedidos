namespace Gastronomia.Application;

public sealed record ProductoCliente(Guid Id, string Nombre, decimal Precio, int Stock, string Categoria, string Descripcion, string? ImagenKey, bool Activo);
public sealed record FavoritoResult(Guid ProductoId, bool EsFavorito);
public sealed record ProductoRanking(ProductoCliente Producto, long CantidadPedida);
public interface IUserProductsService
{
    Task<FavoritoResult> ToggleAsync(Guid usuarioId, Guid productoId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductoCliente>> FavoritosAsync(Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductoRanking>> MasPedidosAsync(Guid usuarioId, CancellationToken ct = default);
}
