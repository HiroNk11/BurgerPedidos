using System.Security.Claims;
using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Gastronomia.Api;

[ApiController]
[Route("api/tenants/{tenantId:guid}/orders")]
public sealed class OrdersController(IOrderService service,OrderEvents events,TenantContext tenant) : ControllerBase
{
    private Guid ActorId() => Guid.TryParse(User.FindFirstValue("sub"), out var id) ? id : throw new BadHttpRequestException("El token requiere un sub GUID válido.");
    [AllowAnonymous, HttpPost]
    public async Task<ActionResult<PedidoResult>> Crear(CrearPedidoRequest request, CancellationToken ct)
    {
        var result = await service.CrearPedidoAsync(request, User.Identity?.IsAuthenticated == true ? ActorId() : null, ct);
        events.Publish(tenant.TenantId);return StatusCode(StatusCodes.Status201Created, result);
    }
    [Authorize(Policy = "Staff"), HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<PedidoResult>> Aceptar(Guid id, [FromQuery] bool iniciarPreparacion, CancellationToken ct) {var result=await service.AceptarPedidoAsync(id,iniciarPreparacion,ct);events.Publish(tenant.TenantId);return Ok(result);}
    [Authorize(Policy = "Staff"), HttpPut("{id:guid}/state")]
    public async Task<ActionResult<PedidoResult>> Estado(Guid id, EstadoRequest request, CancellationToken ct) {var result=await service.CambiarEstadoAsync(id,request.Estado,ct);events.Publish(tenant.TenantId);return Ok(result);}
    [Authorize, HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<HistorialPedido>>> Historial([FromQuery] int pagina = 1, [FromQuery] int tamanio = 20, CancellationToken ct = default) => Ok(await service.HistorialAsync(ActorId(), pagina, tamanio, ct));
    [Authorize, HttpPost("link-guest")]
    public async Task<ActionResult> Vincular(CancellationToken ct)
    {
        if (!string.Equals(User.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase)) return Forbid();
        var email = User.FindFirstValue("email") ?? throw new BadHttpRequestException("Email verificado requerido.");
        int count = await service.VincularPedidosInvitadoAsync(email, ActorId(), ct); return Ok(new { Vinculados = count });
    }
    [Authorize(Policy = "Staff"), HttpPost("{id:guid}/payments/{paymentId:guid}/confirm-manual")]
    public async Task<IActionResult> ConfirmarPago(Guid id, Guid paymentId, ConfirmarPagoRequest request, CancellationToken ct) { await service.ConfirmarPagoManualAsync(id, paymentId, ActorId(), request.ImporteRecibido, ct);events.Publish(tenant.TenantId); return NoContent(); }
}
public sealed record EstadoRequest(EstadoPedido Estado);

public sealed record ConfirmarPagoRequest(decimal ImporteRecibido);
