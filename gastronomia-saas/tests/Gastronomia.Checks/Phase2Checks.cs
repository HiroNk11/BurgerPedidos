using System.Collections.Concurrent;
using System.Security.Cryptography;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal static class Phase2Checks
{
    public static async Task RunAsync(IDbContextFactory<GastronomiaDbContext> factory, TenantContext scope, Action<bool, string> assert)
    {
        var time = new CustomerTestTime(new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero));
        var mail = new CustomerTestMail();
        var auth = new PasswordlessService(factory, scope, time, new HmacOtpCodeHasher(RandomNumberGenerator.GetBytes(32)), mail, new CustomerTestIssuer(time));
        var accounts = new CustomerAccountService(factory); var history = new CustomerOrdersService(factory);
        var orders = new OrderService(factory, scope, time);
        var tenant = new Tenant("Cuentas prueba", "5491124830571"); var otherTenant = new Tenant("Otro comercio cuentas", "5491124830571");
        scope.TenantId = tenant.Id;
        var product = new Product(tenant.Id, "Burger actual", 100, 30);
        var gone = new Product(tenant.Id, "Sin stock", 200, 30);
        var inactive = new Product(tenant.Id, "Fuera de carta", 300, 30);
        await using (var db = await factory.CreateDbContextAsync()) { db.AddRange(tenant, otherTenant, product, gone, inactive); await db.SaveChangesAsync(); }
        string email = "cuenta@example.com";
        CrearPedidoRequest Request(string address, List<CrearItemRequest>? items = null) => new(Guid.NewGuid(), address, "5491100000000", MetodoPago.Efectivo, items ?? [new(product.Id, 1)]);
        async Task Reject(Func<Task> action, string label)
        {
            try { await action(); }
            catch (Exception e) when (e is InvalidOtpException or MissingException or DomainException) { assert(true, label); return; }
            throw new Exception("FAIL: " + label);
        }
        var guest = await orders.CrearPedidoAsync(Request(" CUENTA@example.com "), null);
        await auth.RequestOtpAsync(email);
        var firstMail = mail.Last(tenant.Id, email);
        await auth.RequestOtpAsync(email);
        assert(mail.Count == 1, "OTP cooldown evita correos duplicados");
        await using (var db = await factory.CreateDbContextAsync())
        {
            var challenge = await db.OtpChallenges.SingleAsync();
            assert(challenge.CodeHash.Length == 32 && challenge.ExpiraUtc - challenge.CreadoUtc == TimeSpan.FromMinutes(10), "OTP guarda HMAC y vencimiento de diez minutos");
        }
        string Wrong(string valid) => valid == "000000" ? "000001" : "000000";
        await Reject(() => auth.VerifyOtpAsync(email, Wrong(firstMail.Codigo)), "OTP incorrecto rechazado");
        var login = await auth.VerifyOtpAsync(" CUENTA@EXAMPLE.COM ", firstMail.Codigo);
        assert(login.NuevoUsuario && login.PedidosVinculados == 1 && login.Cliente.Email.ToLowerInvariant() == email, "OTP verificado crea usuario y vincula compras normalizadas");
        await using (var db = await factory.CreateDbContextAsync())
        {
            assert((await db.OtpChallenges.SingleAsync()).IntentosFallidos == 1 && (await db.Orders.SingleAsync(o => o.Id == guest.Id)).UsuarioId == login.Cliente.Id, "intentos fallidos y vínculo se persisten en SQL");
        }
        await Reject(() => auth.VerifyOtpAsync(email, firstMail.Codigo), "OTP consumido no permite replay");
        await accounts.UpdateAsync(login.Cliente.Id, new("Cliente Uno", "5491100000000"));
        var address = await accounts.SaveAddressAsync(login.Cliente.Id, null, new("Calle ficticia", "123", "2 A", -34.6, -58.4));
        var edited = await accounts.SaveAddressAsync(login.Cliente.Id, address.Id, new("Otra calle", "456", null));
        var profile = await accounts.GetAsync(login.Cliente.Id);
        assert(profile.Nombre == "Cliente Uno" && profile.Direcciones.Single().Calle == "Otra calle" && edited.Latitud is null, "perfil y ABM de dirección persisten datos");
        await Reject(() => accounts.SaveAddressAsync(login.Cliente.Id, null, new("X", "1", null, 91, 0)), "dirección con coordenadas inválidas rechazada");
        await auth.RequestOtpAsync("otro@example.com");
        var second = await auth.VerifyOtpAsync("otro@example.com", mail.Last(tenant.Id, "otro@example.com").Codigo);
        await Reject(() => accounts.SaveAddressAsync(second.Cliente.Id, address.Id, new("Ajena", "1")), "otro usuario no modifica dirección ajena");
        await Reject(() => accounts.DeleteAddressAsync(second.Cliente.Id, address.Id), "otro usuario no elimina dirección ajena");
        await accounts.DeleteAddressAsync(login.Cliente.Id, address.Id);
        assert((await accounts.GetAsync(login.Cliente.Id)).Direcciones.Count == 0, "baja de dirección no altera pedidos históricos");
        await auth.RequestOtpAsync("intentos@example.com");
        var lockedMail = mail.Last(tenant.Id, "intentos@example.com");
        for (int i = 0; i < 5; i++) await Reject(() => auth.VerifyOtpAsync(lockedMail.Email, Wrong(lockedMail.Codigo)), "OTP intento fallido contabilizado " + (i + 1));
        await Reject(() => auth.VerifyOtpAsync(lockedMail.Email, lockedMail.Codigo), "OTP se bloquea después de cinco fallos");
        await auth.RequestOtpAsync("expira@example.com");
        var expires = mail.Last(tenant.Id, "expira@example.com"); time.Advance(TimeSpan.FromMinutes(10));
        await Reject(() => auth.VerifyOtpAsync(expires.Email, expires.Codigo), "OTP no se acepta al alcanzar su vencimiento");
        await auth.RequestOtpAsync("carrera@example.com"); var racing = mail.Last(tenant.Id, "carrera@example.com");
        async Task<bool> VerifyRace() { try { await auth.VerifyOtpAsync(racing.Email, racing.Codigo); return true; } catch (InvalidOtpException) { return false; } }
        var results = await Task.WhenAll(VerifyRace(), VerifyRace());
        assert(results.Count(x => x) == 1, "dos verificaciones simultáneas consumen OTP una sola vez");
        // A later guest purchase is linked on the next verified login as well.
        var laterGuest = await orders.CrearPedidoAsync(Request(email), null);
        await auth.RequestOtpAsync(email);
        var repeated = await auth.VerifyOtpAsync(email, mail.Last(tenant.Id, email).Codigo);
        assert(!repeated.NuevoUsuario && repeated.Cliente.Id == login.Cliente.Id && repeated.PedidosVinculados == 1, "nuevo login conserva usuario y recupera compras invitadas posteriores");
        var historical = await orders.CrearPedidoAsync(Request(email, [new(product.Id, 3), new(gone.Id, 1), new(inactive.Id, 1)]), login.Cliente.Id);
        await orders.CambiarEstadoAsync(historical.Id, EstadoPedido.Cancelado);
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Precio, 150).SetProperty(p => p.Stock, 2));
            await db.Products.Where(p => p.Id == gone.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 0));
            await db.Products.Where(p => p.Id == inactive.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, false));
        }
        var cart = await history.ReorderAsync(login.Cliente.Id, historical.Id);
        assert(cart.Items.Count == 1 && cart.Items[0].PrecioUnitarioActual == 150 && cart.Items[0].Cantidad == 2 && cart.SubtotalProductos == 300, "reorder recalcula precio y ajusta cantidad al stock actual");
        assert(cart.ItemsRemovidos.Count == 2 && cart.Warnings.Any(w => w.Code == "PrecioActualizado") && cart.Warnings.Any(w => w.Code == "CantidadReducida"), "reorder informa cambios y productos removidos");
        await using (var db = await factory.CreateDbContextAsync())
        {
            assert((await db.Orders.SingleAsync(o => o.Id == historical.Id)).Total == 800 && (await db.Products.SingleAsync(p => p.Id == product.Id)).Stock == 2 && await db.Orders.CountAsync() == 3, "reorder no crea pedido, reserva stock ni cambia snapshot");
        }
        await Reject(() => history.ReorderAsync(second.Cliente.Id, historical.Id), "reorder de otro cliente no expone pedido");
        await Reject(() => history.ReorderAsync(login.Cliente.Id, guest.Id), "pedido activo no se duplica mediante reorder histórico");
        var list = await history.HistoryAsync(login.Cliente.Id);
        assert(list.TotalActivos == 2 && list.TotalHistoricos == 1 && list.Historicos.Single().Id == historical.Id, "historial separa activos de entregados y cancelados");
        await auth.RequestOtpAsync("limite@example.com");
        for (int i = 0; i < 4; i++) { time.Advance(TimeSpan.FromSeconds(61)); await auth.RequestOtpAsync("limite@example.com"); }
        int mailCount = mail.Count;
        time.Advance(TimeSpan.FromSeconds(61)); await auth.RequestOtpAsync("limite@example.com");
        assert(mail.Count == mailCount, "OTP limita reenvíos por email aunque cambie la IP");
        time.Advance(TimeSpan.FromHours(1)); await auth.RequestOtpAsync("limite@example.com");
        assert(mail.Count == mailCount + 1, "OTP permite solicitar otro código al renovar ventana horaria");
        scope.TenantId = otherTenant.Id;
        await Reject(() => history.DetailAsync(login.Cliente.Id, historical.Id), "cuenta de otro comercio no accede al historial");
        await Reject(() => auth.VerifyOtpAsync(email, mail.Last(tenant.Id, email).Codigo), "OTP de otro comercio rechazado");
        scope.TenantId = tenant.Id;
    }
}
internal sealed class CustomerTestTime(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset current = now;
    public override DateTimeOffset GetUtcNow() => current;
    public void Advance(TimeSpan time) => current += time;
}
internal sealed class CustomerTestMail : IEmailService
{
    private readonly ConcurrentDictionary<(Guid, string), OtpEmail> messages = new();
    public int Count { get; private set; }
    public Task EnviarOtpAsync(OtpEmail email, CancellationToken ct) { messages[(email.TenantId, email.Email.Trim().ToUpperInvariant())] = email; Count++; return Task.CompletedTask; }
    public OtpEmail Last(Guid tenant, string email) => messages[(tenant, email.Trim().ToUpperInvariant())];
}
internal sealed class CustomerTestIssuer(TimeProvider clock) : IAccessTokenIssuer
{
    public AccessTokenResult Issue(Usuario user) => new("test-" + user.Id, "Bearer", clock.GetUtcNow().AddHours(1));
}
