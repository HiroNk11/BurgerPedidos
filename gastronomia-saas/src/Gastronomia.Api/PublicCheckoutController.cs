using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Api;
public sealed record CalcularTotalesRequest(Guid TenantId,
 [Required, MinLength(1), MaxLength(100)] List<CrearItemRequest> Items,
 [MaxLength(40)] string? CodigoCupon = null, TipoEntrega TipoEntrega = TipoEntrega.Retiro);
public sealed record CheckoutRequest(Guid TenantId,CrearPedidoRequest Pedido);
[ApiController,AllowAnonymous,Route("api/pedidos"),EnableRateLimiting("checkout")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PublicCheckoutController(TenantContext tenant,IDbContextFactory<GastronomiaDbContext> factory,IOrderService orders,OrderEvents events):ControllerBase {
 private void SetTenant(Guid id){if(id==Guid.Empty)throw new DomainException("Comercio requerido.");tenant.TenantId=id;}
 [HttpPost("cotizar")]
 public async Task<IActionResult> Quote(CheckoutRequest request,CancellationToken ct){SetTenant(request.TenantId);var user=await OptionalActor(request.TenantId);return Ok(await orders.CotizarAsync(request.Pedido,ct,user));}
 [HttpPost("calcular-totales")]
 public async Task<IActionResult> Calculate(CalcularTotalesRequest request,CancellationToken ct)
 {
  SetTenant(request.TenantId);var userId=await OptionalActor(request.TenantId);
  await using var db=await factory.CreateDbContextAsync(ct);
  var email=userId.HasValue ? await db.Users.Where(u=>u.Id==userId&&u.EmailVerificado).Select(u=>u.Email).SingleOrDefaultAsync(ct)??throw new MissingException("Cuenta no encontrada.") : "quote@example.com";
  var quoteRequest=new CrearPedidoRequest(Guid.NewGuid(),email,"1100000000",MetodoPago.Efectivo,request.Items,request.CodigoCupon,TipoEntrega:request.TipoEntrega,DireccionEntrega:request.TipoEntrega==TipoEntrega.Delivery?"Cotización":null);
  return Ok(await orders.CotizarAsync(quoteRequest,ct,userId));
 }
 private async Task<Guid?> OptionalActor(Guid requestedTenant)
 {
  if(Request.Headers.Authorization.Count==0)return null;
  var authentication=await HttpContext.AuthenticateAsync(CustomerAuthentication.Scheme);
  if(!authentication.Succeeded||authentication.Principal is null)throw new BadHttpRequestException("La sesión venció.",401);
  HttpContext.User=authentication.Principal;
  if(!User.IsInRole("customer")||User.FindFirst("email_verified")?.Value!="true")throw new BadHttpRequestException("Acceso de cliente requerido.",403);
  var actor=CustomerAuthentication.Actor(HttpContext,tenant);
  if(tenant.TenantId!=requestedTenant)throw new BadHttpRequestException("Comercio no autorizado.",403);
  return actor;
 }
 [HttpPost]
 public async Task<IActionResult> Create(CheckoutRequest request,CancellationToken ct)
 {
  SetTenant(request.TenantId);
  if(request.Pedido.CheckoutToken is null)throw new DomainException("Token de checkout requerido.");
  var userId=await OptionalActor(request.TenantId);
  var result=await orders.CrearPedidoAsync(request.Pedido,userId,ct);
  events.Publish(tenant.TenantId);
  return StatusCode(201,result);
 }
 [HttpGet("{id:guid}")]
 public async Task<IActionResult> Status(Guid id,[FromQuery]Guid tenantId,CancellationToken ct){SetTenant(tenantId);await using var db=await factory.CreateDbContextAsync(ct);var o=await AuthorizedOrder(db,id,ct);return Ok(new{o.Id,o.ShortCode,o.EstadoPedido,o.EstadoPago,o.Total,o.Moneda});}
 internal async Task<Order> AuthorizedOrder(GastronomiaDbContext db,Guid id,CancellationToken ct){
  if(Request.Headers.Authorization.Count>0)
  {
   var authentication=await HttpContext.AuthenticateAsync(CustomerAuthentication.Scheme);
   if(!authentication.Succeeded||authentication.Principal is null)throw new BadHttpRequestException("La sesión venció. Volvé a ingresar.",StatusCodes.Status401Unauthorized);
   var principal=authentication.Principal;
   if(!principal.IsInRole("customer")||principal.FindFirst("email_verified")?.Value!="true"||!Guid.TryParse(principal.FindFirst("tenant_id")?.Value,out var ownerTenant)||ownerTenant!=db.CurrentTenantId||!Guid.TryParse(principal.FindFirst("sub")?.Value,out var ownerId))throw new MissingException("Pedido no encontrado.");
   return await db.Orders.Include(o=>o.Payments).SingleOrDefaultAsync(o=>o.Id==id&&o.UsuarioId==ownerId,ct)??throw new MissingException("Pedido no encontrado.");
  }
  var token=Request.Headers["X-Order-Token"].ToString();if(token.Length!=64||!System.Text.RegularExpressions.Regex.IsMatch(token,"^[A-Fa-f0-9]{64}$"))throw new MissingException("Pedido no encontrado.");
  var order=await db.Orders.Include(o=>o.Payments).SingleOrDefaultAsync(o=>o.Id==id,ct);var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
  if(order?.CheckoutTokenHash is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash),Encoding.ASCII.GetBytes(order.CheckoutTokenHash)))throw new MissingException("Pedido no encontrado.");return order;
 }
 [HttpPost("{id:guid}/comprobante"),RequestSizeLimit(6*1024*1024)]
 public async Task<IActionResult> Receipt(Guid id,[FromQuery]Guid tenantId,IFormFile archivo,[FromServices]IWebHostEnvironment env,CancellationToken ct){
  SetTenant(tenantId);if(archivo.Length is <=0 or >5*1024*1024)throw new DomainException("La imagen debe pesar hasta 5 MB.");
  await using var db=await factory.CreateDbContextAsync(ct);var order=await AuthorizedOrder(db,id,ct);var payment=order.Payments.SingleOrDefault(p=>p.Metodo==MetodoPago.TransferenciaCBU)??throw new DomainException("El pedido no usa transferencia.");
  if(order.EstadoPedido==EstadoPedido.Cancelado||order.EstadoPago==EstadoPago.Pagado)throw new ConflictException("El pedido ya no admite comprobantes.");
  await using var stream=archivo.OpenReadStream();using var memory=new MemoryStream();await stream.CopyToAsync(memory,ct);var bytes=memory.ToArray();
  string? ext=bytes.Length>8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})?"png":bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255?"jpg":bytes.Length>12&&Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP"?"webp":null;
  if(ext is null)throw new DomainException("Solo se admiten imágenes PNG, JPEG o WebP.");
  string file=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+"."+ext;string directory=Path.Combine(env.ContentRootPath,"App_Data","receipts",tenantId.ToString("N"),id.ToString("N"));Directory.CreateDirectory(directory);
  string path=Path.Combine(directory,file);
  if(!System.IO.File.Exists(path)) {
   string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {await System.IO.File.WriteAllBytesAsync(temporary,bytes,ct);try{System.IO.File.Move(temporary,path,false);}catch(IOException)when(System.IO.File.Exists(path)) {}}
   finally {if(System.IO.File.Exists(temporary))System.IO.File.Delete(temporary);}
  }
  var url=$"/api/tenants/{tenantId}/staff/receipts/{id}/{file}";
  payment.AdjuntarComprobante(url);order.RegistrarPago(EstadoPago.ComprobanteEnviado);
  try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){throw new ConflictException("El pedido cambió. Actualizá antes de reintentar.");}
  events.Publish(tenantId);return Ok(new{estadoPago=order.EstadoPago,comprobanteUrl=url});
 }
}
[ApiController,AllowAnonymous,Route("api/tienda")]
public sealed class StoreController(TenantContext tenant,IDbContextFactory<GastronomiaDbContext> factory):ControllerBase{
 [HttpGet]
 public async Task<IActionResult> Get([FromQuery]Guid tenantId,CancellationToken ct){tenant.TenantId=tenantId;await using var db=await factory.CreateDbContextAsync(ct);var t=await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==tenantId&&t.Activo,ct);if(t is null)return NotFound();var products=await db.Products.AsNoTracking().Where(p=>p.Activo).OrderBy(p=>p.Nombre).Select(p=>new{p.Id,p.Nombre,p.Precio,p.Stock,p.Categoria,p.Descripcion,p.ImagenKey}).ToListAsync(ct);return Ok(new{t.Id,t.Nombre,t.Moneda,envio=t.TarifaEnvio,products});}
}
[ApiController,Authorize(Policy="Staff"),Route("api/tenants/{tenantId:guid}/staff/receipts")]
public sealed class StaffReceiptsController(IDbContextFactory<GastronomiaDbContext> factory,IWebHostEnvironment env):ControllerBase{
 [HttpGet("{orderId:guid}/{file}")]
 public async Task<IActionResult> Get(Guid tenantId,Guid orderId,string file,CancellationToken ct){if(!System.Text.RegularExpressions.Regex.IsMatch(file,"^[a-f0-9]{64}\\.(png|jpg|webp)$"))return NotFound();await using var db=await factory.CreateDbContextAsync(ct);string url=$"/api/tenants/{tenantId}/staff/receipts/{orderId}/{file}";if(!await db.Payments.AnyAsync(p=>p.OrderId==orderId&&p.ComprobanteUrl==url,ct))return NotFound();string path=Path.Combine(env.ContentRootPath,"App_Data","receipts",tenantId.ToString("N"),orderId.ToString("N"),file);if(!System.IO.File.Exists(path))return NotFound();Response.Headers.CacheControl="no-store";return PhysicalFile(path,file.EndsWith(".png")?"image/png":file.EndsWith(".webp")?"image/webp":"image/jpeg");}
}