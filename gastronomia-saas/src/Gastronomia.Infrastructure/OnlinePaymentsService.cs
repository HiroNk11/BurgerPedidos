using System.Data;
using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class OnlinePaymentsService(IDbContextFactory<GastronomiaDbContext> factory, IPaymentGateway gateway) : IOnlinePaymentsService
{
    public async Task<PaymentIntentResult> StartAsync(Guid usuarioId, Guid orderId, CancellationToken ct = default)
    {
        await using var gate = await factory.CreateDbContextAsync(ct);
        // Session lock entre réplicas: serializa el inicio sin mantener una transacción de datos durante HTTP.
        await gate.Database.OpenConnectionAsync(ct);
        await using var command = gate.Database.GetDbConnection().CreateCommand();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=15000; SELECT @result;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = $"checkout:{gate.CurrentTenantId:N}:{orderId:N}"; command.Parameters.Add(parameter);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0) throw new ConflictException("Otro proceso está iniciando este pago. Volvé a consultar.");
        try
        {
            var order = await OwnedOrder(gate, usuarioId, orderId, ct);
            var payment = order.Payments.Single(p => p.Metodo == MetodoPago.PasarelaOnline);
            if (payment.PreferenceId is not null) return new(payment.Proveedor!, payment.TransaccionIdExterna, payment.PreferenceId, payment.CheckoutUrl);
            if (payment.TransaccionIdExterna is not null) return new(payment.Proveedor!, payment.TransaccionIdExterna);
            var intent = new PaymentIntentRequest(gate.CurrentTenantId, payment.Id, order.Id, payment.Importe, payment.Moneda, CheckoutItems(order));
            var remote = await gateway.CreateAsync(intent, ct);
            return await SqlTransactions.RunAsync(factory, async db =>
            {
                var current = await OwnedOrder(db, usuarioId, orderId, ct);
                var target = current.Payments.Single(p => p.Id == intent.PaymentId);
                if (remote.PreferenceId is not null && remote.CheckoutUrl is not null) target.AsociarPreferencia(remote.Proveedor, remote.PreferenceId, remote.CheckoutUrl);
                else if (remote.TransaccionId is not null) target.AsociarTransaccion(remote.Proveedor, remote.TransaccionId);
                else throw new PaymentGatewayUnavailableException("La pasarela no devolvió un checkout válido.");
                await db.SaveChangesAsync(ct); return remote;
            }, ct);
        }
        finally
        {
            // Los locks de sesión deben liberarse antes de devolver la conexión al pool.
            await using var release = gate.Database.GetDbConnection().CreateCommand();
            release.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
            var resource = release.CreateParameter(); resource.ParameterName = "@resource"; resource.Value = parameter.Value; release.Parameters.Add(resource);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
    public static IReadOnlyList<PaymentCheckoutItem> CheckoutItems(Order order)
    {
        var result = new List<PaymentCheckoutItem>();
        foreach (var item in order.Items.OrderBy(i => i.ProductId))
        {
            // Distribuye centavos sin alterar cantidad ni total neto histórico.
            var basePrice = decimal.Floor(item.Subtotal / item.Cantidad * 100) / 100;
            int extras = decimal.ToInt32((item.Subtotal - basePrice * item.Cantidad) * 100);
            if (item.Cantidad > extras && basePrice > 0) result.Add(new(item.ProductId.ToString("N"), item.NombreProductoHistorico, item.Cantidad - extras, basePrice));
            if (extras > 0) result.Add(new(item.ProductId.ToString("N") + "-cent", item.NombreProductoHistorico, extras, basePrice + .01m));
        }
        if (order.ImporteEnvio > 0) result.Add(new("delivery", "Envío", 1, order.ImporteEnvio));
        return result;
    }
    private static async Task<Order> OwnedOrder(GastronomiaDbContext db, Guid user, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Payments).Include(o => o.Items).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == orderId && o.UsuarioId == user, ct) ?? throw new MissingException("Pedido no encontrado.");
        if (order.EstadoPedido == EstadoPedido.Cancelado || order.EstadoPago == EstadoPago.Pagado || !order.Payments.Any(p => p.Metodo == MetodoPago.PasarelaOnline)) throw new DomainException("El pedido no admite iniciar un pago online.");
        return order;
    }
}
