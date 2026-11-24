using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Gastronomia.Api;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);
ProductionHosting.Configure(builder);
var localStaff = builder.Configuration.GetSection("Development:LocalStaff").Get<LocalStaffOptions>() ?? new();
if (localStaff.Enabled && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("El acceso local del panel solo se permite en Development.");
if (localStaff.Enabled && (localStaff.TenantId == Guid.Empty || localStaff.UserId == Guid.Empty || string.IsNullOrWhiteSpace(localStaff.PasswordHash) || string.IsNullOrWhiteSpace(localStaff.Email))) throw new InvalidOperationException("Inicializar el panel local antes de ejecutarlo.");
builder.Services.Configure<LocalStaffOptions>(builder.Configuration.GetSection("Development:LocalStaff"));
var protection=builder.Services.AddDataProtection().SetApplicationName("Gastronomia.Panel").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath,"App_Data","keys")));
if(OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;});
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;
 o.AddPolicy("payment-webhook",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
 o.AddPolicy("otp-request",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(10),QueueLimit=0}));
 o.AddPolicy("otp-verify",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=30,Window=TimeSpan.FromMinutes(5),QueueLimit=0}));o.AddPolicy("checkout",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));o.AddPolicy("staff-login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=5,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TenantContext>();
builder.Services.AddDbContextFactory<GastronomiaDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer") ?? throw new InvalidOperationException("Configurar ConnectionStrings__SqlServer.")), ServiceLifetime.Scoped);
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IUserProductsService, UserProductsService>();
builder.Services.AddScoped<IPaymentReconciler, PaymentReconciler>();
builder.Services.AddScoped<IOnlinePaymentsService, OnlinePaymentsService>();
var paymentProvider = builder.Configuration["Payments:Provider"] ?? "Disabled";
if (paymentProvider == "DevelopmentHmac")
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("La pasarela simulada solo se permite en Development.");
    builder.Services.AddScoped<IPaymentGateway, DevelopmentHmacPaymentGateway>();
}
else if (paymentProvider == "MercadoPago")
{
    var mercadoPago = builder.Configuration.GetSection("MercadoPago").Get<MercadoPagoOptions>() ?? new();
    mercadoPago.Validate(); // Fail fast antes de arrancar, sin imprimir los secretos.
    builder.Services.AddSingleton(mercadoPago);
    builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>(client => client.Timeout = TimeSpan.FromSeconds(8))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        .RedactLoggedHeaders(new[] { "Authorization" });
}
else if (paymentProvider == "Disabled") builder.Services.AddScoped<IPaymentGateway, UnconfiguredPaymentGateway>();
else throw new InvalidOperationException("Registrar el adaptador del proveedor elegido antes de habilitar pagos online.");
if(localStaff.Enabled) {
 builder.Services.AddAuthentication(StaffSessionController.Scheme).AddCookie(StaffSessionController.Scheme,o=>{
  o.Cookie.Name="Gastronomia.LocalStaff";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
  o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
 });
} else {
 var authority=builder.Configuration["Authentication:Authority"]??throw new InvalidOperationException("Configurar Authentication__Authority, o inicializar el acceso local de desarrollo.");
 var audience=builder.Configuration["Authentication:Audience"]??throw new InvalidOperationException("Configurar Authentication__Audience.");
 builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{o.Authority=authority;o.Audience=audience;o.RequireHttpsMetadata=true;o.MapInboundClaims=false;o.TokenValidationParameters.RoleClaimType="role";});
}
builder.Services.AddAuthorization(o => o.AddPolicy("Staff", p => p.RequireAuthenticatedUser().RequireRole("staff")));
builder.Services.AddCustomerAccounts();
builder.Services.AddSingleton<OrderEvents>();
var emailProvider=builder.Configuration["Email:Provider"]??"Local";
if(emailProvider.Equals("Local",StringComparison.OrdinalIgnoreCase)) {if(!builder.Environment.IsDevelopment())throw new InvalidOperationException("El buzón local solo se permite en desarrollo. Configurar Email__Provider=Resend.");builder.Services.AddScoped<ITransactionalEmailSender,LocalMailboxEmailSender>();}
else if(emailProvider.Equals("Resend",StringComparison.OrdinalIgnoreCase)) builder.Services.AddHttpClient<ITransactionalEmailSender,ResendEmailSender>(client=>client.Timeout=TimeSpan.FromSeconds(30));
else throw new InvalidOperationException("Proveedor de email desconocido.");
builder.Services.AddCors(o=>o.AddPolicy("Expo",policy=>policy.WithOrigins(builder.Configuration.GetSection("Checkout:Origins").Get<string[]>()??new[]{"http://localhost:8081","http://127.0.0.1:8081","http://192.168.0.249:8081","http://localhost:8082","http://127.0.0.1:8082","http://192.168.0.249:8082","http://localhost:8091","http://127.0.0.1:8091"}).WithMethods("GET","POST","PUT","DELETE").AllowAnyHeader()));
builder.Services.AddHostedService<OutboxSimulationWorker>();
var app = builder.Build();
_ = app.Services.GetRequiredService<CustomerAuthKeys>(); // Fail fast on missing/invalid production secrets.
app.UseForwardedHeaders();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()!.Error;
    int status = exception switch { InvalidPaymentSignatureException => 401, PaymentGatewayUnavailableException => 503, InvalidOtpException => 401, DbUpdateConcurrencyException => 409, DomainException or ValidationException => 400, MissingException => 404, ConflictException => 409, BadHttpRequestException b => b.StatusCode, _ => 500 };
    if (status == 500) app.Logger.LogError(exception, "Error no controlado");
    await Results.Problem(statusCode: status, title: status == 500 ? "Error interno." : exception is DbUpdateConcurrencyException ? "Los datos cambiaron. Volvé a consultar antes de guardar." : exception.Message).ExecuteAsync(context);
}));
if(!app.Environment.IsDevelopment()) app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
app.Use(async(context,next)=>{
 context.Response.Headers["X-Content-Type-Options"]="nosniff";
 context.Response.Headers["Referrer-Policy"]="no-referrer";
 context.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
 await next();
});
app.UseStaticFiles();app.UseRouting();app.UseCors("Expo");app.UseRateLimiter();app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (Guid.TryParse(context.Request.RouteValues["tenantId"]?.ToString(), out var id))
    {
        if (id == Guid.Empty) { context.Response.StatusCode = 400; return; }
        context.RequestServices.GetRequiredService<TenantContext>().TenantId = id;
        if (context.User.Identity?.IsAuthenticated == true && (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var claimTenant) || claimTenant != id)) { context.Response.StatusCode = 403; return; }
    }
    if(context.User.Identity?.AuthenticationType==StaffSessionController.Scheme && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)) {
     try {await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);} catch(AntiforgeryValidationException) {context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new {title="La sesión de seguridad cambió. Recargá el panel."});return;}
    }
    await next();
});
app.UseAuthorization(); app.MapControllers(); app.MapFallbackToFile("/admin/{*path:nonfile}","admin/index.html"); app.Run();
