using System.Data;
using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class CustomerAccountService(IDbContextFactory<GastronomiaDbContext> factory) : ICustomerAccountService
{
    private static async Task<Usuario> RequireUserAsync(GastronomiaDbContext db, Guid id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.EmailVerificado && db.Tenants.Any(t => t.Id == u.TenantId && t.Activo), ct)
        ?? throw new MissingException("Cuenta no disponible.");
    private static DireccionResult Address(Direccion d) => new(d.Id, d.Calle, d.Numero, d.PisoDepto, d.Latitud, d.Longitud);
    public async Task<PerfilCliente> GetAsync(Guid userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await RequireUserAsync(db, userId, ct);
        var addresses = await db.Direcciones.AsNoTracking().Where(d => d.UsuarioId == userId).OrderBy(d => d.Id).ToListAsync(ct);
        return new(user.Id, user.Email, user.Nombre, user.Telefono, addresses.Select(Address).ToList());
    }
    public async Task<PerfilCliente> UpdateAsync(Guid userId, PerfilRequest request, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await RequireUserAsync(db, userId, ct);
        user.ActualizarPerfil(request.Nombre, request.Telefono);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }
    public async Task<DireccionResult> SaveAddressAsync(Guid userId, Guid? addressId, DireccionRequest request, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireUserAsync(db, userId, ct);
        Direccion address;
        if (addressId.HasValue)
        {
            address = await db.Direcciones.SingleOrDefaultAsync(d => d.Id == addressId && d.UsuarioId == userId, ct)
                ?? throw new MissingException("Dirección no encontrada.");
            address.Actualizar(request.Calle, request.Numero, request.PisoDepto, request.Latitud, request.Longitud);
        }
        else
        {
            if (await db.Direcciones.CountAsync(d => d.UsuarioId == userId, ct) >= 10) throw new DomainException("Podés guardar hasta 10 direcciones.");
            address = new Direccion(db.CurrentTenantId, userId, request.Calle, request.Numero, request.PisoDepto, request.Latitud, request.Longitud);
            db.Direcciones.Add(address);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Address(address);
    }
    public async Task DeleteAddressAsync(Guid userId, Guid addressId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireUserAsync(db, userId, ct);
        var address = await db.Direcciones.SingleOrDefaultAsync(d => d.Id == addressId && d.UsuarioId == userId, ct)
            ?? throw new MissingException("Dirección no encontrada.");
        db.Direcciones.Remove(address);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CustomerOrdersService(IDbContextFactory<GastronomiaDbContext> factory) : ICustomerOrdersService
{
    private static bool Historical(EstadoPedido state) => state is EstadoPedido.Entregado or EstadoPedido.Cancelado;
    private static PedidoCliente Map(Order o) => new(o.Id, o.ShortCode, o.CreadoUtc, o.EstadoPedido, o.EstadoPago, o.Total, o.Moneda, o.TipoEntrega, o.DireccionEntrega, o.Notas,
        o.Items.OrderBy(i => i.Id).Select(i => new ItemResult(i.ProductId, i.NombreProductoHistorico, i.Cantidad, i.PrecioUnitarioHistorico, i.DescuentoAplicado, i.Subtotal)).ToList(), o.Payments.OrderByDescending(p => p.CreadoUtc).FirstOrDefault()?.Metodo);
    private static async Task RequireUserAsync(GastronomiaDbContext db, Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.EmailVerificado && db.Tenants.Any(t => t.Id == u.TenantId && t.Activo), ct)) throw new MissingException("Cuenta no disponible.");
    }
    public async Task<MisPedidosResult> HistoryAsync(Guid userId, int pagina = 1, int tamanio = 20, CancellationToken ct = default)
    {
        if (pagina is < 1 or > 100000 || tamanio is < 1 or > 100) throw new DomainException("Paginación inválida.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireUserAsync(db, userId, ct);
        var own = db.Orders.AsNoTracking().Where(o => o.UsuarioId == userId);
        var activeQuery = own.Where(o => o.EstadoPedido != EstadoPedido.Entregado && o.EstadoPedido != EstadoPedido.Cancelado);
        var historyQuery = own.Where(o => o.EstadoPedido == EstadoPedido.Entregado || o.EstadoPedido == EstadoPedido.Cancelado);
        int activeCount = await activeQuery.CountAsync(ct), historyCount = await historyQuery.CountAsync(ct);
        var active = await activeQuery.OrderByDescending(o => o.CreadoUtc).ThenByDescending(o => o.Id).Take(100).Include(o => o.Items).Include(o => o.Payments).AsSplitQuery().ToListAsync(ct);
        var history = await historyQuery.OrderByDescending(o => o.CreadoUtc).ThenByDescending(o => o.Id).Skip((pagina - 1) * tamanio).Take(tamanio).Include(o => o.Items).Include(o => o.Payments).AsSplitQuery().ToListAsync(ct);
        return new(active.Select(Map).ToList(), activeCount, history.Select(Map).ToList(), historyCount, pagina, tamanio);
    }
    public async Task<PedidoCliente> DetailAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireUserAsync(db, userId, ct);
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == orderId && o.UsuarioId == userId, ct)
            ?? throw new MissingException("Pedido no encontrado.");
        return Map(order);
    }
    public async Task<CarritoPropuesto> ReorderAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireUserAsync(db, userId, ct);
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == orderId && o.UsuarioId == userId, ct)
            ?? throw new MissingException("Pedido no encontrado.");
        if (!Historical(order.EstadoPedido)) throw new DomainException("Volver a pedir requiere un pedido entregado o cancelado.");
        var ids = order.Items.Select(i => i.ProductId).ToArray();
        var catalog = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var items = new List<ReorderItem>(); var warnings = new List<ReorderWarning>(); var removed = new List<ReorderRemovedItem>();
        foreach (var line in order.Items.OrderBy(i => i.Id))
        {
            if (!catalog.TryGetValue(line.ProductId, out var product) || !product.Activo || product.Stock == 0)
            {
                string reason = product is null || !product.Activo ? "Producto no disponible en el catálogo actual." : "Producto sin stock.";
                removed.Add(new(line.ProductId, line.NombreProductoHistorico, line.Cantidad, reason));
                warnings.Add(new("ProductoRemovido", line.ProductId, $"{line.NombreProductoHistorico}: {reason}"));
                continue;
            }
            int quantity = Math.Min(line.Cantidad, product.Stock);
            if (quantity != line.Cantidad) warnings.Add(new("CantidadReducida", product.Id, $"{product.Nombre}: quedan {quantity} unidades; se ajustó la cantidad propuesta."));
            if (product.Precio != line.PrecioUnitarioHistorico) warnings.Add(new("PrecioActualizado", product.Id, $"{product.Nombre}: el precio cambió respecto al pedido anterior."));
            items.Add(new(product.Id, product.Nombre, quantity, product.Precio, product.Precio * quantity, line.PrecioUnitarioHistorico));
        }
        if (order.DescuentoTotal > 0) warnings.Add(new("PromocionNoReaplicada", null, "Las promociones anteriores no se copian. Se comprobarán las promociones vigentes al cotizar."));
        return new(order.Id, items, warnings, removed, items.Sum(i => i.Subtotal), order.Moneda);
    }
}
