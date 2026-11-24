using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class HmacOtpCodeHasher(byte[] key) : IOtpCodeHasher
{
    private readonly byte[] secret = key.Length >= 32 ? key.ToArray() : throw new ArgumentException("La clave OTP requiere al menos 32 bytes.", nameof(key));
    public byte[] Hash(Guid tenantId, string normalizedEmail, Guid messageId, string code) =>
        HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{tenantId:N}|{normalizedEmail}|{messageId:N}|{code}"));
}

public sealed class PasswordlessService(
    IDbContextFactory<GastronomiaDbContext> factory,
    TenantContext tenant,
    TimeProvider clock,
    IOtpCodeHasher hasher,
    IEmailService emailService,
    IAccessTokenIssuer tokenIssuer) : IPasswordlessService
{
    private static string ValidateEmail(string? email)
    {
        email = email?.Trim() ?? "";
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email) || string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email inválido.");
        return email;
    }

    // A fresh context on retry avoids reusing a consumed/tracked OTP. The locking
    // read serializes request/verify for the same tenant/email across processes.
    private async Task<T> InTransactionAsync<T>(Func<GastronomiaDbContext, Task<T>> action, CancellationToken ct)
    {
        if (tenant.TenantId == Guid.Empty) throw new DomainException("Comercio requerido.");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!await db.Tenants.AnyAsync(t => t.Id == tenant.TenantId && t.Activo, ct)) throw new MissingException("Comercio no disponible.");
                var result = await action(db);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception e) when (e is DbUpdateConcurrencyException || e is SqlException { Number: 1205 } || e is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
            {
                await transaction.RollbackAsync(ct);
                if (attempt == 2) throw new ConflictException("La solicitud cambió. Reintentá la operación.");
                await Task.Delay(50 * (attempt + 1), ct);
            }
        }
        throw new InvalidOperationException();
    }

    private Task<OtpChallenge?> LockChallengeAsync(GastronomiaDbContext db, string normalized, CancellationToken ct) =>
        db.OtpChallenges.FromSqlInterpolated($"SELECT * FROM [dbo].[OtpChallenges] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId]={tenant.TenantId} AND [NormalizedEmail]={normalized}").SingleOrDefaultAsync(ct);

    public async Task<RequestOtpResult> RequestOtpAsync(string email, CancellationToken ct = default)
    {
        email = ValidateEmail(email);
        string normalized = EmailKey.Normalize(email);
        var message = await InTransactionAsync<OtpEmail?>(async db =>
        {
            var challenge = await LockChallengeAsync(db, normalized, ct);
            var now = clock.GetUtcNow();
            if (challenge is not null && !challenge.PuedeEnviar(now)) return null;
            if (challenge is null) { challenge = new OtpChallenge(tenant.TenantId, email); db.OtpChallenges.Add(challenge); }
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            Guid messageId = Guid.NewGuid();
            challenge.Emitir(hasher.Hash(tenant.TenantId, normalized, messageId, code), messageId, now);
            return new OtpEmail(tenant.TenantId, messageId, email, code, challenge.ExpiraUtc);
        }, ct);
        // The response never discloses whether an account already exists. Codes
        // are sent only through the configured email adapter, never in JSON/logs.
        if (message is not null) await emailService.EnviarOtpAsync(message, ct);
        return new RequestOtpResult("Si la solicitud puede procesarse, recibirás un código de acceso. Revisá el correo y usá el código más reciente.");
    }

    public async Task<CustomerLoginResult> VerifyOtpAsync(string email, string codigo, CancellationToken ct = default)
    {
        email = ValidateEmail(email);
        string normalized = EmailKey.Normalize(email);
        // Return failure as data inside the transaction so failed attempts commit.
        var verified = await InTransactionAsync<(Usuario? User, bool NewUser, int Linked)>(async db =>
        {
            var challenge = await LockChallengeAsync(db, normalized, ct);
            var now = clock.GetUtcNow();
            if (challenge is null || !challenge.PuedeVerificar(now)) return (null, false, 0);
            bool validShape = codigo is { Length: 6 } && codigo.All(c => c is >= '0' and <= '9');
            var candidate = hasher.Hash(tenant.TenantId, normalized, challenge.MessageId, validShape ? codigo : "invalid");
            if (!validShape || !CryptographicOperations.FixedTimeEquals(candidate, challenge.CodeHash))
            {
                challenge.RegistrarFallo();
                return (null, false, 0);
            }
            challenge.Consumir(now);
            var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
            bool isNew = user is null;
            if (user is null) { user = new Usuario(tenant.TenantId, Guid.NewGuid(), email, true); db.Users.Add(user); }
            else user.VerificarEmail();
            await db.SaveChangesAsync(ct); // The FK must exist before linking guest orders.
            int linked = await db.Orders.Where(o => o.TenantId == tenant.TenantId && o.UsuarioId == null && o.GuestEmailNormalized == normalized)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.UsuarioId, (Guid?)user.Id), ct);
            return (user, isNew, linked);
        }, ct);
        if (verified.User is null) throw new InvalidOtpException();
        await using var profileDb = await factory.CreateDbContextAsync(ct);
        var addresses = await profileDb.Direcciones.AsNoTracking().Where(d => d.UsuarioId == verified.User.Id).OrderBy(d => d.Id)
            .Select(d => new DireccionResult(d.Id, d.Calle, d.Numero, d.PisoDepto, d.Latitud, d.Longitud)).ToListAsync(ct);
        var profile = new PerfilCliente(verified.User.Id, verified.User.Email, verified.User.Nombre, verified.User.Telefono, addresses);
        return new CustomerLoginResult(tokenIssuer.Issue(verified.User), profile, verified.NewUser, verified.Linked);
    }
}
