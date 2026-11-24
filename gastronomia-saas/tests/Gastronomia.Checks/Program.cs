using System.Text.RegularExpressions;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var modelOptions = new DbContextOptionsBuilder<GastronomiaDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=GastronomiaModel;Integrated Security=true;TrustServerCertificate=true").Options;
using var model = new GastronomiaDbContext(modelOptions, new TenantContext());
if (args.Length == 2 && args[0] == "--ddl") { await File.WriteAllTextAsync(args[1], "-- Generado por EF Core 10 desde GastronomiaDbContext. Ejecutar en una base NUEVA.\n" + model.Database.GenerateCreateScript()); Console.WriteLine("DDL generado."); return; }
var now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
int checks = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
void Throws(Action action, string name) { try { action(); } catch (DomainException) { Assert(true, name); return; } throw new Exception("FAIL: " + name); }
var tid = Guid.NewGuid();
var percent = new Promotion(tid, "10%", TipoPromocion.PorFecha, TipoDescuento.Porcentaje, 10, now.AddDays(-1), now.AddDays(1));
Assert(percent.Beneficio(10.05m) == 1.01m, "redondeo monetario");
Assert(!percent.EsAplicable(now.AddDays(1), TimeZoneInfo.Utc, 100), "fin de promoción exclusivo");
var overnight = new Promotion(tid, "Lunes nocturno", TipoPromocion.PorDiaSemana, TipoDescuento.ImporteFijo, 1, now.AddDays(-10), now.AddDays(10), dias: 2, inicio: new TimeOnly(22, 0), fin: new TimeOnly(2, 0));
Assert(overnight.EsAplicable(new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, 100), "martes madrugada pertenece al lunes de inicio");
Assert(!overnight.EsAplicable(new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, 100), "fuera de horario nocturno");
var product = new Product(tid, "Burger", 100, 10); var item = new OrderItem(product, 2, 20); product.CambiarPrecio(200);
Assert(item.PrecioUnitarioHistorico == 100 && item.Subtotal == 180, "precio histórico independiente del catálogo");
Throws(() => product.Reservar(11), "stock negativo rechazado");
Throws(() => new OrderItem(product, 0, 0), "cantidad cero rechazada");
Throws(() => new Promotion(tid, "inválida", TipoPromocion.PorFecha, TipoDescuento.Porcentaje, 101, now, now.AddDays(1)), "porcentaje inválido rechazado");
Assert(model.Model.GetEntityTypes().Count() == 12, "modelo EF completo");
await MercadoPagoChecks.RunAsync(Assert);
if (args.Length != 2 || args[0] != "--sql") { Console.WriteLine($"{checks} verificaciones sin SQL correctas. Usar --sql <directorio sql> para integración."); return; }

