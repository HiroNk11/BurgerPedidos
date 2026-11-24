using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class OrderService(IDbContextFactory<GastronomiaDbContext> factory, TenantContext tenant, TimeProvider clock) : IOrderService
{
    private async Task<T> TransactionAsync<T>(Func<GastronomiaDbContext, Task<T>> action, CancellationToken ct, IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        if (tenant.TenantId == Guid.Empty) throw new DomainException("Comercio requerido.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(isolation, ct);
            try { var result = await action(db); await tx.CommitAsync(ct); return result; }
            catch (Exception ex) when (IsRetryable(ex))
            {
                await tx.RollbackAsync(ct);
                if (attempt == 2) throw new ConflictException("Los recursos cambiaron concurrentemente. Reintentá usando el mismo RequestId.");
                await Task.Delay(TimeSpan.FromMilliseconds(30 * (attempt + 1)), ct);
            }
        }
        throw new InvalidOperationException();
    }
    private static bool IsRetryable(Exception ex) => SqlTransactions.IsRetryable(ex);
    private async Task<Tenant> ComercioAsync(GastronomiaDbContext db, CancellationToken ct) => await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenant.TenantId && x.Activo, ct) ?? throw new MissingException("Comercio no disponible.");
    private static void Validate(CrearPedidoRequest request)
    {
        Validator.ValidateValue(request.GuestEmail,new ValidationContext(request){MemberName=nameof(request.GuestEmail)},[new RequiredAttribute(),new EmailAddressAttribute(),new MaxLengthAttribute(254)]);
        Validator.ValidateValue(request.GuestPhone,new ValidationContext(request){MemberName=nameof(request.GuestPhone)},[new RequiredAttribute(),new RegularExpressionAttribute(@"^\+?[0-9]{8,15}$")]);
        Validator.ValidateValue(request.CouponCode,new ValidationContext(request){MemberName=nameof(request.CouponCode)},[new MaxLengthAttribute(40)]);
        if(string.IsNullOrWhiteSpace(request.NombreCliente)||request.NombreCliente.Length>100||request.DireccionEntrega?.Length>400||request.Notas?.Length>500||!Enum.IsDefined(request.TipoEntrega)||request.TipoEntrega==TipoEntrega.Delivery&&string.IsNullOrWhiteSpace(request.DireccionEntrega)) throw new DomainException("Datos de entrega inválidos.");
        if(request.Latitud.HasValue!=request.Longitud.HasValue || request.Latitud.HasValue && (!double.IsFinite(request.Latitud.Value)||!double.IsFinite(request.Longitud!.Value)||request.Latitud is <-90 or >90 || request.Longitud is <-180 or >180)) throw new DomainException("Coordenadas inválidas.");
        if(request.CheckoutToken is not null && !System.Text.RegularExpressions.Regex.IsMatch(request.CheckoutToken,"^[A-Fa-f0-9]{64}$")) throw new DomainException("Token de checkout inválido.");
        if (request.RequestId == Guid.Empty || !Enum.IsDefined(request.MetodoPago) || request.Items is null || request.Items.Count is < 1 or > 100) throw new DomainException("Solicitud inválida.");
        foreach (var i in request.Items) { if (i is null || i.ProductId == Guid.Empty) throw new DomainException("Producto requerido."); if(i.Cantidad is <1 or >1000) throw new DomainException("Cantidad inválida."); }
    }
    internal static string? TokenHash(string? token) => token is null?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private sealed record Pricing(Tenant Tenant,List<Product> Products,Promotion? Promotion,decimal Bruto);
    private async Task<Pricing> PriceAsync(GastronomiaDbContext db,CrearPedidoRequest request,Guid? usuarioId,CancellationToken ct) {
        var comercio=await ComercioAsync(db,ct);
        var quantities=request.Items.GroupBy(i=>i.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(i=>i.Cantidad));
        if(quantities.Any(q=>q.Value>1000)) throw new DomainException("Cantidad inválida.");
        var ids=quantities.Keys.ToArray();var products=await db.Products.Where(p=>ids.Contains(p.Id)).OrderBy(p=>p.Id).ToListAsync(ct);
        if(products.Count!=ids.Length||products.Any(p=>!p.Activo||p.Stock<quantities[p.Id])) throw new DomainException("Hay productos sin stock o no disponibles.");
        decimal bruto=products.Sum(p=>p.Precio*quantities[p.Id]);Money.Validate(bruto);
        string? coupon=string.IsNullOrWhiteSpace(request.CouponCode)?null:request.CouponCode.Trim().ToUpperInvariant();
        var now=clock.GetUtcNow();var zone=TimeZoneInfo.FindSystemTimeZoneById(comercio.TimeZoneId);
        var candidates=await db.Promotions.Where(p=>p.Activa&&p.DesdeUtc<=now&&p.HastaUtc>now&&(coupon==null?p.CouponCode==null:p.CouponCode==coupon)).ToListAsync(ct);
        var limitedIds = candidates.Where(p => p.MaxUsesPerUser.HasValue).Select(p => p.Id).ToArray();
        var counts = new Dictionary<Guid, int>();
        if (usuarioId.HasValue)
        {
            var buyer = await db.Users.SingleOrDefaultAsync(u => u.Id == usuarioId && u.EmailVerificado, ct);
            if (buyer is null || buyer.NormalizedEmail != EmailKey.Normalize(request.GuestEmail)) throw new DomainException("El email debe coincidir con el usuario verificado.");
            if (limitedIds.Length > 0)
                counts = await db.Orders.Where(o => o.PromotionId.HasValue && limitedIds.Contains(o.PromotionId.Value) && o.EstadoPedido != EstadoPedido.Cancelado && (o.UsuarioId == usuarioId || o.GuestEmailNormalized == buyer.NormalizedEmail))
                    .GroupBy(o => o.PromotionId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        }
        // Un límite por persona necesita identidad verificada; un email invitado no prueba identidad.
        candidates = candidates.Where(p => !p.MaxUsesPerUser.HasValue || usuarioId.HasValue && counts.GetValueOrDefault(p.Id) < p.MaxUsesPerUser.Value).ToList();
        var promo=candidates.Where(p=>p.EsAplicable(now,zone,bruto)).OrderByDescending(p=>p.Beneficio(bruto)).ThenBy(p=>p.Id).FirstOrDefault();
        if(coupon is not null&&promo is null) throw new DomainException("Cupón inválido, agotado o fuera de vigencia.");
        if(promo?.Beneficio(bruto)==0)promo=null;return new(comercio,products,promo,bruto);
    }
    public async Task<CotizacionResult> CotizarAsync(CrearPedidoRequest request,CancellationToken ct=default, Guid? usuarioId=null) {
        request=request with {GuestEmail=request.GuestEmail?.Trim()??"",GuestPhone=request.GuestPhone?.Trim()??""};Validate(request);
        await using var db=await factory.CreateDbContextAsync(ct);var p=await PriceAsync(db,request,usuarioId,ct);
        decimal discount=p.Promotion?.Beneficio(p.Bruto)??0,envio=request.TipoEntrega==TipoEntrega.Delivery?p.Tenant.TarifaEnvio:0;
        return new(p.Bruto,discount,envio,p.Bruto-discount+envio,p.Tenant.Moneda,p.Promotion?.Nombre,p.Promotion?.CouponCode);
    }
    public Task<PedidoResult> CrearPedidoAsync(CrearPedidoRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        request = request with { GuestEmail = request.GuestEmail?.Trim() ?? "", GuestPhone = request.GuestPhone?.Trim() ?? "" };
        Validate(request);
        var quantities = request.Items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad));
        if (quantities.Any(q => q.Value > 1000)) throw new DomainException("Máximo 1000 unidades por producto.");
        string? coupon = string.IsNullOrWhiteSpace(request.CouponCode) ? null : request.CouponCode.Trim().ToUpperInvariant();
        var canonical = JsonSerializer.Serialize(new { UsuarioId = usuarioId, Email = EmailKey.Normalize(request.GuestEmail), Phone = request.GuestPhone.Trim(), request.MetodoPago, request.NombreCliente,request.TipoEntrega,request.DireccionEntrega,request.Latitud,request.Longitud,request.Notas,TokenHash=TokenHash(request.CheckoutToken), Coupon = coupon, Items = quantities.OrderBy(x => x.Key).Select(x => new { x.Key, x.Value }).ToArray() });
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return TransactionAsync(async db =>
        {
            var comercio = await ComercioAsync(db, ct);
            var existing = await db.Orders.Include(x => x.Items).Include(x => x.Payments).SingleOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
            if (existing is not null) { if (existing.RequestHash != hash) throw new ConflictException("RequestId ya usado con otros datos."); return Result(existing, comercio); }
            if (usuarioId.HasValue)
            {
                var user = await db.Users.SingleOrDefaultAsync(x => x.Id == usuarioId.Value, ct);
                if (user is null || !user.EmailVerificado || user.NormalizedEmail != EmailKey.Normalize(request.GuestEmail)) throw new DomainException("El email debe coincidir con el usuario verificado.");
            }
            var pricing=await PriceAsync(db,request,usuarioId,ct);
            var products=pricing.Products;var promotion=pricing.Promotion;
            decimal envio=request.TipoEntrega==TipoEntrega.Delivery?comercio.TarifaEnvio:0;
            decimal expected=pricing.Bruto-(promotion?.Beneficio(pricing.Bruto)??0)+envio;
            if(request.TotalEsperado.HasValue && request.TotalEsperado.Value!=expected) throw new ConflictException("El precio o la promoción cambió. Volvé a cotizar antes de confirmar.");
            decimal remaining=promotion?.Beneficio(pricing.Bruto)??0;
            // Una sola promoción; asignación determinista por ProductId, sin pérdidas por redondeo.
            var items = new List<OrderItem>();
            foreach (var p in products) { int count = quantities[p.Id]; p.Reservar(count); decimal discount = Math.Min(remaining, p.Precio * count); items.Add(new OrderItem(p, count, discount)); remaining -= discount; }
            promotion?.Consumir();
            var now=clock.GetUtcNow();
            var order = new Order(tenant.TenantId, request.RequestId, hash, usuarioId, request.GuestEmail, request.GuestPhone.Trim(), now, comercio.Moneda, items, promotion,envio,request.NombreCliente.Trim(),request.TipoEntrega,request.TipoEntrega==TipoEntrega.Delivery?request.DireccionEntrega?.Trim():null,request.TipoEntrega==TipoEntrega.Delivery?request.Latitud:null,request.TipoEntrega==TipoEntrega.Delivery?request.Longitud:null,request.Notas?.Trim(),TokenHash(request.CheckoutToken));
            order.Payments.Add(new Payment(order, request.MetodoPago, now)); db.Orders.Add(order);db.OutboxMessages.Add(new OutboxMessage(order,"PedidoCreado",clock.GetUtcNow()));
            await db.SaveChangesAsync(ct); return Result(order, comercio);
        }, ct, IsolationLevel.Serializable);
    }
    public Task<PedidoResult> AceptarPedidoAsync(Guid orderId, bool iniciarPreparacion = false, CancellationToken ct = default) => TransactionAsync(async db =>
    {
        var comercio = await ComercioAsync(db, ct); var order = await LoadAsync(db, orderId, ct);
        if (order.EstadoPedido == EstadoPedido.PendienteConfirmacion) { order.Aceptar(); db.OutboxMessages.Add(new OutboxMessage(order, "PedidoAceptado", clock.GetUtcNow())); }
        else if (order.EstadoPedido is not (EstadoPedido.Aceptado or EstadoPedido.EnPreparacion)) throw new DomainException("El pedido ya no puede aceptarse.");
        if (iniciarPreparacion && order.EstadoPedido == EstadoPedido.Aceptado) order.CambiarEstado(EstadoPedido.EnPreparacion);
        await db.SaveChangesAsync(ct); return Result(order, comercio);
    }, ct);
    public Task<PedidoResult> CambiarEstadoAsync(Guid orderId, EstadoPedido estado, CancellationToken ct = default) => TransactionAsync(async db =>
    {
        var comercio = await ComercioAsync(db, ct); var order = await LoadAsync(db, orderId, ct);
        if (order.EstadoPedido == estado) return Result(order, comercio);
        if (estado == EstadoPedido.Cancelado)
        {
            order.Cancelar();
            var ids = order.Items.Select(i => i.ProductId).ToArray(); var products = await db.Products.Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id).ToDictionaryAsync(p => p.Id, ct);
            foreach (var item in order.Items) products[item.ProductId].Reponer(item.Cantidad);
            if (order.PromotionId.HasValue) (await db.Promotions.SingleAsync(p => p.Id == order.PromotionId, ct)).Liberar();
        }
        else order.CambiarEstado(estado);
        db.OutboxMessages.Add(new OutboxMessage(order, "Pedido" + estado, clock.GetUtcNow())); await db.SaveChangesAsync(ct); return Result(order, comercio);
    }, ct);
    public Task<int> VincularPedidosInvitadoAsync(string email, Guid usuarioId, CancellationToken ct = default)
    {
        var normalized = EmailKey.Normalize(email);
        return TransactionAsync(async db =>
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
            if (user is null || !user.EmailVerificado || user.NormalizedEmail != normalized) throw new DomainException("Solo se vinculan pedidos al mismo email verificado.");
            // ExecuteUpdate requiere predicado de seguridad explícito; no se confía en IDs del cliente.
            return await db.Orders.Where(o => o.TenantId == tenant.TenantId && o.UsuarioId == null && o.GuestEmailNormalized == normalized).ExecuteUpdateAsync(setters => setters.SetProperty(o => o.UsuarioId, (Guid?)usuarioId), ct);
        }, ct, IsolationLevel.Serializable);
    }
    public async Task<IReadOnlyList<HistorialPedido>> HistorialAsync(Guid usuarioId, int pagina = 1, int tamanio = 20, CancellationToken ct = default)
    {
        if (pagina < 1 || pagina > 100000 || tamanio is < 1 or > 100) throw new DomainException("Paginación inválida.");
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Orders.AsNoTracking().Where(o => o.UsuarioId == usuarioId).OrderByDescending(o => o.CreadoUtc).ThenByDescending(o => o.Id).Skip((pagina - 1) * tamanio).Take(tamanio).Select(o => new HistorialPedido(o.Id, o.ShortCode, o.CreadoUtc, o.EstadoPedido, o.EstadoPago, o.Total, o.Moneda)).ToListAsync(ct);
    }
    public async Task ConfirmarPagoManualAsync(Guid orderId, Guid paymentId, Guid operadorId, decimal importeRecibido, CancellationToken ct = default)
    {
        await TransactionAsync(async db =>
        {
            if (!await db.Users.AnyAsync(u => u.Id == operadorId && u.EmailVerificado, ct)) throw new DomainException("Operador no registrado en el comercio.");
            var order = await LoadAsync(db, orderId, ct); var payment = order.Payments.SingleOrDefault(p => p.Id == paymentId) ?? throw new MissingException("Pago no encontrado.");
            if (order.EstadoPedido == EstadoPedido.Cancelado) throw new DomainException("Pedido cancelado.");
            if (importeRecibido != payment.Importe || payment.Importe != order.Total || payment.Moneda != order.Moneda) throw new DomainException("Importe de pago inconsistente.");
            if (payment.Estado == EstadoPago.Pagado) return true;
            if (order.EstadoPago == EstadoPago.Pagado) throw new ConflictException("El pedido ya tiene un pago confirmado.");
            payment.ConfirmarManual(operadorId, clock.GetUtcNow()); order.RegistrarPago(EstadoPago.Pagado); db.OutboxMessages.Add(new OutboxMessage(order, "PagoConfirmado", clock.GetUtcNow())); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    private static async Task<Order> LoadAsync(GastronomiaDbContext db, Guid id, CancellationToken ct) => await db.Orders.Include(o => o.Items).Include(o => o.Payments).SingleOrDefaultAsync(o => o.Id == id, ct) ?? throw new MissingException("Pedido no encontrado.");
    private static PedidoResult Result(Order o, Tenant t)
    {
        var lines = o.Items.OrderBy(i => i.ProductId).Select(i => new ItemResult(i.ProductId, i.NombreProductoHistorico, i.Cantidad, i.PrecioUnitarioHistorico, i.DescuentoAplicado, i.Subtotal)).ToArray();
        string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " " + o.Moneda;
        var text = string.Join("\n", new[] { "Pedido " + o.ShortCode }.Concat(lines.Select(i => $"{i.Cantidad} x {i.Nombre}: {Amount(i.Subtotal)}")).Concat(new[] { "Descuento: " + Amount(o.DescuentoTotal), "Envío: "+Amount(o.ImporteEnvio),"Entrega: "+o.TipoEntrega,"Cliente: "+o.NombreCliente,"Dirección: "+o.DireccionEntrega, o.Latitud.HasValue?$"Mapa: https://www.google.com/maps/search/?api=1&query={o.Latitud.Value.ToString(CultureInfo.InvariantCulture)},{o.Longitud!.Value.ToString(CultureInfo.InvariantCulture)}":"", "Notas: "+o.Notas,"Total: " + Amount(o.Total), "Email: " + o.GuestEmail, "Teléfono: " + o.GuestPhone, "Pendiente de confirmación del local." }));
        return new(o.Id, o.ShortCode, o.CreadoUtc, o.EstadoPedido, o.EstadoPago, o.ImporteBruto, o.DescuentoTotal, o.Total, o.Moneda, o.ImporteEnvio, o.Payments.OrderBy(p => p.CreadoUtc).First().Id, lines, text, $"https://wa.me/{t.WhatsApp}?text={Uri.EscapeDataString(text)}");
    }
}
