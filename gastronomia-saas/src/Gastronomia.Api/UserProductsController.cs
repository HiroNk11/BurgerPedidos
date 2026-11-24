using Gastronomia.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Gastronomia.Api;

[ApiController, Authorize(Policy = CustomerAuthentication.Policy), Route("api/usuarios")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserProductsController(IUserProductsService service, TenantContext tenant) : ControllerBase
{
    private Guid Actor() => CustomerAuthentication.Actor(HttpContext, tenant);
    [HttpPost("favoritos/{productoId:guid}")]
    public async Task<ActionResult<FavoritoResult>> Toggle(Guid productoId, CancellationToken ct) => Ok(await service.ToggleAsync(Actor(), productoId, ct));
    [HttpGet("favoritos")]
    public async Task<IActionResult> Favorites(CancellationToken ct) => Ok(await service.FavoritosAsync(Actor(), ct));
    [HttpGet("mas-pedidos")]
    public async Task<IActionResult> Ranking(CancellationToken ct) => Ok(await service.MasPedidosAsync(Actor(), ct));
}
