using Gastronomia.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Gastronomia.Infrastructure;

internal static class Mapping
{
    public static void TenantEntity<T>(EntityTypeBuilder<T> b, string table, bool compositePrimaryKey = false) where T : class, ITenantEntity
    {
        b.ToTable(table);
        if (compositePrimaryKey) b.HasKey(x => new { x.TenantId, x.Id });
        else { b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.TenantId, x.Id }); }
        b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Version).IsRowVersion();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b) { b.ToTable("Tenants", t => { t.HasCheckConstraint("CK_Tenants_WhatsApp", "[WhatsApp] NOT LIKE '%[^0-9]%' AND LEN([WhatsApp]) BETWEEN 10 AND 15"); t.HasCheckConstraint("CK_Tenants_Currency", "LEN([Moneda]) = 3"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); }
}
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b) { Mapping.TenantEntity(b, "Users", compositePrimaryKey: true); b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique(); }
}
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        Mapping.TenantEntity(b, "Products"); b.ToTable(t => { t.HasCheckConstraint("CK_Products_Price", "[Precio]>=0"); t.HasCheckConstraint("CK_Products_Stock", "[Stock]>=0"); }); b.HasIndex(x => new { x.TenantId, x.Activo });
    }
}
public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        Mapping.TenantEntity(b, "Promotions"); b.Property(x => x.HoraInicio).HasColumnType("time(0)"); b.Property(x => x.HoraFin).HasColumnType("time(0)");
        b.Property(x => x.CouponCode).UseCollation("Latin1_General_100_BIN2");
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Promotions_Enums", "[Tipo] BETWEEN 0 AND 3 AND [TipoDescuento] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Promotions_Value", "[Valor]>0 AND ([TipoDescuento]<>0 OR [Valor]<=100) AND [MinimoCompra]>=0");
            t.HasCheckConstraint("CK_Promotions_Dates", "[HastaUtc]>[DesdeUtc]");
            t.HasCheckConstraint("CK_Promotions_UserUses", "[MaxUsesPerUser] IS NULL OR [MaxUsesPerUser]>0");
            t.HasCheckConstraint("CK_Promotions_Uses", "[CurrentUses]>=0 AND ([MaxUses] IS NULL OR ([MaxUses]>0 AND [CurrentUses]<=[MaxUses]))");
            t.HasCheckConstraint("CK_Promotions_Schedule", "([DiasSemanaMask] IS NULL OR [DiasSemanaMask] BETWEEN 1 AND 127) AND (([HoraInicio] IS NULL AND [HoraFin] IS NULL) OR ([HoraInicio] IS NOT NULL AND [HoraFin] IS NOT NULL AND [HoraInicio]<>[HoraFin])) AND ([Tipo]<>1 OR [DiasSemanaMask] IS NOT NULL) AND ([Tipo]<>2 OR [HoraInicio] IS NOT NULL) AND ([Tipo]<>3 OR [CouponCode] IS NOT NULL)");
            t.HasCheckConstraint("CK_Promotions_Coupon", "[CouponCode] IS NULL OR (LEN([CouponCode])>0 AND [CouponCode]=UPPER(LTRIM(RTRIM([CouponCode]))))");
        });
        b.HasIndex(x => new { x.TenantId, x.CouponCode }).IsUnique().HasFilter("[CouponCode] IS NOT NULL"); b.HasIndex(x => new { x.TenantId, x.Activa, x.DesdeUtc, x.HastaUtc });
    }
}
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        Mapping.TenantEntity(b, "Orders");
        b.Property(x => x.ShortCode).HasSentinel("").HasDefaultValueSql("('#PED-' + CONVERT(varchar(20), NEXT VALUE FOR [dbo].[OrderCodeSequence]))");
        b.Property(x => x.RequestHash).IsFixedLength().IsUnicode(false);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => new { x.TenantId, x.UsuarioId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Promotion>().WithMany().HasForeignKey(x => new { x.TenantId, x.PromotionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.PromotionId, x.GuestEmailNormalized, x.EstadoPedido });
        b.HasIndex(x => new { x.TenantId, x.PromotionId, x.UsuarioId, x.EstadoPedido });
        b.HasIndex(x => x.ShortCode).IsUnique(); b.HasIndex(x => new { x.TenantId, x.RequestId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UsuarioId, x.CreadoUtc });
        b.HasIndex(x => new { x.TenantId, x.GuestEmailNormalized }).HasFilter("[UsuarioId] IS NULL"); b.HasIndex(x => new { x.TenantId, x.EstadoPedido, x.CreadoUtc });
        b.ToTable(t => { t.HasTrigger("TR_Orders_Snapshot"); t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_Orders_States", "[EstadoPedido] BETWEEN 0 AND 5 AND [EstadoPago] BETWEEN 0 AND 3"); t.HasCheckConstraint("CK_Orders_Totals", "[ImporteBruto]>=0 AND [DescuentoTotal]>=0 AND [DescuentoTotal]<=[ImporteBruto] AND [ImporteEnvio]>=0 AND [Total]=[ImporteBruto]-[DescuentoTotal]+[ImporteEnvio]"); t.HasCheckConstraint("CK_Orders_Delivery", "[TipoEntrega] IN (0,1) AND ([TipoEntrega]=0 OR ([DireccionEntrega] IS NOT NULL AND LEN(LTRIM(RTRIM([DireccionEntrega])))>0)) AND (([Latitud] IS NULL AND [Longitud] IS NULL) OR ([Latitud] IS NOT NULL AND [Longitud] IS NOT NULL AND [Latitud] BETWEEN -90 AND 90 AND [Longitud] BETWEEN -180 AND 180))"); t.HasCheckConstraint("CK_Orders_Code", "[ShortCode] LIKE '#PED-%'"); });
    }
}
public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        Mapping.TenantEntity(b, "OrderItems");
        b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.OrderId, x.ProductId }).IsUnique();
        b.ToTable(t => { t.HasTrigger("TR_OrderItems_Immutable"); t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_OrderItems_Amounts", "[Cantidad] BETWEEN 1 AND 1000 AND [PrecioUnitarioHistorico]>=0 AND [DescuentoAplicado]>=0 AND [DescuentoAplicado]<=[PrecioUnitarioHistorico]*[Cantidad] AND [Subtotal]=[PrecioUnitarioHistorico]*[Cantidad]-[DescuentoAplicado]"); });
    }
}
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        Mapping.TenantEntity(b, "Payments");
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConfirmadoPorUsuarioId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Proveedor).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.TransaccionIdExterna).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.PreferenceId).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => new { x.Proveedor, x.PreferenceId }).IsUnique().HasFilter("[PreferenceId] IS NOT NULL");
        b.HasIndex(x => new { x.Proveedor, x.TransaccionIdExterna }).IsUnique().HasFilter("[TransaccionIdExterna] IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.OrderId }).IsUnique().HasFilter("[Estado] = 2");
        b.ToTable(t => { t.HasCheckConstraint("CK_Payments_Amounts", "[Importe]>=0 AND [Metodo] BETWEEN 0 AND 2 AND [Estado] BETWEEN 0 AND 3"); t.HasCheckConstraint("CK_Payments_Receipt", "([ComprobanteUrl] IS NULL OR ([Metodo]=1 AND ([ComprobanteUrl] LIKE 'https://%' OR [ComprobanteUrl] LIKE '/api/tenants/%/staff/receipts/%'))) AND ([Estado]<>1 OR [ComprobanteUrl] IS NOT NULL)"); t.HasCheckConstraint("CK_Payments_External", "([TransaccionIdExterna] IS NULL AND [Proveedor] IS NULL AND [PreferenceId] IS NULL AND [CheckoutUrl] IS NULL) OR ([Metodo]=2 AND [Proveedor] IS NOT NULL AND ([TransaccionIdExterna] IS NOT NULL OR [PreferenceId] IS NOT NULL))"); t.HasCheckConstraint("CK_Payments_Preference", "([PreferenceId] IS NULL AND [CheckoutUrl] IS NULL) OR ([Metodo]=2 AND [PreferenceId] IS NOT NULL AND [CheckoutUrl] LIKE 'https://%')"); t.HasCheckConstraint("CK_Payments_Confirmed", "[Estado]<>2 OR ([ConfirmadoUtc] IS NOT NULL AND (([Metodo]=2 AND [TransaccionIdExterna] IS NOT NULL) OR ([Metodo]<>2 AND [ConfirmadoPorUsuarioId] IS NOT NULL)))"); });
    }
}
public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        Mapping.TenantEntity(b, "OutboxMessages"); b.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.OrderId, x.Tipo }).IsUnique(); b.HasIndex(x => new { x.TenantId, x.CreadoUtc }).HasFilter("[ProcesadoUtc] IS NULL");
    }
}
