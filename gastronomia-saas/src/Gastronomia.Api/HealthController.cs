using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Api;

[ApiController, AllowAnonymous, Route("health")]
public sealed class HealthController(IDbContextFactory<GastronomiaDbContext> factory) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new {status = "live"});
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(2));
            await using var db = await factory.CreateDbContextAsync(ct);
            // Comprueba conexión y esquema de checkout; no expone nombres de base ni errores SQL.
            await db.Payments.IgnoreQueryFilters().AsNoTracking().Select(p => new {p.PreferenceId, p.CheckoutUrl}).Take(1).ToListAsync(deadline.Token);
            return Ok(new {status = "ready"});
        }
        catch (Exception) when (!ct.IsCancellationRequested) {return StatusCode(503, new {status = "not-ready"});}
    }
}
