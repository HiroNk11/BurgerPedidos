using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Api;
[ApiController,Authorize(Policy="Staff"),Route("api/tenants/{tenantId:guid}/staff/orders")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class StaffOrdersController(IDbContextFactory<GastronomiaDbContext> factory) : ControllerBase {
 [HttpGet]
 public async Task<IActionResult> List([FromQuery]EstadoPedido? estado,[FromQuery]EstadoPago? pago,[FromQuery]string? buscar,[FromQuery]DateTimeOffset? desde,[FromQuery]DateTimeOffset? hasta,[FromQuery]bool soloPendientesPago=false,[FromQuery]int pagina=1,CancellationToken ct=default) {
  if(pagina<1 || pagina>100000 || buscar?.Length>80 || (desde.HasValue && hasta.HasValue && desde>=hasta)) return BadRequest(new {title="Filtros inválidos."});
  await using var db=await factory.CreateDbContextAsync(ct);
  var query=db.Orders.AsNoTracking();
  if(soloPendientesPago) query=query.Where(o=>o.EstadoPedido!=EstadoPedido.Cancelado&&o.EstadoPago!=EstadoPago.Pagado);
  if(estado.HasValue) query=query.Where(o=>o.EstadoPedido==estado);
  if(pago.HasValue) query=query.Where(o=>o.EstadoPago==pago);
  if(desde.HasValue) query=query.Where(o=>o.CreadoUtc>=desde);
  if(hasta.HasValue) query=query.Where(o=>o.CreadoUtc<hasta);
  if(!string.IsNullOrWhiteSpace(buscar)){string term=buscar.Trim();query=query.Where(o=>o.ShortCode.Contains(term)||o.GuestEmail.Contains(term)||o.GuestPhone.Contains(term));}
  int total=await query.CountAsync(ct);
  var items=await query.OrderByDescending(o=>o.CreadoUtc).ThenByDescending(o=>o.Id).Skip((pagina-1)*20).Take(20).Select(o=>new {o.Id,o.ShortCode,o.CreadoUtc,o.GuestEmail,o.GuestPhone,o.EstadoPedido,o.EstadoPago,o.Total,o.Moneda}).ToListAsync(ct);
  return Ok(new{items,total,pagina,totalPaginas=(int)Math.Ceiling(total/20m)});
 }
 [HttpGet("summary")]
 public async Task<IActionResult> Summary(CancellationToken ct) {
  await using var db=await factory.CreateDbContextAsync(ct);var orders=db.Orders.AsNoTracking();
  return Ok(new{pendientes=await orders.CountAsync(o=>o.EstadoPedido==EstadoPedido.PendienteConfirmacion,ct),enCocina=await orders.CountAsync(o=>o.EstadoPedido==EstadoPedido.Aceptado||o.EstadoPedido==EstadoPedido.EnPreparacion,ct),pagosPorRevisar=await orders.CountAsync(o=>o.EstadoPedido!=EstadoPedido.Cancelado&&o.EstadoPago!=EstadoPago.Pagado,ct),cobrado=await orders.Where(o=>o.EstadoPago==EstadoPago.Pagado).SumAsync(o=>(decimal?)o.Total,ct)??0,moneda=await db.Tenants.Where(t=>t.Id==db.CurrentTenantId).Select(t=>t.Moneda).SingleAsync(ct)});
 }
 [HttpGet("{id:guid}")]
 public async Task<IActionResult> Detail(Guid id,CancellationToken ct) {
  await using var db=await factory.CreateDbContextAsync(ct);
  var order=await db.Orders.AsNoTracking().Include(o=>o.Items).Include(o=>o.Payments).SingleOrDefaultAsync(o=>o.Id==id,ct);
  if(order is null) return NotFound();
  return Ok(new{order.Id,order.ShortCode,order.CreadoUtc,order.GuestEmail,order.GuestPhone,order.EstadoPedido,order.EstadoPago,order.ImporteBruto,order.DescuentoTotal,order.ImporteEnvio,order.NombreCliente,order.TipoEntrega,order.DireccionEntrega,order.Latitud,order.Longitud,order.Notas,order.Total,order.Moneda,order.PromocionHistorica,items=order.Items.Select(i=>new{i.NombreProductoHistorico,i.PrecioUnitarioHistorico,i.Cantidad,i.DescuentoAplicado,i.Subtotal}),payments=order.Payments.OrderByDescending(p=>p.CreadoUtc).Select(p=>new{p.Id,p.Metodo,p.Estado,p.Importe,p.Moneda,p.ComprobanteUrl,p.TransaccionIdExterna,p.CreadoUtc,p.ConfirmadoUtc,p.ConfirmadoPorUsuarioId})});
 }
}
