using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Gastronomia.Infrastructure;

public sealed class DireccionConfiguration : IEntityTypeConfiguration<Direccion>
{
    public void Configure(EntityTypeBuilder<Direccion> b)
    {
        Mapping.TenantEntity(b, "Direcciones");
        b.HasOne<Usuario>().WithMany(u => u.Direcciones).HasForeignKey(d => new { d.TenantId, d.UsuarioId }).HasPrincipalKey(u => new { u.TenantId, u.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.TenantId, d.UsuarioId });
        b.ToTable(t => t.HasCheckConstraint("CK_Direcciones_Coordinates", "([Latitud] IS NULL AND [Longitud] IS NULL) OR ([Latitud] IS NOT NULL AND [Longitud] IS NOT NULL AND [Latitud] BETWEEN -90 AND 90 AND [Longitud] BETWEEN -180 AND 180)"));
    }
}
public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        Mapping.TenantEntity(b, "OtpChallenges");
        b.Property(o => o.CodeHash).HasColumnType("binary(32)");
        b.HasIndex(o => new { o.TenantId, o.NormalizedEmail }).IsUnique();
        b.HasIndex(o => o.ExpiraUtc);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_OtpChallenges_Attempts", "[IntentosFallidos] BETWEEN 0 AND 5 AND [EnviosEnVentana] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_OtpChallenges_Expiration", "[ExpiraUtc]>[CreadoUtc]");
        });
    }
}
