using Gastronomia.Application;
using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
namespace Gastronomia.Infrastructure;

public sealed class GastronomiaDbContext(DbContextOptions<GastronomiaDbContext> options, TenantContext tenant) : DbContext(options)
{
    public Guid CurrentTenantId => tenant.TenantId;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Usuario> Users => Set<Usuario>();
    public DbSet<Direccion> Direcciones => Set<Direccion>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UsuarioFavorito> UsuarioFavoritos => Set<UsuarioFavorito>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void ConfigureConventions(ModelConfigurationBuilder b) => b.Properties<decimal>().HavePrecision(18, 2);
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("dbo"); b.HasSequence<long>("OrderCodeSequence").StartsAt(1000);
        b.ApplyConfigurationsFromAssembly(typeof(GastronomiaDbContext).Assembly);
        b.Entity<Usuario>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Direccion>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<OtpChallenge>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<UsuarioFavorito>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<PaymentLog>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Product>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Promotion>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Order>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<OrderItem>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Payment>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<OutboxMessage>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
    }
    private void Guard()
    {
        ChangeTracker.DetectChanges();
        foreach (var e in ChangeTracker.Entries<ITenantEntity>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (CurrentTenantId == Guid.Empty || e.Entity.TenantId != CurrentTenantId || (e.State != EntityState.Added && (Guid)e.OriginalValues[nameof(ITenantEntity.TenantId)]! != CurrentTenantId)) throw new DomainException("Operación fuera del comercio actual.");
            if (e.Entity is PaymentLog && e.State != EntityState.Added) throw new DomainException("La auditoría de pagos es inmutable.");
            if (e.Entity is OrderItem && e.State != EntityState.Added) throw new DomainException("El detalle histórico es inmutable.");
            if (e.Entity is Order && e.State == EntityState.Modified) foreach (var name in new[] { "ImporteBruto", "DescuentoTotal", "Total", "Moneda", "ShortCode", "GuestEmail", "GuestEmailNormalized", "GuestPhone", "PromotionId", "PromocionHistorica", "CreadoUtc", "RequestId", "RequestHash", "CheckoutTokenHash", "NombreCliente", "TipoEntrega", "DireccionEntrega", "Latitud", "Longitud", "Notas", "ImporteEnvio" }) if (e.Property(name).IsModified) throw new DomainException("El snapshot del pedido es inmutable.");
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { Guard(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { Guard(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
