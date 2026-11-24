using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Gastronomia.Application;
using Gastronomia.Domain;
using Gastronomia.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
namespace Gastronomia.Api;

public sealed class CustomerAuthKeys
{
    public byte[] JwtKey { get; }
    public byte[] OtpKey { get; }
    public string Issuer { get; }
    public string Audience { get; }
    public CustomerAuthKeys(IConfiguration config, IWebHostEnvironment env, IDataProtectionProvider protection)
    {
        Issuer = config["CustomerAuth:Issuer"] ?? "Gastronomia.Customer";
        Audience = config["CustomerAuth:Audience"] ?? "Gastronomia.Client";
        string? jwt = config["CustomerAuth:JwtKey"], otp = config["CustomerAuth:OtpKey"];
        if (jwt is null && otp is null && env.IsDevelopment())
        {
            var protector = protection.CreateProtector("Gastronomia.CustomerAuth.Keys.v1");
            string path = Path.Combine(env.ContentRootPath, "App_Data", "customer-auth.keys");
            if (File.Exists(path))
            {
                var stored = JsonSerializer.Deserialize<StoredKeys>(protector.Unprotect(File.ReadAllText(path))) ?? throw new InvalidOperationException("Claves locales inválidas.");
                jwt = stored.Jwt; otp = stored.Otp;
            }
            else
            {
                jwt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); otp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, protector.Protect(JsonSerializer.Serialize(new StoredKeys(jwt, otp))));
                    try { File.Move(temporary, path, false); }
                    catch (IOException) when (File.Exists(path))
                    {
                        var stored = JsonSerializer.Deserialize<StoredKeys>(protector.Unprotect(File.ReadAllText(path)))!;
                        jwt = stored.Jwt; otp = stored.Otp;
                    }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        JwtKey = Decode(jwt, "CustomerAuth:JwtKey"); OtpKey = Decode(otp, "CustomerAuth:OtpKey");
        if (CryptographicOperations.FixedTimeEquals(JwtKey, OtpKey)) throw new InvalidOperationException("Las claves JWT y OTP deben ser diferentes.");
    }
    private static byte[] Decode(string? value, string name)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); return bytes.Length >= 32 ? bytes : throw new FormatException(); }
        catch (FormatException) { throw new InvalidOperationException($"Configurar {name} como clave Base64 de al menos 32 bytes."); }
    }
    private sealed record StoredKeys(string Jwt, string Otp);
}

public sealed class CustomerJwtIssuer(CustomerAuthKeys keys, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessTokenResult Issue(Usuario user)
    {
        if (!user.EmailVerificado) throw new InvalidOperationException("No emitir acceso sin email verificado.");
        var now = clock.GetUtcNow(); var expires = now.AddHours(1);
        Claim[] claims = [new("sub", user.Id.ToString()), new("tenant_id", user.TenantId.ToString()), new("email", user.Email), new("email_verified", "true"), new("role", "customer"), new("jti", Guid.NewGuid().ToString("N"))];
        var token = new JwtSecurityToken(keys.Issuer, keys.Audience, claims, now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(keys.JwtKey), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires);
    }
}

public sealed class CustomerEmailService(ITransactionalEmailSender sender, TimeProvider clock) : IEmailService
{
    public async Task EnviarOtpAsync(OtpEmail email, CancellationToken ct)
    {
        string text = $"Tu código para ingresar a Burger Club es: {email.Codigo}\nVence en 10 minutos y solo puede utilizarse una vez. No lo compartas.\nSi no solicitaste este acceso, podés ignorar el correo.";
        await sender.SendAsync(new OutgoingEmail(email.TenantId, email.MessageId, Guid.Empty, "Acceso a tu cuenta", email.Email, "Tu código de acceso a Burger Club", text, clock.GetUtcNow()), ct);
    }
}

public static class CustomerAuthentication
{
    public const string Scheme = "CustomerJwt";
    public const string Policy = "Customer";
    public static IServiceCollection AddCustomerAccounts(this IServiceCollection services)
    {
        services.AddSingleton<CustomerAuthKeys>();
        services.AddSingleton<IOtpCodeHasher>(sp => new HmacOtpCodeHasher(sp.GetRequiredService<CustomerAuthKeys>().OtpKey));
        services.AddScoped<IAccessTokenIssuer, CustomerJwtIssuer>();
        services.AddScoped<IEmailService, CustomerEmailService>();
        services.AddScoped<IPasswordlessService, PasswordlessService>();
        services.AddScoped<ICustomerAccountService, CustomerAccountService>();
        services.AddScoped<ICustomerOrdersService, CustomerOrdersService>();
        services.AddAuthentication().AddJwtBearer(Scheme, _ => { });
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<CustomerAuthKeys>((options, keys) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = keys.Issuer,
                ValidateAudience = true, ValidAudience = keys.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(keys.JwtKey),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = "role", NameClaimType = "email"
            };
        });
        services.AddAuthorization(o => o.AddPolicy(Policy, p => p.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireRole("customer").RequireClaim("email_verified", "true")));
        return services;
    }
    public static Guid Actor(HttpContext http, TenantContext scope)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId) || userId == Guid.Empty || !Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId) || tenantId == Guid.Empty)
            throw new BadHttpRequestException("Identidad de cliente inválida.");
        scope.TenantId = tenantId;
        return userId;
    }
}
