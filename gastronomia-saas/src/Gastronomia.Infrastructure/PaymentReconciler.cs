using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class PaymentReconciler(IDbContextFactory<GastronomiaDbContext> factory, TenantContext tenant, TimeProvider clock) : IPaymentReconciler
{
    public Task<PaymentReconciliationResult> ReconcileAsync(VerifiedPaymentEvent e, CancellationToken ct = default)
    {
        if (e.TenantId == Guid.Empty || tenant.TenantId != e.TenantId || string.IsNullOrWhiteSpace(e.Proveedor) || e.Proveedor.Length > 80 || string.IsNullOrWhiteSpace(e.EventoId) || e.EventoId.Length > 160 || string.IsNullOrWhiteSpace(e.TransaccionId) || e.TransaccionId.Length > 160 || string.IsNullOrWhiteSpace(e.Moneda) || e.Moneda.Length != 3 || e.Estado is not ("approved" or "rejected" or "pending" or "in_process" or "authorized" or "in_mediation" or "cancelled" or "refunded" or "charged_back") || string.IsNullOrWhiteSpace(e.PayloadHash) || !System.Text.RegularExpressions.Regex.IsMatch(e.PayloadHash,"^[A-F0-9]{64}$")) throw new DomainException("Evento de pasarela inválido.");
        Money.Validate(e.Importe);
        return SqlTransactions.RunAsync(factory, async db =>
        {
            var previous = await db.PaymentLogs.SingleOrDefaultAsync(l => l.Proveedor == e.Proveedor && l.EventoId == e.EventoId, ct);
            if (previous is not null)
            {
                if (previous.PayloadHash != e.PayloadHash) throw new ConflictException("Evento repetido con contenido diferente.");
                return new PaymentReconciliationResult(true, previous.Resultado);
            }
            var payment = await db.Payments.SingleOrDefaultAsync(p => p.Proveedor == e.Proveedor && (e.PaymentId.HasValue ? p.Id == e.PaymentId : p.TransaccionIdExterna == e.TransaccionId), ct)
                ?? throw new PaymentGatewayUnavailableException("La transacción aún no está asociada. Reintentar la notificación.");
            if (e.PaymentId.HasValue && (payment.PreferenceId != e.PreferenceId || payment.OrderId != e.OrderId || e.PreferenceId is null)) throw new DomainException("El evento no corresponde al checkout guardado.");
            var order = await db.Orders.Include(o => o.Payments).SingleAsync(o => o.Id == payment.OrderId, ct);
            string result;
            if (payment.Metodo != MetodoPago.PasarelaOnline || payment.Importe != e.Importe || payment.Moneda != e.Moneda || order.Total != e.Importe || order.Moneda != e.Moneda) result = "ImporteOMonedaInconsistente";
            else if (order.EstadoPedido == EstadoPedido.Cancelado) result = e.Estado == "approved" ? "RequiereReembolso" : "PedidoCancelado";
            else if (e.Estado is "refunded" or "charged_back" or "in_mediation") result = "RequiereConciliacionManual";
            else if (e.Estado is "pending" or "in_process" or "authorized") result = "PagoEnProceso";
            else if (payment.Estado == EstadoPago.Pagado) result = e.Estado == "approved" && payment.TransaccionIdExterna != e.TransaccionId ? "RequiereReembolsoDuplicado" : "IgnoradoPagoConfirmado";
            else if (order.EstadoPago == EstadoPago.Pagado) result = "RequiereConciliacionManual";
            else if (e.Estado == "approved")
            {
                if (e.PreferenceId is not null) payment.AsociarIntentoVerificado(e.Proveedor, e.PreferenceId, e.TransaccionId);
                payment.ConfirmarPasarela(e.Proveedor, e.TransaccionId, e.Importe, e.Moneda, clock.GetUtcNow());
                order.RegistrarPago(EstadoPago.Pagado);
                db.OutboxMessages.Add(new OutboxMessage(order, "PagoConfirmado", clock.GetUtcNow()));
                result = "PagoConfirmado";
            }
            else
            {
                if (e.PreferenceId is not null) payment.AsociarIntentoVerificado(e.Proveedor, e.PreferenceId, e.TransaccionId);
                payment.RechazarPasarela(); order.RegistrarPago(EstadoPago.Rechazado); result = "PagoRechazado";
            }
            db.PaymentLogs.Add(new PaymentLog(payment, e.EventoId, e.PayloadHash, e.Estado, e.Importe, e.Moneda, result, clock.GetUtcNow(), e.TransaccionId));
            await db.SaveChangesAsync(ct);
            return new PaymentReconciliationResult(false, result);
        }, ct);
    }
}