string name = "GastronomiaChecks_" + Guid.NewGuid().ToString("N");
string connection = Environment.GetEnvironmentVariable("GASTRONOMIA_TEST_CONNECTION") ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true";
var masterBuilder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", ConnectTimeout = 30 };
await using var master = new SqlConnection(masterBuilder.ConnectionString); await master.OpenAsync();
await using (var create = master.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
var dbBuilder = new SqlConnectionStringBuilder(connection) { InitialCatalog = name, ConnectTimeout = 30 };
var sqlOptions = new DbContextOptionsBuilder<GastronomiaDbContext>().UseSqlServer(dbBuilder.ConnectionString).Options;
var tenantA = new Tenant("A", "5491124830571"); var tenantB = new Tenant("B", "5491124830571");
var scope = new TenantContext { TenantId = tenantA.Id };
var factory = new TestFactory(sqlOptions, scope); var service = new OrderService(factory, scope, new FixedTime(now));
async Task ExpectAsync(Func<Task> action, string label) { try { await action(); } catch (Exception e) when (e is DomainException or ConflictException or MissingException) { Assert(true, label); return; } throw new Exception("FAIL: " + label); }
async Task<bool> TryCreate(CrearPedidoRequest request) { try { await service.CrearPedidoAsync(request, null); return true; } catch (Exception e) when (e is DomainException or ConflictException) { return false; } }
CrearPedidoRequest Request(Product p, string? coupon = null, Guid? requestId = null) => new(requestId ?? Guid.NewGuid(), " Buyer@Example.com ", "5491124830571", MetodoPago.TransferenciaCBU, [new(p.Id, 1)], coupon);
try
{
    await using (var db = await factory.CreateDbContextAsync())
    {
        await db.Database.OpenConnectionAsync();
        foreach (var file in new[] { "001_schema.sql", "002_history_guards.sql" })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(args[1], file));
            foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)) if (!string.IsNullOrWhiteSpace(batch)) { using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = batch; await command.ExecuteNonQueryAsync(); }
        }
    }
    var burger = new Product(tenantA.Id, "Burger", 100, 20); var last = new Product(tenantA.Id, "Ultima unidad", 50, 1); var abundant = new Product(tenantA.Id, "Para cupón", 50, 100); var other = new Product(tenantB.Id, "Privado B", 60, 10);
    var user = new Usuario(tenantA.Id, Guid.NewGuid(), "buyer@example.com", true); var unverified = new Usuario(tenantA.Id, Guid.NewGuid(), "otro@example.com", false);
    var auto = new Promotion(tenantA.Id, "Auto 10%", TipoPromocion.PorFecha, TipoDescuento.Porcentaje, 10, now.AddDays(-1), now.AddDays(1));
    var autoSmall = new Promotion(tenantA.Id, "Auto 5%", TipoPromocion.PorFecha, TipoDescuento.Porcentaje, 5, now.AddDays(-1), now.AddDays(1));
    var limited = new Promotion(tenantA.Id, "Uno", TipoPromocion.Cupon, TipoDescuento.ImporteFijo, 5, now.AddDays(-1), now.AddDays(1), 1, "UNO");
    await using (var db = await factory.CreateDbContextAsync()) { db.AddRange(tenantA, tenantB, burger, last, abundant, user, unverified, auto, autoSmall, limited); await db.SaveChangesAsync(); }
    var factoryB = new TestFactory(sqlOptions, new TenantContext { TenantId = tenantB.Id });
    await using (var db = await factoryB.CreateDbContextAsync()) { db.Products.Add(other); db.Users.Add(new Usuario(tenantB.Id, user.Id, "buyer@example.com", true)); await db.SaveChangesAsync(); }
    var request = Request(burger); var first = await service.CrearPedidoAsync(request, null);
    Assert(first.ShortCode.StartsWith("#PED-") && first.Total == 90 && first.Descuento == 10, "creación, ShortCode SQL y mayor beneficio sin acumulación");
    Assert(first.EstadoPedido == EstadoPedido.PendienteConfirmacion && first.EstadoPago == EstadoPago.Pendiente, "estados iniciales independientes");
    var replay = await service.CrearPedidoAsync(request, null); Assert(replay.Id == first.Id, "reintento idempotente");
    await ExpectAsync(async () => { await service.CrearPedidoAsync(request with { GuestPhone = "5491100000000" }, null); }, "RequestId con payload diferente da conflicto");
    await using (var db = await factory.CreateDbContextAsync()) { var p = await db.Products.SingleAsync(p => p.Id == burger.Id); p.CambiarPrecio(200); await db.SaveChangesAsync(); }
    Assert((await service.CrearPedidoAsync(request, null)).Total == 90, "reintento conserva precios históricos después del cambio");
    var accepted = await service.AceptarPedidoAsync(first.Id, true); Assert(accepted.EstadoPedido == EstadoPedido.EnPreparacion && accepted.EstadoPago == EstadoPago.Pendiente, "aceptar no confirma el pago");
    await service.AceptarPedidoAsync(first.Id, true);
    await using (var db = await factory.CreateDbContextAsync()) { Assert(await db.OutboxMessages.CountAsync(x => x.OrderId == first.Id && x.Tipo == "PedidoAceptado") == 1, "aceptación repetida no duplica notificaciones"); Assert((await db.Products.SingleAsync(p => p.Id == burger.Id)).Stock == 19, "aceptación no descuenta stock otra vez"); }
    Assert((await service.CambiarEstadoAsync(first.Id, EstadoPedido.Entregado)).EstadoPago == EstadoPago.Pendiente, "entregar no confirma el pago automáticamente");
    await ExpectAsync(async()=>{await service.ConfirmarPagoManualAsync(first.Id,first.PaymentId,user.Id,first.Total+1);},"cobro manual rechaza importe recibido diferente");
    await service.ConfirmarPagoManualAsync(first.Id, first.PaymentId, user.Id, first.Total); await service.ConfirmarPagoManualAsync(first.Id, first.PaymentId, user.Id, first.Total);
    Assert((await service.CambiarEstadoAsync(first.Id, EstadoPedido.Entregado)).EstadoPago == EstadoPago.Pagado, "pago manual e idempotencia, luego entrega");
    await ExpectAsync(async () => { await service.CambiarEstadoAsync(first.Id, EstadoPedido.Cancelado); }, "no cancelar pedido entregado/pagado sin reembolso");
    await ExpectAsync(async () => { await service.VincularPedidosInvitadoAsync("otro@example.com", unverified.Id); }, "no vincular email sin verificar");
    await ExpectAsync(async () => { await service.VincularPedidosInvitadoAsync("buyer@example.com", unverified.Id); }, "no vincular email ajeno");
    Assert(await service.VincularPedidosInvitadoAsync(" BUYER@example.com ", user.Id) == 1, "vinculación normalizada de invitado"); Assert(await service.VincularPedidosInvitadoAsync("buyer@example.com", user.Id) == 0, "vinculación idempotente");
    Assert((await service.HistorialAsync(user.Id)).Count == 1, "historial por usuario y fecha");
    await ExpectAsync(async () => { await service.CrearPedidoAsync(Request(other), null); }, "producto de otro comercio rechazado");
    await using (var db = await factory.CreateDbContextAsync()) { Assert(!await db.Products.AnyAsync(p => p.Id == other.Id), "filtro de tenant"); Assert(await db.Users.CountAsync(u => u.Id == user.Id)==1,"misma identidad en varios comercios, aislada por tenant"); }
    var stocks = await Task.WhenAll(TryCreate(Request(last)), TryCreate(Request(last))); Assert(stocks.Count(x => x) == 1, "dos compradores: una sola venta de última unidad");
    var coupons = await Task.WhenAll(TryCreate(Request(abundant, "uno")), TryCreate(Request(abundant, "UNO"))); Assert(coupons.Count(x => x) == 1, "concurrencia MaxUses=1");
    Guid couponOrder;
    await using (var db = await factory.CreateDbContextAsync()) { Assert((await db.Products.SingleAsync(p => p.Id == last.Id)).Stock == 0, "stock final no negativo"); Assert((await db.Promotions.SingleAsync(p => p.Id == limited.Id)).CurrentUses == 1, "uso de cupón consistente"); couponOrder = await db.Orders.Where(o => o.PromotionId == limited.Id).Select(o => o.Id).SingleAsync(); }
    await service.CambiarEstadoAsync(couponOrder, EstadoPedido.Cancelado); await service.CambiarEstadoAsync(couponOrder, EstadoPedido.Cancelado);
    await using (var db = await factory.CreateDbContextAsync()) { Assert((await db.Promotions.SingleAsync(p => p.Id == limited.Id)).CurrentUses == 0 && (await db.Products.SingleAsync(p => p.Id == abundant.Id)).Stock == 100, "cancelación libera stock y cupón una sola vez"); }
    await using (var db = await factory.CreateDbContextAsync())
    {
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[OrderItems] SET [PrecioUnitarioHistorico]=[PrecioUnitarioHistorico] WHERE [OrderId]={first.Id}"); throw new Exception("Trigger no aplicado"); } catch (SqlException ex) when (ex.Number == 51000) { Assert(true, "trigger SQL protege detalle histórico"); }
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[Orders] SET [Total]=[Total] WHERE [Id]={first.Id}"); throw new Exception("Trigger no aplicado"); } catch (SqlException ex) when (ex.Number == 51002) { Assert(true, "trigger SQL protege snapshot del pedido"); }
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[Products] SET [Stock]=-1 WHERE [Id]={burger.Id}"); throw new Exception("CHECK no aplicado"); } catch (SqlException ex) when (ex.Number == 547) { Assert(true, "CHECK SQL impide stock negativo"); }
        try { await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [dbo].[OrderItems] ([Id],[TenantId],[OrderId],[ProductId],[NombreProductoHistorico],[PrecioUnitarioHistorico],[Cantidad],[DescuentoAplicado],[Subtotal]) VALUES ({Guid.NewGuid()},{tenantA.Id},{first.Id},{other.Id},N'X',1,1,0,1)"); throw new Exception("FK no aplicada"); } catch (SqlException ex) when (ex.Number == 547) { Assert(true, "FK compuesta impide relación entre comercios"); }
    }
    var checkoutProduct=new Product(tenantA.Id,"Checkout delivery",100,3);
    await using(var db=await factory.CreateDbContextAsync()){db.Products.Add(checkoutProduct);await db.SaveChangesAsync();}
    var checkout=Request(checkoutProduct) with {NombreCliente="Cliente de prueba",TipoEntrega=TipoEntrega.Delivery,DireccionEntrega="Dirección ficticia 123",Latitud=-34.60,Longitud=-58.38,Notas="Solo prueba",CheckoutToken=new string('a',64)};
    var quote=await service.CotizarAsync(checkout);
    Assert(quote.Bruto==100&&quote.Descuento==10&&quote.Envio==1500&&quote.Total==1590,"cotización incorpora descuento y tarifa de delivery");
    await using(var db=await factory.CreateDbContextAsync()){Assert((await db.Products.SingleAsync(p=>p.Id==checkoutProduct.Id)).Stock==3,"cotizar no reserva stock");}
    await ExpectAsync(()=>service.CrearPedidoAsync(checkout with {TotalEsperado=1},null),"precio esperado distinto rechaza creación");
    await using(var db=await factory.CreateDbContextAsync()){Assert(!await db.Orders.AnyAsync(o=>o.RequestId==checkout.RequestId)&&(await db.Products.SingleAsync(p=>p.Id==checkoutProduct.Id)).Stock==3,"precio cambiado no deja pedido ni stock consumido");}
    var saved=await service.CrearPedidoAsync(checkout with {TotalEsperado=quote.Total},null);
    Assert(saved.Total==quote.Total&&saved.Envio==1500&&saved.MensajeWhatsApp.Contains(saved.ShortCode)&&saved.MensajeWhatsApp.Contains("Dirección ficticia 123"),"payload WhatsApp conserva código y entrega");
    await service.CrearPedidoAsync(checkout with {TotalEsperado=quote.Total},null);
    await using(var db=await factory.CreateDbContextAsync()){
     var snapshot=await db.Orders.SingleAsync(o=>o.Id==saved.Id);
     Assert(snapshot.CheckoutTokenHash!=checkout.CheckoutToken&&snapshot.CheckoutTokenHash?.Length==64&&snapshot.DireccionEntrega==checkout.DireccionEntrega&&snapshot.Latitud==-34.60,"checkout guarda hash del token y snapshot de ubicación");
     Assert(await db.OutboxMessages.CountAsync(m=>m.OrderId==saved.Id&&m.Tipo=="PedidoCreado")==1&&(await db.Products.SingleAsync(p=>p.Id==checkoutProduct.Id)).Stock==2,"reintento público no duplica stock ni email de creación");
    }
    await ExpectAsync(()=>service.CotizarAsync(checkout with {Latitud=91}),"latitud fuera de rango rechazada");
    await ExpectAsync(()=>service.CotizarAsync(checkout with {DireccionEntrega=null}),"delivery sin dirección rechazado");
    await Phase2Checks.RunAsync(factory, scope, Assert);
    await Phase3Checks.RunAsync(factory, scope, Assert);
    await PaymentPreferencesChecks.RunAsync(factory, scope, Assert);
    Console.WriteLine($"{checks} verificaciones correctas, incluyendo SQL Server real.");
}
finally
{
    // Solo se borra la base efímera creada por esta ejecución, nunca una base recibida del usuario.
    SqlConnection.ClearAllPools();
    if (!Regex.IsMatch(name, @"^GastronomiaChecks_[0-9a-f]{32}$")) throw new InvalidOperationException("Nombre inseguro.");
    await using var drop = master.CreateCommand(); drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"; await drop.ExecuteNonQueryAsync();
}
sealed class TestFactory(DbContextOptions<GastronomiaDbContext> options, TenantContext tenant) : IDbContextFactory<GastronomiaDbContext>
{
    public GastronomiaDbContext CreateDbContext() => new(options, tenant);
    public Task<GastronomiaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}
sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
