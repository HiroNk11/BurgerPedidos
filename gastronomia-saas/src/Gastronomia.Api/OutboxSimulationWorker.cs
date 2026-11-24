using System.Globalization;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Api;
public sealed class OutboxSimulationWorker(IServiceScopeFactory scopes,ILogger<OutboxSimulationWorker> logger,TimeProvider clock,OrderEvents events):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken ct){
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(3));
  try{while(await timer.WaitForNextTickAsync(ct)){
   try{
    using var root=scopes.CreateScope();await using var catalog=await root.ServiceProvider.GetRequiredService<IDbContextFactory<GastronomiaDbContext>>().CreateDbContextAsync(ct);
    var tenants=await catalog.Tenants.Where(t=>t.Activo).Select(t=>t.Id).ToListAsync(ct);
    foreach(var tenantId in tenants){using var scope=scopes.CreateScope();scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId=tenantId;await using var db=await scope.ServiceProvider.GetRequiredService<IDbContextFactory<GastronomiaDbContext>>().CreateDbContextAsync(ct);
     var now=clock.GetUtcNow();var ids=await db.OutboxMessages.AsNoTracking().Where(m=>m.ProcesadoUtc==null&&(m.NextAttemptUtc==null||m.NextAttemptUtc<=now)&&(m.LockedUntilUtc==null||m.LockedUntilUtc<=now)).OrderBy(m=>m.CreadoUtc).Select(m=>m.Id).Take(20).ToListAsync(ct);
     foreach(var id in ids){Guid lease=Guid.NewGuid();now=clock.GetUtcNow();var until=now.AddMinutes(2);
      var claim=db.OutboxMessages.Where(m=>m.TenantId==tenantId&&m.Id==id&&m.ProcesadoUtc==null&&(m.LockedUntilUtc==null||m.LockedUntilUtc<=now));
      if(await claim.ExecuteUpdateAsync(s=>s.SetProperty(m=>m.LeaseId,(Guid?)lease).SetProperty(m=>m.LockedUntilUtc,(DateTimeOffset?)until).SetProperty(m=>m.Attempts,m=>m.Attempts+1),ct)!=1)continue;
      var message=await db.OutboxMessages.AsNoTracking().SingleAsync(m=>m.Id==id,ct);
      var owned=db.OutboxMessages.Where(m=>m.TenantId==tenantId&&m.Id==id&&m.LeaseId==lease);
      try{
       string? providerId=null;
       if(message.Tipo is "PedidoCreado" or "PedidoAceptado" or "PedidoEnCamino"){
        var order=await db.Orders.AsNoTracking().Include(o=>o.Items).SingleAsync(o=>o.Id==message.OrderId,ct);
        var subject=message.Tipo switch{"PedidoCreado"=>"Recibimos tu solicitud de pedido "+order.ShortCode,"PedidoAceptado"=>"Tu pedido fue aceptado "+order.ShortCode,_=>"Tu pedido va en camino "+order.ShortCode};
        var intro=message.Tipo switch{"PedidoCreado"=>"Recibimos tu solicitud. Todavía falta la confirmación del local.","PedidoAceptado"=>"El local aceptó tu pedido. La confirmación del pedido no acredita el pago.",_=>"Tu pedido va en camino."};
        var text=string.Join("\n",new[]{"Hola "+order.NombreCliente+",",intro,order.ShortCode}.Concat(order.Items.Select(i=>$"{i.Cantidad} x {i.NombreProductoHistorico}: {i.Subtotal.ToString("0.00",CultureInfo.InvariantCulture)} {order.Moneda}")).Concat(new[]{"Descuento: "+order.DescuentoTotal.ToString("0.00",CultureInfo.InvariantCulture),"Envío: "+order.ImporteEnvio.ToString("0.00",CultureInfo.InvariantCulture),"Total: "+order.Total.ToString("0.00",CultureInfo.InvariantCulture)+" "+order.Moneda,"Entrega: "+order.TipoEntrega,"Dirección: "+order.DireccionEntrega}));
        providerId=await scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>().SendAsync(new(tenantId,message.Id,order.Id,order.ShortCode,order.GuestEmail,subject,text,message.CreadoUtc),ct);
       }
       var processed=clock.GetUtcNow();await owned.ExecuteUpdateAsync(s=>s.SetProperty(m=>m.ProcesadoUtc,(DateTimeOffset?)processed).SetProperty(m=>m.ProviderMessageId,providerId).SetProperty(m=>m.LastError,(string?)null).SetProperty(m=>m.LeaseId,(Guid?)null).SetProperty(m=>m.LockedUntilUtc,(DateTimeOffset?)null),ct);events.Publish(tenantId);
      }catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
      catch(Exception ex){var retry=clock.GetUtcNow().AddSeconds(Math.Min(900,Math.Pow(2,Math.Min(message.Attempts,8))*5));string error=ex is HttpRequestException?ex.Message:"No se pudo entregar el correo; revisar configuración del proveedor.";await owned.ExecuteUpdateAsync(s=>s.SetProperty(m=>m.NextAttemptUtc,(DateTimeOffset?)retry).SetProperty(m=>m.LastError,error).SetProperty(m=>m.LeaseId,(Guid?)null).SetProperty(m=>m.LockedUntilUtc,(DateTimeOffset?)null),ct);logger.LogWarning("Evento {EventId} pendiente de reintento",id);}
     }
    }
   }catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){logger.LogWarning(ex,"Outbox pendiente; se reintentará.");}
  }}catch(OperationCanceledException)when(ct.IsCancellationRequested){}
 }
}