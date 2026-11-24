using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Gastronomia.Infrastructure;

public sealed class UsuarioFavoritoConfiguration : IEntityTypeConfiguration<UsuarioFavorito>
{
    public void Configure(EntityTypeBuilder<UsuarioFavorito> b)
    {
        Mapping.TenantEntity(b, "UsuarioFavoritos");
        b.HasIndex(x => new { x.TenantId, x.UsuarioId, x.ProductoId }).IsUnique();
        b.HasOne(x => x.Usuario).WithMany(x => x.Favoritos).HasForeignKey(x => new { x.TenantId, x.UsuarioId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany(x => x.Favoritos).HasForeignKey(x => new { x.TenantId, x.ProductoId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
{
    public void Configure(EntityTypeBuilder<PaymentLog> b)
    {
        Mapping.TenantEntity(b, "PaymentLogs");
        b.Property(x => x.Proveedor).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.EventoId).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.PayloadHash).IsUnicode(false).IsFixedLength();
        b.HasIndex(x => new { x.Proveedor, x.EventoId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.PaymentId, x.RecibidoUtc });
        b.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.TenantId, x.PaymentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasTrigger("TR_PaymentLogs_Immutable"); t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_PaymentLogs_Amounts", "[Importe]>=0 AND LEN([Moneda])=3"); });
    }
}
