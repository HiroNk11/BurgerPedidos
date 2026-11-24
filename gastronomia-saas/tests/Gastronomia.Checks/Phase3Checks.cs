using System.Security.Cryptography;
using System.Text;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class Phase3Checks
{
    public static async Task RunAsync(IDbContextFactory<GastronomiaDbContext> factory, TenantContext scope, Action<bool,string> check)
    {
        var clock = new FixedTime(new DateTimeOffset(2026,11,12,12,0,0,TimeSpan.Zero));
        var tenant = new Tenant("Fase 3", "5491100000000"); scope.TenantId = tenant.Id;
        var a = new Usuario(tenant.Id, Guid.NewGuid(), "uno.fase3@example.com", true);
        var b = new Usuario(tenant.Id, Guid.NewGuid(), "dos.fase3@example.com", true);
        var p = new Product(tenant.Id,"Favorita",100,100);
        var q = new Product(tenant.Id,"Segunda",200,100);
        var quota = new Promotion(tenant.Id,"Personal",TipoPromocion.Cupon,TipoDescuento.Porcentaje,20,clock.GetUtcNow().AddDays(-1),clock.GetUtcNow().AddDays(1),2,"PERSONAL",maxUsesPerUser:1);
        var legacy = new Promotion(tenant.Id,"Legacy",TipoPromocion.Cupon,TipoDescuento.ImporteFijo,10,clock.GetUtcNow().AddDays(-1),clock.GetUtcNow().AddDays(1),100,"LEGACY");
        await using(var db=await factory.CreateDbContextAsync()){db.AddRange(tenant,a,b,p,q,quota,legacy);await db.SaveChangesAsync();}
        var products = new UserProductsService(factory,clock);
        var orders = new OrderService(factory,scope,clock);
        CrearPedidoRequest Request(Usuario user, Product product, int count=1, string? coupon=null, MetodoPago method=MetodoPago.Efectivo) => new(Guid.NewGuid(),user.Email,"1100000000",method,[new(product.Id,count)],coupon);
        async Task Fails(Func<Task> action,string label){try{await action();}catch(Exception e)when(e is DomainException or MissingException or ConflictException){check(true,label);return;}throw new Exception("FAIL: "+label);}
        check((await products.ToggleAsync(a.Id,p.Id)).EsFavorito,"favorito agrega");
        check((await products.FavoritosAsync(a.Id)).Single().Id==p.Id && (await products.FavoritosAsync(b.Id)).Count==0,"favoritos privados por cuenta");
        check(!(await products.ToggleAsync(a.Id,p.Id)).EsFavorito,"favorito quita");
        await Task.WhenAll(products.ToggleAsync(a.Id,p.Id),products.ToggleAsync(a.Id,p.Id));
        check((await products.FavoritosAsync(a.Id)).Count==0,"toggles concurrentes serializados sin duplicados");
        await Fails(()=>products.ToggleAsync(a.Id,Guid.NewGuid()),"favorito rechaza producto inexistente");
        var one = await orders.CrearPedidoAsync(Request(a,p,3),a.Id);
        await orders.CrearPedidoAsync(Request(a,q,2),a.Id);
        var canceled = await orders.CrearPedidoAsync(Request(a,q,20),a.Id);await orders.CambiarEstadoAsync(canceled.Id,EstadoPedido.Cancelado);
        await orders.CrearPedidoAsync(Request(b,q,10),b.Id);
        var rank=await products.MasPedidosAsync(a.Id);
        check(rank.Count==2 && rank[0].Producto.Id==p.Id && rank[0].CantidadPedida==3 && rank[1].CantidadPedida==2,"ranking suma cantidades, excluye cancelados y otras cuentas");
        await Fails(()=>orders.CotizarAsync(Request(a,p,coupon:"PERSONAL")),"cupón personal requiere identidad verificada");
        var quote=await orders.CotizarAsync(Request(a,p,coupon:"personal"),usuarioId:a.Id);
        check(quote.Total==80 && quote.Descuento==20 && quote.CodigoCupon=="PERSONAL","cotización autentica cupón personal y no consume");
        async Task<PedidoResult?> TryBuy(){try{return await orders.CrearPedidoAsync(Request(a,p,coupon:"PERSONAL"),a.Id);}catch(Exception e)when(e is DomainException or ConflictException){return null;}}
        var race=await Task.WhenAll(TryBuy(),TryBuy());
        check(race.Count(o=>o is not null)==1,"dos pedidos simultáneos respetan límite de una compra por cuenta");
        await orders.CrearPedidoAsync(Request(b,p,coupon:"PERSONAL"),b.Id);
        await using(var db=await factory.CreateDbContextAsync()){check((await db.Promotions.SingleAsync(x=>x.Id==quota.Id)).CurrentUses==2,"cuota global contabilizada una vez por pedido");}
        await Fails(()=>orders.CotizarAsync(Request(a,p,coupon:"PERSONAL"),usuarioId:a.Id),"cotización rechaza cupón agotado");
        var winner=race.Single(o=>o is not null)!;await orders.CambiarEstadoAsync(winner.Id,EstadoPedido.Cancelado);
        check((await orders.CotizarAsync(Request(a,p,coupon:"PERSONAL"),usuarioId:a.Id)).Total==80,"cancelación libera cuota global y personal");
        await orders.CrearPedidoAsync(Request(a,p,coupon:"LEGACY"),null);
        await using(var db=await factory.CreateDbContextAsync()){await db.Promotions.Where(x=>x.Id==legacy.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.MaxUsesPerUser,1));}
        await Fails(()=>orders.CotizarAsync(Request(a,p,coupon:"LEGACY"),usuarioId:a.Id),"límite personal cuenta compras invitadas por email verificado");
        var online = await orders.CrearPedidoAsync(Request(a,p,method:MetodoPago.PasarelaOnline),a.Id);
        var paymentService = new OnlinePaymentsService(factory,new TestGateway());
        await Fails(()=>paymentService.StartAsync(b.Id,online.Id),"otro usuario no puede asociar intento de pago");
        var intent=await paymentService.StartAsync(a.Id,online.Id);
        check(intent==await paymentService.StartAsync(a.Id,online.Id),"iniciar pago es idempotente con PaymentId");
        var reconciler = new PaymentReconciler(factory,scope,clock);
        VerifiedPaymentEvent Event(string id,string state="approved",decimal amount=100,string currency="ARS",string? transaction=null) => new(tenant.Id,"Test",id,transaction??intent.TransaccionId!,state,amount,currency,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id+state+amount+currency))));
        var prefix=Guid.NewGuid().ToString("N");
        var wrong=await reconciler.ReconcileAsync(Event(prefix+"wrong",amount:99));
        check(wrong.Resultado=="ImporteOMonedaInconsistente","webhook no confirma importe diferente");
        check((await reconciler.ReconcileAsync(Event(prefix+"currency",currency:"USD"))).Resultado=="ImporteOMonedaInconsistente","webhook rechaza moneda diferente");
        var rejection=await reconciler.ReconcileAsync(Event(prefix+"reject",state:"rejected"));
        check(rejection.Resultado=="PagoRechazado","webhook rejected cambia pago sin cancelar pedido");
        var approval=Event(prefix+"approve");var duplicates=await Task.WhenAll(reconciler.ReconcileAsync(approval),reconciler.ReconcileAsync(approval));
        check(duplicates.Count(d=>d.Duplicate)==1,"webhooks simultáneos procesan evento una sola vez");
        check((await reconciler.ReconcileAsync(Event(prefix+"late",state:"rejected"))).Resultado=="IgnoradoPagoConfirmado","rechazo tardío no revierte pago confirmado");
        await Fails(()=>reconciler.ReconcileAsync(approval with{PayloadHash=new string('A',64)}),"mismo evento con payload diferente da conflicto");
        await using(var db=await factory.CreateDbContextAsync())
        {
            var o=await db.Orders.SingleAsync(o=>o.Id==online.Id);check(o.EstadoPago==EstadoPago.Pagado&&o.EstadoPedido==EstadoPedido.PendienteConfirmacion,"pago confirmado no altera estado del pedido");
            check(await db.OutboxMessages.CountAsync(x=>x.OrderId==online.Id&&x.Tipo=="PagoConfirmado")==1,"webhook no duplica notificación de pago");
            check(await db.PaymentLogs.CountAsync(x=>x.EventoId==approval.EventoId)==1,"evento auditado con índice único");
            try{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.PaymentLogs SET Resultado=Resultado WHERE EventoId={approval.EventoId}");throw new Exception("FAIL: auditoría SQL");}catch(SqlException ex)when(ex.Number==51003){check(true,"auditoría SQL inmutable");}
        }
        var lateOrder=await orders.CrearPedidoAsync(Request(a,p,method:MetodoPago.PasarelaOnline),a.Id);
        var lateIntent=await paymentService.StartAsync(a.Id,lateOrder.Id);await orders.CambiarEstadoAsync(lateOrder.Id,EstadoPedido.Cancelado);
        check((await reconciler.ReconcileAsync(Event(prefix+"refund",transaction:lateIntent.TransaccionId))).Resultado=="RequiereReembolso","aprobación tras cancelación registra necesidad de reembolso");
        var previousTenant=scope.TenantId;scope.TenantId=Guid.NewGuid();
        check((await products.MasPedidosAsync(a.Id).ContinueWith(t=>t.IsFaulted)).Equals(true),"ranking aislado por tenant");
        scope.TenantId=previousTenant;
    }
    private sealed class TestGateway : IPaymentGateway
    {
        public Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest r,CancellationToken ct)=>Task.FromResult(new PaymentIntentResult("Test","t_"+r.PaymentId.ToString("N")));
        public Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest r,CancellationToken ct)=>throw new NotSupportedException();
    }
}
