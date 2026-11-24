using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;

internal static class MercadoPagoChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var now = new DateTimeOffset(2026,11,12,12,0,0,TimeSpan.Zero);
        var tenantId=Guid.NewGuid();var orderId=Guid.NewGuid();var paymentId=Guid.NewGuid();
        MercadoPagoOptions Options() => new() {AccessToken="TEST-TOKEN-NO-REAL",WebhookSecret="secreto-exclusivo-de-tests",TenantId=tenantId,CollectorId="42",Sandbox=true,NotificationUrl="https://api.example.com/api/webhooks/pagos",SuccessUrl="https://shop.example.com/success",FailureUrl="https://shop.example.com/failure",PendingUrl="https://shop.example.com/pending"};
        async Task Fails<T>(Func<Task> action,string label) where T:Exception {try{await action();}catch(T){check(true,label);return;}throw new Exception("FAIL: "+label);}
        var missing=Options();missing.AccessToken="";
        await Fails<InvalidOperationException>(()=>{_ = new MercadoPagoGateway(new HttpClient(),missing,new FixedTime(now));return Task.CompletedTask;},"Mercado Pago falla al inicializar sin AccessToken");
        missing=Options();missing.WebhookSecret="";
        await Fails<InvalidOperationException>(()=>{_ = new MercadoPagoGateway(new HttpClient(),missing,new FixedTime(now));return Task.CompletedTask;},"Mercado Pago falla al inicializar sin WebhookSecret");
        missing=Options();missing.NotificationUrl="http://api.example.com/webhook";
        await Fails<InvalidOperationException>(()=>{_ = new MercadoPagoGateway(new HttpClient(),missing,new FixedTime(now));return Task.CompletedTask;},"Mercado Pago exige callback HTTPS");
        var handler=new MpHandler(tenantId,orderId,paymentId);using var http=new HttpClient(handler);
        var gateway=new MercadoPagoGateway(http,Options(),new FixedTime(now));
        var intent=new PaymentIntentRequest(tenantId,paymentId,orderId,100,"ARS",[new("burger","Burger",1,100)]);
        var result=await gateway.CreateAsync(intent,default);
        check(result.PreferenceId=="pref-1" && result.TransaccionId is null && result.CheckoutUrl!.Contains("sandbox"),"preferencia y transacción son identificadores separados");
        check(handler.LastKey==paymentId.ToString("D") && handler.LastBody.GetProperty("external_reference").GetString()==orderId.ToString("D"),"preferencia usa PaymentId idempotente y OrderId external_reference");
        check(handler.LastBody.GetProperty("metadata").GetProperty("tenant_id").GetString()==tenantId.ToString("D") && handler.LastBody.GetProperty("items")[0].GetProperty("unit_price").GetDecimal()==100 && handler.LastBody.GetProperty("notification_url").GetString()!.Contains("source_news=webhooks"),"preferencia envía ítems, metadata de tenant y URL de notificación");
        await Fails<DomainException>(()=>gateway.CreateAsync(intent with {TenantId=Guid.NewGuid()},default),"credencial de vendedor no se usa para otro tenant");
        await Fails<DomainException>(()=>gateway.CreateAsync(intent with {Importe=99},default),"preferencia rechaza total diferente de sus ítems");
        GatewayWebhookRequest Signed(string delivery="delivery-1",string? stamp=null,string? body=null)
        {
            stamp ??= now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var mac=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Options().WebhookSecret),Encoding.UTF8.GetBytes($"id:123;request-id:{delivery};ts:{stamp};")));
            return new(Encoding.UTF8.GetBytes(body??"{\"id\":\"untrusted\",\"status\":\"approved\",\"tenantId\":\"forged\"}"),new Dictionary<string,string>{{"x-signature",$"ts={stamp},v1={mac}"},{"x-request-id",delivery}},new Dictionary<string,string>{{"data.id","123"}});
        }
        handler.Calls=0;
        await Fails<InvalidPaymentSignatureException>(()=>gateway.VerifyAsync(new(ReadOnlyMemory<byte>.Empty,new Dictionary<string,string>(),new Dictionary<string,string>()),default),"webhook MP sin firma no alcanza HTTP");
        check(handler.Calls==0,"firma se verifica antes de consultar al proveedor");
        var forged=Signed() with {Headers=new Dictionary<string,string>{{"x-signature",$"ts={now.ToUnixTimeSeconds()},v1={new string('0',64)}"},{"x-request-id","delivery-1"}}};
        await Fails<InvalidPaymentSignatureException>(()=>gateway.VerifyAsync(forged,default),"webhook MP rechaza HMAC incorrecto");
        await Fails<InvalidPaymentSignatureException>(()=>gateway.VerifyAsync(Signed(stamp:now.AddMinutes(-10).ToUnixTimeSeconds().ToString()),default),"webhook MP rechaza firma antigua");
        var original=await gateway.VerifyAsync(Signed(),default);
        check(original.Estado=="rejected" && original.PaymentId==paymentId && original.TenantId==tenantId && original.PreferenceId=="pref-1","webhook usa API verificada y no estado/tenant del cuerpo");
        check(handler.Paths.TakeLast(3).SequenceEqual(new[]{"/v1/payments/123","/merchant_orders/987","/checkout/preferences/pref-1"}),"webhook verifica pago, merchant order y metadata de preferencia");
        var duplicate=await gateway.VerifyAsync(Signed("delivery-2",body:"{}"),default);
        check(duplicate.EventoId==original.EventoId&&duplicate.PayloadHash==original.PayloadHash,"entregas con otro request-id deduplican el mismo snapshot");
        var milliseconds=await gateway.VerifyAsync(Signed(stamp:now.ToUnixTimeMilliseconds().ToString()),default);
        check(milliseconds.EventoId==original.EventoId,"validación temporal admite ts en milisegundos sin cambiar el manifiesto");
        handler.Status="approved";
        check((await gateway.VerifyAsync(Signed(),default)).EventoId!=original.EventoId,"otro estado de la misma transacción conserva una identidad de evento distinta");
        handler.Collector="99";
        await Fails<PaymentGatewayUnavailableException>(()=>gateway.VerifyAsync(Signed(),default),"pago de otro vendedor no se concilia");handler.Collector="42";
        handler.MetaTenant=Guid.NewGuid();
        await Fails<PaymentGatewayUnavailableException>(()=>gateway.VerifyAsync(Signed(),default),"preferencia de otro tenant no se concilia");handler.MetaTenant=tenantId;
        handler.Live=true;
        await Fails<PaymentGatewayUnavailableException>(()=>gateway.VerifyAsync(Signed(),default),"pago real no se acepta en ambiente sandbox");handler.Live=false;
        handler.Fail=true;
        await Fails<PaymentGatewayUnavailableException>(()=>gateway.CreateAsync(intent,default),"error HTTP de Mercado Pago se propaga sin éxito simulado");
        // Tres unidades con descuento que no divide exactamente: sumatoria exacta a centavos.
        var product=new Product(tenantId,"Burger centavos",100,10);var item=new OrderItem(product,3,.01m);
        var order=new Order(tenantId,Guid.NewGuid(),new string('A',64),null,"test@example.com","1100000000",now,"ARS",[item],null);
        var lines=OnlinePaymentsService.CheckoutItems(order);
        check(lines.Sum(l=>l.Cantidad*l.PrecioUnitario)==299.99m&&lines.Sum(l=>l.Cantidad)==3,"preferencia conserva total histórico y centavos de descuento");
    }
    private sealed class MpHandler(Guid tenant,Guid orderId,Guid paymentId):HttpMessageHandler
    {
        public int Calls;public string Status="rejected",Collector="42";public Guid MetaTenant=tenant;public bool Live,Fail;public string? LastKey;public JsonElement LastBody;public List<string> Paths=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;Paths.Add(request.RequestUri!.AbsolutePath);
            if(Fail)return new(HttpStatusCode.ServiceUnavailable){Content=new StringContent("PRIVATE PROVIDER ERROR")};
            if(request.Method==HttpMethod.Post)
            {
                LastKey=request.Headers.GetValues("X-Idempotency-Key").Single();LastBody=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
                return Json(new {id="pref-1",collector_id=42,external_reference=orderId,metadata=new{tenant_id=MetaTenant,payment_id=paymentId},init_point="https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=pref-1",sandbox_init_point="https://sandbox.mercadopago.com.ar/checkout/v1/redirect?pref_id=pref-1"});
            }
            if(request.RequestUri.AbsolutePath=="/v1/payments/123")return Json(new {id=123,collector_id=Collector,external_reference=orderId,live_mode=Live,order=new{id=987,type="mercadopago"},status=Status,status_detail="test",transaction_amount=100,currency_id="ARS",date_last_updated="2026-11-12T12:00:00Z"});
            if(request.RequestUri.AbsolutePath=="/merchant_orders/987")return Json(new {id=987,collector=new{id=Collector},external_reference=orderId,preference_id="pref-1",payments=new[]{new{id=123}}});
            if(request.RequestUri.AbsolutePath=="/checkout/preferences/pref-1")return Json(new {id="pref-1",collector_id=Collector,external_reference=orderId,metadata=new{tenant_id=MetaTenant,order_id=orderId,payment_id=paymentId}});
            throw new Exception("HTTP inesperado: "+request.RequestUri.AbsolutePath);
        }
        private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};
    }
}


