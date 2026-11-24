using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Promotion : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, MaxLength(120)] public string Nombre { get; private set; } = "";
    public TipoPromocion Tipo { get; private set; }
    public TipoDescuento TipoDescuento { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Valor { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MinimoCompra { get; private set; }
    [MaxLength(40)] public string? CouponCode { get; private set; }
    public DateTimeOffset DesdeUtc { get; private set; }
    public DateTimeOffset HastaUtc { get; private set; }
    // Bit 0=domingo, bit 6=sábado; null=sin restricción adicional.
    public int? DiasSemanaMask { get; private set; }
    public TimeOnly? HoraInicio { get; private set; }
    public TimeOnly? HoraFin { get; private set; }
    public int? MaxUses { get; private set; }
    public int? MaxUsesPerUser { get; private set; }
    public int CurrentUses { get; private set; }
    public bool Activa { get; private set; } = true;
    private Promotion() { }
    public Promotion(Guid tenantId, string nombre, TipoPromocion tipo, TipoDescuento descuento, decimal valor, DateTimeOffset desde, DateTimeOffset hasta, int? maxUses = null, string? coupon = null, int? dias = null, TimeOnly? inicio = null, TimeOnly? fin = null, decimal minimo = 0, int? maxUsesPerUser = null)
    {
        if (maxUsesPerUser is <= 0) throw new DomainException("Límite por usuario inválido.");
        MaxUsesPerUser = maxUsesPerUser;
        Money.Validate(valor); Money.Validate(minimo);
        if (valor <= 0 || (descuento == TipoDescuento.Porcentaje && valor > 100) || hasta <= desde || maxUses is <= 0 || dias is < 1 or > 127 || (inicio.HasValue != fin.HasValue) || (inicio.HasValue && inicio == fin) || (tipo == TipoPromocion.PorDiaSemana && dias is null) || (tipo == TipoPromocion.PorHorario && inicio is null) || (tipo == TipoPromocion.Cupon && string.IsNullOrWhiteSpace(coupon))) throw new DomainException("Promoción inválida.");
        TenantId = tenantId; Nombre = nombre; Tipo = tipo; TipoDescuento = descuento; Valor = valor; DesdeUtc = desde; HastaUtc = hasta; MaxUses = maxUses; CouponCode = string.IsNullOrWhiteSpace(coupon) ? null : coupon.Trim().ToUpperInvariant(); DiasSemanaMask = dias; HoraInicio = inicio; HoraFin = fin; MinimoCompra = minimo;
    }
    public bool EsAplicable(DateTimeOffset utc, TimeZoneInfo zone, decimal bruto)
    {
        if (!Activa || utc < DesdeUtc || utc >= HastaUtc || bruto < MinimoCompra || (MaxUses.HasValue && CurrentUses >= MaxUses)) return false;
        var local = TimeZoneInfo.ConvertTime(utc, zone); var hora = TimeOnly.FromDateTime(local.DateTime); var dia = local.DayOfWeek;
        if (HoraInicio.HasValue && HoraFin.HasValue)
        {
            bool overnight = HoraInicio > HoraFin;
            if (!(overnight ? hora >= HoraInicio || hora < HoraFin : hora >= HoraInicio && hora < HoraFin)) return false;
            // La madrugada pertenece al día de inicio de la promoción nocturna.
            if (overnight && hora < HoraFin) dia = local.AddDays(-1).DayOfWeek;
        }
        return DiasSemanaMask is null || (DiasSemanaMask.Value & (1 << (int)dia)) != 0;
    }
    public decimal Beneficio(decimal bruto) => Math.Min(bruto, Money.Round(TipoDescuento == TipoDescuento.Porcentaje ? bruto * Valor / 100m : Valor));
    public void Consumir() { if (MaxUses.HasValue && CurrentUses >= MaxUses) throw new DomainException("La promoción agotó sus usos."); CurrentUses = checked(CurrentUses + 1); }
    public void Liberar() { if (CurrentUses <= 0) throw new DomainException("Contador de promoción inconsistente."); CurrentUses--; }
}
