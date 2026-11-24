using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class UserProductsService(IDbContextFactory<GastronomiaDbContext> factory, TimeProvider clock) : IUserProductsService
{
    private static async Task RequireUser(GastronomiaDbContext db, Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id && u.EmailVerificado, ct)) throw new MissingException("Cuenta no encontrada.");
    }
    public Task<FavoritoResult> ToggleAsync(Guid usuarioId, Guid productoId, CancellationToken ct = default) => SqlTransactions.RunAsync(factory, async db =>
    {
        await RequireUser(db, usuarioId, ct);
        var existing = await db.UsuarioFavoritos.SingleOrDefaultAsync(f => f.UsuarioId == usuarioId && f.ProductoId == productoId, ct);
        if (existing is not null) db.UsuarioFavoritos.Remove(existing);
        else
        {
            if (!await db.Products.AnyAsync(p => p.Id == productoId && p.Activo, ct)) throw new MissingException("Producto no disponible.");
            db.UsuarioFavoritos.Add(new UsuarioFavorito(db.CurrentTenantId, usuarioId, productoId, clock.GetUtcNow()));
        }
        await db.SaveChangesAsync(ct); return new FavoritoResult(productoId, existing is null);
    }, ct);
    public async Task<IReadOnlyList<ProductoCliente>> FavoritosAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await RequireUser(db, usuarioId, ct);
        return await db.UsuarioFavoritos.AsNoTracking().Where(f => f.UsuarioId == usuarioId).OrderByDescending(f => f.CreadoUtc).ThenBy(f => f.ProductoId)
            .Select(f => new ProductoCliente(f.Producto.Id, f.Producto.Nombre, f.Producto.Precio, f.Producto.Stock, f.Producto.Categoria, f.Producto.Descripcion, f.Producto.ImagenKey, f.Producto.Activo)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<ProductoRanking>> MasPedidosAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await RequireUser(db, usuarioId, ct);
        var counts = await (from item in db.OrderItems.AsNoTracking()
                            join order in db.Orders on new {item.TenantId, Id = item.OrderId} equals new {order.TenantId, order.Id}
                            where order.UsuarioId == usuarioId && order.EstadoPedido != EstadoPedido.Cancelado
                            group item by item.ProductId into g
                            select new {Id = g.Key, Count = g.Sum(i => (long)i.Cantidad)})
                            .OrderByDescending(x => x.Count).ThenBy(x => x.Id).Take(10).ToListAsync(ct);
        var ids = counts.Select(c => c.Id).ToArray();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return counts.Where(c => products.ContainsKey(c.Id)).Select(c => {var p = products[c.Id]; return new ProductoRanking(new(p.Id, p.Nombre, p.Precio, p.Stock, p.Categoria, p.Descripcion, p.ImagenKey, p.Activo), c.Count);}).ToArray();
    }
}
