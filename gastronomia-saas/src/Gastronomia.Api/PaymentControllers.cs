using Gastronomia.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Gastronomia.Api;

[ApiController, AllowAnonymous, Route("api/webhooks/pagos")]
[RequestSizeLimit(64 * 1024), EnableRateLimiting("payment-webhook")]
public sealed class PaymentWebhooksController(IPaymentGateway gateway, IPaymentReconciler reconciler, TenantContext tenant, OrderEvents events, ILogger<PaymentWebhooksController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        if (body.Length > 64 * 1024) return StatusCode(413);
        var verified = await gateway.VerifyAsync(new(body.ToArray(), Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase), Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString())), ct);
        // TenantId procede exclusivamente del adaptador autenticado, jamás de una query sin verificar.
        tenant.TenantId = verified.TenantId;
        var result = await reconciler.ReconcileAsync(verified, ct);
        if (!result.Duplicate && (result.Resultado.StartsWith("Requiere", StringComparison.Ordinal) || result.Resultado == "ImporteOMonedaInconsistente"))
            logger.LogError("Conciliación requiere revisión. Tenant {TenantId}, evento {EventoId}, resultado {Resultado}.", tenant.TenantId, verified.EventoId, result.Resultado);
        if (!result.Duplicate) events.Publish(tenant.TenantId);
        return Ok(result);
    }
}
[ApiController, Authorize(Policy = CustomerAuthentication.Policy), Route("api/pedidos")]
public sealed class OnlinePaymentsController(IOnlinePaymentsService service, TenantContext tenant) : ControllerBase
{
    [HttpPost("{id:guid}/iniciar-pago")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct) => Ok(await service.StartAsync(CustomerAuthentication.Actor(HttpContext, tenant), id, ct));
}
