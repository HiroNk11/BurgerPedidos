using System.Security.Cryptography;
using System.Text;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal static class PaymentPreferencesChecks
{
    public static async Task RunAsync(IDbContextFactory<GastronomiaDbContext> factory,TenantContext scope,Action<bool,string> check)
    {
        var tenant=new Tenant("Checkout Pro SQL","5491100000000");scope.TenantId=tenant.Id;
        var user=new Usuario(tenant.Id,Guid.NewGuid(),"checkout.pro@example.com",true);var product=new Product(tenant.Id,"Pago MP",100,10);
        await using(var db=await factory.CreateDbContextAsync()){db.AddRange(tenant,user,product);await db.SaveChangesAsync();}
        var orders=new OrderService(factory,scope,TimeProvider.System);
        var order=await orders.CrearPedidoAsync(new(Guid.NewGuid(),user.Email,"1100000000",MetodoPago.PasarelaOnline,[new(product.Id,1)]),user.Id);
        var gateway=new PreferenceGateway();var service=new OnlinePaymentsService(factory,gateway);
        var starts=await Task.WhenAll(service.StartAsync(user.Id,order.Id),service.StartAsync(user.Id,order.Id));
        check(gateway.Calls==1&&starts[0]==starts[1],"inicios concurrentes comparten una sola preferencia guardada");
        await using(var db=await factory.CreateDbContextAsync()){var payment=await db.Payments.SingleAsync(p=>p.Id==order.PaymentId);check(payment.PreferenceId is not null&&payment.TransaccionIdExterna is null&&payment.Estado==EstadoPago.Pendiente,"guardar preferencia no confirma ni inventa una transacción");}
        var reconciler=new PaymentReconciler(factory,scope,TimeProvider.System);var prefix=Guid.NewGuid().ToString("N");
        VerifiedPaymentEvent Event(string tx,string status,string suffix) => new(tenant.Id,"MercadoPago",prefix+suffix,tx,status,100,"ARS",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(suffix))),order.PaymentId,order.Id,starts[0].PreferenceId);
        check((await reconciler.ReconcileAsync(Event("101","pending","pending"))).Resultado=="PagoEnProceso","pending se audita sin confirmar el pago");
        try{await reconciler.ReconcileAsync(Event("101","approved","wrong") with {PreferenceId="otra"});throw new Exception("FAIL: preferencia ajena");}catch(DomainException){check(true,"conciliación exige la preferencia persistida del intento");}
        await reconciler.ReconcileAsync(Event("101","rejected","rejected"));
        await reconciler.ReconcileAsync(Event("102","approved","approved"));
        await using(var db=await factory.CreateDbContextAsync()){var payment=await db.Payments.SingleAsync(p=>p.Id==order.PaymentId);check(payment.Estado==EstadoPago.Pagado&&payment.TransaccionIdExterna=="102","nuevo intento aprobado puede seguir a otro ID rechazado");}
        check((await reconciler.ReconcileAsync(Event("103","approved","double"))).Resultado=="RequiereReembolsoDuplicado","segundo cobro aprobado distinto genera auditoría de reembolso duplicado");
        check((await reconciler.ReconcileAsync(Event("102","refunded","refund"))).Resultado=="RequiereConciliacionManual","reembolso remoto requiere conciliación explícita sin borrar el historial");
        await using(var db=await factory.CreateDbContextAsync())
        {
            var logs=await db.PaymentLogs.Where(l=>l.PaymentId==order.PaymentId).ToListAsync();
            check(logs.Any(l=>l.TransaccionIdExterna=="101")&&logs.Any(l=>l.TransaccionIdExterna=="102")&&logs.Any(l=>l.TransaccionIdExterna=="103"),"auditoría conserva cada transacción real aunque cambie el intento ganador");
            check(await db.OutboxMessages.CountAsync(m=>m.OrderId==order.Id&&m.Tipo=="PagoConfirmado")==1,"varios eventos de checkout generan una única notificación de pago confirmado");
        }
    }
    private sealed class PreferenceGateway:IPaymentGateway
    {
        public int Calls;
        public Task<VerifiedPaymentEvent> VerifyAsync(GatewayWebhookRequest r,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<PaymentIntentResult> CreateAsync(PaymentIntentRequest r,CancellationToken ct){Interlocked.Increment(ref Calls);await Task.Delay(50,ct);return new("MercadoPago",null,"pref-"+r.PaymentId.ToString("N"),"https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=test");}
    }
}
