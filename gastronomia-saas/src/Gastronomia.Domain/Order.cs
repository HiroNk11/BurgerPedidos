using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Gastronomia.Domain;

public sealed class Order : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    [Required, MaxLength(32)] public string ShortCode { get; private set; } = "";
    [MaxLength(64)] public string? CheckoutTokenHash {get;private set;}
    [MaxLength(100)] public string NombreCliente {get;private set;} = "Invitado";
    public TipoEntrega TipoEntrega {get;private set;}
    [MaxLength(400)] public string? DireccionEntrega {get;private set;}
    public double? Latitud {get;private set;}
    public double? Longitud {get;private set;}
    [MaxLength(500)] public string? Notas {get;private set;}
    [Column(TypeName="decimal(18,2)")] public decimal ImporteEnvio {get;private set;}
    public Guid RequestId { get; private set; }
    [Required, MaxLength(64)] public string RequestHash { get; private set; } = "";
    public Guid? UsuarioId { get; private set; }
    [Required, EmailAddress, MaxLength(254)] public string GuestEmail { get; private set; } = "";
    [Required, MaxLength(254)] public string GuestEmailNormalized { get; private set; } = "";
    [Required, MaxLength(25)] public string GuestPhone { get; private set; } = "";
    public DateTimeOffset CreadoUtc { get; private set; }
    public EstadoPedido EstadoPedido { get; private set; } = EstadoPedido.PendienteConfirmacion;
    public EstadoPago EstadoPago { get; private set; } = EstadoPago.Pendiente;
    [Column(TypeName = "decimal(18,2)")] public decimal ImporteBruto { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DescuentoTotal { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Total { get; private set; }
    [Required, MaxLength(3)] public string Moneda { get; private set; } = "ARS";
    public Guid? PromotionId { get; private set; }
    [MaxLength(120)] public string? PromocionHistorica { get; private set; }
    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items;
    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();
    private Order() { }
    public Order(Guid tenantId, Guid requestId, string hash, Guid? usuarioId, string email, string phone, DateTimeOffset now, string moneda, IEnumerable<OrderItem> items, Promotion? promo, decimal envio=0,string nombreCliente="Invitado",TipoEntrega tipoEntrega=TipoEntrega.Retiro,string? direccion=null,double? latitud=null,double? longitud=null,string? notas=null,string? tokenHash=null)
    {
        Money.Validate(envio);ImporteEnvio=envio;NombreCliente=nombreCliente;TipoEntrega=tipoEntrega;DireccionEntrega=direccion;Latitud=latitud;Longitud=longitud;Notas=notas;CheckoutTokenHash=tokenHash;
        TenantId = tenantId; RequestId = requestId; RequestHash = hash; UsuarioId = usuarioId; GuestEmail = email.Trim(); GuestEmailNormalized = EmailKey.Normalize(email); GuestPhone = phone; CreadoUtc = now; Moneda = moneda;
        _items.AddRange(items); if (_items.Count == 0) throw new DomainException("El pedido debe contener productos.");
        foreach (var i in _items) i.Attach(Id);
        ImporteBruto = _items.Sum(i => i.PrecioUnitarioHistorico * i.Cantidad); DescuentoTotal = _items.Sum(i => i.DescuentoAplicado); Total = _items.Sum(i => i.Subtotal)+envio; Money.Validate(ImporteBruto); Money.Validate(Total);
        PromotionId = promo?.Id; PromocionHistorica = promo?.Nombre;
    }
    public void Aceptar() { if (EstadoPedido != EstadoPedido.PendienteConfirmacion) throw new DomainException("Solo puede aceptarse un pedido pendiente."); EstadoPedido = EstadoPedido.Aceptado; }
    public void CambiarEstado(EstadoPedido nuevo)
    {
        bool valido = (EstadoPedido, nuevo) is (EstadoPedido.Aceptado, EstadoPedido.EnPreparacion) or (EstadoPedido.EnPreparacion, EstadoPedido.EnCamino) or (EstadoPedido.EnCamino, EstadoPedido.Entregado) or (EstadoPedido.EnPreparacion, EstadoPedido.Entregado);
        if (!valido) throw new DomainException("Transición de pedido inválida.");
        EstadoPedido = nuevo;
    }
    public void Cancelar() { if (EstadoPedido is EstadoPedido.Cancelado or EstadoPedido.Entregado) throw new DomainException("El pedido no puede cancelarse."); if (EstadoPago == EstadoPago.Pagado) throw new DomainException("Un pedido pagado requiere un flujo explícito de reembolso."); EstadoPedido = EstadoPedido.Cancelado; }
    public void RegistrarPago(EstadoPago estado) { if (!Enum.IsDefined(estado)) throw new DomainException("Estado de pago inválido."); if (EstadoPedido == EstadoPedido.Cancelado) throw new DomainException("Pedido cancelado."); if (EstadoPago == EstadoPago.Pagado && estado != EstadoPago.Pagado) throw new DomainException("Un pago confirmado requiere un flujo explícito de reembolso."); EstadoPago = estado; }
}
public sealed class OrderItem : ITenantEntity
{
    [Key] public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    [Timestamp] public byte[] Version { get; private set; } = [];
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    [Required, MaxLength(160)] public string NombreProductoHistorico { get; private set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal PrecioUnitarioHistorico { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DescuentoAplicado { get; private set; }
    [Range(1, 1000)] public int Cantidad { get; private set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Subtotal { get; private set; }
    private OrderItem() { }
    public OrderItem(Product product, int cantidad, decimal descuento)
    {
        if (cantidad is < 1 or > 1000) throw new DomainException("Cantidad inválida."); Money.Validate(descuento);
        var bruto = product.Precio * cantidad; Money.Validate(bruto); if (descuento > bruto) throw new DomainException("Descuento inválido.");
        TenantId = product.TenantId; ProductId = product.Id; NombreProductoHistorico = product.Nombre; PrecioUnitarioHistorico = product.Precio; Cantidad = cantidad; DescuentoAplicado = descuento; Subtotal = bruto - descuento;
    }
    internal void Attach(Guid orderId) => OrderId = orderId;
}
