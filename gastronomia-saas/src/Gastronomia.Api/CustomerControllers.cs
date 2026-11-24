using Gastronomia.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Gastronomia.Api;

[ApiController, AllowAnonymous, Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerAuthController(IPasswordlessService service, TenantContext tenant) : ControllerBase
{
    private void SetTenant(Guid id) { if (id == Guid.Empty) throw new BadHttpRequestException("Comercio requerido."); tenant.TenantId = id; }
    [HttpPost("request-otp"), EnableRateLimiting("otp-request")]
    public async Task<IActionResult> RequestOtp(RequestOtpRequest request, CancellationToken ct)
    {
        SetTenant(request.TenantId);
        return Accepted(await service.RequestOtpAsync(request.Email, ct));
    }
    [HttpPost("verify-otp"), EnableRateLimiting("otp-verify")]
    public async Task<ActionResult<CustomerLoginResult>> VerifyOtp(VerifyOtpRequest request, CancellationToken ct)
    {
        SetTenant(request.TenantId);
        return Ok(await service.VerifyOtpAsync(request.Email, request.Codigo, ct));
    }
}

[ApiController, Authorize(Policy = CustomerAuthentication.Policy), Route("api/cuenta")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerAccountController(ICustomerAccountService service, TenantContext tenant) : ControllerBase
{
    private Guid Actor() => CustomerAuthentication.Actor(HttpContext, tenant);
    [HttpGet]
    public async Task<ActionResult<PerfilCliente>> Get(CancellationToken ct) => Ok(await service.GetAsync(Actor(), ct));
    [HttpPut]
    public async Task<ActionResult<PerfilCliente>> Update(PerfilRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(Actor(), request, ct));
    [HttpGet("direcciones")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => Ok((await service.GetAsync(Actor(), ct)).Direcciones);
    [HttpGet("direcciones/{id:guid}")]
    public async Task<ActionResult<DireccionResult>> Address(Guid id, CancellationToken ct)
    {
        var result = (await service.GetAsync(Actor(), ct)).Direcciones.SingleOrDefault(d => d.Id == id);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpPost("direcciones")]
    public async Task<ActionResult<DireccionResult>> CreateAddress(DireccionRequest request, CancellationToken ct)
    {
        var result = await service.SaveAddressAsync(Actor(), null, request, ct);
        return Created($"/api/cuenta/direcciones/{result.Id}", result);
    }
    [HttpPut("direcciones/{id:guid}")]
    public async Task<ActionResult<DireccionResult>> UpdateAddress(Guid id, DireccionRequest request, CancellationToken ct) => Ok(await service.SaveAddressAsync(Actor(), id, request, ct));
    [HttpDelete("direcciones/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct) { await service.DeleteAddressAsync(Actor(), id, ct); return NoContent(); }
}

[ApiController, Authorize(Policy = CustomerAuthentication.Policy), Route("api/pedidos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerOrdersController(ICustomerOrdersService service, TenantContext tenant) : ControllerBase
{
    private Guid Actor() => CustomerAuthentication.Actor(HttpContext, tenant);
    [HttpGet("mis-pedidos")]
    public async Task<ActionResult<MisPedidosResult>> Mine([FromQuery] int pagina = 1, [FromQuery] int tamanio = 20, CancellationToken ct = default) => Ok(await service.HistoryAsync(Actor(), pagina, tamanio, ct));
    [HttpGet("mis-pedidos/{id:guid}")]
    public async Task<ActionResult<PedidoCliente>> Detail(Guid id, CancellationToken ct) => Ok(await service.DetailAsync(Actor(), id, ct));
    [HttpPost("reorder/{idPedidoHistorico:guid}")]
    public async Task<ActionResult<CarritoPropuesto>> Reorder(Guid idPedidoHistorico, CancellationToken ct) => Ok(await service.ReorderAsync(Actor(), idPedidoHistorico, ct));
}
